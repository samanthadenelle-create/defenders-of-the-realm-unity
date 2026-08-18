// =============================================================================
// DragonAnimatorSetup - builds Syndrath's AnimatorController + boss prefab (WO-760).
// -----------------------------------------------------------------------------
// Entry points the main Unity session runs (Defenders menu, or via -executeMethod):
//
//     -executeMethod DeNelle.Editor.DragonAnimatorSetup.BuildSyndrathDragon   (both)
//     -executeMethod DeNelle.Editor.DragonAnimatorSetup.BuildDragonAnimator   (controller)
//     -executeMethod DeNelle.Editor.DragonAnimatorSetup.BuildDragonBossPrefab (prefab)
//
// THE ASSET. Assets/Dragon/ = Asset-Store product 71047 "Dragon Animated"
// (WDallgraphics; licenseType: Store). Unlike a single baked-takes FBX, its clips
// live in SEPARATE animation FBXs under Assets/Dragon/Animations/dragon@<name>.FBX
// (each carrying one clip named <name>: idle / walk / run / fly / glide / takeoff /
// landing / attack1-3 / bite / hit / die / die2). The rig mesh + Animator live on
// Assets/Dragon/Prefab/Dragon.prefab. This REPLACES the old CC-BY-NC 3DHaupt dragon.
//
// WHAT BuildDragonAnimator DOES. Builds Assets/Generated/Animators/SyndrathDragon.
// controller with states + parameters matching the DragonAnim contract that
// DragonBoss.cs drives:
//     Params : Speed(float) Attack(trigger) Dead(bool)
//              Takeoff(trigger) Fly(bool) Landing(trigger) Grounded(bool)
//              Attack1/2/3(trigger)
//     States : Fly (default) / GroundIdle / Takeoff / Landing / Attack (finale)
//              Attack1 / Attack2 / Attack3 (grounded fire) / Death
//     Flow   : GroundIdle -Takeoff-> Fly ; Fly -Landing-> GroundIdle ;
//              GroundIdle -Attack1/2/3-> GroundIdle ; Fly -Attack-> Fly ;
//              AnyState -Dead-> Death.
// The DragonAnim parameter STRINGS are mirrored verbatim below (PSpeed .. PAttack3)
// because DeNelle.Editor cannot reference DeNelle.Village - they MUST stay identical
// to DeNelle.Village.DragonAnim.
//
// WHAT BuildDragonBossPrefab DOES. Assembles Assets/EnemyContent/Boss_Dragon.
// prefab (the load path WaveManager.SpawnApexBoss uses): the Dragon.prefab rig as
// the visual child (Animator -> SyndrathDragon.controller, root motion off), a
// solid CapsuleCollider (air-defense / hero raycasts hit it), and the DragonBoss
// component (added by reflection - DragonBoss implements IDamageable itself, so no
// adapter). The demo scripts on the store prefab (Dragon / target / CameraLookAt /
// Particles) are stripped so nothing but DragonBoss drives the instance.
//
// ISOLATION. Editor-only (DeNelle.Editor.asmdef). No compile-time dependency on any
// gameplay type - DragonBoss is resolved by full type name via reflection, the same
// discipline VillageSceneBuilder uses. IDEMPOTENT - re-running overwrites in place.
// Logs DRAGON_BUILD_OK on success so the orchestrator can grep it. This script does
// NOT run itself; the main Unity session triggers it.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>
    /// Editor utility that builds Syndrath the Devourer's <see cref="AnimatorController"/>
    /// from the licensed Assets/Dragon rig and assembles the apex-boss prefab.
    /// Entry points: <see cref="BuildSyndrathDragon"/> (both),
    /// <see cref="BuildDragonAnimator"/>, <see cref="BuildDragonBossPrefab"/>.
    /// </summary>
    public static class DragonAnimatorSetup
    {
        // -- Paths -----------------------------------------------------------------
        private const string DragonRoot   = "Assets/Dragon";
        private const string AnimDir      = DragonRoot + "/Animations";
        private const string RigPrefab    = DragonRoot + "/Prefab/Dragon.prefab";

        private const string AnimatorDir  = "Assets/Generated/Animators";
        private const string ControllerPath = AnimatorDir + "/SyndrathDragon.controller";

        private const string PrefabDir  = DeNelle.Core.AssetRoots.EnemyContent;
        private const string PrefabPath = PrefabDir + "/Boss_Dragon.prefab";

        // -- Gameplay type - resolved by reflection (no asmdef dependency) ---------
        private const string TypeDragonBoss = "DeNelle.Village.DragonBoss";

        // -- Animator parameter names - MIRROR DeNelle.Village.DragonAnim verbatim --
        private const string PSpeed    = "Speed";    // float
        private const string PAttack   = "Attack";   // trigger - finale strike beat
        private const string PDead     = "Dead";     // bool
        private const string PTakeoff  = "Takeoff";  // trigger
        private const string PFly      = "Fly";      // bool - airborne
        private const string PLanding  = "Landing";  // trigger
        private const string PGrounded = "Grounded"; // bool - grounded
        private const string PAttack1  = "Attack1";  // trigger - grounded fire
        private const string PAttack2  = "Attack2";  // trigger - grounded fire
        private const string PAttack3  = "Attack3";  // trigger - grounded fire

        // The enemy physics layer (matches VillageSceneBuilder's EnemyLayer).
        private const int EnemyLayer = 8;

        // Demo MonoBehaviours shipped on the store Dragon.prefab - stripped from the
        // game prefab so only DragonBoss drives the instance.
        private static readonly HashSet<string> DemoScriptNames = new HashSet<string>
        {
            "Dragon", "target", "CameraLookAt", "Particles"
        };

        // =====================================================================
        //  Entry points
        // =====================================================================

        /// <summary>Builds the controller AND the boss prefab, in order.</summary>
        [MenuItem("Defenders/Enemies/Build Syndrath Dragon")]
        public static void BuildSyndrathDragon()
        {
            BuildDragonAnimator();
            BuildDragonBossPrefab();
        }

        /// <summary>
        /// Builds <c>Assets/Generated/Animators/SyndrathDragon.controller</c> - the
        /// DragonAnim contract states + params wired to the Assets/Dragon clips.
        /// Idempotent.
        /// </summary>
        [MenuItem("Defenders/Enemies/Build Syndrath Animator Controller")]
        public static void BuildDragonAnimator()
        {
            EnsureFolder(AnimatorDir);

            // -- Gather the licensed rig's clips (one per animation FBX) -----------
            AnimationClip fly    = LoadClip("fly", "glide", "run");
            AnimationClip idle   = LoadClip("idle", "walk");
            AnimationClip takeoff = LoadClip("takeoff", "fly");
            AnimationClip landing = LoadClip("landing", "idle");
            AnimationClip atk1   = LoadClip("attack1", "bite", "attack2");
            AnimationClip atk2   = LoadClip("attack2", "attack1", "bite");
            AnimationClip atk3   = LoadClip("attack3", "attack2", "bite");
            AnimationClip bite   = LoadClip("bite", "attack1");
            AnimationClip die    = LoadClip("die", "die2", "hit");

            // Finale generic strike reuses the bite/attack1 clip.
            AnimationClip attack = bite ?? atk1;

            // -- Build the controller ----------------------------------------------
            AnimatorController controller = null;
            Guard(() => controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath),
                  "create SyndrathDragon.controller");
            if (controller == null)
            {
                Debug.LogError("[DragonAnimatorSetup] Could not create controller at '" + ControllerPath + "'. Aborting.");
                return;
            }

            controller.AddParameter(PSpeed,    AnimatorControllerParameterType.Float);
            controller.AddParameter(PAttack,   AnimatorControllerParameterType.Trigger);
            controller.AddParameter(PDead,     AnimatorControllerParameterType.Bool);
            controller.AddParameter(PTakeoff,  AnimatorControllerParameterType.Trigger);
            controller.AddParameter(PFly,      AnimatorControllerParameterType.Bool);
            controller.AddParameter(PLanding,  AnimatorControllerParameterType.Trigger);
            controller.AddParameter(PGrounded, AnimatorControllerParameterType.Bool);
            controller.AddParameter(PAttack1,  AnimatorControllerParameterType.Trigger);
            controller.AddParameter(PAttack2,  AnimatorControllerParameterType.Trigger);
            controller.AddParameter(PAttack3,  AnimatorControllerParameterType.Trigger);

            var sm = controller.layers[0].stateMachine;

            // Fly is the default - the dragon spends most of the encounter aloft.
            var sFly = sm.AddState("Fly");
            sFly.motion = fly;
            sm.defaultState = sFly;

            var sGround = sm.AddState("GroundIdle");
            sGround.motion = idle;

            var sTakeoff = sm.AddState("Takeoff");
            sTakeoff.motion = takeoff;

            var sLanding = sm.AddState("Landing");
            sLanding.motion = landing;

            var sAttack = sm.AddState("Attack");     // finale strike (generic Attack trigger)
            sAttack.motion = attack;

            var sA1 = sm.AddState("Attack1");
            sA1.motion = atk1;
            var sA2 = sm.AddState("Attack2");
            sA2.motion = atk2 ?? atk1;
            var sA3 = sm.AddState("Attack3");
            sA3.motion = atk3 ?? atk1;

            var sDeath = sm.AddState("Death");
            sDeath.motion = die ?? idle;

            // -- Transitions --------------------------------------------------------
            // Takeoff: grounded -> launch clip -> fly.
            AddTrigger(sGround, sTakeoff, PTakeoff);
            AddExit(sTakeoff, sFly, 0.8f);

            // Landing: airborne -> land clip -> grounded idle.
            AddTrigger(sFly, sLanding, PLanding);
            AddExit(sLanding, sGround, 0.85f);

            // Finale generic strike from the air, back to fly.
            AddTrigger(sFly, sAttack, PAttack);
            AddExit(sAttack, sFly, 0.8f);

            // Grounded fire attacks, back to the grounded idle.
            AddTrigger(sGround, sA1, PAttack1);
            AddTrigger(sGround, sA2, PAttack2);
            AddTrigger(sGround, sA3, PAttack3);
            AddExit(sA1, sGround, 0.75f);
            AddExit(sA2, sGround, 0.75f);
            AddExit(sA3, sGround, 0.75f);

            // Death - latched from ANY state on the Dead bool; never transitions out
            // (DragonBoss drives the spiralling fall then destroys the object).
            var anyDeath = sm.AddAnyStateTransition(sDeath);
            anyDeath.AddCondition(AnimatorConditionMode.If, 0f, PDead);
            anyDeath.hasExitTime = false;
            anyDeath.duration = 0.15f;
            anyDeath.canTransitionToSelf = false;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "[DragonAnimatorSetup] Built " + ControllerPath + " (DragonAnim contract).\n" +
                "  Fly=" + Name(fly) + "  GroundIdle=" + Name(idle) + "  Takeoff=" + Name(takeoff) +
                "  Landing=" + Name(landing) + "\n  Attack=" + Name(attack) + "  Attack1=" + Name(atk1) +
                "  Attack2=" + Name(atk2) + "  Attack3=" + Name(atk3) + "  Death=" + Name(die));
        }

        /// <summary>
        /// Assembles <c>Assets/EnemyContent/Boss_Dragon.prefab</c> - the licensed
        /// rig + SyndrathDragon.controller + a solid CapsuleCollider + DragonBoss
        /// (reflection). Idempotent - overwrites the prefab in place.
        /// </summary>
        [MenuItem("Defenders/Enemies/Build Syndrath Boss Prefab")]
        public static void BuildDragonBossPrefab()
        {
            EnsureFolder(PrefabDir);

            var dragonBossType = FindType(TypeDragonBoss);
            if (dragonBossType == null)
            {
                Debug.LogError(
                    "[DragonAnimatorSetup] Type '" + TypeDragonBoss + "' not found - is the " +
                    "DeNelle.Village assembly compiled? Prefab build aborted.");
                return;
            }

            var rig = AssetDatabase.LoadAssetAtPath<GameObject>(RigPrefab);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                Debug.LogWarning(
                    "[DragonAnimatorSetup] SyndrathDragon.controller not found - run " +
                    "'Build Syndrath Animator Controller' first. The prefab's Animator will " +
                    "have no controller assigned.");
            }

            var root = new GameObject("Boss_Dragon");
            GameObject prefab = null;
            try
            {
                root.layer = EnemyLayer;

                // -- Visual child - the licensed dragon rig (placeholder on a miss) --
                GameObject visual;
                if (rig != null)
                {
                    visual = (GameObject)PrefabUtility.InstantiatePrefab(rig);
                    if (visual != null)
                    {
                        // Break the prefab connection so we can safely strip demo scripts.
                        PrefabUtility.UnpackPrefabInstance(
                            visual, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    }
                    if (visual == null)
                    {
                        visual = new GameObject("[FALLBACK] DragonRig");
                    }
                    else
                    {
                        visual.name = "DragonRig";
                    }
                }
                else
                {
                    Debug.LogWarning(
                        "[DragonAnimatorSetup] Dragon rig prefab not found at '" + RigPrefab +
                        "' - using a placeholder capsule for the visual.");
                    visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    visual.name = "[PLACEHOLDER] DragonRig";
                    visual.transform.localScale = new Vector3(2f, 3f, 5f);
                }
                visual.transform.SetParent(root.transform, false);
                visual.transform.localPosition = Vector3.zero;

                // Strip demo scripts + every collider from the rig - DragonBoss owns
                // the one physics body (added below), and no demo behaviour should run.
                StripDemoScripts(visual);
                StripColliders(visual);

                // -- Animator - assign SyndrathDragon.controller on the rig ---------
                var animator = visual.GetComponentInChildren<Animator>();
                if (animator == null)
                    animator = visual.AddComponent<Animator>();
                if (controller != null)
                    animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;   // DragonBoss owns the flight

                // -- Collider - a SOLID capsule so air-defense / hero raycasts hit --
                // (DefenseTower's OverlapSphere ignores triggers; DragonBoss.EnsureHit-
                // Collider early-outs when a non-trigger collider is already present.)
                var capsule = root.AddComponent<CapsuleCollider>();
                capsule.direction = 2;             // Z-axis - along the long dragon body
                capsule.height = 7f;
                capsule.radius = 1.6f;
                capsule.center = Vector3.zero;
                capsule.isTrigger = false;

                // -- DragonBoss component (reflection - no asmdef dependency) --------
                if (root.GetComponent(dragonBossType) == null)
                    root.AddComponent(dragonBossType);

                SetLayerRecursive(root, EnemyLayer);

                Guard(() => prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath),
                      "save Boss_Dragon.prefab");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            if (prefab == null)
            {
                Debug.LogError("[DragonAnimatorSetup] Failed to save the boss prefab at '" + PrefabPath + "'.");
                return;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                "[DragonAnimatorSetup] Built " + PrefabPath + " - DragonBoss + solid CapsuleCollider + " +
                "DragonRig(Animator -> SyndrathDragon.controller).\n" +
                "DRAGON_BUILD_OK");
        }

        // =====================================================================
        //  Clip loading
        // =====================================================================

        /// <summary>
        /// Loads the AnimationClip for the first matching <paramref name="names"/>
        /// from Assets/Dragon/Animations/dragon@&lt;name&gt;.FBX (each FBX carries one
        /// clip). Returns null when none of the candidates resolve (logged once).
        /// </summary>
        private static AnimationClip LoadClip(params string[] names)
        {
            foreach (string n in names)
            {
                string fbx = AnimDir + "/dragon@" + n + ".FBX";
                var reps = AssetDatabase.LoadAllAssetRepresentationsAtPath(fbx);
                if (reps == null) continue;
                foreach (var rep in reps)
                {
                    if (rep is AnimationClip clip && !clip.name.StartsWith("__preview"))
                        return clip;
                }
            }
            Debug.LogWarning(
                "[DragonAnimatorSetup] No clip found for any of [" + string.Join(", ", names) +
                "] under " + AnimDir + " - the matching state will be motion-less (still a valid asset).");
            return null;
        }

        /// <summary>Clip name for logging - "(none)" when null.</summary>
        private static string Name(AnimationClip clip) => clip != null ? clip.name : "(none)";

        // =====================================================================
        //  Transition helpers
        // =====================================================================

        /// <summary>Interrupt transition fired by a trigger - no exit time.</summary>
        private static void AddTrigger(AnimatorState from, AnimatorState to, string trigger)
        {
            var t = from.AddTransition(to);
            t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            t.hasExitTime = false;
            t.duration = 0.12f;
        }

        /// <summary>Auto transition back once the clip has (mostly) played.</summary>
        private static void AddExit(AnimatorState from, AnimatorState to, float exitTime)
        {
            var t = from.AddTransition(to);
            t.hasExitTime = true;
            t.exitTime = exitTime;
            t.duration = 0.2f;
        }

        // =====================================================================
        //  GameObject helpers
        // =====================================================================

        /// <summary>Removes the store demo MonoBehaviours from the rig hierarchy.</summary>
        private static void StripDemoScripts(GameObject go)
        {
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (mb == null) continue;   // already-missing script
                if (DemoScriptNames.Contains(mb.GetType().Name))
                    UnityEngine.Object.DestroyImmediate(mb, true);
            }
        }

        /// <summary>Removes every Collider from <paramref name="go"/> and its children.</summary>
        private static void StripColliders(GameObject go)
        {
            foreach (var col in go.GetComponentsInChildren<Collider>(true))
                if (col != null) UnityEngine.Object.DestroyImmediate(col, true);
        }

        /// <summary>Sets <paramref name="go"/> and all descendants to <paramref name="layer"/>.</summary>
        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursive(child.gameObject, layer);
        }

        // =====================================================================
        //  Reflection + folder helpers
        // =====================================================================

        /// <summary>Resolves a type by full name across every loaded assembly.</summary>
        private static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName, false);
                if (type != null) return type;
            }
            return null;
        }

        /// <summary>Creates <paramref name="dir"/> (and parents) if it does not exist.</summary>
        private static void EnsureFolder(string dir)
        {
            if (AssetDatabase.IsValidFolder(dir)) return;

            string parent = Path.GetDirectoryName(dir)?.Replace('\\', '/');
            string leaf = Path.GetFileName(dir);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf)) return;

            if (!AssetDatabase.IsValidFolder(parent))
                EnsureFolder(parent);

            AssetDatabase.CreateFolder(parent, leaf);
        }

        /// <summary>Runs an asset op, logging + swallowing any throw so the build never half-aborts.</summary>
        private static void Guard(Action op, string what)
        {
            try { op(); }
            catch (Exception e)
            {
                Debug.LogError("[DragonAnimatorSetup] '" + what + "' threw: " + e.Message);
            }
        }
    }
}
