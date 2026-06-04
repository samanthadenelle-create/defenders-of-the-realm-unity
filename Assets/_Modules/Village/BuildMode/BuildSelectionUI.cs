// =============================================================================
// BuildSelectionUI — the code-built Move/Sell action panel for Build Mode (WO-108 P2).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// When the player taps a placed structure (armed == null), BuildModeController
// selects it and shows this small action panel: MOVE / SELL (with its 50%-refund
// amount) / CANCEL. It raises callbacks the controller wires to its move + sell +
// deselect verbs. The placement palette stays the CREATE strip; this is the EDIT
// panel for an already-placed structure.
//
// CODE-BUILT, NO UXML (repo rule: .uxml UIDocuments render empty in player builds).
// Adopts a sibling UIDocument's PanelSettings the same way BuildPaletteUI does so
// it renders, and sorts ABOVE the palette.
// =============================================================================

using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeNelle.Village
{
    /// <summary>
    /// The Build Mode edit-action panel for a selected structure: Move / Sell /
    /// Cancel. Built in code so it renders in player builds; mirrors BuildPaletteUI.
    /// </summary>
    public sealed class BuildSelectionUI : MonoBehaviour
    {
        /// <summary>Raised when MOVE is tapped — enter the re-placement loop.</summary>
        public event Action OnMoveRequested;

        /// <summary>Raised when SELL is tapped — free + remove + refund.</summary>
        public event Action OnSellRequested;

        /// <summary>Raised when UPGRADE is tapped — spend the tier cost + level up (S5).</summary>
        public event Action OnUpgradeRequested;

        /// <summary>Raised when CANCEL is tapped — clear the selection.</summary>
        public event Action OnCancelRequested;

        private UIDocument _document;
        private VisualElement _root;
        private Label _titleLabel;
        private Button _sellBtn;
        private Button _upgradeBtn;

        private void Awake()
        {
            _document = GetComponent<UIDocument>();
            if (_document == null) _document = gameObject.AddComponent<UIDocument>();
            AdoptPanelSettings();
        }

        /// <summary>
        /// Adopt a sibling UIDocument's PanelSettings so the panel renders (mirrors
        /// BuildPaletteUI). Sorts above the palette so the action panel is on top.
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
                _document.sortingOrder = src.sortingOrder + 8;   // above HUD + palette
            }
            else
            {
                Debug.LogWarning("[BuildSelectionUI] No sibling PanelSettings found — panel will not render.");
            }
        }

        // ── Show / Hide ────────────────────────────────────────────────────────

        /// <summary>
        /// Show the action panel for a structure (S5: with its upgrade state). The title
        /// carries the current/max tier; the Upgrade button shows the next-tier cost, greys +
        /// reads "MAX" at the ceiling, and greys when the player can't yet afford the step.
        /// </summary>
        /// <param name="structureName">Display label.</param>
        /// <param name="refund">Sell refund total (units across all pools).</param>
        /// <param name="level">Current upgrade level (1-based).</param>
        /// <param name="maxLevel">Highest level this structure can reach (1 = not upgradeable).</param>
        /// <param name="upgradeCostTotal">Total units of the next-tier upgrade cost (for display).</param>
        /// <param name="canAffordUpgrade">True when the player can pay the next-tier cost now.</param>
        public void Show(string structureName, int refund, int level, int maxLevel,
                         int upgradeCostTotal, bool canAffordUpgrade)
        {
            EnsureBuilt();

            int lvl = Mathf.Max(1, level);
            int max = Mathf.Max(1, maxLevel);

            if (_titleLabel != null)
            {
                string baseName = string.IsNullOrEmpty(structureName) ? "Structure" : structureName;
                _titleLabel.text = max > 1 ? $"{baseName}  (Lv {lvl}/{max})" : baseName;
            }
            if (_sellBtn != null)
                _sellBtn.text = "Sell  ◆ " + Mathf.Max(0, refund);

            if (_upgradeBtn != null)
            {
                bool upgradeable = max > 1;
                _upgradeBtn.style.display = upgradeable ? DisplayStyle.Flex : DisplayStyle.None;
                if (upgradeable)
                {
                    bool atMax = lvl >= max;
                    if (atMax)
                    {
                        _upgradeBtn.text = "Max Tier";
                        _upgradeBtn.SetEnabled(false);
                    }
                    else
                    {
                        _upgradeBtn.text = "Upgrade  ◆ " + Mathf.Max(0, upgradeCostTotal);
                        _upgradeBtn.SetEnabled(canAffordUpgrade);
                    }
                }
            }

            if (_root != null) _root.style.display = DisplayStyle.Flex;
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

            _root = new VisualElement { name = "build-selection-root" };
            _root.style.position = Position.Absolute;
            // Anchored centre-top so it never overlaps the bottom palette strip.
            _root.style.top = 90;
            _root.style.left = 0; _root.style.right = 0;
            _root.style.flexDirection = FlexDirection.Row;
            _root.style.justifyContent = Justify.Center;
            _root.pickingMode = PickingMode.Ignore;   // click-through except the bar
            docRoot.Add(_root);

            var bar = new VisualElement();
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.alignItems = Align.Center;
            bar.style.paddingLeft = 12; bar.style.paddingRight = 12;
            bar.style.paddingTop = 8; bar.style.paddingBottom = 8;
            bar.style.backgroundColor = new Color(0.06f, 0.08f, 0.14f, 0.95f);
            _root.Add(bar);

            _titleLabel = new Label("Structure");
            _titleLabel.style.color = Color.white;
            _titleLabel.style.fontSize = 15;
            _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _titleLabel.style.marginRight = 14;
            bar.Add(_titleLabel);

            var moveBtn = new Button(() => OnMoveRequested?.Invoke()) { text = "Move" };
            StyleButton(moveBtn, new Color(0.20f, 0.42f, 0.66f, 0.98f));
            bar.Add(moveBtn);

            // S5 — the UPGRADE verb (the CoC sink). Hidden for non-upgradeable structures
            // (maxLevel == 1) and disabled at the tier ceiling / when unaffordable; Show()
            // sets its text + enabled state per the selected structure's tier each time.
            _upgradeBtn = new Button(() => OnUpgradeRequested?.Invoke()) { text = "Upgrade" };
            StyleButton(_upgradeBtn, new Color(0.24f, 0.56f, 0.30f, 0.98f));
            bar.Add(_upgradeBtn);

            _sellBtn = new Button(() => OnSellRequested?.Invoke()) { text = "Sell" };
            StyleButton(_sellBtn, new Color(0.62f, 0.40f, 0.20f, 0.98f));
            bar.Add(_sellBtn);

            var cancelBtn = new Button(() => OnCancelRequested?.Invoke()) { text = "Cancel" };
            StyleButton(cancelBtn, new Color(0.30f, 0.32f, 0.40f, 0.98f));
            bar.Add(cancelBtn);
        }

        private static void StyleButton(Button b, Color bg)
        {
            b.style.height = 32; b.style.minWidth = 84;
            b.style.marginLeft = 4; b.style.marginRight = 4;
            b.style.color = Color.white;
            b.style.backgroundColor = bg;
        }
    }
}
