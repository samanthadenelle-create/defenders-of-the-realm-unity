// =============================================================================
// CastleMoatBuilder — FIRST-PASS diegetic WATER MOAT + 4 WIDE DRAWBRIDGES (BONES).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.World
//
// OWNER (overnight): the "you cannot go past here" castle edge should READ as DELIBERATE.
// A WIDE WATER MOAT around the castle is the natural impassable boundary ("water makes it
// make sense" vs. an invisible wall), and 4 WIDE DRAWBRIDGES at the cardinal gates are the
// intentional exits. The bridges double as defensive CHOKEPOINTS (enemies must funnel
// across them; towers/troops cover the lane; a RAISED bridge seals it) and ARE the WO-509
// four RegionGates. See docs/CASTLE_MOAT_DESIGN_NOTE.md for the full frame + sliced plan.
//
// WHAT THIS FIRST-PASS BUILDS (visual BONES only — owner finesses look/feel):
//   * A square WATER MOAT ring of translucent teal quads hugging the castle perimeter,
//     OUTSIDE the wall line, framing the playable castle island. Reuses MoatWaterShimmer
//     so the ring reads as flowing water (the proven DEF-195 component), not a glass pane.
//   * 4 WIDE wooden DRAWBRIDGE decks spanning the moat at the cardinal gates (N/E/S/W),
//     each derived from the south-recipe gate at world = Euler(0,yaw,0) * southGate so they
//     land EXACTLY on the real gate openings (the CastleHubBuilder MakeGatePose convention).
//
// WHAT THIS DOES *NOT* DO (deliberately — needs the editor/architect lane, NOT a runtime
// builder, and would break the hand-tuned navmesh/seam if guessed blind):
//   * SHRINK the castle footprint (CastleHubBuilder geometry + a navmesh re-bake).
//   * Wire the N/E/W gates as FUNCTIONAL RegionGate crossings (region-gates.json today has
//     only `castle_to_outerworld` south; RuntimeRegionGate builds one per recipe row).
//   * Raise/lower (the defensive lever) or carve the moat into the navmesh.
//   Those are specced in docs/CASTLE_MOAT_DESIGN_NOTE.md with the exact seams.
//
// BOUNDARY SOURCE (SME): the castle is bounded by CastleHubBuilder's perimeter walls/gates;
// the south gate is at castle-local (-4.37, 0, -40.6) (Resources/Data/castle-south-recipe.json),
// corner towers at radial ~42m, and the 4 cardinal gates are that south gate rotated by
// yaw {0,90,180,270} about origin (CastleHubBuilder.BuildGateExitStrips / MakeGatePose,
// CastleHubBuilder.cs:712-725). The moat sits just OUTSIDE that perimeter; the bridges sit
// ON the 4 gate radials so the only ways across the water are the gates.
//
// SAFETY: flag-gated (FeatureFlags.CastleMoat, default ON). Self-bootstrap mirrors
// OuterWorldBoundaryInjector (AfterSceneLoad + sceneLoaded re-arm). Guarded, null-safe,
// idempotent, ASCII-only, never throws out of a sceneLoaded handler (WebGL-safe), no
// gitignored packs (engine primitives + URP/Lit), mobile-cheap (shared materials).
// Instrumented per CLAUDE.md S12: FlowTrace.Step("CastleMoat", ...).
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;
using DeNelle.Core;
using DeNelle.Core.Diagnostics;
using OffsetForge;

namespace DeNelle.Village.World
{
    /// <summary>
    /// Builds the diegetic castle water moat + 4 wide drawbridge decks at runtime (BONES).
    /// First-pass visual only; flag-gated + tunable. See file header + design note.
    /// </summary>
    public static class CastleMoatBuilder
    {
        private const string MoatRootName = "CastleMoat";

        // ---- TUNABLES (BONES — owner finesses; no rebuild needed) ------------------------

        // OWNER DIRECTIVE 2026-07-02 (F8 flag_14 "no water"): the moat+water+bridge IS the
        // designed natural seam — that's why the castle was raised. The old 3m ribbon at
        // r=44.5..47.5 left the bridges crossing DRY grass for most of their span. The band
        // is now DERIVED from the island geometry, not guessed:
        //   inner edge = RampInnerRadius (44 = CastleHubBuilder.PlinthHalf mirror) so the
        //                water visibly LAPS the raised plinth face;
        //   outer edge = RampOuterRadius - 2 (58) so a 2m dry shore remains before the
        //                bridge/ramp landings (r=60) — every deck stays above the water for
        //                its whole descent (deck y at r=58 ≈ 0.16 > waterY ≈ 0.05).
        private const float MoatInnerRadius  = RampInnerRadius;                          // 44 — laps the plinth
        private const float MoatWidth        = (RampOuterRadius - 2f) - RampInnerRadius; // 14m across
        private const float MoatCentreRadius = MoatInnerRadius + MoatWidth * 0.5f;       // 51 — band centreline

        // WO-593 raise fallout (F8 2026-07-02, captured trace "4 ring quads ... @ y=-0.40"):
        // WaterY was a -0.4 CONSTANT from the WO-590 dip era. The dip is GONE —
        // ExteriorTerrainBuilder.cs:208 CastleDepressionDepth = 0f (2026-06-30) keeps the
        // OuterWorld terrain FLUSH at y=0 within +-62 of origin — so a plane at -0.4 is
        // BURIED under the terrain and no water is visible from outside. The water level is
        // now DERIVED at build time from the MEASURED outer ground (raycast just outside the
        // ring, fallback = the terrain-flush 0) + this small offset ABOVE it, so the sheet
        // renders on top of the ground and laps against the raised plinth (top = castle.liftY).
        private const float WaterAboveGround = 0.05f;

