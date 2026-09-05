// =============================================================================
// ArenaCatalogRegression — headless oracle for the seeded Arena data spines:
// ArenaCatalog (3 opponents) + ArenaDefenseCatalog (6 defenders + point pool).
// -----------------------------------------------------------------------------
// Pure data + logic — loads the REAL static catalogs (lazy Build()) and asserts the
// invariants the Arena raid/setup loops depend on:
//   OPPONENTS (ArenaCatalog.All):
//     1. exactly 3, unique non-empty ids, resolvable via Get(id).
//     2. Wager ascends (ArenaWagerTunables defaults 50/100/200 - the rail-declared
//        values, WO-1366) and WinPurse == ArenaWagerTunables.PurseFor(Wager) (200% today).
//     3. Tier + Threat + GuardCount are positive and non-decreasing with wager.
//     4. every BaseRecipe realizes to a NON-EMPTY fort (an empty recipe = no defender
//        base = the instant-win / empty-Arena class of bug).
//   DEFENDERS (ArenaDefenseCatalog.All):
//     5. exactly 6, unique non-empty ids, PointCost > 0, resolvable via Get(id).
//     6. Unit defenders carry a UnitClass; Structure defenders carry a BehaviorId.
//     7. DefensePointPool == 50 and the CHEAPEST defender is affordable on an empty
//        layout (a day-one defender can always be placed).
//   WAGER CURRENCY PER CHANNEL (WO-1366, owner 2026-09-04 "same logic, different
//   currency for wagers"):
//     8. GooglePlay debits/credits GameState.Resources.Crystals and NEVER touches the
//        SKR stub key; SolanaDappStore debits/credits the stub and NEVER touches
//        Crystals; Unknown refuses every debit WITH WORDS and moves nothing. Drives the
//        real ArenaWalletService against a throwaway GameStateService (installed by
//        reflection, restored in finally) with PaymentChannelResolver.OverrideForTests.
//
// NO PlayMode. Case 8 stands up a THROWAWAY GameState (never the player's save; the
// dotr-save + stub PlayerPrefs are snapshotted and restored). Mirrors
// MonetizationCovenantRegression: public static bool Run(out string reason).
// =============================================================================
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using DeNelle.Core.Payments;
using DeNelle.Core.Platform;
using DeNelle.Core.State;
using DeNelle.Village.Arena;

