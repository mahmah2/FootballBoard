using System.Linq;
using FootballBoard.Domain.Config;
using NUnit.Framework;
using static FootballBoard.Domain.Tests.Scenario;

namespace FootballBoard.Domain.Tests
{
    public class PassingTests
    {
        private static TeamPlan Pass(int receiver) => TeamPlan.Empty().WithAction(CarrierAction.StraightPass(receiver));
        private static TeamPlan Long(int receiver) => TeamPlan.Empty().WithAction(CarrierAction.LongPass(receiver));

        [Test]
        public void ClearStraightPassAlwaysSucceeds()
        {
            var e = Create();
            Arrange(e, Red2, (Red2, 10, 6), (Red3, 16, 6));
            var r = PlayTurn(e, Pass(Red3));
            Assert.That(Single<PassResolved>(r).Outcome, Is.EqualTo(PassOutcome.Completed));
            Assert.That(e.State.BallCarrier.Id, Is.EqualTo(Red3));
        }

        [Test]
        public void StraightPassesReachAtMostHalfThePitch()
        {
            var e = Create();
            Arrange(e, Red2, (Red2, 3, 6), (Red3, 16, 6), (Red4, 17, 2));
            Assert.That(e.MaxStraightPassCells, Is.EqualTo(13));
            Assert.That(e.SubmitPlan(Team.Red, Pass(Red3)).Accepted, Is.True, "13 squares is allowed");

            e = Create();
            Arrange(e, Red2, (Red2, 3, 6), (Red3, 16, 6), (Red4, 17, 2));
            var r = e.SubmitPlan(Team.Red, Pass(Red4));
            Assert.That(r.Accepted, Is.False);
            Assert.That(r.Error, Does.Contain("long pass"));
            Assert.That(e.SubmitPlan(Team.Red, Long(Red4)).Accepted, Is.True);
        }

        [TestCase(Blue2)]
        [TestCase(Red4)]
        public void AnyPlayerInTheLaneBlocksAStraightPass(int blocker)
        {
            var e = Create();
            Arrange(e, Red2, (Red2, 10, 6), (Red3, 16, 6), (blocker, 13, 6));
            var r = e.SubmitPlan(Team.Red, Pass(Red3));
            Assert.That(r.Accepted, Is.False);
            Assert.That(r.Error, Does.Contain("blocked"));
        }

        [Test]
        public void LaneIsCheckedAtDeclarationByDefault()
        {
            var e = Create();
            Arrange(e, Red2, (Red2, 10, 6), (Red3, 16, 6), (Blue2, 13, 7));
            var r = PlayTurn(e, Pass(Red3), TeamPlan.Empty().WithMove(Blue2, new Cell(13, 6)));
            Assert.That(Single<PassResolved>(r).Outcome, Is.EqualTo(PassOutcome.Completed));
        }

        [Test]
        public void LaneRevalidationCanBeEnabledInConfig()
        {
            var rules = RulesConfig.CreateDefault();
            rules.Passing.RevalidateLaneAfterMovement = true;
            rules.Turns.ActionBeforeMovement = false;
            var e = Create(rules: rules);
            Arrange(e, Red2, (Red2, 10, 6), (Red3, 16, 6), (Blue2, 13, 7));
            var r = PlayTurn(e, Pass(Red3), TeamPlan.Empty().WithMove(Blue2, new Cell(13, 6)));
            Assert.That(r.Events.OfType<ActionCancelled>().Count(), Is.EqualTo(1));
            Assert.That(e.State.BallCarrier.Id, Is.EqualTo(Red2));
        }

        [TestCase(0.4, Red3, PassOutcome.Completed)]
        [TestCase(0.6, Blue2, PassOutcome.Intercepted)]
        public void OpponentOnDestinationMakesItFiftyFifty(double roll, int expectedOwner, PassOutcome outcome)
        {
            var e = Create(new ScriptedRandom(roll));
            Arrange(e, Red2, (Red2, 10, 6), (Red3, 16, 6), (Blue2, 16, 6));
            var r = PlayTurn(e, Pass(Red3));
            var pass = Single<PassResolved>(r);
            Assert.That(pass.Outcome, Is.EqualTo(outcome));
            Assert.That(pass.IsRandomized, Is.True);
            Assert.That(e.State.BallCarrier.Id, Is.EqualTo(expectedOwner));
        }

        [Test]
        public void SharedSquareCarrierCannotPassForward()
        {
            var e = Create();
            Arrange(e, Red2, (Red2, 12, 6), (Blue2, 12, 6), (Red3, 16, 6), (Red4, 12, 2));
            Assert.That(e.SubmitPlan(Team.Red, Pass(Red3)).Accepted, Is.False);
            Assert.That(e.SubmitPlan(Team.Red, Long(Red3)).Accepted, Is.False);
            Assert.That(e.SubmitPlan(Team.Red, Pass(Red4)).Accepted, Is.True, "sideways is allowed");
        }

