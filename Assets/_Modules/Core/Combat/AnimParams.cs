// =============================================================================
// AnimParams — canonical animator parameter names + hashes for ALL actors (WO-284).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Combat
//
// The single source of truth for actor animator parameters. Every controller the
// factories build AND every caller (via ActorAnimator) uses THESE names/hashes —
// no more per-class `Animator.StringToHash("Speed")` drifting out of sync.
//
// Speed is RAW world units/sec (NOT 0..1): the hero/enemy controllers blend on
// Velocity.magnitude with thresholds tuned around it (idle 0 / walk ~6 / run ~9),
// so normalising here would break the WO-283 locomotion blend. Keep it raw.
//
// Death is the canonical `Dead` BOOL latch (+ `DeathDir`), replacing the older
// `Death` trigger convention (PetAnimatorController) so a dead actor stays down
// instead of flickering back to idle.
// =============================================================================

using UnityEngine;

namespace DeNelle.Core.Combat
{
    /// <summary>Which quadrant a hit came from, relative to the victim's facing.</summary>
    public enum HitDirection { Front = 0, Left = 1, Right = 2, Gut = 3 }

    /// <summary>Which way the actor falls on death. 3-5 added 2026-07-03 for the Paladin
    /// hero package's DIRECTIONAL DEATHS (owner design): the DeathDir int selector in
    /// KnightPackage.controller maps 0 Fall / 1 Left / 2 Right / 3 Front / 4 Back /
    /// 5 Assassinate. Additive — old values unchanged; the runtime driver is a follow-up
    /// (undriven, DeathDir stays 0 = Fall, the default death, which is safe).</summary>
    public enum DeathDirection { Fall = 0, Left = 1, Right = 2, Front = 3, Back = 4, Assassinate = 5 }

    /// <summary>Sharp facing-change turn direction (optional locomotion polish).</summary>
    public enum TurnDirection { None = 0, Left = -1, Right = 1 }

    /// <summary>Pet emote ids.</summary>
    public enum EmoteType { None = 0, Happy = 1, Celebrate = 2, Alert = 3 }

    /// <summary>
    /// Canonical animator parameter names + cached hashes. Reference these instead
    /// of re-declaring local <c>StringToHash</c> in actor classes.
    /// </summary>
    public static class AnimParams
    {
        // ── Names (must match the strings the animator factories declare) ────────
        public const string Speed    = "Speed";     // Float  — locomotion blend (raw world u/s)
        public const string InCombat = "InCombat";  // Bool   — idle vs combat idle
        public const string Attack   = "Attack";    // Trigger— primary melee
        public const string Combo    = "Combo";     // Int    — combo index (Knight)
        public const string Cast     = "Cast";      // Trigger— spell cast (casters)
        public const string CastVariant = "CastVariant"; // Int — which spell/cast clip (0 = generic; q/w/e/r = 1..4)
        public const string WindUp   = "WindUp";    // Trigger— telegraph before attack
        public const string Block    = "Block";     // Bool   — hold block
        public const string Injured  = "Injured";   // Bool   — wounded stance (low HP) locomotion swap
        public const string Hit      = "Hit";       // Trigger— hit reaction
        public const string HitDir   = "HitDir";    // Int    — HitDirection
        public const string Dead     = "Dead";      // Bool   — canonical death latch
        public const string DeathDir = "DeathDir";  // Int    — DeathDirection
        public const string Victory  = "Victory";   // Trigger— victory pose
        public const string TurnDir  = "TurnDir";   // Int    — TurnDirection
        public const string Emote    = "Emote";     // Int    — EmoteType (pets)

        // ── Hashes (cached) ──────────────────────────────────────────────────────
        public static readonly int SpeedHash    = Animator.StringToHash(Speed);
        public static readonly int InCombatHash  = Animator.StringToHash(InCombat);
        public static readonly int AttackHash    = Animator.StringToHash(Attack);
        public static readonly int ComboHash     = Animator.StringToHash(Combo);
        public static readonly int CastHash      = Animator.StringToHash(Cast);
        public static readonly int CastVariantHash = Animator.StringToHash(CastVariant);
        public static readonly int WindUpHash    = Animator.StringToHash(WindUp);
        public static readonly int BlockHash     = Animator.StringToHash(Block);
        public static readonly int InjuredHash   = Animator.StringToHash(Injured);
        public static readonly int HitHash       = Animator.StringToHash(Hit);
        public static readonly int HitDirHash    = Animator.StringToHash(HitDir);
        public static readonly int DeadHash      = Animator.StringToHash(Dead);
        public static readonly int DeathDirHash  = Animator.StringToHash(DeathDir);
        public static readonly int VictoryHash   = Animator.StringToHash(Victory);
        public static readonly int TurnDirHash   = Animator.StringToHash(TurnDir);
        public static readonly int EmoteHash     = Animator.StringToHash(Emote);
    }
}
