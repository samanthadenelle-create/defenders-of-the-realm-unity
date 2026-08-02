// =============================================================================
// GearCatalog — typed model + loader for weapons.json / armor.json (Gear v1).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Mirrors AbilityCatalog.cs exactly: canonical JSON under StreamingAssets, read
// via Application.streamingAssetsPath, parsed by Newtonsoft.Json. Gear is CONTENT,
// not code — add/retune weapons + armor by editing the JSON, no recompile.
//
// A weapon contributes a damageMult to the hero's damage chain (base x talent x
// level x timing x WEAPON). Armor contributes a fractional incoming-damage
// reduction. Equip eligibility is gated by req (level v1; dex/arcane/might later).
//
// ANDROID NOTE: same StreamingAssets caveat as AbilityCatalog (a UnityWebRequest
// read is required on Android; synchronous File.ReadAllText is valid in Editor /
// Windows / macOS). To be revisited with the Seeker build.
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using DeNelle.Core.State;

namespace DeNelle.Village
{
    /// <summary>Equip requirement. Level-gated for v1; attribute keys (dex/arcane/might) are
    /// carried for a later pass and default to 0 (= no requirement).</summary>
    [Serializable]
    public sealed class GearReq
    {
        public int level = 1;
        public int dex;
        public int arcane;
        public int might;
    }

    /// <summary>A weapon: its damageMult multiplies the hero's outgoing ability damage.</summary>
    [Serializable]
    public sealed class WeaponDef
    {
        public string id;
        public string name;
        public string icon;
        public string job;        // "mage" | "knight" | "ranger" | "any"
        public string rarity;

        // ELEMENTAL brand (fire-VFX verification pipeline). OPTIONAL; empty/null = no element
        // (unchanged behavior). When set (e.g. "fire"), the melee on-hit path plays a full
        // multi-layer elemental impact VFX at the hit point via the shared VFXManager pool,
        // LAYERED on the existing weaponskill impact — so an elemental blade is VISUALLY read
        // in combat. Catalog-driven: WeaponVfxMap.ElementalOnHitKey is the ONE reader that maps
        // this string to a HovlVfxCatalog key, so ANY future element:"fire" weapon shows the
        // effect (never hardcoded to one weapon id). Newtonsoft leaves it null when the row omits it.
        public string element;

        // HAND-SLOT model (owner 2026-06-18, docs/STORE_EQUIP_SPEC.md "Equip-slot rules").
        // These three fields exist in the canonical weapons.json but were previously DROPPED on
        // deserialize because WeaponDef didn't declare them. Now read so the equip layer can
        // enforce main-hand/off-hand rules without inventing data.
        //   hand       — "1h" (one-handed; may share with an off-hand) | "2h" (occupies BOTH hands).
        //                Empty/absent => treated as "1h" (the permissive default; never blocks).
        //   category   — weapon family: "sword"|"axe"|"bow"|"staff"|"shield"|... A "shield" is an
        //                OFF-HAND item (seats in the off hand, never the main hand).
        //   damageType — "melee"|"ranged"|"magic" (carried for shop deltas/buffs; not gameplay-gated yet).
        public string hand;
        public string category;
        public string damageType;

        public float damageMult = 1f;
        // MELEE reach (m): extends the basic-attack hitbox radius for a melee weapon
        // (greatsword/polearm/axe outreach a dagger). 0 = "unset" -> the hero keeps
        // PlayerAttackController's fixed AttackRange. Only meaningful for melee jobs
        // (knight); ranged jobs (mage/ranger) attack via AbilityDef.Range and leave
        // this 0, so their reach is unaffected.
        public float reach = 0f;
        public GearReq req;

        // WO-295: Legendary "Aegis of Elarion" set. setId groups items into a set
        // (the per-class Aegis weapons + the Aegis armor all carry "aegis"); the
        // AegisSetEffect detects a full set by this key. Empty for ordinary gear.
        public string setId;

