// =============================================================================
// DungeonRunGrade — THE run rubric for a dungeon delve (WO-1040 §3b's seam).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Catalog
// Pure math. No Unity types, no services — like BattleStarRating, which this
// deliberately mirrors (duration -> stars -> reward weighting).
//
// ⚠⚠ READ THIS BEFORE ADDING ANY OTHER DUNGEON SCORING ANYWHERE ⚠⚠
//
// THIS IS THE ONLY DUNGEON RUN RUBRIC. WO-1041 §3 and WO-1042 §5(2) both say "one
// rubric, owned by WO-1040 §3b — do not invent a second one". When WO-1040 is
// implemented it fills in THIS type; it does NOT add a parallel one. If you are about
// to write a second kills/deaths/time score somewhere else, you are the bug those two
// tickets were pre-warning about.
//
// ⚠ HONEST STATUS (2026-08-16, WO-1042 lane): WO-1040 IS NOT IMPLEMENTED. Verified at
// source — there is no run-stat record anywhere in the tree (DungeonRuntimeState tracks
// rooms/chests/lore but no kills, deaths, potions or elapsed clock, and is not even
// persisted). WO-1042 could not "consume WO-1040's grade" as its brief assumed, because
// there was nothing to consume. Rather than hide a private score inside the polish code
// — which is exactly the duplicate-authority mistake the tickets warn about — the shared
// rubric is created HERE, in the place WO-1040 will own, with a PROVISIONAL scoring that
// WO-1040 refines in place. Refine StarsFor(); do not route around it.
//
// -----------------------------------------------------------------------------
// THE THREE TRAPS WO-1040 §3b PRE-RECORDED, AND HOW THIS SCORING AVOIDS THEM
//
//  1. "Speed vs exploration" — a time-based score punishes the player for reading lore,
//     opening chests and finding secret rooms, i.e. for engaging with the content the
//     dungeon was built to show. ⇒ ELAPSED TIME IS DELIBERATELY NOT SCORED. The field is
//     carried on the stats record for telemetry only.
//  2. "Potions + deaths double-punish" — both measure the same underlying thing (you took
//     damage), so scoring both charges the player twice for one mistake, and hits exactly
//     the struggling players who most need the reward. ⇒ ONLY DEATHS ARE SCORED.
//     PotionsUsed is carried but never costs a star.
//  3. "A completed run must always pay" — ⇒ THE FLOOR IS ZERO STARS, NOT NO REWARD.
//     A 0-star run is still a completed run and still pays a rough stone that still
//     polishes into a real gem (see JewelPolishOdds — every row's weights sum to 1).
//     Mastery moves the ODDS; it is never the only door.
// =============================================================================

using System;

namespace DeNelle.Core.Catalog
{
    /// <summary>
    /// What a single dungeon run recorded. A plain data carrier — the SCORING lives in
    /// <see cref="DungeonRunGrade"/> so there is exactly one place that turns stats into a grade.
    /// </summary>
    [Serializable]
    public struct DungeonRunStats
    {
        /// <summary>Enemies actually killed this run (engagement, not speed).</summary>
        public int EnemiesKilled;

        /// <summary>
        /// Healing/utility consumables spent. Carried for telemetry and UI ONLY — deliberately
        /// NOT scored, see trap 2 in this file's header.
        /// </summary>
        public int PotionsUsed;

        /// <summary>Times the hero went down this run. The one survival signal that is scored.</summary>
        public int Deaths;

        /// <summary>
        /// Wall-clock seconds spent underground. Carried for telemetry ONLY — deliberately NOT
        /// scored, see trap 1 in this file's header.
        /// </summary>
        public float ElapsedSeconds;

        /// <summary>Deepest floor index reached (0 = entry floor). The player's own risk dial.</summary>
        public int DeepestFloor;

        /// <summary>True when the run's boss was defeated.</summary>
        public bool BossDefeated;
    }

    /// <summary>
    /// Turns a <see cref="DungeonRunStats"/> into a 0..3 star grade, and combines that grade with
    /// elected depth into the single 0..3 weighting input the reward systems consume.
    /// </summary>
    public static class DungeonRunGrade
    {
        /// <summary>Maximum stars a run can earn. Matches BattleStarRating.MaxStars (one visual language).</summary>
        public const int MaxStars = 3;

        /// <summary>Kills at or above this count count as "cleared the floor" rather than "sprinted past it".</summary>
        public const int EngagementKills = 8;

        /// <summary>Reaching this floor or deeper is an elected-risk delve and adds the depth bonus.</summary>
        public const int DeepDelveFloor = 3;

        /// <summary>
        /// The run's star grade, 0..3. PROVISIONAL (see header) — WO-1040 refines THIS method.
        /// <para>
        /// One star each for: surviving (no deaths), beating the boss, and engaging with the floor
        /// (kills at or above <see cref="EngagementKills"/>). Time is not scored (trap 1) and potions
        /// are not scored (trap 2). A completed run that earns none of the three is 0 stars, which is
        /// a real grade that still pays (trap 3) — it is never "no reward".
        /// </para>
        /// </summary>
        public static int StarsFor(DungeonRunStats s)
        {
            int stars = 0;
            if (s.Deaths <= 0) stars++;
            if (s.BossDefeated) stars++;
            if (s.EnemiesKilled >= EngagementKills) stars++;
            return Clamp(stars, 0, MaxStars);
        }

        /// <summary>
        /// The elected-risk bonus: +1 for a deep delve (WO-1041 §3 "deeper = better", the torch/oil/
        /// darkness system being the player's own difficulty dial). 0 otherwise.
        /// </summary>
        public static int DepthBonus(DungeonRunStats s) => s.DeepestFloor >= DeepDelveFloor ? 1 : 0;

        /// <summary>
        /// The single 0..3 weighting input every reward system consumes: grade plus elected depth,
        /// clamped.
        /// <para>
        /// ⚠ WHY GRADE AND DEPTH COLLAPSE INTO ONE NUMBER RATHER THAN ACTING ON SEPARATE AXES:
        /// WO-1042 §5(2) forbids stacking every axis on one input ("better tier AND better odds AND
        /// shorter time") because a good run then trivialises the system and a bad run feels
        /// worthless. Two INPUTS (how well you played, how deep you chose to go) feeding ONE OUTPUT
        /// AXIS (the odds) keeps both meaningful while leaving duration, gem count and the tier set
        /// itself completely untouched by performance.
        /// </para>
        /// </summary>
        public static int PolishScore(DungeonRunStats s) => Clamp(StarsFor(s) + DepthBonus(s), 0, MaxStars);

        private static int Clamp(int v, int lo, int hi) => v < lo ? lo : (v > hi ? hi : v);
    }
}
