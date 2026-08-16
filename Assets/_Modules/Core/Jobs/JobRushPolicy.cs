// =============================================================================
// JobRushPolicy — the ONE authority on which queue jobs may be finished early by
// SPENDING CURRENCY (WO-1042 §5(4); owner rulings 2026-08-16).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Jobs
//
// ⛔⛔ THE DOCTRINE, IN ONE LINE: **SELL THE WAIT, NEVER THE ROLL.** ⛔⛔
//
//   A paid instant-resolve of a **RANDOM** outcome is, mechanically, a LOOT BOX, and is
//   regulated in several of the jurisdictions in this project's shipping plan.
//   Owner ruling, 2026-08-16, verbatim: "explicitly exclude this".
//
// WHAT IS ALLOWED, AND WHAT IS NOT — the line is PURCHASE, not speed:
//
//   ✅ TIME. Waiting it out is the default cost of every polish. Always allowed.
//   ✅ AD-SKIP, including on a random-outcome job. An ad shortens a countdown the player
//      would have reached anyway; no purchase is involved, so it is not the regulated
//      shape. The owner named ads as the intended revenue stream here. This is why
//      BuildTimerService.CanWatchAdToSkip / WatchAdToSkip carry NO gate from this policy —
//      that absence is deliberate and documented at those sites. Do not "tidy" one in.
//   ⛔ CASH / PREMIUM CURRENCY (crystals) buying an instant finish of a random outcome.
//      FORBIDDEN now, and still forbidden when real payments land.
//
// ⚠⚠ DO NOT COLLAPSE THESE TWO PURCHASES — THEY ARE DIFFERENT, AND ONLY ONE IS EXCLUDED
// (owner rulings 2026-08-16, in sequence):
//
//   ⛔ BUYING A **SKIP** — paying crystals to finish a polish that is already running, so the
//      unknown outcome resolves NOW. EXCLUDED. That is this file. The player is paying for the
//      resolution itself.
//   ✅ BUYING AN **ATTEMPT** — paying for another re-polish roll. ALLOWED, deliberately, and it
//      is a separate path that this policy does not touch. Owner: "I still think paying for the
//      opportunity is fine. understanding the risk is important and should be on the player to
//      decide." The model is coherent because EVERY path — free, ad-funded, paid — rolls the
//      IDENTICAL per-roll table: money buys ATTEMPTS, never better odds. A confirmation screen
//      discloses the real percentages (derived from the roll table, never authored twice), and a
//      shatter chance re-ties attempts to earned material so attempts can never be stockpiled
//      into a guaranteed outcome.
//
// So: a future reader who finds paid re-rolls shipping must NOT conclude the skip exclusion was
// abandoned. Buying a chance and buying certainty are not the same purchase. Both rulings are
// live; they govern different verbs.
//
// A re-polish can additionally TRADE DOWN — the new tier may be worse than the one you put in —
// which is exactly why the SKIP stays excluded: paying to resolve it faster is paying to reach a
// possible loss sooner, with none of the disclosure the attempt path carries.
//
// -----------------------------------------------------------------------------
// WHY A POLICY OBJECT AND NOT "we just didn't wire a button"
//
// The Obsidian queue's paid-finish verb (BuildTimerService.TryInstantFinish /
// InstantFinishPrice / CompleteAnyJob) is GENERIC over JobKind — it matches a job by its
// StructureId string and never consults its kind. A new JobKind therefore INHERITS
// purchasability by default. Excluding it by omission (declining to add a Finish button)
// would leave a GAP: the next seat that adds a generic "Finish Now" row to any job list,
// or calls the service directly, silently re-acquires the loot box.
//
// So the exclusion lives at the MECHANISM. Every paid-finish entry point asks this policy
// first, and a refusal is LOUD (FlowTrace.Warn naming the ruling) rather than a silent
// no-op — a silent no-op reads as a bug to the next person and gets "fixed".
//
// Pinned by DungeonGemExclusivityRegression (marker DUNGEON_GEM_EXCLUSIVITY_OK), which
// fails if a random-outcome kind ever becomes paid-finishable or if an entry point stops
// consulting this policy.
//
// If you are here because the restriction looked arbitrary and you want to remove it:
// it is not arbitrary, and removing it is a legal decision, not an engineering one.
// Take it to the owner.
// =============================================================================

