// =============================================================================
// YarnDialogueSetup — wires the paid ClassicRPG Yarn add-on into our progression.
// -----------------------------------------------------------------------------
// Copies the turnkey "Classic RPG Dialogue System" prefab (DialogueRunner +
// RPGDialoguePresenter + Canvas UI — renders in builds) out of the package into
// Resources/Dialogue, and configures its DialogueRunner to AUTO-PLAY the
// CompanionMeeting node from our compiled YarnProject. That keeps the RUNTIME
// trivial: DialogueBootstrap just Resources.Load + Instantiate the prefab on the
// village progression-start hook — no Yarn types, no asmdef ref, no reflection.
//
// Fields set on DialogueRunner (Yarn Spinner v3 API, verified in DialogueRunner.cs):
//   yarnProject (internal SerializeField)  -> DefendersDialogue.yarnproject
//   autoStart   (public bool)              -> true   (Start() runs startNode)
//   startNode   (public string)            -> "CompanionMeeting"
//
// Run: Defenders > Yarn > Setup Dialogue System.  Re-runnable / idempotent.
// =============================================================================
using System;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class YarnDialogueSetup
    {
        const string SrcPrefab = "Packages/dev.yarnspinner.unity.addons.classicrpg/Runtime/Prefabs/Classic RPG Dialogue System.prefab";
        const string DstDir    = "Assets/Resources/Dialogue";
        const string DstPrefab = "Assets/Resources/Dialogue/DialogueSystem.prefab";
        const string ProjectPath = "Assets/Dialogue/DefendersDialogue.yarnproject";
        const string StartNode = "CompanionMeeting";

        [MenuItem("Defenders/Yarn/Setup Dialogue System")]
        public static void Setup()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
                AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(DstDir))
                AssetDatabase.CreateFolder("Assets/Resources", "Dialogue");

            var project = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(ProjectPath);
            if (project == null)
            {
                Debug.LogError($"[YarnDialogueSetup] YarnProject not found/compiled at '{ProjectPath}'. Aborting.");
                return;
            }

            // Fresh copy of the package prefab into Resources.
            AssetDatabase.DeleteAsset(DstPrefab);
            if (!AssetDatabase.CopyAsset(SrcPrefab, DstPrefab))
            {
                Debug.LogError($"[YarnDialogueSetup] CopyAsset failed: '{SrcPrefab}' -> '{DstPrefab}'. Is the classicrpg add-on present?");
                return;
            }
            AssetDatabase.Refresh();

            // Configure the prefab's DialogueRunner to auto-play CompanionMeeting.
            var runnerType = FindType("Yarn.Unity.DialogueRunner");
            if (runnerType == null)
            {
                Debug.LogError("[YarnDialogueSetup] Yarn.Unity.DialogueRunner type not found — is Yarn Spinner imported?");
                return;
            }

            var root = PrefabUtility.LoadPrefabContents(DstPrefab);
            try
            {
                var runner = root.GetComponentInChildren(runnerType, true);
                if (runner == null)
                {
                    Debug.LogError("[YarnDialogueSetup] No DialogueRunner on the copied prefab.");
                    return;
                }
                var so = new SerializedObject(runner);
                SetObjectRef(so, "yarnProject", project);
                SetBool(so, "autoStart", true);
                SetString(so, "startNode", StartNode);
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, DstPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[YarnDialogueSetup] DONE — {DstPrefab} wired to '{StartNode}' (autoStart) using {ProjectPath}. " +
                      "Runtime just instantiates Resources/Dialogue/DialogueSystem.");
        }

        static void SetObjectRef(SerializedObject so, string prop, UnityEngine.Object val)
        {
            var p = so.FindProperty(prop);
            if (p == null) { Debug.LogWarning($"[YarnDialogueSetup] field '{prop}' not found on DialogueRunner."); return; }
            p.objectReferenceValue = val;
        }
        static void SetBool(SerializedObject so, string prop, bool val)
        {
            var p = so.FindProperty(prop);
            if (p == null) { Debug.LogWarning($"[YarnDialogueSetup] field '{prop}' not found."); return; }
            p.boolValue = val;
        }
        static void SetString(SerializedObject so, string prop, string val)
        {
            var p = so.FindProperty(prop);
            if (p == null) { Debug.LogWarning($"[YarnDialogueSetup] field '{prop}' not found."); return; }
            p.stringValue = val;
        }

        static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName, false);
                if (t != null) return t;
            }
            return null;
        }
    }
}
