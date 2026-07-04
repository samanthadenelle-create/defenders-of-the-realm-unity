// =============================================================================
// CastleMoatBuilder — diegetic WATER MOAT + 4 STONE-BRIDGE CROSSINGS (BONES).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.World
//
// OWNER (overnight): the "you cannot go past here" castle edge should READ as DELIBERATE.
// A WIDE WATER MOAT around the castle is the natural impassable boundary ("water makes it
// make sense" vs. an invisible wall), and 4 stone bridges at the cardinal gates are the
// intentional exits. The bridges double as defensive CHOKEPOINTS (enemies must funnel
// across them; towers/troops cover the lane) and ARE the WO-509 four RegionGates.
// See docs/CASTLE_MOAT_DESIGN_NOTE.md + docs/MOAT_WATER_DESIGN_2026-07-03.md.
//
// OWNER RULINGS (2026-07-03, MOAT WATER SLICE 1):
//   1) WATER BAND widened to ~18m (r=44..62) — the water IS the walk-off containment +
//      seam enforcement (diegetic), so it must run UNDER the FULL playable span of every
//      crossing. MoatCentreRadius re-derived; the dead dip-fill path DELETED.
//   2) ALL FOUR crossings are now CLONES of the owner-verified SOUTH stone bridge — the
//      old label=="South" special-case + the N/W/E funnel-ramp path are RETIRED. Each side
//      places the same stone bridge prefab, built in the south frame then rigidly yaw-rotated
//      about the world origin (South 0 / West 90 / North 180 / East 270); the analytic
//      deck-collider + lift-seat are computed in the south frame and CARRIED into each side
//      by that same origin-yaw rotation (they are children of the bridge).
//   3) WATER MESH is now ONE mitred square-annulus (kills the corner double-blend the 4
//      overlapping transparent planes caused): three concentric DISJOINT sub-bands (a
//      wet-shore darkening strip at the plinth + a lighter shallow band + a deeper mid band),
//      URP/Lit + WebGL-safe. MoatWaterShimmer (DEF-195) + FishSchool reused UNCHANGED.
//
// CROSSING-SPAN ORACLE (owner, binding): the water band (44..62) must stay fully UNDER
// every crossing span with dry bedding both ends. The south bridge (and thus every clone)
// spans castle-end r=44 (plinth face, raised to liftY) -> outer-end r=44+deckSpan (~66),
// so band 44..62 is under 44..66 with the plinth (dry, raised) + ground beyond 62 (dry).
//
// SAFETY: flag-gated (FeatureFlags.CastleMoat, default ON). Self-bootstrap mirrors
// OuterWorldBoundaryInjector (AfterSceneLoad + sceneLoaded re-arm). Guarded, null-safe,
// idempotent, ASCII-only, never throws out of a sceneLoaded handler (WebGL-safe), no
// gitignored packs (engine primitives + URP/Lit), mobile-cheap (shared materials).
// Instrumented per CLAUDE.md S12: FlowTrace.Step("CastleMoat", ...).
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using DeNelle.Core;
using DeNelle.Core.Diagnostics;
using OffsetForge;

namespace DeNelle.Village.World
{
    /// <summary>
    /// Builds the diegetic castle water moat + 4 stone-bridge crossings at runtime (BONES).
    /// First-pass visual only; flag-gated + tunable. See file header + design note.
    /// </summary>
    public static class CastleMoatBuilder
    {
        private const string MoatRootName = "CastleMoat";

        // ---- TUNABLES ------------------------

        // WATER BAND geometry (owner ruling 2026-07-03): the moat+water is the DIEGETIC seam
        // that covers the gap from the raised castle plinth to the outer terrain — that is
        // why the castle was raised. The band is DERIVED from the island geometry, widened to
        // ~18m so the water runs UNDER the FULL playable span of every crossing (the water IS the
        // walk-off containment + seam enforcement, not an invisible rail):
        //   inner edge = RampInnerRadius (44 = CastleHubBuilder.PlinthHalf mirror) so the water
        //                visibly LAPS the raised plinth face;
        //   outer edge = MoatOuterRadius (62) — every crossing (all four = south stone-bridge
        //                clones) spans PAST 62 with dry bedding both ends (south bridge span
        //                reaches ~66), so the band stays fully UNDER the deck (crossing-span oracle).
        private const float RampInnerRadius  = 44f;   // = CastleHubBuilder.PlinthHalf (plinth face / bridge castle-end)
        private const float MoatInnerRadius  = RampInnerRadius;                    // 44 — laps the plinth
        private const float MoatOuterRadius  = 62f;                               // owner ruling: ~18m band (44..62)
        private const float MoatWidth        = MoatOuterRadius - MoatInnerRadius;  // 18m across
        private const float MoatCentreRadius = MoatInnerRadius + MoatWidth * 0.5f; // 53 — band centreline

        // Two-tone depth-read sub-bands + a wet-shore darkening strip at the plinth edge (slice 1
        // geometry — the shader/foam treatment is a later owner-gated slice). Radii, inner->outer:
        //   plinth wet-shore strip : MoatInnerRadius .. +MoatShoreStripWidth (dark, welds water to plinth)
        //   shallow band (lighter) : strip outer .. MoatCentreRadius
        //   deep band (deeper)     : MoatCentreRadius .. MoatOuterRadius (widest — the shimmer target)
        private const float MoatShoreStripWidth = 1.5f;

        // WO-605 (owner pivot 2026-07-03): the moat is a CONTAINED BASIN filled with SOLID OPAQUE
        // water at a FIXED level — NO raycast probe (the probe caught the plinth/gate colliders at
        // liftY and floated the sheet, submerging the deck; that path is DELETED). The level is a
        // DESIGN CONSTANT set BELOW the bridge deck outer-end (~y=2.15) so the water reads full but
        // never submerges a deck.
        private const float FixedWaterY = 1.5f;

        // Raised OUTER basin lip (mesh rim, NOT TerrainData — TerrainData edits risk the known
        // binary-asset corruption + navmesh-rebake landmines). The trench between this rim and the
        // raised castle plinth (liftY) is the basin the solid water fills.
        private const float LipWidth  = 3f;     // rim thickness (r = MoatOuterRadius .. +LipWidth)
        private const float LipFloorY = 0f;     // basin-floor / OuterWorld terrain-flush level
        private const float LipTopY   = 2.0f;   // crest: ABOVE water (1.5), BELOW the deck where it passes over (~2.7)
        private static readonly Color LipColor  = new Color(0.50f, 0.50f, 0.52f); // stone retaining rim
        private static readonly Color BermColor = new Color(0.28f, 0.42f, 0.20f); // natural earthy-grass bank

        // Solid OPAQUE blue water (owner pivot): a FILLED channel, always visible (no edge-on-invisible
        // transparent plane). The 3 depth-read sub-bands DERIVE their shades from this single base;
        // alpha stays 1 (opaque) so the fill always reads.
        private static readonly Color WaterColor = new Color(0.10f, 0.30f, 0.62f, 1f);

        // Fish-school size over the water band (graceful/optional, WO-590). Capped low for the Pi.
        private const int FishSchoolCount = 10;

        // ---- CROSSINGS (all four = clones of the owner-verified SOUTH stone bridge) ----
        // Owner ruling 2026-07-03: every cardinal crossing is the SAME stone bridge prefab
        // (Resources/Bridges/Bridge_Medieval_Stone, OffsetForge id 'bridge_south'), placed in the
        // south frame then rigidly yaw-rotated per side about the world origin. The funnel-ramp
        // path retired (the bridge covers every side); the analytic deck-collider + lift-seat are
        // computed in the south frame and carried into each side by the same origin-yaw rotation.
        // liftY (PlayerPrefs 'castle.liftY') raises the castle end to the plinth top.
        private const float BridgeY = 0.05f;   // raw first-pass seat height (offsets.json overrides)

        // ---- HEDGE LIP RING (owner 2026-07-03: seal the water edge with a low hedge, not a wall) ----
        // Option A (clone/web-safe): the polyperfect Fence_Shrub (_M, a hedge-railing hybrid) is
        // GITIGNORED, so — exactly like Bridge_Medieval_Stone — a committed Resources prefab is baked
        // by the editor menu 'Defenders > Seam > Generate Hedge Resources Prefab' (HedgePrefabGenerator)
        // and loaded at runtime. A continuous ring of instances rings the moat INNER lip with FOUR GAPS
        // aligned to the cardinal bridge mouths (so it never blocks a crossing). A thin invisible
        // BoxCollider ring (same 4 gaps) is the actual seal — the water mesh has no collider today.
        private const string HedgeResourcePath   = "Hedges/Fence_Shrub";
        private const float  HedgeLipInset        = 0.5f;   // sit the ring just INSIDE r=44 (on the plinth top, off the water face)
        private const float  HedgeSpacing         = 2.0f;   // ~= Fence_Shrub width (2.09m) so instances read continuous
        private const float  HedgeGapMargin       = 2.5f;   // extra clearance each side of a bridge mouth (keeps the crossing open)
        private const float  HedgeDefaultGapHalf  = 8f;     // half-gap fallback when a bridge mouth can't be measured
        private const float  HedgeColliderHeight  = 1.5f;   // invisible seal-wall height (block, don't hide the water)
        private const float  HedgeColliderThick   = 0.5f;   // thin seal-wall depth
        private static readonly Color HedgeColor   = new Color(0.20f, 0.42f, 0.18f); // low-poly hedge green (URP/Lit; pack mat is gitignored)

        // ---- COMMITTED MATERIALS (WO-605, WebGL-safe) --------------------------------------
        // Runtime Shader.Find("Universal Render Pipeline/Lit") is a WebGL build-time LIABILITY:
        // if no committed ASSET references the URP/Lit shader, the build strips it and Shader.Find
        // returns null at runtime -> water + bridge lose their material (invisible water / white
        // bridge — the owner's WO-605 symptom). The permanent fix (mirrors the committed Bridge/Hedge
        // Resources PREFABS) is to load COMMITTED .mat assets from Resources, which pull the shader
        // INTO the build; runtime Shader.Find stays only as a last-resort fallback. Bake the assets
        // with 'Defenders > Seam > Generate Moat Materials' (MoatMaterialGenerator).
        private const string WaterMaterialResource  = "Materials/Moat/MoatWater";   // Assets/Resources/Materials/Moat/MoatWater.mat
        private const string StoneMaterialResource  = "Materials/Moat/BridgeStone";  // Assets/Resources/Materials/Moat/BridgeStone.mat

        // The castle hub scene this builds for. (HubScenes covers variants; we additionally
        // require the south recipe to resolve so the gate radials are real.)
        private const string TargetScene = "MainCastle_Hall";

        // WO-608: the merged single-scene overworld also hosts the castle, so BuildMoat runs there too. On the
        // merged scene the seam is gone → only the 4 DECORATIVE bridges are placed (no water/lip/hedge/
        // berms/navcarve) — see BuildBridgesOnly / MergedBridgesOnly.
        private const string MergedScene = "Main_Castle_Overworld";

        // --------------------------------------------------------------------
        //  SELF-BOOTSTRAP (mirrors OuterWorldBoundaryInjector).
        // --------------------------------------------------------------------
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SafeBuild();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => SafeBuild();

        // Never let the moat build throw out of a sceneLoaded handler (halts WebGL).
        private static void SafeBuild()
        {
            try { BuildMoat(); }
            catch (System.Exception e)
            {
                Debug.LogWarning("[CastleMoat] moat build threw (non-fatal): " + e);
            }
        }

