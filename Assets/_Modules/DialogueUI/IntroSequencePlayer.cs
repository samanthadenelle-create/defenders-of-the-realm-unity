// =============================================================================
// IntroSequencePlayer — plays the 9-screen cinematic intro on the dialogue spine.
// -----------------------------------------------------------------------------
// Hosts the shared Resources/Dialogue/DialogueSystem prefab (so the intro inherits
// the tap-to-advance + clean styling), installs an IntroCommandBridge (fades /
// audio / transition handlers), and starts IntroSequence at Intro_Screen1.
//
// Decoupled trigger: registers itself on Core's IntroLauncher.Play at startup so the
// Title screen's "Play Intro" button (DeNelle.Onboarding) can fire it WITHOUT a hard
// reference to the dialogue stack. Mirrors DialogueService's host-or-reuse pattern;
// suppresses the prefab's CompanionMeeting autostart so the intro node runs instead.
// =============================================================================

using UnityEngine;
using Yarn.Unity;

namespace DeNelle.DialogueUI
{
    public static class IntroSequencePlayer
    {
        private const string PrefabResourcePath = "Dialogue/DialogueSystem";
        private const string FirstNode = "Intro_Screen1";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            DeNelle.Core.IntroLauncher.Play = Play;
        }

        /// <summary>Play the cinematic intro from the first screen.</summary>
        public static void Play()
        {
            DialogueRunner runner = Object.FindObjectOfType<DialogueRunner>() ?? Host();
            if (runner == null) return;

            if (runner.IsDialogueRunning) runner.Stop();

            if (!NodeExists(runner, FirstNode))
            {
                Debug.LogError($"[IntroSequencePlayer] Node '{FirstNode}' not in the compiled Yarn program — " +
                               "intro cannot play (is IntroSequence.yarn in DefendersDialogue.yarnproject?).");
                return;
            }

            runner.StartDialogue(FirstNode).Forget();
            Debug.Log("[IntroSequencePlayer] Playing cinematic intro.");
        }

        private static DialogueRunner Host()
        {
            var prefab = Resources.Load<GameObject>(PrefabResourcePath);
            if (prefab == null)
            {
                Debug.LogWarning($"[IntroSequencePlayer] Resources/{PrefabResourcePath} not found — intro cannot play.");
                return null;
            }

            var instance = Object.Instantiate(prefab);
            instance.name = "DialogueSystem (Intro)";

            var runner = instance.GetComponentInChildren<DialogueRunner>(true);
            if (runner == null)
            {
                Debug.LogWarning("[IntroSequencePlayer] Hosted prefab has no DialogueRunner — cannot play intro.");
                Object.Destroy(instance);
                return null;
            }

            runner.autoStart = false;   // suppress the prefab's CompanionMeeting autostart

            var bridgeGo = new GameObject("IntroCommandBridge");
            bridgeGo.transform.SetParent(instance.transform, false);
            bridgeGo.AddComponent<IntroCommandBridge>().Install(runner);

            return runner;
        }

        private static bool NodeExists(DialogueRunner runner, string node)
        {
            var project = runner.YarnProject;
            if (project == null) return false;
            string[] names = project.NodeNames;
            if (names == null) return false;
            foreach (string n in names)
                if (n == node) return true;
            return false;
        }
    }
}
