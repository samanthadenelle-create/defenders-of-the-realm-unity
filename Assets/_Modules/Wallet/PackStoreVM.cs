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

            // Economy layer — crystals / food / coins into the resource wallet.
            var econ = pack.Contents != null ? pack.Contents.Economy : null;
            if (econ != null)
            {
                var r = state.Resources;
                r.Crystals += econ.Crystals;
                r.Food += econ.Food;
                r.Coins += econ.Coins;
                state.Resources = r;
            }

            // Ownership — the pack SKU + every cosmetic SKU it grants.
            RecordOwned(state.OwnedItemIds, pack.Sku);
            if (pack.Contents != null && pack.Contents.Cosmetics != null)
                foreach (var sku in pack.Contents.Cosmetics)
                    RecordOwned(state.OwnedItemIds, sku);

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
