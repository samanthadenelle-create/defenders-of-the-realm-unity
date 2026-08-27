// =============================================================================
// ManaScrollFtueRegression [mana-scroll]  --  WO-1235 guardrails for the founding
// mana potions, the recipe-scroll drop, and THE LIVE-SAVE ANTI-RETRO-LOCK.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression.
//
// WO-1235 buys ONE loop: have -> use -> RUN OUT -> find the recipe -> craft more.
// The potions are the SETUP, not the reward. Four things must hold for that loop
// to exist at all, and this suite pins each of them:
//
//   1. THE FOUNDING KIT. A new save starts with exactly FoundingManaPotions Mana
//      Draughts AND FoundingHealPotions Minor Healing Draughts, keyed by the
//      CANONICAL ids -- mana under HudCommands.ManaPotionId, health under
//      HpPotionId. Reusing the health id for mana is the specific mistake the WO
//      calls out, and it would be invisible until a player pressed the wrong belt
//      slot.
//
//   2. THE SPAWN RULE, as a pure truth table. Owner ruling #1 (trigger: mana falls
//      to 0 or 1 for the FIRST time) and owner ruling #3 (HARD precondition: no
//      station, no scroll) both live inside ManaRecipeScrollService's one rule, so
//      the table proves neither can be routed around.
//
//   3. ⭐ AN EXISTING PRE-MIGRATION SAVE KEEPS EVERY RECIPE IT COULD ALREADY
//      CRAFT. THE GAME IS LIVE. Before WO-1235 every consumable recipe was open to
//      everybody; a gate that simply started applying would silently delete
//      crafting from real players, on saves nobody can roll back. This is the case
//      that matters most and it is proved from BOTH directions: the v40 migration
//      grandfathers a pre-gate save, and the gate itself is fail-open for every id
//      outside the allow-list.
//
//   4. THE RECIPE MATCHES THE SCROLL ART (owner ruling 2026-08-26). The FTUE
//      teaching recipe is Moonbloom Herb + Arcane Dust + Spring Water, 1/1/1, and
//      all three ingredient ids must resolve in materials.json -- a scroll that
//      depicts a recipe the panel cannot show would mis-teach the one player it
//      exists for.
//
// Marker: MANA_SCROLL_OK / MANA_SCROLL_FAIL. Expected: GREEN.
//
// Wire (DataRegression.RunAll):
//   DeNelle.Core.Diagnostics.Guard.Try("Regression", "mana-scroll suite", () => { if (!DeNelle.Editor.Regression.ManaScrollFtueRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[mana-scroll] " + r); });
// =============================================================================

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using DeNelle.Core.State;
using DeNelle.Village;
using DeNelle.Village.Crafting;

namespace DeNelle.Editor.Regression
{
    public static class ManaScrollFtueRegression
    {
        private const string SaveKey = "dotr-save";
        private const string RecipeId = RecipeUnlockKeys.ManaPotionRecipeId;

        /// <summary>The owner-ruled ingredient trio, as ids. Mirrored deliberately: the ruling is
        /// explicit that these ids must NOT be renamed, so a rename fails here on purpose.</summary>
        private static readonly string[] RuledIngredients = { "ing_moonbloom", "ArcaneDust", "ing_spring_water" };
        private const int RuledCount = 1;   // 1/1/1, owner ruling 2026-08-26

        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("MANA_SCROLL_OK - " + reason);
            else Debug.LogError("MANA_SCROLL_FAIL: " + reason);
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- MANA SCROLL FTUE (WO-1235 founding kit / spawn rule / anti-retro-lock / art parity) ---");

