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
//   echo.born:2          ← EchoService.EchoUnlocked (EchoService.cs:78, raised on the
//                          wave-5 unlock :305 and GrantEcho :335) when the new count >= 2
//   skillpoint.earned:first ← HeroProgression.OnAnyLevelUp (static, HeroProgression.cs:69,
//                          raised :195) — EVERY hero level banks a skill point
//                          (ApplyLevelRewards -> SkillSystem.GrantSkillPoint, :181), so the
//                          first level-up IS the first skill point earned. Raised every
//                          level; the flow's tutorial_ctx one-shot persistence dedupes.
//
// dialogue.ended:<id> / panel.opened:<id> are wired CORE-side
// (TutorialCoreSignalAdapter); hero.reached:<anchor> is the TutorialFlow probe.
//
// KNOWN-UNWIRED contextual trigger (no source event exists in the tree yet —
// noted per spec "where one is missing, note it"; it FlowTrace.Onces so a run
// self-reports the gap): inventory.gear_added:first — there is NO discrete
// "gear entered the inventory" event: GearLoadout.OnGearChanged fires on every
// equip/refresh incl. the initial loadout (wrong semantics), and
// VillageInventory.Changed is a mixed materials+gear larder with no item-type
// payload. Wire it when a real gear-acquired event lands.
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
        private EchoService _echo;
        private bool _economyHooked;
        private bool _affordRaised;   // session guard; per-save one-shot lives in TutorialFlow

        private void OnEnable()
        {
            BuildModeController.BuildModeChanged += OnBuildModeChanged;
            // F8 2026-07-08 ("stuck on raise first tower", STEP-STUCK capture): the LIVE placement
            // path is BuildModeController.Place — its StructurePlaced event is the primary
            // build.tower_placed source (the TowerPlacementSystem/BuildMenu hooks below are legacy).
            BuildModeController.StructurePlaced += OnStructurePlaced;
            // skillpoint.earned:first — the static level-up relay survives HeroProgression
            // instance swaps (DEF-261); every level banks a point, so level 1 = first point.
            HeroProgression.OnAnyLevelUp += OnAnyLevelUp;

            // Self-report the contextual trigger that still has no source (spec note).
            FlowTrace.Once("Tutorial", "unwired-ctx-signals",
                "contextual trigger with NO source event in the tree yet: 'inventory.gear_added:first' " +
                "(no discrete gear-acquired event; OnGearChanged fires on init/equip-swap, " +
                "VillageInventory.Changed carries no item type) — its hint stays dormant.");
        }

        private void OnDisable()
        {
            BuildModeController.BuildModeChanged -= OnBuildModeChanged;
            BuildModeController.StructurePlaced -= OnStructurePlaced;
            HeroProgression.OnAnyLevelUp -= OnAnyLevelUp;
            if (_tps != null) _tps.OnTowerPlaced -= OnTowerPlaced;
            if (_buildMenu != null) _buildMenu.BuildingPlaced -= OnBuildingPlaced;
            if (_wave != null) _wave.OnWaveCleared.RemoveListener(OnWaveCleared);
            if (_arena != null) _arena.OnBattleEnded -= OnBattleEnded;
            if (_echo != null) _echo.EchoUnlocked -= OnEchoUnlocked;
            var svc = GameStateService.Instance;
            if (_economyHooked && svc != null) svc.ResourcesChanged.RemoveListener(OnResourcesChanged);
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextDiscoverAt) return;
            _nextDiscoverAt = Time.unscaledTime + DiscoverInterval;
            Discover();
        }

        private void OnStructurePlaced(string entryId)
        {
            TutorialSignals.Raise(TutorialSignals.TowerPlaced);
            // WO-702 (founding arc): ALSO raise the per-item id so a step can gate on a
            // SPECIFIC structure ("build.structure_placed:pet-house" — the guided Echo
            // Hollow / Lumberyard placements). Additive: the generic TowerPlaced above
            // keeps every existing row working.
            if (!string.IsNullOrEmpty(entryId))
                TutorialSignals.Raise(TutorialSignals.StructurePlacedPrefix + entryId);
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
            if (_echo == null && EchoService.Instance != null)
            {
                _echo = EchoService.Instance;
                _echo.EchoUnlocked -= OnEchoUnlocked;
                _echo.EchoUnlocked += OnEchoUnlocked;
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

        // echo.born:2 — EchoService raises EchoUnlocked with the NEW count (wave-5 unlock
        // or GrantEcho); the ctx_echo_assign hint wants the second birth. Re-raises are
        // harmless: the flow's tutorial_ctx one-shot persistence fires the hint once per save.
        private void OnEchoUnlocked(int newCount)
        {
            if (newCount >= 2) TutorialSignals.Raise(TutorialSignals.EchoBornSecond);
        }

        // skillpoint.earned:first — every hero level banks a skill point
        // (HeroProgression.ApplyLevelRewards -> SkillSystem.GrantSkillPoint), so the first
        // level-up IS the first point. Raised each level; the flow one-shot dedupes.
        private void OnAnyLevelUp(int _) =>
            TutorialSignals.Raise(TutorialSignals.FirstSkillPoint);

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
