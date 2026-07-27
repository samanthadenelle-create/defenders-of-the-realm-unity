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
using DeNelle.Core.Diagnostics;

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

        /// <summary>Last army cap value we emitted a [Flow:Perk] line for (change-only logging).</summary>
        private static int s_lastLoggedCap = -1;

        /// <summary>
        /// Army population cap — total troop slots. DYNAMIC: base 10
        /// (<see cref="DefaultMaxArmySize"/>) + the SUMMED <c>armyCapBonus</c> from the
        /// active perk contract (the "Barracks: more troops" capstone). Null-safe — with no
        /// modifier it stays the base 10. Derived, so NOT serialized (the old stored
        /// "maxArmySize" key on legacy saves is harmlessly ignored on load).
        /// </summary>
        [JsonIgnore] public int MaxArmySize
        {
            get
            {
                int bonus = 0;
                var mods = ModifierService.Active;               // never null (Compute() returns fresh)
                if (mods != null && mods.ArmyCapBonus > 0) bonus = mods.ArmyCapBonus;
                int cap = DefaultMaxArmySize + bonus;
                if (cap != s_lastLoggedCap)
                {
                    s_lastLoggedCap = cap;
                    FlowTrace.Step("Perk", "army cap -> " + cap);
                }
                return cap;
            }
        }

        /// <summary>
        /// Monotonic id counter for minting stable <see cref="PlayerTroop.Id"/> values
        /// ("troop-{n}") — persisted so ids stay unique + deterministic across saves
        /// (no Date.now / random). Only ever increments.
        /// </summary>
        [JsonProperty("nextId")] public int NextId = 1;

        /// <summary>
        /// WO-779 — the persisted RECOVERY CLOCK anchor: the unix-ms wall-clock at which
        /// <see cref="AdvanceRecovery"/> last advanced the wounded countdowns. Mirrors
        /// <c>GameState.LastHarvestClaimMs</c> (the offline-accrual clock) so wounded troops
        /// heal FORWARD across app-closes off the SAME clock the Obsidian work queue reads
        /// (TimeSource.NowUnixMs) — reused, never forked. Additive at the END: an older save
        /// with no key loads 0, and the first <see cref="AdvanceRecovery"/> SEEDS it to now
        /// (crediting nothing that launch) so a pre-anchor save never banks a giant
        /// retroactive heal. Serialized straight to JSON with the rest of GameState.Army.
        /// </summary>
        [JsonProperty("lastRecoveryTickMs")] public double LastRecoveryTickMs;

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

        /// <summary>
        /// WO-771.9 — grants a freshly-TRAINED troop into the roster UNCONDITIONALLY (no
        /// capacity/afford check). This is the completion effect of a timed
        /// <see cref="DeNelle.Core.Jobs.JobKind.TrainTroop"/> job: the resource cost was charged
        /// + the army-cap checked at ENQUEUE time, so on completion the paid troop must land even
        /// if the cap has since filled (CoC parity — the barracks holds the trained unit). Mints a
        /// stable id, appends a rank-0 healthy <see cref="PlayerTroop"/>, and returns it. Null id →
        /// no-op (returns null).
        /// </summary>
        public PlayerTroop GrantTrained(string troopDefId)
        {
            if (string.IsNullOrEmpty(troopDefId)) return null;
            if (Owned == null) Owned = new List<PlayerTroop>();
            var troop = new PlayerTroop(MintId(), troopDefId);
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
        /// The raid-EXIT reconcile (WO-453 Step 4): given the ids of every troop that was
        /// DEPLOYED into the raid and the ids of the SURVIVORS (still alive at retreat), marks
        /// every deployed-but-not-survivor troop wounded (recovery countdown) and leaves the
        /// survivors untouched. The wounded-recovery loss model — NEVER deletes a troop. Both
        /// id sets are null-safe (a null set reads as empty); a non-positive
        /// <paramref name="recoverySeconds"/> recovers a downed troop immediately (per
        /// <see cref="MarkWounded"/>). Lookup is by <see cref="PlayerTroop.Id"/>.
        /// </summary>
        public void ReconcileAfterRaid(IEnumerable<string> deployedIds, IEnumerable<string> survivorIds, float recoverySeconds)
        {
            if (Owned == null || deployedIds == null) return;

            var survivors = new HashSet<string>(StringComparer.Ordinal);
            if (survivorIds != null)
                foreach (var s in survivorIds)
                    if (!string.IsNullOrEmpty(s)) survivors.Add(s);

            var deployed = new HashSet<string>(StringComparer.Ordinal);
            foreach (var d in deployedIds)
                if (!string.IsNullOrEmpty(d)) deployed.Add(d);

            // Index the roster by id once so a big army doesn't go O(n*m).
            foreach (var t in Owned)
            {
                if (t == null || string.IsNullOrEmpty(t.Id)) continue;
                if (!deployed.Contains(t.Id)) continue;        // not sent on this raid
                if (survivors.Contains(t.Id)) continue;        // came home — untouched
                MarkWounded(t, recoverySeconds);               // deployed + fell → wounded
            }
        }

        /// <summary>
        /// Advances recovery on every wounded troop by <paramref name="dt"/> seconds;
        /// a troop whose countdown reaches 0 recovers (Wounded cleared). Call once per
        /// tick (real-time or simulated). No-op when nothing is wounded. Returns the
        /// number of troops that recovered on THIS call.
        ///
        /// PURE STEP (dt-based, no clock) so it is headlessly unit-testable with a
        /// simulated delta — the wall-clock resolver is <see cref="AdvanceRecovery"/>.
        /// NEVER adds/removes a troop and NEVER touches <see cref="PlayerTroop.Id"/> or
        /// <see cref="PlayerTroop.VeterancyRank"/> — a recovered troop is the SAME
        /// instance with Wounded cleared, so the roster / OwnedTroopId / veterancy
        /// accounting is untouched (no double-resurrect) and it is idempotent once healed
        /// (a healed troop is skipped by the <c>!t.Wounded</c> guard).
        /// </summary>
        public int TickRecovery(float dt)
        {
            if (Owned == null || dt <= 0f) return 0;
            int recovered = 0;
            foreach (var t in Owned)
            {
                if (t == null || !t.Wounded) continue;
                t.RecoveryRemaining -= dt;
                if (t.RecoveryRemaining <= 0f)
                {
                    t.RecoveryRemaining = 0f;
                    t.Wounded = false;
                    recovered++;
                }
            }
            return recovered;
        }

        /// <summary>
        /// WO-779 — the wall-clock RECOVERY RESOLVER: the live + offline advance hook the
        /// zero-caller <see cref="TickRecovery"/> was missing. Computes the elapsed seconds
        /// since the last advance from the persisted <see cref="LastRecoveryTickMs"/> anchor
        /// (fed <paramref name="nowMs"/> = TimeSource.NowUnixMs by the Village tick — the
        /// SAME clock the Obsidian work queue uses, reused not forked) and ticks every
        /// wounded troop by that delta. Call on LOAD (credits the offline gap) AND on a
        /// lightweight live cadence (~1/sec). Returns the number of troops that recovered.
        ///
        /// Null/empty-army safe (<see cref="TickRecovery"/> no-ops). Monotonic + retroactive-
        /// safe:
        ///   • fresh anchor (&lt;= 0) → SEED to now, tick NOTHING (a pre-anchor save can't
        ///     bank a giant first-load heal — mirrors OfflineHarvestService's fresh-clock seed);
        ///   • a backwards/zero clock delta → advance the anchor but tick nothing (never
        ///     re-heal on a rewound clock).
        /// The anchor is part of GameState.Army, so every Save() that persists a wound (e.g.
        /// RaidDeployController.DoRetreat) persists the fresh anchor ATOMICALLY with it — the
        /// away-gap on reload is measured from the wound's own save point (no over-heal).
        /// </summary>
        public int AdvanceRecovery(double nowMs)
        {
            // Fresh anchor → seed to now, credit nothing this call.
            if (LastRecoveryTickMs <= 0.0)
            {
                LastRecoveryTickMs = nowMs;
                return 0;
            }

            double elapsedSec = (nowMs - LastRecoveryTickMs) / 1000.0;
            LastRecoveryTickMs = nowMs;          // always advance the anchor (even on a 0/backwards delta)
            if (elapsedSec <= 0.0) return 0;      // no time passed / clock ran backwards → no heal

            int recovered = TickRecovery((float)elapsedSec);
            if (recovered > 0)
                FlowTrace.Step("Army", $"recovery advanced {elapsedSec:0}s -> {recovered} troop(s) healed.");
            return recovered;
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
