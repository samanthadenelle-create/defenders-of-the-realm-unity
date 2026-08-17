// =============================================================================
// PackGrantRegression [pack-grant] -- proves a purchased pack DELIVERS (ECON-01/02).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. PackStoreVM / PackCatalog / PackDef live in
// DeNelle.Wallet and GlimmerCurrencyService in DeNelle.Cosmetics -- neither is
// referenced by this asmdef, so this oracle drives them by AppDomain reflection
// (the same bridge PackStoreVM itself uses). It installs the REAL EconomyService +
// GlimmerCurrencyService singletons over a throwaway GameState, then calls the REAL
// PackStoreVM.ApplyPackContents(founders-vow PackDef) and asserts the ENTIRE
// advertised entitlement landed: Glimmer +1000, Crystals/Food/Coins by the packs.json
// amounts, and all 5 cosmetic SKUs return true from GlimmerCurrencyService.Owns.
//
// Marker: PACK_GRANT_OK / PACK_GRANT_FAIL. Expected: GREEN.
//
// Wire (DataRegression.RunAll):
//   if (!PackGrantRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[pack-grant] " + r);
// =============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using DeNelle.Village;
using DeNelle.Core.State;

namespace DeNelle.Editor
{
    public static class PackGrantRegression
    {
        private const string SaveKey = "dotr-save";
        private const string CosmeticsKey = "dotr-cosmetics-v1";
        private const string PackSku = "founders-vow";

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- PACK GRANT (PackStoreVM.ApplyPackContents 'founders-vow' -> currency + cosmetic entitlement) ---");

            // Resolve the reflection-only types up front.
            Type vmType = FindType("DeNelle.Wallet.PackStoreVM");
            Type catType = FindType("DeNelle.Wallet.PackCatalog");
            Type glimType = FindType("DeNelle.Cosmetics.GlimmerCurrencyService");
            if (vmType == null || catType == null || glimType == null)
            {
                failures.Add($"pack types not loaded (PackStoreVM={vmType != null}, PackCatalog={catType != null}, GlimmerCurrencyService={glimType != null})");
                reason = Finish(failures, log);
                return failures.Count == 0;
            }

            // Expected amounts + SKUs from packs.json (the advertised contract).
            int expGlimmer = 0, expCrystals = 0, expFood = 0, expCoins = 0;
            var expSkus = new List<string>();
            if (!ReadExpected(out expGlimmer, out expCrystals, out expFood, out expCoins, expSkus, out string readErr))
            {
                failures.Add("packs.json read: " + readErr);
                reason = Finish(failures, log);
                return failures.Count == 0;
            }
            log.AppendLine($"  packs.json '{PackSku}': glimmer={expGlimmer} crystals={expCrystals} food={expFood} coins={expCoins} cosmetics={expSkus.Count}");

            bool hadSave = PlayerPrefs.HasKey(SaveKey);
            string rawSave = hadSave ? PlayerPrefs.GetString(SaveKey, null) : null;
            bool hadCos = PlayerPrefs.HasKey(CosmeticsKey);
            string rawCos = hadCos ? PlayerPrefs.GetString(CosmeticsKey, null) : null;
            PlayerPrefs.DeleteKey(CosmeticsKey);   // fresh cosmetic wallet

            GameStateService priorGss = GameStateService.Instance;
            object priorEcon = GetInstance(typeof(EconomyService));
            object priorGlim = GetInstance(glimType);

