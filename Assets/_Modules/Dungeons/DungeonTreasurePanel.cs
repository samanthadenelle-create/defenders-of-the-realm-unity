// =============================================================================
// DungeonTreasurePanel (WO-850) - the confirm/reward beat for the deepest cache.
// -----------------------------------------------------------------------------
// Owner ruling: "prompt then confirm" - opening the cache presents what is inside
// and the player TAKES it. The grant is the CALLER's callback; this panel only
// decides WHEN it fires, and never reports a reward it did not apply (the WO-844
// potion lesson). Because the shared Close is retired and the scrim has no
// onClose, this modal has NO dismiss - so every way it can end (the Take tap, or
// PanelManager swapping it out for another screen) pays the player, and the
// pending callback is consumed before teardown so it can never pay twice.
//
// Show() returns a BOOL: true only when the modal is really on screen and has
// therefore taken ownership of the grant. It returns false when a duplicate Show
// is refused, or when PanelManager REJECTS the open (the WO-437 battle-lock, which
// tears the panel back down). The caller must grant directly on false - otherwise
// a rejected open would consume the cache and pay nothing.
//
// KIT LAWS OBSERVED:
//  - Built through ElarionUiKit only (UiObsidianConformanceRegression HardFailOnNew
//    rejects a new file that hand-rolls uGUI).
//  - ONE exit. The shared Close is retired and "Take" is the single CTA - the same
//    owner F8 (seq 628) that removed Continue-vs-Close from the Echo emergence
//    beat: two exits on a linear beat read as one choice offered twice.
//  - ASCII-only source (DungeonTreasureRegression case 5 fails the first non-ASCII
//    character - TMP renders it as tofu on device).
//  - Meaning never by colour: every material line prints "Name xN" as TEXT, so the
//    payout is readable red/green colourblind.
//  - Text bands are FIXED PIXELS (>= the font's line height), never parent
//    fractions - fractional bands silently cull glyphs (the WO-832/841 truncation
//    root cause). EnsureBand stamps that height after the kit builds the label.
//  - Registered with PanelManager (ONE cached handle for the panel's lifetime, as
//    PanelHandle's contract requires) so the shared Interact button - which bails
//    on PanelManager.AnyOpen - stays hidden under the modal.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using TMPro;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.UI;
using DeNelle.Village.Items;   // MaterialCatalog - display names only, never inventory

namespace DeNelle.Dungeons
{
    /// <summary>The small "TREASURE FOUND" modal shown when the deepest cache is opened.</summary>
    public static class DungeonTreasurePanel
    {
        private const string Sys = "DungeonTreasure";
        private const string PanelName = "DungeonTreasure";

        // Fixed-pixel line bands (see header). The kit's reference canvas is 1080x1920 and
        // ElarionUi.FontBody is 50px, so a body line needs ~60px; the payout block is sized
        // as LinePx per line. HeadingPx gives a single heading line the same clearance.
        private const float LinePx = 60f;
        private const float HeadingPx = 66f;

        private static GameObject s_canvas;
        private static PanelHandle s_handle;

        // The pending grant, held only while the modal is live. There is NO dismiss on this
        // panel (the shared Close is retired and the scrim has no onClose), so every way the
        // modal can end - the Take tap, or PanelManager swapping us out for another screen -
        // must pay the player. Nulled on teardown so it can never be granted twice.
        private static Action s_onTake;

        /// <summary>True while the reward modal is on screen.</summary>
        public static bool IsOpen => s_canvas != null;

