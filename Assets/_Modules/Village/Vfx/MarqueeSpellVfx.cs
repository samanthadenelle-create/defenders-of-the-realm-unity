// =============================================================================
// MarqueeSpellVfx — the ONE declaration of which owner-tagged VFX keys are
// SELF-CONTAINED (marquee) spells: the prefab owns cast, flight AND impact, so
// the engine's projectile spawn must be suppressed for that cast.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
// WO-1305 Part A (owner ruling 2026-09-02).
//
// WHY THIS EXISTS
//   Almost every effect key in HovlVfxCatalog is a ROLE COMPONENT: a `_Cast`
//   key is the wind-up flash only, and the ability system then flies its own
//   body (HeroAbilities.LaunchProjectile -> RangedAttackVFX.FireSpellOrb /
//   FireArrow / FlyCosmeticProjectile) to the real target. A handful of pack
//   prefabs are NOT role components — they are whole shows. Spell_Fire_9 is the
//   owner's example: she watched it in the VFX Caster and reported "the way it
//   displays is there is a wind up directly into projectiles flying and
//   bouncing", then chose "Make it a marquee spell".
//
//   Tagged as a plain `_Cast` with no declaration, such a prefab produces TWO
//   projectiles per cast: the prefab's own authored fireballs, plus the engine's
//   orb travelling to the actual target. This class is the single place that
//   says "this key is the whole show" so exactly one body flies.
//
// WHAT THIS IS NOT
//   * NOT a second spawner and NOT a second pool. Marquee keys play through the
//     SAME VFXManager.PlayKey pooled path as every other key (one owner per
//     concern; ARCHITECTURE_PRINCIPLES 2b/2b.1 — Assets/_Modules/Village/Vfx is
//     already scar tissue from a second VFX stack). This class holds a set of
//     strings and answers a question. It never instantiates anything.
//   * NOT a creative pick. A key appears below ONLY because the owner tagged
//     that prefab to that key in her VFX Caster (VfxManualPicks.json,
//     manual:true) AND ruled the effect a marquee. Never add a key here from a
//     CLI judgement call — project memory `vfx-map-owner-tags-no-creative-pick`.
//
// HOW A MARQUEE KEY REACHES A CAST
//   The key still has to be bound to an ability by the owner, exactly like any
//   other motion VFX: a motion-castings.json row's `vfxKey` for the cast
//   keyword (registry-only mode, HeroAbilities.RegistryOnlyMotionVfx = true), or
//   an individually owner-tagged abilities.json VfxCast in
//   HeroAbilities.OwnerPickedVfxKeys. Declaring a key here does NOT bind it to
//   anything — it only says what happens once she does bind it.
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// Registry of SELF-CONTAINED ("marquee") VFX catalog keys — prefabs that own
    /// the whole cast → flight → impact show themselves, so the ability system must
    /// not also fly its own projectile body for that cast. Pure lookup: it spawns
    /// nothing and owns no pool (<see cref="VFXManager.PlayKey"/> stays the one
    /// spawn owner).
    /// </summary>
    public static class MarqueeSpellVfx
    {
        // Named TraceSystem, not System: a `System` const here would shadow the
        // System namespace and break StringComparer.Ordinal below.
        private const string TraceSystem = "Vfx";

        /// <summary>
        /// The declared marquee keys. EVERY entry is an OWNER TAG, not a CLI pick.
        ///
        /// `firespell_Cast` — owner-tagged 2026-09-02 in the VFX Caster to
        /// `Assets/Spells Pack/Particles/Prefabs/Spells/Spell_Fire_9.prefab`
        /// (`Assets/Editor/VfxManualPicks.json` key "firespell_Cast", manual:true,
        /// isLoop:false; baked at `Assets/Resources/VFX/HovlVfxCatalog.asset` with
        /// IsLoop: 0, which is the hard prerequisite — 4 of its 7 emitters are
        /// authored looping and only the IsLoop:0 row lets
        /// VFXManager.EnforceOneshotEmission clear them). She ruled it a marquee
        /// spell after watching it wind up straight into its own flying, bouncing
        /// fireballs.
        /// </summary>
        private static readonly HashSet<string> Keys =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "firespell_Cast",
            };

        /// <summary>
        /// True when <paramref name="vfxKey"/> names a self-contained marquee effect —
        /// the caller must SUPPRESS its own projectile spawn for that cast. Null/empty
        /// is false (a cast with no authored key is silent-by-design, not a marquee).
        /// </summary>
        public static bool IsMarquee(string vfxKey)
            => !string.IsNullOrEmpty(vfxKey) && Keys.Contains(vfxKey);

        /// <summary>
        /// Declares, once per key, that a cast has been recognised as a marquee — so a
        /// log read after the fact can tell "the prefab owned the show" apart from "the
        /// projectile never spawned" (§12: a suppression that leaves no trace is
        /// indistinguishable from a broken projectile).
        /// </summary>
        public static void TraceRecognised(string vfxKey, string abilityId)
        {
            if (string.IsNullOrEmpty(vfxKey)) return;
            FlowTrace.Once(TraceSystem, "marquee:" + vfxKey,
                $"marquee VFX '{vfxKey}' recognised for cast '{abilityId ?? "<unknown>"}' — the prefab " +
                "owns cast+flight+impact; the engine projectile spawn is SUPPRESSED for this ability.");
        }

        /// <summary>All declared marquee keys — regression/tooling read-only view.</summary>
        public static IReadOnlyCollection<string> DeclaredKeys => Keys;
    }
}
