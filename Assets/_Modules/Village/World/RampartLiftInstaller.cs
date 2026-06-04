// =============================================================================
// RampartLiftInstaller — spawns the Elden Ring-style rampart lifts at runtime so
// the hero can reach the wall-top deck without the unclimbable stairs (task #8).
// -----------------------------------------------------------------------------
// Self-bootstrapping DDOL singleton (mirrors HeroControlEnsurer / TribeManager):
// no scene edit, no village regen, no bake. Builds a stone slab + LiftPlatform at
// each rampart access point when the Village scene loads.
//
// Coordinates are read from the village builder (VillageSceneBuilder.Fortify.cs):
//   • deck top (walkable surface)  = 5.4   (DeckTopY)
//   • N/S walk-lane Z              = ±31.1 (LaneZ — where the old ramps landed)
//   • placed at X = -10 (interior of the wall, lands on the deck lane)
//
// "STARTS UNDERGROUND" (owner 2026-06-02): GroundY was hard-coded to 0, but the
// interior floor near the wall base isn't exactly 0, so the slab buried itself.
// We now RAYCAST the real ground at each spot and seat the slab ON it. And the
// blue beacon used to be a CHILD of the moving root — it rode UP with the platform
// and stopped marking the spot — so the marker (ground ring + beacon) is now a
// SEPARATE static object that stays at ground level the whole time.
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    /// <summary>Runtime-spawns the rampart lifts (no scene edit / bake).</summary>
    public sealed class RampartLiftInstaller : MonoBehaviour
    {
        public static RampartLiftInstaller Instance { get; private set; }
        private const string TargetScene = "Village2";

        // Rampart geometry (VillageSceneBuilder.Fortify.cs).
        private const float DeckTopY   = 5.4f;
        private const float LaneZ      = 31.1f;
        private const float AccessX    = -10f;
        private const float Footprint  = 5f;
        private const float Thickness  = 0.3f;   // slab thickness

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
            if (scene.name == TargetScene) { _built = false; BuildLifts(); }
        }

        private void BuildLifts()
        {
            if (_built) return;
            _built = true;

            // North + South access — same points the old ramps served.
            SpawnLift("RampartLift (North)", new Vector3(AccessX, 0f,  LaneZ));
            SpawnLift("RampartLift (South)", new Vector3(AccessX, 0f, -LaneZ));
        }

        private static void SpawnLift(string name, Vector3 xzPos)
        {
            // Find the REAL FLOOR height at this spot so the slab seats on the ground
            // instead of burying itself (owner: "starts underground") — but the rampart
            // DECK sits directly above the lift (y≈5.4), so a naive downward ray hits the
            // DECK first and spawns the platform up top (owner: "spawns on the top level").
            // So scan ALL colliders below and take the HIGHEST one under ~2 m — that's the
            // walkable floor, never the deck. Fall back to 0 when nothing qualifies.
            float groundY = 0f;
            var hits = Physics.RaycastAll(new Vector3(xzPos.x, 8f, xzPos.z),
                                          Vector3.down, 12f, ~0, QueryTriggerInteraction.Ignore);
            float bestFloor = float.NegativeInfinity;
            foreach (var h in hits)
                if (h.point.y < 2f && h.point.y > bestFloor) bestFloor = h.point.y;
            if (bestFloor > float.NegativeInfinity) groundY = bestFloor;

            // ── Static ground marker (does NOT move with the lift) ──────────────
            // A glowing floor ring + a tall beacon, anchored at ground level so the
            // spot stays marked even while the platform is up at the deck.
            var marker = new GameObject(name + " Marker");
            marker.transform.position = new Vector3(xzPos.x, groundY, xzPos.z);

            // Flat emissive ring on the floor — readable even if the slab sits low.
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "LiftRing";
            if (ring.TryGetComponent(out Collider rc)) Destroy(rc);
            ring.transform.SetParent(marker.transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.04f, 0f);   // just above the floor
            ring.transform.localScale = new Vector3(Footprint * 1.05f, 0.04f, Footprint * 1.05f);
            TintEmissive(ring, new Color(0.35f, 0.65f, 1f), 2.0f, transparent: false);

            // Tall translucent beacon rising past the deck — a "ride up here" landmark.
            var beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beacon.name = "LiftBeacon";
            if (beacon.TryGetComponent(out Collider bc)) Destroy(bc);
            beacon.transform.SetParent(marker.transform, false);
            float beaconH = (DeckTopY - groundY) + 3f;
            beacon.transform.localPosition = new Vector3(0f, beaconH * 0.5f, 0f);
            beacon.transform.localScale = new Vector3(0.5f, beaconH * 0.5f, 0.5f);
            TintEmissive(beacon, new Color(0.35f, 0.65f, 1f), 1.6f, transparent: true);

            // ── The moving lift ─────────────────────────────────────────────────
            // Root transform.y == the platform's top-surface level (LiftPlatform
            // contract). Seat the slab ON the ground: top surface a slab-thickness
            // above groundY so the whole slab is visible (bottom resting on floor).
            var root = new GameObject(name);
            float bottomSurfaceY = groundY + Thickness;
            root.transform.position = new Vector3(xzPos.x, bottomSurfaceY, xzPos.z);

            var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
            slab.name = "LiftDeck";
            slab.transform.SetParent(root.transform, false);
            slab.transform.localPosition = new Vector3(0f, -Thickness * 0.5f, 0f);
            slab.transform.localScale = new Vector3(Footprint, Thickness, Footprint);
            TintSlab(slab);

            var lift = root.AddComponent<LiftPlatform>();
            lift.Configure(bottomSurfaceY, DeckTopY, Footprint);
        }

        // Emissive tint helper — opaque (floor ring) or translucent (beacon shaft).
        private static void TintEmissive(GameObject go, Color glow, float emission, bool transparent)
        {
            var mr = go.GetComponent<Renderer>();
            if (mr == null) return;
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (sh == null) return;
            var m = new Material(sh);
            float alpha = transparent ? 0.55f : 1f;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(glow.r, glow.g, glow.b, alpha));
            else m.color = glow;
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", glow * emission);
            }
            if (transparent)
            {
                if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);
                if (m.HasProperty("_Blend")) m.SetFloat("_Blend", 0f);
                m.renderQueue = 3000;
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
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
            if (m.HasProperty("_EmissionColor"))
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", new Color(0.18f, 0.30f, 0.55f));
            }
            mr.sharedMaterial = m;
        }
    }
}
