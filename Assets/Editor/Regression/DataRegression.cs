// =============================================================================
// DataRegression — headless "pass the real data object in, see the real response"
// regression harness. Owner directive 2026-06-13: instrument + run headless; this is
// the start of a robust regression script.
//
// Runs in batchmode (Unity closed) via:
//   run-unity-method.ps1 -Method DeNelle.Editor.DataRegression.RunAll -LogName data-regression.log
//
// It loads the REAL canonical catalogs through the SAME code path the game uses
// (GearCatalog -> CanonicalJson -> Newtonsoft), enumerates the resulting OBJECTS, and
// validates the response — so a silent JSON->object mapping break (wrong top-level key,
// renamed field, parse-to-empty) becomes a hard REGRESSION FAIL line instead of an
// empty store at runtime with no error. Prints a single authoritative marker:
//   REGRESSION_OK   (all checks passed)  /  REGRESSION_FAIL: <n> failure(s)
// =============================================================================
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using DeNelle.Village;
using DeNelle.Village.Arena;
using DeNelle.Village.Items;
using DeNelle.Core.State;
using DeNelle.Core.Catalog;

namespace DeNelle.Editor
{
    public static class DataRegression
    {
        public static void RunAll()
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("=== DataRegression: real catalog objects in, real response out ===");

            // --- GEAR (the active 'empty store' suspect) ---------------------------
            // Force a fresh read through the real loader (CanonicalJson, Resources-first).
            GearCatalog.Reload();

            var weapons = new List<WeaponDef>(GearCatalog.AllWeapons());
            var armors  = new List<ArmorDef>(GearCatalog.AllArmors());

            log.AppendLine($"weapons.json -> {weapons.Count} WeaponDef objects");
            log.AppendLine($"armor.json   -> {armors.Count} ArmorDef objects");

            // Response check 1: did the JSON map to objects AT ALL? (catches the silent
            // parse-to-empty: file present but top-level key / field names mismatch.)
            if (weapons.Count == 0) failures.Add("weapons.json deserialized to 0 objects (mapping break or empty 'weapons' array)");
            if (armors.Count == 0)  failures.Add("armor.json deserialized to 0 objects (mapping break or empty 'armor' array)");

            // Response check 2: did the DISPLAY fields populate? A row renders blank if
            // name/id came through null/empty even when the count is right. This is exactly
            // the 'rows exist but look empty' case the owner suspected.
            int badWeapon = 0, badArmor = 0;
            foreach (var w in weapons)
            {
                bool ok = w != null && !string.IsNullOrEmpty(w.id) && !string.IsNullOrEmpty(w.name);
                if (!ok) badWeapon++;
                log.AppendLine($"  W {(w != null ? w.id : "<null>")} | name='{(w != null ? w.name : "<null>")}' " +
                               $"| dmg={(w != null ? w.damageMult : 0f):0.00} | cost={CostStr(GearCatalog.GetBuyCost(w))}");
            }
            foreach (var a in armors)
            {
                bool ok = a != null && !string.IsNullOrEmpty(a.id) && !string.IsNullOrEmpty(a.name);
                if (!ok) badArmor++;
                log.AppendLine($"  A {(a != null ? a.id : "<null>")} | name='{(a != null ? a.name : "<null>")}' " +
                               $"| def={(a != null ? a.defense : 0f):0.00} | cost={CostStr(GearCatalog.GetBuyCost(a))}");
            }
            if (badWeapon > 0) failures.Add($"{badWeapon} weapon(s) have null/empty id or name (would render as blank rows)");
            if (badArmor  > 0) failures.Add($"{badArmor} armor(s) have null/empty id or name (would render as blank rows)");

            // Response check 3: store would have NON-EMPTY stock for a general vendor.
            int generalStock = weapons.Count + armors.Count;
            if (generalStock == 0) failures.Add("general vendor stock is EMPTY (no weapons + no armors)");
            else log.AppendLine($"general vendor stock = {generalStock} gear rows (+ potions added at runtime)");

            // --- ABILITIES (abilities.json -> AbilityCatalog) ----------------------
            // Same shape as the gear checks: load through the REAL loader, assert the
            // JSON mapped to objects and every entry's DISPLAY fields populated. There
            // is no Resources PATH to resolve here on purpose: AbilityDef.Icon is a HUD
            // GLYPH (e.g. "✦"), NOT a Resources path (see AbilityCatalog.cs Icon doc +
            // HeroAbilities), and Color is a hex string — neither is Resources.Load'able,
            // so asserting a path on them would INVENT an expectation. We validate only
            // what the catalog actually declares.
            CheckAbilities(failures, log);

            // --- ENEMIES (enemies.json -> EnemyCatalog) ----------------------------
            // This is the catalog that carries the #22 archer->lumber CLASS of bug: an
            // entry's id resolves (via EnemyFactory.ModelForEnemy) to a MODEL PATH, and
            // a wrong/missing path silently degrades to a tinted capsule at runtime
            // (EnemyFactory.cs:100-114 fallback) — varied ids, one look, no error. We
            // load the catalog through the same CanonicalJson bytes WaveDataLoader reads,
            // then for EVERY enemy resolve its model the way the factory does and assert
            // Resources.Load<GameObject>("Enemies/<model>") returns a real prefab.
            CheckEnemies(failures, log);

