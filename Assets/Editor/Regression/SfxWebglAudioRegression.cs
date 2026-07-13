// =============================================================================
// SfxWebglAudioRegression — headless oracle for the WO-682 defect class: a
// Resources/Sfx audio clip whose IMPORT diverges for WebGL fails FSB decode in
// the browser at error level ("Loading FSB failed for audio clip \"SwordSwing\"",
// db-proven 2026-07-12) and stalls the frame on first use (167ms/4000ms).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. No scene / no PlayMode — data-decidable.
//
// Invariants proven from the ASSETS (the real load surface, not a re-derivation):
//   (1) every AudioClip under the two Resources/Sfx folders loads as an
//       AudioClip via AssetDatabase (import sanity), and
//   (2) NO Sfx clip carries a WebGL platformSettingOverrides block in its .meta.
//       The FSB-failing SwordSwing.wav was the ONLY clip with one (sampleRate
//       override + quality 0.45); every clip that decodes fine on WebGL ships
//       the default import (`platformSettingOverrides: {}`). This locks the
//       root-cause class from silently recurring on the next sound drop.
//
// NOTE (scope): "every SfxId in the SfxClipLibrary resolves" was considered and
// SKIPPED — this asmdef does not reference DeNelle.Audio (no SfxId/SfxClipLibrary
// visibility), no SfxClipLibrary.asset exists (MASTER_CATALOG/audio.md), and
// null library rows are by-design silent no-ops — it would be a false-failing
// oracle. The .meta scan below is the invariant the captured data actually proves.
//
// Wire into the suite from DataRegression.RunAll (one line):
//   if (!SfxWebglAudioRegression.Run(out var sfxWebglReason)) failures.Add(sfxWebglReason); else log.AppendLine("[sfx-webgl] " + sfxWebglReason);
// =============================================================================
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class SfxWebglAudioRegression
    {
        // The two Resources/Sfx roots the runtime lazy-loads from (GameSfx /
        // EnemyCombatAudio / AbilityAudioBridge / AudioService.PlayUiClick).
        private static readonly string[] SfxFolders =
        {
            "Assets/Resources/Sfx",
            "Assets/_Modules/Audio/Resources/Sfx",
        };

        /// <summary>
        /// Proves every Resources/Sfx clip imports and none carries a divergent
        /// WebGL import override (the WO-682 FSB-decode-failure class). Returns
        /// true (PASS) only when both invariants hold for every clip.
        /// Deterministic, self-contained, no scene / no PlayMode.
        /// </summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- SFX WEBGL AUDIO IMPORT (WO-682: import sanity + no WebGL overrides) ---");

            var folders = new List<string>();
            foreach (var f in SfxFolders)
            {
                if (AssetDatabase.IsValidFolder(f)) folders.Add(f);
                else log.AppendLine($"folder '{f}' absent — skipped");
            }

            int scanned = 0;
            if (folders.Count > 0)
            {
                foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", folders.ToArray()))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    scanned++;

                    // (1) Import sanity — the asset must load as an AudioClip at all.
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    if (clip == null)
                    {
                        log.AppendLine($"{path} | loads=NO");
                        failures.Add($"'{path}' does not load as an AudioClip (import broken)");
                        continue;
                    }

                    // (2) WO-682 invariant — no WebGL platformSettingOverrides block.
                    string metaPath = path + ".meta";
                    if (!File.Exists(metaPath))
                    {
                        log.AppendLine($"{path} | loads=yes | meta=MISSING");
                        failures.Add($"'{path}' has no .meta file (guid instability + import drift)");
                        continue;
                    }
                    bool webglOverride = HasWebglOverride(File.ReadAllText(metaPath));
                    log.AppendLine($"{path} | loads=yes | webglOverride={webglOverride}");
                    if (webglOverride)
                        failures.Add($"'{path}' carries a WebGL platformSettingOverrides block — the WO-682 " +
                            "FSB-decode-failure class (SwordSwing root, db-proven 'Loading FSB failed'). Remove " +
                            "the override so the clip ships the default import like every working Sfx clip.");
                }
            }

            log.AppendLine($"scanned {scanned} Sfx clip(s) across {folders.Count} folder(s)");
            if (scanned == 0)
                failures.Add("scanned 0 Sfx clips — Resources/Sfx folders missing/empty (the runtime lazy-load surface is gone)");

            reason = Finish(failures, log);
            return failures.Count == 0;
        }

        // A healthy audio .meta reads "platformSettingOverrides: {}". Any 'WebGL:'
        // key inside that block is a per-platform override — the defect class.
        // The block ends at the next top-level AudioImporter field (forceToMono).
        private static bool HasWebglOverride(string metaText)
        {
            int start = metaText.IndexOf("platformSettingOverrides:", System.StringComparison.Ordinal);
            if (start < 0) return false;
            int end = metaText.IndexOf("forceToMono:", start, System.StringComparison.Ordinal);
            string block = end > start ? metaText.Substring(start, end - start) : metaText.Substring(start);
            return block.Contains("WebGL:");
        }

        private static string Finish(List<string> failures, StringBuilder log)
        {
            if (failures.Count == 0)
            {
                Debug.Log(log.ToString() + "SFX_WEBGL_OK");
                return "SFX WEBGL OK — every Resources/Sfx clip imports, none carries a WebGL import override";
            }
            string reason = "sfx-webgl: " + string.Join("; ", failures);
            Debug.LogError(log.ToString() + "SFX_WEBGL_FAIL: " + reason);
            return reason;
        }
    }
}
