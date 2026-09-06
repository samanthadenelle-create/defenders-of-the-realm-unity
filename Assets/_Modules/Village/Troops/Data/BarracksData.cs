// =============================================================================
// BarracksData — serializable data records for the Barracks & Troop Upgrade
// progression (WO-771.9, queue-independent foundation).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// These are CONTENT, not code — same pattern as TroopDef/TroopCatalog: the
// progression tables are authored in canonical JSON and hydrated here via
// Newtonsoft.Json. Two catalogs feed this system:
//   - barracks.json        -> BarracksDef[]      (per-level unlocks + cost/time)
//   - troop-upgrades.json  -> TroopUpgradeDef[]  (per-troop stat curves + abilities)
//
// DUAL-COPY RULE (BINDING): both JSON files live BYTE-IDENTICAL in
//   Assets/Resources/Data/Canonical/*.json     (WebGL-safe, Resources.Load first)
//   Assets/StreamingAssets/Data/Canonical/*.json (desktop fallback + source)
// See CanonicalJson / LocalJsonCatalogSource. Loaders live in TroopStatResolver.cs.
//
// SCOPE (WO-771.9 foundation half): pure data + a side-effect-free stat resolver.
// The BarracksService, job-enqueue/completion, gating and spawn-wiring came in the
// LATER integration agent (after WO-773 landed the queue + the v34->v35 save-schema
// migration). NONE of those live here.
// ⛔ The "code-built BarracksPanel" this line used to promise WAS built, was never given a
// door, and was DELETED on 2026-09-06 (WO-1430 Group A, owner ruling 21). The barracks and
// troop surfaces the player uses are the Manage screen's Build and Army tabs.
//
// ResourceCost is REUSED verbatim from DeNelle.Village.ResourceCost
// (EconomyService.cs) — the same struct EconomyService.CanAfford/TrySpend consume,
// so the (later) BarracksService affordability check plugs straight in. It carries
// Wood/Food/Iron/Crystals/Coins; the spec's "Gold/Wood/Stone" shorthand maps onto
// the real project economy (Gold == Coins, Stone axis is retired -> Iron/Crystals).
//
// StatusKind is REUSED from DeNelle.BattleATB.Engine (the only StatusKind in the
// codebase) — the canonical burn/poison/bleed/slow/freeze/stun/regen/haste/shield/
// mark vocabulary. This is why DeNelle.Village.asmdef gains a DeNelle.BattleATB
// reference (non-circular: BattleATB -> Core/Data only).
// =============================================================================

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using DeNelle.BattleATB.Engine; // StatusKind

namespace DeNelle.Village
{
    /// <summary>
    /// One Barracks LEVEL definition — what upgrading the Barracks to this level
    /// costs, how long it takes, and which troop ids it unlocks. Hydrated from
    /// barracks.json; never constructed inline. Level 1 is the day-one baseline
    /// (zero cost / zero time) that unlocks the starting roster.
    /// </summary>
    [Serializable]
    public sealed class BarracksDef
    {
        /// <summary>Barracks level this row describes (1 = day-one baseline).</summary>
        [JsonProperty("level")] public int Level;
        /// <summary>Troop ids (matching troops.json ids) this level makes trainable.
        /// May be empty for a "stat-only" level; typically 0-2 new troops per level.</summary>
        [JsonProperty("unlocksTroopIds")] public string[] UnlocksTroopIds = Array.Empty<string>();
        /// <summary>Resource cost to reach this level. Reuses DeNelle.Village.ResourceCost
        /// so EconomyService.CanAfford/TrySpend consume it directly (WO-773 integration).</summary>
        [JsonProperty("cost")] public ResourceCost Cost;
        /// <summary>Wall-clock seconds the (later) build-timer queue takes to complete this upgrade.</summary>
        [JsonProperty("buildTimeSeconds")] public float BuildTimeSeconds;
        /// <summary>Player-facing label for this level (e.g. "Barracks II").</summary>
        [JsonProperty("displayName")] public string DisplayName;
    }