        // Terrain-flush outer ground level, sourced from ExteriorTerrainBuilder.cs:208
        // (CastleDepressionDepth = 0f -> ground held at world Y=0 inside the castle
        // footprint). Used only as the raycast fallback (e.g. OuterWorld not additive yet).
        private const float OuterGroundFallbackY = 0f;

        // Translucent teal water tint (matches the proven MoatWater look from the old village moat).
        private static readonly Color WaterColor = new Color(0.10f, 0.42f, 0.45f, 0.62f);

        // ---- WATER FILL tunables (WO-590) ------------------------------------------------
        // MEASURED DIP (instrument-first, S12 — NOT guessed): the OuterWorld terrain WRAPS
        // UNDER the castle (castle floor at world Y=0) and is SUNK within the castle footprint,
        // so a void/dip rings the island and the castle reads as FLOATING. Numbers, with source:
        //   * Dip BOTTOM Y = -3 m       -> ExteriorTerrainBuilder.cs:204 (CastleDepressionDepth=-3f),
        //                                  and :197-204 (terrain wraps under the castle, floor Y=0).
        //   * Full-depression footprint -> ExteriorTerrainBuilder.cs:193-194 (CastleClearHalfX/Z=62f):
        //                                  terrain is at -3 m out to ~+/-62 m from origin.
        //   * Taper back up to Y=0      -> ExteriorTerrainBuilder.cs:195 (CastleClearFalloff=14f): the
        //                                  terrain climbs -3 -> 0 over r=62..76, so OuterWorld ground
        //                                  returns to the castle-floor level at ~r=76 m (the far shore).
        //   * Existing moat ring        -> this file: MoatCentreRadius=46 (:63), MoatWidth=3 (:67),
        //                                  WaterY=-0.4 (:70). The fill picks up just OUTSIDE that ring.

        // Inner radius of the broad fill: start at the OUTER edge of the moat band so the
        // two water bodies do NOT overlap (overlapping coplanar transparent quads double-blend /
        // z-fight). Derived = MoatCentreRadius + MoatWidth/2 (58 with the 2026-07-02 band widen).
        private const float FillInnerRadius = MoatCentreRadius + MoatWidth * 0.5f;

        // Outer radius of the fill (dip-era; only used when the dip actually EXISTS — see
        // BuildMoat's dip probe). Derived from the ExteriorTerrainBuilder depression taper.
        private const float FillOuterRadius = 72f;

        // WO-593: the WO-590 dip-fill assumed a -3m depression. With CastleDepressionDepth = 0f
        // (ExteriorTerrainBuilder.cs:208, 2026-06-30) the ground is FLUSH — a 24m-wide fill sheet
        // riding above flush ground would read as a flood. So the fill only builds when the
        // MEASURED ground at the fill band is at least this far below the ring-side ground.
        private const float DipRequiredDepth = 0.5f;

        // Fish-school size over the fill (graceful/optional, WO-590). Capped low for the owner's Pi.
        private const int FishSchoolCount = 10;

        // ---- DRAWBRIDGE tunables ----
        // Bridge deck WIDTH (across the lane). WIDE + readable as the proper way out (owner),
        // while still a SINGLE-LANE chokepoint towers/troops can cover.
        private const float BridgeWidth = 9f;

        // Bridge length = moat width + a margin each side so the deck OVERLAPS both banks (no gap).
        private const float BridgeBankOverlap = 2.5f;

        // Bridge deck sits just above the water so it reads as laid across the channel.
        private const float BridgeY = 0.05f;

        // ---- WO-593 RAMP DECKS (N/W/E) — the raised castle needs a walkable DESCENT ---------
        // Captured proof (break-log 2026-07-02): SPAWN_TO_GATE_FAIL on all sides — courtyard nav
        // at y=liftY(3) vs outer ground y=0; the old FLAT deck at y=0.05 left a 3m cliff at each
        // gate. The SOUTH gate has the owner-verified stone bridge (OffsetForge 'bridge_south',
        // auto-pitch) — N/W/E now get a sloped cube RAMP with the SAME descent convention:
        //   high end  = the plinth edge (r = RampInnerRadius, top y = castle.liftY)
        //   low end   = the outer-ground landing (r = RampOuterRadius, y = ground)
        // RampInnerRadius mirrors CastleHubBuilder.PlinthHalf (44 — the plinth edge the ramp
        // must meet; editor const, unreachable from this runtime assembly, so mirrored with this
        // pointer). RampOuterRadius mirrors the verified south-bridge landing (~r=60 — offsets.json
        // 'bridge_south' z=-53.4 centre, WO-593 span note "plinth edge z~-44 to landing z~-60").
        private const float RampInnerRadius = 44f;   // = CastleHubBuilder.PlinthHalf (keep in sync)
        private const float RampOuterRadius = 60f;   // = south stone-bridge landing radius

        // Ticket 2026-07-02 "the edge is wrong": the ramp's outer end must SINK slightly BELOW the
        // MEASURED terrain at the landing XZ (raycast, same approach as the moat-water fix) so the
        // join reads built-into-the-earth instead of a box lip hovering above the ground. This is
        // the deck CENTRE-LINE depth below ground at the landing; with the 0.2m deck the top
        // surface ends ~0.15m under the turf. The ONLY magic constant in the ramp geometry.
        private const float RampLandingSink = 0.25f;

