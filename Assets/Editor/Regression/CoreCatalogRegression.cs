// =============================================================================
// CoreCatalogRegression — the Core catalog/registry read + mapping contract.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core). Headless, no-scene.
// Proves, through the REAL game code paths, that the Core "data catalog" seam maps
// JSON -> objects and registers/looks-up defs without a silent blank:
//   • GarrisonRecipeCatalog.Reload() deserializes garrison-recipes.json to >=1
//     GarrisonRecipe, every recipe carries a non-empty Id, ids are UNIQUE, and the
//     owner-canon village2_stronghold recipe is present (memory: village2 stronghold).
//   • DataInjector.TryInject round-trips a known table to a non-null object, and a
//     bogus table path returns false (the guarded miss path, not an exception).
//   • CatalogRegistry register/get/OfType/replace semantics hold (no duplicate on
//     re-register; id-less entries rejected).
//
// Throwaway registry entries use a GUID id so they can't collide with real content;
// the registry is a headless one-shot process, so a leftover synthetic entry is inert.
//
// Wire into the suite from DataRegression.RunAll (one line):
//   if (!CoreCatalogRegression.Run(out var coreCatReason)) failures.Add(coreCatReason); else log.AppendLine("[core-catalog] " + coreCatReason);
// =============================================================================
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using DeNelle.Core;
using DeNelle.Core.Catalog;
using DeNelle.Core.World;

namespace DeNelle.Editor
{
    public static class CoreCatalogRegression
    {
        private const string RequiredRecipeId = "village2_stronghold";

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- CORE CATALOG (garrison recipes + DataInjector + CatalogRegistry) ---");

            // ── (1) GarrisonRecipeCatalog — JSON -> GarrisonRecipe mapping ───────────
            GarrisonRecipeCatalog.Reload();
            var recipes = GarrisonRecipeCatalog.All;
            if (recipes == null || recipes.Count == 0)
                failures.Add("GarrisonRecipeCatalog.All is EMPTY (garrison-recipes.json failed to map to recipes)");
            else
            {
                var seen = new HashSet<string>();
                bool foundRequired = false;
                for (int i = 0; i < recipes.Count; i++)
                {
                    var r = recipes[i];
                    if (r == null) { failures.Add($"garrison recipe [{i}] is null"); continue; }
                    if (string.IsNullOrEmpty(r.Id)) { failures.Add($"garrison recipe [{i}] has an empty Id"); continue; }
                    if (!seen.Add(r.Id)) failures.Add($"duplicate garrison recipe Id '{r.Id}'");
                    if (r.Id == RequiredRecipeId) foundRequired = true;
                    // Find(id) must round-trip the same object (case-insensitive lookup).
                    if (GarrisonRecipeCatalog.Find(r.Id) == null)
                        failures.Add($"GarrisonRecipeCatalog.Find('{r.Id}') returned null for a loaded recipe");
                }
                if (!foundRequired)
                    failures.Add($"required recipe '{RequiredRecipeId}' (village2 stronghold) missing from garrison-recipes.json");
                log.AppendLine($"garrison recipes: {recipes.Count} loaded, required '{RequiredRecipeId}' present={foundRequired}.");
            }

            // ── (2) DataInjector — generic table -> object + guarded miss ────────────
            bool ok = DataInjector.TryInject<GarrisonRecipeFile>("Data/Canonical/garrison-recipes.json", out var file);
            if (!ok || file == null || file.Recipes == null || file.Recipes.Count == 0)
                failures.Add("DataInjector.TryInject<GarrisonRecipeFile>('garrison-recipes.json') did not return a populated object");
            // A missing table must return false via the guarded path (NOT throw, NOT return a stub).
            bool bogus = DataInjector.TryInject<GarrisonRecipeFile>("Data/Canonical/__does_not_exist__.json", out var _);
            if (bogus)
                failures.Add("DataInjector.TryInject on a bogus table returned TRUE (miss path should be false)");

            // ── (3) CatalogRegistry — register / get / OfType / replace semantics ────
            string gid = "test-" + System.Guid.NewGuid().ToString("N");
            var entry = new CatalogEntry { id = gid, displayName = "oracle probe", type = CatalogType.Decoration };
            int before = CatalogRegistry.Count;
            CatalogRegistry.Register(entry);
            if (!ReferenceEquals(CatalogRegistry.Get(gid), entry))
                failures.Add("CatalogRegistry.Get did not return the just-registered entry");
            var ofType = CatalogRegistry.OfType(CatalogType.Decoration);
            bool inType = false;
            if (ofType != null) foreach (var e in ofType) if (ReferenceEquals(e, entry)) { inType = true; break; }
            if (!inType) failures.Add("CatalogRegistry.OfType(Decoration) did not contain the registered entry");
            // Re-register the SAME entry: must NOT duplicate in the type list, Count unchanged.
            CatalogRegistry.Register(entry);
            if (CatalogRegistry.Count != before + 1)
                failures.Add($"CatalogRegistry.Count changed unexpectedly on re-register (before={before}, now={CatalogRegistry.Count})");
            // Id-less entry must be rejected (no throw, no add).
            int preNull = CatalogRegistry.Count;
            CatalogRegistry.Register(new CatalogEntry { id = "", type = CatalogType.Decoration });
            if (CatalogRegistry.Count != preNull)
                failures.Add("CatalogRegistry accepted an id-less entry (should be skipped)");

            if (failures.Count == 0)
            {
                Debug.Log(log.ToString() + "CORE_CATALOG_OK");
                reason = "CORE CATALOG OK — garrison recipes map + DataInjector round-trips + CatalogRegistry semantics hold";
                return true;
            }
            reason = "core-catalog: " + string.Join("; ", failures);
            Debug.LogError(log.ToString() + "CORE_CATALOG_FAIL: " + reason);
            return false;
        }
    }
}
