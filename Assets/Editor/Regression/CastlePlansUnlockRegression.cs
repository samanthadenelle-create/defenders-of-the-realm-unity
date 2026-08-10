// =============================================================================
// CastlePlansUnlockRegression [castle-plans] -- WO-1013 guardrails for the
// Castle Defense Plans drop / Arcane Spire visible-lock.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. Pins the four WO-1013 guardrails:
//   1. LOCKED-UNTIL-UNLOCK -- build-categories.json carries tower_arcane_spire as
//      a Defense-verb visibleLockedIds row (reason in WORDS, and NOT also hidden
//      by lockedIds), and BuildPaletteVM projects it as a Locked card with its
//      REAL cost until the unlock provider flips -- then as a normal card.
//   2. UNLOCK+GRANT IDEMPOTENT -- CastleDefensePlansPickup.TryCollect() over a
//      throwaway GameState + real EconomyService grants EXACTLY ONCE; the second
//      call is a no-op (collect once, ever) and the flag persists in the
//      SeenTutorials store under 'unlock.tower_arcane_spire'.
//   3. WAVE 3+ SPAWN NOTHING SCRIPTED -- CastleDefensePlansService.ShouldSpawnDrop
//      truth table: spawns at >= 2 waves while uncollected (persisting across
//      waves until collected), NEVER after collection, never twice.
//   4. FUNDING == LIVE CATALOG ROW -- the granted basket equals the
//      structures-catalog.json tower_arcane_spire cost read from the data (never
//      a number restated here), and that basket is crystals-inclusive (WO-947
//      arcane basket).
//
// Marker: CASTLE_PLANS_OK / CASTLE_PLANS_FAIL. Expected: GREEN.
//
// Wire (DataRegression.RunAll):
//   DeNelle.Core.Diagnostics.Guard.Try("Regression", "castle-plans suite", () => { if (!CastlePlansUnlockRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[castle-plans] " + r); });
// =============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using DeNelle.Village;
using DeNelle.Core.State;
using DeNelle.Core.Catalog;
using CoreCost = DeNelle.Core.Catalog.ResourceCost;

namespace DeNelle.Editor
{
    public static class CastlePlansUnlockRegression
    {
        private const string SaveKey = "dotr-save";
        private const string SpireId = "tower_arcane_spire";
        private const string UnlockKey = "unlock." + SpireId;

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- CASTLE PLANS (WO-1013 visible-lock / wave-2 drop / unlock+grant) ---");

            // ---- 4 (data half): the live catalog row's cost, read FROM the data ----
            if (!ReadCatalogCost(out CoreCost rowCost, out string readErr))
            {
                failures.Add("structures-catalog.json read: " + readErr);
                reason = Finish(failures, log);
                return failures.Count == 0;
            }
            log.AppendLine($"  catalog row '{SpireId}': wood={rowCost.wood} food={rowCost.food} iron={rowCost.iron} crystals={rowCost.crystals}");
            if (rowCost.crystals <= 0)
                failures.Add($"[castle-plans] '{SpireId}' cost is NOT crystals-inclusive (crystals={rowCost.crystals}) -- the WO-947 arcane basket requires crystals");

            // ---- 1a: the spire is HIDDEN until earned (WO-964, owner ruling 2026-08-10) ----
            //
            // ⚠ THIS ASSERTION WAS INVERTED ON 2026-08-10, and the inversion is the point.
            // WO-1013 (same day, earlier) shipped the spire as a VISIBLE-LOCKED card so the
            // player could see what was coming and save for it. The owner then played it and
            // ruled the opposite, verbatim (F8 seq 2303): "dont show the spire, leave as blank
            // till earned, allows us to unlock new items and not reveal what they are." The
            // reveal IS the reward, and hiding it is what lets new structures ship unspoiled.
            //
            // So the guardrail flips sides: the spire must sit in Defense lockedIds (filtered
            // OUT of the palette entirely) and must NOT carry a visibleLockedIds row. The
            // ProgressionUnlocks gate is unchanged -- it still decides WHEN, only the
            // pre-unlock PRESENTATION changed. This suite stays strict in both directions:
            // shipping it buildable from minute one still fails, and so does regressing it
            // back to a greyed card.
            var defense = BuildCategoryRegistry.Get(BuildType.Defense);
            if (defense == null || defense.LockedIds == null || !defense.LockedIds.Contains(SpireId))
                failures.Add($"[castle-plans] '{SpireId}' is NOT in Defense lockedIds -- WO-964 requires it HIDDEN until earned, not shown-locked and not buildable");
            else
                log.AppendLine($"  spire hidden until earned (WO-964): in Defense lockedIds, no visible card");
            if (defense != null && defense.VisibleLockedReasons != null
                && defense.VisibleLockedReasons.ContainsKey(SpireId))
                failures.Add($"[castle-plans] '{SpireId}' still carries a visibleLockedIds row -- that is the RETIRED WO-1013 presentation; WO-964 hides it instead of greying it");

