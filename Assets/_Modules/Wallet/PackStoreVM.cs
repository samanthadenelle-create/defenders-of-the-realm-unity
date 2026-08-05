// =============================================================================
// PackStoreVM — the pack-store's game-state seam (WO-744 MVVM migration).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Wallet   Namespace: DeNelle.Wallet
//
// Extracted from PackStore (the View) so the View stops naming GameStateService
// and the MarketplaceInteractor scene-resolve (FindFirstObjectOfType). The MONEY
// / ENTITLEMENT PATH is unchanged — ApplyPackContents / RecordOwned / IsOwned are
// moved VERBATIM (same crystals/food/coins top-up, same owned-SKU record, same
// self-reporting FlowTrace + Save). The async WalletService purchase orchestration
// stays in the View (it drives the status banner + card re-render); it now asks
// THIS VM to read ownership + apply contents instead of touching GameState itself.
//
// Also owns the store-close resolve: TryCloseViaInteractor (drives
// MarketplaceInteractor.CloseStore via reflection — the DeNelle.Wallet -> Village
// one-way asmdef guard) + the ReEnableDisabledHeroLocomotion fallback, so the soft-
// lock guard behaviour is preserved but the View no longer names FindFirstObjectOfType.
//
// GameState is resolved through an injected provider (CreateDefault -> GameStateService)
// so the VM is unit-testable with a plain GameState (ARCHITECTURE_PRINCIPLES §2c).
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.State;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Wallet
{
    /// <summary>
    /// The pack store's game-state ViewModel: ownership queries + the verbatim entitlement grant +
    /// the interactor-driven close resolve. Keeps the View free of GameStateService / scene finds.
    /// </summary>
    public sealed class PackStoreVM
    {
        private readonly Func<GameState> _stateProvider;

        /// <summary>Sole resolution site — binds the live GameState so the View never names the singleton.</summary>
        public static PackStoreVM CreateDefault() =>
            new PackStoreVM(() => GameStateService.Instance != null ? GameStateService.Instance.State : null);

        public PackStoreVM(Func<GameState> stateProvider)
        {
            _stateProvider = stateProvider;
        }

        private GameState State => _stateProvider != null ? _stateProvider() : null;

        // ── Ownership ─────────────────────────────────────────────────────────

        /// <summary>True when the pack SKU is already in the player's owned items.</summary>
        public bool IsOwned(string sku)
        {
            var state = State;
            if (state == null || state.OwnedItemIds == null) return false;
            return state.OwnedItemIds.Contains(sku);
        }

        // ── Entitlement grant (money/reward path — moved VERBATIM from PackStore) ──

        /// <summary>
        /// Applies a purchased pack's contents to the live game state — the economy top-up lands in
        /// the resource wallet and the pack SKU plus its cosmetic SKUs are recorded as owned. Mirrors
        /// the React entitlement fulfilment (storeItems.ts purchaseGrantFor + grantItem). Behaviour is
        /// unchanged from PackStore.ApplyPackContents — same order, same self-reporting, same Save.
        /// </summary>
        public void ApplyPackContents(PackDef pack)
        {
            using var _ = FlowTrace.Enter("Store", $"ApplyPackContents '{pack?.Sku ?? "<null>"}'");
            var state = State;
            if (state == null)
            {
                // The payment already confirmed by the time we reach here — no GameState = the player
                // paid and the entitlement is LOST. Fail loudly (was a swallowed LogWarning).
                FlowTrace.Fail("Store",
                    $"ApplyPackContents: no GameStateService/State — pack '{pack?.Sku ?? "<null>"}' contents NOT applied. " +
                    "If this followed a confirmed payment, the player is CHARGED with NO entitlement.");
                return;
            }

            // Economy layer - route EVERY advertised currency through its canonical, persisted,
            // HUD-refreshing grant seam (ECON-01 fix). The old code wrote state.Resources DIRECTLY
            // and applied ONLY Crystals/Food/Coins, so Glimmer + Wood/Iron were silently dropped
            // (a shipped lie on the pack card - every pack advertises glimmer and granted none). Now:
            //   Glimmer                     -> GlimmerCurrencyService.TryAddGlimmer(int)
            //   Wood/Iron/Food/Crystals     -> EconomyService.GrantSpendable(int,int,int,int)
            //   Coins (Gold)                -> EconomyService.AddCoins(int)
            // Each currency is passed to EXACTLY ONE seam, so nothing is double-granted.
            var econ = pack.Contents != null ? pack.Contents.Economy : null;
            int gGlimmer = 0, gWood = 0, gIron = 0, gFood = 0, gCrystals = 0, gCoins = 0;
            if (econ != null)
            {
                gGlimmer  = Mathf.Max(0, econ.Glimmer);
                gWood     = Mathf.Max(0, econ.Wood);
                gIron     = Mathf.Max(0, econ.Iron);
                gFood     = Mathf.Max(0, econ.Food);
                gCrystals = Mathf.Max(0, econ.Crystals);
                gCoins    = Mathf.Max(0, econ.Coins);

                if (gGlimmer > 0) TryGrantGlimmer(gGlimmer, pack.Sku);
                // Wood/Iron/Food/Crystals land in a single GrantSpendable (mirrors Wood/Iron to the
                // persisted GameState ledger AND routes Food/Crystals through AddFood/AddCrystals ->
                // Save + ResourcesChanged); Coins(Gold) land via AddCoins.
                if (gWood > 0 || gIron > 0 || gFood > 0 || gCrystals > 0)
                    TryGrantResources(gWood, gFood, gIron, gCrystals, pack.Sku);
                if (gCoins > 0) TryGrantCoins(gCoins, pack.Sku);
            }

            // Ownership — the pack SKU + every cosmetic SKU it grants.
            RecordOwned(state.OwnedItemIds, pack.Sku);
            int cosmeticCount = 0;
            if (pack.Contents != null && pack.Contents.Cosmetics != null)
                foreach (var sku in pack.Contents.Cosmetics)
                {
                    RecordOwned(state.OwnedItemIds, sku);
                    // ECON-02 fix: the wardrobe / Cosmetic Shop reads GlimmerCurrencyService ownership,
                    // NOT GameState.OwnedItemIds. Without this, a pack cosmetic is "owned" to the pack
                    // system yet GlimmerCurrencyService.Owns(sku)==false and Equip(sku) no-ops (split-
                    // brain, economy-meta FLAG #16). Grant ownership into the Glimmer store too via
                    // GrantAchievement (the outside-the-spend own-set writer). Write BOTH stores so the
                    // pack IsOwned check and the wardrobe agree; GrantAchievement is idempotent.
                    if (!string.IsNullOrEmpty(sku)) { TryGrantCosmeticOwnership(sku, pack.Sku); cosmeticCount++; }
                }

            // Convenience tokens are consumable items — the v2 foundation has no
            // token tray yet; they are flagged for the Week-8 inventory pass.
            // (Recording the pack SKU above is enough for the entitlement check.)

            // VERIFY the entitlement actually landed before persisting — the SKU must now be owned, or
            // the paid-for grant silently failed. This is the proof the entitlement took.
            bool owned = state.OwnedItemIds != null && state.OwnedItemIds.Contains(pack.Sku);
            if (!owned)
                FlowTrace.Fail("Store",
                    $"ApplyPackContents: pack '{pack.Sku}' NOT recorded as owned after grant — entitlement did NOT take (player may be charged with nothing to show).");
            else
                FlowTrace.Step("Store", $"ApplyPackContents: pack '{pack.Sku}' recorded owned + economy applied.");

            // ECON-01/02 PROOF LINE - logs EXACTLY what this pack granted (every currency delta + the
            // cosmetic-ownership count) so a silent drop can never recur unseen. If any figure here reads
            // 0 for something the pack card advertised, the grant seam is broken and the trace shows it.
            FlowTrace.Step("Pack",
                $"granted '{pack.Sku}': glimmer={gGlimmer} wood={gWood} iron={gIron} food={gFood} " +
                $"crystals={gCrystals} coins={gCoins} cosmetics={cosmeticCount} " +
                "(each routed through its canonical persisted seam - TryAddGlimmer / GrantSpendable / AddCoins / GrantAchievement)");

            // Persist through the service so the save round-trips.
            FlowTrace.Try("Store", $"save after granting '{pack.Sku}'", () =>
            {
                var svc = GameStateService.Instance;
                if (svc != null) svc.Save();
            });
        }

        private static void RecordOwned(List<string> owned, string sku)
        {
            if (owned == null || string.IsNullOrEmpty(sku)) return;
            if (!owned.Contains(sku)) owned.Add(sku);
        }

        // ---- Cross-asmdef grant seams (ECON-01 / ECON-02) -------------------
        // PackStoreVM lives in DeNelle.Wallet, which cannot reference DeNelle.Cosmetics
        // (Cosmetics -> Wallet already, so a back-reference would be circular) nor DeNelle.Village
        // (one-way asmdef guard). So the canonical, persisted grant services are reached by
        // AppDomain type-name reflection - the SAME bridge CryptoPaymentManager.GrantGlimmer and
        // TryCloseViaInteractor below already use. Every miss Fails LOUDLY: by the time ApplyPackContents
        // runs the payment has ALREADY confirmed, so a silent grant failure = a lost, paid-for entitlement.

        /// <summary>Resolves a singleton service's live Instance by type name across loaded assemblies.</summary>
        private static object ResolveServiceInstance(string typeName, out Type type)
        {
            type = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = asm.GetType(typeName);
                if (type != null) break;
            }
            if (type == null) return null;
            return type.GetProperty("Instance",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public)?.GetValue(null);
        }

        /// <summary>Glimmer -> GlimmerCurrencyService.TryAddGlimmer(int) (ECON-01). Persists to the cosmetic wallet.</summary>
        private static void TryGrantGlimmer(int amount, string packSku)
        {
            var svc = ResolveServiceInstance("DeNelle.Cosmetics.GlimmerCurrencyService", out var t);
            if (svc == null)
            {
                FlowTrace.Fail("Pack", $"grant glimmer +{amount} for '{packSku}' FAILED: GlimmerCurrencyService missing (service/type not loaded) - paid-for Glimmer LOST.");
                return;
            }
            var m = t.GetMethod("TryAddGlimmer", new[] { typeof(int) });
            if (m == null)
            {
                FlowTrace.Fail("Pack", $"grant glimmer +{amount} for '{packSku}' FAILED: TryAddGlimmer(int) not found - paid-for Glimmer LOST.");
                return;
            }
            try { m.Invoke(svc, new object[] { amount }); }
            catch (Exception ex) { FlowTrace.Fail("Pack", $"grant glimmer +{amount} for '{packSku}' THREW: {ex.GetType().Name}: {ex.Message} - paid-for Glimmer LOST."); }
        }

        /// <summary>
        /// Cosmetic SKU -> GlimmerCurrencyService ownership so the wardrobe can equip it (ECON-02).
        /// The Cosmetic Shop / wardrobe reads GlimmerCurrencyService.Owns(sku) as the SSOT for cosmetic
        /// ownership, and the pack-grant oracle asserts the same. GrantAchievement only registers SKUs
        /// that live in CosmeticCatalog (cosmetics.json) — pack cosmetic SKUs (e.g.
        /// "cosmetic.founders-vow.hero-outfit") are pack rewards, NOT shop-catalog items, so
        /// GrantAchievement no-ops on them and Owns(sku) stays false. So we ALSO call the
        /// catalog-independent MarkCosmeticOwned(string), which writes straight into the same owned-set
        /// GlimmerCurrencyService.Owns reads and persists it — guaranteeing Owns(sku)==true post-grant.
        /// Both writes are idempotent; the GameState.OwnedItemIds record (RecordOwned) is unchanged.
        /// </summary>
        private static void TryGrantCosmeticOwnership(string cosmeticSku, string packSku)
        {
            var svc = ResolveServiceInstance("DeNelle.Cosmetics.GlimmerCurrencyService", out var t);
            if (svc == null)
            {
                FlowTrace.Fail("Pack", $"grant cosmetic '{cosmeticSku}' (pack '{packSku}') FAILED: GlimmerCurrencyService missing - pack cosmetic will be UNEQUIPPABLE.");
                return;
            }

            // Keep the catalog-gated achievement write (harmless no-op for non-catalog pack SKUs; still
            // the right seam for any pack SKU that IS a catalog cosmetic).
            var achM = t.GetMethod("GrantAchievement", new[] { typeof(string) });
            if (achM != null)
            {
                try { achM.Invoke(svc, new object[] { cosmeticSku }); }
                catch (Exception ex) { FlowTrace.Fail("Pack", $"grant cosmetic '{cosmeticSku}' (pack '{packSku}') GrantAchievement THREW: {ex.GetType().Name}: {ex.Message}"); }
            }

            // The load-bearing write: register ownership catalog-independently so Owns(sku)==true.
            var markM = t.GetMethod("MarkCosmeticOwned", new[] { typeof(string) });
            if (markM == null)
            {
                FlowTrace.Fail("Pack", $"grant cosmetic '{cosmeticSku}' (pack '{packSku}') FAILED: MarkCosmeticOwned(string) not found - pack cosmetic will be UNEQUIPPABLE.");
                return;
            }
            try { markM.Invoke(svc, new object[] { cosmeticSku }); }
            catch (Exception ex) { FlowTrace.Fail("Pack", $"grant cosmetic '{cosmeticSku}' (pack '{packSku}') MarkCosmeticOwned THREW: {ex.GetType().Name}: {ex.Message} - pack cosmetic will be UNEQUIPPABLE."); }

            FlowTrace.Step("Pack", $"cosmetic '{cosmeticSku}' (pack '{packSku}') registered owned in GlimmerCurrencyService (Owns==true).");
        }

        /// <summary>
        /// Wood/Iron/Food/Crystals -> EconomyService.GrantSpendablePurchased(int wood,int food,int
        /// iron,int crystals) (ECON-01). Persists + raises ResourcesChanged.
        /// <para>WO-857 Phase F: this MUST resolve the <b>Purchased</b> seam, not the plain
        /// GrantSpendable. The plain one applies the town bank cap, and a pack advertising 5,000 food
        /// then delivered only the starter wallet's 1,920 headroom (caught by PackGrantRegression).
        /// An advertised quantity always arrives in full — see BankGrantKind.PurchasedOrPromised.</para>
        /// <para>The legacy name is kept as a LAST-RESORT fallback: if the purchased seam is ever
        /// renamed away, delivering a clamped amount is still better for the player than delivering
        /// nothing, but it is a FAIL-level event because the pack has under-delivered.</para>
        /// </summary>
        private static void TryGrantResources(int wood, int food, int iron, int crystals, string packSku)
        {
            var svc = ResolveServiceInstance("DeNelle.Village.EconomyService", out var t);
            if (svc == null)
            {
                FlowTrace.Fail("Pack", $"grant resources (W{wood}/F{food}/I{iron}/C{crystals}) for '{packSku}' FAILED: EconomyService missing - paid-for resources LOST.");
                return;
            }
            var sig = new[] { typeof(int), typeof(int), typeof(int), typeof(int) };
            var m = t.GetMethod("GrantSpendablePurchased", sig);
            if (m == null)
            {
                m = t.GetMethod("GrantSpendable", sig);
                if (m != null)
                    FlowTrace.Fail("Pack",
                        $"grant resources for '{packSku}': GrantSpendablePurchased(int,int,int,int) NOT FOUND - falling back to the " +
                        "CAPPED GrantSpendable. A paid pack may now UNDER-DELIVER its advertised amounts against the town bank cap.");
            }
            if (m == null)
            {
                FlowTrace.Fail("Pack", $"grant resources for '{packSku}' FAILED: no GrantSpendablePurchased/GrantSpendable(int,int,int,int) - paid-for resources LOST.");
                return;
            }
            try { m.Invoke(svc, new object[] { wood, food, iron, crystals }); }
            catch (Exception ex) { FlowTrace.Fail("Pack", $"grant resources for '{packSku}' THREW: {ex.GetType().Name}: {ex.Message} - paid-for resources LOST."); }
        }

        /// <summary>Coins (Gold) -> EconomyService.AddCoins(int) (ECON-01). Persists + raises ResourcesChanged.</summary>
        private static void TryGrantCoins(int coins, string packSku)
        {
            var svc = ResolveServiceInstance("DeNelle.Village.EconomyService", out var t);
            if (svc == null)
            {
                FlowTrace.Fail("Pack", $"grant coins +{coins} for '{packSku}' FAILED: EconomyService missing - paid-for Gold LOST.");
                return;
            }
            var m = t.GetMethod("AddCoins", new[] { typeof(int) });
            if (m == null)
            {
                FlowTrace.Fail("Pack", $"grant coins +{coins} for '{packSku}' FAILED: AddCoins(int) not found - paid-for Gold LOST.");
                return;
            }
            try { m.Invoke(svc, new object[] { coins }); }
            catch (Exception ex) { FlowTrace.Fail("Pack", $"grant coins +{coins} for '{packSku}' THREW: {ex.GetType().Name}: {ex.Message} - paid-for Gold LOST."); }
        }

        // ── Store close resolve (interactor reflection + hero re-enable fallback) ──

        /// <summary>
        /// Closes the store the way MarketplaceInteractor does. Returns true when the interactor
        /// handled it (its private CloseStore re-enables HeroLocomotion + clears _storeOpen); returns
        /// false after running the ReEnableDisabledHeroLocomotion fallback, signalling the View to hide
        /// its own GameObject. Preserves the exact soft-lock-guard behaviour of PackStore.CloseStore.
        /// </summary>
        public bool CloseViaInteractor()
        {
            if (TryCloseViaInteractor()) return true;

            // Fallback: re-enable a disabled hero locomotion; the View hides itself.
            ReEnableDisabledHeroLocomotion();
            return false;
        }

        private bool TryCloseViaInteractor()
        {
            // Find MarketplaceInteractor by type name across loaded assemblies
            // (we can't reference the Village asmdef directly).
            Type interactorType = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                interactorType = asm.GetType("DeNelle.Village.MarketplaceInteractor");
                if (interactorType != null) break;
            }
            if (interactorType == null) return false;

            var interactor = FindFirstObjectOfType(interactorType, true);
            if (interactor == null) return false;

            var closeMethod = interactorType.GetMethod(
                "CloseStore",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic);
            if (closeMethod == null) return false;

            closeMethod.Invoke(interactor, null);
            return true;
        }

        private static UnityEngine.Object FindFirstObjectOfType(Type t, bool includeInactive)
        {
            var found = Resources.FindObjectsOfTypeAll(t);
            if (found == null) return null;
            foreach (var obj in found)
            {
                // Skip assets / prefabs not in a live scene.
                if (obj is Component comp && comp.gameObject.scene.IsValid())
                {
                    if (includeInactive || comp.gameObject.activeInHierarchy)
                        return obj;
                }
            }
            return null;
        }

        private void ReEnableDisabledHeroLocomotion()
        {
            Type locoType = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                locoType = asm.GetType("DeNelle.Village.HeroLocomotion");
                if (locoType != null) break;
            }
            if (locoType == null) return;

            var found = Resources.FindObjectsOfTypeAll(locoType);
            if (found == null) return;
            foreach (var obj in found)
            {
                if (obj is Behaviour behaviour && behaviour.gameObject.scene.IsValid())
                    behaviour.enabled = true;
            }
        }
    }
}
