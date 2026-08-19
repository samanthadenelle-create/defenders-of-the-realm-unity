# WORK ORDER 1125 — The rewarded-ad contract could not survive a real SDK, and now can

**Status:** PART 1 IMPLEMENTED (the async contract, gate-green 2026-08-19) — PART 2 READY TO IMPLEMENT
(the SDK itself, blocked on credentials + account approval)
**Minted:** 2026-08-19 (CLI seat) — banner bumped 1125 → 1126 in the SAME edit
**Lane:** Monetization / ads. `Core/Ads`, `Village/Monetization`, `Village/Buildings`, two UI sites.
**Priority:** HIGH — ads are the shortest path to revenue (the payment rail has four owner-gated
blockers; see `docs/MONETIZATION_STATE_2026-08-19.md`).
**Provenance:** owner 2026-08-19, after AppLovin declined non-Play distribution: *"so we need to keep
the path we have"* → Unity. The contract defect was self-documented in
`RewardedAdManager.cs` since it was written.

---

## 1. THE DEFECT (PART 1 — FIXED)

`RewardedAdManager.TryShowAd(Action)` returns `granted` — true only if the reward callback fired
**before the method returned**. That holds today because `ShowAdInternal` is a synchronous refusal.

**It breaks the instant a real network is wired.** A real SDK presents a full-screen ad and calls back
seconds later, so `granted` is ALWAYS false at return. The player watches the entire ad, earns the
reward, and the caller reports failure:

- `ManageScreenVM.cs:1016` — `Notice = svc.WatchAdToSkip(...) ? "Time skipped." : "No ad available right now."`
- `ObsidianQueueHud.cs:404` — toasts `"Ad skip unavailable right now."` in Danger tone

Both would lie to a player who just sat through thirty seconds of video. The file predicted this in its
own summary: *"an override will need this bool contract revisited (present -> await callback -> grant),
not just filled in."* This WO is that revisit.

## 2. WHAT LANDED

- **`RewardedAdManager.RequestAd(Action onReward, Action<AdShowResult> onComplete)`** — returns
  PRESENTATION STARTED; the reward arrives from the SDK's earned-reward callback; the outcome reports
  when the ad ends. **One-shot guarded**: a double callback (completion racing dismissal, or a
  chatty SDK) cannot pay twice. If an SDK reports `Rewarded` without ever firing the reward callback it
  **warns and grants nothing** — the grant may only ever come from the callback itself.
- **`ShowAdInternal(Action, Action<AdShowResult>)`** — the async seam an SDK overrides. The base
  delegates to the legacy sync seam so any existing override keeps working untouched.
- **`BuildTimerService.WatchAdToSkip(channel, id, onComplete)`** — same gates, and the grant body is
  identical to the bool overload so the two paths can never drift into granting different things.
  **Every refusal reports through the callback**, so a button disabled on tap is never left stuck.
- **Both UI sites moved**, and now distinguish outcomes the bool could not:

| outcome | before | after |
|---|---|---|
| earned | "Time skipped." | "Time skipped." |
| dismissed early | "No ad available right now." | "Ad closed early - no time skipped." |
| our daily cap | "No ad available right now." | "You have used your ad skips for now." |
| genuine no-fill | "No ad available right now." | unchanged |

The dismissed/capped split is not cosmetic. `AdUnavailableReason` documents `CappedByGame` as *"a GAME
rule, never the network's… the difference between the cap binding and fill binding (WO-912 §10.7 — the
single most important launch metric)."* Flattening them makes that metric unreadable.

**Kept, deliberately:** the synchronous overloads. They are still correct for the shipping state (flag
OFF ⇒ synchronous refusal), and deleting them is a wider blast radius than this fix needs.

**Gate:** `COMPILE_GATE_OK`. Braces balanced on all four files, 0 NULs.

## 3. ⛔ THE COVENANT — READ BEFORE TOUCHING ANY REWARD

`AdPlacementCovenantRegression` (marker `AD_COVENANT_OK`, registered in `DataRegression.RunAll`) exists
because of WO-912 §9.3:

> Our rewarded ad is permitted by AdMob's and Unity's published terms for ONE reason — the reward is
> minutes off a build timer, and there is no path from it to money. AdMob forbids rewards "directly
> convertible into direct monetary items"; Unity forbids incentivising with "anything of value".
> **CRYSTALS ARE THE SKR ON-RAMP.** … the cost of getting that wrong is not a balance complaint — it is
> a terminated publisher account.

On 2026-08-07 `ad-placements.json` shipped granting **+150 crystals** for a clip, plus +100 more in a
nested bonus object, *while WO-912 was being written to tell Unity in writing that no ad reward can
reach money*. Nothing caught it because **that file has no interpreter** — no `AdGateService` exists and
nothing under `Assets/**.cs` reads it. **Timer minutes only. Never crystals.**

## 4. PART 2 — WHAT REMAINS

**Blocked on the owner:** LevelPlay **App Key** + **rewarded ad unit ID**, and the ironSource Ads
account is **pending approval** (dashboard banner, 2026-08-19). Approval gates *fill*, not integration.

**Credentials captured 2026-08-19 (Unity Ads direct path):** Android Game ID **6171199**, placement
**`Rewarded_Android`**. iOS (`6171198`, `Rewarded_iOS`) is out of scope — we ship Android/Seeker.
The app is registered in LevelPlay as `com.denellestudios.ech…`, which matches `AndroidBuild.cs:46`.

1. Install the **Ads Mediation** package (absent from `Packages/manifest.json`) + Mobile Dependency
   Resolver. Without the resolver, code compiles in-editor and the **gradle build fails**.
2. **`AD_ID` permission** — `com.google.android.gms.permission.AD_ID` in the manifest. Required at API
   33+, and we now pin `targetSdkVersion = 36` (WO-1124), so this is mandatory, not optional.
3. Implement the provider behind `IAdService` in a leaf assembly behind a version define — the
   `SolanaWalletProvider` model (WO-754 §3.3), so an absent SDK is a compile-time no-op rather than a
   broken build. Override the ASYNC `ShowAdInternal`, never the sync one.
4. **Write the `ad-placements.json` interpreter.** A spec with no reader cannot fail at runtime, which
   is exactly how the crystal violation shipped. Today only a static guard holds it.
5. ILRD (`LevelPlay.OnImpressionDataReady`) → `EventTracker`, subscribed BEFORE `Init` or early
   impressions are lost. The callback fires on a **background thread** — forward, don't touch Unity API.
6. Flip `FeatureFlags.RewardedAdSkip` ON only when the SDK is real AND WO-912 server-side window
   validation exists. Both prerequisites, not one.

## 5. ACCEPTANCE

1. **A regression proving the async ladder**, and it must be able to fail:
   present → callback → grant fires **exactly once**; present → dismissed → **no grant**;
   double callback → **one grant**; SDK claims Rewarded with no reward callback → **no grant**.
2. Cooldown is spent on presentation, never on refusal — a refusal must not lock out a retry.
3. `AD_COVENANT_OK` still green; no placement grants crystals.
4. Device verification via the **LevelPlay Test Suite** (does not run in the editor). Mock ads in the
   editor validate the happy path only — `OnAdLoadFailed`, `OnAdClicked` and ILRD **never fire** there,
   so error handling cannot be tested in-editor and must not be claimed as tested.
5. Airplane-mode test: ads fail gracefully, no crash, no blocked gameplay.

## 6. WHAT NOT TO DO

- Do **not** grant from "we showed it" — only from the earned-reward callback. Granting on show is
  fraud against the network once one is live.
- Do **not** reward crystals (§3).
- Do **not** override the synchronous `ShowAdInternal` for a real SDK — that is the defect this WO fixed.
- Do **not** mix legacy Unity Ads (`Project Settings ▸ Services ▸ Ads`) with the mediation package;
  duplicate SDKs.
- Do **not** ship with Test Suite enabled — `SetMetaData("is_test_suite", "enable")` and
  `LaunchTestSuite()` must both come out before release.