            // --- STRUCTURES (structures-catalog.json -> CatalogRegistry) -----------
            // The build-mode tower/structure catalog. Each CatalogEntry.visualPrefabPath
            // is a Resources-relative prefab path that StructureFactory.Create feeds to
            // VisualFactory.Skin -> Resources.Load<GameObject>. A path that loads null is
            // EXACTLY the archer->lumber class (a tower wired to the wrong/missing visual)
            // — caught here as a FAIL naming the entry + path. Parsed identically to the
            // real CatalogBootstrap.LoadFromJson (StringEnumConverter + ignore-null/miss).
            CheckStructures(failures, log);

            // --- BUILDINGS (buildings.json -> BuildingCatalog) ---------------------
            // Load through the real loader; assert non-zero + non-empty id/displayName.
            // NOTE (conservative): BuildingDef.Model is a KayKit mesh KEY, NOT a
            // Resources path — gameplay buildings render through the structures catalog /
            // build pipeline, never via Resources.Load(Model). So we do NOT assert a path
            // load on Model (that would invent an expectation the catalog doesn't declare).
            CheckBuildings(failures, log);

            // --- ITEM-MODEL CAPABILITY INVARIANTS (WO-Item-1, docs/ITEM_MODEL.md §2c) ---
            // OWNER-RATIFIED 2026-06-18: the model invariants live in the regression test,
            // not just the doc — so every change/regen is gated by data, not faith. HARD
            // asserts on the resolved capability flags + a SOFT prefabPath coverage count
            // (WO-Item-2's generator fills those — do NOT fail on them yet).
            CheckItemCapabilities(weapons, armors, failures, log);

            // --- ARMED-HERO INVARIANT (WO-Item Addressables equip) -----------------
            // At scale (433+ weapons, Blink Addressable-keyed) BestWeapon(job,1) may now
            // return a weapon whose prefab is an Addressable key. If neither the Addressable
            // key resolves NOR the EquipmentController's Resources map yields an attachable
            // mesh, the hero spawns UNARMED (WO-425 regression). This is the permission gate
            // that the armed-hero invariant holds at scale: for each class the level-1 auto-
            // equip is non-null AND its prefab reference resolves.
            CheckArmedHeroInvariant(failures, log);

            // --- HAND-SLOT EQUIP RULES (owner 2026-06-18, docs/STORE_EQUIP_SPEC.md) -
            // Drive the REAL GearLoadout equip flow on a throwaway GameObject and assert the
            // mutually-exclusive main-hand/off-hand rules hold: a 2H clears the off-hand; an
            // off-hand clears a 2H main; a 1H + shield coexist; the swap never leaves the hero
            // unarmed when a 1H exists. Exercises the actual enforcement, not a re-derivation.
            CheckHandSlotRules(failures, log);

            // --- BATTLE CLOSING (WO-505) — victory/defeat audio + star rating ------
            // Two provable bones from the silent-climax gap: (a) the victory + defeat
            // music clips resolve to a NON-NULL AudioClip through the SAME Resources path
            // AudioBootstrap uses (Resources.Load<AudioClip>("victory"/"defeat")) — this
            // catches the silent-track bug class (e.g. Resources.Load("dungeon") == null);
            // (b) BattleStarRating computes the right tier + multiplier for sample durations.
            CheckBattleClosing(failures, log);

            // --- WEAPON SWING-TRAIL VFX (WO-504 slice 3) ---------------------------
            // The Knight swings one shared mesh, so the rarity must read through the
            // swing-trail color/width. Assert the pure WeaponVfxMap resolver returns a
            // DISTINCT color per band, the gold const at legendary, the steel default
            // for null, and a MONOTONICALLY escalating width. Bones — owner felt-tunes
            // the exact colors later; this gates the MAPPING, not the aesthetic.
            CheckWeaponVfx(failures, log);

            // --- verdict -----------------------------------------------------------
            log.AppendLine("=== verdict ===");
            if (failures.Count == 0)
            {
                log.AppendLine("REGRESSION_OK");
                Debug.Log(log.ToString());
            }
            else
            {
                log.AppendLine($"REGRESSION_FAIL: {failures.Count} failure(s):");
                foreach (var f in failures) log.AppendLine("  - " + f);
                // LogError so it also lands in break-log.jsonl and fails loudly in the log scan.
                Debug.LogError(log.ToString());
            }
        }

        // =====================================================================
        //  ABILITIES — abilities.json via AbilityCatalog (the real loader)
        // =====================================================================
        private static void CheckAbilities(List<string> failures, StringBuilder log)
        {
            AbilityCatalog.Reload();

            // Enumerate every class loadout the catalog exposes. We probe the known
            // hero classes (the catalog is keyed by lowercase class id); the default
            // 'mage' is the v2-foundation class that MUST be present.
            string[] classes = { "mage", "knight", "ranger", "cleric" };
            int total = 0;
            int bad = 0;
            foreach (var cls in classes)
            {
                var loadout = AbilityCatalog.GetLoadout(cls);
                if (loadout == null || loadout.Count == 0) continue;   // class simply not authored yet
                foreach (var ab in loadout)
                {
                    total++;
                    bool ok = ab != null
                              && !string.IsNullOrEmpty(ab.Slot)
                              && !string.IsNullOrEmpty(ab.Name);
                    if (!ok) bad++;
                    log.AppendLine($"  AB [{cls}] slot='{(ab != null ? ab.Slot : "<null>")}' " +
                                   $"name='{(ab != null ? ab.Name : "<null>")}' " +
                                   $"effect='{(ab != null ? ab.Effect : "<null>")}'");
                }
            }

            log.AppendLine($"abilities.json -> {total} AbilityDef object(s) across {classes.Length} probed class(es)");

            // The default 'mage' loadout is the proven v2 content: if it is empty, the
            // JSON->object mapping broke (wrong top-level key 'classes', renamed slots,
            // or a parse-to-empty) exactly like the gear case.
            if (AbilityCatalog.GetLoadout(AbilityCatalog.DefaultClass).Count == 0)
                failures.Add($"abilities.json: default class '{AbilityCatalog.DefaultClass}' has 0 abilities (mapping break or empty 'classes')");
            if (total == 0)
                failures.Add("abilities.json deserialized to 0 AbilityDef objects (mapping break or empty 'classes')");
            if (bad > 0)
                failures.Add($"{bad} ability(ies) have null/empty slot or name (would render blank on the hotbar)");
        }