        // WO-295: saga flavour text (names the four crafters). Optional; null for v1 gear.
        public string saga;

        // WO-300: Elarion weaponsmithing lore. Both optional + default empty so existing
        // items are unaffected. `flavor` = item flavour line (Bright Centuries tone);
        // `makersMark` = the forge stamp on the tang (Emberhand/Oathweld/Heartwood/Last-Pressing)
        // that the realm — and an appraiser — learns to read. Surfaced via GearAppraisal.
        public string flavor;
        public string makersMark;

        // Shop integration (basic): resource costs. Populated from canonical JSON.
        public int buyWood;
        public int buyFood;
        public int buyIron;
        public int buyCrystals;

        // WO-Item-1: the catalog⊥repo LOOK half (docs/ITEM_MODEL.md §3). prefabPath =
        // the equippable model, iconPath = the inventory/store sprite. Both are NULL
        // for now — WO-Item-2's gear generator populates them per owned asset; do NOT
        // hand-author them here. The legacy emoji `icon` stays the v1 placeholder.
        public string prefabPath;
        public string iconPath;

        // WO-Item (Addressables equip): HOW prefabPath is loaded. The Blink generator
        // stamps "addressable" on every Blink row (its prefabPath is an Addressables
        // address, e.g. "gear/weapon/Sword1h_01"); legacy/Tripo rows leave it null/empty
        // and continue to resolve through the hardcoded Resources map in EquipmentController.
        // EquipmentController.LoadsViaAddressable(WeaponDef) reads THIS (or the "gear/"
        // address prefix) to pick the load path. Newtonsoft populates it when present.
        public string loadVia;

        // WO-Item-1: OPTIONAL explicit capability override from JSON (null when absent).
        // When present it wins; when absent the kind default applies (see Capabilities).
        // Nullable so a row without the field is unchanged (Newtonsoft leaves it null).
        public ItemCapability? capabilities;

        /// <summary>WO-Item-1: the entry's resolved capability flags. A Weapon defaults to
        /// Carriable|Equippable (docs/ITEM_MODEL.md §2/§3); an explicit JSON `capabilities`
        /// override wins when present. Systems read THIS, never the catalog-of-origin.</summary>
        public ItemCapability Capabilities =>
            capabilities ?? (ItemCapability.Carriable | ItemCapability.Equippable);

        // ── WO-861 ARROW RIDERS (Sylas's ammo-as-weapon) ────────────────────────────
        // The Ranger's "weapon" is the ARROW, so an equipped arrow carries an on-hit rider
        // its basic attack applies. Read by HeroAbilities.TryResolveAmmoRider and applied
        // ONLY on the Ranger's locked-Q basic (gated by IsArrowRiderEligible) -- Knight and
        // Mage basics are provably unaffected.
        // THIS FILE WAS ORPHANED: the effects lane and the data lane EACH believed the other
        // owned GearCatalog.cs, so these fields were never added and every rider sat inert
        // behind a hard `return false`. Both halves shipped correct and the SEAM between them
        // was empty -- the classic file-fence gap. Added by the orchestrator, who owns the seams.
        // Absent in JSON => null/0 => no rider, so every existing weapon row is unchanged.
        /// <summary>On-hit rider an equipped arrow applies: "burn" | "poison" | "slow". Empty/null = none.</summary>
        public string ammoEffect;
        /// <summary>Damage-per-second for a burn/poison rider.</summary>
        public float ammoDps;
        /// <summary>Duration (seconds) of the rider.</summary>
        public float ammoSeconds;
        /// <summary>Slow magnitude 0..1 for a "slow" rider (0.35 = -35% move speed).</summary>
        public float ammoSlowPct;

        /// <summary>WO-295: part of the legendary Aegis of Elarion set.</summary>
        public bool IsAegis =>
            !string.IsNullOrEmpty(setId) && setId.Equals("aegis", StringComparison.OrdinalIgnoreCase);

