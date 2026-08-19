// =============================================================================
// BiomeRoads — the hub -> PORTAL -> TUNNEL -> four BIOME DROPS topology.
// -----------------------------------------------------------------------------
// OWNER DIRECTIVE (2026-08-16, verbatim, in two beats):
//   1. "get someone creating the 4 biomes and make simple access points at far
//      corners of map"
//   2. "place a portal to simple tunnel system that will drop into the new biomes"
//
// So the shape is NOT four doors in the hub. It is a hub-and-spoke:
//
//        hub (Main_Castle_Overworld)
//              |  ONE portal  (DungeonWorldPortalSpawner authored row)
//              v
//        dg_hollow_roads          <- the "simple tunnel system": one 4-way
//              |                     Intersection + four short arms. Authored as
//              |                     PURE DATA (a dungeon-graph JSON), no new
//              |                     system, no new baker.
//        +-----+-----+-----+
//        v     v     v     v
//      N arm  E arm  S arm  W arm  <- four BIOME DROPS, one per region
//
// -----------------------------------------------------------------------------
// THE FOUR BIOMES ARE ALREADY AUTHORED. THIS FILE INVENTS NO NAMES.
//
// They are RegionId.Goldfields / Stoneback / Mirewood / Ashwood — declared in
// RegionZone.cs, tabled in ZoneManager.Regions (display name + danger tier +
// cardinal), given a neighbour graph by ZoneManager.DefaultZoneGraph, and already
// PAINTED into the ground by ExteriorTerrainBuilder as four directional terrain
// biomes (N forest / E farmland / S barren-Wound / W river valley). Every name,
// cardinal and tier below is READ from that table rather than restated, because a
// second copy of an authored set is exactly the drift CLAUDE.md sec.2 / sec.5 keep
// having to un-rot (the stale WO block, the hardcoded repo root, the retired
// asmdef table).
//
// ⚠ NOT to be confused with realm-map.json's FIVE regions (Thornwood / Mirewood /
// Hollowfrost / Emberwastes / Starfall Reach). That file is the ported React v1
// REALM-MAP progression catalog — a parchment node map, explicitly "a later-week /
// v1.1 feature", and its "biome" field is a palette tag ("forest"/"swamp"/"ice").
// It is a DIFFERENT AXIS from the four walkable cardinal regions of the merged
// overworld, and the two share the name "Mirewood" by coincidence of authorship.
// The owner asked for FOUR biomes; the four-set is the cardinal one. Named here so
// the next seat does not "reconcile" two catalogs that were never the same thing.
//
// -----------------------------------------------------------------------------
// WHY THE DROPS GO WHERE THEY GO — AND WHY NOTHING HERE IS A TYPED DISTANCE.
//
// Every drop point is DERIVED from the MEASURED world bounds handed in by the
// caller (Terrain.activeTerrain's real terrainData size), never from a typed world
// constant. This is not stylistic: the two bugs shipped on 2026-08-15 were both a
// hardcoded distance that had quietly stopped matching the geometry, and
// ZoneManager itself carries a scar comment about its Village box being sized to a
// RETIRED scene's wall footprint (42/33) long after the live castle moved to +/-44
// with gates at +/-50. A number typed here would be wrong the first time the
// terrain is re-baked at a different size, and it would be wrong SILENTLY.
//
// The drop for a region sits on that region's CARDINAL AXIS, out at
// <see cref="EdgeFraction"/> of the measured half-extent. Cardinal, not diagonal:
// ZoneManager.GetZone classifies by the DOMINANT axis, so a point on an exact
// diagonal (|x| == |z|) is a coin-flip between two regions -- it resolves to the X
// pair purely because the comparison is `>=`. Seating on the axis makes
// "which biome did I just land in" unambiguous by construction, which is the one
// property a drop point cannot afford to get wrong.
//
// MY READING OF "far corners of map", stated so the owner can correct it in one
// word: the four drops are the far EXTREMES OF THE FOUR REGIONS -- the deepest
// unambiguous point of each quadrant -- rather than the four literal diagonal
// corners of the square. If she meant the literal diagonals, change EdgeFraction's
// use in ResolveDrops to a diagonal offset; the derivation, the oracle and the
// tunnel are all unaffected.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core.World
{
    /// <summary>
    /// The single authority for the hub -> portal -> tunnel -> four-biome-drop topology.
    /// Pure logic (no scene access in the derivation half) so a headless oracle can prove
    /// the geometry without opening a scene.
    /// </summary>
    public static class BiomeRoads
    {
        private const string Sys = "BiomeRoads";

        /// <summary>
        /// The tunnel scene. MUST keep the <c>dg_</c> prefix:
        /// <see cref="DeNelle.Core.HubScenes.IsComposedDungeon"/>
        /// keys the WO-1112 hero carry off it, so a differently-named tunnel would drop the player
        /// in with a bare rig and NO abilities -- silently, which is how WO-1112's defect survived
        /// nightly play. Authored as a dungeon-graph JSON of the same id; the baker derives the
        /// scene path from graphId, so this string is the scene name AND the graph id.
        /// <para>
        /// WO-1044 R1 renamed the tunnel's DISPLAY NAME and deliberately left this ID ALONE. The id
        /// is a four-way contract -- <see cref="ArmRoomIdFor"/>, the authored graph JSON
        /// (dg_hollow_roads.json, dual copy), HollowRoadsDropInjector, and BiomeRoadsRegression --
        /// and renaming it buys a tidier string at the price of four silent breakages. IDS ARE LIVE
        /// CONTRACTS; DISPLAY NAMES ARE NOT. The id staying "hollow" while the player reads
        /// "The Rootways" is intentional, not drift.
        /// </para>
        /// </summary>
        public const string TunnelSceneId = "dg_hollow_roads";

        /// <summary>
        /// Player-facing name of the tunnel. ASCII only (mobile font atlas).
        /// <para>
        /// WO-1044 R1/R2 (owner ruling 2026-08-17, "yes to all defaults on 1044"): the tunnel is
        /// <b>The Rootways</b>, and its origin is the Heart's own roots. The rename is not taste --
        /// the retired name "The Hollow Roads" read as <i>the Hollowed's</i> roads, which promises
        /// enemies in a graph that authors ZERO encounters, so the player walked a deliberately
        /// empty crossroads and concluded the content was missing. "The Rootways" explains the
        /// quiet as lore: the Heart's song runs down there, so the rot does not; and it explains why
        /// the tunnel reaches exactly the four marches (roots reach where the wards are).
        /// </para>
        /// <para>
        /// This is the ONLY authored display-name constant in the dungeon set -- no other
        /// <c>dg_*</c> carries one -- which is why R1 costs one string and no risk.
        /// The canonical record of the name lives in canon-strings.json ("tunnelName", dual copy);
        /// this const is the Core-assembly copy the portal spawner reads, kept in step by
        /// BiomeRoadsRegression Case 7.
        /// </para>
        /// </summary>
        public const string TunnelDisplayName = "The Rootways";

        /// <summary>
        /// Fraction of the measured half-extent at which a drop sits.
        /// <para>
        /// 0.8, not 1.0 and not 0.9, for two measured reasons. (a) The terrain collider ENDS at the
        /// edge, so a drop at 1.0 is a coin-flip between the last walkable metre and a warp into
        /// the void. (b) ExteriorTerrainBuilder plants its horizon tree band in the outermost
        /// <c>HorizonBandWidth = 90</c> metres; on the live 1000m terrain that band starts at 410m,
        /// so 0.9 would drop the hero INTO the decorative treeline while 0.8 (400m on that terrain)
        /// lands just short of it, on open ground.
        /// </para>
        /// <para>
        /// It is still a FRACTION of MEASURED bounds rather than the metre count it happens to
        /// resolve to today: 400 typed here would silently stop meaning "just inside the treeline"
        /// the first time the terrain is re-baked at another size, which is precisely how
        /// ZoneManager's Village box ended up sized to a retired scene's walls.
        /// </para>
        /// <para>
        /// For reference against the authored progression: ZoneManager reaches full region depth
        /// (<c>RegionDepthSpan = 220</c>) at ~272m out, so every drop lands well past the deep-end
        /// threshold of its region -- "far", in the sense the owner asked for.
        /// </para>
        /// </summary>
        public const float EdgeFraction = 0.8f;

        /// <summary>
        /// The four regions the tunnel drops into, in tunnel-arm order (N, E, S, W). READ from
        /// <see cref="ZoneManager.Regions"/> -- this array carries ids only, never names, tiers or
        /// cardinals, so the authored table stays the one place those live.
        /// <para>
        /// <see cref="RegionId.Village"/> is deliberately absent: it is the hub you came FROM,
        /// not a biome you travel TO.
        /// </para>
        /// </summary>
        public static readonly RegionId[] DropRegions =
        {
            RegionId.Ashwood,     // North  (+Z) - tier 4, the ruined front line
            RegionId.Goldfields,  // East   (+X) - tier 1, the safe breadbasket
            RegionId.Mirewood,    // South  (-Z) - tier 3, toward the Wound
            RegionId.Stoneback,   // West   (-X) - tier 2, the stony uplands
        };

        /// <summary>
        /// One resolved drop: which region, the tunnel arm room that leads to it, the derived
        /// world point, and the human-readable derivation. The derivation string is carried on the
        /// record ON PURPOSE -- it is what a FlowTrace line prints, so a wrong drop shows its own
        /// arithmetic in the capture instead of making the next seat re-derive it by hand.
        /// </summary>
        public struct Drop
        {
            /// <summary>Which of the four authored cardinal regions this drop lands in.</summary>
            public RegionId Region;
            /// <summary>The tunnel graph node id whose far end carries this drop.</summary>
            public string ArmRoomId;
            /// <summary>Derived world position (Y comes from the caller's ground probe, not here).</summary>
            public Vector3 Point;
            /// <summary>How <see cref="Point"/> was computed, in words, for the trace.</summary>
            public string Derivation;
        }

        /// <summary>
        /// The tunnel graph node id for a drop region. Kept as a switch over the AUTHORED enum
        /// rather than a parallel array so adding a region cannot leave a silently-unmapped arm --
        /// the default case is a loud, named failure, not a fallback.
        /// </summary>
        public static string ArmRoomIdFor(RegionId id)
        {
            switch (id)
            {
                case RegionId.Ashwood:    return "arm_ashwood";
                case RegionId.Goldfields: return "arm_goldfields";
                case RegionId.Mirewood:   return "arm_mirewood";
                case RegionId.Stoneback:  return "arm_stoneback";
                default:
                    FlowTrace.Fail(Sys, $"ArmRoomIdFor('{id}') has NO tunnel arm - a region was added to " +
                                        "DropRegions without an arm in the dg_hollow_roads graph, so its drop " +
                                        "would have nowhere to stand.");
                    return "";
            }
        }

        /// <summary>
        /// The outward unit direction for a region, READ from the authored
        /// <see cref="RegionZone.Cardinal"/> string rather than re-typed as a vector here. That
        /// indirection is the whole point: the cardinal is authored once, in the region table, and
        /// a re-cardinalised region carries to this file for free. An unrecognised cardinal returns
        /// zero and says so -- it never guesses a direction.
        /// </summary>
        public static Vector3 OutwardDirection(RegionId id)
        {
            if (!ZoneManager.Regions.TryGetValue(id, out var zone) || zone == null)
            {
                FlowTrace.Fail(Sys, $"OutwardDirection: region '{id}' is not in ZoneManager.Regions - " +
                                    "cannot derive a drop direction for a region with no authored record.");
                return Vector3.zero;
            }

            string cardinal = zone.Cardinal ?? "";
            if (cardinal == "North") return new Vector3(0f, 0f, 1f);
            if (cardinal == "South") return new Vector3(0f, 0f, -1f);
            if (cardinal == "East")  return new Vector3(1f, 0f, 0f);
            if (cardinal == "West")  return new Vector3(-1f, 0f, 0f);

            FlowTrace.Fail(Sys, $"OutwardDirection: region '{id}' carries cardinal '{cardinal}', which is not " +
                                "one of North/South/East/West - no outward direction can be derived, so this " +
                                "region gets no drop rather than a guessed one.");
            return Vector3.zero;
        }

        /// <summary>
        /// PURE, headless-callable derivation: the four drop points for a MEASURED world bounds.
        /// No scene access, no Terrain reference, no UnityEngine.Random -- which is exactly what
        /// lets the oracle prove "these positions are derived, not typed" without a play session.
        /// <para>
        /// Y is left at the bounds centre height; the caller is expected to ground-probe (raycast /
        /// NavMesh.SamplePosition) before actually seating anything, because only the live scene
        /// knows where the ground is. A drop that cannot be grounded must FAIL LOUDLY at that seam
        /// rather than warping the hero into the terrain.
        /// </para>
        /// </summary>
        /// <param name="worldBounds">MEASURED world bounds (Terrain.terrainData size + position).</param>
        public static List<Drop> ResolveDrops(Bounds worldBounds)
        {
            var drops = new List<Drop>(DropRegions.Length);

            // Degenerate bounds => derive NOTHING. Returning a list of origin-points would seat all
            // four drops on top of the Heart, which reads as "placed" in every log line while being
            // completely wrong -- the exact failure mode that makes a bad position expensive.
            // ⚠ REACH IS MEASURED FROM THE WORLD ORIGIN, NOT FROM bounds.center.
            //
            // This is not a nicety, it is a correctness requirement, and the first draft of this
            // method got it wrong. ZoneManager.GetZone classifies a point against the WORLD ORIGIN
            // (its Village box is `Mathf.Abs(worldPos.x) <= VillageHalfX`, and its dominant-axis
            // split is on the raw x/z signs). If a drop is built by offsetting from bounds.center
            // instead, then the moment a terrain is baked off-centre the drop stops sitting on the
            // classifier's axis -- so "which biome did I land in" becomes ambiguous, and the arrival
            // check would compare a point derived in one frame of reference against a classifier
            // working in another. The two must share an origin, and the classifier's origin is the
            // one that cannot move.
            //
            // So the measured bounds are used for what they legitimately tell us -- HOW FAR the
            // walkable world extends in each direction from the origin -- and nothing else.
            float reachXPos = worldBounds.max.x;
            float reachXNeg = -worldBounds.min.x;
            float reachZPos = worldBounds.max.z;
            float reachZNeg = -worldBounds.min.z;

            if (reachXPos <= 1f || reachXNeg <= 1f || reachZPos <= 1f || reachZNeg <= 1f)
            {
                // WARN, not FAIL, and deliberately so: this is a PURE function that the oracle calls
                // with degenerate bounds ON PURPOSE to prove it returns nothing. A Fail here would
                // route to Debug.LogError on a PASSING regression run, and an error row emitted by a
                // green suite is exactly the noise that teaches every seat (and the F8 watcher) to
                // stop trusting error rows from this system. The real escalation belongs to the
                // CALLER, which knows whether an empty result matters -- HollowRoadsDropInjector
                // Fails loudly when it gets zero drops.
                FlowTrace.Warn(Sys, $"ResolveDrops got degenerate bounds (extents {worldBounds.extents}) - " +
                                    "no drop points derived. Suspect no active Terrain in the scene, or a " +
                                    "measurement taken before the terrain streamed in.");
                return drops;
            }

            for (int i = 0; i < DropRegions.Length; i++)
            {
                RegionId id = DropRegions[i];
                Vector3 dir = OutwardDirection(id);
                if (dir == Vector3.zero) continue;   // OutwardDirection already Failed loudly.

                // The reach ALONG THIS SPECIFIC DIRECTION, measured from the origin to the bounds
                // edge on that side -- not a shared radius, and not a half-extent. Four separate
                // numbers because a terrain that is non-square OR off-centre gives each direction a
                // different amount of room; collapsing them to one value makes two drops land short
                // of their region while the other two overshoot the walkable edge.
                float edgeReach;
                if (dir.x > 0.5f)       edgeReach = reachXPos;
                else if (dir.x < -0.5f) edgeReach = reachXNeg;
                else if (dir.z > 0.5f)  edgeReach = reachZPos;
                else                    edgeReach = reachZNeg;

                float reach = edgeReach * EdgeFraction;

                var drop = new Drop
                {
                    Region = id,
                    ArmRoomId = ArmRoomIdFor(id),
                    // Seated from the ORIGIN, on a single cardinal axis, so ZoneManager's
                    // origin-relative dominant-axis split classifies it unambiguously.
                    Point = new Vector3(dir.x * reach, worldBounds.center.y, dir.z * reach),
                    Derivation = $"{id} ({ZoneName(id)}, tier {DangerTier(id)}) = world origin + " +
                                 $"{Cardinal(id)} * ({edgeReach:0.#}m origin-to-edge reach * " +
                                 $"{EdgeFraction:0.##} edge fraction = {reach:0.#}m)",
                };
                drops.Add(drop);
            }

            if (drops.Count != DropRegions.Length)
            {
                FlowTrace.Fail(Sys, $"ResolveDrops derived only {drops.Count} of {DropRegions.Length} drops - " +
                                    "at least one region has no authored cardinal, so a tunnel arm leads nowhere. " +
                                    "A tunnel arm with no destination is a door that silently does nothing, which " +
                                    "is the defect class this whole feature is written to avoid.");
            }
            return drops;
        }

        /// <summary>Display name of a region, read from the authored table (never restated here).</summary>
        public static string ZoneName(RegionId id)
            => ZoneManager.Regions.TryGetValue(id, out var z) && z != null ? z.DisplayName : id.ToString();

        /// <summary>Cardinal of a region, read from the authored table.</summary>
        public static string Cardinal(RegionId id)
            => ZoneManager.Regions.TryGetValue(id, out var z) && z != null ? z.Cardinal : "";

        /// <summary>Danger tier of a region, read from the authored table.</summary>
        public static int DangerTier(RegionId id)
            => ZoneManager.Regions.TryGetValue(id, out var z) && z != null ? z.DangerTier : 0;

        /// <summary>
        /// Player-facing travel label for a drop. ASCII only. Carries the region's DANGER TIER in
        /// words, because the four regions differ by a factor of four in threat
        /// (ZoneManager.ThreatLevel = 5 * tier + depth band) and a tunnel mouth that reads the same
        /// for Goldfields and Ashwood is an unsignposted difficulty cliff.
        /// </summary>
        public static string TravelLabel(RegionId id)
        {
            string name = ZoneName(id);
            switch (DangerTier(id))
            {
                case 1:  return $"Travel to {name} (calm)";
                case 2:  return $"Travel to {name} (uneasy)";
                case 3:  return $"Travel to {name} (dangerous)";
                case 4:  return $"Travel to {name} (deadly)";
                default: return $"Travel to {name}";
            }
        }

        /// <summary>
        /// MEASURE the live world bounds from the active Terrain(s). Returns false -- loudly -- when
        /// there is nothing to measure, so a caller can decline to place rather than fall back to a
        /// typed extent. THE FALLBACK IS DELIBERATELY ABSENT: a hardcoded 1000x1000 here would make
        /// this file read as "derived" while behaving as "typed" the moment the terrain failed to
        /// load, which is strictly worse than not placing at all.
        /// </summary>
        public static bool TryMeasureWorldBounds(out Bounds bounds)
        {
            bounds = default;
            bool any = false;

            var terrains = UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            if (terrains == null || terrains.Length == 0)
            {
                FlowTrace.Fail(Sys, "TryMeasureWorldBounds found NO active Terrain - world extent cannot be " +
                                    "measured, so no biome drop can be derived. Nothing is placed (there is no " +
                                    "typed fallback on purpose).");
                return false;
            }

            for (int i = 0; i < terrains.Length; i++)
            {
                var t = terrains[i];
                if (t == null || t.terrainData == null) continue;
                Vector3 size = t.terrainData.size;
                Vector3 origin = t.GetPosition();
                var b = new Bounds(origin + size * 0.5f, size);
                if (!any) { bounds = b; any = true; } else bounds.Encapsulate(b);
            }

            if (!any)
            {
                FlowTrace.Fail(Sys, $"TryMeasureWorldBounds saw {terrains.Length} Terrain object(s) but none " +
                                    "carried terrainData - extent unmeasurable, no drops derived.");
                return false;
            }

            FlowTrace.Step(Sys, $"world bounds MEASURED from {terrains.Length} terrain(s): centre {bounds.center} " +
                                $"size {bounds.size} (half-extents {bounds.extents}) - every drop below is derived " +
                                "from this, not typed.");
            return true;
        }
    }
}
