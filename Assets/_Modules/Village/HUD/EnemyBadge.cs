// =============================================================================
// EnemyBadge (WO-1232, owner ruling 2026-08-26) — the ONE map from an enemy's
// AUTHORED classification to the word the player reads.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Hud
//
// WHY THIS EXISTS AS ITS OWN (TINY) FILE
//   The numeric enemy level is REMOVED. Owner verbatim: "HP / 25 is not a level
//   system. Dressing it up as one just produces very confident nonsense." What the
//   HUD target frame shows beside the name is now IDENTITY, not difficulty:
//       boss:true      -> "BOSS"
//       role:"elite"   -> "ELITE"
//       anything else  -> ""  (NOTHING renders — silence is the default, never a
//                              blank label the player has to interpret)
//
//   It is a public, single-purpose entry point rather than a private helper inside
//   TargetProducer so the regression oracle can DRIVE it instead of source-linting a
//   mapping it cannot call (an internal producer is invisible to the Editor assembly,
//   and a lint-only pin on player-visible copy is exactly the hollow pass CLAUDE.md
//   §12 forbids).
//
// TWO RULES THIS FILE HOLDS
//   1. NO APEX ARM. waves.json authors an `apexBoss`, but until a tier is authored
//      deliberately a third badge re-invents the fake precision the ruling removed.
//   2. A WORD, NEVER A TINT (the owner is red/green colourblind), and ASCII only
//      (TMP glyph landmine). Do not return an icon, a colour tag, or rich text.
//
// NOT A DIFFICULTY MODEL. The replacement the owner named — a Combat Rating from HP,
// damage, attack cadence, armour, abilities and encounter role, surfaced as
// Low/Even/High/Deadly — is a SEPARATE, UNBUILT spec. Do not stub it here.
// =============================================================================

namespace DeNelle.Village.Hud
{
    /// <summary>
    /// Maps <see cref="EnemyTier"/> (the authored def flags) to the target frame's badge word.
    /// The single source of that copy — every surface that wants to say "what am I facing"
    /// calls this, so there is never a second, drifting spelling of BOSS.
    /// </summary>
    public static class EnemyBadge
    {
        /// <summary>The word BOSS, as the player reads it. ASCII, uppercase, no decoration.</summary>
        public const string Boss = "BOSS";

        /// <summary>The word ELITE, as the player reads it.</summary>
        public const string Elite = "ELITE";

        /// <summary>
        /// The authored badge for <paramref name="enemy"/>: <see cref="Boss"/>, <see cref="Elite"/>,
        /// or an EMPTY string for an ordinary (or null / def-less) enemy — which the frame renders
        /// as nothing at all.
        /// </summary>
        public static string For(Enemy enemy)
        {
            if (enemy == null) return string.Empty;
            return For(enemy.Tier);
        }

        /// <summary>Tier overload — the actual mapping; <see cref="For(Enemy)"/> is the sugar.</summary>
        public static string For(EnemyTier tier)
        {
            switch (tier)
            {
                case EnemyTier.Boss:  return Boss;
                case EnemyTier.Elite: return Elite;
                default:              return string.Empty;   // ordinary: NOTHING is shown
            }
        }
    }
}
