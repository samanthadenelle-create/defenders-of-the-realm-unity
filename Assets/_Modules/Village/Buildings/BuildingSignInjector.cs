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
using DeNelle.Core.Diagnostics;

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
            using var _ = FlowTrace.Enter("BuildingSign", "Inject (sign every Building)");

            var buildings = FindObjectsByType<Building>(FindObjectsSortMode.None);
            if (buildings == null || buildings.Length == 0)
            {
                FlowTrace.Warn("BuildingSign", "Inject: found 0 Building(s) to sign — none signed this pass.");
                return;
            }

            int signed = 0, skippedNoLabel = 0;
            // Guard EACH building independently: one bad object (a thrown AddComponent/Configure)
            // is logged + skipped, never aborts signing the rest — a half-signed village would
            // read as "some buildings have no nameplate".
            Guard.TryEach("BuildingSign", "sign building", buildings, b =>
            {
                if (b == null) return;
                if (b.GetComponentInChildren<BuildingSign>() != null) return;   // already signed
                string label = b.DisplayLabel;
                if (string.IsNullOrEmpty(label)) { skippedNoLabel++; return; }
                var sign = b.gameObject.AddComponent<BuildingSign>();
                if (sign == null)
                {
                    FlowTrace.Fail("BuildingSign", $"Inject: AddComponent<BuildingSign> returned null on '{b.name}' — no nameplate.");
                    return;
                }
                sign.Configure(label);
                signed++;
            });

            FlowTrace.Step("BuildingSign",
                $"Inject: signed {signed} building(s) ({buildings.Length} found, {skippedNoLabel} had no DisplayLabel).");
        }
    }
}
