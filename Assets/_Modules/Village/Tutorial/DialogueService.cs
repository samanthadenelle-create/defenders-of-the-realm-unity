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
        /// True if <paramref name="node"/> exists in the shared dialogue system's
        /// compiled Yarn program. Hosts the system if needed (so it works before the
        /// first Play). Used by callers to gate OPTIONAL dialogue beats — e.g. a
        /// welcome node that may not be authored yet — without faulting when absent.
        /// Returns false (and never throws) if the prefab/project/node is missing.
        /// </summary>
        public static bool NodeExists(string node)
        {
            if (string.IsNullOrEmpty(node)) return false;
            DialogueRunner runner = Current ?? Host();
            if (runner == null) return false;   // Host() already logged the reason.
            return NodeExists(runner, node);
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

            // WEBGL CRASH-GUARD (WO-331): StartDialogue runs its synchronous prologue
            // (program load, first-line / command dispatch, presenter init) INLINE before
            // it yields. A throw there (e.g. a Yarn SignalContentComplete fired outside a
            // running command, a missing presenter, a command-handler exception) would
            // escape here — and under WebGL (ExplicitlyThrownExceptionsOnly) an uncaught
            // throw on Village load HALTS the player. Dialogue is non-essential to the
            // village loading, so a failure must degrade (log) — never crash the scene.
            try
            {
                runner.StartDialogue(node).Forget();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[DialogueService] StartDialogue('{node}') threw — dialogue skipped so " +
                               "the village still loads (WebGL crash-guard). " + ex);
                return false;
            }
            Debug.Log($"[DialogueService] Playing '{node}'.");
            return true;
        }

        /// <summary>
        /// Opens the ONE parameterized building-interaction node (StructureMenu) for
        /// <paramref name="structureId"/> — sets the $structureId Yarn variable, then
        /// plays. The node + commands scope everything to that building's own data, so
        /// the same flow serves every structure ("rinse and repeat, just the parameter").
        /// </summary>
        /// <summary>Stops the current dialogue immediately (used by walk-away auto-close).</summary>
        public static void Stop()
        {
            if (Current != null && Current.IsDialogueRunning) Current.Stop();
        }

        // ── New-Game dialogue reset (DeNelle.Core decoupling hook) ────────────
        // Onboarding's "Start New" must begin with clean Yarn state. Onboarding
        // can't reference this Village assembly (or Yarn), so it calls
        // DeNelle.Core.DialogueResetService.ResetForNewGame(), which invokes the
        // YarnVariableClear hook we register below. Mirrors IntroLauncher.

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterResetHook()
        {
            DeNelle.Core.DialogueResetService.YarnVariableClear = ClearVariableStorage;
        }

        /// <summary>
        /// Wipes the live runner's Yarn VARIABLE STORAGE (every $-toggle and the
        /// visited-node flags Yarn tracks there) so a New Game starts with clean
        /// dialogue state. No-op when no runner is hosted yet (title time, before
        /// any village scene) — there is simply no in-process Yarn state to clear,
        /// and a fresh runner starts empty anyway. We do NOT instantiate a runner
        /// just to clear it (Current, not Host()).
        /// </summary>
        public static void ClearVariableStorage()
        {
            var runner = Current;
            if (runner == null) return;   // no live runner → nothing carried over
            if (runner.IsDialogueRunning) runner.Stop();   // don't clear mid-line
            var vs = runner.VariableStorage;
            if (vs != null)
            {
                vs.Clear();   // Yarn.Unity VariableStorageBehaviour.Clear() — wipes all variables
                Debug.Log("[DialogueService] Yarn variable storage cleared for New Game.");
            }
            CurrentStructureId = null;
        }

        /// <summary>The structureId of the most recent <see cref="PlayStructure"/> call.
        /// CmdStructureStatus reads THIS rather than the Yarn command arg: a bare command-arg
        /// (&lt;&lt;structure_status $structureId&gt;&gt;) arrives as the literal "$structureId" — it does
        /// not interpolate — so the arg can't be trusted. One dialogue runs at a time.</summary>
        public static string CurrentStructureId { get; private set; }

        public static bool PlayStructure(string structureId, string displayName = null)
        {
            if (string.IsNullOrEmpty(structureId)) return false;
            CurrentStructureId = structureId;

            DialogueRunner runner = Current ?? Host();
            if (runner == null) return false;
            if (runner.IsDialogueRunning) return false;   // don't interrupt an active line

            // The pet house gets its OWN "name + select your pet" flow (PetHouse node)
            // instead of the generic buy/sell/upgrade StructureMenu. Every other
            // building shares the parameterized StructureMenu. Fall back to StructureMenu
            // if the PetHouse node isn't compiled in this build.
            string node = (structureId == "pet-house" && NodeExists(runner, "PetHouse"))
                ? "PetHouse" : "StructureMenu";
            if (!NodeExists(runner, node))
            {
                Debug.LogError($"[DialogueService] '{node}' node missing — building hook can't open.");
                return false;
            }

            if (runner.VariableStorage != null)
            {
                runner.VariableStorage.SetValue("$structureId", structureId);
                // Seed the player-facing name from the building's OWN sign label so the
                // dialogue title matches the big-letters sign (e.g. "Forge", not the
                // titleized id "Workshop"). CmdStructureStatus keeps it and only fills in
                // yield/cost from the progression data.
                if (!string.IsNullOrEmpty(displayName))
                    runner.VariableStorage.SetValue("$structureName", displayName);
            }
            // WEBGL CRASH-GUARD (WO-331): see Play() — the synchronous StartDialogue
            // prologue must never escape into the frame and halt the WebGL player.
            try
            {
                runner.StartDialogue(node).Forget();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[DialogueService] StartDialogue('{node}') threw for structure " +
                               $"'{structureId}' — dialogue skipped (WebGL crash-guard). " + ex);
                return false;
            }
            Debug.Log($"[DialogueService] Structure '{structureId}' → node '{node}' ('{displayName}').");
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

            // WEBGL CRASH-GUARD (WO-331): installing the command bridge registers ~30
            // Yarn command handlers. A throw here (duplicate registration, a bad handler
            // signature, a missing sub-system) would otherwise escape the host path and
            // halt the WebGL player on Village load. Degrade to "hosted, no FTUE commands"
            // rather than crash — the runner still exists for plain dialogue.
            try
            {
                var bridgeGo = new GameObject("DialogueCommandBridge");
                bridgeGo.transform.SetParent(instance.transform, false);
                bridgeGo.AddComponent<DialogueCommandBridge>().Install(runner);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[DialogueService] DialogueCommandBridge install threw — FTUE commands " +
                               "may be unbound, but the village still loads (WebGL crash-guard). " + ex);
            }

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
