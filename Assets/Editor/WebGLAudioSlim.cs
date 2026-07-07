using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>
    /// WO-(Pi/Vercel) build-slim pass. Vercel caps any single file at 100 MB; the
    /// Unity WebGL <c>.data</c> was 126 MB, dominated by ~52 MB of audio that is
    /// already in a compressed codec (so the final Brotli pass can't shrink it).
    ///
    /// This sets a **WebGL-only platform override** on every AudioClip to Vorbis at
    /// a modest quality (music streamed, SFX compressed-in-memory). It does NOT
    /// touch the default/desktop import settings — the Windows build keeps
    /// full-quality audio — and it touches **no texture or mesh** (visual polish is
    /// preserved). Headless entry: <c>DeNelle.Editor.WebGLAudioSlim.Run</c>.
    /// </summary>
    public static class WebGLAudioSlim
    {
        private const string Platform = "WebGL";

        public static void Run()
        {
            var guids = AssetDatabase.FindAssets("t:AudioClip");
            int changed = 0, skipped = 0;

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is AudioImporter imp)) { skipped++; continue; }

                var lower = path.ToLowerInvariant();
                bool isMusic = lower.Contains("/music/") || lower.Contains("theme")
                            || lower.Contains("battle") || lower.Contains("raid")
                            || lower.Contains("world") || lower.Contains("village")
                            || lower.Contains("victory") || lower.Contains("defeat")
                            || lower.Contains("gameover") || lower.Contains("title");

                // Size comes from the low-bitrate RE-ENCODE, not loadType — so DecompressOnLoad
                // keeps the size win (CompressedInMemory was the FMOD-illegal "Cannot create
                // FMOD::Sound" class). Codec = AAC, NOT Vorbis: WebGL clips decode via the
                // browser's WebAudio decodeAudioData, and mobile Safari / WebKit (incl. the Pi
                // Browser) REJECTS Vorbis with "EncodingError: Decoding failed" (owner mobile
                // capture 2026-07-06, first fired at the arena warp where echo_theme/WeaponDraw
                // first play). AAC decodes on every mobile browser at comparable size.
                var s = imp.GetOverrideSampleSettings(Platform);
                s.compressionFormat = AudioCompressionFormat.AAC;
                s.loadType = AudioClipLoadType.DecompressOnLoad;
                s.quality = isMusic ? 0.30f : 0.45f;

                imp.SetOverrideSampleSettings(Platform, s);
                imp.SaveAndReimport();
                changed++;
            }

            Debug.Log($"WEBGL_AUDIO_SLIM_OK :: WebGL audio override set on {changed} AudioClip(s) " +
                      $"(DecompressOnLoad + AAC, music q0.30 / sfx q0.45); skipped {skipped}. " +
                      "Desktop/default settings untouched; no texture/mesh changed.");
        }
    }
}
