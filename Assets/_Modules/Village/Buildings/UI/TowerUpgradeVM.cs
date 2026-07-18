// =============================================================================
// TowerUpgradeVM — pure ViewModel for a single tower's upgrade affordance (Silo C).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.UI
//
// Strict-MVVM migration (UI_MVVM_MIGRATION_PLAN.md §1, Silo C): the economy /
// level / cost logic that TowerUpgradeButton.UpdateUI read inline
// (EconomyService.Instance, Tower.Data.upgrades) moves HERE. The button becomes a
// dumb skin: it binds this VM, renders <see cref="ButtonText"/> + <see
// cref="Interactable"/>, and routes its click to <see cref="Upgrade"/>.
//
// PURE C#: it drives a tower through the small <see cref="ITowerUpgradeTarget"/>
// seam (Tower implements it via the adapter below — Tower.cs itself is NOT edited,
// out of Silo C's file set) + an injected <see cref="IEconomy"/>, so §2c tests run
// over fakes with no scene. Behaviour is preserved verbatim: affordability is the
// same Wood-pool gate (EconomyService.CanAfford(int) == Wood >= cost) and the
// upgrade routes through the single cost-enforced Tower.TryUpgrade.
// =============================================================================

using System;

namespace DeNelle.Village.UI
{
    /// <summary>The slice of a placed tower this VM drives — testable without a scene Tower.</summary>
    public interface ITowerUpgradeTarget
    {
        /// <summary>True when the tower is initialised (has TowerData) — else the button reads "Upgrade" disabled.</summary>
        bool HasData { get; }
        /// <summary>Placed level (1..MaxLevel).</summary>
        int CurrentLevel { get; }
        /// <summary>Wood cost to reach the next level (int.MaxValue when maxed / unauthored → never free).</summary>
        int NextUpgradeCost { get; }
        /// <summary>The single cost-enforced upgrade transaction.</summary>
        Tower.UpgradeResult TryUpgrade();
    }

    /// <summary>Wraps a live <see cref="Tower"/> as an <see cref="ITowerUpgradeTarget"/> (Tower is
    /// not edited — this adapter lives entirely in Silo C's own file).</summary>
    public sealed class TowerUpgradeTarget : ITowerUpgradeTarget
    {
        private readonly Tower _tower;
        public TowerUpgradeTarget(Tower tower) { _tower = tower; }
        public bool HasData => _tower != null && _tower.Data != null;
        public int CurrentLevel => _tower != null ? _tower.CurrentLevel : 0;
        public int NextUpgradeCost => _tower != null ? _tower.NextUpgradeCost : int.MaxValue;
        public Tower.UpgradeResult TryUpgrade()
            => _tower != null ? _tower.TryUpgrade() : Tower.UpgradeResult.Uninitialized;
    }

    /// <summary>
    /// ViewModel for the per-tower upgrade button. Projects the button label + interactable state
    /// from the target tower + wallet, and exposes <see cref="Upgrade"/> as the command.
    /// </summary>
    public sealed class TowerUpgradeVM
    {
        private readonly IEconomy _economy;
        private ITowerUpgradeTarget _target;

        public event Action Changed;

        public string ButtonText { get; private set; } = "Upgrade";
        public bool Interactable { get; private set; }
        /// <summary>The level the tower would reach (CurrentLevel + 1), for display.</summary>
        public int NextLevel { get; private set; }
        /// <summary>The Wood cost to reach <see cref="NextLevel"/>.</summary>
        public int Cost { get; private set; }

        /// <summary>Resolves EconomyService.Instance itself (the sole resolution site).</summary>
        public static TowerUpgradeVM CreateDefault() => new TowerUpgradeVM(EconomyService.Instance);

        public TowerUpgradeVM(IEconomy economy)
        {
            _economy = economy;
            Recompute();
        }

        /// <summary>Point the VM at a live tower.</summary>
        public void SetTarget(Tower tower)
        {
            _target = tower != null ? new TowerUpgradeTarget(tower) : null;
            Recompute();
            Raise();
        }

        /// <summary>Point the VM at a target seam (tests / non-Tower callers).</summary>
        public void SetTarget(ITowerUpgradeTarget target)
        {
            _target = target;
            Recompute();
            Raise();
        }

        /// <summary>Perform the upgrade through the single cost-enforced transaction, then re-project.</summary>
        public void Upgrade()
        {
            if (_target != null) _target.TryUpgrade();   // cost gate is internal — never free
            Recompute();
            Raise();
        }

        private void Recompute()
        {
            if (_target == null || !_target.HasData)
            {
                ButtonText = "Upgrade";
                Interactable = false;
                NextLevel = 0;
                Cost = 0;
                return;
            }

            NextLevel = _target.CurrentLevel + 1;
            if (NextLevel > Tower.MaxLevel)
            {
                ButtonText = "Max Level";
                Interactable = false;
                Cost = 0;
                return;
            }

            Cost = _target.NextUpgradeCost;
            // Same gate EconomyService.CanAfford(int) applies — the Wood pool covers the cost.
            bool canAfford = _economy != null && _economy.Wood >= Cost;
            ButtonText = $"Upgrade (L{NextLevel})  {Cost}";
            Interactable = canAfford;
        }

        private void Raise() => Changed?.Invoke();
    }
}
