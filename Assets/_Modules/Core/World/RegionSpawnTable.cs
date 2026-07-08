// =============================================================================
// RegionSpawnTable (WO-155) — the region→enemy roster as queryable data.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.World
//
// THE single source of truth for "which enemy roams which region, at what depth."
// Pure data in DeNelle.Core (no Village ref, headless-safe) so the roaming-mob
// spawner (RegionMobSpawner, DeNelle.Village) and any future world feature read
// ONE table instead of hard-coding the roster at a call site. Mirrors the owner's
// REGION_ENEMY_ROSTER.md assignment exactly:
//
//   Goldfields (E,t1) + Stoneback (W,t2)  →  Wildlands LIVING
//        Orc Raider · Wildlands Caveman · Feral Wolf
//   Mirewood (S,t3) + Ashwood (N,t4)      →  Wound-tied CORRUPTED
//        Tiefling Cultist · Necromancer of the Wound
//
// DEPTH BANDS — the roster respects the two-axis read (ZoneManager.Depth):
//   Edge  (depth < MidThreshold)  : fodder — lone Wolves / a Caveman / Cultist skirmisher.
//   Mid   (MidThreshold..CoreThreshold) : the region's standard body.
//   Core  (depth >= CoreThreshold): the heavy — Orc Raider packs / Necromancer concentrations.
// A spawn's depth band gates which roster entries are eligible, so "deeper = tougher
// enemy CHOICE" on top of ThreatLevel scaling "deeper = higher LEVEL". Ashwood core
// (tier 4 × depth 1) is the deadliest ground in the world by construction.
//
// The enemy ID strings here are the forward-design ids from docs/enemy-codex.md
// (orc-raider / caveman / feral-wolf / tiefling-cultist / necromancer). Only
// `necromancer` exists in enemies.json today; the Wildlands ids are not yet statted
// there. The spawner therefore SYNTHESISES code-built EnemyDef stat blocks per id
// (the TribeManager precedent) — this table assigns identity + region + weight, it
// does NOT re-stat enemies the JSON owns. When enemies.json gains the Wildlands
// entries, the spawner can resolve by id from the catalog with zero table change.
//
// HOLLOW ONES: deliberately NOT in this table. Per the roster doc they remain the
// village wave faction and are not roamed open-world (owner has not confirmed the
// haunted-seam gradient). Honoured by omission — see RegionMobSpawner.
// =============================================================================
using System.Collections.Generic;
using UnityEngine;

namespace DeNelle.Core.World
{
    /// <summary>How deep into a region a roster entry is eligible to spawn.</summary>
    public enum DepthBand
    {
        /// <summary>Region edge — fodder / lone skirmishers (lowest depth).</summary>
        Edge = 0,
        /// <summary>Region body — the standard mid-depth roster.</summary>
        Mid = 1,
        /// <summary>Region core — the heavies; the deadliest ground (highest depth).</summary>
        Core = 2,
    }

    /// <summary>
    /// One row of a region's roster (WO-155): an enemy def id, its pick weight, and
    /// the SHALLOWEST depth band at which it becomes eligible. An entry is eligible
    /// at its band and every band deeper (so a Core heavy never appears at the edge,
    /// but edge fodder can still appear in the core for variety). Pure data.
    /// </summary>
    public struct RegionEnemyEntry
    {
        /// <summary>enemy-codex / enemies.json def id (e.g. "orc-raider", "necromancer").</summary>
        public string EnemyId;
        /// <summary>Relative pick weight within the eligible set (higher = more common).</summary>
        public float Weight;
        /// <summary>Shallowest band this entry is eligible at (Edge ⇒ everywhere).</summary>
        public DepthBand MinBand;

        public RegionEnemyEntry(string enemyId, float weight, DepthBand minBand)
        {
            EnemyId = enemyId;
            Weight = weight;
            MinBand = minBand;
        }
    }

    /// <summary>
    /// Static region→enemy roster + the depth-banded weighted picker. Pure data,
    /// pure Core. The owner-authored REGION_ENEMY_ROSTER.md assignment, made queryable.
    /// </summary>
    public static class RegionSpawnTable
    {
        // ── Depth-band cut points (0 edge … 1 core, from ZoneManager.Depth) ───────
        /// <summary>Depth at/above which a position counts as Mid band.</summary>
        public const float MidThreshold = 0.34f;
        /// <summary>Depth at/above which a position counts as Core band.</summary>
        public const float CoreThreshold = 0.67f;

