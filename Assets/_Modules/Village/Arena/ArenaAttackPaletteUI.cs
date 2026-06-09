// =============================================================================
// ArenaAttackPaletteUI — the code-built Arena ATTACK recruit palette (WO-389 #3).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Arena
//
// The ATTACK twin of ArenaDefensePaletteUI. SIMPLER: there is no grid placement —
// the player RECRUITS a roster (a List<string> of troop ids), each tap ADDS a
// troop to the squad (spending its PointCost), and the squad sum must stay <= the
// point pool (DefensePointPool = 50, reused as the squad budget). The top bar
// shows "Squad Points: N / 50" and a Launch button to start the raid; each card
// is a recruit (+1) tap, greyed when adding it would exceed the pool.
//
// CODE-BUILT, NO UXML (repo rule: .uxml UIDocuments render empty in player builds).
// Adopts a sibling's PanelSettings exactly like ArenaDefensePaletteUI so it
// renders. Pure display: the controller owns the squad list + point math and pushes
// the live spent/remaining each Render, so this never double-counts.
// =============================================================================

using System;
using UnityEngine;
using UnityEngine.UIElements;
using DeNelle.Core.UI;

namespace DeNelle.Village.Arena
{
    /// <summary>
    /// The Arena Attack recruit palette. Lists <see cref="ArenaDefenseCatalog"/> defs
    /// as tappable RECRUIT cards (name + point cost), shows the remaining pool, and
    /// raises <see cref="OnRecruit"/> when one is tapped. Built in code so it renders
    /// in player builds. Mirrors <c>ArenaDefensePaletteUI</c>, minus grid placement.
    /// </summary>
    public sealed class ArenaAttackPaletteUI : MonoBehaviour
    {
        /// <summary>Raised when a card is tapped to recruit a troop — arg is the def.</summary>
        public event Action<ArenaDefenseDef> OnRecruit;

        /// <summary>Raised when the Launch button is tapped (start the raid).</summary>
        public event Action OnLaunchRequested;

        /// <summary>Raised when the Cancel button is tapped (abandon recruiting).</summary>
        public event Action OnCancelRequested;

        private UIDocument _document;
        private VisualElement _root;
        private VisualElement _strip;
        private Label _pointsLabel;

        // Live budget state, pushed in by the controller each Render so the palette
        // never re-derives the spend (single source of truth = the controller's squad).
        private int _spent;
        private int _remaining = ArenaDefenseCatalog.DefensePointPool;
        private int _squadCount;

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
            if (_document == null) _document = gameObject.AddComponent<UIDocument>();
            AdoptPanelSettings();
        }

        /// <summary>
        /// Adopt a sibling UIDocument's PanelSettings so the palette renders (a fresh
        /// doc has none → invisible). Mirrors ArenaDefensePaletteUI / BuildPaletteUI.
        /// </summary>
        private void AdoptPanelSettings()
        {
            if (_document == null) return;
            UIDocument hud = null, any = null;
            foreach (var doc in FindObjectsByType<UIDocument>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (doc == null || doc == _document || doc.panelSettings == null) continue;
                if (any == null) any = doc;
                if (doc.gameObject.name.IndexOf("Hud", StringComparison.OrdinalIgnoreCase) >= 0) { hud = doc; break; }
            }
            var src = hud ?? any;
            if (src != null)
            {
                _document.panelSettings = src.panelSettings;
                _document.sortingOrder = src.sortingOrder + 6;   // above HUD
            }
            else
            {
                Debug.LogWarning("[ArenaAttackPaletteUI] No sibling PanelSettings found — palette will not render.");
            }
        }

        // ── Show / Hide ────────────────────────────────────────────────────────

        /// <summary>Show the palette and render it against the supplied budget.</summary>
        public void Show(int spent, int remaining, int squadCount)
        {
            EnsureBuilt();
            if (_root != null) _root.style.display = DisplayStyle.Flex;
            Render(spent, remaining, squadCount);
        }

        public void Hide()
        {
            if (_root != null) _root.style.display = DisplayStyle.None;
        }

        private void EnsureBuilt()
        {
            if (_root != null) return;
            var docRoot = _document != null ? _document.rootVisualElement : null;
            if (docRoot == null) return;

            _root = new VisualElement { name = "arena-attack-palette-root" };
            _root.style.position = Position.Absolute;
            _root.style.left = 0; _root.style.right = 0; _root.style.bottom = 0;
            _root.style.flexDirection = FlexDirection.Column;
            _root.pickingMode = PickingMode.Ignore;   // click-through; the bar takes taps
            docRoot.Add(_root);

            // Top row: remaining-points readout + Launch / Cancel. Stone bar, gilt under-rule.
            var topBar = new VisualElement();
            topBar.style.flexDirection = FlexDirection.Row;
            topBar.style.justifyContent = Justify.SpaceBetween;
            topBar.style.alignItems = Align.Center;
            topBar.style.paddingLeft = 14; topBar.style.paddingRight = 14;
            topBar.style.paddingTop = 6; topBar.style.paddingBottom = 6;
            topBar.style.backgroundColor = ElarionUi.PanelStone;
            topBar.style.borderBottomWidth = 2;
            topBar.style.borderBottomColor = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.55f);

