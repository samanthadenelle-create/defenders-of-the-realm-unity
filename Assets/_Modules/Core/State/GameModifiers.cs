// =============================================================================
// GameModifiers — the flat, JSON-serializable PERK CONTRACT (WO-430).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.State
//
// The keystone of the city-upgrade system (owner 2026-06-14): building tiers do
// NOT apply themselves ad-hoc. They COMPILE into ONE GameModifiers object that
// every consumer reads — towers, troops, resource production — and that every
// SCENE CREATION (castle AND raids) can be handed as an override JSON ("all scene
// creations should allow an override accepting a modifier JSON that applies these
// perks"). This is what makes upgrades PERSIST INTO RAIDS: the raid scene reads
// the same Active modifiers the castle does, so a +troop-damage Armorer perk
// assists the raid automatically.
//
// DESIGN: every numeric field DEFAULTS to a no-op (mult = 1.0), every flag to
// false — so an EMPTY/default contract changes nothing. Consumers multiply by the
// mult / branch on the flag; a missing field is identity. Pure data (Newtonsoft),
// WebGL-safe via CanonicalJson. Core owns it (GameState/SaveSchema round-trip;
// Core may not reference Village — same rule as PlayerTroop).
//
// SOURCE: ModifierService.Compute() builds this from GameState.BuildingTiers via
// the building-tiers catalog. An override (dev menu / scene config) replaces it.
// =============================================================================

using System;
using Newtonsoft.Json;

namespace DeNelle.Core.State
{
    /// <summary>Flat perk contract compiled from building tiers; default = all no-op.</summary>
    [Serializable]
    public sealed class GameModifiers
    {
        // ── Tower perks (Arcane Tower focus) ────────────────────────────────
        [JsonProperty("towerDamageMult")] public float TowerDamageMult = 1f;
        [JsonProperty("towerRangeMult")]  public float TowerRangeMult  = 1f;

        // ── Troop perks (Armorer focus) — these are what carry into RAIDS ────
        [JsonProperty("troopDamageMult")] public float TroopDamageMult = 1f;
        [JsonProperty("troopHealthMult")] public float TroopHealthMult = 1f;

        // ── Economy perks (Lumber Mill / Windmill / Forge focus) ────────────
        [JsonProperty("woodProductionMult")] public float WoodProductionMult = 1f;
        [JsonProperty("foodProductionMult")] public float FoodProductionMult = 1f;
        [JsonProperty("resourceEfficiencyMult")] public float ResourceEfficiencyMult = 1f; // Forge
        [JsonProperty("offlineBonusMult")] public float OfflineBonusMult = 1f;

        // ── Phase-2 WC3 building capstones (owner-named cheap levers) ─────────
        // ArmyCapBonus: SUMMED across owned perks/tiers (int, default 0 = no-op);
        // folds into ArmyStorage.MaxArmySize (base 10 + bonus). "Barracks: more troops."
        [JsonProperty("armyCapBonus")] public int ArmyCapBonus = 0;
        // AutoCollect: OR-ed flag; when true a ticking service auto-taps CollectAll so
        // resources bank without a manual tap. "Lumbermill: auto-gather capstone."
        [JsonProperty("autoCollect")] public bool AutoCollect = false;

        // ── Tier-4 unique abilities (flags; the "wow" moment per building) ──
        [JsonProperty("arcaneOverload")] public bool ArcaneOverload = false; // once/wave empower all towers 15s
        [JsonProperty("battleForged")]   public bool BattleForged   = false; // deployed troops +25% stats 60s
        [JsonProperty("forgefire")]      public bool Forgefire       = false; // periodic free troop equipment
        [JsonProperty("eternalGrove")]   public bool EternalGrove    = false; // periodic wood burst
        [JsonProperty("windsOfPlenty")]  public bool WindsOfPlenty   = false; // periodic food windfall

        /// <summary>A shared no-op instance (all mults 1, all flags false). Never mutate.</summary>
        public static readonly GameModifiers None = new GameModifiers();

        /// <summary>Deep copy (so callers can layer/override without mutating the source).</summary>
        public GameModifiers Clone() => new GameModifiers
        {
            TowerDamageMult = TowerDamageMult, TowerRangeMult = TowerRangeMult,
            TroopDamageMult = TroopDamageMult, TroopHealthMult = TroopHealthMult,
            WoodProductionMult = WoodProductionMult, FoodProductionMult = FoodProductionMult,
            ResourceEfficiencyMult = ResourceEfficiencyMult, OfflineBonusMult = OfflineBonusMult,
            ArmyCapBonus = ArmyCapBonus, AutoCollect = AutoCollect,
            ArcaneOverload = ArcaneOverload, BattleForged = BattleForged, Forgefire = Forgefire,
            EternalGrove = EternalGrove, WindsOfPlenty = WindsOfPlenty,
        };
    }
}
