// =============================================================================
// RoughStoneFanfareVM (WO-1596) - the MODEL half of the rough-stone fanfare.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Dungeons   Namespace: DeNelle.Dungeons
//
// OWNER, VERBATIM (2026-09-07 09:46, after her first Sunken Vault clear):
//   "ok so i got the crystal but that scren need to be a big moment fanfare full
//    screen, the user needs to know that this is a BIG deal"
//
// The device log proved the beat was a LOG LINE: the grant, the "Take it to the
// Jeweler" sentence and the scene route all landed inside the same 10 ms and the
// player saw nothing. This VM is the composition half of the fix.
//
// ARCHITECTURE (ARCHITECTURE_PRINCIPLES sec.2 - presentation never touches the
// objects):
//   * THE VM COMPOSES. It turns (stone id, polish score, first-ever) into the
//     strings, the star count and the ART CANDIDATE KEYS the screen needs.
//   * THE VIEW RENDERS. RoughStoneFanfarePanel reads these fields and draws them.
//   * NEITHER GRANTS. DungeonController.GrantRunPayout stays the ONE producer of
//     the stone; this type never touches the larder - it neither adds nor consumes,
//     and it never ASKS the larder anything either. "First-ever" is a PARAMETER
//     handed down from the payout event, measured there before the stone was banked;
//     re-deriving it here would be a second authority on the guaranteed introduction.
//     The only catalog this type reads is materials.json (display name / glyph /
//     iconPath) - copy, never state.
//
// WARNING - THE WORDS MATTER, NOT ONLY THE CODE. RoughStoneFanfareRegression case
// [no-grant] is a SUBSTRING lint over this whole file, comments included, and that
// bluntness is deliberate: a lint that parses C# to decide whether a mention is
// "only a comment" is a lint that can be talked out of firing. This header used to
// name the inventory type while explaining that it does not use it, and the suite
// correctly went red for it (Builds/reg-wave8.log). The prose was wrong, not the
// oracle - so the prose changed. Do not soften the oracle to let a name back in.
//
// ASCII ONLY. Every string that can reach TMP is ASCII - a non-ASCII glyph renders
// as tofu on the Seeker (the DungeonTreasurePanel lesson, case 5 of its suite).
// =============================================================================

using System.Collections.Generic;
using DeNelle.Core.Catalog;

namespace DeNelle.Dungeons
{
    /// <summary>
    /// Everything the rough-stone fanfare screen needs, composed once at the grant.
    /// Immutable by construction: build it with <see cref="For"/> (resolves the
    /// catalog) or <see cref="Compose"/> (deterministic - the regression fixture).
    /// </summary>
    public sealed class RoughStoneFanfareVM
    {
        /// <summary>The item id this fanfare celebrates (always the dungeon rough stone today).</summary>
        public string StoneId { get; private set; }

        /// <summary>Player-facing material name, from materials.json. Never null.</summary>
        public string StoneName { get; private set; }

        /// <summary>The kit's display-face headline. ASCII, upper case, short.</summary>
        public string Title { get; private set; }

        /// <summary>ONE line that says what it is and why it matters.</summary>
        public string Meaning { get; private set; }

        /// <summary>The single verb on the single button.</summary>
        public string CtaLabel { get; private set; }

        /// <summary>Filled stars, 0..<see cref="MaxStars"/>. The run's polish score.</summary>
        public int Stars { get; private set; }

        /// <summary>Star ceiling - taken from the ONE rubric, never re-typed.</summary>
        public int MaxStars { get { return DungeonRunGrade.MaxStars; } }

        /// <summary>True for the guaranteed FIRST-EVER stone (the big version of the beat).</summary>
        public bool FirstEver { get; private set; }

        /// <summary>
        /// Resources keys to try for the stone's art, most specific first. The View walks
        /// this list and falls through to <see cref="Glyph"/> when none resolves.
        /// <para>ART ASK (2026-09-07): <c>Assets/Resources/ItemIcons/ing_rough_stone.png</c> does
        /// NOT EXIST - materials.json authors <c>iconPath: ""</c> for this id (read at source).
        /// The fallback is deliberately a PROCEDURAL kit disc plus the material's own glyph, NOT
        /// another item's icon: painting ing_heartstone_crystal under "A ROUGH STONE" would tell
        /// the player they earned a different thing.</para>
        /// </summary>
        public IReadOnlyList<string> ArtKeys { get; private set; }

