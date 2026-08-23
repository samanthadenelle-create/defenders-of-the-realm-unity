// =============================================================================
// JewelerStationInjector — runtime, NON-DESTRUCTIVE placement of the Jeweler's Bench
// jewelry-crafting station in the castle hub. Opens PanelId.JewelerCrafting.
// -----------------------------------------------------------------------------
// Mirrors CraftingStationInjector (the Apothecary station) EXACTLY: a self-bootstrapping
// DDOL singleton that spawns ONE "Jeweler's Bench" station at load WITHOUT touching any
// .unity file or CastleHubBuilder (scene-resave corruption risk, CLAUDE.md §3; canonical
// placement is owner-deferred). Idempotent per load, gated to the castle/home hub.
//
// WHAT IT BUILDS:
//   A station GameObject at a tweakable courtyard const, with a Building
//   (Type=JewelersBench, BuildingId="jewelers-bench", DisplayLabel="Jeweler") + a
//   BuildingInteractable (the same proximity prompt the other buildings use). Walking up
//   + interacting opens PanelId.JewelerCrafting DIRECTLY — JewelersBench returns null from
//   StructureHookIdFor, so Interact() falls through the Yarn path to TryPanelFor ->
//   JewelerCrafting (no Yarn detour), exactly like the Apothecary.
//
// VISUAL: reuses VisualFactory.Skin with a candidate jeweler/store model; on a pack-missing
//   clone it Guards down to a simple placeholder cube (Debug.LogWarning, never error — §4).
//
// Village -> Core only; code-spawn only, no scene hand-edit. Null-guarded throughout.
// =============================================================================

