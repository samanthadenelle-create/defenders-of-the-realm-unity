// =============================================================================
// MusicSelectionPanel — the player music-selection jukebox UI (WO-162).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Audio   Namespace: DeNelle.Audio
//
// The audio ENGINE for this feature already exists on AudioService:
//   • AudioService.AmbientChoicesFor(context)  — the curated, authorable track set
//   • AudioService.GetAmbientChoice(context)    — the player's persisted pick
//   • AudioService.SetAmbientChoice(context, t) — persist + (if in context) play
//   • AudioService.PlayAmbientContext / IsStateCue — combat-state music OVERRIDES
//     the jukebox pick, returning to it afterwards.
// This file is JUST the selection UI on top of that — no new audio system
// (CLAUDE.md §5/§9, WO-162 constraint).
//
// WO-F conversion (2026-07-03, coverage matrix row #46): UIDocument/UITK card ->
// code-built uGUI on the Obsidian master frame (BuildObsidianModal: FrameCore +
// medallion + the ONE shared Close + tap-outside scrim), per the HelpMenu
// reference recipe. Track rows are Obsidian buttons rebuilt each open so a
// context change (village -> overworld) shows the right set + checkmark.
// Toggle()/Open() stay public — the HUD kit's dock reaches them by reflection.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.UI;   // shared Elarion kit — jukebox matches the one game UI