        [Test]
        public void SuccessfulLongPassIgnoresBlockers()
        {
            var e = Create(new ScriptedRandom(0.05));
            Arrange(e, Red2, (Red2, 10, 6), (Red3, 16, 6), (Blue2, 13, 6));
            var r = PlayTurn(e, Long(Red3));
            Assert.That(Single<PassResolved>(r).Outcome, Is.EqualTo(PassOutcome.Completed));
            Assert.That(e.State.BallCarrier.Id, Is.EqualTo(Red3));
        }

        [Test]
        public void FailedLongPassLandingOnOpponentGivesThemTheBall()
        {
            // Neighbour index 4 is (+1, 0).
            var e = Create(new ScriptedRandom(0.5, ScriptedRandom.Index(4, 8)));
            Arrange(e, Red2, (Red2, 10, 6), (Red3, 16, 6), (Blue2, 17, 6));
            var r = PlayTurn(e, Long(Red3));
            Assert.That(Single<PassResolved>(r).Outcome, Is.EqualTo(PassOutcome.LandedOnOpponent));
            Assert.That(e.State.BallCarrier.Id, Is.EqualTo(Blue2));
        }

        [Test]
        public void LooseBallGoesToFirstArrival()
        {
            // Neighbour index 6 is (0, +1): ball lands on empty (16,7).
            var e = Create(new ScriptedRandom(0.5, ScriptedRandom.Index(6, 8)));
            Arrange(e, Red2, (Red2, 10, 6), (Red3, 16, 6), (Blue2, 17, 8));
            var r = PlayTurn(e, Long(Red3));
            Assert.That(Single<PassResolved>(r).Outcome, Is.EqualTo(PassOutcome.LooseBall));
            Assert.That(e.State.Ball.IsLoose, Is.True);
            Assert.That(e.State.BallPosition, Is.EqualTo(new Cell(16, 7)));

            // Still Red's turn (the ball is loose, nobody owns it): Blue2 reaches the ball alone.
            r = PlayTurn(e, TeamPlan.Empty(), TeamPlan.Empty().WithMove(Blue2, new Cell(16, 7)));
            Assert.That(e.State.TurnTeam, Is.EqualTo(Team.Blue), "Blue owns the ball now, so Blue plans first");
            Assert.That(Single<LooseBallClaimed>(r).PlayerId, Is.EqualTo(Blue2));
            Assert.That(e.State.BallCarrier.Id, Is.EqualTo(Blue2));
        }

        [Test]
        public void SimultaneousArrivalIsAClash()
        {
            var e = Create(new ScriptedRandom(0.5, ScriptedRandom.Index(6, 8), 0.2));
            Arrange(e, Red2, (Red2, 10, 6), (Red3, 16, 6), (Blue2, 17, 8));
            PlayTurn(e, Long(Red3));
            var r = PlayTurn(e, TeamPlan.Empty().WithMove(Red3, new Cell(16, 7)), TeamPlan.Empty().WithMove(Blue2, new Cell(16, 7)));
            var claim = Single<LooseBallClaimed>(r);
            Assert.That(claim.IsRandomized, Is.True);
            Assert.That(claim.Contenders, Is.EquivalentTo(new[] { Red3, Blue2 }));
            Assert.That(e.State.BallCarrier.Id, Is.EqualTo(Red3)); // roll 0.2 picks the first of [3, 102]
        }

        [Test]
        public void LongPassOverSidelineIsThrowInForOtherTeam()
        {
            // Receiver on row 0; neighbour index 1 is (0, -1) -> off the top sideline.
            var e = Create(new ScriptedRandom(0.5, ScriptedRandom.Index(1, 8)));
            Arrange(e, Red2, (Red2, 10, 2), (Red3, 14, 0), (Red4, 9, 9), (Red5, 9, 10), (Blue2, 16, 2));
            var r = PlayTurn(e, Long(Red3));
            Assert.That(Single<PassResolved>(r).Outcome, Is.EqualTo(PassOutcome.OutOfPlay));
            var restart = Single<RestartAwarded>(r);
            Assert.That(restart.Kind, Is.EqualTo(RestartKind.ThrowIn));
            Assert.That(restart.Team, Is.EqualTo(Team.Blue));
            Assert.That(e.State.BallCarrier.Id, Is.EqualTo(Blue2));
            Assert.That(Pos(e, Blue2), Is.EqualTo(new Cell(14, 0)));
        }

