using System;

namespace DeNelle.Core.HudModel
{
    /// <summary>
    /// WO-1523. Cosmetics-pushed facts the HUD may read, mirroring the
    /// <see cref="PostureSignals"/> shape exactly (producer pushes, consumers read a
    /// pure static). It is a SEPARATE class on purpose: PostureSignals documents itself
    /// as "Village-pushed facts", and a second producer inside it would blur the one
    /// thing that makes that file readable.
    ///
    /// WHY THIS EXISTS AT ALL: DeNelle.HUD and DeNelle.Core may not reference
    /// DeNelle.Cosmetics (asmdef isolation - Cosmetics references Core, never the other
    /// way). The Hero deck therefore cannot ask CosmeticOwnershipService how many looks
    /// the player owns. The alternative already in the tree is CosmeticShopPanel's
    /// reflection bridge, and CLAUDE.md section 10 forbids adding new reflection to a bridge
    /// script. So Cosmetics PUSHES the one number the HUD needs.
    ///
    /// This is NOT a second authoring of ownership: the list still lives in
    /// CosmeticOwnershipService (PlayerPrefs "dotr-cosmetics-v1"). Only the COUNT is
    /// copied here, by that service, in the same breath as it changes.
    /// </summary>
    public static class CosmeticSignals
    {
        /// <summary>
        /// How many cosmetics the player owns, as the owner last published it.
        /// Defaults to 0 - "nothing unlocked" - which HIDES the wardrobe. That is the
        /// deliberate opposite of the PostureSignals.RaidCapable never-false default:
        /// the raid door defaults OPEN so a pre-publish scene never hides a door the
        /// player earned, whereas WO-1523 rules that an all-locked wardrobe must not be
        /// shown at all, so the pre-publish answer here has to be "hide it". The service
        /// bootstraps at BeforeSceneLoad and publishes on its first state read, so a real
        /// session always has the true count before any deck can open.
        /// </summary>
        public static int OwnedCount { get; private set; }

        /// <summary>True once the player owns at least one cosmetic.</summary>
        public static bool AnyOwned => OwnedCount > 0;

        /// <summary>Raised when <see cref="OwnedCount"/> changes value.</summary>
        public static event Action OwnedCountChanged;

        /// <summary>
        /// Producer-only (DeNelle.Cosmetics.CosmeticOwnershipService). Negative counts
        /// clamp to zero rather than throwing - a signal that hard-fails would take the
        /// whole deck down with it, and zero is the safe answer here.
        /// </summary>
        public static void SetOwnedCount(int count)
        {
            if (count < 0) count = 0;
            if (OwnedCount == count) return;
            OwnedCount = count;
            OwnedCountChanged?.Invoke();
        }

        /// <summary>Test seam: return to the pre-publish state (count 0, no listeners fired
        /// beyond the change event). Suites use it so one case cannot leak into the next.</summary>
        public static void ResetForTests() => SetOwnedCount(0);
    }
}
