// =============================================================================
// HeroAnimatorSetup — generic Tripo-hero animator setup. Tripo characters
// ship with two NLA-track animations (Walk = the longer clip, Cast = the
// shorter clip). This utility renames them, configures the FBX importer,
// and builds an AnimatorController with three states (Idle / Walk / Cast)
// driven by Speed (float) + Cast (trigger) — matching the hashes
// HeroLocomotion + HeroAbilities already use.
//
// Run via menu items per hero OR via -executeMethod for batch.
// =============================================================================

using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class HeroAnimatorSetup
    {
        // ── Menu entry points (one per hero) ─────────────────────────────────
        // Hero FBXes live at Assets/Resources/Heroes/ (the project's Resources
        // folder, so Resources.Load can find them at runtime). The "Wizard"
        // hero's FBX is named Mage.fbx on disk — menu label stays "Wizard"
        // for the WO-029 character spec, but the asset path uses the real
        // filename. Controllers are emitted alongside each FBX.
        [MenuItem("Defenders/Animation/Setup Wizard Animator")]
        public static void SetupWizard() => Setup("Assets/Resources/Heroes/Mage.fbx",
                                                  "Assets/Resources/Heroes/Mage.controller");

        [MenuItem("Defenders/Animation/Setup Ranger Animator")]
        public static void SetupRanger() => Setup("Assets/Resources/Heroes/Ranger.fbx",
                                                  "Assets/Resources/Heroes/Ranger.controller");

        [MenuItem("Defenders/Animation/Setup Knight Animator")]
        public static void SetupKnight() => Setup("Assets/Resources/Heroes/Knight.fbx",
                                                  "Assets/Resources/Heroes/Knight.controller");

        /// <summary>
        /// Configure a Tripo hero FBX + build its Idle/Walk/Cast controller.
        /// Idempotent — re-running rewrites the controller and the clip names.
        /// </summary>
        public static void Setup(string fbxPath, string controllerPath)
        {
            if (!ConfigureFbxImporter(fbxPath))
            {
                Debug.LogError($"[HeroAnimatorSetup] Importer setup failed for '{fbxPath}'.");
                return;
            }
            var ctrl = BuildController(fbxPath, controllerPath);
            if (ctrl == null) return;
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[HeroAnimatorSetup] Done — controller at '{controllerPath}', " +
                      $"states: Idle / Walk / Cast.");
        }

        // ── Importer ─────────────────────────────────────────────────────────
        /// <summary>
        /// Pick the longer NLA clip as Walk, the shorter as Cast. Tripo names
        /// them generically ("NlaTrack" / "NlaTrack.001"); we keep only the
        /// unprefixed two and rename them.
        /// </summary>
        private static bool ConfigureFbxImporter(string fbxPath)
        {
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[HeroAnimatorSetup] ModelImporter not found at '{fbxPath}'.");
                return false;
            }

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;

            // Pick the two default takes whose names do NOT carry the "Armature|"
            // prefix (those are FBX export dupes pointing at the same data).
            ModelImporterClipAnimation longClip = null, shortClip = null;
            foreach (var c in importer.defaultClipAnimations)
            {
                if (c.name.Contains("|")) continue; // skip "Armature|..." dupes
                float frames = c.lastFrame - c.firstFrame;
                if (longClip == null || frames > (longClip.lastFrame - longClip.firstFrame))
                {
                    shortClip = longClip;
                    longClip = c;
                }
                else if (shortClip == null || frames > (shortClip.lastFrame - shortClip.firstFrame))
                {
                    shortClip = c;
                }
            }
            if (longClip == null || shortClip == null)
            {
                Debug.LogWarning($"[HeroAnimatorSetup] Could not find two NLA clips inside '{fbxPath}' " +
                                 "— importer left untouched.");
                return false;
            }

            longClip.name = "Walk";
            longClip.loopTime = true;
            longClip.loopPose = true;
            shortClip.name = "Cast";
            shortClip.loopTime = false;

            importer.clipAnimations = new[] { longClip, shortClip };
            importer.SaveAndReimport();
            return true;
        }

        // ── AnimatorController ───────────────────────────────────────────────
        private static AnimatorController BuildController(string fbxPath, string controllerPath)
        {
            var subAssets = AssetDatabase.LoadAllAssetsAtPath(fbxPath);
            AnimationClip walk = null, cast = null;
            foreach (var a in subAssets)
            {
                if (!(a is AnimationClip clip)) continue;
                if (clip.name == "Walk") walk = clip;
                else if (clip.name == "Cast") cast = clip;
            }
            if (walk == null || cast == null)
            {
                Debug.LogError($"[HeroAnimatorSetup] Walk / Cast clips not present after reimport for '{fbxPath}'.");
                return null;
            }

            AssetDatabase.DeleteAsset(controllerPath);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Cast",  AnimatorControllerParameterType.Trigger);

            var sm = ctrl.layers[0].stateMachine;

            // Idle = clipless state; Tripo doesn't ship a bind-pose anim so we
            // let the rig hold its current pose. Walk loops; Cast plays once.
            var idle = sm.AddState("Idle");
            var walkState = sm.AddState("Walk");
            walkState.motion = walk;
            var castState = sm.AddState("Cast");
            castState.motion = cast;

            sm.defaultState = idle;

            var idleToWalk = idle.AddTransition(walkState);
            idleToWalk.hasExitTime = false; idleToWalk.duration = 0.15f;
            idleToWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

            var walkToIdle = walkState.AddTransition(idle);
            walkToIdle.hasExitTime = false; walkToIdle.duration = 0.15f;
            walkToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            var anyToCast = sm.AddAnyStateTransition(castState);
            anyToCast.hasExitTime = false; anyToCast.duration = 0.05f;
            anyToCast.AddCondition(AnimatorConditionMode.If, 0f, "Cast");

            var castToIdle = castState.AddTransition(idle);
            castToIdle.hasExitTime = true; castToIdle.exitTime = 0.95f; castToIdle.duration = 0.1f;

            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }
    }
}
