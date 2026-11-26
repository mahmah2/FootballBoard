using UnityEditor;
using UnityEngine;

namespace FootballBoard.EditorTools
{
    /// <summary>
    /// Import settings for replacement audio in Resources/Audio and Resources/Music. Music
    /// ("music_*") streams from disk instead of being unpacked into memory (a 3-minute stereo track
    /// would otherwise take about 30 MB of RAM on a phone). Sound effects ("sfx_*") are small and
    /// decompress on load so they play instantly.
    /// </summary>
    public sealed class AudioImportRules : AssetPostprocessor
    {
        private void OnPreprocessAudio()
        {
            var path = assetPath.Replace('\\', '/');
            if (!path.StartsWith("Assets/Resources/Audio/") && !path.StartsWith("Assets/Resources/Music/")) return;

            var importer = (AudioImporter)assetImporter;
            var name = System.IO.Path.GetFileNameWithoutExtension(path);
            var settings = importer.defaultSampleSettings;
            if (name.StartsWith("music_"))
            {
                settings.loadType = AudioClipLoadType.Streaming;
                settings.compressionFormat = AudioCompressionFormat.Vorbis;
                settings.quality = 0.6f;
                importer.loadInBackground = true;
            }
            else
            {
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.ADPCM;
                importer.forceToMono = true;
            }
            importer.defaultSampleSettings = settings;
        }
    }
}
