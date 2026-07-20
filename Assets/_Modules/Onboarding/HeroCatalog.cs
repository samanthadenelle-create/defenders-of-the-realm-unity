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

        // ── WO-329: stat-card presentation data ─────────────────────────────
        // Pure UI presentation values for the hero-select STAT CARD. HP/Attack/
        // Speed are 1-5 "pip" ratings (a glanceable archetype read, not raw combat
        // numbers — those live in the Village combat data which Onboarding does not
        // reference). The signature ability name + its one-line effect are mirrored
        // from Resources/Data/Canonical/abilities.json (mage/knight/ranger; Elara
        // authored to match her Divine Healer role) so the card shows real,
        // in-game-consistent copy without a cross-module JSON dependency.

        /// <summary>HP rating, 1-5 pips (survivability archetype).</summary>
        public readonly int Hp;
        /// <summary>Attack rating, 1-5 pips (damage archetype).</summary>
        public readonly int Attack;
        /// <summary>Speed rating, 1-5 pips (mobility / cast-rate archetype).</summary>
        public readonly int Speed;
        /// <summary>Signature ability display name (e.g. "Frost Nova").</summary>
        public readonly string AbilityName;
        /// <summary>One-line signature ability effect blurb.</summary>
        public readonly string AbilityDesc;

        // ── Primary skill kit (hero-select detail panel) ────────────────────
        // The hero's Q/W/E/R primary skills, MIRRORED VERBATIM (slot key + name)
        // from Resources/Data/Canonical/abilities.json — the same source the
        // signature ability is drawn from — so the hero-select "Primary Skills"
        // panel shows the real, in-game kit without a cross-module JSON load
        // (Onboarding references DeNelle.Core only; see the file header). May be
        // empty for a hero whose ability set is not yet authored in abilities.json
        // (e.g. the Cleric) — the UI shows a labelled placeholder in that case.

        /// <summary>The hero's primary Q/W/E/R skills (verbatim from abilities.json); may be empty.</summary>
        public readonly HeroSkillInfo[] PrimarySkills;

        public HeroCardInfo(HeroClass hero, string nameKey, string roleKey,
                            string blurbKey, string glyph, Color accent,
                            int hp, int attack, int speed,
                            string abilityName, string abilityDesc,
                            HeroSkillInfo[] primarySkills = null)
        {
            Hero = hero;
            NameKey = nameKey;
            RoleKey = roleKey;
            BlurbKey = blurbKey;
            Glyph = glyph;
            Accent = accent;
            Hp = hp;
            Attack = attack;
            Speed = speed;
            AbilityName = abilityName;
            AbilityDesc = abilityDesc;
            PrimarySkills = primarySkills ?? System.Array.Empty<HeroSkillInfo>();
        }
    }

    /// <summary>
    /// One primary-skill entry for the hero-select detail panel — the slot key
    /// (Q / F / E / R) plus the ability's display name. Mirrored verbatim from
    /// Resources/Data/Canonical/abilities.json (no narrative authored here).
    /// </summary>
    public sealed class HeroSkillInfo
    {
        /// <summary>The slot key as shown on the ability bar (Q / F / E / R).</summary>
        public readonly string Slot;
        /// <summary>The ability's display name (e.g. "Frost Nova").</summary>
        public readonly string Name;

        public HeroSkillInfo(string slot, string name)
        {
            Slot = slot;
            Name = name;
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
                "T", new Color(0.45f, 0.75f, 1.0f),       // Thrain — icy blue
                hp: 2, attack: 5, speed: 3,
                abilityName: "Frost Nova",
                abilityDesc: "Freezing burst — 26 dmg + freeze in a ring.",
                primarySkills: new[]
                {
                    new HeroSkillInfo("Q", "Arcane Bolt"),
                    new HeroSkillInfo("F", "Frost Nova"),
                    new HeroSkillInfo("E", "Healing Beacon"),
                    new HeroSkillInfo("R", "Meteor Strike"),
                }),
            new HeroCardInfo(
                HeroClass.Knight, "hero.knight.name", "hero.knight.role", "hero.knight.blurb",
                "G", new Color(0.98f, 0.84f, 0.40f),      // Grom — holy gold
                hp: 5, attack: 3, speed: 2,
                // WO-750 (owner 2026-07-19): mirror the LIVE knight kit names from
                // abilities.json (Sword Heroic / Shield Charge / Warden's Grace / Radiant Strike)
                // + the combat Q/W/E/R slot letters. This is the hand-mirror kept in sync
                // (Onboarding references DeNelle.Core only, so it can't read AbilityCatalog in
                // DeNelle.Village — a true single source needs an asmdef ref or a Core-side
                // abilities reader; see the WO-750 report). Signature = the W-slot ability
                // (matches the mage/ranger convention: Frost Nova / Snare Trap).
                abilityName: "Shield Charge",
                abilityDesc: "Charge behind your shield — knocks back, slows, breaks guard.",
                primarySkills: new[]
                {
                    new HeroSkillInfo("Q", "Sword Heroic"),
                    new HeroSkillInfo("W", "Shield Charge"),
                    new HeroSkillInfo("E", "Warden's Grace"),
                    new HeroSkillInfo("R", "Radiant Strike"),
                }),
            new HeroCardInfo(
                HeroClass.Ranger, "hero.ranger.name", "hero.ranger.role", "hero.ranger.blurb",
                "S", new Color(0.41f, 0.74f, 0.48f),      // Sylas — wood-green
                hp: 3, attack: 4, speed: 5,
                abilityName: "Snare Trap",
                abilityDesc: "Snares foes at range and deals damage.",
                primarySkills: new[]
                {
                    new HeroSkillInfo("Q", "Quick Shot"),
                    new HeroSkillInfo("F", "Snare Trap"),
                    new HeroSkillInfo("E", "Mending Salve"),
                    new HeroSkillInfo("R", "Storm of Arrows"),
                }),
            new HeroCardInfo(
                HeroClass.Cleric, "hero.cleric.name", "hero.cleric.role", "hero.cleric.blurb",
                "E", new Color(1.0f, 0.93f, 0.70f),       // Elara — warm white-gold
                hp: 4, attack: 2, speed: 3,
                abilityName: "Sacred Mending",
                abilityDesc: "Heals the Heart and wards the faithful."),
        };
    }
}