        [Test]
        public void LongPassOverGoalLineIsGoalKick()
        {
            // Receiver on Blue's goal line; neighbour index 4 is (+1, 0) -> beyond the goal line.
            var e = Create(new ScriptedRandom(0.5, ScriptedRandom.Index(4, 8)));
            Arrange(e, Red2, (Red2, 20, 2), (Red3, 25, 2));
            var r = PlayTurn(e, Long(Red3));
            var restart = Single<RestartAwarded>(r);
            Assert.That(restart.Kind, Is.EqualTo(RestartKind.GoalKick));
            Assert.That(e.State.BallCarrier.Id, Is.EqualTo(BlueGk));
            Assert.That(Pos(e, BlueGk), Is.EqualTo(new Cell(25, 6)));
        }
    }

    public class ShotTests
    {
        private static TeamPlan Shoot() => TeamPlan.Empty().WithAction(CarrierAction.Shot());

        [Test]
        public void KeeperTwoCellsAwayIsSixtyPercentAndConcedingTeamKicksOff()
        {
            var e = Create(new ScriptedRandom(0.1));
            Arrange(e, Red2, (Red2, 22, 4));
            var r = PlayTurn(e, Shoot());
            Assert.That(Single<ShotResolved>(r).Outcome, Is.EqualTo(ShotOutcome.Goal));
            Assert.That(e.State.Score(Team.Red), Is.EqualTo(1));
            Assert.That(e.State.TurnTeam, Is.EqualTo(Team.Blue));
            Assert.That(e.State.BallCarrier.Team, Is.EqualTo(Team.Blue));
            Assert.That(e.State.BallPosition, Is.EqualTo(e.State.Pitch.KickoffSpot(Team.Blue)));
            Assert.That(e.State.Players.All(p => e.State.Pitch.InOwnHalf(p.Team, p.Position)), Is.True);
        }

        [Test]
        public void ACarrierStandingInTheGoalMouthCanShoot()
        {
            // Red 4 on a goal-line square of Blue's goal: the default aim is its own square.
            var e = Create(new ScriptedRandom(0.1));
            Arrange(e, Red4, (Red4, 25, 4));
            Assert.That(e.ShotTargetFor(e.State.GetPlayer(Red4), CarrierAction.Shot()), Is.EqualTo(new Cell(25, 4)));
            var r = PlayTurn(e, Shoot());
            Assert.That(Single<ShotResolved>(r).Outcome, Is.EqualTo(ShotOutcome.Goal));
        }

        [Test]
        public void ConcedingTeamKeepsTheTurnAfterScoringBack()
        {
            var e = Create(new ScriptedRandom(0.1, 0.1));
            Arrange(e, Red2, (Red2, 22, 4));
            PlayTurn(e, Shoot());                 // Red scores, Blue restarts
            Arrange(e, Blue2, (Blue2, 3, 4));
            PlayTurn(e, Shoot());                 // Blue scores, Red restarts
            Assert.That(e.State.TurnTeam, Is.EqualTo(Team.Red));
            Assert.That(e.State.Score(Team.Blue), Is.EqualTo(1));
        }

        [Test]
        public void KeeperOnTargetSavesNinetyFivePercent()
        {
            var e = Create(new ScriptedRandom(0.5));
            Arrange(e, Red2, (Red2, 22, 6));
            var r = PlayTurn(e, Shoot());
            var shot = Single<ShotResolved>(r);
            Assert.That(shot.Outcome, Is.EqualTo(ShotOutcome.Saved));
            Assert.That(shot.GoalChance, Is.EqualTo(0.05));
            Assert.That(e.State.BallCarrier.Id, Is.EqualTo(BlueGk));
        }

        [Test]
        public void KeeperOneCellAwayIsThirtyPercent()
        {
            var e = Create(new ScriptedRandom(0.2));
            Arrange(e, Red2, (Red2, 22, 5));
            var shot = Single<ShotResolved>(PlayTurn(e, Shoot()));
            Assert.That(shot.GoalChance, Is.EqualTo(0.3));
            Assert.That(shot.Outcome, Is.EqualTo(ShotOutcome.Goal));
        }

        [Test]
        public void KeeperPositionIsTakenAfterMovement()
        {
            var e = Create(new ScriptedRandom(0.5), MoveFirstRules());
            Arrange(e, Red2, (Red2, 22, 5));
            var r = PlayTurn(e, Shoot(), TeamPlan.Empty().WithMove(BlueGk, new Cell(25, 5)));
            Assert.That(Single<ShotResolved>(r).Outcome, Is.EqualTo(ShotOutcome.Saved));
        }