namespace DeNelle.Audio
{
    /// <summary>
    /// A small jukebox panel: lists the curated ambient tracks for the current
    /// context (Village vs Overworld), shows which one is selected, and lets the
    /// player pick one. The pick persists and plays via the existing AudioService
    /// ambient-context path; combat/victory/defeat music still overrides.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MusicSelectionPanel : MonoBehaviour
    {
        /// <summary>Key that toggles the jukebox panel open/closed.</summary>
        public const KeyCode ToggleKey = KeyCode.J;

        /// <summary>WO-563: public open/toggle so a reachable HUD button (the kit dock) can
        /// surface the jukebox on touch — the J key is keyboard-only + gated behind DevHotkeys.</summary>
        public void Toggle() => SetOpen(!_open);
        public void Open()   => SetOpen(true);

        private ElarionUiKit.ObsidianModal _modal;
        private Transform _rowHost;   // the frame's body zone — rows rebuilt into it each open
        private bool _open;

        // Strict MVVM (Silo E): ALL selection state/logic lives in the VM; this View
        // reads vm.* only and never touches AudioService.
        private JukeboxVM _vm;

        // PanelManager mutual-exclusion handle (one panel at a time).
        private DeNelle.Core.UI.PanelHandle _panelHandle;

        private void Awake()
        {
            _panelHandle = PanelManager.Register("Jukebox", () => SetOpen(false), () => _open);
        }

        private void OnDestroy()
        {
            if (_vm != null) _vm.Changed -= RebuildRows;
            _vm?.Dispose();
            _vm = null;
            if (_modal != null && _modal.canvas != null) Destroy(_modal.canvas);
        }

        private void Update()
        {
            // WO-437: the global 'J' open is gated behind the global DevHotkeys
            // kill-switch (default OFF) — and blocked during battle. ESC-close stays
            // (only acts when this panel is already open).
            if (DeNelle.Core.FeatureFlags.DevHotkeys
                && Input.GetKeyDown(ToggleKey)
                && !DeNelle.Core.Combat.BattleLock.IsInBattle())
                SetOpen(!_open);

            if (_open && Input.GetKeyDown(KeyCode.Escape))
                SetOpen(false);
        }

        // ── UI construction (kit modal, lazy on first open) ─────────────────
        private void EnsureBuilt()
        {
            if (_modal != null && _modal.canvas != null) return;

            _modal = ElarionUiKit.BuildObsidianModal("JukeboxUI", "Jukebox",
                new Vector2(0.30f, 0.14f), new Vector2(0.70f, 0.86f), () => SetOpen(false),
                frameName: RpgUiCatalog.FrameCore, medallionIcon: "music");

            var body = _modal.chrome.layout != null && _modal.chrome.layout.body != null
                ? _modal.chrome.layout.body.transform
                : _modal.chrome.content.transform;

            // EYES-SWEEP 2026-07-06 (#3): the subtitle band was 0.90–1.00 — its first line tucked
            // under the header trim and its wrapped 3rd line spilled into the first track row
            // (rows started at 0.88). Give it a proper band BELOW the header trim, FitBlock so the
            // whole message always fits its band, and start the track list underneath it.
            var subtitle = ElarionUiKit.Label(body,
                "Pick the music for where you are. Battle music still takes over during fights.",
                0.84f, 0.965f, ElarionUi.ParchmentDim, ElarionUi.FontLabel,
                TMPro.TextAlignmentOptions.Center, 0.04f, 0.96f);
            // SWEEP 9413 R2 (#4): default FitBlock clamps min to FontFloor(30) — at FontLabel(40)
            // the band couldn't fit the wrap and Truncate hard-cut "…Battle music s". Taller band
            // (0.84–0.965) + an explicit smaller min so the hint SHRINKS to fit instead of cutting.
            ElarionUiKit.FitBlock(subtitle, 24f, ElarionUi.FontLabel);

            // Rows live in a dedicated host under the body so RebuildRows can clear
            // them without touching the subtitle.
            var hostGo = new GameObject("TrackRows", typeof(RectTransform));
            hostGo.transform.SetParent(body, false);
            var hrt = hostGo.GetComponent<RectTransform>();
            hrt.anchorMin = new Vector2(0f, 0f);
            hrt.anchorMax = new Vector2(1f, 0.82f);
            hrt.offsetMin = Vector2.zero; hrt.offsetMax = Vector2.zero;
            _rowHost = hostGo.transform;

            // VM FIRST — it resolves AudioService itself, so this View never touches a service.
            _vm = JukeboxVM.CreateDefault(() => SetOpen(false));
            _vm.Changed += RebuildRows;

            _modal.canvas.SetActive(false);   // built hidden; SetOpen shows it
        }

        // Rebuilds the track rows for the player's CURRENT ambient context, with a
        // checkmark on the selected one. Called every open so the set + selection
        // are always current.
        // Renders purely from vm.* — no AudioService reads (strict MVVM, Silo E).
        private void RebuildRows()
        {
            if (_rowHost == null) return;
            for (int i = _rowHost.childCount - 1; i >= 0; i--)
                Destroy(_rowHost.GetChild(i).gameObject);

            if (_vm == null || !_vm.AudioReady)
            {
                ElarionUiKit.Label(_rowHost, "Audio not ready.", 0.85f, 0.97f,
                    ElarionUi.Danger, ElarionUi.FontBody,
                    TMPro.TextAlignmentOptions.Center, 0.05f, 0.95f);
                return;
            }

            const float rowH = 0.115f, gap = 0.02f;
            float top = 0.97f;
            foreach (var row in _vm.Tracks)
            {
                MusicTrack track = row.Track;
                bool isSelected = row.IsSelected;
                // Selected row = Green family (the pack's own affirmative), rest Gray.
                // ASCII '>' marker (eyes-on 2026-07-03: the checkmark glyph is missing from the TMP font).
                ElarionUiKit.BuildObsidianButton(_rowHost,
                    (isSelected ? ">  " : "") + row.DisplayName,
                    ElarionUiKit.ObsidianButtonStyle.Style1,
                    isSelected ? ElarionUiKit.ObsidianButtonColor.Green
                               : ElarionUiKit.ObsidianButtonColor.Gray,
                    new Vector2(0.06f, top - rowH), new Vector2(0.94f, top),
                    () => OnPick(track));
                top -= rowH + gap;
                if (top - rowH < 0f) break;   // bounded: never overflow the well
            }
        }

        // Selecting a track routes to the VM command (persists + previews); the VM
        // raises Changed, which re-renders the checkmark via RebuildRows.
        private void OnPick(MusicTrack track)
        {
            _vm?.SetAmbientChoice(track);
        }

        private void SetOpen(bool open)
        {
            if (open) EnsureBuilt();
            if (_modal == null || _modal.canvas == null) { _open = false; return; }
            _open = open;
            _modal.canvas.SetActive(open);
            if (open)
            {
                // Announce open: closes any previously-open panel. Battle-lock may reject
                // (NotifyOpened==false) — revert and stay hidden, never force-show.
                if (!PanelManager.NotifyOpened(_panelHandle))
                {
                    _open = false;
                    _modal.canvas.SetActive(false);
                    return;
                }
                _vm?.Rebuild();   // refresh context + selection on open (raises Changed -> RebuildRows)
            }
            else
            {
                PanelManager.NotifyClosed(_panelHandle);
            }
        }
    }
}
