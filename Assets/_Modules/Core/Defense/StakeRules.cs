// =============================================================================
// StakeRules — THE LOSS-STAKES RULING (WO-1139, ruling of 2026-08-22).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Defense
//
// ⭐ THE RULING, IN ONE LINE:
//        COLLECTOR LOOTING ONLY. NO BANK THEFT.
//        "What you have COLLECTED is safe. What is still sitting in the building
//         is at risk."
//
// ⛔ THIS FILE COMPUTES NO THEFT, BECAUSE THE THEFT ALREADY HAPPENED.
//    `ResourceCollector.OnSiegeDestroyed` carries off half of a broken collector's
//    UNCOLLECTED pending (`RaidLootFraction = 0.5f`) at the moment it breaks — that
//    is WO-664, it has shipped, and it is the ONE theft in the game. Everything here
//    exists to REPORT that loss into a `StakesLedger`, never to invent a second one.
//
// ⛔⛔ WHAT WAS DELETED FROM THIS FILE ON 2026-08-22, AND WHY IT MUST NOT COME BACK.
//    An earlier pass implemented the (superseded) 2026-08-21 ruling as a FLAT 15%
//    OF THE BANKED WALLET with a 20%-of-capacity protected floor:
//        StealFraction, ProtectedFloorFraction, ProtectedFloor(int), TakeFrom(int,int),
//        Build(outcome, wood, woodCap, iron, ironCap, food, foodCap)
//    All six are GONE. They were a RIVAL SYSTEM — a second theft, from a different
//    pool, on a different trigger, through a different ledger. Two authorities for one
//    concept is how a player gets charged twice for one siege: the collector has
//    ALREADY removed the resources from its own pending, so a wallet debit on top of it
//    takes the same siege's price out of the player twice over.
//    Reasons the owner recorded, so this is not re-litigated:
//      • CoC parity is a trap here. CoC loots storages lightly — but only behind
//        shields, village guard, the loot cart and matchmaking limits. We have NONE of
//        that scaffolding, so bank theft would make us HARSHER than the game we model.
//      • Agency is the retention variable, not severity. Collector loot is fully
//        preventable by collecting; bank loot has no agency at all, least of all offline.
//      • The lever with better returns is LEGIBILITY, not a bigger number.
//    ⛔ Do not "restore the floor" or "add a small storage take". There is no bank
//       arithmetic in this file for either to hang off, and that absence is the design.
//
// ⛔ CRYSTALS ARE NEVER LOOTED — AND THE EXEMPTION IS STRUCTURAL, NOT REMEMBERED.
//    A crystal COLLECTOR exists (`HarvestResource.Crystals = 0`), so without a rule it
//    would be robbed like any other. A player cannot distinguish harvested crystals
//    from PURCHASED ones — same wallet — so any crystal loss reads as losing bought
//    currency, which turns a gameplay loss into a refund request on a live published
//    title. `IsLootable` is the single gate, `Add` is the only writer, and `Add` routes
//    a non-lootable bucket to nowhere. There is no expression here in which a crystal
//    could be taken. SiegeLossStakesRegression fails the gate if one ever is.
//
// ⛔ WHAT IS NEVER LOST, and is therefore ABSENT from this file entirely:
//    building downgrades, destroyed permanent progress, stars, cleared-camp progress,
//    and — since 2026-08-22 — the banked wallet. There is no code path here that could
//    express any of them.
// =============================================================================

using DeNelle.Core.Economy;

namespace DeNelle.Core.Defense
{
    /// <summary>
    /// The loss-stakes ruling as pure, wallet-free bookkeeping: which buckets a siege may ever
    /// report, and the one writer that fills them.
    /// <see cref="DeNelle.Village.DefenseReportBuilder"/> is the only production caller.
    /// </summary>
    public static class StakeRules
    {
        /// <summary>
        /// The id stamped on every ledger this ruling produces. An old report keeps the id of the
        /// ruling that WROTE it (<see cref="StakesLedger.InterimRuleId"/> for pre-stakes records),
        /// so a stakes-carrying build can never mis-read an interim report as "they lost nothing
        /// that day".
        /// <para>⚠ The id names the COLLECTOR-LOOT ruling. The superseded flat-bank id
        /// (<c>stakes.theft15.floor20.wo1139</c>) was live only inside the tree and never reached
        /// a player, but the rename is still the point: a report is self-describing about which
        /// ruling produced its numbers.</para>
        /// </summary>
        public const string RuleId = "stakes.collectorloot.wo1139";

        /// <summary>
        /// ⛔ WHICH BUCKETS A SIEGE MAY EVER REPORT A LOSS IN. Wood, iron and food are EARNED and
        /// sit in a collector until the player picks them up, so they are at risk. Crystals are
        /// BOUGHT (or indistinguishable from bought) and Coins are not a harvest at all — neither
        /// is ever lootable, on any outcome, from any source.
        /// <para>This is the whole crystal exemption. It is one expression rather than a habit at
        /// N call sites precisely so it cannot be forgotten at the N+1th.</para>
        /// </summary>
        public static bool IsLootable(BankResource resource)
        {
            return resource == BankResource.Wood
                || resource == BankResource.Iron
                || resource == BankResource.Food;
        }

        /// <summary>
        /// THE ONLY WRITER of a stakes bucket. Adds <paramref name="amount"/> of
        /// <paramref name="resource"/> to <paramref name="ledger"/>, and DROPS it — silently to
        /// the wire, loudly to the caller's trace — when the bucket is not lootable.
        ///
        /// <para>Negative and zero amounts are ignored: a "loss" that gives resources back is not
        /// a stake, it is a bug, and it must never be able to reach a ledger the player reads.</para>
        /// </summary>
        /// <returns>True when the amount was actually recorded.</returns>
        public static bool Add(StakesLedger ledger, BankResource resource, int amount)
        {
            if (ledger == null || amount <= 0) return false;
            if (!IsLootable(resource)) return false;   // ⛔ crystals/coins: no bucket, no expression

            switch (resource)
            {
                case BankResource.Wood: ledger.Wood += amount; return true;
                case BankResource.Iron: ledger.Iron += amount; return true;
                case BankResource.Food: ledger.Food += amount; return true;
                default: return false;
            }
        }

        /// <summary>
        /// A well-formed, all-zero ledger stamped with THIS ruling. The honest answer for a
        /// defence in which no collector broke — and the fallback direction when the report
        /// cannot be built at all: an all-zero ledger says "nothing was carried off", which is
        /// safe to be wrong about in a way that an invented loss never is.
        /// </summary>
        public static StakesLedger Empty()
        {
            return new StakesLedger { StakesRuleId = RuleId };
        }
    }
}
