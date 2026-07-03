// =============================================================================
// BuildButtonBridge — wires the HUD "Build" button to Build Mode (DEF-264).
// -----------------------------------------------------------------------------
// PROBLEM (owner playtest 2026-06-04): Village2 was assembled component-by-
// component (B0–B5) and never got the BuildMode systems wired into the scene.
// The HUD Build button raises VillageHudController.BuildRequested (a UnityEvent
// in DeNelle.HUD), but nothing in Village2 listens — so tapping Build does
// nothing. Input is fine; the listener was simply never attached (DEF-171's
// BuildModeController.EnsureExists() self-creates grid/ghost/palette — it just
// was never triggered).
//
// FIX (global, code-only, no scene edit/bake — mirrors EventSystemEnsurer /
// HeroControlEnsurer / UIInputModuleFix in this folder): a self-bootstrapping
// [RuntimeInitializeOnLoadMethod] ensurer that, on each village scene load, finds
// the VillageHudController and subscribes its BuildRequested event to
// BuildModeController.EnsureExists().Toggle().
//
// CROSS-ASMDEF (CLAUDE.md §5): DeNelle.Village must NOT reference DeNelle.HUD
// (Village → Core only). VillageHudController + its public BuildRequested
// UnityEvent live in DeNelle.HUD and are NOT on the IVillageHud interface
// (CoreServices.Hud), so this bridge reaches them via the established reflection
// pattern: find the controller by type name, read the public BuildRequested
// UnityEvent field, and AddListener a UnityAction that toggles build mode.
// UnityEvent/UnityAction are in UnityEngine (which Village references), so the
// listener itself is a normal typed delegate — only the field lookup is reflected.
//
// Idempotent: tracks the subscribed event per scene load so a re-load (or a second
// bootstrap) never double-subscribes; the toggle would otherwise fire twice and
// cancel itself. The move-joystick exclusion zone is already handled inside
// BuildModeController (PlaceConfirmedThisFrame / VirtualJoystick.IsInZone).
// =============================================================================

using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    /// <summary>
    /// Self-bootstrapping bridge that wires the HUD Build button
    /// (VillageHudController.BuildRequested, in DeNelle.HUD) to
    /// <see cref="BuildModeController"/> on every village scene load. Code-only,
    /// no scene edit — mirrors EventSystemEnsurer / HeroControlEnsurer. Reaches the
    /// HUD by reflection because Village cannot reference HUD (CLAUDE.md §5).
    /// </summary>
    public static class BuildButtonBridge
    {
        // The UnityEvent we last subscribed to + the action we registered, so we can
        // detach on a scene change and never double-subscribe (idempotent).
        private static UnityEvent s_subscribedEvent;
        private static UnityAction s_listener;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            TryWire();
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Castle-hub fix: the HUD Build button lives wherever VillageHudController
            // is — Village2 AND MainCastle_Hall — so the old "Village"-only scene-name
            // filter wrongly skipped wiring in the castle hub (Build fired nothing).
            // TryWire() is null-guarded (no-ops when no HUD) and idempotent (compares
            // s_subscribedEvent by reference + detaches the old listener on a HUD
            // reload), so calling it unconditionally is safe and won't double-subscribe.
            TryWire();
        }

        /// <summary>
        /// Find the live VillageHudController and subscribe its BuildRequested event
        /// to the build-mode toggle. Re-findable + idempotent: if we're already
        /// subscribed to this scene's HUD event, it's a no-op.
        /// </summary>
        private static void TryWire()
        {
            // Find the HUD controller by type name (it's in DeNelle.HUD, which Village
            // can't reference). It's a MonoBehaviour, so FindObjectsByType-style search
            // via the resolved Type.
            System.Type hudType = ResolveHudType();
            if (hudType == null) return;

            var hud = Object.FindAnyObjectByType(hudType) as Component;
            if (hud == null) return;   // not a village scene / HUD not up yet

            // The public BuildRequested field is a UnityEngine.Events.UnityEvent.
            FieldInfo field = hudType.GetField("BuildRequested",
                BindingFlags.Public | BindingFlags.Instance);
            if (field == null)
            {
                Debug.LogWarning("[BuildButtonBridge] VillageHudController has no public 'BuildRequested' field — Build button not wired.");
                return;
            }

            var evt = field.GetValue(hud) as UnityEvent;
            if (evt == null) return;   // event not constructed yet (HUD pre-Awake)

            // Already wired to THIS event? Idempotent no-op (avoids a double-toggle on
            // a re-load / second bootstrap, which would cancel itself out).
            if (ReferenceEquals(evt, s_subscribedEvent)) return;

            // Detach from any previous scene's event before re-subscribing.
            if (s_subscribedEvent != null && s_listener != null)
                s_subscribedEvent.RemoveListener(s_listener);

            s_listener = OnBuildRequested;
            evt.AddListener(s_listener);
            s_subscribedEvent = evt;

            Debug.Log("[BuildButtonBridge] Wired HUD Build button → BuildModeController.Toggle().");
        }

        private static void OnBuildRequested()
        {
            // DEF-171's controller self-creates grid/ghost/palette on first use.
            BuildModeController.EnsureExists()?.Toggle();
        }

        // Resolve the DeNelle.HUD VillageHudController type across loaded assemblies
        // (Village has no compile-time reference to DeNelle.HUD). Cached after first hit.
        private static System.Type s_hudType;
        private static System.Type ResolveHudType()
        {
            if (s_hudType != null) return s_hudType;
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType("DeNelle.HUD.VillageHudController", false);
                if (t != null) { s_hudType = t; break; }
            }
            return s_hudType;
        }
    }
}
