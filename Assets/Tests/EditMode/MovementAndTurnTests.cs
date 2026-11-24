using System.Linq;
using FootballBoard.Domain.Config;
using NUnit.Framework;
using static FootballBoard.Domain.Tests.Scenario;

namespace FootballBoard.Domain.Tests
{
    public class MovementTests
    {
        [Test]
        public void OutfieldPlayersMoveOneCellIncludingDiagonals()
        {
            var e = Create();
            Arrange(e, Red4, (Red2, 10, 6));
            PlayTurn(e, TeamPlan.Empty().WithMove(Red2, new Cell(11, 7)));
            Assert.That(Pos(e, Red2), Is.EqualTo(new Cell(11, 7)));
        }

        [Test]
        public void TwoCellMoveIsRejected()
        {
            var e = Create();
            Arrange(e, Red4, (Red2, 10, 6));
            var r = e.SubmitPlan(Team.Red, TeamPlan.Empty().WithMove(Red2, new Cell(12, 6)));
            Assert.That(r.Accepted, Is.False);
        }

        [Test]
        public void MovingOffThePitchIsRejected()
        {
            var e = Create();
            Arrange(e, Red4, (Red2, 10, 0));
            Assert.That(e.SubmitPlan(Team.Red, TeamPlan.Empty().WithMove(Red2, new Cell(10, -1))).Accepted, Is.False);
        }

        [Test]
        public void GoalkeeperMustStayInsideTheBox()
        {
            var e = Create();
            Arrange(e, Red4, (RedGk, 3, 6));
            Assert.That(e.SubmitPlan(Team.Red, TeamPlan.Empty().WithMove(RedGk, new Cell(4, 6))).Accepted, Is.False);
        }

        [Test]
        [TestCase(1, 6, true)]
        [TestCase(2, 6, true)]
        [TestCase(2, 8, true)]
        [TestCase(3, 6, false)]
        public void GoalkeeperChoosesOneOrTwoCellsEachTurn(int x, int y, bool allowed)
        {
            var e = Create();
            Arrange(e, Red4);
            Assert.That(e.SubmitPlan(Team.Red, TeamPlan.Empty().WithMove(RedGk, new Cell(x, y))).Accepted, Is.EqualTo(allowed));
        }

        [Test]
        public void GoalkeeperRangeComesFromRules()
        {
            var rules = RulesConfig.CreateDefault();
            rules.Goalkeeper.MaxMoveCells = 1;
            var e = Create(rules: rules);
            Arrange(e, Red4);
            Assert.That(e.SubmitPlan(Team.Red, TeamPlan.Empty().WithMove(RedGk, new Cell(2, 6))).Accepted, Is.False);
            Assert.That(e.GetMoveTargets(RedGk), Has.Member(new Cell(1, 6)));
        }

        [Test]
        public void CarrierMovesOnlyWithMoveOption()
        {
            var e = Create();
            Arrange(e, Red2, (Red2, 10, 6), (Red3, 10, 2));
            var withPass = TeamPlan.Empty().WithAction(CarrierAction.StraightPass(Red3)).WithMove(Red2, new Cell(11, 6));
            Assert.That(e.SubmitPlan(Team.Red, withPass).Accepted, Is.False);

            PlayTurn(e, TeamPlan.Empty().WithAction(CarrierAction.Move()).WithMove(Red2, new Cell(11, 6)));
            Assert.That(Pos(e, Red2), Is.EqualTo(new Cell(11, 6)));
            Assert.That(e.State.BallCarrier.Id, Is.EqualTo(Red2));
        }

        [Test]
        public void PassReceiverIsLocked()
        {
            var e = Create();
            Arrange(e, Red2, (Red2, 10, 6), (Red3, 10, 2));
            var plan = TeamPlan.Empty().WithAction(CarrierAction.StraightPass(Red3)).WithMove(Red3, new Cell(11, 2));
            Assert.That(e.SubmitPlan(Team.Red, plan).Accepted, Is.False);
        }

