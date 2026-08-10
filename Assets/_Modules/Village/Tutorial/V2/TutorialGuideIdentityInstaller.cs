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
        // =====================================================================
        //  WO-1014 2b - THE OWNER'S NAME SEAM. ONE PLACE. NOT AUTHORED BY CLI.
        // ---------------------------------------------------------------------
        //  The owner heard the guide called "Storm" during her 2026-08-10 felt
        //  test. That name is NOT authored anywhere for the guide: today the
        //  guide's identity is DERIVED from EchoRosterCatalog.ByCount(1) below
        //  ("Aldwin, the Ice Echo"), while the roster ALSO carries a Storm
        //  affinity and "Bran, the Storm Echo" (echo-stormcoil-serpent,
        //  EchoRosterCatalog.cs:186-194). So the wolf is wearing a roster row,
        //  not a chosen name - which is exactly why it reads as "no knowledge of
        //  who this wolf is".
        //
        //  NAMING THE GUIDE IS AN OWNER CREATIVE DECISION (CLAUDE.md section 2;
        //  the same "she picks, we wire" pattern as the VFX owner-tag rule).
        //  When she rules, drop the name HERE and nowhere else - dialogue copy
        //  keeps authoring the "{guide}" token, so nothing else in the game
        //  changes. Leave EMPTY to keep deriving from the founding roster row.
        //
        //    ""            -> derive from EchoRosterCatalog.ByCount(1) (today)
        //    "Storm"       -> if she adopts Storm as canon (reserve it in the
        //                     roster too, so nothing else can claim it)
        //    "<her name>"  -> any authored canon name
        //  Set GuideCanonTitle/Affiliation only if she wants the CARD sub-lines
        //  to change with it; empty keeps the roster's.
        // =====================================================================
        private const string GuideCanonNameOverride  = "";
        private const string GuideCanonTitleOverride = "";
        private const string GuideCanonAffiliationOverride = "";

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

            // WO-1014 2b: the owner's name seam wins over the derived roster row.
            // Empty (today) = derive, exactly as before - a no-op until she rules.
            bool owned = !string.IsNullOrEmpty(GuideCanonNameOverride);
            string name = owned ? GuideCanonNameOverride : shortName;
            string title = !string.IsNullOrEmpty(GuideCanonTitleOverride) ? GuideCanonTitleOverride
                         : owned ? GuideCanonNameOverride : full;
            string affil = !string.IsNullOrEmpty(GuideCanonAffiliationOverride)
                         ? GuideCanonAffiliationOverride : first.Element;

            FlowTrace.Step("Tutorial",
                "guide-identity source = " + (owned
                    ? "OWNER OVERRIDE (WO-1014 2b seam) -> '" + name + "'"
                    : "DERIVED from EchoRosterCatalog.ByCount(1) '" + full + "' - the guide has NO authored " +
                      "name yet (owner creative pin, WO-1014 2b). Any 'Storm' the player hears is the roster's " +
                      "Storm affinity / 'Bran, the Storm Echo', never this guide."));

            TutorialGuide.Configure(
                displayName: name,
                fullTitle: title,
                affiliation: affil,           // e.g. "Essence of a fallen keeper" — the card sub-line
                portraitTexturePath: "Echoes/Portraits/" + first.PortraitName);
        }
    }
}
