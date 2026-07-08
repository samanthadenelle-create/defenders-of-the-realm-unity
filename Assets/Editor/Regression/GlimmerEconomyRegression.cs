// =============================================================================
// GlimmerEconomyRegression — headless "real object in, real response out" gate for
// the Glimmer soft-currency economy (the highest-risk debit-and-grant flow) plus
// the pet active-slot persistence invariant.
// -----------------------------------------------------------------------------
// Mirrors MonetizationCovenantRegression's contract (DeNelle.Editor, public static
// bool Run(out string reason), true=pass+summary / false=fail+detail) so
// DataRegression.RunAll registers it with the same one-liner.
//
// WHY REFLECTION: GlimmerCurrencyService lives in the DeNelle.Cosmetics assembly,
// which the editor-regression asmdef does NOT reference (same reason the Pets/
// Cosmetics/Wallet modules bridge into each other by reflection). So this oracle
// drives the REAL, unmodified GlimmerCurrencyService via AppDomain type lookup +
// reflected member invokes — the actual game code path, on a throwaway GameObject,
// with the PlayerPrefs blob preserved + restored.
//
// SECTION 1 — GLIMMER PURCHASE ROUND-TRIP (expected PASS):
//   Grant N glimmer, TryPurchase a real buyable cosmetic, and assert the invariant
//   the whole monetization covenant rests on:  spend N  ⇒  balance −N  AND  owned +1.
//   Then assert a repeat purchase is refused WITHOUT a second debit (the debit-
//   without-grant catastrophe: the player pays and gets nothing). Then a SpendGlimmer
//   round-trip + an overspend that must not mutate the balance.
//
// SECTION 2 — PET ACTIVE-SLOT PERSISTENCE (FAIL-BY-DESIGN, flag_17):
//   PetAcquisitionService assigns owned pets to deploy slots at runtime, but the
//   assignment is NOT persisted — only StarterPetId's slot is auto-restored on load
//   (its own header FLAG + catalog flag_17). This oracle asserts the persisted
//   GameState carries a pet active-slot field; it does NOT, so this section FAILS
//   TRUTHFULLY today and flips green the moment a save field is added. (GameState is
//   in DeNelle.Core.State — directly referenceable — so this is a pure reflection
//   scan of the persisted schema, no PlayMode.)
// =============================================================================
using System;
using System.Collections;
using System.Reflection;
using Newtonsoft.Json.Linq;
using UnityEngine;
using DeNelle.Core.State;

namespace DeNelle.Editor
{
    public static class GlimmerEconomyRegression
    {
        public static bool Run(out string reason)
        {
            var failures = new System.Collections.Generic.List<string>();
            var notes = new System.Collections.Generic.List<string>();

            CheckPurchaseRoundTrip(failures, notes);
            CheckPetSlotPersistence(failures, notes);

            if (failures.Count == 0)
            {
                reason = "GLIMMER ECONOMY OK — purchase round-trip (spend N ⇒ balance −N, owned +1) + pet-slot persistence hold" +
                         (notes.Count > 0 ? $" [notes: {string.Join("; ", notes)}]" : "");
                return true;
            }

            reason = $"GLIMMER ECONOMY FAIL x{failures.Count}: " + string.Join(" | ", failures) +
                     (notes.Count > 0 ? $" [notes: {string.Join("; ", notes)}]" : "");
            return false;
        }