        [Test]
        public void DefendingCarrierMayMoveWithTheBall()
        {
            var e = Create();
            Arrange(e, Blue2, (Blue2, 15, 6));
            PlayTurn(e, TeamPlan.Empty(), TeamPlan.Empty().WithMove(Blue2, new Cell(14, 6)));
            Assert.That(Pos(e, Blue2), Is.EqualTo(new Cell(14, 6)));
            Assert.That(e.State.BallCarrier.Id, Is.EqualTo(Blue2));
        }

        [Test]
        public void DefendingCarrierLockCanBeEnabledInConfig()
        {
            var rules = RulesConfig.CreateDefault();
            rules.Carrier.CanMoveOnOpponentTurn = false;
            var e = Create(rules: rules);
            Arrange(e, Blue2, (Blue2, 15, 6));
            e.SubmitPlan(Team.Red, TeamPlan.Empty());
            Assert.That(e.SubmitPlan(Team.Blue, TeamPlan.Empty().WithMove(Blue2, new Cell(14, 6))).Accepted, Is.False);
        }

        [Test]
        public void TwoPlayersMayShareASquare()
        {
            var e = Create();
            Arrange(e, Red4, (Red2, 11, 6), (Blue2, 13, 6));
            PlayTurn(e, TeamPlan.Empty().WithMove(Red2, new Cell(12, 6)), TeamPlan.Empty().WithMove(Blue2, new Cell(12, 6)));
            Assert.That(e.State.OccupancyAt(new Cell(12, 6)), Is.EqualTo(2));
        }

        [Test]
        public void MovesThatWouldOverfillASquareAreCancelled()
        {
            var e = Create();
            Arrange(e, Red4, (Red2, 12, 6), (Red3, 11, 6), (Blue2, 13, 6));
            var r = PlayTurn(e, TeamPlan.Empty().WithMove(Red3, new Cell(12, 6)), TeamPlan.Empty().WithMove(Blue2, new Cell(12, 6)));
            Assert.That(Pos(e, Red3), Is.EqualTo(new Cell(11, 6)));
            Assert.That(Pos(e, Blue2), Is.EqualTo(new Cell(13, 6)));
            Assert.That(r.Events.OfType<MoveCancelled>().Count(), Is.EqualTo(2));
            Assert.That(e.State.Players.GroupBy(p => p.Position).Max(g => g.Count()), Is.LessThanOrEqualTo(2));
        }

        [Test]
        public void PlayersMaySwapSquares()
        {
            var e = Create();
            Arrange(e, Red4, (Red2, 12, 6), (Blue2, 13, 6));
            PlayTurn(e, TeamPlan.Empty().WithMove(Red2, new Cell(13, 6)), TeamPlan.Empty().WithMove(Blue2, new Cell(12, 6)));
            Assert.That(Pos(e, Red2), Is.EqualTo(new Cell(13, 6)));
            Assert.That(Pos(e, Blue2), Is.EqualTo(new Cell(12, 6)));
        }

        [Test]
        public void RejectedPlanLeavesStateUnchanged()
        {
            var e = Create();
            Arrange(e, Red4, (Red2, 10, 6));
            var before = e.State.Players.Select(p => p.Position).ToList();
            var r = e.SubmitPlan(Team.Red, TeamPlan.Empty().WithMove(Red2, new Cell(11, 6)).WithMove(Red3, new Cell(9, 9)));
            Assert.That(r.Accepted, Is.False);
            Assert.That(e.State.Players.Select(p => p.Position), Is.EqualTo(before));
            Assert.That(e.State.PlanningTeam, Is.EqualTo(Team.Red));
            Assert.That(e.State.ElapsedSeconds, Is.EqualTo(0));
        }

