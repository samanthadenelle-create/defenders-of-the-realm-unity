// =============================================================================
// TowerInteractable — proximity upgrade affordance for a placed Tower.
// -----------------------------------------------------------------------------
// Owner directive 2026-06-27 (tower-upgrade CONSOLIDATION): there is ONE canonical
// tower-upgrade surface and it is the SAME proximity context button the buildings
// already use — NOT a per-tower world button, NOT a free menu. When the hero gets
// close to an UPGRADABLE tower (not maxed, next cost known), the HUD's bottom
// context (diamond) button swaps Quest -> Upgrade (exactly like approaching an
// upgradable building); tapping it runs the cost-enforced Tower.TryUpgrade; when
// the tower is maxed or the hero walks away it reverts to the Quest face.
//
// HOW IT SHARES THE BUILDING AFFORDANCE (no new surface): the HUD assembly may not
// reference Village/Tower, so this Village component WRITES the cross-assembly
// DeNelle.Core.UI.HudBuildingFocus signal — Set(id, action) attaches this tower's
// Tower.TryUpgrade as the custom upgrade action. VillageHudController reads the id
// (to swap the face + light the comet) and invokes the action on tap. This mirrors
// BuildingInteractable's HudBuildingFocus.Set/Clear proximity poll exactly.
//
// Auto-added to every tower by Tower.Initialize (EnsureUpgradeInteractable), so
// deprecating the BuildMenu "Upgrade Tower" screen + the TowerManagerPanel Upgrade
// button never leaves a tower un-upgradable.
// =============================================================================

using DeNelle.Core.Diagnostics;
using DeNelle.Core.UI;
using UnityEngine;

namespace DeNelle.Village
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Tower))]
    public sealed class TowerInteractable : MonoBehaviour
    {
        // Matches BuildingInteractable.ActivateRadius so towers and buildings claim the
        // upgrade focus at the same hero distance (one consistent approach feel).
        private const float ActivateRadius = 6f;

        private Tower _tower;
        private Transform _hero;
        private string _focusId;     // unique per-tower HudBuildingFocus id
        private bool _focusHeld;     // true while THIS tower holds the HUD upgrade focus

        private void Awake()
        {
            _tower = GetComponent<Tower>();
            // Stable, collision-free id (the string is only used for face-swap + Clear matching;
            // the actual upgrade runs through the injected action, not a panel lookup by id).
            _focusId = "tower:" + GetInstanceID();
        }

        private void Start() => ResolveHero();

        private void ResolveHero()
        {
            // Reflection-free direct find — HeroLocomotion lives in this asmdef.
            var hero = FindObjectOfType<HeroLocomotion>();
            if (hero != null) _hero = hero.transform;
        }

        private void Update()
        {
            if (_hero == null) { ResolveHero(); if (_hero == null) return; }

            // Build mode: the player is AUTHORING (placing structures), not interacting —
            // release the focus and skip, mirroring BuildingInteractable. Restored when
            // build mode exits because this Update resumes claiming the focus in range.
            if (MobileInteractButton.Suppressed)
            {
                ReleaseFocus();
                return;
            }

            float distSqr = (_hero.position - transform.position).sqrMagnitude;
            bool inRange = distSqr <= ActivateRadius * ActivateRadius;

            // Upgradable = not maxed AND next-level cost is known (Tower.CanUpgrade). At max
            // level / unknown cost this is false, so the focus releases and the HUD reverts
            // to the Quest face (consistent with the maxed-upgrade-circle fix for buildings).
            bool want = inRange && _tower != null && _tower.CanUpgrade;

            if (want)
            {
                // Claim the shared context button with THIS tower's cost-enforced transaction.
                HudBuildingFocus.Set(_focusId, RunUpgrade);
            }
            else if (_focusHeld)
            {
                ReleaseFocus();
            }

            if (want != _focusHeld)
            {
                _focusHeld = want;
                FlowTrace.Step("Tower", "UpgradeFocus " + (want ? "SET" : "CLEAR") +
                    " id='" + _focusId + "' (inRange=" + inRange +
                    ", canUpgrade=" + (_tower != null && _tower.CanUpgrade) + ").");
            }
        }

        /// <summary>HUD context-button tap handler — runs the cost-gated transaction.</summary>
        private void RunUpgrade()
        {
            if (_tower == null) return;
            var result = _tower.TryUpgrade();
            FlowTrace.Step("Tower", "UpgradeFocus tap -> TryUpgrade result=" + result + " (id='" + _focusId + "').");
            // If the tap maxed the tower, the next Update sees CanUpgrade==false and releases
            // the focus, reverting the HUD to the Quest face — no extra handling needed here.
        }

        private void ReleaseFocus()
        {
            HudBuildingFocus.Clear(_focusId);   // only clears if WE hold it (last-writer-safe)
            _focusHeld = false;
        }

        private void OnDisable() => ReleaseFocus();
        private void OnDestroy() => ReleaseFocus();   // don't leave the HUD focused on a gone tower
    }
}
