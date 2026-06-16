// =============================================================================
// IActorAnimator — the verb layer that drives actor animation (WO-284).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Combat
//
// Gameplay code calls these VERBS ("the hero attacks", "took a hit", "died")
// instead of poking animator parameters directly. The concrete ActorAnimator is
// the ONLY place that calls SetTrigger/SetBool/SetFloat/SetInteger for an actor,
// and every call is null/param guarded — a controller missing a given state makes
// the verb a safe no-op (e.g. a Ranger with no Block).
// =============================================================================

namespace DeNelle.Core.Combat
{
    /// <summary>Verb-level animation driver shared by heroes, enemies, pets.</summary>
    public interface IActorAnimator
    {
        /// <summary>Locomotion blend input — RAW world units/sec (not 0..1).</summary>
        void SetLocomotion(float worldSpeed);

        /// <summary>Idle vs combat-idle stance.</summary>
        void SetCombatStance(bool inCombat);

        /// <summary>Primary melee. <paramref name="combo"/> selects the swing in a combo (0-based).</summary>
        void PlayAttack(int combo = 0);

        /// <summary>Spell cast (casters). Plays the generic cast (variant 0).</summary>
        void PlayCast();

        /// <summary>
        /// Spell cast with a specific clip <paramref name="variant"/> — lets each spell
        /// (q/w/e/r → 1..4) play its own cast animation. Sets the <c>CastVariant</c> int
        /// then fires the <c>Cast</c> trigger; variant 0 = the generic cast. A controller
        /// with no per-variant state simply falls back to its default Cast state (no-op safe).
        /// </summary>
        void PlayCast(int variant);

        /// <summary>Telegraph wind-up before an attack lands.</summary>
        void PlayWindUp();

        /// <summary>Hold (or release) a block.</summary>
        void SetBlocking(bool on);

        /// <summary>Flinch from a hit coming from <paramref name="dir"/>.</summary>
        void PlayHit(HitDirection dir);

        /// <summary>Latch the death state (Dead = true) falling <paramref name="dir"/>.</summary>
        void Die(DeathDirection dir = DeathDirection.Fall);

        /// <summary>Clear the death latch (respawn paths).</summary>
        void Revive();

        /// <summary>Victory pose.</summary>
        void PlayVictory();

        /// <summary>Sharp turn (optional locomotion polish).</summary>
        void PlayTurn(TurnDirection dir);

        /// <summary>Pet emote; no-op if the controller has no Emote param.</summary>
        void PlayEmote(EmoteType emote);
    }
}
