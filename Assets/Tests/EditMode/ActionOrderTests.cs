using System.Linq;
using NUnit.Framework;
using static FootballBoard.Domain.Tests.Scenario;

namespace FootballBoard.Domain.Tests
{
    /// <summary>Default order: the declared pass or shot resolves first, then every player moves.</summary>
    public class ActionOrderTests
    {
        [Test]
        public void EventsShowTheActionBeforeAnyMovement()
        {
            var e = Create();
            Arrange(e, Red2, (Red2, 10, 6), (Red3, 16, 6), (Red4, 9, 9), (Blue2, 14, 8));
            var r = PlayTurn(e,
                TeamPlan.Empty().WithAction(CarrierAction.StraightPass(Red3)).WithMove(Red4, new Cell(10, 9)),
                TeamPlan.Empty().WithMove(Blue2, new Cell(15, 7)));
            var events = r.Events.ToList();
            var pass = events.FindIndex(ev => ev is PassResolved);
            var firstMove = events.FindIndex(ev => ev is PlayerMoved);
            Assert.That(pass, Is.LessThan(firstMove));
            Assert.That(Pos(e, Red4), Is.EqualTo(new Cell(10, 9)));
            Assert.That(Pos(e, Blue2), Is.EqualTo(new Cell(15, 7)));
        }

        [Test]
        public void DefenderStepsOntoReceiverTooLateToContestThePass()
        {
            // The pass is not contested; arriving on the new carrier's square is an automatic tackle (failed here).
            var e = Create(new ScriptedRandom(0.7));
            Arrange(e, Red2, (Red2, 10, 6), (Red3, 16, 6), (Blue2, 17, 6));
            var r = PlayTurn(e, TeamPlan.Empty().WithAction(CarrierAction.StraightPass(Red3)),
                TeamPlan.Empty().WithMove(Blue2, new Cell(16, 6)));
            var pass = Single<PassResolved>(r);
            Assert.That(pass.Contested, Is.False);
            Assert.That(Single<TackleResolved>(r).Success, Is.False);
            Assert.That(e.State.BallCarrier.Id, Is.EqualTo(Red3));
            Assert.That(Pos(e, Blue2), Is.EqualTo(new Cell(16, 6)));
        }

        [Test]
        public void DefenderCanTackleTheReceiverAfterThePass()
        {
            var e = Create(new ScriptedRandom(0.3));
            Arrange(e, Red2, (Red2, 10, 6), (Red3, 16, 6), (Blue2, 17, 6));
            var r = PlayTurn(e, TeamPlan.Empty().WithAction(CarrierAction.StraightPass(Red3)),
                TeamPlan.Empty().WithMove(Blue2, new Cell(16, 6), tackle: true));
            Assert.That(Single<TackleResolved>(r).CarrierId, Is.EqualTo(Red3));
            Assert.That(e.State.BallCarrier.Id, Is.EqualTo(Blue2));
            Assert.That(e.State.TurnTeam, Is.EqualTo(Team.Blue));
        }

        [Test]
        public void KeeperMovesAfterTheShot()
        {
            // Keeper two cells from the target when the shot is declared: a sure goal, even though it moves across.
            var e = Create(new ScriptedRandom(0.1)); // keeper two cells from the target: 60%, roll 0.1 scores
            Arrange(e, Red2, (Red2, 22, 4));
            var r = PlayTurn(e, TeamPlan.Empty().WithAction(CarrierAction.Shot()),
                TeamPlan.Empty().WithMove(BlueGk, new Cell(25, 5)));
            Assert.That(Single<ShotResolved>(r).Outcome, Is.EqualTo(ShotOutcome.Goal));
        }

        [Test]
        public void RestartTakerKeepsTheRestartSquare()
        {
            // Long pass overhit off the top sideline: Blue2 takes the throw-in, so its planned move is dropped.
            var e = Create(new ScriptedRandom(0.5, ScriptedRandom.Index(1, 8)));
            Arrange(e, Red2, (Red2, 10, 2), (Red3, 14, 0), (Red4, 9, 9), (Red5, 9, 10), (Blue2, 16, 2));
            var r = PlayTurn(e, TeamPlan.Empty().WithAction(CarrierAction.LongPass(Red3)),
                TeamPlan.Empty().WithMove(Blue2, new Cell(16, 3)));
            Assert.That(Pos(e, Blue2), Is.EqualTo(new Cell(14, 0)));
            Assert.That(r.Events.OfType<MoveCancelled>().Single().PlayerId, Is.EqualTo(Blue2));
            Assert.That(e.State.BallCarrier.Id, Is.EqualTo(Blue2));
        }

        [Test]
        public void NoMovementAfterAGoal()
        {
            var e = Create(new ScriptedRandom(0.1));
            Arrange(e, Red2, (Red2, 22, 4), (Red3, 10, 6));
            var r = PlayTurn(e, TeamPlan.Empty().WithAction(CarrierAction.Shot()).WithMove(Red3, new Cell(11, 6)));
            Assert.That(r.Events.OfType<GoalScored>().Count(), Is.EqualTo(1));
            Assert.That(e.State.Players.All(p => e.State.Pitch.InOwnHalf(p.Team, p.Position)), Is.True);
            Assert.That(e.State.ElapsedSeconds, Is.EqualTo(60));
        }
    }
}
