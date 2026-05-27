// =============================================================================
// NpcPackBuild — DEF-91 Phase 1D-1E. Builds the Animator Controllers + prefabs for
// the CGTrader People pack (after NpcPackSetup Phase 1A-1C set the Generic rigs +
// materials). Each character uses its OWN clips (Generic, no retargeting).
// -----------------------------------------------------------------------------
//   1D  Animator Controllers (Assets/_Modules/Village/NPCs/Animators/):
//        - Wandering (Mevina, Tob): Idle/Walk/Talk + float Speed + bool IsTalking.
//        - Blacksmith: Forging (looping default) + Idle fallback.
//        - Merchant: Idle (default) + Talk + bool IsTalking.
//        Root motion OFF (NavMeshAgent drives position).
//   1E  Prefabs (Assets/_Modules/Village/NPCs/Prefabs/): SKM mesh + material +
//        Animator(controller) + NavMeshAgent + AmbientNPC + TownsfolkBubble +
//        TownsfolkDialogue. Village/AI components added by reflection (the Editor
//        asmdef does not reference DeNelle.Village / UnityEngine.AI).
// Run: Defenders -> NPC Pack - Phase 1D-1E (controllers + prefabs).
// =============================================================================

using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class NpcPackBuild
    {
        private const string PackRoot = "Assets/Models/People";
        private const string MatDir   = "Assets/_Modules/Village/NPCs/Materials";
        private const string AnimDir  = "Assets/_Modules/Village/NPCs/Animators";
        private const string PrefDir  = "Assets/Resources/NPCs";   // Resources so VillageNpcInjector can Resources.Load the prefabs (Phase 3)

        [MenuItem("Defenders/NPC Pack - Phase 1D-1E (controllers + prefabs)")]
        public static void BuildControllersAndPrefabs()
        {
            EnsureFolder(AnimDir);
            EnsureFolder(PrefDir);

            // ── 1D Animator Controllers ─────────────────────────────────────────
            var acMevina = BuildWandering($"{AnimDir}/AC_AmbientNPC_Mevina.controller",
                Clip("Peasant", "_Idle_1"), Clip("Peasant", "_Walk"), Clip("Peasant", "_Talking"));
            var acTob = BuildWandering($"{AnimDir}/AC_AmbientNPC_Tob.controller",
                Clip("Peasant Tob", "_Idle_1"), Clip("Peasant Tob", "_Walk"), Clip("Peasant Tob", "_Talking"));
            var acBlacksmith = BuildLoopWithFallback($"{AnimDir}/AC_Blacksmith.controller",
                "Forging", Clip("Blacksmith", "_Forging"), Clip("Blacksmith", "_Idle_1"));
            var acMerchant = BuildTalk($"{AnimDir}/AC_Merchant.controller",
                Clip("Merchant", "_Idle_1"), Clip("Merchant", "_Talking2"));

            // ── 1E Prefabs ──────────────────────────────────────────────────────
            int prefabs = 0;
            prefabs += BuildPrefab("NPC_Blacksmith",     SkmPath("Blacksmith", "SKM_Blacksmith"),            $"{MatDir}/MAT_Blacksmith.mat",     acBlacksmith);
            prefabs += BuildPrefab("NPC_Merchant",       SkmPath("Merchant", "SKM_Merchant"),                $"{MatDir}/MAT_Merchant.mat",       acMerchant);
            prefabs += BuildPrefab("NPC_Peasant_Mevina", SkmPath("Peasant", "SKM_Peasant_Mevina"),           $"{MatDir}/MAT_Peasant_Mevina.mat", acMevina);
            prefabs += BuildPrefab("NPC_Peasant_Tob",    SkmPath("Peasant Tob", "SKM_Peasant_Tob_Unity"),    $"{MatDir}/MAT_Peasant_Tob.mat",    acTob);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[NpcPackBuild] Phase 1D-1E done: 4 controllers, {prefabs} prefabs.");
        }

        // One-shot (Phase 3): move the already-built prefabs from the old module
        // folder into Resources/NPCs (GUID-preserved) so VillageNpcInjector can
        // Resources.Load them. Harmless to re-run.
        [MenuItem("Defenders/NPC Pack - Relocate Prefabs to Resources")]
        public static void RelocateNpcPrefabs()
        {
            EnsureFolder(PrefDir);
            const string oldDir = "Assets/_Modules/Village/NPCs/Prefabs";
            string[] names = { "NPC_Blacksmith", "NPC_Merchant", "NPC_Peasant_Mevina", "NPC_Peasant_Tob" };
            int moved = 0;
            foreach (var n in names)
            {
                string src = $"{oldDir}/{n}.prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(src) == null) continue;
                string err = AssetDatabase.MoveAsset(src, $"{PrefDir}/{n}.prefab");
                if (string.IsNullOrEmpty(err)) moved++;
                else Debug.LogWarning($"[NpcPackBuild] move {n}: {err}");
            }
            if (AssetDatabase.IsValidFolder(oldDir)) AssetDatabase.DeleteAsset(oldDir);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[NpcPackBuild] relocated {moved} prefab(s) to {PrefDir}.");
        }

        // ── Controller builders ─────────────────────────────────────────────────
        private static AnimatorController BuildWandering(string path, AnimationClip idle, AnimationClip walk, AnimationClip talk)
        {
            var ac = AnimatorController.CreateAnimatorControllerAtPath(path);
            ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ac.AddParameter("IsTalking", AnimatorControllerParameterType.Bool);
            var sm = ac.layers[0].stateMachine;

            var sIdle = sm.AddState("Idle"); sIdle.motion = idle;
            var sWalk = sm.AddState("Walk"); sWalk.motion = walk;
            var sTalk = sm.AddState("Talk"); sTalk.motion = talk;
            sm.defaultState = sIdle;

            Trans(sIdle, sWalk, "Speed", AnimatorConditionMode.Greater, 0.1f, 0.1f);
            Trans(sWalk, sIdle, "Speed", AnimatorConditionMode.Less, 0.05f, 0.15f);

            var toTalk = sm.AddAnyStateTransition(sTalk);
            toTalk.AddCondition(AnimatorConditionMode.If, 0f, "IsTalking");
            toTalk.hasExitTime = false; toTalk.duration = 0.1f; toTalk.canTransitionToSelf = false;
            var backFromTalk = sTalk.AddTransition(sIdle);
            backFromTalk.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsTalking");
            backFromTalk.hasExitTime = false; backFromTalk.duration = 0.15f;
            return ac;
        }

        private static AnimatorController BuildTalk(string path, AnimationClip idle, AnimationClip talk)
        {
            var ac = AnimatorController.CreateAnimatorControllerAtPath(path);
            ac.AddParameter("IsTalking", AnimatorControllerParameterType.Bool);
            var sm = ac.layers[0].stateMachine;
            var sIdle = sm.AddState("Idle"); sIdle.motion = idle;
            var sTalk = sm.AddState("Talk"); sTalk.motion = talk;
            sm.defaultState = sIdle;

            var toTalk = sm.AddAnyStateTransition(sTalk);
            toTalk.AddCondition(AnimatorConditionMode.If, 0f, "IsTalking");
            toTalk.hasExitTime = false; toTalk.duration = 0.1f; toTalk.canTransitionToSelf = false;
            var back = sTalk.AddTransition(sIdle);
            back.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsTalking");
            back.hasExitTime = false; back.duration = 0.15f;
            return ac;
        }

        private static AnimatorController BuildLoopWithFallback(string path, string mainName, AnimationClip main, AnimationClip fallback)
        {
            var ac = AnimatorController.CreateAnimatorControllerAtPath(path);
            var sm = ac.layers[0].stateMachine;
            var sMain = sm.AddState(mainName); sMain.motion = main;
            var sIdle = sm.AddState("Idle");   sIdle.motion = fallback;
            sm.defaultState = sMain;   // always doing its job (forging) by default
            return ac;
        }

        private static void Trans(AnimatorState from, AnimatorState to, string param, AnimatorConditionMode mode, float threshold, float dur)
        {
            var t = from.AddTransition(to);
            t.AddCondition(mode, threshold, param);
            t.hasExitTime = false;
            t.duration = dur;
        }

        // ── Prefab assembly ──────────────────────────────────────────────────────
        private static int BuildPrefab(string prefabName, string skmPath, string matPath, AnimatorController controller)
        {
            var skm = AssetDatabase.LoadAssetAtPath<GameObject>(skmPath);
            if (skm == null) { Debug.LogWarning($"[NpcPackBuild] SKM missing: {skmPath}"); return 0; }
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);

            var go = (GameObject)PrefabUtility.InstantiatePrefab(skm);
            go.name = prefabName;

            if (mat != null)
                foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    smr.sharedMaterial = mat;

            var anim = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
            anim.runtimeAnimatorController = controller;
            anim.applyRootMotion = false;

            AddByName(go, "UnityEngine.AI.NavMeshAgent");
            AddByName(go, "DeNelle.Village.AmbientNPC");
            AddByName(go, "DeNelle.Village.TownsfolkBubble");
            // TownsfolkDialogue is a static line-pool class (not a Component);
            // AmbientNPC uses it statically, so the prefab needs no instance.

            string path = $"{PrefDir}/{prefabName}.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
            return 1;
        }

        // ── Helpers ──────────────────────────────────────────────────────────────
        private static string SkmPath(string folder, string skmName) => $"{PackRoot}/{folder}/{skmName}.fbx";

        // Find the AnimationClip whose AS_* FBX (in <folder>/Animation/) ends with suffix.
        private static AnimationClip Clip(string folder, string suffix)
        {
            string animDir = $"{PackRoot}/{folder}/Animation";
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { animDir }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                var file = Path.GetFileNameWithoutExtension(p);
                if (!file.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) continue;
                var clip = AssetDatabase.LoadAllAssetsAtPath(p).OfType<AnimationClip>()
                                        .FirstOrDefault(c => c != null && !c.name.StartsWith("__"));
                if (clip != null) return clip;
            }
            Debug.LogWarning($"[NpcPackBuild] clip not found: {folder} *{suffix}");
            return null;
        }

        private static void AddByName(GameObject go, string typeFullName)
        {
            var t = ResolveType(typeFullName);
            if (t == null) { Debug.LogWarning($"[NpcPackBuild] type not found: {typeFullName}"); return; }
            if (!typeof(Component).IsAssignableFrom(t))
            {
                Debug.LogWarning($"[NpcPackBuild] not a Component, skipping: {typeFullName}");
                return;
            }
            if (go.GetComponent(t) == null) go.AddComponent(t);
        }

        private static Type ResolveType(string fullName)
        {
            var t = Type.GetType(fullName);
            if (t != null) return t;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                t = asm.GetType(fullName);
                if (t != null) return t;
            }
            return null;
        }

        private static void EnsureFolder(string dir)
        {
            if (AssetDatabase.IsValidFolder(dir)) return;
            var parent = Path.GetDirectoryName(dir).Replace("\\", "/");
            var leaf = Path.GetFileName(dir);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
