// =============================================================================
// DialogueService — the ONE launch path for every Yarn dialogue in the game.
// -----------------------------------------------------------------------------
// All dialogue content compiles into a single DefendersDialogue.yarnproject and
// plays through a single shared prefab (Resources/Dialogue/DialogueSystem) whose
// LineAdvancer + ClassicRPG presenter were tuned for tap/click-to-advance and a
// softened continue indicator (see DialogueAdvanceSetup). This service is the
// matching launch seam: any caller — NPC talk, lore stone, intro, companion bark,
// level-up — starts dialogue the same way and inherits that advance behavior and
// styling for free:
//
//     DialogueService.Play("Lore_Elarion");
//
// It hosts-or-reuses the shared runner, installs DialogueCommandBridge BEFORE the
// runner starts (command registration must precede the run), suppresses the
// prefab's CompanionMeeting autostart so the CALLER decides the node, validates
// the node against the compiled program, then starts it. All cross-calls are
// null-guarded; a missing prefab / project / node logs and returns false rather
// than throwing, so a content gap can never hard-fault the game.
// =============================================================================

using UnityEngine;
using Yarn.Unity;

namespace DeNelle.Village
{
    /// <summary>
    /// Game-wide entry point for starting Yarn dialogue. Reuses the live shared
    /// DialogueRunner if one is already hosted, otherwise instantiates the shared
    /// Resources/Dialogue/DialogueSystem prefab and wires it once.
    /// </summary>
    public static class DialogueService
    {
        private const string PrefabResourcePath = "Dialogue/DialogueSystem";

        /// <summary>The live shared runner, or null if none is hosted yet.</summary>
        public static DialogueRunner Current => Object.FindObjectOfType<DialogueRunner>();

        /// <summary>True while any dialogue is currently playing.</summary>
        public static bool IsRunning
        {
            get { var r = Current; return r != null && r.IsDialogueRunning; }
        }

        /// <summary>
        /// Start the Yarn node <paramref name="node"/> on the shared dialogue
        /// system, hosting it first if needed. Returns false (and logs) if the
        /// prefab/project is missing, the node doesn't exist, or a dialogue is
        /// already running (an in-progress line is never interrupted).
        /// </summary>
        public static bool Play(string node)
        {
            if (string.IsNullOrEmpty(node))
            {
                Debug.LogWarning("[DialogueService] Play called with an empty node name — ignored.");
                return false;
            }

            DialogueRunner runner = Current ?? Host();
            if (runner == null) return false; // Host() already logged the reason.

            if (runner.IsDialogueRunning)
            {
                Debug.LogWarning($"[DialogueService] A dialogue is already running — '{node}' was not started " +
                                 "(the current line is not interrupted).");
                return false;
            }

            if (!NodeExists(runner, node))
            {
                Debug.LogError($"[DialogueService] Node '{node}' is not in the compiled Yarn program " +
                               "(check the spelling and that its .yarn file is in DefendersDialogue.yarnproject).");
                return false;
            }

            runner.StartDialogue(node).Forget();
            Debug.Log($"[DialogueService] Playing '{node}'.");
            return true;
        }

        // Instantiate the shared dialogue prefab, suppress its CompanionMeeting
        // autostart (the caller picks the node), and install the command bridge
        // BEFORE the runner's Start() runs (we are post-Instantiate / pre-Start).
        private static DialogueRunner Host()
        {
            var prefab = Resources.Load<GameObject>(PrefabResourcePath);
            if (prefab == null)
            {
                Debug.LogWarning($"[DialogueService] Resources/{PrefabResourcePath} not found — no dialogue " +
                                 "can play (the dialogue prefab/pack may be missing from this build).");
                return null;
            }

            var instance = Object.Instantiate(prefab);
            instance.name = "DialogueSystem";

            var runner = instance.GetComponentInChildren<DialogueRunner>(true);
            if (runner == null)
            {
                Debug.LogWarning("[DialogueService] Hosted prefab has no DialogueRunner — cannot play dialogue.");
                Object.Destroy(instance);
                return null;
            }

            // The prefab is configured to autostart CompanionMeeting; suppress that
            // so Play() controls which node runs. autoStart is read in Start(),
            // which has not run yet (Instantiate only ran Awake).
            runner.autoStart = false;

            var bridgeGo = new GameObject("DialogueCommandBridge");
            bridgeGo.transform.SetParent(instance.transform, false);
            bridgeGo.AddComponent<DialogueCommandBridge>().Install(runner);

            return runner;
        }

        private static bool NodeExists(DialogueRunner runner, string node)
        {
            YarnProject project = runner.YarnProject;
            if (project == null)
            {
                Debug.LogError("[DialogueService] The DialogueRunner has no Yarn Project assigned.");
                return false;
            }

            string[] names = project.NodeNames;
            if (names == null) return false;
            foreach (string n in names)
                if (n == node) return true;
            return false;
        }
    }
}
