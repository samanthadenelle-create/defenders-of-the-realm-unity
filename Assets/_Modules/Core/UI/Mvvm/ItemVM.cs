namespace DeNelle.Core.UI.Mvvm
{
    /// <summary>
    /// The repeating-unit binding contract: one item/slot card's data, View-agnostic. A value type
    /// (no per-row allocation in hot lists). Sprites are referenced by ROLE + NAME (resolved by the
    /// View via RpgUiCatalog), never as UnityEngine.Sprite — so the contract stays Unity-UI-free
    /// (UI_MVVM_BINDING_MAP.md §1/§3). ONE bound slot card serves shop / inventory / loot / crafting /
    /// cosmetics — it is ~70% of the visual surface.
    /// </summary>
    public readonly struct ItemVM
    {
        /// <summary>Stable identifier used by commands (Select/Buy/Use).</summary>
        public readonly string Id;
        public readonly string Name;
        /// <summary>RpgUiCatalog role for the icon (e.g. "icons").</summary>
        public readonly string IconRole;
        /// <summary>RpgUiCatalog name for the icon sprite.</summary>
        public readonly string IconName;
        /// <summary>Optional direct Resources icon path (e.g. a GearIconCatalog-resolved sprite key),
        /// used when the icon does not live under an RpgUiCatalog role+name. Null when unused — the
        /// View prefers IconRole+IconName and falls back to IconPath. Keeps the contract Unity-UI-free
        /// (still a plain string; the View resolves it). (MVVM migration Phase 0 / GearIconCatalog seam.)</summary>
        public readonly string IconPath;
        public readonly int Price;
        /// <summary>Currency identifier this price is denominated in (e.g. "gold").</summary>
        public readonly string CurrencyId;
        /// <summary>Whether the player can currently afford it (drives price tint).</summary>
        public readonly bool Affordable;
        /// <summary>Rarity key (drives the View's frame escalation), or null.</summary>
        public readonly string Rarity;
        public readonly bool Equipped;
        public readonly bool Locked;
        /// <summary>Why the item is locked (e.g. "Requires Lv 5" / "Class: Ranger"), or null when unlocked.
        /// The View shows this hint on a greyed, non-purchasable row so the player sees progression.</summary>
        public readonly string LockReason;

        public ItemVM(string id, string name, string iconRole, string iconName, int price,
                      string currencyId, bool affordable, string rarity = null,
                      bool equipped = false, bool locked = false, string lockReason = null,
                      string iconPath = null)
        {
            Id = id;
            Name = name;
            IconRole = iconRole;
            IconName = iconName;
            Price = price;
            CurrencyId = currencyId;
            Affordable = affordable;
            Rarity = rarity;
            Equipped = equipped;
            Locked = locked;
            LockReason = lockReason;
            IconPath = iconPath;
        }
    }
}
