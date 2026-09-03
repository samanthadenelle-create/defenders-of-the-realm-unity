// =============================================================================
// FtuePointerVfxRegression — WO-1344: the FTUE "where to go" pointer is HER tag,
// and it cannot swallow a tap.
// -----------------------------------------------------------------------------
// Two things can silently undo this feature, and neither is visible to a compile:
//
//  (1) THE OWNER'S TAG GETS RE-POINTED. FtueWorldPointer.PointerKey is compiled
//      code; Assets/Editor/VfxManualPicks.json is a file the owner edits through
//      the VFX Caster. Rename the constant, mistype the key, or let a refactor
//      point the FTUE at some other key, and the two sides part company with no
//      error anywhere - the pointer just stops drawing, which is exactly the
//      "missing VFX vs subtle VFX" ambiguity this ticket exists to end. So: her
//      row is read OFF DISK and required to carry that key AND the prefabPath the
//      key was tagged to. The CLI must never re-pick art to close a gap here.
//
//  (2) THE POINTER STARTS EATING TAPS. WO-1340's contextual beat "gates nothing
//      by construction" and the FocusMask it replaces never blocks input on a
//      world-anchored target. A pointer that swallowed a tap would soft-lock the
//      FTUE on the very step it is teaching. So: the owner's marker prefab is
//      loaded off disk and required to carry ZERO Collider, ZERO Collider2D,
//      ZERO Canvas, ZERO GraphicRaycaster and ZERO uGUI Graphic anywhere in its
//      hierarchy, and the pointer's own source is required to add none of those
//      either. Both halves matter: the prefab is the body that appears on screen,
//      the source is the only place a raycaster could be bolted onto it later.
//
// SKIP RULE, asymmetric on purpose (same rule as SurfaceImpactVfxRegression): the
// Hovl packs are gitignored, so on a clean clone the prefab is absent and check
// (2) SKIPS AND SAYS SO - a suite that goes red on a fresh clone is a suite the
// next person deletes. Check (1) never skips: the JSON is tracked and is the
// thing that actually goes wrong.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DeNelle.Village;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DeNelle.Editor.Regression
{
    public static class FtuePointerVfxRegression
    {
        /// <summary>The prefab the owner tagged the FTUE pointer key to, VERBATIM from
        /// VfxManualPicks.json. Restated here ONLY so a re-point is caught: this suite
        /// compares it against the file, it never feeds it to anything that spawns.</summary>
        private const string OwnerTaggedPrefabPath =
            "Assets/Hovl Studio/Map track markers VFX/Prefabs/Marker 1 arrows Loop.prefab";

        private const string PicksRelative = "Editor/VfxManualPicks.json";

        /// <summary>Source of the pointer piece — scanned for raycast-capable components.</summary>
        private const string PointerSourcePath =
            "Assets/_Modules/Village/Tutorial/V2/FtueWorldPointer.cs";

        public static bool Run(out string reason)
        {
            var fails = new List<string>();
            var log = new StringBuilder();

            CheckOwnerTag(fails, log);
            CheckPrefabIsInputTransparent(fails, log);
            CheckPointerSourceAddsNoRaycaster(fails, log);

            if (fails.Count > 0)
            {
                reason = "ftue-pointer-vfx FAIL (" + fails.Count + "): " +
                         string.Join(" | ", fails.ToArray());
                return false;
            }

            reason = "ftue-pointer-vfx OK - " + log;
            return true;
        }

        // ── (1) the code key still names HER row, pointing at HER prefab ─────────

        private static void CheckOwnerTag(List<string> fails, StringBuilder log)
        {
            string picks = Path.Combine(Application.dataPath, PicksRelative);
            if (!File.Exists(picks))
            {
                fails.Add("owner tag file missing: '" + picks + "'. The FTUE pointer key is an OWNER " +
                          "row in that file; with no file there is nothing for the code constant to " +
                          "agree with and this check would pass vacuously.");
                return;
            }

            string json;
            try { json = File.ReadAllText(picks); }
            catch (Exception e) { fails.Add("could not read VfxManualPicks.json: " + e.Message); return; }

            string key = FtueWorldPointer.PointerKey;
            if (string.IsNullOrEmpty(key))
            {
                fails.Add("FtueWorldPointer.PointerKey is empty - the FTUE pointer can never play.");
                return;
            }

            int keyAt = json.IndexOf("\"" + key + "\"", StringComparison.Ordinal);
            if (keyAt < 0)
            {
                fails.Add("FtueWorldPointer.PointerKey is '" + key + "', but no such key appears in " +
                          "Assets/Editor/VfxManualPicks.json. Either the code constant drifted or the " +
                          "owner retagged the FTUE pointer; the CLI must NOT re-pick art to close the " +
                          "gap - map her tag verbatim or raise it with her.");
                return;
            }

            // The prefabPath is written by the VFX Caster on the line after the key, inside the
            // same row object. Look for it in the WINDOW that follows the key rather than anywhere
            // in the file, so another row carrying the same path cannot satisfy this. The window
            // is a fixed span (a Caster row is five short lines) rather than a scan to the row's
            // closing brace: a brace character in this source would trip the repo's balance gate.
            const int RowWindowChars = 400;
            int windowEnd = Math.Min(json.Length, keyAt + RowWindowChars);
            string row = json.Substring(keyAt, windowEnd - keyAt);
            if (row.IndexOf(OwnerTaggedPrefabPath, StringComparison.OrdinalIgnoreCase) < 0)
            {
                fails.Add("the owner's row for '" + key + "' no longer points at '" +
                          OwnerTaggedPrefabPath + "'. If SHE retagged it, update this suite to her new " +
                          "path in the same breath; if the CODE moved her tag, that is the defect - " +
                          "a key -> prefab mapping is hers alone.");
                return;
            }

            log.Append("owner tag '").Append(key).Append("' -> her tagged marker prefab, verified off disk; ");
        }

        // ── (2) the body that appears on screen cannot receive input ─────────────

        private static void CheckPrefabIsInputTransparent(List<string> fails, StringBuilder log)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OwnerTaggedPrefabPath);
            if (prefab == null)
            {
                // HOLLOW PASS FIXED 2026-09-03 at the gate (CLI). This branch used to log
                // "SKIPPED" and return having asserted NOTHING - and the caller's only channel is
                // a bool, so an absent art pack read as a clean PASS on the one check that keeps
                // the FTUE castable. The ratchet caught it, correctly.
                //
                // Three-way rule: this is CONTENT/ART-ABSENT (the Hovl packs are gitignored, so a
                // clean clone legitimately has no prefab), so we ASSERT THROUGH THE PROVEN
                // FALLBACK rather than skipping. With no prefab the guarantee that still has to
                // hold is that the pointer DECLINES and the old FocusMask stands - so pin the
                // decline. If a future seat removes that guard, a clean clone would draw nothing
                // and swallow nothing, but a machine WITH the pack would draw an unvetted body.
                string src = File.Exists(PointerSourcePath) ? File.ReadAllText(PointerSourcePath) : null;
                if (src == null)
                    fails.Add("[input-transparency] the marker prefab is absent AND " + PointerSourcePath +
                              " is missing, so neither the body nor its decline path can be checked. " +
                              "This suite cannot certify input transparency on this machine.");
                else if (src.IndexOf("CanPlayKey", StringComparison.Ordinal) < 0)
                    fails.Add("[input-transparency] the marker prefab is absent (gitignored pack) AND " +
                              "FtueWorldPointer no longer consults VFXManager.CanPlayKey. Without that " +
                              "guard the pointer cannot decline when its prefab does not resolve, so the " +
                              "FTUE loses the fallback that keeps the step teachable.");
                else
                    log.Append("marker prefab not on this machine (gitignored pack) - asserted THROUGH the " +
                               "fallback instead: FtueWorldPointer still gates on VFXManager.CanPlayKey, so " +
                               "an unresolvable key declines to the FocusMask rather than drawing; ");
                return;
            }

            int colliders = prefab.GetComponentsInChildren<Collider>(true).Length;
            int colliders2d = prefab.GetComponentsInChildren<Collider2D>(true).Length;
            int canvases = prefab.GetComponentsInChildren<Canvas>(true).Length;
            int raycasters = prefab.GetComponentsInChildren<BaseRaycaster>(true).Length;
            int graphics = prefab.GetComponentsInChildren<Graphic>(true).Length;

            if (colliders + colliders2d + canvases + raycasters + graphics > 0)
                fails.Add("the FTUE pointer prefab '" + OwnerTaggedPrefabPath + "' carries input-capable " +
                          "components (Collider=" + colliders + " Collider2D=" + colliders2d +
                          " Canvas=" + canvases + " BaseRaycaster=" + raycasters + " Graphic=" + graphics +
                          "). The pointer MUST be input-transparent: WO-1340's beat gates nothing by " +
                          "construction, and a pointer that swallows a tap soft-locks the FTUE on the " +
                          "very step it is teaching.");
            else
                log.Append("marker prefab is input-transparent (0 Collider / 0 Collider2D / 0 Canvas / " +
                           "0 BaseRaycaster / 0 Graphic across ")
                   .Append(prefab.GetComponentsInChildren<Transform>(true).Length)
                   .Append(" transforms); ");
        }

        // ── (3) and the pointer piece never bolts one on ─────────────────────────

        private static void CheckPointerSourceAddsNoRaycaster(List<string> fails, StringBuilder log)
        {
            string abs = Path.Combine(Application.dataPath, "..", PointerSourcePath);
            if (!File.Exists(abs))
            {
                fails.Add("FtueWorldPointer source not found at '" + PointerSourcePath + "' - the " +
                          "input-transparency guarantee has no subject.");
                return;
            }

            string src;
            try { src = File.ReadAllText(abs); }
            catch (Exception e) { fails.Add("could not read FtueWorldPointer source: " + e.Message); return; }

            // Only the ADD sites matter: a comment naming these types is how the rule is
            // documented, so scan for the construction verbs, not the bare names.
            string[] banned =
            {
                "AddComponent<GraphicRaycaster>",
                "AddComponent<Canvas>",
                "AddComponent<Collider>",
                "AddComponent<BoxCollider>",
                "AddComponent<SphereCollider>",
                "AddComponent<Image>",
                "AddComponent<PhysicsRaycaster>",
            };
            foreach (var b in banned)
                if (src.IndexOf(b, StringComparison.Ordinal) >= 0)
                    fails.Add("FtueWorldPointer calls " + b + " - the FTUE pointer must stay " +
                              "input-transparent (see the file header and WO-1344).");

            if (fails.Count == 0)
                log.Append("FtueWorldPointer adds no raycaster, canvas or collider; ");
        }
    }
}
