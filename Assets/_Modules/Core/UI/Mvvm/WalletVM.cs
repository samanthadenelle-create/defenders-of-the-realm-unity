using System.Collections.Generic;

namespace DeNelle.Core.UI.Mvvm
{
    /// <summary>
    /// Binding contract for the currency/wallet lockup (the Blink "BlinkCoinAmount" pattern): an
    /// ordered set of (currency, icon, amount) entries the View renders as icon+amount chips.
    /// View-agnostic (UI_MVVM_BINDING_MAP.md §3). Icons are role+name strings resolved by the View.
    /// </summary>
    public readonly struct WalletVM
    {
        /// <summary>One currency chip: id + icon (role/name) + current amount.</summary>
        public readonly struct Entry
        {
            public readonly string CurrencyId;
            public readonly string IconRole;
            public readonly string IconName;
            public readonly int Amount;

            public Entry(string currencyId, string iconRole, string iconName, int amount)
            {
                CurrencyId = currencyId;
                IconRole = iconRole;
                IconName = iconName;
                Amount = amount;
            }
        }

        /// <summary>The currency chips, in display order. May be empty, never null after construction.</summary>
        public readonly IReadOnlyList<Entry> Entries;

        public WalletVM(IReadOnlyList<Entry> entries)
        {
            Entries = entries ?? System.Array.Empty<Entry>();
        }
    }
}