        // ── HAND-SLOT predicates (docs/STORE_EQUIP_SPEC.md "Equip-slot rules") ──────
        /// <summary>True when this weapon occupies BOTH hands (hand=="2h"). Absent/empty => 1h.</summary>
        public bool IsTwoHanded =>
            !string.IsNullOrEmpty(hand) && hand.Trim().Equals("2h", StringComparison.OrdinalIgnoreCase);

        /// <summary>True when this is an OFF-HAND item (a shield). Seats in the off hand, never the main hand.</summary>
        public bool IsOffHandItem =>
            !string.IsNullOrEmpty(category) && category.Trim().Equals("shield", StringComparison.OrdinalIgnoreCase);

        /// <summary>True when this is a one-handed MAIN-HAND weapon (1h and not a shield/off-hand item).</summary>
        public bool IsOneHandedMain => !IsTwoHanded && !IsOffHandItem;
    }

    /// <summary>A piece of armor: defense = fractional incoming-damage reduction (0.04 = 4%).</summary>
    [Serializable]
    public sealed class ArmorDef
    {
        public string id;
        public string name;
        public string icon;
        public string job;
        // ARMOR WEIGHT CLASS (owner 2026-06-16 "armor lightweight heavy distinction"):
        // "light" = Ranger + Mage may wear it; "heavy" = Knight + Cleric. Empty / "any" =
        // no weight restriction (a universal starter / endgame set anyone can wear).
        // Weapons stay 1:1 by `job`; armor groups by this weight instead. See
        // GearCatalog.ClassWeight / ArmorFitsClass.
        public string weight;
        public string rarity;
        public float defense;     // 0..0.9 fractional damage reduction
        public float hpBonus;     // carried for a later pass; v1 applies defense only
        public GearReq req;

        // WO-295: set key (see WeaponDef.setId). The Aegis armor carries "aegis";
        // AegisSetEffect needs BOTH an Aegis weapon and Aegis armor for the full set.
        public string setId;

        // WO-295: saga flavour text (names the four crafters). Optional; null for v1 gear.
        public string saga;

        // WO-300: Elarion weaponsmithing lore (see WeaponDef). Optional + default empty.
        public string flavor;
        public string makersMark;

        // Shop integration (basic): resource costs. Populated from canonical JSON.
        public int buyWood;
        public int buyFood;
        public int buyIron;
        public int buyCrystals;

        // WO-Item-1: the catalog⊥repo LOOK half (docs/ITEM_MODEL.md §3). See WeaponDef.
        // NULL for now — WO-Item-2's generator populates them; do NOT hand-author here.
        public string prefabPath;
        public string iconPath;

        // WO-Item (Addressables): HOW prefabPath loads (mirrors WeaponDef.loadVia). The Blink
        // generator stamps "addressable" on every Blink armor row (prefabPath = a "gear/armor/.."
        // Addressables key); null/"resources" => a Resources path. HeroArmorVisual reads this.
        public string loadVia;

        // WO-Item-1: OPTIONAL explicit capability override from JSON (null when absent).
        public ItemCapability? capabilities;

        /// <summary>WO-Item-1: the entry's resolved capability flags. Gear/Armor defaults to
        /// Carriable|Equippable (docs/ITEM_MODEL.md §2/§3); an explicit JSON `capabilities`
        /// override wins when present. Systems read THIS, never the catalog-of-origin.</summary>
        public ItemCapability Capabilities =>
            capabilities ?? (ItemCapability.Carriable | ItemCapability.Equippable);

        /// <summary>WO-295: part of the legendary Aegis of Elarion set.</summary>
        public bool IsAegis =>
            !string.IsNullOrEmpty(setId) && setId.Equals("aegis", StringComparison.OrdinalIgnoreCase);
    }

    [Serializable] public sealed class WeaponCatalogData { public List<WeaponDef> weapons; }
    [Serializable] public sealed class ArmorCatalogData  { public List<ArmorDef> armor; }

