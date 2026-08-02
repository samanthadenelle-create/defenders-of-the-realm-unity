// =============================================================================
// KayKitNpcAnimatorSetup - WO-833: builds the ONE shared idle AnimatorController
// the 12 staged KayKit structure-NPC bodies (WO-818) play in the hub.
// -----------------------------------------------------------------------------
// WHY: the staged FBXs at Assets/Resources/NPCs/KayKit/ import as Humanoid with a
// model-generated avatar but ship NO AnimationClips and NO controller - a skinned
// humanoid whose Animator has no controller renders its BIND POSE, which is the
// owner's F8 2026-08-02 "NPC Stuck in T Pose". No KayKit-specific controller is
// needed: because every staged body is Humanoid (KayKitNpcImporter flips the
// copies, avatar verdict OK 12/12), the project's OWN mocap idle - the hero's
// calm standby m-standby-idle (Humanoid animationType:3, loopTime:1, same clip
// HeroAnimatorFactory uses as the KnightMocap default Locomotion idle) -
// retargets onto all 12 rigs. Zero new animation assets. KayKit's own animation
// pack (KayKit Character Animations 1.1) is GENERIC-rigged (animationType:2) and
// gitignored, so it stays an optional flavor follow-up, not the fix.
//
// WHAT Build DOES: creates Assets/Resources/NPCs/KayKit/KayKitNpcIdle.controller
// (under Resources so KayKitNpcBody.ArmIdle can Resources.Load it at runtime;
// the folder is TRACKED - commit the controller) with a single default "Idle"
// state playing the mocap standby clip. Idempotent - re-running overwrites in
// place (DragonAnimatorSetup pattern).
//
// Run headless:  -executeMethod DeNelle.Editor.KayKitNpcAnimatorSetup.Build
// Menu:          Defenders/Art/Build KayKit NPC Idle Controller
// Marker:        KAYKIT_IDLE_OK  (KAYKIT_IDLE_FAIL on a hard miss)
// =============================================================================

using System;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>
    /// WO-833 - builds the shared KayKit structure-NPC idle controller
    /// (<c>Assets/Resources/NPCs/KayKit/KayKitNpcIdle.controller</c>) from the
    /// project's Humanoid mocap standby clip. Entry point: <see cref="Build"/>.
    /// </summary>
    public static class KayKitNpcAnimatorSetup
    {
        // The staged-body folder (KayKitNpcImporter.StageDir) - the controller lives
        // beside the FBXs it animates, under Resources so runtime can load it.
        private const string ControllerDir  = "Assets/Resources/NPCs/KayKit";
        private const string ControllerPath = ControllerDir + "/KayKitNpcIdle.controller";

        // The calm standby idle - the SAME clip HeroAnimatorFactory wires as the
        // KnightMocap default Locomotion idle (MocapIdleClip = "m-standby-idle").
        // Humanoid (animationType:3) + loopTime:1, verified in the .meta - so it
        // retargets onto every Humanoid KayKit avatar and loops.
        private const string IdleFbxPath =
            "Assets/Action/Knight/Motion/studio-mocap-series-magical-moves/m-standby-idle.fbx";

        [MenuItem("Defenders/Art/Build KayKit NPC Idle Controller")]
        public static void BuildMenu() => Build();

        /// <summary>Batchmode entry: -executeMethod DeNelle.Editor.KayKitNpcAnimatorSetup.Build</summary>
        public static void Build()
        {
            AnimationClip idle = LoadClip(IdleFbxPath);
            if (idle == null)
            {
                Debug.LogError("[KayKitNpcAnimatorSetup] No AnimationClip found at '" + IdleFbxPath +
                               "' - cannot build the idle controller.\nKAYKIT_IDLE_FAIL");
                return;
            }

            // Clip verdicts (prove the retarget inputs, do not assume): the clip must be
            // Humanoid motion to retarget onto the KayKit avatars, and looping to idle forever.
            string humanVerdict = idle.isHumanMotion ? "Humanoid OK" : "WARN clip is NOT humanoid motion (will not retarget)";
            string loopVerdict  = idle.isLooping     ? "loop OK"     : "WARN clip is NOT looping (idle will freeze on the last frame)";
            if (!idle.isHumanMotion)
                Debug.LogWarning("[KayKitNpcAnimatorSetup] '" + idle.name + "' is not humanoid motion - " +
                                 "the KayKit bodies would not animate. Check the FBX rig import (animationType must be Humanoid).");
            if (!idle.isLooping)
                Debug.LogWarning("[KayKitNpcAnimatorSetup] '" + idle.name + "' is not looping - " +
                                 "set loopTime on the FBX clip so the idle cycles.");

            EnsureFolder(ControllerDir);

            AnimatorController controller = null;
            try
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }
            catch (Exception e)
            {
                Debug.LogError("[KayKitNpcAnimatorSetup] create controller threw: " + e.Message + "\nKAYKIT_IDLE_FAIL");
                return;
            }
            if (controller == null)
            {
                Debug.LogError("[KayKitNpcAnimatorSetup] Could not create controller at '" + ControllerPath +
                               "'.\nKAYKIT_IDLE_FAIL");
                return;
            }

            // ONE default state, no parameters, no transitions - the NPCs just idle.
            var sm = controller.layers[0].stateMachine;
            var sIdle = sm.AddState("Idle");
            sIdle.motion = idle;
            sm.defaultState = sIdle;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[KayKitNpcAnimatorSetup] Built " + ControllerPath +
                      " - Idle='" + idle.name + "' (" + humanVerdict + ", " + loopVerdict + ").\n" +
                      "KAYKIT_IDLE_OK");
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
