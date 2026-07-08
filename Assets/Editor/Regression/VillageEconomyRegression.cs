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
using System.Reflection;
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

            // HEADLESS STATE INSTALL — editmode batchmode NEVER runs GameStateService.Awake
            // (Awake fires only in play mode / on ExecuteAlways), so a bare
            // AddComponent<GameStateService>() leaves Instance + State null — the exact cause of
            // the historic false-FAIL "no GameStateService/State available". Mirror
            // CoreSaveContractRegression: construct a THROWAWAY GameState SO and install it as the
            // active state for the duration by setting the private static _instance + the
            // [SerializeField] _state via reflection, restoring the prior live service in finally.
            GameStateService priorInstance = GameStateService.Instance;
            GameObject econGo = null, crystGo = null, gssGo = null;
            GameState throwaway = null;
            bool installed = false;
            try
            {
                throwaway = ScriptableObject.CreateInstance<GameState>();   // fresh defaults; all collections init'd → Save()-safe
                gssGo = new GameObject("GameStateService (eco-oracle)");
                var gss = gssGo.AddComponent<GameStateService>();
                if (!TryInstallHeadlessState(gss, throwaway, out string installErr))
                {
                    // The GameStateService singleton/state seam moved — genuinely unrunnable
                    // headless. NAMED SKIP (return true), never a false FAIL (harness-integrity).
                    reason = "VILLAGE ECONOMY skipped: needs fleet — " + installErr;
                    return true;
                }
                installed = true;
                var state = gss.State;   // the throwaway — never null now
                if (state == null)
                { reason = "VILLAGE ECONOMY skipped: needs fleet — throwaway state did not install"; return true; }

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
                if (gssGo != null) Object.DestroyImmediate(gssGo);
                if (throwaway != null) Object.DestroyImmediate(throwaway);

                // Restore the live service the batch's later oracles read. DestroyImmediate
                // above may have nulled the static via OnDestroy, so set it back explicitly.
                if (installed) TrySetInstanceStatic(priorInstance);

                // Restore the persisted save blob (gs.Save() wrote to it during the run).
                if (hadSave) PlayerPrefs.SetString(SaveKey, rawSave);
                else PlayerPrefs.DeleteKey(SaveKey);
                PlayerPrefs.Save();
            }

            if (failures.Count == 0)
            {
                reason = "VILLAGE ECONOMY OK — crystal single-source across 3 stores + Wood/Iron dual-wallet consistent";
                return true;
            }
            reason = $"VILLAGE ECONOMY FAIL x{failures.Count}: " + string.Join(" | ", failures);
            return false;
        }

        // =====================================================================
        //  Headless state-install helpers (editmode has no Awake)
        // =====================================================================

        /// <summary>
        /// Installs <paramref name="state"/> as the active state on <paramref name="svc"/> and
        /// promotes <paramref name="svc"/> to the live singleton, by reflection over the private
        /// <c>_state</c> field + the <c>_instance</c> static — the same seam Awake sets, which does
        /// NOT run on AddComponent in editmode batchmode. Returns false (with a named reason) if
        /// either seam was renamed/removed, so the caller NAMED-SKIPs instead of false-failing.
        /// </summary>
        private static bool TryInstallHeadlessState(GameStateService svc, GameState state, out string err)
        {
            err = null;
            var stateField = typeof(GameStateService).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance);
            if (stateField == null)
            { err = "GameStateService._state field not found by reflection (state seam renamed/removed)"; return false; }
            stateField.SetValue(svc, state);
            if (!TrySetInstanceStatic(svc))
            { err = "GameStateService._instance static not found by reflection (singleton seam renamed/removed)"; return false; }
            return true;
        }

        /// <summary>Sets the private static <c>GameStateService._instance</c> (null allowed, to restore).
        /// Returns false only if the field seam is gone.</summary>
        private static bool TrySetInstanceStatic(GameStateService svc)
        {
            var f = typeof(GameStateService).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static);
            if (f == null) return false;
            f.SetValue(null, svc);
            return true;
        }
    }
}
