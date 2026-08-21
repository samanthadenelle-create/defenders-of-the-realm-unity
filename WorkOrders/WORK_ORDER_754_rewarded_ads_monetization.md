# WORK ORDER — 754: Rewarded-Ad Monetization (IAdService seam + first live placement)

**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.
**Author:** Monetization Architect (design only — no `.cs` written, per CLAUDE.md §2/§13)
**Silo:** Monetization/Backend (§9 — isolated, parallel-safe lane; no scene files, no VillageSceneBuilder)
**Date:** 2026-07-19
**North star (KEY_FACTS.md):** *"Monetization = rewarded-ad income paths, never a wall."*
**Covenant (binding, honesty law):** ads are always **opt-in**; the reward is a bonus, the base
action always completes without one; no dark patterns, no forced interstitials, no pay-to-win.
V1 ships **ZERO crypto**; soft currency is client-owned. This WO does not touch the wallet rail.

---

## 0. TL;DR — this is a WIRE-UP, not a greenfield

Monetization is ~70% built (CLAUDE.md §8). A rewarded-ad spine **already exists in data + a stub
seam**; what's missing is (a) a real ad SDK behind the seam and (b) a clean `DeNelle.Core` interface
so any module can offer an ad without referencing the Village assembly. This WO:

1. Adds one Core interface — **`IAdService`** — mirroring the `IAudioService`/`CoreServices` pattern.
2. Keeps the existing **`RewardedAdManager`** (DeNelle.Village) as the reward-policy gate, and makes it
   *implement/route through* `IAdService` instead of hard-coding the stub grant.
3. Adds two `IAdService` implementations: a **stub** (editor + WebGL/Pi + headless) and a **LevelPlay**
   (Unity Ads/ironSource) Android impl **behind a feature flag**.
4. Wires **one** live placement end-to-end (the build-timer skip, which is already coded against the
   seam) so we ship a real, felt rewarded-ad with the SDK — everything else stays data-ready.

**Do NOT** re-implement the placement table, the reward vocabulary, `EventTracker`, or the
economy grant. They exist. This WO slots an SDK under them.

---

## 1. SME AUDIT — what already exists (cite before you build)

| Piece | File:line | State |
|---|---|---|
| Rewarded-ad **SDK seam** (stub gate) | `Assets/_Modules/Village/Monetization/RewardedAdManager.cs:33` (`TryShowAd(Action)` :83, `ShowAdInternal` virtual :97, 480s cooldown :36) | LIVE stub — grants reward immediately, no SDK. **This is THE integration point** (its own `// TODO integrate Unity Ads / AdMob` :96). |
| Placement/reward **data catalog** | `Assets/Resources/Data/Canonical/ad-placements.json` (+ StreamingAssets twin) | LIVE data: 5 placements, 7 rewards, `global.adProvider` = `stub`\|`unityads`\|`admob`, `hardDailyCap` 12, covenant line. |
| `AdGateService` interpreter (reads that JSON) | designed in `WorkOrders/WORK_ORDER_ad_generator.md` §A | **NOT implemented** — no `AdGateService.cs` exists. Optional follow-on; NOT required for this WO's first cut. |
| Live placement wired to the seam | `Assets/_Modules/Village/Buildings/BuildTimerService.cs:254` (`CanWatchAdToSkip`), `:268` (`WatchAdToSkip` → `RewardedAdManager.TryShowAd`) | LIVE — already opt-in, daily-capped (`GameState.AdSkipsUsedToday`), never a wall (timer always finishes). **The proof placement.** |
| Cross-assembly service registry to mirror | `Assets/_Modules/Core/CoreServices.cs:32` (Hud/Audio/Jupiter/Population/WalletSigner slots, Register/Unregister pattern) | LIVE — the pattern `IAdService` follows. |
| Core interface exemplar | `Assets/_Modules/Core/Audio/IAudioService.cs` | LIVE — shape/namespace template for `IAdService`. |
| Feature-flag pattern | `Assets/_Modules/Core/FeatureFlags.cs:621` (`Get(name, defaultOn)`, PlayerPrefs `ff.<name>`) | LIVE — where the SDK gate flag lives. |
| Analytics | `Assets/_Modules/Core/Analytics/EventTracker.cs:109` (`Track(name, props)`) | LIVE — ad funnel events post to backend. |

