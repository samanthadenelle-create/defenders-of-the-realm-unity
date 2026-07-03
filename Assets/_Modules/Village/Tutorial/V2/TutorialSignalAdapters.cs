// =============================================================================
// TutorialSignalAdapters — Village-side real-event → TutorialSignals bus (WO-T1).
// -----------------------------------------------------------------------------
// The spec's rule (§2.1b): REUSE what the game already emits — no new gameplay
// events. This component subscribes the REAL Village events and raises the
// stable bus ids the tutorial-steps.json registry names:
//
//   build.mode_entered   ← BuildModeController.BuildModeChanged (static Action<bool>,
//                          BuildModeController.cs:50, fired :213/:248)
//   build.tower_placed   ← TowerPlacementSystem.OnTowerPlaced (TowerPlacementSystem.cs:39,
//                          raised at the commit, :355) AND BuildMenu.BuildingPlaced
//                          (BuildMenu.cs:142 — now actually raised, see BuildMenu)
//   wave.cleared         ← WaveManager.OnWaveCleared (WaveManager.cs:260, invoked :1674)
//   arena.resolved:win/loss ← BattleArena.OnBattleEnded (BattleArena.cs:191, raised :1564)
//   economy.can_afford_upgrade ← GameStateService.ResourcesChanged, first time (post-
//                          Onboarded) the wallet covers the cheapest tower
//
// dialogue.ended:<id> / panel.opened:<id> are wired CORE-side
// (TutorialCoreSignalAdapter); hero.reached:<anchor> is the TutorialFlow probe.
//
// KNOWN-UNWIRED contextual triggers (no source event exists in the tree yet —
// noted per spec "where one is missing, note it"; they FlowTrace.Once so a run
// self-reports the gap): echo.born:2, inventory.gear_added:first,
// skillpoint.earned:first.
//
// Sources that spawn late (TowerPlacementSystem self-bootstraps; BattleArena
// stages on first encounter) are subscribed by a 1 Hz discovery tick — no
// per-frame Find churn. Added alongside TutorialFlow by its Bootstrap, so it
// only exists when ff.tutorialv2 is ON.
// =============================================================================

using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;
using DeNelle.Core.Tutorial;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>Subscribes the real gameplay events and raises the tutorial bus ids.</summary>
    [DisallowMultipleComponent]
    public sealed class TutorialSignalAdapters : MonoBehaviour
    {
        private const float DiscoverInterval = 1f;
        /// <summary>Cheapest buildable tower (BuildMenu Variants: Stone Tower 120 crystals) —
        /// the "first affordable moment" threshold for the ctx_first_spend hint. Provisional
        /// until costs move to catalog data (BuildMenu.cs "Week 6" note).</summary>
        private const int CheapestTowerCrystals = 120;

        private float _nextDiscoverAt;
        private TowerPlacementSystem _tps;
        private BuildMenu _buildMenu;
        private WaveManager _wave;
        private Arena.BattleArena _arena;
        private bool _economyHooked;
        private bool _affordRaised;   // session guard; per-save one-shot lives in TutorialFlow

        private void OnEnable()
        {
            BuildModeController.BuildModeChanged += OnBuildModeChanged;

            // Self-report the contextual triggers that have no source yet (spec note).
            FlowTrace.Once("Tutorial", "unwired-ctx-signals",
                "contextual triggers with NO source event in the tree yet: 'echo.born:2', " +
                "'inventory.gear_added:first', 'skillpoint.earned:first' — their hints stay dormant " +
                "until those systems emit an event to adapt.");
        }

        private void OnDisable()
        {
            BuildModeController.BuildModeChanged -= OnBuildModeChanged;
            if (_tps != null) _tps.OnTowerPlaced -= OnTowerPlaced;
            if (_buildMenu != null) _buildMenu.BuildingPlaced -= OnBuildingPlaced;
            if (_wave != null) _wave.OnWaveCleared.RemoveListener(OnWaveCleared);
            if (_arena != null) _arena.OnBattleEnded -= OnBattleEnded;
            var svc = GameStateService.Instance;
            if (_economyHooked && svc != null) svc.ResourcesChanged.RemoveListener(OnResourcesChanged);
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextDiscoverAt) return;
            _nextDiscoverAt = Time.unscaledTime + DiscoverInterval;
            Discover();
        }

        // ── Late-spawning source discovery (1 Hz) ─────────────────────────────

        private void Discover()
        {
            if (_tps == null && TowerPlacementSystem.Instance != null)
            {
                _tps = TowerPlacementSystem.Instance;
                _tps.OnTowerPlaced -= OnTowerPlaced;
                _tps.OnTowerPlaced += OnTowerPlaced;
            }
            if (_buildMenu == null)
            {
                _buildMenu = FindAnyObjectByType<BuildMenu>();
                if (_buildMenu != null)
                {
                    _buildMenu.BuildingPlaced -= OnBuildingPlaced;
                    _buildMenu.BuildingPlaced += OnBuildingPlaced;
                }
            }
            if (_wave == null)
            {
                _wave = FindAnyObjectByType<WaveManager>();
                if (_wave != null)
                {
                    _wave.OnWaveCleared.RemoveListener(OnWaveCleared);
                    _wave.OnWaveCleared.AddListener(OnWaveCleared);
                }
            }
            if (_arena == null)
            {
                _arena = Arena.BattleArena.Existing;   // never force-creates the arena
                if (_arena != null)
                {
                    _arena.OnBattleEnded -= OnBattleEnded;
                    _arena.OnBattleEnded += OnBattleEnded;
                }
            }
            if (!_economyHooked)
            {
                var svc = GameStateService.Instance;
                if (svc != null)
                {
                    svc.ResourcesChanged.AddListener(OnResourcesChanged);
                    _economyHooked = true;
                }
            }
        }

        // ── Event → bus ───────────────────────────────────────────────────────

        private static void OnBuildModeChanged(bool entered)
        {
            if (entered) TutorialSignals.Raise(TutorialSignals.BuildModeEntered);
        }

        private void OnTowerPlaced(DeNelle.Core.Data.TowerData _) =>
            TutorialSignals.Raise(TutorialSignals.TowerPlaced);

        private void OnBuildingPlaced(Building _, BuildingDef __) =>
            TutorialSignals.Raise(TutorialSignals.TowerPlaced);

        private void OnWaveCleared(int _) =>
            TutorialSignals.Raise(TutorialSignals.WaveCleared);

        private void OnBattleEnded(Arena.EncounterParams _, bool won) =>
            TutorialSignals.Raise(won ? TutorialSignals.ArenaWin : TutorialSignals.ArenaLoss);

        private void OnResourcesChanged()
        {
            if (_affordRaised) return;
            var svc = GameStateService.Instance;
            var state = svc != null ? svc.State : null;
            if (state == null || !state.Onboarded) return;   // "first true AFTER Onboarded" (spec)
            if (state.Resources.Crystals < CheapestTowerCrystals) return;
            _affordRaised = true;
            TutorialSignals.Raise(TutorialSignals.CanAffordUpgrade);
        }
    }
}
