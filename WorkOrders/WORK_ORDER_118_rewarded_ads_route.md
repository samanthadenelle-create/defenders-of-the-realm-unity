# WORK ORDER 118 — Rewarded-Ad Monetization Route (the population engine)

**Status:** READY TO IMPLEMENT
**Date:** 2026-05-29
**Priority:** High — this is the **store-build F2P revenue layer** + retention engine for the 95% who never pay
**Lanes:** Monetization/Backend (isolated) · code-only · no scene files · UI code-built
**Blessing:** `docs/NORTH_STAR.md` → "Free tier = rewarded ads = the population engine" + "Two builds, two channels"
**Depends on:** WO-117 (worker-dispatch auto-collect + node defense — the **first ad surface**) · WO-115 (offline-harvest welcome-back popup) · reconciles with WO-120 (backend) for later server validation
**Mirrors:** `Assets/_Modules/Wallet/` provider seam — `IWalletProvider` + `StubWalletProvider` (WalletService.cs)

---

## Why (the vision)

NORTH_STAR is explicit: **~95–98% of players never pay**, and rewarded ads are the only way
to monetize that silent majority — often **30–50% of total revenue**. But the deeper point is
strategic, not just revenue:

> **Free players are the *content*, not a cost.** In a competitive game the whales pay to raid
> bases, climb ladders, win tournaments — which **requires a full stadium of opponents.** The
> free majority *are* those opponents / clan-mates / ladder / raid targets. Rewarded ads let them
> participate by paying with **attention instead of cash** — which simultaneously (1) monetizes
> the 95%, (2) retains them, (3) **keeps the whale economy worth paying into.**

So the design principle for every placement in this WO is one sentence:

> **Each rewarded ad grants the same benefit a spender would buy — earned with attention instead of cash.**

A non-spender who watches an ad gets the *exact* convenience a Pack buyer gets (per
`docs/monetization-v2-spec.md` §5.3: instant-collect, auto-collect, time-savers). Never combat
power (C1 holds). Never a wall, never a gate, never progress — **a path, never a wall.**

---

## Discipline guardrails (non-negotiable — from NORTH_STAR)

1. **Opt-in / rewarded ONLY.** No interstitials, no banners, no forced ads. Every ad is a button
   the player chooses to tap for a stated reward. Progress is **never** gated behind an ad.
2. **STORE BUILD ONLY.** Ads are physically **compiled out** of the crypto (Solana/Pi) builds —
   not flagged off at runtime. See §4 (asmdef strip). The crypto builds monetize via tournament
   buy-ins, not attention (store-policy + brand/feel separation, NORTH_STAR "Two builds").
3. **Per-placement daily caps + cooldowns.** Every placement has a `maxPerDay` and a `cooldownSec`.
   No infinite money-printing; ads stay a *bonus lever*, not a grind.
4. **No FOMO, no dark patterns** (covenant C4). Ad CTAs are dismissible, never auto-open, never
   pop up mid-combat (covenant C5). The player initiates.
5. **No combat power.** Rewards are the §5.3 convenience class + soft currency only. Never damage,
   range, HP, wall strength, or resource caps.

---

## Architecture — mirror the wallet provider seam

The ad provider is **a provider behind an interface**, exactly like the payment rail
(`IWalletProvider` → `StubWalletProvider` / `SolanaWalletProvider`). NORTH_STAR: *"ads are a
provider behind an interface ('like adding Stripe')."* Reconcile with the existing pattern; do
**not** invent a parallel store.

### Provider interface — `IAdProvider`

Lives in a **new** assembly `DeNelle.Ads` (namespace `DeNelle.Ads`). DESIGN-ONLY illustrative
shape — CLI owns the final signatures:

```csharp
// DeNelle.Ads — IAdProvider.cs   (DESIGN-ONLY illustrative)
namespace DeNelle.Ads
{
    public enum AdLoadState { Unloaded, Loading, Ready, NoFill, Error }

    public interface IAdProvider
    {
        string ProviderName { get; }
        AdLoadState StateFor(string placementId);

        /// <summary>Pre-fetch a rewarded ad so the button can light up only when Ready.</summary>
        void Preload(string placementId);

        /// <summary>
        /// Show a rewarded ad. onComplete fires ONLY on a verified full-watch
        /// (the reward-granting callback). onFailed fires on no-fill / skip / error —
        /// the reward is NOT granted, and the UI says so cleanly (never silently fails).
        /// </summary>
        void ShowRewarded(string placementId, Action onComplete, Action<string> onFailed);
    }
}
```