### The reward-grant seams a rewarded ad hooks into (all LIVE, cite these)

| Reward kind | Grant seam | File:line |
|---|---|---|
| Currency: crystals/food/wood/iron/coins | `EconomyService.Grant(ResourceCost)` / `AddResource(ResourceType,int)` / `AddCoins(int)` | `Assets/_Modules/Village/EconomyService.cs:294, :393, :463` |
| Crystals / Food (single wallet) | `GameStateService.AddCrystals` / `AddFood` (routed via EconomyService) | `EconomyService.cs:286, :284` |
| Glimmer (cosmetic-shop soft currency) | `GlimmerCurrencyService.TryAddGlimmer(int)` | `Assets/_Modules/Cosmetics/GlimmerCurrencyService.cs:193` (called cross-asmdef by reflection today) |
| Time-skip (build/upgrade) | `BuildTimerService.WatchAdToSkip` → `ApplySkipSeconds` / `CompleteJob` | `BuildTimerService.cs:268, :309, :335` |
| Harvest doubler (offline) | `OfflineHarvestService` (multiplier window) | `Assets/_Modules/Village/Harvest/OfflineHarvestService.cs:50` |
| Battle continue/revive | battle-continue effect (gated `ff.overworldencounter`) | placement `place.defeat.continue` in `ad-placements.json` |

**Conclusion:** the policy, the data, the caps, and the grant seams are all present. The single real
gap is *"a real ad is never actually shown"* (`RewardedAdManager.ShowAdInternal` just calls the reward).
This WO fills that gap and gives it a Core-clean home.

---

## 2. AD SDK RECOMMENDATION (Android APK on Seeker + web/Pi surface)

### Constraints that decide it
- **Engine:** Unity 6, Android APK (`AndroidBuild.BuildSeekerApk`, `BuildOptions.None` — a RELEASE build).
- **Distribution:** **sideloaded** onto the Solana Seeker — **no Google Play install path**. The ad SDK
  must not assume Play-Store distribution or a Play-Store listing to serve.
- **Format:** rewarded video only (never interstitial/banner — the covenant forbids walls).
- **Second surface:** mobile web / Pi Browser = a **WebGL** build. **No native mobile ad SDK runs in
  WebGL** — AdMob/LevelPlay/AppLovin are Android/iOS native only. Web needs a separate path (see §2.3).

### 2.1 Options evaluated

| SDK | Rewarded video | Unity 6 Android | Sideloaded (no Play Store) | eCPM / fill | Friction |
|---|---|---|---|---|---|
| **Unity LevelPlay** (Unity Ads + ironSource mediation, merged product) | First-class | First-party UPM package (`com.unity.services.levelplay`) | **Yes** — install-source agnostic; no Play-listing requirement, no Google Play Services dependency to serve | Good, and mediation lifts it (can add AdMob/AppLovin as mediated networks later) | **Lowest** — native to the engine, one package, Unity dashboard app key |
| **Google AdMob** (Google Mobile Ads Unity plugin) | First-class | Supported plugin | Technically yes, but AdMob's policy/ops assume a store listing and it pulls **Google Play Services**; a sideloaded Seeker build is a policy grey area and a heavier dependency | **Highest** raw eCPM | Higher — GMS dependency, app-review/listing friction, account-approval risk on a non-Play app |
| **AppLovin MAX** | First-class | Supported plugin | Yes | High, strong mediation | Medium — third-party SDK + dashboard, no engine-native path |

### 2.2 Recommendation: **Unity LevelPlay** for the Android APK

Rationale, in priority order:
1. **Install-source agnostic.** LevelPlay serves regardless of how the APK got on the device — the
   right property for a sideloaded Seeker build with no Play Store path. AdMob's model leans on a
   Play listing + Google Play Services; that is exactly the friction we don't want on the Seeker.
2. **Engine-native, one dependency.** It ships as a Unity package (`com.unity.services.levelplay`),
   integrates through Unity Gaming Services, and needs no third-party Gradle surgery — lowest risk to
   `AndroidBuild.BuildSeekerApk`.
3. **Mediation is the eCPM answer.** Start with Unity Ads demand; **add AdMob and AppLovin as mediated
   networks inside LevelPlay later** to lift fill/eCPM without another engine integration. We get
   AdMob's economics eventually without AdMob's distribution friction now.
