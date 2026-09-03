// =============================================================================
// GroundZFightFixer — WO-333 (P0) runtime floor z-fighting fix (no rebake).
// -----------------------------------------------------------------------------
// SYMPTOM (owner playtest): a flickering / shimmering floor in the playable
// Village2 scene — the whole ground shimmers as the camera moves, with double
// (overlapping) colliders under the player.
//
// ROOT CAUSE: Village2's baked "Ground" plane sits at exactly Y=0, COPLANAR with
// the ExteriorTerrain in the merged world (leveled flat to Y=0 across the
// village footprint). Two opaque surfaces at the same depth
// → z-fighting flicker over the entire floor, plus two colliders stacked at Y=0.
//
// THE GENERATOR IS ALREADY FIXED — Village2Generator.CreateGroundPlane() now seats
// the plane at Y=-0.05 so the terrain wins the depth test. But that only takes
// effect on a FRESH village REGEN. The SHIPPED baked Village2.unity still has the
// ground at Y=0. This runtime component lowers the BAKED plane at load so the fix
// lands WITHOUT a rebake.
//
// WHAT THIS DOES (asset-independent, always lands):
//   On every village scene load, find the large (~140x140 m) plane named "Ground"
//   near the village centre at/around Y=0 and lower it to Y=-0.05. The merged
//   terrain then wins the depth test as the single visible floor; the plane stays
//   in place as the Build-Mode raycast collider (Build Mode positions its ghost by
//   raycasting this floor collider — so we keep the object, just drop it 5 cm).
//
// Mirrors the runtime-fixer pattern of TreeOfLifeMaterialFixer /
// EnvironmentTreeMaterialFixer:
//   • [RuntimeInitializeOnLoadMethod(AfterSceneLoad)] + SceneManager.sceneLoaded
//     re-arm — the player boots into Title and reaches Village2 LATER, so a one-shot
//     check would miss the town; we re-run on every scene load.
//   • Scene-gated by EXCLUSION + GEOMETRY — never touches Title / HeroSelect / DTT /
//     dungeons. See the "SCENE ROUTING" region below for why it is no longer gated by
//     a hardcoded scene-name prefix.
//   • WEBGL-SAFE (WO-331): an uncaught exception in a sceneLoaded handler HALTS the
//     WebGL player, so every entry point is wrapped in try/catch. No File I/O, no
//     scene-mesh-ref dependency — just transform reads/writes.
//   • IDEMPOTENT: only lowers a plane that is still at/near Y=0 (within a small
//     epsilon); a plane already at -0.05 (regenerated town, or a second load) is
//     left untouched, so repeated loads never drift it further down.
// =============================================================================