namespace DeNelle.Editor
{
    public static class ArenaCatalogRegression
    {
        public static bool Run(out string reason)
        {
            var failures = new List<string>();

            // ---- OPPONENTS ----------------------------------------------------
            var opps = ArenaCatalog.All;
            if (opps == null || opps.Count == 0)
            { reason = "ARENA CATALOG FAIL: ArenaCatalog.All is empty (seed build broke)"; return false; }
            if (opps.Count != 3)
                failures.Add($"expected 3 seeded opponents, found {opps.Count}");

            var oppIds = new HashSet<string>();
            long prevWager = long.MinValue;
            int prevThreat = int.MinValue;
            foreach (var o in opps)
            {
                if (o == null) { failures.Add("null opponent entry"); continue; }
                if (string.IsNullOrEmpty(o.Id)) { failures.Add("opponent with null/empty id"); }
                else if (!oppIds.Add(o.Id)) failures.Add($"duplicate opponent id '{o.Id}'");
                else if (ArenaCatalog.Get(o.Id) != o) failures.Add($"ArenaCatalog.Get('{o.Id}') did not round-trip");

                // WO-1366: the purse multiplier is a tunable (default 200% = Wager*2). Pin the
                // catalog to the ONE holder so a drift between the two goes red.
                long expectedPurse = ArenaWagerTunables.PurseFor(o.Wager);
                if (o.WinPurse != expectedPurse)
                    failures.Add($"opponent '{o.Id}' WinPurse {o.WinPurse} != ArenaWagerTunables.PurseFor(Wager) ({expectedPurse}, pursePct {ArenaWagerTunables.WinPursePct})");
                if (ArenaWagerTunables.WinPursePct == ArenaWagerTunables.WinPursePctDefault && o.WinPurse != o.Wager * 2L)
                    failures.Add($"opponent '{o.Id}' WinPurse {o.WinPurse} != Wager*2 ({o.Wager * 2L}) at the default purse percent - today's behaviour changed");
                if (o.Wager <= prevWager) failures.Add($"opponent '{o.Id}' wager {o.Wager} did not ascend (prev {prevWager})");
                prevWager = o.Wager;
                if (o.Threat < prevThreat) failures.Add($"opponent '{o.Id}' threat {o.Threat} decreased (prev {prevThreat})");
                prevThreat = o.Threat;
                if (o.Tier <= 0 || o.Threat <= 0 || o.GuardCount <= 0)
                    failures.Add($"opponent '{o.Id}' has non-positive tier/threat/guards ({o.Tier}/{o.Threat}/{o.GuardCount})");

                int recipeCount = o.BaseRecipe != null ? o.BaseRecipe.Count : 0;
                if (recipeCount == 0)
                    failures.Add($"opponent '{o.Id}' BaseRecipe is EMPTY — no defender fort (instant-win / empty-Arena bug class)");
            }

            // ---- DEFENDERS ----------------------------------------------------
            var defs = ArenaDefenseCatalog.All;
            if (defs == null || defs.Count == 0)
            { failures.Add("ArenaDefenseCatalog.All is empty (seed build broke)"); }
            else
            {
                if (defs.Count != 6)
                    failures.Add($"expected 6 seeded defenders, found {defs.Count}");

                var defIds = new HashSet<string>();
                ArenaDefenseDef cheapest = null;
                foreach (var d in defs)
                {
                    if (d == null) { failures.Add("null defender entry"); continue; }
                    if (string.IsNullOrEmpty(d.Id)) failures.Add("defender with null/empty id");
                    else if (!defIds.Add(d.Id)) failures.Add($"duplicate defender id '{d.Id}'");
                    else if (ArenaDefenseCatalog.Get(d.Id) != d) failures.Add($"ArenaDefenseCatalog.Get('{d.Id}') did not round-trip");

                    if (d.PointCost <= 0) failures.Add($"defender '{d.Id}' PointCost {d.PointCost} <= 0");
                    if (d.Kind == DefenderKind.Unit && !d.UnitClass.HasValue)
                        failures.Add($"unit defender '{d.Id}' has no UnitClass (no body to spawn)");
                    if (d.Kind == DefenderKind.Structure && string.IsNullOrEmpty(d.BehaviorId))
                        failures.Add($"structure defender '{d.Id}' has no BehaviorId (no behavior to attach)");

                    if (cheapest == null || d.PointCost < cheapest.PointCost) cheapest = d;
                }

                if (ArenaDefenseCatalog.DefensePointPool != 50)
                    failures.Add($"DefensePointPool is {ArenaDefenseCatalog.DefensePointPool}, expected 50");
                if (cheapest != null &&
                    !ArenaDefenseCatalog.CanAfford(new List<PlacedDefenderData>(), cheapest.Id))
                    failures.Add($"cheapest defender '{cheapest.Id}' ({cheapest.PointCost}pt) is NOT affordable on an empty pool (day-one place broken)");
            }

            // ---- TUNABLE DEFAULTS = TODAY'S CONSTANTS (WO-1366 section 5) ------
            // The holder's defaults must be the values ArenaCatalog hardcoded before the
            // move (50 / 100 / 200 / 2x). A re-picked default here is a silent balance change.
            if (ArenaWagerTunables.WagerTier1Default != 50 || ArenaWagerTunables.WagerTier2Default != 100 ||
                ArenaWagerTunables.WagerTier3Default != 200 || ArenaWagerTunables.WinPursePctDefault != 200)
                failures.Add($"ArenaWagerTunables defaults are {ArenaWagerTunables.WagerTier1Default}/{ArenaWagerTunables.WagerTier2Default}/" +
                             $"{ArenaWagerTunables.WagerTier3Default}/{ArenaWagerTunables.WinPursePctDefault}% - expected 50/100/200/200% (today's constants; the owner re-picks, not a seat)");

            // ---- WAGER CURRENCY PER CHANNEL (WO-1366) ---------------------------
            RunWagerChannelCase(failures);

            if (failures.Count == 0)
            {
                reason = $"ARENA CATALOG OK - 3 opponents (purse={ArenaWagerTunables.WinPursePct}% of wager, forts non-empty) + 6 defenders (pool {ArenaDefenseCatalog.DefensePointPool}, day-one affordable) + wager currency per channel (Play=Crystals, Solana=stub, Unknown=refused)";
                return true;
            }
            reason = $"ARENA CATALOG FAIL x{failures.Count}: " + string.Join(" | ", failures);
            return false;
        }

