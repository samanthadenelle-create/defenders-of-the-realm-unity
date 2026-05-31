# Monetization / Store — Reconciliation Map (WO-72..80 vs feat/tower-core-loop)

**Author:** Claude Code (overnight, 2026-05-28) · **Why:** the WO-72..80 set is written
against the greenfield/other-repo architecture (`MonetizationManager`, `CosmeticData` SO,
`CryptoPaymentManager`, `aetherShards`). This branch already has a **richer, mostly-built**
stack under different names. Building the WOs literally would create **duplicates/conflicts**.
This maps WO intent → branch reality so the morning push is fast and avoids blind duplication.

## What already exists on this branch (verified by survey + reading the code)

| System | Branch implementation | State |
|---|---|---|
| Hard currency / wallet | `DeNelle.Wallet.WalletService` (+ `SolanaWalletProvider` / `StubWalletProvider`), `CurrencyKind` SOL/USDC/SKR | Design-complete, **devnet-stubbed** (no Solana Unity SDK installed; `SOLANA_SDK` define unset) |
| Pack store UI + purchase | `DeNelle.Wallet.PackStore` (UI-Toolkit, `PackCatalog`/`PackDef`), `MarketplaceInteractor` (walk-up `[F]` trigger) | **Connected** — `PackStore.ApplyPackContents()` credits crystals/food/coins to `GameState`, records ownership, `Save()`s, fires `PackPurchased`. Only the real payment is stubbed. |
| Soft currency (cosmetic) | `DeNelle.Cosmetics.GlimmerCurrencyService` — `Owns/TryPurchase/Equip/TryAddGlimmer`, PlayerPrefs `dotr-cosmetics-v1` | Done |
| Cosmetic catalog + shop UI | `CosmeticCatalog` (cosmetics.json), `HUD.CosmeticShopPanel` (C-key, category tabs, buy/equip) | Done — **missing only the visual applier** (id → renderer/material/prefab swap) |
| Battle pass data | `DeNelle.Core.Data.BattlePassData` (+ `BattlePassReward` struct: rewardId/amount/kind) | Authored SO only — **no runtime manager, no authored asset, no XP feed, no UI** |
| Rewarded ads | `Village.Monetization.RewardedAdManager` (8-min cooldown, virtual `ShowAdInternal` seam) | Skeleton — no ad SDK |
| Crypto swap | `Web3.JupiterSwapService` + panel (live quotes) | Quote works; **swap signing stubbed** (`WalletBridgeStub`) |
| Persistence | `GameState` (41 fields) + `GameStateService` + `PersistenceBridge` (wave/scene/quit hooks) | Done |

## WO-by-WO verdict

| WO | Wants (greenfield) | Branch reality | Verdict |
|---|---|---|---|
| 72 MonetizationManager + CosmeticData | unified manager + SO | `WalletService`+`GlimmerCurrencyService`+`PackStore` already cover it; `CosmeticDef` (glimmerCost) ≠ `CosmeticData` (aetherShardPrice) | **Skip / reconcile** — don't add a parallel manager |
| 73 ShopUI + CosmeticApplier + BattlePassSystem | uGUI shop, applier, BP | Shop UI exists (`CosmeticShopPanel`/`PackStore`); **CosmeticApplier genuinely missing** (visual); **BattlePassManager genuinely missing** (but dormant w/o asset+UI) | **Partial** — applier + BP manager are real gaps, but both need authored assets / prefab wiring → do **with editor + eyes** |
| 74 CryptoPaymentManager (Solana) | Solana payments | `WalletService.Pay` already the seam; needs the Solana Unity SDK + `SOLANA_SDK` | **Defer** — needs SDK install (editor) + owner; do not build blind |
| 75 Tabbed ShopUI w/ crypto | crypto tabs | `PackStore` already has SOL/USDC/SKR rail chips | **Skip / reconcile** |
| 76 StakingBonusManager | SKR staking | absent | **Defer** — sensitive (crypto), needs design + owner |
| 77 DailyLoginBonus | daily streak | absent (`RewardedAdManager` only) | **Buildable** later — self-contained PlayerPrefs streak; low risk but needs a UI hook to matter |
| 78 TxVerification + StakingDashboard | receipt validation, dashboard | absent | **Defer** — backend + crypto |
| 79 WarRoomWindow (Editor) | editor tool | absent | **Buildable** (editor-only tool) — low gameplay risk; do when useful |
| 80 Vercel/Neon backend + BackendAPI | server | — | **Out of scope for Unity** — backend lives in the React repo (persistence-pivot decision). Do NOT add here. |

## Genuine, safe gaps worth building (with the owner / editor, where I can verify)

1. **CosmeticApplier** — id → hero/pet/building renderer material or prefab swap. Needs per-cosmetic visual data + prefab wiring + eyes. (`CosmeticDef` currently only has `previewColor`; a first pass could tint by that.)
2. **BattlePassManager** — runtime over `BattlePassData`, grants via `GlimmerCurrencyService` (clean ref: Cosmetics→Core). Needs an authored `BattlePassData` asset + an XP feed + a UI to be non-dormant.
3. **DailyLoginBonus** (WO-77) — self-contained streak/PlayerPrefs + Glimmer grant; pair with a small UI.
4. **Glimmer-from-pack gap** — `PackStore.ApplyPackContents` credits crystals/food/coins but NOT the pack's `Economy.Glimmer` (it's shown in `DescribeContents`). Small fix; cross-asmdef (Wallet→Cosmetics) so needs a bridge.

## Defer (needs SDK / backend / owner sign-off — NOT to be built blind)
Solana payment signing (SDK install), Jupiter swap signing, staking, tx verification, Vercel/Neon backend.

## Recommended morning order
Connect the editor → (1) decide CosmeticApplier visual approach + wire it with eyes; (2) author a `BattlePassData` asset + `BattlePassManager` + a simple BP UI; (3) DailyLoginBonus; (4) owner-gated: install Solana SDK → real payments. Skip 72/75 (duplicates), skip 80 (React repo).
