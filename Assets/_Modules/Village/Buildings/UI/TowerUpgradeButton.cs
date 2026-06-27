// =============================================================================
// TowerUpgradeButton — DEF-74 (Linear). UI affordance that targets a Tower and
// upgrades it with cost validation.
// -----------------------------------------------------------------------------
// namespace DeNelle.Village.UI. Bound to a tower via SetTargetTower(Tower); the
// button is interactable only when the tower is below max level AND the player can
// afford the next upgrade's cost (DEF-74 CP1 Issue 9). On click it re-checks
// affordability, spends, and calls Tower.Upgrade().
//
// ADAPTATION (mandatory — codebase deviation from the spec): the spec calls for
// TextMeshProUGUI / uGUI, but in this project UXML-source UIDocuments render EMPTY
// in player builds and uGUI needs a Canvas + EventSystem. So this is built as a
// CODE Button (UnityEngine.UIElements) on a small code-built UIDocument — the same
// pattern as DeNelle.Settings.MusicToggleHud. The economy/level logic from the
// spec's OnUpgradeClicked()/UpdateUI() is preserved verbatim; only the widget
// toolkit differs.
// =============================================================================

using UnityEngine;
using UnityEngine.UIElements;
using DeNelle.Core.Data;

namespace DeNelle.Village.UI
{
    /// <summary>
    /// A UI Toolkit upgrade button for a single <see cref="Tower"/>. Attach to a
    /// GameObject with a UIDocument (or let it build its own root); call
    /// <see cref="SetTargetTower"/> to point it at a tower.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class TowerUpgradeButton : MonoBehaviour
    {
        private Tower _selectedTower;
        private Button _button;

        private void OnEnable()
        {
            var doc = GetComponent<UIDocument>();
            var root = doc != null ? doc.rootVisualElement : null;
            if (root == null) return;

            if (_button == null)
            {
                _button = new Button(OnUpgradeClicked) { name = "tower-upgrade-button" };
                var s = _button.style;
                s.position = Position.Absolute;
                s.bottom = 80f; s.right = 14f;
                s.minWidth = 160f; s.height = 44f;
                s.fontSize = 16f;
                s.unityFontStyleAndWeight = FontStyle.Bold;
                s.unityTextAlign = TextAnchor.MiddleCenter;
                s.color = Color.white;
                s.backgroundColor = new Color(0.16f, 0.40f, 0.62f, 0.95f);
                s.borderTopLeftRadius = 8f; s.borderTopRightRadius = 8f;
                s.borderBottomLeftRadius = 8f; s.borderBottomRightRadius = 8f;
                root.Add(_button);
            }
            UpdateUI();
        }

        /// <summary>Point this button at the tower it should upgrade.</summary>
        public void SetTargetTower(Tower tower)
        {
            _selectedTower = tower;
            UpdateUI();
        }

        /// <summary>
        /// Routes to the single cost-enforced transaction (owner 2026-06-27, tower-upgrade
        /// CONSOLIDATION): the cost gate now lives INSIDE Tower.TryUpgrade so it can never
        /// be bypassed regardless of caller. This dumb view just calls it and reflects the
        /// result. NOTE: this per-tower button is NO LONGER the canonical surface — the
        /// canonical upgrade affordance is the shared proximity HUD context button
        /// (TowerInteractable -> HudBuildingFocus -> Tower.TryUpgrade). Kept (cost-enforced)
        /// only so an authored upgradeUIPrefab, if re-introduced, stays safe.
        /// </summary>
        public void OnUpgradeClicked()
        {
            if (_selectedTower == null) return;
            _selectedTower.TryUpgrade();   // cost gate is internal — never free
            UpdateUI();
        }

        /// <summary>Reflect target state: interactable only if level &lt; 3 AND affordable.</summary>
        public void UpdateUI()
        {
            if (_button == null) return;

            if (_selectedTower == null || _selectedTower.Data == null)
            {
                _button.text = "Upgrade";
                _button.SetEnabled(false);
                return;
            }

            int nextLevel = _selectedTower.CurrentLevel + 1;
            if (nextLevel > Tower.MaxLevel)
            {
                _button.text = "Max Level";
                _button.SetEnabled(false);
                return;
            }

            int cost = NextUpgradeCost(nextLevel);
            bool canAfford = EconomyService.Instance != null && EconomyService.Instance.CanAfford(cost);
            _button.text = $"Upgrade (L{nextLevel})  {cost}";
            _button.SetEnabled(canAfford);   // UI Toolkit equivalent of Button.interactable
        }

        /// <summary>Cost to upgrade INTO <paramref name="nextLevel"/> (bounds-safe).</summary>
        private int NextUpgradeCost(int nextLevel)
        {
            var upgrades = _selectedTower.Data.upgrades;
            int idx = nextLevel - 1;
            if (upgrades == null || idx < 0 || idx >= upgrades.Length || upgrades[idx] == null)
                return int.MaxValue;   // unknown cost → unaffordable, never free
            return upgrades[idx].upgradeCost;
        }
    }
}
