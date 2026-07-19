// =============================================================================
// WildlandsRoster - the SINGLE SOURCE OF TRUTH for the overworld "Wildlands"
// enemy BASE stat blocks that are spawned OUTSIDE the village wave loop's own
// spawn path (roaming mobs, enemy outposts, camp counterattacks, and additive
// garrisons).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHY THIS EXISTS: the overworld roster ids (orc-raider, ...) were historically
// NOT in enemies.json, so FOUR different spawners each CODE-BUILT their own
// EnemyDef for the same id via a private switch - and those switches DRIFTED.
// The CombatAtbRegression divergence oracle (Check H) caught it: RegionMobSpawner
// / EnemyOutpost / CampDefenseWave stat "orc-raider" at Hp 95, while the
// GarrisonController path (GarrisonStatBlocks) stat the SAME id at base Hp 170.
// One id, two stat blocks - a raider was nearly 2x tougher depending on which
// system spawned it.
//
// THE FIX: resolve the BASE stat block from the enemies.json catalog-of-record
// (read ONCE, cached) with a byte-matching code fallback, so every spawner reads
// the SAME numbers BY CONSTRUCTION. Each spawner still applies its OWN CONTEXT
// multiplier on top (threat scale / early-game ease / garrison GlobalDifficultyMult
// / per-level scale) - the BASE is unified here; the context stays per-system.
//
// The oracle compares RegionMobSpawner.BuildRoamerDef(id, threat:0).Hp (the raw
// base) against GarrisonStatBlocks.BuildTypedDef(id, 0).Hp / GlobalDifficultyMult
// (the garrison base recovered by dividing its 1.2x global dial back out). Both
// now source their base from THIS class, so they are equal by construction.
//
// Canon: the village is Elarion (never Avalon). ASCII-only runtime strings.
// LogWarning / FlowTrace.Warn, never a hard error, if the catalog can't be read
// (the code fallback keeps spawns alive AND divergence-free).
// =============================================================================

using System;
using Newtonsoft.Json;
using DeNelle.Core;               // CanonicalJson
using DeNelle.Core.Diagnostics;   // FlowTrace

namespace DeNelle.Village
{
    /// <summary>
    /// Single source of truth for the overworld Wildlands enemy BASE stat blocks
    /// (no context multipliers applied). Backed by enemies.json (read once, cached)
    /// with a matching code fallback. See <see cref="BaseDef"/>.
    /// </summary>
    public static class WildlandsRoster
    {
        private static EnemyCatalog _catalog;
        private static bool _loaded;

        /// <summary>
        /// True when this roster owns <paramref name="id"/> as a single source (the
        /// overworld ids that are NOT part of the village wave loop's own spawn path).
        /// orc-raider is the id the CombatAtbRegression divergence oracle guards.
        /// </summary>
        public static bool Owns(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            switch (id.Trim().ToLowerInvariant())
            {
                case "orc-raider": return true;
                default:           return false;
            }
        }

        /// <summary>
        /// The canonical BASE stat block for a Wildlands id, as a FRESH
        /// <see cref="EnemyDef"/> with NO context multipliers applied. Resolves from
        /// enemies.json first (the catalog-of-record) and falls back to identical code
        /// constants when the catalog entry is absent - so every spawner reads the same
        /// base numbers whether or not the JSON is present. Callers apply their own
        /// threat / ease / global / level scaling on top.
        /// </summary>
        public static EnemyDef BaseDef(string id)
        {
            string key = string.IsNullOrEmpty(id) ? "orc-raider" : id.Trim().ToLowerInvariant();

            EnemyDef fromJson = LookupCatalog(key);
            if (fromJson != null) return Clone(fromJson);

            return Fallback(key);
        }

        private static EnemyDef LookupCatalog(string id)
        {
            EnsureLoaded();
            return _catalog != null ? _catalog.Find(id) : null;
        }

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;   // one attempt per session; a failed read stays on the fallback
            try
            {
                string json = CanonicalJson.Read(WaveDataLoader.EnemiesRelativePath);
                if (!string.IsNullOrEmpty(json))
                    _catalog = JsonConvert.DeserializeObject<EnemyCatalog>(json);
            }
            catch (Exception ex)
            {
                FlowTrace.Warn("WildlandsRoster",
                    $"enemies.json read/parse failed ({ex.GetType().Name}: {ex.Message}) - using code fallback base stats (divergence-free).");
                _catalog = null;
            }
        }

        // A fresh EnemyDef carrying the same scalar stats. Callers read scalars into
        // their own new EnemyDef, but returning a fresh object guards the cached
        // catalog entry against any accidental mutation.
        private static EnemyDef Clone(EnemyDef s)
        {
            return new EnemyDef
            {
                Id             = s.Id,
                Name           = s.Name,
                DisplayName    = s.DisplayName,
                Family         = s.Family,
                Role           = s.Role,
                Ai             = s.Ai,
                Hp             = s.Hp,
                MoveSpeed      = s.MoveSpeed,
                ContactDamage  = s.ContactDamage,
                AttackInterval = s.AttackInterval,
                Height         = s.Height,
                AggroRadius    = s.AggroRadius,
                XpReward       = s.XpReward,
                GlimmerReward  = s.GlimmerReward,
            };
        }

        // Code fallback - IDENTICAL numbers to the enemies.json orc-raider entry, so a
        // missing/unreadable catalog can NEVER reintroduce the stat divergence. Keep
        // these in sync with Assets/(Resources|StreamingAssets)/Data/Canonical/enemies.json.
        private static EnemyDef Fallback(string id)
        {
            switch (id)
            {
                case "orc-raider":
                default:
                    return new EnemyDef
                    {
                        Id             = "orc-raider",
                        Name           = "Orc Raider",
                        DisplayName    = "Orc Raider",
                        Family         = "orc",
                        Role           = "skirmisher",
                        Ai             = "charger",
                        Hp             = 130f,
                        MoveSpeed      = 3.1f,
                        ContactDamage  = 12f,
                        AttackInterval = 1.3f,
                        Height         = 2.0f,
                        AggroRadius    = 14f,
                        XpReward       = 22,
                        GlimmerReward  = 3,
                    };
            }
        }
    }
}