        /// <summary>
        /// Present the reward. <paramref name="onTake"/> runs exactly once, when the modal
        /// ends (the Take tap, or an arbiter-forced close - this panel has no dismiss).
        /// Returns TRUE when the modal is live and has therefore taken ownership of the
        /// grant; FALSE when it refused to open (duplicate Show, unusable chrome, or an
        /// arbiter rejection), in which case the CALLER still owns paying the player.
        /// </summary>
        public static bool Show(IReadOnlyList<(string Id, int Count)> bundle, bool firstClear, Action onTake)
        {
            if (s_canvas != null)
            {
                FlowTrace.Warn(Sys, "reward panel already open - ignoring duplicate Show");
                return false;
            }

            var modal = ElarionUiKit.BuildObsidianModal(
                PanelName, "TREASURE FOUND",
                new Vector2(0.20f, 0.24f), new Vector2(0.80f, 0.78f),
                onClose: null, sortingOrder: 31030,
                frameName: RpgUiCatalog.FrameCore);
            if (modal == null || modal.canvas == null || modal.chrome == null || modal.chrome.content == null)
            {
                FlowTrace.Fail(Sys, "BuildObsidianModal returned no usable chrome - reward panel NOT shown");
                if (modal != null && modal.canvas != null) UnityEngine.Object.Destroy(modal.canvas);
                return false;
            }
            s_canvas = modal.canvas;
            var content = modal.chrome.content.transform;

            // ONE exit: retire the shared Close so Take is the only way out (owner F8 seq 628).
            if (modal.chrome.close != null) modal.chrome.close.gameObject.SetActive(false);

            // -- body: one ASCII line per material, top-down, fixed-pixel bands ----
            var heading = ElarionUiKit.Label(content, "The cache holds:", 0.70f, 0.78f,
                ElarionUi.ParchmentDim, ElarionUi.FontBody, TextAlignmentOptions.Center,
                0.08f, 0.92f);
            EnsureBand(heading, HeadingPx);
            ElarionUiKit.FitSingleLine(heading);

            var lines = new List<string>();
            if (bundle != null)
            {
                foreach (var entry in bundle)
                {
                    if (string.IsNullOrEmpty(entry.Id) || entry.Count <= 0) continue;
                    lines.Add(DisplayNameFor(entry.Id) + " x" + entry.Count);
                }
            }
            if (lines.Count == 0) lines.Add("(empty)");

            // Single TMP block for the payout keeps every line in ONE fixed band whose
            // height is computed from the line count - no per-line fractional anchors.
            var sb = new StringBuilder();
            for (int i = 0; i < lines.Count; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(lines[i]);
            }
            var payout = ElarionUiKit.Label(content, sb.ToString(), 0.40f, 0.66f,
                ElarionUi.Parchment, ElarionUi.FontBody, TextAlignmentOptions.Center,
                0.08f, 0.92f, bold: true);
            EnsureBand(payout, LinePx * Mathf.Max(1, lines.Count));

            if (firstClear)
            {
                var unlock = ElarionUiKit.Label(content,
                    "First clear -- a new recipe is remembered.", 0.30f, 0.38f,
                    ElarionUi.Gilt, ElarionUi.FontBody, TextAlignmentOptions.Center,
                    0.06f, 0.94f);
                EnsureBand(unlock, HeadingPx);
                ElarionUiKit.FitSingleLine(unlock);
            }

            // -- the ONE CTA (same bottom-row budget as the Echo beat) -------------
            ElarionUiKit.Button(content, "Take", ElarionUiKit.ButtonKind.Confirm,
                new Vector2(0.34f, 0.05f), new Vector2(0.66f, 0.245f),
                CloseAndGrant);

            // ONE handle for the panel's lifetime (PanelHandle's documented contract).
            // The pending grant is armed only AFTER the arbiter accepts: NotifyOpened can
            // REJECT (WO-437 battle-lock) and invokes the handle's Close on its way out, so
            // arming first would pay the reward inside the rejection AND leave the caller
            // paying it again on our false.
            s_onTake = null;
            if (s_handle == null) s_handle = PanelManager.Register(PanelName, CloseAndGrant, () => IsOpen);
            if (!PanelManager.NotifyOpened(s_handle))
            {
                FlowTrace.Warn(Sys, "PanelManager rejected the reward panel (battle-lock) - caller must grant directly");
                Teardown();
                return false;
            }
            s_onTake = onTake;

            FlowTrace.Step(Sys, $"reward panel opened ({lines.Count} line(s), firstClear={firstClear}).");
            return true;
        }

        /// <summary>The ONE way this modal ends: tear it down, then pay the pending grant.
        /// Wired to both the Take tap and the arbiter's forced close, because there is no
        /// dismiss on this panel - see <see cref="s_onTake"/>.</summary>
        private static void CloseAndGrant()
        {
            var pending = s_onTake;
            s_onTake = null;                    // consume FIRST - a re-entrant close cannot double-pay
            Teardown();
            // Grant AFTER teardown so a throwing grant can never leave the modal wedged open.
            if (pending != null) Guard.Try(Sys, "treasure take callback", () => pending());
        }

        /// <summary>Destroy the modal and release the arbiter. Never grants; idempotent.</summary>
        private static void Teardown()
        {
            if (s_canvas != null)
            {
                UnityEngine.Object.Destroy(s_canvas);
                s_canvas = null;
            }
            if (s_handle != null) PanelManager.NotifyClosed(s_handle);
        }

        /// <summary>
        /// Player-facing material name. Routes through the shared catalog so the panel and
        /// the inventory always agree; falls back to the raw id (visible, never blank) so a
        /// mis-authored bundle id is OBVIOUS on screen instead of rendering an empty row.
        /// </summary>
        private static string DisplayNameFor(string id)
        {
            string name = null;
            Guard.Try(Sys, "resolve material display name", () =>
            {
                name = MaterialCatalog.DisplayName(id);
            });
            return string.IsNullOrEmpty(name) ? id : name;
        }

        /// <summary>Force a fixed-PIXEL height on a label's rect (see header: fractional
        /// bands cull glyphs). Anchors stay centred on their existing midpoint.</summary>
        private static void EnsureBand(TMP_Text label, float pixels)
        {
            if (label == null) return;
            var rt = label.rectTransform;
            float mid = (rt.anchorMin.y + rt.anchorMax.y) * 0.5f;
            rt.anchorMin = new Vector2(rt.anchorMin.x, mid);
            rt.anchorMax = new Vector2(rt.anchorMax.x, mid);
            rt.offsetMin = new Vector2(0f, -pixels * 0.5f);
            rt.offsetMax = new Vector2(0f, pixels * 0.5f);
        }
    }
}