        // Deck slab thickness (was the cube's y scale). Curb cross-section derives from this
        // (see AddRampCurbs) so the curbs stay proportionate to the deck, never rampart-chunky.
        private const float RampDeckThickness = 0.2f;

        // Owner addendum (F8 2026-07-02): the descent ramps FUNNEL OUTWARD — gate-width at the
        // plinth end, flaring wider at the landing to gather approaching traffic into the gate
        // lane (also widens the enemy pathing mouth from the spawn arcs). Landing width =
        // BridgeWidth * this; owner asked ~1.5-2x, mid of that range.
        private const float RampLandingFlare = 1.75f;

        // Warm timber tint for the wooden drawbridge decks.
        private static readonly Color BridgeColor = new Color(0.42f, 0.28f, 0.14f, 1f);

        // The castle hub scene this builds for. (HubScenes covers variants; we additionally
        // require the south recipe to resolve so the gate radials are real.)
        private const string TargetScene = "MainCastle_Hall";

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
        /// Build the water moat ring + 4 drawbridge decks on the castle hub scene.
        /// No-op when the flag is OFF, off the hub scene, or when the moat already exists.
        /// </summary>
        public static void BuildMoat()
        {
            // Flag gate — default ON so the owner sees the BONES; PlayerPrefs "ff.castlemoat" = 0 to hide.
            if (!FeatureFlags.CastleMoat) return;

            // Only on the castle hub. MainCastle_Hall is the ACTIVE home scene, but be tolerant of
            // the loaded-additive case the same way the boundary injector is.
            Scene hub = SceneManager.GetSceneByName(TargetScene);
            bool hubActive = SceneManager.GetActiveScene().name == TargetScene;
            if (!hubActive && (!hub.IsValid() || !hub.isLoaded)) return;

            // IDEMPOTENT: a moat already present -> done (repeated loads never stack).
            if (GameObject.Find(MoatRootName) != null) return;

            var root = new GameObject(MoatRootName);

            // Gate radials come from the south recipe; the 4 cardinal gates are it rotated by yaw.
            Vector3 southGate = ReadSouthGatePos();
            float gateLateral = southGate.x;   // off-centre lateral the bridges/gates share (x4 symmetry)

            // WO-593: MEASURE the outer ground so the water level DERIVES from reality
            // (§12 — the -0.4 constant was proven buried by the trace + CastleDepressionDepth=0f).
            // Probe just OUTSIDE the band, lateral +20 off the gate lane so the invisible
            // GateExit_*_Nav strip colliders at y=liftY can't be mistaken for ground. (2026-07-02:
            // was band-centre + full width = r=65, INSIDE the terrain taper past r=62 — keep the
            // probe within the ExteriorTerrainBuilder flush zone: band outer edge + 1.5 = r=59.5.)
            float ringGroundY = MeasureGroundY(new Vector3(20f, 0f, -(MoatCentreRadius + MoatWidth * 0.5f + 1.5f)), OuterGroundFallbackY);
            float waterY      = ringGroundY + WaterAboveGround;

            Material waterMat  = BuildLitMaterial("CastleMoat_Water",  WaterColor,  transparent: true);
            Material bridgeMat = BuildLitMaterial("CastleMoat_Bridge", BridgeColor, transparent: false);

            int waterQuads = BuildWaterRing(root.transform, waterMat, waterY);
            // WO-590 dip-fill — ONLY when the dip still exists: probe the fill band centre; if the
            // ground there is not clearly below the ring-side ground (CastleDepressionDepth=0f era)
            // the broad sheet would read as a flood over flush grass, so it is skipped.
            float bandCentreR = (FillInnerRadius + FillOuterRadius) * 0.5f;
            float dipGroundY  = MeasureGroundY(new Vector3(bandCentreR, 0f, -bandCentreR), ringGroundY);
            bool  dipExists   = dipGroundY < ringGroundY - DipRequiredDepth;
            int fillQuads = dipExists ? BuildWaterFill(root.transform, waterMat, waterY) : 0;
            if (!dipExists)
                FlowTrace.Step("CastleMoat", "dip-fill SKIPPED — measured band ground y=" + dipGroundY.ToString("0.00") +
                    " vs ring ground y=" + ringGroundY.ToString("0.00") + " (no -3m dip; CastleDepressionDepth=0f, flush terrain).");
            int bridges    = BuildDrawbridges(root.transform, bridgeMat, gateLateral, ringGroundY);

            // Bring the ring to life with the proven shimmer (reuse, DEF-195). Point it at the
            // shared water material; it scrolls a procedural ripple normal across every quad
            // (ring + fill, since they share waterMat).
            AttachShimmer(root, waterMat);

            // WO-590: a small fish school over the water (graceful/optional — skips if no model).
            // 2026-07-02: the moat BAND always builds now (44..58 lapping the plinth), so the
            // school spawns over IT rather than only over the (dip-era) fill sheet.
            SpawnFishSchool(root.transform, waterY);

            // A new GameObject lands in the active scene by default; if the hub is loaded but not
            // active, move the moat into it so it shares the hub's lifetime.
            if (!hubActive && hub.IsValid() && hub.isLoaded)
                SceneManager.MoveGameObjectToScene(root, hub);

            FlowTrace.Step("CastleMoat",
                "built castle moat: " + waterQuads + " ring quads (r=" + MoatCentreRadius +
                "m width=" + MoatWidth + "m @ y=" + waterY.ToString("0.00") +
                " = measured ground " + ringGroundY.ToString("0.00") + " + " + WaterAboveGround.ToString("0.00") + ") + " +
                fillQuads + " dip-fill quads + " +
                bridges + " drawbridge decks/ramps at the 4 cardinal gates " +
                "(gateLateral=" + gateLateral.ToString("0.00") + ", source: castle-south-recipe x4 symmetry; WO-593 lift-aware).");
        }

