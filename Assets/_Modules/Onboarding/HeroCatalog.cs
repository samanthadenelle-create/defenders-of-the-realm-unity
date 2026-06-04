// =============================================================================
// HeroCatalog — the three hero cards shown on the hero-select screen.
// -----------------------------------------------------------------------------
// The three heroes are a CODE enum (DeNelle.Core.State.HeroClass — Mage /
// Knight / Ranger), not a JSON content file: there is no heroes.json the way
// there is a pets.json. So this small static catalog supplies the per-hero
// PRESENTATION data the hero-select cards need —
//
//   * the en.json string KEYS for the hero's name / role / blurb (the actual
//     copy is never typed inline — it lives in en.json and is resolved at
//     runtime via CanonStrings, per port-spec Part 4), and
//   * a card glyph + an accent colour, which are pure UI presentation values
//     (not narrative content) and so legitimately live in C#.
//
// HeroSelectController builds one card per entry here, in catalog order.
// The chosen HeroClass is written to GameState via GameStateService.ChooseHero.
// =============================================================================

using DeNelle.Core.State;
using UnityEngine;

namespace DeNelle.Onboarding
{
    /// <summary>
    /// Presentation data for one hero card — the en.json string keys for its
    /// copy plus its card glyph and accent colour. Pure UI metadata; the canon
    /// copy itself is resolved from en.json at runtime.
    /// </summary>
    public sealed class HeroCardInfo
    {
        /// <summary>The hero class this card selects.</summary>
        public readonly HeroClass Hero;
        /// <summary>en.json key for the hero's display name (e.g. hero.mage.name).</summary>
        public readonly string NameKey;
        /// <summary>en.json key for the hero's short role line (e.g. hero.mage.role).</summary>
        public readonly string RoleKey;
        /// <summary>en.json key for the hero's blurb paragraph (e.g. hero.mage.blurb).</summary>
        public readonly string BlurbKey;
        /// <summary>A single-character glyph shown big in the card portrait block.</summary>
        public readonly string Glyph;
        /// <summary>The card's accent colour — the thin strip + active-card tint cue.</summary>
        public readonly Color Accent;

        public HeroCardInfo(HeroClass hero, string nameKey, string roleKey,
                            string blurbKey, string glyph, Color accent)
        {
            Hero = hero;
            NameKey = nameKey;
            RoleKey = roleKey;
            BlurbKey = blurbKey;
            Glyph = glyph;
            Accent = accent;
        }
    }

    /// <summary>
    /// The three hero cards, in display order, for the hero-select screen.
    /// </summary>
    public static class HeroCatalog
    {
        /// <summary>The hero cards in catalog order: Mage, Knight, Ranger, Cleric.</summary>
        public static readonly HeroCardInfo[] Heroes =
        {
            new HeroCardInfo(
                HeroClass.Mage, "hero.mage.name", "hero.mage.role", "hero.mage.blurb",
                "T", new Color(0.45f, 0.75f, 1.0f)),      // Thrain — icy blue
            new HeroCardInfo(
                HeroClass.Knight, "hero.knight.name", "hero.knight.role", "hero.knight.blurb",
                "G", new Color(0.98f, 0.84f, 0.40f)),     // Grom — holy gold
            new HeroCardInfo(
                HeroClass.Ranger, "hero.ranger.name", "hero.ranger.role", "hero.ranger.blurb",
                "S", new Color(0.41f, 0.74f, 0.48f)),     // Sylas — wood-green
            new HeroCardInfo(
                HeroClass.Cleric, "hero.cleric.name", "hero.cleric.role", "hero.cleric.blurb",
                "E", new Color(1.0f, 0.93f, 0.70f)),      // Elara — warm white-gold
        };
    }
}
