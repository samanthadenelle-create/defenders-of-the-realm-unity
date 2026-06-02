// =============================================================================
// RampartLiftInstaller — spawns the Elden Ring-style rampart lifts at runtime so
// the hero can reach the wall-top deck without the unclimbable stairs (task #8).
// -----------------------------------------------------------------------------
// Self-bootstrapping DDOL singleton (mirrors HeroControlEnsurer / TribeManager):
// no scene edit, no village regen, no bake. Builds a stone slab + LiftPlatform at
// each rampart access point when the Village scene loads.
//
// Coordinates are read from the village builder (VillageSceneBuilder.Fortify.cs):
//   • deck top (walkable surface)  = 5.4   (deckTopY)
//   • N/S walk-lane Z              = ±31.1 (laneZ — where the old ramps landed)
//   • placed at X = -10 (interior of the wall, lands on the deck lane)
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    /// <summary>Runtime-spawns the rampart lifts (no scene edit / bake).</summary>
    public sealed class RampartLiftInstaller : MonoBehaviour
    {
        public static RampartLiftInstaller Instance { get; private set; }
        private const string TargetScene = "Village";

        // Rampart geometry (VillageSceneBuilder.Fortify.cs).
        private const float DeckTopY   = 5.4f;
        private const float GroundY    = 0f;
        private const float LaneZ      = 31.1f;
        private const float AccessX    = -10f;
        private const float Footprint  = 3.2f;

        private bool _built;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("RampartLiftInstaller").AddComponent<RampartLiftInstaller>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            if (SceneManager.GetActiveScene().name == TargetScene) BuildLifts();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == TargetScene) BuildLifts();
        }

        private void BuildLifts()
        {
            if (_built) return;
            _built = true;

            // North + South access — same points the old ramps served.
            SpawnLift("RampartLift (North)", new Vector3(AccessX, GroundY,  LaneZ));
            SpawnLift("RampartLift (South)", new Vector3(AccessX, GroundY, -LaneZ));
        }

        private static void SpawnLift(string name, Vector3 groundPos)
        {
            // Root transform.y == the platform's top-surface level (LiftPlatform contract).
            var root = new GameObject(name);
            root.transform.position = groundPos;

            // Visual slab: a cube offset DOWN by half its thickness so its top face sits
            // on the surface. Keep its solid BoxCollider so the hero stands on it.
            const float thickness = 0.3f;
            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = "LiftDeck";
            slab.transform.SetParent(root.transform, false);
            slab.transform.localPosition = new Vector3(0f, -thickness * 0.5f, 0f);
            slab.transform.localScale = new Vector3(Footprint, thickness, Footprint);
            TintSlab(slab);

            var lift = root.AddComponent<LiftPlatform>();
            lift.Configure(GroundY, DeckTopY, Footprint);
        }

        // Runed blue-grey stone so the lift reads as an interactable, not floor scenery.
        private static void TintSlab(GameObject slab)
        {
            var mr = slab.GetComponent<Renderer>();
            if (mr == null) return;
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (sh == null) return;
            var m = new Material(sh);
            var stone = new Color(0.34f, 0.42f, 0.58f);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", stone); else m.color = stone;
            // Faint glow rim so it catches the eye.
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", new Color(0.18f, 0.30f, 0.55f));
            }
            mr.sharedMaterial = m;
        }
    }
}
