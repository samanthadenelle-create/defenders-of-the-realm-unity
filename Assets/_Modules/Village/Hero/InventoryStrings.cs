// =============================================================================
// InventoryStrings — the ONE home for every word the Bag ("The Armory Rail") says.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WO-1133 D9. Player-facing sentences live in canon-strings.json (CLAUDE.md §7),
// in BOTH canonical copies (Assets/Resources/Data/Canonical and
// Assets/StreamingAssets/Data/Canonical), byte-identical and ASCII-only — TMP
// renders non-ASCII as tofu. Nothing in this file is a sentence; this class only
// names KEYS and hands the resolve to VillageStrings, the module's existing loader.
//
// WHY A KEYS-ONLY TWIN AND NOT A NEW LOADER: StoreStrings (DeNelle.Wallet),
// PromoStrings (DeNelle.Core), CanonStrings (DeNelle.Onboarding) and VillageStrings
// (this assembly) already establish the shape — the asmdefs do not let one module
// reach another's reader (read the .asmdef, CLAUDE.md §5). The Bag is IN
// DeNelle.Village, so it does not even need a twin loader: VillageStrings is right
// here. Duplicating the WORDS would be duplicated state; naming the keys is not.
//
// A missing key resolves to the house "[[missing:key]]" marker via VillageStrings —
// visible on screen rather than a silent blank, which is what makes a typo
// self-reporting instead of a mystery.
// =============================================================================

using DeNelle.Village.Hero;   // InventoryTabKind — the section identity the empty line is keyed by

namespace DeNelle.Village
{
    /// <summary>Canon-backed copy for the Bag / Armory Rail. Keys only — no sentences.</summary>
    public static class InventoryStrings
    {
        // ── Rail entries (D9) ────────────────────────────────────────────────
        /// <summary>Rail entry one — the gear section.</summary>
        public const string KeyRailGear     = "invRailGear";
        /// <summary>Rail entry — loose weapons.</summary>
        public const string KeyRailWeapons  = "invRailWeapons";
        /// <summary>Rail entry — loose armor.</summary>
        public const string KeyRailArmor    = "invRailArmor";
        /// <summary>Rail entry — trinkets.</summary>
        public const string KeyRailTrinkets = "invRailTrinkets";
        /// <summary>Rail entry — potions / consumables.</summary>
        public const string KeyRailPotions  = "invRailPotions";
        /// <summary>Rail entry — the talent tree (a pseudo-section: it routes out).</summary>
        public const string KeyRailSkills   = "invRailSkills";
        /// <summary>Rail entry — the realm map (dormant behind FeatureFlags.MapTab).</summary>
        public const string KeyRailMap      = "invRailMap";
        /// <summary>The dim badge on the dormant Map entry — never a colour-only tell.</summary>
        public const string KeyRailMapSoon  = "invRailMapSoon";
        /// <summary>The badge a rail entry carries when the worn item lives in it.</summary>
        public const string KeyRailWorn     = "invRailWorn";
        /// <summary>The rail column's own caption.</summary>
        public const string KeyRailHeader   = "invRailHeader";

        // WO-1254 top-tab/navigation copy. These are deliberately distinct from the
        // retired rail keys so the gate can prove the painted surface has exactly six
        // category destinations and no pseudo-tab.
        public const string KeyTabGear       = "invTabGear";
        public const string KeyTabWeapons    = "invTabWeapons";
        public const string KeyTabOffHand    = "invTabOffHand";
        public const string KeyTabArmor      = "invTabArmor";
        public const string KeyTabTrinkets   = "invTabTrinkets";
        public const string KeyTabPotions    = "invTabPotions";
        public const string KeyMoreCount     = "invMoreCount";
        public const string KeyMoreBelow     = "invMoreBelow";
        public const string KeyEmptyOffHand  = "invEmptyOffHand";
        public const string KeyGoToItems     = "invGoToItems";
        public const string KeyHeaderTalents = "invHeaderTalents";
        public const string KeyNextTabsHint  = "invNextTabsHint";

