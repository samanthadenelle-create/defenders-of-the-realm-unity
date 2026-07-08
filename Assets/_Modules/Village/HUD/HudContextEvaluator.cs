// =============================================================================
// HudContextEvaluator — WO-541 Stage 2: the ONE HUD-context authority.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Hud
//
// Consolidates the SAME inputs the two existing evaluators read — derived ONCE —
// and writes the Core HudContextModel:
//   • Combat  : a village wave is ACTIVE (WaveManager.Phase), OR a staged / in-place
//               fight is live (BattleLock.IsInBattle — ATB, Arena, BattleArena,
//               HeroCombatEngagement). Scene ground alone (raid / enemy-owned) does
//               NOT flip Battle — the PostureEvaluator opens hostile(prebattle) on
//               pursuit/target instead (owner 2026-07-05: peaceful default).
//   • Town    : a non-combat HUB scene with the hero inside the town ring.
//   • Overworld : non-combat, outside the town ring (the merged overworld) / a non-hub scene.
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
                          || BattleLock.IsInBattle();

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
                $"inVillage={inVillage} modal={modal} buildMode={buildMode} scene='{scene}' -> {ctx}");

            if (_pushedOnce && _combat && !combat)
                HudPostureReset.OnCombatEnded();

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

        // Countdown seconds at which a wave reads "imminent" (battle-worthy). Mirrors the
        // single-sourced WaveProducer.ImminentThreshold (HudModelProducers.cs) — that const is
        // `private` inside WaveProducer so it can't be referenced here without touching that file;
        // this evaluator is edit-scoped to itself, so the value is duplicated with THIS pointer.
        // Keep the two in lockstep; promote to a shared const if they ever diverge.
        private const float ImminentThreshold = 5f;

        /// <summary>Owner ruling 2026-07-08 (refines the 2026-07-06 "countdown counts as battle"): a
        /// wave COUNTDOWN reads as Battle ONLY when the wave is IMMINENT (final ~<see cref="ImminentThreshold"/>s),
        /// NOT for the whole long empty between-wave gap. So an ACTIVE wave is always battle; a countdown
        /// with more than the threshold remaining releases the HUD to its non-battle context (Town/Overworld),
        /// and only the last few seconds re-arm the "wave incoming" tension. This keeps the imminent window
        /// single-sourced to the same threshold WaveProducer already uses.</summary>
        private static bool IsWaveActive()
        {
            var wm = WaveManager.Instance;
            if (wm == null) return false;

            if (wm.Phase == DeNelle.Village.WavePhase.Active)
                return true;

            if (wm.Phase == DeNelle.Village.WavePhase.Countdown)
            {
                bool imminent = wm.CountdownRemaining <= ImminentThreshold;
                FlowTrace.Step("HUD",
                    imminent
                        ? $"countdown IMMINENT ({wm.CountdownRemaining:0.0}s <= {ImminentThreshold}s) -> counts as Battle"
                        : $"countdown long-gap ({wm.CountdownRemaining:0.0}s > {ImminentThreshold}s) -> gated OUT of Battle (HUD releases)");
                return imminent;
            }

            return false;
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