4. **Rewarded video is its flagship format**, with a completion callback that maps 1:1 onto our
   "grant only on genuine completion" contract.

AdMob stays the **Phase-2 lever** — added as a LevelPlay-mediated network once revenue justifies the
account/listing work, not a separate SDK integration.

### 2.3 The WebGL / Pi Browser caveat (must be stated to the owner)
No native mobile ad SDK runs in a WebGL build. For V1 the **web/Pi surface ships the stub `IAdService`**:
the ad-offer buttons are simply **hidden on web** (the base action still completes — covenant-safe), OR
a later web-only rewarded provider (an HTML5 rewarded network) is dropped behind the same `IAdService`
interface. Either way the interface is the seam; **web = no ad, never a broken ad button** (this matches
the Demo Law in `FeatureFlags.cs`: a reachable feature must WORK or be HIDDEN). No web work ships in the
first cut beyond "offers hidden when `IAdService.IsRewardedSupported == false`".

---

## 3. ARCHITECTURE — `IAdService` in DeNelle.Core (mirror CoreServices)

### 3.1 New Core interface — `Assets/_Modules/Core/Ads/IAdService.cs` (namespace `DeNelle.Core.Ads`)

```csharp
namespace DeNelle.Core.Ads
{
    /// <summary>Result of a rewarded-ad attempt (completion is the ONLY path that grants).</summary>
    public enum AdShowResult { Completed, Skipped, Failed, NotReady, Unsupported }

    /// <summary>
    /// Cross-assembly rewarded-ad seam. Resolved via CoreServices.Ads (always null-check).
    /// Implementations: StubAdService (editor/web/headless — completes immediately) and the
    /// real LevelPlayAdService (Android, behind ff.livead). Rewarded ONLY — no banners/interstitials.
    /// </summary>
    public interface IAdService
    {
        /// <summary>True when a rewarded ad could be shown right now (SDK ready + not cooling down).
        /// Web/unsupported platforms return false so call-sites can HIDE the offer (Demo Law).</summary>
        bool IsRewardedReady { get; }

        /// <summary>False on platforms with no rewarded support (WebGL/Pi) — offers are hidden, not broken.</summary>
        bool IsRewardedSupported { get; }

        /// <summary>Show a rewarded ad for a stable placementId. onResult fires with Completed ONLY on a
        /// genuine full view; the caller grants the reward there and nowhere else. Never throws.</summary>
        void ShowRewarded(string placementId, System.Action<AdShowResult> onResult);
    }
}
```

- **Assembly:** lives in `DeNelle.Core` (new folder `Core/Ads/`). No new asmdef — Core already ships the
  interface layer.
- **Registry slot:** add to `CoreServices.cs` mirroring the Audio slot verbatim:
  `public static IAdService Ads { get; private set; }` + `RegisterAds(IAdService)` / `UnregisterAds(IAdService)`
  with the same double-register `FlowTrace.Warn` + null-check discipline (`CoreServices.cs:107` is the template).
- Call-sites use `CoreServices.Ads?.ShowRewarded(...)` and gate visibility on
  `CoreServices.Ads?.IsRewardedSupported == true && CoreServices.Ads.IsRewardedReady`.

### 3.2 Stub implementation — `Assets/_Modules/Core/Ads/StubAdService.cs`

