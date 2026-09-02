# WORK ORDER 1320 — Pi rewarded ads, behind the EXISTING IAdService seam

**Status:** READY TO IMPLEMENT
**Silo:** Monetization / Pi
**Minted:** 2026-09-02 (CLI) on owner instruction, from fetched Pi SDK docs.
**Severity:** P1 feature. Contains one LATENT P0 defect (see below) that must be fixed regardless.

## ⛔ THE LATENT DEFECT — fix this even if the feature is deferred

`Assets/Plugins/WebGL/PiBridge.jslib:192-207` + `WebGLPiPlatform.cs:145-147`:
`case "adReady": _adTcs?.TrySetResult(true);` resolves **`true` for EVERY outcome**.
`AD_CLOSED`, `ADS_NOT_SUPPORTED`, a rewarded ad the player dismissed - all return "rewarded".

`PiCallbackData` (`WebGLPiPlatform.cs:191-199`) declares no `result` and no `adId`, so `JsonUtility`
**silently drops both**. Nothing calls `IPiPlatform.ShowAd` today (grepped - it is dead code), which is
the only reason this has never paid out a free reward.

## The documented API (fetched 2026-09-02 — cite these, do not work from memory)

- https://pi-apps.github.io/pi-sdk-docs/pi-sdk/Ads
- https://pi-apps.github.io/pi-sdk-docs/platform/Ads
- https://raw.githubusercontent.com/pi-apps/pi-platform-docs/master/ads.md
- https://raw.githubusercontent.com/pi-apps/pi-platform-docs/master/platform_API.md

```js
Pi.Ads.isAdReady(type) -> { ready: boolean }
Pi.Ads.requestAd(type) -> { result: "AD_LOADED" | "ADS_NOT_SUPPORTED" | ... }
Pi.Ads.showAd(type)    -> { result: "AD_REWARDED" | "AD_CLOSED" | "ADS_NOT_SUPPORTED" | ..., adId?: string }
```
- `type` is `'interstitial' | 'rewarded'`. **Banner is NOT in the SDK** - it is a Developer Portal toggle.
- **`adId` appears only on rewarded** and is the token the backend verifies.
- **Rewarded ads require an AUTHENTICATED user** (docs state this explicitly).
- Feature detection: `(await Pi.nativeFeaturesList()).includes("ad_network")`.
- `isAdReady`/`requestAd` are the documented ADVANCED path; Pi Browser preloads internally, so a bare
  `showAd` is still valid. We need `isAdReady` anyway because `IAdService.IsRewardedReady` is a SYNC
  property and cannot be answered without it.

⚠ **The result strings are NOT exhaustively documented.** Confirmed: `AD_LOADED`, `AD_REWARDED`,
`AD_CLOSED`, `ADS_NOT_SUPPORTED`. An older in-repo work order claims `AD_NOT_AVAILABLE`; that string
could NOT be confirmed. **Do not write a C# enum that pretends to be exhaustive.** String-compare only
the four confirmed values, log anything else verbatim via `FlowTrace.Warn`, map unknown to a generic
failure.

## Server-side verification is MANDATORY

Docs: *"you must verify the rewarded status of the ad using Pi Platform API, before rewarding users"* -
because players may run hacked SDK builds.

`GET https://api.minepi.com/v2/ads_network/status/<adId>`, header `Authorization: Key <PI_NETWORK_API_KEY>`.
Grant **only** when `mediator_ack_status === "granted"`.

New `api/pi/ads-verify.js`, beside `api/pi/verify.js`, reusing `api/_lib/pi-payments.js`'s
`PI_API_ROOT` / `piApiKey()` / `configured()` (`:90-108`) so the key stays server-side (`pi-payments.js:36`).
Copy the CORS block from `api/pi/verify.js:27-35` - the app is served from `<app>.pinet.com`
(**confirmed: `https://echoesofelarions6578.pinet.com`**), so this is cross-origin.

