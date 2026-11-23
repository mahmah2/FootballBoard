using System.Collections.Generic;
using System.Linq;

namespace FootballBoard.Domain
{
    public enum CarrierOption
    {
        /// <summary>The carrier may move one cell (or stay); no pass or shot this turn.</summary>
        Move,
        StraightPass,
        LongPass,
        Shot
    }

    public sealed class CarrierAction
    {
        public CarrierOption Option { get; set; }
        public int? ReceiverId { get; set; }

        public static CarrierAction Move() => new CarrierAction { Option = CarrierOption.Move };
        public static CarrierAction StraightPass(int receiverId) => new CarrierAction { Option = CarrierOption.StraightPass, ReceiverId = receiverId };
        public static CarrierAction LongPass(int receiverId) => new CarrierAction { Option = CarrierOption.LongPass, ReceiverId = receiverId };
        public static CarrierAction Shot() => new CarrierAction { Option = CarrierOption.Shot };

        internal CarrierAction Copy() => new CarrierAction { Option = Option, ReceiverId = ReceiverId };
    }

    public sealed class MoveOrder
    {
        public int PlayerId { get; set; }
        public Cell Target { get; set; }

        /// <summary>Request a tackle if this player ends the movement on the opposing carrier's square.</summary>
        public bool Tackle { get; set; }

        public MoveOrder() { }

        public MoveOrder(int playerId, Cell target, bool tackle = false)
        {
            PlayerId = playerId;
            Target = target;
            Tackle = tackle;
        }
    }

    /// <summary>
    /// Everything one team decides for one arrangement step: its moves and, for the team whose
    /// turn it is while holding the ball, the carrier option. Players not listed stay put.
    /// </summary>
    public sealed class TeamPlan
    {
        public List<MoveOrder> Moves { get; set; } = new List<MoveOrder>();

        /// <summary>Null means "Move" (the carrier may still move via <see cref="Moves"/>).</summary>
        public CarrierAction Action { get; set; }

        public static TeamPlan Empty() => new TeamPlan();

        public TeamPlan WithMove(int playerId, Cell target, bool tackle = false)
        {
            Moves.Add(new MoveOrder(playerId, target, tackle));
            return this;
        }

        public TeamPlan WithAction(CarrierAction action)
        {
            Action = action;
            return this;
        }

        internal TeamPlan Copy() => new TeamPlan
        {
            Moves = (Moves ?? new List<MoveOrder>()).Where(m => m != null).Select(m => new MoveOrder(m.PlayerId, m.Target, m.Tackle)).ToList(),
            Action = Action?.Copy()
        };
    }
}
