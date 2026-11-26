using UnityEngine;

namespace FootballBoard.Presentation
{
    /// <summary>Draws a white speaker icon (with sound waves, or crossed out) into a small sprite.</summary>
    public static class SpeakerIcon
    {
        private const int Size = 96;

        public static Sprite Create(bool on)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = on ? "SpeakerOn" : "SpeakerOff", filterMode = FilterMode.Bilinear };
            var pixels = new Color32[Size * Size];
            const int ss = 4; // supersampling for smooth edges
            for (var y = 0; y < Size; y++)
            for (var x = 0; x < Size; x++)
            {
                var hits = 0;
                for (var sy = 0; sy < ss; sy++)
                for (var sx = 0; sx < ss; sx++)
                {
                    var u = (x + (sx + 0.5f) / ss) / Size;   // 0..1, left to right
                    var v = (y + (sy + 0.5f) / ss) / Size;   // 0..1, bottom to top
                    if (Inside(u, v, on)) hits++;
                }
                pixels[y * Size + x] = new Color32(255, 255, 255, (byte)(255 * hits / (ss * ss)));
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite _circle;

        /// <summary>A white anti-aliased disc (slider handle).</summary>
        public static Sprite Circle()
        {
            if (_circle != null) return _circle;
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Circle", filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var d = Mathf.Sqrt((x + 0.5f - size / 2f) * (x + 0.5f - size / 2f) + (y + 0.5f - size / 2f) * (y + 0.5f - size / 2f));
                var a = Mathf.Clamp01(size / 2f - 1f - d + 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return _circle = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite _down;

        /// <summary>A white triangle pointing down (dropdown marker).</summary>
        public static Sprite DownTriangle()
        {
            if (_down != null) return _down;
            const int size = 64, ss = 4;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "DownTriangle", filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var hits = 0;
                for (var sy = 0; sy < ss; sy++)
                for (var sx = 0; sx < ss; sx++)
                {
                    var u = (x + (sx + 0.5f) / ss) / size;
                    var v = (y + (sy + 0.5f) / ss) / size;
                    // Apex at the bottom centre, flat top edge.
                    var halfWidth = (v - 0.18f) / (0.82f - 0.18f) * 0.42f;
                    if (v >= 0.18f && v <= 0.82f && Mathf.Abs(u - 0.5f) <= halfWidth) hits++;
                }
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(255 * hits / (ss * ss)));
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return _down = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static bool Inside(float u, float v, bool on)
        {
            var dy = Mathf.Abs(v - 0.5f);
            // Speaker body (a small box) and its cone (widening to the right).
            if (u >= 0.08f && u <= 0.27f && dy <= 0.13f) return true;
            if (u > 0.27f && u <= 0.5f && dy <= 0.13f + (u - 0.27f) * 1.25f) return true;

            if (on)
            {
                // Two sound waves: arcs centred on the cone's mouth.
                float cx = 0.5f, cy = 0.5f;
                var d = Mathf.Sqrt((u - cx) * (u - cx) + (v - cy) * (v - cy));
                var angle = Mathf.Atan2(v - cy, u - cx) * Mathf.Rad2Deg;
                if (Mathf.Abs(angle) <= 50f && u > cx)
                    if (Mathf.Abs(d - 0.17f) <= 0.04f || Mathf.Abs(d - 0.33f) <= 0.04f) return true;
                return false;
            }

            // Muted: an X to the right of the cone.
            float x0 = 0.6f, x1 = 0.9f, y0 = 0.35f, y1 = 0.65f;
            if (u < x0 - 0.03f || u > x1 + 0.03f || v < y0 - 0.03f || v > y1 + 0.03f) return false;
            var t = (u - x0) / (x1 - x0);
            var onDiag1 = Mathf.Abs(v - (y0 + t * (y1 - y0))) <= 0.045f;
            var onDiag2 = Mathf.Abs(v - (y1 - t * (y1 - y0))) <= 0.045f;
            return onDiag1 || onDiag2;
        }
    }
}
