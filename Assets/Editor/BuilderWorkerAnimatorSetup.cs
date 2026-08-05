// =============================================================================
// BuilderWorkerAnimatorSetup - WO-871: builds the ONE shared work/rest
// AnimatorController the build-site worker (ConstructionWorkerPool) plays while a
// structure's build or upgrade timer is running.
// -----------------------------------------------------------------------------
// WHY A FACTORY AND NOT A HAND-MADE ASSET: same reason as WO-833
// (KayKitNpcAnimatorSetup) - the staged KayKit bodies at Assets/Resources/NPCs/KayKit/
// import as Humanoid with a generated avatar but ship NO clips and NO controller, and
// a controller-less skinned humanoid renders its BIND POSE (the owner's F8 2026-08-02
// "NPC Stuck in T Pose"). The controller is generated, idempotent, and committed.
//
// -- CLIPS: BROWSED FROM THE OWNED LIBRARY, NONE AUTHORED (WO-871 sec.2/sec.5) -----
// Both clips below are ALREADY IN THE REPO, git-TRACKED, and animationType:3
// (Humanoid) - so they retarget onto every staged KayKit body exactly as the WO-833
// idle already does. No import, no retarget pass, no new animation asset.
//
//   WORK  = Assets/Action/Knight/Motion/studio-mocap-hero-motion/axe_chopping_m.fbx
//           ("axe_chopping_m", 188 frames). The ONLY sustained manual-labour motion in
//           the tracked Humanoid inventory - a repeated overhead chop that reads as
//           work at a build site.
//   REST  = Assets/Action/Knight/Motion/studio-mocap-series-magical-moves/m-standby-idle.fbx
//           ("m-standby-idle") - the SAME calm standby idle WO-833 wired as the shared
//           KayKit NPC idle and HeroAnimatorFactory uses as the KnightMocap idle.
//           loopTime:1, so the rest beat genuinely loops.
//
// THE CLIP THIS WO WANTED AND WE DO NOT HAVE: a dedicated hammer/saw/construction
// loop. What exists is either off-limits or unusable as-is -
//   * KayKit Character Animations 1.1 "Rig_Medium_Tools" (mining/harvest takes) is the
//     right family but is animationType:2 (GENERIC) and gitignored - WO-833 already
//     ruled it out for exactly this reason; it needs a re-rig + staging pass.
//   * Assets/Blink/.../Gathering/MiningLoop.fbx is a true work loop but the whole
//     Assets/Blink/ tree is gitignored (.gitignore:292).
//   * hammer_bash_f / axe_chop_tank / scythe_reaping_m are tracked Humanoid takes but
//     read as COMBAT swings, not labour.
// Both clip paths below are single named consts so an owner retag is a one-line edit
// (memory `vfx-map-owner-tags-no-creative-pick` - the CLI maps, it does not pick).
//
// -- WHY TWO STATES AND NOT ONE LOOP (WO-855 Phase 4) ------------------------------
// Build tiers are now 30s / 1.5m / 4.5m / 13.5m / 40m / 2h, not a flat 15s. A single
// ~3-second chop looping unbroken for two hours is maddening. So: Work self-loops with
// a short cross-blend (the take is loopTime:0, so the blend hides the restart pop), and
// a bool "Rest" swaps to the calm standby idle. ConstructionWorker drives that bool on a
// randomized 9-16s work / 2.5-5s rest cycle with a random start phase and per-worker
// animator speed jitter - so no loop plays more than ~16s uninterrupted and two workers
// never chop in lockstep.
//
// Run headless:  -executeMethod DeNelle.Editor.BuilderWorkerAnimatorSetup.Build
// Menu:          Defenders/Art/Build Builder Worker Controller
// Marker:        BUILDER_WORKER_ANIM_OK  (BUILDER_WORKER_ANIM_FAIL on a hard miss)
// =============================================================================