        /// <summary>
        /// Build the water moat ring + 4 stone-bridge crossings on the castle hub scene.
        /// No-op when the flag is OFF, off the hub scene, or when the moat already exists.
        /// </summary>
        public static void BuildMoat() => BuildInternal(MergedBridgesOnly());

        /// <summary>WO-608 merged-world (owner 2026-07-04): place ONLY the 4 drawbridge structures — NO
        /// water, NO outer lip, NO hedge ring, NO bank berms, NO moat nav-carve. The single merged scene
        /// <see cref="MergedScene"/> has no seam to mask, so the moat WATER + LIP are dropped as decorative;
        /// with the castle flush at y=0 the bridges are flat decorative gateways at the 4 exits. Public so
        /// the editor <c>WorldMergeBuilder</c> can bake the bridges into the merged scene (invoked by
        /// reflection — DeNelle.Editor does not reference DeNelle.Village). Builds under the SAME
        /// <see cref="MoatRootName"/> root so the runtime self-boot's idempotent Find() suppresses any
        /// re-build on the saved scene.</summary>
        public static void BuildBridgesOnly() => BuildInternal(bridgesOnly: true);

        // On the merged single scene we drop the water/lip and keep only the decorative bridges (owner
        // 2026-07-04). True whenever ff.mergedworld is ON and the merged scene is active or loaded.
        private static bool MergedBridgesOnly()
        {
            if (!FeatureFlags.MergedWorld) return false;
            if (SceneManager.GetActiveScene().name == MergedScene) return true;
            Scene merged = SceneManager.GetSceneByName(MergedScene);
            return merged.IsValid() && merged.isLoaded;
        }

        private static void BuildInternal(bool bridgesOnly)
        {
            // Flag gate — default ON so the owner sees the BONES; PlayerPrefs "ff.castlemoat" = 0 to hide.
            if (!FeatureFlags.CastleMoat) return;

            // OWNER 2026-07-04 (F8 x4 + editor-runtime screenshot): the MERGED world has NO moat and NO
            // bridges. The whole moat/water/lip/bridge apparatus existed ONLY to mask + cross the
            // castle<->overworld SEAM; the merged single scene has no seam, so ALL of it is gone. This
            // runtime self-boot is what rebuilds it every scene load (BuildDrawbridges below ran even in
            // bridges-only mode — the bridge the owner kept seeing; BuildWaterRing is the water). Build
            // NOTHING on the merged scene.
            if (MergedBridgesOnly())
            {
                FlowTrace.Step("CastleMoat", "MERGED world — moat + bridges SKIPPED ENTIRELY (owner 2026-07-04: seam gone, nothing to build).");
                return;
            }

            // Only on the castle hub. MainCastle_Hall is the ACTIVE home scene; the merged scene
            // (Main_Castle_Overworld) also hosts the castle. Be tolerant of the loaded-additive case
            // the same way the boundary injector is.
            Scene hub = SceneManager.GetSceneByName(TargetScene);
            if (!hub.IsValid() || !hub.isLoaded) hub = SceneManager.GetSceneByName(MergedScene);
            string activeName = SceneManager.GetActiveScene().name;
            bool hubActive = activeName == TargetScene || activeName == MergedScene;
            if (!hubActive && (!hub.IsValid() || !hub.isLoaded)) return;

            // IDEMPOTENT: a moat/bridge root already present -> done (repeated loads never stack).
            if (GameObject.Find(MoatRootName) != null) return;

            var root = new GameObject(MoatRootName);

            // Gate radials come from the south recipe; the 4 cardinal gates are it rotated by yaw.
            Vector3 southGate = ReadSouthGatePos();
            float gateLateral = southGate.x;   // off-centre lateral the bridges/gates share (x4 symmetry)

            // WO-605 owner pivot: FIXED water level — a design constant BELOW the bridge deck outer
            // end (~2.15); NO raycast probe (deleted). The basin is CONTAINED by the raised castle
            // plinth (inner) + a raised outer lip (built below as MESH, not TerrainData).
            float waterY = FixedWaterY;

            int waterVerts = 0, lipVerts = 0, berms = 0, hedges = 0, navCarves = 0;

            // Water fill FIRST (index-0 shimmer target) — skipped on the merged bridges-only path.
            if (!bridgesOnly) waterVerts = BuildWaterRing(root.transform, waterY);

            int bridges = BuildDrawbridges(root.transform, gateLateral);

            if (!bridgesOnly)
            {
                // Lip AFTER the bridges so MeasureMouth reads the REAL deck colliders (same source the
                // hedge + navcarve gaps use) — the rim is NOTCHED at each measured mouth so no deck ever
                // crosses solid lip regardless of the bridge pose (WO-605 owner: "do the lip or it cuts").
                lipVerts   = BuildOuterLip(root.transform, gateLateral); // raised outer basin rim (mesh), notched at the 4 mouths
                berms      = BuildBridgeBankBerms(root.transform, gateLateral); // natural curved seal at each mouth
                hedges     = BuildHedgeRing(root.transform, gateLateral);
                navCarves  = BuildMoatNavCarve(root.transform, gateLateral);   // carve the water off the live navmesh

                // Bring the ring to life with the proven shimmer (reuse, DEF-195). It auto-resolves the
                // first child renderer's sharedMaterial == the annulus' deep-band material (index 0).
                AttachShimmer(root);

                // WO-590: a small fish school over the water band (graceful/optional — skips if no model).
                SpawnFishSchool(root.transform, waterY);
            }

            // A new GameObject lands in the active scene by default; if the hub is loaded but not
            // active, move the moat into it so it shares the hub's lifetime.
            if (!hubActive && hub.IsValid() && hub.isLoaded)
                SceneManager.MoveGameObjectToScene(root, hub);

            if (bridgesOnly)
            {
                FlowTrace.Step("CastleMoat",
                    "BRIDGES-ONLY (WO-608 merged world): placed " + bridges + " decorative drawbridge structures at the 4 " +
                    "gate exits (gateLateral=" + gateLateral.ToString("0.00") + ", x4 symmetry) — NO water/lip/hedge/berms/" +
                    "navcarve (seam removed; the bridges are flat decoration on the flush y=0 ground).");
                return;
            }

            FlowTrace.Step("CastleMoat",
                "built castle BASIN moat: outer lip rim " + lipVerts + " verts + SOLID water fill " + waterVerts +
                " verts (channel r=" + MoatInnerRadius + ".." + MoatOuterRadius + " FILLED, width=" + MoatWidth +
                "m @ FIXED y=" + waterY.ToString("0.00") + " below deck-outer ~2.15) + " + bridges +
                " stone-bridge crossings + " + berms + " bank berms (natural mouth seal); gateLateral=" +
                gateLateral.ToString("0.00") + ", source: castle-south-recipe x4 symmetry.");

            FlowTrace.Step("CastleMoat", "hedge lip ring: " + hedges + " Fence_Shrub instances around the inner lip (r=" +
                (MoatInnerRadius - HedgeLipInset).ToString("0.0") + ") with 4 cardinal gaps at the bridge mouths + an invisible " +
                "BoxCollider seal-ring (same gaps) — the water edge is sealed without blocking a crossing.");
        }