### Stub provider — `StubAdProvider` (editor / no-SDK)

Mirrors `StubWalletProvider` precisely: lets the whole ad surface compile + run **today** with no
SDK installed, so every placement is exercisable in the editor before LevelPlay lands.

```csharp
// DeNelle.Ads — StubAdProvider.cs   (DESIGN-ONLY illustrative)
public sealed class StubAdProvider : IAdProvider
{
    public string ProviderName => "Editor Stub Ads";
    public AdLoadState StateFor(string placementId) => AdLoadState.Ready; // always fillable in editor
    public void Preload(string placementId) { /* no-op */ }

    public void ShowRewarded(string placementId, Action onComplete, Action<string> onFailed)
    {
        // Editor: simulate a short watch, then INSTANT SUCCESS — grants the reward
        // so the full opt-in→reward loop is testable with no network.
        Debug.Log($"[StubAdProvider] Simulated rewarded watch complete — {placementId}.");
        onComplete?.Invoke();
    }
}
```

### Real provider — `LevelPlayAdProvider` (Unity Ads / LevelPlay, store build only)

Behind the **same** interface; the only genuinely new piece (like `SolanaWalletProvider` was). It
is gated by a define so the crypto build never compiles it (§4). DESIGN-only — CLI wires the SDK:

```csharp
// DeNelle.Ads — LevelPlayAdProvider.cs   (DESIGN-ONLY, store build only)
#if ADS_LEVELPLAY
public sealed class LevelPlayAdProvider : IAdProvider
{
    public string ProviderName => "Unity LevelPlay";
    // Map placementId → LevelPlay ad unit; LoadRewardedVideo / ShowRewardedVideo;
    // OnAdRewarded → onComplete;  OnAdShowFailed / OnAdClosed-without-reward → onFailed.
    // ... SDK glue ...
}
#endif
```

### Service façade — `AdService`

Mirrors `WalletService` (the seam owner). Resolves the active `IAdProvider`
(`StubAdProvider` in editor / no-SDK; `LevelPlayAdProvider` when `ADS_LEVELPLAY` is defined),
enforces caps + cooldowns, and routes the `onComplete` callback into the **existing economy** —
never a new currency store:

```csharp
// DeNelle.Ads — AdService.cs   (DESIGN-ONLY illustrative)
public bool TryWatch(string placementId, Action onGranted)
{
    if (!IsEligible(placementId)) return false;          // cap / cooldown gate
    _provider.ShowRewarded(placementId,
        onComplete: () => {
            RecordWatch(placementId);                    // stamp cap/cooldown
            onGranted?.Invoke();                         // caller grants via EconomyService etc.
        },
        onFailed: reason => CoreServices.Hud?.ShowToast($"Ad unavailable — {reason}"));
    return true;
}
```

**Reward granting** flows through the systems that already exist, with `?.` on cross-module calls
(CLAUDE.md §10):
- soft-currency bonus → `EconomyService.Grant(...)` (DeNelle.Village) / `CrystalEconomy`
- instant-collect / auto-resolve → the **WO-117 worker-dispatch** API
- double-offline → the **WO-115 welcome-back** accrual multiplier

`AdService` never mutates economy state directly — it hands the verified `onComplete` back to the
caller, exactly as `WalletService` hands a `PaymentResult` back to `PackStore`.

---

## The ad surfaces (placements)

Tied to the worker / auto-collect loop (WO-117) as the headline. Each placement = "the same
benefit a spender would buy, earned with attention." Caps/cooldowns are starting values — owner
tunes.

| # | Placement ID | What the player gets | Spender-equivalent | Cap/day | Cooldown |
|---|---|---|---|---|---|
| 1 | `collect_now` | **Watch-to-instant-collect** — fill a node's store to cap *right now* (skip the timer) | Pack §5.3 "harvest auto-collect" / instant token | 6 | 60s |
| 2 | `node_shield` | **Watch-to-shield / auto-resolve** a random-encounter invasion on a node (the "safe" lever — no manual defense) | convenience "instant-repair" class | 3 | 90s |
| 3 | `double_rate` | **Watch-to-double collect rate** for the next cycle on a chosen node | "2× harvest" buff | 4 | 120s |
| 4 | `double_offline` | **Watch-to-double the OFFLINE accrual** — shown on the WO-115 welcome-back popup | Pack auto-collect headstart | 1 | once / return |
| 5 | `bonus_grant` | **Generic watch-for-bonus** — a small crystal/resource grant | economy top-up (small) | 5 | 90s |

