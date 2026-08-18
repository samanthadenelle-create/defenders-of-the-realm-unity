# WO-1120 — Ads: effective free path (stop free grants; real SDK; placements)

**Status:** READY TO IMPLEMENT after WO-912 D3 (LevelPlay account / units) — partial block is account setup  
**Minted:** 2026-08-17 (CLI seat) — program WO-1117  
**Lane:** Monetization / Ads  
**Depends on:** WO-912 (seam + rulings); pairs with WO-1119 harvest boost for ad reward  
**Does NOT replace WO-912** — this is the **product + profitability** slice of the same system

---

## 0. One-line truth

**Ads are the FREE half of the covenant, not a fake button.**  
Today `RewardedAdManager.ShowAdInternal` calls `onReward?.Invoke()` with no SDK — so "Watch an ad to skip 10 min" is a **free skip**, up to the rolling window. That kills both ad revenue **and** crystal conversion (why pay when the button is free?).

---

## 1. How ads make money (two channels)

| Channel | Mechanism | Product rule |
|---|---|---|
| **A. eCPM** | Player watches → network pays | Real impression only; never grant on show |
| **B. Conversion** | Free minutes create near-miss; crystals finish the rest | Ad skip = **minutes**, never full instant-finish; crystal button stays "Finish Now" |

Target economy (WO-912 D1 math): long jobs need **many** watches and still leave residual → crystals feel reasonable, not predatory.

---

## 2. Enabled placements (from `ad-placements.json` — make LIVE)

| Placement | Surface | Reward | Cap | Role |
|---|---|---|---|---|
| `place.build.skip` | build queue | 10 min timeskip (`adSkipSeconds`) | 10/day | **Primary free path** |
| `place.harvest.doubler` | harvest / Echo UI | 2× harvest **1h** (Version B — same engine as 1119) | 3/day | Soft engagement |
| `place.daily.chest` | daily | soft coins only | 1/day | Habit loop |

**Hard laws (already in JSON — enforce in code):**
1. **NO ad reward may grant crystals** (or any premium currency).  
2. Timeskip amount = **one authority**: `BuildTimerConfig.adSkipSeconds`.  
3. **No revive / battle-continue** (combat power — deleted; never restore).  
4. Grant only on `OnUserEarnedReward` (or LevelPlay equivalent), **never on show/open**.  
5. No-fill / dismissed-early → **no grant**, clear toast.

---

## 3. Implementation scope

1. **Wire LevelPlay / IronSource** behind existing `IAdService` / `ShowAdInternal` subclass seam (do not rewrite callers).  
2. **Interpreter** for `ad-placements.json` (today nothing reads it — stub status in file header).  
3. `IsAdReady` = real fill check, not a stopwatch alone.  
4. **Device-clock tamper** (WO-912): rolling window must not refresh from simple clock skew once real ads ship — server-side or hardened ledger (same change set as SDK if possible).  
5. **Compliance:** age/consent hooks per security audit before personalised ads (flag `respectDoNotSell`).  
6. Feature flag stays OFF until account + units + one successful paid impression on device.  
7. Regression: covenant suite — no crystal grants in any enabled reward; revive absent.

---

## 4. Effective utilization playbook (ops, not code)

| Do | Don't |
|---|---|
| Offer ad at **felt pain** (long queue, harvest full wait) | Interrupt calm exploration |
| Cap daily so free path never replaces paid entirely | Unlimited free finishes |
| Show residual time after skip | Label ad button "Finish Now" (that's crystals) |
| Track fill rate + reward complete rate | Grant on load to "test" |

---

## 5. Acceptance

1. Stub path **gone** on release builds with ads flag ON — no reward without network callback.  
2. Editor/dev stub remains isolated (`#if` or explicit DevAdProvider).  
3. Build skip grants exactly N seconds, not job complete.  
4. Harvest doubler uses Version B engine (1119).  
5. Zero crystal grants from any placement (regression hard fail).  
6. One owner-device proof: real ad → reward → FlowTrace + LevelPlay dashboard impression.  

## 6. Not in scope

- Pack pricing (1118), payment (1121), season pass (1122).  