        // =====================================================================
        //  Case 8 - the wager currency follows the payment channel (WO-1366)
        // =====================================================================
        // RED-first pin. The one-line mutation that turns it red:
        //   CurrencySkinResolver.ResolveWagerCurrency: `case PaymentChannel.GooglePlay:
        //   return WagerCurrency.Crystals;` -> `return WagerCurrency.Skr;` - the Play half
        //   then fails twice (Crystals untouched at 120, stub key moved off "500").

        private const string StubPrefKey = "dotr-arena-skr-balance";   // ArenaWalletService's key, unchanged on purpose
        private const string SaveKey = SaveSchema.PlayerPrefsKey;

        private static void RunWagerChannelCase(List<string> failures)
        {
            const string tag = "[wager-channel]";
            bool hadStub = PlayerPrefs.HasKey(StubPrefKey);
            string rawStub = hadStub ? PlayerPrefs.GetString(StubPrefKey, null) : null;
            bool hadSave = PlayerPrefs.HasKey(SaveKey);
            string rawSave = hadSave ? PlayerPrefs.GetString(SaveKey, null) : null;
            GameStateService priorGss = GameStateService.Instance;
            GameObject gssGo = null;
            GameState throwaway = null;
            try
            {
                throwaway = ScriptableObject.CreateInstance<GameState>();
                gssGo = new GameObject("GSS (arena-wager oracle)");
                var gss = gssGo.AddComponent<GameStateService>();
                if (gss == null || !InstallState(gss, throwaway))
                {
                    failures.Add($"{tag} GameStateService _state/_instance seam not reflectable - the Crystals half cannot be driven (a rename of a private field, not a data problem)");
                    return;
                }

                // A KNOWN stub balance so both "moved" and "never touched" are measurable.
                PlayerPrefs.SetString(StubPrefKey, "500");
                PlayerPrefs.Save();
                ArenaWalletService.ForgetCachedStubForTests();

                // -- GooglePlay: Crystals move, the stub key does not -------------------
                PaymentChannelResolver.OverrideForTests(PaymentChannel.GooglePlay);
                SetCrystals(gss, 120);
                if (ArenaWalletService.Currency != CurrencySkinResolver.WagerCurrency.Crystals)
                    failures.Add($"{tag}/play resolved {ArenaWalletService.Currency}, expected Crystals - Play must wager Crystals (owner 2026-09-04)");
                if (ArenaWalletService.Balance != 120)
                    failures.Add($"{tag}/play Balance read {ArenaWalletService.Balance}, expected the live Crystals (120) - it is reading the stub or nothing");
                if (!ArenaWalletService.Debit(50, out string why))
                    failures.Add($"{tag}/play Debit(50) with 120 Crystals was refused: '{why}'");
                if (Crystals(gss) != 70)
                    failures.Add($"{tag}/play after Debit(50) Crystals={Crystals(gss)}, expected 70 - the wager did not debit GameState.Resources.Crystals");
                if (PlayerPrefs.GetString(StubPrefKey, "") != "500")
                    failures.Add($"{tag}/play the SKR stub key moved to '{PlayerPrefs.GetString(StubPrefKey, "")}' - Play must NEVER touch dotr-arena-skr-balance (free-Crystals trap, WO-1366 s4)");
                if (ArenaWalletService.Debit(1000, out why))
                    failures.Add($"{tag}/play Debit(1000) with 70 Crystals SUCCEEDED - the guard must refuse before AddCrystals clamps");
                if (string.IsNullOrEmpty(why))
                    failures.Add($"{tag}/play the insufficient-Crystals refusal carried an EMPTY reason - a dead button");
                if (Crystals(gss) != 70)
                    failures.Add($"{tag}/play a REFUSED debit changed Crystals to {Crystals(gss)} (expected 70)");
                ArenaWalletService.Credit(100);
                if (Crystals(gss) != 170)
                    failures.Add($"{tag}/play after Credit(100) Crystals={Crystals(gss)}, expected 170 - the purse did not land in Crystals");
                if (PlayerPrefs.GetString(StubPrefKey, "") != "500")
                    failures.Add($"{tag}/play Credit moved the SKR stub key to '{PlayerPrefs.GetString(StubPrefKey, "")}' - Play must never touch it");

                // -- SolanaDappStore: the stub moves, Crystals do not -------------------
                PaymentChannelResolver.OverrideForTests(PaymentChannel.SolanaDappStore);
                ArenaWalletService.ForgetCachedStubForTests();
                if (ArenaWalletService.Currency != CurrencySkinResolver.WagerCurrency.Skr)
                    failures.Add($"{tag}/solana resolved {ArenaWalletService.Currency}, expected Skr - the dApp Store keeps today's SKR stub");
                if (ArenaWalletService.Balance != 500)
                    failures.Add($"{tag}/solana Balance read {ArenaWalletService.Balance}, expected the stub's 500 - byte-identical-to-today is broken");
                if (!ArenaWalletService.Debit(50, out why))
                    failures.Add($"{tag}/solana Debit(50) with a 500 stub was refused: '{why}'");
                if (PlayerPrefs.GetString(StubPrefKey, "") != "450")
                    failures.Add($"{tag}/solana after Debit(50) the stub key reads '{PlayerPrefs.GetString(StubPrefKey, "")}', expected 450");
                if (Crystals(gss) != 170)
                    failures.Add($"{tag}/solana Debit changed Crystals to {Crystals(gss)} (expected 170) - Solana must NEVER touch Crystals");
                ArenaWalletService.Credit(100);
                if (PlayerPrefs.GetString(StubPrefKey, "") != "550")
                    failures.Add($"{tag}/solana after Credit(100) the stub key reads '{PlayerPrefs.GetString(StubPrefKey, "")}', expected 550");
                if (Crystals(gss) != 170)
                    failures.Add($"{tag}/solana Credit changed Crystals to {Crystals(gss)} (expected 170)");

                // -- Unknown: refused WITH WORDS, nothing moves --------------------------
                PaymentChannelResolver.OverrideForTests(PaymentChannel.Unknown);
                if (ArenaWalletService.Currency != CurrencySkinResolver.WagerCurrency.Refused)
                    failures.Add($"{tag}/unknown resolved {ArenaWalletService.Currency}, expected Refused - an unstamped artifact must fail closed");
                if (ArenaWalletService.CanAfford(1))
                    failures.Add($"{tag}/unknown CanAfford(1) is TRUE on a refused channel - the RAID button would light up");
                if (ArenaWalletService.Debit(50, out why))
                    failures.Add($"{tag}/unknown Debit(50) SUCCEEDED on a refused channel");
                if (string.IsNullOrEmpty(why))
                    failures.Add($"{tag}/unknown the refusal carried an EMPTY reason - the ruling forbids a dead button as much as the sale");
                if (Crystals(gss) != 170)
                    failures.Add($"{tag}/unknown a refused Debit changed Crystals to {Crystals(gss)}");
                if (PlayerPrefs.GetString(StubPrefKey, "") != "550")
                    failures.Add($"{tag}/unknown a refused Debit moved the stub key to '{PlayerPrefs.GetString(StubPrefKey, "")}'");
            }
            catch (Exception ex)
            {
                failures.Add($"{tag} threw {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                PaymentChannelResolver.ClearTestOverride();
                if (gssGo != null) UnityEngine.Object.DestroyImmediate(gssGo);
                if (throwaway != null) UnityEngine.Object.DestroyImmediate(throwaway);
                SetGssInstance(priorGss);
                // Restore EXACTLY what was there, including "no key at all".
                if (hadStub) PlayerPrefs.SetString(StubPrefKey, rawStub); else PlayerPrefs.DeleteKey(StubPrefKey);
                if (hadSave) PlayerPrefs.SetString(SaveKey, rawSave); else PlayerPrefs.DeleteKey(SaveKey);
                PlayerPrefs.Save();
                ArenaWalletService.ForgetCachedStubForTests();
            }
        }

        private static int Crystals(GameStateService gss) => gss.State.Resources.Crystals;

        private static void SetCrystals(GameStateService gss, int amount)
        {
            var s = gss.State;
            var r = s.Resources;
            r.Crystals = amount;
            s.Resources = r;
        }

        // -- harness plumbing (test scaffolding only; mirrors CrystalProductionRegression) --
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
