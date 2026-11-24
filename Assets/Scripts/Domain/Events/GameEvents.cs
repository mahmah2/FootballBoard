using System.Collections.Generic;

namespace FootballBoard.Domain
{
    /// <summary>
    /// Something that happened while a command resolved, in order. The presentation layer plays
    /// these back; outcomes are already decided. <see cref="IsRandomized"/> marks results that
    /// should get the slow-motion randomization sequence.
    /// </summary>
    public abstract class GameEvent
    {
        public virtual bool IsRandomized => false;
    }

    public enum StepKind { Arrangement, Action }

    public enum RestartKind { Kickoff, GoalKick, ThrowIn }

    public enum PassKind { Straight, Long }

    public enum PassOutcome
    {
        /// <summary>Receiver got the ball.</summary>
        Completed,
        /// <summary>An opponent shared the receiver's square and won the 50/50.</summary>
        Intercepted,
        /// <summary>Long pass missed and landed on an opponent.</summary>
        LandedOnOpponent,
        /// <summary>Long pass missed and landed on another teammate.</summary>
        LandedOnTeammate,
        /// <summary>Long pass missed and landed on an empty cell; the ball is loose.</summary>
        LooseBall,
        /// <summary>Long pass missed and left the pitch; a restart follows.</summary>
        OutOfPlay,
        /// <summary>Accurate long pass into an empty square; the ball waits there for whoever reaches it.</summary>
        ReachedSquare
    }

    public enum ShotOutcome { Blocked, LostContest, Missed, Saved, Goal }

    public sealed class CoinTossed : GameEvent
    {
        public Team KickoffTeam { get; }
        public bool WasRandom { get; }
        public override bool IsRandomized => WasRandom;
        public CoinTossed(Team kickoffTeam, bool wasRandom) { KickoffTeam = kickoffTeam; WasRandom = wasRandom; }
    }

    public sealed class TurnStarted : GameEvent
    {
        public Team Team { get; }
        public int TurnNumber { get; }
        public TurnStarted(Team team, int turnNumber) { Team = team; TurnNumber = turnNumber; }
    }

    /// <summary>The turn team's plan is stored (hidden); the other team must now plan.</summary>
    public sealed class PlanLocked : GameEvent
    {
        public Team Team { get; }
        public Team NextPlanningTeam { get; }
        public PlanLocked(Team team, Team next) { Team = team; NextPlanningTeam = next; }
    }

    public sealed class StepStarted : GameEvent
    {
        public StepKind Step { get; }
        public StepStarted(StepKind step) { Step = step; }
    }

    public sealed class ClockAdvanced : GameEvent
    {
        public int ElapsedSeconds { get; }
        public int RemainingSeconds { get; }
        public ClockAdvanced(int elapsed, int remaining) { ElapsedSeconds = elapsed; RemainingSeconds = remaining; }
    }

    public sealed class PlayerMoved : GameEvent
    {
        public int PlayerId { get; }
        public Cell From { get; }
        public Cell To { get; }
        public PlayerMoved(int playerId, Cell from, Cell to) { PlayerId = playerId; From = from; To = to; }
    }

    /// <summary>The move would have put more than the allowed number of players on one square.</summary>
    public sealed class MoveCancelled : GameEvent
    {
        public int PlayerId { get; }
        public Cell Target { get; }
        public MoveCancelled(int playerId, Cell target) { PlayerId = playerId; Target = target; }
    }

    public sealed class LooseBallClaimed : GameEvent
    {
        public int PlayerId { get; }
        public IReadOnlyList<int> Contenders { get; }
        public override bool IsRandomized => Contenders.Count > 1;
        public LooseBallClaimed(int playerId, IReadOnlyList<int> contenders) { PlayerId = playerId; Contenders = contenders; }
    }

    public sealed class TackleResolved : GameEvent
    {
        public int TacklerId { get; }
        public int CarrierId { get; }
        public bool Success { get; }
        public override bool IsRandomized => true;
        public TackleResolved(int tacklerId, int carrierId, bool success) { TacklerId = tacklerId; CarrierId = carrierId; Success = success; }
    }

