// =============================================================================
// GearAppraisal — WO-300 Elarion weaponsmithing lore + appraisal mechanic.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Read-only lore surface over the gear catalog. Given a WeaponDef / ArmorDef it
// returns the "appraisal" a vendor (Sable the Jeweler / Coppin at the Market)
// would give: the item's lore line, its maker's mark, its crafting tier, and an
// ESTIMATED VALUE in crystals derived from tier + stats. It mutates nothing —
// it only reads the def + the GearTierTable lookup.
//
// LORE (LORE_ELARION_WEAPONSMITHING.md): an Elarion blade bore a maker's mark on
// the tang (Emberhand / Oathweld / Heartwood / Last-Pressing) and the realm learned
// to read them. Marks here are derived from the item's saga / forge when not set
// explicitly on the def, so existing items appraise sensibly with no JSON edits.
//
// PURE-ish: no MonoBehaviour, no I/O, no cross-assembly calls beyond the same-asm
// GearCatalog + GearTier. WebGL-safe (no reflection, no File I/O). ASCII output.
// =============================================================================

using System;
using System.Text;
using DeNelle.Village.Crafting;

namespace DeNelle.Village
{
    /// <summary>
    /// Result of appraising a piece of gear: lore + mark + tier + an estimated value.
    /// Value class so callers (vendor dialogue, tooltips) can surface fields individually.
    /// </summary>
    public sealed class GearAppraisalResult
    {
        public string id;
        public string displayName;
        public GearTier tier;
        public string tierLabel;     // "Common" / "Fine" / "Master" / "Legendary"
        public string tierHex;       // UI tint, e.g. "#FF9A1F"
        public string makersMark;    // the forge stamp (may be empty for plain gear)
        public string flavor;        // lore flavour line (may be empty)
        public bool isElarionMarked; // true if it carries a recognised Elarion mark
        public bool isLegendary;     // tier == Legendary (the Aegis / saga pieces)
        public int estimatedValue;   // appraised worth, in crystals

        /// <summary>One-line vendor-style summary (e.g. for a dialogue bark or log).</summary>
        public string Summary()
        {
            var sb = new StringBuilder();
            sb.Append(displayName).Append(" — ").Append(tierLabel);
            if (!string.IsNullOrEmpty(makersMark))
                sb.Append(", ").Append(makersMark).Append(" mark");
            sb.Append(" — worth ~").Append(estimatedValue).Append(" crystals.");
            return sb.ToString();
        }

        /// <summary>Multi-line appraisal text for a tooltip / vendor panel.</summary>
        public string FullText()
        {
            var sb = new StringBuilder();
            sb.Append(displayName).Append("  (").Append(tierLabel).Append(")\n");
            if (!string.IsNullOrEmpty(flavor)) sb.Append('"').Append(flavor).Append("\"\n");
            if (!string.IsNullOrEmpty(makersMark))
            {
                sb.Append("Maker's mark: ").Append(makersMark);
                if (isElarionMarked) sb.Append("  (a true Elarion mark)");
                sb.Append('\n');
            }
            sb.Append("Appraised value: ~").Append(estimatedValue).Append(" crystals.");
            return sb.ToString();
        }
    }

    /// <summary>
    /// Static, read-only appraisal service over the gear catalog. Every entry point
    /// null-guards and degrades gracefully (a null def -> null result).
    /// </summary>
    public static class GearAppraisal
    {
        // The four canonical Elarion forge marks (LORE §2). Used both to recognise a
        // real mark and to derive one from a saga/forge string when the def omits it.
        public const string MarkEmberhand   = "Emberhand";   // the Forge (steel)
        public const string MarkOathweld    = "Oathweld";    // the Armory (plating)
        public const string MarkHeartwood   = "Heartwood";   // the Lumbermill (living wood)
        public const string MarkLastPressing = "Last-Pressing"; // the Windmill (quench)

        /// <summary>Appraise a weapon. Returns null for a null def.</summary>
        public static GearAppraisalResult Appraise(WeaponDef w)
        {
            if (w == null) return null;
            var tier = GearTierTable.Parse(w.rarity);
            string mark = ResolveMark(w.makersMark, w.saga, w.IsAegis || tier == GearTier.Legendary);
            return new GearAppraisalResult
            {
                id = w.id,
                displayName = string.IsNullOrEmpty(w.name) ? w.id : w.name,
                tier = tier,
                tierLabel = GearTierTable.DisplayName(tier),
                tierHex = GearTierTable.RarityHex(tier),
                makersMark = mark,
                flavor = ResolveFlavor(w.flavor, w.saga, tier),
                isElarionMarked = IsElarionMark(mark),
                isLegendary = tier == GearTier.Legendary,
                estimatedValue = EstimateWeaponValue(w, tier),
            };
        }