        // ── Worn-slot keys, the Gear section (D3 / D9) ───────────────────────
        /// <summary>Worn slot — main hand.</summary>
        public const string KeySlotMainHand = "invSlotMainHand";
        /// <summary>Worn slot — off hand / shield.</summary>
        public const string KeySlotOffHand  = "invSlotOffHand";
        /// <summary>Worn slot — body armor.</summary>
        public const string KeySlotArmor    = "invSlotArmor";
        /// <summary>Worn slot — amulet.</summary>
        public const string KeySlotAmulet   = "invSlotAmulet";
        /// <summary>Worn slot — ring.</summary>
        public const string KeySlotRing     = "invSlotRing";
        /// <summary>A vacant worn slot reads this, never a blank plate (D3).</summary>
        public const string KeySlotEmpty    = "invSlotEmpty";

        // ── Empty-section lines (D9). Each names WHAT FILLS IT. ──────────────
        /// <summary>Weapons section, empty — the NORMAL early-game case.</summary>
        public const string KeyEmptyWeapons   = "invEmptyWeapons";
        /// <summary>Armor section, empty.</summary>
        public const string KeyEmptyArmor     = "invEmptyArmor";
        /// <summary>Trinkets section, empty.</summary>
        public const string KeyEmptyTrinkets  = "invEmptyTrinkets";
        /// <summary>Potions section, empty.</summary>
        public const string KeyEmptyPotions   = "invEmptyPotions";
        /// <summary>Skills section — a pseudo-section, so it states what opens.</summary>
        public const string KeyEmptySkills    = "invEmptySkills";
        /// <summary>Map section while FeatureFlags.MapTab is OFF (visible, inert).</summary>
        public const string KeyEmptyMapLocked = "invEmptyMapLocked";

        // ── Pane, nothing selected (D3 pane states) ──────────────────────────
        /// <summary>Pane heading when no item is selected.</summary>
        public const string KeyPaneNoSelection      = "invPaneNoSelection";
        /// <summary>The highest-value gap line on the Gear section.</summary>
        public const string KeyPaneGearGaps         = "invPaneGearGaps";
        /// <summary>Shown where the delta column would be while the model exposes no worn comparison.</summary>
        public const string KeyPaneNothingToCompare = "invPaneNothingToCompare";
        public const string KeyGearPaneTitle        = "invGearPaneTitle";
        public const string KeyGearPaneGuide        = "invGearPaneGuide";
        public const string KeyGearPaneOpenSlots    = "invGearPaneOpenSlots";
        public const string KeyGearPaneComplete     = "invGearPaneComplete";

        // ── Pane, item selected ──────────────────────────────────────────────
        /// <summary>Compare column header — what is worn.</summary>
        public const string KeyPaneColumnWorn = "invPaneColumnWorn";
        /// <summary>Compare column header — the candidate.</summary>
        public const string KeyPaneColumnThis = "invPaneColumnThis";
        /// <summary>The WORN badge — the WORD carries the state, never a tint (D5).</summary>
        public const string KeyPaneWornBadge  = "invPaneWornBadge";
        /// <summary>Primary action — equip the selected gear.</summary>
        public const string KeyActionEquip    = "invActionEquip";
        /// <summary>Primary action — drink / use the selected consumable.</summary>
        public const string KeyActionUse      = "invActionUse";
        /// <summary>Primary action face when the selection is already worn.</summary>
        public const string KeyActionWorn     = "invActionWorn";
        /// <summary>Wayfinding action — "Go to {0}", {0} = a section or vendor name.</summary>
        public const string KeyActionGoTo     = "invActionGoTo";
        /// <summary>The line under the action naming what the action replaces. {0} = item name.</summary>
        public const string KeyNextReplaces   = "invNextReplaces";
        /// <summary>Footer next-step hint — comparison.</summary>
        public const string KeyNextCompareHint = "invNextCompareHint";
        /// <summary>Footer next-step hint — the rail is the navigation.</summary>
        public const string KeyNextRailHint    = "invNextRailHint";
        /// <summary>Footer next-step hint — counts are on the rail.</summary>
        public const string KeyNextCountHint   = "invNextCountHint";

        // ── Verdict lines (composed, not stored whole — D9) ──────────────────
        /// <summary>The two-clause tradeoff shape: "{0}, {1}." Clauses come from the deltas.</summary>
        public const string KeyVerdictTradeoff = "invVerdictTradeoff";
        /// <summary>Every measured stat improves.</summary>
        public const string KeyVerdictBetter   = "invVerdictBetter";
        /// <summary>Every measured stat worsens.</summary>
        public const string KeyVerdictWorse    = "invVerdictWorse";
        /// <summary>No measured stat moves.</summary>
        public const string KeyVerdictSame     = "invVerdictSame";
        /// <summary>The selection IS the worn item.</summary>
        public const string KeyVerdictWearing  = "invVerdictWearing";

