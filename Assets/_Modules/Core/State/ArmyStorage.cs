// =============================================================================
// ArmyStorage — the persisted army manager (WO-453 Step 2 / 3).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.State
//
// The player's PERSISTED ARMY STATE — the roster of owned troops, the army cap,
// and the train / wound-recover / veterancy logic over them. Held by GameState
// (GameState.Army), the SAME ownership shape BaseLayout / ArenaDefense use: a
// plain saveable class on the state object, NOT a separate singleton/PlayerPrefs.
// Round-trips through SaveSchema (v22) — additive at the END so older saves load
// with an empty cap-10 army.
//
// LOSS MODEL = WOUNDED-RECOVERY, NO PERMADEATH (owner-decided): MarkWounded never
// removes a troop — it flags it + starts a recovery countdown; TickRecovery clears
// the flag when it elapses. The roster is stable; a downed troop just sits out.
//
// ASSEMBLY NOTE: lives in DeNelle.Core (NOT Village) because GameState.Army is a
// Core field and Core may not reference Village (CS0234 circular) — identical to
// PlacedDefenderData's rationale. The TroopDef catalog + the resource wallet that
// training needs both live in Village, so the catalog-dependent methods take small
// SEAM DELEGATES (a slot resolver + an affordability/spend callback) the Village
// caller wires to TroopCatalog / EconomyService. The pure army logic
// (cap / wounded / recovery / veterancy) needs no seam and runs Core-side.
// =============================================================================