            // ---- 1b: VM projection -- Locked with real cost until the provider flips ----
            try
            {
                var entry = new CatalogEntry
                {
                    id = SpireId,
                    displayName = "Arcane Spire",
                    type = CatalogType.Tower,
                    repo = new RepoProps { cost = rowCost, maxLevel = 3 },
                };
                var entries = new List<CatalogEntry> { entry };
                // WO-964: the spire is HIDDEN by lockedIds until earned, not greyed. The category
                // is built the way build-categories.json now authors it.
                var category = new BuildCategory
                {
                    Types = new[] { CatalogType.Tower },
                    Label = "Build Defenses",
                    LockedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { SpireId },
                };
                bool unlocked = false;
                var vm = new BuildPaletteVM(
                    null,
                    _ => category,
                    _ => entries,
                    // Freebie provider says NO, deliberately (changed with WO-964).
                    // It used to say YES to prove the D20 rule "a LOCKED card must still show its
                    // real cost, never FREE" -- but WO-964 removed the locked card entirely: before
                    // the unlock there is no card at all. After the unlock, first-build-free is a
                    // LEGITIMATE rule, so a YES here would assert a violation that is not one.
                    // What this case proves now is the REVEALED card's cost display, so the freebie
                    // axis is held out of it.
                    _ => false,
                    () => entries.Count,
                    BuildType.Defense,
                    null,
                    id => unlocked);

                // BEFORE the unlock: the card must be ABSENT entirely (WO-964 -- "leave as blank
                // till earned ... not reveal what they are"), not greyed, not buildable.
                if (vm.Cards.Count != 0)
                    failures.Add($"[castle-plans] VM projected {vm.Cards.Count} card(s) while the unlock flag is DOWN -- WO-964 requires the spire to be INVISIBLE until earned (a greyed card is the retired WO-1013 presentation)");
                else
                    log.AppendLine("  VM projection: spire absent before the unlock (WO-964) OK");

                // AFTER the unlock: it must APPEAR, buildable, at its real cost. This is the half
                // that catches "hidden forever" -- before WO-964's unlock-aware filter, lockedIds
                // was static and this assertion would have failed, because the plans drop could
                // flip the flag and the card would still never come back.
                unlocked = true;
                vm.Refresh();
                if (vm.Cards.Count != 1)
                    failures.Add($"[castle-plans] after the unlock flips, VM projected {vm.Cards.Count} cards (expected 1) -- the spire is hidden FOREVER, which is worse than showing it early");
                else
                {
                    var card = vm.Cards[0];
                    if (card.Locked)
                        failures.Add("[castle-plans] after the unlock flips the card is still Locked -- the lock never lifts");
                    if (card.Freebie)
                        failures.Add("[castle-plans] unlocked card reports Freebie -- it must show its normal cost (D20 no-FREE)");
                    // The cost seam is SoftcappedCostFor, which can only RAISE a tower cost above
                    // the catalog row (never lower, never zero) -- so the invariant proved here is
                    // "the REAL cost displays": non-zero and >= the row on every component. Exact
                    // equality would flake if a scene with softcapped towers happens to be open.
                    if (card.EffectiveCost.IsZero)
                        failures.Add("[castle-plans] unlocked card cost is ZERO -- reads as FREE, the exact D20 violation");
                    else if (card.EffectiveCost.crystals < rowCost.crystals || card.EffectiveCost.wood < rowCost.wood
                        || card.EffectiveCost.iron < rowCost.iron || card.EffectiveCost.food < rowCost.food)
                        failures.Add($"[castle-plans] unlocked card cost {CostStr(card.EffectiveCost)} fell BELOW catalog row {CostStr(rowCost)} (normal cost must display)");
                }
                vm.Dispose();
                log.AppendLine("  VM projection: hidden-until-earned -> appears buildable on Refresh OK");
            }
            catch (Exception ex)
            {
                failures.Add($"[castle-plans] VM projection threw: {ex.GetType().Name}: {ex.Message}");
            }

