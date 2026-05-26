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

        // Batch entry — wires all three at once. Also the -executeMethod target
        // for headless runs (build-windows.ps1 / run-unity-method.ps1).
        [MenuItem("Defenders/Animation/Setup ALL Hero Animators")]
        public static void SetupAll()
        {
            SetupWizard();   // Mage.fbx
            SetupRanger();
            SetupKnight();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[HeroAnimatorSetup] SetupAll complete (Mage / Ranger / Knight).");
        }

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
            // Generic rigs need an Avatar generated FROM the model so the Animator
            // can map clip curves onto the skeleton. The FBXs imported with
            // avatarSetup=0 (None) — without this the controller plays but the
            // bones never move ("sliding statue" even with a wired controller).
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;

            // Diagnostic: list what takes the FBX actually exposes, so a missing
            // animation is obvious in the build log rather than a silent skip.
            var takes = importer.defaultClipAnimations;
            Debug.Log($"[HeroAnimatorSetup] '{fbxPath}' exposes {takes.Length} take(s): " +
                      string.Join(", ", System.Array.ConvertAll(takes, t =>
                          $"{t.name}[{t.firstFrame:F0}-{t.lastFrame:F0}]")));

            // Pick the two default takes whose names do NOT carry the "Armature|"
            // prefix (those are FBX export dupes pointing at the same data).
            ModelImporterClipAnimation longClip = null, shortClip = null;
            foreach (var c in takes)
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
            if (longClip == null)
            {
                Debug.LogWarning($"[HeroAnimatorSetup] No usable animation take inside '{fbxPath}' " +
                                 "— importer left untouched. (FBX has no embedded clip?)");
                return false;
            }

            longClip.name = "Walk";
            longClip.loopTime = true;
            longClip.loopPose = true;
            if (shortClip != null)
            {
                shortClip.name = "Cast";
                shortClip.loopTime = false;
                importer.clipAnimations = new[] { longClip, shortClip };
            }
            else
            {
                Debug.LogWarning($"[HeroAnimatorSetup] Only ONE take in '{fbxPath}' — using it as Walk; " +
                                 "no Cast clip (Cast trigger will simply no-op).");
                importer.clipAnimations = new[] { longClip };
            }
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
            if (walk == null)
            {
                Debug.LogError($"[HeroAnimatorSetup] Walk clip not present after reimport for '{fbxPath}'.");
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
            sm.defaultState = idle;

            var idleToWalk = idle.AddTransition(walkState);
            idleToWalk.hasExitTime = false; idleToWalk.duration = 0.15f;
            idleToWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");

            var walkToIdle = walkState.AddTransition(idle);
            walkToIdle.hasExitTime = false; walkToIdle.duration = 0.15f;
            walkToIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");

            // Cast state only if the FBX shipped a second take. The Cast param
            // still exists so HeroAbilities can fire it harmlessly when absent.
            if (cast != null)
            {
                var castState = sm.AddState("Cast");
                castState.motion = cast;

                var anyToCast = sm.AddAnyStateTransition(castState);
                anyToCast.hasExitTime = false; anyToCast.duration = 0.05f;
                anyToCast.AddCondition(AnimatorConditionMode.If, 0f, "Cast");

                var castToIdle = castState.AddTransition(idle);
                castToIdle.hasExitTime = true; castToIdle.exitTime = 0.95f; castToIdle.duration = 0.1f;
            }
            else
            {
                Debug.LogWarning($"[HeroAnimatorSetup] No Cast clip for '{fbxPath}' — " +
                                 "controller built with Idle/Walk only.");
            }

            EditorUtility.SetDirty(ctrl);
            return ctrl;
        }
    }
}
