using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using FootballBoard.Domain.Config;

namespace FootballBoard.Domain
{
    /// <summary>Kick-off positions. Index 0 is the goalkeeper; index i belongs to shirt number i+1.</summary>
    internal static class Formation
    {
        public static List<Cell> For(Team team, RulesConfig rules, Pitch pitch, int count)
        {
            var red = RedLayout(rules, pitch, count);
            return team == Team.Red ? red : red.Select(pitch.MirrorForBlue).ToList();
        }

        private static List<Cell> RedLayout(RulesConfig rules, Pitch pitch, int count)
        {
            var key = count.ToString(CultureInfo.InvariantCulture);
            if (rules.Teams.Formations != null && rules.Teams.Formations.TryGetValue(key, out var spots))
                return spots.Select(s => new Cell(s.Column, pitch.CentreRow + s.RowOffset)).ToList();

            // Automatic layout: keeper in goal, then a defensive line just outside the box
            // and a midfield line a few columns behind the centre line.
            var half = pitch.Columns / 2;
            var defenceX = Math.Min(pitch.BoxDepth + 1, half - 2);
            var midfieldX = Math.Max(defenceX + 1, half - 3);
            var outfield = count - 1;
            var defenders = (outfield + 1) / 2;

            var result = new List<Cell> { new Cell(0, pitch.CentreRow) };
            AddLine(result, defenceX, defenders, pitch.Rows);
            AddLine(result, midfieldX, outfield - defenders, pitch.Rows);
            return result;
        }

        private static void AddLine(List<Cell> cells, int x, int count, int rows)
        {
            for (var i = 0; i < count; i++)
            {
                var y = (int)Math.Round((i + 1) * (rows - 1) / (double)(count + 1), MidpointRounding.AwayFromZero);
                cells.Add(new Cell(x, y));
            }
        }
    }
}