    /// <summary>
    /// Static loader + query surface for the weapon / armor catalogs. Graceful: every
    /// query null-guards, so a missing/empty catalog simply yields no gear (the hero
    /// falls back to a 1.0 multiplier / 0 defense — existing combat is unchanged).
    /// </summary>
    public static class GearCatalog
    {
        private const string WeaponsPath     = "Data/Canonical/weapons.json";
        private const string ArmorPath       = "Data/Canonical/armor.json";
        private const string AccessoriesPath = "Data/Canonical/accessories.json";

        private static List<WeaponDef>    _weapons;
        private static List<ArmorDef>     _armor;
        private static List<AccessoryDef> _accessories;

        /// <summary>Forces a re-read of all catalogs (weapons + armor + accessories).</summary>
        public static void Reload()
        {
            _weapons = null;
            _armor = null;
            _accessories = null;
            EnsureLoaded();
        }

        /// <summary>Highest-damageMult weapon the given class+level can equip, or null.</summary>
        public static WeaponDef BestWeapon(string job, int level)
        {
            EnsureLoaded();
            WeaponDef best = null;
            if (_weapons != null)
            {
                foreach (var w in _weapons)
                {
                    if (w == null || !JobMatches(w.job, job) || !MeetsReq(w.req, level)) continue;
                    if (best == null || w.damageMult > best.damageMult) best = w;
                }
            }
            return best;
        }

        /// <summary>Highest-defense armor the given class+level can equip, or null.
        /// Respects BOTH the legacy `job` gate and the new weight-class gate (light/heavy).</summary>
        public static ArmorDef BestArmor(string job, int level)
        {
            EnsureLoaded();
            ArmorDef best = null;
            if (_armor != null)
            {
                foreach (var a in _armor)
                {
                    if (a == null || !JobMatches(a.job, job) || !ArmorFitsClass(a, job) || !MeetsReq(a.req, level)) continue;
                    if (best == null || a.defense > best.defense) best = a;
                }
            }
            return best;
        }

        /// <summary>
        /// Highest-damageMult ONE-HANDED MAIN-HAND weapon (1h, not a shield) the class+level can
        /// equip, or null. Used by the hand-slot enforcement as the main-hand fall-back when a 2H
        /// weapon is removed (equipping a shield while a 2H is held) — keeps the armed-hero invariant:
        /// a 1H can coexist with the off-hand, a 2H cannot.
        /// </summary>
        public static WeaponDef BestOneHandedWeapon(string job, int level)
        {
            EnsureLoaded();
            WeaponDef best = null;
            if (_weapons != null)
            {
                foreach (var w in _weapons)
                {
                    if (w == null || !w.IsOneHandedMain) continue;
                    if (!JobMatches(w.job, job) || !MeetsReq(w.req, level)) continue;
                    if (best == null || w.damageMult > best.damageMult) best = w;
                }
            }
            return best;
        }

        // ── Class restriction surface (equip UI + auto-equip) ────────────────────

        /// <summary>The armor WEIGHT a class wears: Ranger/Mage = "light", Knight/Cleric =
        /// "heavy" (owner 2026-06-16). Unknown classes default to "heavy" (front-line safe).</summary>
        public static string ClassWeight(string job)
        {
            switch ((job ?? string.Empty).ToLowerInvariant())
            {
                case "ranger":
                case "mage":   return "light";
                case "knight":
                case "cleric": return "heavy";
                default:       return "heavy";
            }
        }

        /// <summary>True when <paramref name="job"/> may wear armor <paramref name="a"/>: a
        /// piece with no weight (empty / "any") fits everyone; otherwise its weight must
        /// match the class's weight (light↔Ranger/Mage, heavy↔Knight/Cleric).</summary>
        public static bool ArmorFitsClass(ArmorDef a, string job)
        {
            if (a == null) return false;
            string w = (a.weight ?? string.Empty).Trim().ToLowerInvariant();
            if (w.Length == 0 || w == "any") return true;
            return w == ClassWeight(job);
        }