            // ---- 3: the spawn rule truth table (pure) ----
            if (CastleDefensePlansService.ShouldSpawnDrop(1, false, false))
                failures.Add("[castle-plans] drop would spawn after only 1 wave survived");
            if (!CastleDefensePlansService.ShouldSpawnDrop(2, false, false))
                failures.Add("[castle-plans] drop does NOT spawn after wave 2 survived");
            if (!CastleDefensePlansService.ShouldSpawnDrop(7, false, false))
                failures.Add("[castle-plans] uncollected drop stopped spawning at wave 7 -- it must persist until collected");
            if (CastleDefensePlansService.ShouldSpawnDrop(2, false, true))
                failures.Add("[castle-plans] a second prop would spawn while one is standing");
            if (CastleDefensePlansService.ShouldSpawnDrop(3, true, false)
                || CastleDefensePlansService.ShouldSpawnDrop(99, true, false))
                failures.Add("[castle-plans] wave 3+ would spawn a scripted drop AFTER collection -- the once-ever guardrail is broken");
            log.AppendLine("  ShouldSpawnDrop truth table OK (>=2 uncollected spawns; collected never again)");

            // ---- 2 + 4 (grant half): collect once, ever, funding == catalog row ----
            bool hadSave = PlayerPrefs.HasKey(SaveKey);
            string rawSave = hadSave ? PlayerPrefs.GetString(SaveKey, null) : null;

            GameStateService priorGss = GameStateService.Instance;
            object priorEcon = GetInstance(typeof(EconomyService));
            int registryCountBefore = CatalogRegistry.Count;
            bool registeredTemp = false;

            GameObject gssGo = null, econGo = null;
            GameState throwaway = null;
            try
            {
                throwaway = ScriptableObject.CreateInstance<GameState>();
                gssGo = new GameObject("GSS (castle-plans oracle)");
                var gss = gssGo.AddComponent<GameStateService>();
                if (!InstallState(gss, throwaway))
                { reason = "CASTLE PLANS skipped: GameStateService state seam not reflectable (needs fleet)"; return true; }

                econGo = new GameObject("EconomyService (castle-plans oracle)");
                var econ = econGo.AddComponent<EconomyService>();
                SetInstance(typeof(EconomyService), econ);

                // The mechanics read the LIVE registry; register the real-cost row if this
                // editor session has not bootstrapped the catalog.
                if (CatalogRegistry.Get(SpireId) == null)
                {
                    CatalogRegistry.Register(new CatalogEntry
                    {
                        id = SpireId,
                        displayName = "Arcane Spire",
                        type = CatalogType.Tower,
                        repo = new RepoProps { cost = rowCost, maxLevel = 3 },
                    });
                    registeredTemp = true;
                }

                int woodBefore = throwaway.Wood, ironBefore = throwaway.Iron;
                int foodBefore = throwaway.Resources.Food, crystalsBefore = throwaway.Resources.Crystals;

                bool first = CastleDefensePlansPickup.TryCollect();
                if (!first) failures.Add("[castle-plans] first TryCollect() returned false on a fresh state");

                int woodD = throwaway.Wood - woodBefore;
                int ironD = throwaway.Iron - ironBefore;
                int foodD = throwaway.Resources.Food - foodBefore;
                int crysD = throwaway.Resources.Crystals - crystalsBefore;
                log.AppendLine($"  granted deltas: wood=+{woodD} food=+{foodD} iron=+{ironD} crystals=+{crysD}");
                if (woodD != rowCost.wood) failures.Add($"[castle-plans] wood delta {woodD} != catalog {rowCost.wood}");
                if (foodD != rowCost.food) failures.Add($"[castle-plans] food delta {foodD} != catalog {rowCost.food}");
                if (ironD != rowCost.iron) failures.Add($"[castle-plans] iron delta {ironD} != catalog {rowCost.iron}");
                if (crysD != rowCost.crystals) failures.Add($"[castle-plans] crystals delta {crysD} != catalog {rowCost.crystals}");

                bool flag = throwaway.SeenTutorials != null
                    && throwaway.SeenTutorials.TryGetValue(UnlockKey, out var v) && v;
                if (!flag) failures.Add($"[castle-plans] '{UnlockKey}' not persisted in SeenTutorials after collection");
                if (!ProgressionUnlocks.IsUnlocked(SpireId))
                    failures.Add("[castle-plans] ProgressionUnlocks.IsUnlocked false after collection");

                bool second = CastleDefensePlansPickup.TryCollect();
                if (second) failures.Add("[castle-plans] second TryCollect() returned true -- collect-once-ever broken");
                if (throwaway.Wood - woodBefore != woodD || throwaway.Resources.Crystals - crystalsBefore != crysD
                    || throwaway.Iron - ironBefore != ironD || throwaway.Resources.Food - foodBefore != foodD)
                    failures.Add("[castle-plans] second TryCollect() mutated the wallet -- double grant");
            }
            catch (Exception ex)
            {
                failures.Add($"castle-plans oracle threw: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                if (econGo != null) UnityEngine.Object.DestroyImmediate(econGo);
                if (gssGo != null) UnityEngine.Object.DestroyImmediate(gssGo);
                if (throwaway != null) UnityEngine.Object.DestroyImmediate(throwaway);
                SetInstance(typeof(EconomyService), priorEcon);
                SetGssInstance(priorGss);
                // Only wipe the registry if WE were its sole tenant (it was empty before);
                // there is no per-id Remove, and clearing someone else's rows would be worse
                // than leaving one extra real-cost row behind.
                if (registeredTemp && registryCountBefore == 0) CatalogRegistry.Clear();
                if (hadSave) PlayerPrefs.SetString(SaveKey, rawSave); else PlayerPrefs.DeleteKey(SaveKey);
                PlayerPrefs.Save();
            }

            reason = Finish(failures, log);
            return failures.Count == 0;
        }