        [Test]
        public void MissFromDistanceIsGoalKick()
        {
            var e = Create(new ScriptedRandom(0.7));
            Arrange(e, Red2, (Red2, 12, 6));
            var r = PlayTurn(e, Shoot());
            var shot = Single<ShotResolved>(r);
            Assert.That(shot.Accuracy, Is.EqualTo(0.2).Within(1e-9));
            Assert.That(shot.Outcome, Is.EqualTo(ShotOutcome.Missed));
            Assert.That(Single<RestartAwarded>(r).Kind, Is.EqualTo(RestartKind.GoalKick));
            Assert.That(e.State.BallCarrier.Id, Is.EqualTo(BlueGk));
        }

        [Test]
        public void AdjacentDefenderAtDeclarationBlocksAndIsMovedBack()
        {
            var e = Create(rules: MoveFirstRules());
            Arrange(e, Red2, (Red2, 20, 6), (Blue2, 21, 6));
            var r = PlayTurn(e, Shoot(), TeamPlan.Empty().WithMove(Blue2, new Cell(21, 7)));
            var shot = Single<ShotResolved>(r);
            Assert.That(shot.Outcome, Is.EqualTo(ShotOutcome.Blocked));
            Assert.That(shot.DefenderId, Is.EqualTo(Blue2));
            Assert.That(Pos(e, Blue2), Is.EqualTo(new Cell(21, 6)));
            Assert.That(e.State.BallCarrier.Id, Is.EqualTo(Blue2));
        }

        [Test]
        public void GoalkeeperDoesNotCountAsBlocker()
        {
            var e = Create(new ScriptedRandom(0.5));
            Arrange(e, Red2, (Red2, 24, 6));
            var shot = Single<ShotResolved>(PlayTurn(e, Shoot()));
            Assert.That(shot.Outcome, Is.EqualTo(ShotOutcome.Saved));
        }

        [Test]
        public void OpponentOnShootersSquareContestsTheShot()
        {
            var e = Create(new ScriptedRandom(0.6), MoveFirstRules());
            Arrange(e, Red2, (Red2, 22, 4), (Blue2, 21, 3));
            var r = PlayTurn(e, Shoot(), TeamPlan.Empty().WithMove(Blue2, new Cell(22, 4)));
            Assert.That(Single<ShotResolved>(r).Outcome, Is.EqualTo(ShotOutcome.LostContest));
            Assert.That(e.State.BallCarrier.Id, Is.EqualTo(Blue2));
        }
    }

    public class TackleTests
    {
        [Test]
        public void SuccessfulTackleWinsBallAndCancelsDeclaredAction()
        {
            var e = Create(new ScriptedRandom(0.3), MoveFirstRules());
            Arrange(e, Red2, (Red2, 12, 6), (Red3, 12, 2), (Blue2, 13, 6));
            var r = PlayTurn(e,
                TeamPlan.Empty().WithAction(CarrierAction.StraightPass(Red3)),
                TeamPlan.Empty().WithMove(Blue2, new Cell(12, 6), tackle: true));
            Assert.That(Single<TackleResolved>(r).Success, Is.True);
            Assert.That(r.Events.OfType<ActionCancelled>().Count(), Is.EqualTo(1));
            Assert.That(e.State.BallCarrier.Id, Is.EqualTo(Blue2));
        }

        [Test]
        public void FailedTackleLetsTheActionHappen()
        {
            var e = Create(new ScriptedRandom(0.7), MoveFirstRules());
            Arrange(e, Red2, (Red2, 12, 6), (Red3, 12, 2), (Blue2, 13, 6));
            var r = PlayTurn(e,
                TeamPlan.Empty().WithAction(CarrierAction.StraightPass(Red3)),
                TeamPlan.Empty().WithMove(Blue2, new Cell(12, 6), tackle: true));
            Assert.That(Single<TackleResolved>(r).Success, Is.False);
            Assert.That(e.State.BallCarrier.Id, Is.EqualTo(Red3));
        }

        [Test]
        public void NoTackleWithoutRequest()
        {
            var e = Create();
            Arrange(e, Red2, (Red2, 12, 6), (Blue2, 13, 6));
            var r = PlayTurn(e, null, TeamPlan.Empty().WithMove(Blue2, new Cell(12, 6)));
            Assert.That(r.Events.OfType<TackleResolved>(), Is.Empty);
            Assert.That(e.State.BallCarrier.Id, Is.EqualTo(Red2));
        }

        [Test]
        public void TacklesCanBeDisabledInConfig()
        {
            var rules = RulesConfig.CreateDefault();
            rules.Tackle.Enabled = false;
            var e = Create(rules: rules);
            Arrange(e, Red2, (Red2, 12, 6), (Blue2, 13, 6));
            e.SubmitPlan(Team.Red, TeamPlan.Empty());
            Assert.That(e.SubmitPlan(Team.Blue, TeamPlan.Empty().WithMove(Blue2, new Cell(12, 6), tackle: true)).Accepted, Is.False);
        }
    }
}
