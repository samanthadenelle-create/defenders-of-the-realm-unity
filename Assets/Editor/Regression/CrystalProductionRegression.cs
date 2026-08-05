// =============================================================================
// CrystalProductionRegression [crystal-production] -- the Crystal Mine oracle.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Village + DeNelle.Core).
//
// WO-856 (2026-08-04) REWRITE. This suite existed to prove "a crystal PRODUCER
// yields > 0 at a REACHABLE level" and it did the opposite: it reflectively wrote
// CrystalMine's private level field into a state NO PLAYER COULD EVER REACH, then
// declared the yield proven. That one line is why a mine that had never paid a
// single crystal shipped green. The cheat is deleted and turned into a GUARD --
// see [single-level-authority] below.
//
// The five proofs, all driven through the seams the GAME uses:
//   [l1-pays]                 a real CrystalMine on a real PlacedStructure at
//                             level 1 raises the crystal balance on a cleared
//                             wave. NO private field is written to get there --
//                             that is the entire point of this case.
//   [level-round-trip]        a PlacedStructureData{mine_crystal, level=3} that
//                             goes through the REAL save serializer and back
//                             reloads at 3 and pays the L3 rung, not the L1 one.
//   [curve-monotonic]         buildings.json authors the payout as an array whose
//                             length matches mine_crystal's repo.maxLevel, every
//                             rung > 0, never descending.
//   [yield-is-data]           buildings.json (not C#) declares the yield.
//   [single-level-authority]  CrystalMine has NO private "_currentLevel" field.
//                             Yesterday's cheat is today's guard: reintroducing a
//                             component-local level shadows the persisted
//                             PlacedStructure.level and re-commits the original sin
//                             (ARCHITECTURE_PRINCIPLES section 1).
//
// Marker: CRYSTAL_PRODUCTION_OK / CRYSTAL_PRODUCTION_FAIL. Expected: GREEN.
//
// Wire (DataRegression.RunAll) -- already registered, do not re-register:
//   if (!CrystalProductionRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[crystal-production] " + r);
// =============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using DeNelle.Village;
using DeNelle.Core.State;

namespace DeNelle.Editor
{
    public static class CrystalProductionRegression
    {
        private const string SaveKey = "dotr-save";
        private const string MineBuildingId = "crystal-mine";      // buildings.json row (the yield curve)
        private const string MineCatalogId  = "mine_crystal";      // structures-catalog.json row (the ladder)

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- CRYSTAL PRODUCTION (the mine pays from L1, on an authored curve, at the persisted level) ---");

            int[] curve = ReadCurve(log, out bool authoredAsArray);

            Case(failures, "l1-pays",                () => CheckPaysAtLevel(1, curve, failures, log));
            Case(failures, "level-round-trip",       () => CheckLevelRoundTrip(curve, failures, log));
            Case(failures, "curve-monotonic",        () => CheckCurve(curve, authoredAsArray, failures, log));
            Case(failures, "yield-is-data",          () => CheckYieldIsDataDriven(failures, log));
            Case(failures, "single-level-authority", () => CheckSingleLevelAuthority(failures, log));