        /// <summary>Appraise a piece of armor. Returns null for a null def.</summary>
        public static GearAppraisalResult Appraise(ArmorDef a)
        {
            if (a == null) return null;
            var tier = GearTierTable.Parse(a.rarity);
            string mark = ResolveMark(a.makersMark, a.saga, a.IsAegis || tier == GearTier.Legendary);
            return new GearAppraisalResult
            {
                id = a.id,
                displayName = string.IsNullOrEmpty(a.name) ? a.id : a.name,
                tier = tier,
                tierLabel = GearTierTable.DisplayName(tier),
                tierHex = GearTierTable.RarityHex(tier),
                makersMark = mark,
                flavor = ResolveFlavor(a.flavor, a.saga, tier),
                isElarionMarked = IsElarionMark(mark),
                isLegendary = tier == GearTier.Legendary,
                estimatedValue = EstimateArmorValue(a, tier),
            };
        }

        /// <summary>Appraise a ring/amulet accessory. Returns null for a null def.</summary>
        public static GearAppraisalResult Appraise(AccessoryDef ac)
        {
            if (ac == null) return null;
            var tier = GearTierTable.Parse(ac.rarity);
            string mark = ResolveMark(ac.makersMark, ac.saga, ac.IsAegis || tier == GearTier.Legendary);
            return new GearAppraisalResult
            {
                id = ac.id,
                displayName = string.IsNullOrEmpty(ac.name) ? ac.id : ac.name,
                tier = tier,
                tierLabel = GearTierTable.DisplayName(tier),
                tierHex = GearTierTable.RarityHex(tier),
                makersMark = mark,
                flavor = ResolveFlavor(ac.flavor, ac.saga, tier),
                isElarionMarked = IsElarionMark(mark),
                isLegendary = tier == GearTier.Legendary,
                estimatedValue = EstimateAccessoryValue(ac, tier),
            };
        }

        /// <summary>Appraise by weapon id (catalog lookup). Null if not found.</summary>
        public static GearAppraisalResult AppraiseWeaponId(string id) => Appraise(GearCatalog.FindWeapon(id));

        /// <summary>Appraise by armor id (catalog lookup). Null if not found.</summary>
        public static GearAppraisalResult AppraiseArmorId(string id) => Appraise(GearCatalog.FindArmor(id));

        /// <summary>True if the mark is one of the four recognised Elarion forge stamps.</summary>
        public static bool IsElarionMark(string mark)
        {
            if (string.IsNullOrEmpty(mark)) return false;
            return mark.Equals(MarkEmberhand, StringComparison.OrdinalIgnoreCase)
                || mark.Equals(MarkOathweld, StringComparison.OrdinalIgnoreCase)
                || mark.Equals(MarkHeartwood, StringComparison.OrdinalIgnoreCase)
                || mark.Equals(MarkLastPressing, StringComparison.OrdinalIgnoreCase);
        }

        // ---- value model -----------------------------------------------------
        // Estimated worth (in crystals) is derived purely from tier + the item's
        // combat stats, so it tracks the same numbers that make the item desirable:
        //   value = round( BaseByTier(tier) + StatWorth )
        // A legendary / Elarion-marked piece carries a prestige premium on top.

        /// <summary>
        /// WO-1064 Gold acquisition floor. The 1,000-Gold rewarded daily chest is the denominator:
        /// common alternative 1 day, fine 2, master 6, epic-band 12, true legendary 25.
        /// </summary>
        public static int TierBaseValue(GearTier tier)
        {
            switch (tier)
            {
                case GearTier.Fine:      return 2000;
                case GearTier.Master:    return 6000;
                case GearTier.Legendary: return 12000;
                case GearTier.Common:
                default:                 return 1000;
            }
        }

        private static int EstimateWeaponValue(WeaponDef w, GearTier tier)
        {
            // A weapon's worth scales with how much extra damage it grants over a
            // baseline 1.0 multiplier (50 crystals per +1.0 mult), plus a small
            // bonus for melee reach beyond the default 3.2m hitbox.
            float statWorth = Math.Max(0f, w.damageMult - 1f) * 1000f;
            if (w.reach > 3.2f) statWorth += (w.reach - 3.2f) * 200f;
            float effectPremium = string.IsNullOrEmpty(w.effectKind) ? 0f : TierBaseValue(tier) * 0.20f;
            int floor = string.Equals(w.rarity, "legendary", StringComparison.OrdinalIgnoreCase)
                ? 25000 : TierBaseValue(tier);
            return FinishValue(floor, statWorth + effectPremium, tier, w.IsAegis);
        }