        // --------------------------------------------------------------------
        //  WATER RING — ONE mitred square-annulus mesh (r=44..62) replacing the 4
        //  overlapping transparent planes (which double-blended at the corners — the
        //  code's own flagged failure). Three concentric, DISJOINT sub-bands (no overlap,
        //  so no corner double-blend): a wet-shore darkening strip hugging the plinth, a
        //  lighter shallow band, and a deeper mid band (the widest -> the shimmer target).
        //  Each band = 4 mitred trapezoid quads sharing corner miter seams (picture-frame).
        //  One Mesh, three submeshes, three shared URP/Lit transparent materials, one
        //  MeshRenderer. Visual only (no collider — water never blocks; impassability is
        //  navmesh/boundary, not physics).
        // --------------------------------------------------------------------
        private static int BuildWaterRing(Transform parent, float waterY)
        {
            FlowTrace.Step("CastleMoat", "BuildWaterRing ENTER: mitred annulus r=" + MoatInnerRadius + ".." +
                MoatOuterRadius + " @ y=" + waterY.ToString("0.00") + " (committed-material path, WebGL-safe).");

            // Sub-band radii (square half-extents), inner -> outer.
            float rStrip = MoatInnerRadius + MoatShoreStripWidth;   // plinth wet-shore strip outer edge
            float rMid   = MoatCentreRadius;                        // shallow|deep split

            // OPAQUE depth-read shades DERIVED from the one base blue (alpha 1 = SOLID fill): deep darker,
            // shallow lighter, shore darkest at the plinth.
            Color deepCol    = ScaleColor(WaterColor, 0.85f, 1f);   // deeper mid band
            Color shallowCol = ScaleColor(WaterColor, 1.25f, 1f);   // lighter shallow band
            Color shoreCol   = ScaleColor(WaterColor, 0.60f, 1f);   // dark wet-shore strip at the plinth

            // WO-605: committed Resources/.mat (WebGL-safe, OPAQUE blue) instanced per band; runtime Shader.Find fallback.
            Material deepMat    = ResolveMaterial(WaterMaterialResource, "CastleMoat_WaterDeep",    deepCol,    transparent: false);
            Material shallowMat = ResolveMaterial(WaterMaterialResource, "CastleMoat_WaterShallow", shallowCol, transparent: false);
            Material shoreMat   = ResolveMaterial(WaterMaterialResource, "CastleMoat_WaterShore",   shoreCol,   transparent: false);

            var verts      = new System.Collections.Generic.List<Vector3>(48);
            var uvs        = new System.Collections.Generic.List<Vector2>(48);
            var triDeep    = new System.Collections.Generic.List<int>(24);
            var triShallow = new System.Collections.Generic.List<int>(24);
            var triShore   = new System.Collections.Generic.List<int>(24);

            // Deep band FIRST so submesh/material index 0 is the widest surface — the shimmer
            // auto-resolves the first child renderer's sharedMaterial (== index 0) and scrolls its
            // ripple normal there.
            AddMitredBand(verts, uvs, triDeep,    rMid,            MoatOuterRadius, waterY);
            AddMitredBand(verts, uvs, triShallow, rStrip,          rMid,            waterY);
            AddMitredBand(verts, uvs, triShore,   MoatInnerRadius, rStrip,          waterY);

            var mesh = new Mesh { name = "MoatWaterAnnulus" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = 3;
            mesh.SetTriangles(triDeep,    0);
            mesh.SetTriangles(triShallow, 1);
            mesh.SetTriangles(triShore,   2);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject("MoatWater");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = new[] { deepMat, shallowMat, shoreMat };

            FlowTrace.Step("CastleMoat", "water annulus built: mitred square ring r=" + MoatInnerRadius + ".." +
                MoatOuterRadius + " (width " + MoatWidth + "m), 3 disjoint sub-bands (shore/shallow/deep), " +
                verts.Count + " verts, no corner overlap @ y=" + waterY.ToString("0.00") + ".");
            return verts.Count;
        }

        // One mitred band (picture-frame) between inner (a) and outer (b) square half-extents at
        // height y: 4 trapezoid quads, adjacent quads joined on the 45-degree corner miter so no
        // two quads overlap (this is what kills the old 4-plane corner double-blend).
        private static void AddMitredBand(System.Collections.Generic.List<Vector3> verts,
            System.Collections.Generic.List<Vector2> uvs,
            System.Collections.Generic.List<int> tris, float a, float b, float y)
        {
            // Inner (a) and outer (b) square corners, CCW: +X+Z, +X-Z, -X-Z, -X+Z.
            Vector3[] I =
            {
                new Vector3( a, y,  a), new Vector3( a, y, -a), new Vector3(-a, y, -a), new Vector3(-a, y,  a),
            };
            Vector3[] O =
            {
                new Vector3( b, y,  b), new Vector3( b, y, -b), new Vector3(-b, y, -b), new Vector3(-b, y,  b),
            };
            for (int k = 0; k < 4; k++)
            {
                int n = (k + 1) & 3;
                // Quad (I_k, O_k, O_n, I_n) winds to a +Y (up) top face; the I_k..O_k corner line
                // is the shared miter seam with the previous quad.
                AddQuadTop(verts, uvs, tris, I[k], O[k], O[n], I[n]);
            }
        }

        // Adds one quad (a,b,c,d) with an upward (+Y) facing normal + planar world-XZ UVs.
        private static void AddQuadTop(System.Collections.Generic.List<Vector3> verts,
            System.Collections.Generic.List<Vector2> uvs,
            System.Collections.Generic.List<int> tris, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            // Planar world-XZ UV (~10m tile) so the shared ripple normal tiles across the ring the
            // same way the retired planes did (keeps the DEF-195 shimmer reading at moat scale).
            uvs.Add(new Vector2(a.x / 10f, a.z / 10f));
            uvs.Add(new Vector2(b.x / 10f, b.z / 10f));
            uvs.Add(new Vector2(c.x / 10f, c.z / 10f));
            uvs.Add(new Vector2(d.x / 10f, d.z / 10f));
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
        }

        private static Color ScaleColor(Color c, float rgbMul, float alpha)
            => new Color(Mathf.Clamp01(c.r * rgbMul), Mathf.Clamp01(c.g * rgbMul), Mathf.Clamp01(c.b * rgbMul), alpha);

        // --------------------------------------------------------------------
        //  FISH SCHOOL (WO-590) — a small wandering school over the SOUTH water band
        //  (front of the castle, most visible). Graceful/optional: FishSchool loads a
        //  model from Resources, else builds a tiny primitive fish, never hard-errors.
        // --------------------------------------------------------------------
        private static void SpawnFishSchool(Transform parent, float waterY)
        {
            Guard.Try("CastleMoat", "spawn fish school", () =>
            {
                // School lives over the always-built moat BAND (44..62), south side.
                float bandCentre = MoatCentreRadius;
                float bandWidth  = MoatWidth;

                var go = new GameObject("MoatFishSchool");
                go.transform.SetParent(parent, false);
                // Centre the school over the south band, just below the water surface.
                go.transform.position = new Vector3(0f, waterY, -bandCentre);

                var school = go.AddComponent<FishSchool>();
                // Box bounds: across = the band depth (a margin in), along = a believable patch.
                school.Configure(
                    FishSchoolCount,
                    new Vector3((bandWidth * 0.5f) - 1.5f, 0.4f, 16f),
                    waterY - 0.3f);
            });
        }

        // --------------------------------------------------------------------
        //  CROSSINGS — 4 stone bridges at the cardinal gates (all clones of the south).
        //  Each side places the SAME owner-verified stone bridge in the south frame, then
        //  TryPlaceBridgePrefab rigidly yaw-rotates it about the world origin so it lands on
        //  the gate radial (the 4 gates ARE the south gate rotated by yaw {0,90,180,270}).
        // --------------------------------------------------------------------
        private static int BuildDrawbridges(Transform parent, float gateLateral)
        {
            var sides = new (string label, float yaw)[] { ("South", 0f), ("West", 90f), ("North", 180f), ("East", 270f) };

            int count = 0;
            foreach (var (label, yaw) in sides)
                if (TryPlaceBridgePrefab(parent, label, yaw, gateLateral)) count++;
            return count;
        }

        // ====================================================================
        //  OUTER BASIN LIP (WO-605 owner pivot) — a raised RIM ring of MESH geometry
        //  (NOT TerrainData: editing TerrainData risks the known binary-asset corruption
        //  + navmesh-rebake landmines). Encloses the moat's outer edge so the trench
        //  between this rim and the raised castle plinth is a CONTAINED BASIN the solid
        //  water fills. One mesh: inner vertical wall (faces the water) + top cap + outer
        //  vertical wall. Crest (LipTopY) sits ABOVE the fixed water level and BELOW the
        //  bridge deck where it passes over the rim, so the deck never clips through.
        //  Visual only (no collider) — it does not block the bridge outer landing or navmesh.
        // ====================================================================
        //  NOTCHED at the 4 bridge mouths (WO-605 owner: "you have to do the lip otherwise it cuts
        //  through the bridge"): rather than eyeball a new LipTopY, the rim SKIPS the crest/wall
        //  across each measured mouth (mirrors the 4-gap logic in BuildHedgeRing + BuildMoatNavCarve,
        //  same MeasureMouth source), so the deck NEVER crosses solid lip regardless of the pose.
        private static int BuildOuterLip(Transform parent, float gateLateral)
        {
            float innerR = MoatOuterRadius;                // 62 — laps the water band outer edge
            float outerR = MoatOuterRadius + LipWidth;     // 65 — world-side face

            var verts = new List<Vector3>(96);
            var uvs   = new List<Vector2>(96);
            var tris  = new List<int>(144);

            // The four square-annulus sides (mirror BuildHedgeRing / BuildMoatNavCarve): a straight
            // lip cross-section (inner wall @innerR + crest cap innerR..outerR + outer wall @outerR)
            // running along the side axis, SPLIT at the measured bridge mouth. Each side runs the full
            // outer extent [-outerR..outerR] so the corners stay covered (adjacent sides overlap there
            // — harmless for a visual mesh). sign = which side (+/-) the rim sits on.
            var sides = new (string card, bool axisAlongZ, float sign)[]
            {
                ("South", false, -1f),
                ("North", false,  1f),
                ("East",  true,   1f),
                ("West",  true,  -1f),
            };

            foreach (var (card, axisAlongZ, sign) in sides)
            {
                // Gap centre + half-width from the REAL bridge mouth (deck collider) — the SAME source
                // the hedge + navcarve gaps use, so the lip notch == the hedge gap == the carve gap ==
                // the open crossing lane. Fallback = gate-lateral symmetry (built AFTER the bridges).
                MeasureMouth(parent, card, axisAlongZ, gateLateral, out float mouthLat, out float gapHalf);

                // Two rim segments per side, flanking the bridge-mouth notch.
                AddLipSegment(verts, uvs, tris, axisAlongZ, sign, innerR, outerR, -outerR, mouthLat - gapHalf);
                AddLipSegment(verts, uvs, tris, axisAlongZ, sign, innerR, outerR, mouthLat + gapHalf, outerR);
            }

            var mesh = new Mesh { name = "MoatBasinLipMesh" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject("MoatBasinLip");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            var lipMat = ResolveMaterial(StoneMaterialResource, "CastleMoat_BasinLip", LipColor, transparent: false);
            if (lipMat != null) mr.sharedMaterial = lipMat;

            FlowTrace.Step("CastleMoat", "basin lip built: NOTCHED rim r=" + innerR + ".." + outerR + " floorY=" +
                LipFloorY + " crestY=" + LipTopY + " (" + verts.Count + " verts, MESH not TerrainData) with 4 cardinal " +
                "NOTCHES at the measured bridge mouths — the deck NEVER crosses solid lip (pose-independent); water level " +
                FixedWaterY + " sits below the crest so the channel reads CONTAINED.");
            return verts.Count;
        }

        // One straight lip cross-section over the run interval [t0..t1] on ONE side: an inner vertical
        // wall @innerR + a crest cap (innerR..outerR @LipTopY) + an outer vertical wall @outerR, all
        // double-sided. axisAlongZ => the run travels along Z at fixed X (E/W sides); else it runs along
        // X at fixed Z (S/N sides). sign = which side (+/-) the rim sits on. Skips a consumed (near-zero)
        // interval so a mouth near a corner cannot emit an inverted segment.
        private static void AddLipSegment(List<Vector3> verts, List<Vector2> uvs, List<int> tris,
            bool axisAlongZ, float sign, float innerR, float outerR, float t0, float t1)
        {
            if (t1 - t0 <= 0.25f) return;   // notch consumed this segment (mouth near the corner)
            float innerP = sign * innerR;   // perpendicular coord of the inner wall
            float outerP = sign * outerR;   // perpendicular coord of the outer wall

            // Map a (run t, perpendicular p, height y) triple to world space for this side.
            System.Func<float, float, float, Vector3> P = (t, p, y) =>
                axisAlongZ ? new Vector3(p, y, t) : new Vector3(t, y, p);

            // inner vertical wall (double-sided so it reads from the water side and the basin side)
            AddQuadTop(verts, uvs, tris, P(t0, innerP, LipFloorY), P(t0, innerP, LipTopY), P(t1, innerP, LipTopY), P(t1, innerP, LipFloorY));
            AddQuadTop(verts, uvs, tris, P(t1, innerP, LipFloorY), P(t1, innerP, LipTopY), P(t0, innerP, LipTopY), P(t0, innerP, LipFloorY));
            // outer vertical wall (double-sided — reads from the OuterWorld side)
            AddQuadTop(verts, uvs, tris, P(t0, outerP, LipFloorY), P(t0, outerP, LipTopY), P(t1, outerP, LipTopY), P(t1, outerP, LipFloorY));
            AddQuadTop(verts, uvs, tris, P(t1, outerP, LipFloorY), P(t1, outerP, LipTopY), P(t0, outerP, LipTopY), P(t0, outerP, LipFloorY));
            // crest cap at y=LipTopY, from innerR..outerR (double-sided)
            AddQuadTop(verts, uvs, tris, P(t0, innerP, LipTopY), P(t0, outerP, LipTopY), P(t1, outerP, LipTopY), P(t1, innerP, LipTopY));
            AddQuadTop(verts, uvs, tris, P(t1, innerP, LipTopY), P(t1, outerP, LipTopY), P(t0, outerP, LipTopY), P(t0, innerP, LipTopY));
        }

        // ====================================================================
        //  BRIDGE-MOUTH BANK BERMS (WO-605 owner refinement 2) — at each crossing where
        //  the deck meets the bank, a small NATURAL CURVE: a low, rounded, gently sloped
        //  raised bank on EACH side of the deck mouth. It funnels the player onto the bridge
        //  and organically seals the side gaps (no slipping off the deck edge into the water),
        //  reading as a natural berm, not a hard cut. A squashed sphere = smooth curved mound
        //  (MESH geometry, not TerrainData) whose own collider is the physical seal. Built as
        //  clones in the SOUTH frame then yaw-rotated about the origin per side (mirrors the
        //  bridges), so the pair always straddles the real deck. Parented to a SEPARATE
        //  container (NOT the bridges) so clone-parity CHECK4 is untouched.
        // ====================================================================
        private static int BuildBridgeBankBerms(Transform parent, float gateLateral)
        {
            var container = new GameObject("MoatBankBerms");
            container.transform.SetParent(parent, false);

            var bermMat = ResolveMaterial(StoneMaterialResource, "CastleMoat_Berm", BermColor, transparent: false);
            float liftY = UnityEngine.PlayerPrefs.GetFloat("castle.liftY", 3f);

            // Deck half-width (~2.15) + a hair, so a berm hugs each deck edge; the pair leaves the deck lane open.
            const float deckHalf = 2.3f;
            const float bermHalfW = 1.4f;      // berm radius across (x)
            float sideOffset = deckHalf + bermHalfW;   // lateral centre of each flanking berm

            var sides = new (string label, float yaw)[] { ("South", 0f), ("West", 90f), ("North", 180f), ("East", 270f) };
            int count = 0;
            foreach (var (label, yaw) in sides)
            {
                for (int s = -1; s <= 1; s += 2)   // left / right of the mouth
                {
                    Guard.Try("CastleMoat", "build bank berm (" + label + (s < 0 ? "_L" : "_R") + ")", () =>
                    {
                        // SOUTH frame: mouth on the castle bank at r=RampInnerRadius, lateral = gateLateral +- offset.
                        var berm = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        berm.name = "BankBerm_" + label + (s < 0 ? "_L" : "_R");
                        berm.transform.SetParent(container.transform, true);
                        berm.transform.position = new Vector3(gateLateral + s * sideOffset, liftY, -(RampInnerRadius - 1f));
                        // Squash the sphere into a low, long, rounded bank: wide-ish across, low, long along the approach.
                        berm.transform.localScale = new Vector3(bermHalfW * 2f, 2.6f, 7.5f);

                        if (bermMat != null)
                        {
                            var r = berm.GetComponent<Renderer>();
                            if (r != null) r.sharedMaterial = bermMat;
                        }

                        // SEAL: a SphereCollider mis-seals under non-uniform scale (it uses the LARGEST axis
                        // -> a ~3.75m radius from the 7.5 length would bulge onto the deck lane). Swap it for a
                        // BoxCollider matching the squashed footprint (inner edge == deck edge) so it funnels
                        // onto the bridge without intruding on the crossing.
                        var sphereCol = berm.GetComponent<Collider>();
                        if (sphereCol != null) Object.Destroy(sphereCol);
                        berm.AddComponent<BoxCollider>();   // auto-sizes to the unit-sphere bounds (1) * transform scale

                        if (Mathf.Abs(yaw) > 0.01f)
                            berm.transform.RotateAround(Vector3.zero, Vector3.up, yaw);   // carry the pair to this side
                        count++;
                    });
                }
            }

            FlowTrace.Step("CastleMoat", "bank berms built: " + count + " natural curved banks (2 per crossing, squashed-sphere " +
                "MESH + collider) flanking each deck mouth at r~" + RampInnerRadius + " lateral +-" + sideOffset.ToString("0.0") +
                " — funnels onto the bridge + seals the side gaps organically; hedge lip continues along the rest of the rim.");
            return count;
        }

        // --------------------------------------------------------------------
        //  Stone bridge prefab (all four crossings) — loaded from Resources at runtime
        //  (runtime can't AssetDatabase-load the polyperfect FBX; a Resources prefab is
        //  generated by 'Defenders > Seam > Generate Bridge Resources Prefab'). Fine
        //  placement REUSES OffsetForge (id 'bridge_south'): owner hand-tunes in play,
        //  tells the offsets, they persist in offsets.json and replay every build. Built in
        //  the SOUTH frame (analytic seat + deck collider), then yaw-rotated per side about
        //  the world origin. Returns false (no crossing) if the prefab isn't present.
        // --------------------------------------------------------------------
        private const string BridgeResourcePath = "Bridges/Bridge_Medieval_Stone";
        private const string BridgeOffsetId      = "bridge_south";
        private static OffsetTable _bridgeOffsets;
        private static bool _bridgeOffsetsLoaded;

        private static OffsetTable BridgeOffsets()
        {
            if (!_bridgeOffsetsLoaded)
            {
                _bridgeOffsetsLoaded = true;
                var ta = Resources.Load<TextAsset>("OffsetForge/offsets");
                _bridgeOffsets = OffsetTableIO.Load(ta != null ? ta.text : null);
            }
            return _bridgeOffsets;
        }

        private static bool TryPlaceBridgePrefab(Transform parent, string label, float yaw, float gateLateral)
        {
            FlowTrace.Step("CastleMoat", "TryPlaceBridgePrefab ENTER '" + label + "' (yaw=" + yaw.ToString("0") +
                ", committed-stone-material path, WebGL-safe).");
            var prefab = Resources.Load<GameObject>(BridgeResourcePath);
            if (prefab == null)
            {
                FlowTrace.Warn("CastleMoat", "no Resources/" + BridgeResourcePath +
                    " — run 'Defenders > Seam > Generate Bridge Resources Prefab'; crossing '" + label + "' skipped.");
                return false;
            }
            return Guard.Try("CastleMoat", "place stone bridge prefab (" + label + ")", () =>
            {
                var bridge = Object.Instantiate(prefab, parent);
                bridge.name = "RuntimeSeam_Bridge_" + label;

                var entry = BridgeOffsets()?.Find(BridgeOffsetId);
                if (entry != null)
                {
                    // ABSOLUTE placement from the owner's hand-tuned Inspector transform (local under
                    // the identity CastleMoat root == world). scaleXyz (3-axis) wins over uniform scale.
                    // Applied in the SOUTH frame; the per-side yaw rotation is applied at the end.
                    bridge.transform.localPosition = entry.pos.ToVector3();
                    bridge.transform.localRotation = Quaternion.Euler(entry.rot.ToVector3());
                    Vector3 s = entry.scaleXyz.ToVector3();
                    if (s == Vector3.zero) s = Vector3.one * (entry.scale > 0.0001f ? entry.scale : 1f);
                    bridge.transform.localScale = s;
                    FlowTrace.Step("CastleMoat", "RuntimeSeam_Bridge_" + label + " placed ABSOLUTE from 'bridge_south' " +
                        "(pos " + entry.pos + ", rot " + entry.rot + ", scale " + s + ").");
                }
                else
                {
                    // First pass: seat at the SOUTH gate radial so the owner can hand-tune from a
                    // sensible start; the per-side yaw rotation below carries it to this side.
                    Vector3 southCentre = Vector3.back * MoatCentreRadius + Vector3.right * gateLateral;
                    southCentre.y = BridgeY;
                    bridge.transform.position = southCentre;
                    bridge.transform.rotation = Quaternion.identity;
                    FlowTrace.Warn("CastleMoat", "no 'bridge_south' entry in offsets.json — raw first-pass transform for '" +
                        label + "'; tell me the offsets and I'll persist them.");
                }

                // WO-593 descent seat — MEASURED, pivot-agnostic (F8 2026-07-02 flag_12/flag_16): the
                // old auto-pitch rotated about the PREFAB PIVOT and blind-raised the whole bridge by
                // liftY/2, so where the geometry landed depended on the FBX pivot. Derive the seat from
                // the bridge's OWN combined renderer bounds instead:
                //   1) slide along the span so the CASTLE end sits at the plinth face (z = -RampInnerRadius);
                //   2) pitch about the OUTER-end line so the castle end rises exactly liftY — the outer
                //      end keeps the owner-verified ground seat from offsets.json.
                // All in the SOUTH frame; the per-side yaw rotation is applied last.
                float liftY = UnityEngine.PlayerPrefs.GetFloat("castle.liftY", 3f);
                Bounds bb = default; bool haveBounds = false;
                foreach (var br in bridge.GetComponentsInChildren<Renderer>(true))
                {
                    if (br == null) continue;
                    if (!haveBounds) { bb = br.bounds; haveBounds = true; }
                    else bb.Encapsulate(br.bounds);
                }
                // Walking-plane endpoints for the deck collider below — derived from the SAME seat
                // parameters, captured PRE-pitch (fleet-9500 RCA: any bounds-derived top is parapet
                // height ~7.3 because the FBX is one combined mesh; the WALKING surface is the analytic
                // plane castle-end y=liftY -> outer-end ground seat).
                bool haveWalkPlane = false;
                float walkSpan = 0f, walkOuterY = 0f, walkCenterX = 0f, walkWidth = 0f;
                if (liftY > 0.01f && haveBounds && bb.size.z > 1f)
                {
                    walkSpan = bb.size.z;
                    walkOuterY = bb.min.y;      // owner offsets.json ground seat (pre-pitch)
                    walkCenterX = bb.center.x;
                    walkWidth = bb.size.x;
                    haveWalkPlane = true;

                    // 1) castle end (the +Z face in the south frame) -> plinth face.
                    float shiftZ = -RampInnerRadius - bb.max.z;
                    bridge.transform.position += new Vector3(0f, 0f, shiftZ);

                    // 2) pitch about the OUTER-end line (world X axis through the low end) so the
                    //    castle end rises liftY over the measured span. Negative angle about +X
                    //    tilts the +Z (castle) end UP.
                    float span = bb.size.z;
                    Vector3 outerEndPivot = new Vector3(bb.center.x, bb.min.y, (-RampInnerRadius) - span);
                    float pitchDeg = -Mathf.Atan2(liftY, span) * Mathf.Rad2Deg;
                    bridge.transform.RotateAround(outerEndPivot, Vector3.right, pitchDeg);

                    FlowTrace.Step("CastleMoat", "bridge '" + label + "' seated from MEASURED bounds: span=" + span.ToString("0.0") +
                        "m, slid z " + shiftZ.ToString("0.00") + " so castle end = plinth face z=" + (-RampInnerRadius) +
                        ", pitched " + pitchDeg.ToString("0.0") + "deg about the outer end (castle end top -> y~" + liftY +
                        ", outer end r~" + (RampInnerRadius + span).ToString("0.0") + " keeps the owner offsets.json ground seat).");
                }
                else if (liftY > 0.01f)
                {
                    FlowTrace.Warn("CastleMoat", "bridge '" + label + "' bounds unmeasurable (no renderers?) — " +
                        "skipping the lift seat; bridge left at the raw offsets.json pose.");
                }

                // F8 2026-07-02/03: the Resources prefab is saved straight off the FBX so it carries NO
                // colliders, and a MeshCollider FAILS in the built player (non-accessible mesh). BOX
                // colliders from the analytic walk-plane instead: import-flag-proof, and the side rails
                // give better walk-off containment than the visual mesh. ANALYTIC walkable deck: the
                // walking surface IS the seat's own plane — castle end (z=-RampInnerRadius) at y=liftY,
                // outer end at the pre-pitch ground seat. A thin inclined world-space box whose TOP is
                // exactly that plane. Built in the SOUTH frame; carried into the side by the yaw rotate.
                if (haveWalkPlane)
                {
                    const float slabThick = 0.4f;
                    // MEASURED stone-walkway height (BridgeDeckMeasure diag 2026-07-03): the FBX walkway's
                    // dominant up-facing band is at LOCAL y=2.6 (arches fill 0->2 below, parapets at 3.4
                    // above). The slab top = the stone surface: bridge base + 2.6*scaleY, castle end
                    // lifted by the same descent pitch the seat applies.
                    const float DeckSurfaceLocalY = 2.6f;
                    float deckAboveBase = DeckSurfaceLocalY * Mathf.Abs(bridge.transform.lossyScale.y);
                    Vector3 castleEndTop = new Vector3(walkCenterX, walkOuterY + deckAboveBase + liftY + 0.05f, -RampInnerRadius);
                    Vector3 outerEndTop  = new Vector3(walkCenterX, walkOuterY + deckAboveBase + 0.05f, -RampInnerRadius - walkSpan);
                    Vector3 alongSpan = castleEndTop - outerEndTop;

                    var deckGo = new GameObject("Bridge_DeckCollider");
                    deckGo.transform.SetParent(bridge.transform, true);   // world pose is authoritative
                    deckGo.transform.rotation = Quaternion.LookRotation(alongSpan.normalized, Vector3.up);
                    deckGo.transform.position = (castleEndTop + outerEndTop) * 0.5f
                        - deckGo.transform.up * (slabThick * 0.5f);       // top face on the walking plane
                    var deck = deckGo.AddComponent<BoxCollider>();
                    deck.size = new Vector3(walkWidth, slabThick, alongSpan.magnitude);

                    // Side rails: thin boxes rising ~1.2m off the deck top — walk-off containment.
                    for (int side = -1; side <= 1; side += 2)
                    {
                        var railGo = new GameObject("Bridge_RailCollider" + (side < 0 ? "_L" : "_R"));
                        railGo.transform.SetParent(deckGo.transform, false);
                        var rail = railGo.AddComponent<BoxCollider>();
                        rail.center = new Vector3(side * walkWidth * 0.5f, slabThick * 0.5f + 0.6f, 0f);
                        rail.size = new Vector3(0.3f, 1.2f, alongSpan.magnitude);
                    }
                    FlowTrace.Step("CastleMoat", "bridge '" + label + "' deck collider built ANALYTIC: top plane " +
                        $"castle-end y={castleEndTop.y:F2} -> outer-end y={outerEndTop.y:F2}, span " +
                        $"{alongSpan.magnitude:F1}m, width {walkWidth:F1}m + 2 rails (band 44..{MoatOuterRadius} " +
                        $"stays UNDER the deck span 44..{(RampInnerRadius + walkSpan):F1}).");
                }
                else
                {
                    FlowTrace.Warn("CastleMoat", "bridge '" + label + "' — no walk-plane captured (seat skipped) — no deck collider built.");
                }

                // Color fix: polyperfect materials import as missing/white under URP (CLAUDE.md S4).
                // WO-605 Bug B: paint EVERY slot with the COMMITTED stone material (WebGL-safe). If it is
                // NULL (shader stripped + no committed asset), we MUST NOT assign a null array — that is what
                // ships the Unity-default WHITE bridge. Guarded: a failed resolve logs a Fail and LEAVES the
                // prefab's own materials intact rather than blanking them white.
                var stone = ResolveMaterial(StoneMaterialResource, "CastleMoat_BridgeStone", new Color(0.55f, 0.55f, 0.57f), transparent: false);
                if (stone == null)
                {
                    FlowTrace.Fail("CastleMoat", "bridge '" + label + "' stone material is NULL (committed load + Shader.Find both " +
                        "failed) — LEAVING the prefab's own materials so the bridge is never blanked Unity-default-white. " +
                        "Bake 'Defenders > Seam > Generate Moat Materials'.");
                }
                else
                {
                    Guard.Try("CastleMoat", "repaint bridge stone (" + label + ")", () =>
                    {
                        foreach (var r in bridge.GetComponentsInChildren<Renderer>(true))
                        {
                            if (r == null) continue;
                            int slots = (r.sharedMaterials != null && r.sharedMaterials.Length > 0) ? r.sharedMaterials.Length : 1;
                            var mats = new Material[slots];
                            for (int i = 0; i < slots; i++) mats[i] = stone;
                            r.sharedMaterials = mats;
                        }
                    });
                }

                // Owner ruling 2026-07-03 — all four crossings are this same south bridge, rigidly
                // yaw-rotated about the world origin into each side's frame (the gates ARE the south
                // gate rotated by yaw). Building in the south frame then rotating carries the analytic
                // deck-collider + rails + lift-seat (children of the bridge) into the per-side frame.
                if (Mathf.Abs(yaw) > 0.01f)
                {
                    bridge.transform.RotateAround(Vector3.zero, Vector3.up, yaw);
                    FlowTrace.Step("CastleMoat", "RuntimeSeam_Bridge_" + label + " yaw-rotated " + yaw.ToString("0") +
                        "deg about origin (south clone; deck collider + rails carried into side frame).");
                }
            });
        }

        // ====================================================================
        //  HEDGE LIP RING (owner 2026-07-03) — seal the moat's inner water edge.
        // --------------------------------------------------------------------
        //  A continuous ring of polyperfect Fence_Shrub instances hugs the inner lip
        //  (r = MoatInnerRadius - inset, on the plinth top) with FOUR GAPS aligned to
        //  the actual RuntimeSeam_Bridge_<Cardinal> mouths, so the hedge never blocks a
        //  crossing. Behind it, an invisible thin BoxCollider seal-ring (same 4 gaps) is
        //  what ACTUALLY stops the player entering the water (the water mesh has no
        //  collider). Low + web-safe: _M prefab from a committed Resources copy, shared
        //  URP/Lit hedge material (pack material is gitignored), static-batched, own
        //  per-instance colliders stripped (the seal-ring is the single authority).
        //  Runs AFTER the bridges so mouths can be measured from the real deck colliders.
        //  Sits at r~43.5 — clear of the oracle's water sampling band (r~57.5) and the
        //  bridge gaps, so VerifyMoatComplete stays MOAT_COMPLETE.
        // ====================================================================
        private static int BuildHedgeRing(Transform parent, float gateLateral)
        {
            var prefab = Resources.Load<GameObject>(HedgeResourcePath);
            if (prefab == null)
            {
                // Missing source (pack unimported on a fresh clone / not baked yet): warn + skip, never error (S4).
                Debug.LogWarning("[CastleMoat] no Resources/" + HedgeResourcePath +
                    " — run 'Defenders > Seam > Generate Hedge Resources Prefab'; hedge lip ring skipped.");
                return 0;
            }

            int placed = 0;
            Guard.Try("CastleMoat", "build hedge lip ring", () =>
            {
                float lipR  = MoatInnerRadius - HedgeLipInset;                 // ~43.5 — on the plinth top, off the water face
                float seatY = UnityEngine.PlayerPrefs.GetFloat("castle.liftY", 3f); // plinth top (bridge castle-end height)

                var container = new GameObject("MoatHedgeRing");
                container.transform.SetParent(parent, false);

                // Shared low-poly hedge material (URP/Lit) — repaints the gitignored pack material.
                var hedgeMat = BuildLitMaterial("CastleMoat_Hedge", HedgeColor, transparent: false);

                // Four sides of the square lip. axisAlongZ = the run travels along Z (fixed X);
                // else it travels along X (fixed Z). yaw orients the fence width along the run.
                var sides = new (string card, bool axisAlongZ, float fixedCoord, float yaw)[]
                {
                    ("South", false, -lipR, 0f),
                    ("North", false,  lipR, 0f),
                    ("East",  true,   lipR, 90f),
                    ("West",  true,  -lipR, 90f),
                };

                foreach (var (card, axisAlongZ, fixedCoord, yaw) in sides)
                {
                    // Gap centre + half-width from the REAL bridge mouth on this side (deck collider),
                    // so the gaps track any re-author of the bridges. Fallback = gateLateral-derived.
                    MeasureMouth(parent, card, axisAlongZ, gateLateral, out float mouthLat, out float gapHalf);

                    // -- visible hedge instances along the run, skipping the mouth gap --
                    for (float t = -lipR + HedgeSpacing * 0.5f; t <= lipR; t += HedgeSpacing)
                    {
                        if (Mathf.Abs(t - mouthLat) < gapHalf) continue;   // leave the crossing open

                        Vector3 pos = axisAlongZ
                            ? new Vector3(fixedCoord, seatY, t)
                            : new Vector3(t, seatY, fixedCoord);

                        var inst = Object.Instantiate(prefab, container.transform);
                        inst.name = "Hedge_" + card + "_" + placed;
                        inst.transform.position = pos;
                        inst.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

                        // Strip the source's own collider — the analytic seal-ring is the single authority.
                        foreach (var col in inst.GetComponentsInChildren<Collider>(true))
                            if (col != null) Object.Destroy(col);

                        // Repaint every slot with the shared hedge material (pack material is gitignored -> magenta otherwise).
                        foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
                        {
                            if (r == null) continue;
                            int slots = (r.sharedMaterials != null && r.sharedMaterials.Length > 0) ? r.sharedMaterials.Length : 1;
                            var mats = new Material[slots];
                            for (int i = 0; i < slots; i++) mats[i] = hedgeMat;
                            r.sharedMaterials = mats;
                        }
                        placed++;
                    }

                    // -- invisible seal-ring: two thin box colliders per side, flanking the gap --
                    BuildSealSegment(container.transform, card + "_A", axisAlongZ, fixedCoord, seatY, -lipR, mouthLat - gapHalf);
                    BuildSealSegment(container.transform, card + "_B", axisAlongZ, fixedCoord, seatY, mouthLat + gapHalf, lipR);
                }

                // Fold the visible hedge into as few draw calls as possible (WebGL/Pi-friendly).
                Guard.Try("CastleMoat", "static-batch hedge ring", () => StaticBatchingUtility.Combine(container));

                FlowTrace.Step("CastleMoat", "hedge lip ring built: " + placed + " Fence_Shrub instances at r=" +
                    lipR.ToString("0.0") + " (seat y=" + seatY.ToString("0.0") + "), 4 cardinal gaps at the bridge mouths, " +
                    "invisible seal-ring behind (8 box segments), static-batched.");
            });
            return placed;
        }

        // Measure the bridge-mouth lateral centre + half-gap on one side from the real deck collider.
        // mouthLat is the coordinate ALONG the run (Z for the E/W sides, X for the N/S sides).
        private static void MeasureMouth(Transform parent, string card, bool axisAlongZ, float gateLateral,
            out float mouthLat, out float gapHalf)
        {
            mouthLat = 0f;
            gapHalf = HedgeDefaultGapHalf;
            var bridgeTf = parent.Find("RuntimeSeam_Bridge_" + card);
            if (bridgeTf != null)
            {
                Vector3 c = TryCombinedBounds(bridgeTf.gameObject, out var bb) ? bb.center : bridgeTf.position;
                mouthLat = axisAlongZ ? c.z : c.x;

                // Half-gap = half the deck width + a margin, so the crossing lane stays fully open.
                var deckTf = FindDescendant(bridgeTf, "Bridge_DeckCollider");
                var deck = deckTf != null ? deckTf.GetComponent<BoxCollider>() : null;
                float deckWidth = deck != null ? deck.size.x * Mathf.Abs(deckTf.lossyScale.x) : 0f;
                if (deckWidth < 1f && bb.size != Vector3.zero) deckWidth = axisAlongZ ? bb.size.z : bb.size.x;
                gapHalf = Mathf.Max(HedgeDefaultGapHalf, deckWidth * 0.5f + HedgeGapMargin);
            }
            else
            {
                // No bridge object found — fall back to the gate-lateral symmetry (south x4).
                mouthLat = (card == "North" || card == "East") ? -gateLateral : gateLateral;
            }
        }

        // One invisible thin box collider covering [a..b] along the run axis on a side of the lip.
        private static void BuildSealSegment(Transform parent, string tag, bool axisAlongZ, float fixedCoord,
            float seatY, float a, float b)
        {
            float len = b - a;
            if (len <= 0.25f) return;   // gap consumed this segment (mouth near the corner) — nothing to seal
            float mid = (a + b) * 0.5f;

            var go = new GameObject("MoatHedgeSeal_" + tag);
            go.transform.SetParent(parent, false);
            go.transform.position = axisAlongZ
                ? new Vector3(fixedCoord, seatY + HedgeColliderHeight * 0.5f, mid)
                : new Vector3(mid, seatY + HedgeColliderHeight * 0.5f, fixedCoord);

            var box = go.AddComponent<BoxCollider>();
            box.size = axisAlongZ
                ? new Vector3(HedgeColliderThick, HedgeColliderHeight, len)
                : new Vector3(len, HedgeColliderHeight, HedgeColliderThick);
        }

        // ====================================================================
        //  MOAT NAVMESH CARVE (architect 2026-07-03) — carve the WATER BAND off the
        //  LIVE navmesh so NavMeshAgent enemies cannot SPAWN on / PATH ACROSS the water.
        // --------------------------------------------------------------------
        //  Enemies move only as NavMeshAgents (constrained to the navmesh, ignore physics
        //  colliders — same fact WallNavObstacleInstaller documents). The moat water is
        //  currently WALKABLE navmesh (nothing carves it), so agents stand on / cross the
        //  water. This lays a ring of CARVING NavMeshObstacle boxes over the water band
        //  (the SAME square annulus r=MoatInnerRadius..MoatOuterRadius the water mesh uses),
        //  with the SAME FOUR cardinal GAPS as the hedge (reusing MeasureMouth on the real
        //  bridge decks) so the bridges remain the only walkable crossings. Carving cuts the
        //  LIVE navmesh at runtime (no rebake needed); carveOnlyStationary keeps it cheap/
        //  stable (the moat never moves). Parented under a MoatNavCarve container, idempotent.
        //  Every dimension DERIVES from the moat radii + gate radials (single source of truth):
        //  each side is a strip at fixed perpendicular coord = +-MoatCentreRadius, MoatWidth
        //  deep, running the full outer extent (corners covered by the overlapping perpendicular
        //  strips), split at its measured bridge mouth. Runs AFTER the bridges so MeasureMouth
        //  reads the real deck colliders. Mirrors WallNavObstacleInstaller.TryFitObstacle.
        // ====================================================================
        private const string MoatNavCarveName = "MoatNavCarve";
        private const float  NavCarveEndMargin = 0.25f;   // shrink each carve segment a hair off its ends (bridge-lane clearance)

        private static int BuildMoatNavCarve(Transform parent, float gateLateral)
        {
            // IDEMPOTENT: never stack carve rings on a re-entrant build (BuildMoat is already
            // root-guarded, but keep the container self-guarding too).
            if (parent.Find(MoatNavCarveName) != null) return 0;

            int boxes = 0;
            Guard.Try("CastleMoat", "build moat navmesh carve", () =>
            {
                var container = new GameObject(MoatNavCarveName);
                container.transform.SetParent(parent, false);

                // Vertical span: from below the basin floor (y=0) up past the bridge deck, so the
                // carve covers the navmesh surface wherever the water sits (basin floor OR plinth
                // top), regardless of terrain wobble. The carve is XZ-footprint driven; height just
                // needs to straddle every plausible navmesh level under the band.
                float liftY       = UnityEngine.PlayerPrefs.GetFloat("castle.liftY", 3f);
                float carveBottom = LipFloorY - 1f;          // below the basin floor (y=0)
                float carveTop    = liftY + 3f;              // above the bridge deck (castle end ~liftY+deck)
                float carveHeight = carveTop - carveBottom;
                float carveY      = (carveBottom + carveTop) * 0.5f;

                // The four square-annulus sides (mirror BuildHedgeRing): a strip at fixed
                // perpendicular coord = +-MoatCentreRadius, MoatWidth deep (= 44..62), running
                // the full outer extent along its axis, split at the measured bridge mouth.
                var sides = new (string card, bool axisAlongZ, float fixedCoord)[]
                {
                    ("South", false, -MoatCentreRadius),
                    ("North", false,  MoatCentreRadius),
                    ("East",  true,   MoatCentreRadius),
                    ("West",  true,  -MoatCentreRadius),
                };

                foreach (var (card, axisAlongZ, fixedCoord) in sides)
                {
                    // Gap centre + half-width from the REAL bridge mouth on this side (deck collider),
                    // the SAME source the hedge gaps use — so the carve gap == the hedge gap == the
                    // open crossing lane. Fallback = gate-lateral symmetry when a bridge is absent.
                    MeasureMouth(parent, card, axisAlongZ, gateLateral, out float mouthLat, out float gapHalf);

                    // Two carve segments per side, flanking the bridge-mouth gap. Run the FULL outer
                    // extent (-MoatOuterRadius..MoatOuterRadius) so the corners stay covered (the
                    // perpendicular sides overlap there — harmless for carving).
                    if (TryAddCarveSegment(container.transform, card + "_A", axisAlongZ, fixedCoord, carveY, carveHeight,
                            -MoatOuterRadius, mouthLat - gapHalf)) boxes++;
                    if (TryAddCarveSegment(container.transform, card + "_B", axisAlongZ, fixedCoord, carveY, carveHeight,
                            mouthLat + gapHalf, MoatOuterRadius)) boxes++;
                }
            });

            FlowTrace.Step("CastleMoat", "moat navmesh carve: " + boxes + " carving NavMeshObstacle box(es) over the water band r=" +
                MoatInnerRadius + ".." + MoatOuterRadius + " (square annulus, " + MoatWidth + "m deep) with 4 cardinal bridge-mouth " +
                "GAPS (MeasureMouth, == the hedge gaps) — NavMeshAgent enemies can no longer spawn on / path across the water; the " +
                "bridges stay the only crossings (carving, carveOnlyStationary, LIVE navmesh, no rebake).");
            return boxes;
        }

        // One CARVING NavMeshObstacle box covering [a..b] along the run axis on a side of the
        // water band. Mirrors WallNavObstacleInstaller.TryFitObstacle (shape=Box, carving=true,
        // carveOnlyStationary=true). Identity local scale, so obstacle.size IS the world size.
        // Returns true when a box was added (skips a fully gap-consumed segment).
        private static bool TryAddCarveSegment(Transform parent, string tag, bool axisAlongZ, float fixedCoord,
            float carveY, float carveHeight, float a, float b)
        {
            float len = b - a;
            if (len <= 0.5f) return false;   // gap consumed this segment (mouth near a corner) — nothing to carve
            float mid = (a + b) * 0.5f;
            // Shrink a hair off both ends: the overlapping perpendicular strip still covers the
            // corner sliver, and the gap-side edge stays clear of the bridge lane.
            len = Mathf.Max(0.5f, len - NavCarveEndMargin * 2f);
            float bandDeep = MoatWidth;      // 44..62 = full water band depth

            var go = new GameObject("MoatNavCarve_" + tag);
            go.transform.SetParent(parent, false);
            go.transform.position = axisAlongZ
                ? new Vector3(fixedCoord, carveY, mid)
                : new Vector3(mid, carveY, fixedCoord);

            var obstacle = go.AddComponent<NavMeshObstacle>();
            obstacle.shape  = NavMeshObstacleShape.Box;
            obstacle.center = Vector3.zero;
            obstacle.size   = axisAlongZ
                ? new Vector3(bandDeep, carveHeight, len)
                : new Vector3(len, carveHeight, bandDeep);
            // Carve the LIVE navmesh so the water blocks agents without a rebake; the moat never
            // moves, so carveOnlyStationary keeps the carve cheap + stable.
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true;
            return true;
        }

        // --------------------------------------------------------------------
        //  SOUTH GATE recipe read (mirrors RuntimeRegionGate.ReadSouthGatePos) — the
        //  bridges/moat track any re-author of the gate without a code edit.
        // --------------------------------------------------------------------
        private static Vector3 ReadSouthGatePos()
        {
            Vector3 fallback = new Vector3(-4.37f, 0f, -40.6f);
            var ta = Resources.Load<TextAsset>("Data/castle-south-recipe");
            if (ta == null)
            {
                FlowTrace.Warn("CastleMoat", "castle-south-recipe not found — using fallback south gate " + fallback + ".");
                return fallback;
            }
            SouthRecipe recipe = null;
            Guard.Try("CastleMoat", "parse castle-south-recipe", () => recipe = JsonUtility.FromJson<SouthRecipe>(ta.text));
            if (recipe != null && recipe.pieces != null)
                foreach (var p in recipe.pieces)
                    if (p != null && p.name == "Gate_South" && p.pos != null && p.pos.Length == 3)
                        return new Vector3(p.pos[0], p.pos[1], p.pos[2]);
            FlowTrace.Warn("CastleMoat", "Gate_South not in recipe — using fallback " + fallback + ".");
            return fallback;
        }

        // --------------------------------------------------------------------
        //  Helpers — shared URP/Lit materials, shimmer attach.
        // --------------------------------------------------------------------

        // Build one shared URP/Lit material (the BattleArena _BaseColor pattern). Transparent
        // path sets the URP surface-type/blend keywords so alpha actually renders. Null-safe.
        private static Material BuildLitMaterial(string name, Color color, bool transparent)
        {
            Material mat = null;
            Guard.Try("CastleMoat", "build material '" + name + "'", () =>
            {
                var sh = Shader.Find("Universal Render Pipeline/Lit");
                if (sh == null) sh = Shader.Find("Standard"); // editor/non-URP fallback
                if (sh == null) return;
                mat = new Material(sh) { name = name };
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);

                if (transparent)
                {
                    // URP/Lit transparent surface (Surface=1 Transparent, alpha blend).
                    if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
                    if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
                    mat.SetOverrideTag("RenderType", "Transparent");
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    // Keep water low-gloss (de-glossed teal, owner constraint) — no glassy crystal.
                    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.25f);
                }
            });
            return mat;
        }