        // --------------------------------------------------------------------
        //  WATER RING — 4 side quads forming a square channel just outside the wall.
        //  A Unity Plane is 10x10m at scale 1; we size each side to span the ring side
        //  and lay it flat at WaterY. Quads OVERLAP at the corners (cheap, no corner mesh).
        // --------------------------------------------------------------------
        private static int BuildWaterRing(Transform parent, Material mat, float waterY)
        {
            float R = MoatCentreRadius;
            // Each side runs the full span 2*R (+ overlap into the corners) and is MoatWidth deep.
            float sideLength = (R * 2f) + MoatWidth;   // + width so corners overlap, no gap

            // sides: (label, yaw) — same convention as CastleHubBuilder's ring sides.
            var sides = new (string label, float yaw)[] { ("South", 0f), ("West", 90f), ("North", 180f), ("East", 270f) };

            int count = 0;
            foreach (var (label, yaw) in sides)
            {
                Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
                Vector3 outward = rot * Vector3.back;          // -Z rotated to this side
                Vector3 sideCentre = outward * R;              // midpoint of this side, on the ring
                sideCentre.y = waterY;                          // WO-593: derived from measured ground

                var quad = GameObject.CreatePrimitive(PrimitiveType.Plane);
                quad.name = "MoatWater_" + label;
                quad.transform.SetParent(parent, false);
                quad.transform.position = sideCentre;
                quad.transform.rotation = rot;                 // length axis runs ALONG the side
                // Plane local X = width axis (across the channel), local Z = length axis (along side).
                quad.transform.localScale = new Vector3(MoatWidth / 10f, 1f, sideLength / 10f);

                StripCollider(quad);                            // water never blocks; visual only
                ApplyMaterial(quad, mat);
                count++;
            }
            return count;
        }

        // --------------------------------------------------------------------
        //  WATER FILL (WO-590) — broad square ANNULUS that fills the castle-seam DIP.
        //  4 LARGE quads (one per side) form a square water FRAME from the moat's outer
        //  edge (FillInnerRadius) out to the OuterWorld shoreline (FillOuterRadius),
        //  leaving the castle island (the centre square) clear -> minimal transparent
        //  OVERDRAW (no water plane under the island; few-big-quads, Pi-cheap). Mirrors
        //  BuildWaterRing's proven side/yaw math exactly, just with the wider band radii,
        //  so it stays concentric with the ring. Shares the moat water material -> the one
        //  MoatWaterShimmer animates it too. Visual only (colliders stripped), shared mat.
        // --------------------------------------------------------------------
        private static int BuildWaterFill(Transform parent, Material mat, float waterY)
        {
            float bandCentre = (FillInnerRadius + FillOuterRadius) * 0.5f; // centreline radius of each side band
            float bandWidth  = FillOuterRadius - FillInnerRadius;          // across-channel depth of the band
            // Mirror the ring: span the full side + the band width so corners overlap, no gap.
            float sideLength = (bandCentre * 2f) + bandWidth;

            var sides = new (string label, float yaw)[] { ("South", 0f), ("West", 90f), ("North", 180f), ("East", 270f) };

            int count = 0;
            foreach (var (label, yaw) in sides)
            {
                Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
                Vector3 outward = rot * Vector3.back;          // -Z rotated to this side
                Vector3 sideCentre = outward * bandCentre;     // midpoint of this side band
                sideCentre.y = waterY;                          // WO-593: coplanar with the ring (measured ground + offset)

                var quad = GameObject.CreatePrimitive(PrimitiveType.Plane);
                quad.name = "MoatFill_" + label;
                quad.transform.SetParent(parent, false);
                quad.transform.position = sideCentre;
                quad.transform.rotation = rot;                 // length axis runs ALONG the side
                // Plane local X = width axis (across the band), local Z = length axis (along side).
                quad.transform.localScale = new Vector3(bandWidth / 10f, 1f, sideLength / 10f);

                StripCollider(quad);                            // water never blocks; visual only
                ApplyMaterial(quad, mat);
                count++;
            }
            return count;
        }

