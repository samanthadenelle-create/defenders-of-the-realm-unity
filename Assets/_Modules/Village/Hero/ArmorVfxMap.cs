// =============================================================================
// ArmorVfxMap — WO-543: pure rarity -> hero rim-light glow resolver.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// The armor/accessory channel's analog of WeaponVfxMap (which owns the swing
// TRAIL). Rings + amulets have NO mesh, so a legendary accessory must READ
// legendary through a RIM-LIGHT glow on the hero's SkinnedMeshRenderer — driven
// here from the DOMINANT rarity across the equipped armor + ring + amulet, with an
// optional makersMark theme tint (same blend pattern as WeaponVfxMap).
//
// PURE: no MonoBehaviour, no I/O, no cross-assembly service calls. Deterministic.
// HeroArmorRimLight applies the returned profile via MaterialPropertyBlock on the
// hero mesh at equip / on OnGearChanged.
//
//   *** COLORS + INTENSITIES ARE BONES FOR OWNER FELT-TUNING ***
//   Every color/intensity below is a NAMED static-readonly / const. The mapping is
//   self-asserted by DataRegression (distinct color per band, gold == GoldColor,
//   intensities escalate, common == off).
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>The resolved rim-light look for the equipped armor/accessory set: a rim color,
    /// an intensity (0 = off), and whether the legendary apex burst VFX should play. Returned by
    /// <see cref="ArmorVfxMap.Resolve(ArmorDef, AccessoryDef, AccessoryDef)"/>.</summary>
    public readonly struct ArmorVfxProfile
    {
        /// <summary>Rim-light color (RGB). Alpha is unused (intensity carries strength).</summary>
        public readonly Color RimColor;

        /// <summary>Rim-light intensity 0..1 (0 = off / no glow). Applied as the emission/rim strength.</summary>
        public readonly float RimIntensity;

        /// <summary>True when the LEGENDARY apex is reached — play the slow Lana "Burst_rings" particle on the hero.</summary>
        public readonly bool LegendaryBurst;

        public ArmorVfxProfile(Color rimColor, float rimIntensity, bool legendaryBurst)
        {
            RimColor = rimColor;
            RimIntensity = rimIntensity;
            LegendaryBurst = legendaryBurst;
        }
    }

    /// <summary>
    /// Pure resolver: the DOMINANT rarity across the equipped armor + ring + amulet (+ optional
    /// makersMark theme tint) -> an <see cref="ArmorVfxProfile"/>. Null-safe (no items / unknown
    /// rarity -> the common "off" profile, so the hero glows only when wearing rare+ gear).
    /// </summary>
    public static class ArmorVfxMap
    {
        // =====================================================================
        //  TUNABLE TABLE - rarity -> rim color + intensity (BONES; owner felt-tune)
        // -----------------------------------------------------------------------
        //    common  -> none   (off, 0.00)
        //    uncommon-> warm white (0.15)
        //    rare    -> Oathweld cool-blue (0.30)
        //    epic    -> violet (0.45)
        //    legend. -> gold (0.70 + Burst_rings)
        //    elarion -> gold (shares the legendary apex)
        // =====================================================================

        // -- Colors (the apex consts the regression pins by name) --
        /// <summary>Common / default — no glow (intensity resolves to 0). A neutral steel base color.</summary>
        public static readonly Color CommonColor   = new Color(0.80f, 0.85f, 0.95f, 1.00f);
        /// <summary>Uncommon — warm white.</summary>
        public static readonly Color UncommonColor = new Color(1.00f, 0.97f, 0.88f, 1.00f);
        /// <summary>Rare — Oathweld cool-blue.</summary>
        public static readonly Color RareColor     = new Color(0.42f, 0.62f, 1.00f, 1.00f);
        /// <summary>Epic — violet.</summary>
        public static readonly Color EpicColor     = new Color(0.70f, 0.40f, 1.00f, 1.00f);
        /// <summary>Legendary / elarion apex — gold. The regression pins legendary == this const.</summary>
        public static readonly Color GoldColor     = new Color(1.00f, 0.78f, 0.22f, 1.00f);

        // -- Intensities (monotonic escalation common..legendary; common == off) --
        public const float CommonIntensity    = 0.00f;   // off
        public const float UncommonIntensity  = 0.15f;
        public const float RareIntensity      = 0.30f;
        public const float EpicIntensity      = 0.45f;
        public const float LegendaryIntensity = 0.70f;

        // -- Theme tint blend (optional, from makersMark) — same strength as WeaponVfxMap --
        /// <summary>How strongly the makersMark theme tint pulls the rarity color toward the forge's
        /// signature hue. A light touch so the rarity band still reads first. Tunable.</summary>
        public const float ThemeTintStrength = 0.18f;

        // Forge-stamp signature hues (makersMark). Unknown / empty mark => no tint.
        private static readonly Color EmberhandTint    = new Color(1.00f, 0.45f, 0.15f, 1f); // warm orange
        private static readonly Color OathweldTint     = new Color(0.42f, 0.62f, 1.00f, 1f); // cooler blue
        private static readonly Color HeartwoodTint    = new Color(0.40f, 0.85f, 0.45f, 1f); // green
        private static readonly Color LastPressingTint = new Color(1.00f, 0.86f, 0.40f, 1f); // amber-gold

        /// <summary>
        /// Resolve the rim-light profile from the equipped armor + ring + amulet. The DOMINANT
        /// (highest) rarity across the three drives the look; the makersMark of the dominant item
        /// supplies the optional theme tint. Null-safe: all-null / common -> the off profile.
        /// </summary>
        public static ArmorVfxProfile Resolve(ArmorDef armor, AccessoryDef ring, AccessoryDef amulet)
        {
            // Pick the dominant item by rarity rank (ties prefer armor, then ring, then amulet).
            string armorR  = armor  != null ? armor.rarity  : null;
            string ringR   = ring   != null ? ring.rarity   : null;
            string amuletR = amulet != null ? amulet.rarity : null;

            int ra = Rank(armorR), rr = Rank(ringR), rm = Rank(amuletR);
            string dominantRarity = armorR;
            string dominantMark   = armor != null ? armor.makersMark : null;
            int best = ra;
            if (rr > best) { best = rr; dominantRarity = ringR;   dominantMark = ring   != null ? ring.makersMark   : null; }
            if (rm > best) { best = rm; dominantRarity = amuletR; dominantMark = amulet != null ? amulet.makersMark : null; }

            return Resolve(dominantRarity, dominantMark);
        }

        /// <summary>Resolve from a single rarity band (+ optional makersMark). Used by Resolve and the regression.</summary>
        public static ArmorVfxProfile Resolve(string rarity, string makersMark = null)
        {
            Color baseColor = RarityColor(rarity);
            float intensity = RarityIntensity(rarity);
            bool burst = IsLegendaryBand(rarity);

            // No glow at common -> no point tinting; keep the neutral base.
            Color themed = intensity > 0f ? ApplyThemeTint(baseColor, makersMark) : baseColor;
            return new ArmorVfxProfile(themed, intensity, burst);
        }

        /// <summary>Rarity band string -> apex rim color. Unknown/null -> common.</summary>
        public static Color RarityColor(string rarity)
        {
            switch (Normalize(rarity))
            {
                case "uncommon":  return UncommonColor;
                case "rare":      return RareColor;
                case "epic":      return EpicColor;
                case "legendary":
                case "elarion":   return GoldColor;
                case "common":
                default:          return CommonColor;
            }
        }

        /// <summary>Rarity band string -> rim intensity (common == 0/off). Unknown/null -> common.</summary>
        public static float RarityIntensity(string rarity)
        {
            switch (Normalize(rarity))
            {
                case "uncommon":  return UncommonIntensity;
                case "rare":      return RareIntensity;
                case "epic":      return EpicIntensity;
                case "legendary":
                case "elarion":   return LegendaryIntensity;
                case "common":
                default:          return CommonIntensity;
            }
        }

        /// <summary>True when the band is the legendary apex (drives the Burst_rings particle).</summary>
        public static bool IsLegendaryBand(string rarity)
        {
            string n = Normalize(rarity);
            return n == "legendary" || n == "elarion";
        }

        // Monotonic rarity rank for dominant-item selection. Unknown/null/common == 0.
        private static int Rank(string rarity)
        {
            switch (Normalize(rarity))
            {
                case "uncommon":  return 1;
                case "rare":      return 2;
                case "epic":      return 3;
                case "legendary": return 4;
                case "elarion":   return 5;   // top mark band (still gold apex)
                case "common":
                default:          return 0;
            }
        }

        /// <summary>Blend the rarity color toward the makersMark's signature hue by
        /// <see cref="ThemeTintStrength"/>. Unknown/empty mark -> the color is returned unchanged.</summary>
        private static Color ApplyThemeTint(Color baseColor, string makersMark)
        {
            if (!TryThemeTint(makersMark, out Color tint)) return baseColor;
            Color blended = Color.Lerp(baseColor, tint, ThemeTintStrength);
            blended.a = baseColor.a;
            return blended;
        }

        private static bool TryThemeTint(string makersMark, out Color tint)
        {
            switch (Normalize(makersMark))
            {
                case "emberhand":     tint = EmberhandTint;    return true;
                case "oathweld":      tint = OathweldTint;     return true;
                case "heartwood":     tint = HeartwoodTint;    return true;
                case "last-pressing":
                case "lastpressing":  tint = LastPressingTint; return true;
                default:              tint = default;          return false;
            }
        }

        private static string Normalize(string s) =>
            string.IsNullOrEmpty(s) ? string.Empty : s.Trim().ToLowerInvariant();
    }
}
