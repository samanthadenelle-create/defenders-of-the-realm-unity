// =============================================================================
// CrystalProductionRegression [crystal-production] -- FAIL-BY-DESIGN.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Village + DeNelle.Core).
// Two proofs:
//   (1) A crystal PRODUCER yields > 0 at a reachable level -- drive a real CrystalMine
//       at max level through its OnWaveCleared tick over a real CrystalEconomy (backed
//       by a throwaway GameState) and assert the crystal balance rose. (Expected PASS.)
//   (2) The yield is DATA-DRIVEN -- buildings.json declares crystal production for a
//       producer. It does NOT: no building entry carries a production/yield/perHour
//       key; CrystalMine's +1@L3 is hard-coded in C#. So this half FAILS TRUTHFULLY
//       until the yield is moved into buildings.json, then flips green. (FAIL-BY-DESIGN.)
//
// Marker: CRYSTAL_PRODUCTION_OK / CRYSTAL_PRODUCTION_FAIL. Expected: RED (design gap 2).
//
// Wire (DataRegression.RunAll):
//   if (!CrystalProductionRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[crystal-production] " + r);
// =============================================================================
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using DeNelle.Village;
using DeNelle.Core.State;

namespace DeNelle.Editor
{
    public static class CrystalProductionRegression
    {
        private const string SaveKey = "dotr-save";

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- CRYSTAL PRODUCTION (producer yields >0 + yield is data-driven) ---");

            // (1) A real producer yields > 0 at a reachable level.
            bool hadSave = PlayerPrefs.HasKey(SaveKey);
            string rawSave = hadSave ? PlayerPrefs.GetString(SaveKey, null) : null;
            GameStateService priorGss = GameStateService.Instance;
            object priorCryst = GetInstance(typeof(CrystalEconomy));
            GameObject gssGo = null, crystGo = null, mineGo = null;
            GameState throwaway = null;
            try
            {
                throwaway = ScriptableObject.CreateInstance<GameState>();
                gssGo = new GameObject("GSS (crystal-prod oracle)");
                var gss = gssGo.AddComponent<GameStateService>();
                if (!InstallState(gss, throwaway))
                {
                    log.AppendLine("  NOTE: GameStateService state seam not reflectable -- producer-yield drive skipped");
                }
                else
                {
                    crystGo = new GameObject("CrystalEconomy (crystal-prod oracle)");
                    var cryst = crystGo.AddComponent<CrystalEconomy>();
                    SetInstance(typeof(CrystalEconomy), cryst);

                    mineGo = new GameObject("CrystalMine (crystal-prod oracle)");
                    var mine = mineGo.AddComponent<CrystalMine>();
                    // Reach max level (guaranteed +1 crystal per cleared wave).
                    var lvlField = typeof(CrystalMine).GetField("_currentLevel", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (lvlField != null) lvlField.SetValue(mine, CrystalMine.MaxLevel);
                    else failures.Add("[crystal-production] CrystalMine._currentLevel field not found -- cannot reach a yielding level");

                    var onWave = typeof(CrystalMine).GetMethod("OnWaveCleared", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (onWave == null)
                        failures.Add("[crystal-production] CrystalMine.OnWaveCleared(int) not found -- the yield tick seam is missing/renamed");
                    else
                    {
                        int before = cryst.CurrentCrystals;
                        onWave.Invoke(mine, new object[] { 1 });
                        int after = cryst.CurrentCrystals;
                        log.AppendLine($"  CrystalMine @ L{CrystalMine.MaxLevel} wave-clear: crystals {before} -> {after}");
                        if (!(after > before))
                            failures.Add($"[crystal-production] a max-level CrystalMine yielded no crystals on a cleared wave ({before}->{after})");
                    }
                }
            }
            catch (System.Exception ex)
            {
                failures.Add($"crystal-production yield drive threw: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                if (mineGo != null) Object.DestroyImmediate(mineGo);
                if (crystGo != null) Object.DestroyImmediate(crystGo);
                if (gssGo != null) Object.DestroyImmediate(gssGo);
                if (throwaway != null) Object.DestroyImmediate(throwaway);
                SetInstance(typeof(CrystalEconomy), priorCryst);
                SetGssInstance(priorGss);
                if (hadSave) PlayerPrefs.SetString(SaveKey, rawSave); else PlayerPrefs.DeleteKey(SaveKey);
                PlayerPrefs.Save();
            }

            // (2) FAIL-BY-DESIGN: buildings.json must declare crystal production for a producer.
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
            catch (System.Exception ex) { log.AppendLine("  buildings.json parse note: " + ex.Message); }

            log.AppendLine($"  buildings.json declares crystal production: {dataDriven}");
            if (!dataDriven)
                failures.Add("[crystal-production] FAIL-BY-DESIGN: NO buildings.json entry declares crystal production (production/yield/crystalsPerHour) -- CrystalMine's +1@L3 is hard-coded in C#. Move the yield into buildings.json to flip this green.");

            reason = Finish(failures, log);
            return failures.Count == 0;
        }

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

        private static FieldInfo InstanceField(System.Type t)
        {
            var f = t.GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static)
                 ?? t.GetField("<Instance>k__BackingField", BindingFlags.NonPublic | BindingFlags.Static);
            if (f != null) return f;
            foreach (var ff in t.GetFields(BindingFlags.NonPublic | BindingFlags.Static))
                if (ff.Name.Contains("Instance") && ff.FieldType == t) return ff;
            return null;
        }

        private static object GetInstance(System.Type t) { var f = InstanceField(t); return f != null ? f.GetValue(null) : null; }
        private static void SetInstance(System.Type t, object val) { var f = InstanceField(t); if (f != null) f.SetValue(null, val); }

        private static string Finish(List<string> failures, StringBuilder log)
        {
            if (failures.Count == 0)
            {
                Debug.Log(log.ToString() + "CRYSTAL_PRODUCTION_OK");
                return "CRYSTAL PRODUCTION OK -- a producer yields >0 at a reachable level AND buildings.json declares the yield";
            }
            string reason = "crystal-production: " + string.Join("; ", failures);
            Debug.LogError(log.ToString() + "CRYSTAL_PRODUCTION_FAIL: " + reason);
            return reason;
        }
    }
}
