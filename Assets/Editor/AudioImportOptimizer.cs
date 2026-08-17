// =============================================================================
// AudioImportOptimizer — fixes the AUDIO IMPORT SETTINGS for the mobile/Android
// build. Build-size AND runtime-RAM win, with NO asset migration and NO gameplay
// code change (Lane 5, Deliverable 1).
// -----------------------------------------------------------------------------
// MEASURED PROBLEM (read off the .meta files, 2026-08-17 — do not re-derive):
//   • 132 audio files / 143.3 MB total; 111.4 MB of it sits under a Resources/
//     folder across EIGHT Resources roots, so it is FORCE-INCLUDED in every build.
//   • EVERY ONE of the 54 Resources clips imports as `loadType: 0`
//     (DecompressOnLoad) — including twenty music beds of 39-320 s.
//   • There is NO Android platform override on ANY audio asset. The only override
//     block present is `platformSettingOverrides: WebGL:` (compressionFormat 7 =
//     AAC, quality 0.3), and it exists on the music files only. Android therefore
//     inherits `defaultSettings` verbatim:
//         loadType: 0 (DecompressOnLoad) / compressionFormat: 1 (Vorbis) / quality: 1
//     i.e. Vorbis at MAXIMUM quality, fully decompressed into RAM.
//
//   The RAM consequence is arithmetic, not an estimate. DecompressOnLoad holds
//   16-bit PCM resident: length x rate x channels x 2 bytes. Summed over the 54
//   Resources clips that is ~548 MB, of which ~533 MB is the twenty music beds
//   (heartwood_collapse.wav alone = 173.8 s x 48 kHz x 2 ch x 2 B = 31.8 MB source
//   -> ~31.8 MB resident PCM). On a phone that is the whole budget.
//
// WHAT THIS TOOL DOES — import settings ONLY. It never re-encodes, moves, renames
// or deletes a source file, and it never hand-edits a .meta: every write goes
// through AudioImporter under Unity's control (canon §3 / Lane 5 constraint).
//
// CLASSIFICATION (length is read from the imported AudioClip, NEVER guessed from
// the filename — a "Swords_Clash.mp3" that is 3.0 s and a "FootstepsWalk.mp3"
// that is 6.9 s both fall out of the length read, not the name):
//
//   MUSIC   — >= 15 s AND not under an Sfx/Voice folder
//             -> Streaming + Vorbis q 0.5 + preloadAudioData false, stereo KEPT.
//   MID     — 2 s .. 15 s  (or any >= 15 s clip that lives under Sfx/Voice, which
//             is the PlayOneShot conflict class — see below)
//             -> CompressedInMemory + Vorbis q 0.5 + forceToMono.
//   SHORT   — < 2 s
//             -> DecompressOnLoad (correct: they must fire instantly) + ADPCM
//                + forceToMono + preloadAudioData TRUE.
//
// STEREO CALL (music): music stays STEREO. The saving from forceToMono on a
// Streaming clip is bytes-on-disk only — a streamed clip costs no resident PCM
// either way — and the game crossfades two music beds through a stereo mixer
// (MusicDirector's A/B pair, Assets/_Modules/Audio/MusicDirector.cs:341/354).
// Collapsing the beds to mono would flatten every crossfade for a saving already
// dwarfed by the q1.0 -> q0.5 step. SFX are the opposite case: they are fired 2D
// through a shared voice pool (AudioService.CreateChildSource sets
// spatialBlend = 0), so a mono SFX is perceptually identical and exactly half the
// data. Hence mono for SFX, stereo for music.
//
// ⚠ THE STREAMING / PlayOneShot CONSTRAINT (why the Sfx/Voice folder rule exists)
//   A Streaming clip cannot be fired reliably through AudioSource.PlayOneShot —
//   there is no decoded buffer to start from, so the shot is late or silent. Every
//   Resources SFX key in this project is ultimately played by
//   `AudioService.PlayOneShotOn` -> `voice.PlayOneShot(clip, volume)`
//   (Assets/_Modules/Audio/AudioService.cs:696), reached from
//   CoreServices.Audio?.PlaySfx / PlayUiSfx / PlayVoice / PlaySfxAtPosition.
//   Therefore NOTHING under an Sfx/ or Voice/ folder is ever classified Streaming,
//   regardless of length, and a >= 15 s clip found there is REPORTED AS A CONFLICT
//   and demoted to CompressedInMemory instead.
//
//   Verified state of that conflict set today (grep of every AudioSource.PlayOneShot
//   call site against every Resources audio key):
//     • The longest clip reachable by PlayOneShot is
//       Assets/_Modules/Audio/Resources/Sfx/FootstepsWalk.mp3 at 6.9 s — and even
//       that one is used as a LOOPING `.clip` by
//       Assets/_Modules/Village/Hero/HeroLocomotion.cs:910-916, not as a one-shot.
//     • Every music clip is played by assignment + Play(), never PlayOneShot:
//       Assets/_Modules/Audio/MusicDirector.cs:341 `fadeIn.clip = clip;` / :354
//       `fadeIn.Play();`  and Assets/_Modules/Village/Audio/BattleMusicManager.cs:530
//       (Resources.Load, then the same assign-and-play bed), and
//       Assets/_Modules/Village/Audio/HeartwoodAmbientController.cs:305-307.
//     • => ZERO actual conflicts at the time of writing. The two structural risks,
//       both currently harmless because the clips DO NOT EXIST on disk, are:
//         - Assets/_Modules/Village/Audio/HeartwoodAmbientController.cs:338
//           `_stinger.PlayOneShot(clip, _stingerVolume)` fed by :169-170
//           (`Audio/Sfx/Heart_Hit`, `Audio/Sfx/Heart_Fall`);
//         - Assets/_Modules/Village/Audio/TowerVoiceController.cs:190
//           `_source.PlayOneShot(clip, _volume)` fed by :124-127
//           (`Audio/Voice/HeartFailing[_1.._3]`).
//       Both live under an Sfx/ or Voice/ path segment, so the folder rule already
//       protects them the day someone drops a long file there.
//
// PLATFORM SCOPE — Android override + a sensible default, WebGL LEFT ALONE.
//   • The Android override is written explicitly (SetOverrideSampleSettings
//     ("Android", ...)), because Android has no override today and is the primary
//     ship target (Solana dApp store).
//   • defaultSettings gets the same classification, which is correct for
//     Standalone too.
//   • The existing WebGL override block is NEVER read-modified-written. WebGL does
//     not support Streaming at all, and its AAC q0.3 block is already a deliberate
//     tuning; leaving it in place is what stops the new Streaming default from
//     becoming collateral damage on the web build.
//   • sampleRateSetting / sampleRateOverride are deliberately NOT touched. A
//     48k -> 44.1k resample is a further win but it is a fidelity decision, not a
//     mechanical one, and it is not needed for the headline saving.
//
// IDEMPOTENT: every field is compared before writing; a clip already at target
// settings is counted "already" and never reimported. Re-running changes nothing.
//
// DRY RUN: `ReportOnly()` logs the identical per-clip table + totals and writes
// NOTHING. Run it first, read the table, then run `Run()`.
//
// Run (menu): Defenders > Build > Report Audio Import Settings (dry run)
//             Defenders > Build > Optimize Audio Import Settings
//   headless: -executeMethod DeNelle.Editor.AudioImportOptimizer.ReportOnly
//             -executeMethod DeNelle.Editor.AudioImportOptimizer.Run
// EDITOR-ONLY. Does not run gameplay, does not commit, does not touch data JSON.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>
    /// Classifies every AudioClip under <c>Assets/</c> by its REAL length and applies
    /// the matching import settings as an Android platform override plus a sensible
    /// default. Idempotent, dry-run capable, import-settings only (never re-encodes
    /// or moves a source file).
    /// </summary>
    public static class AudioImportOptimizer
    {
        // ── Classification thresholds ────────────────────────────────────────

        /// <summary>Clips at or above this length are music-class (Streaming) unless a
        /// PlayOneShot folder rule forbids it.</summary>
        internal const float MusicSeconds = 15f;

        /// <summary>Clips below this length are short-SFX class (DecompressOnLoad + ADPCM).</summary>
        internal const float ShortSfxSeconds = 2f;

        /// <summary>Vorbis quality used for both music and mid-length SFX.</summary>
        internal const float VorbisQuality = 0.5f;

        /// <summary>The platform key <see cref="AudioImporter.SetOverrideSampleSettings"/> takes.</summary>
        internal const string AndroidPlatform = "Android";

        /// <summary>Path segments whose contents are fired through
        /// <c>AudioSource.PlayOneShot</c> and therefore may NEVER be Streaming.</summary>
        internal static readonly string[] PlayOneShotFolders = { "/sfx/", "/voice/" };

        // ── Clip classes ────────────────────────────────────────────────────

        /// <summary>What a clip is used for, decided from its real length + its folder.</summary>
        internal enum ClipClass
        {
            /// <summary>Long, non-one-shot: Streaming + Vorbis, stereo kept.</summary>
            Music,
            /// <summary>2-15 s: CompressedInMemory + Vorbis, mono.</summary>
            Mid,
            /// <summary>&lt;2 s: DecompressOnLoad + ADPCM, mono, preloaded — must fire instantly.</summary>
            ShortSfx,
        }

        // ── Entry points ────────────────────────────────────────────────────

        /// <summary>DRY RUN — logs the before/after table + projected saving, writes NOTHING.</summary>
        [MenuItem("Defenders/Build/Report Audio Import Settings (dry run)")]
        public static void ReportOnly() => Execute(apply: false);

        /// <summary>Applies the classification to every AudioClip under Assets/. Idempotent.</summary>
        [MenuItem("Defenders/Build/Optimize Audio Import Settings")]
        public static void Run() => Execute(apply: true);

        // ── Core ────────────────────────────────────────────────────────────

        private static void Execute(bool apply)
        {
            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets" });
            if (guids == null || guids.Length == 0)
            {
                Debug.LogWarning("[AudioImportOptimizer] No AudioClip found under Assets/ — nothing to do.");
                return;
            }

            var log = new StringBuilder();
            log.AppendLine(apply
                ? "[AudioImportOptimizer] APPLY — classifying every AudioClip under Assets/."
                : "[AudioImportOptimizer] DRY RUN (ReportOnly) — nothing is written.");
            log.AppendLine("class    | sec   | ch | resources | now (lt/cf/q/mono/pre) -> target                | src MB | est MB now -> after | path");

            int changed = 0, already = 0, failed = 0, conflicts = 0;
            double srcMb = 0, estNow = 0, estAfter = 0, pcmNow = 0, pcmAfter = 0;
            var conflictLines = new List<string>();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;

                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null)
                {
                    failed++;
                    Debug.LogWarning($"[AudioImportOptimizer] '{path}' has no AudioImporter — skipped.");
                    continue;
                }

                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                if (clip == null)
                {
                    failed++;
                    Debug.LogWarning($"[AudioImportOptimizer] '{path}' did not load as an AudioClip — skipped " +
                                     "(cannot classify without a real length; nothing guessed from the filename).");
                    continue;
                }

                float seconds = clip.length;                 // REAL length — never inferred from the name.
                int channels = Mathf.Max(1, clip.channels);
                int rate = Mathf.Max(1, clip.frequency);

                bool oneShotFolder = IsPlayOneShotFolder(path);
                ClipClass klass = Classify(seconds, oneShotFolder);

                // A long clip sitting in a PlayOneShot folder is the conflict case: it
                // WOULD have been Streaming on length alone, and Streaming would break
                // its PlayOneShot firing. Report it; ship it as CompressedInMemory.
                if (oneShotFolder && seconds >= MusicSeconds)
                {
                    conflicts++;
                    conflictLines.Add(
                        $"  CONFLICT {seconds:F1}s '{path}' lives under an Sfx/Voice folder, so it is fired via " +
                        "AudioSource.PlayOneShot (Assets/_Modules/Audio/AudioService.cs:696) — Streaming would make " +
                        "the shot late or silent. Demoted to CompressedInMemory instead of Streaming.");
                }

                bool isResources = path.Replace('\\', '/').IndexOf("/Resources/", StringComparison.OrdinalIgnoreCase) >= 0;

                AudioImporterSampleSettings target = TargetSettings(klass, importer.defaultSampleSettings);
                bool targetMono = klass != ClipClass.Music;

                string before = Describe(importer.defaultSampleSettings, importer.forceToMono,
                                         importer.ContainsSampleSettingsOverride(AndroidPlatform)
                                             ? importer.GetOverrideSampleSettings(AndroidPlatform)
                                             : (AudioImporterSampleSettings?)null);
                string after = $"{target.loadType}/{target.compressionFormat}/q{target.quality:F2}/" +
                               $"mono={(targetMono ? 1 : 0)}/pre={(target.preloadAudioData ? 1 : 0)}+Android";

                // ── sizes (source is exact; the encoded columns are ESTIMATES) ──
                double srcBytes = SourceBytes(path);
                double mbSrc = srcBytes / (1024.0 * 1024.0);
                double mbNow = EstimateEncodedMb(importer.defaultSampleSettings, seconds, channels, rate,
                                                 importer.forceToMono);
                double mbAfter = EstimateEncodedMb(target, seconds, channels, rate, targetMono);
                double residentNow = ResidentPcmMb(importer.defaultSampleSettings.loadType, seconds, rate, channels);
                double residentAfter = ResidentPcmMb(target.loadType, seconds, rate, targetMono ? 1 : channels);

                srcMb += mbSrc; estNow += mbNow; estAfter += mbAfter;
                pcmNow += residentNow; pcmAfter += residentAfter;

                log.AppendLine(
                    $"{klass,-8} | {seconds,5:F1} | {channels,2} | {(isResources ? "FORCED " : "       ")} | " +
                    $"{before,-46} -> {after,-46} | {mbSrc,6:F2} | {mbNow,6:F2} -> {mbAfter,6:F2} | {path}");

                if (!apply) continue;

                bool needsWrite = NeedsWrite(importer, target, targetMono);
                if (!needsWrite) { already++; continue; }

                bool ok = ApplyTo(importer, target, targetMono, path);
                if (ok) changed++; else failed++;
            }

            log.AppendLine();
            log.AppendLine($"TOTALS  source {srcMb:F1} MB | encoded ESTIMATE {estNow:F1} MB -> {estAfter:F1} MB " +
                           $"(saving ~{Mathf.Max(0f, (float)(estNow - estAfter)):F1} MB)");
            log.AppendLine($"RESIDENT PCM (exact arithmetic, DecompressOnLoad only): {pcmNow:F1} MB -> {pcmAfter:F1} MB " +
                           "— Streaming/CompressedInMemory hold no full PCM copy.");
            log.AppendLine("⚠ The 'encoded' columns are ESTIMATES from a quality->bitrate model, NOT measured build " +
                           "output. Unity does not expose a compressed size before a build. The RESIDENT PCM line is " +
                           "exact (length x rate x channels x 2 bytes) and is the number to trust.");
            if (conflictLines.Count > 0)
            {
                log.AppendLine();
                log.AppendLine($"STREAMING / PlayOneShot CONFLICTS: {conflicts}");
                foreach (string c in conflictLines) log.AppendLine(c);
            }
            else
            {
                log.AppendLine();
                log.AppendLine("STREAMING / PlayOneShot CONFLICTS: 0 — no clip >= " + MusicSeconds +
                               "s lives under an Sfx/ or Voice/ folder.");
            }

            if (apply)
            {
                log.AppendLine();
                log.AppendLine($"APPLIED: {changed} reimported, {already} already at target (idempotent no-op), {failed} failed.");
                if (changed > 0)
                {
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                }
            }

            Debug.Log(log.ToString());
            if (failed > 0)
                Debug.LogWarning($"[AudioImportOptimizer] {failed} clip(s) could not be processed — see the warnings above.");
        }

        // ── Classification ──────────────────────────────────────────────────

        /// <summary>Class for a clip of <paramref name="seconds"/> length. A clip in a
        /// PlayOneShot folder can never be <see cref="ClipClass.Music"/> (Streaming).</summary>
        internal static ClipClass Classify(float seconds, bool playOneShotFolder)
        {
            if (seconds < ShortSfxSeconds) return ClipClass.ShortSfx;
            if (seconds >= MusicSeconds && !playOneShotFolder) return ClipClass.Music;
            return ClipClass.Mid;
        }

        /// <summary>True when the asset path contains an Sfx/ or Voice/ segment — the
        /// content that AudioService fires through AudioSource.PlayOneShot.</summary>
        internal static bool IsPlayOneShotFolder(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return false;
            string p = assetPath.Replace('\\', '/').ToLowerInvariant();
            foreach (string seg in PlayOneShotFolders)
                if (p.Contains(seg)) return true;
            return false;
        }

        /// <summary>The target sample settings for a class. Sample-rate fields are carried
        /// over from <paramref name="baseline"/> untouched (deliberate — see header).</summary>
        internal static AudioImporterSampleSettings TargetSettings(ClipClass klass, AudioImporterSampleSettings baseline)
        {
            AudioImporterSampleSettings s = baseline; // preserves sampleRateSetting / sampleRateOverride / conversionMode

            switch (klass)
            {
                case ClipClass.Music:
                    s.loadType = AudioClipLoadType.Streaming;
                    s.compressionFormat = AudioCompressionFormat.Vorbis;
                    s.quality = VorbisQuality;
                    s.preloadAudioData = false;
                    break;

                case ClipClass.Mid:
                    s.loadType = AudioClipLoadType.CompressedInMemory;
                    s.compressionFormat = AudioCompressionFormat.Vorbis;
                    s.quality = VorbisQuality;
                    s.preloadAudioData = false;
                    break;

                default: // ShortSfx — DecompressOnLoad is CORRECT here; they must fire instantly.
                    s.loadType = AudioClipLoadType.DecompressOnLoad;
                    s.compressionFormat = AudioCompressionFormat.ADPCM;
                    s.quality = 1f;   // ADPCM ignores quality; pinned so the compare is stable.
                    s.preloadAudioData = true;
                    break;
            }
            return s;
        }

        // ── Idempotency + write ─────────────────────────────────────────────

        /// <summary>True when the default settings, the Android override or forceToMono
        /// differ from target — i.e. a reimport would actually change something.</summary>
        private static bool NeedsWrite(AudioImporter importer, AudioImporterSampleSettings target, bool targetMono)
        {
            if (importer.forceToMono != targetMono) return true;
            if (!SameSettings(importer.defaultSampleSettings, target)) return true;
            if (!importer.ContainsSampleSettingsOverride(AndroidPlatform)) return true;
            return !SameSettings(importer.GetOverrideSampleSettings(AndroidPlatform), target);
        }

        private static bool SameSettings(AudioImporterSampleSettings a, AudioImporterSampleSettings b)
        {
            return a.loadType == b.loadType
                && a.compressionFormat == b.compressionFormat
                && Mathf.Approximately(a.quality, b.quality)
                && a.preloadAudioData == b.preloadAudioData
                && a.sampleRateSetting == b.sampleRateSetting
                && a.sampleRateOverride == b.sampleRateOverride;
        }

        /// <summary>Writes the default settings + the Android override + forceToMono and
        /// reimports. WebGL's existing override block is never read or written.</summary>
        private static bool ApplyTo(AudioImporter importer, AudioImporterSampleSettings target, bool targetMono, string path)
        {
            try
            {
                importer.forceToMono = targetMono;
                importer.defaultSampleSettings = target;
                importer.SetOverrideSampleSettings(AndroidPlatform, target);
                importer.SaveAndReimport();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AudioImportOptimizer] '{path}' FAILED to apply " +
                                 $"({ex.GetType().Name}: {ex.Message}) — left at its previous settings.");
                return false;
            }
        }

        // ── Reporting helpers ───────────────────────────────────────────────

        private static string Describe(AudioImporterSampleSettings def, bool mono, AudioImporterSampleSettings? android)
        {
            string a = android.HasValue
                ? $"{android.Value.loadType}/{android.Value.compressionFormat}/q{android.Value.quality:F2}"
                : "NO-ANDROID-OVERRIDE";
            return $"{def.loadType}/{def.compressionFormat}/q{def.quality:F2}/mono={(mono ? 1 : 0)}/" +
                   $"pre={(def.preloadAudioData ? 1 : 0)} [{a}]";
        }

        /// <summary>Exact on-disk size of the SOURCE file (not the encoded build size).</summary>
        private static double SourceBytes(string assetPath)
        {
            try
            {
                string full = System.IO.Path.Combine(
                    System.IO.Directory.GetParent(Application.dataPath).FullName, assetPath);
                var fi = new System.IO.FileInfo(full);
                return fi.Exists ? fi.Length : 0.0;
            }
            catch { return 0.0; }
        }

        /// <summary>
        /// ESTIMATED encoded size in MB for a set of settings. Unity exposes no
        /// pre-build compressed size, so this is a model, clearly labelled as such
        /// everywhere it is printed. It is only ever used to compare BEFORE vs AFTER
        /// under the SAME model, which is what makes the delta meaningful.
        /// </summary>
        private static double EstimateEncodedMb(AudioImporterSampleSettings s, float seconds, int channels,
                                                int rate, bool mono)
        {
            int ch = mono ? 1 : Mathf.Max(1, channels);
            switch (s.compressionFormat)
            {
                case AudioCompressionFormat.PCM:
                    return seconds * rate * ch * 2.0 / (1024.0 * 1024.0);
                case AudioCompressionFormat.ADPCM:
                    return seconds * rate * ch * 0.5 / (1024.0 * 1024.0);   // 4 bits/sample
                default: // Vorbis / AAC / anything else — quality-driven bitrate model
                    return VorbisKbps(s.quality, ch) * seconds / 8.0 / 1024.0;
            }
        }

        /// <summary>Unity's quality slider -> approximate Vorbis bitrate (kbps) for a stereo
        /// stream; mono is billed at ~55%. Piecewise-linear over the documented anchors.</summary>
        private static double VorbisKbps(float quality, int channels)
        {
            float[] q = { 0f, 0.1f, 0.3f, 0.5f, 0.7f, 1f };
            float[] b = { 45f, 64f, 80f, 128f, 192f, 500f };
            double bitrate = b[b.Length - 1];
            for (int i = 0; i < q.Length - 1; i++)
            {
                if (quality <= q[i + 1])
                {
                    bitrate = b[i] + (b[i + 1] - b[i]) * (quality - q[i]) / (q[i + 1] - q[i]);
                    break;
                }
            }
            return bitrate * (channels == 1 ? 0.55 : 1.0);
        }

        /// <summary>
        /// EXACT resident-PCM cost in MB. Only DecompressOnLoad keeps a full 16-bit PCM
        /// copy in memory; Streaming and CompressedInMemory do not. This is the number
        /// that is arithmetic rather than estimate.
        /// </summary>
        private static double ResidentPcmMb(AudioClipLoadType loadType, float seconds, int rate, int channels)
        {
            if (loadType != AudioClipLoadType.DecompressOnLoad) return 0.0;
            return seconds * rate * Mathf.Max(1, channels) * 2.0 / (1024.0 * 1024.0);
        }
    }
}