        // =====================================================================
        //  ENEMIES — enemies.json via EnemyCatalog + the FACTORY model-path resolve
        // =====================================================================
        private static void CheckEnemies(List<string> failures, StringBuilder log)
        {
            // Load the catalog through the same WebGL-safe bytes WaveDataLoader reads
            // (its step 1 is CanonicalJson.Read; we deserialize the same way it does so
            // a schema/key break here is the SAME break the game would hit). The async
            // WaveDataLoader.LoadEnemiesAsync isn't awaitable in this sync harness, so we
            // mirror its exact parse: CanonicalJson.Read -> JsonConvert<EnemyCatalog>.
            string json = DeNelle.Core.CanonicalJson.Read(WaveDataLoader.EnemiesRelativePath);
            EnemyCatalog catalog = null;
            if (!string.IsNullOrEmpty(json))
            {
                try { catalog = JsonConvert.DeserializeObject<EnemyCatalog>(json); }
                catch (System.Exception ex)
                {
                    failures.Add($"enemies.json failed to parse: {ex.Message}");
                    log.AppendLine($"enemies.json -> PARSE ERROR: {ex.Message}");
                    return;
                }
            }

            if (catalog == null || catalog.Enemies == null || catalog.Enemies.Count == 0)
            {
                failures.Add("enemies.json deserialized to 0 EnemyDef objects (mapping break or empty 'enemies')");
                log.AppendLine("enemies.json -> 0 EnemyDef objects");
                return;
            }

            log.AppendLine($"enemies.json -> {catalog.Enemies.Count} EnemyDef object(s)");

            int badField = 0;
            foreach (var e in catalog.Enemies)
            {
                // Skip the schema-doc placeholder row (its id is the field description, not
                // a real enemy) — be conservative, don't fail on a documented non-entry.
                if (e != null && e.Id != null && e.Id.Contains(" ")) continue;

                bool ok = e != null && !string.IsNullOrEmpty(e.Id) && !string.IsNullOrEmpty(e.Name);
                if (!ok) { badField++; continue; }

                // PREFAB-PATH CHECK (catches the archer->lumber class). Resolve the model
                // EXACTLY as the single enemy-creation path does (EnemyFactory.ModelForEnemy),
                // then attempt the same Resources.Load<GameObject>("Enemies/<model>") the
                // factory's VisualFactory.Skin call performs. A null load means this enemy
                // ships as a tinted-capsule fallback at runtime — a silent regression.
                string model = EnemyFactory.ModelForEnemy(e);
                string path = "Enemies/" + model;
                var prefab = Resources.Load<GameObject>(path);
                if (prefab == null)
                {
                    failures.Add($"enemies.json: '{e.Id}' resolves to model '{model}' but Resources.Load<GameObject>(\"{path}\") is NULL (would spawn as a tinted capsule — wrong/missing prefab)");
                    log.AppendLine($"  EN {e.Id} -> model '{model}' | PREFAB MISSING at '{path}'");
                }
                else
                {
                    log.AppendLine($"  EN {e.Id} -> model '{model}' | prefab OK ('{path}')");
                }
            }
            if (badField > 0)
                failures.Add($"{badField} enemy(ies) have null/empty id or name");
        }

        // =====================================================================
        //  STRUCTURES — structures-catalog.json visualPrefabPath load + type check
        // =====================================================================
        [System.Serializable]
        private sealed class StructuresCatalogFile
        {
            [JsonProperty("version")] public int Version;
            [JsonProperty("entries")] public List<CatalogEntry> Entries = new List<CatalogEntry>();
        }

