// =============================================================================
// VillageEconomyRegression — headless oracle for the village economy wallets:
//   (A) CRYSTAL single-source-of-truth across the THREE stores that expose it, and
//   (B) the Wood/Iron DUAL-WALLET seam (the documented landmine).
// -----------------------------------------------------------------------------
// Drives the REAL EconomyService + CrystalEconomy + GameStateService against a
// controlled crystal/wood balance and asserts, from data:
//
//   (A) CRYSTAL SSOT — GameState.Resources.Crystals is the ONE store; EconomyService
//       .Crystals and CrystalEconomy.CurrentCrystals must both read THROUGH it. After a
//       direct set AND after an EconomyService.TrySpend(crystals), all three views move in
//       lockstep. (Any divergence = a stale parallel crystal pool, the flag in the catalog.)
//
//   (B) Wood/Iron DUAL-WALLET — the catalog FLAG: EconomyService.Wood/Iron live in an
//       in-session pool (shop + HUD read it) while the building-upgrade ResourceLedger
//       reads GameState.Wood/Iron. They do NOT auto-sync. Two probes:
//         B1 (PASS): GrantSpendable(wood,iron) writes BOTH wallets equally (the deliberate
//            both-wallet path).
//         B2 (FAIL-BY-DESIGN): a plain Grant(wood) reaches the shop pool but NOT the
//            ledger — so wood earned via the ordinary Grant path (e.g. wave rewards) is
//            INVISIBLE to the upgrade flow. This oracle asserts the invariant "a wood grant
//            is visible to both wallets" and FAILS TRUTHFULLY, surfacing the divergence with
//            data instead of a comment. Fix = route income through GrantSpendable (or unify
//            the pools); do NOT silence this oracle.
//
// SAFETY: snapshots the raw PlayerPrefs save + restores/reloads it in a finally.
// Mirrors MonetizationCovenantRegression: public static bool Run(out string reason).
// =============================================================================
using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.State;
using DeNelle.Village;

namespace DeNelle.Editor
{
    public static class VillageEconomyRegression
    {
        private const string SaveKey = "dotr-save";

        public static bool Run(out string reason)
        {
            var failures = new List<string>();

            bool hadSave = PlayerPrefs.HasKey(SaveKey);
            string rawSave = hadSave ? PlayerPrefs.GetString(SaveKey, null) : null;

            GameObject econGo = null, crystGo = null;
            bool createdGss = false;
            try
            {
                var gss = GameStateService.Instance;
                if (gss == null)
                {
                    new GameObject("GameStateService (eco-oracle)").AddComponent<GameStateService>();
                    gss = GameStateService.Instance;
                    createdGss = true;
                }
                if (gss == null || gss.State == null)
                { reason = "VILLAGE ECONOMY FAIL: no GameStateService/State available"; return false; }
                var state = gss.State;

                var econ = EconomyService.Instance;
                if (econ == null)
                {
                    econGo = new GameObject("EconomyService (oracle)");
                    econ = econGo.AddComponent<EconomyService>();
                }
                var cryst = CrystalEconomy.Instance;
                if (cryst == null)
                {
                    crystGo = new GameObject("CrystalEconomy (oracle)");
                    cryst = crystGo.AddComponent<CrystalEconomy>();
                }

                // ---- (A) CRYSTAL SSOT across the 3 stores -------------------------
                var bal = state.Resources; bal.Crystals = 1000; state.Resources = bal;
                if (econ.Crystals != 1000)
                    failures.Add($"(A) EconomyService.Crystals={econ.Crystals}, expected 1000 (not reading GameState SSOT)");
                if (cryst.CurrentCrystals != 1000)
                    failures.Add($"(A) CrystalEconomy.CurrentCrystals={cryst.CurrentCrystals}, expected 1000 (stale parallel pool)");
                if (state.Resources.Crystals != 1000)
                    failures.Add($"(A) GameState.Resources.Crystals={state.Resources.Crystals}, expected 1000");

                // Spend 300 through EconomyService -> all three must read 700 in lockstep.
                if (!econ.TrySpend(DeNelle.Village.ResourceCost.CrystalsOnly(300)))
                    failures.Add("(A) EconomyService.TrySpend(300 crystals) returned false at balance 1000 (afford check broken)");
                if (econ.Crystals != 700 || cryst.CurrentCrystals != 700 || state.Resources.Crystals != 700)
                    failures.Add($"(A) after spend 300: econ={econ.Crystals} crystalEco={cryst.CurrentCrystals} gameState={state.Resources.Crystals} — the 3 crystal stores diverged (not single-source)");

                // ---- (B1) GrantSpendable writes BOTH wood wallets (PASS) ----------
                int shopW0 = econ.Wood;
                int ledgerW0 = DeNelle.Village.Buildings.Progression.ResourceLedger.Balance(
                    DeNelle.Village.Buildings.Progression.HarvestResource.Wood);
                econ.GrantSpendable(wood: 40);
                int shopW1 = econ.Wood;
                int ledgerW1 = DeNelle.Village.Buildings.Progression.ResourceLedger.Balance(
                    DeNelle.Village.Buildings.Progression.HarvestResource.Wood);
                if (shopW1 - shopW0 != 40)
                    failures.Add($"(B1) GrantSpendable(40) shop-pool delta {shopW1 - shopW0} != 40");
                if (ledgerW1 - ledgerW0 != 40)
                    failures.Add($"(B1) GrantSpendable(40) LEDGER delta {ledgerW1 - ledgerW0} != 40 (the both-wallet path failed)");

                // ---- (B2) plain Grant(wood) DIVERGES (FAIL-BY-DESIGN) -------------
                int shopW2 = econ.Wood;
                int ledgerW2 = DeNelle.Village.Buildings.Progression.ResourceLedger.Balance(
                    DeNelle.Village.Buildings.Progression.HarvestResource.Wood);
                econ.Grant(wood: 25);   // the ordinary income path (e.g. wave reward)
                int shopDelta = econ.Wood - shopW2;
                int ledgerDelta = DeNelle.Village.Buildings.Progression.ResourceLedger.Balance(
                    DeNelle.Village.Buildings.Progression.HarvestResource.Wood) - ledgerW2;
                if (shopDelta != ledgerDelta)
                    failures.Add($"(B2) DUAL-WALLET DIVERGENCE: a plain Grant(wood:25) moved the shop pool by {shopDelta} but the upgrade ledger by {ledgerDelta} — wood income via Grant is INVISIBLE to the building-upgrade flow (route income through GrantSpendable or unify the pools)");
            }
            catch (System.Exception ex)
            {
                failures.Add($"oracle threw: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                if (econGo != null) Object.DestroyImmediate(econGo);
                if (crystGo != null) Object.DestroyImmediate(crystGo);

                if (hadSave) PlayerPrefs.SetString(SaveKey, rawSave);
                else PlayerPrefs.DeleteKey(SaveKey);
                PlayerPrefs.Save();
                var gss = GameStateService.Instance;
                if (gss != null && !createdGss) gss.Load();
            }

            if (failures.Count == 0)
            {
                reason = "VILLAGE ECONOMY OK — crystal single-source across 3 stores + Wood/Iron dual-wallet consistent";
                return true;
            }
            reason = $"VILLAGE ECONOMY FAIL x{failures.Count}: " + string.Join(" | ", failures);
            return false;
        }
    }
}
