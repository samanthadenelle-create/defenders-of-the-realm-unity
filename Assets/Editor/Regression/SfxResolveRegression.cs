// =============================================================================
// SfxResolveRegression [sfx-resolve] -- proves the core one-shot SFX clips resolve.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. The runtime lazy-loads combat/UI SFX via
// Resources.Load<AudioClip>("Sfx/<Name>") (GameSfx / EnemyCombatAudio /
// AbilityAudioBridge / AudioService.PlayUiClick). A missing/renamed clip is a
// SILENT no-op at runtime (null clip -> no sound, no error). This oracle resolves
// the five load-bearing clip keys through the SAME Resources path and fails on any
// null -- so a dropped/renamed clip is a build-gate failure, not a silent hole.
//
// Marker: SFX_RESOLVE_OK / SFX_RESOLVE_FAIL. Expected: GREEN.
//
// Wire (DataRegression.RunAll):
//   if (!SfxResolveRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[sfx-resolve] " + r);
// =============================================================================
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class SfxResolveRegression
    {
        private static readonly string[] Keys =
        {
            "Sfx/SwordSwing",
            "Sfx/WeaponDraw",
            "Sfx/DragonRoar",
            "Sfx/FootstepsWalk",
            "Sfx/UiClick",
        };

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- SFX RESOLVE (Resources.Load<AudioClip> for the load-bearing clip keys) ---");

            foreach (var key in Keys)
            {
                var clip = Resources.Load<AudioClip>(key);
                log.AppendLine($"  {key} -> {(clip != null ? "OK" : "NULL")}");
                if (clip == null)
                    failures.Add($"[sfx-resolve] Resources.Load<AudioClip>(\"{key}\") is NULL -- the clip is missing/renamed and its cue plays silently");
            }

            if (failures.Count == 0)
            {
                Debug.Log(log.ToString() + "SFX_RESOLVE_OK");
                reason = $"SFX RESOLVE OK -- all {Keys.Length} core Sfx clips resolve non-null";
                return true;
            }
            reason = "sfx-resolve: " + string.Join("; ", failures);
            Debug.LogError(log.ToString() + "SFX_RESOLVE_FAIL: " + reason);
            return false;
        }
    }
}