        [Test]
        public void SetupAllowsOnlyOwnHalf()
        {
            var e = MatchEngine.Create(null, null, new ScriptedRandom());
            Assert.That(e.PlacePlayer(Red2, new Cell(12, 3)).Accepted, Is.True);
            Assert.That(e.PlacePlayer(Red2, new Cell(13, 3)).Accepted, Is.False);
            Assert.That(e.PlacePlayer(RedGk, new Cell(2, 6)).Accepted, Is.False);
        }
    }

    public class TurnAndClockTests
    {
        [Test]
        public void CoinTossPicksKickoffTeamWhoPlaysFirst()
        {
            var e = MatchEngine.Create(null, null, new ScriptedRandom(0.9));
            var r = e.StartMatch();
            Assert.That(Single<CoinTossed>(r).KickoffTeam, Is.EqualTo(Team.Blue));
            Assert.That(e.State.TurnTeam, Is.EqualTo(Team.Blue));
            Assert.That(e.State.BallCarrier.Position, Is.EqualTo(e.State.Pitch.KickoffSpot(Team.Blue)));
        }

        [Test]
        public void TeamWithTheBallKeepsTheTurn()
        {
            // Planning order R B, R B ... while Red keeps the ball.
            var e = Create();
            Assert.That(e.SubmitPlan(Team.Blue, TeamPlan.Empty()).Accepted, Is.False, "turn team plans first");
            PlayTurn(e);
            Assert.That(e.State.TurnTeam, Is.EqualTo(Team.Red));
            PlayTurn(e);
            Assert.That(e.State.TurnTeam, Is.EqualTo(Team.Red));
            Assert.That(e.State.TurnNumber, Is.EqualTo(3));
        }

        [Test]
        public void TurnPassesToTheTeamThatWinsTheBall()
        {
            var e = Create(new ScriptedRandom(0.3));
            Arrange(e, Red2, (Red2, 12, 6), (Blue2, 13, 6));
            PlayTurn(e, null, TeamPlan.Empty().WithMove(Blue2, new Cell(12, 6), tackle: true));
            Assert.That(e.State.PossessionTeam, Is.EqualTo(Team.Blue));
            Assert.That(e.State.TurnTeam, Is.EqualTo(Team.Blue));
            Assert.That(e.State.PlanningTeam, Is.EqualTo(Team.Blue));
        }

        [Test]
        public void LooseBallDoesNotSwitchTheTurn()
        {
            var e = Create();
            Arrange(e, Red2);
            e.ForceLooseBall(new Cell(12, 3));
            PlayTurn(e);
            Assert.That(e.State.TurnTeam, Is.EqualTo(Team.Red));
            Assert.That(e.State.PlanningTeam, Is.EqualTo(Team.Red));
        }

        [Test]
        public void StrictAlternationCanBeRestoredInConfig()
        {
            var rules = RulesConfig.CreateDefault();
            rules.Turns.PossessionTeamPlaysFirst = false;
            var e = Create(rules: rules);
            PlayTurn(e);
            Assert.That(e.State.TurnTeam, Is.EqualTo(Team.Blue));
            PlayTurn(e);
            Assert.That(e.State.TurnTeam, Is.EqualTo(Team.Red));
        }

        [Test]
        public void TenMinuteMatchIsTenTeamTurns()
        {
            var e = Create(rules: NoGoldenGoal());
            var turns = 0;
            while (e.State.Phase == MatchPhase.Planning)
            {
                PlayTurn(e);
                turns++;
            }
            Assert.That(turns, Is.EqualTo(10));
            Assert.That(e.State.ElapsedSeconds, Is.EqualTo(600));
            Assert.That(e.State.Phase, Is.EqualTo(MatchPhase.MatchOver));
            Assert.That(e.State.Result.IsDraw, Is.True);
        }

        [Test]
        public void NinetyMinuteMatchIsNinetyTeamTurns()
        {
            var e = Create(rules: NoGoldenGoal(), settings: new MatchSettings { DurationMinutes = 90 });
            var turns = 0;
            while (e.State.Phase == MatchPhase.Planning)
            {
                PlayTurn(e);
                turns++;
            }
            Assert.That(turns, Is.EqualTo(90));
        }

