// =============================================================================
// DungeonSealedDoorPanel — WO-1114, the door that is closed FOR A REASON.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// ⛔ THE ONE RULE THIS SCREEN EXISTS TO KEEP:
//   A closed dungeon must read as WORLD, never as BUILD STATUS. This is not an
//   error dialog and it must never look like one. No red, no warning glyph, no
//   "under construction", no "coming soon", no dev vocabulary of any kind — the
//   dev meaning lives in the DungeonDoorState enum and stops there. What the
//   player gets is authored prose in the ordinary parchment/gilt body palette,
//   in the same Obsidian frame every other screen in the game uses, so a door
//   that does not open reads as a deliberate piece of the world.
//   DungeonStatusRegression [door-copy] gates the vocabulary; it is a gate and
//   not a comment precisely because this is the rule most likely to rot.
//
// COPY OWNERSHIP: the backend MAY ship authored headline/body per dungeon. When
//   it does not, the DEFAULTS come from canon-strings.json via VillageStrings
//   .Canon — never from a literal typed into this file (CLAUDE.md §7). This file
//   owns that fallback for the whole module: DungeonPortal calls DoorHeadline()
//   here rather than resolving copy a second time.
//   ⚠ Those eight canon values shipped UNRATIFIED (WO-1114 §9.1). The owner has
//   an open ruling to accept or rewrite them. Swapping them is data-only.
//
// PATTERN: JewelPolishConfirmPanel (the most recent in-tree modal) — ElarionUiKit
//   BuildObsidianModal + the null-guard-and-destroy + PanelManager Register /
//   NotifyOpened / NotifyClosed. The arbiter registration is MANDATORY:
//   ModalArbiterRegistrationRegression treats any bare BuildObsidianModal( call
//   as a top-band build and hard-fails a file that does not register.
//   Hand-rolled uGUI fails [ui-obsidian]; UXML does not render in player builds.
//
// ASCII-only copy (tofu on device otherwise). Meaning never by colour alone —
//   the whole message is words (owner is red/green colourblind).
// =============================================================================

using UnityEngine;
using TMPro;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.UI;
using DeNelle.Core.World;

namespace DeNelle.Village
{
    /// <summary>
    /// The dialogue shown when the player taps a dungeon door that is not open.
    /// Also the single owner of the per-status default copy lookup.
    /// </summary>
    public static class DungeonSealedDoorPanel
    {
        private const string Sys = DungeonStatusCatalog.Sys;
        private const string PanelName = "DungeonSealedDoor";

        // Fixed-pixel bands flowed from the content top edge. A fractional band culls
        // glyphs on a variable-length body, which is exactly what this screen carries.
        private const float NamePx = 44f;
        private const float BodyPx = 190f;
        private const float StackTopPx = 28f;
        private const float StackGapPx = 14f;

        private static GameObject s_canvas;
        private static PanelHandle s_handle;

        /// <summary>True while the sealed-door dialogue is on screen.</summary>
        public static bool IsOpen => s_canvas != null;

        // ─────────────────────────────────────────────────────────────────────
        //  Copy resolution — authored payload prose first, canon default second
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// The one-line prose for a closed door: the payload's authored headline when
        /// it carries one, else the per-status default from canon-strings.json.
        /// An OPEN door has no headline and returns null — callers must not print one.
        /// </summary>
        public static string DoorHeadline(DungeonDoorInfo info)
        {
            if (info.IsOpen) return null;
            if (!string.IsNullOrWhiteSpace(info.Headline)) return info.Headline;
            return VillageStrings.Canon(HeadlineKey(info.State));
        }

        /// <summary>Body prose for a closed door. Same precedence as <see cref="DoorHeadline"/>.</summary>
        public static string DoorBody(DungeonDoorInfo info)
        {
            if (info.IsOpen) return null;
            if (!string.IsNullOrWhiteSpace(info.Body)) return info.Body;
            return VillageStrings.Canon(BodyKey(info.State));
        }

        /// <summary>canon-strings.json key for a state's default headline.</summary>
        private static string HeadlineKey(DungeonDoorState state)
        {
            switch (state)
            {
                case DungeonDoorState.Collapsed: return "dungeonCollapsedHeadline";
                case DungeonDoorState.Rescue:    return "dungeonRescueHeadline";
                case DungeonDoorState.Flooded:   return "dungeonFloodedHeadline";
                default:                         return "dungeonSealedHeadline";
            }
        }

