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
// (CLAUDE.md §5/§9, WO-162 constraint). It lives in the Audio assembly (not HUD)
// so it can call AudioService directly without crossing the HUD→Core-only rule;
// UIElements is part of the engine, available with no extra asmdef reference.
//
// CODE-BUILT UI (no UXML — PIPELINE_STATE §8: "UXML in builds does NOT work").
// The whole visual tree is constructed in C#. Toggled with the J key in any
// scene that has a hero; spawned by MusicSelectionPanelBootstrap.
//
// Preview + selected-state + persistence are all delegated to AudioService:
//   - tapping a row calls SetAmbientChoice (persists; plays live if in-context)
//   - the chosen row shows a checkmark, read back from GetAmbientChoice on open.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using DeNelle.Core.UI;   // shared Elarion theme — jukebox matches the one game UI

namespace DeNelle.Audio
{
    /// <summary>
    /// A small jukebox panel: lists the curated ambient tracks for the current
    /// context (Village vs Overworld), shows which one is selected, and lets the
    /// player pick one. The pick persists and plays via the existing AudioService
    /// ambient-context path; combat/victory/defeat music still overrides.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class MusicSelectionPanel : MonoBehaviour
    {
        /// <summary>Key that toggles the jukebox panel open/closed.</summary>
        public const KeyCode ToggleKey = KeyCode.J;

        /// <summary>WO-563: public open/toggle so a reachable HUD button (SocialAccessCluster) can
        /// surface the jukebox on touch — the J key is keyboard-only + gated behind DevHotkeys, so
        /// it was unreachable on mobile/WebGL. Mirrors ClanChatPanel/LeaderboardPanel.Toggle().</summary>
        public void Toggle() => SetOpen(!_open);
        public void Open()   => SetOpen(true);

        private UIDocument _doc;
        private VisualElement _overlay;
        private VisualElement _rowList;
        private bool _open;

        // PanelManager mutual-exclusion handle (one panel at a time).
        private DeNelle.Core.UI.PanelHandle _panelHandle;

        // Rebuilt each open so a context change (village -> overworld) shows the
        // right track set and the right checkmark.
        private void Awake()
        {
            _doc = GetComponent<UIDocument>();
            if (_doc == null) _doc = gameObject.AddComponent<UIDocument>();

            if (_doc.panelSettings == null)
            {
                foreach (var existing in FindObjectsByType<UIDocument>(
                             FindObjectsInactive.Include))
                {
                    if (existing == _doc || existing.panelSettings == null) continue;
                    _doc.panelSettings = existing.panelSettings;
                    break;
                }
            }

            if (_doc.panelSettings == null)
            {
                Debug.LogWarning("[MusicSelectionPanel] No PanelSettings available — jukebox hidden.");
                enabled = false;
                return;
            }

            _doc.sortingOrder = 96; // above HUD chips, near the cosmetic shop (95)
            BuildTree();
            // Register with the modal arbiter so opening the jukebox closes any other
            // panel (and vice-versa). Probe = the panel's own open flag.
            _panelHandle = PanelManager.Register("Jukebox", () => SetOpen(false), () => _open);
            SetOpen(false);
        }

        private void Update()
        {
            // WO-437: the global 'J' open is gated behind the global DevHotkeys
            // kill-switch (default OFF) — and blocked during battle — so it no longer
            // spams the "13 windows" in a build and is dead in the editor too unless a
            // dev opts in (PlayerPrefs ff.devhotkeys=1). ESC-close stays (only acts
            // when this panel is already open; it does not steal the key globally).
            if (DeNelle.Core.FeatureFlags.DevHotkeys
                && Input.GetKeyDown(ToggleKey)
                && !DeNelle.Core.Combat.BattleLock.IsInBattle())
                SetOpen(!_open);

            // Escape closes when open (does not steal the key globally).
            if (_open && Input.GetKeyDown(KeyCode.Escape))
                SetOpen(false);
        }

        // ── UI construction (code-built, no UXML) ────────────────────────────

        private void BuildTree()
        {
            var root = _doc.rootVisualElement;
            root.Clear();
            root.style.flexGrow = 1;

            // Full-screen dim scrim that centres the panel card (shared palette).
            _overlay = new VisualElement();
            _overlay.style.flexGrow = 1;
            _overlay.style.justifyContent = Justify.Center;
            _overlay.style.alignItems = Align.Center;
            _overlay.style.backgroundColor = ElarionUi.Scrim;
            root.Add(_overlay);

            // Warm-stone card with a runic-gold rim (the one game UI language).
            var card = new VisualElement();
            card.style.width = 360;
            card.style.paddingTop = 16;
            card.style.paddingBottom = 16;
            card.style.paddingLeft = 18;
            card.style.paddingRight = 18;
            ElarionUi.StylePanel(card, dark: true);
            _overlay.Add(card);

            // Gilt crest title + a single gold underline rule.
            card.Add(ElarionUi.MakeTitle("Jukebox"));
            card.Add(ElarionUi.MakeRule());

            var sub = new Label("Pick the music for where you are. Battle music still takes over during fights.");
            sub.style.fontSize = ElarionUi.FontLabel;
            sub.style.color = ElarionUi.ParchmentDim;
            sub.style.whiteSpace = WhiteSpace.Normal;
            sub.style.marginBottom = 10;
            card.Add(sub);

            _rowList = new VisualElement();
            card.Add(_rowList);

            var close = new Button(() => SetOpen(false)) { text = "Close" };
            ElarionUi.StyleButton(close, ElarionUi.ButtonKind.Gold);
            close.style.marginTop = 12;
            card.Add(close);
        }

        // Rebuilds the track rows for the player's CURRENT ambient context, with a
        // checkmark on the selected one. Called every open so the set + selection
        // are always current.
        private void RebuildRows()
        {
            if (_rowList == null) return;
            _rowList.Clear();

            var svc = AudioService.Instance;
            if (svc == null)
            {
                var warn = new Label("Audio not ready.");
                warn.style.color = ElarionUi.Danger;
                _rowList.Add(warn);
                return;
            }

            AudioService.AmbientContext context = svc.CurrentAmbientContext;
            MusicTrack chosen = svc.GetAmbientChoice(context);
            // None => the player hasn't picked; the context default is effectively selected.
            if (chosen == MusicTrack.None)
                chosen = AudioService.DefaultTrackFor(context);

            IReadOnlyList<MusicChoice> choices = AudioService.AmbientChoicesFor(context);
            foreach (var choice in choices)
            {
                MusicTrack track = choice.Track;
                var row = new Button(() => OnPick(track)) { text = string.Empty };
                StyleRow(row, isSelected: track == chosen);

                var name = new Label(choice.DisplayName);
                name.style.flexGrow = 1;
                name.style.fontSize = ElarionUi.FontBody;
                name.style.color = ElarionUi.Parchment;
                row.Add(name);

                if (track == chosen)
                {
                    var check = new Label("✓"); // ✓
                    check.style.fontSize = 16;
                    check.style.color = ElarionUi.Affordable;
                    row.Add(check);
                }

                _rowList.Add(row);
            }
        }

        // Selecting a track persists the pick and previews it immediately (the
        // service plays it live when the player is in this ambient context and not
        // mid-combat-cue — exactly the WO-162 "previews + shows as selected" spec).
        private void OnPick(MusicTrack track)
        {
            var svc = AudioService.Instance;
            if (svc == null) return;
            svc.SetAmbientChoice(svc.CurrentAmbientContext, track);
            RebuildRows(); // refresh the checkmark
        }

        private void SetOpen(bool open)
        {
            _open = open;
            if (_overlay != null)
                _overlay.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
            if (open)
            {
                // Announce open: closes any previously-open panel. Battle-lock may reject
                // (NotifyOpened==false) — revert and stay hidden, never force-show.
                if (!PanelManager.NotifyOpened(_panelHandle))
                {
                    _open = false;
                    if (_overlay != null) _overlay.style.display = DisplayStyle.None;
                    return;
                }
                RebuildRows();
            }
            else
            {
                PanelManager.NotifyClosed(_panelHandle);
            }
        }

        // ── Style helpers ────────────────────────────────────────────────────

        private static void StyleRow(Button row, bool isSelected)
        {
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.height = 40;
            row.style.marginTop = 4;
            row.style.marginBottom = 0;
            row.style.paddingLeft = 12;
            row.style.paddingRight = 12;
            // Selected row glows aether-violet (the runic accent); rest is warm stone.
            row.style.backgroundColor = isSelected ? ElarionUi.AetherDim : ElarionUi.PanelStone;
            ElarionUi.SetRadius(row, ElarionUi.RadiusSm);
            ElarionUi.SetBorderWidth(row, 1);
            ElarionUi.SetBorderColor(row, isSelected
                ? new Color(ElarionUi.Aether.r, ElarionUi.Aether.g, ElarionUi.Aether.b, 0.7f)
                : new Color(ElarionUi.StoneTrim.r, ElarionUi.StoneTrim.g, ElarionUi.StoneTrim.b, 0.4f));
        }
    }
}