using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace DeNelle.Core.State
{
    /// <summary>
    /// The persisted army: the owned-troop roster + cap + train/wound/veterancy
    /// logic. Held by <see cref="GameState"/> (mirrors BaseLayout/ArenaDefense
    /// ownership). Catalog-dependent methods (slots, cost) take seam delegates so
    /// this Core type never references the Village TroopCatalog / EconomyService.
    /// </summary>
    [Serializable]
    public sealed class ArmyStorage
    {
        /// <summary>Default army population cap (expandable later via a barracks tier).</summary>
        public const int DefaultMaxArmySize = 10;

        /// <summary>The owned-troop roster (saved). Never null after construction.</summary>
        [JsonProperty("owned")] public List<PlayerTroop> Owned = new List<PlayerTroop>();

        /// <summary>Army population cap — total troop slots. Fresh = 10; barracks raise it later.</summary>
        [JsonProperty("maxArmySize")] public int MaxArmySize = DefaultMaxArmySize;

        /// <summary>
        /// Monotonic id counter for minting stable <see cref="PlayerTroop.Id"/> values
        /// ("troop-{n}") — persisted so ids stay unique + deterministic across saves
        /// (no Date.now / random). Only ever increments.
        /// </summary>
        [JsonProperty("nextId")] public int NextId = 1;

        // ── Capacity ─────────────────────────────────────────────────────────

        /// <summary>
        /// Total army slots occupied by the current roster. Each troop's slot cost is
        /// resolved via <paramref name="slotOf"/> (TroopDef.Slots, Village-side) — a
        /// missing/unknown def counts as 1 slot (a safe floor). Footman/Archer = 1.
        /// </summary>
        public int SlotsUsed(Func<string, int> slotOf)
        {
            if (Owned == null) return 0;
            int total = 0;
            foreach (var t in Owned)
            {
                if (t == null) continue;
                int s = slotOf != null ? slotOf(t.TroopDefId) : 1;
                total += s > 0 ? s : 1;
            }
            return total;
        }

        /// <summary>Free army slots = <see cref="MaxArmySize"/> − used. Never negative.</summary>
        public int SlotsRemaining(Func<string, int> slotOf)
        {
            int rem = MaxArmySize - SlotsUsed(slotOf);
            return rem < 0 ? 0 : rem;
        }

        /// <summary>
        /// True when <paramref name="troopId"/> is a known def (slot &gt; 0) AND the
        /// remaining army slots cover that def's slot cost. Pure capacity check — does
        /// NOT consider resource cost (that is <see cref="TrainNow"/>'s affordability seam).
        /// </summary>
        public bool CanTrain(string troopId, Func<string, int> slotOf)
        {
            if (string.IsNullOrEmpty(troopId)) return false;
            int cost = slotOf != null ? slotOf(troopId) : 0;
            if (cost <= 0) return false;                 // unknown def → not trainable
            return SlotsRemaining(slotOf) >= cost;
        }

        /// <summary>
        /// STEP-stub training (owner's "training stub — resource cost only"): if there
        /// is army room AND the player can afford the troop's resource cost, mints a
        /// fresh <see cref="PlayerTroop"/> (rank 0, not wounded), appends it, and
        /// returns it. Returns null when capacity OR affordability fails — no mutation
        /// on failure. The real timed queue + offline accrual is a LATER increment.
        /// </summary>
        /// <param name="troopId">The TroopDef id to train (e.g. "troop-footman").</param>
        /// <param name="slotOf">Resolves a def id → its army slot cost (TroopDef.Slots).</param>
        /// <param name="tryAfford">
        /// Affordability+spend seam (Village wires this to EconomyService.TrySpend of the
        /// def's CostWood/Iron/Food): returns true AND has deducted the cost, or false +
        /// no spend. Pass a callback that always returns true for a free/dev train.
        /// </param>
        public PlayerTroop TrainNow(string troopId, Func<string, int> slotOf, Func<string, bool> tryAfford)
        {
            if (!CanTrain(troopId, slotOf)) return null;
            // Spend AFTER the capacity check, but only commit the troop if the spend took.
            if (tryAfford != null && !tryAfford(troopId)) return null;
            var troop = new PlayerTroop(MintId(), troopId);
            if (Owned == null) Owned = new List<PlayerTroop>();
            Owned.Add(troop);
            return troop;
        }

        // ── Deployment ───────────────────────────────────────────────────────

        /// <summary>The deployable troops — healthy (not wounded) members of the roster.</summary>
        public IEnumerable<PlayerTroop> GetDeployable()
        {
            if (Owned == null) yield break;
            foreach (var t in Owned)
                if (t != null && t.IsDeployable) yield return t;
        }

        // ── Loss / recovery (wounded-recovery model — NEVER deletes) ──────────

        /// <summary>
        /// The raid-loss path: marks <paramref name="t"/> wounded and starts its
        /// recovery countdown. NEVER removes the troop (no permadeath). Clamps the
        /// recovery seconds to &gt;= 0; a non-positive value recovers it immediately.
        /// </summary>
        public void MarkWounded(PlayerTroop t, float recoverySeconds)
        {
            if (t == null) return;
            if (recoverySeconds <= 0f)
            {
                t.Wounded = false;
                t.RecoveryRemaining = 0f;
                return;
            }
            t.Wounded = true;
            t.RecoveryRemaining = recoverySeconds;
        }

        /// <summary>
        /// Advances recovery on every wounded troop by <paramref name="dt"/> seconds;
        /// a troop whose countdown reaches 0 recovers (Wounded cleared). Call once per
        /// tick (real-time or simulated). No-op when nothing is wounded.
        /// </summary>
        public void TickRecovery(float dt)
        {
            if (Owned == null || dt <= 0f) return;
            foreach (var t in Owned)
            {
                if (t == null || !t.Wounded) continue;
                t.RecoveryRemaining -= dt;
                if (t.RecoveryRemaining <= 0f)
                {
                    t.RecoveryRemaining = 0f;
                    t.Wounded = false;
                }
            }
        }

        // ── Veterancy ─────────────────────────────────────────────────────────

        /// <summary>
        /// Grants one veterancy rank to <paramref name="t"/> (called on a survived
        /// 3-star raid), capped at <see cref="PlayerTroop.MaxVeterancyRank"/>. Idempotent
        /// at the cap.
        /// </summary>
        public void AddVeterancy(PlayerTroop t)
        {
            if (t == null) return;
            if (t.VeterancyRank < PlayerTroop.MaxVeterancyRank)
                t.VeterancyRank++;
        }

        // ── Internal ──────────────────────────────────────────────────────────

        /// <summary>Mints the next stable troop id ("troop-{n}") and advances the counter.</summary>
        private string MintId()
        {
            if (NextId < 1) NextId = 1;
            string id = "troop-" + NextId.ToString();
            NextId++;
            return id;
        }
    }
}