`mediator_ack_status` can be `null` briefly after the ad. Bounded retry (~3 attempts / 3s), and treat
still-null as NOT granted - **fail closed**, matching the rate fetcher.

## Slot it behind the EXISTING seam. Do NOT build a second ad system.

`Assets/_Modules/Core/Ads/IAdService.cs` is a complete, provider-neutral seam with `AdServices.Register`.
`AdGateService` + `AdPlacementCatalog` + `ad-placements.json` already own placements, caps, cooldowns and
the covenant. `LevelPlayInitializer.cs:255` shows the pattern: **register only AFTER init succeeds.**

- `PiAdProvider : MonoBehaviour, IAdService`, `ProviderName => "PiAds"`, in its OWN leaf asmdef.
  ⚠ The existing `DeNelle.Village.AdProviders.asmdef` carries `defineConstraints: ["LEVELPLAY_PRESENT"]`,
  which would suppress a Pi provider whenever that package is absent. Do not put Pi in it.
- `AdServices` holds exactly ONE `Current`. Gate Pi on `WebGLPiPlatform.IsPiBrowserEnvironment`
  (`:54-60`) AND `ad_network` in `nativeFeaturesList` AND Pi-authenticated; and make
  `LevelPlayInitializer.Install()` (`:128-146`) REFUSE inside Pi Browser so the two never race.
- `IPiPlatform.ShowAd` (`IPiPlatform.cs:33-34`) returns `UniTask<bool>` and **must change** - a bool
  cannot carry `adId`. Replace with a result struct plus `IsAdReady`/`RequestAd`, mirroring how the
  payment path uses a struct. Nothing calls it, so this is a free breaking change; take it now.
- jslib: flatten to `data: { adType, adResult: r.result, adId: r.adId || '' }` (JsonUtility cannot read
  dynamic objects), add `PiIsAdReady`, `PiRequestAd`, `PiNativeFeatures`, and **a local timeout on every
  ad call** - off Pi Browser the SDK can hang ~120s before rejecting (WO-678), and `ShowAd` has none.

## Acceptance criteria

1. A rewarded ad grants ONLY after `api/pi/ads-verify.js` returns `granted`. Prove the refusal path too.
2. Off Pi Browser, or without `ad_network`, or unauthenticated: the provider never registers,
   `NullAdService` answers `Disabled`, and `AdGateService.Offer` hides the button. No hang, no error.
3. `_LAW_1` still holds - ad rewards are time-only (timeskip), never crystals.
4. The `PiShowAd` always-true bug is gone, evidenced by a case that feeds `AD_CLOSED` and asserts no grant.
5. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n>`, with a new suite pinning "no `granted` -> no grant".

## What NOT to touch

- ⛔ No second ad service, manager, pool or placement catalog. One seam.
- ⛔ Do not bypass `AdGateService` - it owns caps, cooldowns and the ledger.
- ⛔ Never grant on the client's `AD_REWARDED` alone. `RewardedAdManager.cs:92-98` already refuses a
  sync grant path for exactly this reason.
- ⛔ The API key is server-side only - never in a client bundle, log line, or committed file.
- ⛔ Do not add interstitials. `IAdService` is rewarded-only by design; that is a separate decision.
- ⛔ Do not invent result strings beyond the four confirmed.

## Open questions for the owner (do NOT guess these)

1. **Consent:** Pi publishes no `SetGDPRConsent`/`SetCCPA` API, so on Pi the existing prompt would be
   asked with nowhere to apply the answer. Ask-and-record, or defer to Pi Browser's own regime?
   This is a legal/product call (`AdConsentService.cs:64-66` says so).
2. **Has the app been approved for the Pi Developer Ad Network in the Developer Portal?** Docs require
   an application. **No approval = no revenue regardless of code.**
3. `ad-placements.json` records a LevelPlay `adUnitId` per placement and warns when one is missing
   (`AdPlacementCatalog.cs:245-248`). Pi has no ad-unit concept. Per-provider unit map, or a
   provider-aware skip?
