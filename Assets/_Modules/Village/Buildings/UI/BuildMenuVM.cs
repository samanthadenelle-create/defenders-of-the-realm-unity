// =============================================================================
// BuildMenuVM — pure ViewModel for the village build menu (MVVM Silo C).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Strict-MVVM migration (UI_MVVM_MIGRATION_PLAN.md §1, Silo C): the three
// game-state seams BuildMenu read inline move HERE so the View is a dumb skin:
//   * the crystal balance (was GameStateService.State.Resources.Crystals) →
//     <see cref="Crystals"/> (IEconomy.Crystals is the SAME GameState-backed store);
//   * the placed-tower poll (was FindObjectsByType<Tower>()) → the shared
//     <see cref="Towers"/> (PlacedTowerListVM — the sanctioned resolution site);
//   * the Repair-Wall REFLECTION (AppDomain scan + FindAnyObjectByType + MethodInfo
//     invoke — the architect flagged it as NOT a sanctioned seam) → the typed
//     <see cref="RepairNearestWall"/> command on the real WallRepairController API.
//
// Owns the GameState.ResourcesChanged subscription for the open-menu live refresh
// and raises <see cref="Changed"/>. PURE C# (no uGUI types); the injectable ctor
// lets §2c tests drive Crystals + the tower list over fakes.
// =============================================================================

using System;
using DeNelle.Core.State;
using DeNelle.Core.UI.Mvvm;
using DeNelle.Village.UI;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// ViewModel for the build menu: exposes the live crystal balance, the placed-tower list
    /// (for the Upgrade screen), and a typed Repair-Wall command. Created fresh on Open and
    /// disposed on Close (the panel-VM lifecycle).
    /// </summary>
    public sealed class BuildMenuVM : IPanelViewModel, IDisposable
    {
        private readonly IEconomy _economy;
        private readonly int _fallbackCrystals;
        private readonly Action _onClose;
        private readonly UnityEngine.Events.UnityAction _stateHandler;
        private WallRepairController _wallRepair;
        private bool _subscribed;
        private bool _disposed;

        /// <summary>The shared placed-tower list VM (owns the FindObjectsByType&lt;Tower&gt; poll).</summary>
        public PlacedTowerListVM Towers { get; }

        /// <summary>Resolves EconomyService.Instance + WallRepairController + the tower list itself
        /// (the sole resolution site) and hooks the live ResourcesChanged feed.</summary>
        public static BuildMenuVM CreateDefault(Action onClose, int fallbackCrystals)
        {
            var vm = new BuildMenuVM(
                EconomyService.Instance,
                PlacedTowerListVM.CreateDefault(onClose),
                UnityEngine.Object.FindFirstObjectByType<WallRepairController>(),
                fallbackCrystals,
                onClose);
            vm.Subscribe();
            return vm;
        }

        public BuildMenuVM(IEconomy economy, PlacedTowerListVM towers,
            WallRepairController wallRepair, int fallbackCrystals, Action onClose)
        {
            _economy = economy;
            Towers = towers;
            _wallRepair = wallRepair;
            _fallbackCrystals = fallbackCrystals;
            _onClose = onClose;
            _stateHandler = Raise;
        }

        private void Subscribe()
        {
            if (_subscribed || _disposed) return;
            _subscribed = true;
            var gs = GameStateService.Instance;
            if (gs != null) gs.ResourcesChanged.AddListener(_stateHandler);
        }

        // ── IPanelViewModel ───────────────────────────────────────────────────
        public event Action Changed;
        public string Title => "Build";
        public void Close() => _onClose?.Invoke();

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_subscribed)
            {
                var gs = GameStateService.Instance;
                if (gs != null && _stateHandler != null) gs.ResourcesChanged.RemoveListener(_stateHandler);
            }
            Towers?.Dispose();
            Changed = null;
        }

        // ── Read-only data ────────────────────────────────────────────────────

        /// <summary>The live crystal balance the menu spends from (IEconomy.Crystals is the single
        /// GameState-backed store; falls back to the standalone-test value when no service).</summary>
        public int Crystals => _economy != null ? _economy.Crystals : _fallbackCrystals;

        // ── Commands ──────────────────────────────────────────────────────────

        /// <summary>Repair the most-damaged wall/structure through the sanctioned WallRepairController
        /// API (replaces the removed reflection seam). Surfaces the worst-damaged structure's repair
        /// prompt; no-op (warned) when the controller is absent.</summary>
        public void RepairNearestWall()
        {
            if (_wallRepair == null)
                _wallRepair = UnityEngine.Object.FindFirstObjectByType<WallRepairController>();
            if (_wallRepair != null) _wallRepair.SurfaceWorstRepair();
            else Debug.LogWarning("[BuildMenuVM] WallRepairController not in scene — Repair Wall no-op.");
        }

        private void Raise() { if (!_disposed) Changed?.Invoke(); }
    }
}
