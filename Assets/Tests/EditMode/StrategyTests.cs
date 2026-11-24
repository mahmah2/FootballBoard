using System.Linq;
using FootballBoard.Domain.AI;
using NUnit.Framework;
using static FootballBoard.Domain.Tests.Scenario;

namespace FootballBoard.Domain.Tests
{
    public class StrategyTests
    {
        [TestCase(1UL)]
        [TestCase(7UL)]
        [TestCase(42UL)]
        public void ComputerVersusComputerPlaysWholeMatchesWithLegalPlans(ulong seed)
        {
            var engine = MatchEngine.Create(null, new MatchSettings { Seed = seed, DurationMinutes = 90 });
            var red = new BasicStrategy(new SeededRandom(seed + 100));
            var blue = new BasicStrategy(new SeededRandom(seed + 200));
            engine.StartMatch();
            var guard = 0;
            while (engine.State.Phase != MatchPhase.MatchOver && guard++ < 1000)
            {
                if (engine.State.Phase == MatchPhase.AwaitingTieDecision)
                {
                    engine.SubmitTieChoice(Team.Red, TieChoice.Draw);
                    engine.SubmitTieChoice(Team.Blue, TieChoice.Draw);
                    continue;
                }
                var team = engine.State.PlanningTeam;
                var plan = (team == Team.Red ? red : blue).PlanTurn(engine, team);
                var result = engine.SubmitPlan(team, plan);
                Assert.That(result.Accepted, Is.True, $"turn {engine.State.TurnNumber}: {result.Error}");
            }
            Assert.That(engine.State.Phase, Is.EqualTo(MatchPhase.MatchOver));
        }

        [Test]
        public void ShootsWhenAClearChanceIsOn()
        {
            // Inside the box, keeper on (25,6): aiming at (25,4) or (25,8) is a near-certain goal.
            var e = Create();
            Arrange(e, Red2, (Red2, 22, 4));
            var plan = new BasicStrategy(new SeededRandom(3)).PlanTurn(e, Team.Red);
            Assert.That(plan.Action?.Option, Is.EqualTo(CarrierOption.Shot));
            var target = plan.Action.TargetCell.Value;
            Assert.That(target.DistanceTo(new Cell(25, 6)), Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void PassesForwardInsteadOfShootingFromItsOwnHalf()
        {
            var e = Create();
            Arrange(e, Red2, (Red2, 6, 6), (Red3, 14, 6));
            var plan = new BasicStrategy(new SeededRandom(3)).PlanTurn(e, Team.Red);
            Assert.That(plan.Action?.Option, Is.EqualTo(CarrierOption.StraightPass));
            Assert.That(plan.Action.ReceiverId, Is.EqualTo(Red3));
        }

        [Test]
        public void TacklesACarrierItCanReach()
        {
            var e = Create();
            Arrange(e, Red2, (Red2, 12, 6), (Blue2, 13, 6));
            e.SubmitPlan(Team.Red, TeamPlan.Empty());
            var plan = new BasicStrategy(new SeededRandom(3)).PlanTurn(e, Team.Blue);
            var press = plan.Moves.Single(m => m.PlayerId == Blue2);
            Assert.That(press.Target, Is.EqualTo(new Cell(12, 6)));
            Assert.That(press.Tackle, Is.True);
        }

        [Test]
        public void ChasesALooseBall()
        {
            var e = Create();
            Arrange(e, Red2, (Blue2, 18, 6));
            e.ForceLooseBall(new Cell(16, 6));
            e.ForceTurn(Team.Blue);
            var plan = new BasicStrategy(new SeededRandom(3)).PlanTurn(e, Team.Blue);
            Assert.That(plan.Moves.Single(m => m.PlayerId == Blue2).Target, Is.EqualTo(new Cell(17, 6)));
        }

        [Test]
        public void KeeperFollowsTheBallAcrossTheGoal()
        {
            var e = Create();
            Arrange(e, Red2, (Red2, 18, 1));
            e.SubmitPlan(Team.Red, TeamPlan.Empty());
            var plan = new BasicStrategy(new SeededRandom(3)).PlanTurn(e, Team.Blue);
            var keeperMove = plan.Moves.Single(m => m.PlayerId == BlueGk);
            Assert.That(keeperMove.Target.Y, Is.LessThan(6), "keeper shifts toward the ball's side");
        }

        [Test]
        public void DoesNotSeeTheOtherTeamsHiddenPlan()
        {
            // Same board, two different hidden Red plans: Blue's plan must be identical.
            TeamPlan BlueAfter(TeamPlan redPlan)
            {
                var e = Create();
                Arrange(e, Red2, (Red2, 12, 6), (Red3, 16, 3), (Blue2, 18, 6));
                Assert.That(e.SubmitPlan(Team.Red, redPlan).Accepted, Is.True);
                return new BasicStrategy(new SeededRandom(9)).PlanTurn(e, Team.Blue);
            }
            var a = BlueAfter(TeamPlan.Empty());
            var b = BlueAfter(TeamPlan.Empty().WithAction(CarrierAction.StraightPass(Red3)));
            Assert.That(b.Moves.Select(m => (m.PlayerId, m.Target, m.Tackle)), Is.EqualTo(a.Moves.Select(m => (m.PlayerId, m.Target, m.Tackle))));
        }
    }
}
