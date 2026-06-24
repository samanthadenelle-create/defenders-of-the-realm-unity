// =============================================================================
// SpellVfxFactory — maps a spell / ability (id, effect, element) → a named
// VFXType, then plays it through the canonical VFXManager. WO-195.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHY THIS EXISTS (reconcile, not a 3rd stack):
//   The project's canonical VFX stack is VFXManager (prefab pooling + quality
//   gating + procedural AbilityVfxKit fallback) keyed by the VFXType enum.
//   Hero casting already routes through AbilityVfxKit.PlayHeroAbility -> a
//   (class, AbilityEffect) -> VFXType lookup, but that lookup is partial and
//   element-blind, so distinct spells (fire/frost/arcane/holy) reuse the same
//   generic effect.
//
//   SpellVfxFactory is the ONE place that turns a spell into its three VFX
//   beats — cast flash, projectile, impact — choosing the right element-coded
//   VFXType. It does NOT pool, gate quality, or spawn particles itself: it
//   delegates every Play to VFXManager (which pools / quality-gates / bridges
//   audio / falls back to AbilityVfxKit). Mirrors the catalog->factory pattern
//   used elsewhere (StructureFactory, EnemyAnimatorFactory).
//
//   WebGL-safe: no UXML, no Resources.Load here (VFXManager owns prefab loads
//   from the wired VFXCatalog, with a procedural fallback when none is wired).
//
// USAGE (from a cast site):
//   SpellVfxFactory.PlayCast(def.EffectEnum, heroClass, def.UnityColor, castPos);
//   SpellVfxFactory.PlayImpact(def.EffectEnum, heroClass, def.UnityColor, hitPos);
//   // or element-first when you don't have an AbilityDef:
//   SpellVfxFactory.PlayCast(SpellElement.Frost, castPos);
// =============================================================================

using UnityEngine;
using DeNelle.Core.Diagnostics;   // §12 TGVRU: trace the spell-VFX delegation

namespace DeNelle.Village
{
    /// <summary>
    /// Element a spell reads as — drives the colour-coded VFX pick. Derived from
    /// the ability's effect + accent colour when an explicit element isn't given.
    /// </summary>
    public enum SpellElement
    {
        Arcane = 0,  // white-violet (Aether) — default caster element
        Fire   = 1,  // red-orange (Flame) — meteor, cinder
        Frost  = 2,  // pale-blue (Ice) — nova, snare-chill
        Holy   = 3,  // warm gold-green — heals, wards
        Physical = 4 // steel — knight melee
    }

    /// <summary>
    /// Stateless router: (spell effect/element/class) -> VFXType, played through
    /// the canonical <see cref="VFXManager"/>. No pooling / particles of its own.
    /// WO-195.
    /// </summary>
    public static class SpellVfxFactory
    {
        // ── Public Play API ───────────────────────────────────────────────────

        /// <summary>
        /// Play the cast / wind-up flash for a hero ability. Routes to a
        /// class- and element-appropriate Cast_* VFXType via VFXManager (pooled
        /// prefab when wired, procedural otherwise). Null-safe.
        /// </summary>
        public static void PlayCast(AbilityEffect effect, string heroClass, Color accent, Vector3 position)
        {
            var element = ResolveElement(effect, heroClass, accent);
            PlayCast(element, position);
        }

        /// <summary>Element-first cast flash (use when no AbilityDef is on hand).</summary>
        public static void PlayCast(SpellElement element, Vector3 position)
        {
            var type = CastTypeFor(element);
            // U §12: VFXManager.Play null-guards Instance internally, so a null manager makes the
            // cast flash SILENTLY no-op (spell looks like it does nothing). Trace the delegation
            // (Throttled — combat hot path) and Once-report a missing manager so it self-detects.
            WarnIfNoManager("PlayCast", type);
            FlowTrace.Throttle("SpellVfx", $"cast:{element}", 1f,
                $"PlayCast element={element} -> {type} at {position}.");
            VFXManager.Play(type, position + Vector3.up * 1.2f);
        }

