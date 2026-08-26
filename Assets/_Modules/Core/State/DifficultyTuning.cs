// =============================================================================
// DifficultyTuning — the single source of truth for what Difficulty changes.
// -----------------------------------------------------------------------------
// The owner spec (docs/qa/owner-acceptance-checklist.md "Difficulty & waves"):
// a three-way difficulty toggle that scales the BETWEEN-WAVE countdown so the
// time a player gets to build between waves lands at:
//
//   Easy   ~ 10 minutes between waves
//   Normal ~  5 minutes between waves   (the existing default)
//   Hard   ~  3 minutes between waves
//
// !! STALE AS OF 2026-08-26 -- READ THIS BEFORE TRUSTING THE MINUTES ABOVE.
// The owner raised the between-wave window to 15 MINUTES ("can we extend the
// time between raids to 15 minutes" / "need time to rebuild and farm", with
// "they can always click start now" as the release valve). waves.json now
// authors 900 s for waves 2-20 (wave 1 stays 45 s, FTUE).
//
// The constants below are RATIOS, not absolutes, so they still divide cleanly
// (Easy 2.0 / Hard 0.6) -- but applied to a 900 s base they now yield:
//   Normal  900 s = 15 min   <- the owner's ruling, correct
//   Easy   1800 s = 30 min   <- was intended as 10 min
//   Hard    540 s =  9 min   <- was intended as  3 min
// RULED 2026-08-26: the owner reviewed that spread and APPROVED it -- "i like
// that" / "changes from casual to hardcore". The widened gap is the POINT, not
// a side effect: at 30 min a player can fully rebuild and farm between waves,
// at 9 min they are permanently behind, so the toggle became a real casual-vs-
// hardcore choice rather than a nudge. The 10/5/3 minute targets in the header
// above are HISTORICAL -- they describe the pre-2026-08-26 300 s base and are
// kept only to explain where the 2.0 / 0.6 ratios came from.
//
// The live targets are: Easy 30 min / Normal 15 min / Hard 9 min.
//
// !! Do NOT "restore" the old minute targets by editing these constants. They
// are ratios; the base moved, the owner ruled on the result, and the ratios
// are what she approved.
//
// IMPORTANT — the multiplier is computed against the canonical wave-build
// window, NOT hard-coded seconds. waves.json authors the post-first-wave
// countdown at 300 s (LATER_PREPARE_SECONDS, the React v1 value), so:
//
//   Normal = 300 s  -> multiplier 1.0  (300 s * 1.0  = 300 s = 5 min)
//   Easy   = 600 s  -> multiplier 2.0  (300 s * 2.0  = 600 s = 10 min)
//   Hard   = 180 s  -> multiplier 0.6  (300 s * 0.6  = 180 s = 3 min)
//
// WaveManager multiplies each WaveDef.CountdownSeconds (whatever waves.json
// declares — first wave 45 s, later waves 300 s) by CountdownMultiplier. The
// ratios above hold for ANY base value, so the short 45 s first-wave window
// also scales proportionally (Easy 90 s / Normal 45 s / Hard 27 s) — a new
// player on Easy still gets a gentle, not punishing, first-wave timer.
//
// Lives in DeNelle.Core.State next to the Difficulty enum so BOTH the Settings
// module (the toggle) and the Village module (WaveManager) can read it without
// either taking a dependency on the other — module isolation (port-spec Part 2).
// =============================================================================

namespace DeNelle.Core.State
{
    /// <summary>
    /// Static tuning table for <see cref="Difficulty"/>. Currently difficulty
    /// affects exactly one thing — the between-wave countdown length — expressed
    /// as a multiplier on the canonical <c>WaveDef.CountdownSeconds</c> so the
    /// owner's Easy/Normal/Hard minute targets are derived, never hard-coded.
    /// </summary>
    public static class DifficultyTuning
    {
        /// <summary>
        /// The canonical "Normal" between-wave build window, in seconds — the
        /// post-first-wave countdown waves.json authors (React LATER_PREPARE_SECONDS).
        /// The Easy / Hard multipliers below are derived from the owner's minute
        /// targets relative to this reference. Kept here so the relationship is
        /// auditable in one place.
        /// </summary>
        public const float NormalBuildWindowSeconds = 300f;

        /// <summary>The owner's Easy between-wave target — 10 minutes.</summary>
        public const float EasyBuildWindowSeconds = 600f;

        /// <summary>The owner's Hard between-wave target — 3 minutes.</summary>
        public const float HardBuildWindowSeconds = 180f;

        /// <summary>
        /// The factor <see cref="DeNelle.Village"/>'s WaveManager multiplies
        /// every <c>WaveDef.CountdownSeconds</c> by for the given difficulty.
        /// Easy stretches the build window, Hard compresses it; Normal is 1.0
        /// (the data stands as authored). Any unknown value falls back to Normal.
        /// </summary>
        public static float CountdownMultiplier(Difficulty difficulty)
        {
            switch (difficulty)
            {
                case Difficulty.Easy: return EasyBuildWindowSeconds / NormalBuildWindowSeconds; // 2.0
                case Difficulty.Hard: return HardBuildWindowSeconds / NormalBuildWindowSeconds; // 0.6
                default: return 1.0f;                                                          // Normal
            }
        }

        /// <summary>A short player-facing label for a difficulty (shown on the selector).</summary>
        public static string Label(Difficulty difficulty)
        {
            switch (difficulty)
            {
                case Difficulty.Easy: return "Easy";
                case Difficulty.Hard: return "Hard";
                default: return "Normal";
            }
        }

        /// <summary>
        /// A one-line description of what the difficulty does — shown under the
        /// selector so the player knows the choice changes the build window.
        /// </summary>
        public static string Blurb(Difficulty difficulty)
        {
            switch (difficulty)
            {
                case Difficulty.Easy:
                    return "A long, calm build window — about 10 minutes between waves.";
                case Difficulty.Hard:
                    return "A short, tense build window — about 3 minutes between waves.";
                default:
                    return "The standard build window — about 5 minutes between waves.";
            }
        }
    }
}
