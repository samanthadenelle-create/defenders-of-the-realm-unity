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

        private UIDocument _doc;
        private VisualElement _overlay;
        private VisualElement _rowList;
        private bool _open;

        // Rebuilt each open so a context change (village -> overworld) shows the
        // right track set and the right checkmark.
        private void Awake()
        {
            _doc = GetComponent<UIDocument>();
            if (_doc == null) _doc = gameObject.AddComponent<UIDocument>();

            if (_doc.panelSettings == null)
            {
                foreach (var existing in FindObjectsByType<UIDocument>(
                             FindObjectsInactive.Include, FindObjectsSortMode.None))
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
            SetOpen(false);
        }

        private void Update()
        {
            if (Input.GetKeyDown(ToggleKey))
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

            // Full-screen dim overlay that centres the panel card.
            _overlay = new VisualElement();
            _overlay.style.flexGrow = 1;
            _overlay.style.justifyContent = Justify.Center;
            _overlay.style.alignItems = Align.Center;
            _overlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.55f);
            root.Add(_overlay);

            var card = new VisualElement();
            card.style.width = 360;
            card.style.paddingTop = 16;
            card.style.paddingBottom = 16;
            card.style.paddingLeft = 18;
            card.style.paddingRight = 18;
            card.style.backgroundColor = new Color(0.10f, 0.09f, 0.16f, 0.96f);
            SetBorderRadius(card, 12);
            _overlay.Add(card);

            var title = new Label("Jukebox");
            title.style.fontSize = 20;
            title.style.color = new Color(0.85f, 0.80f, 1f, 1f);
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 4;
            card.Add(title);

            var sub = new Label("Pick the music for where you are. Battle music still takes over during fights.");
            sub.style.fontSize = 11;
            sub.style.color = new Color(0.7f, 0.7f, 0.78f, 1f);
            sub.style.whiteSpace = WhiteSpace.Normal;
            sub.style.marginBottom = 10;
            card.Add(sub);

            _rowList = new VisualElement();
            card.Add(_rowList);

            var close = new Button(() => SetOpen(false)) { text = "Close" };
            StyleButton(close);
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
                warn.style.color = new Color(0.8f, 0.6f, 0.6f, 1f);
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
                name.style.fontSize = 14;
                name.style.color = Color.white;
                row.Add(name);

                if (track == chosen)
                {
                    var check = new Label("✓"); // ✓
                    check.style.fontSize = 16;
                    check.style.color = new Color(0.55f, 0.9f, 0.55f, 1f);
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
            if (open) RebuildRows();
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
            row.style.backgroundColor = isSelected
                ? new Color(0.28f, 0.24f, 0.46f, 1f)
                : new Color(0.16f, 0.15f, 0.24f, 1f);
            SetBorderRadius(row, 8);
            SetBorderWidth(row, 0);
        }

        private static void StyleButton(Button b)
        {
            b.style.height = 34;
            b.style.color = Color.white;
            b.style.backgroundColor = new Color(0.22f, 0.20f, 0.34f, 1f);
            SetBorderRadius(b, 8);
            SetBorderWidth(b, 0);
        }

        private static void SetBorderRadius(VisualElement e, float r)
        {
            e.style.borderTopLeftRadius = r;
            e.style.borderTopRightRadius = r;
            e.style.borderBottomLeftRadius = r;
            e.style.borderBottomRightRadius = r;
        }

        private static void SetBorderWidth(VisualElement e, float w)
        {
            e.style.borderTopWidth = w;
            e.style.borderBottomWidth = w;
            e.style.borderLeftWidth = w;
            e.style.borderRightWidth = w;
        }
    }
}
