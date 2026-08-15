// =============================================================================
// CombatCast — unified cast presentation for hero, enemy, and troop casters.
// WO-935 Phase 1: one call sites use cast("fireball") / cast("heal", target).
// -----------------------------------------------------------------------------
// Presentation only + optional damage/heal hooks. Does NOT invent a second VFX
// catalog: routes through SpellVfxFactory → VFXManager and IActorAnimator.PlayCast.
// =============================================================================

using UnityEngine;
using DeNelle.Core.Combat;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>Shared cast entry: anim + VFX (+ optional effect) for any caster Transform.</summary>
    public static class CombatCast
    {
        public const string Fireball = "fireball";
        public const string Heal     = "heal";
        public const string Arcane   = "arcane_bolt";

        /// <summary>
        /// Play cast presentation for <paramref name="spellId"/> from <paramref name="caster"/>.
        /// Optional <paramref name="target"/> for projectile impact / heal landing.
        /// Optional <paramref name="onResolve"/> runs after presentation (damage/heal caller-owned).
        /// </summary>
        public static void Play(string spellId, Transform caster, Transform target = null,
                                System.Action onResolve = null)
        {
            if (caster == null)
            {
                FlowTrace.Warn("CombatCast", "Play REFUSED: caster is null");
                return;
            }

            string id = string.IsNullOrEmpty(spellId) ? Arcane : spellId.Trim().ToLowerInvariant();
            var actor = caster.GetComponentInChildren<ActorAnimator>(true)
                     ?? caster.GetComponentInParent<ActorAnimator>();
            if (actor != null) actor.PlayCast();
            else
            {
                // Troop/enemy may only have raw Animator with Cast trigger.
                var anim = caster.GetComponentInChildren<Animator>(true);
                if (anim != null && anim.runtimeAnimatorController != null)
                    anim.SetTrigger(AnimParams.CastHash);
            }

            Vector3 origin = caster.position + Vector3.up * 1.2f;
            Vector3 impact = target != null ? target.position + Vector3.up * 1.0f : origin + caster.forward * 4f;

            switch (id)
            {
                case Fireball:
                case "fire":
                    SpellVfxFactory.PlayCast(SpellElement.Fire, origin);
                    SpellVfxFactory.PlayImpact(SpellElement.Fire, impact);
                    break;
                case Heal:
                case "holy":
                    SpellVfxFactory.PlayCast(SpellElement.Holy, origin);
                    SpellVfxFactory.PlayImpact(SpellElement.Holy, target != null ? impact : origin);
                    break;
                case Arcane:
                case "arcane":
                default:
                    SpellVfxFactory.PlayCast(SpellElement.Arcane, origin);
                    if (target != null)
                        SpellVfxFactory.PlayImpact(SpellElement.Arcane, impact);
                    break;
            }

            FlowTrace.Throttle("CombatCast", "play:" + id, 0.5f,
                $"Play spell='{id}' caster='{caster.name}' target='{(target != null ? target.name : "<none>")}'");

            onResolve?.Invoke();
        }
    }
}