        // WO-605 — WebGL-safe material resolve: load a COMMITTED .mat from Resources (which forces the
        // shader into the build), INSTANCE it and stamp the per-band colour; fall back to the runtime
        // BuildLitMaterial (Shader.Find) only when the committed asset is absent (fresh clone / not baked
        // yet). Never returns silently: a fallback logs a Warn, a total miss logs a Fail so a headless run
        // pinpoints the dead material step (invisible water / white bridge).
        private static Material ResolveMaterial(string resourcePath, string name, Color color, bool transparent)
        {
            Material committed = null;
            Guard.Try("CastleMoat", "load committed material '" + resourcePath + "'", () =>
            {
                committed = Resources.Load<Material>(resourcePath);
            });
            if (committed != null)
            {
                // Instance so the per-band colour stamp never mutates the shared committed asset.
                var inst = new Material(committed) { name = name };
                if (inst.HasProperty("_BaseColor")) inst.SetColor("_BaseColor", color);
                if (inst.HasProperty("_Color")) inst.SetColor("_Color", color);
                FlowTrace.Step("CastleMoat", "material '" + name + "' from COMMITTED Resources/" + resourcePath +
                    " (WebGL-safe; shader baked into the build).");
                return inst;
            }

            FlowTrace.Warn("CastleMoat", "committed material Resources/" + resourcePath + " MISSING — falling back to runtime " +
                "Shader.Find for '" + name + "' (WebGL liability; run 'Defenders > Seam > Generate Moat Materials').");
            var mat = BuildLitMaterial(name, color, transparent);
            if (mat == null)
                FlowTrace.Fail("CastleMoat", "material '" + name + "' is NULL after committed-load AND Shader.Find fallback — " +
                    "URP/Lit shader stripped from the build; surface will render Unity-default. Bake the committed .mat.");
            return mat;
        }

