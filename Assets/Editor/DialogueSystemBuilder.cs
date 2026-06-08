// =============================================================================
// DialogueSystemBuilder — the one-shot, batchmode-friendly entry point that
// GUARANTEES Resources/Dialogue/DialogueSystem.prefab exists and is fully wired
// the way DialogueService.cs expects (WO-358).
// -----------------------------------------------------------------------------
// WHY THIS WRAPS rather than re-implements:
// DialogueService.Host() does Resources.Load<GameObject>("Dialogue/DialogueSystem")
// + Instantiate, then GetComponentInChildren<DialogueRunner>() and reads its
// YarnProject / nodes. The PROVEN way to produce exactly that prefab already lives
// in this assembly as two reflection-light editor steps:
//   • YarnDialogueSetup.Setup()    — force-reimports DefendersDialogue.yarnproject,
//       validates it COMPILED (non-empty program), copies the paid ClassicRPG
//       "Classic RPG Dialogue System.prefab" (full Canvas UI that renders in builds
//       — NO UXML, WebGL-safe) into Assets/Resources/Dialogue/DialogueSystem.prefab,
//       and wires its DialogueRunner.yarnProject / autoStart / startNode.
//   • DialogueAdvanceSetup.Wire()  — binds tap/click <Pointer>/press advance,
//       swaps in the portrait-aware CompanionDialoguePresenter, and HIDES the big
//       blue "Next" continue indicator (alpha 0).
// Code-BUILDING the prefab from scratch would throw away the ClassicRPG Canvas
// styling that those steps preserve, so this builder DELEGATES to them (one menu /
// one batchmode method) and then HARD-VERIFIES the result has the components
// DialogueService needs: a DialogueRunner whose YarnProject is assigned and whose
// compiled program has the FTUE opener node. Anything short of that fails loudly.
//
// All Yarn access is reflection-only (this assembly has no Yarn/ClassicRPG asmdef
// reference — same constraint the two delegated steps obey).
//
//   Menu:      Defenders/Dialogue/Build DialogueSystem Prefab
//   Batchmode: DeNelle.Editor.DialogueSystemBuilder.Build
// =============================================================================

using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class DialogueSystemBuilder
    {
        const string DstPrefab = "Assets/Resources/Dialogue/DialogueSystem.prefab";

        // The node DialogueService/CompanionMeetingTrigger open as the FTUE opener.
        // We only assert the prefab's program CONTAINS it (don't require autoStart).
        const string OpenerNode = "CompanionMeeting";

        [MenuItem("Defenders/Dialogue/Build DialogueSystem Prefab")]
        public static void Build()
        {
            // 1) Copy + wire the prefab from the paid ClassicRPG package, then bind
            //    tap-to-advance + hide the blue continue indicator. Both steps log
            //    + abort on their own failures (missing package / uncompiled project).
            YarnDialogueSetup.Setup();
            DialogueAdvanceSetup.Wire();

            // 2) HARD VERIFY the produced prefab is what DialogueService.Host() expects.
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DstPrefab);
            if (prefab == null)
            {
                Debug.LogError($"[DialogueSystemBuilder] {DstPrefab} was NOT produced — see the " +
                               "YarnDialogueSetup/DialogueAdvanceSetup errors above. Dialogue would " +
                               "fail silently (DialogueService.Host can't Resources.Load it).");
                return;
            }

            Component runner = FindByExactType(prefab, "Yarn.Unity.DialogueRunner");
            if (runner == null)
            {
                Debug.LogError("[DialogueSystemBuilder] The prefab has NO Yarn.Unity.DialogueRunner — " +
                               "DialogueService.Host would reject it. Prefab is invalid.");
                return;
            }

            // DialogueRunner.YarnProject must be assigned, else DialogueService.NodeExists
            // logs "no Yarn Project assigned" and every Play() returns false.
            object project = GetMemberValue(runner, "YarnProject") ?? GetSerializedYarnProject(runner);
            if (project == null)
            {
                Debug.LogError("[DialogueSystemBuilder] DialogueRunner has NO YarnProject assigned — " +
                               "DialogueService.NodeExists() would fail for every node. Re-run after " +
                               "fixing the .yarnproject import.");
                return;
            }

            // The compiled program must carry the opener node (proves the .yarn sources
            // compiled in, not just that a project asset is referenced).
            bool hasOpener = ProjectHasNode(project, OpenerNode);
            Debug.Log($"[DialogueSystemBuilder] DONE — {DstPrefab} present, DialogueRunner + YarnProject wired. " +
                      (hasOpener
                          ? $"Compiled program contains '{OpenerNode}'. DialogueService.Play(...) will work."
                          : $"WARNING: compiled program does NOT contain '{OpenerNode}' — check that its .yarn " +
                            "is included in DefendersDialogue.yarnproject (other nodes may still play)."));
        }

        // ── reflection-light helpers (no Yarn asmdef reference) ──────────────

        static Component FindByExactType(GameObject root, string fullName)
        {
            foreach (var c in root.GetComponentsInChildren<Component>(true))
                if (c != null && c.GetType().FullName == fullName) return c;
            return null;
        }

        // Try the public YarnProject property (Yarn Spinner v3 exposes it).
        static object GetMemberValue(object obj, string name)
        {
            if (obj == null) return null;
            var t = obj.GetType();
            var prop = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && prop.CanRead)
            {
                try { return prop.GetValue(obj); } catch { /* fall through */ }
            }
            return null;
        }

        // Fallback: read the [SerializeField] internal yarnProject field directly.
        static object GetSerializedYarnProject(object runner)
        {
            var f = runner.GetType().GetField("yarnProject",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            try { return f != null ? f.GetValue(runner) : null; } catch { return null; }
        }

        // YarnProject.NodeNames (string[]) lists every compiled node.
        static bool ProjectHasNode(object project, string node)
        {
            var prop = project.GetType().GetProperty("NodeNames",
                BindingFlags.Public | BindingFlags.Instance);
            object val = null;
            try { if (prop != null) val = prop.GetValue(project); } catch { return false; }
            if (val is string[] names)
            {
                foreach (var n in names)
                    if (n == node) return true;
            }
            return false;
        }
    }
}