        // ── The roster (mirrors REGION_ENEMY_ROSTER.md) ───────────────────────────
        // Living regions: Wolf is edge fodder, Caveman the mid body, Orc Raider the
        // core heavy. Wound-tied regions: Tiefling Cultist the skirmisher body,
        // Necromancer the core concentration. Weights skew toward the band-typical
        // body so a region reads as "mostly its theme" with the heavy as the spike.
        private static readonly IReadOnlyDictionary<RegionId, RegionEnemyEntry[]> Roster =
            new Dictionary<RegionId, RegionEnemyEntry[]>
            {
                // Goldfields (E, t1, safest) — Wildlands living.
                { RegionId.Goldfields, new[]
                    {
                        new RegionEnemyEntry("feral-wolf",  3f, DepthBand.Edge),
                        new RegionEnemyEntry("caveman",     2f, DepthBand.Mid),
                        new RegionEnemyEntry("orc-raider",  1.2f, DepthBand.Core),
                    }
                },
                // Stoneback (W, t2) — Wildlands living (heavier than Goldfields via ThreatLevel).
                { RegionId.Stoneback, new[]
                    {
                        new RegionEnemyEntry("feral-wolf",  2.5f, DepthBand.Edge),
                        new RegionEnemyEntry("caveman",     2.5f, DepthBand.Mid),
                        new RegionEnemyEntry("orc-raider",  1.6f, DepthBand.Core),
                    }
                },
                // Mirewood (S, t3) — Wound-tied corrupted.
                { RegionId.Mirewood, new[]
                    {
                        new RegionEnemyEntry("tiefling-cultist", 3f, DepthBand.Edge),
                        new RegionEnemyEntry("necromancer",      1f, DepthBand.Core),
                    }
                },
                // Ashwood (N, t4, deadliest) — Wound-tied; the core is the worst ground in the world.
                { RegionId.Ashwood, new[]
                    {
                        new RegionEnemyEntry("tiefling-cultist", 2.5f, DepthBand.Edge),
                        new RegionEnemyEntry("necromancer",      1.6f, DepthBand.Core),
                    }
                },
            };

        /// <summary>The depth band a 0..1 depth value falls into (Edge/Mid/Core).</summary>
        public static DepthBand BandFor(float depth)
        {
            if (depth >= CoreThreshold) return DepthBand.Core;
            if (depth >= MidThreshold) return DepthBand.Mid;
            return DepthBand.Edge;
        }

        /// <summary>True when this region has a roaming roster (the four outer regions do;
        /// Village does not — it is the safe home zone / Hollow-One wave ground).</summary>
        public static bool HasRoster(RegionId region) =>
            region != RegionId.Village && Roster.ContainsKey(region);

        /// <summary>The full authored roster for a region (all bands), or null if none.</summary>
        public static RegionEnemyEntry[] RosterFor(RegionId region) =>
            Roster.TryGetValue(region, out var entries) ? entries : null;

        /// <summary>
        /// Picks a region-appropriate enemy id for a position by depth-banded weight.
        /// Resolves the region (ZoneManager) + depth band, filters the roster to the
        /// entries eligible at that band, then weighted-rolls. Returns null when the
        /// position has no roster (Village) — the caller then spawns nothing there.
        /// </summary>
        /// <param name="region">The classified region (caller passes ZoneManager.GetZone).</param>
        /// <param name="depth">0..1 depth into the region (ZoneManager.Depth).</param>
        /// <param name="rng01">A uniform 0..1 roll — caller supplies UnityEngine.Random.value.</param>
        public static string PickEnemyId(RegionId region, float depth, float rng01)
        {
            var entries = RosterFor(region);
            if (entries == null || entries.Length == 0)
            {
                // Village (no roster) is the expected path here; a NON-Village miss is an anomaly.
                if (region != RegionId.Village)
                    DeNelle.Core.Diagnostics.FlowTrace.Warn("SpawnTable", $"PickEnemyId: region '{region}' has NO roster (spawns nothing).");
                return null;
            }

            DepthBand band = BandFor(depth);

            // Sum the weight of every entry eligible at this band (MinBand <= band).
            float total = 0f;
            for (int i = 0; i < entries.Length; i++)
                if ((int)entries[i].MinBand <= (int)band)
                    total += Mathf.Max(0f, entries[i].Weight);

            if (total <= 0f)
            {
                // No band-eligible entry (e.g. an edge spawn in a region whose only
                // entry is Core-gated) — fall back to the shallowest entry so the
                // region is never empty.
                DeNelle.Core.Diagnostics.FlowTrace.Warn("SpawnTable", $"PickEnemyId('{region}',depth={depth:F2},band={band}): no band-eligible entry (total weight 0) -> shallowest fallback.");
                return ShallowestId(entries);
            }

            float roll = Mathf.Clamp01(rng01) * total;
            for (int i = 0; i < entries.Length; i++)
            {
                if ((int)entries[i].MinBand > (int)band) continue;
                roll -= Mathf.Max(0f, entries[i].Weight);
                if (roll <= 0f) return entries[i].EnemyId;
            }
            return ShallowestId(entries);
        }

        private static string ShallowestId(RegionEnemyEntry[] entries)
        {
            string best = entries[0].EnemyId;
            int bestBand = (int)entries[0].MinBand;
            for (int i = 1; i < entries.Length; i++)
            {
                if ((int)entries[i].MinBand < bestBand)
                {
                    bestBand = (int)entries[i].MinBand;
                    best = entries[i].EnemyId;
                }
            }
            return best;
        }
    }
}
