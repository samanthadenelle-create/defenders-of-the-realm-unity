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
        public float damageMult = 1f;
        public GearReq req;
    }

    /// <summary>A piece of armor: defense = fractional incoming-damage reduction (0.04 = 4%).</summary>
    [Serializable]
    public sealed class ArmorDef
    {
        public string id;
        public string name;
        public string icon;
        public string job;
        public string rarity;
        public float defense;     // 0..0.9 fractional damage reduction
        public float hpBonus;     // carried for a later pass; v1 applies defense only
        public GearReq req;
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
        private const string WeaponsPath = "Data/Canonical/weapons.json";
        private const string ArmorPath   = "Data/Canonical/armor.json";

        private static List<WeaponDef> _weapons;
        private static List<ArmorDef>  _armor;

        /// <summary>Forces a re-read of both catalogs.</summary>
        public static void Reload()
        {
            _weapons = null;
            _armor = null;
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

        /// <summary>Highest-defense armor the given class+level can equip, or null.</summary>
        public static ArmorDef BestArmor(string job, int level)
        {
            EnsureLoaded();
            ArmorDef best = null;
            if (_armor != null)
            {
                foreach (var a in _armor)
                {
                    if (a == null || !JobMatches(a.job, job) || !MeetsReq(a.req, level)) continue;
                    if (best == null || a.defense > best.defense) best = a;
                }
            }
            return best;
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
            if (_weapons == null) _weapons = LoadWeapons();
            if (_armor   == null) _armor   = LoadArmor();
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

        private static T LoadJson<T>(string relativePath, string label) where T : class
        {
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