        /// <summary>True when <paramref name="job"/> may wield weapon <paramref name="w"/>
        /// (its `job` is "any" or matches the class). Public wrapper over the job-match rule
        /// so the equip UI can filter the weapon list per selected party member.</summary>
        public static bool WeaponFitsClass(WeaponDef w, string job)
        {
            return w != null && JobMatches(w.job, job);
        }

        /// <summary>Find exact weapon by id (case-insensitive), or null. Used by shop/equip flows.</summary>
        public static WeaponDef FindWeapon(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            EnsureLoaded();
            if (_weapons == null) return null;
            foreach (var w in _weapons)
            {
                if (w != null && string.Equals(w.id, id, StringComparison.OrdinalIgnoreCase)) return w;
            }
            return null;
        }

        /// <summary>Find exact armor by id (case-insensitive), or null. Used by shop/equip flows.</summary>
        public static ArmorDef FindArmor(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            EnsureLoaded();
            if (_armor == null) return null;
            foreach (var a in _armor)
            {
                if (a != null && string.Equals(a.id, id, StringComparison.OrdinalIgnoreCase)) return a;
            }
            return null;
        }

        /// <summary>Find exact accessory (ring/amulet) by id (case-insensitive), or null. Used by shop/equip flows.</summary>
        public static AccessoryDef FindAccessory(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            EnsureLoaded();
            if (_accessories == null) return null;
            foreach (var ac in _accessories)
            {
                if (ac != null && string.Equals(ac.id, id, StringComparison.OrdinalIgnoreCase)) return ac;
            }
            return null;
        }

        /// <summary>All loaded accessory defs (for the Jeweler stock + EquipVM slot lists). Never null.</summary>
        public static IReadOnlyList<AccessoryDef> Accessories
        {
            get
            {
                EnsureLoaded();
                return _accessories ?? (IReadOnlyList<AccessoryDef>)System.Array.Empty<AccessoryDef>();
            }
        }

        /// <summary>All loaded accessory defs (alias of <see cref="Accessories"/> for vendor enumeration). Never null.</summary>
        public static IReadOnlyList<AccessoryDef> AllAccessories() => Accessories;

        /// <summary>Accessories that seat in <paramref name="slot"/> ("ring"/"amulet") and meet the level
        /// requirement. Job is "any" for v1 accessories, so only slot + level gate. Never null.</summary>
        public static IReadOnlyList<AccessoryDef> AccessoriesForSlot(string slot, int level)
        {
            EnsureLoaded();
            var list = new List<AccessoryDef>();
            if (_accessories == null || string.IsNullOrEmpty(slot)) return list;
            string s = slot.Trim().ToLowerInvariant();
            foreach (var ac in _accessories)
            {
                if (ac == null) continue;
                string acSlot = (ac.slot ?? string.Empty).Trim().ToLowerInvariant();
                if (acSlot != s) continue;
                if (!MeetsReq(ac.req, level)) continue;
                list.Add(ac);
            }
            return list;
        }

        /// <summary>All loaded weapon defs (for vendor stock enumeration). Never null.</summary>
        public static IReadOnlyList<WeaponDef> AllWeapons()
        {
            EnsureLoaded();
            return _weapons ?? (IReadOnlyList<WeaponDef>)System.Array.Empty<WeaponDef>();
        }

        /// <summary>All loaded armor defs (for vendor stock enumeration). Never null.</summary>
        public static IReadOnlyList<ArmorDef> AllArmors()
        {
            EnsureLoaded();
            return _armor ?? (IReadOnlyList<ArmorDef>)System.Array.Empty<ArmorDef>();
        }

        /// <summary>GOLD buy price for an accessory (for Economy.TrySpend). Mirrors the weapon/armor
        /// overloads: the price is GearAppraisal's estimated value, floored at 1. Null-safe.</summary>
        public static DeNelle.Village.ResourceCost GetBuyCost(AccessoryDef ac)
        {
            if (ac == null) return default;
            return new DeNelle.Village.ResourceCost(coins: GoldPrice(GearAppraisal.Appraise(ac)));
        }

