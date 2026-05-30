// =============================================================================
// HeroAnimatorFactory — builds per-class hero AnimatorControllers from the
// Mixamo clips in Assets/Action/ (WO-140). Replaces the obsolete Tripo-era
// HeroAnimatorSetup (which scraped 2 generic NLA takes per FBX).
// -----------------------------------------------------------------------------
// Data-driven: ONE Build(HeroSpec) algorithm consumes a per-class spec, so the
// three heroes are three spec literals (not three code paths) — refactor-ready
// toward a shared AnimatorBuildSpec when the catalog grows (NOT a meta-factory;
// see WO-140). NOT recursive: a controller is a flat state list + a blend tree.
//
// Runtime contract (verified in HeroLocomotion/HeroAbilities, 2026-05-30):
//   Speed   (float)   — HeroLocomotion sets = velocity magnitude → Idle/Walk/Run
//   Cast    (trigger) — HeroAbilities.TryCast → the class attack
//   Victory (trigger) — HeroLocomotion on wave clear → victory pose
// Controllers load via Resources.Load("Heroes/<slug>") where slug ∈
// {Knight, Ranger, Mage}; output paths are fixed at Resources/Heroes/<slug>.controller.
//
// Clips are Humanoid (Assets/Action are animationType 3 + CreateFromThisModel),
// so one clip retargets onto every hero. Every clip lookup is null-guarded — a
// missing FBX logs a warning and skips that state; the build still completes.
// =============================================================================
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class HeroAnimatorFactory
    {
        private const string ActionDir = "Assets/Action/";

        // Shared locomotion clips (all classes) — FBX basenames in Assets/Action.
        private const string IdleClip      = "standing idle 01";
        private const string WalkClip      = "standing walk forward";
        private const string RunClip       = "standing run forward";
        private const string VictoryClip   = "Joyful Jump";   // placeholder cheer

        private struct HeroSpec
        {
            public string slug;            // Knight | Ranger | Mage
            public string controllerPath;  // Resources/Heroes/<slug>.controller
            public string castClip;        // per-class attack clip basename
            public string castClipFallback;// used + warned if the primary is absent
        }

        private static readonly HeroSpec[] Specs =
        {
            new HeroSpec { slug = "Knight",
                controllerPath = "Assets/Resources/Heroes/Knight.controller",
                castClip = "Sword And Shield Attack", castClipFallback = "Sword And Shield Power Up" },
            new HeroSpec { slug = "Mage",
                controllerPath = "Assets/Resources/Heroes/Mage.controller",
                castClip = "Spell Cast", castClipFallback = "Standing 1H Magic Attack 03" },
            // Ranger has no Mixamo bow clip in Assets/Action — placeholder + warn.
            new HeroSpec { slug = "Ranger",
                controllerPath = "Assets/Resources/Heroes/Ranger.controller",
                castClip = "Standing 2H Magic Attack 01", castClipFallback = "Spell Cast" },
        };

        [MenuItem("Defenders/Animation/Build Hero Animators (Mixamo)")]
        public static void BuildAll()
        {
            foreach (var spec in Specs) Build(spec);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[HeroAnimatorFactory] BuildAll complete — Knight / Mage / Ranger.");
        }

        private static void Build(HeroSpec spec)
        {
            AnimationClip idle    = LoadClip(IdleClip);
            AnimationClip walk    = LoadClip(WalkClip);
            AnimationClip run     = LoadClip(RunClip);
            AnimationClip victory = LoadClip(VictoryClip);
            AnimationClip cast    = LoadClip(spec.castClip);
            if (cast == null && !string.IsNullOrEmpty(spec.castClipFallback))
            {
                cast = LoadClip(spec.castClipFallback);
                if (cast != null)
                    Debug.LogWarning($"[HeroAnimatorFactory] {spec.slug}: cast clip '{spec.castClip}' " +
                                     $"missing — using fallback '{spec.castClipFallback}' (placeholder).");
            }

            // Idempotent: recreate the controller from scratch.
            AssetDatabase.DeleteAsset(spec.controllerPath);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(spec.controllerPath);

            ctrl.AddParameter("Speed",   AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Cast",    AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Victory", AnimatorControllerParameterType.Trigger);

            var sm = ctrl.layers[0].stateMachine;

            // ── Locomotion: a 1-D blend tree on Speed (Idle→Walk→Run) ──────────
            // HeroLocomotion feeds a scalar Speed, so a 1-D forward blend is the
            // right model (strafe clips left for a later 2-D pass).
            var locoState = sm.AddState("Locomotion");
            sm.defaultState = locoState;

            var blend = new BlendTree { name = "Locomotion", blendType = BlendTreeType.Simple1D,
                                        blendParameter = "Speed",
                                        // CRITICAL: Unity defaults useAutomaticThresholds = true, which
                                        // OVERWRITES the AddChild thresholds with auto-spaced 0/0.5/1 —
                                        // that's why walk@6/run@9 never stuck and the hero skipped walk
                                        // (Speed 0..6 vs thresholds 0/0.5/1 = always max = run). Turn it
                                        // OFF so the explicit thresholds below are honored.
                                        useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(blend, ctrl);
            locoState.motion = blend;
            // Null-guarded children: only add the clips that loaded. Always have at
            // least one motion so the state isn't empty.
            // Thresholds tuned to HeroLocomotion's actual feed: _moveSpeed = 6, so
            // Speed (= Velocity.magnitude) ramps 0→6. The hero has ONE move speed,
            // so 6 must land in the WALK band (else it skips walk → straight to run,
            // the "skips walk" bug). Walk centred at 6; Run only if speed exceeds ~9.
            var added = new List<float>();
            if (idle != null) { blend.AddChild(idle, 0f);  added.Add(0f); }
            if (walk != null) { blend.AddChild(walk, 6f);  added.Add(6f); }
            if (run  != null) { blend.AddChild(run,  9f);  added.Add(9f); }
            if (added.Count == 0)
                Debug.LogWarning($"[HeroAnimatorFactory] {spec.slug}: no locomotion clips found — " +
                                 "Locomotion state is empty.");

            // ── Cast — Any → Cast on the trigger, returns to Locomotion ────────
            if (cast != null)
            {
                var castState = sm.AddState("Cast");
                castState.motion = cast;
                var toCast = sm.AddAnyStateTransition(castState);
                toCast.hasExitTime = false; toCast.duration = 0.05f;
                toCast.AddCondition(AnimatorConditionMode.If, 0f, "Cast");
                var castBack = castState.AddTransition(locoState);
                castBack.hasExitTime = true; castBack.exitTime = 0.9f; castBack.duration = 0.1f;
            }
            else
            {
                Debug.LogWarning($"[HeroAnimatorFactory] {spec.slug}: no cast clip — " +
                                 "Cast trigger will no-op (param still declared).");
            }

            // ── Victory — Any → Victory on the trigger, returns to Locomotion ──
            if (victory != null)
            {
                var vicState = sm.AddState("Victory");
                vicState.motion = victory;
                var toVic = sm.AddAnyStateTransition(vicState);
                toVic.hasExitTime = false; toVic.duration = 0.1f;
                toVic.AddCondition(AnimatorConditionMode.If, 0f, "Victory");
                var vicBack = vicState.AddTransition(locoState);
                vicBack.hasExitTime = true; vicBack.exitTime = 0.95f; vicBack.duration = 0.15f;
            }

            EditorUtility.SetDirty(ctrl);
            Debug.Log($"[HeroAnimatorFactory] {spec.slug} built — Locomotion({added.Count} clips)" +
                      $"{(cast != null ? " + Cast" : "")}{(victory != null ? " + Victory" : "")} " +
                      $"→ {spec.controllerPath}");
        }

        /// <summary>Load the Humanoid AnimationClip from an Assets/Action FBX by basename.
        /// Returns the first real clip sub-asset (skips the __preview__ clip). Null + warn if absent.</summary>
        private static AnimationClip LoadClip(string fbxBaseName)
        {
            string path = ActionDir + fbxBaseName + ".fbx";
            var assets = AssetDatabase.LoadAllAssetsAtPath(path);
            if (assets == null || assets.Length == 0)
            {
                Debug.LogWarning($"[HeroAnimatorFactory] clip FBX not found: {path}");
                return null;
            }
            foreach (var a in assets)
            {
                if (a is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                    return clip;
            }
            Debug.LogWarning($"[HeroAnimatorFactory] no AnimationClip inside {path}");
            return null;
        }
    }
}
