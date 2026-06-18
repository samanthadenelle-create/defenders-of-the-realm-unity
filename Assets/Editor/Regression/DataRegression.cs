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
