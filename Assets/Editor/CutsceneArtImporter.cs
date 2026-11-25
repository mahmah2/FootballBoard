using UnityEditor;
using UnityEngine;

namespace FootballBoard.EditorTools
{
    /// <summary>
    /// Import rules for cutscene art in <c>Assets/Resources/Cutscenes/</c>:
    /// imported as uncompressed-quality sprites without mipmaps, and, when the image sits on a flat
    /// saturated colour (magenta #FF00FF recommended, or green #00FF00), that colour is keyed out to
    /// transparency with soft edges and the colour fringe removed. Images that already have real
    /// transparency, and backgrounds (names starting with "bg_"), are left untouched.
    /// </summary>
    public sealed class CutsceneArtImporter : AssetPostprocessor
    {
        private const string Folder = "Assets/Resources/Cutscenes/";
        private const float Transparent = 0.18f; // colour distance (0..~1.7) treated as background
        private const float Opaque = 0.42f;      // distance from which a pixel is fully kept

        private bool IsCutsceneArt => assetPath.Replace('\\', '/').StartsWith(Folder);
        private bool IsBackground => System.IO.Path.GetFileName(assetPath).StartsWith("bg_");

        // Bump when the import rules change so Unity re-imports the art.
        public override uint GetVersion() => 2;

        private void OnPreprocessTexture()
        {
            if (!IsCutsceneArt) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = !IsBackground;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            if (IsBackground) return;

            // A PNG saved without an alpha channel would otherwise get a format with no alpha (DXT1 /
            // ETC2 RGB), and the keyed-out backdrop would draw black. Force formats that keep alpha.
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            ForceAlphaFormat(importer, "Standalone", TextureImporterFormat.BC7);
            ForceAlphaFormat(importer, "Android", TextureImporterFormat.ASTC_6x6);
            ForceAlphaFormat(importer, "iPhone", TextureImporterFormat.ASTC_6x6);
        }

        private static void ForceAlphaFormat(TextureImporter importer, string platform, TextureImporterFormat format)
        {
            var settings = importer.GetPlatformTextureSettings(platform);
            settings.overridden = true;
            settings.maxTextureSize = 2048;
            settings.format = format;
            settings.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SetPlatformTextureSettings(settings);
        }

        private void OnPostprocessTexture(Texture2D texture)
        {
            if (!IsCutsceneArt || IsBackground) return;
            var pixels = texture.GetPixels();
            int w = texture.width, h = texture.height;
            var corners = new[] { pixels[0], pixels[w - 1], pixels[(h - 1) * w], pixels[h * w - 1] };

            // Already transparent, or no flat saturated backdrop: nothing to key.
            foreach (var c in corners) if (c.a < 0.5f) return;
            var key = (corners[0] + corners[1] + corners[2] + corners[3]) / 4f;
            foreach (var c in corners) if (Distance(c, key) > 0.12f) return;
            if (Mathf.Max(key.r, key.g, key.b) - Mathf.Min(key.r, key.g, key.b) < 0.6f)
            {
                Debug.LogWarning($"[Cutscene art] {assetPath}: background is not a flat saturated colour; use magenta #FF00FF. Left as is.");
                return;
            }

            for (var i = 0; i < pixels.Length; i++)
            {
                var c = pixels[i];
                var alpha = Mathf.InverseLerp(Transparent, Opaque, Distance(c, key));
                if (alpha <= 0f) { pixels[i] = new Color(0, 0, 0, 0); continue; }
                if (alpha < 1f)
                {
                    // Un-mix the backdrop from edge pixels so no magenta/green fringe remains.
                    c = new Color(
                        Mathf.Clamp01((c.r - key.r * (1 - alpha)) / alpha),
                        Mathf.Clamp01((c.g - key.g * (1 - alpha)) / alpha),
                        Mathf.Clamp01((c.b - key.b * (1 - alpha)) / alpha));
                }
                c.a = alpha;
                pixels[i] = c;
            }
            texture.SetPixels(pixels);
            texture.Apply();
            Debug.Log($"[Cutscene art] {assetPath}: keyed out background colour #{ColorUtility.ToHtmlStringRGB(key)}.");
        }

        private static float Distance(Color a, Color b)
        {
            float dr = a.r - b.r, dg = a.g - b.g, db = a.b - b.b;
            return Mathf.Sqrt(dr * dr + dg * dg + db * db);
        }
    }
}
