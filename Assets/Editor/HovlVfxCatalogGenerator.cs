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

        /// <summary>Owner-authored manual picks overlay (VfxCasterWindow "Tag &amp; Catalog").
        /// Merged AFTER the built-in Map on regenerate — manual rows are CANON and win
        /// on key collision (same law as MotionCastings manual:true rows).</summary>
        public const string ManualPicksPath = "Assets/Editor/VfxManualPicks.json";

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
        private const string MAP    = "Assets/Hovl Studio/Map track markers VFX/Prefabs/";

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
            // Arcane TOWER ambient aura (owner 2026-07-15 "arcane towers should have an aura") — a
            // looping magic circle held at the spire base. Reads by MOTION + LUMINANCE (a slow
            // rotating rune ring), colorblind-safe; the violet tint is only a hint. Same prefab as
            // Poi_NodeAura (known-imported), recolorable ON so ArcaneAura's HDR violet applies. Loop
            // -> PlayKey returns a VFXHandle ArcaneAura.cs Stop()s on destroy.
            { "Arcane_Aura",            new Pick(MAGIC + "Loop version/Magic circle sun loop.prefab", poolSize: 3, recolorable: true, isLoop: true) },
            // Dungeon-ENTRANCE portal gateway (owner felt-test 2026-07-15 "the dungeon
            // portal arch looks plain -- can creative make it magical?"). A looping magic
            // circle laid as a glowing rune ring at the overworld arch base so the entrance
            // reads as an ACTIVE arcane gateway ("step here, it's magical"). Same URP-clean,
            // proven-imported Hovl magic-circle prefab the Arcane_Aura / Poi_NodeAura keys
            // use (guaranteed present). Recolorable ON so DungeonWorldPortalSpawner's HDR
            // arcane-violet tint applies; loop -> PlayKey returns a VFXHandle the spawner
            // holds. COLORBLIND-SAFE (owner red/green): reads by MOTION + LUMINANCE (a slow
            // rotating rune ring), the violet is only a hint. Attached in DungeonWorldPortalSpawner.
            { "Dungeon_Portal_Gate",    new Pick(MAGIC + "Loop version/Magic circle sun loop.prefab", poolSize: 3, recolorable: true, isLoop: true) },
            // Heart-of-Elarion + founding-Echo ambient AURA (owner 2026-07-16 "the aura on the
            // tree/echo renders as ugly white squares"). VFXType.Aura_HeartPulse BRIDGES to this
            // key (VFXManager._hovlKeyForType), so BOTH HeartAuraController (the tree nucleus) and
            // EchoSpiritPresentation (the floating spirit) render this REAL soft glow loop instead of
            // the textureless procedural billboard-square fallback. "Buff white twist" is the
            // documented companion-ambient aura (Docs/VFX/HovlStudio_Inventory.md #25) -- a soft
            // NEUTRAL-WHITE VOLUMETRIC glow (not a flat ground rune ring), so it fits an airborne
            // nucleus better than the Magic-circle loop. COLORBLIND-SAFE (owner red/green): reads by
            // MOTION + LUMINANCE (a slow rising glow), neutral white carries no color meaning (matches
            // HeartAuraController's fixed warm-white law). Recolorable ON but PlayAura passes no tint,
            // so it stays neutral. Loop -> PlayKey returns a VFXHandle each controller Stop()s.
            { "Aura_HeartPulse",        new Pick(RPG + "Buff white twist.prefab", poolSize: 4, recolorable: true, isLoop: true) },

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

            // ═══ WO-VFX-POI: point-of-interest CALLOUTS (owner red/green colorblind — these read
            // by MOTION / SHAPE / LUMINANCE / VERTICALITY, never hue) ═══
            // Near-field NODE aura — a looping high-luminance ground circle under a harvest node.
            // Recolour OFF (keep the bright neutral gold ring); small pool (only ~6 live at once,
            // capped by PoiCalloutSystem to the shared loop budget).
            { "Poi_NodeAura",           new Pick(MAGIC + "Loop version/Magic circle sun loop.prefab", poolSize: 6, recolorable: false, isLoop: true) },
            // Far-field ENEMY FORTRESS beacon — a TALL looping pillar/beam visible from range,
            // stands until the outpost is cleared. Verticality is the read (not hue). Scale up so it
            // towers over the fort silhouette.
            { "Poi_Landmark",           new Pick(MAP + "Marker 4 Pillar Loop.prefab", poolSize: 3, scale: 4f, recolorable: false, isLoop: true) },
        };

        // ── Manual picks overlay (owner tags from VfxCasterWindow) ────────────
        // JSON lookup table + thin interpreter: { "rows": [ { key, prefabPath,
        // isLoop, scale, manual } ] }. Read is guarded — a missing/bad file warns
        // and yields zero rows, never throws (regenerate must not die on it).

        /// <summary>One owner-tagged catalog row. manual:true = CANON — the merge
        /// lets it beat the built-in Map on key collision.</summary>
        [Serializable]
        public class ManualPickRow
        {
            public string key;
            public string prefabPath;
            public bool   isLoop;
            public float  scale = 1f;
            public bool   manual = true;
        }

        [Serializable]
        private class ManualPicksFile
        {
            public List<ManualPickRow> rows = new List<ManualPickRow>();
        }

        /// <summary>Load the manual overlay rows. Missing file = empty list (fine);
        /// unreadable/garbled file = warn + empty list (never throws).</summary>
        public static List<ManualPickRow> ReadManualPicks()
        {
            try
            {
                if (!System.IO.File.Exists(ManualPicksPath))
                    return new List<ManualPickRow>();
                string json = System.IO.File.ReadAllText(ManualPicksPath);
                var file = JsonUtility.FromJson<ManualPicksFile>(json);
                if (file == null || file.rows == null)
                {
                    Debug.LogWarning($"[HovlVfxCatalogGenerator] '{ManualPicksPath}' parsed to no rows — " +
                                     "overlay skipped (fix or delete the file).");
                    return new List<ManualPickRow>();
                }
                return file.rows;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HovlVfxCatalogGenerator] could not read '{ManualPicksPath}': {e.Message} — " +
                                 "overlay skipped this regenerate.");
                return new List<ManualPickRow>();
            }
        }

        /// <summary>Write/update one manual row (keyed replace, else append) and save
        /// the overlay JSON. Returns false (with a warning) on any IO failure.</summary>
        public static bool WriteManualPick(ManualPickRow row)
        {
            if (row == null || string.IsNullOrEmpty(row.key) || string.IsNullOrEmpty(row.prefabPath))
            {
                Debug.LogWarning("[HovlVfxCatalogGenerator] WriteManualPick: empty key or prefabPath — not saved.");
                return false;
            }
            try
            {
                var file = new ManualPicksFile { rows = ReadManualPicks() };
                int existing = file.rows.FindIndex(r =>
                    string.Equals(r.key, row.key, StringComparison.OrdinalIgnoreCase));
                if (existing >= 0) file.rows[existing] = row;
                else file.rows.Add(row);
                file.rows.Sort((a, b) => string.CompareOrdinal(a.key, b.key));
                System.IO.File.WriteAllText(ManualPicksPath, JsonUtility.ToJson(file, prettyPrint: true));
                AssetDatabase.ImportAsset(ManualPicksPath);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HovlVfxCatalogGenerator] could not write '{ManualPicksPath}': {e.Message}");
                return false;
            }
        }

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

            // Merge the owner's manual overlay AFTER the built-in picks. Manual is
            // CANON: on key collision the manual row replaces the Map row. Missing
            // prefabs warn + skip, same policy as above.
            int manualWired = 0;
            foreach (var m in ReadManualPicks())
            {
                if (string.IsNullOrEmpty(m.key) || string.IsNullOrEmpty(m.prefabPath))
                {
                    Debug.LogWarning("[HovlVfxCatalogGenerator] manual overlay row with empty " +
                                     "key/prefabPath — skipped.");
                    continue;
                }
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(m.prefabPath);
                if (prefab == null)
                {
                    Debug.LogWarning($"[HovlVfxCatalogGenerator] manual prefab missing for '{m.key}': " +
                                     $"'{m.prefabPath}' — key skipped (will no-op at call time).");
                    skippedMissing++;
                    continue;
                }
                string mKey = m.key;
                rows.RemoveAll(r => string.Equals(r.key, mKey, StringComparison.OrdinalIgnoreCase));
                rows.Add((m.key, prefab, new Pick(m.prefabPath, scale: m.scale, isLoop: m.isLoop)));
                manualWired++;
            }
            if (manualWired > 0)
                Debug.Log($"[HovlVfxCatalogGenerator] merged {manualWired} manual overlay rows " +
                          $"from '{ManualPicksPath}' (manual wins on collision).");

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