            _pointsLabel = new Label("Squad Points: 0 / 50");
            _pointsLabel.style.color = ElarionUi.Aether;
            _pointsLabel.style.fontSize = ElarionUi.FontHead;
            _pointsLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            topBar.Add(_pointsLabel);

            var rightGroup = new VisualElement();
            rightGroup.style.flexDirection = FlexDirection.Row;
            rightGroup.style.alignItems = Align.Center;

            var cancelBtn = new Button(() => OnCancelRequested?.Invoke()) { text = "Cancel" };
            ElarionUi.StyleButton(cancelBtn, ElarionUi.ButtonKind.Neutral);
            cancelBtn.style.minWidth = 88;
            cancelBtn.style.marginRight = 8;
            rightGroup.Add(cancelBtn);

            var launchBtn = new Button(() => OnLaunchRequested?.Invoke()) { text = "Launch Raid" };
            ElarionUi.StyleButton(launchBtn, ElarionUi.ButtonKind.Gold);
            launchBtn.style.minWidth = 120;
            rightGroup.Add(launchBtn);

            topBar.Add(rightGroup);
            _root.Add(topBar);

            // Bottom row: horizontal card strip in a recessed stone tray.
            var scroll = new ScrollView(ScrollViewMode.Horizontal);
            scroll.style.height = 128;
            scroll.style.backgroundColor = ElarionUi.PanelStoneDark;
            scroll.style.paddingTop = 4; scroll.style.paddingBottom = 4;
            scroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            scroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            _strip = scroll.contentContainer;
            _strip.style.flexDirection = FlexDirection.Row;
            _root.Add(scroll);
        }

        // ── Render ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Re-draw the recruit cards + the remaining-points readout for the supplied
        /// budget. A card is enabled only when recruiting its def keeps the squad spend
        /// within the pool.
        /// </summary>
        public void Render(int spent, int remaining, int squadCount)
        {
            _spent = Mathf.Max(0, spent);
            _remaining = remaining;
            _squadCount = Mathf.Max(0, squadCount);
            EnsureBuilt();
            if (_strip == null) return;

            if (_pointsLabel != null)
                _pointsLabel.text = $"Squad Points: {_spent} / {ArenaDefenseCatalog.DefensePointPool}" +
                                    (_squadCount > 0 ? $"   ({_squadCount} units)" : "");

            _strip.Clear();
            int cards = 0;
            foreach (var def in ArenaDefenseCatalog.All)
            {
                if (def == null) continue;
                _strip.Add(BuildCard(def));
                cards++;
            }

            if (cards == 0)
            {
                var none = new Label("No troops registered.");
                none.style.color = Color.white;
                none.style.paddingLeft = 12; none.style.paddingTop = 12;
                _strip.Add(none);
            }
        }

        private VisualElement BuildCard(ArenaDefenseDef def)
        {
            // Affordable = this def's cost fits the REMAINING pool (a recruit always ADDS).
            bool affordable = def.PointCost <= _remaining;

            var card = new Button(() =>
            {
                OnRecruit?.Invoke(def);
                // The controller mutates the squad + re-pushes the budget via Render.
            });
            card.style.width = 116; card.style.height = 108;
            card.style.marginLeft = 6; card.style.marginRight = 6;
            card.style.marginTop = 8; card.style.marginBottom = 8;
            card.style.paddingTop = 8; card.style.paddingBottom = 8;
            card.style.paddingLeft = 8; card.style.paddingRight = 8;
            card.style.flexDirection = FlexDirection.Column;
            card.style.justifyContent = Justify.SpaceBetween;
            card.style.backgroundColor = ElarionUi.PanelStone;
            ElarionUi.SetRadius(card, ElarionUi.RadiusMd);
            ElarionUi.SetBorderWidth(card, 1);
            ElarionUi.SetBorderColor(card,
                new Color(ElarionUi.StoneTrim.r, ElarionUi.StoneTrim.g, ElarionUi.StoneTrim.b, 0.5f));
            card.style.opacity = affordable ? 1f : 0.45f;
            card.SetEnabled(affordable);

            var nameLabel = new Label(string.IsNullOrEmpty(def.DisplayName) ? def.Id : def.DisplayName);
            nameLabel.style.color = ElarionUi.Parchment;
            nameLabel.style.fontSize = ElarionUi.FontLabel;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.whiteSpace = WhiteSpace.Normal;
            card.Add(nameLabel);

            var costLabel = new Label(def.PointCost + " pts");
            costLabel.style.color = affordable ? ElarionUi.Affordable : ElarionUi.Danger;
            costLabel.style.fontSize = ElarionUi.FontLabel;
            costLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            costLabel.style.whiteSpace = WhiteSpace.Normal;
            card.Add(costLabel);

            return card;
        }
    }
}
