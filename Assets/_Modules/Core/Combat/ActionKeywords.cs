// =============================================================================
// ActionKeywords — canonical motion-casting keyword vocabulary (WO-670 slice 1).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Combat
//
// The compile-time MIRROR of the `vocabulary` block in
// Assets/StreamingAssets/Data/Canonical/motion-castings.json (the declaration —
// see docs/ACTION_KEYWORD_REGISTRY_ARCHITECTURE.md §2). CLOSED vocabulary, not
// open strings: two agents inventing `attackA` vs `attack_0` is the VFX-two-stack
// scar in data form. An EditMode test (MotionCastingsTests) asserts JSON
// vocabulary == these constants — drift fails the gate, so there is effectively
// one source.
//
// A new keyword = json version bump + vocabulary row there + a const here + the
// one reader that consumes it (add-by-entry discipline). Pure consts — no Unity
// dependency; sits next to AnimParams (the animator PARAMETER vocabulary; this
// is the motion KEYWORD vocabulary — different axes, same single-source rule).
// =============================================================================

namespace DeNelle.Core.Combat
{
    /// <summary>
    /// Canonical (target, keyword) motion vocabulary — the compile-time mirror of
    /// motion-castings.json's <c>vocabulary</c> block. Reference these instead of
    /// literal keyword strings.
    /// </summary>
    public static class ActionKeywords
    {
        // ── locomotion ───────────────────────────────────────────────────────
        public const string Idle        = "idle";
        public const string Walk        = "walk";
        public const string Run         = "run";
        public const string CombatIdle  = "combatIdle";
        public const string CombatWalk  = "combatWalk";
        public const string CombatRun   = "combatRun";
        public const string InjuredIdle = "injuredIdle";
        public const string InjuredWalk = "injuredWalk";
        public const string InjuredRun  = "injuredRun";

        // ── attack ───────────────────────────────────────────────────────────
        public const string Attack0 = "attack0";
        public const string Attack1 = "attack1";
        public const string Attack2 = "attack2";
        public const string Attack3 = "attack3";
        public const string Heavy   = "heavy";
        public const string Skill1  = "skill1";
        public const string Skill2  = "skill2";

        // ── cast ─────────────────────────────────────────────────────────────
        public const string Cast        = "cast";
        public const string CastChannel = "castChannel";
        /// <summary>Heal/ward cast (the hero's E-slot heal state) — added with the
        /// owner heal-cast pick 2026-07-11 (json vocabulary version 2); reader =
        /// KnightPackageControllerBuilder's Cast_e slot wrap. Melee/caster hard
        /// rule: heal-type actions fire a CAST clip, never a swing (F8-48).</summary>
        public const string CastHeal    = "castHeal";

        // ── reaction ─────────────────────────────────────────────────────────
        public const string Hit       = "hit";
        public const string Block     = "block";
        public const string Parry     = "parry";
        public const string Dodge     = "dodge";
        public const string Knockdown = "knockdown";
        public const string GettingUp = "gettingUp";

        // ── death ────────────────────────────────────────────────────────────
        public const string Death0 = "death0";
        public const string Death1 = "death1";
        public const string Death2 = "death2";
        public const string Death3 = "death3";
        public const string Death4 = "death4";
        public const string Death5 = "death5";

        // ── signature ────────────────────────────────────────────────────────
        public const string Taunt     = "taunt";
        public const string Unsheathe = "unsheathe";
        public const string Victory   = "victory";
        public const string WindUp    = "windup";

        // ── Category views (mirror the json vocabulary categories exactly) ───
        public static readonly string[] LocomotionKeywords =
        {
            Idle, Walk, Run, CombatIdle, CombatWalk, CombatRun,
            InjuredIdle, InjuredWalk, InjuredRun,
        };

        public static readonly string[] AttackKeywords =
        {
            Attack0, Attack1, Attack2, Attack3, Heavy, Skill1, Skill2,
        };

        public static readonly string[] CastKeywords =
        {
            Cast, CastChannel, CastHeal,
        };

        public static readonly string[] ReactionKeywords =
        {
            Hit, Block, Parry, Dodge, Knockdown, GettingUp,
        };

        public static readonly string[] DeathKeywords =
        {
            Death0, Death1, Death2, Death3, Death4, Death5,
        };

        public static readonly string[] SignatureKeywords =
        {
            Taunt, Unsheathe, Victory, WindUp,
        };

        /// <summary>Every keyword in the closed vocabulary (all categories).</summary>
        public static readonly string[] All = BuildAll();

        private static string[] BuildAll()
        {
            var all = new string[LocomotionKeywords.Length + AttackKeywords.Length +
                                 CastKeywords.Length + ReactionKeywords.Length +
                                 DeathKeywords.Length + SignatureKeywords.Length];
            int i = 0;
            foreach (var set in new[] { LocomotionKeywords, AttackKeywords, CastKeywords,
                                        ReactionKeywords, DeathKeywords, SignatureKeywords })
                foreach (string kw in set)
                    all[i++] = kw;
            return all;
        }
    }
}