        private static int EstimateArmorValue(ArmorDef a, GearTier tier)
        {
            // Armor's worth scales with its fractional damage reduction (300 crystals
            // per full 1.0 of defense -> 0.28 plate ~= 84) plus a touch per hpBonus.
            float statWorth = Math.Max(0f, a.defense) * 4000f + Math.Max(0f, a.hpBonus) * 20f;
            return FinishValue(TierBaseValue(tier), statWorth, tier, IsElarionMark(ResolveMark(a.makersMark, a.saga, a.IsAegis)));
        }

        private static int EstimateAccessoryValue(AccessoryDef ac, GearTier tier)
        {
            // An accessory's worth blends its (additive) damage + defense + flat HP bonuses, on the
            // same scales the weapon/armor models use (50/+1.0 dmg, 300/+1.0 def, 0.5/hp).
            float statWorth = Math.Max(0f, ac.damageMult) * 50f
                            + Math.Max(0f, ac.defense) * 300f
                            + Math.Max(0f, ac.hpBonus) * 0.5f;
            return FinishValue(TierBaseValue(tier), statWorth, tier, IsElarionMark(ResolveMark(ac.makersMark, ac.saga, ac.IsAegis)));
        }

        private static int FinishValue(int baseValue, float statWorth, GearTier tier, bool elarionMarked)
        {
            float v = baseValue + statWorth;
            // Prestige premium: a recognised Elarion mark, and especially a legendary
            // saga piece, commands far more than its raw stats — "buy an Elarion blade,
            // and bury your gold with it." (LORE §2)
            // Lore and ordinary maker marks carry no power price. A true set/saga item may
            // carry the explicit prestige premium supplied by the caller.
            if (elarionMarked) v *= 1.15f;
            return Mathf_RoundToInt(v);
        }

        // Local rounding so this file stays free of a hard UnityEngine.Mathf dependency
        // in the value math (keeps it trivially unit-testable). Round half-up.
        private static int Mathf_RoundToInt(float f) => (int)Math.Floor(f + 0.5f);

        // ---- lore derivation -------------------------------------------------

        /// <summary>
        /// The maker's mark to show. Prefers an explicit def.makersMark; else derives one
        /// from the saga text (which names the forge/crafter); else, for a legendary piece
        /// with no hint, falls back to the Emberhand (the Forge) so legends read as marked.
        /// </summary>
        private static string ResolveMark(string explicitMark, string saga, bool legendaryHint)
        {
            if (!string.IsNullOrEmpty(explicitMark)) return explicitMark.Trim();
            string derived = DeriveMarkFromSaga(saga);
            if (!string.IsNullOrEmpty(derived)) return derived;
            return legendaryHint ? MarkEmberhand : string.Empty;
        }

        /// <summary>Scan a saga string for a forge/craft keyword and map it to its mark.</summary>
        private static string DeriveMarkFromSaga(string saga)
        {
            if (string.IsNullOrEmpty(saga)) return string.Empty;
            string s = saga.ToLowerInvariant();
            // Order: most specific craft signals first.
            if (s.Contains("oathweld") || s.Contains("halvard")) return MarkOathweld;
            if (s.Contains("heartwood") || s.Contains("pell") || s.Contains("living wood") || s.Contains("living-wood")) return MarkHeartwood;
            if (s.Contains("last pressing") || s.Contains("last-pressing") || s.Contains("wren") || s.Contains("quench")) return MarkLastPressing;
            if (s.Contains("threefold") || s.Contains("borin") || s.Contains("reforged") || s.Contains("steel")) return MarkEmberhand;
            return string.Empty;
        }

        /// <summary>
        /// The flavour line to show. Prefers an explicit def.flavor; else the saga (legendary
        /// pieces ship rich saga text); else a generic tier-appropriate line. Never null.
        /// </summary>
        private static string ResolveFlavor(string explicitFlavor, string saga, GearTier tier)
        {
            if (!string.IsNullOrEmpty(explicitFlavor)) return explicitFlavor.Trim();
            if (!string.IsNullOrEmpty(saga)) return saga.Trim();
            switch (tier)
            {
                case GearTier.Master:
                    return "Master-forged. The old smiths would have nodded at this one.";
                case GearTier.Legendary:
                    return "They say a true Elarion edge hums when it is held to the ear.";
                case GearTier.Fine:
                    return "Honest work, kept keen.";
                case GearTier.Common:
                default:
                    return string.Empty;
            }
        }
    }
}
