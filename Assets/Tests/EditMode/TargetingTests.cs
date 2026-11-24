using System.Linq;
using NUnit.Framework;
using static FootballBoard.Domain.Tests.Scenario;

namespace FootballBoard.Domain.Tests
{
    public class ShotTargetTests
    {
        [Test]
        public void ChosenCornerBeatsAKeeperInTheMiddle()
        {
            // Keeper on (25,6). Aiming at (25,8) puts the target two cells away: 60% instead of 5%.
            var e = Create(new ScriptedRandom(0.1));
            Arrange(e, Red2, (Red2, 22, 6));
            var r = PlayTurn(e, TeamPlan.Empty().WithAction(CarrierAction.Shot(new Cell(25, 8))));
            var shot = Single<ShotResolved>(r);
            Assert.That(shot.Target, Is.EqualTo(new Cell(25, 8)));
            Assert.That(shot.Outcome, Is.EqualTo(ShotOutcome.Goal));
        }

        [Test]
        public void WithoutATargetTheShotAimsAlongTheShootersRow()
        {
            var e = Create(new ScriptedRandom(0.5));
            Arrange(e, Red2, (Red2, 22, 6));
            var shot = Single<ShotResolved>(PlayTurn(e, TeamPlan.Empty().WithAction(CarrierAction.Shot())));
            Assert.That(shot.Target, Is.EqualTo(new Cell(25, 6)));
            Assert.That(shot.Outcome, Is.EqualTo(ShotOutcome.Saved));
        }

        [TestCase(25, 3)]  // beside the goal
        [TestCase(24, 6)]  // not on the goal line
        [TestCase(0, 6)]   // own goal
        public void TargetMustBeInsideTheOpponentsGoal(int x, int y)
        {
            var e = Create();
            Arrange(e, Red2, (Red2, 22, 6));
            var r = e.SubmitPlan(Team.Red, TeamPlan.Empty().WithAction(CarrierAction.Shot(new Cell(x, y))));
            Assert.That(r.Accepted, Is.False);
        }

        [Test]
        public void BlockerIsCheckedTowardTheChosenTarget()
        {
            // Shooting from (20,4) at (25,8): the first step is diagonal, onto (21,5).
            var e = Create();
            Arrange(e, Red2, (Red2, 20, 4), (Blue2, 21, 5));
            var shot = Single<ShotResolved>(PlayTurn(e, TeamPlan.Empty().WithAction(CarrierAction.Shot(new Cell(25, 8)))));
            Assert.That(shot.Outcome, Is.EqualTo(ShotOutcome.Blocked));
        }
    }

    public class LongPassToSquareTests
    {
        private static TeamPlan LongTo(int x, int y) => TeamPlan.Empty().WithAction(CarrierAction.LongPassTo(new Cell(x, y)));

        [Test]
        public void AccurateBallIntoSpaceIsCollectedByTheRunner()
        {
            // 0.05 < 1/9: on target. The ball waits on (16,6) and Red3 runs onto it in the same turn.
            var e = Create(new ScriptedRandom(0.05));
            Arrange(e, Red2, (Red2, 10, 6), (Red3, 15, 6));
            var r = PlayTurn(e, LongTo(16, 6).WithMove(Red3, new Cell(16, 6)));
            Assert.That(Single<PassResolved>(r).Outcome, Is.EqualTo(PassOutcome.ReachedSquare));
            Assert.That(Single<LooseBallClaimed>(r).PlayerId, Is.EqualTo(Red3));
            Assert.That(e.State.BallCarrier.Id, Is.EqualTo(Red3));
        }

        [TestCase(0.2, Red3)]
        [TestCase(0.7, Blue2)]
        public void TwoRunnersArrivingTogetherIsFiftyFifty(double clashRoll, int winner)
        {
            var e = Create(new ScriptedRandom(0.05, clashRoll));
            Arrange(e, Red2, (Red2, 10, 6), (Red3, 15, 6), (Blue2, 17, 6));
            var r = PlayTurn(e, LongTo(16, 6).WithMove(Red3, new Cell(16, 6)), TeamPlan.Empty().WithMove(Blue2, new Cell(16, 6)));
            var claim = Single<LooseBallClaimed>(r);
            Assert.That(claim.Contenders, Is.EquivalentTo(new[] { Red3, Blue2 }));
            Assert.That(claim.IsRandomized, Is.True);
            Assert.That(e.State.BallCarrier.Id, Is.EqualTo(winner));
        }

        [Test]
        public void NobodyArrivesAndTheBallStaysLoose()
        {
            var e = Create(new ScriptedRandom(0.05));
            Arrange(e, Red2, (Red2, 10, 6));
            PlayTurn(e, LongTo(16, 6));
            Assert.That(e.State.Ball.IsLoose, Is.True);
            Assert.That(e.State.BallPosition, Is.EqualTo(new Cell(16, 6)));
        }

        [Test]
        public void MissedBallLandsAroundTheChosenSquare()
        {
            // Miss, then neighbour index 4 = (+1, 0): lands on (17,6), where Blue2 stands.
            var e = Create(new ScriptedRandom(0.5, ScriptedRandom.Index(4, 8)));
            Arrange(e, Red2, (Red2, 10, 6), (Blue2, 17, 6));
            var pass = Single<PassResolved>(PlayTurn(e, LongTo(16, 6)));
            Assert.That(pass.Outcome, Is.EqualTo(PassOutcome.LandedOnOpponent));
            Assert.That(pass.TargetCell, Is.EqualTo(new Cell(16, 6)));
            Assert.That(e.State.BallCarrier.Id, Is.EqualTo(Blue2));
        }

        [Test]
        public void AccurateBallOntoAnOpponentIsTheirs()
        {
            var e = Create(new ScriptedRandom(0.05));
            Arrange(e, Red2, (Red2, 10, 6), (Blue2, 16, 6));
            var pass = Single<PassResolved>(PlayTurn(e, LongTo(16, 6)));
            Assert.That(pass.Outcome, Is.EqualTo(PassOutcome.Intercepted));
            Assert.That(e.State.BallCarrier.Id, Is.EqualTo(Blue2));
        }

        [TestCase(0.4, Red3)]
        [TestCase(0.6, Blue2)]
        public void AccurateBallOntoBothTeamsIsFiftyFifty(double roll, int owner)
        {
            var e = Create(new ScriptedRandom(0.05, roll));
            Arrange(e, Red2, (Red2, 10, 6), (Red3, 16, 6), (Blue2, 16, 6));
            var pass = Single<PassResolved>(PlayTurn(e, LongTo(16, 6)));
            Assert.That(pass.Contested, Is.True);
            Assert.That(e.State.BallCarrier.Id, Is.EqualTo(owner));
        }

        [TestCase(10, 6)]   // the carrier's own square
        [TestCase(30, 6)]   // off the pitch
        public void InvalidSquaresAreRejected(int x, int y)
        {
            var e = Create();
            Arrange(e, Red2, (Red2, 10, 6));
            Assert.That(e.SubmitPlan(Team.Red, LongTo(x, y)).Accepted, Is.False);
        }

        [Test]
        public void TheRunnerIsNotLockedLikeAReceiver()
        {
            var e = Create();
            Arrange(e, Red2, (Red2, 10, 6), (Red3, 15, 6));
            Assert.That(e.SubmitPlan(Team.Red, LongTo(16, 6).WithMove(Red3, new Cell(16, 6))).Accepted, Is.True);
        }
    }
}
