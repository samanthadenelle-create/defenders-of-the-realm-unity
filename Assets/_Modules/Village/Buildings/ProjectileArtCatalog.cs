// =============================================================================
// ProjectileArtCatalog — maps a projectile (by element / tower type / Ranger bow)
// to a real ARTWORK sprite sliced off the projectile sheets, with a graceful
// default when a sprite is missing.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// RUNTIME LOAD (WebGL-safe): the sliced sheets are mirrored into
// Assets/Resources/ProjectileIcons by ProjectileArtSlicer. We load every
// sub-sprite via Resources.LoadAll<Sprite>("ProjectileIcons/<sheet>") and index
// them by sprite name. NO AssetDatabase, NO StreamingAssets, NO File IO -> safe
// in WebGL/IL2CPP. Mirrors the proven ItemIconCatalog pattern.
//
// MAPPING:
//   • Towers fire by DamageElement (TowerCombat already computes it from the
//     tower's empowerment / level). Element -> bolt sprite + impact sprite:
//        None/Physical -> arrow_plain        (archer bolt)
//        Aether/Arcane -> bolt_arcane         + impact_arcane
//        Flame         -> bolt_fire           + impact_fire
//        Ice           -> bolt_ice_blue       + impact_ice
//     A tower can also be classified by towerName ("archer"/"mage"/"frost"/
//     "lightning"/"arcane") via ForTowerName when no element is set, so an
//     un-empowered Archer fires an arrow and an un-empowered Frost fires ice.
//   • The Ranger's bow always fires arrow_plain (ForArrow); the Mage's orb fires
//     the arcane bolt (ForElement(Aether)).
//
// Sheet sprite names are defined by ProjectileArtSlicer (kept in lockstep):
//   arrow_plain/fire/ice/red/gold/steel/dark (+ variants),
//   bolt_fire/arcane/ice_blue/lightning/holy/nature (+ _lc lifecycle variants),
//   impact_fire/arcane/ice/lightning (+ cast_* flashes).
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Combat;

namespace DeNelle.Village
{
    /// <summary>Resolves projectile travel + impact sprites by element / tower type.
    /// Sprite-first, default-fallback. Lazy, cached, WebGL-safe.</summary>
    public static class ProjectileArtCatalog
    {
        // sprite-name -> Sprite, built once from the Resources/ProjectileIcons sheets.
        private static Dictionary<string, Sprite> _byName;
        private static bool _loaded;

        // The sliced sheets we pull sub-sprites from (under Resources/ProjectileIcons).
        private static readonly string[] Sheets =
        {
            "ProjectileIcons/projectiles_arrows_magic",
            "ProjectileIcons/projectiles_spell_vfx_lifecycle",
        };

        // ---------------------------------------------------------------------
        // PUBLIC API
        // ---------------------------------------------------------------------

        /// <summary>Travel-body sprite for a projectile of <paramref name="element"/>,
        /// or null (caller keeps its procedural visual).</summary>
        public static Sprite ForElement(DamageElement element)
        {
            EnsureLoaded();
            switch (element)
            {
                case DamageElement.Aether: return First("bolt_arcane", "bolt_arcane_lc");
                case DamageElement.Flame:  return First("bolt_fire", "bolt_fire_lc", "arrow_fire");
                case DamageElement.Ice:    return First("bolt_ice_blue", "bolt_ice_lc", "arrow_ice");
                default:                   return ForArrow(); // None / physical -> arrow
            }
        }

        /// <summary>Impact-burst sprite for a projectile of <paramref name="element"/>,
        /// or null (caller skips the sprite impact).</summary>
        public static Sprite ImpactForElement(DamageElement element)
        {
            EnsureLoaded();
            switch (element)
            {
                case DamageElement.Aether: return First("impact_arcane", "bolt_arcane");
                case DamageElement.Flame:  return First("impact_fire", "bolt_fire");
                case DamageElement.Ice:    return First("impact_ice", "impact_ice_b");
                default:                   return First("impact_arcane"); // generic spark
            }
        }

