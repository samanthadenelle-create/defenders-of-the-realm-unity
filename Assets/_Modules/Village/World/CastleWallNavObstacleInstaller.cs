// =============================================================================
// CastleWallNavObstacleInstaller (task #14, owner F8 2026-07-15: "see how im in the
// wall. we need to add walls inside with coliders so only the actual archway
// extends. I can hand edit those if you want to do offsets")
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.World
//
// WHY THIS EXISTS (the SME read, NOT a guess):
//   The overworld castle walls are the four CastleSide_* groups
//   (CastleWallsFromRecipe: Gate_South arch + Wall_South_L/R + SeamFill + DoorJamb
//   flank boxes + CornerTower, mirrored x4). Every masonry piece already carries a
//   physics collider. But the HERO IS A NavMeshAgent (HeroLocomotion) -- it moves on
//   the NavMesh and IGNORES physics colliders entirely. So a wall's MeshCollider /
//   BoxCollider does NOTHING to stop the hero. The only thing that stops a
//   NavMeshAgent is the NavMesh being CARVED (no walkable surface) where the wall
//   stands.
//
//   The castle relies PURELY on the bake to carve the walls -- and the merged
//   Main_Castle_Overworld nav floor (CourtyardFloor_Nav) is a 130x130 plane FORCED
//   walkable (NavMeshModifier overrideArea=Walkable). When the bake is stale, or the
//   forced-walkable floor wins over the wall footprint, the NavMesh stays continuous
//   ACROSS the wall and the hero walks straight into/through it -- exactly what the
//   owner felt ("im in the wall").
//
//   The village perimeter has a bake-independent backstop (WallNavObstacleInstaller,
//   which carves the "WallBarrier-*" boxes). The CASTLE walls (CastleSide_*) had NO
//   such backstop -- WallNavObstacleInstaller only matches "WallBarrier". This is the
//   castle's equivalent: at runtime it fits a CARVING NavMeshObstacle to every castle
//   wall masonry collider, cutting a hole in the LIVE NavMesh so the wall blocks the
//   agent regardless of what the bake did.
//
//   ARCHWAY STAYS PASSABLE: we SKIP every "Gate*" piece (the arch mesh) and never
//   carve the central doorway gap between the two DoorJamb flank boxes -- no masonry
//   sits there, so the opening stays walkable. Result: the wall is SOLID except at the
//   actual archway, which is the owner's ask verbatim. GateTraversalInjector still
//   warps the hero through the opening.
//
// OWNER-TWEAKABLE OFFSETS (owner: "I can hand edit those if you want to do offsets"):
//   Resources/Data/castle-wall-collider-offsets.json -- thicknessPadding, minHeight,
//   a doorway-protect half-clear, and a per-side inset. Read at scene load; hand-edit
//   without a recompile. See that file's _comment for what each knob does.
//
// SHIPS IN A CODE BUILD: self-bootstraps via RuntimeInitializeOnLoadMethod (like
//   WallNavObstacleInstaller / GateTraversalInjector) and re-scans on every
//   sceneLoaded -- NO scene-file edit, NO rebake needed to take effect. Fully guarded:
//   never throws into gameplay (an uncaught throw in sceneLoaded halts the WebGL
//   player).
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village.World
{
    /// <summary>
    /// Runtime backstop that carves the castle perimeter walls (CastleSide_* masonry)
    /// out of the live NavMesh so the NavMeshAgent hero cannot walk INTO a castle wall,
    /// while leaving the archway/gate opening walkable. Bake-independent; needs no scene
    /// edit and no rebake. Mirrors the proven WallNavObstacleInstaller pattern.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CastleWallNavObstacleInstaller : MonoBehaviour
    {
        /// <summary>Object-name prefix of the four castle wall side roots
        /// (CastleWallsFromRecipe -> "CastleSide_South", mirrored to West/North/East).</summary>
        private const string SideRootPrefix = "CastleSide_";

        /// <summary>Name substring marking the ARCHWAY (Gate_South arch mesh). Any piece whose
        /// name contains this is LEFT UNCARVED so the opening stays passable -- except pieces that
        /// are explicitly solid gate MASONRY (DoorJamb flank boxes), which must block.</summary>
        private const string ArchNameContains  = "Gate";
        private const string SolidJambContains = "DoorJamb";

        /// <summary>Holder for the generated obstacle proxies (identity transform, so each proxy's
        /// world AABB maps 1:1 to a unit-scaled box obstacle).</summary>
        private const string ObstacleHolderName = "[CastleWallObstacles]";

        private static CastleWallNavObstacleInstaller _instance;

        // Track colliders already fitted so a re-scan never double-adds.
        private readonly HashSet<int> _carved = new HashSet<int>();
        private WallColliderConfig _cfg;
        private Transform _holder;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[CastleWallNavObstacleInstaller]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<CastleWallNavObstacleInstaller>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            SceneManager.sceneLoaded += OnSceneLoaded;
            // The overworld scene is already active when AfterSceneLoad fires -- scan now.
            SafeScan();
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_instance == this) _instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => SafeScan();

        // Never throw out of a sceneLoaded handler (an uncaught throw halts the WebGL player).
        private void SafeScan()
        {
            try { ScanAndCarve(); }
            catch (Exception e)
            {
                Debug.LogWarning("[Flow:WallCollider] castle wall carve threw (non-fatal): " + e);
            }
        }

        /// <summary>
        /// Finds every castle wall masonry collider in the loaded scenes and fits a carving
        /// NavMeshObstacle to it (idempotent). Overworld-only. Public so a dev tool / test can
        /// force a re-scan after a scene is built at runtime.
        /// </summary>
        public void ScanAndCarve()
        {
            string active = SceneManager.GetActiveScene().name;
            if (!DeNelle.Core.HubScenes.IsOverworld(active)) return;   // castle walls live only on the overworld

            _cfg = _cfg ?? WallColliderConfig.Load();
            if (!_cfg.enabled)
            {
                FlowTrace.Once("WallCollider", "disabled",
                    "castle-wall-collider-offsets.json enabled=false -- castle walls NOT carved.");
                return;
            }

            // Gate (archway) world centres, so the doorway-protect radius (archwayHalfClear) can
            // keep an optional extra clearance open around each opening.
            List<Vector3> gateCentres = CollectGateCentres();

            int added = 0;
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                Scene scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (root == null) continue;
                    added += CarveSidesUnder(root.transform, gateCentres);
                }
            }

            if (added > 0)
                FlowTrace.Step("WallCollider",
                    $"carved {added} castle wall segment(s) into the live NavMesh on '{active}' -- " +
                    "walls now block the NavMeshAgent hero; archway/gate opening left passable.");
        }

        /// <summary>Recursively find every CastleSide_* root under <paramref name="t"/> and carve
        /// its masonry colliders. Returns the count newly added.</summary>
        private int CarveSidesUnder(Transform t, List<Vector3> gateCentres)
        {
            int added = 0;
            if (t == null) return added;

            if (t.name.StartsWith(SideRootPrefix, StringComparison.Ordinal))
            {
                string side = t.name.Substring(SideRootPrefix.Length);   // "South" / "North" / ...
                added += CarveSide(t, side, gateCentres);
                // A side root's carve walks all its own children; still recurse for safety (cheap).
            }

            for (int i = 0; i < t.childCount; i++)
                added += CarveSidesUnder(t.GetChild(i), gateCentres);

            return added;
        }

        /// <summary>Fit a carving obstacle to every masonry collider under one CastleSide_* root.</summary>
        private int CarveSide(Transform sideRoot, string side, List<Vector3> gateCentres)
        {
            int added = 0;
            float inset = _cfg.InsetForSide(side);

            var colliders = sideRoot.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider col = colliders[i];
                if (col == null) continue;
                if (IsArchway(col.transform, sideRoot)) continue;   // leave the arch opening passable

                if (TryFitObstacle(col, side, inset, gateCentres)) added++;
            }
            return added;
        }

        /// <summary>True if this collider belongs to the ARCH opening (a "Gate*" piece) rather than
        /// solid masonry. DoorJamb flank boxes carry "Gate" in their generated name but ARE solid
        /// masonry, so they are explicitly NOT treated as archway.</summary>
        private bool IsArchway(Transform t, Transform sideRoot)
        {
            Transform cur = t;
            while (cur != null)
            {
                string n = cur.name;
                if (n.IndexOf(SolidJambContains, StringComparison.OrdinalIgnoreCase) >= 0) return false; // solid jamb
                if (n.IndexOf(ArchNameContains, StringComparison.OrdinalIgnoreCase) >= 0) return true;    // arch mesh
                if (cur == sideRoot) break;
                cur = cur.parent;
            }
            return false;
        }

        /// <summary>Adds one box-shaped carving NavMeshObstacle sized to the collider's WORLD bounds,
        /// parented under an identity holder so the world AABB maps 1:1 to a unit-scaled box.
        /// Idempotent across re-scans. Returns true when a NEW obstacle was added.</summary>
        private bool TryFitObstacle(Collider col, string side, float inset, List<Vector3> gateCentres)
        {
            int id = col.GetInstanceID();
            if (!_carved.Add(id)) return false;   // already handled this collider

            Bounds b = col.bounds;                // world-space AABB
            Vector3 centre = b.center;

            // Per-side inset: nudge the obstacle toward (-) / away from (+) the castle centre along
            // the horizontal direction from origin to the wall. Lets the owner tune the wall in/out.
            Vector3 horiz = new Vector3(centre.x, 0f, centre.z);
            if (Mathf.Abs(inset) > 0.001f && horiz.sqrMagnitude > 0.0001f)
                centre += horiz.normalized * inset;

            // Optional doorway protection: if this box would intrude within archwayHalfClear of a
            // gate centre, skip it so the passage cannot pinch (default 0 = trust the wall geometry).
            if (_cfg.archwayHalfClear > 0.01f && gateCentres != null)
            {
                for (int g = 0; g < gateCentres.Count; g++)
                {
                    float dx = centre.x - gateCentres[g].x;
                    float dz = centre.z - gateCentres[g].z;
                    if ((dx * dx + dz * dz) <= _cfg.archwayHalfClear * _cfg.archwayHalfClear)
                    {
                        FlowTrace.Step("WallCollider",
                            $"side={side} skip carve at {centre} -- within archwayHalfClear " +
                            $"{_cfg.archwayHalfClear:F1}m of a gate opening (kept passable).");
                        return false;
                    }
                }
            }

            Vector3 size = b.size;
            size.x += _cfg.thicknessPadding;      // grow the footprint so a thin wall fully blocks
            size.z += _cfg.thicknessPadding;
            size.y = Mathf.Max(size.y, _cfg.minHeight);

            var holder = EnsureHolder();
            var go = new GameObject("CastleWallObstacle_" + side + "_" + col.gameObject.name);
            go.transform.SetParent(holder, false);
            go.transform.position   = centre;
            go.transform.rotation   = Quaternion.identity;   // AABB is axis-aligned; identity holder => lossyScale 1
            go.transform.localScale = Vector3.one;

            var obstacle = go.AddComponent<NavMeshObstacle>();
            obstacle.shape   = NavMeshObstacleShape.Box;
            obstacle.size    = size;
            obstacle.center  = Vector3.zero;
            obstacle.carving = true;                 // cut the LIVE navmesh -- blocks agents without a rebake
            obstacle.carveOnlyStationary = true;     // the wall never moves; cheap + stable carve
            return true;
        }

        /// <summary>World centres of the four gate/arch openings (for archwayHalfClear protection).</summary>
        private List<Vector3> CollectGateCentres()
        {
            var centres = new List<Vector3>(4);
            var all = UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i];
                if (t == null) continue;
                // The arch is the "Gate_*" piece directly under a CastleSide_* root.
                if (t.parent == null || !t.parent.name.StartsWith(SideRootPrefix, StringComparison.Ordinal)) continue;
                if (t.name.IndexOf(ArchNameContains, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (t.name.IndexOf(SolidJambContains, StringComparison.OrdinalIgnoreCase) >= 0) continue;
                centres.Add(t.position);
            }
            return centres;
        }

        private Transform EnsureHolder()
        {
            if (_holder != null) return _holder;
            var existing = GameObject.Find(ObstacleHolderName);
            var go = existing != null ? existing : new GameObject(ObstacleHolderName);
            go.transform.position   = Vector3.zero;
            go.transform.rotation   = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            _holder = go.transform;
            return _holder;
        }

        // =====================================================================
        //  Owner-tweakable config (Resources/Data/castle-wall-collider-offsets.json).
        //  Hand-edit; read at scene load. Guarded: any parse miss falls back to sane defaults.
        // =====================================================================
        [Serializable]
        private sealed class WallColliderConfig
        {
            public bool  enabled          = true;
            public float thicknessPadding = 0.4f;
            public float minHeight        = 6.0f;
            public float archwayHalfClear = 0.0f;
            public SideOffset[] sides;

            [Serializable]
            public sealed class SideOffset { public string side; public float inset; }

            public float InsetForSide(string side)
            {
                if (sides == null || string.IsNullOrEmpty(side)) return 0f;
                for (int i = 0; i < sides.Length; i++)
                    if (sides[i] != null && string.Equals(sides[i].side, side, StringComparison.OrdinalIgnoreCase))
                        return sides[i].inset;
                return 0f;
            }

            public static WallColliderConfig Load()
            {
                try
                {
                    var ta = Resources.Load<TextAsset>("Data/castle-wall-collider-offsets");
                    if (ta == null || string.IsNullOrEmpty(ta.text))
                    {
                        FlowTrace.Once("WallCollider", "no-config",
                            "castle-wall-collider-offsets.json not found -- using built-in defaults " +
                            "(enabled, padding 0.4, minHeight 6).");
                        return new WallColliderConfig();
                    }
                    var cfg = JsonUtility.FromJson<WallColliderConfig>(ta.text);
                    return cfg ?? new WallColliderConfig();
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Flow:WallCollider] castle-wall-collider-offsets.json parse failed (" +
                                     e.Message + ") -- using defaults.");
                    return new WallColliderConfig();
                }
            }
        }
    }
}