        // Reuse the proven MoatWaterShimmer (DEF-195) so the ring reads as flowing water, not glass.
        // It auto-resolves the shared material from the first child renderer (the water annulus'
        // deep-band material == submesh/material index 0) and scrolls its ripple normal.
        private static void AttachShimmer(GameObject root)
        {
            Guard.Try("CastleMoat", "attach MoatWaterShimmer", () =>
            {
                root.AddComponent<DeNelle.Village.MoatWaterShimmer>();
            });
        }

        // ====================================================================
        //  MOAT COMPLETENESS ORACLE (headless, deterministic — CLAUDE.md S12).
        // --------------------------------------------------------------------
        //  Public entry a headless play/regression harness calls to PROVE the moat
        //  is COMPLETE + every seam crossing is TRAVERSABLE, so nobody eyeballs it.
        //  Reads the ACTUAL built objects (water annulus mesh triangles, the four
        //  bridge instances, their deck/rail colliders, the live navmesh) — never
        //  re-derives the same geometry it is testing. Each check logs via FlowTrace
        //  and appends a short reason to `failures`; the run ends with ONE grep-able
        //  marker: MOAT_COMPLETE (all pass) or MOAT_INCOMPLETE: <failing checks>.
        //
        //  Geometry checks (1-4) are valid edit-time OR in play. The REACHABILITY
        //  leg (5) needs a LIVE navmesh, so it self-detects an un-baked navmesh and
        //  logs INCONCLUSIVE (not a false-fail) rather than red-flagging edit-time.
        // ====================================================================
        private const string VerifySys = "MoatVerify";
        private static readonly string[] CardinalOrder = { "South", "West", "North", "East" };

