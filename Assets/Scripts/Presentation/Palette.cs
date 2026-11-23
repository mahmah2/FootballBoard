using UnityEngine;

namespace FootballBoard.Presentation
{
    /// <summary>The only place colours are defined (see docs/ART_BIBLE.md).</summary>
    public static class Palette
    {
        public static readonly Color Room = Hex(0x211A15);
        public static readonly Color Table = Hex(0x3B2A20);
        public static readonly Color TableEdge = Hex(0x4E3828);
        public static readonly Color Turf = Hex(0x3F8F4E);
        public static readonly Color TurfStripe = Hex(0x4A9C59);
        public static readonly Color GridLine = Hex(0x357A43);
        public static readonly Color Chalk = Hex(0xEEF0E6);
        public static readonly Color RedPrimary = Hex(0xD94A3D);
        public static readonly Color RedSecondary = Hex(0xF6E3D3);
        public static readonly Color BluePrimary = Hex(0x2F6BD6);
        public static readonly Color BlueSecondary = Hex(0xE3ECF8);
        public static readonly Color RedKeeper = Hex(0xF08A24);
        public static readonly Color BlueKeeper = Hex(0x1FB5A8);
        public static readonly Color Skin = Hex(0xE7B98F);
        public static readonly Color Shadow = Hex(0x1F2A33);
        public static readonly Color Accent = Hex(0xFFD23F);
        public static readonly Color Warning = Hex(0xFF6B57);

        // Lighting (from the mood line)
        public static readonly Color KeyLight = Hex(0xFFE8C8);
        public static readonly Color AmbientSky = Hex(0x6B7A8F);
        public static readonly Color AmbientEquator = Hex(0x4A4640);
        public static readonly Color AmbientGround = Hex(0x2B2019);

        // UI
        public static readonly Color PanelDark = WithAlpha(Hex(0x1F2A33), 0.88f);
        public static readonly Color PanelOpaque = Hex(0x172028);

        public static Color TeamPrimary(Domain.Team team) => team == Domain.Team.Red ? RedPrimary : BluePrimary;
        public static Color TeamSecondary(Domain.Team team) => team == Domain.Team.Red ? RedSecondary : BlueSecondary;
        public static Color Keeper(Domain.Team team) => team == Domain.Team.Red ? RedKeeper : BlueKeeper;

        public static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);

        private static Color Hex(int rgb) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }
}