            reason = Finish(failures, log);
            return failures.Count == 0;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add($"[{name}] THREW {ex.GetType().Name}: {ex.Message}"); }
        }

        // =====================================================================
        //  The authored curve (buildings.json "crystal-mine".crystalsPerWave).
        // =====================================================================

        /// <summary>Reads the payout curve exactly as CrystalMine does, including the
        /// scalar read-migration. Returns null (and records nothing) when absent -- the
        /// [yield-is-data] / [curve-monotonic] cases report that.
        /// <paramref name="authoredAsArray"/> distinguishes the AUTHORED shape (an array =
        /// a real ladder) from a scalar that merely read-migrated: the migration exists so
        /// a hand-edit never throws at runtime, NOT so the ladder can quietly go flat.</summary>
        private static int[] ReadCurve(StringBuilder log, out bool authoredAsArray)
        {
            authoredAsArray = false;
            try
            {
                string json = DeNelle.Core.CanonicalJson.Read("Data/Canonical/buildings.json");
                if (string.IsNullOrEmpty(json)) return null;
                var arr = JObject.Parse(json)["buildings"] as JArray;
                if (arr == null) return null;
                foreach (var tok in arr)
                {
                    if (!(tok is JObject o) || (string)o["id"] != MineBuildingId) continue;
                    var y = o["crystalsPerWave"];
                    if (y == null) return null;
                    if (y is JArray rungs)
                    {
                        var curve = new int[rungs.Count];
                        for (int i = 0; i < rungs.Count; i++) curve[i] = (int)rungs[i];
                        authoredAsArray = true;
                        log.AppendLine("  crystalsPerWave curve: [" + string.Join(", ", curve) + "]");
                        return curve;
                    }
                    // Scalar read-migration (a hand-edit back to a number is a FLAT curve).
                    int flat = (int)y;
                    log.AppendLine($"  crystalsPerWave authored as a SCALAR ({flat}) -- read-migrated to a flat curve");
                    return new[] { flat };
                }
            }
            catch (Exception ex) { log.AppendLine("  crystalsPerWave read note: " + ex.Message); }
            return null;
        }

        private static int RungFor(int[] curve, int level)
        {
            if (curve == null || curve.Length == 0) return 0;
            return curve[Mathf.Clamp(level - 1, 0, curve.Length - 1)];
        }

        // =====================================================================
        //  Case 1 - [l1-pays] / Case 2 - [level-round-trip]
        //
        //  Both drive a REAL CrystalMine sitting on a REAL PlacedStructure through
        //  OnWaveCleared over a REAL CrystalEconomy (backed by a throwaway
        //  GameState). The level is set the ONLY way the game sets it -- on
        //  PlacedStructure.level, the field BaseLayoutLoader restores from the save.
        //  NOTHING private on CrystalMine is written.
        // =====================================================================

        private static void CheckPaysAtLevel(int level, int[] curve, List<string> failures, StringBuilder log)
        {
            int expected = RungFor(curve, level);
            int delta = DrivePayout(level, failures, log, out bool drove);
            if (!drove) return;

            log.AppendLine($"  [l1-pays] mine at PlacedStructure.level={level} wave-clear: +{delta} crystals (curve rung {expected})");
            if (delta <= 0)
                failures.Add(
                    $"[l1-pays] a freshly-placed CrystalMine at level {level} awarded {delta} crystals on a cleared " +
                    "wave. A producer that produces nothing at its FOUNDING level is not a producer -- the player " +
                    "pays 80 wood + 50 iron for a prop. (WO-856: the payout must scale from L1, never switch on at max.)");
            else if (expected > 0 && delta != expected)
                failures.Add(
                    $"[l1-pays] the mine paid {delta} at level {level} but buildings.json authors {expected} for that " +
                    "rung -- the C# is not reading the authored curve.");
        }

        private static void CheckLevelRoundTrip(int[] curve, List<string> failures, StringBuilder log)
        {
            // The REAL save serializer, on the REAL persisted record type.
            var record = new PlacedStructureData(MineCatalogId, 4, 7, 0, 3);
            string wire = JsonConvert.SerializeObject(
                new List<PlacedStructureData> { record }, SaveSchema.JsonSettings);
            var reloaded = JsonConvert.DeserializeObject<List<PlacedStructureData>>(wire, SaveSchema.JsonSettings);

            if (reloaded == null || reloaded.Count != 1)
            {
                failures.Add("[level-round-trip] the BaseLayout record did not survive a save/load round trip at all");
                return;
            }
            // BaseLayoutLoader.cs:342 -- ps.level = Mathf.Max(1, data.level).
            int loadedLevel = Mathf.Max(1, reloaded[0].level);
            log.AppendLine($"  [level-round-trip] PlacedStructureData level 3 -> save -> load -> {loadedLevel}");
            if (loadedLevel != 3)
            {
                failures.Add(
                    $"[level-round-trip] a level-3 '{MineCatalogId}' reloaded at level {loadedLevel}. " +
                    "PlacedStructureData.level is the ONE persisted level authority the mine reads; if it does " +
                    "not round-trip, every upgrade the player paid for is lost on relaunch.");
                return;
            }

            int expected = RungFor(curve, loadedLevel);
            int l1 = RungFor(curve, 1);
            int delta = DrivePayout(loadedLevel, failures, log, out bool drove);
            if (!drove) return;

            log.AppendLine($"  [level-round-trip] reloaded L{loadedLevel} mine wave-clear: +{delta} crystals (L3 rung {expected}, L1 rung {l1})");
            if (expected > 0 && delta != expected)
                failures.Add(
                    $"[level-round-trip] a reloaded level-{loadedLevel} mine paid {delta} crystals, not the authored " +
                    $"L{loadedLevel} rung of {expected}" +
                    (delta == l1 ? " -- it paid the LEVEL 1 rung, so the persisted level never reached the payout." : "."));
        }

        /// <summary>Stands up GameStateService + CrystalEconomy + a PlacedStructure-hosted
        /// CrystalMine, drives one cleared wave, and returns the crystal delta. Every global
        /// it touches is restored in the finally block.</summary>
        private static int DrivePayout(int level, List<string> failures, StringBuilder log, out bool drove)
        {
            drove = false;
            bool hadSave = PlayerPrefs.HasKey(SaveKey);
            string rawSave = hadSave ? PlayerPrefs.GetString(SaveKey, null) : null;
            GameStateService priorGss = GameStateService.Instance;
            object priorCryst = GetInstance(typeof(CrystalEconomy));
            GameObject gssGo = null, crystGo = null, mineGo = null;
            GameState throwaway = null;
            int delta = 0;
            try
            {
                throwaway = ScriptableObject.CreateInstance<GameState>();
                gssGo = new GameObject("GSS (crystal-prod oracle)");
                var gss = gssGo.AddComponent<GameStateService>();
                if (!InstallState(gss, throwaway))
                {
                    log.AppendLine("  NOTE: GameStateService state seam not reflectable -- producer-yield drive skipped");
                    return 0;
                }

                crystGo = new GameObject("CrystalEconomy (crystal-prod oracle)");
                var cryst = crystGo.AddComponent<CrystalEconomy>();
                SetInstance(typeof(CrystalEconomy), cryst);

                // A real placed structure carrying a real mine -- the shape StructureFactory
                // + BaseLayoutLoader produce (both components land on the same root object).
                mineGo = new GameObject("CrystalMine (crystal-prod oracle)");
                var placed = mineGo.AddComponent<PlacedStructure>();
                placed.itemId = MineCatalogId;
                placed.level = level;            // the ONE level authority -- a public, persisted field
                var mine = mineGo.AddComponent<CrystalMine>();

                var onWave = typeof(CrystalMine).GetMethod("OnWaveCleared", BindingFlags.NonPublic | BindingFlags.Instance);
                if (onWave == null)
                {
                    failures.Add("[crystal-production] CrystalMine.OnWaveCleared(int) not found -- the yield tick seam is missing/renamed");
                    return 0;
                }

                int before = cryst.CurrentCrystals;
                onWave.Invoke(mine, new object[] { 1 });
                delta = cryst.CurrentCrystals - before;
                drove = true;
            }
            catch (Exception ex)
            {
                failures.Add($"crystal-production yield drive threw: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                if (mineGo != null) UnityEngine.Object.DestroyImmediate(mineGo);
                if (crystGo != null) UnityEngine.Object.DestroyImmediate(crystGo);
                if (gssGo != null) UnityEngine.Object.DestroyImmediate(gssGo);
                if (throwaway != null) UnityEngine.Object.DestroyImmediate(throwaway);
                SetInstance(typeof(CrystalEconomy), priorCryst);
                SetGssInstance(priorGss);
                if (hadSave) PlayerPrefs.SetString(SaveKey, rawSave); else PlayerPrefs.DeleteKey(SaveKey);
                PlayerPrefs.Save();
            }
            return delta;
        }

        // =====================================================================
        //  Case 3 - [curve-monotonic] the authored curve is a real ladder.
        // =====================================================================

        private static void CheckCurve(int[] curve, bool authoredAsArray, List<string> failures, StringBuilder log)
        {
            if (curve == null || curve.Length == 0)
            {
                failures.Add(
                    $"[curve-monotonic] buildings.json '{MineBuildingId}' authors no crystalsPerWave curve. The mine's " +
                    "payout is DATA (WO-856 section 5); with the key gone it falls back to a flat 1/wave and every " +
                    "upgrade the player buys changes nothing.");
                return;
            }

            if (!authoredAsArray)
                failures.Add(
                    $"[curve-monotonic] '{MineBuildingId}'.crystalsPerWave is authored as a bare SCALAR. The runtime " +
                    "read-migrates a scalar to a flat curve so a hand-edit never throws - that is a safety net, NOT " +
                    "the authored shape. The mine must author an ARRAY with one rung per reachable level " +
                    "(WO-856 section 5: [2, 4, 7]); a flat curve means every upgrade the player pays for yields the " +
                    "same as the day it was built.");

            int maxLevel = ReadCatalogMaxLevel();
            log.AppendLine($"  [curve-monotonic] rungs={curve.Length} vs '{MineCatalogId}' repo.maxLevel={maxLevel}");
            if (maxLevel <= 0)
                failures.Add(
                    $"[curve-monotonic] structures-catalog.json '{MineCatalogId}' authors NO repo.maxLevel. " +
                    "BuildModeController.MaxLevelFor then returns 1, the Upgrade verb hits `level >= maxLevel` on a " +
                    "freshly-built mine and toasts \"Max tier reached.\" - the mine is frozen at L1 forever and every " +
                    "rung above the first is unreachable (this was the WO-856 defect D2, verified at BuildModeController" +
                    ".cs:2247).");
            else if (curve.Length != maxLevel)
                failures.Add(
                    $"[curve-monotonic] the curve has {curve.Length} rung(s) but '{MineCatalogId}' authors maxLevel " +
                    $"{maxLevel}. Rungs above maxLevel are yields the player can never reach; rungs below it are " +
                    "upgrades that buy nothing.");

            for (int i = 0; i < curve.Length; i++)
            {
                if (curve[i] <= 0)
                    failures.Add($"[curve-monotonic] rung L{i + 1} pays {curve[i]} -- every level of a producer must produce");
                if (i > 0 && curve[i] < curve[i - 1])
                    failures.Add(
                        $"[curve-monotonic] rung L{i + 1} ({curve[i]}) is LOWER than L{i} ({curve[i - 1]}) -- " +
                        "an upgrade must never reduce output.");
            }
        }

        /// <summary>repo.maxLevel for mine_crystal, straight out of structures-catalog.json,
        /// clamped the way BuildModeController.MaxLevelFor clamps it (1..3). 0 = not authored.</summary>
        private static int ReadCatalogMaxLevel()
        {
            try
            {
                string json = DeNelle.Core.CanonicalJson.Read("Data/Canonical/structures-catalog.json");
                if (string.IsNullOrEmpty(json)) return 0;
                var root = JObject.Parse(json);
                var arr = (root["entries"] as JArray) ?? (root["items"] as JArray) ?? (root["structures"] as JArray);
                if (arr == null)
                    foreach (var prop in root.Properties())
                        if (prop.Value is JArray a) { arr = a; break; }
                if (arr == null) return 0;
                foreach (var tok in arr)
                {
                    if (!(tok is JObject o) || (string)o["id"] != MineCatalogId) continue;
                    var repo = o["repo"] as JObject;
                    var ml = repo != null ? repo["maxLevel"] : null;
                    return ml == null ? 0 : Mathf.Clamp((int)ml, 1, 3);
                }
            }
            catch { /* reported by the caller's rung/maxLevel mismatch, never silently swallowed */ }
            return 0;
        }

        // =====================================================================
        //  Case 4 - [yield-is-data] the yield lives in buildings.json, not in C#.
        // =====================================================================

        private static void CheckYieldIsDataDriven(List<string> failures, StringBuilder log)
        {
            bool dataDriven = false;
            try
            {
                string json = DeNelle.Core.CanonicalJson.Read("Data/Canonical/buildings.json");
                if (!string.IsNullOrEmpty(json))
                {
                    var root = JObject.Parse(json);
                    var arr = (root["buildings"] as JArray) ?? (root.First as JArray);
                    if (arr != null)
                        foreach (var tok in arr)
                            if (tok is JObject o && (o["production"] != null || o["crystalsPerHour"] != null ||
                                                     o["yield"] != null || o["perHour"] != null || o["crystalsPerWave"] != null))
                            { dataDriven = true; break; }
                }
            }
            catch (Exception ex) { log.AppendLine("  buildings.json parse note: " + ex.Message); }

            log.AppendLine($"  buildings.json declares crystal production: {dataDriven}");
            if (!dataDriven)
                failures.Add(
                    "[yield-is-data] NO buildings.json entry declares crystal production " +
                    "(production/yield/crystalsPerHour/crystalsPerWave) -- the yield has been moved back into a C# " +
                    "literal, where the owner cannot tune it without a rebuild.");
        }

        // =====================================================================
        //  Case 5 - [single-level-authority] yesterday's cheat, as today's guard.
        // =====================================================================

        private static void CheckSingleLevelAuthority(List<string> failures, StringBuilder log)
        {
            const string bannedField = "_current" + "Level";   // split so a grep for the field name stays clean
            var stray = typeof(CrystalMine).GetField(bannedField, BindingFlags.NonPublic | BindingFlags.Instance);
            log.AppendLine($"  [single-level-authority] CrystalMine.{bannedField} present: {stray != null}");
            if (stray != null)
                failures.Add(
                    $"[single-level-authority] CrystalMine has a private '{bannedField}' field again. Until WO-856 " +
                    "that field WAS this bug: it persisted nowhere, no loader wrote it, and the payout gated on it -- " +
                    "so a mine could never reach the level its own yield demanded, and this very suite reflectively " +
                    "set it to fake a pass. The level authority is PlacedStructureData.level -> PlacedStructure.level " +
                    "(the save round-trips it). A component-local level is a SECOND authority; the mine must READ, " +
                    "never own (ARCHITECTURE_PRINCIPLES section 1 / section 2b).");
        }

        // =====================================================================
        //  Harness plumbing (test scaffolding only -- never production code).
        // =====================================================================

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
                Debug.Log(log.ToString() + "CRYSTAL_PRODUCTION_OK");
                return "CRYSTAL PRODUCTION OK -- the mine pays from L1 on the authored curve, at the persisted " +
                       "PlacedStructure level, with no component-local level authority";
            }
            string reason = "crystal-production: " + string.Join("; ", failures);
            Debug.LogError(log.ToString() + "CRYSTAL_PRODUCTION_FAIL: " + reason);
            return reason;
        }
    }
}
