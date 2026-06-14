// =============================================================================
// EventSystemEnsurer — guarantees every loaded scene has a working EventSystem.
// -----------------------------------------------------------------------------
// BUG (owner playtests, DEF-247 / DEF-260): scenes built without an EventSystem
// (or whose baked EventSystem was stripped) spam the console with
//   "Failed to RaycastGui because your scene doesn't have an event system!
//    To add one, go to: GameObject/UI/EventSystem."
// and — more importantly — deliver NO pointer/click events to UI Toolkit panels
// or uGUI, so every on-screen button (ATTACK, ability slots, pet toggles, menus)
// is dead on tap. The Defend-the-Tower (PatriciaLight) scene hit this even though
// its scene builder bakes an EventSystem, because a stale (un-rebuilt) scene or a
// strip pass can leave the saved scene without one.
//
// FIX (global, code-only, no scene edits): a single [RuntimeInitializeOnLoadMethod]
// ensurer that — on first load AND on every subsequent sceneLoaded — checks whether
// the active scene set has ANY EventSystem and, if not, creates one carrying an
// InputSystemUIInputModule (this project drives input via the Input System package,
// so the UI module must match — a legacy StandaloneInputModule would throw because
// the old Input Manager is disabled). This covers EVERY scene at once, so it fixes
// DEF-247 project-wide, not just the DTT scene.
//
// Pairs with UIInputModuleFix (same folder): this ENSURES the EventSystem + module
// exist; UIInputModuleFix then repairs the module's actions asset when the package
// default fails to load in a build. Order-independent — both run AfterSceneLoad and
// on sceneLoaded, and each is idempotent.
// =============================================================================

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    /// <summary>
    /// Adds an <see cref="EventSystem"/> (+ <see cref="InputSystemUIInputModule"/>)
    /// to any loaded scene that lacks one, so UI clicks fire and the
    /// "scene doesn't have an event system" RaycastGui error stops. Global —
    /// applies to every scene (DEF-247), runs on load and on each scene change.
    /// </summary>
    public static class EventSystemEnsurer
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            EnsureEventSystem();
            SceneManager.sceneLoaded += (_, __) => EnsureEventSystem();

            // Scene-load-only recovery CANNOT heal a MID-SCENE EventSystem loss — which is
            // exactly what strands the UI after a Yarn dialogue ENDS (its teardown leaves
            // EventSystem.current == null, so EVERY click + hover dies until the next scene
            // load: the "dev tools / settings dead after dialogue" wedge, S1 2026-06-14,
            // proven by the presenter's own "EventSystem.current is NULL" trace). A tiny
            // persistent watchdog re-heals every frame (near-free: it early-returns the
            // instant a live EventSystem exists).
            var watchdog = new GameObject("EventSystemWatchdog (auto)") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(watchdog);
            watchdog.AddComponent<EventSystemWatchdog>();
        }

        /// <summary>Re-establish a working EventSystem if the active one was lost mid-scene.
        /// Re-enables a disabled/inactive one (preserving its configured input module +
        /// actions) when possible; otherwise creates a fresh one. No-op when one is live.</summary>
        public static void Heal()
        {
            if (EventSystem.current != null) return;
            var existing = Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include);
            if (existing != null)
            {
                // Present but inactive/disabled (the post-dialogue case): revive in place so
                // its already-configured InputSystemUIInputModule + actions are preserved.
                if (!existing.gameObject.activeSelf) existing.gameObject.SetActive(true);
                if (!existing.enabled) existing.enabled = true;
                return;
            }
            EnsureEventSystem();   // truly none present — create one
        }

        public static void EnsureEventSystem()
        {
            // Already present anywhere in the loaded scenes? Nothing to do.
            if (EventSystem.current != null) return;
            if (Object.FindAnyObjectByType<EventSystem>(FindObjectsInactive.Include) != null) return;

            var go = new GameObject("EventSystem (auto)");
            go.AddComponent<EventSystem>();
            // This project uses the Input System package; pair the EventSystem with
            // the matching UI module (NOT the legacy StandaloneInputModule, which
            // throws when the old Input Manager is disabled). UIInputModuleFix will
            // repair its actions asset if the package default fails to load in a build.
            go.AddComponent<InputSystemUIInputModule>();

            Debug.Log("[EventSystemEnsurer] Scene had no EventSystem — added one " +
                      "(+ InputSystemUIInputModule) so UI clicks route and the " +
                      "RaycastGui error stops.");
        }
    }

    /// <summary>Persistent per-frame heartbeat that self-heals a lost EventSystem (e.g. a
    /// Yarn dialogue teardown stranding it mid-scene) within one frame, so UI clicks + hover
    /// never stay dead. Created DontDestroyOnLoad + hidden by EventSystemEnsurer.Init.</summary>
    internal sealed class EventSystemWatchdog : MonoBehaviour
    {
        private void Update() => EventSystemEnsurer.Heal();
    }
}
