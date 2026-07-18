// =============================================================================
// TowerEmpowerButton — world-space Empower UI that appears at Tower MaxLevel.
// -----------------------------------------------------------------------------
// Place this component on a Button (or a parent containing one) that is part of
// the TowerData.upgradeUIPrefab layout. The component auto-finds the parent Tower
// via GetComponentInParent, so no manual wiring is needed beyond placing it on
// the prefab.
//
// Lifecycle:
//   • Hidden (GameObject disabled) until tower.CurrentLevel == Tower.MaxLevel
//     and !tower.IsEmpowered.
//   • Shows crystal cost + ability name. Greyed-out when balance is insufficient.
//   • On click: calls tower.TryEmpower(). If true → swaps to "Empowered" badge.
//     If false → shows "Need X Crystals" on the status label.
//   • After empowerment the button disables itself — the badge takes its place.
// =============================================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DeNelle.Core.Data;

namespace DeNelle.Village
{
    /// <summary>
    /// World-space UI button that activates tower empowerment at max level.
    /// Add to the TowerData.upgradeUIPrefab canvas alongside the upgrade button.
    /// </summary>
    public sealed class TowerEmpowerButton : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Button elements")]
        [Tooltip("The clickable Button component on this (or a child) GameObject.")]
        [SerializeField] private Button _button;

        [Tooltip("Label showing cost — e.g. '8 Crystals'.")]
        [SerializeField] private TMP_Text _costLabel;

        [Tooltip("Label showing ability name — e.g. 'Mana Surge'.")]
        [SerializeField] private TMP_Text _abilityLabel;

        [Tooltip("Label for feedback — 'Need X Crystals' or 'Empowered!'.")]
        [SerializeField] private TMP_Text _statusLabel;

        [Header("State GameObjects")]
        [Tooltip("Root shown while the button is available (pre-empower state).")]
        [SerializeField] private GameObject _buttonRoot;

        [Tooltip("Root shown after empowerment — replace with a badge / icon.")]
        [SerializeField] private GameObject _empoweredBadge;

        [Header("Polling")]
        [Tooltip("Seconds between affordability re-checks.")]
        [SerializeField, Min(0.1f)] private float _refreshInterval = 0.5f;

        // ── Runtime ───────────────────────────────────────────────────────────

        private Tower _tower;
        private float _nextRefresh;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            _tower = GetComponentInParent<Tower>();
            if (_tower == null)
            {
                Debug.LogWarning("[TowerEmpowerButton] No Tower found in parent hierarchy. " +
                                 "Ensure this component is part of a TowerData.upgradeUIPrefab that " +
                                 "is instantiated as a child of the Tower GameObject.");
                enabled = false;
                return;
            }

            // Auto-find a Button on this or any direct child if not assigned.
            if (_button == null) _button = GetComponentInChildren<Button>(true);

            if (_button != null)
                _button.onClick.AddListener(OnEmpowerClicked);

            // Start hidden; Refresh() will show or hide correctly.
            SetVisible(false);
        }

        private void Update()
        {
            if (Time.time < _nextRefresh) return;
            _nextRefresh = Time.time + _refreshInterval;
            Refresh();
        }

        // ── Visibility / state ────────────────────────────────────────────────

        private void Refresh()
        {
            if (_tower == null) return;

            bool atMaxLevel  = _tower.CurrentLevel >= Tower.MaxLevel;
            bool canEmpower  = atMaxLevel && !_tower.IsEmpowered && HasEmpowermentData();

            if (_tower.IsEmpowered)
            {
                // Already empowered — show badge, hide button.
                SetVisible(false);
                if (_empoweredBadge != null) _empoweredBadge.SetActive(true);
                enabled = false;  // stop polling
                return;
            }

            if (!canEmpower)
            {
                SetVisible(false);
                return;
            }

            // Show the empower button with current cost info.
            SetVisible(true);

            var emp = _tower.Data?.empowerment;
            if (emp == null) return;

            if (_costLabel  != null) _costLabel.text  = $"{emp.crystalCost} Crystals";
            if (_abilityLabel != null) _abilityLabel.text = emp.abilityName;

            bool affordable = CrystalEconomy.Instance != null &&
                              CrystalEconomy.Instance.CanAfford(emp.crystalCost);

            if (_button != null) _button.interactable = affordable;

            if (_statusLabel != null)
            {
                if (!affordable && CrystalEconomy.Instance != null)
                    _statusLabel.text = $"Need {emp.crystalCost} Crystals  " +
                                        $"(have {CrystalEconomy.Instance.CurrentCrystals})";
                else
                    _statusLabel.text = string.Empty;
            }
        }

        // ── Click handler ─────────────────────────────────────────────────────

        private void OnEmpowerClicked()
        {
            if (_tower == null) return;

            bool success = _tower.TryEmpower();
            if (success)
            {
                SetVisible(false);
                if (_empoweredBadge != null) _empoweredBadge.SetActive(true);
                if (_statusLabel != null) _statusLabel.text = string.Empty;
                enabled = false;
            }
            else
            {
                // TryEmpower already logs the reason; mirror a brief on-screen hint.
                if (_statusLabel != null)
                {
                    var emp = _tower.Data?.empowerment;
                    int cost = emp != null ? emp.crystalCost : 0;
                    int have = CrystalEconomy.Instance != null ? CrystalEconomy.Instance.CurrentCrystals : 0;
                    _statusLabel.text = have < cost
                        ? $"Need {cost} Crystals  (have {have})"
                        : "Cannot empower now.";
                }
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void SetVisible(bool visible)
        {
            if (_buttonRoot != null) _buttonRoot.SetActive(visible);
            else gameObject.SetActive(visible);
        }

        private bool HasEmpowermentData()
        {
            var emp = _tower?.Data?.empowerment;
            return emp != null && emp.ability != EmpowermentAbility.None;
        }
    }
}
