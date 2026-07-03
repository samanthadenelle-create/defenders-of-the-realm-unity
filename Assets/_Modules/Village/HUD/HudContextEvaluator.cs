// =============================================================================
// HudContextEvaluator — WO-541 Stage 2: the ONE HUD-context authority.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Hud
//
// Consolidates the SAME inputs the two existing evaluators read — derived ONCE —
// and writes the Core HudContextModel:
//   • Combat  : a wave is counting down / active (WaveManager.Phase), OR an ATB /
//               Arena battle is live (BattleLock.IsInBattle — the Core-clean probe
//               ATBCombatManager + ArenaMode register), OR the active scene is a
//               RaidBase_* raid / an enemy-owned scene (HubScenes).
//   • Town    : a non-combat HUB scene with the hero inside the town ring.
//   • Overworld : non-combat, outside the town ring (OuterWorld) / a non-hub scene.
//   • Modal   : a registered modal panel is open (PanelManager.AnyOpen) — overlays.
//
// PRECEDENCE (frozen rule, WO-541): Modal > Battle > Town > Overworld.
//
// NO VILLAGE<->HUD EDGE: this lives in DeNelle.Village and does NOT reference
// BattleHudVisibilityManager (DeNelle.HUD) — Village cannot reference HUD. It
// derives the identical combat signals from the UNDERLYING systems instead:
// WaveManager (own assembly, direct — no reflection), BattleLock / HubScenes /
// PanelManager (all DeNelle.Core). Town-vs-Overworld replicates
// VillageHudController.InVillage's radial model (hub scene + hero within the town
// ring) directly from the Village HeroLocomotion + the Heart-at-origin convention,
// rather than calling the HUD. So no new asmdef edge and no reflection is added.
//
// DARK: the existing BattleHudVisibilityManager / ApplyContext logic is UNTOUCHED
// (Stage 4 migrates the views). This only writes the new Core model — additive.
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;
using DeNelle.Core;
using DeNelle.Core.Combat;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.HudModel;
using DeNelle.Core.UI;
using DeNelle.Village;

namespace DeNelle.Village.Hud
{
    /// <summary>The single writer of <see cref="HudContextModel"/> (WO-541 Stage 2).</summary>
    internal sealed class HudContextEvaluator : HudProducer
    {
        // Mirrors VillageHudController's radial model (scene + town ring). The Heart of
        // Elarion sits at the world origin (canon §7); the town footprint reaches ~60u.
        private const string VillageSceneName = "Village2";
        private const float TownRadius = 60f;
        private const float TownRadiusHyst = 8f;

        private HeroLocomotion _hero;

        // Last pushed snapshot (change-gate so the model's [Flow:HUD] transition trace
        // only fires when an input actually changes).
        private HudContext _ctx = (HudContext)(-1);
        private bool _inVillage, _combat, _modal, _buildMode;
        private bool _pushedOnce;

        public HudContextEvaluator(IHudModel model, Transform _host) : base(model, 0.20f) { }

        protected override void Poll()
        {
            string scene = SceneManager.GetActiveScene().name;

            bool combat = IsWaveActive()
                          || BattleLock.IsInBattle()
                          || HubScenes.IsRaid(scene)
                          || HubScenes.IsEnemyOwnedScene(scene);

            bool inVillage = IsInTownRing(scene);
            bool modal = PanelManager.AnyOpen;
            // P4 (HUD_OBSIDIAN §3.3): the 4th space type. Read via the existing
            // BuildModeController seam (same assembly, read-only — Enter/Exit already
            // maintain IsActive + broadcast BuildModeChanged; no new seam needed).
            bool buildMode = IsBuildModeActive();

            // Precedence: Modal overlays everything; else BuildMode (an edit session owns
            // the screen and freezes waves — BuildModeController.Enter/FreezeWaves — so it
            // outranks a residual combat signal); else Battle; else Town; else Overworld.
            HudContext ctx = modal ? HudContext.Modal
                           : buildMode ? HudContext.BuildMode
                           : combat ? HudContext.Battle
                           : inVillage ? HudContext.Town
                           : HudContext.Overworld;

            // Observability (mirrors BattleHudVisibilityManager.EvaluateMode's input trace).
            FlowTrace.Throttle("HUD", "ctx-eval", 1f,
                $"context inputs: wave={IsWaveActive()} battleLock={BattleLock.IsInBattle()} " +
                $"raid={HubScenes.IsRaid(scene)} enemyScene={HubScenes.IsEnemyOwnedScene(scene)} " +
                $"inVillage={inVillage} modal={modal} buildMode={buildMode} scene='{scene}' -> {ctx}");

            if (_pushedOnce && ctx == _ctx && inVillage == _inVillage && combat == _combat &&
                modal == _modal && buildMode == _buildMode)
                return;

            _ctx = ctx; _inVillage = inVillage; _combat = combat; _modal = modal;
            _buildMode = buildMode; _pushedOnce = true;
            // HudContextModel.Set fires Changed only on a real Context change but ALWAYS
            // traces the state; a real change also emits the fleet-assertable
            // "[Flow:HudModel] context A->B" transition line (P4 contract).
            Model.Context.Set(ctx, inVillage, combat, modal, buildMode);
        }

        /// <summary>P4: true while a Build Mode edit session is live (BuildModeController.IsActive).</summary>
        private static bool IsBuildModeActive()
        {
            var bmc = BuildModeController.Instance;
            return bmc != null && bmc.IsActive;
        }

        /// <summary>WO-579: village wave in its ACTIVE (fighting) phase — Battle context. The calm
        /// prepare-phase Countdown stays Town (its top-left clock shows the next-wave timer), so only the
        /// Active phase counts as combat here (matches BattleHudVisibilityManager.IsWaveFighting). Was
        /// Countdown||Active; narrowed so arming a wave's countdown no longer hides the Town timer.</summary>
        private static bool IsWaveActive()
        {
            var wm = WaveManager.Instance;
            if (wm == null) return false;
            return wm.Phase == DeNelle.Village.WavePhase.Active;
        }

        /// <summary>
        /// Replicates VillageHudController.InVillage: a HUB scene (HubScenes) with the
        /// hero inside the town ring around the Heart-at-origin. No hero resolved yet =>
        /// treated as in-ring (don't flip to Overworld before the hero spawns).
        /// </summary>
        private bool IsInTownRing(string scene)
        {
            if (!HubScenes.IsHub(scene)) return false;

            if (_hero == null || !_hero) _hero = Object.FindAnyObjectByType<HeroLocomotion>();
            if (_hero == null) return true; // hero not spawned -> default to town

            Vector3 p = _hero.transform.position;
            float distSqr = p.x * p.x + p.z * p.z;   // horizontal distance to origin (Heart)
            // Hysteresis: once inside, allow drifting slightly past before flipping out.
            float edge = _inVillage ? TownRadius + TownRadiusHyst : TownRadius;
            return distSqr <= edge * edge;
        }
    }
}
