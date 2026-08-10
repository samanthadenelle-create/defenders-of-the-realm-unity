// =============================================================================
// HostilePalette (WO-956) - the faction colour law for ENEMY-side presentation.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// THE LAW (owner F8 seq 2269, 2026-08-10 - "it was a enemy that was showing as
// green"; owner is red/green colourblind): a HOSTILE thing must never wear the
// SAFE hue. Green is the player-side language (heals, player buffs, the hero's
// own feedback) - an enemy wrapped in green reads as friendly/safe to the one
// pair of eyes that matters. FACTION drives presentation: any enemy-side tint
// or effect that would present green-dominant is substituted with the hostile
// palette below, at the seam that resolves the colour - never per-effect
// hardcoding at call sites.
//
// ## ALL COLOURS BELOW ARE PLACEHOLDERS (WO-956, FLAGGED FOR THE OWNER)
// The FINAL hostile hues are the owner's look pass. These constants exist so
// the green-axis violation is closed NOW with a clearly-named stand-in; retune
// them freely in the look pass - every consumer reads them from here.
//
// Word+shape still carries meaning everywhere (standing law: never meaning by
// colour alone); this palette is the COLOUR HALF only. The shape half already
// exists where these are applied (the WO-889 auras are separated by motion and
// shape; the ranged cast reads by orb flight + telegraph ring).
// =============================================================================

using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// WO-956: faction colour law for enemy-side presentation. Green is the SAFE
    /// (player-side) hue and hostile things never wear it - callers that resolve
    /// an enemy tint route it through <see cref="EnforceOnTint"/>, and enemy aura
    /// holders use <see cref="IsGreenDominant"/> to detect authored art that
    /// violates the axis. All hues here are owner-look-pass PLACEHOLDERS.
    /// </summary>
    public static class HostilePalette
    {
        // ---------------------------------------------------------------------
        // PLACEHOLDER hues (WO-956) - final values = owner look pass. Named by
        // ROLE so the look pass can retune without chasing call sites.
        // ---------------------------------------------------------------------

        /// <summary>
        /// PLACEHOLDER (WO-956, owner look pass pending): sickly violet for enemy
        /// support/heal/aura EFFECTS that would otherwise present green. Violet is
        /// nowhere on the red/green axis and already reads "enemy magic" (arcane
        /// caster language) elsewhere in the game.
        /// </summary>
        public static readonly Color PlaceholderEffectTint = new Color(0.58f, 0.32f, 0.68f, 1f);

        /// <summary>
        /// PLACEHOLDER (WO-956, owner look pass pending): umber/amber-brown for
        /// enemy BODY fallback tints (untextured Warband grunts). A body colour,
        /// not a magic colour - earthy, off the green axis, still clearly a brute.
        /// </summary>
        public static readonly Color PlaceholderBodyTint = new Color(0.45f, 0.30f, 0.20f, 1f);

        // Green-dominance thresholds: G must be the strictly-largest channel by a
        // readable MARGIN and bright enough to register as a hue at all. Chosen so
        // the known offenders trip (retired orc-grunt tint 0.30/0.42/0.22; the Lana
        // Fog_poison keys ~0.19/0.58/0.12) while near-neutral hides do not (troll
        // grey-green 0.38/0.40/0.34 has only a 0.02 G margin - reads grey).
        private const float GreenMargin = 0.08f;
        private const float GreenFloor  = 0.25f;

        /// <summary>
        /// True when <paramref name="c"/> presents on the green axis: green is the
        /// strictly-dominant channel by a readable margin. Alpha is ignored (an
        /// invisible green is still green the moment it fades in).
        /// </summary>
        public static bool IsGreenDominant(Color c)
            => c.g > GreenFloor
               && c.g > c.r + GreenMargin
               && c.g > c.b + GreenMargin;

        /// <summary>
        /// The faction gate for enemy-side TINTS: returns <paramref name="tint"/>
        /// unchanged when it is off the green axis, else substitutes
        /// <see cref="PlaceholderEffectTint"/> (alpha preserved) and reports the
        /// substitution - a data-authored green enemy effect self-detects instead
        /// of silently wearing the safe hue.
        /// </summary>
        public static Color EnforceOnTint(Color tint, string context)
        {
            if (!IsGreenDominant(tint)) return tint;

            var safe = new Color(PlaceholderEffectTint.r, PlaceholderEffectTint.g,
                                 PlaceholderEffectTint.b, tint.a);
            FlowTrace.Warn("HostilePalette",
                $"{context}: authored enemy tint {tint} is GREEN-dominant (the SAFE hue - owner is " +
                "red/green colourblind). Substituted the WO-956 hostile-palette placeholder " +
                $"{safe}; final hue = owner look pass. Fix the authoring data to clear this warn.");
            return safe;
        }
    }
}