using DeNelle.Core.Diagnostics;

namespace DeNelle.Core.Jobs
{
    /// <summary>
    /// Decides whether a <see cref="JobKind"/> may be completed early in exchange for CURRENCY.
    /// The single source of truth for the 2026-08-16 owner ruling; see this file's header for the
    /// doctrine ("sell the wait, never the roll") and for why ad-skip is deliberately NOT gated here.
    /// </summary>
    public static class JobRushPolicy
    {
        private const string Sys = "Obsidian";

        /// <summary>
        /// The reason, in one line, quoted verbatim into every refusal so the trace explains itself
        /// without the reader having to find this file.
        /// </summary>
        public const string RandomOutcomeRuling =
            "a paid instant resolve of a RANDOM outcome is mechanically a loot box and is regulated " +
            "in several jurisdictions in the shipping plan (owner ruling 2026-08-16); ads and waiting " +
            "are allowed - sell the WAIT, never the ROLL";

        /// <summary>
        /// True when the job's RESULT is rolled at completion rather than known at enqueue time.
        /// Such a job may never be finished early for currency (see <see cref="RandomOutcomeRuling"/>).
        /// <para>
        /// ⚠ Add a kind here ONLY if its outcome is genuinely random. Every deterministic kind must
        /// stay out so its existing paid rush keeps working — the ruling is scoped to random outcomes
        /// and must not be over-applied to building, training, structure upgrades or research.
        /// </para>
        /// </summary>
        public static bool IsRandomOutcome(JobKind kind)
        {
            switch (kind)
            {
                // WO-1042: the refined gem TIER is rolled when the polish lands (odds shaped by the
                // WO-1040 run grade). A RE-polish is the same kind and can trade DOWN, so paying for
                // one could buy a strictly worse item. Both are the regulated shape.
                case JobKind.JewelPolish:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// True when a job of <paramref name="kind"/> may be finished early by SPENDING CRYSTALS.
        /// Every deterministic kind returns true (unchanged behaviour); random-outcome kinds return
        /// false. ⚠ This does NOT govern ad-skip, which stays allowed for every kind by owner ruling.
        /// </summary>
        public static bool AllowsPaidInstantFinish(JobKind kind) => !IsRandomOutcome(kind);

        /// <summary>
        /// The guard every PAID-finish entry point calls. Returns TRUE when the caller must ABORT,
        /// having already emitted a loud <see cref="FlowTrace.Warn"/> that names the ruling, and
        /// filled <paramref name="failure"/> with player-safe ASCII copy. Returns FALSE (leaving
        /// <paramref name="failure"/> null) for every purchasable job, so deterministic paths are
        /// untouched.
        /// </summary>
        /// <param name="kind">The kind of the job the caller is about to finish early for currency.</param>
        /// <param name="entryPoint">Where the attempt came from, for the trace (e.g. "TryInstantFinish").</param>
        /// <param name="jobId">The job's StructureId, for the trace.</param>
        /// <param name="failure">Player-facing refusal copy (ASCII only), or null when allowed.</param>
        public static bool RefusePaidFinish(JobKind kind, string entryPoint, string jobId, out string failure)
        {
            if (AllowsPaidInstantFinish(kind)) { failure = null; return false; }

            // ASCII only (device tofu), and it tells the player what they CAN do instead of only
            // what they cannot — the wait and the ad are both still open to them.
            failure = "This cannot be bought. Wait it out, or watch an ad to speed it up.";

            // LOUD, never silent (CLAUDE.md section 12.2): a swallowed refusal reads as a bug and
            // invites a "fix" that reinstates the loot box.
            FlowTrace.Warn(Sys,
                $"PAID FINISH REFUSED at {entryPoint} for job '{jobId}' (kind {kind}) - {RandomOutcomeRuling}. " +
                "Deterministic jobs and ad-skip are unaffected; see JobRushPolicy.");
            return true;
        }
    }
}
