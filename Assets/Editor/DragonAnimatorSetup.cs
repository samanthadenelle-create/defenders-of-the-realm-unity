// =============================================================================
// DragonAnimatorSetup — builds the Black Dragon AnimatorController + boss prefab.
// -----------------------------------------------------------------------------
// Two static entry points the main Unity session runs (Defenders menu, or via
// the Unity -executeMethod flag):
//
//     -executeMethod DeNelle.Editor.DragonAnimatorSetup.BuildDragonAnimator
//     -executeMethod DeNelle.Editor.DragonAnimatorSetup.BuildDragonBossPrefab
//
// (BuildAll runs both, in order.)
//
// THE ASSET. Assets/Black Dragon/Dragon_Baked_Actions_fbx_7.4_binary.fbx — a
// rigged dragon (Generic rig, animationType 2) whose .fbx.meta exposes FOUR
// baked animation takes:
//     Armature|Fly_New   Armature|Idel_New (sic)   Armature|Run_New   Armature|Walk_New
// Like the KayKit Character Animations FBXs, it imports with clipAnimations: []
// — Unity auto-splits the four takes into named AnimationClip sub-assets. There
// is NO Attack take and NO Death take in the file.
//
// ── WHAT BuildDragonAnimator DOES ────────────────────────────────────────────
// Builds Assets/Generated/Animators/Dragon.controller — exactly the AnimatorSetup
// pattern: scan the FBX sub-assets with LoadAllAssetRepresentationsAtPath, match
// each state's clip by case-insensitive keyword search (so it is correct whether
// the take-names come through as "Fly_New" or "Armature|Fly_New"). States:
//     Fly    — the airborne loop (default state — a flying boss is always aloft)
//     Idle   — grounded / hover-still loop (the dragon's "Idel_New" take)
//     Attack — the strike beat. The FBX has NO attack clip, so this state reuses
//              the Fly clip; DragonBoss.cs realises the attack as a dive-swoop +
//              VFX, not a dedicated clip. The state still exists so the Attack
//              trigger is valid and a bespoke clip can be dropped in later.
//     Death  — the collapse. The FBX has NO death clip; DragonBoss.cs realises
//              death as a code-driven spiralling fall. The state reuses Idle as
//              a safe placeholder Motion so the controller is valid.
// Parameters: Speed (float), Attack (trigger), Dead (bool) — the exact names
// DragonBoss.cs drives by Animator.StringToHash.
//
// ── WHAT BuildDragonBossPrefab DOES ──────────────────────────────────────────
// Assembles Assets/Prefabs/Village/Generated/Boss_Dragon.prefab:
//   • the dragon FBX as the visual child (an Animator with Dragon.controller),
//   • a CapsuleCollider sized to the dragon (the body the hero / pets sweep —
//     a TRIGGER, matching the Enemy prefab discipline),
//   • the DragonBoss MonoBehaviour and an EnemyDamageable-style seam. DragonBoss
//     implements IDamageable itself, so NO adapter is needed — the prefab is the
//     boss component + the collider + the rig.
//
// ── ASSEMBLY / ISOLATION NOTE ────────────────────────────────────────────────
// Lives in the fixed DeNelle.Editor.asmdef (Editor-only). DeNelle.Editor does
// NOT reference DeNelle.Village, so DragonBoss is added by REFLECTION (resolved
// by full type name) — the same discipline VillageSceneBuilder uses. This script
// takes NO compile-time dependency on any gameplay type.
//
// IDEMPOTENT — re-running overwrites the .controller and .prefab in place.
//
// This script does NOT run itself; the main Unity session triggers it.
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
    /// Editor utility that builds the Black Dragon's <see cref="AnimatorController"/>
    /// and assembles the flying-boss prefab from the imported dragon FBX.
    /// Entry points: <see cref="BuildDragonAnimator"/>, <see cref="BuildDragonBossPrefab"/>.
    /// </summary>
    public static class DragonAnimatorSetup
    {
        // ── Paths ─────────────────────────────────────────────────────────────
        private const string DragonFbx =
            "Assets/Black Dragon/Dragon_Baked_Actions_fbx_7.4_binary.fbx";

        private const string AnimatorDir  = "Assets/Generated/Animators";
        private const string ControllerPath = AnimatorDir + "/Dragon.controller";

        private const string PrefabDir  = "Assets/Prefabs/Village/Generated";
        private const string PrefabPath = PrefabDir + "/Boss_Dragon.prefab";

        // ── Gameplay type — resolved by reflection (no asmdef dependency) ──────
        private const string TypeDragonBoss = "DeNelle.Village.DragonBoss";

        // ── Animator parameter names — MUST match DragonBoss.cs's hashes ───────
        private const string PSpeed  = "Speed";   // float   — Idle <-> Fly blend
        private const string PAttack = "Attack";  // trigger — strike beat
        private const string PDead   = "Dead";    // bool    — death (latch)

        // Speed above this counts as "flying" (the dragon's cruise is ~14).
        private const float FlyThreshold = 0.5f;

        // The enemy physics layer (matches VillageSceneBuilder's EnemyLayer).
        private const int EnemyLayer = 8;

        // =====================================================================
        //  Entry points
        // =====================================================================

        /// <summary>Builds the controller AND the boss prefab, in order.</summary>
        [MenuItem("Defenders/Animation/Build Dragon Boss (Controller + Prefab)")]
        public static void BuildAll()
        {
            BuildDragonAnimator();
            BuildDragonBossPrefab();
        }

        /// <summary>
        /// Builds <c>Assets/Generated/Animators/Dragon.controller</c> — Fly / Idle
        /// / Attack / Death states wired to the dragon FBX's baked clips, with the
        /// Speed / Attack / Dead parameters. Idempotent.
        /// </summary>
        [MenuItem("Defenders/Animation/Build Dragon Animator Controller")]
        public static void BuildDragonAnimator()
        {
            EnsureFolder(AnimatorDir);

            // ── Gather the dragon's baked clips ──────────────────────────────
            var clips = LoadDragonClips();
            if (clips.Count == 0)
            {
                Debug.LogWarning(
                    "[DragonAnimatorSetup] No AnimationClip sub-assets found in '" +
                    DragonFbx + "'. The controller will be built with EMPTY states " +
                    "(still a valid asset) — re-run once the FBX has imported.");
            }
            else
            {
                Debug.Log(
                    "[DragonAnimatorSetup] Dragon clip library: " + clips.Count +
                    " clip(s) — " + string.Join(", ", clips.Select(c => c.name)));
            }

            // ── Clip resolution (keyword search — robust to 'Armature|' prefix) ─
            AnimationClip flyClip  = FindClip(clips, "fly", "flight", "soar", "glide");
            // The FBX's idle take is mis-spelled "Idel" — match both spellings.
            AnimationClip idleClip = FindClip(clips, "idle", "idel", "rest", "hover");
            // No attack take exists — reuse Fly so the strike beat reads as a
            // wing-beat; DragonBoss drives the actual dive in code.
            AnimationClip attackClip = FindClip(clips, "attack", "bite", "strike", "claw")
                                       ?? flyClip;
            // No death take — reuse Idle as a safe placeholder Motion.
            AnimationClip deathClip = FindClip(clips, "death", "die", "dead")
                                      ?? idleClip;

            // ── Build the controller ─────────────────────────────────────────
            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter(PSpeed,  AnimatorControllerParameterType.Float);
            controller.AddParameter(PAttack, AnimatorControllerParameterType.Trigger);
            controller.AddParameter(PDead,   AnimatorControllerParameterType.Bool);

            var sm = controller.layers[0].stateMachine;

            // Fly is the default — a flying boss spends the encounter aloft.
            var fly = sm.AddState("Fly");
            fly.motion = flyClip;
            sm.defaultState = fly;

            var idle = sm.AddState("Idle");
            idle.motion = idleClip;

            var attack = sm.AddState("Attack");
            attack.motion = attackClip;

            var death = sm.AddState("Death");
            death.motion = deathClip;

            // ── Transitions ──────────────────────────────────────────────────
            // Fly <-> Idle on the Speed float — the locomotion blend. The dragon
            // is almost always above the threshold; Idle is the grounded fallback.
            var flyToIdle = fly.AddTransition(idle);
            flyToIdle.AddCondition(AnimatorConditionMode.Less, FlyThreshold, PSpeed);
            flyToIdle.hasExitTime = false;
            flyToIdle.duration = 0.25f;

            var idleToFly = idle.AddTransition(fly);
            idleToFly.AddCondition(AnimatorConditionMode.Greater, FlyThreshold, PSpeed);
            idleToFly.hasExitTime = false;
            idleToFly.duration = 0.25f;

            // Attack — fired by the Attack trigger from Fly or Idle; plays once
            // then returns to Fly (the dragon climbs back to its orbit).
            AddTriggerTransition(fly, attack, PAttack);
            AddTriggerTransition(idle, attack, PAttack);
            var attackOut = attack.AddTransition(fly);
            attackOut.hasExitTime = true;
            attackOut.exitTime = 0.8f;
            attackOut.duration = 0.2f;

            // Death — latched by the Dead bool from ANY state, never transitions
            // out (DragonBoss.cs drives the spiralling fall and then destroys).
            AddDeathTransition(fly, death);
            AddDeathTransition(idle, death);
            AddDeathTransition(attack, death);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "[DragonAnimatorSetup] Built " + ControllerPath + " — states Fly/Idle/" +
                "Attack/Death, parameters Speed/Attack/Dead.\n" +
                "  Fly    <- " + ClipName(flyClip) + "\n" +
                "  Idle   <- " + ClipName(idleClip) + "\n" +
                "  Attack <- " + ClipName(attackClip) + " (FBX has no attack take — " +
                "reuses Fly; DragonBoss drives the dive in code)\n" +
                "  Death  <- " + ClipName(deathClip) + " (FBX has no death take — " +
                "reuses Idle; DragonBoss drives the fall in code)");
        }

        /// <summary>
        /// Assembles <c>Assets/Prefabs/Village/Generated/Boss_Dragon.prefab</c> —
        /// the dragon FBX visual + an Animator on <c>Dragon.controller</c> + a
        /// trigger CapsuleCollider + the <c>DragonBoss</c> component (added by
        /// reflection). Idempotent — overwrites the prefab in place.
        /// </summary>
        [MenuItem("Defenders/Animation/Build Dragon Boss Prefab")]
        public static void BuildDragonBossPrefab()
        {
            EnsureFolder(PrefabDir);

            var dragonBossType = FindType(TypeDragonBoss);
            if (dragonBossType == null)
            {
                Debug.LogError(
                    "[DragonAnimatorSetup] Type '" + TypeDragonBoss + "' not found — " +
                    "is the DeNelle.Village assembly compiled? Prefab build aborted.");
                return;
            }

            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(DragonFbx);
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                Debug.LogWarning(
                    "[DragonAnimatorSetup] Dragon.controller not found — run " +
                    "'Build Dragon Animator Controller' first. Prefab will have an " +
                    "Animator with no controller assigned.");
            }

            // Build the prefab content in a temp root.
            var root = new GameObject("Boss_Dragon");
            try
            {
                root.layer = EnemyLayer;

                // ── Visual child — the dragon FBX (placeholder on a miss) ────
                GameObject visual;
                if (fbx != null)
                {
                    visual = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
                    visual.name = "DragonRig";
                }
                else
                {
                    Debug.LogWarning(
                        "[DragonAnimatorSetup] Dragon FBX not found at '" + DragonFbx +
                        "' — using a placeholder capsule for the visual.");
                    visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    visual.name = "[PLACEHOLDER] DragonRig";
                    visual.transform.localScale = new Vector3(2f, 3f, 5f);
                }
                visual.transform.SetParent(root.transform, false);
                visual.transform.localPosition = Vector3.zero;

                // Strip any colliders the FBX hierarchy carries — the boss's own
                // capsule below is the single physics body (Enemy-prefab rule).
                StripColliders(visual);

                // ── Animator — assign Dragon.controller onto the rig ─────────
                // A rigged FBX imports with an Animator on its root; reuse it if
                // present, otherwise add one to the visual child.
                var animator = visual.GetComponentInChildren<Animator>();
                if (animator == null)
                    animator = visual.AddComponent<Animator>();
                if (controller != null)
                    animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false; // DragonBoss owns the flight

                // ── Collider — the body hero abilities + pets sweep ──────────
                // A trigger capsule, oriented along Z (the dragon is long): the
                // hero/pet OverlapSphere finds triggers; a trigger body also
                // keeps the airborne dragon from physically shoving anything.
                var capsule = root.AddComponent<CapsuleCollider>();
                capsule.direction = 2;            // Z-axis — along the dragon body
                capsule.height = 7f;
                capsule.radius = 1.6f;
                capsule.center = new Vector3(0f, 0f, 0f);
                capsule.isTrigger = true;

                // ── DragonBoss component (reflection — no asmdef dependency) ─
                if (root.GetComponent(dragonBossType) == null)
                    root.AddComponent(dragonBossType);

                SetLayerRecursive(root, EnemyLayer);

                var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (prefab == null)
                {
                    Debug.LogError(
                        "[DragonAnimatorSetup] Failed to save the dragon boss prefab " +
                        "at '" + PrefabPath + "'.");
                }
                else
                {
                    Debug.Log(
                        "[DragonAnimatorSetup] Built " + PrefabPath + " — DragonBoss + " +
                        "trigger CapsuleCollider + DragonRig(Animator -> Dragon.controller)." +
                        "\n  INTEGRATOR: spawn this prefab for the apex dragon encounter " +
                        "and call DragonBoss.Configure(id, heartTransform, maxHp).");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        // =====================================================================
        //  Clip library
        // =====================================================================

        /// <summary>
        /// Loads every <see cref="AnimationClip"/> sub-asset out of the dragon
        /// FBX. The FBX imports with <c>clipAnimations: []</c>, so this picks up
        /// the four auto-split takes (Fly / Idle / Run / Walk) by whatever names
        /// Unity generated.
        /// </summary>
        private static List<AnimationClip> LoadDragonClips()
        {
            var clips = new List<AnimationClip>();
            var reps = AssetDatabase.LoadAllAssetRepresentationsAtPath(DragonFbx);
            if (reps == null) return clips;

            foreach (var rep in reps)
            {
                if (rep is AnimationClip clip && !clip.name.StartsWith("__preview"))
                    clips.Add(clip);
            }
            return clips;
        }

        /// <summary>
        /// Returns the first clip whose name contains ANY of <paramref name="keywords"/>
        /// (case-insensitive). Earlier keywords win. Null when nothing matches.
        /// </summary>
        private static AnimationClip FindClip(
            List<AnimationClip> library, params string[] keywords)
        {
            if (library == null || library.Count == 0) return null;

            foreach (string kw in keywords)
            {
                string needle = kw.ToLowerInvariant();
                foreach (var clip in library)
                {
                    if (clip != null &&
                        clip.name.ToLowerInvariant().Contains(needle))
                        return clip;
                }
            }
            return null;
        }

        /// <summary>Clip name for logging — "(none)" when the clip is null.</summary>
        private static string ClipName(AnimationClip clip)
        {
            return clip != null ? clip.name : "(none)";
        }

        // =====================================================================
        //  Transition helpers
        // =====================================================================

        /// <summary>Adds an interrupt transition fired by a trigger — no exit time.</summary>
        private static void AddTriggerTransition(
            AnimatorState from, AnimatorState to, string trigger)
        {
            var t = from.AddTransition(to);
            t.AddCondition(AnimatorConditionMode.If, 0f, trigger);
            t.hasExitTime = false;
            t.duration = 0.12f;
        }

        /// <summary>Adds a transition into Death gated on the <c>Dead</c> bool.</summary>
        private static void AddDeathTransition(AnimatorState from, AnimatorState death)
        {
            var t = from.AddTransition(death);
            t.AddCondition(AnimatorConditionMode.If, 0f, PDead);
            t.hasExitTime = false;
            t.duration = 0.15f;
        }

        // =====================================================================
        //  GameObject helpers
        // =====================================================================

        /// <summary>Removes every Collider from <paramref name="go"/> and its children.</summary>
        private static void StripColliders(GameObject go)
        {
            foreach (var col in go.GetComponentsInChildren<Collider>(true))
                UnityEngine.Object.DestroyImmediate(col);
        }

        /// <summary>Sets <paramref name="go"/> and all descendants to <paramref name="layer"/>.</summary>
        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
                SetLayerRecursive(child.gameObject, layer);
        }

        // =====================================================================
        //  Reflection helper
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

        // =====================================================================
        //  Folder helper
        // =====================================================================

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
    }
}
