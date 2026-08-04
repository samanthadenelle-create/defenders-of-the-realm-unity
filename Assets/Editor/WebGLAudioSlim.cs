using System.Collections.Generic;
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
    /// This sets a **WebGL-only platform override** on every **MUSIC** AudioClip:
    /// AAC, DecompressOnLoad, q0.30. Music is where the megabytes are (the three
    /// root-parked tracks alone are ~12 min of runtime); SFX are one-second stings
    /// whose combined size never justified the risk. **SFX are deliberately SKIPPED
    /// — see the WO-682 note at the skip site below.** It does NOT touch the
    /// default/desktop import settings — the Windows and Android builds keep
    /// full-quality audio — and it touches **no texture or mesh** (visual polish is
    /// preserved). Headless entry: <c>DeNelle.Editor.WebGLAudioSlim.Run</c>.
    /// </summary>
    public static class WebGLAudioSlim
    {
        private const string Platform = "WebGL";

        private const float MusicQuality = 0.30f;

        // There is deliberately NO SfxQuality. SFX get no WebGL override at all —
        // see the skip in Run(). The retired value was 0.45f; do not reinstate it
        // without reading Assets/Editor/Regression/SfxWebglAudioRegression.cs.

        /// <summary>
        /// An explicit SFX folder ANYWHERE in the tree. Checked FIRST and wins
        /// outright, because third-party packs name folders after the combat verb —
        /// e.g. <c>Assets/Leohpaz/RPG_Essentials_Free/10_Battle_SFX/</c>, ten ~1s
        /// combat stings whose FOLDER contains "battle".
        /// </summary>
        private static readonly string[] SfxFolderMarkers = { "/sfx/", "_sfx/", "/sounds/" };

        /// <summary>A dedicated music folder anywhere in the tree.</summary>
        private static readonly string[] MusicFolderMarkers = { "/music/" };

        /// <summary>
        /// Roots that are music trees by construction. Their only SFX subtree
        /// (<c>Assets/Audio/SFX/</c>) is already excluded by <see cref="SfxFolderMarkers"/>.
        /// </summary>
        private static readonly string[] MusicRoots = { "assets/audio/" };

        /// <summary>
        /// Classify by ASSET LOCATION, not by filename keywords.
        ///
        /// The old heuristic matched substrings ("theme", "battle", "raid", "world",
        /// "village", "victory", "defeat", "gameover", "title") against the path, and
        /// failed in BOTH directions: three full-length tracks parked at the
        /// <c>Assets/Audio/Resources/</c> root (siege_iron_bastion 5:24,
        /// whispering_pines 3:56, whispering_depths 2:42 — ~12 min, ~40% of music
        /// runtime) matched nothing and encoded at SFX quality, while ten one-second
        /// Leohpaz combat stings under <c>10_Battle_SFX/</c> matched "battle" and
        /// would encode as music. Any new track named outside the keyword list
        /// re-opens the same hole; a folder is a fact, a filename is a guess.
        /// </summary>
        private static bool IsMusic(string lowerPath)
        {
            if (SfxFolderMarkers.Any(lowerPath.Contains)) return false;
            if (MusicFolderMarkers.Any(lowerPath.Contains)) return true;
            if (MusicRoots.Any(lowerPath.StartsWith)) return true;
            return false;
        }

        public static void Run()
        {
            var guids = AssetDatabase.FindAssets("t:AudioClip");
            int changed = 0, skipped = 0, sfxSkipped = 0;
            var music = new List<string>();

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!(AssetImporter.GetAtPath(path) is AudioImporter imp)) { skipped++; continue; }

                // ── SFX ARE SKIPPED ON PURPOSE. DO NOT "FIX" THIS ASYMMETRY. ──────────
                // WO-682: an Sfx clip whose import DIVERGES for WebGL fails FSB decode in
                // the browser — "Loading FSB failed for audio clip \"SwordSwing\"", a
                // db-proven PRODUCTION failure from real players (2026-07-12), plus a
                // 167ms/4000ms frame stall on first use. Every Sfx clip that decodes fine
                // ships the DEFAULT import; SwordSwing, the one clip that carried an
                // override, was the one that broke. That evidence outranks this tool's
                // size argument, so this pass applies to MUSIC ONLY.
                //
                // The oracle that holds the line is
                // Assets/Editor/Regression/SfxWebglAudioRegression.cs — it FAILS the gate
                // if ANY clip under Assets/Resources/Sfx or
                // Assets/_Modules/Audio/Resources/Sfx carries a WebGL override block.
                // Deleting this skip turns the gate RED (it already did once, 2026-08-03:
                // REGRESSION_FAIL naming all 34 Sfx clips this pass had just touched).
                //
                // Music keeps the override for the OPPOSITE, equally evidence-backed
                // reason: music shipped as Vorbis (compressionFormat 1), and mobile
                // Safari / WebKit / the Pi Browser — this project's stated V1 platform —
                // reject Vorbis with "EncodingError: Decoding failed". Music MUST be AAC.
                // Two different clip classes, two different proven failures, one rule each.
                bool isMusic = IsMusic(path.ToLowerInvariant());
                if (!isMusic) { sfxSkipped++; continue; }
                music.Add(path);

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
                s.quality = MusicQuality;

                imp.SetOverrideSampleSettings(Platform, s);
                imp.SaveAndReimport();
                changed++;
            }

            // Print the music set so the classification is VERIFIABLE from the gate log
            // rather than assumed — a clip silently dropping out of this list is the
            // exact failure the keyword heuristic used to hide.
            Debug.Log($"WEBGL_AUDIO_SLIM_MUSIC :: {music.Count} clip(s) classified as MUSIC:\n  " +
                      string.Join("\n  ", music.OrderBy(p => p)));

            Debug.Log($"WEBGL_AUDIO_SLIM_OK :: WebGL audio override set on {changed} MUSIC clip(s) " +
                      $"(DecompressOnLoad + AAC q{MusicQuality:0.00}); {sfxSkipped} SFX clip(s) skipped " +
                      "by design (WO-682 FSB-decode class — see SfxWebglAudioRegression.cs); " +
                      $"{skipped} non-audio-importer asset(s) skipped. " +
                      "Desktop/default settings untouched; no texture/mesh changed.");
        }
    }
}