        private static void CheckStructures(List<string> failures, StringBuilder log)
        {
            // Parse identically to the production CatalogBootstrap.LoadFromJson so a
            // schema break shows up HERE the same way it would at startup.
            string json = DeNelle.Core.CanonicalJson.Read("Data/Canonical/structures-catalog.json");
            StructuresCatalogFile file = null;
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    var settings = new JsonSerializerSettings
                    {
                        Converters = { new StringEnumConverter() },
                        NullValueHandling = NullValueHandling.Ignore,
                        MissingMemberHandling = MissingMemberHandling.Ignore,
                    };
                    file = JsonConvert.DeserializeObject<StructuresCatalogFile>(json, settings);
                }
                catch (System.Exception ex)
                {
                    failures.Add($"structures-catalog.json failed to parse: {ex.Message}");
                    log.AppendLine($"structures-catalog.json -> PARSE ERROR: {ex.Message}");
                    return;
                }
            }

            if (file == null || file.Entries == null || file.Entries.Count == 0)
            {
                failures.Add("structures-catalog.json deserialized to 0 CatalogEntry objects (mapping break or empty 'entries')");
                log.AppendLine("structures-catalog.json -> 0 CatalogEntry objects");
                return;
            }

            log.AppendLine($"structures-catalog.json -> {file.Entries.Count} CatalogEntry object(s)");

            int badField = 0;
            foreach (var entry in file.Entries)
            {
                bool ok = entry != null && !string.IsNullOrEmpty(entry.id) && !string.IsNullOrEmpty(entry.displayName);
                if (!ok) { badField++; continue; }

                // Composites have no own mesh (they bundle cell entries) and a sparse
                // decoration row may legitimately omit visualPrefabPath — only assert the
                // ones the catalog ACTUALLY declares a path for (conservative).
                if (string.IsNullOrEmpty(entry.visualPrefabPath))
                {
                    log.AppendLine($"  ST {entry.id} | no visualPrefabPath (composite/decoration) — skipped");
                    continue;
                }

                // PREFAB-PATH CHECK: StructureFactory.Create -> VisualFactory.Skin does
                // Resources.Load<GameObject>(visualPrefabPath). A null load = the structure
                // builds with NO mesh (StructureFactory.cs:88-90 warning) — the archer->
                // lumber class for towers/structures.
                var prefab = Resources.Load<GameObject>(entry.visualPrefabPath);
                if (prefab == null)
                {
                    failures.Add($"structures-catalog.json: '{entry.id}' visualPrefabPath '{entry.visualPrefabPath}' loads NULL (structure would build with no mesh — wrong/missing prefab)");
                    log.AppendLine($"  ST {entry.id} -> '{entry.visualPrefabPath}' | PREFAB MISSING");
                }
                else
                {
                    log.AppendLine($"  ST {entry.id} -> '{entry.visualPrefabPath}' | prefab OK");
                }
            }
            if (badField > 0)
                failures.Add($"{badField} structure entry(ies) have null/empty id or displayName");
        }

        // =====================================================================
        //  BUILDINGS — buildings.json via BuildingCatalog (the real loader)
        // =====================================================================
        private static void CheckBuildings(List<string> failures, StringBuilder log)
        {
            BuildingCatalog.Reload();
            var buildings = new List<BuildingDef>(BuildingCatalog.Buildings);

            log.AppendLine($"buildings.json -> {buildings.Count} BuildingDef object(s)");
            if (buildings.Count == 0)
                failures.Add("buildings.json deserialized to 0 BuildingDef objects (mapping break or empty 'buildings')");

            int bad = 0;
            foreach (var b in buildings)
            {
                // displayName is a canon-strings KEY (not a literal) but must be non-empty
                // so the build menu can resolve a name; id is the build/cooldown key.
                bool ok = b != null && !string.IsNullOrEmpty(b.Id) && !string.IsNullOrEmpty(b.DisplayName);
                if (!ok) bad++;
                log.AppendLine($"  BD {(b != null ? b.Id : "<null>")} | displayName='{(b != null ? b.DisplayName : "<null>")}' " +
                               $"| model='{(b != null ? b.Model : "<null>")}' (mesh key, not a Resources path)");
            }
            if (bad > 0)
                failures.Add($"{bad} building(s) have null/empty id or displayName");
        }

        // =====================================================================
        //  ITEM-MODEL CAPABILITIES — WO-Item-1 invariants (docs/ITEM_MODEL.md §2c)
        // -----------------------------------------------------------------------
        //  HARD (fail REGRESSION_FAIL when violated):
        //   - every Weapon entry resolves Carriable|Equippable
        //   - every Armor/Gear entry resolves Carriable|Equippable
        //   - every Consumable entry resolves Carriable|Usable
        //   - NO entry resolves both Carriable and AI (an item is never an enemy)
        //  SOFT (report a coverage count, do NOT fail — WO-Item-2's generator fills
        //  prefabPath; failing now would block the additive foundation):
        //   - how many Carriable entries resolve a non-null prefabPath
        // =====================================================================
        private static void CheckItemCapabilities(
            List<WeaponDef> weapons, List<ArmorDef> armors,
            List<string> failures, StringBuilder log)
        {
            const ItemCapability EQUIP = ItemCapability.Carriable | ItemCapability.Equippable;
            const ItemCapability USE   = ItemCapability.Carriable | ItemCapability.Usable;

            // Load consumables through the same real loader the game uses.
            ConsumableCatalog.Reload();
            var consumables = new List<ConsumableDef>(ConsumableCatalog.All);
            log.AppendLine($"consumables.json -> {consumables.Count} ConsumableDef object(s)");

            int carriableTotal = 0;     // SOFT denominator
            int prefabResolved = 0;     // SOFT numerator (prefabPath non-null on a Carriable)

            // --- Weapons: must resolve Carriable|Equippable, never AI ---
            foreach (var w in weapons)
            {
                if (w == null) continue;
                var cap = w.Capabilities;
                if ((cap & EQUIP) != EQUIP)
                    failures.Add($"weapons.json: '{w.id}' resolves {cap} — must retain Carriable|Equippable");
                if ((cap & ItemCapability.Carriable) != 0 && (cap & ItemCapability.AI) != 0)
                    failures.Add($"weapons.json: '{w.id}' resolves BOTH Carriable and AI (an item is never an enemy)");
                if ((cap & ItemCapability.Carriable) != 0)
                {
                    carriableTotal++;
                    if (!string.IsNullOrEmpty(w.prefabPath)) prefabResolved++;
                }
            }

            // --- Armor/Gear: must resolve Carriable|Equippable, never AI ---
            foreach (var a in armors)
            {
                if (a == null) continue;
                var cap = a.Capabilities;
                if ((cap & EQUIP) != EQUIP)
                    failures.Add($"armor.json: '{a.id}' resolves {cap} — must retain Carriable|Equippable");
                if ((cap & ItemCapability.Carriable) != 0 && (cap & ItemCapability.AI) != 0)
                    failures.Add($"armor.json: '{a.id}' resolves BOTH Carriable and AI (an item is never an enemy)");
                if ((cap & ItemCapability.Carriable) != 0)
                {
                    carriableTotal++;
                    if (!string.IsNullOrEmpty(a.prefabPath)) prefabResolved++;
                }
            }

            // --- Consumables: must resolve Carriable|Usable, never AI ---
            foreach (var c in consumables)
            {
                if (c == null) continue;
                var cap = c.Capabilities;
                if ((cap & USE) != USE)
                    failures.Add($"consumables.json: '{c.Id}' resolves {cap} — must retain Carriable|Usable");
                if ((cap & ItemCapability.Carriable) != 0 && (cap & ItemCapability.AI) != 0)
                    failures.Add($"consumables.json: '{c.Id}' resolves BOTH Carriable and AI (an item is never an enemy)");
                if ((cap & ItemCapability.Carriable) != 0)
                {
                    carriableTotal++;
                    if (!string.IsNullOrEmpty(c.PrefabPath)) prefabResolved++;
                }
            }

            // SOFT coverage line — WO-Item-2's generator populates prefabPath; until then
            // 0/N is EXPECTED and must NOT fail (the foundation is additive, no behavior change).
            log.AppendLine($"[item-model] capability invariants checked on " +
                           $"{weapons.Count}W + {armors.Count}A + {consumables.Count}C entries");
            log.AppendLine($"[item-model] SOFT prefabPath coverage: {prefabResolved}/{carriableTotal} " +
                           $"Carriable entries resolve a non-null prefabPath (WO-Item-2 fills the rest)");
        }

        // =====================================================================
        //  ARMED-HERO INVARIANT — BestWeapon(job,1) non-null + prefab resolves
        // -----------------------------------------------------------------------
        //  For each playable class: the level-1 auto-equip MUST return a WeaponDef
        //  (never null → the hero would spawn unarmed), AND that def's prefab MUST
        //  resolve to something attachable:
        //    • Addressable def (loadVia=="addressable" / "gear/" prefabPath) → the key
        //      must be present in the Gear group (Addressables.LoadResourceLocations).
        //    • otherwise → the EquipmentController Resources map must yield a mesh
        //      (Resources.Load of "Heroes/Props/Weapons/<mesh>"). Resolve never returns
        //      null for a non-empty id, so this always yields a path; we assert the prop
        //      actually exists in Resources so the hero shows the real mesh, not just the
        //      tinted-primitive last-resort.
        //  HARD-fails REGRESSION_FAIL on a null pick or an unresolvable Addressable key.
        // =====================================================================
        private static void CheckArmedHeroInvariant(List<string> failures, StringBuilder log)
        {
            GearCatalog.Reload();
            string[] classes = { "knight", "mage", "ranger", "cleric" };
            log.AppendLine("[armed-hero] BestWeapon(job,1) resolves an attachable prefab per class:");

            foreach (var job in classes)
            {
                WeaponDef w = GearCatalog.BestWeapon(job, 1);
                if (w == null)
                {
                    failures.Add($"armed-hero: BestWeapon('{job}', 1) returned NULL — hero would spawn UNARMED");
                    log.AppendLine($"  AH [{job}] -> <null> | UNARMED");
                    continue;
                }

                if (EquipmentController.IsAddressableWeapon(w))
                {
                    // Blink Addressable weapon: the prefabPath must be a present key in the catalog.
                    bool keyPresent = AddressableKeyExists(w.prefabPath);
                    if (!keyPresent)
                    {
                        failures.Add($"armed-hero: BestWeapon('{job}', 1) = '{w.id}' is Addressable " +
                                     $"'{w.prefabPath}' but that key is NOT present in the Addressables " +
                                     "catalog (Gear group) — Blink prefab would fail to load");
                        log.AppendLine($"  AH [{job}] -> '{w.id}' | Addressable '{w.prefabPath}' | KEY MISSING");
                    }
                    else
                    {
                        log.AppendLine($"  AH [{job}] -> '{w.id}' | Addressable '{w.prefabPath}' | key OK");
                    }
                }
                else
                {
                    // Legacy/Tripo weapon: resolve the Resources mesh path the controller would load.
                    string path = EquipmentController.ResolveWeaponMeshResourcePath(w.id);
                    var prefab = string.IsNullOrEmpty(path) ? null : Resources.Load<GameObject>(path);
                    if (prefab == null)
                    {
                        failures.Add($"armed-hero: BestWeapon('{job}', 1) = '{w.id}' maps to Resources " +
                                     $"prop '{path ?? "<null>"}' which loads NULL — hero would show only the " +
                                     "tinted-primitive fallback (real weapon mesh missing from Resources)");
                        log.AppendLine($"  AH [{job}] -> '{w.id}' | Resources '{path}' | PROP MISSING (primitive fallback)");
                    }
                    else
                    {
                        log.AppendLine($"  AH [{job}] -> '{w.id}' | Resources '{path}' | prop OK");
                    }
                }
            }
        }

        // =====================================================================
        //  HAND-SLOT EQUIP RULES — main-hand / off-hand mutual exclusion
        // -----------------------------------------------------------------------
        //  Drives the REAL GearLoadout equip methods on a throwaway hero GO and asserts:
        //   1. equip 1H + shield -> BOTH slots filled (the allowed combo).
        //   2. equip 2H (over a 1H+shield) -> off-hand CLEARED (2H takes both hands).
        //   3. equip shield while a 2H is held -> 2H REMOVED, main falls back to a 1H
        //      (never left unarmed when a 1H exists — armed-hero invariant).
        //  Discovers test ids from the catalog (knight = the class with both a 1H and a 2H,
        //  shield = job 'any') so it stays valid as the catalog grows.
        // =====================================================================
        private static void CheckHandSlotRules(List<string> failures, StringBuilder log)
        {
            GearCatalog.Reload();
            log.AppendLine("[hand-slot] main-hand / off-hand mutual-exclusion rules:");

            const string Job = "knight";   // has BOTH a 1H main and a 2H in the catalog
            int level = 99;                // unlock everything for the test

            WeaponDef oneH   = GearCatalog.BestOneHandedWeapon(Job, level);
            WeaponDef twoH   = FindTwoHanded(Job, level);
            WeaponDef shield = FindShield(level);

            if (oneH == null)   { failures.Add("[hand-slot] no 1H weapon found for 'knight' — cannot test the rules"); return; }
            if (twoH == null)   { failures.Add("[hand-slot] no 2H weapon found for 'knight' — cannot test the rules"); return; }
            if (shield == null) { failures.Add("[hand-slot] no shield/off-hand item found in the catalog — cannot test the rules"); return; }

            log.AppendLine($"  test ids: 1H='{oneH.id}' 2H='{twoH.id}' shield='{shield.id}'");

            // Clear any persisted choices for this class so the test starts from a clean slate
            // and doesn't write durable state for the real game.
            string key = Job.ToLowerInvariant();
            PlayerPrefs.DeleteKey("dotr-equip-weapon-" + key);
            PlayerPrefs.DeleteKey("dotr-equip-offhand-" + key);
            PlayerPrefs.DeleteKey("dotr-equip-armor-" + key);

            var go = new GameObject("HandSlotRegressionHero");
            GearLoadout loadout = null;
            try
            {
                loadout = go.AddComponent<GearLoadout>();
                loadout.BindOwnerClass(Job);   // sets the class + runs an initial Refresh

                // --- 1. 1H + shield coexist ---
                loadout.EquipWeaponById(oneH.id);
                loadout.EquipOffHandById(shield.id);
                if (loadout.EquippedWeapon == null || loadout.EquippedWeapon.id != oneH.id)
                    failures.Add($"[hand-slot] 1H+shield: main-hand expected '{oneH.id}' but was '{loadout.EquippedWeapon?.id ?? "<null>"}'");
                if (loadout.EquippedOffHand == null || loadout.EquippedOffHand.id != shield.id)
                    failures.Add($"[hand-slot] 1H+shield: off-hand expected '{shield.id}' but was '{loadout.EquippedOffHand?.id ?? "<null>"}'");
                log.AppendLine($"  R1 1H+shield -> main='{loadout.EquippedWeapon?.id ?? "<null>"}' off='{loadout.EquippedOffHand?.id ?? "<null>"}'");

                // --- 2. equip 2H over 1H+shield -> off-hand cleared ---
                loadout.EquipWeaponById(twoH.id);
                if (loadout.EquippedWeapon == null || loadout.EquippedWeapon.id != twoH.id)
                    failures.Add($"[hand-slot] equip 2H: main-hand expected '{twoH.id}' but was '{loadout.EquippedWeapon?.id ?? "<null>"}'");
                if (loadout.EquippedOffHand != null)
                    failures.Add($"[hand-slot] equip 2H: off-hand should be CLEARED but was '{loadout.EquippedOffHand.id}' (2H takes both hands)");
                log.AppendLine($"  R2 equip 2H -> main='{loadout.EquippedWeapon?.id ?? "<null>"}' off='{loadout.EquippedOffHand?.id ?? "<null>"}'");

                // --- 3. equip shield while 2H held -> 2H removed, main falls back to a 1H ---
                loadout.EquipOffHandById(shield.id);
                if (loadout.EquippedOffHand == null || loadout.EquippedOffHand.id != shield.id)
                    failures.Add($"[hand-slot] shield-over-2H: off-hand expected '{shield.id}' but was '{loadout.EquippedOffHand?.id ?? "<null>"}'");
                if (loadout.EquippedWeapon != null && loadout.EquippedWeapon.IsTwoHanded)
                    failures.Add($"[hand-slot] shield-over-2H: 2H '{loadout.EquippedWeapon.id}' should have been REMOVED but is still in the main hand");
                // Armed-hero invariant: a 1H exists for this class, so the main hand must NOT be empty.
                if (loadout.EquippedWeapon == null)
                    failures.Add("[hand-slot] shield-over-2H: main hand left UNARMED though a 1H fallback exists (armed-hero invariant broken)");
                else if (loadout.EquippedWeapon.IsOffHandItem)
                    failures.Add($"[hand-slot] shield-over-2H: main hand holds an off-hand item '{loadout.EquippedWeapon.id}' (a shield can never be the main hand)");
                log.AppendLine($"  R3 shield-over-2H -> main='{loadout.EquippedWeapon?.id ?? "<null>"}' off='{loadout.EquippedOffHand?.id ?? "<null>"}'");
            }
            catch (System.Exception ex)
            {
                failures.Add($"[hand-slot] rule check threw: {ex.Message}");
                log.AppendLine($"  hand-slot check EXCEPTION: {ex}");
            }
            finally
            {
                if (go != null) Object.DestroyImmediate(go);
                // Leave no durable test state behind.
                PlayerPrefs.DeleteKey("dotr-equip-weapon-" + key);
                PlayerPrefs.DeleteKey("dotr-equip-offhand-" + key);
                PlayerPrefs.DeleteKey("dotr-equip-armor-" + key);
            }
        }

        private static WeaponDef FindTwoHanded(string job, int level)
        {
            foreach (var w in GearCatalog.AllWeapons())
            {
                if (w == null || !w.IsTwoHanded) continue;
                if (GearCatalog.WeaponFitsClass(w, job) && (w.req == null || level >= w.req.level)) return w;
            }
            return null;
        }

        private static WeaponDef FindShield(int level)
        {
            foreach (var w in GearCatalog.AllWeapons())
            {
                if (w == null || !w.IsOffHandItem) continue;
                if (w.req == null || level >= w.req.level) return w;
            }
            return null;
        }

        // True when <paramref name="key"/> resolves to at least one Addressable resource
        // location (i.e. the address is registered in the content catalog — the Gear group
        // entries marked by BlinkAddressableMarker). Synchronous via WaitForCompletion; the
        // handle is released after the check so the locations probe never leaks.
        private static bool AddressableKeyExists(string key)
        {
            if (string.IsNullOrEmpty(key)) return false;
            try
            {
                AsyncOperationHandle<IList<IResourceLocation>> h =
                    Addressables.LoadResourceLocationsAsync(key);
                IList<IResourceLocation> locs = h.WaitForCompletion();
                bool exists = locs != null && locs.Count > 0;
                if (h.IsValid()) Addressables.Release(h);
                return exists;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[DataRegression] Addressable key probe threw for '{key}': {ex.Message}");
                return false;
            }
        }

        // =====================================================================
        //  BATTLE CLOSING — WO-505: victory/defeat clip resolve + star-rating math
        // -----------------------------------------------------------------------
        //  (a) AUDIO: the win/loss climax must not be silent. The clips ship at
        //      Assets/Audio/Resources/{victory,defeat}.mp3 and AudioBootstrap loads
        //      them by short name via Resources.Load<AudioClip>("victory"/"defeat").
        //      We do the EXACT same load and FAIL if either returns null — that is the
        //      silent-track bug class (the known Resources.Load("dungeon") == null).
        //  (b) STARS: BattleStarRating.StarsForDuration must map sample durations to the
        //      right tier (60s->3, 100s->2, 200s->1) and MultiplierForStars must match
        //      (3->1.50x, 2->1.25x, 1->1.00x). Pure math; deterministic.
        //  Emits FlowTrace.Fail per violation so it lands in the break-log marker, in
        //  addition to the REGRESSION_FAIL line.
        // =====================================================================
        private static void CheckBattleClosing(List<string> failures, StringBuilder log)
        {
            log.AppendLine("[battle-closing] victory/defeat clip resolve + star-rating tiers:");

            // (a) AUDIO — resolve through the same Resources path AudioBootstrap uses.
            string[] cueNames = { "victory", "defeat" };
            foreach (var name in cueNames)
            {
                var clip = Resources.Load<AudioClip>(name);
                if (clip == null)
                {
                    string msg = $"battle-closing: Resources.Load<AudioClip>(\"{name}\") is NULL — " +
                                 "the win/loss climax would play SILENT (clip missing from " +
                                 "Assets/Audio/Resources/ or not imported as an AudioClip)";
                    failures.Add(msg);
                    DeNelle.Core.Diagnostics.FlowTrace.Fail("Regression", msg);
                    log.AppendLine($"  AUDIO '{name}' -> NULL (SILENT CLIMAX)");
                }
                else
                {
                    log.AppendLine($"  AUDIO '{name}' -> clip OK ('{clip.name}', {clip.length:0.0}s)");
                }
            }

            // (b) STARS — sample durations -> expected tier, and the matching multiplier.
            // (duration, expectedStars, expectedMultiplier)
            var samples = new (float dur, int stars, float mult)[]
            {
                (60f,  3, 1.50f),   // fast clean win
                (90f,  3, 1.50f),   // exactly the 3-star boundary (inclusive)
                (100f, 2, 1.25f),   // mid
                (120f, 2, 1.25f),   // exactly the 2-star boundary (inclusive)
                (200f, 1, 1.00f),   // slow win
            };
            foreach (var s in samples)
            {
                int gotStars = BattleStarRating.StarsForDuration(s.dur);
                float gotMult = BattleStarRating.MultiplierForStars(gotStars);
                bool starOk = gotStars == s.stars;
                bool multOk = Mathf.Approximately(gotMult, s.mult);
                if (!starOk)
                {
                    string msg = $"battle-closing: StarsForDuration({s.dur:0}s) = {gotStars}, expected {s.stars}";
                    failures.Add(msg);
                    DeNelle.Core.Diagnostics.FlowTrace.Fail("Regression", msg);
                }
                if (!multOk)
                {
                    string msg = $"battle-closing: MultiplierForStars({gotStars}) = {gotMult:0.00}, expected {s.mult:0.00}";
                    failures.Add(msg);
                    DeNelle.Core.Diagnostics.FlowTrace.Fail("Regression", msg);
                }
                log.AppendLine($"  STARS dur={s.dur:0}s -> {gotStars} star(s) x{gotMult:0.00} " +
                               $"(expected {s.stars} x{s.mult:0.00}) {((starOk && multOk) ? "OK" : "FAIL")}");
            }
        }

        // =====================================================================
        //  WEAPON SWING-TRAIL VFX - WO-504 slice 3 (WeaponVfxMap pure resolver)
        // -----------------------------------------------------------------------
        //  Gates the rarity -> trail color/width MAPPING (not the aesthetic - the
        //  exact colors are owner-felt-tune bones). Asserts:
        //   1. each band resolves a DISTINCT color (common != legendary, etc.);
        //   2. legendary (and elarion) == the GoldColor const, common/null == SteelColor;
        //   3. a null weapon -> the steel common default (null-safe);
        //   4. trail WIDTH escalates MONOTONICALLY common < uncommon < rare < epic < legendary.
        //  Emits FlowTrace.Fail per violation so it lands in the break-log marker.
        // =====================================================================
        private static void CheckWeaponVfx(List<string> failures, StringBuilder log)
        {
            log.AppendLine("[weapon-vfx] rarity -> swing-trail color/width mapping (WO-504 s3):");

            // (1) distinct color per band - build the per-band colors via the resolver.
            string[] bands = { "common", "uncommon", "rare", "epic", "legendary", "elarion" };
            var colors = new Dictionary<string, Color>();
            var widths = new Dictionary<string, float>();
            foreach (var b in bands)
            {
                var w = new WeaponDef { id = "vfx_" + b, name = b, rarity = b };
                var profile = WeaponVfxMap.Resolve(w);
                colors[b] = profile.TrailColor;
                widths[b] = profile.TrailWidth;
                log.AppendLine($"  VFX {b} -> color=({profile.TrailColor.r:0.00},{profile.TrailColor.g:0.00}," +
                               $"{profile.TrailColor.b:0.00},{profile.TrailColor.a:0.00}) width={profile.TrailWidth:0.000}");
            }

            // The five DISTINCT visual tiers (legendary & elarion intentionally SHARE the gold apex).
            string[] distinct = { "common", "uncommon", "rare", "epic", "legendary" };
            for (int i = 0; i < distinct.Length; i++)
                for (int j = i + 1; j < distinct.Length; j++)
                {
                    if (ApproxColor(colors[distinct[i]], colors[distinct[j]]))
                    {
                        string msg = $"weapon-vfx: bands '{distinct[i]}' and '{distinct[j]}' resolve the SAME trail color " +
                                     "(each rarity tier must read distinct)";
                        failures.Add(msg);
                        DeNelle.Core.Diagnostics.FlowTrace.Fail("Regression", msg);
                    }
                }

            // common vs legendary must differ (the headline read).
            if (ApproxColor(colors["common"], colors["legendary"]))
            {
                string msg = "weapon-vfx: common and legendary resolve the same trail color (a legendary blade must read legendary)";
                failures.Add(msg);
                DeNelle.Core.Diagnostics.FlowTrace.Fail("Regression", msg);
            }

            // (2) apex/default consts pinned by name.
            if (!ApproxColor(colors["legendary"], WeaponVfxMap.GoldColor))
            {
                string msg = "weapon-vfx: legendary color != WeaponVfxMap.GoldColor (the gold apex const)";
                failures.Add(msg);
                DeNelle.Core.Diagnostics.FlowTrace.Fail("Regression", msg);
            }
            if (!ApproxColor(colors["elarion"], WeaponVfxMap.GoldColor))
            {
                string msg = "weapon-vfx: elarion mark color != WeaponVfxMap.GoldColor (top band shares the gold apex)";
                failures.Add(msg);
                DeNelle.Core.Diagnostics.FlowTrace.Fail("Regression", msg);
            }

            // (3) null weapon -> steel common default (null-safe).
            var nullProfile = WeaponVfxMap.Resolve(null);
            if (!ApproxColor(nullProfile.TrailColor, WeaponVfxMap.SteelColor))
            {
                string msg = "weapon-vfx: Resolve(null) color != WeaponVfxMap.SteelColor (null weapon must fall back to the steel default)";
                failures.Add(msg);
                DeNelle.Core.Diagnostics.FlowTrace.Fail("Regression", msg);
            }
            if (!Mathf.Approximately(nullProfile.TrailWidth, WeaponVfxMap.CommonWidth))
            {
                string msg = "weapon-vfx: Resolve(null) width != WeaponVfxMap.CommonWidth (null weapon must fall back to the common width)";
                failures.Add(msg);
                DeNelle.Core.Diagnostics.FlowTrace.Fail("Regression", msg);
            }

            // (4) width escalates MONOTONICALLY common < uncommon < rare < epic < legendary.
            for (int i = 1; i < distinct.Length; i++)
            {
                float prev = widths[distinct[i - 1]];
                float cur  = widths[distinct[i]];
                if (!(cur > prev))
                {
                    string msg = $"weapon-vfx: trail width does not escalate '{distinct[i - 1]}'({prev:0.000}) -> " +
                                 $"'{distinct[i]}'({cur:0.000}) (must be monotonically increasing)";
                    failures.Add(msg);
                    DeNelle.Core.Diagnostics.FlowTrace.Fail("Regression", msg);
                }
            }
        }

        private static bool ApproxColor(Color a, Color b)
        {
            return Mathf.Approximately(a.r, b.r) && Mathf.Approximately(a.g, b.g)
                && Mathf.Approximately(a.b, b.b) && Mathf.Approximately(a.a, b.a);
        }

        private static string CostStr(DeNelle.Village.ResourceCost c)
        {
            var parts = new List<string>();
            if (c.Wood > 0) parts.Add(c.Wood + "W");
            if (c.Iron > 0) parts.Add(c.Iron + "I");
            if (c.Food > 0) parts.Add(c.Food + "F");
            if (c.Crystals > 0) parts.Add(c.Crystals + "C");
            return parts.Count == 0 ? "Free" : string.Join("+", parts);
        }
    }
}
