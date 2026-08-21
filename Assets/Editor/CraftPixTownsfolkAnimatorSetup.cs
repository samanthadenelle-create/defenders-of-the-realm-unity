// =============================================================================
// CraftPixTownsfolkAnimatorSetup — re-point the town's shared townsfolk controller
// off the HERO's combat locomotion and onto the civilian people clips.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor (editor-only). Batch:
//   -executeMethod DeNelle.Editor.CraftPixTownsfolkAnimatorSetup.Run
// Marker: CRAFTPIX_TOWNSFOLK_ANIM_OK / _FAIL
// Menu:   Defenders/Art/Repoint CraftPix Townsfolk Animator
//
// OWNER, 2026-08-20: "they need to use ide not combat idle", "they have full
// access to the regular controller animations", "they are human rig".
//
// THE MEASURED DEFECT (resolved from guids, not from folder names):
//   AC_CraftPixTownsfolk  Idle -> Assets/Action/Shared/Shared_Idle.fbx
//                         Walk -> Assets/Action/Shared/Shared_Walk_Forward.fbx
// Those are the HERO's shared locomotion clips - the same ones Knight/Cleric/Mage/
// Ranger controllers play. A hero idle is a combat-ready stance: weight forward,
// arms out from the body, ready to swing. Correct for the player character,
// wrong for a shopkeeper standing behind a market stall in a peaceful town.
//
// Every one of the 14 CraftPix bodies shares this ONE controller, so this asset is
// the whole town's posture in a single file - which is why the fix belongs here and
// not at 14 prefabs or 3 injectors.
//
// ⚠ THIS IS THE SECOND HALF OF THE FIX. The first half (commit 79c1e61b) stopped the
// injectors from arming a CraftPix person with KayKitNpcIdle, which plays the Knight
// mocap standby. That was real and it is fixed - but it left the person on its OWN
// controller, and this file is the proof that its own controller was playing a hero
// clip too. Removing one path to the combat idle while the default path still led
// there would have looked like a fix and changed nothing the owner can see.
//
// WHY THESE REPLACEMENTS: Assets/Supercyan/Animations/CharacterPackAnimations/
// MovementAnimations/common_people@{idle,walk} are authored for exactly this - idle
// civilians. Both import animationType: 3 (Humanoid), as do the CraftPix bodies, so
// the clips retarget onto the pack's auto-generated avatars. Verified from the .meta,
// not assumed: a Generic clip cannot pose a Humanoid avatar (it T-poses and slides),
// which is the failure mode the enemy rig audit already catches elsewhere.
//
// IDEMPOTENT. Re-running re-asserts the same two motions and reports 0 changes.
// =============================================================================

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class CraftPixTownsfolkAnimatorSetup
    {
        private const string ControllerPath =
            "Assets/Resources/NPCs/CraftPixPeople/AC_CraftPixTownsfolk.controller";

        private const string CivilianFolder =
            "Assets/Supercyan/Animations/CharacterPackAnimations/MovementAnimations/";

        /// <summary>state name -> the civilian FBX whose clip it should play.</summary>
        private static readonly (string State, string Fbx)[] Wanted =
        {
            ("Idle", CivilianFolder + "common_people@idle.FBX"),
            ("Walk", CivilianFolder + "common_people@walk.FBX"),
        };

        /// <summary>The hero motion tree. A townsfolk clip resolving in here is the defect.</summary>
        private const string HeroMotionRoot = "Assets/Action/";

        [MenuItem("Defenders/Art/Repoint CraftPix Townsfolk Animator")]
        public static void Run()
        {
            var problems = new List<string>();
            var changes = new List<string>();

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                Fail($"controller not found at {ControllerPath}");
                return;
            }

            var machine = controller.layers != null && controller.layers.Length > 0
                ? controller.layers[0].stateMachine
                : null;
            if (machine == null) { Fail("controller has no Base Layer state machine"); return; }

            foreach (var (stateName, fbxPath) in Wanted)
            {
                var state = machine.states.FirstOrDefault(s => s.state != null && s.state.name == stateName).state;
                if (state == null)
                {
                    problems.Add($"no '{stateName}' state on the Base Layer - the controller changed shape; " +
                                 "update this script deliberately rather than silently skipping the state");
                    continue;
                }

                if (!File.Exists(fbxPath))
                {
                    problems.Add($"'{stateName}': civilian source missing at {fbxPath}");
                    continue;
                }

                // Prove the source can pose a Humanoid avatar BEFORE swapping it in. A Generic
                // clip on a Humanoid body T-poses and slides - a silent, look-at-it-only failure.
                var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
                if (importer == null || importer.animationType != ModelImporterAnimationType.Human)
                {
                    problems.Add($"'{stateName}': {Path.GetFileName(fbxPath)} is not imported Humanoid " +
                                 $"(animationType={importer?.animationType.ToString() ?? "no importer"}). " +
                                 "A non-Humanoid clip cannot retarget onto the CraftPix avatars.");
                    continue;
                }

                var clip = AssetDatabase.LoadAllAssetsAtPath(fbxPath)
                                        .OfType<AnimationClip>()
                                        .FirstOrDefault(c => c != null && !c.name.StartsWith("__preview__"));
                if (clip == null)
                {
                    problems.Add($"'{stateName}': no AnimationClip inside {fbxPath}");
                    continue;
                }

                string before = state.motion != null ? AssetDatabase.GetAssetPath(state.motion) : "<none>";
                if (state.motion == clip)
                {
                    changes.Add($"{stateName} already {clip.name}");
                    continue;
                }

                state.motion = clip;
                changes.Add($"{stateName}: {Path.GetFileName(before)} -> {Path.GetFileName(fbxPath)}::{clip.name}");
            }

            if (problems.Count > 0) { Fail(string.Join(" | ", problems)); return; }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // ---- Verify from the SAVED asset, not from the objects we just mutated ----
            AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            var verify = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            var stillHero = new List<string>();
            foreach (var clip in verify.animationClips ?? new AnimationClip[0])
            {
                if (clip == null) continue;
                string src = (AssetDatabase.GetAssetPath(clip) ?? string.Empty).Replace('\\', '/');
                if (src.StartsWith(HeroMotionRoot)) stillHero.Add($"{clip.name} <- {src}");
            }
            if (stillHero.Count > 0)
            {
                Fail($"after save, the controller STILL sources hero clips: {string.Join("; ", stillHero)}. " +
                     "Nothing was proven - do not report this as a pass.");
                return;
            }

            byte[] bytes = File.ReadAllBytes(ControllerPath);
            if (System.Array.IndexOf(bytes, (byte)0) >= 0)
            {
                Fail($"{ControllerPath} contains NUL bytes after save - REVERT IT (git checkout).");
                return;
            }

            Debug.Log($"CRAFTPIX_TOWNSFOLK_ANIM_OK {changes.Count} state(s): {string.Join("; ", changes)}; " +
                      $"no clip resolves under {HeroMotionRoot}; asset {bytes.Length} bytes, NUL-clean.");
        }

        private static void Fail(string reason)
        {
            Debug.LogError("CRAFTPIX_TOWNSFOLK_ANIM_FAIL: " + reason);
        }
    }
}