        /// <summary>
        /// Play the impact / detonation burst at the spell's landing point. For
        /// AoE/meteor spells pass the blast centre; for single-target pass the foe.
        /// </summary>
        public static void PlayImpact(AbilityEffect effect, string heroClass, Color accent, Vector3 position)
        {
            var element = ResolveElement(effect, heroClass, accent);
            var type = ImpactTypeFor(element, effect);
            WarnIfNoManager("PlayImpact", type);
            FlowTrace.Throttle("SpellVfx", $"impact:{element}", 1f,
                $"PlayImpact effect={effect} element={element} -> {type} at {position}.");
            VFXManager.Play(type, position);
        }

        /// <summary>Element-first impact burst.</summary>
        public static void PlayImpact(SpellElement element, Vector3 position)
        {
            var type = ImpactTypeFor(element, AbilityEffect.Strike);
            WarnIfNoManager("PlayImpact", type);
            FlowTrace.Throttle("SpellVfx", $"impact:{element}", 1f,
                $"PlayImpact element={element} -> {type} at {position}.");
            VFXManager.Play(type, position);
        }

        /// <summary>
        /// Attach a travelling projectile trail to a moving Transform (Mage orb /
        /// Ranger arrow / element bolt). Returns a VFXHandle — call handle.Stop()
        /// on hit. Null when no VFXManager. Mirrors VFXManager.PlayProjectile.
        /// </summary>
        public static VFXHandle PlayProjectile(AbilityEffect effect, string heroClass, Color accent, Transform projectile)
        {
            if (projectile == null)
            {
                FlowTrace.Warn("SpellVfx", "PlayProjectile: null projectile transform — no trail attached.");
                return null;
            }
            var element = ResolveElement(effect, heroClass, accent);
            var type = ProjectileTypeFor(element, heroClass);

            // U §12: the Instance?. short-circuit returns null SILENTLY when no VFXManager exists —
            // the projectile then flies with NO trail and the caller can't tell why. Trace it.
            if (VFXManager.Instance == null)
            {
                FlowTrace.Once("SpellVfx", "proj-nomanager",
                    $"PlayProjectile: VFXManager.Instance is null — projectile trail '{type}' will not appear.");
                return null;
            }

            FlowTrace.Throttle("SpellVfx", $"proj:{element}", 1f,
                $"PlayProjectile element={element} class={heroClass} -> {type}.");
            var handle = VFXManager.Instance.PlayProjectile(type, projectile);
            // R §12: a null handle = PlayProjectile fell through (loop-cap hit, or no catalog prefab
            // AND procedural-loop build failed) — projectile is SILENTLY trail-less. Self-report.
            if (handle == null)
                FlowTrace.Warn("SpellVfx",
                    $"PlayProjectile: PlayProjectile('{type}') returned a NULL handle — " +
                    "no trail (loop-cap hit or missing catalog prefab + failed procedural fallback).");
            return handle;
        }

        // §12 U helper: VFXManager.Play() swallows a null Instance internally (null-safe), so a
        // missing manager makes a cast/impact SILENTLY do nothing. Once-report per system so that
        // "the spell looks like it does nothing" surfaces in the break-log with the exact VFXType.
        private static void WarnIfNoManager(string where, VFXType type)
        {
            if (VFXManager.Instance == null)
                FlowTrace.Once("SpellVfx", "nomanager",
                    $"{where}: VFXManager.Instance is null — '{type}' will not appear (no VFXManager in scene).");
        }

        // ── Element resolution ────────────────────────────────────────────────