        /// <summary>The material's authored ASCII glyph - the honest fallback when no art resolves.</summary>
        public string Glyph { get; private set; }

        /// <summary>The trace line this beat writes, so the log and the screen cannot disagree.</summary>
        public string TraceSummary
        {
            get
            {
                return "first=" + (FirstEver ? "true" : "false")
                     + " score=" + Stars + "/" + MaxStars
                     + " id='" + (StoneId ?? "") + "'"
                     + " art=" + (ArtKeys != null ? ArtKeys.Count : 0) + " candidate(s)";
            }
        }

        private RoughStoneFanfareVM() { }

        // -- COPY, IN ONE PLACE ------------------------------------------------
        // The View may not compose a sentence; every player-facing string is here so the
        // regression can lint the whole surface for ASCII in one read.

        /// <summary>The headline. Same on both versions - the stone IS the headline.</summary>
        public const string TitleText = "A ROUGH STONE";

        /// <summary>First-ever meaning: it names the door the stone opens.</summary>
        public const string MeaningFirst =
            "Unpolished, unidentified. The Jeweler can polish it into a Ring of Power.";

        /// <summary>Repeat meaning: shorter, per the WO - the player already knows the door.</summary>
        public const string MeaningRepeat =
            "Another uncut stone. The Jeweler can polish it.";

        /// <summary>The one verb, first time.</summary>
        public const string CtaFirst = "TAKE IT TO THE JEWELER";

        /// <summary>The one verb, afterwards.</summary>
        public const string CtaRepeat = "TAKE";

        /// <summary>Glyph used when materials.json authors none. ASCII by construction.</summary>
        public const string DefaultGlyph = "@";

        /// <summary>The art key the ART ASK is filed against.</summary>
        public const string PreferredArtKey = "ItemIcons/ing_rough_stone";

        /// <summary>
        /// Compose from explicit values - no catalog, no Resources, no scene. This is the
        /// entry the EditMode fixture uses, so the copy rules are provable without Unity
        /// having loaded a single JSON file.
        /// </summary>
        public static RoughStoneFanfareVM Compose(string stoneId, string displayName,
                                                  int polishScore, bool firstEver,
                                                  string iconPath, string glyph)
        {
            int max = DungeonRunGrade.MaxStars;
            int stars = polishScore < 0 ? 0 : (polishScore > max ? max : polishScore);

            var keys = new List<string>();
            if (!string.IsNullOrEmpty(iconPath)) keys.Add(iconPath);
            // The canonical key is always tried, even when materials.json authors nothing:
            // the day the ART ASK is filled the panel picks it up with no code edit.
            if (!keys.Contains(PreferredArtKey)) keys.Add(PreferredArtKey);

            return new RoughStoneFanfareVM
            {
                StoneId = stoneId ?? "",
                StoneName = string.IsNullOrEmpty(displayName) ? (stoneId ?? "") : displayName,
                Title = TitleText,
                Meaning = firstEver ? MeaningFirst : MeaningRepeat,
                CtaLabel = firstEver ? CtaFirst : CtaRepeat,
                Stars = stars,
                FirstEver = firstEver,
                ArtKeys = keys,
                Glyph = string.IsNullOrEmpty(glyph) ? DefaultGlyph : glyph,
            };
        }

        /// <summary>
        /// Compose for a real grant: reads the display name / icon path / glyph off
        /// materials.json through the SHARED catalog, so the fanfare and the larder can
        /// never name the stone differently. Never throws - a catalog miss degrades to the
        /// raw id plus the default glyph, which is visible and obviously wrong rather than
        /// blank (CLAUDE.md sec.12, no silent failures).
        /// </summary>
        public static RoughStoneFanfareVM For(string stoneId, int polishScore, bool firstEver)
        {
            string name = null, icon = null, glyph = null;
            DeNelle.Core.Diagnostics.Guard.Try("JewelPolish", "resolve rough stone catalog copy", () =>
            {
                name = DeNelle.Village.Items.MaterialCatalog.DisplayName(stoneId);
                icon = DeNelle.Village.Items.MaterialCatalog.IconPath(stoneId);
                var def = DeNelle.Village.Items.MaterialCatalog.Find(stoneId);
                glyph = def != null ? def.Glyph : null;
            });
            return Compose(stoneId, name, polishScore, firstEver, icon, glyph);
        }
    }
}
