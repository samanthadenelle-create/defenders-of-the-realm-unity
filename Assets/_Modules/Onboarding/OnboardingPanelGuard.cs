// =============================================================================
// OnboardingPanelGuard — stops the onboarding UI Toolkit panel from eating clicks
// in gameplay scenes (the "dev tools / UI dead after Yarn" root cause).
// -----------------------------------------------------------------------------
// ROOT CAUSE (proven 2026-06-13 by a pointer-interceptor dump, not re-derived
// here): in MainCastle_Hall the uGUI EventSystem.RaycastAll returned exactly ONE
// hit — the UI Toolkit PanelRaycaster proxy that UITK auto-registers for the
// "OnboardingPanelSettings" PanelSettings — and it was the FIRST/topmost, so it
// consumed every click before the dev menu (AdminOverlay, sortingOrder 2710) or
// any other gameplay UI could receive it. The onboarding panel was active,
// full-screen and pickingMode=Position in a scene where onboarding is over.
//
// HOW IT SURVIVES: the onboarding screens (Title / HeroSelect / PetSelect) all
// share ONE OnboardingPanelSettings asset, and several runtime UIDocuments adopt
// it opportunistically (e.g. IntroCommandBridge.EnsureFade borrows whatever
// PanelSettings the first live UIDocument exposes; the Yarn intro runs in the
// Title scene where that first doc is the onboarding panel). Whatever the exact
// adoption path, the symptom is the same: a UIDocument backed by
// OnboardingPanelSettings ends up live + pickable in a gameplay scene and its
// raycaster sits on top of the click stack. Rather than chase each adoption path,
// this guard enforces the INVARIANT directly:
//
//   The OnboardingPanelSettings panel may only intercept pointer input while the
//   active scene is an ONBOARDING scene (Title / HeroSelect / PetSelect). In any
//   other scene every UIDocument bound to that PanelSettings is neutralised —
//   the UIDocument is disabled and its root is collapsed + made un-pickable — so
//   it renders nothing and steals no clicks.
//
// This runs on EVERY scene load (RuntimeInitializeOnLoadMethod + sceneLoaded),
// so a panel that leaks across a transition is caught the instant the gameplay
// scene becomes active. It does NOT touch onboarding scenes, so the real
// onboarding flow (Title → HeroSelect → PetSelect) still shows the panel.
//
// Lives in DeNelle.Onboarding (refs Core for FlowTrace; the onboarding scene
// names are the SceneRouter constants in Core). UIElements is auto-referenced.
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using DeNelle.Core;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Onboarding
{
    /// <summary>
    /// Disables / un-picks any UIDocument backed by the OnboardingPanelSettings
    /// panel whenever the active scene is not an onboarding scene, so the
    /// onboarding panel cannot intercept clicks in gameplay (the dev-tools-dead-
    /// after-Yarn bug — an OnboardingPanelSettings PanelRaycaster topping the
    /// EventSystem click stack in MainCastle_Hall).
    /// </summary>
    public static class OnboardingPanelGuard
    {
        // The PanelSettings asset name created by OnboardingSceneBuilder /
        // IntroFlowSceneBuilder (Assets/_Modules/Onboarding/Generated/
        // OnboardingPanelSettings.asset). Matched by name so we never need a hard
        // asset reference (the panel is adopted by several docs by value, and the
        // guard must work even for a runtime-borrowed instance carrying this name).
        private const string OnboardingPanelName = "OnboardingPanelSettings";

        // The guard may ONLY un-do its own actions. When it neutralises a document in
        // a gameplay scene it records that document's instance id + its ORIGINAL
        // pickingMode here; on return to an onboarding scene it restores exactly those
        // docs to exactly that pickingMode. Docs the guard never disabled (e.g. the
        // intro FADE overlay, which IntroCommandBridge deliberately sets to
        // PickingMode.Ignore and which borrows OnboardingPanelSettings) are left
        // COMPLETELY ALONE. Regression fix 2026-06-13: the old restore branch forced
        // pickingMode=Position on EVERY OnboardingPanelSettings doc — including that
        // click-through fade overlay — so on the very first Title boot the fade flipped
        // to click-BLOCKING and ate the Start button. Keyed by GetInstanceID() so a
        // stale entry for a destroyed doc never dereferences a fake-null.
        private static readonly System.Collections.Generic.Dictionary<int, PickingMode>
            _disabledByGuard = new System.Collections.Generic.Dictionary<int, PickingMode>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Init()
        {
            // Sweep the scene we booted into, then keep watching every load.
            EnforceForActiveScene();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnforceForActiveScene();
        }

        /// <summary>True if the active scene is one of the onboarding screens
        /// where the OnboardingPanelSettings panel is legitimately the live UI.</summary>
        private static bool IsOnboardingScene(string sceneName)
        {
            return sceneName == SceneRouter.Title
                || sceneName == SceneRouter.HeroSelect
                || sceneName == SceneRouter.PetSelect;
        }

        /// <summary>
        /// In a non-onboarding (gameplay) scene, neutralise every UIDocument bound
        /// to the OnboardingPanelSettings panel so it stops rendering / intercepting.
        /// A no-op while an onboarding scene is active.
        /// </summary>
        private static void EnforceForActiveScene()
        {
            string active = SceneManager.GetActiveScene().name;

            UIDocument[] docs = Object.FindObjectsByType<UIDocument>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            // Audit P1 fix (no restore on return to onboarding): returning to an
            // onboarding scene (e.g. Village -> Title via PauseController.GoTitle)
            // previously early-returned, leaving panels we had disabled in the prior
            // gameplay scene dead (display=None, pickingMode=Ignore, enabled=false) —
            // the Title UI never came back. Instead, RESTORE the OnboardingPanelSettings
            // documents here so the onboarding UI is live again.
            if (IsOnboardingScene(active))
            {
                int restored = 0;
                foreach (var doc in docs)
                {
                    if (doc == null) continue;
                    var settings = doc.panelSettings;
                    if (settings == null) continue;
                    if (settings.name != OnboardingPanelName) continue;

                    // ONLY restore docs the guard itself disabled — and ONLY to their
                    // original pickingMode. A doc we never touched (the click-through
                    // fade overlay on a fresh Title boot) is left exactly as its owner
                    // configured it, so it keeps PickingMode.Ignore and never eats the
                    // Start button. This is the boot-into-Title fix.
                    if (!_disabledByGuard.TryGetValue(doc.GetInstanceID(), out var priorPicking))
                        continue;

                    if (!doc.enabled) doc.enabled = true;

                    var root = doc.rootVisualElement;
                    if (root != null)
                    {
                        root.style.display = DisplayStyle.Flex;   // renders again
                        root.pickingMode = priorPicking;          // restore ORIGINAL picking, not a forced Position
                    }

                    _disabledByGuard.Remove(doc.GetInstanceID());
                    restored++;
                }

                if (restored > 0)
                {
                    FlowTrace.Step("UI",
                        $"OnboardingPanelGuard restored {restored} OnboardingPanelSettings " +
                        $"UIDocument(s) on return to onboarding scene '{active}' " +
                        "(re-enables Title/HeroSelect/PetSelect UI after a gameplay scene disabled it)");
                }
                return;
            }

            int neutralised = 0;
            foreach (var doc in docs)
            {
                if (doc == null) continue;
                var settings = doc.panelSettings;
                if (settings == null) continue;
                if (settings.name != OnboardingPanelName) continue;

                // Collapse + un-pick the live root first so it steals no clicks
                // even if disabling the document is deferred a frame.
                var root = doc.rootVisualElement;
                if (root != null)
                {
                    // Record the ORIGINAL pickingMode before we clobber it, so the
                    // onboarding-scene restore can put it back exactly (and so a
                    // click-through overlay is restored to Ignore, not Position).
                    if (!_disabledByGuard.ContainsKey(doc.GetInstanceID()))
                        _disabledByGuard[doc.GetInstanceID()] = root.pickingMode;

                    root.style.display = DisplayStyle.None;   // renders nothing
                    root.pickingMode = PickingMode.Ignore;    // intercepts no pointer events
                }
                else
                {
                    // No root yet but still disable the doc; record a sane default so
                    // restore re-enables it (Position is the UIElements default).
                    if (!_disabledByGuard.ContainsKey(doc.GetInstanceID()))
                        _disabledByGuard[doc.GetInstanceID()] = PickingMode.Position;
                }

                // Then fully disable the document so its panel no longer registers
                // a PanelRaycaster in the EventSystem at all — onboarding is over,
                // the panel should not exist in a gameplay scene.
                if (doc.enabled) doc.enabled = false;

                neutralised++;
            }

            if (neutralised > 0)
            {
                FlowTrace.Step("UI",
                    $"OnboardingPanelGuard neutralised {neutralised} OnboardingPanelSettings " +
                    $"UIDocument(s) in non-onboarding scene '{active}' " +
                    "(dev-tools-after-Yarn interceptor — onboarding panel no longer eats clicks)");
            }
        }
    }
}
