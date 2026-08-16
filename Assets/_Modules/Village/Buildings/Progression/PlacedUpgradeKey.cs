// =============================================================================
// PlacedUpgradeKey — the ONE composer/parser for a placed structure's job key,
// "itemId@cellX_cellZ".
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Buildings.Progression
//
// WHY THIS TYPE EXISTS
//   The key shape was spelled by hand in four places (BuildModeController's sell +
//   upgrade paths, UnderConstructionVisual.KeyFor, CompletedUpgradeApplier's parse)
//   and is now ALSO the id the Manage screen and the upgrade panel pass around as a
//   building id (WO placed-upgrade doorways). A string shape written in five places
//   is the same dual-authority defect UpgradeFamilyResolver was created to retire —
//   one site drifting (a '-' instead of '_', a cell order swap) breaks the timer
//   lookup silently, because a key that does not match simply never resolves.
//
//   UpgradeFamilyResolver.Resolve keys PlacedStructure off the '@', so the '@' is
//   load-bearing grammar, not decoration. Compose/TryParse are the only two places
//   allowed to know it.
// =============================================================================

namespace DeNelle.Village.Buildings.Progression
{
    /// <summary>Composer + parser for the placed-structure job key "itemId@cellX_cellZ".</summary>
    public static class PlacedUpgradeKey
    {
        /// <summary>Compose the job key for a placed structure at a grid cell.</summary>
        public static string Compose(string itemId, int cellX, int cellZ)
            => (itemId ?? "") + "@" + cellX + "_" + cellZ;

        /// <summary>
        /// Parse a job key back into its item id + cell. Returns false (and zeroed outputs)
        /// for anything that is not a well-formed placed-structure key — a bare catalog id,
        /// an empty string, or a malformed cell part.
        /// </summary>
        public static bool TryParse(string key, out string itemId, out int cellX, out int cellZ)
        {
            itemId = null;
            cellX = 0;
            cellZ = 0;
            if (string.IsNullOrEmpty(key)) return false;

            int at = key.LastIndexOf('@');
            if (at <= 0 || at + 1 >= key.Length) return false;

            string cellPart = key.Substring(at + 1);
            int us = cellPart.IndexOf('_');
            if (us <= 0) return false;
            if (!int.TryParse(cellPart.Substring(0, us), out cellX)) return false;
            if (!int.TryParse(cellPart.Substring(us + 1), out cellZ)) return false;

            itemId = key.Substring(0, at);
            return !string.IsNullOrEmpty(itemId);
        }

        /// <summary>True when <paramref name="key"/> parses as a placed-structure key.</summary>
        public static bool IsPlacedKey(string key) => TryParse(key, out _, out _, out _);
    }
}
