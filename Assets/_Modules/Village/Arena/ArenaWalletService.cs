// =============================================================================
// ArenaWalletService — CLIENT-SIDE SKR WAGER STUB (ARENA MVP).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Arena   (STATIC)
//
// Proves the SKR-SINK mechanic (the grant thesis: stake SKR on an async-PvP raid,
// win the purse / lose the stake) WITHOUT the backend or a real on-chain wallet.
// A local SKR balance persisted in PlayerPrefs, with Debit / Credit / Balance.
// The player starts with a seed balance so they can immediately wager.
//
// STUB — replace with WalletService (CurrencyKind.Skr) + backend payout when
// deployed. The real wallet is async (UniTask<WalletBalance> GetBalance, on-chain
// transfer for payouts) and authoritative; this client stub is a synchronous local
// number ONLY so the wager LOOP is playable + demoable today. The swap is isolated
// to this one file: ArenaMode calls Debit/Credit/Balance and nothing else, so the
// real wallet drops in behind the same three methods (made async) with no other
// caller change. CLEARLY devnet/dev-only — never trust this number for real funds.
//
// SCOPE (MVP): client-stub SKR — NOT real on-chain custody, NOT a backend-escrowed
// purse. The opponent's "lost" stake is minted by the stub (no real counterparty),
// because opponents are SEEDED, not matched. Those are later bites.
//
// ASCII-only strings. No Unity scene dependency (pure PlayerPrefs static).
// =============================================================================

using UnityEngine;

namespace DeNelle.Village.Arena
{
    /// <summary>
    /// A client-side SKR balance stub for the Arena wager loop. Persisted in
    /// PlayerPrefs. STUB — see file header for the real-wallet swap point.
    /// </summary>
    public static class ArenaWalletService
    {
        // PlayerPrefs key for the persisted client SKR balance (devnet stub).
        private const string PrefBalanceKey = "dotr-arena-skr-balance";

        // Seed balance so a brand-new player can immediately stake a wager.
        private const long SeedBalance = 500L;

        // True once we have read (and seeded, if first run) the persisted balance.
        private static bool _loaded;
        private static long _balance;

        /// <summary>The current client SKR balance (devnet stub). Seeded to 500 on first run.</summary>
        public static long Balance
        {
            get { EnsureLoaded(); return _balance; }
        }

        /// <summary>True if the stub holds at least <paramref name="amount"/> SKR.</summary>
        public static bool CanAfford(long amount)
        {
            EnsureLoaded();
            return amount <= 0 || _balance >= amount;
        }

        /// <summary>
        /// Stake / spend <paramref name="amount"/> SKR. Returns false (no change) if the
        /// balance is insufficient or the amount is non-positive. Persists on success.
        /// // STUB — replace with WalletService (CurrencyKind.Skr) + backend escrow when deployed.
        /// </summary>
        public static bool Debit(long amount)
        {
            EnsureLoaded();
            if (amount <= 0) return false;
            if (_balance < amount)
            {
                Debug.LogWarning($"[ArenaWalletService] STUB Debit refused: need {amount} SKR, have {_balance}.");
                return false;
            }
            _balance -= amount;
            Persist();
            Debug.Log($"[ArenaWalletService] STUB Debit {amount} SKR -> balance {_balance}.");
            return true;
        }

        /// <summary>
        /// Pay <paramref name="amount"/> SKR into the balance (a won purse). Persists.
        /// // STUB — replace with WalletService (CurrencyKind.Skr) + backend payout when deployed.
        /// </summary>
        public static void Credit(long amount)
        {
            EnsureLoaded();
            if (amount <= 0) return;
            _balance += amount;
            Persist();
            Debug.Log($"[ArenaWalletService] STUB Credit {amount} SKR -> balance {_balance}.");
        }

        /// <summary>DEV/TEST only: hard-reset the stub balance back to the seed amount.</summary>
        public static void DevReset()
        {
            _balance = SeedBalance;
            _loaded = true;
            Persist();
        }

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            if (PlayerPrefs.HasKey(PrefBalanceKey))
            {
                // PlayerPrefs has no long getter; we store as a string for full range.
                long.TryParse(PlayerPrefs.GetString(PrefBalanceKey, SeedBalance.ToString()), out _balance);
            }
            else
            {
                _balance = SeedBalance;
                Persist();
                Debug.Log($"[ArenaWalletService] STUB seeded first-run SKR balance = {SeedBalance}.");
            }
        }

        private static void Persist()
        {
            PlayerPrefs.SetString(PrefBalanceKey, _balance.ToString());
            PlayerPrefs.Save();
        }
    }
}
