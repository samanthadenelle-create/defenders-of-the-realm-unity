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
        /// <summary>Signature ability display name (the W-slot ability, e.g. "Arcane Shell").</summary>
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
    /// (Q / W / E / R) plus the ability's display name. Mirrored verbatim from
    /// Resources/Data/Canonical/abilities.json (no narrative authored here).
    /// ⛔ The ONLY legal slot keys are Q, W, E, R. "F" is NOT a slot — it appeared
    /// here for the mage and the ranger until WO-1166; F is the LEARNABLE-POOL key
    /// abilities.json uses inside classes.*-skills, not a bar slot the player has.
    /// </summary>
    public sealed class HeroSkillInfo
    {
        /// <summary>The slot key as shown on the ability bar (Q / W / E / R only).</summary>
        public readonly string Slot;
        /// <summary>The ability's display name (e.g. "Arcane Shell").</summary>
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
                // WO-1166 (owner ruling 2026-08-25, "update truth to match source"):
                // the advertised mage kit was Q Arcane Bolt / F Frost Nova / E Healing
                // Beacon / R Meteor Strike - three wrong names AND a slot letter ("F")
                // that is not a real slot. abilities.json classes.mage is the source and
                // ships Q Fireball / W Arcane Shell / E Drain / R Poison Cloud (WO-861 A1
                // owner-approved 2026-08-02, retuned by WO-1019 Part B: Mend -> Drain and
                // Meteor Strike -> Poison Cloud; the displaced spells moved into
                // classes.mage-skills, which is where "Frost Nova"/"Arcane Bolt" actually
                // live - LEARNABLE POOL entries, never this hero's default bar).
                // Signature = the W-slot ability, matching the knight/ranger convention.
                // Pinned by HeroKitMirrorRegression [hero-kit-mirror].
                abilityName: "Arcane Shell",
                abilityDesc: "Ward yourself - take 40% less damage for 4s.",
                primarySkills: new[]
                {
                    new HeroSkillInfo("Q", "Fireball"),
                    new HeroSkillInfo("W", "Arcane Shell"),
                    new HeroSkillInfo("E", "Drain"),
                    new HeroSkillInfo("R", "Poison Cloud"),
                }),
            new HeroCardInfo(
                HeroClass.Knight, "hero.knight.name", "hero.knight.role", "hero.knight.blurb",
                "G", new Color(0.98f, 0.84f, 0.40f),      // Grom — holy gold
                hp: 5, attack: 3, speed: 2,
                // WO-750 (owner 2026-07-19): mirror the LIVE knight kit names from
                // abilities.json + the combat Q/W/E/R slot letters. This is the hand-mirror kept
                // in sync (Onboarding references DeNelle.Core only, so it can't read AbilityCatalog
                // in DeNelle.Village — a true single source needs an asmdef ref or a Core-side
                // abilities reader; see the WO-750 report). Signature = the W-slot ability
                // (matches the mage/ranger convention: Arcane Shell / Snare Trap).
                // WO-1166: the W-slot name in abilities.json is "Shield Bash", not the
                // "Shield Charge" this card advertised (the def's own description still
                // opens "Charge behind your shield", which is how the wrong display name
                // survived). Name mirrored verbatim; the blurb keeps the def's wording.
                abilityName: "Shield Bash",
                abilityDesc: "Charge behind your shield - knocks back, slows, breaks guard.",
                primarySkills: new[]
                {
                    new HeroSkillInfo("Q", "Sword Heroic"),
                    new HeroSkillInfo("W", "Shield Bash"),
                    new HeroSkillInfo("E", "Warden's Grace"),
                    new HeroSkillInfo("R", "Radiant Strike"),
                }),
            new HeroCardInfo(
                HeroClass.Ranger, "hero.ranger.name", "hero.ranger.role", "hero.ranger.blurb",
                "S", new Color(0.41f, 0.74f, 0.48f),      // Sylas — wood-green
                hp: 3, attack: 4, speed: 5,
                // WO-1166: Snare Trap is the W slot, not "F" (there is no F slot - the bar
                // is Q/W/E/R, Q locked, W/E/R loadout-swappable). E ships as "Healing Shot";
                // "Mending Salve" is a KNIGHT-SKILLS pool entry and was never on this bar.
                abilityName: "Snare Trap",
                abilityDesc: "Roots and slows a foe - 18 dmg at 12m.",
                primarySkills: new[]
                {
                    new HeroSkillInfo("Q", "Quick Shot"),
                    new HeroSkillInfo("W", "Snare Trap"),
                    new HeroSkillInfo("E", "Healing Shot"),
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