        [Test]
        public void DrawAsksForTieDecisionAndBothMustAgreeOnGoldenGoal()
        {
            var e = Create();
            e.ForceElapsed(540);
            var r = PlayTurn(e);
            Assert.That(r.Events.OfType<TieDecisionRequired>().Count(), Is.EqualTo(1));
            Assert.That(e.State.Phase, Is.EqualTo(MatchPhase.AwaitingTieDecision));

            Assert.That(e.SubmitTieChoice(Team.Red, TieChoice.GoldenGoal).Accepted, Is.True);
            e.SubmitTieChoice(Team.Blue, TieChoice.Draw);
            Assert.That(e.State.Phase, Is.EqualTo(MatchPhase.MatchOver));
            Assert.That(e.State.Result.IsDraw, Is.True);
        }

        [Test]
        public void GoldenGoalEndsMatchOnNextGoal()
        {
            var e = Create(new ScriptedRandom(0.1));
            e.ForceElapsed(540);
            PlayTurn(e);
            e.SubmitTieChoice(Team.Red, TieChoice.GoldenGoal);
            var r = e.SubmitTieChoice(Team.Blue, TieChoice.GoldenGoal);
            Assert.That(r.Events.OfType<GoldenGoalStarted>().Count(), Is.EqualTo(1));
            Assert.That(e.State.Phase, Is.EqualTo(MatchPhase.Planning));

            // Blue's turn (Red played last). Play an empty Blue turn, then Red scores.
            PlayTurn(e);
            Arrange(e, Red2, (Red2, 22, 4));
            PlayTurn(e, TeamPlan.Empty().WithAction(CarrierAction.Shot()));
            Assert.That(e.State.Phase, Is.EqualTo(MatchPhase.MatchOver));
            Assert.That(e.State.Result.Winner, Is.EqualTo(Team.Red));
            Assert.That(e.State.Result.DecidedByGoldenGoal, Is.True);
        }

        [Test]
        public void PauseBlocksCommands()
        {
            var e = Create();
            e.Pause();
            Assert.That(e.SubmitPlan(Team.Red, TeamPlan.Empty()).Accepted, Is.False);
            e.Resume();
            Assert.That(e.SubmitPlan(Team.Red, TeamPlan.Empty()).Accepted, Is.True);
        }

        [Test]
        public void SameSeedGivesSameMatch()
        {
            string Run()
            {
                var e = MatchEngine.Create(null, new MatchSettings { Seed = 42 });
                e.StartMatch();
                var log = new System.Text.StringBuilder();
                while (e.State.Phase == MatchPhase.Planning)
                {
                    var turn = e.State.TurnTeam;
                    var carrier = e.State.BallCarrier;
                    var plan = TeamPlan.Empty();
                    if (carrier != null && carrier.Team == turn)
                    {
                        var mate = e.State.TeamPlayers(turn).First(p => !p.IsGoalkeeper && p.Id != carrier.Id && p.Position != carrier.Position);
                        plan.WithAction(CarrierAction.LongPass(mate.Id));
                        if (e.ValidatePlan(turn, plan) != null) plan = TeamPlan.Empty();
                    }
                    foreach (var ev in PlayTurn(e, plan).Events) log.Append(ev.GetType().Name).Append(';');
                    if (e.State.Phase == MatchPhase.AwaitingTieDecision)
                    {
                        e.SubmitTieChoice(Team.Red, TieChoice.Draw);
                        e.SubmitTieChoice(Team.Blue, TieChoice.Draw);
                    }
                }
                log.Append(e.State.BallPosition);
                return log.ToString();
            }

            Assert.That(Run(), Is.EqualTo(Run()));
        }

        private static RulesConfig NoGoldenGoal()
        {
            var rules = RulesConfig.CreateDefault();
            rules.Ties.OfferGoldenGoal = false;
            return rules;
        }
    }
}
