// =============================================================================
// PackCosmeticIntegrityRegression [pack-cosmetic-integrity] -- the audit P1 ECON-1
// integrity oracle: EVERY advertised pack cosmetic must be GRANTABLE (ECON-1).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. PackStoreVM / PackCatalog / PackDef live in
// DeNelle.Wallet and CosmeticOwnershipService in DeNelle.Cosmetics -- neither asmdef
// is referenced here, so this oracle drives them by AppDomain reflection (the same
// bridge PackStoreVM itself uses and PackGrantRegression proved out for founders-vow).
//
// ECON-1 GAP: a paid pack advertises cosmetic SKUs (contents.cosmetics[]). If any
// one of those SKUs does not end up OWNED after the grant path runs, the pack sells
// a cosmetic it cannot deliver -- a dangling / unredeemable SKU. PackGrantRegression
// proves this for the 5 founders-vow SKUs only. This oracle GENERALIZES that to a
// hard integrity guard over EVERY pack in the catalog: it installs the REAL
// EconomyService + CosmeticOwnershipService singletons over a throwaway GameState,
// then for every pack calls the REAL PackStoreVM.ApplyPackContents(pack) and asserts
// CosmeticOwnershipService.Owns(sku)==true for each advertised cosmetic SKU. A cosmetic
// that stays unowned after its pack's grant = RED.
//
// This deliberately does NOT require the SKU to exist in cosmetics.json: founders-vow
// (and other pack-exclusive) SKUs are pack-only entitlements, granted catalog-
// independently via CosmeticOwnershipService.MarkCosmeticOwned. The contract is
// "advertised => grantable/owned", not "in cosmetics.json".
//
// Marker: PACK_COSMETIC_INTEGRITY_OK / PACK_COSMETIC_INTEGRITY_FAIL. Expected: GREEN.
//
// Wire (DataRegression.RunAll):
//   if (!PackCosmeticIntegrityRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[pack-cosmetic-integrity] " + r);
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
    public static class PackCosmeticIntegrityRegression
    {
        private const string SaveKey = "dotr-save";
        private const string CosmeticsKey = "dotr-cosmetics-v1";

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- PACK COSMETIC INTEGRITY (every pack: advertised cosmetic SKU => CosmeticOwnershipService.Owns after ApplyPackContents) ---");

            // Resolve the reflection-only types up front.
            Type vmType = FindType("DeNelle.Wallet.PackStoreVM");
            Type catType = FindType("DeNelle.Wallet.PackCatalog");
            Type glimType = FindType("DeNelle.Cosmetics.CosmeticOwnershipService");
            if (vmType == null || catType == null || glimType == null)
            {
                failures.Add($"pack types not loaded (PackStoreVM={vmType != null}, PackCatalog={catType != null}, CosmeticOwnershipService={glimType != null})");
                reason = Finish(failures, log);
                return failures.Count == 0;
            }

            // The advertised contract, straight from packs.json: pack SKU -> its cosmetics[].
            var advertised = new List<KeyValuePair<string, List<string>>>();
            if (!ReadAdvertised(advertised, out string readErr))
            {
                failures.Add("packs.json read: " + readErr);
                reason = Finish(failures, log);
                return failures.Count == 0;
            }
            log.AppendLine($"  packs.json: {advertised.Count} packs enumerated");

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
                gssGo = new GameObject("GSS (pack-cosmetic-integrity oracle)");
                var gss = gssGo.AddComponent<GameStateService>();
                if (!InstallState(gss, throwaway))
                {
                    return DeNelle.Editor.Regression.RegressionOutcome.Skip(out reason,
                        "PACK COSMETIC INTEGRITY", "GameStateService state seam not reflectable (needs fleet)");
                }

                econGo = new GameObject("EconomyService (pack-cosmetic-integrity oracle)");
                var econ = econGo.AddComponent<EconomyService>();
                SetInstance(typeof(EconomyService), econ);

                glimGo = new GameObject("CosmeticOwnershipService (pack-cosmetic-integrity oracle)");
                var glim = glimGo.AddComponent(glimType);
                SetInstance(glimType, glim);

                var ownsM = glimType.GetMethod("Owns", new[] { typeof(string) });
                if (ownsM == null)
                { failures.Add("CosmeticOwnershipService.Owns(string) not resolvable by reflection"); reason = Finish(failures, log); return false; }

                // Reload the real catalog + resolve the production grant seam.
                catType.GetMethod("Reload", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
                var findM = catType.GetMethod("Find", new[] { typeof(string) });
                if (findM == null)
                { failures.Add("PackCatalog.Find(string) not resolvable by reflection"); reason = Finish(failures, log); return false; }

                var vm = vmType.GetMethod("CreateDefault", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
                if (vm == null) { failures.Add("PackStoreVM.CreateDefault() returned null"); reason = Finish(failures, log); return false; }
                MethodInfo applyM = null;
                {
                    var packDefType = FindType("DeNelle.Wallet.PackDef");
                    if (packDefType != null) applyM = vmType.GetMethod("ApplyPackContents", new[] { packDefType });
                }
                if (applyM == null) { failures.Add("PackStoreVM.ApplyPackContents(PackDef) not resolvable by reflection"); reason = Finish(failures, log); return false; }

                int packsChecked = 0, skusChecked = 0, skusOwned = 0;
                foreach (var entry in advertised)
                {
                    string packSku = entry.Key;
                    var cosmetics = entry.Value;

                    object pack = findM.Invoke(null, new object[] { packSku });
                    if (pack == null)
                    {
                        failures.Add($"[pack-cosmetic-integrity] pack '{packSku}' advertised in packs.json but PackCatalog.Find returned null -- unloadable pack");
                        continue;
                    }

                    // Drive the REAL production grant path for this pack.
                    applyM.Invoke(vm, new[] { pack });
                    packsChecked++;

                    if (cosmetics == null || cosmetics.Count == 0)
                    {
                        log.AppendLine($"  '{packSku}': no advertised cosmetics (nothing to grant)");
                        continue;
                    }

                    int ownedThisPack = 0;
                    foreach (var sku in cosmetics)
                    {
                        if (string.IsNullOrEmpty(sku)) continue;
                        skusChecked++;
                        bool owned = false;
                        var r = ownsM.Invoke(glim, new object[] { sku });
                        if (r is bool b) owned = b;
                        if (owned) { skusOwned++; ownedThisPack++; }
                        else failures.Add($"[pack-cosmetic-integrity] pack '{packSku}' advertises cosmetic '{sku}' but CosmeticOwnershipService.Owns==false after ApplyPackContents -- DANGLING/UNREDEEMABLE SKU (ECON-1)");
                    }
                    log.AppendLine($"  '{packSku}': cosmetics owned {ownedThisPack}/{cosmetics.Count}");
                }

                log.AppendLine($"  totals: packs applied {packsChecked}, cosmetic SKUs owned {skusOwned}/{skusChecked}");
            }
            catch (Exception ex)
            {
                failures.Add($"pack-cosmetic-integrity oracle threw: {ex.GetType().Name}: {ex.Message}");
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

        /// <summary>Reads every pack's advertised cosmetic SKUs straight from packs.json (the shipped contract).</summary>
        private static bool ReadAdvertised(List<KeyValuePair<string, List<string>>> advertised, out string err)
        {
            err = null;
            string json = DeNelle.Core.CanonicalJson.Read("Data/Canonical/packs.json");
            if (string.IsNullOrEmpty(json)) { err = "packs.json not found/empty"; return false; }
            JObject root;
            try { root = JObject.Parse(json); } catch (Exception ex) { err = "parse error: " + ex.Message; return false; }
            var packs = root["packs"] as JArray;
            if (packs == null) { err = "no 'packs' array"; return false; }
            foreach (var tok in packs)
            {
                if (!(tok is JObject o)) continue;
                string sku = o["sku"]?.ToString();
                if (string.IsNullOrEmpty(sku)) continue;
                var list = new List<string>();
                var cos = o["contents"]?["cosmetics"] as JArray;
                if (cos != null)
                    foreach (var c in cos) { string s = c?.ToString(); if (!string.IsNullOrEmpty(s)) list.Add(s); }
                advertised.Add(new KeyValuePair<string, List<string>>(sku, list));
            }
            if (advertised.Count == 0) { err = "packs array has no usable pack entries"; return false; }
            return true;
        }

        // ---- reflection helpers (mirrors PackGrantRegression) -------------------
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
                Debug.Log(log.ToString() + "PACK_COSMETIC_INTEGRITY_OK");
                return "PACK COSMETIC INTEGRITY OK -- every pack's advertised cosmetic SKU is owned after ApplyPackContents (no dangling/unredeemable cosmetics)";
            }
            string reason = "pack-cosmetic-integrity: " + string.Join("; ", failures);
            Debug.LogError(log.ToString() + "PACK_COSMETIC_INTEGRITY_FAIL: " + reason);
            return reason;
        }
    }
}
