// =============================================================================
// Constants — Solana addresses/mints + the village TOWER_SLOTS engine constant
// -----------------------------------------------------------------------------
// Port of src/lib/constants.ts (four Solana exports). TowerSlots is added here
// because GameState needs it for the 9-element towers/towerAbilities arrays and
// it has no other natural home in Week 1 (its provenance is villageSlice.ts,
// NOT constants.ts — noted so a reviewer is not surprised).
//
// FLAG: the four wallet/mint values look canon-adjacent. Per the v2 port-spec
// Part 4 they should ultimately flow from data/wallets.json. They are ported
// literally here so the Week-7 Wallet module compiles; cross-check against
// docs/wallets-of-record.md during Week-7 work — the docs win on any conflict.
// =============================================================================

namespace DeNelle.Core
{
    /// <summary>Static, literal port of <c>src/lib/constants.ts</c>.</summary>
    public static class Constants
    {
        /// <summary>Admin Solana wallet.</summary>
        public const string AdminAddress = "BwBB9LUS3Nmxqgc41xNbGUygsUVQniv9PdngiycicjJV";

        /// <summary>Project vault / treasury wallet.</summary>
        public const string ProjectVaultAddress = "CsNvnGxP3kkJ2hdkDpeC46q6cqkCK5SM7FSJ4C1fem33";

        /// <summary>SOL identifier string.</summary>
        public const string Sol = "solana";

        /// <summary>USDC SPL-token mint.</summary>
        public const string Usdc = "EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v";

        /// <summary>
        /// Number of tower build slots in the village (TOWER_SLOTS from
        /// villageSlice.ts). GameState's towers/towerAbilities arrays are this long.
        /// </summary>
        public const int TowerSlots = 9;
    }
}