        // ---- data readers -------------------------------------------------------

        private static bool ReadCatalogCost(out CoreCost cost, out string err)
        {
            cost = default; err = null;
            string json = DeNelle.Core.CanonicalJson.Read("Data/Canonical/structures-catalog.json");
            if (string.IsNullOrEmpty(json)) { err = "structures-catalog.json not found/empty"; return false; }
            JObject root;
            try { root = JObject.Parse(json); } catch (Exception ex) { err = "parse error: " + ex.Message; return false; }
            var entries = root["entries"] as JArray;
            if (entries == null) { err = "no 'entries' array"; return false; }
            foreach (var tok in entries)
            {
                if (!(tok is JObject o) || o["id"]?.ToString() != SpireId) continue;
                var c = o["repo"]?["cost"] as JObject;
                if (c == null) { err = $"'{SpireId}' has no repo.cost"; return false; }
                cost = new CoreCost
                {
                    wood = c["wood"]?.Value<int>() ?? 0,
                    food = c["food"]?.Value<int>() ?? 0,
                    iron = c["iron"]?.Value<int>() ?? 0,
                    crystals = c["crystals"]?.Value<int>() ?? 0,
                };
                return true;
            }
            err = $"structures-catalog.json has no '{SpireId}' entry";
            return false;
        }

        private static string CostStr(CoreCost c)
            => $"W{c.wood}/F{c.food}/I{c.iron}/C{c.crystals}";

        // ---- reflection helpers (the PackGrantRegression shape) -----------------

        private static bool InstallState(GameStateService svc, GameState state)
        {
            var f = typeof(GameStateService).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance);
            if (f == null) return false;
            f.SetValue(svc, state);
            return SetGssInstance(svc);
        }

        private static bool SetGssInstance(GameStateService svc)
        {
            var f = typeof(GameStateService).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static);
            if (f == null) return false;
            f.SetValue(null, svc);
            return true;
        }

        private static FieldInfo InstanceField(Type t)
        {
            var f = t.GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static)
                 ?? t.GetField("<Instance>k__BackingField", BindingFlags.NonPublic | BindingFlags.Static);
            if (f != null) return f;
            foreach (var ff in t.GetFields(BindingFlags.NonPublic | BindingFlags.Static))
                if (ff.Name.Contains("Instance") && ff.FieldType == t) return ff;
            return null;
        }

        private static object GetInstance(Type t) { var f = InstanceField(t); return f != null ? f.GetValue(null) : null; }
        private static void SetInstance(Type t, object val) { var f = InstanceField(t); if (f != null) f.SetValue(null, val); }

        private static string Finish(List<string> failures, StringBuilder log)
        {
            if (failures.Count == 0)
            {
                Debug.Log(log.ToString() + "CASTLE_PLANS_OK");
                return "CASTLE PLANS OK -- spire visible-locked with real cost until collection; collect once-ever grants exactly the live catalog basket; wave 3+ drops nothing scripted";
            }
            string reason = "castle-plans: " + string.Join("; ", failures);
            Debug.LogError(log.ToString() + "CASTLE_PLANS_FAIL: " + reason);
            return reason;
        }
    }
}