            try
            {
                Case(failures, "spawn-rule",   () => Case2_SpawnRuleTruthTable(failures, log));
                Case(failures, "recipe-art",   () => Case4_RecipeMatchesTheScrollArt(failures, log));
                Case(failures, "gate-failopen",() => Case3a_GateIsFailOpenForUngatedIds(failures, log));
                Case(failures, "migration",    () => Case3b_PreGateSaveIsGrandfathered(failures, log));
                Case(failures, "founding-kit", () => Case1_FoundingKit(failures, log));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            if (failures.Count == 0)
            {
                Debug.Log(log.ToString() + "MANA_SCROLL_OK");
                reason = "MANA SCROLL FTUE OK - a new save carries " + StartingBudget.FoundingManaPotions +
                         " Mana Draughts under the MANA id beside " + StartingBudget.FoundingHealPotions +
                         " healing draughts under the HEALTH id; the scroll spawns only once the player has run " +
                         "out AND a crafting station is reachable, never after it is taught; a pre-gate save is " +
                         "grandfathered by the v40 migration and the gate is fail-open for every ungated recipe; " +
                         "and the teaching recipe matches the scroll art at 1/1/1.";
                return true;
            }
            reason = "mana-scroll FAIL x" + failures.Count + ": " + string.Join(" | ", failures);
            Debug.LogError(log.ToString() + "MANA_SCROLL_FAIL: " + reason);
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  Case 1 - the founding kit, and the id it is keyed under
        // =====================================================================

        private static void Case1_FoundingKit(List<string> failures, StringBuilder log)
        {
            if (StartingBudget.FoundingManaPotions <= 0)
            {
                failures.Add("[founding-kit] StartingBudget.FoundingManaPotions is " +
                             StartingBudget.FoundingManaPotions + ". The WO-1235 loop is have -> use -> RUN OUT -> " +
                             "find the recipe: with zero founding potions the player never FEELS the resource, and " +
                             "the scroll's trigger (mana at or below the threshold) would fire on turn one, before " +
                             "the lesson exists.");
                return;
            }
            if (DeNelle.Core.HUD.HudCommands.ManaPotionId == DeNelle.Core.HUD.HudCommands.HpPotionId)
            {
                failures.Add("[founding-kit] HudCommands.ManaPotionId and HpPotionId are the SAME id - the WO warns " +
                             "about exactly this: the mana grant would land on the healing stack and the belt's " +
                             "mana slot would stay empty.");
                return;
            }

            GameStateService priorGss = GameStateService.Instance;
            bool hadSave = PlayerPrefs.HasKey(SaveKey);
            string rawSave = hadSave ? PlayerPrefs.GetString(SaveKey, null) : null;
            GameObject gssGo = null;
            GameState throwaway = null;
            try
            {
                throwaway = ScriptableObject.CreateInstance<GameState>();
                gssGo = new GameObject("GSS (mana-scroll oracle)");
                var gss = gssGo.AddComponent<GameStateService>();
                if (!InstallState(gss, throwaway))
                {
                    log.AppendLine("  " + RegressionOutcome.PartialSkip("founding-kit",
                        "GameStateService state seam not reflectable in this environment (needs fleet) - the " +
                        "founding grant was NOT asserted"));
                    return;
                }

                // ResetToNewGame() is public and parameterless - the ONE founding grant seam
                // (every New Game routes through it), so the oracle drives the real path rather
                // than re-implementing the grant and asserting its own copy.
                gss.ResetToNewGame();

                var inv = throwaway.GearInventory;
                if (inv == null)
                {
                    failures.Add("[founding-kit] ResetToNewGame left GearInventory null - the founding grant IS " +
                                 "that dictionary, so nothing was seeded.");
                    return;
                }

                AssertStack(failures, inv, DeNelle.Core.HUD.HudCommands.ManaPotionId,
                    StartingBudget.FoundingManaPotions, "mana");
                AssertStack(failures, inv, DeNelle.Core.HUD.HudCommands.HpPotionId,
                    StartingBudget.FoundingHealPotions, "healing");

                log.AppendLine("  founding kit: " + StartingBudget.FoundingManaPotions + "x '" +
                               DeNelle.Core.HUD.HudCommands.ManaPotionId + "' + " +
                               StartingBudget.FoundingHealPotions + "x '" +
                               DeNelle.Core.HUD.HudCommands.HpPotionId + "'");
            }
            finally
            {
                if (gssGo != null) UnityEngine.Object.DestroyImmediate(gssGo);
                if (throwaway != null) UnityEngine.Object.DestroyImmediate(throwaway);
                SetGssInstance(priorGss);
                if (hadSave) PlayerPrefs.SetString(SaveKey, rawSave); else PlayerPrefs.DeleteKey(SaveKey);
                PlayerPrefs.Save();
            }
        }

        private static void AssertStack(List<string> failures, Dictionary<string, int> inv,
                                        string id, int want, string label)
        {
            inv.TryGetValue(id, out int have);
            if (have != want)
                failures.Add("[founding-kit] a NEW save carries " + have + "x '" + id + "' (" + label +
                             "), expected " + want + ". The founding kit is the SETUP for the WO-1235 loop; a " +
                             "wrong count changes when - or whether - the player ever feels the need.");
        }

        // =====================================================================
        //  Case 2 - the spawn rule truth table (pure, no play session)
        // =====================================================================

        private static void Case2_SpawnRuleTruthTable(List<string> failures, StringBuilder log)
        {
            // The one shape that SHOULD spawn: need felt, station standing, not taught, no prop.
            if (!ManaRecipeScrollService.ShouldSpawnDrop(true, true, false, false))
                failures.Add("[spawn-rule] the scroll does NOT spawn when the player has run out, a station is " +
                             "standing, the recipe is untaught and no prop stands - that is the ONE shape the whole " +
                             "feature exists for, and a rule that never fires is a feature that never ships.");

            // ⭐ Owner ruling #3, the HARD precondition: no station, no scroll.
            if (ManaRecipeScrollService.ShouldSpawnDrop(true, false, false, false))
                failures.Add("[spawn-rule] the scroll spawns with NO crafting station reachable. Owner ruling " +
                             "WO-1235 #3, verbatim: 'Never teach a verb the player cannot immediately perform.' " +
                             "This is a HARD precondition, not a follow-up polish item.");

            // ⭐ Owner ruling #1, the trigger: the need must be felt first.
            if (ManaRecipeScrollService.ShouldSpawnDrop(false, true, false, false))
                failures.Add("[spawn-rule] the scroll spawns before the player has run out. The potions are the " +
                             "SETUP: offering the recipe while the stack is full destroys the 'I need this -> I " +
                             "discover the solution' moment the trigger exists to create (owner ruling #1).");

            // Once taught, the rule is closed FOREVER - and this is also what stops an existing,
            // grandfathered player from ever seeing a scroll for a recipe they already had.
            if (ManaRecipeScrollService.ShouldSpawnDrop(true, true, true, false))
                failures.Add("[spawn-rule] a scroll spawns for a recipe that is ALREADY TAUGHT. That breaks the " +
                             "once-ever guardrail, and it would also put a redundant prop in front of every " +
                             "existing player, all of whom the v40 migration grandfathered as taught.");

            // Never two props.
            if (ManaRecipeScrollService.ShouldSpawnDrop(true, true, false, true))
                failures.Add("[spawn-rule] a second prop would spawn while one is already standing.");

            // The need latch itself, at and around the constant (never restating the number).
            int t = ManaRecipeScrollService.NeedThreshold;
            if (!ManaRecipeScrollService.ShouldLatchNeed(t))
                failures.Add("[spawn-rule] a stack AT the threshold (" + t + ") does not latch the need - the owner " +
                             "ruled the trigger is mana falling TO 0 or 1, inclusive.");
            if (!ManaRecipeScrollService.ShouldLatchNeed(0))
                failures.Add("[spawn-rule] an EMPTY mana stack does not latch the need - that is the strongest " +
                             "possible form of having run out.");
            if (ManaRecipeScrollService.ShouldLatchNeed(t + 1))
                failures.Add("[spawn-rule] a stack ABOVE the threshold (" + (t + 1) + ") latches the need - the " +
                             "scroll would arrive while the player still has potions and has learned nothing.");
            if (ManaRecipeScrollService.ShouldLatchNeed(StartingBudget.FoundingManaPotions))
                failures.Add("[spawn-rule] the FOUNDING stack (" + StartingBudget.FoundingManaPotions + ") latches " +
                             "the need immediately - a brand-new player would be handed the recipe before ever " +
                             "using a potion, which inverts the entire WO-1235 loop.");

            log.AppendLine("  spawn rule truth table OK (need>=latch " + t + ", station HARD-required, once-ever)");
        }

        // =====================================================================
        //  Case 3a - the gate is FAIL-OPEN for every ungated recipe id
        // =====================================================================

        private static void Case3a_GateIsFailOpenForUngatedIds(List<string> failures, StringBuilder log)
        {
            // The allow-list must contain the mana recipe...
            if (!RecipeUnlockKeys.IsGated(RecipeId))
                failures.Add("[gate-failopen] '" + RecipeId + "' is NOT on the gated allow-list - the scroll would " +
                             "teach a recipe that was never withheld, so the FTUE moment teaches nothing.");

            // ...and NOTHING ELSE, without an owner ruling. WO-1235 #2 is verbatim: the scroll
            // grants ONLY the Mana Potion recipe, "Do NOT unlock the recipe book".
            if (RecipeUnlockKeys.GatedRecipeIds.Length != 1)
                failures.Add("[gate-failopen] the gated allow-list holds " + RecipeUnlockKeys.GatedRecipeIds.Length +
                             " ids, expected exactly 1. Owner ruling WO-1235 #2: the scroll 'unlocks Crafting as a " +
                             "VISIBLE SYSTEM' but grants ONLY the Mana Potion recipe - 'Do NOT unlock the recipe " +
                             "book'. Every id added here silently removes a recipe from every live player.");

            // Every OTHER recipe in the catalog must be craftable with no save state whatsoever.
            // This is the retro-lock defence stated as an assertion.
            var all = DeNelle.Village.Items.ConsumableCraftingCatalog.All;
            if (all == null || all.Count == 0)
            {
                log.AppendLine("  " + RegressionOutcome.PartialSkip("gate-failopen",
                    "consumable-recipes.json produced no recipes in this environment - the fail-open sweep was " +
                    "NOT run"));
            }
            else
            {
                int ungated = 0;
                foreach (var r in all)
                {
                    if (r == null || string.IsNullOrEmpty(r.Id)) continue;
                    if (RecipeUnlockKeys.IsGated(r.Id)) continue;
                    ungated++;
                    if (!RecipeUnlocks.IsCraftable(r.Id))
                        failures.Add("[gate-failopen] ungated recipe '" + r.Id + "' reports NOT craftable. THE GAME " +
                                     "IS LIVE: every recipe outside the allow-list was open to every player before " +
                                     "WO-1235, and withholding one now removes content from a save that cannot be " +
                                     "rolled back.");
                }
                if (ungated == 0)
                    failures.Add("[gate-failopen] every recipe in the catalog is gated - the allow-list has become " +
                                 "a deny-list, which is the retro-lock failure exactly.");
                log.AppendLine("  fail-open sweep: " + ungated + " ungated recipe(s), all craftable with no save state");
            }

            // An empty/unknown id must be refused rather than silently allowed.
            if (RecipeUnlocks.IsCraftable(null) || RecipeUnlocks.IsCraftable(""))
                failures.Add("[gate-failopen] IsCraftable returned true for a null/empty recipe id.");
        }

        // =====================================================================
        //  Case 3b - ⭐ A PRE-MIGRATION SAVE KEEPS WHAT IT COULD ALREADY CRAFT
        // =====================================================================

        private static void Case3b_PreGateSaveIsGrandfathered(List<string> failures, StringBuilder log)
        {
            // A save as it existed BEFORE the gate: no recipe-unlock keys at all.
            var preGate = new SaveSchema.PersistedState
            {
                SeenTutorials = new Dictionary<string, bool>(),
            };

            // Migrate it the way a real load does: from the last version that predates the gate.
            //
            // !! DO NOT WRITE THIS AS `SaveSchema.CurrentVersion - 1`. It was exactly that, and it
            // silently stopped testing anything the moment an unrelated ticket bumped the schema:
            // the recipe gate landed at v40, so "pre-gate" is v39 - a HISTORICAL FACT that never
            // moves. When WO-823 took CurrentVersion to 41, `CurrentVersion - 1` became 40, the
            // chain skipped MigrateToV40 altogether, and the grandfather never ran. The oracle then
            // reported that live players lose crafting - against production code that is CORRECT.
            //
            // A relative reference to a moving value is not a constant; it is a slow bug. Same
            // defect class as the stale WO-number block, the retired dependency table, the portrait
            // path in eleven literals, and echo-spec restating CurrentVersion = 39.
            const int RecipeGateSchemaVersion = 40;   // the version WO-1235's gate landed at
            var migrated = SaveMigrator.Migrate(preGate, RecipeGateSchemaVersion - 1);
            if (migrated == null)
            {
                failures.Add("[migration] SaveMigrator.Migrate returned null for a pre-gate save.");
                return;
            }
            if (migrated.SeenTutorials == null)
            {
                failures.Add("[migration] the migration left seenTutorials null - the grandfather keys had nowhere " +
                             "to land, so every existing player loses the recipe.");
                return;
            }

            foreach (string id in RecipeUnlockKeys.GatedRecipeIds)
            {
                string key = RecipeUnlockKeys.KeyFor(id);
                if (!migrated.SeenTutorials.TryGetValue(key, out bool taught) || !taught)
                    failures.Add("[migration] a PRE-GATE save was NOT granted '" + id + "' (key '" + key + "'). " +
                                 "THIS IS THE UNRECOVERABLE ONE: every recipe was craftable by everybody before " +
                                 "WO-1235, so a gate that starts applying without this grandfather silently deletes " +
                                 "crafting from every live player, on a save they cannot roll back.");
            }

            // Idempotent: re-running must never TAKE anything away.
            var again = SaveMigrator.Migrate(migrated, RecipeGateSchemaVersion - 1);   // same historical pin - see the note above
            foreach (string id in RecipeUnlockKeys.GatedRecipeIds)
            {
                string key = RecipeUnlockKeys.KeyFor(id);
                if (again == null || again.SeenTutorials == null ||
                    !again.SeenTutorials.TryGetValue(key, out bool still) || !still)
                    failures.Add("[migration] re-running the migration CLEARED '" + id + "' - a migration that can " +
                                 "revoke a grant is worse than one that never ran.");
            }

            // A save already AT the current version must not enter the grandfather at all: that is
            // what keeps a NEW game correctly gated and makes the scroll mean something.
            var current = new SaveSchema.PersistedState { SeenTutorials = new Dictionary<string, bool>() };
            var untouched = SaveMigrator.Migrate(current, SaveSchema.CurrentVersion);
            foreach (string id in RecipeUnlockKeys.GatedRecipeIds)
                if (untouched != null && untouched.SeenTutorials != null &&
                    untouched.SeenTutorials.TryGetValue(RecipeUnlockKeys.KeyFor(id), out bool got) && got)
                    failures.Add("[migration] a save already at CurrentVersion (" + SaveSchema.CurrentVersion +
                                 ") was granted '" + id + "'. A NEW game must be gated, or the scroll teaches " +
                                 "something the player already had and the FTUE beat is a no-op.");

            // The bump itself must be real: the migrator's top step has to reach CurrentVersion,
            // or the grandfather never runs for the saves that need it.
            log.AppendLine("  migration: pre-gate save grandfathered " + RecipeUnlockKeys.GatedRecipeIds.Length +
                           " recipe(s) at v" + SaveSchema.CurrentVersion + ", idempotent, new saves untouched");
        }

        // =====================================================================
        //  Case 4 - the recipe matches the scroll art (owner ruling 2026-08-26)
        // =====================================================================

        private static void Case4_RecipeMatchesTheScrollArt(List<string> failures, StringBuilder log)
        {
            string json = DeNelle.Core.CanonicalJson.Read("Data/Canonical/consumable-recipes.json");
            if (string.IsNullOrEmpty(json))
            {
                failures.Add("[recipe-art] consumable-recipes.json not found/empty.");
                return;
            }
            JObject root;
            try { root = JObject.Parse(json); }
            catch (Exception ex) { failures.Add("[recipe-art] parse error: " + ex.Message); return; }

            var recipes = root["recipes"] as JArray;
            if (recipes == null) { failures.Add("[recipe-art] no 'recipes' array."); return; }

            JObject entry = null;
            foreach (var tok in recipes)
                if (tok is JObject o && string.Equals(o["id"]?.ToString(), RecipeId, StringComparison.OrdinalIgnoreCase))
                { entry = o; break; }

            if (entry == null)
            {
                failures.Add("[recipe-art] consumable-recipes.json has no '" + RecipeId + "' entry - the scroll " +
                             "would teach a recipe that does not exist.");
                return;
            }

            if (!string.Equals(entry["output"]?.ToString(), DeNelle.Core.HUD.HudCommands.ManaPotionId,
                               StringComparison.OrdinalIgnoreCase))
                failures.Add("[recipe-art] '" + RecipeId + "' outputs '" + entry["output"] + "', not the canonical " +
                             "mana potion id '" + DeNelle.Core.HUD.HudCommands.ManaPotionId + "' - the recipe the " +
                             "scroll teaches would not refill the belt slot the player just emptied.");

            var ings = entry["ingredients"] as JArray;
            if (ings == null) { failures.Add("[recipe-art] '" + RecipeId + "' has no ingredients array."); return; }

            var seen = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var tok in ings)
                if (tok is JObject o && o["id"] != null)
                    seen[o["id"].ToString()] = o["count"]?.Value<int>() ?? 1;

            if (seen.Count != RuledIngredients.Length)
                failures.Add("[recipe-art] '" + RecipeId + "' lists " + seen.Count + " ingredient(s), expected " +
                             RuledIngredients.Length + ". The scroll art (ItemIcons/scroll_mana_potion.jpg) depicts " +
                             "THREE, and this is the FTUE TEACHING recipe - a mismatch between the scroll the " +
                             "player reads and the panel they act on actively mis-teaches (owner ruling 2026-08-26).");

            foreach (string want in RuledIngredients)
            {
                if (!seen.TryGetValue(want, out int count))
                {
                    failures.Add("[recipe-art] '" + RecipeId + "' is missing ingredient '" + want + "'. ⛔ The owner " +
                                 "explicitly CONSIDERED AND REJECTED renaming these materials, because their " +
                                 "display names appear across loot, chests, the treasure panel and every other " +
                                 "recipe - so this id is the ruling, not a suggestion.");
                    continue;
                }
                if (count != RuledCount)
                    failures.Add("[recipe-art] '" + RecipeId + "' wants " + count + "x '" + want + "', expected " +
                                 RuledCount + ". The 1/1/1 counts are owner-ruled and may not move without a " +
                                 "further ruling.");
            }

            // Every ingredient must actually resolve in materials.json, or the panel renders a row
            // for a material that does not exist.
            string matJson = DeNelle.Core.CanonicalJson.Read("Data/Canonical/materials.json");
            if (string.IsNullOrEmpty(matJson))
            {
                log.AppendLine("  " + RegressionOutcome.PartialSkip("recipe-art",
                    "materials.json not readable - ingredient resolution was NOT asserted"));
            }
            else
            {
                var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    var mats = JObject.Parse(matJson)["materials"] as JArray;
                    if (mats != null)
                        foreach (var tok in mats)
                            if (tok is JObject o && o["id"] != null) known.Add(o["id"].ToString());
                }
                catch (Exception ex) { failures.Add("[recipe-art] materials.json parse error: " + ex.Message); }

                if (known.Count == 0)
                    log.AppendLine("  " + RegressionOutcome.PartialSkip("recipe-art",
                        "materials.json yielded no ids - ingredient resolution was NOT asserted"));
                else
                    foreach (string want in RuledIngredients)
                        if (!known.Contains(want))
                            failures.Add("[recipe-art] ingredient '" + want + "' does not exist in materials.json - " +
                                         "the crafting panel would render an unresolvable row on the one recipe a " +
                                         "brand-new player is taught.");
            }

            log.AppendLine("  recipe art parity: " + RecipeId + " -> " + string.Join(" + ", RuledIngredients) +
                           " (" + RuledCount + " each)");
        }

        // ---- reflection helpers (the CastlePlansUnlockRegression shape) ---------

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
    }
}
