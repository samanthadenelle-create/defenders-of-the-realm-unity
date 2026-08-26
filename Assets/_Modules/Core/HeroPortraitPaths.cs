// =============================================================================
// HeroPortraitPaths — THE single declaration of where hero portrait/card art is
// addressed. Sibling to AssetRoots and EnemyArtPaths; same owner ruling, one
// content family across.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core
//
// OWNER RULING 2026-08-26 (WO-1234): "i am ok if you repoint and use a constant
// string for reference" -> "to start moving to consistency".
//
// ── WHY THIS FILE EXISTS ─────────────────────────────────────────────────────
// The Resources folder segment for hero art was written out ELEVEN times across
// SIX files in five different assemblies:
//     Onboarding/HeroSelectController.cs      (x2, the runtime load)
//     Village/Hero/InventoryPaperDoll.cs      (x2, the paper-doll medallion)
//     Village/Hero/InventoryUIBuilder.cs      (x1, the frame medallion socket)
//     DialogueUI/PortraitCache.cs             (x1, the doc comment for the key)
//     Editor/WebGLTextureShrink.cs            (x1, the per-folder maxSize rule)
//     Editor/Regression/ArtResourceRegression.cs (x3, the oracle's own ledger)
// Every one of them was correct the day it was typed. That is exactly the
// duplicated-state failure CLAUDE.md catalogues in §0 (the hardcoded repo root),
// §2 (the stale WO-number block) and §5 (the retired dependency table): the copy
// nobody updates is the one that breaks, and it breaks SILENTLY — a portrait that
// does not resolve renders a placeholder crest with no error on screen.
//
// ── THE ONE RULE ─────────────────────────────────────────────────────────────
//   A hero portrait Resources key is HeroPortraitPaths.ResourceKey(slug).
//   ⛔ DO NOT TYPE THE FOLDER NAME AT A CALL SITE. Ask this file.
//
// ⚠ THE SLUG IS NOT DECLARED HERE, DELIBERATELY. Each caller already owns its own
// id->slug mapping (HeroSelectController.SlugFor, InventoryPaperDoll.PortraitSlug,
// dialogue speaker names), and those map from DIFFERENT id spaces (HeroClass enum,
// a persisted job string, a speaker name). Hoisting them here would fuse three
// unrelated vocabularies into one table. The FOLDER is the shared fact; the slug
// is not.
//
// ⛔ DO NOT RENAME THE FOLDER OR THE FILES. Sylas / Elara / Thrain / Grom are LIVE
// resource keys and the art is already installed against them.
//
// ── THE GATE ─────────────────────────────────────────────────────────────────
// ArtResourceRegression's [portrait-path-literals] case sweeps every .cs under
// Assets/ and FAILS if the quoted folder literal reappears anywhere but this file.
// It derives the token it searches for FROM ResourcesFolder, so the suite can
// never become copy number twelve. (AssetRoots.cs spent three days promising a
// gate that did not exist — see its header. This one is registered, via
// ArtResourceRegression, which DataRegression.RunAll already calls.)
// =============================================================================

namespace DeNelle.Core
{
    /// <summary>
    /// Single source of truth for the hero portrait/card art location. Pure string
    /// composition, no filesystem access — which is what keeps it usable from
    /// runtime code, editor tools AND the regression oracle without any of them
    /// needing a new assembly reference (every consumer already references
    /// DeNelle.Core).
    /// </summary>
    public static class HeroPortraitPaths
    {
        /// <summary>
        /// The Resources-relative FOLDER segment hero art is addressed under, e.g.
        /// <c>Resources.Load&lt;Texture2D&gt;("HeroPortraits/Sylas")</c>.
        /// <para>⛔ CHANGE THIS ONE LINE to relocate the tree. The regression case
        /// [portrait-path-literals] fails the build if the quoted literal reappears
        /// at any call site.</para>
        /// </summary>
        public const string ResourcesFolder = "HeroPortraits";

        /// <summary>
        /// Project-relative folder the art is AUTHORED into (the Resources folder on
        /// disk). Editor-side only — importers and bakers stat real files, the
        /// runtime never sees this form.
        /// </summary>
        public const string AuthoringFolder = "Assets/_Modules/Onboarding/Resources/" + ResourcesFolder;

        /// <summary>
        /// The Resources key for one portrait slug — extension-free, as
        /// Resources.Load requires. Returns null for a null/empty slug rather than
        /// composing a folder-only key that would silently load nothing.
        /// </summary>
        public static string ResourceKey(string slug)
        {
            if (string.IsNullOrEmpty(slug)) return null;
            return ResourcesFolder + "/" + slug;
        }
    }
}
