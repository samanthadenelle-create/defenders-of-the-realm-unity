// =============================================================================
// CastleSpawnMarkerHider — runtime, NON-DESTRUCTIVE guard that hides the visible
// placeholder CAPSULE ("pill") promoted to the hero spawn marker in the Central
// Castle Hub (MainCastle_Hall).
// -----------------------------------------------------------------------------
// WHY this exists:
//   CastleHubBuilder promotes the owner's placeholder Capsule to the canonical
//   spawn marker "HeroStartPoint_PlayerSpawn" and (at bake time) strips its mesh.
//   But the marker is a baked SCENE OBJECT — if a build was made from a scene
//   state where the MeshRenderer was left enabled (or only the renderer, not the
//   MeshFilter, was removed), a raw white capsule shows up beside the hero spawn
//   in the live build. Re-saving MainCastle_Hall.unity by hand to fix it carries
//   the project's scene-resave corruption risk (CLAUDE.md §3).
//
//   So, mirroring CastleSpawnPointInjector / CastleVendorNpcInjector exactly, this
//   self-bootstrapping DDOL singleton FINDS the spawn marker on every
//   MainCastle_Hall load and disables its renderer — WITHOUT touching the .unity
//   file. The marker's TRANSFORM (the spawn point) and any function are preserved;
//   only the visible mesh is hidden. Idempotent per load.
//
// Village -> Core only. No reflection, no cross-asmdef ref.
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    /// <summary>Hides the placeholder capsule mesh on the castle hero-spawn marker at runtime.</summary>
    public sealed class CastleSpawnMarkerHider : MonoBehaviour
    {
        public static CastleSpawnMarkerHider Instance { get; private set; }

        private const string TargetScene = "MainCastle_Hall";

        // Names CastleHubBuilder uses for the promoted spawn marker(s). The first is
        // the canonical castle spawn pill; the second is the personal-quarters anchor.
        private static readonly string[] MarkerNames =
        {
            "HeroStartPoint_PlayerSpawn",
            "HeroStartPoint_InsidePersonalQuarters",
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("CastleSpawnMarkerHider").AddComponent<CastleSpawnMarkerHider>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            if (SceneManager.GetActiveScene().name == TargetScene) Hide();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == TargetScene) Hide();
        }

        // Disable the renderer on each spawn marker so the placeholder capsule is
        // invisible. We do NOT destroy the GameObject or move its transform — the
        // spawn point keeps working; only the visible pill is hidden.
        private void Hide()
        {
            foreach (var name in MarkerNames)
            {
                var marker = GameObject.Find(name);
                if (marker == null) continue;

                var r = marker.GetComponent<Renderer>();
                if (r != null && r.enabled)
                {
                    r.enabled = false;
                    Debug.Log($"[CastleSpawnMarkerHider] hid placeholder capsule on '{name}' (marker transform kept).");
                }
            }
        }
    }
}
