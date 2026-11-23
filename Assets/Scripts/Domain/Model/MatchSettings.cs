using System.Collections.Generic;
using FootballBoard.Domain.Config;

namespace FootballBoard.Domain
{
    /// <summary>Choices made on the match-setup screen. Null values fall back to rules.json defaults.</summary>
    public sealed class MatchSettings
    {
        public GameMode Mode { get; set; } = GameMode.HumanVsHuman;

        /// <summary>The human's team in Human vs Computer mode.</summary>
        public Team HumanTeam { get; set; } = Team.Red;

        public int? DurationMinutes { get; set; }
        public int? GoalkeeperMoveCells { get; set; }
        public int? PlayersPerTeam { get; set; }

        /// <summary>Random seed. Null picks one from the clock; set it for reproducible matches.</summary>
        public ulong? Seed { get; set; }

        public IReadOnlyList<string> Validate(RulesConfig rules)
        {
            var e = new List<string>();
            if (DurationMinutes.HasValue && !rules.Clock.PresetMinutes.Contains(DurationMinutes.Value))
                e.Add($"Match duration {DurationMinutes} is not one of the presets ({string.Join(", ", rules.Clock.PresetMinutes)}).");
            if (GoalkeeperMoveCells.HasValue && !rules.Goalkeeper.AllowedMoveCells.Contains(GoalkeeperMoveCells.Value))
                e.Add($"Goalkeeper movement {GoalkeeperMoveCells} is not allowed ({string.Join(", ", rules.Goalkeeper.AllowedMoveCells)}).");
            if (PlayersPerTeam.HasValue)
                RulesConfig.CheckRange(e, "Players per team", PlayersPerTeam.Value, TeamRules.MinimumPlayers, TeamRules.MaximumPlayers);
            return e;
        }
    }
}