    public sealed class ActionCancelled : GameEvent
    {
        public int CarrierId { get; }
        public CarrierOption Option { get; }
        public string Reason { get; }
        public ActionCancelled(int carrierId, CarrierOption option, string reason) { CarrierId = carrierId; Option = option; Reason = reason; }
    }

    public sealed class PassResolved : GameEvent
    {
        public PassKind Kind { get; }
        public int PasserId { get; }
        /// <summary>The intended receiver, or null for a long pass into a square.</summary>
        public int? ReceiverId { get; }
        /// <summary>The square aimed at (long passes), or null.</summary>
        public Cell? TargetCell { get; }
        public PassOutcome Outcome { get; }
        /// <summary>Player holding the ball afterwards, or null if loose / out of play.</summary>
        public int? NewOwnerId { get; }
        /// <summary>Where the ball ended up (may be off the pitch for <see cref="PassOutcome.OutOfPlay"/>).</summary>
        public Cell BallCell { get; }
        public bool Contested { get; }
        public override bool IsRandomized => Kind == PassKind.Long || Contested;

        public PassResolved(PassKind kind, int passerId, int? receiverId, PassOutcome outcome, int? newOwnerId, Cell ballCell, bool contested, Cell? targetCell = null)
        {
            Kind = kind; PasserId = passerId; ReceiverId = receiverId; Outcome = outcome; TargetCell = targetCell;
            NewOwnerId = newOwnerId; BallCell = ballCell; Contested = contested;
        }
    }

    public sealed class ShotResolved : GameEvent
    {
        public int ShooterId { get; }
        public ShotOutcome Outcome { get; }
        public Cell Target { get; }
        /// <summary>Blocker, contest winner or goalkeeper involved, if any.</summary>
        public int? DefenderId { get; }
        public double Accuracy { get; }
        public double GoalChance { get; }
        public override bool IsRandomized => Outcome != ShotOutcome.Blocked;

        public ShotResolved(int shooterId, ShotOutcome outcome, Cell target, int? defenderId, double accuracy, double goalChance)
        {
            ShooterId = shooterId; Outcome = outcome; Target = target; DefenderId = defenderId;
            Accuracy = accuracy; GoalChance = goalChance;
        }
    }

    public sealed class GoalScored : GameEvent
    {
        public Team ScoringTeam { get; }
        public int RedScore { get; }
        public int BlueScore { get; }
        public GoalScored(Team team, int red, int blue) { ScoringTeam = team; RedScore = red; BlueScore = blue; }
    }

    public sealed class RestartAwarded : GameEvent
    {
        public RestartKind Kind { get; }
        public Team Team { get; }
        public int PlayerId { get; }
        public Cell Cell { get; }
        public bool TieBrokenByCoin { get; }
        public override bool IsRandomized => TieBrokenByCoin;

        public RestartAwarded(RestartKind kind, Team team, int playerId, Cell cell, bool tieBrokenByCoin)
        {
            Kind = kind; Team = team; PlayerId = playerId; Cell = cell; TieBrokenByCoin = tieBrokenByCoin;
        }
    }

    public sealed class TieDecisionRequired : GameEvent { }

    public sealed class GoldenGoalStarted : GameEvent { }

    public sealed class MatchEnded : GameEvent
    {
        public MatchResult Result { get; }
        public MatchEnded(MatchResult result) { Result = result; }
    }

    public sealed class CommandResult
    {
        public bool Accepted { get; }
        public string Error { get; }
        public IReadOnlyList<GameEvent> Events { get; }

        private CommandResult(bool accepted, string error, IReadOnlyList<GameEvent> events)
        {
            Accepted = accepted; Error = error; Events = events;
        }

        public static CommandResult Ok(IReadOnlyList<GameEvent> events) => new CommandResult(true, null, events);
        public static CommandResult Ok() => new CommandResult(true, null, new GameEvent[0]);
        public static CommandResult Reject(string error) => new CommandResult(false, error, new GameEvent[0]);
    }
}
