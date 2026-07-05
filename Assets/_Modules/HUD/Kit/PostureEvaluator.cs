// =============================================================================
// PostureEvaluator — derives the master-state posture arc (A4.2-A4.7 — P23).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.HUD   Namespace: DeNelle.HUD.Kit   (Core-only references)
//
// POSTURE FOLLOWS THE SCENE (A4.2): the ground classifier is SceneOwnership,
// which resolves per scene load and logs "[Flow:World] SceneOwnership resolved".
// DeNelle.HUD cannot reference DeNelle.Village, so the SAME classification is
// read through the Core seam HubScenes.IsEnemyOwnedScene(scene) — the identical
// scene-config source the Village classifier uses (HudContextEvaluator already
// derives its Battle context from it).
//
// INPUT MAP (all Core):
//   HudContextModel (CoreServices.HudModel.Context)  — Modal/BuildMode/Battle/
//     Town/Overworld, single-writer (HudContextEvaluator, P4).
//   PostureSignals.PursuitActive                     — an enemy is pursuing /
//     has the hero in aggro (RegionMobSpawner + Enemy.ReportPursuit pulses).
//   TargetModel.HasTarget                            — the player holds a lock
//     (about to engage while not yet in Battle context).
//   PostureSignals.EndStateVisible                   — hostile(postbattle):
//     the EndState template owns the screen (A4.6 decision node).
//
// DERIVATION (precedence, top wins):
//   Modal            <- Context == Modal
//   HostilePostbattle<- EndStateVisible
//   HostileActive    <- Context == Battle (wave-live-in-town wakes hostile
//                       areas via the SAME row — A4.2: the wave IS the threat)
//   Build            <- Context == BuildMode (a calm(town) variant, A4.2)
//   HostilePrebattle <- engagement window open (pursuit OR player target lock)
//   CalmTown         <- Context == Town
//   CalmExplore      <- otherwise (peaceful default — even on enemy-owned ground
//                       until something is actively threatening the hero)
//
// Every change emits the fleet-assertable line:
//   "[Flow:HudKit] posture calm(town)->hostile(prebattle)"
// =============================================================================

using System;
using UnityEngine;
using DeNelle.Core;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.HudModel;

namespace DeNelle.HUD.Kit
{
    /// <summary>The single writer of the kit's live posture (see header).</summary>
    public sealed class PostureEvaluator : MonoBehaviour
    {
        private const float PollInterval = 0.15f;

        /// <summary>The current posture.</summary>
        public HudPosture Posture { get; private set; } = HudPosture.CalmTown;

        /// <summary>Raised when <see cref="Posture"/> changes value.</summary>
        public event Action<HudPosture> PostureChanged;

        private float _timer;
        private bool _first = true;

        private void Update()
        {
            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f) return;
            _timer = PollInterval;

            var next = Evaluate();
            if (!_first && next == Posture) return;

            var prev = Posture;
            Posture = next;
            // The fleet-assertable transition line (P23 contract).
            FlowTrace.Step("HudKit", "posture " +
                (_first ? "<boot>" : HudPostureKeys.Key(prev)) + "->" + HudPostureKeys.Key(next));
            _first = false;
            PostureChanged?.Invoke(next);
        }

        private static HudPosture Evaluate()
        {
            var hm = CoreServices.HudModel;
            var ctx = hm != null ? hm.Context : null;

            // Modal mutes both trees (A4.2).
            if (ctx != null && ctx.Context == HudContext.Modal) return HudPosture.Modal;

            // The decision node owns the screen (A4.6) — the kit stands down.
            if (PostureSignals.EndStateVisible) return HudPosture.HostilePostbattle;

            // The fight (A4.6: pure battle). A LIVE town wave also lands here via the
            // context evaluator's wave-active input — hostile areas wake while the wave
            // is live, then sleep (A4.2).
            if (ctx != null && ctx.Context == HudContext.Battle) return HudPosture.HostileActiveBattle;

            // Build = a calm(town) variant (A4.2): near-empty HUD row.
            if (ctx != null && ctx.Context == HudContext.BuildMode) return HudPosture.Build;

            // The engagement window (A4.5): pursuit/aggro pulses OR a MANUAL player lock.
            // Auto-nearest reticle tracking must NOT keep battle chrome up (HasTarget alone
            // is always true near hostiles — owner 2026-07-05 peaceful-after-battle).
            bool manualLock = hm != null && hm.Target != null && hm.Target.HasTarget && hm.Target.Locked;
            if (PostureSignals.PursuitActive || manualLock)
                return HudPosture.HostilePrebattle;

            // Calm forks by ground (A4.3).
            return ctx != null && ctx.Context == HudContext.Town
                ? HudPosture.CalmTown
                : HudPosture.CalmExplore;
        }
    }
}
