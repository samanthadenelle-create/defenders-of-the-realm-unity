// =============================================================================
// UIInputModuleFix — makes UI Toolkit HUD clicks work in player builds.
// -----------------------------------------------------------------------------
// BUG (owner playtest 2026-05-24): every clickable HUD element (Build button,
// Q/W/E/R ability slots, the wave-countdown force-start, the "?" help/dev-tools
// button) does nothing on click in the Windows build, while keyboard gameplay
// (WASD, ability keys 1-4) works.
//
// ROOT CAUSE: the scene EventSystems' InputSystemUIInputModule reference the
// Input System PACKAGE's DefaultInputActions.inputactions (guid
// ca9f5fa95ffab41fb9a615ab714db018, which lives only in Library/PackageCache —
// NOT a project asset). That asset isn't reliably included in the player build,
// so at runtime actionsAsset fails to load -> the module delivers no pointer/
// click events to the runtime UI Toolkit panel -> all HUD clicks are dead.
// Keyboard gameplay is unaffected because it polls Keyboard.current directly,
// bypassing the EventSystem. (Same "package-internal / fresh-clone missing asset"
// class as the WO-05 material.) This affects ALL scenes' EventSystems.
//
// FIX (code-only, no curated-scene edits): on every scene load, find each
// InputSystemUIInputModule whose actionsAsset didn't load and call
// AssignDefaultActions() — which recreates the standard UI action set (Point /
// Click / Navigate / ...) in code, with no asset dependency. Runs in the build
// only where needed (in the Editor the package asset loads, so actionsAsset is
// non-null and this is a no-op).
// =============================================================================

using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    public static class UIInputModuleFix
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            EnsureActions();
            SceneManager.sceneLoaded += (_, __) => EnsureActions();
        }

        private static void EnsureActions()
        {
            var modules = Object.FindObjectsByType<InputSystemUIInputModule>(FindObjectsInactive.Include);
            foreach (var m in modules)
            {
                if (m == null) continue;
                // actionsAsset == null means the serialized (package) actions asset
                // failed to load in this build — recreate a working default set.
                if (m.actionsAsset == null)
                {
                    m.AssignDefaultActions();
                    Debug.Log("[UIInputModuleFix] InputSystemUIInputModule had no actions asset " +
                              "— assigned default UI actions so HUD clicks work.");
                }
            }
        }
    }
}
