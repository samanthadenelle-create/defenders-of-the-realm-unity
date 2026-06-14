// =============================================================================
// TroopDef — typed model for one buildable troop (WO-453 Step 1).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Troops are CONTENT, not code (the same call the pets make): TroopCatalog reads
// Assets/StreamingAssets/Data/Canonical/troops.json at load and hydrates these
// typed records. Modelled on PetDef — a flat [JsonProperty] def the factory +
// controller read their stats off, never typing troop names / numbers inline.
//
// Step-1 scope is COMBAT-ONLY: a troop is a lightweight friendly fighter that
// hunts the nearest hostile and is itself damageable. The cost / build-time
// fields (CostWood / CostIron / CostFood / BuildSeconds) are authored now so the
// later build-queue + army-storage steps (Step 2+) read them without a schema
// change — they are inert in Step 1.
// =============================================================================

using System;
using Newtonsoft.Json;

namespace DeNelle.Village
{
    /// <summary>
    /// One buildable troop's static definition — hydrated from troops.json by
    /// <see cref="TroopCatalog"/>, never constructed inline. Mirrors the flat
    /// <c>PetDef</c> shape so the loader / factory / controller read stats off it.
    /// </summary>
    [Serializable]
    public sealed class TroopDef
    {
        /// <summary>Stable id — e.g. <c>troop-footman</c>.</summary>
        [JsonProperty("id")] public string Id;
        /// <summary>Display name — verbatim (Footman / Archer).</summary>
        [JsonProperty("displayName")] public string DisplayName;
        /// <summary>Combat role — <c>melee</c> or <c>ranged</c> (ranged = reach, not a projectile yet).</summary>
        [JsonProperty("role")] public string Role;
        /// <summary>Army slots this troop occupies (population cost).</summary>
        [JsonProperty("slots")] public int Slots = 1;
        /// <summary>Resources/Heroes model id the factory skins — e.g. <c>Knight</c> / <c>Ranger</c>.</summary>
        [JsonProperty("model")] public string Model;
        /// <summary>Element — None for the Step-1 starter troops.</summary>
        [JsonProperty("element")] public string Element;
        /// <summary>Max HP — drives the damageable health pool.</summary>
        [JsonProperty("maxHp")] public float MaxHp = 100f;
        /// <summary>Per-hit attack damage.</summary>
        [JsonProperty("attackDamage")] public float AttackDamage = 12f;
        /// <summary>Seconds between attacks.</summary>
        [JsonProperty("attackCooldown")] public float AttackCooldown = 1.0f;
        /// <summary>Attack reach (units) — melee is short, ranged is long.</summary>
        [JsonProperty("attackRange")] public float AttackRange = 2.5f;
        /// <summary>Hunt move speed (units/sec).</summary>
        [JsonProperty("moveSpeed")] public float MoveSpeed = 4.0f;
        /// <summary>How far the troop scans for a hostile to hunt (units).</summary>
        [JsonProperty("huntScanRadius")] public float HuntScanRadius = 14f;

        // ── Build economy (authored now, inert in Step 1; consumed by Step 2+) ──
        /// <summary>Wood cost to build this troop.</summary>
        [JsonProperty("costWood")] public int CostWood;
        /// <summary>Iron cost to build this troop.</summary>
        [JsonProperty("costIron")] public int CostIron;
        /// <summary>Food cost to build this troop.</summary>
        [JsonProperty("costFood")] public int CostFood;
        /// <summary>Seconds to build this troop in the (later) training queue.</summary>
        [JsonProperty("buildSeconds")] public float BuildSeconds;
    }
}
