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
//                       areas via the SAME row — A4.2: the wave IS the threat;
//                       since WO-1436 a RaidBase_* scene lands here too, because
//                       HubScenes.SceneDeclaresCombat feeds that same context
//                       input — the ASSAULT is the threat, for its whole duration)
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
            // WO-1483: town frame path — the HUD posture poll.
            using var _perf = DeNelle.Core.Diagnostics.FlowTrace.Measure(
                "Perf", "PostureEvaluator.Update", 4f, 1f);

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

            // A MANUAL player lock only — auto-nearest reticle tracking must NOT keep battle
            // chrome up (HasTarget alone is always true near hostiles; owner 2026-07-05
            // peaceful-after-battle).
            bool manualLock = hm != null && hm.Target != null && hm.Target.HasTarget && hm.Target.Locked;

            return Derive(
                hasContext: ctx != null,
                context: ctx != null ? ctx.Context : HudContext.Overworld,
                endStateVisible: PostureSignals.EndStateVisible,
                pursuitActive: PostureSignals.PursuitActive,
                manualLock: manualLock);
        }

        /// <summary>
        /// The posture derivation as a PURE function of its four inputs — the same chain
        /// <see cref="Evaluate"/> has always run, hoisted so it can be ASSERTED (WO-1436).
        ///
        /// <para>WHY (WO-1436, P0): the raid HUD stayed in a peaceful posture for a whole
        /// assault and the player had no reachable ability faces, with 394+ suites green.
        /// Every existing HUD oracle asked "does the bar render its faces correctly FOR a
        /// posture?" — and it did. None could ask "is the posture RIGHT for the scene the
        /// player is standing in?", because the answer was only computable inside a running
        /// MonoBehaviour reading live statics. Now the scene/posture seam oracle evaluates
        /// exactly this function, so a raid scene resolving peaceful FAILS the build.</para>
        ///
        /// <para>Behaviour is unchanged — no reordering, no new branch. Keep it that way:
        /// if this and <see cref="Evaluate"/> ever disagree, the oracle is testing fiction.</para>
        /// </summary>
        /// <param name="hasContext">False when CoreServices.HudModel has not resolved yet —
        /// preserved as its own argument because the original chain's `ctx != null` guards
        /// made a missing model fall through to the calm fork rather than to Modal.</param>
        public static HudPosture Derive(bool hasContext, HudContext context,
                                        bool endStateVisible, bool pursuitActive, bool manualLock)
        {
            // Modal mutes both trees (A4.2).
            if (hasContext && context == HudContext.Modal) return HudPosture.Modal;

            // The decision node owns the screen (A4.6) — the kit stands down.
            if (endStateVisible) return HudPosture.HostilePostbattle;

            // The fight (A4.6: pure battle). A LIVE town wave also lands here via the
            // context evaluator's wave-active input — hostile areas wake while the wave
            // is live, then sleep (A4.2). Since WO-1436 a RaidBase_* scene lands here too,
            // via HubScenes.SceneDeclaresCombat feeding that same context input.
            if (hasContext && context == HudContext.Battle) return HudPosture.HostileActiveBattle;

            // Build = a calm(town) variant (A4.2): near-empty HUD row.
            if (hasContext && context == HudContext.BuildMode) return HudPosture.Build;

            // The engagement window (A4.5): pursuit/aggro pulses OR a MANUAL player lock.
            if (pursuitActive || manualLock) return HudPosture.HostilePrebattle;

            // Calm forks by ground (A4.3).
            return hasContext && context == HudContext.Town
                ? HudPosture.CalmTown
                : HudPosture.CalmExplore;
        }
    }
}
