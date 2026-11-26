using UnityEditor;

namespace FootballBoard.EditorTools
{
    /// <summary>
    /// Full-screen UI artwork in Assets/Resources/UI (e.g. bg_menu): imported as a sprite, no mipmaps,
    /// at most 2048 pixels wide and compressed, so a large generated image stays light on phones.
    /// </summary>
    public sealed class UiArtImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').StartsWith("Assets/Resources/UI/")) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
        }
    }
}