using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core
{
    public static class GroundZFightFixer
    {
        // Target depth for the baked ground plane: 0.5 m below Y=0 so the coplanar
        // terrain in the merged world wins the depth test EVERYWHERE. Matches the value
        // Village2Generator.CreateGroundPlane() now bakes.
        //   WO-333 update: a 5 cm drop was NOT enough — the terrain RENDER mesh is
        //   LOD-simplified (heightmapPixelError=4) so its visible surface deviates
        //   several cm from the flat heightmap; in patches it dipped below -0.05 and the
        //   plane poked through, giving the MOTTLED dark-grey/green CHECKERBOARD. -0.5 m
        //   clears all terrain render deviation so the merged terrain is the single visible floor.
        private const float TargetY = -0.5f;

        // CASTLE HUB target — DATA-PROVEN regression fix (2026-06-28, owner F8 "well/castle not
        // on ground, ground under y=0"). The hub plaza is the DESIGNED stone floor; it must WIN
        // the depth test, not yield. The old code sank the 26 CourtyardFloor tiles to TargetY
        // (-0.5) — correct for Village2 (its "Ground" plane should yield to nicer terrain) but
        // BACKWARDS for the castle. Player.log proved the mover: "[GroundZFightFixer] hub —
        // lowered 26 CourtyardFloor tiles to Y=-0.5". The sink is old, but re-centering
        // the merged terrain and re-baking the navmesh to y=0.06 raised props (~0) and
        // the hero (0.06) above the still-sunk plaza → the 0.5m float. Seat the plaza a hair
        // above origin so it sits at prop/hero level (no float) and still wins over any coplanar
        // terrain at y=0. Authored near-zero tiles are left alone by the idempotency guard.
        private const float HubTargetY = 0.02f;

        // Only lower a plane that is still ABOVE the target (a baked plane at Y=0 OR the
        // earlier -0.05 fix). A plane already at/below the target (regenerated town, or a
        // re-load after we lowered it) is skipped — keeps the fix idempotent and prevents
        // drift on repeated loads.
        private const float NearZeroEpsilon = 0.01f;

        // How far (XZ) from the village centre the plane's centre may sit and still
        // count as THE ground plane. The baked plane is centred on origin; this is a
        // generous guard so we never grab some unrelated small plane elsewhere.
        private const float CentreRadius = 30f;

        // Minimum world footprint (per axis) to qualify as the big ground plane and
        // not a small decorative quad. The real ground spans ~140 m; require >=60 m.
        private const float MinFootprint = 60f;

        /// <summary>
        /// Registrar. Runs once at app start, then re-runs on EVERY scene load — the
        /// player boots into Title and reaches Village2 LATER, so a one-shot check
        /// would miss the town. Idempotent per load.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            // Also fix the scene already active at app start.
            SafeFix();
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene,
                                          UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            SafeFix();
        }

        // WO-331: never let the ground fix throw out of a sceneLoaded handler (halts WebGL).
        private static void SafeFix()
        {
            try { FixGroundPlane(); }
            catch (System.Exception e)
            {
                Debug.LogWarning("[GroundZFightFixer] ground fix threw (non-fatal): " + e);
            }
        }

        /// <summary>
        /// Lower the baked Village2 ground plane to Y=-0.05 so the merged-world terrain
        /// is the single visible floor. Public so a generator or test can call it.
        /// No-op outside a village scene, or when the plane is already lowered.
        /// </summary>
        public static void FixGroundPlane()
        {
            // GARRISON / OUTPOST path FIRST, and UNGATED by the active scene.
            // The outpost (Garrison_troll_outpost / _frost_keep / _ruined_keep / hill_fort)
            // loads ADDITIVELY over the hub/merged world and is NEVER SetActive'd, so
            // GetActiveScene() still reports "MainCastle_*" / "Village*" while the outpost
            // floor is live. InFixableScene() therefore can never route to it.
            // Run the Garrison pass whenever ANY Garrison_* scene is loaded (cheap +
            // idempotent), independent of which scene is active.
            if (AnyGarrisonSceneLoaded())
            {
                FixGarrisonFloor();
                // Do NOT return — the active hub/village may ALSO need its own floor fix
                // while the outpost is additively layered on top.
            }

            string sceneName = ActiveSceneName();
            if (!InFixableScene())
            {
                // WO-1301: a fixer that silently does not run looks IDENTICAL to one that
                // runs and fails. Say which it was, every load, by name.
                FlowTrace.Step("GroundZFightFixer",
                    "gate SKIP scene='" + sceneName + "' — excluded (menu/battle/dungeon); no floor pass runs here.");
                return;
            }

            FlowTrace.Step("GroundZFightFixer",
                "gate MATCH scene='" + sceneName + "' (hub=" + HubScenes.IsHub(sceneName) +
                " overworld=" + HubScenes.IsOverworld(sceneName) + ") — running floor passes.");

            // CASTLE HUB plaza pass: the legacy hub's plaza floor is a 5x5 GRID of small
            // tiles named "CourtyardFloor_{x}_{z}" (~8 m each) at Y=0.01, coplanar with the
            // terrain at Y=0 → flashing floor. The big single-plane logic below never
            // matches these (no >60m footprint, no "Ground" plane), so they get their OWN
            // pass. GATED BY GEOMETRY, not by scene name: a scene with no plaza tiles
            // simply gets 0 here and falls through.
            int tiles = FixHubFloorTiles();

            // The runtime opaque floor exists ONLY because the legacy hub had a plaza with
            // ~1% coverage over terrain depressed to Y=-3 (see EnsureHubOpaqueFloor).
            // ⛔ It must NEVER fire in a scene that already HAS a terrain floor under the
            // plaza — the merged world does, and a 90x90 grey stone slab dropped into a
            // grass town is a far worse defect than the one we came to fix.
            if (tiles > 0 && !TerrainCoversPlaza())
            {
                EnsureHubOpaqueFloor();
            }
            else if (tiles > 0)
            {
                FlowTrace.Step("GroundZFightFixer",
                    "hub opaque floor SKIPPED in '" + sceneName + "' — a Terrain already covers the " +
                    "plaza, so the scene has a real floor and needs no injected slab.");
            }

            // Property-based coplanar sweep — the generalisation of this whole component.
            // Catches any large flat VISIBLE surface sitting on the terrain, whatever it is
            // named and whatever scene it lives in.
            FixCoplanarGroundSurfaces(sceneName);

            GameObject ground = FindBakedGroundPlane();
            if (ground == null) return;

            float y = ground.transform.position.y;
            // IDEMPOTENT: only lower a plane still ABOVE the target (baked Y=0, or the
            // earlier -0.05 fix). Already-lowered planes (regen / re-load) are at/below
            // the target → skip.
            if (y > TargetY + NearZeroEpsilon)
            {
                Vector3 p = ground.transform.position;
                p.y = TargetY;
                ground.transform.position = p;
                Debug.Log("[GroundZFightFixer] WO-333 — lowered baked '" + ground.name +
                          "' plane from Y=" + y.ToString("0.###") + " to Y=" + TargetY +
                          " so the merged terrain wins the depth test (no rebake; " +
                          "plane kept as the Build-Mode raycast collider).");
            }
        }

        // Castle-hub pass: find ALL plaza floor tiles (CourtyardFloor_* / qFloorWood*)
        // and lower EACH still-high tile to TargetY so the merged terrain wins the
        // depth test. Idempotent: a tile already at/below TargetY (re-load) is skipped,
        // so repeated loads never drift it down. Small tiles → no >60m footprint guard.
        private static int FixHubFloorTiles()
        {
            var all = Object.FindObjectsByType<MeshRenderer>();
            int lowered = 0;
            int matched = 0;
            foreach (var mr in all)
            {
                if (mr == null) continue;
                if (!NameIsHubFloorTile(mr.name)) continue;
                // A DISABLED renderer is invisible: it cannot z-fight, and moving it only
                // shifts a raycast/nav collider for no visual gain. Skip it.
                if (!mr.enabled || !mr.gameObject.activeInHierarchy) continue;
                matched++;

                Transform t = mr.transform;
                float y = t.position.y;
                // SEAT the hub plaza at HubTargetY (~0) so it reads as the floor at prop/hero
                // level. Only MOVE a tile meaningfully off-target (the old -0.5 sink, or an
                // outlier); a tile already within epsilon of HubTargetY is left alone.
                if (Mathf.Abs(y - HubTargetY) > NearZeroEpsilon)
                {
                    Vector3 p = t.position;
                    p.y = HubTargetY;
                    t.position = p;
                    lowered++;
                }
            }
            if (lowered > 0)
            {
                Debug.Log("[GroundZFightFixer] hub — seated " + lowered +
                          " CourtyardFloor tiles to Y=" + HubTargetY +
                          " (plaza is the castle floor; wins over coplanar terrain; no float).");
            }
            FlowTrace.Step("GroundZFightFixer",
                "plaza pass: " + matched + " visible plaza tile(s) matched, " + lowered + " seated to Y=" +
                HubTargetY + ".");
            return matched;
        }

        // Name of the runtime opaque floor we inject under the castle hub. Used as the
        // idempotency key — if a child of this name already exists, the pass bails.
        private const string HubOpaqueFloorName = "HubOpaqueFloor (runtime)";

        // Castle-hub OPAQUE FLOOR pass — DATA-PROVEN fix (2026-06-28, Player.log:
        // "[WorldSceneLoader] TERRAINDIAG surfaceY @x=0 -> -3.000"). The hub has NO
        // continuous opaque floor: only ~25 tiny 2 m CourtyardFloor tiles on 8 m centres
        // (~1% coverage, seated near Y=0 by FixHubFloorTiles), while the big nav plane
        // "CourtyardFloor_Nav" (130x130) is renderer-DISABLED (nav-only). So the ground
        // the player SEES across the whole castle is the ExteriorTerrain in the merged world,
        // intentionally depressed to Y=-3.0 under the castle footprint
        // (ExteriorTerrainBuilder CastleDepressionDepth=-3 so terrain can't poke through a
        // floor) — but with no floor, the 3 m drop shows EVERYWHERE = "all counter sunk".
        //
        // Fix: spawn ONE opaque ~90x90 m stone plane at Y=0 (just below the 0.01-0.02 tiles
        // so it doesn't z-fight them) covering the ±44 m wall interior (NOT the 130 m nav
        // plane, which pokes 21 m past the walls into the wilderness). Non-blocking (no
        // collider — the nav plane already handles walkability). Idempotent: bail if it
        // already exists. URP/Lit stone material so it is never magenta / colourless.
        private static void EnsureHubOpaqueFloor()
        {
            // IDEMPOTENT: bail if we already injected the floor this session.
            if (GameObject.Find(HubOpaqueFloorName) != null) return;

            // Anchor on the castle XZ centre: reuse the existing hub-tile centroid so the
            // floor lands under the actual plaza even if the hub root isn't at origin.
            Vector3 centre = HubFloorCentreXZ();

            var plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = HubOpaqueFloorName;
            // Unity primitive Plane is 10x10 m at scale 1 → scale 9 = 90x90 m.
            plane.transform.localScale = new Vector3(9f, 1f, 9f);
            plane.transform.position = new Vector3(centre.x, 0.0f, centre.z);

            // Non-blocking: the nav plane (CourtyardFloor_Nav) already handles walkability,
            // so strip this floor's collider to avoid double colliders / nav interference.
            var col = plane.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            // Material: a URP/Lit stone surface so it is NEVER magenta (missing shader) and
            // NEVER colourless. Prefer a committed grey/stone material; fall back to a
            // neutral-stone Lit material built at runtime.
            var mr = plane.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                Material stone = LoadHubFloorMaterial();
                if (stone != null)
                {
                    mr.sharedMaterial = stone;
                }
                else
                {
                    // WO-580: NEVER leave the bare CreatePrimitive DEFAULT material on this
                    // 90x90 plane — under URP it renders flat WHITE (a giant white floor /
                    // bright slab the owner would read as a "white" artifact). If no stone
                    // material could be resolved (URP/Lit shader stripped), hide the renderer
                    // rather than show white; the nav plane still handles walkability.
                    mr.enabled = false;
                    FlowTrace.Warn("GroundZFightFixer",
                        "WO-580: hub opaque floor material unresolved — renderer DISABLED to " +
                        "avoid a default-white slab (URP/Lit shader missing?).");
                }
            }

            FlowTrace.Step("GroundZFightFixer",
                "hub — created '" + HubOpaqueFloorName + "' opaque floor (90x90 m) at Y=0.0, " +
                "centre (" + centre.x.ToString("0.#") + ", " + centre.z.ToString("0.#") +
                ") so the depressed merged terrain no longer shows as 'all counter sunk'.");
        }

        // Centre the runtime floor on the hub plaza: average the XZ of the seated
        // CourtyardFloor tiles. Falls back to origin if none are found (baked hub is
        // centred on origin anyway).
        private static Vector3 HubFloorCentreXZ()
        {
            var all = Object.FindObjectsByType<MeshRenderer>();
            double sx = 0, sz = 0; int n = 0;
            foreach (var mr in all)
            {
                if (mr == null) continue;
                if (!NameIsHubFloorTile(mr.name)) continue;
                Vector3 p = mr.transform.position;
                sx += p.x; sz += p.z; n++;
            }
            if (n == 0) return Vector3.zero;
            return new Vector3((float)(sx / n), 0f, (float)(sz / n));
        }

        // A URP/Lit stone material for the hub opaque floor. Tries a committed grey/stone
        // material under Resources first; otherwise builds a neutral-stone Lit material at
        // runtime. Never returns a missing/Standard shader (URP would render it magenta).
        private static Material LoadHubFloorMaterial()
        {
            string[] candidates =
            {
                "Materials/M_21_Grey_Light_LPUP",
                "Materials/M_Stone_Grey",
                "Materials/StoneGrey",
                "Materials/Ground_Stone",
            };
            foreach (var path in candidates)
            {
                var m = Resources.Load<Material>(path);
                if (m != null) return m;
            }

            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) return null; // never fall through to a magenta missing shader
            var mat = new Material(lit) { name = "HubOpaqueFloor_Stone (runtime)" };
            // Neutral warm stone — not magenta, not flat white/black.
            var stoneColor = new Color(0.32f, 0.30f, 0.28f, 1f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", stoneColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", stoneColor);
            return mat;
        }

        private static bool NameIsHubFloorTile(string n)
        {
            if (string.IsNullOrEmpty(n)) return false;
            string lower = n.ToLowerInvariant();
            // ⛔ "CourtyardFloor_Nav" is the 130x130 NAV/raycast plane, NOT a plaza tile —
            // it is renderer-disabled by design and must never be moved by this pass.
            // The plain StartsWith below used to swallow it (verified in
            // Main_Castle_Overworld.unity: CourtyardFloor_Nav, scale 13 = 130 m, renderer
            // m_Enabled: 0). Excluded by name AS WELL AS by the enabled-renderer guard in
            // FixHubFloorTiles, because either one alone is a single point of failure.
            if (lower.Contains("_nav") || lower.EndsWith("nav")) return false;
            return lower.StartsWith("courtyardfloor") || lower.StartsWith("qfloorwood");
        }

        /// <summary>
        /// True when an active Terrain's footprint covers the plaza centre — i.e. the scene
        /// ALREADY has a real ground surface there and needs no injected opaque slab.
        /// </summary>
        private static bool TerrainCoversPlaza()
        {
            var terrains = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            if (terrains == null || terrains.Length == 0) return false;
            Vector3 centre = HubFloorCentreXZ();
            for (int i = 0; i < terrains.Length; i++)
            {
                var t = terrains[i];
                if (t == null || t.terrainData == null || !t.gameObject.activeInHierarchy) continue;
                Vector3 o = t.transform.position;
                Vector3 sz = t.terrainData.size;
                if (centre.x >= o.x && centre.x <= o.x + sz.x &&
                    centre.z >= o.z && centre.z <= o.z + sz.z) return true;
            }
            return false;
        }

        // True when ANY Garrison_* scene is currently loaded (additively or single).
        // The raid outpost is loaded additively over the hub/merged world and is NEVER
        // SetActive'd, so GetActiveScene() never reports it — we must walk the loaded
        // scene list instead of asking for the active one (the "additive load never
        // becomes active" trap).
        private static bool AnyGarrisonSceneLoaded()
        {
            int count = UnityEngine.SceneManagement.SceneManager.sceneCount;
            for (int i = 0; i < count; i++)
            {
                var s = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (s.isLoaded && !string.IsNullOrEmpty(s.name) && s.name.StartsWith("Garrison"))
                    return true;
            }
            return false;
        }

        // GARRISON / OUTPOST pass: the outpost floor is a SINGLE large Plane named
        // "GarrisonGround" (open camps) or "DungeonFloor" (caves), built at Y=0 by
        // GarrisonSceneBuilder.BuildGroundOrFloor — coplanar with the merged-world
        // terrain at Y=0 → the SAME z-fighting flicker the hub had. The big
        // single-plane finder below cannot catch it: (a) the active scene is the hub, not
        // the outpost, so it never routes here, and (b) the outpost plane footprint is
        // ~48-56 m (medium half=16 / large half=20), BELOW the 60 m MinFootprint guard.
        // So this dedicated pass matches by NAME and lowers EACH still-high outpost floor
        // to TargetY so the terrain wins the depth test. Idempotent: a floor already
        // at/below TargetY (re-load) is skipped, so repeated loads never drift it down.
        private static void FixGarrisonFloor()
        {
            var all = Object.FindObjectsByType<MeshRenderer>();
            int lowered = 0;
            foreach (var mr in all)
            {
                if (mr == null) continue;
                if (!NameIsGarrisonFloor(mr.name)) continue;

                Transform t = mr.transform;
                float y = t.position.y;
                // IDEMPOTENT: only lower a floor still ABOVE the target.
                if (y > TargetY + NearZeroEpsilon)
                {
                    Vector3 p = t.position;
                    p.y = TargetY;
                    t.position = p;
                    lowered++;
                }
            }
            if (lowered > 0)
            {
                string scene = ActiveGarrisonSceneName();
                FlowTrace.Step("GroundZFightFixer",
                    "Garrison " + scene + " — offset " + lowered + " floor tiles to Y=" +
                    TargetY + " so the merged terrain wins the depth test (no rebake).");
            }
        }

        // Match the GarrisonSceneBuilder floor objects: open camps build "GarrisonGround";
        // dungeon/cave recipes build "DungeonFloor". Tolerate a "(Clone)" suffix + case.
        private static bool NameIsGarrisonFloor(string n)
        {
            if (string.IsNullOrEmpty(n)) return false;
            string lower = n.ToLowerInvariant();
            return lower.StartsWith("garrisonground") || lower.StartsWith("dungeonfloor");
        }

        // The name of the first loaded Garrison_* scene (for the FlowTrace proof line).
        private static string ActiveGarrisonSceneName()
        {
            int count = UnityEngine.SceneManagement.SceneManager.sceneCount;
            for (int i = 0; i < count; i++)
            {
                var s = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (s.isLoaded && !string.IsNullOrEmpty(s.name) && s.name.StartsWith("Garrison"))
                    return s.name;
            }
            return "Garrison_?";
        }

        // ─────────────────────────────────────────────────────────────────────
        //  SCENE ROUTING — ONE gate, and it is an EXCLUSION gate (WO-1301)
        // ---------------------------------------------------------------------
        //  THE DEFECT THIS SHAPE EXISTS FOR (data-proven 2026-09-03):
        //    This component used to decide "may I run here?" with an ALLOW-list of
        //    hardcoded scene-name prefixes:
        //        n.StartsWith("Village") || n.StartsWith("MainCastle") ||
        //        n.StartsWith("Castle")  || n.StartsWith("Garrison")
        //    and a second copy of two of those in InHubScene(). WO-608 (MergedWorld)
        //    then made the home hub `Main_Castle_Overworld`. That name starts with
        //    "Main_" — so it matches NEITHER "MainCastle" NOR "Castle", and the whole
        //    fixer became a silent no-op in the only town the player ever stands in.
        //    PROOF, not theory: 35 MB of owner Player.log full of
        //    scene='Main_Castle_Overworld' contains ZERO "GroundZFightFixer" lines,
        //    while [Flow:FloorDiag]/[Flow:HUD] from that same scene appear in the
        //    thousands. The last commit to touch this file (4afd7f658, "remove
        //    OuterWorld references (WO-608)") is the very rename that orphaned it and
        //    left the gate untouched. The owner's words: "It was fixed and now it's back."
        //
        //  WHY AN EXCLUSION GATE INSTEAD OF ONE MORE PREFIX:
        //    An ALLOW-list fails CLOSED and SILENTLY on a rename — the fixer stops
        //    running and nothing says so. An EXCLUSION list fails OPEN: a renamed or
        //    brand-new world scene keeps getting the floor passes, and each pass is
        //    already gated on finding the GEOMETRY it repairs (plaza tiles / a big
        //    "Ground" plane / a surface coplanar with a Terrain), so "running" in a
        //    scene with nothing to fix costs one FindObjectsByType and changes nothing.
        //    Names drift; the geometry a floor fixer repairs does not.
        //    The scenes we must NEVER touch are a SHORT, STABLE, KNOWN set, and
        //    dungeons are resolved from the canonical DeNelle.Core.HubScenes.IsDungeon
        //    rather than from a copy typed in here.
        //
        //  ⛔ DO NOT re-add a hub/village/castle name to this file. If a new hub scene
        //     appears, it belongs in HubScenes.Names — the one place — and this gate
        //     needs no edit at all.
        // ─────────────────────────────────────────────────────────────────────

        private static string ActiveSceneName()
        {
            return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        }

        /// <summary>
        /// May this component touch floors in the active scene? TRUE for every WORLD
        /// scene; FALSE only for the front-end / battle-box / dungeon scenes that own
        /// their own floors. Preserves the original exclusions (Title / HeroSelect /
        /// DTT / dungeons) without an allow-list that a rename can silently empty.
        /// </summary>
        private static bool InFixableScene()
        {
            return WouldRunInScene(ActiveSceneName());
        }

        /// <summary>
        /// ORACLE SEAM (WO-1301) — the routing decision as a PURE function of a scene name,
        /// so a regression can prove "this fixer reaches the live hub" without loading a
        /// scene or rendering a frame. InFixableScene() is nothing but this applied to the
        /// active scene, so the oracle and the shipping path cannot diverge.
        /// </summary>
        public static bool WouldRunInScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return false;
            // Dungeons resolve from the CANONICAL scene-family source, never a local copy.
            if (HubScenes.IsDungeon(sceneName)) return false;
            if (IsTemporarilyExcludedScene(sceneName)) return false;
            return !IsNonWorldScene(sceneName);
        }

        /// <summary>
        /// ⚠ DELIBERATE AND TEMPORARY (2026-09-03, lead ruling) — felt-test SCOPE CONTAINMENT.
        /// </summary>
        /// <remarks>
        /// ⛔ THIS IS NOT A JUDGEMENT THAT RAID SCENES DO NOT NEED THE FLOOR FIX, AND IT MUST
        /// NOT BE INHERITED AS CANON. Read this before deleting it OR before keeping it.
        ///
        /// RaidBase_* scenes were excluded by the OLD hub-prefix allow-list purely as a side
        /// effect of not being named "Village"/"Castle"/"Garrison". The new exclusion gate
        /// (see SCENE ROUTING above) would have swept them in — a correct WIDENING, and one
        /// this file stands behind on the merits: raids either have the coplanar defect or
        /// they do not.
        ///
        /// It is held back for a REASON OF TIMING, not of design. The owner is felt-testing
        /// on a device continuously and the build in flight exists to verify four unrelated
        /// fixes. A behaviour change she did not ask for, in scenes she is actively testing,
        /// costs more tonight than it gains, and finding out whether raids z-fight deserves
        /// its own run rather than riding along in someone else's verification build.
        ///
        /// TO LIFT IT: delete this method and its call in WouldRunInScene, then run the raid
        /// scenes once and read the "coplanar census" FlowTrace lines — they name every large
        /// flat visible surface and its separation from the terrain, so the answer arrives as
        /// data, not as a theory. The oracle case in HubSceneLiteralRegression asserts this
        /// exclusion set and MUST be updated in the same edit; a suite that disagrees with the
        /// code is the exact drift class this whole ticket was about.
        ///
        /// Resolved through the canonical DeNelle.Core.HubScenes.IsRaid — never a fresh
        /// "RaidBase" prefix typed in here, which would re-seed the original defect.
        /// </remarks>
        private static bool IsTemporarilyExcludedScene(string n)
        {
            return HubScenes.IsRaid(n);
        }

        /// <summary>
        /// Front-end, mockup and battle-box scenes: no world floor, nothing to repair, and
        /// historically the scenes this RuntimeInitialize hook must stay out of. These names
        /// are safe to hold here in a way hub names are not: they are not the moving target
        /// (a hub gets renamed as the world evolves; "Title" does not), and a MISS here is
        /// harmless — the geometry gate below simply finds nothing.
        /// </summary>
        private static bool IsNonWorldScene(string n)
        {
            const System.StringComparison OIC = System.StringComparison.OrdinalIgnoreCase;
            return n.StartsWith("Title", OIC)
                || n.StartsWith("HeroSelect", OIC)
                || n.StartsWith("PetSelect", OIC)
                || n.StartsWith("ATBBattle", OIC)
                || n.StartsWith("BattleHUD", OIC)
                || n.StartsWith("HUD_", OIC)
                || n.StartsWith("VfxGallery", OIC);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  PROPERTY PASS — offset any large VISIBLE flat surface that is coplanar
        //  with the Terrain it sits on. This is the whole component generalised:
        //  it matches the DEFECT (two opaque surfaces at the same depth) instead of
        //  matching a scene name or an object name, so it keeps working through a
        //  rename, a rebake, or a re-authored floor.
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>Two surfaces within this distance z-fight on device.</summary>
        public const float CoplanarEpsilon = 0.05f;

        /// <summary>How far BELOW the terrain surface a coplanar duplicate is sunk. Matches
        /// TargetY's rationale: the terrain RENDER mesh is LOD-simplified, so a few cm is not
        /// enough — its visible surface deviates from the heightmap and the plane pokes back
        /// through as a mottled checkerboard.</summary>
        public const float CoplanarSinkBelow = 0.5f;

        private static void FixCoplanarGroundSurfaces(string sceneName)
        {
            var terrains = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            if (terrains == null || terrains.Length == 0)
            {
                FlowTrace.Step("GroundZFightFixer",
                    "coplanar sweep: scene '" + sceneName + "' has NO Terrain — nothing to be coplanar " +
                    "WITH; sweep skipped (this is normal for the legacy tile-plaza hub).");
                return;
            }

            var all = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
            int examined = 0, sunk = 0;
            foreach (var mr in all)
            {
                if (mr == null) continue;
                // Invisible geometry cannot z-fight. This is what keeps the nav plane,
                // and every other renderer-disabled helper, out of the sweep.
                if (!mr.enabled || !mr.gameObject.activeInHierarchy) continue;
                // Never sweep the floor we ourselves injected.
                if (mr.name == HubOpaqueFloorName) continue;

                Bounds b = mr.bounds;
                // Only a BIG, FLAT thing can be a floor. A prop, a wall or a roof is not.
                if (b.size.x < MinFootprint || b.size.z < MinFootprint) continue;
                if (b.size.y > 1f) continue;

                float surfaceY;
                if (!TryTerrainSurfaceY(terrains, b.center, out surfaceY)) continue;

                float separation = b.center.y - surfaceY;
                examined++;
                FlowTrace.Step("GroundZFightFixer",
                    "coplanar census '" + mr.name + "' scene='" + sceneName + "' y=" +
                    b.center.y.ToString("0.###") + " terrainY=" + surfaceY.ToString("0.###") +
                    " separation=" + separation.ToString("0.####") + " size=" +
                    b.size.x.ToString("0.#") + "x" + b.size.z.ToString("0.#") + " m");

                if (Mathf.Abs(separation) <= CoplanarEpsilon)
                {
                    Transform t = mr.transform;
                    Vector3 p = t.position;
                    // Move by the DELTA, not to an absolute Y: the renderer bounds centre and
                    // the transform origin are not the same point on a child-nested mesh.
                    p.y += (surfaceY - CoplanarSinkBelow) - b.center.y;
                    t.position = p;
                    sunk++;
                    FlowTrace.Warn("GroundZFightFixer",
                        "coplanar FIX '" + mr.name + "' was " + separation.ToString("0.####") +
                        " m from the terrain surface (z-fighting) — sunk to " +
                        CoplanarSinkBelow.ToString("0.##") + " m BELOW it so the terrain is the " +
                        "single visible floor.");
                }
            }

            FlowTrace.Step("GroundZFightFixer",
                "coplanar sweep done: scene='" + sceneName + "' terrains=" + terrains.Length +
                " large-flat-visible-surfaces=" + examined + " sunk=" + sunk + ".");
        }

        /// <summary>World-space Y of the terrain surface under <paramref name="worldPos"/>,
        /// for the first terrain whose footprint contains it. False when no terrain covers it.</summary>
        private static bool TryTerrainSurfaceY(Terrain[] terrains, Vector3 worldPos, out float surfaceY)
        {
            surfaceY = 0f;
            for (int i = 0; i < terrains.Length; i++)
            {
                var t = terrains[i];
                if (t == null || t.terrainData == null || !t.gameObject.activeInHierarchy) continue;
                Vector3 o = t.transform.position;
                Vector3 sz = t.terrainData.size;
                if (worldPos.x < o.x || worldPos.x > o.x + sz.x) continue;
                if (worldPos.z < o.z || worldPos.z > o.z + sz.z) continue;
                // SampleHeight is terrain-LOCAL; add the terrain's own world Y.
                surfaceY = t.SampleHeight(worldPos) + o.y;
                return true;
            }
            return false;
        }

        // Find the large ground plane named "Ground" centred near the village origin at
        // or around Y=0. Matches by NAME + SIZE + CENTRE so we never grab an unrelated
        // small plane. Uses renderer bounds (visible footprint), not transform values,
        // so a scaled primitive Plane is measured correctly.
        private static GameObject FindBakedGroundPlane()
        {
            var all = Object.FindObjectsByType<MeshRenderer>();
            GameObject best = null;
            float bestDist = float.MaxValue;
            foreach (var mr in all)
            {
                if (mr == null) continue;
                if (!NameIsGround(mr.name)) continue;

                Bounds b = mr.bounds;
                // Big enough to be THE floor, not a decorative quad.
                if (b.size.x < MinFootprint || b.size.z < MinFootprint) continue;
                // Centred near the village origin on XZ.
                float d = new Vector2(b.center.x, b.center.z).sqrMagnitude;
                if (d > CentreRadius * CentreRadius) continue;
                // Roughly flat near Y=0 (don't grab a wall or elevated platform).
                if (Mathf.Abs(b.center.y) > 2f) continue;

                if (d < bestDist) { bestDist = d; best = mr.gameObject; }
            }
            return best;
        }

        private static bool NameIsGround(string n)
        {
            if (string.IsNullOrEmpty(n)) return false;
            // Village generator names the plane exactly "Ground". (The GARRISON outpost
            // floor "GarrisonGround"/"DungeonFloor" is handled by the dedicated
            // FixGarrisonFloor pass instead — its ~48-56 m footprint is below this
            // finder's 60 m MinFootprint guard and the outpost is never the active scene.)
            // Tolerate a "(Clone)" suffix and case just in case.
            string lower = n.ToLowerInvariant();
            return lower == "ground" || lower.StartsWith("ground");
        }
    }
}
