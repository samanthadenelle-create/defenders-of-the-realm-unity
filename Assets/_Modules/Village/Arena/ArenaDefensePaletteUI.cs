// =============================================================================
// ArenaDefensePaletteUI — the code-built Arena Defense SETUP palette (WO-389 P2).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Arena
//
// The defense-layer twin of BuildPaletteUI: a horizontal strip of the 6 pre-
// placeable Arena DEFENDERS (ArenaDefenseCatalog), each card showing the troop's
// name + POINT cost. The budget is the point POOL (DefensePointPool = 50), NOT
// EconomyService resources — so the top bar shows "remaining points" and a card
// greys out when adding it would exceed the pool. Tapping a card arms the def via
// OnDefSelected; the Done button exits.
//
// CODE-BUILT, NO UXML (repo rule: .uxml UIDocuments render empty in player builds).
// Owns its own UIDocument + adopts a sibling's PanelSettings exactly like
// BuildPaletteUI so it renders. Pure display: the controller owns the point math
// (it passes in the live spent/remaining each Render) so this never double-counts.
// =============================================================================

using System;
using UnityEngine;
using UnityEngine.UIElements;
using DeNelle.Core.UI;

namespace DeNelle.Village.Arena
{
    /// <summary>
    /// The Arena Defense setup palette. Lists <see cref="ArenaDefenseCatalog"/> defs
    /// as tappable cards (name + point cost), shows the remaining pool, and raises
    /// <see cref="OnDefSelected"/> when one is armed. Built in code so it renders in
    /// player builds. Mirrors <c>BuildPaletteUI</c>.
    /// </summary>
    public sealed class ArenaDefensePaletteUI : MonoBehaviour
    {
        /// <summary>Raised when a card is tapped — arg is the armed defender def.</summary>
        public event Action<ArenaDefenseDef> OnDefSelected;

        /// <summary>Raised when the Done button is tapped (exit setup).</summary>
        public event Action OnExitRequested;

        private UIDocument _document;
        private VisualElement _root;
        private VisualElement _strip;
        private Label _pointsLabel;
        private string _armedId;

        // Live budget state, pushed in by the controller each Render so the palette
        // never re-derives the spend (single source of truth = the controller's layout).
        private int _spent;
        private int _remaining = ArenaDefenseCatalog.DefensePointPool;

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
            if (_document == null) _document = gameObject.AddComponent<UIDocument>();
            AdoptPanelSettings();
        }

        /// <summary>
        /// Adopt a sibling UIDocument's PanelSettings so the palette renders (a fresh
        /// doc has none → invisible). Mirrors BuildPaletteUI / BuildMenu.
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
                Debug.LogWarning("[ArenaDefensePaletteUI] No sibling PanelSettings found — palette will not render.");
            }
        }

        // ── Show / Hide ────────────────────────────────────────────────────────

        /// <summary>Show the palette and render it against the supplied budget.</summary>
        public void Show(int spent, int remaining)
        {
            EnsureBuilt();
            if (_root != null) _root.style.display = DisplayStyle.Flex;
            Render(spent, remaining);
        }

        public void Hide()
        {
            if (_root != null) _root.style.display = DisplayStyle.None;
            _armedId = null;
        }

        private void EnsureBuilt()
        {
            if (_root != null) return;
            var docRoot = _document != null ? _document.rootVisualElement : null;
            if (docRoot == null) return;

            _root = new VisualElement { name = "arena-defense-palette-root" };
            _root.style.position = Position.Absolute;
            _root.style.left = 0; _root.style.right = 0; _root.style.bottom = 0;
            _root.style.flexDirection = FlexDirection.Column;
            _root.pickingMode = PickingMode.Ignore;   // click-through; the bar takes taps
            docRoot.Add(_root);

            // Top row: remaining-points readout + Done. Stone bar, gilt under-rule.
            var topBar = new VisualElement();
            topBar.style.flexDirection = FlexDirection.Row;
            topBar.style.justifyContent = Justify.SpaceBetween;
            topBar.style.alignItems = Align.Center;
            topBar.style.paddingLeft = 14; topBar.style.paddingRight = 14;
            topBar.style.paddingTop = 6; topBar.style.paddingBottom = 6;
            topBar.style.backgroundColor = ElarionUi.PanelStone;
            topBar.style.borderBottomWidth = 2;
            topBar.style.borderBottomColor = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.55f);

            _pointsLabel = new Label("Defense Points: 50 / 50");
            _pointsLabel.style.color = ElarionUi.Aether;
            _pointsLabel.style.fontSize = ElarionUi.FontHead;
            _pointsLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            topBar.Add(_pointsLabel);

            var exitBtn = new Button(() => OnExitRequested?.Invoke()) { text = "Done" };
            ElarionUi.StyleButton(exitBtn, ElarionUi.ButtonKind.Gold);
            exitBtn.style.minWidth = 88;
            topBar.Add(exitBtn);
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
        /// Re-draw the cards + the remaining-points readout for the supplied budget. A
        /// card is enabled only when adding its def keeps the spend within the pool
        /// (or it is the currently-armed def, so re-tapping it stays legal).
        /// </summary>
        public void Render(int spent, int remaining)
        {
            _spent = Mathf.Max(0, spent);
            _remaining = remaining;
            EnsureBuilt();
            if (_strip == null) return;

            if (_pointsLabel != null)
                _pointsLabel.text = $"Defense Points: {_remaining} / {ArenaDefenseCatalog.DefensePointPool}";

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
                var none = new Label("No defenders registered.");
                none.style.color = Color.white;
                none.style.paddingLeft = 12; none.style.paddingTop = 12;
                _strip.Add(none);
            }
        }

        private VisualElement BuildCard(ArenaDefenseDef def)
        {
            bool armed = def.Id == _armedId;
            // Affordable = this def's cost fits the REMAINING pool (or it's already armed).
            bool affordable = armed || def.PointCost <= _remaining;

            var card = new Button(() =>
            {
                _armedId = def.Id;
                OnDefSelected?.Invoke(def);
                Render(_spent, _remaining);   // refresh the armed highlight
            });
            card.style.width = 116; card.style.height = 108;
            card.style.marginLeft = 6; card.style.marginRight = 6;
            card.style.marginTop = 8; card.style.marginBottom = 8;
            card.style.paddingTop = 8; card.style.paddingBottom = 8;
            card.style.paddingLeft = 8; card.style.paddingRight = 8;
            card.style.flexDirection = FlexDirection.Column;
            card.style.justifyContent = Justify.SpaceBetween;
            card.style.backgroundColor = armed ? ElarionUi.AetherDim : ElarionUi.PanelStone;
            ElarionUi.SetRadius(card, ElarionUi.RadiusMd);
            ElarionUi.SetBorderWidth(card, armed ? 2 : 1);
            ElarionUi.SetBorderColor(card, armed
                ? ElarionUi.Gilt
                : new Color(ElarionUi.StoneTrim.r, ElarionUi.StoneTrim.g, ElarionUi.StoneTrim.b, 0.5f));
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
