// =============================================================================
// NightMarketPalette — the four band lights of The Night Market (WO-1050)
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Wallet   Namespace: DeNelle.Wallet
//
// ⛔ WHY THIS IS A FILE AND NOT FOUR `new Color(...)` LITERALS INSIDE PackStore.
// The owner is RED/GREEN COLOURBLIND. The rule that follows from that is not
// "pick friendly hues" — it is that COLOUR MAY NEVER BE THE SOLE CARRIER OF
// MEANING, and that rule is only worth anything if something can CHECK it. Four
// literals buried in a render method cannot be checked; a named table with a
// luma function can, and NightMarketRegression does exactly that: it asserts the
// four band lights step apart in rec.709 greyscale and that every band also
// carries a text eyebrow and a mark. Strip the hues and the shelf still reads.
//
// So the honest statement of what makes a band identifiable is, in order:
//   1. the EYEBROW  — a word ("CLOSE THE GAP"), from canon-strings.json
//   2. the MARK     — a 3 px rail at the head of the band and down the selected card
//   3. the VALUE    — the light's greyscale step, which survives hue removal
//   4. the HUE      — decoration, and the first thing that is allowed to be lost
//
// ⚠ DO NOT ASK THE OWNER TO PICK OR APPROVE HUES (memory
// `owner-colorblind-delegate-visual-creative`). Ask about BEHAVIOUR. The gate is
// the greyscale check, and it lives in code so it runs without her.
// =============================================================================

using UnityEngine;

namespace DeNelle.Wallet
{
    /// <summary>The four band lights + the ground, and the greyscale rule that binds them.</summary>
    public static class NightMarketPalette
    {
        // ── The four lights ──────────────────────────────────────────────────
        // Greyscale (rec.709, of 255) is noted per entry and is ASSERTED by the
        // regression, not trusted from this comment. Comments lie; the oracle does not.

        /// <summary>Patronage — gold. The realm's own colour, kept rare. Luma ~195.</summary>
        public static readonly Color Patronage = Hex(0xF0, 0xC2, 0x4A);

        /// <summary>Free — verdant. ⛔ NEVER used on anything that costs money. Luma ~177.</summary>
        public static readonly Color Free = Hex(0x3E, 0xD5, 0x98);

        /// <summary>Gap — ember. Timber, iron, grain: the stall-fire. Luma ~145.</summary>
        public static readonly Color Gap = Hex(0xFF, 0x7A, 0x33);

        /// <summary>Basket — aether. Crystal, premium, the wallet. Luma ~113.</summary>
        public static readonly Color Basket = Hex(0x8B, 0x5C, 0xF6);

        /// <summary>The ground: violet-biased black, not a neutral grey.</summary>
        public static readonly Color Ground = Hex(0x0A, 0x08, 0x10);

        /// <summary>The raised ground (band strips, card plates).</summary>
        public static readonly Color GroundRaised = Hex(0x16, 0x11, 0x1F);

        /// <summary>
        /// The minimum rec.709 greyscale separation, of 255, between any two band lights.
        /// <para>Chosen so the four bands remain ORDERABLE by value on a monochrome read. The
        /// regression fails below this; it is not a style note.</para>
        /// </summary>
        public const float MinGreyscaleStep = 16f;

        /// <summary>The light for a band. Total over the enum — a new band must add its light here.</summary>
        public static Color For(StoreBand band)
        {
            switch (band)
            {
                case StoreBand.Free:      return Free;
                case StoreBand.Gap:       return Gap;
                case StoreBand.Patronage: return Patronage;
                default:                  return Basket;
            }
        }

        /// <summary>The four lights in fixed band order — the shape the greyscale oracle walks.</summary>
        public static Color[] AllBandLights() => new[] { Free, Gap, Basket, Patronage };

        /// <summary>
        /// rec.709 luma of a colour, 0..255 — what the owner's monochrome read of the shelf
        /// resolves to. This is THE function the greyscale gate is defined in terms of, so it lives
        /// beside the colours rather than being re-derived at each call site.
        /// </summary>
        public static float Luma255(Color c) =>
            (0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b) * 255f;

        /// <summary>
        /// Parses an authored <c>#RRGGBB</c> tint. Returns <paramref name="fallback"/> — the band's
        /// own light — on anything unparseable, so a typo in packs.json degrades to a correct colour
        /// instead of a magenta card. Never throws.
        /// </summary>
        public static Color ParseTint(string hex, Color fallback)
        {
            if (string.IsNullOrEmpty(hex)) return fallback;
            string s = hex.Trim();
            if (s.Length > 0 && s[0] == '#') s = s.Substring(1);
            if (s.Length != 6 && s.Length != 8) return fallback;
            if (!ColorUtility.TryParseHtmlString("#" + s, out var parsed)) return fallback;
            return parsed;
        }

        private static Color Hex(int r, int g, int b) =>
            new Color(r / 255f, g / 255f, b / 255f, 1f);
    }
}