        /// <summary>
        /// GOLD buy price for a weapon (for Economy.TrySpend). Vendor SHOPS now charge GOLD
        /// (Coins), not Wood/Iron/Crystals — the price is GearAppraisal's estimated value
        /// (tier + stats + Elarion-mark premium), the same number the buy label appraises.
        /// Floored at 1 so nothing is ever free. The legacy buy* JSON fields are retained on
        /// the def but no longer drive the shop cost. (Building UPGRADES stay on resources.)
        /// </summary>
        public static DeNelle.Village.ResourceCost GetBuyCost(WeaponDef w)
        {
            if (w == null) return default;
            return new DeNelle.Village.ResourceCost(coins: GoldPrice(GearAppraisal.Appraise(w)));
        }

        /// <summary>GOLD buy price for a piece of armor (for Economy.TrySpend). See the weapon overload.</summary>
        public static DeNelle.Village.ResourceCost GetBuyCost(ArmorDef a)
        {
            if (a == null) return default;
            return new DeNelle.Village.ResourceCost(coins: GoldPrice(GearAppraisal.Appraise(a)));
        }

        /// <summary>The gold price = the appraised estimated value, floored at 1. Null-safe.</summary>
        private static int GoldPrice(GearAppraisalResult appraisal)
        {
            int v = appraisal != null ? appraisal.estimatedValue : 0;
            return Mathf.Max(1, v);
        }

        private static bool JobMatches(string itemJob, string heroJob)
        {
            if (string.IsNullOrEmpty(itemJob)) return true;
            if (itemJob.Equals("any", StringComparison.OrdinalIgnoreCase)) return true;
            return itemJob.Equals(heroJob ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private static bool MeetsReq(GearReq req, int level)
        {
            // v1: level only. (dex/arcane/might carried but not yet enforced.)
            return req == null || level >= req.level;
        }

        private static void EnsureLoaded()
        {
            if (_weapons     == null) _weapons     = LoadWeapons();
            if (_armor       == null) _armor       = LoadArmor();
            if (_accessories == null) _accessories = LoadAccessories();
        }

        private static List<WeaponDef> LoadWeapons()
        {
            var data = LoadJson<WeaponCatalogData>(WeaponsPath, "weapons.json");
            return data?.weapons ?? new List<WeaponDef>();
        }

        private static List<ArmorDef> LoadArmor()
        {
            var data = LoadJson<ArmorCatalogData>(ArmorPath, "armor.json");
            return data?.armor ?? new List<ArmorDef>();
        }

        private static List<AccessoryDef> LoadAccessories()
        {
            var data = LoadJson<AccessoryCatalogData>(AccessoriesPath, "accessories.json");
            return data?.accessories ?? new List<AccessoryDef>();
        }

        private static T LoadJson<T>(string relativePath, string label) where T : class
        {
            // TODO adopt DataInjector: this is exactly DeNelle.Core.DataInjector.Inject<T>
            // (CanonicalJson.Read → JsonConvert.DeserializeObject<T>, WebGL-safe, guarded).
            // Kept inline for now so the gear-specific warning text ("gear disabled…") is
            // preserved; swap to DataInjector once a shared warning is acceptable.
            // WebGL-safe load via CanonicalJson (Resources first, StreamingAssets fallback).
            try
            {
                string json = DeNelle.Core.CanonicalJson.Read(relativePath);
                if (!string.IsNullOrEmpty(json))
                    return JsonConvert.DeserializeObject<T>(json);
                Debug.LogWarning($"[GearCatalog] {label} not found (Resources or StreamingAssets) — gear disabled (hero uses 1.0 mult / 0 defense).");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GearCatalog] Failed to read {label}: {ex.Message}");
            }
            return null;
        }
    }
}
