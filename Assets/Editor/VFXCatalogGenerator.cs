// =============================================================================
// VFXCatalogGenerator (WO-504 slice 1) - SCRIPT-authors the VFXCatalog asset that
// wires the AUTHORED, GIT-COMMITTED VFX prefabs (Lana Studio + Spells Pack + the
// custom Resources projectiles) onto the VFXType enum, so combat plays the pro
// VFX we own instead of the procedural AbilityVfxKit fallback.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor   Namespace: DeNelle.Editor   (editor-only)
//
// WHY THIS EXISTS (the finding, WO-504):
//   VFXManager resolves a VFXType -> prefab through a ScriptableObject VFXCatalog
//   (serialized Entry[] of {Type, Prefab, PoolSize, IsLoop, MinQuality}). NO
//   VFXCatalog asset existed, and VFXManager is not placed in any scene/prefab -
//   so _catalog was always null and EVERY effect fell back to procedural. This
//   generator CREATES the asset and populates it from curated prefab paths. A
//   companion runtime change (VFXManager auto-loads Resources/VFX/VFXCatalog when
//   _catalog is null) makes the wiring take effect with no inspector drag-drop.
//
// THE RULE (WO-504, honoured here):
//   * ONLY git-committed packs are referenced: Assets/Lana Studio/Casual RPG VFX,
//     Assets/Spells Pack/Particles/Prefabs, Assets/Resources/VFX/Projectiles.
//     NOTHING under Assets/Mirza Beig/** (gitignored, absent on clone).
//   * CURATED - "one soldier, not the brigade": ONE best prefab per wired VFXType.
//     Unity strips unreferenced assets from the build, so the rest stay benched on
//     disk. The asset is the ONLY new thing in Resources - no whole pack is dumped
//     into a Resources folder (build-size guard).
//   * Authored by SCRIPT (this generator), never inspector drag-drop (owner canon).
//   * Any VFXType NOT wired here keeps the procedural AbilityVfxKit fallback.
//
// WHY REFLECTION / SerializedObject:
//   DeNelle.Editor.asmdef does NOT reference DeNelle.Village (CLAUDE.md section 5). The
//   VFXCatalog / VFXType / Entry types are resolved by name and every field write
//   goes through SerializedObject - no compile-time dependency on DeNelle.Village.
//
// THE PICKS ARE BONES: the exact prefab per type is the owner's to felt-tune. To
// re-point any mapping, edit the Map table below and re-run. Idempotent.
//
// RUN:
//   Editor menu : Defenders/VFX/Generate VFX Catalog
//   Batchmode   : DeNelle.Editor.VFXCatalogGenerator.Generate
//   Prints marker: VFX_CATALOG_OK on success.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>
    /// Editor generator that builds Resources/VFX/VFXCatalog.asset mapping VFXType
    /// to curated git-committed prefabs. Reflection + SerializedObject so it never
    /// compile-depends on DeNelle.Village. Idempotent; prints VFX_CATALOG_OK.
    /// </summary>
    public static class VFXCatalogGenerator
    {
        private const string Marker     = "VFX_CATALOG_OK";
        private const string AssetDir   = "Assets/Resources/VFX";
        private const string AssetPath  = "Assets/Resources/VFX/VFXCatalog.asset";

        private const string CatalogTypeName = "DeNelle.Village.VFXCatalog, DeNelle.Village";
        private const string VfxTypeEnumName  = "DeNelle.Village.VFXType, DeNelle.Village";

        // -- Curated map: VFXType enum name -> {prefab asset path, isLoop, minQuality} --
        // ONE best prefab per wired type, git-committed packs only. Owner re-points
        // any line here and re-runs. Types absent from this table use procedural.
        private struct Pick
        {
            public string Path;
            public bool   IsLoop;
            public int    MinQuality;   // 0 always, 1 skip-Low, 2 High-only
            public int    PoolSize;
            public Pick(string path, bool isLoop = false, int minQuality = 0, int poolSize = 4)
            { Path = path; IsLoop = isLoop; MinQuality = minQuality; PoolSize = poolSize; }
        }

        private const string Lana   = "Assets/Lana Studio/Casual RPG VFX/Prefabs/";
        private const string Spells = "Assets/Spells Pack/Particles/Prefabs/";
        private const string Res    = "Assets/Resources/VFX/Projectiles/";

        // The curated table. Keep it minimal - high-traffic battle types first.
        private static readonly Dictionary<string, Pick> Map = new Dictionary<string, Pick>
        {
            // -- Impacts (oneshot hits) ----------------------------------------
            // Battle-polish: the melee hit is the highest-traffic combat moment.
            // Upgrade from the small Hit_stone spark to a readable SLASH ARC so every
            // sword connect reads as a strike, not a pebble poof. Still a cheap oneshot.
            { "Impact_Physical",        new Pick(Lana + "Slash/Slash_stone_once.prefab") },
            { "Impact_Flame",           new Pick(Lana + "Range_attack/Hit_fire.prefab") },
            { "Impact_Ice",             new Pick(Lana + "Range_attack/Hit_frost.prefab") },
            { "Impact_Aether",          new Pick(Lana + "Range_attack/Hit_magic.prefab") },
            { "Impact_Heal",            new Pick(Lana + "Range_attack/Hit_heart.prefab") },
            { "Impact_ExplosionFire",   new Pick(Spells + "Projectiles/Explosion/Explosion_Fire.prefab") },
            { "Impact_ExplosionAether", new Pick(Spells + "Projectiles/Explosion/Explosion_Arcane.prefab") },
            { "Impact_ShockwaveRing",   new Pick(Lana + "Burst/Burst_rings.prefab") },
            { "Impact_ShardsBurst",     new Pick(Lana + "Burst/Burst_sharp.prefab") },
            { "Impact_SmokeWisps",      new Pick(Lana + "Burst/Poof_generic.prefab") },

            // -- Projectiles (custom WebGL-safe Resources bodies; loop until hit) -
            { "Projectile_ArcaneBolt",  new Pick(Res + "Projectile_Arcane.prefab", isLoop: true) },
            { "Projectile_FrostBolt",   new Pick(Res + "Projectile_Ice.prefab",    isLoop: true) },
            { "Projectile_Arrow",       new Pick(Lana + "Range_attack/Projectiles_green_shuriken.prefab", isLoop: true) },
            { "Projectile_FlameArrow",  new Pick(Res + "Projectile_Fire.prefab",   isLoop: true) },
            { "Projectile_EnemyCasterBolt", new Pick(Lana + "Range_attack/Projectiles_dark_magic.prefab", isLoop: true) },

            // -- Casts (wind-up on caster) -------------------------------------
            { "Cast_MageCharge",        new Pick(Spells + "Projectiles/Casting/Casting_Arcane.prefab") },
            { "Cast_KnightSlam",        new Pick(Lana + "Burst/Flash_circle.prefab") },
            { "Cast_RangerDraw",        new Pick(Spells + "Projectiles/Casting/Casting_Nature.prefab") },
            { "Cast_Heal",              new Pick(Lana + "Regeneration/Regeneration_health.prefab") },
            { "Cast_FrostNova",         new Pick(Spells + "Projectiles/Casting/Casting_Ice.prefab") },
            { "Cast_NecromancerSummon", new Pick(Spells + "Projectiles/Casting/Casting_Dark.prefab") },
            { "Cast_EnemyCaster",       new Pick(Spells + "Projectiles/Casting/Casting_Dark_2.prefab") },

            // -- Deaths (oneshot burst) ----------------------------------------
            { "Death_Skeleton",         new Pick(Lana + "Burst/Poof_generic.prefab") },
            { "Death_Boss",             new Pick(Spells + "Projectiles/Explosion/Explosion_Dark.prefab") },
            { "Death_Brute",            new Pick(Lana + "Burst/Poof_electric.prefab") },
            { "Death_Wolf",             new Pick(Lana + "Burst/Poof_water.prefab") },
            { "Death_Tiefling",         new Pick(Spells + "Projectiles/Explosion/Explosion_Fire_2.prefab") },
            { "Death_Generic",          new Pick(Lana + "Burst/Poof_generic.prefab") },
            // Battle-polish: dungeon enemy death fires this live but had no wired prefab
            // (procedural only). A darker owned Dark explosion reads bigger than the
            // village Poof for the dungeon run. Owned, cheap oneshot.
            { "Death_EnemyExplosion_Dungeon", new Pick(Spells + "Projectiles/Explosion/Explosion_Dark_2.prefab") },

            // -- Auras (persistent loops) --------------------------------------
            { "Aura_Flame",             new Pick(Lana + "Fire/Fire_medium.prefab",  isLoop: true, minQuality: 1) },
            { "Aura_Ice",               new Pick(Lana + "Fog/Fog_frost.prefab",     isLoop: true, minQuality: 1) },
            { "Aura_Healer",            new Pick(Lana + "Regeneration/Regeneration_health_loop.prefab", isLoop: true, minQuality: 1) },
            { "Aura_EnemyCaster",       new Pick(Lana + "Orbs/Orbs_electric.prefab", isLoop: true, minQuality: 1) },
            { "Aura_Necromancer",       new Pick(Lana + "Fog/Fog_poison.prefab",     isLoop: true, minQuality: 1) },
            { "Aura_SmokeReaper",       new Pick(Lana + "Fog/Fog_speedSlow.prefab",  isLoop: true, minQuality: 1) },

            // -- Environment ---------------------------------------------------
            { "Env_TorchFlame",         new Pick(Lana + "Fire/Fire_small.prefab",   isLoop: true, minQuality: 1) },

            // -- Juice / Feedback ----------------------------------------------
            { "Juice_CriticalHit",      new Pick(Lana + "Burst/Flash_star.prefab") },
            { "Juice_KillStreak",       new Pick(Lana + "Burst/Burst_rainbow_mist.prefab") },
            { "Juice_WaveClear",        new Pick(Lana + "States/Level_up.prefab") },
            { "WaveClear_Celebration",  new Pick(Lana + "States/Level_up.prefab") },
            { "Juice_LevelUp",          new Pick(Lana + "States/Level_up.prefab") },
            { "LevelUp_Celebration",    new Pick(Lana + "States/Level_up.prefab") },
            { "Combo_Tier1",            new Pick(Lana + "Burst/Flash_circle.prefab") },
            { "Combo_Tier2",            new Pick(Lana + "Burst/Flash_dubble_circle.prefab") },
        };

        // -- Menu / batch entry ------------------------------------------------

        [MenuItem("Defenders/VFX/Generate VFX Catalog")]
        public static void Generate()
        {
            try
            {
                int wired = Build();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"[VFXCatalogGenerator] Wired {wired} VFXType entries into {AssetPath}.");
                Debug.Log(Marker);
            }
            catch (Exception e)
            {
                Debug.LogError($"[VFXCatalogGenerator] FAILED: {e.Message}\n{e.StackTrace}");
                // No marker on failure - the gate withholds VFX_CATALOG_OK.
            }
        }

        // -- Core build --------------------------------------------------------

        private static int Build()
        {
            var catalogType = Type.GetType(CatalogTypeName);
            if (catalogType == null)
                throw new Exception($"Could not resolve type '{CatalogTypeName}'. Is DeNelle.Village compiled?");

            var enumType = Type.GetType(VfxTypeEnumName);
            if (enumType == null)
                throw new Exception($"Could not resolve enum '{VfxTypeEnumName}'.");

            EnsureDir(AssetDir);

            // Load or create the catalog ScriptableObject.
            var catalog = AssetDatabase.LoadAssetAtPath(AssetPath, catalogType) as ScriptableObject;
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance(catalogType);
                AssetDatabase.CreateAsset(catalog, AssetPath);
            }

            // Build the resolved (enumValue, prefab, pick) rows from the curated map.
            var rows = new List<(int enumValue, GameObject prefab, Pick pick, string typeName)>();
            int skippedMissing = 0;
            foreach (var kv in Map)
            {
                string typeName = kv.Key;
                if (!Enum.IsDefined(enumType, typeName))
                {
                    Debug.LogWarning($"[VFXCatalogGenerator] VFXType.{typeName} not defined - skipping.");
                    continue;
                }
                int enumValue = (int)Enum.Parse(enumType, typeName);

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(kv.Value.Path);
                if (prefab == null)
                {
                    // Missing prefab (e.g. pack not imported on this machine). Skip - the
                    // type then keeps its procedural fallback. Never hard-fail the gate on
                    // an absent OPTIONAL pack prefab.
                    Debug.LogWarning($"[VFXCatalogGenerator] prefab missing for {typeName}: '{kv.Value.Path}' " +
                                     "- type stays procedural.");
                    skippedMissing++;
                    continue;
                }
                rows.Add((enumValue, prefab, kv.Value, typeName));
            }

            // Write Entries[] via SerializedObject (no compile-time Village dependency).
            var so = new SerializedObject(catalog);
            var entries = so.FindProperty("Entries");
            if (entries == null)
                throw new Exception("VFXCatalog has no serialized 'Entries' array property.");

            entries.arraySize = rows.Count;
            for (int i = 0; i < rows.Count; i++)
            {
                var (enumValue, prefab, pick, _) = rows[i];
                var e = entries.GetArrayElementAtIndex(i);

                var pType    = e.FindPropertyRelative("Type");
                var pPrefab  = e.FindPropertyRelative("Prefab");
                var pPool    = e.FindPropertyRelative("PoolSize");
                var pLoop    = e.FindPropertyRelative("IsLoop");
                var pMinQ    = e.FindPropertyRelative("MinQuality");
                var pLife    = e.FindPropertyRelative("LifetimeOverride");

                if (pType   != null) pType.enumValueIndex      = EnumIndexFor(enumType, enumValue);
                if (pPrefab != null) pPrefab.objectReferenceValue = prefab;
                if (pPool   != null) pPool.intValue            = pick.PoolSize;
                if (pLoop   != null) pLoop.boolValue           = pick.IsLoop;
                if (pMinQ   != null) pMinQ.intValue            = pick.MinQuality;
                if (pLife   != null) pLife.floatValue          = 0f;   // auto-detect
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);

            Debug.Log($"[VFXCatalogGenerator] {rows.Count} wired, {skippedMissing} skipped (missing prefab).");
            return rows.Count;
        }

        // SerializedProperty.enumValueIndex is the ORDINAL position in the enum's
        // value list, not the underlying int. Map the underlying value back to its
        // ordinal so the catalog stores the right VFXType.
        private static int EnumIndexFor(Type enumType, int underlyingValue)
        {
            var values = Enum.GetValues(enumType);
            for (int i = 0; i < values.Length; i++)
                if ((int)values.GetValue(i) == underlyingValue) return i;
            return 0;
        }

        private static void EnsureDir(string dir)
        {
            if (AssetDatabase.IsValidFolder(dir)) return;
            // Create parents as needed (Assets/Resources, then Assets/Resources/VFX).
            var parts = dir.Split('/');
            string cur = parts[0];   // "Assets"
            for (int i = 1; i < parts.Length; i++)
            {
                string next = cur + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(cur, parts[i]);
                cur = next;
            }
        }
    }
}
