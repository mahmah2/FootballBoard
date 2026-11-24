using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FootballBoard.Domain.Config;
using NUnit.Framework;

namespace FootballBoard.Domain.Tests
{
    /// <summary>Returns pre-arranged values so each random outcome in a test is chosen explicitly.</summary>
    internal sealed class ScriptedRandom : IRandomSource
    {
        private readonly Queue<double> _values = new Queue<double>();

        public ScriptedRandom(params double[] values) => Enqueue(values);

        public int Remaining => _values.Count;

        public void Enqueue(params double[] values)
        {
            foreach (var v in values) _values.Enqueue(v);
        }

        public double NextDouble()
        {
            if (_values.Count == 0) throw new InvalidOperationException("ScriptedRandom ran out of values: the engine rolled more dice than the test expected.");
            return _values.Dequeue();
        }

        public int NextInt(int maxExclusive) => Math.Min(maxExclusive - 1, (int)(NextDouble() * maxExclusive));

        /// <summary>Value that makes NextInt(count) return <paramref name="index"/>.</summary>
        public static double Index(int index, int count) => (index + 0.5) / count;
    }

    /// <summary>
    /// Builds board situations on the default 13x26 board with 5 players per team.
    /// Ids: Red goalkeeper 1, outfield 2-5; Blue goalkeeper 101, outfield 102-105.
    /// Goalkeepers start at (0,6) and (25,6).
    /// </summary>
    internal static class Scenario
    {
        public const int RedGk = 1, Red2 = 2, Red3 = 3, Red4 = 4, Red5 = 5;
        public const int BlueGk = 101, Blue2 = 102, Blue3 = 103, Blue4 = 104, Blue5 = 105;

        public static MatchEngine Create(ScriptedRandom random = null, RulesConfig rules = null, MatchSettings settings = null, Team turn = Team.Red)
        {
            var engine = MatchEngine.Create(rules, settings, random ?? new ScriptedRandom());
            var start = engine.StartMatch(turn);
            Assert.That(start.Accepted, Is.True, start.Error);
            return engine;
        }

        /// <summary>Moves every outfield player to the far top/bottom rows, then applies <paramref name="positions"/>.</summary>
        public static void Arrange(MatchEngine engine, int ballOwner, params (int id, int x, int y)[] positions)
        {
            var reds = new[] { Red2, Red3, Red4, Red5 };
            var blues = new[] { Blue2, Blue3, Blue4, Blue5 };
            for (var i = 0; i < 4; i++)
            {
                engine.ForcePosition(reds[i], new Cell(1 + 2 * i, 0));
                engine.ForcePosition(blues[i], new Cell(24 - 2 * i, 12));
            }
            engine.ForcePosition(RedGk, new Cell(0, 6));
            engine.ForcePosition(BlueGk, new Cell(25, 6));
            foreach (var (id, x, y) in positions) engine.ForcePosition(id, new Cell(x, y));
            engine.ForceBallOwner(ballOwner);
            engine.ForceTurn(engine.State.TurnTeam);
        }

        /// <summary>Turn team plans first, then the other team; asserts both are accepted.</summary>
        public static CommandResult PlayTurn(MatchEngine engine, TeamPlan turnPlan = null, TeamPlan otherPlan = null)
        {
            var team = engine.State.TurnTeam;
            var first = engine.SubmitPlan(team, turnPlan ?? TeamPlan.Empty());
            Assert.That(first.Accepted, Is.True, first.Error);
            var second = engine.SubmitPlan(team.Opponent(), otherPlan ?? TeamPlan.Empty());
            Assert.That(second.Accepted, Is.True, second.Error);
            return second;
        }

        public static T Single<T>(CommandResult result) where T : GameEvent => result.Events.OfType<T>().Single();

        public static Cell Pos(MatchEngine engine, int id) => engine.State.GetPlayer(id).Position;

        /// <summary>Spec v1.3 order: everyone moves first, then the declared pass or shot.</summary>
        public static RulesConfig MoveFirstRules()
        {
            var rules = RulesConfig.CreateDefault();
            rules.Turns.ActionBeforeMovement = false;
            return rules;
        }

        public static string FindRulesFile()
        {
            foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                var dir = new DirectoryInfo(start);
                while (dir != null)
                {
                    var candidate = Path.Combine(dir.FullName, "Assets", "Resources", "Rules", "rules.json");
                    if (File.Exists(candidate)) return candidate;
                    dir = dir.Parent;
                }
            }
            throw new FileNotFoundException("Could not find Assets/Resources/Rules/rules.json");
        }
    }
}
