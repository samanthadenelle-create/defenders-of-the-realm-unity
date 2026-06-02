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
        // Owner 2026-06-02 ("walked all around inside and didn't see a lift"): the
        // 3.2 m slab tucked at the wall base was too easy to miss. Widen it and add a
        // tall glowing beacon (below) so it reads as an interactable from the plaza.
        private const float Footprint  = 5f;

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

            // Tall glowing beacon so the lift is visible from across the village
            // (owner couldn't find it). A slim translucent pillar rising past the
            // deck height, emissive blue — reads as "stand here to ride up".
            var beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beacon.name = "LiftBeacon";
            if (beacon.TryGetComponent(out Collider bc)) Destroy(bc);   // never blocks the hero
            beacon.transform.SetParent(root.transform, false);
            // Cylinder is 2 m tall at scale 1 → scale Y so it spans ground→well above the deck.
            float beaconH = DeckTopY + 3f;
            beacon.transform.localPosition = new Vector3(0f, beaconH * 0.5f, 0f);
            beacon.transform.localScale = new Vector3(0.5f, beaconH * 0.5f, 0.5f);
            TintBeacon(beacon);

            var lift = root.AddComponent<LiftPlatform>();
            lift.Configure(GroundY, DeckTopY, Footprint);
        }

        // Translucent emissive blue pillar — a "ride up here" landmark visible from afar.
        private static void TintBeacon(GameObject beacon)
        {
            var mr = beacon.GetComponent<Renderer>();
            if (mr == null) return;
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (sh == null) return;
            var m = new Material(sh);
            var glow = new Color(0.35f, 0.65f, 1f);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(glow.r, glow.g, glow.b, 0.55f));
            else m.color = glow;
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", glow * 1.6f);
            }
            // Transparent so it reads as a light shaft, not a solid post.
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
            if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
            m.renderQueue = 3000;
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mr.sharedMaterial = m;
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
