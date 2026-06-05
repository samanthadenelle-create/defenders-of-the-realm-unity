// =============================================================================
// YarnDialogueSetup — wires the paid ClassicRPG Yarn add-on into our progression.
// -----------------------------------------------------------------------------
// Copies the turnkey "Classic RPG Dialogue System" prefab (DialogueRunner +
// RPGDialoguePresenter + Canvas UI — renders in builds) out of the package into
// Resources/Dialogue, and configures its DialogueRunner to AUTO-PLAY the
// CompanionMeeting node from our compiled YarnProject. That keeps the RUNTIME
// trivial: DialogueBootstrap / CompanionMeetingTrigger just Resources.Load +
// Instantiate the prefab on village entry — no Yarn types, no asmdef ref.
//
// Fields set on DialogueRunner (Yarn Spinner v3 API — VERIFIED against the
// package source, Library/PackageCache/dev.yarnspinner.unity@*/Runtime/
// DialogueRunner/DialogueRunner.cs):
//   [SerializeField] internal YarnProject? yarnProject;   (line 171)  -> the project
//   public bool   autoStart = false;                      (line 275)  -> true
//   public string startNode = "Start";                    (line 289)  -> "CompanionMeeting"
// So the serialized field names below ("yarnProject" / "autoStart" / "startNode")
// are correct. SerializedObject.FindProperty resolves them even though
// `yarnProject` is `internal` (FindProperty is access-modifier agnostic).
//
// ROOT-CAUSE GUARD (the "No nodes have been loaded" blocker, DEF-265):
// the previous version logged a *warning* and still printed DONE when a field
// was missing — a silent pass. Worse, it never checked that the loaded
// YarnProject actually COMPILED. At runtime DialogueRunner does
// `Dialogue.SetProgram(yarnProject.Program); Dialogue.SetNode(node)`. If the
// .yarnproject failed to import, `YarnProject.compiledYarnProgram` (byte[]) is
// null -> Program parses empty -> ZERO nodes -> SetNode throws
// "No nodes have been loaded". This version now:
//   * Confirms the loaded asset is actually a Yarn.Unity.YarnProject.
//   * Reads `compiledYarnProgram` via reflection and ERRORS+ABORTS if it is
//     null/empty (the .yarnproject did not compile — re-import it in Unity).
//   * ERRORS+ABORTS (no longer warns) if any serialized field is missing.
//   * Verifies the yarnProject reference actually persisted after Apply.
// Anything short of a fully-wired prefab is now a hard, actionable failure.
//
// Run: Defenders > Yarn > Setup Dialogue System.  Re-runnable / idempotent.
// =============================================================================
using System;
using System.Reflection;
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

            // FORCE-REIMPORT the .yarnproject FIRST. Unity does NOT auto-reimport a
            // .yarnproject when its referenced .yarn files change (no declared asset
            // dependency), so it can sit with an EMPTY compiled program — the root cause
            // of the runtime "No nodes have been loaded" error. A forced reimport
            // recompiles the .yarn sources into the project before we load + validate it.
            AssetDatabase.ImportAsset(ProjectPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();

            var project = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(ProjectPath);
            if (project == null)
            {
                Debug.LogError($"[YarnDialogueSetup] YarnProject not found/compiled at '{ProjectPath}'. " +
                               "Select the .yarnproject in the Inspector and confirm it imported without errors. Aborting.");
                return;
            }

            // Verify the loaded asset is really a YarnProject (not some other main
            // asset at that path) and that it actually COMPILED. A null/empty
            // compiledYarnProgram is THE cause of the runtime "No nodes have been
            // loaded" error — catch it here instead of shipping a dead prefab.
            if (!ValidateCompiledProject(project)) return;

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
            bool wired = false;
            try
            {
                var runner = root.GetComponentInChildren(runnerType, true);
                if (runner == null)
                {
                    Debug.LogError("[YarnDialogueSetup] No DialogueRunner on the copied prefab.");
                    return;
                }
                var so = new SerializedObject(runner);

                // Each setter ABORTS (returns false) on a missing field — no more
                // silent warn-and-continue. If the Yarn API ever renames a field
                // this fails loudly instead of producing a null-project prefab.
                if (!SetObjectRef(so, "yarnProject", project)) return;
                if (!SetBool(so, "autoStart", true)) return;
                if (!SetString(so, "startNode", StartNode)) return;
                so.ApplyModifiedPropertiesWithoutUndo();

                // Confirm the project reference actually persisted onto the runner.
                var verify = new SerializedObject(runner);
                var vp = verify.FindProperty("yarnProject");
                if (vp == null || vp.objectReferenceValue != project)
                {
                    Debug.LogError("[YarnDialogueSetup] 'yarnProject' did NOT persist onto the DialogueRunner " +
                                   "after Apply — the prefab would have ZERO nodes at runtime. Aborting; the prefab was not saved.");
                    return;
                }

                PrefabUtility.SaveAsPrefabAsset(root, DstPrefab);
                wired = true;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            if (!wired) return;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[YarnDialogueSetup] DONE — {DstPrefab} wired to '{StartNode}' (autoStart) using {ProjectPath}. " +
                      "yarnProject reference verified + project compiled with nodes. " +
                      "Runtime just instantiates Resources/Dialogue/DialogueSystem.");
        }

        // Returns true only if 'project' is a Yarn.Unity.YarnProject whose
        // compiledYarnProgram byte[] is present and non-empty. Reflection-only so
        // DeNelle.Editor needn't reference the Yarn runtime asmdef.
        static bool ValidateCompiledProject(UnityEngine.Object project)
        {
            var t = project.GetType();
            if (t.FullName != "Yarn.Unity.YarnProject")
            {
                Debug.LogError($"[YarnDialogueSetup] Asset at '{ProjectPath}' is a '{t.FullName}', not a Yarn.Unity.YarnProject. " +
                               "Did the .yarnproject import correctly? Aborting.");
                return false;
            }

            var field = t.GetField("compiledYarnProgram", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null)
            {
                Debug.LogError("[YarnDialogueSetup] YarnProject has no 'compiledYarnProgram' field — Yarn Spinner API changed. Aborting.");
                return false;
            }

            var bytes = field.GetValue(project) as byte[];
            if (bytes == null || bytes.Length == 0)
            {
                Debug.LogError($"[YarnDialogueSetup] The YarnProject at '{ProjectPath}' has NO compiled program " +
                               "(compiledYarnProgram is null/empty). This is exactly what produces the runtime " +
                               "'No nodes have been loaded' error. The .yarn files did not compile into the project. " +
                               "Fix: select the .yarnproject in Unity, resolve any import errors shown in the Inspector / Console, " +
                               "and re-run this menu. Aborting (prefab NOT touched).");
                return false;
            }
            return true;
        }

        // ---- field setters: ERROR + abort (return false) when the field is missing ----
        static bool SetObjectRef(SerializedObject so, string prop, UnityEngine.Object val)
        {
            var p = so.FindProperty(prop);
            if (p == null) { Debug.LogError($"[YarnDialogueSetup] serialized field '{prop}' not found on DialogueRunner — Yarn API may have changed. Aborting."); return false; }
            p.objectReferenceValue = val;
            return true;
        }
        static bool SetBool(SerializedObject so, string prop, bool val)
        {
            var p = so.FindProperty(prop);
            if (p == null) { Debug.LogError($"[YarnDialogueSetup] serialized field '{prop}' not found on DialogueRunner. Aborting."); return false; }
            p.boolValue = val;
            return true;
        }
        static bool SetString(SerializedObject so, string prop, string val)
        {
            var p = so.FindProperty(prop);
            if (p == null) { Debug.LogError($"[YarnDialogueSetup] serialized field '{prop}' not found on DialogueRunner. Aborting."); return false; }
            p.stringValue = val;
            return true;
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