            GameObject gssGo = null, econGo = null, glimGo = null;
            GameState throwaway = null;
            try
            {
                throwaway = ScriptableObject.CreateInstance<GameState>();
                gssGo = new GameObject("GSS (pack-grant oracle)");
                var gss = gssGo.AddComponent<GameStateService>();
                if (!InstallState(gss, throwaway))
                {
                    return DeNelle.Editor.Regression.RegressionOutcome.Skip(out reason,
                        "PACK GRANT", "GameStateService state seam not reflectable (needs fleet)");
                }

                econGo = new GameObject("EconomyService (pack-grant oracle)");
                var econ = econGo.AddComponent<EconomyService>();
                SetInstance(typeof(EconomyService), econ);

                glimGo = new GameObject("GlimmerCurrencyService (pack-grant oracle)");
                var glim = glimGo.AddComponent(glimType);
                SetInstance(glimType, glim);

                var glimmerProp = glimType.GetProperty("Glimmer", BindingFlags.Public | BindingFlags.Instance);
                var ownsM = glimType.GetMethod("Owns", new[] { typeof(string) });
                if (glimmerProp == null || ownsM == null)
                { failures.Add("GlimmerCurrencyService.Glimmer/Owns not resolvable by reflection"); reason = Finish(failures, log); return false; }

                // Load the real founders-vow PackDef through the catalog.
                catType.GetMethod("Reload", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
                var findM = catType.GetMethod("Find", new[] { typeof(string) });
                object pack = findM?.Invoke(null, new object[] { PackSku });
                if (pack == null)
                { failures.Add($"PackCatalog.Find('{PackSku}') returned null -- packs.json missing the founders-vow entry"); reason = Finish(failures, log); return false; }

                // Snapshot before.
                int glimmerBefore = (int)glimmerProp.GetValue(glim);
                var resBefore = throwaway.Resources;
                int crystalsBefore = resBefore.Crystals, foodBefore = resBefore.Food, coinsBefore = resBefore.Coins;

                // Build the VM against the live (throwaway) state and APPLY the pack.
                var vm = vmType.GetMethod("CreateDefault", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
                if (vm == null) { failures.Add("PackStoreVM.CreateDefault() returned null"); reason = Finish(failures, log); return false; }
                var applyM = vmType.GetMethod("ApplyPackContents", new[] { pack.GetType() });
                if (applyM == null) { failures.Add("PackStoreVM.ApplyPackContents(PackDef) not resolvable by reflection"); reason = Finish(failures, log); return false; }
                applyM.Invoke(vm, new[] { pack });

                // Snapshot after + assert every advertised delta.
                int glimmerAfter = (int)glimmerProp.GetValue(glim);
                var resAfter = throwaway.Resources;
                int glimmerDelta = glimmerAfter - glimmerBefore;
                int crystalsDelta = resAfter.Crystals - crystalsBefore;
                int foodDelta = resAfter.Food - foodBefore;
                int coinsDelta = resAfter.Coins - coinsBefore;
                log.AppendLine($"  granted deltas: glimmer=+{glimmerDelta} crystals=+{crystalsDelta} food=+{foodDelta} coins=+{coinsDelta}");

                if (glimmerDelta != expGlimmer) failures.Add($"[pack-grant] Glimmer delta {glimmerDelta} != advertised {expGlimmer}");
                if (crystalsDelta != expCrystals) failures.Add($"[pack-grant] Crystals delta {crystalsDelta} != advertised {expCrystals}");
                if (foodDelta != expFood) failures.Add($"[pack-grant] Food delta {foodDelta} != advertised {expFood}");
                if (coinsDelta != expCoins) failures.Add($"[pack-grant] Coins delta {coinsDelta} != advertised {expCoins}");

                int ownedOk = 0;
                foreach (var sku in expSkus)
                {
                    bool owned = (bool)ownsM.Invoke(glim, new object[] { sku });
                    if (owned) ownedOk++;
                    else failures.Add($"[pack-grant] cosmetic SKU '{sku}' NOT owned after grant (GlimmerCurrencyService.Owns false -- unequippable, ECON-02)");
                }
                log.AppendLine($"  cosmetics owned {ownedOk}/{expSkus.Count}");
            }
            catch (Exception ex)
            {
                failures.Add($"pack-grant oracle threw: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                if (econGo != null) UnityEngine.Object.DestroyImmediate(econGo);
                if (glimGo != null) UnityEngine.Object.DestroyImmediate(glimGo);
                if (gssGo != null) UnityEngine.Object.DestroyImmediate(gssGo);
                if (throwaway != null) UnityEngine.Object.DestroyImmediate(throwaway);
                SetInstance(typeof(EconomyService), priorEcon);
                SetInstance(glimType, priorGlim);
                SetGssInstance(priorGss);
                if (hadSave) PlayerPrefs.SetString(SaveKey, rawSave); else PlayerPrefs.DeleteKey(SaveKey);
                if (hadCos) PlayerPrefs.SetString(CosmeticsKey, rawCos); else PlayerPrefs.DeleteKey(CosmeticsKey);
                PlayerPrefs.Save();
            }

            reason = Finish(failures, log);
            return failures.Count == 0;
        }

        private static bool ReadExpected(out int glimmer, out int crystals, out int food, out int coins,
                                         List<string> skus, out string err)
        {
            glimmer = crystals = food = coins = 0; err = null;
            string json = DeNelle.Core.CanonicalJson.Read("Data/Canonical/packs.json");
            if (string.IsNullOrEmpty(json)) { err = "packs.json not found/empty"; return false; }
            JObject root;
            try { root = JObject.Parse(json); } catch (Exception ex) { err = "parse error: " + ex.Message; return false; }
            var packs = root["packs"] as JArray;
            if (packs == null) { err = "no 'packs' array"; return false; }
            foreach (var tok in packs)
            {
                if (!(tok is JObject o) || o["sku"]?.ToString() != PackSku) continue;
                var econ = o["contents"]?["economy"] as JObject;
                if (econ != null)
                {
                    glimmer = econ["glimmer"]?.Value<int>() ?? 0;
                    crystals = econ["crystals"]?.Value<int>() ?? 0;
                    food = econ["food"]?.Value<int>() ?? 0;
                    coins = econ["coins"]?.Value<int>() ?? 0;
                }
                var cos = o["contents"]?["cosmetics"] as JArray;
                if (cos != null) foreach (var c in cos) { string s = c?.ToString(); if (!string.IsNullOrEmpty(s)) skus.Add(s); }
                return true;
            }
            err = $"packs.json has no '{PackSku}' entry";
            return false;
        }

        // ---- reflection helpers -------------------------------------------------
        private static Type FindType(string full)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(full, false);
                if (t != null) return t;
            }
            return null;
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
                Debug.Log(log.ToString() + "PACK_GRANT_OK");
                return "PACK GRANT OK -- founders-vow granted Glimmer + Crystals/Food/Coins by the advertised amounts and all cosmetic SKUs are owned";
            }
            string reason = "pack-grant: " + string.Join("; ", failures);
            Debug.LogError(log.ToString() + "PACK_GRANT_FAIL: " + reason);
            return reason;
        }
    }
}