- `sealed class StubAdService : IAdService` (plain C#, not a MonoBehaviour — no scene need).
- `IsRewardedSupported => true` in editor/standalone/headless; **`false` on `RuntimePlatform.WebGLPlayer`**
  (so web hides offers). `IsRewardedReady => true`.
- `ShowRewarded` → `FlowTrace.Step("Ad","stub-complete",placementId)` then
  `onResult?.Invoke(AdShowResult.Completed)` (immediate grant — keeps headless/devnet flows working, matching
  today's `RewardedAdManager` stub behavior).
- This is the **default** registration so nothing is null before the real SDK loads or on unsupported
  platforms.

### 3.3 Real SDK implementation — `Assets/_Modules/Ads/LevelPlayAdService.cs` (behind a flag)

- New leaf assembly **`DeNelle.Ads`** (`DeNelle.Ads.asmdef` → references `DeNelle.Core` only;
  `autoReferenced:true`), so the SDK dependency is isolated (Core stays SDK-free). Mirrors how
  `DeNelle.Wallet` isolates the Solana SDK.
- `versionDefine`: `com.unity.services.levelplay` → define **`LEVELPLAY_SDK`** (empty version expression,
  like the Wallet asmdef's `SOLANA_SDK` — flips on the moment the package resolves). **ALL SDK type/method
  references live inside `#if LEVELPLAY_SDK`**; with the package absent the class compiles as a no-op that
  reports `IsRewardedReady=false` (identical safety model to `SolanaWalletProvider`).
- Behavior: `Init` on Awake (App Key from config, see §3.5) → load a rewarded ad → on the SDK's
  `OnAdRewarded`/`OnAdClosed` callbacks map to `AdShowResult.Completed`/`Skipped`, on load/show error →
  `Failed`. Pre-loads the next ad after each show. Every SDK call/method tagged `// SDK-VERIFY:` (names
  confirmed against the resolved LevelPlay package version at integration — same discipline as
  `SolanaWalletProvider`).
- **Self-bootstrap:** a `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` bootstrap (in `DeNelle.Ads`)
  registers the real service **only when** `Application.platform == Android` **AND** `FeatureFlags.LiveAd`
  is ON **AND** `LEVELPLAY_SDK` is defined; otherwise `StubAdService` is registered. One owner, always
  non-null.

### 3.4 New feature flag — `FeatureFlags.LiveAd` (`ff.livead`, default **OFF**)

Add to `FeatureFlags.cs` following the existing doc-comment convention:
> When ON (and on Android with the LevelPlay package present), the real LevelPlay rewarded-ad service is
> registered as `CoreServices.Ads`; when OFF (default), the `StubAdService` is used (immediate grant, no
> SDK). "Unflag when proven" — ships OFF until a real ad is confirmed rendering + rewarding on the Seeker.
> PlayerPrefs `ff.livead` = 1 to enable on a test build. **This flag is NOT URL-activatable** (it is a
> monetization flag — the `s_urlActivatableFlags` allow-list in `FeatureFlags.cs:636` explicitly bars
> monetization flags; do not add it there).

### 3.5 SDK config (owner-supplied, no secrets in git)

- New `Assets/Resources/Data/Canonical/ad-sdk-config.json` (public config only — App Key + placement/ad-unit
  ids are publishable client ids, NOT secrets; same class of value as the AdMob app-id):
  `{ "provider":"levelplay", "androidAppKey":"", "rewardedPlacement":"DefaultRewardedVideo", "testMode":true, "testDeviceIds":[] }`.
- Loaded via `CanonicalJson.Read` (WebGL-safe, the established loader). Empty `androidAppKey` → the real
  service logs once and degrades to stub (never crashes). The owner fills these in §7.

### 3.6 How `RewardedAdManager` changes (keep it — it's the POLICY gate)

`RewardedAdManager` stays as the reward-cooldown/cap policy in `DeNelle.Village`, but its
`ShowAdInternal` (`RewardedAdManager.cs:97`) stops self-granting and instead **routes through the Core
seam**:

```
protected virtual void ShowAdInternal(Action onReward)
{
    var ads = DeNelle.Core.CoreServices.Ads;
    if (ads == null || !ads.IsRewardedReady) { onReward?.Invoke(); return; } // stub/degrade path = today's behavior
    ads.ShowRewarded("build.skip", r => { if (r == AdShowResult.Completed) onReward?.Invoke(); });
}
```

- Result: **`BuildTimerService.WatchAdToSkip` is unchanged** — it already calls `TryShowAd(onReward)` and
  the reward (`ApplySkipSeconds`) only lands on completion. The reward now fires after a *real* ad on
  Android, and *immediately* under the stub (editor/web) — same public contract.
- This is the minimal change to `RewardedAdManager` the ad_generator WO permitted ("optionally let it call
  the seam", `WORK_ORDER_ad_generator.md:171`).

### 3.7 Analytics (honesty + funnel)

At the `IAdService` layer, `Track` the funnel through the existing `EventTracker` (`EventTracker.cs:109`):
`ad_offer_shown`, `ad_started`, `ad_completed`, `ad_skipped`, `ad_failed` — each with
`{ placementId, provider }`. No new analytics system; reuse the batched/offline-safe tracker.

---

## 4. REWARDED-AD INCOME PATHS (the felt design — "never a wall")

Every path below already has its grant seam (§1) and most already have a data placement in
`ad-placements.json`. Each is **opt-in, capped, and the base action completes without the ad.**

| Placement (data id) | Felt offer | Grant seam it hooks | Cap / honesty |
|---|---|---|---|
| `place.build.skip` **(FIRST CUT)** | "Finish this build now — watch a short clip" | `BuildTimerService.WatchAdToSkip` → `ApplySkipSeconds` (`BuildTimerService.cs:268`) | Timer ALWAYS finishes on its own; ad shaves `adSkipSeconds`. Daily cap `GameState.AdSkipsUsedToday` + 480s cooldown. Instant-finish also purchasable with crystals — ad is the free path. |
| `place.harvest.doubler` | "Double your offline harvest for 1 hour" | `OfflineHarvestService` multiplier window | Passive income is already earned; ad multiplies the *next* claim, doesn't gate it. dailyCap 3. |
| `place.store.crystals` | "Free crystals — watch a clip" (in the shop, next to the never-a-wall covenant) | `EconomyService.Grant(crystals:150)` (`EconomyService.cs:294`) | Pure bonus faucet, dailyCap 4. Store also sells crystals; ad is the free alternative, never required. |
| `place.daily.chest` | "Claim today's bonus chest" (one free watch/day) | `EconomyService.Grant(coins+crystals)` | dailyCap 1 — a daily retention nudge, always claimable free. |
| `place.defeat.continue` | "Get back up — revive and continue once" (gated `ff.overworldencounter`) | battle-continue effect | dailyCap 2. The run is never blocked — retry-from-checkpoint always exists; the ad only saves the *current* attempt. |
| (later) Glimmer trickle | "Watch for +15 glimmer" on the cosmetic shop | `GlimmerCurrencyService.TryAddGlimmer` (`GlimmerCurrencyService.cs:193`) | Cosmetic-only currency — perfectly covenant-aligned (flex, not power). maxStack 3. |

**Frequency discipline:** the global `hardDailyCap` (12, in `ad-placements.json`) sums across all
placements; per-placement cooldowns + daily caps layer under it. No placement auto-opens; every one is a
button the player chooses to press. The covenant line ("Ads are always optional. You are never required
to watch one. Ever.") renders on every ad surface.

---

## 5. ROLLOUT

### First cut (this WO — ships behind `ff.livead` OFF, provable in editor immediately)
1. `IAdService` + `AdShowResult` in `DeNelle.Core.Ads`.
2. `CoreServices.Ads` slot (Register/Unregister).
3. `StubAdService` + its default bootstrap (registered everywhere until the real SDK opts in). Web returns
   `IsRewardedSupported=false`.
4. `RewardedAdManager.ShowAdInternal` routes through `CoreServices.Ads` (§3.6).
5. `EventTracker` ad-funnel events at the seam.
6. **One live placement:** `place.build.skip` — already wired; now it's the real end-to-end path.
7. `DeNelle.Ads` asmdef + `LevelPlayAdService` compiled as a **no-op** (package not yet added) so the tree
   is green and the seam is real. `ad-sdk-config.json` shipped with empty keys.
8. Regression: an EditMode test that registers a fake `IAdService`, calls `WatchAdToSkip`, asserts the
   reward lands only on `Completed` and never on `Failed`/`Skipped` (mirrors `RewardedAdManager`/BuildTimer
   test style). Headless AutoPilot confirms the stub path grants and the funnel events fire.

### Phase 2 (later WO, after owner adds the SDK + keys)
- Add the LevelPlay package; flip `LEVELPLAY_SDK` on; confirm `LevelPlayAdService` renders a real rewarded
  ad on the Seeker; owner felt-verifies; flip `ff.livead` ON for the test APK.
- Add AdMob + AppLovin as **mediated networks inside LevelPlay** for eCPM.
- Implement the `AdGateService` interpreter (`WORK_ORDER_ad_generator.md` §A) so placements are fully
  data-driven and the other four placements (harvest/store/daily/defeat) light up from JSON with no new code.
- Web/Pi: either keep offers hidden or drop an HTML5 rewarded provider behind the same `IAdService`.

### Owner setup — REQUIRED before Phase 2 can render a real ad (see §7)

---

## 6. WHAT NOT TO TOUCH
- Do **NOT** greenfield a placement table, reward vocabulary, or analytics — `ad-placements.json`,
  `packs.json`, and `EventTracker` exist.
- Do **NOT** rewrite `RewardedAdManager` — only reroute `ShowAdInternal` through `CoreServices.Ads` (§3.6);
  its cooldown/cap policy stays.
- Do **NOT** put any SDK reference in `DeNelle.Core` — the SDK lives ONLY in the leaf `DeNelle.Ads` asmdef
  behind `#if LEVELPLAY_SDK`.
- Do **NOT** touch the wallet / crypto rail — rewarded ads are out-of-store, no `WalletService`, no SKR.
- Do **NOT** add `ff.livead` to the URL-activatable allow-list (`FeatureFlags.cs:636`) — monetization flags
  are barred there.
- Do **NOT** add banners or interstitials — rewarded only (covenant).
- No `.unity` hand-edits; brace-gate + CompileGate green; null-conditional on every `CoreServices.Ads?.` call.

---

## 7. ★ OWNER SETUP — actions only you can do ★

To move from stub to a real rewarded ad on the Seeker (Phase 2), the owner must provide:

1. **Create a Unity LevelPlay (ironSource) account** at the Unity dashboard / levelplay.com and **register
   the Android app** (the sideloaded package name from `AndroidBuild.BuildSeekerApk`). This yields an
   **Android App Key**.
2. **Create a Rewarded Video ad unit / placement** in the LevelPlay dashboard; note its **placement name**
   (default `DefaultRewardedVideo`).
3. **Provide these values** for `ad-sdk-config.json` (they are public client ids, safe to commit):
   - `androidAppKey` = the LevelPlay Android App Key
   - `rewardedPlacement` = the rewarded placement name
   - `testDeviceIds` = your Seeker's advertising/device id (for `testMode:true` — mandatory during
     integration so you don't rack up policy strikes on live inventory)
4. **Add the LevelPlay Unity package** (`com.unity.services.levelplay`) via Package Manager — or tell CLI to,
   in the Phase-2 WO. (CLI cannot create your ad account or generate keys.)
5. **(Phase 2, optional, for eCPM)** create an **AdMob** account + a rewarded ad unit, and an **AppLovin**
   account, to add as mediated networks inside LevelPlay — provide those ad-unit ids when ready.
6. **Decide the web/Pi stance:** confirm "hide ad offers on web for V1" (recommended) vs. sourcing a web
   rewarded provider later.

Until (1)–(4) are provided, the game runs the **stub** everywhere: fully playable, rewards grant on the
opt-in buttons, no real ad — which is exactly the correct V1 default per "unflag when proven."

---

## 8. ACCEPTANCE CRITERIA
- [ ] `DeNelle.Core.Ads.IAdService` + `AdShowResult` added; `CoreServices.Ads` slot mirrors the Audio slot.
- [ ] `StubAdService` registered by default; `IsRewardedSupported == false` on `WebGLPlayer`; grants on `Completed`.
- [ ] `DeNelle.Ads` asmdef (refs Core only) + `LevelPlayAdService` compile GREEN with the package ABSENT
      (all SDK behind `#if LEVELPLAY_SDK`, reports not-ready when off).
- [ ] `FeatureFlags.LiveAd` (`ff.livead`) default OFF; NOT in the URL allow-list.
- [ ] `RewardedAdManager.ShowAdInternal` routes through `CoreServices.Ads`; `BuildTimerService.WatchAdToSkip`
      unchanged and still: opt-in, daily-capped, timer always finishes (never a wall).
- [ ] Reward lands ONLY on `AdShowResult.Completed` (regression-proven with a fake `IAdService`).
- [ ] Ad funnel events (`ad_offer_shown`/`ad_started`/`ad_completed`/`ad_skipped`/`ad_failed`) fire through
      `EventTracker`.
- [ ] `ad-sdk-config.json` shipped (empty keys) + loaded WebGL-safe; empty key degrades to stub with one log.
- [ ] Every seam step FlowTrace-instrumented (`[Flow:Ad]`); a blocked/failed show logs WHY, never silently.
- [ ] Canon: one-line entry in `PIPELINE_STATE.md` §8 pointing at `IAdService` + `ff.livead` + this WO.

## 9. LANE / COORDINATION
Monetization/Backend lane (§9) — isolated, parallel-safe (new Core interface + new leaf asmdef + one
existing-file reroute; no scene files, no VillageSceneBuilder). Single-committer reconciliation per §11.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
