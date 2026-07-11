// =============================================================================
// HovlVfxCatalogGenerator (WO-VFX-002) — SCRIPT-authors the HovlVfxCatalog asset
// that wires the shortlist Hovl Studio prefabs onto string keys, so any system can
// call VFXManager.PlayKey("Fireball_Projectile", ...) and get the pro Hovl effect.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor   Namespace: DeNelle.Editor   (editor-only)
//
// WHY THIS EXISTS:
//   VFXManager resolves a string key -> Hovl prefab through the HovlVfxCatalog
//   ScriptableObject (Rows[] of {Key, Prefab, PoolSize, DefaultScale,
//   DefaultLifetime, Recolorable, IsLoop}). The Hovl prefabs are NOT under
//   Resources/, so the catalog holds serialized prefab refs and the .asset itself
//   lives in Resources/VFX/HovlVfxCatalog.asset (the only new Resources item — no
//   whole pack is dumped in). This generator authors that asset from the curated
//   key->path table below (owner canon: authored by SCRIPT, never inspector drag).
//
// WHY REFLECTION / SerializedObject:
//   DeNelle.Editor.asmdef does NOT reference DeNelle.Village (CLAUDE.md §5). The
//   HovlVfxCatalog / Row types are resolved by name and every field write goes
//   through SerializedObject — no compile-time dependency on DeNelle.Village.
//
// THE PICKS ARE BONES: exact prefab per key is the owner's to felt-tune. Re-point
// any line in the Map table and re-run. Idempotent.
//
// RUN:
//   Editor menu : Defenders/VFX/Generate Hovl VFX Catalog
//   Batchmode   : DeNelle.Editor.HovlVfxCatalogGenerator.Generate
//   Prints marker: HOVL_VFX_CATALOG_OK on success.
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>
    /// Editor generator that builds Resources/VFX/HovlVfxCatalog.asset mapping string
    /// keys to shortlist Hovl prefabs. Reflection + SerializedObject so it never
    /// compile-depends on DeNelle.Village. Idempotent; prints HOVL_VFX_CATALOG_OK.
    /// </summary>
    public static class HovlVfxCatalogGenerator
    {
        private const string Marker    = "HOVL_VFX_CATALOG_OK";
        private const string AssetDir  = "Assets/Resources/VFX";
        private const string AssetPath = "Assets/Resources/VFX/HovlVfxCatalog.asset";

        private const string CatalogTypeName = "DeNelle.Village.HovlVfxCatalog, DeNelle.Village";

        private struct Pick
        {
            public string Path;
            public int    PoolSize;
            public float  DefaultScale;
            public float  DefaultLifetime;
            public bool   Recolorable;
            public bool   IsLoop;
            public Pick(string path, int poolSize = 6, float scale = 1f, float lifetime = 0f,
                        bool recolorable = true, bool isLoop = false)
            {
                Path = path; PoolSize = poolSize; DefaultScale = scale;
                DefaultLifetime = lifetime; Recolorable = recolorable; IsLoop = isLoop;
            }
        }

        private const string AAA   = "Assets/Hovl Studio/AAA Projectiles Vol 1/Prefabs/";
        private const string RPG    = "Assets/Hovl Studio/RPG VFX Bundle/Random effect prefabs/";
        private const string AOE    = "Assets/Hovl Studio/AOE Magic spells Vol.1/Prefabs/";
        private const string MAGIC  = "Assets/Hovl Studio/Magic circles/Prefabs/";

        // -- Curated shortlist: key -> {prefab path, pool, scale, lifetime, recolor, loop} --
        // Exact paths verified against Docs/VFX/HovlStudio_Inventory.md §5. Owner re-points
        // any line and re-runs. Projectile-loops are LOOP (play until impact); casts/impacts/
        // explosions are ONESHOT; the collector FULL glow is a LOOP glow aura.
        private static readonly Dictionary<string, Pick> Map = new Dictionary<string, Pick>
        {
            // ── Fireball triplet (cast + fly + impact) ────────────────────────
            { "Fireball_Projectile",    new Pick(AAA + "Projectile VFX loop/Projectile 16 fire.prefab", isLoop: true) },
            { "Fireball_Cast",          new Pick(AAA + "Flash and hits/Flash 16 fire.prefab") },
            { "Fireball_Impact",        new Pick(AAA + "Flash and hits/Hit 16 fire.prefab") },

            // ── Thunderbolt (fly + impact) ────────────────────────────────────
            { "Thunderbolt_Projectile", new Pick(AAA + "Projectile VFX loop/Projectile 2 electro.prefab", isLoop: true) },
            { "Thunderbolt_Impact",     new Pick(AAA + "Flash and hits/Hit 2 electro.prefab") },

            // ── Arcane triplet (cast + fly + impact) ──────────────────────────
            { "Arcane_Projectile",      new Pick(AAA + "Projectile VFX loop/Projectile 17 nova violet.prefab", isLoop: true) },
            { "Arcane_Cast",            new Pick(AAA + "Flash and hits/Flash 17 nova violet.prefab") },
            { "Arcane_Impact",          new Pick(AAA + "Flash and hits/Hit 17 nova violet.prefab") },

            // ── Frost (fly + impact) ──────────────────────────────────────────
            { "Frost_Projectile",       new Pick(AAA + "Projectile VFX loop/Projectile 26 blue diamond.prefab", isLoop: true) },
            { "Frost_Impact",           new Pick(AAA + "Flash and hits/Hit 26 blue crystal.prefab") },

            // ── Economy / raid / celebration ──────────────────────────────────
            // Collector FULL glow — a looping gold glow the collector-fill view can
            // upgrade to (from VFXType.LevelUp_Celebration). Recolourable off = keep gold.
            { "Collector_Full",         new Pick(RPG + "Gold dot.prefab", poolSize: 4, recolorable: false, isLoop: true) },
            { "Raid_Explosion",         new Pick(AOE + "Meteor hit.prefab", scale: 1.5f) },
            { "LevelUp_Burst",          new Pick(RPG + "Lvl up.prefab", poolSize: 4) },

            // ═══ WO-VFX-003: Knight skill-tree actives — the 13 keys the 16 actives need ═══
            // beyond the WO-002 triplets above. Mapping + tints: Docs/VFX/SkillTree_VFX_Mapping.md.
            // Recolour per element at call time (HDR StartColor); owner is colorblind so heal/shield
            // read by SHAPE + MOTION, not hue.

            // ── Lightning cast (Thunderbolt uses WO-002's Thunderbolt_Projectile/_Impact) ──
            { "Thunderbolt_Cast",       new Pick(AAA + "Flash and hits/Flash 2 electro.prefab") },

            // ── Thrown spear (Throwing Spear / Snare Arrow) — physical arrow family 11 ──
            { "Spear_Projectile",       new Pick(AAA + "Projectile VFX loop/Projectile 11 orange arrow.prefab", isLoop: true) },
            { "Spear_Impact",           new Pick(AAA + "Flash and hits/Hit 11 orange arrow.prefab") },

            // ── Knight melee slash + close-hit (Shield Bash / Warden's Roar strike / basic slash) ──
            { "Melee_Slash",            new Pick(AOE + "Flower slash.prefab") },
            { "Melee_Impact",           new Pick(RPG + "Punch Hit.prefab") },

            // ── Cleave / ground-slam blast (Sweeping Cut / Champion's Combo / Suppressing Volley) ──
            { "Cleave_Impact",          new Pick(AOE + "Energy explosion.prefab", scale: 1.3f) },

            // ── Heal (Mending Salve / Second Wind / Oathmend HoT / universal Mend) ──
            { "Heal_Cast",              new Pick(MAGIC + "Magic circle sun.prefab", recolorable: false) },
            { "Heal_Aura",              new Pick(RPG + "Buff heal.prefab", recolorable: false, isLoop: true) },

            // ── Healing Fountain aura (owner 2026-07-10) — a looping buff aura the fountain holds
            //    while regenerating the Tree of Life out of battle. Recolourable ON so the fountain's
            //    HDR gold tint (GoldAura) applies; loop until the heal gate closes. Reads by MOTION +
            //    LUMINANCE (rising aura), not hue (owner colorblind). HealingFountain.cs AuraKey. ──
            { "Fountain_Heal_Aura",     new Pick(RPG + "Buff heal.prefab", recolorable: true, isLoop: true) },

            // ── Taunt (Warden's Roar) — outward roar shock + a held ground aura ──
            { "Taunt_Roar",             new Pick(AOE + "Energy explosion.prefab") },
            { "Taunt_Aura",             new Pick(MAGIC + "Loop version/Magic circle blood loop.prefab", isLoop: true) },

            // ── Eternal Aegis (invuln) — shield cast + a looping shield bubble on the hero ──
            { "Aegis_Cast",             new Pick(MAGIC + "Magic shield holy.prefab", recolorable: false) },
            { "Aegis_Shield",           new Pick(MAGIC + "Loop version/Magic shield holy loop.prefab", recolorable: false, isLoop: true) },

            // ── Emberbrand Throw residual — a burn/curse DoT aura on the struck foe ──
            { "Ember_Burn",             new Pick(RPG + "Debuff 1.prefab", isLoop: true) },

            // ── Universal Dash (blink) — a quick swirl at the blink origin ──
            { "Dash_Blink",             new Pick(RPG + "Buff white twist.prefab") },
        };

        [MenuItem("Defenders/VFX/Generate Hovl VFX Catalog")]
        public static void Generate()
        {
            try
            {
                int wired = Build();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"[HovlVfxCatalogGenerator] Wired {wired} keys into {AssetPath}.");
                Debug.Log(Marker);
            }
            catch (Exception e)
            {
                Debug.LogError($"[HovlVfxCatalogGenerator] FAILED: {e.Message}\n{e.StackTrace}");
                // No marker on failure — the gate withholds HOVL_VFX_CATALOG_OK.
            }
        }

        private static int Build()
        {
            var catalogType = Type.GetType(CatalogTypeName);
            if (catalogType == null)
                throw new Exception($"Could not resolve type '{CatalogTypeName}'. Is DeNelle.Village compiled?");

            EnsureDir(AssetDir);

            var catalog = AssetDatabase.LoadAssetAtPath(AssetPath, catalogType) as ScriptableObject;
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance(catalogType);
                AssetDatabase.CreateAsset(catalog, AssetPath);
            }

            // Resolve rows from the curated map. Missing prefabs are skipped (never
            // hard-fail on an un-imported optional pack).
            var rows = new List<(string key, GameObject prefab, Pick pick)>();
            int skippedMissing = 0;
            foreach (var kv in Map)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(kv.Value.Path);
                if (prefab == null)
                {
                    Debug.LogWarning($"[HovlVfxCatalogGenerator] prefab missing for '{kv.Key}': " +
                                     $"'{kv.Value.Path}' — key skipped (will no-op at call time).");
                    skippedMissing++;
                    continue;
                }
                rows.Add((kv.Key, prefab, kv.Value));
            }

            var so = new SerializedObject(catalog);
            var entries = so.FindProperty("Rows");
            if (entries == null)
                throw new Exception("HovlVfxCatalog has no serialized 'Rows' array property.");

            entries.arraySize = rows.Count;
            for (int i = 0; i < rows.Count; i++)
            {
                var (key, prefab, pick) = rows[i];
                var e = entries.GetArrayElementAtIndex(i);

                var pKey   = e.FindPropertyRelative("Key");
                var pPref  = e.FindPropertyRelative("Prefab");
                var pPool  = e.FindPropertyRelative("PoolSize");
                var pScale = e.FindPropertyRelative("DefaultScale");
                var pLife  = e.FindPropertyRelative("DefaultLifetime");
                var pRecol = e.FindPropertyRelative("Recolorable");
                var pLoop  = e.FindPropertyRelative("IsLoop");

                if (pKey   != null) pKey.stringValue           = key;
                if (pPref  != null) pPref.objectReferenceValue = prefab;
                if (pPool  != null) pPool.intValue             = pick.PoolSize;
                if (pScale != null) pScale.floatValue          = pick.DefaultScale;
                if (pLife  != null) pLife.floatValue           = pick.DefaultLifetime;
                if (pRecol != null) pRecol.boolValue           = pick.Recolorable;
                if (pLoop  != null) pLoop.boolValue            = pick.IsLoop;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);

            Debug.Log($"[HovlVfxCatalogGenerator] {rows.Count} wired, {skippedMissing} skipped (missing prefab).");
            return rows.Count;
        }

        private static void EnsureDir(string dir)
        {
            if (AssetDatabase.IsValidFolder(dir)) return;
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
