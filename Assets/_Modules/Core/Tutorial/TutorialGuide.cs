// =============================================================================
// TutorialGuide — the GUIDE-IDENTITY seam (WO-1012 §2a, as RE-RULED 2026-08-09).
// -----------------------------------------------------------------------------
// THE GUIDE IS THE PLAYER'S FIRST PET — an Echo of Elarion (owner, verbatim:
// "our pet, which is an echo of Elarion, is the one that is our guide"). This
// static seam is the ONE place the guide's identity lives at runtime: dialogue
// lines and objective texts author the "{guide}" TOKEN (copy itself unchanged —
// "the verbiage is fine"), and every presentation surface resolves it here:
//   * DialogueViewModel  — line speaker + card affiliation
//   * DialogueView (HUD) — the medallion portrait
//   * GuideLineUi        — the one-liner kicker + portrait
//   * TutorialFlow       — ObjectiveStrip / coach-nudge text
//
// WHY A CONFIGURE SEAM (not a hardcoded identity): ROTATION IS PARKED, not
// dead — the owner's rotation formula (guide = (playerHero + 1) % 4 over
// HeroClass) remains the sanctioned alternative in WO-1012 §2a. Keeping the
// identity a data/config swap means that pivot is ONE Configure(...) call from
// a different installer — zero dialogue-data or UI changes. Today's installer
// is TutorialGuideIdentityInstaller (DeNelle.Village), which reads the
// founding Echo (EchoRosterCatalog.ByCount(1) — Aldwin, the Ice Echo) and
// configures this seam at boot. Core deliberately holds NO copy of the roster
// row (the duplicated-state drift lesson — CLAUDE.md §0/§2/§5): unconfigured,
// it falls back to a neutral "Echo of Elarion" identity with the styled
// silhouette portrait — honest, never blank, warned once.
// =============================================================================

using System;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core.Tutorial
{
    /// <summary>
    /// The tutorial guide's runtime identity (WO-1012 §2a re-ruling: the guide IS
    /// the player's first pet-Echo). Authored content refers to the guide only via
    /// <see cref="Token"/>; presentation resolves name/affiliation/portrait here.
    /// </summary>
    public static class TutorialGuide
    {
        /// <summary>The data token authored in dialogues.json / tutorial-steps.json
        /// wherever the guide speaks or is named. Resolved at runtime, never shown raw.</summary>
        public const string Token = "{guide}";

        // Unconfigured fallback identity — honest and neutral, never blank. The real
        // identity is installed at boot by TutorialGuideIdentityInstaller (Village).
        private const string FallbackName = "Echo of Elarion";
        private const string FallbackAffiliation = "Spirit of the Tree";

        private static bool _configured;
        private static string _displayName = FallbackName;
        private static string _fullTitle = FallbackName;
        private static string _affiliation = FallbackAffiliation;
        private static string _portraitTexturePath;   // Resources path (Texture2D or Sprite)

        private static Sprite _portraitCache;
        private static string _portraitCachedPath;

        /// <summary>True once an installer has configured the live identity.</summary>
        public static bool IsConfigured => _configured;

        /// <summary>The guide's short speaker name (e.g. "Aldwin") — what the token
        /// resolves to on kickers, cards and objective texts.</summary>
        public static string DisplayName { get { WarnIfUnconfigured(); return _displayName; } }

        /// <summary>The guide's full card title (e.g. "Aldwin, the Ice Echo").</summary>
        public static string FullTitle { get { WarnIfUnconfigured(); return _fullTitle; } }

        /// <summary>The guide's card affiliation sub-line (e.g. "Essence of a fallen keeper").</summary>
        public static string Affiliation { get { WarnIfUnconfigured(); return _affiliation; } }

        /// <summary>
        /// Install the guide identity. Called at boot by the active installer
        /// (pet-Echo today; the parked hero-rotation mechanism would simply call
        /// this with a hero's name/portrait — the whole pivot is this one call).
        /// </summary>
        public static void Configure(string displayName, string fullTitle, string affiliation,
                                     string portraitTexturePath)
        {
            _displayName = string.IsNullOrEmpty(displayName) ? FallbackName : displayName;
            _fullTitle = string.IsNullOrEmpty(fullTitle) ? _displayName : fullTitle;
            _affiliation = string.IsNullOrEmpty(affiliation) ? FallbackAffiliation : affiliation;
            if (!string.Equals(_portraitTexturePath, portraitTexturePath, StringComparison.Ordinal))
            {
                _portraitTexturePath = portraitTexturePath;
                _portraitCache = null;   // path changed — drop the cached sprite
            }
            _configured = true;
            FlowTrace.Step("Tutorial",
                $"guide identity CONFIGURED: '{_displayName}' ('{_fullTitle}', affiliation='{_affiliation}', " +
                $"portrait='{_portraitTexturePath ?? "<none>"}') — WO-1012 P2 pet-Echo guide (rotation parked).");
        }

        /// <summary>Replaces every <see cref="Token"/> occurrence in <paramref name="text"/>
        /// with the guide's <see cref="DisplayName"/>. Null-safe; non-token text passes through.</summary>
        public static string ResolveToken(string text)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf(Token, StringComparison.Ordinal) < 0)
                return text;
            return text.Replace(Token, DisplayName);
        }

        /// <summary>True when a speaker string IS the guide — the raw token, or the
        /// resolved display name / full title (case-insensitive).</summary>
        public static bool IsGuideSpeaker(string speakerName)
        {
            if (string.IsNullOrEmpty(speakerName)) return false;
            return string.Equals(speakerName, Token, StringComparison.OrdinalIgnoreCase)
                || string.Equals(speakerName, _displayName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(speakerName, _fullTitle, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The guide's portrait as a runtime Sprite, or null (→ the caller's styled
        /// silhouette / class-portrait fallback — never blank, never throws). Echo
        /// portraits ship as raw Texture2Ds (Resources/Echoes/Portraits/*, the
        /// EchoRosterCatalog convention), so this tries Sprite first then wraps the
        /// texture via Sprite.Create. Cached per path.
        /// </summary>
        public static Sprite PortraitSprite()
        {
            if (string.IsNullOrEmpty(_portraitTexturePath)) return null;
            if (_portraitCache != null &&
                string.Equals(_portraitCachedPath, _portraitTexturePath, StringComparison.Ordinal))
                return _portraitCache;

            var sprite = Guard.Try("Tutorial", "load guide portrait " + _portraitTexturePath, () =>
            {
                var direct = Resources.Load<Sprite>(_portraitTexturePath);
                if (direct != null) return direct;
                var tex = Resources.Load<Texture2D>(_portraitTexturePath);
                if (tex == null)
                {
                    FlowTrace.Warn("Tutorial",
                        $"guide portrait missing at Resources/{_portraitTexturePath} — falling back to silhouette/class portrait.");
                    return (Sprite)null;
                }
                return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                                     new Vector2(0.5f, 0.5f), 100f);
            }, fallback: null);

            if (sprite != null)
            {
                _portraitCache = sprite;
                _portraitCachedPath = _portraitTexturePath;
            }
            return sprite;
        }

        private static void WarnIfUnconfigured()
        {
            if (_configured) return;
            FlowTrace.Once("Tutorial", "guide-identity-unconfigured",
                "guide identity read before any installer configured it — using the neutral " +
                $"'{FallbackName}' fallback (TutorialGuideIdentityInstaller not run yet / stripped?).");
        }
    }
}