        // =====================================================================
        //  SECTION 1 — Glimmer purchase round-trip on the REAL service (reflection)
        // =====================================================================
        private static void CheckPurchaseRoundTrip(System.Collections.Generic.List<string> failures,
                                                   System.Collections.Generic.List<string> notes)
        {
            Type t = FindType("DeNelle.Cosmetics.GlimmerCurrencyService");
            if (t == null)
            {
                failures.Add("GlimmerCurrencyService type not found in the AppDomain (DeNelle.Cosmetics not compiled?)");
                return;
            }

            // Resolve the members we drive.
            var prefKeyField = t.GetField("PrefKey", BindingFlags.Public | BindingFlags.Static);
            var startField   = t.GetField("StartingGlimmer", BindingFlags.Public | BindingFlags.Static);
            var glimmerProp  = t.GetProperty("Glimmer", BindingFlags.Public | BindingFlags.Instance);
            var ownedProp    = t.GetProperty("OwnedCosmetics", BindingFlags.Public | BindingFlags.Instance);
            var tryAdd       = t.GetMethod("TryAddGlimmer", new[] { typeof(int) });
            var tryPurchase  = t.GetMethod("TryPurchase", new[] { typeof(string) });
            var spend        = t.GetMethod("SpendGlimmer", new[] { typeof(int) });
            var owns         = t.GetMethod("Owns", new[] { typeof(string) });

            if (glimmerProp == null || tryAdd == null || tryPurchase == null || spend == null || owns == null || prefKeyField == null)
            {
                failures.Add("GlimmerCurrencyService public API changed — reflection could not resolve Glimmer/TryAddGlimmer/TryPurchase/SpendGlimmer/Owns/PrefKey");
                return;
            }

            string prefKey = (string)prefKeyField.GetValue(null);
            int startingGlimmer = startField != null ? (int)startField.GetValue(null) : 25;

            // Pick a real BUYABLE cosmetic (unlockMethod 'buy', cost > 0) from the catalog.
            if (!TryPickBuyable(out string buyId, out int buyCost, out string pickErr))
            {
                failures.Add($"purchase round-trip: could not pick a buyable cosmetic ({pickErr})");
                return;
            }

            // Preserve + reset the persisted blob so we start from a known fresh wallet.
            bool hadPref = PlayerPrefs.HasKey(prefKey);
            string prevPref = hadPref ? PlayerPrefs.GetString(prefKey) : null;
            PlayerPrefs.DeleteKey(prefKey);

            GameObject go = null;
            try
            {
                go = new GameObject("OracleGlimmerService");
                var comp = go.AddComponent(t); // lazy EnsureState fires on first API call

                int Balance() => (int)glimmerProp.GetValue(comp);
                int OwnedCount()
                {
                    var col = ownedProp != null ? ownedProp.GetValue(comp) as IEnumerable : null;
                    if (col == null) return -1;
                    int n = 0; foreach (var _ in col) n++; return n;
                }

                // Fresh wallet seeds to StartingGlimmer.
                int startBal = Balance();
                if (startBal != startingGlimmer)
                    failures.Add($"purchase round-trip: fresh balance {startBal} != StartingGlimmer {startingGlimmer}");

                // Grant exactly the cost so the buy is affordable + deterministic.
                bool added = (bool)tryAdd.Invoke(comp, new object[] { buyCost });
                if (!added) failures.Add($"purchase round-trip: TryAddGlimmer({buyCost}) returned false");

                int balBefore = Balance();
                int ownedBefore = OwnedCount();

                // THE INVARIANT: spend N ⇒ owned +1, balance −N.
                bool bought = (bool)tryPurchase.Invoke(comp, new object[] { buyId });
                int balAfter = Balance();
                int ownedAfter = OwnedCount();

                if (!bought)
                    failures.Add($"purchase round-trip: TryPurchase('{buyId}') returned false with sufficient balance {balBefore} for cost {buyCost}");
                else
                {
                    if (balAfter != balBefore - buyCost)
                        failures.Add($"purchase round-trip: DEBIT WRONG — balance {balBefore}→{balAfter}, expected −{buyCost} (={balBefore - buyCost})");
                    if (ownedAfter != ownedBefore + 1)
                        failures.Add($"purchase round-trip: GRANT WRONG — owned {ownedBefore}→{ownedAfter}, expected +1 (debit-without-grant risk)");
                    if (!(bool)owns.Invoke(comp, new object[] { buyId }))
                        failures.Add($"purchase round-trip: Owns('{buyId}') false after a successful purchase");
                }

                // Repeat purchase must be refused WITHOUT a second debit.
                int balBeforeRepeat = Balance();
                bool boughtAgain = (bool)tryPurchase.Invoke(comp, new object[] { buyId });
                int balAfterRepeat = Balance();
                if (boughtAgain)
                    failures.Add($"purchase round-trip: TryPurchase('{buyId}') SUCCEEDED twice (double-grant)");
                if (balAfterRepeat != balBeforeRepeat)
                    failures.Add($"purchase round-trip: repeat purchase moved balance {balBeforeRepeat}→{balAfterRepeat} — a DEBIT WITHOUT GRANT (player paid, got nothing)");

                // SpendGlimmer round-trip + overspend guard.
                int b = Balance();
                int spendN = Mathf.Max(1, b / 2);
                bool spent = (bool)spend.Invoke(comp, new object[] { spendN });
                if (!spent) failures.Add($"SpendGlimmer({spendN}) returned false with balance {b}");
                else if (Balance() != b - spendN)
                    failures.Add($"SpendGlimmer: balance {b}→{Balance()}, expected −{spendN}");

                int beforeOver = Balance();
                bool overspent = (bool)spend.Invoke(comp, new object[] { beforeOver + 1000000 });
                if (overspent) failures.Add("SpendGlimmer: an overspend beyond the balance SUCCEEDED (negative balance)");
                if (Balance() != beforeOver)
                    failures.Add($"SpendGlimmer: a refused overspend still mutated the balance {beforeOver}→{Balance()}");
            }
            catch (Exception ex)
            {
                failures.Add($"purchase round-trip threw: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
                // Restore the player's real wallet blob exactly.
                if (hadPref) PlayerPrefs.SetString(prefKey, prevPref);
                else PlayerPrefs.DeleteKey(prefKey);
                PlayerPrefs.Save();
            }
        }

        // =====================================================================
        //  SECTION 2 — pet active-slot persistence (FAIL-BY-DESIGN, flag_17)
        // =====================================================================
        private static void CheckPetSlotPersistence(System.Collections.Generic.List<string> failures,
                                                    System.Collections.Generic.List<string> notes)
        {
            // GameState is the persisted schema (SaveSchema round-trips its fields). Scan for a
            // field that carries the pet ACTIVE-SLOT assignment (which owned pet occupies which
            // deploy slot). Pets/StarterPetId/OwnedPets/PetBonds exist; a slot-assignment field
            // does NOT — so PetAcquisition's multi-slot roster resets on reload (flag_17).
            var fields = typeof(GameState).GetFields(BindingFlags.Public | BindingFlags.Instance);
            bool hasSlotField = false;
            foreach (var f in fields)
            {
                string n = f.Name.ToLowerInvariant();
                // A genuine slot-assignment field would name BOTH the pet domain and 'slot'
                // (e.g. PetActiveSlots / PetSlotAssignments / DeploySlots). PetBonds/OwnedPets
                // are NOT slot assignments; TowerSlots-style fields are not pet slots.
                bool petDomain = n.Contains("pet");
                bool slotDomain = n.Contains("slot") || n.Contains("activedeploy") || n.Contains("deployslot");
                if (petDomain && slotDomain) { hasSlotField = true; break; }
            }

            if (!hasSlotField)
                failures.Add("FAIL-BY-DESIGN (flag_17): GameState persists NO pet active-slot assignment field — " +
                             "PetAcquisitionService slot->species map is runtime-only (rebuilt from StarterPetId in " +
                             "SyncSlotsFromState), so a multi-slot pet roster RESETS on reload. Add a persisted slot " +
                             "field (+ SaveSchema round-trip) to fix; this oracle then flips green.");
        }

        // ── helpers ───────────────────────────────────────────────────────────

        // Read cosmetics.json through the REAL WebGL-safe path and pick the first
        // buyable (unlockMethod 'buy', glimmerCost > 0) — the exact rows TryPurchase accepts.
        private static bool TryPickBuyable(out string id, out int cost, out string err)
        {
            id = null; cost = 0; err = null;
            string json;
            try { json = DeNelle.Core.CanonicalJson.Read("Data/Canonical/cosmetics.json"); }
            catch (Exception ex) { err = "read threw: " + ex.Message; return false; }
            if (string.IsNullOrEmpty(json)) { err = "cosmetics.json missing/empty"; return false; }

            JObject root;
            try { root = JObject.Parse(json); }
            catch (Exception ex) { err = "parse error: " + ex.Message; return false; }

            var items = root["items"] as JArray;
            if (items == null) { err = "no 'items' array"; return false; }
            foreach (var tok in items)
            {
                if (!(tok is JObject o)) continue;
                string unlock = o["unlockMethod"]?.ToString();
                int c = o["glimmerCost"] != null && o["glimmerCost"].Type == JTokenType.Integer
                    ? o["glimmerCost"].Value<int>() : 0;
                string cid = o["id"]?.ToString();
                if (unlock == "buy" && c > 0 && !string.IsNullOrEmpty(cid))
                {
                    id = cid; cost = c; return true;
                }
            }
            err = "no buyable cosmetic (unlockMethod 'buy', glimmerCost > 0) found";
            return false;
        }

        private static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName, false);
                if (t != null) return t;
            }
            return null;
        }
    }
}