        // ── Purse strip ──────────────────────────────────────────────────────
        /// <summary>Purse chip identity — gold.</summary>
        public const string KeyPurseGold     = "invPurseGold";
        /// <summary>Purse chip identity — crystals.</summary>
        public const string KeyPurseCrystals = "invPurseCrystals";
        /// <summary>Purse chip identity — flasks.</summary>
        public const string KeyPurseFlasks   = "invPurseFlasks";

        /// <summary>
        /// EVERY key this screen can paint. The regression walks this array against BOTH
        /// canonical copies, so a key added here without being authored — or authored in
        /// only one copy — fails the gate instead of reaching the player as
        /// "[[missing:invSomething]]".
        /// </summary>
        public static readonly string[] AllKeys =
        {
            KeyRailGear, KeyRailWeapons, KeyRailArmor, KeyRailTrinkets, KeyRailPotions,
            KeyRailSkills, KeyRailMap, KeyRailMapSoon, KeyRailWorn, KeyRailHeader,
            KeyTabGear, KeyTabWeapons, KeyTabOffHand, KeyTabArmor, KeyTabTrinkets,
            KeyTabPotions, KeyMoreCount, KeyMoreBelow, KeyEmptyOffHand, KeyGoToItems,
            KeyHeaderTalents, KeyNextTabsHint,
            KeySlotMainHand, KeySlotOffHand, KeySlotArmor, KeySlotAmulet, KeySlotRing, KeySlotEmpty,
            KeyEmptyWeapons, KeyEmptyArmor, KeyEmptyTrinkets, KeyEmptyPotions, KeyEmptySkills,
            KeyEmptyMapLocked,
            KeyPaneNoSelection, KeyPaneGearGaps, KeyPaneNothingToCompare,
            KeyGearPaneTitle, KeyGearPaneGuide, KeyGearPaneOpenSlots, KeyGearPaneComplete,
            KeyPaneColumnWorn, KeyPaneColumnThis, KeyPaneWornBadge,
            KeyActionEquip, KeyActionUse, KeyActionWorn, KeyActionGoTo,
            KeyNextReplaces, KeyNextCompareHint, KeyNextRailHint, KeyNextCountHint,
            KeyVerdictTradeoff, KeyVerdictBetter, KeyVerdictWorse, KeyVerdictSame, KeyVerdictWearing,
            KeyPurseGold, KeyPurseCrystals, KeyPurseFlasks,
        };

        /// <summary>Resolve one key from canon-strings.json (visible marker when absent).</summary>
        public static string Get(string key) => VillageStrings.Canon(key);

        /// <summary>
        /// Resolve a key that carries {0}/{1} placeholders and fill them. A malformed format
        /// string returns the RAW canon text rather than throwing — a broken placeholder must
        /// degrade to a readable sentence, never to an exception inside a UI build.
        /// </summary>
        public static string Format(string key, params object[] args)
        {
            string raw = Get(key);
            if (args == null || args.Length == 0) return raw;
            try { return string.Format(raw, args); }
            catch (System.FormatException) { return raw; }
        }

        /// <summary>
        /// The empty-section line for a rail entry — the "never show nothing" rule (D2.2).
        /// The purse strip re-uses THIS resolve for the emptiest section, so one string sits
        /// in two placements and the wording can never drift between them (D9).
        /// </summary>
        public static string EmptyLineFor(InventoryTabKind tab)
        {
            switch (tab)
            {
                case InventoryTabKind.Weapons:     return Get(KeyEmptyWeapons);
                case InventoryTabKind.OffHand:     return Get(KeyEmptyOffHand);
                case InventoryTabKind.Armor:       return Get(KeyEmptyArmor);
                case InventoryTabKind.Outfits:     return Get(KeyEmptyTrinkets);
                case InventoryTabKind.Consumables: return Get(KeyEmptyPotions);
                default:                           return Get(KeyEmptyWeapons);
            }
        }
    }
}