        /// <summary>
        /// Headless deterministic confirmation that the castle moat is COMPLETE and every
        /// seam crossing is traversable. Returns true iff all applicable checks pass; logs
        /// each check via FlowTrace and emits a single MOAT_COMPLETE / MOAT_INCOMPLETE marker.
        /// Safe to call any time (no side-effects); the reachability leg self-skips with an
        /// INCONCLUSIVE warn when no navmesh is live (edit-time), so it never false-fails.
        /// </summary>
        public static bool VerifyMoatComplete()
        {
            var failures = new List<string>();
            FlowTrace.Step(VerifySys, "=== MOAT COMPLETENESS ORACLE START (band r=" + MoatInnerRadius + ".." +
                MoatOuterRadius + ", width=" + MoatWidth + "m) ===");

            var root = GameObject.Find(MoatRootName);
            if (root == null)
            {
                FlowTrace.Fail(VerifySys, "MOAT_ROOT_MISSING: no '" + MoatRootName + "' object — the moat build never ran.");
                FlowTrace.Fail(VerifySys, "MOAT_INCOMPLETE: moat-root-missing");
                return false;
            }

            var bridges = CollectBridges(root);

            CheckWaterRingContinuous(root, failures);
            CheckCrossingsCardinal(bridges, failures);
            CheckDecksAndRails(bridges, failures);
            CheckCloneParity(bridges, failures);
            CheckReachability(bridges, failures);
            CheckMoatCarve(root, failures);

            bool ok = failures.Count == 0;
            if (ok) FlowTrace.Step(VerifySys, "MOAT_COMPLETE");
            else FlowTrace.Fail(VerifySys, "MOAT_INCOMPLETE: " + string.Join("; ", failures));
            return ok;
        }