    /// <summary>
    /// A per-level scalar curve. <see cref="Values"/>[i] is the value at level i+1.
    /// Semantics are a MULTIPLIER (baseline 1.0) so a missing/empty curve resolves
    /// to a no-op 1x — the pure-baseline path. Levels beyond the authored length
    /// clamp to the last value (curves plateau, they never wrap or throw).
    /// </summary>
    [Serializable]
    public sealed class StatCurve
    {
        /// <summary>Per-level values; index 0 == level 1. Null/empty == flat 1x.</summary>
        [JsonProperty("values")] public float[] Values;

        /// <summary>The curve value at <paramref name="level"/> (1-based). Returns 1f
        /// when no curve is authored; clamps into range otherwise (never throws).</summary>
        public float Get(int level)
        {
            if (Values == null || Values.Length == 0) return 1f;
            return Values[Mathf.Clamp(level - 1, 0, Values.Length - 1)];
        }
    }

    /// <summary>
    /// A special ability a troop earns once it reaches <see cref="LevelThreshold"/>.
    /// <see cref="AbilityId"/> maps to a canonical ability in abilities.json — resolve
    /// it via DeNelle.Village.AbilityCatalog.FindById when the (later) combat wiring
    /// needs the full AbilityDef. <see cref="StatusKind"/> is the status the ability
    /// applies (canonical DeNelle.BattleATB.Engine.StatusKind vocabulary).
    /// </summary>
    [Serializable]
    public sealed class AbilityUnlock
    {
        /// <summary>Minimum troop upgrade level at which this ability becomes active.</summary>
        [JsonProperty("levelThreshold")] public int LevelThreshold;
        /// <summary>Canonical ability id (abilities.json) — resolve via AbilityCatalog.FindById.</summary>
        [JsonProperty("abilityId")] public string AbilityId;
        /// <summary>The status this ability applies (canonical StatusKind vocabulary).</summary>
        [JsonProperty("statusKind")] public StatusKind StatusKind;
        /// <summary>One-line flavor/description for the upgrade UI.</summary>
        [JsonProperty("description")] public string Description;
    }

    /// <summary>
    /// One troop's upgrade progression — the stat curves it grows along and the
    /// special abilities it unlocks. Hydrated from troop-upgrades.json; never
    /// constructed inline. A troop with NO row here resolves to its pure baseline
    /// (all multipliers 1x, no abilities).
    /// </summary>
    [Serializable]
    public sealed class TroopUpgradeDef
    {
        /// <summary>Troop id this progression applies to (matches troops.json id).</summary>
        [JsonProperty("troopId")] public string TroopId;
        /// <summary>Reach multiplier curve -> scales AttackRange + AggroRadius.</summary>
        [JsonProperty("reach")] public StatCurve Reach;
        /// <summary>Strength multiplier curve -> scales MaxHp + DPS/AttackDamage.</summary>
        [JsonProperty("strength")] public StatCurve Strength;
        /// <summary>Abilities unlocked at level thresholds (typically 3/5/7).</summary>
        [JsonProperty("specialAbilities")] public AbilityUnlock[] SpecialAbilities = Array.Empty<AbilityUnlock>();
        /// <summary>Flavor blurb for the upgrade detail pane.</summary>
        [JsonProperty("flavorText")] public string FlavorText;
    }

    /// <summary>The parsed barracks.json root — the per-level barracks progression table.</summary>
    [Serializable]
    public sealed class BarracksCatalogData
    {
        [JsonProperty("version")] public int Version;
        /// <summary>Barracks level defs, ascending by <see cref="BarracksDef.Level"/>.</summary>
        [JsonProperty("levels")] public List<BarracksDef> Levels = new List<BarracksDef>();
    }

    /// <summary>The parsed troop-upgrades.json root — one row per trainable troop.</summary>
    [Serializable]
    public sealed class TroopUpgradeCatalogData
    {
        [JsonProperty("version")] public int Version;
        /// <summary>Per-troop upgrade progressions.</summary>
        [JsonProperty("upgrades")] public List<TroopUpgradeDef> Upgrades = new List<TroopUpgradeDef>();
    }
}
