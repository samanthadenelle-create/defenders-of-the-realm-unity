// =============================================================================
// ItemIconCatalog — maps an item (weapon / armor / consumable) to a real ARTWORK
// sprite sliced off the icon sheets, with the existing type-glyph as the fallback.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// RUNTIME LOAD (WebGL-safe): the sliced sheets are mirrored into
// Assets/Resources/ItemIcons by ItemIconSlicer. We load every sub-sprite via
// Resources.LoadAll<Sprite>("ItemIcons/<sheet>") and index them by sprite name.
// NO AssetDatabase, NO StreamingAssets, NO File IO -> safe in WebGL/IL2CPP.
// (AssetDatabase would be editor-only; StreamingAssets needs UnityWebRequest on
// web/Android. Resources.LoadAll is synchronous + works on every target.)
//
// MAPPING: we reuse the SAME keyword logic the inventory's glyph mapper already
// uses (id + name keyword match, job fallback) so a sword/bow/staff/shield/plate/
// helm/potion resolves to its art sheet sprite. Rarity (common..legendary) picks
// the tier column (t1..t5) for the tiered weapon sheets. When no sprite matches,
// callers fall back to the glyph (Sprite == null is the signal).
//
// Sheet sprite names are defined by ItemIconSlicer (kept in lockstep):
//   sword_t1..t5, bow_t1..t5,
//   shield_wooden/steel/rune/dragon/magical,
//   helm_a..e, chest_a..f, gauntlet_a/b, belt_a..d, pauldron_a,
//   potion_health/mana/poison/strength/fire (+ colour variants),
//   mat_herb/crystal/rune/scroll/ore/ingot (+ variants).
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>Resolves item-art sprites (sprite-first, glyph-fallback). Lazy, cached.</summary>
    public static class ItemIconCatalog
    {
        // sprite-name -> Sprite, built once from the Resources/ItemIcons sheets.
        private static Dictionary<string, Sprite> _byName;
        private static bool _loaded;

        // The sliced sheets we pull sub-sprites from (under Resources/ItemIcons).
        private static readonly string[] Sheets =
        {
            "ItemIcons/Ud37F", // swords    sword_t1..t5
            "ItemIcons/inEJH", // bows      bow_t1..t5
            "ItemIcons/WRdWM", // shields   shield_*
            "ItemIcons/VxBVb", // armour    helm_/chest_/gauntlet_/belt_/pauldron_
            "ItemIcons/bRUz5", // potions   potion_*
            "ItemIcons/CtQcX", // crafting  mat_*/potion_*
            "ItemIcons/jdRCa", // crafting  potion_*/mat_*
        };

        // ---------------------------------------------------------------------
        // PUBLIC API
        // ---------------------------------------------------------------------

        /// <summary>Art sprite for a weapon, or null (caller uses the glyph fallback).</summary>
        public static Sprite ForWeapon(WeaponDef w)
        {
            if (w == null) { FlowTrace.Warn("ItemIcon", "ForWeapon(<null>) -> null"); return null; }
            FlowTrace.Step("ItemIcon", $"ForWeapon id='{w.id}' name='{w.name}' rarity='{w.rarity}'");
            // Catalog-authored icon (ITEM_MODEL §3): the rendered mesh silhouette keyed by id.
            // When present, this is authoritative — image + title both identify the same shape.
            var authored = LoadAuthoredIcon(w.iconPath);
            if (authored != null)
            {
                FlowTrace.Step("ItemIcon", $"ForWeapon '{w.id}' -> authored icon '{w.iconPath}'");
                return authored;
            }

            EnsureLoaded();
            string key = ((w.id ?? "") + " " + (w.name ?? "")).ToLowerInvariant();
            int tier = RarityTier(w.rarity);

            // Daggers have no tiered sheet -> use a crafting-sheet dagger if present.
            if (Has(key, "dagger", "knife", "dirk", "stiletto"))
                return First("mat_dagger", "mat_dirk");

            if (Has(key, "bow", "recurve", "longbow", "shortbow"))
                return Tier("bow_t", tier);

            // Wand / staff / scepter / rod -> staff art lives on the swords-adjacent
            // mega sheet only; no dedicated tiered staff row, so fall through to the
            // sword sheet by tier (acts as the generic "magic weapon" silhouette) is
            // wrong visually -> prefer NO sprite so the staff GLYPH (✦) shows instead.
            if (Has(key, "wand", "staff", "scepter", "sceptre", "stave", "rod"))
            {
                FlowTrace.Warn("ItemIcon", $"ForWeapon '{w.id}' is a staff/wand -> null (no tiered staff sheet; caller shows ✦ glyph)");
                return null;
            }

            // Swords / blades / great-weapons -> tiered sword sheet.
            if (Has(key, "greatsword", "claymore", "sword", "blade", "longsword",
                         "saber", "sabre", "edge", "brand", "breaker", "keeper",
                         "axe", "hatchet", "hammer", "maul", "mace"))
                return Tier("sword_t", tier);

            // Fallback by class.
            switch ((w.job ?? "").ToLowerInvariant())
            {
                case "ranger": return Tier("bow_t", tier);
                case "mage":   return null;                 // staff -> glyph
                case "cleric": return null;                 // censer -> glyph
                default:       return Tier("sword_t", tier); // knight / unknown
            }
        }

        /// <summary>Art sprite for an armor piece, or null (caller uses the glyph fallback).</summary>
        public static Sprite ForArmor(ArmorDef a)
        {
            if (a == null) { FlowTrace.Warn("ItemIcon", "ForArmor(<null>) -> null"); return null; }
            FlowTrace.Step("ItemIcon", $"ForArmor id='{a.id}' name='{a.name}' rarity='{a.rarity}'");
            var authored = LoadAuthoredIcon(a.iconPath);
            if (authored != null)
            {
                FlowTrace.Step("ItemIcon", $"ForArmor '{a.id}' -> authored icon '{a.iconPath}'");
                return authored;
            }

            EnsureLoaded();
            string key = ((a.id ?? "") + " " + (a.name ?? "")).ToLowerInvariant();

            // Shields are tiered by named material; map keyword -> material, else by rarity.
            if (Has(key, "shield", "aegis", "buckler", "ward"))
            {
                if (Has(key, "wood"))                                return Get("shield_wooden");
                if (Has(key, "steel", "iron", "heater"))             return Get("shield_steel");
                if (Has(key, "rune", "enchant"))                     return Get("shield_rune");
                if (Has(key, "dragon", "drake", "wyrm"))             return Get("shield_dragon");
                if (Has(key, "magic", "magical", "arcane", "glow"))  return Get("shield_magical");
                // Otherwise pick the shield material by rarity tier.
                switch (RarityTier(a.rarity))
                {
                    case 1:  return Get("shield_wooden");
                    case 2:  return Get("shield_steel");
                    case 3:  return Get("shield_rune");
                    case 4:  return Get("shield_dragon");
                    default: return Get("shield_magical");
                }
            }

            // Helmets / hoods / crowns.
            if (Has(key, "helm", "helmet", "hood", "crown", "cap", "coif"))
                return TierLetter("helm_", a.rarity, 5);

            // Gauntlets / gloves.
            if (Has(key, "gauntlet", "glove", "bracer"))
                return First("gauntlet_a", "gauntlet_b");

            // Belts / girdles / faulds.
            if (Has(key, "belt", "girdle", "sash", "fauld"))
                return TierLetter("belt_", a.rarity, 4);

            // Body armour: plate / mail / leather / cloth / chest -> chest tiers.
            if (Has(key, "plate", "platemail", "chain", "mail", "chainmail",
                         "leather", "hide", "cloth", "robe", "cloak", "garb",
                         "cuirass", "chest", "body", "armor", "armour", "vest"))
                return TierLetter("chest_", a.rarity, 6);

            // Unknown armour -> a chest piece by rarity (still reads as "armour").
            return TierLetter("chest_", a.rarity, 6);
        }

        /// <summary>Art sprite for a consumable id/name, or null (glyph fallback).</summary>
        public static Sprite ForConsumable(string id, string name)
        {
            FlowTrace.Step("ItemIcon", $"ForConsumable id='{id}' name='{name}'");
            EnsureLoaded();
            string key = ((id ?? "") + " " + (name ?? "")).ToLowerInvariant();

            if (Has(key, "health", "heal", "hp", "regen", "life", "vitality"))
                return Get("potion_health");
            if (Has(key, "mana", "aether", "ether", "arcane", "magic"))
                return First("potion_mana", "potion_mana_b");
            if (Has(key, "strength", "might", "power", "rage", "berserk"))
                return First("potion_strength", "potion_strength_b");
            if (Has(key, "poison", "venom", "toxic", "toxin"))
                return First("potion_poison", "potion_poison_b");
            if (Has(key, "fire", "flame", "burn", "bomb", "oil", "incendiary"))
                return Get("potion_fire");
            if (Has(key, "herb", "leaf", "plant", "root", "flora"))
                return First("mat_herb", "mat_herb_b", "mat_herb_c");
            if (Has(key, "crystal", "gem", "shard"))
                return First("mat_crystal_a", "mat_crystal_b", "mat_crystal_g");
            if (Has(key, "rune", "runestone", "stone"))
                return First("mat_rune_a", "mat_rune_b", "mat_rune_e");
            if (Has(key, "scroll", "tome", "parchment"))
                return First("mat_scroll_a", "mat_scroll_b");
            if (Has(key, "ore", "ingot", "metal", "bar"))
                return First("mat_ore", "mat_ingot_a", "mat_ingot_b");

            // Any other potion-ish item -> a generic potion if available.
            if (Has(key, "potion", "elixir", "draught", "tonic", "flask", "brew"))
                return First("potion_a", "potion_e", "potion_mana");

            FlowTrace.Warn("ItemIcon", $"ForConsumable '{id}' matched no keyword -> null (caller uses glyph fallback)");
            return null; // -> glyph fallback
        }

        /// <summary>
        /// F8-641 - art for a CRAFTING MATERIAL row, resolved ONLY from that row's own
        /// authored fields: the catalog iconPath first, then a mat_* sheet sprite chosen by
        /// the row's authored CATEGORY. It deliberately does NOT run the potion keyword
        /// mapper: "HealthHerb"/"Iron Scrap"/"Oil Flask" all keyword-match potion rows, which
        /// is exactly how a material came to render a health bottle under its own name. When
        /// nothing resolves it returns null so the caller paints the row's authored GLYPH -
        /// an honest blank beats another item's art.
        /// </summary>
        public static Sprite ForMaterial(string id, string iconPath, string category)
        {
            FlowTrace.Step("ItemIcon", $"ForMaterial id='{id}' iconPath='{iconPath}' category='{category}'");
            var authored = LoadAuthoredIcon(iconPath);
            if (authored != null)
            {
                FlowTrace.Step("ItemIcon", $"ForMaterial '{id}' -> authored icon '{iconPath}'");
                return authored;
            }

            EnsureLoaded();
            switch ((category ?? "").ToLowerInvariant())
            {
                case "herb":
                case "fungus":
                case "petal":
                case "plant":
                case "flora":   return First("mat_herb", "mat_herb_b", "mat_herb_c");
                case "crystal":
                case "gem":     return First("mat_crystal_a", "mat_crystal_b", "mat_crystal_g");
                case "metal":
                case "ore":
                case "ingot":   return First("mat_ore", "mat_ingot_a", "mat_ingot_b");
                case "stone":
                case "rune":    return First("mat_rune_a", "mat_rune_b", "mat_rune_e");
                case "scroll":
                case "parchment": return First("mat_scroll_a", "mat_scroll_b");
                // A pouch reads as "some raw stuff" - correct for powders/fibres/saps and,
                // crucially, unmistakable for a potion.
                case "dust":
                case "resin":
                case "cloth":   return First("mat_pouch_a", "mat_pouch_b", "mat_pouch_c");
            }

            FlowTrace.Warn("ItemIcon", $"ForMaterial '{id}' has no authored icon and category '{category}' " +
                "maps to no mat_* sheet -> null (caller shows the row's own glyph)");
            return null;
        }

        // Resources-relative path from catalog (e.g. "ItemIcons/tripo_sword_a") — no extension.
        private static Sprite LoadAuthoredIcon(string iconPath)
        {
            if (string.IsNullOrEmpty(iconPath)) return null;
            return Resources.Load<Sprite>(iconPath);
        }

        // ---------------------------------------------------------------------
        // LOAD + LOOKUP
        // ---------------------------------------------------------------------
        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            _byName = new Dictionary<string, Sprite>(System.StringComparer.OrdinalIgnoreCase);

            FlowTrace.Step("ItemIcon", $"EnsureLoaded -> {Sheets.Length} sheet(s) under Resources/ItemIcons");
            for (int i = 0; i < Sheets.Length; i++)
            {
                Sprite[] subs;
                try { subs = Resources.LoadAll<Sprite>(Sheets[i]); }
                catch (System.Exception e)
                {
                    // Hard load fault on a sheet — error level (was Debug.LogWarning only).
                    FlowTrace.Fail("ItemIcon", $"LoadAll FAILED for '{Sheets[i]}': {e.GetType().Name}: {e.Message}");
                    Debug.LogWarning("[ItemIconCatalog] load failed for " + Sheets[i] + ": " + e.Message);
                    continue;
                }
                if (subs == null || subs.Length == 0)
                {
                    FlowTrace.Warn("ItemIcon", $"sheet '{Sheets[i]}' resolved 0 sub-sprites (not sliced/imported)");
                    continue;
                }
                for (int j = 0; j < subs.Length; j++)
                {
                    var sp = subs[j];
                    if (sp == null || string.IsNullOrEmpty(sp.name)) continue;
                    _byName[sp.name] = sp; // later sheets win on a name clash (none expected)
                }
            }

            if (_byName.Count == 0)
            {
                // Data-empty (not a render bug): every caller silently drops to the glyph fallback.
                FlowTrace.Warn("ItemIcon", "0 item-icon sprites indexed under Resources/ItemIcons " +
                    "-> ALL items use glyph fallback (run Defenders/Art/Slice Item Icons)");
                Debug.LogWarning("[ItemIconCatalog] no item-icon sprites found under Resources/ItemIcons — " +
                                 "run Defenders/Art/Slice Item Icons. Inventory will use glyph fallback.");
            }
            else
            {
                FlowTrace.Step("ItemIcon", $"indexed {_byName.Count} item-icon sprite(s)");
            }
        }

        private static Sprite Get(string name)
        {
            if (string.IsNullOrEmpty(name) || _byName == null) return null;
            Sprite s; return _byName.TryGetValue(name, out s) ? s : null;
        }

        // First non-null among the candidates (graceful when a sheet lacks a name).
        private static Sprite First(params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                var s = Get(names[i]);
                if (s != null) return s;
            }
            return null;
        }

        // "<prefix>" + tier (1..5), clamped to the available range.
        private static Sprite Tier(string prefix, int tier)
        {
            tier = Mathf.Clamp(tier, 1, 5);
            var s = Get(prefix + tier);
            if (s != null) return s;
            // Walk down to the nearest existing tier so we always show art.
            for (int t = tier; t >= 1; t--) { s = Get(prefix + t); if (s != null) return s; }
            for (int t = tier; t <= 5; t++) { s = Get(prefix + t); if (s != null) return s; }
            return null;
        }

        // "<prefix>" + a letter (a..) chosen by rarity across `count` variants.
        private static Sprite TierLetter(string prefix, string rarity, int count)
        {
            if (count < 1) return null;
            int tier = RarityTier(rarity);                       // 1..5
            int idx = Mathf.Clamp(Mathf.RoundToInt((tier - 1) / 4f * (count - 1)), 0, count - 1);
            char letter = (char)('a' + idx);
            var s = Get(prefix + letter);
            if (s != null) return s;
            for (int k = 0; k < count; k++) { s = Get(prefix + (char)('a' + k)); if (s != null) return s; }
            return null;
        }

        // common=1 .. legendary=5 (matches the inventory's rarity ladder).
        private static int RarityTier(string rarity)
        {
            switch ((rarity ?? "common").ToLowerInvariant())
            {
                case "uncommon":  return 2;
                case "rare":      return 3;
                case "epic":      return 4;
                case "legendary": return 5;
                default:          return 1; // common
            }
        }

        private static bool Has(string haystack, params string[] needles)
        {
            for (int i = 0; i < needles.Length; i++)
                if (haystack.IndexOf(needles[i], System.StringComparison.Ordinal) >= 0) return true;
            return false;
        }
    }
}