        // Collect the four bridge instances (direct children named RuntimeSeam_Bridge_<Cardinal>).
        private static Dictionary<string, GameObject> CollectBridges(GameObject root)
        {
            var map = new Dictionary<string, GameObject>();
            const string pfx = "RuntimeSeam_Bridge_";
            foreach (Transform t in root.transform)
            {
                if (t == null) continue;
                if (t.name.StartsWith(pfx))
                    map[t.name.Substring(pfx.Length)] = t.gameObject;
            }
            return map;
        }

        // CHECK 1 — WATER RING CONTINUOUS: sample the annulus at every 10deg (36 samples). At
        // each angle a point on the band mid-line (max-norm radius = MoatCentreRadius, so it is
        // inside 44..62 for EVERY direction incl. the diagonals) must be covered by a triangle
        // of the actual built water mesh. A real angular gap in the annulus fails, naming it.
        private static void CheckWaterRingContinuous(GameObject root, List<string> failures)
        {
            if (!Guard.Try(VerifySys, "check1 water-ring-continuous", () =>
            {
                var waterTf = root.transform.Find("MoatWater");
                var mf = waterTf != null ? waterTf.GetComponent<MeshFilter>() : null;
                var mesh = mf != null ? mf.sharedMesh : null;
                if (mesh == null)
                {
                    failures.Add("water-mesh-missing");
                    FlowTrace.Fail(VerifySys, "CHECK1 water ring: MoatWater mesh missing — no water geometry at all.");
                    return;
                }
                var verts = mesh.vertices;
                var tris = mesh.triangles;
                // Sample the DEEP band interior (max-norm between the centreline and outer edge) so
                // the probe never lands exactly on an internal sub-band seam (44..45.5 shore /
                // 45.5..53 shallow / 53..62 deep) — still inside 44..62 for EVERY direction.
                float probeMaxNorm = (MoatCentreRadius + MoatOuterRadius) * 0.5f;   // 57.5
                int gaps = 0;
                string firstGap = null;
                for (int a = 0; a < 360; a += 10)
                {
                    float rad = a * Mathf.Deg2Rad;
                    Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                    float maxc = Mathf.Max(Mathf.Abs(dir.x), Mathf.Abs(dir.y));
                    if (maxc < 1e-4f) continue;
                    Vector2 p = dir * (probeMaxNorm / maxc);   // max-norm 57.5 -> inside band 44..62 for all angles
                    if (!PointInMeshXZ(verts, tris, p))
                    {
                        gaps++;
                        if (firstGap == null) firstGap = a + "deg";
                    }
                }
                if (gaps > 0)
                {
                    failures.Add("water-ring-gap@" + firstGap + "(" + gaps + "/36)");
                    FlowTrace.Fail(VerifySys, "CHECK1 water ring: " + gaps + "/36 mid-band samples fell in a GAP (first " +
                        firstGap + ") — the annulus has an angular hole.");
                }
                else
                {
                    FlowTrace.Step(VerifySys, "CHECK1 water ring CONTINUOUS: 36/36 mid-band samples covered by the annulus mesh " +
                        "(max-norm r=" + MoatCentreRadius + ", inside band 44..62 all directions).");
                }
            })) failures.Add("check1-threw");
        }

        // CHECK 2 — CROSSINGS = 4, cardinally placed. One bridge per cardinal seam (N/E/S/W) and
        // each sits in its named quadrant (guards a mis-yawed clone). Fail if count != 4 or a
        // cardinal is missing / mis-placed.
        private static void CheckCrossingsCardinal(Dictionary<string, GameObject> bridges, List<string> failures)
        {
            if (!Guard.Try(VerifySys, "check2 crossings-cardinal", () =>
            {
                if (bridges.Count != 4)
                {
                    failures.Add("crossings-count=" + bridges.Count + "(!=4)");
                    FlowTrace.Fail(VerifySys, "CHECK2 crossings: found " + bridges.Count + " bridge(s), expected 4.");
                }
                foreach (var card in CardinalOrder)
                {
                    if (!bridges.TryGetValue(card, out var go) || go == null)
                    {
                        failures.Add("crossing-missing:" + card);
                        FlowTrace.Fail(VerifySys, "CHECK2 crossings: cardinal '" + card + "' MISSING.");
                        continue;
                    }
                    Vector3 c = TryCombinedBounds(go, out var bb) ? bb.center : go.transform.position;
                    string actual = CardinalOf(c);
                    if (actual != card)
                    {
                        failures.Add("crossing-miscardinal:" + card + "@" + actual);
                        FlowTrace.Fail(VerifySys, "CHECK2 crossings: '" + card + "' sits in the " + actual +
                            " quadrant (center=" + Fmt(c) + ") — not on its seam.");
                    }
                    else
                    {
                        FlowTrace.Step(VerifySys, "CHECK2 crossings: '" + card + "' present + in the " + card +
                            " quadrant (center=" + Fmt(c) + ").");
                    }
                }
            })) failures.Add("check2-threw");
        }

        // CHECK 3 — WALKABLE DECK, NO WALK-OFF. Each bridge carries a Bridge_DeckCollider BoxCollider
        // whose along-span covers castle-bank->world-bank across the full MoatWidth (so the player
        // can't reach the water / fall off the end), plus both side rails (L + R). Names the offender.
        private static void CheckDecksAndRails(Dictionary<string, GameObject> bridges, List<string> failures)
        {
            if (!Guard.Try(VerifySys, "check3 decks-rails", () =>
            {
                foreach (var kv in bridges)
                {
                    string label = kv.Key;
                    var go = kv.Value;
                    if (go == null) continue;

                    var deckTf = FindDescendant(go.transform, "Bridge_DeckCollider");
                    var deck = deckTf != null ? deckTf.GetComponent<BoxCollider>() : null;
                    if (deck == null)
                    {
                        failures.Add("deck-missing:" + label);
                        FlowTrace.Fail(VerifySys, "CHECK3 '" + label + "': no Bridge_DeckCollider BoxCollider — deck not walkable.");
                        continue;
                    }
                    float span = deck.size.z * Mathf.Abs(deckTf.lossyScale.z);
                    bool spanOk = span >= MoatWidth - 0.5f;
                    if (!spanOk)
                    {
                        failures.Add("deck-span:" + label + "=" + span.ToString("0.0"));
                        FlowTrace.Fail(VerifySys, "CHECK3 '" + label + "': deck span " + span.ToString("0.0") +
                            "m < MoatWidth " + MoatWidth + "m — does not cover bank->bank (walk-off risk).");
                    }

                    var railL = FindDescendant(go.transform, "Bridge_RailCollider_L");
                    var railR = FindDescendant(go.transform, "Bridge_RailCollider_R");
                    bool hasL = railL != null && railL.GetComponent<BoxCollider>() != null;
                    bool hasR = railR != null && railR.GetComponent<BoxCollider>() != null;
                    if (!hasL || !hasR)
                    {
                        failures.Add("rail-missing:" + label + (hasL ? "" : "L") + (hasR ? "" : "R"));
                        FlowTrace.Fail(VerifySys, "CHECK3 '" + label + "': missing side rail(s) (L=" + hasL + " R=" + hasR +
                            ") — no walk-off containment.");
                    }

                    if (spanOk && hasL && hasR)
                        FlowTrace.Step(VerifySys, "CHECK3 '" + label + "': deck span " + span.ToString("0.0") +
                            "m (>= " + MoatWidth + "m) covers bank->bank + 2 side rails (walk-off contained).");
                }
            })) failures.Add("check3-threw");
        }

