using System.IO;
using FootballBoard.Domain.Config;
using NUnit.Framework;

namespace FootballBoard.Domain.Tests
{
    public class ConfigTests
    {
        [Test]
        public void ShippedRulesFileIsValid()
        {
            var json = File.ReadAllText(Scenario.FindRulesFile());
            var ok = RulesConfigLoader.TryFromJson(json, out var config, out var errors);
            Assert.That(ok, Is.True, string.Join("\n", errors));
            Assert.That(config.Board.Rows, Is.EqualTo(13));
            Assert.That(config.Board.Columns, Is.EqualTo(26));
            Assert.That(config.Teams.Formations["5"], Has.Count.EqualTo(5));
        }

        [Test]
        public void MissingKeysKeepDefaults()
        {
            var config = RulesConfigLoader.FromJson("{ \"board\": { \"rows\": 15, \"columns\": 28 } }");
            Assert.That(config.Board.Rows, Is.EqualTo(15));
            Assert.That(config.Board.GoalWidth, Is.EqualTo(5));
            Assert.That(config.Tackle.SuccessChance, Is.EqualTo(0.5));
            Assert.That(config.Clock.PresetMinutes, Is.EquivalentTo(new[] { 10, 20, 30, 40, 50, 60, 70, 90 }));
        }

        [Test]
        public void ListsAreReplacedNotAppended()
        {
            var config = RulesConfigLoader.FromJson("{ \"clock\": { \"defaultMinutes\": 20, \"presetMinutes\": [20] } }");
            Assert.That(config.Clock.PresetMinutes, Is.EquivalentTo(new[] { 20 }));
        }

        [Test]
        public void UnknownKeyIsRejected()
        {
            var ok = RulesConfigLoader.TryFromJson("{ \"board\": { \"rowz\": 15 } }", out _, out var errors);
            Assert.That(ok, Is.False);
            Assert.That(errors[0], Does.Contain("rowz"));
        }

        [Test]
        public void OutOfRangeValuesAreAllReported()
        {
            var ok = RulesConfigLoader.TryFromJson(
                "{ \"board\": { \"rows\": 14 }, \"tackle\": { \"successChance\": 1.5 } }", out _, out var errors);
            Assert.That(ok, Is.False);
            Assert.That(errors, Has.Some.Contains("board.rows"));
            Assert.That(errors, Has.Some.Contains("tackle.successChance"));
        }

        [Test]
        public void BrokenFileFallsBackToDefaults()
        {
            var config = RulesConfigLoader.FromJsonOrDefault("{ not json", out var errors);
            Assert.That(errors, Is.Not.Empty);
            Assert.That(config.Board.Rows, Is.EqualTo(13));
        }

        [Test]
        public void RoundTripProducesValidConfig()
        {
            var json = RulesConfigLoader.ToJson(RulesConfig.CreateDefault());
            Assert.That(RulesConfigLoader.FromJson(json).Passing.LaneTolerance, Is.EqualTo(0.05));
        }

        [Test]
        public void EngineKeepsItsOwnCopyOfTheRules()
        {
            var rules = RulesConfig.CreateDefault();
            var engine = MatchEngine.Create(rules, null, new ScriptedRandom());
            rules.Tackle.SuccessChance = 0;
            Assert.That(engine.State.Rules.Tackle.SuccessChance, Is.EqualTo(0.5));
        }

        [Test]
        public void InvalidMatchSettingsAreRejected()
        {
            Assert.Throws<System.ArgumentException>(() =>
                MatchEngine.Create(null, new MatchSettings { DurationMinutes = 15 }, new ScriptedRandom()));
        }
    }

    public class GeometryTests
    {
        private static readonly Pitch Pitch = new Pitch(new BoardRules());

        [Test]
        public void GoalCoversRowsFiveToNineOnThirteenRows()
        {
            // 0-based rows 4..8 are rows 5..9 counting from 1.
            Assert.That(Pitch.GoalTop, Is.EqualTo(4));
            Assert.That(Pitch.GoalBottom, Is.EqualTo(8));
        }

        [Test]
        public void PenaltyBoxIsFiveWideFourDeep()
        {
            Assert.That(Pitch.InPenaltyBox(Team.Red, new Cell(3, 6)), Is.True);
            Assert.That(Pitch.InPenaltyBox(Team.Red, new Cell(4, 6)), Is.False);
            Assert.That(Pitch.InPenaltyBox(Team.Red, new Cell(0, 3)), Is.False);
            Assert.That(Pitch.InPenaltyBox(Team.Blue, new Cell(22, 8)), Is.True);
        }

        [Test]
        public void ShotTargetIsClampedIntoTheGoal()
        {
            Assert.That(Pitch.ShotTarget(Team.Red, new Cell(20, 6)), Is.EqualTo(new Cell(25, 6)));
            Assert.That(Pitch.ShotTarget(Team.Red, new Cell(20, 0)), Is.EqualTo(new Cell(25, 4)));
            Assert.That(Pitch.ShotTarget(Team.Blue, new Cell(5, 12)), Is.EqualTo(new Cell(0, 8)));
        }

        [Test]
        public void StraightLaneIncludesOnlyCellsBetween()
        {
            var cells = Pitch.LaneCells(new Cell(2, 6), new Cell(6, 6), 0.05);
            Assert.That(cells, Is.EquivalentTo(new[] { new Cell(3, 6), new Cell(4, 6), new Cell(5, 6) }));
        }

        [Test]
        public void DiagonalLaneUsesCellCentres()
        {
            var cells = Pitch.LaneCells(new Cell(0, 0), new Cell(4, 2), 0.05);
            Assert.That(cells, Is.EquivalentTo(new[] { new Cell(2, 1) }));
        }

        [Test]
        public void LaneToleranceBoundaryIsFivePercent()
        {
            // Line (0,0)->(20,1): cell (1,0) is 0.0499 away (blocks), cell (2,0) is 0.0999 away (does not).
            var cells = Pitch.LaneCells(new Cell(0, 0), new Cell(20, 1), 0.05);
            Assert.That(cells, Has.Member(new Cell(1, 0)));
            Assert.That(cells, Has.No.Member(new Cell(2, 0)));
        }

        [Test]
        public void ShotAccuracyFallsLinearlyToCentreLine()
        {
            var engine = MatchEngine.Create(null, null, new ScriptedRandom());
            Assert.That(engine.ShotAccuracy(Team.Red, new Cell(22, 6)), Is.EqualTo(1.0));
            Assert.That(engine.ShotAccuracy(Team.Red, new Cell(21, 6)), Is.EqualTo(1.0 - 0.5 / 9.5).Within(1e-9));
            Assert.That(engine.ShotAccuracy(Team.Red, new Cell(12, 6)), Is.EqualTo(0.5));
            Assert.That(engine.ShotAccuracy(Team.Red, new Cell(3, 6)), Is.EqualTo(0.5));
            Assert.That(engine.ShotAccuracy(Team.Blue, new Cell(3, 6)), Is.EqualTo(1.0));
        }

        [Test]
        public void GoalChanceUsesLastEntryForLargeDistances()
        {
            var engine = MatchEngine.Create(null, null, new ScriptedRandom());
            Assert.That(engine.GoalChance(0), Is.EqualTo(0.05));
            Assert.That(engine.GoalChance(1), Is.EqualTo(0.5));
            Assert.That(engine.GoalChance(2), Is.EqualTo(1.0));
            Assert.That(engine.GoalChance(7), Is.EqualTo(1.0));
        }
    }
}
