# WORK ORDER 73 — RESULT

**Status:** COMPLETE (reconciled)
**Implemented by:** CLI agent
**Date:** 2026-05-29

---

## What was built

Three genuine gaps from the WO filled against the branch's existing architecture.
Nothing duplicated. All files brace-checked.

### 1. `CosmeticApplier.cs` — NEW
**Path:** `Assets/_Modules/Cosmetics/CosmeticApplier.cs`
**Assembly:** `DeNelle.Cosmetics`

Reconciliation vs WO-73 spec:
- WO-73 used non-existent `CosmeticData` SO. Branch uses `CosmeticDef` (from
  `CosmeticCatalog`). Applier reads `CosmeticDef.PreviewUnityColor` for first-pass
  material tinting; Inspector slots for `materialOverride`, `prefabOverride`,
  `vfxPrefab` are ready for art hookup without code changes.
- Two `ApplyCosmetic` overloads: by `string cosmeticId` (catalog lookup) and by
  `CosmeticDef` directly.
- `ResetToDefault()` restores original shared material, destroys override model + VFX,
  re-enables `defaultModel`.
- `EquippedCosmeticId` property for UI/state queries.

### 2. `BattlePassManager.cs` — NEW
**Path:** `Assets/_Modules/Cosmetics/BattlePassManager.cs`
**Assembly:** `DeNelle.Cosmetics`

Reconciliation vs WO-73 spec (`BattlePassSystem`):
- References `GlimmerCurrencyService` (real soft currency) not `MonetizationManager`
  (doesn't exist). Premium pass costs 2 400 **Glimmer** (not "AetherShards").
- Uses `BattlePassData` + `BattlePassReward` (already in `DeNelle.Core.Data`).
- `BattlePassRewardKind.Crystals` credits `GameState.AetherCrystals` (correct field
  name — was wrong as `crystals` in first draft, fixed before shipping).
- `BattlePassRewardKind.Cosmetic` calls `GlimmerCurrencyService.GrantAchievement`.
- Premium pass deducts via new `GlimmerCurrencyService.SpendGlimmer(int)` (see below).
- `LevelUpVFXController.PlayLevelUp(Vector3, int)` called via reflection (Cosmetics
  cannot reference Village directly). Graceful no-op if VFX controller absent.
- PlayerPrefs keys `BP_Level`, `BP_XP`, `BP_HasPremium`.

### 3. `GlimmerCurrencyService.cs` — EDITED (1 method added)
**Path:** `Assets/_Modules/Cosmetics/GlimmerCurrencyService.cs`

Added `SpendGlimmer(int amount)` — balance-checked deduction, same pattern as
`TryAddGlimmer`. Required because `TryAddGlimmer` guards `amount <= 0` so negative
values silently fail; `BattlePassManager.PurchasePremiumPass()` needs a real spend.

### 4. `CryptoPaymentManager.cs` — NEW
**Path:** `Assets/_Modules/Wallet/CryptoPaymentManager.cs`
**Assembly:** `DeNelle.Wallet`

Reconciliation vs WO-74 spec:
- WO-74 wrote raw SDK calls (Web3, WalletBase, PublicKey). Branch already has a
  complete `#if SOLANA_SDK`-guarded seam: `WalletService → IWalletProvider →
  SolanaWalletProvider / StubWalletProvider`. This file is a thin bridge to that seam.
- `ConnectWallet()` → `WalletService.Connect()` (UniTask, never async void).
- `PayWithSOL/SKR/USDC(int aetherAmount)` → `WalletService.PayFlat()` with tunable
  conversion rates (Inspector fields).
- SKR 25% bonus applied before the payment; `StakingBonusManager` (WO-76) hooked via
  reflection — graceful no-op if absent.
- Glimmer granted on confirmed payment via `GlimmerCurrencyService.TryAddGlimmer()`.
- Sync `BuyWithSOL/SKR/USDC` wrappers call `.Forget()` for Button.onClick compatibility.
- Compiles unconditionally — no SDK types imported here; all guarded code is inside
  `SolanaWalletProvider` (already existing).

---

## Brace counts (mandatory check)

| File | Open | Close | Result |
|---|---|---|---|
| `CosmeticApplier.cs` | 16 | 16 | BALANCED |
| `BattlePassManager.cs` | 30 | 30 | BALANCED |
| `GlimmerCurrencyService.cs` | 30 | 30 | BALANCED |
| `CryptoPaymentManager.cs` | 23 | 23 | BALANCED |

---

## What was NOT built (per reconciliation doc)

| WO-73/74 item | Reason skipped |
|---|---|
| `ShopUI.cs` (uGUI) | Duplicates `CosmeticShopPanel` (HUD, code-built UI Toolkit) + `PackStore` (Wallet). Adding a third shop class would conflict. |
| Scene wiring for ShopUI | Store scene-wiring DISABLED (PIPELINE_STATE §5). Needs own PanelSettings before re-enable. |
| UXML-based UI | UXML does not work in builds (CLAUDE.md §8). |
| `MonetizationManager` | Does not exist and must not be created — covered by `WalletService` + `GlimmerCurrencyService`. |
| `CosmeticData` SO | Does not exist — branch uses `CosmeticDef` (JSON catalog). |

---

## Setup instructions (for Samantha)

1. Create a `BattlePassData` SO asset: right-click → Defenders → BattlePassData.
   Populate `freeTrack[]` and `premiumTrack[]` (length = `tierCount`, default 30).
2. Add `BattlePassManager` to the persistent manager GameObject. Assign the SO to
   `battlePassData`. Wire `seasonName`, `xpPerLevel`, `premiumCostGlimmer`.
3. Add `CryptoPaymentManager` to the same persistent manager GameObject.
   Tune `aetherToSol` / `aetherToUsdc` / `aetherToSkr` in Inspector.
4. For each hero/pet/building prefab that should be skinnable: add `CosmeticApplier`.
   Assign `meshRenderer`, `defaultModel`, `attachmentPoint` in Inspector.
   Assign `materialOverride` / `prefabOverride` / `vfxPrefab` per cosmetic when art exists.
5. Call `BattlePassManager.Instance.AddXP(n)` from wave-clear / quest-complete hooks.
6. Call `CryptoPaymentManager.Instance.ConnectWallet()` when the shop opens.
