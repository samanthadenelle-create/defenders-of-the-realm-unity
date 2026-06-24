// =============================================================================
// WeaponVfxMap - WO-504 slice 3: pure rarity -> swing-trail VFX resolver.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// The Knight swings one shared mesh, so a legendary blade must READ legendary
// through its VFX, not its model. This pure resolver maps a WeaponDef's RARITY
// band (the WO-500 ladder: common/uncommon/rare/epic/legendary, plus the
// "elarion" mark) onto an escalating swing-trail COLOR + WIDTH, with an optional
// theme tint blended from the weapon's makersMark forge-stamp.
//
// PURE: no MonoBehaviour, no I/O, no cross-assembly service calls. Deterministic.
// PlayerAttackController calls Resolve(GearLoadout.EquippedWeapon) and applies the
// returned profile to the TrailRenderer at swing time / on OnGearChanged.
//
//   *** COLORS + WIDTHS ARE BONES FOR OWNER FELT-TUNING ***
//   Every color/width below is a NAMED static-readonly / const in the TUNABLE
//   TABLE section - re-point them without touching the resolver logic. The
//   mapping is self-asserted by DataRegression (distinct color per band, gold ==
//   the legendary const, null -> steel default, widths escalate monotonically).
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>The resolved swing-trail look for an equipped weapon: a trail color and
    /// a start-width. Returned by <see cref="WeaponVfxMap.Resolve"/>; applied to the
    /// PlayerAttackController's TrailRenderer.</summary>
    public readonly struct WeaponVfxProfile
    {
        /// <summary>Trail color (RGBA). Alpha carries the trail's head opacity.</summary>
        public readonly Color TrailColor;

        /// <summary>Trail start-width (m) at the swing edge; the trail tapers to 0 at the tail.</summary>
        public readonly float TrailWidth;

        public WeaponVfxProfile(Color trailColor, float trailWidth)
        {
            TrailColor = trailColor;
            TrailWidth = trailWidth;
        }
    }

    /// <summary>
    /// Pure resolver: WeaponDef.rarity (+ optional makersMark theme tint) -> a
    /// <see cref="WeaponVfxProfile"/>. Null-safe (null weapon / unknown rarity ->
    /// the steel common default).
    /// </summary>
    public static class WeaponVfxMap
    {
        // =====================================================================
        //  TUNABLE TABLE - rarity -> trail color + width (BONES; owner felt-tune)
        // -----------------------------------------------------------------------
        //  Re-point any value here without code surgery. The escalation reads:
        //    common  -> steel  (cool white-blue, the legacy hard-coded look)
        //    uncommon-> green
        //    rare    -> blue
        //    epic    -> violet
        //    legend. -> gold
        //    elarion -> gold (the makersMark/top band; shares the legendary apex)
        //  Widths escalate MONOTONICALLY common..legendary so a better blade
        //  carves a visibly fatter arc. Asserted in DataRegression.
        // =====================================================================

        // -- Colors (the apex consts the regression pins by name) --
        /// <summary>Common / default - a cool steel arc (matches the legacy hard-coded trail).</summary>
        public static readonly Color SteelColor    = new Color(0.75f, 0.85f, 1.00f, 0.85f);
        /// <summary>Uncommon - green.</summary>
        public static readonly Color UncommonColor = new Color(0.42f, 0.86f, 0.40f, 0.88f);
        /// <summary>Rare - blue.</summary>
        public static readonly Color RareColor     = new Color(0.36f, 0.58f, 1.00f, 0.90f);
        /// <summary>Epic - violet.</summary>
        public static readonly Color EpicColor     = new Color(0.70f, 0.40f, 1.00f, 0.92f);
        /// <summary>Legendary / elarion apex - gold. The regression pins legendary == this const.</summary>
        public static readonly Color GoldColor     = new Color(1.00f, 0.78f, 0.22f, 1.00f);

        // -- Widths (monotonic escalation common..legendary) --
        public const float CommonWidth    = 0.18f;   // == the legacy _trailStartWidth default
        public const float UncommonWidth  = 0.21f;
        public const float RareWidth      = 0.25f;
        public const float EpicWidth      = 0.30f;
        public const float LegendaryWidth = 0.36f;

        // -- Theme tint blend (optional, from makersMark) --
        /// <summary>How strongly the makersMark theme tint pulls the rarity color toward the
        /// forge's signature hue. 0 = pure rarity color; 1 = pure theme. A light touch so the
        /// rarity band still reads first. Tunable.</summary>
        public const float ThemeTintStrength = 0.18f;

        // Forge-stamp signature hues (makersMark). A subtle tint only - the rarity band
        // dominates. Tunable. Unknown / empty mark => no tint (theme strength 0).
        private static readonly Color EmberhandTint   = new Color(1.00f, 0.45f, 0.15f, 1f); // fire/ember -> warm orange
        private static readonly Color OathweldTint    = new Color(0.45f, 0.62f, 1.00f, 1f); // sworn steel -> cool blue
        private static readonly Color HeartwoodTint   = new Color(0.40f, 0.85f, 0.45f, 1f); // living wood -> green
        private static readonly Color LastPressingTint= new Color(1.00f, 0.86f, 0.40f, 1f); // golden press -> amber

        /// <summary>
        /// Resolve the swing-trail VFX profile for an equipped weapon. Null-safe: a null
        /// weapon (or unknown/empty rarity) returns the steel common default - exactly the
        /// legacy hard-coded look - so combat is unchanged when no weapon is equipped.
        /// </summary>
        public static WeaponVfxProfile Resolve(WeaponDef w)
        {
            string rarity = w != null ? w.rarity : null;
            Color baseColor = RarityColor(rarity);
            float width = RarityWidth(rarity);

            // Optional theme tint from the forge-stamp. A null weapon / empty mark leaves the
            // pure rarity color (theme strength resolves to 0 for an unknown mark).
            string mark = w != null ? w.makersMark : null;
            Color themed = ApplyThemeTint(baseColor, mark);

            return new WeaponVfxProfile(themed, width);
        }

        /// <summary>Rarity band string -> apex trail color. Unknown/null -> steel.</summary>
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
                default:          return SteelColor;
            }
        }

        /// <summary>Rarity band string -> trail start-width. Unknown/null -> common width.</summary>
        public static float RarityWidth(string rarity)
        {
            switch (Normalize(rarity))
            {
                case "uncommon":  return UncommonWidth;
                case "rare":      return RareWidth;
                case "epic":      return EpicWidth;
                case "legendary":
                case "elarion":   return LegendaryWidth;
                case "common":
                default:          return CommonWidth;
            }
        }

        /// <summary>Blend the rarity color toward the makersMark's signature hue by
        /// <see cref="ThemeTintStrength"/>. Preserves the rarity color's alpha (the band's
        /// head opacity wins). Unknown/empty mark -> the color is returned unchanged.</summary>
        private static Color ApplyThemeTint(Color baseColor, string makersMark)
        {
            if (!TryThemeTint(makersMark, out Color tint)) return baseColor;
            Color blended = Color.Lerp(baseColor, tint, ThemeTintStrength);
            blended.a = baseColor.a;   // keep the rarity band's opacity
            return blended;
        }

        /// <summary>True + the forge's signature hue when the mark is recognised; false (no tint) otherwise.</summary>
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