        // CHECK 4 — CLONE PARITY. Every E/N/W bridge must match SOUTH in child transform / collider /
        // renderer counts (they are origin-yaw clones of the verified south bridge). If south is
        // correct and parity holds, all are. Names the divergent clone + the differing metric.
        private static void CheckCloneParity(Dictionary<string, GameObject> bridges, List<string> failures)
        {
            if (!Guard.Try(VerifySys, "check4 clone-parity", () =>
            {
                if (!bridges.TryGetValue("South", out var south) || south == null)
                {
                    failures.Add("parity-no-south-baseline");
                    FlowTrace.Fail(VerifySys, "CHECK4 parity: no South baseline present to compare the clones against.");
                    return;
                }
                int sT = south.GetComponentsInChildren<Transform>(true).Length;
                int sC = south.GetComponentsInChildren<Collider>(true).Length;
                int sR = south.GetComponentsInChildren<Renderer>(true).Length;
                FlowTrace.Step(VerifySys, "CHECK4 parity: South baseline transforms=" + sT + " colliders=" + sC + " renderers=" + sR + ".");
                foreach (var card in new[] { "East", "North", "West" })
                {
                    if (!bridges.TryGetValue(card, out var go) || go == null) continue;  // absence handled by CHECK2
                    int t = go.GetComponentsInChildren<Transform>(true).Length;
                    int c = go.GetComponentsInChildren<Collider>(true).Length;
                    int r = go.GetComponentsInChildren<Renderer>(true).Length;
                    if (t != sT || c != sC || r != sR)
                    {
                        failures.Add("clone-diverges:" + card + "(T" + t + "/" + sT + " C" + c + "/" + sC + " R" + r + "/" + sR + ")");
                        FlowTrace.Fail(VerifySys, "CHECK4 parity: '" + card + "' DIVERGES from South — transforms " + t + "/" + sT +
                            ", colliders " + c + "/" + sC + ", renderers " + r + "/" + sR + ".");
                    }
                    else
                    {
                        FlowTrace.Step(VerifySys, "CHECK4 parity: '" + card + "' matches South (T" + t + " C" + c + " R" + r + ").");
                    }
                }
            })) failures.Add("check4-threw");
        }

        // CHECK 5 — REACHABILITY. For each crossing, sample the castle-bank + world-bank onto the
        // live navmesh and NavMesh.CalculatePath between them; PathComplete proves a route across the
        // moat exists over the deck. Self-skips (INCONCLUSIVE warn, no fail) when no navmesh is live
        // (edit-time / OuterWorld not additively baked) so it never false-reds a geometry-only run.
        private static void CheckReachability(Dictionary<string, GameObject> bridges, List<string> failures)
        {
            if (!Guard.Try(VerifySys, "check5 reachability", () =>
            {
                bool navLiveAny = false;
                foreach (var kv in bridges)
                {
                    string label = kv.Key;
                    var go = kv.Value;
                    if (go == null) continue;

                    if (!TryBankPoints(go, label, out Vector3 castleBank, out Vector3 worldBank))
                    {
                        FlowTrace.Warn(VerifySys, "CHECK5 '" + label + "': could not derive bank endpoints — skipped.");
                        continue;
                    }
                    bool cOn = NavMesh.SamplePosition(castleBank, out NavMeshHit cHit, 6f, NavMesh.AllAreas);
                    bool wOn = NavMesh.SamplePosition(worldBank, out NavMeshHit wHit, 6f, NavMesh.AllAreas);
                    if (!cOn && !wOn)
                    {
                        FlowTrace.Warn(VerifySys, "CHECK5 '" + label + "': neither bank on a live navmesh (edit-time / OuterWorld " +
                            "not additively baked?) — reachability INCONCLUSIVE, not counted.");
                        continue;
                    }
                    navLiveAny = true;
                    if (!cOn || !wOn)
                    {
                        failures.Add("reach-bank-offmesh:" + label + (cOn ? "" : " castle") + (wOn ? "" : " world"));
                        FlowTrace.Fail(VerifySys, "CHECK5 '" + label + "': a bank is off-mesh (castleOn=" + cOn + " worldOn=" + wOn +
                            ") — cannot path across the moat.");
                        continue;
                    }
                    var path = new NavMeshPath();
                    NavMesh.CalculatePath(cHit.position, wHit.position, NavMesh.AllAreas, path);
                    int corners = path.corners != null ? path.corners.Length : 0;
                    if (path.status == NavMeshPathStatus.PathComplete)
                    {
                        FlowTrace.Step(VerifySys, "CHECK5 '" + label + "': castle-bank->world-bank PathComplete (" + corners +
                            " corners) — crossing traversable.");
                    }
                    else
                    {
                        failures.Add("reach-" + path.status + ":" + label);
                        FlowTrace.Fail(VerifySys, "CHECK5 '" + label + "': path " + path.status + " (" + corners +
                            " corners) — crossing NOT traversable across the moat.");
                    }
                }
                if (!navLiveAny)
                    FlowTrace.Warn(VerifySys, "CHECK5 reachability: no live navmesh under ANY crossing — run in a PLAY/headless " +
                        "session for the reachability leg (geometry checks 1-4 still valid).");
            })) failures.Add("check5-threw");
        }

        // CHECK 6 — WATER BAND CARVED. The moat water band must be carved OFF the live navmesh
        // (so enemies can't spawn on / path across the water) EXCEPT at the bridge-mouth gaps.
        // Sample two points on the band: a CARVED point (the SE corner of the square annulus,
        // far from every mouth) must be OFF-mesh; the SOUTH bridge LANE (measured mouth centre)
        // must be ON-mesh (the crossing stays walkable). Proves the carve structurally without an
        // eyeball. Self-skips INCONCLUSIVE (no fail) when no navmesh is live under the moat
        // (edit-time / OuterWorld not additively baked / carve not yet applied), like CHECK5.
        private static void CheckMoatCarve(GameObject root, List<string> failures)
        {
            if (!Guard.Try(VerifySys, "check6 water-band-carved", () =>
            {
                float probeY = FixedWaterY;   // mid-band height; SamplePosition searches a sphere around it

                // Bridge LANE probe — the south mouth gap, which must stay ON-mesh (walkable crossing).
                MeasureMouth(root.transform, "South", false, ReadSouthGatePos().x, out float mouthLat, out float _gap);
                Vector3 lane = new Vector3(mouthLat, probeY, -MoatCentreRadius);
                bool laneOn = NavMesh.SamplePosition(lane, out NavMeshHit _lh, 6f, NavMesh.AllAreas);

                // CARVED band probe — the SE corner of the square annulus (max-norm = MoatCentreRadius),
                // far from all four mid-side bridge mouths, so it must be OFF-mesh once carved.
                Vector3 carved = new Vector3(MoatCentreRadius, probeY, -MoatCentreRadius);
                bool carvedOn = NavMesh.SamplePosition(carved, out NavMeshHit _ch, 6f, NavMesh.AllAreas);

                if (!laneOn)
                {
                    FlowTrace.Warn(VerifySys, "CHECK6 carve: south bridge lane " + Fmt(lane) + " off-mesh — no live navmesh under " +
                        "the moat (edit-time / OuterWorld not additively baked / carve not applied yet) — water-band carve " +
                        "INCONCLUSIVE, not counted.");
                    return;
                }

                if (carvedOn)
                {
                    failures.Add("moat-band-not-carved@corner");
                    FlowTrace.Fail(VerifySys, "CHECK6 carve: the water band (SE corner " + Fmt(carved) + ") is STILL on the navmesh " +
                        "while the crossing lane is walkable — enemies can path across the water; the carve did not take.");
                }
                else
                {
                    FlowTrace.Step(VerifySys, "CHECK6 carve: water band OFF-mesh at the SE corner " + Fmt(carved) +
                        " + south bridge lane ON-mesh (mouthLat=" + mouthLat.ToString("0.0") + ") — the moat is carved and the " +
                        "crossings stay walkable.");
                }
            })) failures.Add("check6-threw");
        }

        // ---- verification helpers ------------------------------------------

        // Derive castle-bank + world-bank probe points from the bridge's OWN deck collider endpoints
        // (deck local +Z = the castle end, per the LookRotation(castleEnd-outerEnd) seat), nudged a
        // couple of metres past each end. Falls back to the cardinal radial through the band.
        private static bool TryBankPoints(GameObject bridge, string label, out Vector3 castleBank, out Vector3 worldBank)
        {
            castleBank = Vector3.zero;
            worldBank = Vector3.zero;
            var deckTf = FindDescendant(bridge.transform, "Bridge_DeckCollider");
            var deck = deckTf != null ? deckTf.GetComponent<BoxCollider>() : null;
            if (deck != null)
            {
                Vector3 fwd = deckTf.forward;                       // toward the castle end
                Vector3 c = deckTf.TransformPoint(deck.center);
                float half = deck.size.z * 0.5f * Mathf.Abs(deckTf.lossyScale.z);
                Vector3 castleEnd = c + fwd * half;
                Vector3 worldEnd = c - fwd * half;
                Vector3 fwdFlat = new Vector3(fwd.x, 0f, fwd.z).normalized;
                castleBank = castleEnd + fwdFlat * 2f; castleBank.y = castleEnd.y;
                worldBank = worldEnd - fwdFlat * 2f; worldBank.y = worldEnd.y;
                return true;
            }
            Vector2 d = CardinalDir(label);
            if (d == Vector2.zero) return false;
            castleBank = new Vector3(d.x * (MoatInnerRadius - 4f), 0f, d.y * (MoatInnerRadius - 4f));
            worldBank = new Vector3(d.x * (MoatOuterRadius + 6f), 0f, d.y * (MoatOuterRadius + 6f));
            return true;
        }

        private static Vector2 CardinalDir(string label)
        {
            switch (label)
            {
                case "South": return new Vector2(0f, -1f);
                case "North": return new Vector2(0f, 1f);
                case "East":  return new Vector2(1f, 0f);
                case "West":  return new Vector2(-1f, 0f);
            }
            return Vector2.zero;
        }

        // Quadrant of a world point (dominant horizontal axis) — maps a bridge center to a cardinal.
        private static string CardinalOf(Vector3 c)
        {
            if (Mathf.Abs(c.z) >= Mathf.Abs(c.x)) return c.z < 0f ? "South" : "North";
            return c.x < 0f ? "West" : "East";
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t != null && t.name == name) return t;
            return null;
        }

        private static bool TryCombinedBounds(GameObject go, out Bounds b)
        {
            b = default;
            bool have = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                if (!have) { b = r.bounds; have = true; }
                else b.Encapsulate(r.bounds);
            }
            return have;
        }

        // 2D (world-XZ) point-in-mesh test over every triangle — reads the built annulus geometry.
        private static bool PointInMeshXZ(Vector3[] verts, int[] tris, Vector2 p)
        {
            for (int i = 0; i + 2 < tris.Length; i += 3)
            {
                Vector2 a = new Vector2(verts[tris[i]].x, verts[tris[i]].z);
                Vector2 b = new Vector2(verts[tris[i + 1]].x, verts[tris[i + 1]].z);
                Vector2 c = new Vector2(verts[tris[i + 2]].x, verts[tris[i + 2]].z);
                if (PointInTri(p, a, b, c)) return true;
            }
            return false;
        }

        private static bool PointInTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = EdgeSign(p, a, b);
            float d2 = EdgeSign(p, b, c);
            float d3 = EdgeSign(p, c, a);
            bool neg = (d1 < 0f) || (d2 < 0f) || (d3 < 0f);
            bool pos = (d1 > 0f) || (d2 > 0f) || (d3 > 0f);
            return !(neg && pos);
        }

        private static float EdgeSign(Vector2 p1, Vector2 p2, Vector2 p3)
            => (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);

        private static string Fmt(Vector3 v)
            => "(" + v.x.ToString("0.0") + "," + v.y.ToString("0.0") + "," + v.z.ToString("0.0") + ")";

        // --------------------------------------------------------------------
        //  RECIPE MODEL (JsonUtility-friendly; mirrors RuntimeRegionGate's shape).
        // --------------------------------------------------------------------
        [System.Serializable] private class SouthPiece  { public string name; public string prefab; public float[] pos; public float[] rot; public float[] scale; }
        [System.Serializable] private class SouthRecipe { public SouthPiece[] pieces; public float[] parentPos; public float[] parentRot; }
    }
}