        /// <summary>canon-strings.json key for a state's default body.</summary>
        private static string BodyKey(DungeonDoorState state)
        {
            switch (state)
            {
                case DungeonDoorState.Collapsed: return "dungeonCollapsedBody";
                case DungeonDoorState.Rescue:    return "dungeonRescueBody";
                case DungeonDoorState.Flooded:   return "dungeonFloodedBody";
                default:                         return "dungeonSealedBody";
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Show
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Show the door's prose. <paramref name="displayName"/> is the dungeon's own
        /// name and is printed as a quiet line above the body — the door stays named,
        /// so a closed dungeon still teases the world instead of shrinking it
        /// (WO-1114 §9.3, the plan's position; the owner's ruling is still open).
        /// <para>
        /// Returns FALSE when the dialogue could not open. The DOOR IS STILL CLOSED on
        /// that path — the gate is DungeonPortal's, never this screen's, so a UI failure
        /// can neither open a sealed dungeon nor leave half-built chrome on screen.
        /// </para>
        /// </summary>
        public static bool Show(DungeonDoorInfo info, string displayName)
        {
            if (s_canvas != null)
            {
                FlowTrace.Warn(Sys, "sealed-door dialogue already open - ignoring duplicate Show.");
                return false;
            }

            if (info.IsOpen)
            {
                // Not a player-visible failure: the caller simply asked at the wrong time.
                FlowTrace.Warn(Sys, "Show called for an OPEN door - refusing (nothing to say about a door that opens).");
                return false;
            }

            string headline = DoorHeadline(info);
            string body = DoorBody(info);
            if (string.IsNullOrWhiteSpace(headline)) headline = VillageStrings.Canon(HeadlineKey(DungeonDoorState.Sealed));
            if (string.IsNullOrWhiteSpace(body)) body = VillageStrings.Canon(BodyKey(DungeonDoorState.Sealed));

            var modal = ElarionUiKit.BuildObsidianModal(
                PanelName, headline.ToUpperInvariant(),
                new Vector2(0.14f, 0.16f), new Vector2(0.86f, 0.86f),
                onClose: Close, sortingOrder: 31040,
                frameName: RpgUiCatalog.FrameCore);
            if (modal == null || modal.canvas == null || modal.chrome == null || modal.chrome.content == null)
            {
                FlowTrace.Fail(Sys, "BuildObsidianModal returned no usable chrome - sealed-door dialogue NOT shown.");
                if (modal != null && modal.canvas != null) UnityEngine.Object.Destroy(modal.canvas);
                return false;
            }
            s_canvas = modal.canvas;
            var content = modal.chrome.content.transform;

            float cursor = StackTopPx;

            if (!string.IsNullOrWhiteSpace(displayName))
            {
                var nameLabel = ElarionUiKit.Label(content, displayName, 0f, 0f,
                    ElarionUi.Gilt, ElarionUi.FontBody, TextAlignmentOptions.Center, 0.06f, 0.94f, bold: true);
                StackDown(nameLabel, NamePx, ref cursor);
                ElarionUiKit.FitSingleLine(nameLabel);
            }

            // The prose. Ordinary parchment body palette - the SAME one the crafting and
            // lore screens use. No error styling anywhere on this screen, by design.
            var bodyLabel = ElarionUiKit.Label(content, body, 0f, 0f,
                ElarionUi.Parchment, ElarionUi.FontBody, TextAlignmentOptions.Center, 0.08f, 0.92f);
            StackDown(bodyLabel, BodyPx, ref cursor);

            // The frame's own labelled obsidian Close is the single dismiss (owner ruling
            // 2026-07-03: never draw an X). It is built by BuildObsidianModal - do not add
            // a second dismiss control here.

            if (s_handle == null) s_handle = PanelManager.Register(PanelName, Close, () => IsOpen);
            if (!PanelManager.NotifyOpened(s_handle))
            {
                FlowTrace.Warn(Sys, "PanelManager rejected the sealed-door dialogue.");
                Teardown();
                return false;
            }

            FlowTrace.Step(Sys, "sealed-door dialogue shown for '" + (displayName ?? "?") + "' state=" + info.State +
                                " copy=" + (string.IsNullOrWhiteSpace(info.Body) ? "canon-default" : "authored") +
                                " (provenance=" + DungeonStatusCatalog.Provenance + ").");
            return true;
        }

        /// <summary>Dismiss. Safe to call when nothing is open.</summary>
        public static void Close()
        {
            if (s_canvas == null) return;
            Teardown();
            FlowTrace.Step(Sys, "sealed-door dialogue closed - the door is unchanged.");
        }

        private static void Teardown()
        {
            if (s_canvas != null) UnityEngine.Object.Destroy(s_canvas);
            s_canvas = null;
            if (s_handle != null) PanelManager.NotifyClosed(s_handle);
        }

        /// <summary>
        /// Place <paramref name="label"/> as the next element in a top-down PIXEL flow and
        /// advance <paramref name="cursor"/>. Growth PUSHES what follows instead of
        /// overlapping it (the WO-865 shape) — the body here is variable-length prose.
        /// </summary>
        private static void StackDown(TMP_Text label, float pixels, ref float cursor)
        {
            if (label == null) return;
            var rt = label.rectTransform;
            rt.anchorMin = new Vector2(rt.anchorMin.x, 1f);
            rt.anchorMax = new Vector2(rt.anchorMax.x, 1f);
            rt.offsetMax = new Vector2(0f, -cursor);
            rt.offsetMin = new Vector2(0f, -(cursor + pixels));
            cursor += pixels + StackGapPx;
        }
    }
}