**Headline = WO-117.** Placements 1–3 hang directly off worker-dispatch / node auto-collect; they
are the reason this WO depends on WO-117. Placement 4 is the WO-115 surface. Placement 5 is the
always-available baseline lever.

**Framing rule for UI copy:** every button states the reward and frames it as *the same benefit a
supporter pack grants* — e.g. *"Watch a short ad to collect this node now — same as a Hearth Spark
instant-collect."* Cozy, opt-in, dismissible.

---

## The two-build separation — compliance by construction (§4)

NORTH_STAR: the store build must **compile crypto OUT**, and (symmetrically) the crypto builds must
**compile ADS OUT** — *physically absent from the binary*, not runtime-flagged. The existing
modular-asmdef strip (`DeNelle.Wallet` / `DeNelle.Web3` excluded from the store build) is the exact
precedent. `DeNelle.Ads` is the mirror image on the other build.

**Mechanism (mirrors the wallet `versionDefines` / asmdef pattern):**
- New assembly `DeNelle.Ads` (`Assets/_Modules/Ads/DeNelle.Ads.asmdef`), references **`DeNelle.Core`
  only** (plus `DeNelle.Village` for the economy grant, per the cross-assembly rule — Village→Core,
  Ads→Core+Village; never Ads↔HUD directly, use `CoreServices.Hud`).