        /// <summary>
        /// Decide the spell's element from its effect, the caster class, and the
        /// ability accent colour (abilities.json "color"). Effect wins first
        /// (Frost Nova = aoe+freeze, Meteor = fire, Heal = holy), then class
        /// (Knight = physical), then the accent hue for everything else.
        /// </summary>
        public static SpellElement ResolveElement(AbilityEffect effect, string heroClass, Color accent)
        {
            switch (effect)
            {
                case AbilityEffect.Heal:
                    return SpellElement.Holy;
                case AbilityEffect.Meteor:
                    return SpellElement.Fire;     // Meteor Strike reads fire
                case AbilityEffect.Aoe:
                    // Mage Frost Nova is the canonical aoe (freeze) — frost. A
                    // Ranger volley (Storm of Arrows) is physical; let class decide.
                    if (IsClass(heroClass, "ranger")) return SpellElement.Physical;
                    return SpellElement.Frost;
            }

            // Class overrides for the remaining strike/snare/cleave shapes.
            if (IsClass(heroClass, "knight")) return SpellElement.Physical;
            if (IsClass(heroClass, "ranger")) return SpellElement.Physical;

            // Caster classes (mage/cleric) — read the accent hue to split
            // fire / frost / arcane so each bolt is element-coded.
            return ElementFromColor(accent);
        }

        /// <summary>Classify an ability accent colour into an element bucket.</summary>
        private static SpellElement ElementFromColor(Color c)
        {
            // Heuristic hue split — matches abilities.json palette:
            //   #b388ff arcane-violet, #7dd3fc/#9ae6b4 frost-cyan, #ff7043 fire-orange.
            if (c.r > 0.65f && c.g < 0.6f && c.b < 0.5f) return SpellElement.Fire;   // orange/red dominant
            if (c.b > 0.7f && c.r < 0.75f && c.b >= c.r) return SpellElement.Frost;  // cyan/blue dominant
            if (c.r > 0.55f && c.b > 0.75f)              return SpellElement.Arcane; // violet (r+b high)
            return SpellElement.Arcane;
        }

        private static bool IsClass(string heroClass, string id)
            => !string.IsNullOrEmpty(heroClass)
               && heroClass.Trim().ToLowerInvariant() == id;

        // ── Element -> VFXType maps (reuse the existing VFXType vocabulary) ─────

        private static VFXType CastTypeFor(SpellElement element) => element switch
        {
            // Battle-polish: a fire cast now reads with its OWN gathering-ember charge
            // (Cast_FireCharge), not the arcane-violet MageCharge — so the Knight's
            // Radiant Strike / a Meteor wind-up looks fiery instead of purple.
            SpellElement.Fire     => VFXType.Cast_FireCharge,
            SpellElement.Frost    => VFXType.Cast_FrostNova,
            SpellElement.Holy     => VFXType.Cast_Heal,
            SpellElement.Physical => VFXType.Cast_KnightSlam,
            _                     => VFXType.Cast_MageCharge,   // Arcane
        };

        private static VFXType ImpactTypeFor(SpellElement element, AbilityEffect effect) => element switch
        {
            // Big-area effects pick the larger explosion variant.
            SpellElement.Fire     => effect == AbilityEffect.Meteor
                                        ? VFXType.Impact_ExplosionFire
                                        : VFXType.Impact_Flame,
            SpellElement.Frost    => VFXType.Impact_Ice,
            SpellElement.Holy     => VFXType.Impact_Heal,
            SpellElement.Physical => effect == AbilityEffect.Aoe || effect == AbilityEffect.Cleave
                                        ? VFXType.Impact_ShockwaveRing
                                        : VFXType.Impact_Physical,
            _                     => effect == AbilityEffect.Aoe || effect == AbilityEffect.Cleave
                                        ? VFXType.Impact_ExplosionAether
                                        : VFXType.Impact_Aether,
        };

        private static VFXType ProjectileTypeFor(SpellElement element, string heroClass)
        {
            if (IsClass(heroClass, "ranger"))
                return element == SpellElement.Fire ? VFXType.Projectile_FlameArrow : VFXType.Projectile_Arrow;

            return element switch
            {
                SpellElement.Fire  => VFXType.Projectile_FlameArrow,
                SpellElement.Frost => VFXType.Projectile_FrostBolt,
                _                  => VFXType.Projectile_ArcaneBolt,
            };
        }
    }
}
