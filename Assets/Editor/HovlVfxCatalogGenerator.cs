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

        // WO-892: TRACKED Particle Pack mirrors, authored by ParticlePackVfxBatchBuilder
        // (Defenders/VFX/Build Particle Pack VFX Batch). Not Hovl art - and that is the
        // point. Every other constant above resolves into /Assets/Hovl Studio/, which is
        // GITIGNORED (.gitignore:218) with ZERO files tracked, so those rows only render on
        // a machine that happens to have the 236 MB pack on disk. This catalog is a
        // STRING-KEY table, not a Hovl-only table: a key may point at any committed prefab,
        // and the structure damage tells - which are gameplay-critical, not dressing - now
        // do, exactly as the death ladder was moved onto tracked art by WO-886.
        private const string DAMAGE = "Assets/Resources/VFX/Damage/";

        // Tracked aura mirrors (same not-Hovl-and-that-is-the-point law as DAMAGE above).
        private const string AURA   = "Assets/Resources/VFX/Aura/";

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
            // Arcane TOWER ambient aura (owner 2026-07-15 "arcane towers should have an aura").
            // ⚠ KEY WITHHELD - OWNER BAN (2026-08-16, verbatim): "Assets\Hovl Studio\Magic circles\
            // Prefabs\Loop version\Magic circle sun loop.prefab" - "remove". The old pick was that
            // sun loop; owner ban 2026-08-16; replacement awaits an owner tag - do NOT substitute
            // (memory: vfx-map-owner-tags-no-creative-pick). With no Map row the key simply never
            // reaches the catalog, and VFXManager.PlayKey("Arcane_Aura") degrades to the throttled
            // hovl-nokey FlowTrace no-op that ArcaneAura.cs is documented to tolerate (its header:
            // "PlayKey no-ops ... the aura simply appears once the catalog row is authored").
            // Enforced by BannedVfxRegression (BANNED_VFX_OK/FAIL).
            //   { "Arcane_Aura",  <withheld - awaiting owner tag> },
            // Dungeon-ENTRANCE portal gateway (owner felt-test 2026-07-15 "the dungeon
            // portal arch looks plain -- can creative make it magical?"). A magic circle laid
            // at the overworld arch base so the entrance reads as an ACTIVE arcane gateway.
            // REPOINTED 2026-08-16: the old pick was the now-banned sun loop (owner ban verbatim
            // above); the owner tagged, same day, verbatim: "Magic circle dark star.prefab - use
            // this rotated for the portals" - so this key is the DARK STAR circle (the rotation is
            // the portal-face presentation lane's wiring, not a catalog concern). Recolorable ON so
            // the HDR arcane-violet tint applies. IsLoop below is only the fallback literal - the
            // generator DERIVES the real flag from the prefab at build time (see the pLoop block).
            { "Dungeon_Portal_Gate",    new Pick(MAGIC + "Magic circle dark star.prefab", poolSize: 3, recolorable: true, isLoop: true) },
            // Heart-of-Elarion ambient AURA (owner 2026-07-16 "the aura on the tree/echo renders
            // as ugly white squares"). VFXType.Aura_HeartPulse BRIDGES to this key
            // (VFXManager._hovlKeyForType), so HeartAuraController (the tree nucleus) renders this
            // REAL soft glow loop instead of the textureless procedural billboard-square fallback.
            // WO-993 (2026-08-16): the founding-Echo half of that sentence is retired with
            // EchoSpiritPresentation -- the guide is a grounded wolf now, not a floating spirit.
            // The KEY and the Heart's use of it are UNCHANGED. "Buff white twist" is the
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
            // ⚠ WO-892 FOUND THIS ROW DEAD, and deliberately did NOT repair it. There is no
            // "Debuff 1.prefab" in that folder - the pack ships "Debuff chain.prefab" and
            // "Debuff scythe.prefab". Build() skips a row whose prefab will not load, so this
            // key has never reached HovlVfxCatalog.asset and PlayKey("Ember_Burn") has always
            // been a throttled no-op. Two consumers were silently dark: StructureDamageVisuals'
            // smolder + fire loops (fixed below - they now use the tracked Damage_* mirrors)
            // and abilities.json knight.emberbrand-throw's "vfxResidual", which is STILL dark.
            // Naming the replacement prefab is an owner pick (memory: the owner tags VFX keys,
            // the CLI maps them verbatim and never substitutes), so the line is left as-is with
            // the finding attached rather than quietly re-pointed at whichever Debuff looks close.
            { "Ember_Burn",             new Pick(RPG + "Debuff 1.prefab", isLoop: true) },

            // ═══ WO-892: STRUCTURE DAMAGE STATES (registry §6g) ═══════════════════
            // Consumed by StructureDamageVisuals (WO-672) - the ONE structure-damage
            // presentation observer, re-skinned by WO-892 rather than rewritten. Every prefab
            // is a TRACKED Particle Pack mirror under Assets/Resources/VFX/Damage/, so a fresh
            // clone renders the damage states; the keys they replace (Ember_Burn, dead per the
            // note above, and Raid_Explosion, gitignored Hovl art) did not.
            //
            // COLOURBLIND LAW (owner is red/green): the four states are separated by SMOKE
            // DENSITY, FLAME PRESENCE, PULSE RHYTHM and LAYER COUNT - all of which survive
            // greyscale. Nothing here is distinguished by hue. Recolorable is OFF on all five
            // for the same reason: a caller tinting one of these would be adding a channel the
            // owner cannot read, over one she can.
            //
            // IsLoop below is what each recipe MEASURES as (per-layer numbers in the builder's
            // run log). It is not taken on faith either way: VfxLoopFlagRegression re-derives
            // it from the prefab and FAILS the gate if a stored flag disagrees.
            { "Damage_Smolder",         new Pick(DAMAGE + "Damage_Smolder.prefab",
                                                 poolSize: 4, recolorable: false, isLoop: true) },
            { "Damage_Fire",            new Pick(DAMAGE + "Damage_Fire.prefab",
                                                 poolSize: 4, recolorable: false, isLoop: true) },
            // The critical-save beacon. poolSize matches maxCriticalBeacons in
            // damage-states.json - the observer will never hold more than that many at once,
            // so a larger pool would only pre-warm instances nothing can ask for.
            { "Damage_CriticalBeacon",  new Pick(DAMAGE + "Damage_CriticalBeacon.prefab",
                                                 poolSize: 3, recolorable: false, isLoop: true) },
            // The break one-shot. isLoop FALSE and that is load-bearing: a fire-and-forget
            // play of a loop-flagged key never returns its slot, and a wave can break several
            // structures inside a few seconds.
            { "Damage_BreakBurst",      new Pick(DAMAGE + "Damage_BreakBurst.prefab",
                                                 poolSize: 3, recolorable: false, isLoop: false) },
            { "Damage_Ruin",            new Pick(DAMAGE + "Damage_Ruin.prefab",
                                                 poolSize: 3, recolorable: false, isLoop: true) },

            // ── Universal Dash (blink) — a quick swirl at the blink origin ──
            { "Dash_Blink",             new Pick(RPG + "Buff white twist.prefab") },

            // ═══ WO-VFX-POI: point-of-interest CALLOUTS (owner red/green colorblind — these read
            // by MOTION / SHAPE / LUMINANCE / VERTICALITY, never hue) ═══
            // Near-field NODE aura — a looping ground aura under a harvest node. Small pool
            // (only ~6 live at once, capped by PoiCalloutSystem to the shared loop budget).
            // REPOINTED 2026-08-16: the old pick was the now-banned "sun loop" magic circle
            // (owner ban verbatim at the Arcane_Aura note above). The owner tagged, same day,
            // verbatim: "Aura_PetLevel2 -> Node Auras" - and this key IS the node aura, so it
            // (that prefab was RENAMED to Aura_TalentNode on 2026-08-20: it was never a pet
            //  aura, and the pet-aura feature it was named for never shipped)
            // now points at the TRACKED Resources mirror (fresh-clone safe, unlike the
            // gitignored Hovl art it replaces). Recolour OFF (keep the authored look).
            { "Poi_NodeAura",           new Pick(AURA + "Aura_TalentNode.prefab", poolSize: 6, recolorable: false, isLoop: true) },
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
                // A pack path with a COMMITTED tracked mirror resolves to the mirror.
                // Derived here for the same reason IsLoop is (see below): a redirect
                // applied to the .asset by hand survives exactly one regenerate.
                string mapPath = ResolveMirror(kv.Key, kv.Value.Path);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(mapPath);
                if (prefab == null)
                {
                    Debug.LogWarning($"[HovlVfxCatalogGenerator] prefab missing for '{kv.Key}': " +
                                     $"'{mapPath}' — key skipped (will no-op at call time).");
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
                // The overlay records the path the owner BROWSED in the VFX Caster, which
                // is the pack path. Redirecting it to the committed byte-copy of that same
                // prefab is not a substitution of her pick — it is the only copy of her
                // pick that renders on a machine without the pack. VfxMirrorRedirect.
                string manualPath = ResolveMirror(m.key, m.prefabPath);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(manualPath);
                if (prefab == null)
                {
                    Debug.LogWarning($"[HovlVfxCatalogGenerator] manual prefab missing for '{m.key}': " +
                                     $"'{manualPath}' — key skipped (will no-op at call time).");
                    skippedMissing++;
                    continue;
                }
                string mKey = m.key;
                rows.RemoveAll(r => string.Equals(r.key, mKey, StringComparison.OrdinalIgnoreCase));
                rows.Add((m.key, prefab, new Pick(manualPath, scale: m.scale, isLoop: m.isLoop)));
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

                // IsLoop is DERIVED FROM THE PREFAB, never from the Map literal.
                //
                // WHY (2026-08-05): the Map's isLoop: argument is the same manual-truth
                // defect that made this a P0 one layer down. 15 of these entries declared
                // isLoop: true against rate-0 burst prefabs - including Poi_NodeAura and
                // Poi_Landmark, whose source files are literally named "...loop.prefab"
                // but emit a single burst at t=0. A burst row flagged as a loop NEVER
                // returns its slot (only VFXHandle.Stop releases one, and fire-and-forget
                // call sites discard the handle), so it permanently consumes one of the 20
                // global loop slots. Six captured F8 sessions show that cap saturated -
                // and Poi_NodeAura and Poi_Landmark appear in those captures as BOTH the
                // leakers and the starved.
                //
                // Deriving here means a corrected catalog cannot be silently undone the
                // next time someone regenerates, which is the only way the fix survives.
                // The Map literal remains as the fallback for a prefab that cannot be
                // read at all, and any disagreement is logged rather than swallowed.
                if (pLoop != null)
                {
                    bool derived;
                    string detail;
                    if (DeNelle.Editor.Regression.VfxLoopFlagRegression.TryResolveExpected(key, prefab, out derived, out detail))
                    {
                        if (derived != pick.IsLoop)
                            Debug.LogWarning($"[HovlVfxCatalogGenerator] '{key}' Map says isLoop:{pick.IsLoop} " +
                                             $"but the prefab derives {derived} - using the PREFAB. {detail}");
                        pLoop.boolValue = derived;
                    }
                    else
                    {
                        Debug.LogWarning($"[HovlVfxCatalogGenerator] '{key}' could not be derived ({detail}) " +
                                         $"- falling back to the Map literal isLoop:{pick.IsLoop}.");
                        pLoop.boolValue = pick.IsLoop;
                    }
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);

            Debug.Log($"[HovlVfxCatalogGenerator] {rows.Count} wired, {skippedMissing} skipped (missing prefab).");
            return rows.Count;
        }

        /// <summary>
        /// Route one pick path through VfxMirrorRedirect and SAY SO in the log when it
        /// moves. Silence here would hide the single most useful fact about a regenerate:
        /// which owner picks are shipping off tracked art and which are still pack-only.
        /// A gitignored pick with no mirror is warned about by name — that row is the
        /// next fresh-clone hole, and it is now visible instead of merely counted by the
        /// self-containment ratchet after the fact.
        /// </summary>
        private static string ResolveMirror(string key, string path)
        {
            string mirrored, detail;
            if (VfxMirrorRedirect.TryResolve(path, out mirrored, out detail))
            {
                Debug.Log($"[HovlVfxCatalogGenerator] '{key}' pack pick '{path}' -> tracked mirror " +
                          $"'{mirrored}' ({detail}).");
                return mirrored;
            }
            if (DeNelle.Editor.Regression.VfxResourceSelfContainmentRegression.IsInGitignoredArtRoot(path))
                Debug.LogWarning($"[HovlVfxCatalogGenerator] '{key}' stays on GITIGNORED art '{path}' — " +
                                 $"{detail}. On a fresh clone this key resolves to nothing.");
            return path;
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
