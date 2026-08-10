// =============================================================================
// TutorialGuideIdentityInstaller — installs the pet-Echo guide identity into the
// Core TutorialGuide seam (WO-1012 §2a as RE-RULED 2026-08-09: the guide IS the
// player's FIRST PET, an Echo of Elarion; hero rotation is PARKED).
// -----------------------------------------------------------------------------
// WHY HERE (DeNelle.Village) AND NOT CORE: the single source of the founding
// Echo's identity is EchoRosterCatalog (Village/Harvest — Aldwin, the Ice Echo,
// order 1, portrait Resources/Echoes/Portraits/Frosthowl). Core must not hold a
// second copy of that row (duplicated-state drift, CLAUDE.md §0/§2/§5), and
// Core cannot reference Village — so Village pushes the identity INTO the Core
// seam at boot. Everything downstream (dialogue speaker "{guide}", GuideLine
// portraits, objective texts) reads TutorialGuide only.
//
// THE PARKED PIVOT: to revive hero rotation, replace THIS installer's Configure
// call with one fed from HeroCanonNames/the rotation formula — nothing else in
// the game changes (the seam is the whole point).
// =============================================================================

using UnityEngine;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.Tutorial;

namespace DeNelle.Village
{
    /// <summary>Boot-time bridge: founding Echo (EchoRosterCatalog row 1) →
    /// <see cref="TutorialGuide"/>. No scene objects, no per-frame work.</summary>
    public static class TutorialGuideIdentityInstaller
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            var first = EchoRosterCatalog.ByCount(1);
            if (first == null)
            {
                FlowTrace.Warn("Tutorial",
                    "guide-identity install: EchoRosterCatalog.ByCount(1) returned null — " +
                    "TutorialGuide stays on its neutral fallback identity.");
                return;
            }

            // "Aldwin, the Ice Echo" → short speaker name "Aldwin" (derived from the
            // roster row, never a second hand-maintained copy of the name).
            string full = first.DisplayName ?? "";
            int comma = full.IndexOf(',');
            string shortName = comma > 0 ? full.Substring(0, comma).Trim() : full;

            TutorialGuide.Configure(
                displayName: shortName,
                fullTitle: full,
                affiliation: first.Element,   // e.g. "Essence of a fallen keeper" — the card sub-line
                portraitTexturePath: "Echoes/Portraits/" + first.PortraitName);
        }
    }
}