        /// <summary>The plain archer/Ranger arrow sprite, or null.</summary>
        public static Sprite ForArrow()
        {
            EnsureLoaded();
            return First("arrow_plain", "arrow_plain_b", "arrow_steel", "arrow_gold");
        }

        /// <summary>The Mage spell-orb sprite (arcane bolt), or null.</summary>
        public static Sprite ForSpellOrb()
        {
            EnsureLoaded();
            return First("bolt_arcane", "bolt_arcane_lc");
        }

        /// <summary>Classify a tower by its data name into a travel sprite when no
        /// element is set (un-empowered base shot). archer->arrow, mage/arcane->
        /// arcane bolt, frost/ice->ice, fire->fire, lightning->lightning.</summary>
        public static Sprite ForTowerName(string towerName)
        {
            EnsureLoaded();
            string n = (towerName ?? string.Empty).ToLowerInvariant();
            if (Has(n, "frost", "ice", "glacial")) return First("bolt_ice_blue", "arrow_ice");
            if (Has(n, "lightning", "storm", "shock", "thunder")) return First("bolt_lightning", "bolt_lightning_lc");
            if (Has(n, "fire", "flame", "ember", "inferno")) return First("bolt_fire", "arrow_fire");
            if (Has(n, "mage", "arcane", "aether", "mystic", "wizard")) return First("bolt_arcane", "bolt_arcane_lc");
            if (Has(n, "archer", "arrow", "ranger", "ballista")) return ForArrow();
            return ForArrow();
        }

        /// <summary>Element a tower's NAME implies, used to pick the matching impact
        /// when the base shot carries no DamageElement.</summary>
        public static DamageElement ElementForTowerName(string towerName)
        {
            string n = (towerName ?? string.Empty).ToLowerInvariant();
            if (Has(n, "frost", "ice", "glacial")) return DamageElement.Ice;
            if (Has(n, "fire", "flame", "ember", "inferno")) return DamageElement.Flame;
            if (Has(n, "mage", "arcane", "aether", "mystic", "wizard")) return DamageElement.Aether;
            return DamageElement.None;
        }

        // ---------------------------------------------------------------------
        // LOAD + LOOKUP
        // ---------------------------------------------------------------------
        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            _byName = new Dictionary<string, Sprite>(System.StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < Sheets.Length; i++)
            {
                Sprite[] subs;
                try { subs = Resources.LoadAll<Sprite>(Sheets[i]); }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[ProjectileArtCatalog] load failed for " + Sheets[i] + ": " + e.Message);
                    continue;
                }
                if (subs == null) continue;
                for (int j = 0; j < subs.Length; j++)
                {
                    var sp = subs[j];
                    if (sp == null || string.IsNullOrEmpty(sp.name)) continue;
                    _byName[sp.name] = sp;
                }
            }

            if (_byName.Count == 0)
                Debug.LogWarning("[ProjectileArtCatalog] no projectile sprites found under " +
                                 "Resources/ProjectileIcons — run Defenders/Art/Slice Projectile Icons. " +
                                 "Projectiles will use their procedural visual.");
        }

        private static Sprite Get(string name)
        {
            if (string.IsNullOrEmpty(name) || _byName == null) return null;
            Sprite s; return _byName.TryGetValue(name, out s) ? s : null;
        }

        private static Sprite First(params string[] names)
        {
            for (int i = 0; i < names.Length; i++)
            {
                var s = Get(names[i]);
                if (s != null) return s;
            }
            return null;
        }

        private static bool Has(string haystack, params string[] needles)
        {
            for (int i = 0; i < needles.Length; i++)
                if (haystack.IndexOf(needles[i], System.StringComparison.Ordinal) >= 0) return true;
            return false;
        }
    }
}