using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>
    /// WO-871 - builds the shared build-site worker controller
    /// (<c>Assets/Resources/NPCs/KayKit/BuilderWorkerWork.controller</c>) from two clips
    /// the project already owns. Entry point: <see cref="Build"/>.
    /// </summary>
    public static class BuilderWorkerAnimatorSetup
    {
        // Beside the staged bodies it animates, under Resources so runtime can load it.
        private const string ControllerDir  = "Assets/Resources/NPCs/KayKit";
        private const string ControllerPath = ControllerDir + "/BuilderWorkerWork.controller";

        /// <summary>OWNER-RETAGGABLE: the work motion. See the header for why this one and what
        /// the inventory does NOT have.</summary>
        private const string WorkFbxPath =
            "Assets/Action/Knight/Motion/studio-mocap-hero-motion/axe_chopping_m.fbx";

        /// <summary>OWNER-RETAGGABLE: the between-bouts rest idle (the shared WO-833 idle).</summary>
        private const string RestFbxPath =
            "Assets/Action/Knight/Motion/studio-mocap-series-magical-moves/m-standby-idle.fbx";

        /// <summary>Must match ConstructionWorker.RestParam.</summary>
        private const string RestParam = "Rest";

        [MenuItem("Defenders/Art/Build Builder Worker Controller")]
        public static void BuildMenu() => Build();

        /// <summary>Batchmode entry: -executeMethod DeNelle.Editor.BuilderWorkerAnimatorSetup.Build</summary>
        public static void Build()
        {
            AnimationClip work = LoadClip(WorkFbxPath);
            AnimationClip rest = LoadClip(RestFbxPath);

            if (work == null)
            {
                Debug.LogError("[BuilderWorkerAnimatorSetup] No AnimationClip found at '" + WorkFbxPath +
                               "' - cannot build the worker controller.\nBUILDER_WORKER_ANIM_FAIL");
                return;
            }
            if (rest == null)
            {
                // The rest beat is variation, not the feature - degrade to work-only rather than fail.
                Debug.LogWarning("[BuilderWorkerAnimatorSetup] No AnimationClip at '" + RestFbxPath +
                                 "' - building a WORK-ONLY controller (no rest variation). Over a 2h build " +
                                 "the worker will chop without pause.");
            }

            // Clip verdicts - prove the retarget inputs, never assume (WO-833 pattern).
            string workVerdict = work.isHumanMotion
                ? "Humanoid OK"
                : "WARN work clip is NOT humanoid motion (will not retarget onto the KayKit bodies)";
            if (!work.isHumanMotion)
                Debug.LogWarning("[BuilderWorkerAnimatorSetup] '" + work.name + "' is not humanoid motion - " +
                                 "the builder body would not animate. Check the FBX rig import (animationType must be Humanoid).");
            if (rest != null && !rest.isLooping)
                Debug.LogWarning("[BuilderWorkerAnimatorSetup] rest clip '" + rest.name + "' is not looping - " +
                                 "the rest beat will freeze on its last frame.");

            EnsureFolder(ControllerDir);

            AnimatorController controller;
            try
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }
            catch (Exception e)
            {
                Debug.LogError("[BuilderWorkerAnimatorSetup] create controller threw: " + e.Message +
                               "\nBUILDER_WORKER_ANIM_FAIL");
                return;
            }
            if (controller == null)
            {
                Debug.LogError("[BuilderWorkerAnimatorSetup] Could not create controller at '" + ControllerPath +
                               "'.\nBUILDER_WORKER_ANIM_FAIL");
                return;
            }

            controller.AddParameter(RestParam, AnimatorControllerParameterType.Bool);

            var sm = controller.layers[0].stateMachine;
            var sWork = sm.AddState("Work");
            sWork.motion = work;
            sm.defaultState = sWork;

            AnimatorState sRest = null;
            if (rest != null)
            {
                sRest = sm.AddState("Rest");
                sRest.motion = rest;

                // Work -> Rest: finish the current chop, then drop into the calm idle.
                var toRest = sWork.AddTransition(sRest);
                toRest.hasExitTime = true;
                toRest.exitTime = 0.95f;
                toRest.hasFixedDuration = false;
                toRest.duration = 0.15f;
                toRest.AddCondition(AnimatorConditionMode.If, 0f, RestParam);

                // Rest -> Work: back to work immediately when the cycle says so.
                var toWork = sRest.AddTransition(sWork);
                toWork.hasExitTime = false;
                toWork.hasFixedDuration = false;
                toWork.duration = 0.2f;
                toWork.AddCondition(AnimatorConditionMode.IfNot, 0f, RestParam);
            }

            // Work -> Work: the chop take is loopTime:0 (ActionClipImporter only loops clips whose
            // name contains idle/walk/run), so the STATE loops it. The short cross-blend at 92%
            // hides the start/end pose mismatch that a hard restart would pop on. Added LAST so the
            // Rest transition above is evaluated first.
            var reloop = sWork.AddTransition(sWork);
            reloop.hasExitTime = true;
            reloop.exitTime = 0.92f;
            reloop.hasFixedDuration = false;
            reloop.duration = 0.08f;
            reloop.AddCondition(AnimatorConditionMode.IfNot, 0f, RestParam);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[BuilderWorkerAnimatorSetup] Built " + ControllerPath +
                      " - Work='" + work.name + "' (" + workVerdict + ", " + work.length.ToString("0.00") + "s, " +
                      (work.isLooping ? "clip loops" : "state-looped via self-transition") + "), Rest='" +
                      (rest != null ? rest.name : "<none>") + "'.\n" +
                      "BUILDER_WORKER_ANIM_OK");
        }

        /// <summary>
        /// First real AnimationClip inside the FBX at <paramref name="fbxPath"/>
        /// (skips Unity's "__preview" clips). Null when none resolve.
        /// </summary>
        private static AnimationClip LoadClip(string fbxPath)
        {
            var reps = AssetDatabase.LoadAllAssetRepresentationsAtPath(fbxPath);
            if (reps == null) return null;
            foreach (var rep in reps)
                if (rep is AnimationClip clip && !clip.name.StartsWith("__preview"))
                    return clip;
            return null;
        }

        /// <summary>Creates <paramref name="dir"/> (and parents) if it does not exist.</summary>
        private static void EnsureFolder(string dir)
        {
            if (AssetDatabase.IsValidFolder(dir)) return;
            string parent = Path.GetDirectoryName(dir)?.Replace('\\', '/');
            string leaf = Path.GetFileName(dir);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf)) return;
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
