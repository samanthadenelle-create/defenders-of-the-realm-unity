// =============================================================================
// AssetRoots — THE single declaration of where relocatable asset trees live.
// -----------------------------------------------------------------------------
// OWNER RULING 2026-08-18, and it corrects a mistake I had just made:
//   "isn't it normally that every time you have a string, you set that string as
//    a constant from a database so that no matter what we do, we change it once
//    ... and everywhere uses the same string"
//
// ⛔ WHAT I DID WRONG. When the structure art moved out of Resources I ran a
// find-and-replace across SIXTEEN editor scripts, swapping one hardcoded literal
// ("Assets/Resources/Structures") for another ("Assets/StructureContent"). That
// is not a fix — it is the same disease with a fresher value. The next relocation
// would be sixteen more edits, sixteen more chances to miss one, and the one that
// is missed fails SILENTLY: a scene builder quietly loads nothing and the town
// renders without a building.
//
// This project has been bitten by duplicated constants repeatedly, and always the
// same way — the copy that nobody updated:
//   • .tripo-extracted markers that outlived the FBX they described
//   • PetForwardYaw = -90, right for one mesh, wrong the moment the body changed
//   • a WO-number banner copied into a second doc and left to rot
//   • visualPrefabPath "Structures/WizardTower_1", which shipped a wizard tower
//     as the Ballista to live players
// One declaration cannot disagree with itself. Sixteen copies always eventually do.
//
// ⚠ WHY A CONST AND NOT A RUNTIME LOOKUP: these are EDITOR paths, consumed by
// importers, scene builders and regressions that must work on a fresh clone with
// no generated data present. A const is available unconditionally. The generated
// manifest remains the DATA record of what moved and when — and
// AssetMoveManifestRegression asserts the two AGREE, so the code and the data
// cannot drift apart without failing a gate. That pairing is the point: a single
// declaration, plus a gate that proves it still matches reality.
// =============================================================================

namespace DeNelle.Core
{
    /// <summary>Single source of truth for relocatable asset-tree roots (editor-side).</summary>
    public static class AssetRoots
    {
        /// <summary>
        /// Structure art (buildings, towers, props). Moved OUT of Resources 2026-08-18: anything
        /// under a Resources/ folder is FORCE-INCLUDED in every build whether or not the player
        /// ever sees it, which cost ~62 MB of APK for art most players never place. Now an
        /// Addressable group served remotely from Cloudflare R2.
        /// <para>⛔ CHANGE THIS ONE LINE to relocate the tree. Do NOT reintroduce the literal
        /// anywhere — <c>AssetRootsRegression</c> fails the build if the string reappears.</para>
        /// </summary>
        public const string StructureContent = "Assets/StructureContent";

        /// <summary>
        /// The Resources path structure art USED to occupy. Kept deliberately: the migrator needs
        /// it to detect a pre-migration tree, and the manifest gate needs it to detect art that has
        /// come BACK (an importer with a stale destination re-inflating the build — the
        /// BlinkOrcImporter trap). It is history, not a location: nothing should ever LOAD from it.
        /// </summary>
        public const string StructureContentLegacyResources = "Assets/Resources/Structures";
    }
}
