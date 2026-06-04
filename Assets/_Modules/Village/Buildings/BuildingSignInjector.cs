// =============================================================================
// BuildingSignInjector — runtime-attaches a BuildingSign nameplate to every
// placed Building, so each reads as "Farm" / "Forge" / "Arcane Tower" / etc.
// -----------------------------------------------------------------------------
// Owner 2026-06-03: "a small sign at each one that calls out farm sawmill armorer
// or whatever they are." Runtime injector (same idiom as RampartLiftInstaller /
// StoryCompanionInjector) — NO scene edit, NO bake. Finds every Building in the
// Village scene and gives it a BuildingSign configured with its DisplayLabel.
// Idempotent + self-bootstrapping DDOL.
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    /// <summary>Attaches a world-space nameplate to every placed building (no bake).</summary>
    public sealed class BuildingSignInjector : MonoBehaviour
    {
        public static BuildingSignInjector Instance { get; private set; }
        private const string TargetScene = "Village2";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("BuildingSignInjector").AddComponent<BuildingSignInjector>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            if (SceneManager.GetActiveScene().name == TargetScene) Inject();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == TargetScene) Inject();
        }

        private static void Inject()
        {
            var buildings = FindObjectsByType<Building>(FindObjectsSortMode.None);
            foreach (var b in buildings)
            {
                if (b == null) continue;
                if (b.GetComponentInChildren<BuildingSign>() != null) continue;   // already signed
                string label = b.DisplayLabel;
                if (string.IsNullOrEmpty(label)) continue;
                var sign = b.gameObject.AddComponent<BuildingSign>();
                sign?.Configure(label);
            }
        }
    }
}
