using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using FootballBoard.Domain;
using UnityEngine;

namespace FootballBoard.Presentation
{
    /// <summary>
    /// A compact, human-readable record of a match: every submitted plan and every resolved event,
    /// one line each. Goes to the Unity console (prefix "[Match]") and to match_log.txt in
    /// Application.persistentDataPath (the previous match is kept as match_log_prev.txt).
    /// </summary>
    public static class MatchLog
    {
        private static string _path;
        public static string FilePath => _path;

        public static void Begin(MatchState s)
        {
            try
            {
                var dir = Application.persistentDataPath;
                _path = Path.Combine(dir, "match_log.txt");
                var prev = Path.Combine(dir, "match_log_prev.txt");
                if (File.Exists(_path)) File.Copy(_path, prev, true);
                File.WriteAllText(_path, "");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Match] log file unavailable: " + e.Message);
                _path = null;
            }
            Write($"=== match {DateTime.Now:yyyy-MM-dd HH:mm} · {s.Mode} · human {s.HumanTeam} · {s.DurationSeconds / 60} min · board {s.Pitch.Columns}x{s.Pitch.Rows}");
            Write("    players: " + string.Join(" ", s.Players.Select(p => $"{P(p)}@{C(p.Position)}")));
        }

        public static void Plan(MatchState s, Team team, TeamPlan plan, string who)
        {
            var moves = plan.Moves.Count == 0 ? "no moves"
                : string.Join(", ", plan.Moves.Select(m => $"{P(s.GetPlayer(m.PlayerId))}{C(s.GetPlayer(m.PlayerId).Position)}->{C(m.Target)}{(m.Tackle ? " TACKLE" : "")}"));
            var action = plan.Action == null ? "" : " | " + Action(s, plan.Action);
            Write($"T{s.TurnNumber} {s.ElapsedSeconds / 60:00}:{s.ElapsedSeconds % 60:00} {team} plan ({who}): {moves}{action}");
        }

        public static void Events(MatchState s, IReadOnlyList<GameEvent> events)
        {
            var moves = new List<string>();
            void FlushMoves()
            {
                if (moves.Count == 0) return;
                Write("    moved: " + string.Join(", ", moves));
                moves.Clear();
            }

            foreach (var e in events)
            {
                if (e is PlayerMoved m) { moves.Add($"{P(s.GetPlayer(m.PlayerId))}{C(m.From)}->{C(m.To)}"); continue; }
                FlushMoves();
                var line = Describe(s, e);
                if (line != null) Write("    " + line);
            }
            FlushMoves();
            Write($"    ball: {(s.Ball.OwnerId.HasValue ? P(s.GetPlayer(s.Ball.OwnerId.Value)) : "loose " + C(s.Ball.LoosePosition))} · score {s.Score(Team.Red)}-{s.Score(Team.Blue)}");
        }

        public static void Note(string text) => Write("    " + text);

        private static string Describe(MatchState s, GameEvent e)
        {
            switch (e)
            {
                case ClockAdvanced _:
                case StepStarted _:
                    return null;
                case TurnStarted t: return $"turn {t.TurnNumber}: {t.Team}";
                case CoinTossed c: return $"kick-off {c.KickoffTeam}{(c.WasRandom ? " (coin toss)" : "")}";
                case PlanLocked l: return $"{l.Team} plan locked, {l.NextPlanningTeam} to plan";
                case MoveCancelled mc: return $"move cancelled {P(s.GetPlayer(mc.PlayerId))}->{C(mc.Target)} (square full / relocated)";
                case LooseBallClaimed lb:
                    return $"loose ball -> {P(s.GetPlayer(lb.PlayerId))}" + (lb.Contenders.Count > 1 ? $" (50/50 vs {string.Join(" ", lb.Contenders.Select(id => P(s.GetPlayer(id))))})" : "");
                case TackleResolved tk: return $"tackle {P(s.GetPlayer(tk.TacklerId))} on {P(s.GetPlayer(tk.CarrierId))}: {(tk.Success ? "WON" : "failed")}";
                case ActionCancelled ac: return $"action cancelled {P(s.GetPlayer(ac.CarrierId))} {ac.Option}: {ac.Reason}";
                case PassResolved pr:
                    var to = pr.ReceiverId.HasValue ? P(s.GetPlayer(pr.ReceiverId.Value)) : pr.TargetCell.HasValue ? C(pr.TargetCell.Value) : "?";
                    var owner = pr.NewOwnerId.HasValue ? P(s.GetPlayer(pr.NewOwnerId.Value)) : "nobody";
                    return $"{pr.Kind} pass {P(s.GetPlayer(pr.PasserId))} -> {to}: {pr.Outcome}, ball at {C(pr.BallCell)}, owner {owner}{(pr.Contested ? " (contested)" : "")}";
                case ShotResolved sr:
                    var keeper = s.Goalkeeper(s.GetPlayer(sr.ShooterId).Team.Opponent());
                    return $"shot {P(s.GetPlayer(sr.ShooterId))} at {C(sr.Target)}: {sr.Outcome.ToString().ToUpperInvariant()}"
                           + $" (on target {sr.Accuracy:P0}, beats keeper {sr.GoalChance:P0}; keeper {P(keeper)} now {C(keeper.Position)}"
                           + (sr.DefenderId.HasValue ? $", involved {P(s.GetPlayer(sr.DefenderId.Value))}" : "") + ")";
                case GoalScored g: return $"GOAL {g.ScoringTeam} {g.RedScore}-{g.BlueScore}";
                case RestartAwarded r: return $"{r.Kind} {r.Team} by {P(s.GetPlayer(r.PlayerId))} at {C(r.Cell)}{(r.TieBrokenByCoin ? " (coin)" : "")}";
                case TieDecisionRequired _: return "draw at full time: tie decision";
                case GoldenGoalStarted _: return "golden goal started";
                case MatchEnded me: return $"=== full time {me.Result.RedScore}-{me.Result.BlueScore}{(me.Result.DecidedByGoldenGoal ? " (golden goal)" : "")}";
                default: return e.GetType().Name;
            }
        }

        private static string Action(MatchState s, CarrierAction a)
        {
            var carrier = s.BallCarrier;
            var who = carrier != null ? P(carrier) + C(carrier.Position) : "?";
            switch (a.Option)
            {
                case CarrierOption.Shot: return $"{who} SHOOTS at {(a.TargetCell.HasValue ? C(a.TargetCell.Value) : "default")}";
                case CarrierOption.StraightPass: return $"{who} PASS -> {(a.ReceiverId.HasValue ? P(s.GetPlayer(a.ReceiverId.Value)) : "?")}";
                case CarrierOption.LongPass:
                    return $"{who} LONG PASS -> " + (a.ReceiverId.HasValue ? P(s.GetPlayer(a.ReceiverId.Value)) : a.TargetCell.HasValue ? C(a.TargetCell.Value) : "?");
                default: return $"{who} {a.Option}";
            }
        }

        private static string P(Player p) => p == null ? "?" : $"{(p.Team == Team.Red ? "R" : "B")}{p.Number}{(p.IsGoalkeeper ? "gk" : "")}";
        private static string C(Cell c) => $"({c.X},{c.Y})";

        private static void Write(string line)
        {
            Debug.Log("[Match] " + line);
            if (_path == null) return;
            try { File.AppendAllText(_path, line + "\n", Encoding.UTF8); }
            catch { _path = null; }
        }
    }
}