using DeNelle.Core;
using DeNelle.Core.Diagnostics;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    /// <summary>Runtime, non-destructive placement of the Jeweler's Bench jewelry-crafting
    /// station in the castle hub. Walk up + interact -> PanelId.JewelerCrafting.</summary>
    public sealed class JewelerStationInjector : MonoBehaviour
    {
        public static JewelerStationInjector Instance { get; private set; }

        /// <summary>Stable id — kept distinct from the "jeweler" vendor SHOP so the bench
        /// returns null from StructureHookIdFor and opens its panel directly.</summary>
        private const string StationId = "jewelers-bench";
        private const string StationLabel = "Jeweler";

        private const string HolderName = "JewelersBenchStation (runtime)";

        // TWEAK: courtyard placement for the Jeweler's Bench, in MainCastle_Hall world space.
        // Mirror of the Apothecary (which sits at +11 east); this is the free spot to the WEST
        // of the courtyard, snapped to the navmesh below so it's reachable. Move freely — the
        // canonical placement is owner-deferred.
        private static readonly Vector3 StationPos = new Vector3(-11f, 0f, 2f);

        // Candidate Resources structure models (same source as the castle storefronts).
        // First that loads wins; all-miss -> cube.
        private static readonly string[] CandidateModels =
        {
            "Structures/jeweler",
            "Structures/store",
            "Structures/Forge",
            "Structures/lumbermill",
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("JewelerStationInjector").AddComponent<JewelerStationInjector>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;

            if (HubScenes.IsHub(SceneManager.GetActiveScene().name)) Inject();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (HubScenes.IsHub(scene.name)) Inject();
        }

        private void Inject()
        {
            using var _ = FlowTrace.Enter("Crafting", "JewelerStationInjector.Inject");

            // WO-673 L3 STANDDOWN, tightened by WO-703 / BLANK-1 (owner ruling 2026-07-13,
            // supersedes the "never lost" carve-out): once the persisted marker is set the
            // injector stands down UNCONDITIONALLY — a fresh start is the tree, the well and
            // the walls (gates included), nothing else, so a marker-set save with no
            // jewelers-bench record shows no bench (and no vendor NPC — the vendor anchor
            // poll finds no Building). A save that carries a record replays it through
            // BaseLayoutLoader (ONE owner per concern, docs/WO673_ARCHITECTURE_REVIEW.md §3).
            if (StrategicPlacementMigration.StanddownActiveForStation(StationId))
            {
                FlowTrace.Step("Placement",
                    $"standdown {HolderName} ('{StationId}' — WO-703/BLANK-1 ruling: marker set, injector skips spawn; " +
                    "a BaseLayout record, if any, replays via BaseLayoutLoader).");
                return;
            }

            // Idempotent: if a jeweler's bench already exists, do NOT spawn a second one.
            if (AlreadyPresent())
            {
                FlowTrace.Step("Crafting", "jeweler's bench station already present — no-op (idempotent).");
                return;
            }

            // Snap the const to the navmesh so the station is reachable / seated on the floor.
            Vector3 pos = StationPos;
            if (NavMesh.SamplePosition(pos, out var hit, 12f, NavMesh.AllAreas))
                pos = hit.position;

            var holder = new GameObject(HolderName);
            holder.transform.position = pos;

            // VISUAL — reuse the castle's Resources structure-model loader; Guard down to a
            // placeholder cube on a pack-missing clone (LogWarning, never error — §4).
            AttachVisual(holder);

            // BEHAVIOUR — Building first (BuildingInteractable RequireComponent<Building>), then
            // the proximity-prompt interactable.
            Building building = null;
            Guard.Try("Crafting", "add+configure Building (jeweler's bench)", () =>
            {
                building = holder.AddComponent<Building>();
                building.Configure(BuildingType.JewelersBench, StationId, StationLabel);
            });
            if (building == null)
            {
                FlowTrace.Fail("Crafting",
                    "failed to add Building to jeweler's bench — destroying holder (no half-built station).");
                Destroy(holder);
                return;
            }

            Guard.Try("Crafting", "add BuildingInteractable (jeweler's bench)",
                () => holder.AddComponent<BuildingInteractable>());

            FlowTrace.Step("Crafting",
                $"spawned Jeweler's Bench station at {pos} (id='{StationId}') -> opens PanelId.JewelerCrafting on interact.");
            Debug.Log($"[JewelerStationInjector] spawned Jeweler's Bench station at {pos} " +
                      "(walk up + interact -> jewelry crafting panel).");
        }

        /// <summary>True if a jeweler's bench station already exists in-scene — a prior runtime holder
        /// OR any Building of type JewelersBench / id 'jewelers-bench'. Keeps Inject idempotent.
        /// Does NOT match the generic 'jeweler' vendor shop storefront.</summary>
        private static bool AlreadyPresent()
        {
            if (GameObject.Find(HolderName) != null) return true;
            foreach (var b in FindObjectsByType<Building>())
            {
                if (b == null) continue;
                if (b.Type == BuildingType.JewelersBench) return true;
                string id = (b.BuildingId ?? "").ToLowerInvariant();
                if (id.Contains("jewelers-bench")) return true;
            }
            return false;
        }

        // ── Visual (reuse VisualFactory.Skin; placeholder cube fallback) ──────
        private static void AttachVisual(GameObject holder)
        {
            GameObject visual = null;
            foreach (var path in CandidateModels)
            {
                // Tripo FBX exports import lying on their side at identity — apply the SAME upright
                // correction every other hub structure gets (pitch +90 stand up + yaw 90 face plaza).
                //
                // ⛔ PITCH WAS -90 HERE AND IT SHIPPED THE BENCH UPSIDE DOWN (fixed 2026-08-22).
                // The comment claimed it was matching "every other hub structure", but every row in
                // CastleHubBuilder.OwnerUprightSkins is +90 — so the comment was wrong and the code
                // followed the comment. Render-proven, from a capture taken at BOTH signs off one
                // camera: docs/proof/2026-08-20-overnight-jeweler-and-offline/jeweler-PLUS90-upright.png
                // vs jeweler-MINUS90-inverted.png. The filenames say it outright.
                //
                // ⚠ WHY THIS SURVIVED SO LONG: +90 and -90 are AABB-IDENTICAL. Bounds, height,
                // footprint and every gate in this repo read the same for both, so nothing except a
                // rendered frame can tell them apart. It also hid behind a NAME COLLISION — this is
                // the "JewelersBenchStation (runtime)" at (-11, 0.08, 2), a DIFFERENT object from
                // "Jeweler_Gems_Storefront" at (18.35, 0, -35.20), which HubStructureVisualInjector
                // owns and which was already correct. Both log as 'jeweler'. Hours went into fixing
                // the storefront while the bench was the one on screen. If a jeweler ever looks wrong
                // again, FIRST establish WHICH of the two you are looking at — the log line
                // "[Flow:Vendor] ... anchored to '<name>' ... @ (x,y,z)" names it.
                // SeatOnGround (set by SkinOptions.Structure) lands the bounds-base on the holder y;
                // bake the 0.7 size into the FIT (6 * 0.7 = 4.2) so the seat measures the final size.
                // Owner 2026-07-03 ("the jeweler is too large, scale ... down 50%"): halve the fit
                // target (4.2m -> 2.1m). This is the UNIFORM sizeM fit, so VisualFactory.Skin's
                // SeatOnGround runs AFTER the fit and re-seats the smaller bounds base onto the
                // holder's y — the bench stays seated on the ground at the new size (no floating).
                var opts = SkinOptions.Structure(6f * 0.35f);
                opts.LocalRotation = Quaternion.Euler(90f, 90f, 0f);
                visual = Guard.Try("Crafting", $"skin jeweler's bench visual '{path}'",
                    () => VisualFactory.Skin(holder.transform, path, opts),
                    fallback: null);
                if (visual != null)
                {
                    FlowTrace.Step("Crafting", $"jeweler's bench visual resolved from '{path}' (upright -90/+90, fit 2.1m [50% scale-down], seated on ground).");
                    return;
                }
            }

            // Pack-missing clone: keep the station VISIBLE + interactable with a placeholder cube.
            FlowTrace.Warn("Crafting",
                "no jeweler structure model resolved (Resources/Structures pack may be absent) — placeholder cube used.");
            Debug.LogWarning("[JewelerStationInjector] no jeweler model found — placeholder cube used.");
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "JewelersBench_Placeholder";
            cube.transform.SetParent(holder.transform, false);
            cube.transform.localPosition = Vector3.up * 1f;
            cube.transform.localScale = new Vector3(2f, 2f, 2f);
            var col = cube.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
            // WO-580 (owner F8 "stray white bar/box in front of me, nothing there"): a raw
            // GameObject.CreatePrimitive leaves the renderer on Unity's DEFAULT material,
            // which renders FLAT WHITE under URP — the stray white box on a lean / pack-missing
            // build. Tint to neutral stone so it reads as a placeholder, never a white bar.
            TintPlaceholderStone(cube, "JewelersBench_Placeholder");
        }

        // WO-580: give a fallback placeholder primitive a neutral stone material so the bare
        // CreatePrimitive default-white material never shows as a stray white box/bar in the
        // hub. URP/Lit (Standard fallback for non-URP editor); null-safe; FlowTrace-proven.
        private static void TintPlaceholderStone(GameObject go, string label)
        {
            if (go == null) return;
            var r = go.GetComponent<MeshRenderer>();
            if (r == null) return;
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (sh == null) return;
            var mat = new Material(sh) { name = "PlaceholderStone (runtime)" };
            var stone = new Color(0.32f, 0.30f, 0.28f, 1f); // neutral warm stone — not white
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", stone);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", stone);
            r.sharedMaterial = mat;
            FlowTrace.Step("Crafting",
                "WO-580: tinted '" + label + "' placeholder cube to neutral stone " +
                "(was default-white CreatePrimitive material → stray white box).");
        }
    }
}