        // --------------------------------------------------------------------
        //  FISH SCHOOL (WO-590) — a small wandering school over the SOUTH fill band
        //  (front of the castle, most visible). Graceful/optional: FishSchool loads a
        //  model from Resources, else builds a tiny primitive fish, never hard-errors.
        // --------------------------------------------------------------------
        private static void SpawnFishSchool(Transform parent, float waterY)
        {
            Guard.Try("CastleMoat", "spawn fish school", () =>
            {
                // 2026-07-02: school lives over the always-built moat BAND (44..58), south side.
                float bandCentre = MoatCentreRadius;
                float bandWidth  = MoatWidth;

                var go = new GameObject("MoatFishSchool");
                go.transform.SetParent(parent, false);
                // Centre the school over the south fill band, just below the water surface.
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
        //  DRAWBRIDGES — 4 wide wooden decks spanning the moat at the cardinal gates.
        //  Each gate world pos = Euler(0,yaw,0) * southGate; the deck centres on the moat
        //  ring radial at that gate's lateral offset and spans across the channel.
        // --------------------------------------------------------------------
        private static int BuildDrawbridges(Transform parent, Material mat, float gateLateral, float groundY)
        {
            float R = MoatCentreRadius;

            // WO-593: the castle base is raised (PlayerPrefs "castle.liftY", default 3 — same key
            // CastleHubBuilder authors the plinth/footprint from), so the N/W/E decks are RAMPS
            // descending from the plinth edge (y=liftY @ r=RampInnerRadius) to the outer-ground
            // landing (y=groundY @ r=RampOuterRadius) — the same descent the verified south stone
            // bridge makes. liftY≈0 degrades to the old flat deck across the moat channel.
            float liftY = UnityEngine.PlayerPrefs.GetFloat("castle.liftY", 3f);
            bool  ramp  = liftY > 0.01f;

            // Ticket 2026-07-02: ramps read as raw default grey — material-match the plinth stone
            // instead (resolved once; live CastleBasePlinth material when present, same-color rebuild
            // otherwise). Timber 'mat' stays for the legacy flat deck only.
            Material rampStone = ramp ? ResolvePlinthStoneMaterial() : null;

            var sides = new (string label, float yaw)[] { ("South", 0f), ("West", 90f), ("North", 180f), ("East", 270f) };

            int count = 0;
            foreach (var (label, yaw) in sides)
            {
                Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
                Vector3 outward = rot * Vector3.back;          // radial out for this side
                Vector3 along   = rot * Vector3.right;         // lateral along the side

                // Bridge centre: on the moat ring radial, shifted laterally to the gate's offset
                // (so the deck sits in front of the real gate opening, x4 symmetry).
                Vector3 centre = outward * R + along * gateLateral;
                centre.y = BridgeY;

                // Canonical SOUTH gate: place the real stone bridge prefab (OffsetForge-tuned,
                // id 'bridge_south'). Falls back to the cube deck if the Resources prefab is missing.
                if (label == "South" && TryPlaceBridgePrefab(parent, centre, rot)) { count++; continue; }

                if (ramp)
                {
                    // Sloped FUNNEL ramp (ticket + owner addendum 2026-07-02): high end meets the
                    // plinth top at gate width; low end FLARES wider and SINKS below the MEASURED
                    // terrain at the landing XZ so the join reads built-into-the-earth (no floating
                    // box lip). Landing probe sits at r=RampOuterRadius (60) — clear of the invisible
                    // GateExit_*_Nav strip colliders, which reach only ~r=58.6 (gate ~40.6 + 18).
                    Vector3 landingXZ    = outward * RampOuterRadius + along * gateLateral;
                    float landingGroundY = MeasureGroundY(landingXZ, groundY, out bool landingMeasured);

                    float span     = RampOuterRadius - RampInnerRadius;                  // horizontal run
                    float highY    = liftY + BridgeY;                                    // deck centre-line at the plinth edge
                    float lowY     = landingGroundY - RampLandingSink;                   // sunk below the measured terrain
                    float drop     = highY - lowY;                                       // vertical fall along the run
                    float slopeLen = Mathf.Sqrt(span * span + drop * drop);             // deck length along the incline
                    float midR     = (RampInnerRadius + RampOuterRadius) * 0.5f;
                    float midY     = (highY + lowY) * 0.5f;
                    float pitchDeg = -Mathf.Atan2(drop, span) * Mathf.Rad2Deg;          // same convention as the south auto-pitch

                    // Tapered deck mesh (cube can't flare): local +Z = inner/high (plinth) end at
                    // gate width, local -Z = outer/low landing end at BridgeWidth * RampLandingFlare.
                    // (Under the pitch below, local +Z tilts UP toward the plinth — proven by the
                    // owner-verified descent convention this reuses.)
                    var deck = new GameObject("Drawbridge_" + label);
                    deck.transform.SetParent(parent, false);
                    deck.transform.position = outward * midR + along * gateLateral + Vector3.up * midY;
                    // Pitch about this side's LATERAL axis so the span descends outward.
                    deck.transform.rotation = rot * Quaternion.AngleAxis(pitchDeg, Vector3.right);

                    float landingWidth = BridgeWidth * RampLandingFlare;
                    Mesh deckMesh = BuildTaperedDeckMesh(BridgeWidth, landingWidth, slopeLen, RampDeckThickness);
                    deck.AddComponent<MeshFilter>().sharedMesh = deckMesh;
                    ApplyRampMaterial(deck.AddComponent<MeshRenderer>(), rampStone);
                    // Keep a collider so the ramp reads as solid ground (the AI crossing itself is
                    // the RuntimeRegionGate NavMeshLink; this is the visible + physical descent).
                    var deckCol = deck.AddComponent<MeshCollider>();
                    deckCol.sharedMesh = deckMesh;
                    deckCol.convex = true;   // 8-corner hull, cheap + robust

                    // Slim side curbs following the flare (ticket: low curbs proportionate to the
                    // deck, not chunky wedges).
                    AddRampCurbs(deck.transform, BridgeWidth, landingWidth, slopeLen, RampDeckThickness, rampStone);

                    FlowTrace.Step("CastleMoat", "Drawbridge_" + label + " RAMP r=" + RampInnerRadius + ".." + RampOuterRadius +
                        " y=" + highY.ToString("0.0") + "->" + lowY.ToString("0.00") +
                        " (landing ground y=" + landingGroundY.ToString("0.00") +
                        " source=" + (landingMeasured ? "terrain raycast" : "fallback ring ground") +
                        ", sunk " + RampLandingSink + "m; funnel " + BridgeWidth + "->" + landingWidth.ToString("0.0") + "m)" +
                        " pitch=" + pitchDeg.ToString("0.0") + "deg (WO-593 descent, seated landing).");
                    count++;
                    continue;
                }

                // Legacy flat deck across the channel (un-raised castle).
                var flatDeck = GameObject.CreatePrimitive(PrimitiveType.Cube);
                flatDeck.name = "Drawbridge_" + label;
                flatDeck.transform.SetParent(parent, false);
                float deckLength = MoatWidth + (BridgeBankOverlap * 2f);   // overlap both banks
                flatDeck.transform.position = centre + Vector3.up * groundY;
                flatDeck.transform.rotation = rot;
                flatDeck.transform.localScale = new Vector3(BridgeWidth, RampDeckThickness, deckLength);
                ApplyMaterial(flatDeck, mat);
                count++;
            }
            return count;
        }

        // WO-593: measure the real outer-ground height by raycast (terrain/ground colliders),
        // ignoring trigger volumes. Falls back to the supplied value (the ExteriorTerrainBuilder
        // flush level) when nothing is hit — e.g. OuterWorld not additively loaded yet.
        private static float MeasureGroundY(Vector3 probeXZ, float fallbackY)
            => MeasureGroundY(probeXZ, fallbackY, out _);

        // Overload reporting the SOURCE (measured vs fallback) so the ramp FlowTrace can prove
        // where the landing y came from (ticket 2026-07-02).
        private static float MeasureGroundY(Vector3 probeXZ, float fallbackY, out bool measured)
        {
            Vector3 origin = new Vector3(probeXZ.x, 25f, probeXZ.z);
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 60f, ~0, QueryTriggerInteraction.Ignore))
            {
                measured = true;
                return hit.point.y;
            }
            measured = false;
            return fallbackY;
        }

        // --------------------------------------------------------------------
        //  RAMP GEOMETRY helpers (ticket + owner addendum 2026-07-02).
        // --------------------------------------------------------------------

        // Ticket: material-match the plinth stone — reuse the LIVE material off the editor-baked
        // "CastleBasePlinth" (CastleHubBuilder.BuildBasePlinth) when it's in the scene; otherwise
        // rebuild the identical URP/Lit stone (BaseColor 0.55,0.55,0.57 — CastleHubBuilder.cs:106,
        // the same stone the south-bridge repaint uses). No new art, no new look.
        private static Material ResolvePlinthStoneMaterial()
        {
            var plinth = GameObject.Find("CastleBasePlinth");
            var r = plinth != null ? plinth.GetComponent<Renderer>() : null;
            if (r != null && r.sharedMaterial != null) return r.sharedMaterial;
            return BuildLitMaterial("CastleMoat_RampStone", new Color(0.55f, 0.55f, 0.57f), transparent: false);
        }

        private static void ApplyRampMaterial(MeshRenderer r, Material mat)
        {
            if (r != null && mat != null) r.sharedMaterial = mat;
        }

        // Tapered (funnel) deck slab: a 6-faced trapezoidal prism in the deck's LOCAL pitched
        // frame. Local +Z = inner/high (plinth) end at innerWidth; local -Z = outer/low landing
        // end at outerWidth (the flare). Faces use duplicated vertices for crisp box shading.
        // Winding is Unity-clockwise viewed from outside (verified per-face via cross products).
        private static Mesh BuildTaperedDeckMesh(float innerWidth, float outerWidth, float length, float thickness)
        {
            float hi = innerWidth * 0.5f;   // half-width at the inner (+Z) end
            float ho = outerWidth * 0.5f;   // half-width at the outer (-Z) end
            float hz = length * 0.5f;
            float hy = thickness * 0.5f;

            var verts = new System.Collections.Generic.List<Vector3>(24);
            var tris  = new System.Collections.Generic.List<int>(36);
            void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                int i = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
                tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
                tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
            }

            // Top (+Y)
            AddQuad(new Vector3(-hi,  hy,  hz), new Vector3( hi,  hy,  hz), new Vector3( ho,  hy, -hz), new Vector3(-ho,  hy, -hz));
            // Bottom (-Y)
            AddQuad(new Vector3(-hi, -hy,  hz), new Vector3(-ho, -hy, -hz), new Vector3( ho, -hy, -hz), new Vector3( hi, -hy,  hz));
            // Inner end (+Z)
            AddQuad(new Vector3(-hi, -hy,  hz), new Vector3( hi, -hy,  hz), new Vector3( hi,  hy,  hz), new Vector3(-hi,  hy,  hz));
            // Outer end (-Z)
            AddQuad(new Vector3( ho, -hy, -hz), new Vector3(-ho, -hy, -hz), new Vector3(-ho,  hy, -hz), new Vector3( ho,  hy, -hz));
            // +X flank (slanted with the flare)
            AddQuad(new Vector3( hi, -hy,  hz), new Vector3( ho, -hy, -hz), new Vector3( ho,  hy, -hz), new Vector3( hi,  hy,  hz));
            // -X flank
            AddQuad(new Vector3(-hi, -hy,  hz), new Vector3(-hi,  hy,  hz), new Vector3(-ho,  hy, -hz), new Vector3(-ho, -hy, -hz));

            var mesh = new Mesh { name = "RampDeck_Tapered" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // Slim side CURBS following the funnel flare (ticket: low curbs proportionate to the
        // deck, not chunky wedges). Two thin cubes in the deck's LOCAL frame, each yawed to run
        // along its (straight) flared side edge; cross-section DERIVES from the deck thickness.
        private static void AddRampCurbs(Transform deck, float innerWidth, float outerWidth, float length, float thickness, Material mat)
        {
            float curbW = thickness * 1.5f;   // 0.3m wide  — proportionate to the 0.2m slab
            float curbH = thickness * 2f;     // 0.4m tall  — a low curb, not a rampart
            for (int s = 0; s < 2; s++)
            {
                float sideSign = s == 0 ? -1f : 1f;
                float xIn  = sideSign * innerWidth * 0.5f;   // edge x at the inner (+Z) end
                float xOut = sideSign * outerWidth * 0.5f;   // edge x at the outer (-Z) end
                float dx = xIn - xOut;                        // lateral change over the run (+Z-ward)
                float edgeLen = Mathf.Sqrt(dx * dx + length * length);

                var curb = GameObject.CreatePrimitive(PrimitiveType.Cube);
                curb.name = deck.name + (sideSign < 0f ? "_CurbL" : "_CurbR");
                curb.transform.SetParent(deck, false);
                // Centre on the edge midpoint, inset half a curb so it rides the deck; sit on top.
                curb.transform.localPosition = new Vector3(
                    (xIn + xOut) * 0.5f - sideSign * (curbW * 0.5f),
                    (thickness + curbH) * 0.5f,
                    0f);
                // Yaw the curb so its length axis follows the flared edge (edge runs (dx,0,length)).
                curb.transform.localRotation = Quaternion.Euler(0f, Mathf.Atan2(dx, length) * Mathf.Rad2Deg, 0f);
                curb.transform.localScale = new Vector3(curbW, curbH, edgeLen);
                ApplyMaterial(curb, mat);
            }
        }

        // --------------------------------------------------------------------
        //  Stone bridge prefab (canonical SOUTH) — loaded from Resources at runtime
        //  (runtime can't AssetDatabase-load the polyperfect FBX; a Resources prefab is
        //  generated by 'Defenders > Seam > Generate Bridge Resources Prefab'). Fine
        //  placement REUSES OffsetForge (id 'bridge_south'): owner hand-tunes in play,
        //  tells the offsets, they persist in offsets.json and replay every build.
        //  Falls back to the cube deck if the prefab isn't present.
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

        private static bool TryPlaceBridgePrefab(Transform parent, Vector3 centre, Quaternion rot)
        {
            var prefab = Resources.Load<GameObject>(BridgeResourcePath);
            if (prefab == null)
            {
                FlowTrace.Warn("CastleMoat", "no Resources/" + BridgeResourcePath +
                    " — run 'Defenders > Seam > Generate Bridge Resources Prefab'; using cube deck fallback for South.");
                return false;
            }
            return Guard.Try("CastleMoat", "place stone bridge prefab (south)", () =>
            {
                var bridge = Object.Instantiate(prefab, parent);
                bridge.name = "RuntimeSeam_Bridge_South";

                var entry = BridgeOffsets()?.Find(BridgeOffsetId);
                if (entry != null)
                {
                    // ABSOLUTE placement from the owner's hand-tuned Inspector transform (local under
                    // the identity CastleMoat root == world). scaleXyz (3-axis) wins over uniform scale.
                    bridge.transform.localPosition = entry.pos.ToVector3();
                    bridge.transform.localRotation = Quaternion.Euler(entry.rot.ToVector3());
                    Vector3 s = entry.scaleXyz.ToVector3();
                    if (s == Vector3.zero) s = Vector3.one * (entry.scale > 0.0001f ? entry.scale : 1f);
                    bridge.transform.localScale = s;
                    FlowTrace.Step("CastleMoat", "RuntimeSeam_Bridge_South placed ABSOLUTE from 'bridge_south' " +
                        "(pos " + entry.pos + ", rot " + entry.rot + ", scale " + s + ").");
                }
                else
                {
                    // First pass: seat at the gate radial so the owner can hand-tune from a sensible start.
                    bridge.transform.position = centre;
                    bridge.transform.rotation = rot;
                    FlowTrace.Warn("CastleMoat", "no 'bridge_south' entry in offsets.json — raw first-pass transform; " +
                        "tell me the offsets and I'll persist them.");
                }

                // WO-593 descent seat — MEASURED, pivot-agnostic (F8 2026-07-02 flag_12 "exit has
                // block in middle not at the bottom of ground" + flag_16 "ramp comes out touches
                // nothing"): the old auto-pitch rotated about the PREFAB PIVOT and blind-raised the
                // whole bridge by liftY/2, so where the geometry landed depended on where the FBX
                // pivot happens to sit — the captured screenshots show the deck hovering mid-gate
                // and its castle end meeting nothing. Derive the seat from the bridge's OWN combined
                // renderer bounds instead:
                //   1) slide along the span so the CASTLE end sits at the plinth face (z = -RampInnerRadius);
                //   2) pitch about the OUTER-end line so the castle end rises exactly liftY —
                //      the outer end keeps the owner-verified ground seat from offsets.json.
                float liftY = UnityEngine.PlayerPrefs.GetFloat("castle.liftY", 3f);
                Bounds bb = default; bool haveBounds = false;
                foreach (var br in bridge.GetComponentsInChildren<Renderer>(true))
                {
                    if (br == null) continue;
                    if (!haveBounds) { bb = br.bounds; haveBounds = true; }
                    else bb.Encapsulate(br.bounds);
                }
                // Walking-plane endpoints for the deck collider below — derived from the SAME
                // seat parameters, captured PRE-pitch (fleet-9500 RCA: any bounds-derived top is
                // parapet height ~7.3 because the FBX is one combined mesh; the WALKING surface
                // is the analytic plane castle-end y=liftY -> outer-end ground seat, full stop).
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
                    //    tilts the +Z (castle) end UP — same convention as the N/W/E ramps.
                    float span = bb.size.z;
                    Vector3 outerEndPivot = new Vector3(bb.center.x, bb.min.y, (-RampInnerRadius) - span);
                    float pitchDeg = -Mathf.Atan2(liftY, span) * Mathf.Rad2Deg;
                    bridge.transform.RotateAround(outerEndPivot, Vector3.right, pitchDeg);

                    FlowTrace.Step("CastleMoat", "bridge seated from MEASURED bounds: span=" + span.ToString("0.0") +
                        "m, slid z " + shiftZ.ToString("0.00") + " so castle end = plinth face z=" + (-RampInnerRadius) +
                        ", pitched " + pitchDeg.ToString("0.0") + "deg about the outer end (castle end top -> y~" + liftY +
                        ", outer end keeps the owner offsets.json ground seat).");
                }
                else if (liftY > 0.01f)
                {
                    FlowTrace.Warn("CastleMoat", "bridge bounds unmeasurable (no renderers?) — " +
                        "skipping the lift seat; bridge left at the raw offsets.json pose.");
                }

                // F8 2026-07-02 flag_14 "no colliders on bridges": the Resources prefab is saved
                // straight off the FBX (BridgePrefabGenerator) so it carries NO colliders — the deck
                // was never solid and never raycast-able (the RuntimeRegionGate descent probe and any
                // ground probe fell through it). F8 2026-07-03 (player build capture): MeshCollider
                // FAILS in the built player — "CollisionMeshData couldn't be created because the mesh
                // has been marked as non-accessible" (the FBX ships without Read/Write; editor-only
                // access masked it). BOX colliders from measured bounds instead: import-flag-proof,
                // and the side rails give BETTER walk-off containment than the visual mesh anyway.
                // ANALYTIC walkable deck (fleet-9500 RCA): ANY bounds-derived top is parapet
                // height (~7.3 — the FBX is ONE combined mesh: arches below + parapets above the
                // deck; fleet-9000's full-bounds top AND fleet-9500's largest-footprint-renderer
                // top both read 7.3), which seated the RuntimeRegionGate threshold 4m above the
                // walkway and severed the south lane (SPAWN_TO_GATE_FAIL / RUNTIME_SEAM_NAV_FAIL
                // x6, navmesh corner at y=3.20). The walking surface IS the seat's own plane:
                // castle end (z=-RampInnerRadius) at y=liftY, outer end at the pre-pitch ground
                // seat. Build a thin inclined world-space box whose TOP is exactly that plane.
                if (haveWalkPlane)
                {
                    const float slabThick = 0.4f;
                    Vector3 castleEndTop = new Vector3(walkCenterX, liftY + 0.05f, -RampInnerRadius);
                    Vector3 outerEndTop  = new Vector3(walkCenterX, walkOuterY + 0.05f, -RampInnerRadius - walkSpan);
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
                    FlowTrace.Step("CastleMoat", "bridge deck collider built ANALYTIC: top plane " +
                        $"castle-end y={castleEndTop.y:F2} -> outer-end y={outerEndTop.y:F2}, span " +
                        $"{alongSpan.magnitude:F1}m, width {walkWidth:F1}m + 2 rails (bounds-top path " +
                        "retired: the combined mesh reads parapet height, never the walkway).");
                }
                else
                {
                    FlowTrace.Warn("CastleMoat", "no walk-plane captured (seat skipped) — no deck collider built.");
                }

                // Color fix: polyperfect materials import as missing/white under URP (CLAUDE.md S4).
                // Paint EVERY material slot on every renderer — a multi-submesh mesh stays white on the
                // unpainted slots if only sharedMaterial[0] is set — with a shared stone URP/Lit material.
                var stone = BuildLitMaterial("CastleMoat_BridgeStone", new Color(0.55f, 0.55f, 0.57f), transparent: false);
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
        //  Helpers — shared URP/Lit materials, collider strip, shimmer attach.
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

        private static void ApplyMaterial(GameObject go, Material mat)
        {
            if (mat == null) return;
            var r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = mat;
        }

        private static void StripCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
        }

        // Reuse the proven MoatWaterShimmer (DEF-195) so the ring reads as flowing water, not glass.
        // It auto-resolves the shared material from a child renderer, but we point it explicitly.
        private static void AttachShimmer(GameObject root, Material waterMat)
        {
            Guard.Try("CastleMoat", "attach MoatWaterShimmer", () =>
            {
                var shimmer = root.AddComponent<DeNelle.Village.MoatWaterShimmer>();
                // MoatWaterShimmer reads its material from a child renderer if its serialized field is
                // null; all our water quads share waterMat, so the auto-resolve lands on it. (No public
                // setter is needed — the first child renderer's sharedMaterial IS waterMat.)
                _ = shimmer;
                _ = waterMat;
            });
        }

        // --------------------------------------------------------------------
        //  RECIPE MODEL (JsonUtility-friendly; mirrors RuntimeRegionGate's shape).
        // --------------------------------------------------------------------
        [System.Serializable] private class SouthPiece  { public string name; public string prefab; public float[] pos; public float[] rot; public float[] scale; }
        [System.Serializable] private class SouthRecipe { public SouthPiece[] pieces; public float[] parentPos; public float[] parentRot; }
    }
}