- The real provider is gated behind `#if ADS_LEVELPLAY` (a `versionDefines` entry keyed to the
  LevelPlay package, mirroring `DeNelle.Wallet.asmdef`'s `com.solana.unity_sdk → SOLANA_SDK`). No
  SDK present → only `StubAdProvider` compiles.
- **Crypto/Pi build** = exclude the `DeNelle.Ads` assembly from the build target (same
  build-define / assembly-exclusion lever the store build uses to drop `DeNelle.Wallet` /
  `DeNelle.Web3`). The crypto binary ships with **zero ad code + zero ad SDK**.
- **Store build** = include `DeNelle.Ads`; define `ADS_LEVELPLAY` so `LevelPlayAdProvider` is live;
  `CurrencyKind` crypto rails + tournament module flagged OFF (per `monetization-v2-spec` two-build).

> Net: `CurrencyKind` + feature flags handle Pi-vs-Solana; the **modular-asmdef strip** handles
> no-crypto-store-build **and** no-ads-crypto-build. Two strips, one pattern.

---

## Reward integrity (v1 device-side, server-validated later)

- **v1:** the rewarded-ad `onComplete` callback grants the reward **device-side**. This is fine for
  the soft / cosmetic / convenience rewards in this WO (no crypto, no real-money value) — the worst
  case of a spoofed callback is a free instant-collect, not stolen value.
- **Flag for later (ties to WO-120 backend lane):** high-value rewards (anything that ever touches
  the SKR yield economy / leaderboard standing in `monetization-v2-spec` §12) **must be
  server-validated** — Unity Ads / LevelPlay S2S reward callbacks verified by the backend before
  granting, exactly as IAP receipts are S2S-validated (NORTH_STAR: *"verify on the server, never the
  client"*). Out of scope for v1; note it in the AdService header so WO-120 picks it up.

---

## Monetization matrix recap (reconcile, don't duplicate)

| Build | Channel | Free majority | Spenders |
|---|---|---|---|
| **Store F2P** | Google Play / iOS | **rewarded ads (opt-in)** — THIS WO | fiat IAP / Packs (`PackStore`, monetization-v2) |
| **Solana crypto** | web / sideload | — (no ads) | tournament buy-ins / pot |
| **Pi** | Pi / sideload | — (no ads) | tournament buy-ins / pot |

Store build: **rewarded ads + IAP**. Crypto builds: **tournament buy-ins — NO ads.**

---

## Files to Create / Edit

| File | Action | Notes |
|---|---|---|
| `Assets/_Modules/Ads/DeNelle.Ads.asmdef` | **Create** | New assembly; references `DeNelle.Core`, `DeNelle.Village`, `UniTask`; `versionDefines` → LevelPlay pkg = `ADS_LEVELPLAY` (mirror wallet asmdef) |
| `Assets/_Modules/Ads/IAdProvider.cs` | **Create** | The provider seam (mirrors `IWalletProvider`) |
| `Assets/_Modules/Ads/StubAdProvider.cs` | **Create** | Editor / no-SDK instant-success stub (mirrors `StubWalletProvider`) |
| `Assets/_Modules/Ads/LevelPlayAdProvider.cs` | **Create** | Real Unity Ads / LevelPlay provider, `#if ADS_LEVELPLAY` gated, store-build only |
| `Assets/_Modules/Ads/AdService.cs` | **Create** | Façade: resolves provider, enforces caps/cooldowns, routes `onComplete` to caller (mirrors `WalletService`) |
| `Assets/_Modules/Ads/AdPlacements.cs` | **Create** | Const placement IDs + cap/cooldown config (table above), tunable |
| `Assets/_Modules/Ads/RewardedAdButton.cs` | **Create** | Code-built UI button (NO UXML — PIPELINE_STATE): label, Ready/locked state from `StateFor`, calls `AdService.TryWatch`, shows result toast via `CoreServices.Hud?` |
| `Assets/_Modules/Ads/Tests/AdServiceTest.cs` | **Create** | EditMode: cap/cooldown gating, stub-success grant path, no-fill→no-grant (mirror `StubWalletProviderTest`) |
| WO-117 worker/auto-collect API | **Consume** | Placements 1–3 call its instant-collect / shield / rate-buff entry points via `?.` — do not edit if it already exposes them; otherwise WO-117 adds the hooks |
| WO-115 welcome-back popup | **Consume** | Placement 4 (`double_offline`) adds the opt-in button to the existing popup |
| `CoreServices` (Core) | **Consume only** | Resolve `CoreServices.Hud?` for toasts; do **not** add an Ads ref to HUD/Village asmdefs |

**Do NOT touch:** `VillageSceneBuilder.cs`, any `.unity` scene, `DeNelle.Wallet` / `DeNelle.Web3`,
`PackStore` (this is the *attention* rail beside it, not a replacement).

---

## Acceptance Criteria

- [ ] `DeNelle.Ads` compiles with **no SDK installed** — `StubAdProvider` is the active provider in
      editor; every placement is exercisable end-to-end with instant simulated success.
- [ ] `IAdProvider` / `StubAdProvider` / `AdService` mirror the `IWalletProvider` /
      `StubWalletProvider` / `WalletService` seam (same shape, same swap-the-provider story).
- [ ] All five placements (`collect_now`, `node_shield`, `double_rate`, `double_offline`,
      `bonus_grant`) wired; rewards route through `EconomyService` / WO-117 / WO-115 with `?.`.
- [ ] Every ad is **opt-in** (a player-tapped button) — no interstitial, no banner, no auto-open,
      no progress gated behind an ad anywhere.
- [ ] Per-placement **daily cap + cooldown** enforced; button shows locked/Ready state; over-cap or
      cooling-down → button disabled with cozy copy, never an error.
- [ ] `onFailed` (no-fill / skip / error) grants **nothing** and shows a clean toast — never a
      silent failure, never a partial grant.
- [ ] **Crypto-build exclusion proven:** with `DeNelle.Ads` excluded (crypto build target), the
      project still compiles and contains **zero ad code / zero ad SDK**. With it included +
      `ADS_LEVELPLAY` defined (store build), `LevelPlayAdProvider` is the active provider.
- [ ] Reward-integrity note present in `AdService` header flagging server-side validation for
      high-value rewards as a WO-120 follow-up; v1 device-side grant is intentional + documented.
- [ ] UI is **code-built** (no UXML); cross-module calls use `?.`; brace-balance gate passes on
      every `.cs` file touched (CLAUDE.md §1).
- [ ] No combat-power reward anywhere (convenience + soft currency only); covenant C1 + C4 + C5 hold.

---

## Notes / flags for the owner

- **WO-117 is the spine of this WO** and isn't on disk yet — placements 1–3 assume its
  worker-dispatch / node auto-collect API exists. Sequence WO-117 first (it's already the Sunday
  priority); this WO consumes it.
- **Define name `ADS_LEVELPLAY`** is a proposal mirroring `SOLANA_SDK` — confirm against the actual
  LevelPlay package name when CLI installs the SDK.
- **Cap/cooldown numbers are starting values** for tuning, not locked.
- **Provider choice:** spec'd for **Unity Ads / LevelPlay** per NORTH_STAR (first-party, in-engine,
  mediated fill). Behind `IAdProvider` so a different mediator could swap in later.
