// =============================================================================
// SafeZoneRecovery -- full HP + MP restore on entering a SAFE ZONE (Castle/Town/Base).
// -----------------------------------------------------------------------------
// Owner brief (2026-06-29, SURVIVAL RULE): Health AND Mana do NOT auto-restore
// after combat (gated by FeatureFlags.NoAutoHeal). In the field the hero relies on
// crafted potions. Full passive recovery happens ONLY at a SAFE ZONE -- the home
// hub scenes (Castle/Town/Base) enumerated by DeNelle.Core.HubScenes.IsHub
// (MainCastle_Hall, Village2, CastleHub, CastleHub_MainKeep).
//
// WHAT IT DOES (on every scene load where HubScenes.IsHub(scene.name) is true):
//   - HeroHealth.Instance.RestoreToFull()   -> full HP (clears a downed latch too)
//   - HeroAbilities.RestoreManaToFull()     -> full MP
// This runs REGARDLESS of ff.noautoheal -- safe zones ALWAYS fully heal; that is
// the design (the flag gates the POST-COMBAT field auto-heal, not this).
//
// WHY A SELF-BOOTSTRAPPING DDOL SINGLETON (not a scene edit) -- mirrors
// HubAmbientVfxInjector: re-saving a .unity carries the project's scene-resave
// corruption risk (CLAUDE.md §3 "NEVER hand-edit"). This finds the hero at runtime
// via its singleton / component, so it never touches a scene file.
//
// Village -> Core only (FeatureFlags / HubScenes / FlowTrace / Guard). No
// cross-asmdef ref, no reflection. Null-safe + Guard-wrapped throughout. ASCII only.
// =============================================================================

using DeNelle.Core;
using DeNelle.Core.Diagnostics;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    /// <summary>Restores the hero to full HP + MP on entering a safe-zone (hub) scene.</summary>
    public sealed class SafeZoneRecovery : MonoBehaviour
    {
        public static SafeZoneRecovery Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject(nameof(SafeZoneRecovery)).AddComponent<SafeZoneRecovery>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;

            // The hero may already be present in the scene that booted us (e.g. starting in
            // MainCastle_Hall) -- recover immediately so a fresh boot into a safe zone tops off.
            if (HubScenes.IsHub(SceneManager.GetActiveScene().name))
                Recover(SceneManager.GetActiveScene().name);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (HubScenes.IsHub(scene.name))
                Recover(scene.name);
        }

        /// <summary>Full HP + MP restore. Null-safe; logs each leg; never throws into the load.</summary>
        private void Recover(string sceneName)
        {
            Guard.Try("SafeZone", "full HP+MP recovery", () =>
            {
                HeroHealth.Instance?.RestoreToFull();

                var abilities = Object.FindAnyObjectByType<HeroAbilities>();
                abilities?.RestoreManaToFull();

                FlowTrace.Step("SafeZone",
                    $"SAFE-ZONE full recovery ({sceneName}): hero HP+MP restored to full.");
            });
        }
    }
}
