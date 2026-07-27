// =============================================================================
// TroopStatResolver — pure, side-effect-free effective-stat resolver for troops,
// plus the two canonical-JSON catalog loaders (WO-771.9 foundation half).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Given a TroopDef (its baseline stats from troops.json) and an upgrade LEVEL,
// TroopStatResolver.Effective(...) folds in the troop's authored upgrade curves
// (troop-upgrades.json) and returns a TroopRuntimeStats snapshot:
//   - Reach curve    -> scales AttackRange + AggroRadius (HuntScanRadius)
//   - Strength curve -> scales MaxHp + DPS (and AttackDamage)
//   - SpecialAbilities with LevelThreshold <= level are collected as unlocked
// A troop with NO upgrade row (or level 1 on a 1.0-baseline curve) resolves to
// its PURE BASELINE — every multiplier is 1x and no abilities unlock.
//
// PURITY: Effective() reads only cached catalog data + the passed def/level and
// allocates a fresh result. It mutates nothing on the def or global state. The
// catalog load is a one-time memoized read (same contract as TroopCatalog) — the
// resolver never writes anything the caller can observe.
//
// The BarracksCatalog + TroopUpgradeCatalog loaders below mirror TroopCatalog /
// AbilityCatalog exactly (CanonicalJson: Resources first, StreamingAssets fallback).
// =============================================================================

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Immutable snapshot of a troop's EFFECTIVE combat stats at a given upgrade
    /// level — baseline (troops.json) with the upgrade curves (troop-upgrades.json)
    /// folded in. Produced by <see cref="TroopStatResolver.Effective"/>; plain data.
    /// </summary>
    public sealed class TroopRuntimeStats
    {
        /// <summary>The troop id these stats belong to (echoes the def id).</summary>
        public string TroopId;
        /// <summary>The upgrade level these stats were resolved at (1-based, clamped &gt;= 1).</summary>
        public int Level;

        /// <summary>Effective attack reach (baseline AttackRange * reach curve).</summary>
        public float AttackRange;
        /// <summary>Effective aggro/scan radius (baseline HuntScanRadius * reach curve).</summary>
        public float AggroRadius;

        /// <summary>Effective max HP (baseline MaxHp * strength curve).</summary>
        public float MaxHp;
        /// <summary>Effective per-hit attack damage (baseline AttackDamage * strength curve).</summary>
        public float AttackDamage;
        /// <summary>Seconds between attacks (unchanged from baseline — the curves scale power, not cadence).</summary>
        public float AttackCooldown;
        /// <summary>Effective sustained damage-per-second (baseline DPS * strength curve).</summary>
        public float Dps;
        /// <summary>Move speed (unchanged from baseline in the foundation model).</summary>
        public float MoveSpeed;

        /// <summary>The reach multiplier applied (1.0 == baseline / no upgrade).</summary>
        public float ReachMultiplier;
        /// <summary>The strength multiplier applied (1.0 == baseline / no upgrade).</summary>
        public float StrengthMultiplier;

        /// <summary>Abilities unlocked at or below <see cref="Level"/> (empty when none).</summary>
        public List<AbilityUnlock> UnlockedAbilities = new List<AbilityUnlock>();
    }

    /// <summary>
    /// Pure resolver: fold a troop's upgrade curves into its baseline def at a
    /// level. Side-effect-free — safe to call anywhere, any number of times.
    /// </summary>
    public static class TroopStatResolver
    {
        /// <summary>
        /// Resolves a troop's effective stats at <paramref name="level"/>. When
        /// <paramref name="def"/> is null returns an empty baseline snapshot; when the
        /// troop has no upgrade row every multiplier is 1x (pure baseline).
        /// </summary>
        public static TroopRuntimeStats Effective(TroopDef def, int level)
        {
            int lvl = Mathf.Max(1, level);

            if (def == null)
            {
                return new TroopRuntimeStats
                {
                    TroopId = null,
                    Level = lvl,
                    AttackRange = 0f,
                    AggroRadius = 0f,
                    MaxHp = 0f,
                    AttackDamage = 0f,
                    AttackCooldown = 0f,
                    Dps = 0f,
                    MoveSpeed = 0f,
                    ReachMultiplier = 1f,
                    StrengthMultiplier = 1f,
                    UnlockedAbilities = new List<AbilityUnlock>(),
                };
            }

            TroopUpgradeDef upg = TroopUpgradeCatalog.Find(def.Id);

            // StatCurve.Get already returns 1f for a null/empty curve, and a null
            // upgrade def means both curves are treated as flat 1x -> pure baseline.
            float reach = upg?.Reach?.Get(lvl) ?? 1f;
            float strength = upg?.Strength?.Get(lvl) ?? 1f;

            float baseDps = def.AttackCooldown > 0f
                ? def.AttackDamage / def.AttackCooldown
                : def.AttackDamage;

            var stats = new TroopRuntimeStats
            {
                TroopId = def.Id,
                Level = lvl,
                AttackRange = def.AttackRange * reach,
                AggroRadius = def.HuntScanRadius * reach,
                MaxHp = def.MaxHp * strength,
                AttackDamage = def.AttackDamage * strength,
                AttackCooldown = def.AttackCooldown,
                Dps = baseDps * strength,
                MoveSpeed = def.MoveSpeed,
                ReachMultiplier = reach,
                StrengthMultiplier = strength,
                UnlockedAbilities = new List<AbilityUnlock>(),
            };

            if (upg?.SpecialAbilities != null)
            {
                foreach (var ability in upg.SpecialAbilities)
                {
                    if (ability != null && ability.LevelThreshold <= lvl)
                        stats.UnlockedAbilities.Add(ability);
                }
            }

            return stats;
        }
    }

    /// <summary>
    /// Static surface over the canonical troop-upgrades.json — loads + caches the
    /// per-troop upgrade progressions. Mirrors TroopCatalog's loading strategy.
    /// </summary>
    public static class TroopUpgradeCatalog
    {
        private const string StreamingRelativePath = "Data/Canonical/troop-upgrades.json";

        private static TroopUpgradeCatalogData _data;

        /// <summary>All authored troop upgrade defs (may be empty on a load failure).</summary>
        public static IReadOnlyList<TroopUpgradeDef> All
        {
            get { EnsureLoaded(); return _data.Upgrades; }
        }

        /// <summary>Looks up a troop's upgrade def by troop id. Returns null when absent.</summary>
        public static TroopUpgradeDef Find(string troopId)
        {
            if (string.IsNullOrEmpty(troopId)) return null;
            EnsureLoaded();
            foreach (var upg in _data.Upgrades)
                if (upg != null && upg.TroopId == troopId) return upg;
            return null;
        }

        /// <summary>Forces a re-read of troop-upgrades.json (used by tests / the Monday sync).</summary>
        public static void Reload()
        {
            _data = null;
            EnsureLoaded();
        }

        private static void EnsureLoaded()
        {
            if (_data != null) return;
            _data = Load();
        }

        private static TroopUpgradeCatalogData Load()
        {
            try
            {
                string json = DeNelle.Core.CanonicalJson.Read(StreamingRelativePath);
                if (!string.IsNullOrEmpty(json))
                {
                    var parsed = JsonConvert.DeserializeObject<TroopUpgradeCatalogData>(json);
                    if (parsed != null && parsed.Upgrades != null)
                        return parsed;
                    Debug.LogError("[TroopUpgradeCatalog] troop-upgrades.json parsed empty.");
                }
                else
                {
                    Debug.LogError("[TroopUpgradeCatalog] troop-upgrades.json not found (Resources or StreamingAssets).");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TroopUpgradeCatalog] Failed to read troop-upgrades.json: {ex.Message}");
            }

            return new TroopUpgradeCatalogData { Upgrades = new List<TroopUpgradeDef>() };
        }
    }

    /// <summary>
    /// Static surface over the canonical barracks.json — loads + caches the
    /// per-level barracks progression table. Mirrors TroopCatalog's loading strategy.
    /// (Consumed by the later BarracksService/UI; loaded here per the foundation spec.)
    /// </summary>
    public static class BarracksCatalog
    {
        private const string StreamingRelativePath = "Data/Canonical/barracks.json";

        private static BarracksCatalogData _data;

        /// <summary>All barracks level defs, in catalog order.</summary>
        public static IReadOnlyList<BarracksDef> All
        {
            get { EnsureLoaded(); return _data.Levels; }
        }

        /// <summary>Looks up a barracks level def by its <see cref="BarracksDef.Level"/>. Null when absent.</summary>
        public static BarracksDef Find(int level)
        {
            EnsureLoaded();
            foreach (var def in _data.Levels)
                if (def != null && def.Level == level) return def;
            return null;
        }

        /// <summary>Highest authored barracks level (0 when the catalog failed to load).</summary>
        public static int MaxLevel
        {
            get
            {
                EnsureLoaded();
                int max = 0;
                foreach (var def in _data.Levels)
                    if (def != null && def.Level > max) max = def.Level;
                return max;
            }
        }

        /// <summary>Forces a re-read of barracks.json (used by tests / the Monday sync).</summary>
        public static void Reload()
        {
            _data = null;
            EnsureLoaded();
        }

        private static void EnsureLoaded()
        {
            if (_data != null) return;
            _data = Load();
        }

        private static BarracksCatalogData Load()
        {
            try
            {
                string json = DeNelle.Core.CanonicalJson.Read(StreamingRelativePath);
                if (!string.IsNullOrEmpty(json))
                {
                    var parsed = JsonConvert.DeserializeObject<BarracksCatalogData>(json);
                    if (parsed != null && parsed.Levels != null)
                        return parsed;
                    Debug.LogError("[BarracksCatalog] barracks.json parsed empty.");
                }
                else
                {
                    Debug.LogError("[BarracksCatalog] barracks.json not found (Resources or StreamingAssets).");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[BarracksCatalog] Failed to read barracks.json: {ex.Message}");
            }

            return new BarracksCatalogData { Levels = new List<BarracksDef>() };
        }
    }
}
