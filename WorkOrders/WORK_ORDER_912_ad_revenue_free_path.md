# WORK ORDER 912 - Ad revenue for the FREE PATH (provider, rolling window, remote config, ad-boost packs)

**Status: READY FOR OWNER RULING**
**Minted:** 2026-08-06 (UI seat audit; number from the `CLI_LANES_WO_NUMBERS.md` banner, main line next-free = 912)
**Silo:** Monetization / Backend (CLAUDE.md sec.9 - isolated lane; no scene files, no VillageSceneBuilder)
**Roles:** Owner rules sec.9 open questions; CLI implements only after those rulings
**Type:** SCOPE + ACCEPTANCE ONLY. No implementation plan, no code.

> ### ★ 2026-08-07 - THE SEEKER CHECK IS DONE. sec.10.3's CENTRAL UNCERTAINTY IS RESOLVED. ★
>
> sec.10.3 named the check ("confirm on the physical Seeker whether the Play Store app is present and
> the device is GMS-certified") and said it "reframes everything". It was run against the attached
> device over adb. **Measured, not sourced from articles:**
>
> ```
> Play Store    com.android.vending  v52.5.22-34  flags=[ SYSTEM ... UPDATED_SYSTEM_APP ]  enabled=1
> Play Services com.google.android.gms  v26.28.33
> GSF           com.google.android.gsf  present
> ro.com.google.gmsversion = 16_202509
> fingerprint   solanamobile/seeker/seeker:16/BP2A.260611.100.A3/mp1V1155:user/release-keys
> Google accounts signed in: yes ; only gms.supervision (parental controls) disabled
> Android 16 / API 36
> ```
>
> **The Play Store is a SYSTEM app carrying UPDATED_SYSTEM_APP, on release-keys, with a licensed
> `ro.com.google.gmsversion`.** Solana Mobile shipped it IN THE SYSTEM IMAGE, which is not possible
> without a Google Mobile Services license. **The Seeker is GMS-certified and Seeker users have Play.**
>
> **CONSEQUENCE FOR D2 - Unity's only weakness is gone.** Unity's restriction is about the USER
> (*"unless the user also has access to the Google Play Store"*), so Unity's first-party demand
> monetizes Seeker users normally. LevelPlay is no longer handicapped mediation running without its
> house demand - it is a full-strength network that is ALSO the only one offering written crypto
> pre-approval before integration. sec.10.5's "two-horse race" was framed as a trade between
> governance (Unity) and technical fit (AppLovin); **that trade is now lopsided in Unity's favour.**
> AppLovin's remaining edge is a lighter dependency graph (sec.10.4) and a cleaner no-fill code -
> real, but not worth trading the pre-approval for.
>
> **AdMob is STILL OUT, and this finding does NOT rescue it.** Its restriction is on where the APP is
> listed (*"publicly available in a supported store"*), not on what the DEVICE has. Device Play access
> cannot cure an app-listing restriction, and the Solana dApp Store is not a supported store. AdMob
> re-enters only if we also publish to Google Play. **sec.10.5's "the whole comparison re-opens -
> including AdMob" is too broad on this point; it re-opens for Unity only.**
>
> **Q3a is also DEFUSED as a blocker.** It asked whether AppLovin rewarded video serves without Play
> Services. The device HAS Play Services, so the question stops being decisive for the Seeker - it
> only matters for non-GMS Android targets we do not currently ship to.
>
> **Still owner-only and unchanged:** Q2a (AppLovin's silent crypto policy) if AppLovin is pursued,
> and the Unity pre-approval request itself. **Also unchanged: sec.10.1 risk 4** - the device is
> API 36 while `AndroidTargetSdkVersion: 0` is unpinned. Pin it before integrating anything.

---

## 0. THE COVENANT (verbatim, owner - binding on everything below)

```
Faucet (buy)  : Crystal packs in Realm Store
Sink  (spend) : "Finish now" on EVERY real wait
Free path     : Wait, or an OPTIONAL Ad
NOT a sink    : combat power, permanent damage buffs
```

> "Waiting is felt pain; crystals turn pain into optional spend without selling win.
> That's the bent-covenant convenience-only rule."

**Ads are the FREE half of that covenant.** This WO is the ad half.

---

## 1. ONE-LINE TRUTH

The ad economy is designed, persisted, and wired to a live UI button - but **there is no ad**.
`RewardedAdManager.ShowAdInternal` hands the player the reward immediately, so the shipped "Ad" button is a
**free timer skip that costs nothing and shows nothing**. The job is: put a real provider behind the existing
seam, implement the owner's rolling window, and make the economy tunable from the backend.

---

## 2. THE RULINGS THIS WO IMPLEMENTS (owner, 2026-08-06)

### 2.1 The ad economy

| Knob | Value | Source |
|---|---|---|
| Seconds off per watch | **10 minutes** (`adSkipSeconds = 10f * 60f`) | `Assets/_Modules/Core/Catalog/BuildTimerConfig.cs:92` |
| Watches allowed per window | **10** (`adSkipsPerWindow = 10`) | `BuildTimerConfig.cs:103` |
| Window length | **4 hours** (`adSkipWindowSeconds = 4f * 60f * 60f`) | `BuildTimerConfig.cs:106` |

`adSkipsPerDay` is **gone** - replaced by the two window fields above. Any doc still citing a per-DAY cap
or a 15-minute chunk is stale.

### 2.2 ★ THE CAP IS A CONVERSION TRIGGER, NOT A LIMIT ON REVENUE ★

This is the single most important line in the document and it is easy to get backwards.

> Owner: *"if they've watched their ten videos within four hours and they're still playing,
> they're gonna have to spend."*

**An impression pays cents; a crystal purchase pays dollars.** Running out of free skips *while still playing*
is the best moment in the session to show a price. The cap does not suppress revenue - **it manufactures the
spend moment.** Uncapping it would trade a dollar-denominated conversion for a cents-denominated impression.

The numbers are tuned to produce a **NEAR MISS**, and the math is self-consistent (verified against
`BuildTimerConfig.cs:77-106` and the tier ladder at `:20`):

| Job | Minutes | Watches to clear | Outcome |
|---|---|---|---|
| 20-minute troop | 20 | **2** | Clears comfortably. **Feels free.** This is what teaches the player the button works. |
| ~2-hour build | 120 | **12 needed, cap stops at 10** | 100 minutes removed, **20 minutes short, within sight of done.** ★ **This gap is the sell.** ★ |
| 8-hour upgrade | 480 | 48 needed | 100 minutes off still leaves **6h 20m**. Late game leans on crystals by construction. |

The design's own comment states the intent (`BuildTimerConfig.cs:80-89`). **Do not "fix" the near-miss.**
A player who can always finish for free never sees a price.

### 2.3 Rolling window = FIXED WINDOW ANCHORED ON FIRST USE

> Owner: *"we have a timestamp of when the first one's spent, and we can change this schema to match it...
> when the first ad comes in, we mark a timer, and that's their four hour rolling from there."*

The exact semantics:

1. First watch -> stamp `windowStart = now`, `used = 1`.
2. Each subsequent watch -> `used++`, allowed while `used < adSkipsPerWindow`.
3. When `now - windowStart >= adSkipWindowSeconds` -> **clear both**; the next watch opens a fresh window.

**Why the simple design is also the BETTER one for the monetization goal - state this in code comments so
nobody "improves" it later:**

- A **true sliding window** drips the allowance back one at a time. The player limps along free
  indefinitely, always one watch away from another skip. **There is never a moment of zero.**
- The owner's **fixed window** means they burn 10 and hit a **HARD WALL AT ZERO** for the rest of the four hours.
- **The wall IS the conversion trigger. A trickle is not.** Given that the cap exists precisely to create the
  spend moment (sec.2.2), fixed-from-first-use serves it strictly better than sliding.

Someone will eventually propose "upgrading" this to a real sliding window as a quality improvement. It is a
**monetization regression**. Guard it with a comment and a regression test.

### 2.4 SCHEMA: no bump needed - **verified at source**

The earlier note that this "needs a schema addition" was **wrong**, and the config file's own warning comment
at `BuildTimerConfig.cs:97-101` is now **stale** (it still says a rolling window "CANNOT" be expressed and
needs a schema addition). **Correct that comment as part of this work** (CLAUDE.md sec.15 - canon in the same
breath as the change).

The existing wire shape carries the ruling exactly:

```csharp
// Assets/_Modules/Core/State/SaveSchema.cs:303-307
/// <summary>Rewarded-ad build-skips used in the current local day (daily cap). Absent -> 0.</summary>
[JsonProperty("adSkipsUsedToday")] public double? AdSkipsUsedToday;

/// <summary>Local-day key the ad-skip counter belongs to. Absent -> null (counter resets on first claim).</summary>
[JsonProperty("adSkipDayKey")] public string AdSkipDayKey;
```

| Need | Existing field | Fits? |
|---|---|---|
| in-window watch count | `AdSkipsUsedToday` - `double?` | **Yes.** A count is a number. |
| window-start timestamp | `AdSkipDayKey` - `string` | **Yes.** A string already holds a date key; it can hold a unix-ms stamp or ISO instant instead. |

So this is a **semantic change to two already-persisted fields, not a schema addition** - materially cheaper
than first assumed. The round-trip already works end to end: `GameStateService.cs:463-464` (save),
`:554-555` (load), `:973-974` (new-game reset), non-negative validation at `SaveSchema.cs:821-822`.

**Rename the fields for honesty** (`AdSkipsUsedInWindow` / `AdSkipWindowStart`) - recommended, since a field
named `...Today` holding a window count is exactly how the next reader ships a day-reset bug. The **JSON
property names must be read-migrated**, not broken: keep accepting the old `adSkipDayKey` / `adSkipsUsedToday`
keys on load so existing saves survive. A stored value that parses as a **date** rather than a timestamp is a
**pre-ruling save** - treat it as "no window open" and clear it.

**Acceptance criterion (explicit, because this is the trap):** a regression must prove the window is
**anchored on first use and expires 4h later**, and specifically that it **does NOT reset at local midnight**.
Reusing the day fields *semantically as days* would silently ship day-reset behaviour and quietly lose the ruling.

---

## 3. SCOPE BOUNDARY - what this WO is NOT

| Adjacent WO | Owns | Boundary |
|---|---|---|
| **WO-911** `WORK_ORDER_911_timer_speedup_crystals_all_channels.md` (READY) | Extending `InstantFinishPrice` / `TryInstantFinish` / `CanWatchAdToSkip` / `WatchAdToSkip` from **Builder-only** to **all three Obsidian channels**; always showing the crystal Instant CTA. | WO-911 owns CHANNEL GENERALIZATION + the crystal CTA. **WO-912 owns the provider, the window, remote config, ad-boost packs, and the no-fill UX.** Same timers, same methods - see sec.12 lane conflict. |
| **WO-754** `WORK_ORDER_754_rewarded_ads_monetization.md` (SPEC - READY, never implemented) | The `IAdService` Core seam, `StubAdService`, a `DeNelle.Ads` leaf asmdef, `ff.livead`, rerouting `RewardedAdManager.ShowAdInternal` through `CoreServices.Ads`. | **Still valid - adopt it, do not re-spec it.** WO-912 is the decision + economy layer on top. |

> **Numbering hazard, flagged not fixed (orchestrator owns the banner):** two numbers are used twice on disk -
> `WORK_ORDER_754_rewarded_ads_monetization.md` vs `WORK_ORDER_754_vfx_caster_particle_pack_preview.md`, and
> `WORK_ORDER_911_timer_speedup_crystals_all_channels.md` vs `WORK_ORDER_911_unified_queue_screen.md`.
> Both predate this WO. **Not touched by me.**

---

## 4. AUDIT - what EXISTS / what is DEAD / what is ABSENT

All cited from source, read this session.

### 4.1 EXISTS AND RUNS

| Thing | file:line | State |
|---|---|---|
| `adSkipSeconds` | `BuildTimerConfig.cs:92` | **READ AND APPLIED** - `BuildTimerService.cs:405` passes it to `ApplySkipSeconds`. |
| Cap enforcement | `BuildTimerService.cs:603-610` (`UnderDailyAdCap`) | **RUNS**, but still reads the **old day-shaped logic**. Must be rewritten for sec.2.3. |
| `GameState.AdSkipsUsedToday` | `GameState.cs:214` | **INCREMENTED** at `BuildTimerService.cs:617`, then `Persist()`. |
| `GameState.AdSkipDayKey` | `GameState.cs:220` | **WRITTEN** at `BuildTimerService.cs:628`. |
| Save round-trip | `SaveSchema.cs:304,307`; `GameStateService.cs:463-464` / `:554-555` / `:973-974` | Persisted since v13 (WO-172). Wire shape fits the new ruling (sec.2.4). |
| Ad-skip gate | `BuildTimerService.cs:380-387` (`CanWatchAdToSkip`), `:393-407` (`WatchAdToSkip`) | Live. Grants inside the reward callback (`:402-406`) - correct shape. |
| The **"Ad" button** | `ObsidianQueueHud.cs:291-298`, handler `:373-381` | **Live and tappable.** |
| Cooldown policy gate | `RewardedAdManager.cs:36` (`CooldownSeconds = 480f`), `:45`, `:83-89` | Live; self-bootstraps `:60-67`. **Note: an 8-minute cooldown x 10 watches = 80 minutes minimum to exhaust a 4-hour window.** The two throttles interact - see sec.9 D3. |
| Ad placement catalog | `Assets/Resources/Data/Canonical/ad-placements.json` | Exists, well-formed - and completely dead (sec.4.3). |
| **Entitlement store** | `GameState.OwnedItemIds` (`GameState.cs:66`), read `PackStoreVM.cs:53-58` (`IsOwned`), written `:111,:116,:155-159` (`RecordOwned`) | **LIVE.** Pack SKUs are recorded here on purchase and verified at `PackStoreVM.cs:130-134`. **This is the seam ad-boost packs ride on** (sec.6). |
| Backend save rail | `api/game/save.js` - Neon Postgres, wallet-signature or guest auth (`api/_lib/wallet-auth.js`), anti-grief balance guards `save.js:43-63` | **LIVE.** This is where server authority would attach (sec.7). |
| Analytics seam | `EventTracker.Track` - `Assets/_Modules/Core/Analytics/EventTracker.cs:109` | Live, but emits **no ad events** (sec.4.3). |

### 4.2 THE HEADLINE BUG - the "ad" is a free skip with no ad

```csharp
// Assets/_Modules/Village/Monetization/RewardedAdManager.cs:97-100
protected virtual void ShowAdInternal(Action onReward)
{
    onReward?.Invoke();
}
```

Live chain today: `ObsidianQueueHud.cs:297` tap -> `:377 WatchAdToSkip` -> `BuildTimerService.cs:402 TryShowAd`
-> `RewardedAdManager.cs:87 ShowAdInternal` -> `:99 onReward.Invoke()` -> `BuildTimerService.cs:404-405`
`RecordAdSkipUsed()` + `ApplySkipSeconds`.

**Player-visible consequence:** free timer skips, no ad, no crystals - a direct unpriced competitor to the
sink the covenant depends on. Known debt (`RewardedAdManager.cs:2-3`, `// TODO` at `:96`), but **shipping**.
Classified as a **P0 BUG**, not a missing feature (sec.8).

### 4.3 DEAD (authored, zero consumers)

| Thing | Proof |
|---|---|
| `ad-placements.json` - the whole placement/reward table | **No `.cs` in `Assets/` references it.** Grepped `ad-placements` / `adPlacements` / `ad_placements` - zero hits. The `AdGateService` it names in its own `_comment` does not exist (no `*AdService*` / `*AdGate*` file anywhere). |
| Its StreamingAssets twin | **Does not exist.** `Assets/StreamingAssets/Data/Canonical/` holds abilities/accessories/armor... but **no `ad-placements.json`**. WO-754 sec.1's "StreamingAssets twin" claim is **stale/wrong**. |
| `global.hardDailyCap: 12` (`ad-placements.json:16`) | Never read - and now **triply** inconsistent: JSON says 12/day, the old code said 10/day, the ruling says 10 per 4h window. |
| Ad funnel analytics | No `ad_offer_shown` / `ad_started` / `ad_completed` / `ad_failed` emitted anywhere. **There is currently no way to measure ad revenue at all.** |

### 4.4 ABSENT (must be built or bought)

| Thing | Evidence |
|---|---|
| **Any ad SDK** | `Packages/manifest.json` read in full: no LevelPlay, no `com.unity.ads`, no Google Mobile Ads, no AppLovin. Only scoped registry is OpenUPM for `com.cysharp.unitask`. |
| `IAdService` / `CoreServices.Ads` | `CoreServices.cs` slots: Hud `:43`, HudModel `:67`, Population `:88`, Audio `:107`, Jupiter `:130`, WalletSigner `:155`. **No Ads slot.** |
| **Any remote-config seam** | **Grepped `Assets/**/*.cs` for `RemoteConfig` / `remote_config` / `remoteconfig`: ZERO hits.** There is no remote config in this project today. See sec.5. |
| Server-authoritative time | See sec.7.1. |
| Server-validated entitlements | See sec.7.2. |

---

## 5. RULING 2a - REMOTE CONFIG (global tuning without a build)

> Owner: *"make that piece configurable from the database."*

### 5.1 Honest answer: **this would be the first remote config in the project**

There is **no remote-config seam today** (sec.4.4). The three knobs (`adSkipSeconds`, `adSkipsPerWindow`,
`adSkipWindowSeconds`) live in a `ScriptableObject` baked into the build (`BuildTimerConfig.cs:44`), whose
header states there is not even an authored `.asset` - *"these C# defaults ARE the live numbers"*
(`BuildTimerConfig.cs:24-25`). Changing any of them today requires a rebuild and a redeploy.

### 5.2 What it could ride on (two realistic options - owner picks, sec.9 D5)

| Option | What exists already | Cost | Notes |
|---|---|---|---|
| **A. Neon + the existing `api/` rail** | `api/game/save.js` + `api/game/load.js` already talk to Neon with auth (`api/_lib/wallet-auth.js`), CORS, and audit logging. Adding a `GET /api/config/economy` is the same stack, same deploy, no new vendor. | **Low.** One serverless endpoint + a client fetch + a cache. | **Recommended.** Nothing new to learn, no new SDK, no new Android dependency - which matters given sec.10's collision risk. Config can also be delivered on the existing load response, avoiding a second round trip. |
| **B. Firebase Remote Config** | Firebase Auth + Analytics are already integrated (`Assets/Plugins/Android/mainTemplate.gradle`: `firebase-auth:24.2.0`, `firebase-analytics:23.2.0`, `firebase-app-unity:13.14.0`) with EDM4U resolving them. | **Medium.** Adds the `firebase-config` module to the Android dependency graph. | Purpose-built (A/B tests, percentage rollouts, no endpoint to write). But it **adds to the exact dependency graph that already broke a build once** (sec.10.1). Same vendor, so lower risk than a new one - but not zero. |

### 5.3 ★ MANDATORY: the baked ScriptableObject stays the fallback ★

**If the config fetch fails, the free path must keep working.** A network blip must never disable ads, and
must never silently set the allowance to zero.

Required behaviour:
- Baked `BuildTimerConfig` defaults are the **floor**. Remote values **override** on successful fetch only.
- **First run / offline / fetch failure / malformed response -> use baked values.** Never block the UI on the fetch.
- **Cache the last good remote config** and use it while offline, so a player who fetched yesterday keeps
  yesterday's economy rather than snapping back to the build's defaults.
- **Validate remote values before applying** - clamp to sane ranges and reject nonsense. A bad row in a
  database must not be able to set `adSkipsPerWindow` to 0 and delete the free path for every player at once.
  *(This is the failure mode that turns a config typo into an outage.)*
- Log every application through `FlowTrace` (CLAUDE.md sec.12) - which values came from remote, which from
  the bake, and why. A silently-applied economy change is undiagnosable.

**Config is public, not secret.** These are tuning numbers the client already knows. No auth hardening needed
on read - but see sec.7 for why the **enforcement** cannot live client-side.

---

## 6. RULING 2b - ENTITLEMENTS (packs that upgrade the ad economy)

> Owner: *"maybe they buy a pack, and that pack doubles the time that ads take off or maybe increases the
> number of ads they can watch in a day, double win. Make things like that lower in crystals because they're
> still gonna be generating items that are gonna generate income."*

### 6.1 These are TWO DIFFERENT MECHANISMS - do not conflate them

| | **Remote config (sec.5)** | **Entitlement (this section)** |
|---|---|---|
| Scope | **Global** - every player | **Per-player** - only the owner of the SKU |
| Lives in | Backend config table | The player's account / save |
| Changes when | The owner retunes the economy | The player buys something |
| Threat model | A bad value hurts everyone | **A faked value is fraud** (sec.7.2) |

Conflating them - e.g. storing the multiplier in the same config blob - means either every player gets the
upgrade or the "config" becomes per-player state with no purchase record. Keep them separate.

### 6.2 The entitlement path already exists

`GameState.OwnedItemIds` (`GameState.cs:66`) is the live owned-SKU list. `PackStoreVM.ApplyPackContents`
records a purchased pack's SKU into it (`PackStoreVM.cs:111`), verifies the grant landed (`:130-134`), and
persists. `IsOwned(sku)` (`:53-58`) is the read. It round-trips through the save
(`SaveSchema.cs:238`, `GameStateService.cs:423`, `:514`).

**So an ad-boost pack does not need a new ownership model** - it needs a SKU in `packs.json`, a resolver that
asks `IsOwned`, and the multiplier applied where the window is evaluated. That is the cheap part.

### 6.3 ★ THE SECURITY POINT - state this plainly, it is the important part ★

**`OwnedItemIds` is a client-authored list in a client-written save, and it is free to fake.**

The codebase already names this threat model verbatim - `SaveSchema.cs:80` describes an attacker
*"editing the blob (resources.\*, ownedItemIds, ...) and relaunching"*. And `api/game/save.js` has
**no guard on `ownedItemIds` at all**: its anti-tamper checks cover only balances and monotonic counters
(`save.js:46-63`), and its own header concedes the model is *"NOT a server-authoritative economy"* (`:43-45`).

**Why this matters far more here than for a cosmetic:**

A faked cosmetic entitlement costs the owner a sale that was never going to happen. **A faked ad-boost
entitlement converts directly into ad impressions** - it raises the number of ads a client can request. That
is **fraudulent traffic against the owner's own ad account**, and it is precisely what ad networks ban
publishers for.

> **Losing the ad account is a categorically worse outcome than any amount of free skipping.**
> A banned publisher account can end the revenue stream permanently and is difficult to appeal. This is the
> one place in the monetization design where the downside is not "we lost some money" but "the business line
> is gone."

**Therefore: the ad-boost entitlement must be server-authoritative.** It must be validated where the save
already round-trips (`api/game/save.js` / `api/game/load.js`), not trusted from the client blob. The purchase
already flows through a payment path (`PackStore.cs:510-512` applies contents only after
*"Payment confirmed -> the player IS charged"*), so the server can know the truth; the gap is that nothing
currently checks it on the way back in.

The same argument applies to the **window ledger itself** - see sec.7.

### 6.4 ★ OPEN DESIGN QUESTION FOR THE OWNER: minutes or count? ★

The two upgrade shapes **pull in opposite directions on ad revenue**, and it is worth choosing knowingly
rather than discovering it after launch:

| SKU shape | Effect | Effect on **impressions** | Effect on **conversion pressure** |
|---|---|---|---|
| **"Double minutes"** - `adSkipSeconds` x2 (10 -> 20 min) | Each ad is worth twice as much | **FEWER impressions.** The same 2h build now needs 6 watches instead of 12. The player finishes sooner and watches less. | **Weakens the near-miss.** A 2h build becomes clearable within the cap (6 < 10) - the exact gap that sec.2.2 exists to create **disappears**. |
| **"+5 watches per window"** - `adSkipsPerWindow` 10 -> 15 | More ads allowed | **MORE impressions.** Directly what the ad account is paid for. | Preserves the wall, just moves it. A 2h build still needs 12 - now reachable - so tune with care. |

**The "double win" the owner described is the COUNT version** - the player gets more free skips *and* the
owner gets more impressions. The minutes version is a **single** win (player only) and actively erodes the
conversion trigger.

**Recommendation: offer them as two separate SKUs**, priced differently, so the owner chooses per-SKU rather
than baking one assumption in:
- **"Patience of the Realm" (+5 watches / window)** - the double win. Cheap. This is the one to lead with.
- **"Echo's Efficiency" (x2 minutes per watch)** - a genuine convenience upgrade, but price it **higher**,
  because it *reduces* ad revenue while increasing player value. It is closer to a crystal product than an
  ad product.

**Do not ship both stacked without modelling it** - `+5 watches` AND `x2 minutes` together is 15 watches x
20 min = **300 minutes per window**, which clears a 4-hour build for free and dissolves the sink entirely.
If both exist, cap the combined effect.

### 6.5 Pricing note (carry into the store WO)

Cheap in crystals is correct. **The buyer keeps generating impressions afterwards** - the SKU behaves more
like a subscription than a consumable. **The sale is not the revenue; it is the start of the revenue.**
An ad-boost pack that pays for itself in impressions over a month should be priced to maximise *adoption*,
not margin. (This also makes it an excellent first purchase - it converts a never-payer into a payer at low
friction, which is worth more than the crystals.)

---

## 7. ANTI-ABUSE - the window ledger and the entitlement

### 7.1 The clock problem, verified at source

Today's day-roll reads the **device-local wall clock**, and bypasses the project's own clock seam:

```csharp
// Assets/_Modules/Village/Buildings/BuildTimerService.cs:621-631
string today = DateTime.Now.ToString("yyyy-MM-dd");   // device-local. Not UTC. Not server.
```

Corroborated by `GameState.cs:217` (*"Local-day key ... device-local"*).

The project **has** a clock abstraction built for exactly this - `TimeSource.NowUnixMs()` =
`device UTC + ServerOffsetMs + DevSkipMs` (`Assets/_Modules/Village/Harvest/TimeSource.cs:68-71`), whose
header says *"the accrual window is only as trustworthy as the clock it reads. Server-authoritative time is
the hardening path"* (`:10-15`). **`RollDayIfNeeded` does not use it.**

And: **`ServerOffsetMs` is never assigned in production.** Grepped every `.cs` - the only writers are
`Assets/Editor/Regression/DevTimeSkipRegression.cs:62,125,145,148,178`. **There is no server time sync in
this project today.** The seam exists; the backend lane behind it (WO-120) is unbuilt.

**★ The consequence for the new window, stated explicitly ★**

If `windowStart` is a **device-local timestamp**, a player moves the clock forward four hours and gets a
fresh allowance - **repeatedly, on demand.**

**That is not merely free skips. It is FABRICATED AD IMPRESSIONS against the owner's ad account** - the same
ban-triggering fraud described in sec.6.3. **This, not the free skipping, is the reason the window needs
hardening.**

### 7.2 Two defences (owner picks - sec.9 D6)

| | **1. Server-stamped** | **2. Wall-clock + monotonic reference** |
|---|---|---|
| How | The server sets/validates `windowStart` where the save already round-trips (`api/game/save.js`, Neon). Optionally the network's **server-to-server rewarded callback** grants the skip. | Persist **both** the wall-clock time and a monotonic uptime value at each watch. A large divergence between the two on load reads as tampering. |
| Strength | **Strongest.** The client cannot invent time it did not receive. Combined with S2S reward validation, the ad itself is verified too. | **Weaker.** Detects naive clock jumps; defeated by a reboot (monotonic resets) or a patient attacker. |
| Cost | Needs **connectivity at the moment of the watch**, or a reconciliation on next sync. Guest players (the `guest-local-*` rail, `api/_lib/wallet-auth.js`) complicate it. | **Works fully offline.** Pure client change, no backend work. |
| Fit | Matches where the anti-tamper guards already live (`save.js:43-63`) and the documented **BUILT-TO-FLIP seam** (`save.js:410-417`) that anticipates exactly this move when real value is involved. | A stopgap. |

**Recommendation: (1) server-stamped, reconciled on sync** - not blocking the watch on connectivity, but
having the server correct the window on the next save/load round trip and refuse impossible histories. It
lands in code that already exists for this purpose, and it is the only version that survives a determined
attacker. Defence (2) is a reasonable interim if shipping before the backend work.

**★ Neither is required to SHIP. One is required BEFORE the ad account carries real volume. ★**
Ship with the client-side window and the stub/dev provider; harden before the first live ad campaign at scale.
Tie this to a go-live checklist item, not to the first build.

### 7.3 What the client should do on detected tampering

**Recommendation: refuse the skip silently, and do not reset or punish the save.**

- **Refuse, don't punish.** A false positive (genuine timezone change, DST, a user legitimately fixing a
  wrong clock, a device that lost its battery) must not destroy a paying player's state. Refusing one skip is
  recoverable; wiping a window or flagging an account is not.
- **Do not surface an accusation.** Do not tell the player they were caught cheating - it is often wrong, and
  it teaches real attackers exactly what the detector measures. The button simply reads as unavailable.
- **Log it** through `FlowTrace` + an `EventTracker` event so the owner can see the rate. If tampering is
  rampant, that is a signal to prioritise sec.7.2 defence (1), and it is data the ad network may ask for.
- **If it ever does surface in UI, it must not be signalled by COLOUR ALONE** - pair any state indication
  with text or an icon (accessibility; also consistent with the three-state UX in sec.8.2).

### 7.4 Reinstall / second device

| Threat | Today | Note |
|---|---|---|
| **Reinstall** | Window resets - `GameStateService.cs:973-974` zeroes both fields on new game. A signed-in reinstall that pulls the cloud save *should* restore them (fields are mapped at `:463-464` / `:554-555`). **Unverified at runtime.** | **Name the check:** sign in, burn watches, reinstall, sign in, read the persisted values from the loaded save. |
| **Second device** | Two devices playing offline each keep a local window; last-write-wins on sync. No server-side counter exists. | Resolved by sec.7.2 defence (1). |
| **Save editing** | The counter and the entitlement are both client-authored (`SaveSchema.cs:80` names this). `save.js` guards balances only. | Resolved by sec.7.2 defence (1) + sec.6.3. |

---

## 8. WHERE ADS ATTACH - and where they must never

### 8.1 Every real wait (candidate attachment points)

The Obsidian queue is the single home for all timed work (CLAUDE.md sec.8).

| Channel | Real countdown? | Ad attachment |
|---|---|---|
| **Builder** (build + upgrade) | Yes - `BuildTimerConfig.DurationSecondsForTier` (`:102-108`), ladder 30s -> ~2h (`:20`), 48h ceiling (`:58`) | **YES - already wired** (`BuildTimerService.cs:393`). The proof placement. |
| **Train** (troops) | Yes | **YES - blocked on WO-911.** `WatchAdToSkip` resolves Builder only (`BuildTimerService.cs:395`). |
| **Research** | Yes | **YES - same WO-911 blocker.** |
| Offline harvest accrual | Yes (passive window) - `OfflineHarvestService.cs` | **CANDIDATE, owner's call (D4).** `ad-placements.json:85-94` authors a 2x doubler. It is a *yield multiplier*, not a wait-skip. **Not recommended for the first cut.** |
| Daily bonus chest | No - a login reward, not a wait (`ad-placements.json:117-127`) | **Not a timer.** Out of covenant scope as written. |

**Recommended first cut: the three Obsidian channels only.** Every real wait is a queue job.

### 8.2 What must NEVER carry an ad (covenant guard rails - these become acceptance criteria)

- **No combat power.** No damage buff, revive, continue, fire-rate - nothing that changes a fight's outcome.
  *This explicitly retires `place.defeat.continue` (`ad-placements.json:106-116`, "Revive and continue this
  battle once") - **it violates the covenant as written** and must be disabled, not shipped.* The project has
  already ruled this way once: fire-rate pre-charge and permanent passives were both REMOVED for reading as
  bought performance (`docs/monetization-v2-spec.md:152-153`).
- **No permanent anything** from an ad. One-shot conveniences only.
- **No forced ads. No interstitials. Ever.** Not between scenes, on app open, on defeat, or on level-up.
  Rewarded video is the ONLY format. Keep the player-facing line already authored at `ad-placements.json:19`:
  *"Ads are always optional. You are never required to watch one. Ever."*
- **No ad gate on progression.** The timer must always finish unaided - already true
  (`ApplySkipSeconds`, `BuildTimerService.cs:428-450`, only *subtracts* time) and must stay true.
- **No ad wall at a moment of loss.**
- **Nothing convertible.** The reward must never touch crystals, SKR, USDC, SOL, the wallet, or any tradeable
  item - both because the covenant says convenience-only and because it is the answer to policy **Q2** (sec.9.2).

### 8.3 NO-FILL UX - required regardless

A "Finish with Ad" button that does nothing when tapped is worse than one honestly unavailable. **Four
distinct states**, and they must not be conflated:

| State | Condition | Copy |
|---|---|---|
| **Available** | provider reports an ad loaded AND cooldown clear AND under window allowance | "Finish 10m - watch a clip" |
| **No fill / not loaded** | provider has nothing to serve | "No ad available right now - try again shortly" |
| **Window exhausted** | `used >= adSkipsPerWindow` | "No free skips for another 2h 14m" - **show the time remaining.** ★ This is the conversion moment (sec.2.2): pair it with the crystal price. ★ |
| **Cooling down** | 480s cooldown active (`RewardedAdManager.cs:36`) | "Next clip in 3m" |

**These must be different messages.** "No ad available" is a network condition retryable in a minute;
"you're out of free skips" is our rule with a known expiry. Telling a player they hit a cap when the network
simply had no inventory costs trust **and** the impression.

**★ The exhausted state is the single most commercially important screen in this feature.** It is where the
near-miss (sec.2.2) is cashed in. It must show (a) how long until free skips return, and (b) the crystal
price to finish now - side by side, no dark pattern, no pressure. The player chooses.

**Today the code cannot tell these apart.** `CanWatchAdToSkip` (`BuildTimerService.cs:380-387`) returns a
single `bool` collapsing "no active job", "over cap", and "not ready" into one `false`; the HUD then hides the
row (`ObsidianQueueHud.cs:274`) or toasts a generic *"Ad skip unavailable right now."* (`:379`).
**Fixing this is in scope.**

**Lead with availability, never fail after a tap** - query the provider for readiness *before* drawing the button.

---

## 9. OPEN RULINGS + POLICY

> ### ✅ OWNER RULED 2026-08-07 — D1, D2, D4, D5, D7, D8 ALL TAKEN AS RECOMMENDED
>
> | # | Ruling |
> |---|---|
> | **D1** | **Two SKUs, lead with +5 watches/window.** Minutes priced x2 higher. Do not stack uncapped. |
> | **D2** | **Unity LevelPlay.** *(See the Seeker-check banner at the top of this file — the check that was named in sec.10.3 and never run has now been run, and it removes LevelPlay's only weakness. Unity's demand restriction is about the USER's Play access, and Seeker users have it. Unity now wins both scored axes: full demand AND the only documented crypto pre-approval path. sec.10.5's "two-horse race" framing is superseded.)* |
> | **D4** | **No.** Ads stay strictly on queue timers for V1 — no offline-harvest doubling, no daily chest. |
> | **D5** | **Neon + the existing `api/` rail.** No new Android dependency. |
> | **D7** | **Retire `place.defeat.continue`.** It is combat power. |
> | **D8** | **Hide ad offers on web for V1.** |
>
> **D3 STILL BINDS AND IS NOT WAIVED.** No SDK is added until the Unity pre-approval comes back **in
> writing**. D2 selects the provider; it does not authorise integration. Unity's Content Policy names
> *"cryptocurrency trading"* a Regulated Activity permitted only *"with prior approval by Unity"* — the
> path exists, but they still have to say yes. **If Unity declines or stalls, the fallback is AppLovin
> MAX and Q2a becomes live again.**
>
> **D6 and D9 are NOT ruled** and do not block: D6 (server-stamped vs monotonic window) is required
> before real ad volume, not before shipping; D9 (does the 480s cooldown survive alongside the window)
> is explicitly a *measure-first* decision — sec.10.7 says do not tune blind.
>
> **Q3a is defused** by the Seeker check (the device has Play Services) and was AppLovin-specific anyway.
> **Q2a survives only if AppLovin becomes the fallback.**
>
> **PREREQUISITE THAT IS THE CLI's, NOT THE OWNER's:** sec.10.1 risk 4 — the Seeker is **API 36** while
> `AndroidTargetSdkVersion: 0` (unpinned, so it is whatever the build machine has). Ad SDKs carry hard
> targetSdk floors and AD_ID behaviour that changes at API 33. **Pin the target SDK before any SDK
> lands**, in its own change with its own build verification — not folded into the integration commit.

### 9.1 OWNER-DECISION table

| # | Decision | Recommendation |
|---|---|---|
| **D1** | **Ad-boost SKU shape: minutes or count?** (sec.6.4) | **Two separate SKUs.** Lead with **+5 watches/window** (the true "double win"). Price **x2 minutes** higher - it *reduces* ad revenue and weakens the near-miss. **Do not stack them uncapped.** |
| **D2** | **Which ad provider?** | **A two-horse race - ask both, then decide (sec.10.5).** Pursue **Unity LevelPlay's documented crypto pre-approval** first (the only network that answers *before* we build) while asking **AppLovin MAX** about its silent crypto policy. **AdMob is out** on store-linking. Do the 5-minute **Seeker Play-Store check** (sec.10.3) before either conversation - it may re-open everything. |
| **D3** | **Policy answers Q1-Q6** (sec.9.2) | **HARD BLOCKER.** No SDK is added until Q1, Q2, Q3 are answered in writing. |
| **D4** | Does the covenant extend to **offline-harvest doubling** and the **daily chest**? | **Recommend NO for V1** - keep ads strictly to queue timers, matching the covenant's exact wording. |
| **D5** | **Remote config: Neon/`api/` or Firebase Remote Config?** (sec.5.2) | **Recommend Neon + the existing `api/` rail** - no new Android dependency, and sec.10.1 makes that matter. |
| **D6** | **Window hardening: server-stamped or monotonic?** (sec.7.2) | **Recommend server-stamped, reconciled on sync.** Neither blocks shipping; one is required before real ad volume. |
| **D7** | Confirm `place.defeat.continue` is **retired** (sec.8.2) | **Recommend retire.** It is combat power. |
| **D8** | Web/Pi stance | **Recommend hide ad offers on web for V1.** The Pi seam cannot serve Android and is V2-gated - see sec.11. |
| **D9** | Do the `RewardedAdManager` 480s cooldown and the new window **both** stay? | **Recommend yes, but re-tune the cooldown.** 480s x 10 = 80 min minimum to exhaust a 4h window, so the cooldown, not the cap, may become the real constraint. Worth measuring before tuning blind. |

### 9.2 POLICY - the part most likely to sink this

**Established from the repo (verified):**

| Fact | Source |
|---|---|
| Android package id | `com.denellestudios.echoesofelarion` - `ProjectSettings/ProjectSettings.asset:169-170` |
| minSdk / targetSdk | `AndroidMinSdkVersion: 26` (`:178`); `AndroidTargetSdkVersion: 0` (`:179`) - **unpinned / "automatic"** |
| Architectures | ARM64 only (`:269`) |
| **Primary distribution** | **Solana dApp Store**, Google Play an explicit *second act* - `docs/biz/GTM_STRATEGY.md:22-26`, `:99-100`; the Play ASO section (`:147`) is future-tense |
| Real crypto payments | Packs priced in **USDC / SOL / SKR** - `docs/monetization-v2-spec.md:76` |
| The wallet | Mobile Wallet Adapter androidlib present; Solana Unity SDK pinned at `Packages/manifest.json:3`. Wallet = identity + cloud-save key + payments |
| Existing `<queries>` merge surface | `Assets/Plugins/Android/MobileWalletAdapter.androidlib/AndroidManifest.xml` declares a `<queries><intent>` block for the `solana-wallet` scheme |

**The honest risk.** This app has three properties ad networks care about, **all at once**: a self-custodial
**crypto wallet**; **real payments in crypto tokens**; and **primary distribution outside Google Play**. Any
one is survivable. Together they are the profile that gets a publisher account human-reviewed rather than
auto-approved. **I cannot settle this from the repo and will not guess it** - ad-network policy is not in this
codebase, changes frequently, and is enforced by human review whose outcome depends on how the app is
described at signup. **Being wrong costs a store rejection or a publisher-account ban, not a bug.**

### 9.3 ★ RESEARCH RESULT: the covenant's "convenience only" rule is ALSO the policy shield ★

Research settled the decisive policy question, and **the answer is good news** - but only because of a design
choice already made. Quoting the networks' own published terms:

- **AdMob:** *"Direct monetary items may not be offered as rewards under any circumstance"* - examples given
  are *"Cash, cryptocurrency, gift card."* A permitted reward is one *"only redeemable and usable by the same
  user who received it, and is **not directly convertible into direct monetary items**"*
  (support.google.com/admob/answer/7313578 - verified at source).
- **Unity / LevelPlay:** publishers may not incentivize ad views with *"cash, prizes, incentives, gift cards,
  goods, services, vouchers **or anything of value**"*; permitted instead are *"in-app virtual rewards such as
  game tokens, virtual currency, gems"* (unity.com/legal/rewarded-inventory-policy). Unity separately classes
  *"cryptocurrency trading"* as a **Regulated Activity** needing prior approval.
- **AppLovin:** **silent** - their publisher policies (rev. 2026-06-18) contain **no provision on
  cryptocurrency, blockchain, NFTs, or cash-equivalent rewards.** Silence is not permission.

**★ Our reward passes this test by construction. ★** The ad grants **minutes off a build timer** - it cannot
be transferred, traded, sold, or converted into crystals, SKR, USDC, SOL, or anything else. `ApplySkipSeconds`
(`BuildTimerService.cs:428-450`) only mutates `StartMs`. **There is no path from the ad reward to money.**

**The subtlety someone will trip over, so pre-empt it:** the ad-boost pack (sec.6) is *bought* with crystals,
which are bought with SKR - so money touches the ad system. **That is the safe direction and it is not what
the policy prohibits.** The clause bars rewards *convertible OUT* into money. Value flowing **IN** (a player
pays for a better free path) is ordinary in-app purchase. Nothing a player earns from an ad can ever leave the
account. **Say exactly this if a network asks.**

**This is the strongest argument for never weakening sec.8.2.** The moment an ad grants crystals - the SKR
on-ramp currency - the reward arguably becomes convertible and the covenant's protection evaporates along with
the policy protection. **Convenience-only is not just a design value here; it is what keeps the ad account alive.**

### 9.4 What research ANSWERED vs what still needs the OWNER

| Q | Status |
|---|---|
| **Q1** - is a crypto **wallet** in the app disqualifying? | **Likely NO - no publisher-side prohibition found at any of the three.** Important correction to a common error: Google's cryptocurrency *ad* policy governs **advertisers promoting crypto**, not publishers whose apps contain crypto features. Google's Publisher Policies have no crypto category. **This is absence of evidence, not an authoritative yes** - still worth confirming. |
| **Q2** - is our reward permitted? | **Effectively YES by construction** (sec.9.3), for AdMob and Unity, whose terms are explicit. **UNKNOWN for AppLovin** (silent policy) - and AppLovin is the recommendation, so **this is the one to ask**. |
| **Q3** - can we serve without a Play listing? | **ANSWERED, and it decides the provider** - see sec.10.2. AdMob and Unity both restrict it; AppLovin appears not to. |
| **Q6** - does the dApp Store restrict ads? | **ANSWERED - effectively no.** The Solana dApp Store Publisher Policy has only two ad-adjacent clauses (no blocking others' ads; no *"disruptive ads or notifications"* forcing interaction). No ad-network prohibition, no rewarded-ad policy, no crypto-reward restriction. **The dApp Store is not the constraint - the ad networks are.** |

**Still owner-only, in writing, BEFORE integration:**

- **Q2a (highest priority).** Ask **AppLovin's account team**: our app contains a non-custodial crypto wallet
  and sells packs for crypto tokens; our rewarded ad grants **only a non-transferable in-game timer reduction
  with no cash-out path**. Is that permitted? *(Their policy is silent - get it in writing.)*
- **Q3a (blocking, technical).** Ask **AppLovin support**: does **rewarded video** serve on a device without
  Google Play Services? *(Their KB article on this was deleted during the Axon rebrand; snippets suggest
  interstitials serve without Play Services but rewarded may not. Rewarded is our only format, so this is
  decisive.)*
- **Q4.** If we later publish to **Google Play**: Play's Blockchain-based Content policy requires declaring
  tokenized digital assets via the Financial features declaration and forbids *"promot[ing] or glamoriz[ing]
  any potential earning"* - relevant to any "play-and-earn" framing in store copy. The Oct-2025 wallet-licensing
  requirement applies to **custodial** wallets; **non-custodial is reportedly exempt** (MWA is non-custodial,
  so likely fine - **UNCERTAIN, secondary sources**). Note also that **AdMob auto-restricts ad serving for any
  app removed from Play** - Play risk and ad risk are coupled.
- **Q5.** Advertising-ID / `AD_ID` requirements and consent flow. *(`ad-placements.json:18` already declares
  `respectDoNotSell: true`; AdMob additionally bundles a UMP consent SDK that must be called before load.)*

**Do not begin integration until Q2a and Q3a have written answers.**

---

## 10. PROVIDER SELECTION + DEPENDENCY-COLLISION ANALYSIS

### 10.1 The existing Android dependency graph - this is the collision surface

From `Assets/Plugins/Android/mainTemplate.gradle` (Android Resolver / EDM4U block):

```
com.google.android.gms:play-services-auth:16+          <- DYNAMIC VERSION RANGE
com.google.android.gms:play-services-base:18.10.0
com.google.firebase:firebase-analytics:23.2.0
com.google.firebase:firebase-app-unity:13.14.0
com.google.firebase:firebase-auth:24.2.0
com.google.firebase:firebase-auth-unity:13.14.0
com.google.firebase:firebase-common:22.1.0
com.google.signin:google-signin-support:1.0.4
```

Plus: `android.useAndroidX=true`, `android.enableJetifier=true` (`gradleTemplate.properties`); local generated
m2repositories for Firebase + GoogleSignIn (`settingsTemplate.gradle`); Java 17 source/target.

**And an already-fought duplicate-class collision, still scarred into the build file:**

```gradle
implementation fileTree(dir: 'libs', include: ['*.jar'],
    exclude: ['androidx.concurrent.concurrent-futures-*.jar'])
```

The inline comment records that the Solana SDK's loose vendored jar collided with the Maven copy pulled
through the Firebase/AndroidX graph and **killed the Gradle build** (captured in `Builds/android-build2.log`).

**Four verified live risks:**

1. **`play-services-auth:16+` is a dynamic range.** Any new SDK pulling a newer GMS artifact drags this to a
   different resolved version between builds. **This is an existing non-reproducible-build hazard**, and an
   ads SDK depending on Play Services is the most likely thing to trip it.
2. **This project has already lost a build to a duplicate-class collision in this exact graph.** Not theoretical.
3. **`<queries>` manifest-merge pressure.** The MWA androidlib already contributes a `<queries>` block. Ad SDKs
   commonly add their own plus an `AD_ID` permission.
4. **`AndroidTargetSdkVersion: 0`** (`ProjectSettings.asset:179`) - the target API level is **unpinned**, so it
   is whatever the build machine has. Ad SDKs have hard targetSdk floors and AD_ID behaviour that changes at
   API 33. **Pin the target SDK before integrating anything.**

### 10.2 Selection criteria (the decision rubric, since the provider is D2)

Whichever provider is chosen must satisfy, in priority order:

1. **Serves without a Google Play listing / without the Play Store app on-device** - non-negotiable given
   dApp Store + sideload distribution (sec.9.2). *This alone may eliminate options.*
2. **Passes Q1/Q2/Q3** (sec.9.2) in writing.
3. **Exposes an "is a rewarded ad ready" query** so the UI can lead with availability rather than failing
   after a tap (sec.8.3). **A provider without this cannot implement the required UX.**
4. **Exposes a no-fill signal distinct from other failures** (sec.8.3).
5. **Offers a server-to-server rewarded callback** - needed for sec.7.2 defence (1) and the strongest answer
   to impression fraud (sec.6.3).
6. **Minimises the sec.10.1 collision surface** - fewest new Play-Services/AndroidX artifacts, no new
   `<queries>`/manifest conflicts.
7. Ships as a Unity package that can be isolated behind a version-define in a leaf assembly (WO-754 sec.3.3
   model, mirroring `SolanaWalletProvider`).

### 10.3 ★ RESEARCH RESULT: criterion 1 eliminates two of the three ★

**WO-754 sec.2.2 recommended Unity LevelPlay. That recommendation is now OVERTURNED**, on the exact criterion
WO-754 believed was LevelPlay's strength (*"install-source agnostic"*). Unity's own support documentation says
the opposite:

> *"Unity Ads is an ad network for iOS (Apple App Store) and Android (Google Play Store)."* ... *"you can
> implement Unity Ads in Android games downloaded through Amazon, but all of our campaigns currently advertise
> apps for the Google Play Store"* ... **"ad impressions will not generate revenue unless the user also has
> access to the Google Play Store."**
> - support.unity.com/hc/en-us/articles/360000117543 (**fetched directly at source**)

> **★ CORRECTION - this eliminates the Unity Ads NETWORK, not LevelPlay MEDIATION. ★** Deeper research
> established the distinction: the quote above constrains **Unity's own first-party demand**. LevelPlay is a
> *mediation* layer and **can still fill from other mediated networks** for non-Play users. So LevelPlay is
> **weakened, not disqualified** - it would simply be running without its house demand, which is a real but
> survivable handicap. My earlier flat "eliminated" was too strong.

**AdMob fails the same criterion for a different reason:**
*"All Android apps must be publicly available in a supported store in order to link to AdMob"*, and the
supported third-party list is exactly **Amazon, OPPO, Samsung Galaxy Store, VIVO, Xiaomi GetApps**. The Solana
dApp Store is **not** on it, and *"apps listed exclusively in unsupported stores can't be reviewed and will
receive **limited ad serving**"* (support.google.com/admob/answer/9989980).

> **Mitigating fact, and it matters: the Seeker appears to ship Google Play ALONGSIDE the dApp Store**, rather
> than replacing it - so Seeker users likely *do* have Play access, which would blunt both restrictions.
> **This is UNCERTAIN (secondary sources only).** **Name the check: confirm on the physical Seeker whether
> the Play Store app is present and the device is GMS-certified.** That single observation decides how much
> weight criterion 1 carries, and the device is already in the owner's hands
> (project memory `adb-path-and-seeker-deploy`).

### 10.4 Provider comparison (verified findings)

| | **AppLovin MAX** | **AdMob** | **Unity LevelPlay** |
|---|---|---|---|
| Unity package | `com.applovin.mediation.ads` **8.6.4** (UPM via AppLovin's own scoped registry, or `.unitypackage`) | `com.google.ads.mobile` **11.3.0** (OpenUPM or `.unitypackage`) | `com.unity.services.levelplay` **8.7.0** (UPM) |
| **Dependency collision risk** | ★ **LOWEST - verified from POM.** `applovin-sdk:13.6.3` declares only `play-services-ads-identifier:17.1.0`, `play-services-appset:16.0.0`, `androidx.browser:1.4.0`. **All three are BELOW our existing pins**, so Gradle's greatest-version-wins lifts them to what we already have - the safe direction. | **HIGHEST, but not on the GMS axis.** `play-services-ads:25.4.0` wants `play-services-basement:18.9.0`, *older* than our 18.10.0 - **so Firebase/GMS is actually fine.** The risk is the AndroidX/Kotlin tail: `androidx.browser 1.8.0`, `androidx.webkit 1.12.1`, `androidx.datastore`, `kotlin-stdlib 2.1.0`, `kotlinx-coroutines 1.8.0`, and **`androidx.privacysandbox.ads:1.0.0-beta05` - a BETA artifact.** Plus a **compileSdk 35 floor** (note our targetSdk is unpinned, sec.10.1 risk 4). | **UNVERIFIED - dependency manifest not checked.** A gap; moot if eliminated on criterion 1. |
| **Criterion 1** (serves without Play) | **BEST.** Docs treat *"Android / Amazon"* as one platform track - the closest thing to an off-Play blessing; no Play-distribution restriction found. **BUT see Q3a** - the KB article was deleted, and snippets suggest **rewarded video may require Play Services**. | **WORST** - store-linking policy -> permanent *limited ad serving*. Also **avoid the "Lite SDK" entirely** (Play-only, deprecated Jan 2026). | **PARTIAL** - Unity's own demand won't monetize non-Play users, but **mediation can fill from other networks**. Handicapped, not disqualified. |
| **Governance / crypto approval** | **WORST - policy is silent.** No crypto/blockchain/NFT provision at all. Must ask; may get no clear answer. Their policy does require notifying the account team about restricted-category content, and a wallet + token payments plausibly reads as *"Financial Services."* | No pre-approval path; policy is explicit and restrictive. | ★ **BEST - the ONLY network with a documented PRE-APPROVAL PATH.** Unity's Content Policy names *"cryptocurrency trading"* among Regulated Activities permitted *"only with prior approval by Unity."* **We can get a written answer BEFORE building.** For an app in exactly this grey zone, that is a genuine asset. |
| **Criterion 3** (ready-check) | `MaxSdk.IsRewardedAdReady(adUnitId)` | `rewardedAd.CanShowAd()` | `LevelPlayRewardedAd.IsAdReady()`, plus **`IsPlacementCapped(string)`** - capping is a first-class SDK concept |
| **Criterion 4** (no-fill signal) | ★ **CLEANEST.** `NoFill` = **204** (*"No ads are eligible for your device"*), **distinct from** `AdLoadFailed` = **-5001** (*all networks failed despite eligibility*). Genuinely useful for telemetry. | **Flat and awkward.** `NO_FILL = 3` on Android (**1 on iOS**), `MEDIATION_NO_FILL = 9`. Unity exposes only `GetCode()`/`GetDomain()` with **no typed C# enum** - must compare ints *and* check domain. | `OnAdLoadFailed` -> `LevelPlayAdError`; **509 = "Mediation No Fill"** (widely reported, official table not opened - **UNCERTAIN**). |
| Config weight | Heaviest to configure (SDK key, Integration Manager, Jetifier toggle), lightest natively | Lightest to configure (App ID only), heaviest natively; bundles UMP consent SDK that must be called before load | Dashboard ad-unit/instance setup per mediated network |

**Traps that apply regardless of choice:**
1. **AppLovin's UPM package pins EDM4U 1.2.182**, which can fight the EDM4U already resolving our Firebase
   dependencies (`Assets/Plugins/Android/mainTemplate.gradle`). It also **requires Jetifier enabled** in the
   Android Resolver settings - we already have `android.enableJetifier=true` (`gradleTemplate.properties`), so
   that one is satisfied.
2. **Adding any Google mediation adapter to MAX re-imports `play-services-ads` and the entire AdMob dependency
   tail** - erasing MAX's dependency advantage. If MAX is chosen, **do not add the AdMob adapter** without
   re-running the collision analysis.
3. **AdMob's floors:** `compileSdk 35`, `minSdk 23`. Our minSdk 26 clears it; our **unpinned targetSdk**
   (sec.10.1 risk 4) does not - pin it first.
4. **LevelPlay's dependency manifest was never opened at source.** Secondary sources cite
   `play-services-ads-identifier:18.2.0` + `play-services-appset:16.1.0` - note these are **higher** than
   AppLovin's, so they would pull our graph *up* rather than resolving harmlessly. **Named check: run Android
   Resolver and read the generated `mainTemplate.gradle` diff before committing to LevelPlay.**

### 10.5 RECOMMENDATION: a **two-horse race**, decided by which risk the owner prefers to carry

This is no longer a clean single answer. The two finalists fail on **opposite** axes, and the honest framing is
a trade, not a winner:

| | **AppLovin MAX** | **Unity LevelPlay** |
|---|---|---|
| Wins on | **Technical fit** - lightest dependency graph against our fragile Firebase/GMS/Solana stack, cleanest no-fill signal (204 vs -5001), best off-Play posture | **Governance fit** - the only documented **pre-approval path** for a crypto app, plus `IsPlacementCapped()` for honest UI |
| Risk carried | **Policy silence.** No crypto provision at all; we may never get a clear answer, and could be integrated before finding out. Plus the unverified rewarded-without-Play-Services question (Q3a). | **Weakened demand.** Unity's own network won't monetize non-Play users; mediation must carry it. Plus an unverified dependency footprint (trap 4). |

**My recommendation: pursue Unity LevelPlay's pre-approval FIRST, in parallel with asking AppLovin.**

The reasoning is asymmetry of consequences, not feature count. **The catastrophic outcome here is a publisher
account terminated after integration** (sec.6.3) - and Unity is the only network that will tell us *before* we
build. A written pre-approval is worth more than a lighter dependency graph, because the dependency problem is
a build error we can debug in an afternoon and the policy problem is a business line disappearing.

**So: ask both (Q2a to AppLovin, pre-approval to Unity), then decide on the answers.**
- Unity approves -> **take LevelPlay.** Certainty beats elegance.
- Unity declines or stalls, AppLovin answers cleanly -> **take AppLovin MAX**, and confirm Q3a first.
- Both silent -> **do not integrate.** Ship the free path with no ad rather than risk the account.

**If the Seeker check (sec.10.3) shows Play IS present and GMS-certified**, criterion 1 loses most of its
force and the whole comparison re-opens - including AdMob. **Do that check before either conversation**; it is
five minutes and it reframes everything.

**Build behind the thin `IAdService` seam regardless** (WO-754 sec.3). All three expose the same shape -
ready-check, show, no-fill callback - and **the policy risk is high enough that a provider swap must not touch
game code.** That is no longer a nicety; it is the mitigation for a possible account loss.

> **Not researched to conclusion:** no documented crypto/blockchain publisher policy was found for **Mintegral**
> or **Chartboost**. Treat as unknown, not as permissive.

### 10.6 Fill rate and frequency capping - now sourced, and it touches the ruling

**The eCPM-decay mechanism is real and verified.** Advertisers set per-user frequency caps (e.g. Google Ads'
*"3 impressions every 24 hours"*); once a user exhausts a campaign's cap, **that bidder drops out of your
auction for the rest of the period.** Per-user eCPM therefore declines with impression frequency within a day
as the eligible bidder pool shrinks - and when enough bidders drop out, the request returns **no fill**.
AdMob applies frequency caps to rewarded ad units at both app and unit level (*"which cap is reached first"*
wins), and LevelPlay exposes `IsPlacementCapped()` directly in its SDK.

**Two consequences for this design:**
1. **The owner's 10-per-4h cap likely sits near or below where fill and eCPM decay bite anyway.** The cap is
   therefore mostly *free* in revenue terms - it manufactures the conversion moment (sec.2.2) without giving
   up impressions the network was going to pay well for. **This strengthens the ruling.**
2. **No-fill will happen** and must be handled honestly (sec.8.3), independent of any cap we set.

### 10.7 ★ THE FINDING THAT MOST AFFECTS THE RULING - read this one carefully ★

AppLovin's own best-practice guidance recommends frequency-capping rewarded placements to
**"only one or two rewarded videos per day"**, so *"the reward does not become saturated"*
(support.applovin.com/en/max/best-practices/tips-for-using-rewarded-videos-more-effectively). Research's
summary of it was blunt: **"design the economy so it still works at 1-3 rewarded views/user/day, not 10."**

**The ruling assumes 10 watches are available within a 4-hour window. Industry guidance says a player may
realistically get 1-3 per DAY.** If that holds, run the sec.2.2 near-miss math again:

| | **Designed** (10 watches available) | **If fill supports ~2/day** |
|---|---|---|
| 20-min troop | 2 watches -> clears. "Feels free." | 2 watches -> **still clears.** Unaffected. |
| ~2h build | 10 watches -> 100 min off, **20 min short = the sell** | 2 watches -> **20 min off a 120-min timer.** The player is 100 minutes short, not 20. |
| 8h upgrade | 100 min off, leans on crystals | 20 min off. Leans on crystals much harder. |

**★ The consequence, stated plainly: the cap may never be the binding constraint. Fill would be. ★**
The carefully-tuned "10 watches, 20 minutes short" near-miss **might never occur**, because the player never
gets to watch 10.

**Three things follow, and none of them is "change the ruling":**

1. **The design still works - but for a different reason than the stated model.** Conversion pressure does not
   disappear; it *increases*, because ads barely dent a long timer. The crystal price still gets its moment.
   What changes is the **felt experience**: instead of "so close, 20 minutes left," the player feels
   "ads aren't really going to solve this." That is a **less satisfying** free path and a **blunter** sell.
2. **It makes the cap nearly free in revenue terms** (sec.10.6 point 1) - even more so than argued there,
   since the network would rarely let a player reach 10 anyway.
3. **The teaching moment survives intact.** Short jobs (which clear in 1-2 watches) are exactly what teaches
   the player the button works - and those are unaffected in both columns. **The onboarding value of ads is
   robust to fill; only the mid-tier near-miss is fragile.**

**Recommendation: keep the ruling as authored, and treat this as the first thing the telemetry must answer.**
The single most valuable metric at launch is **actual watches-per-user-per-day** (M9). If it lands at 1-3, the
2-hour near-miss should be re-tuned - most cheaply by *shortening* the mid-tier durations or *raising*
`adSkipSeconds`, not by raising the cap, since the cap would not be what is binding.

**Do not tune any of this blind, and do not let anyone "fix" the near-miss before real fill data exists**
(sec.15). Caveat worth keeping: AppLovin's guidance concerns what publishers *set* as caps, partly to protect
reward value, and our reward is unusually small - so our tolerable frequency may be genuinely higher than the
guidance assumes. That is an argument for measuring, not for assuming we are the exception.

---

## 11. THE PI PLATFORM SEAM - not the abstraction to build on

- `Assets/_Modules/Core/Platform/IPiPlatform.cs:33-34` - `UniTask<bool> ShowAd(string adType)`
  (`Pi.Ads.showAd("rewarded"|"interstitial")`).
- `Assets/_Modules/Core/Platform/WebGLPiPlatform.cs:95-102` - implementation via the `PiShowAd` jslib extern.

**Why it is not the answer:**

1. **WebGL-only by construction.** Every extern is inside `#if UNITY_WEBGL && !UNITY_EDITOR`
   (`WebGLPiPlatform.cs:21-27`); off WebGL they are compiled-out no-ops (`:28-35`) and `IsAvailable` is false
   (`:48-51`). **On the Seeker Android APK - the primary target - it can never serve an ad.**
2. **Gated to an unshipped platform.** `IPiPlatform.cs:12`: *"V2-gated - no gameplay path calls this until the
   Phase-0 mobile-WebGL gate passes."* Confirmed: nothing calls `ShowAd`.
3. **It is a Pi-Network transport, not an ad abstraction.** The same interface carries `Authenticate` (`:23`),
   `CreatePayment` (`:31`), and Pi payment-approval events (`:37-40`). Wrong bounded context.

**Project memory is confirmed by the code:** ads were scoped for the *Pi/WebGL, ads-only* surface - explicitly
the **secondary** platform. The primary surface has no ad path at all.

**Use the `IAdService` seam WO-754 sec.3 already specified.** Do not redesign it; do not implement against `IPiPlatform`.

> **Defect noted in passing, not in scope:** in `WebGLPiPlatform.HandleCallback` the `"error"` branch
> (`:140-162`) only settles `_adTcs` after `_authTcs` and `_initTcs` are both null (`:156`), so an ad error
> arriving during an in-flight auth leaves the ad task pending forever. There is also **no no-fill callback
> type at all** - `"adReady"` (`:133`) is the only ad outcome. File separately if the Pi surface is revived.

---

## 12. BUGS vs MISSING vs OWNER-DECISION

### 12.1 BUGS - broken today, filed regardless of ad strategy

| # | Bug | Evidence | Sev |
|---|---|---|---|
| **B1** | **The "Ad" button grants a free timer skip with no ad shown.** A live unpriced faucet competing with the crystal sink. | `RewardedAdManager.cs:97-100`; reachable via `ObsidianQueueHud.cs:291-298` | **P0** |
| **B2** | Cap logic is **day-shaped and device-local**, contradicting the sec.2.3 ruling and defeated by a clock change - which fabricates ad impressions (sec.7.1). | `BuildTimerService.cs:603-631` (`DateTime.Now` at `:625`) vs `TimeSource.cs:68-71` | **P1** |
| **B3** | `CanWatchAdToSkip` collapses "no job" / "over cap" / "not ready" into one `bool`, so the UI cannot tell the player the truth - **blocks the sec.8.3 UX and the conversion moment**. | `BuildTimerService.cs:380-387`; generic toast `ObsidianQueueHud.cs:379` | **P1** |
| **B4** | `ad-placements.json` has **no StreamingAssets twin**, unlike every other canonical data file. | `Assets/StreamingAssets/Data/Canonical/` has no `ad-placements.json` | P2 (latent) |
| **B5** | `place.defeat.continue` is authored `enabled: true` and **violates the covenant's "NOT a sink: combat power"**. Dead today, so a landmine not a live defect. | `ad-placements.json:106-116` vs sec.0 | P2 |
| **B6** | Three conflicting cap values: JSON `hardDailyCap: 12`, legacy code 10/day, ruling 10/4h. | `ad-placements.json:16` vs `BuildTimerConfig.cs:103` | P3 |
| **B7** | **Stale canon:** `BuildTimerConfig.cs:97-101` still warns that a rolling window "CANNOT" be expressed by the schema and needs a schema addition. **Superseded by sec.2.4** - the wire shape fits. | `BuildTimerConfig.cs:97-101` vs `SaveSchema.cs:304,307` | P2 (misleads the implementer) |

### 12.2 MISSING - designed but never built

| # | Missing | Owner |
|---|---|---|
| M1 | **Any ad SDK.** Nothing in `Packages/manifest.json`. | This WO (D2) |
| M2 | `IAdService` + `CoreServices.Ads` + `StubAdService` + `DeNelle.Ads` leaf asmdef + `ff.livead` | **WO-754 sec.3 - adopt verbatim** |
| M3 | Ad-skip on **Train and Research** | **WO-911 Phase A** - do not duplicate |
| M4 | Rolling-window ledger per sec.2.3 (semantic rewrite of two persisted fields) | This WO |
| M5 | The four-state no-fill / exhausted / cooldown / available UX | This WO, sec.8.3 |
| M6 | **Remote config seam** - none exists (sec.4.4) | This WO, sec.5 (D5) |
| M7 | **Ad-boost pack SKUs** + the multiplier resolver riding `OwnedItemIds` | This WO, sec.6 (D1) |
| M8 | **Server-authoritative entitlement + window** (sec.6.3, sec.7.2) | Backend WO; **required before real ad volume, not before ship** |
| M9 | **Ad funnel analytics.** `EventTracker.Track` exists (`EventTracker.cs:109`) but emits no ad events. **Without this there is no way to measure whether ads earn anything.** | This WO |
| M10 | `AdGateService` - the interpreter making `ad-placements.json` live | WO-754 Phase 2; **not required for the first cut** |

### 12.3 OWNER-DECISION

See sec.9.1 (D1-D9).

---

## 13. THE HONEST REVENUE PICTURE

**No eCPM figure is asserted here.** I will not invent numbers the owner would plan against.

### 13.1 The formula

```
daily_ad_revenue = DAU
                 x share_of_DAU_who_watch           (opt-in rate)
                 x impressions_per_watcher_per_day  (bounded by cap, cooldown AND fill)
                 x fill_rate                        (network-supplied)
                 x (eCPM / 1000)                    (network-supplied, geo-weighted)
```

`eCPM` must be a **geo-weighted blend**, not one number:
`eCPM_blend = sum over geos of (share_of_DAU_in_geo x eCPM_in_geo)`.

### 13.2 The ceiling under the ruling

- **Per window:** 10 watches.
- **Windows per day:** up to 6 (4h each) in theory, but a window only opens on a watch and requires the player
  to be *playing*. Realistically **1-3 windows/day** for an engaged player.
- **The 480s cooldown binds first:** 10 watches x 8 min = **80 minutes of wall-clock** to exhaust one window.
  A player must be active for over an hour to reach the cap at all. **For most players the cooldown, not the
  cap, is the real constraint** - which is D9, and worth measuring before tuning either.

**The shape, stated plainly:** rewarded video is a **volume business**. Per-impression revenue is small; the
number only becomes interesting at scale. With a per-window ceiling and a cooldown, **revenue scales with DAU
and essentially nothing else.** Doubling DAU doubles it; moving the cap from 10 to 15 barely moves it, because
most players never reach 10.

**The decision-useful corollary:** *at low DAU this earns very little no matter how it is tuned.* So the
question is **not** "will ads pay for themselves this month" but **"is the integration cost worth paying now
so it is in place when DAU arrives?"** - and that is a much cheaper question, because sec.4.1 shows the
economy, the persistence, the UI button, and the policy gate **already exist**. The remaining work is a
provider behind an existing seam.

**And the more valuable revenue may not be the ads at all.** Per sec.2.2, the cap's job is to produce crystal
purchases. If the near-miss works, **the conversion revenue should exceed the impression revenue by an order
of magnitude** - which means the metric to instrument first is not eCPM, it is *"how often does hitting the
wall lead to a purchase."* That is M9's real job.

### 13.3 Inputs the owner must supply

1. **Expected DAU** at the horizon she cares about.
2. **Geo mix of that DAU** - this dominates the answer; Tier-1 vs Tier-3 can differ by an order of magnitude
   on identical impression counts. The Seeker audience skew is a real input only she knows.
3. **eCPM + fill rate for rewarded video from the chosen provider's dashboard or rate card**, by geo. *Ask
   during the sec.9.4 policy conversation - it is the same conversation.*
4. **Her assumption for opt-in share.**

### 13.3a ★ WARNING: published eCPM benchmarks for this are largely fabricated ★

Research went looking for sourced eCPM ranges and **found almost nothing trustworthy.** The figures that
circulate for rewarded video (Tier-1 $15-30 eCPM, US Android ~$16.49, >95% Tier-1 fill) trace back to a
cluster of SEO content farms - two of which published the **byte-identical article title**, a definitive
spun/syndicated-content signal. **Those numbers are not evidence and must not be planned against.**

**The one credible source identified:** Tenjin's *Ad Monetization in Mobile Games Benchmark Report 2026*
(real MMP data, disclosed methodology - 146bn impressions; eCPM from CAS.AI over 6bn impressions, kids apps
excluded). **Its eCPM figures are rendered as SVG charts and could not be extracted as text.**

**Caveat even on the good source:** despite the "2026" title, its stated impression date range is
**2024-01-01 to 2024-06-30** (page last updated 2026-04-23). That is roughly two-year-old data in a market that
moves. Useful for *relative* geo shape; **not** reliable for absolute revenue planning.

**Action, if the owner wants a pre-integration estimate: open that report visually and read the charts.**
Otherwise - and this is the better path - **model the business case on our own measured data after a limited
live test**, not on published benchmarks. Given sec.13.2 (revenue scales with DAU and little else), a small
real test is worth more than any benchmark.

### 13.4 The measurement gap that must close first

**None of this is measurable today** (sec.4.3, M9). Whatever is decided, **ad funnel analytics must ship with
the first live ad** - offer-shown, started, completed, dismissed, failed, no-fill, **window-exhausted**, and
**purchase-after-exhausted** - or the owner will be tuning this on vibes forever.

---

## 14. ACCEPTANCE CRITERIA

Scope and acceptance only. **Nothing below starts before D1, D2, D3 are ruled.**

**Covenant guards (all must be provably true):**
- [ ] Rewarded video is the **only** format. No banner, interstitial, or app-open ad anywhere in the tree.
- [ ] Every ad is **opt-in behind a tap**; none shown unprompted.
- [ ] Every timer **completes on its own** with no ad watched - proven by regression, not inspection.
- [ ] Reward is **time-skip only**. No combat power, permanent effect, crystals, wallet, or tradeable item.
      `place.defeat.continue` disabled (B5/D7).
- [ ] No progression, quest, structure, hero, or region gated behind an ad.

**B1 - the gate on everything else:**
- [ ] `RewardedAdManager.ShowAdInternal` **no longer self-grants**. With no provider present the reward is
      NOT granted and the offer is **hidden** (Demo Law: reachable features WORK or are HIDDEN).
- [ ] Reward lands **only** on a genuine completion signal - regression-proven with a fake provider asserting
      failure / dismissal / no-fill all grant nothing.

**The rolling window (sec.2.3 / 2.4) - the trap:**
- [ ] Window is **anchored on first use**: first watch stamps the start; expiry clears both fields; the next
      watch opens a fresh window.
- [ ] ★ **Regression proves it does NOT reset at local midnight** and DOES expire exactly
      `adSkipWindowSeconds` after the first watch. ★ *(Reusing the day fields semantically as days would
      silently ship day-reset behaviour and lose the ruling.)*
- [ ] Regression proves the **hard wall at zero** - after `adSkipsPerWindow` watches, no further skip is
      offered until expiry. **Not a sliding/trickling allowance.**
- [ ] Old saves read-migrate: a stored value that parses as a **date** is treated as "no window open".
      Old JSON keys still accepted on load. **No schema version bump.**
- [ ] The stale warning at `BuildTimerConfig.cs:97-101` is corrected (B7).

**The four-state UX (sec.8.3, fixes B3):**
- [ ] `CanWatchAdToSkip`'s single `bool` replaced by a result distinguishing
      **available / no-fill / window-exhausted / cooling-down** (plus no-active-job).
- [ ] Availability queried **before the button is drawn**; a tap on a shown Ad button never fails silently.
- [ ] All four states have **distinct player-facing strings**; the exhausted state **shows time remaining
      AND the crystal price** side by side.
- [ ] Any state conveyed in UI is **not signalled by colour alone**.

**Remote config (sec.5):**
- [ ] The three knobs are servable from the backend and applied at runtime without a rebuild.
- [ ] ★ **Baked `BuildTimerConfig` remains the fallback.** Fetch failure / first run / offline uses baked
      values; the free path is never disabled by a network blip. ★
- [ ] Last-good remote config is **cached** and used while offline.
- [ ] Remote values are **validated and clamped** before applying - a bad row cannot zero the allowance for
      every player.
- [ ] Every application is `FlowTrace`d: which values came from remote, which from the bake, and why.

**Entitlements (sec.6):**
- [ ] Ad-boost SKU(s) per D1, riding the existing `OwnedItemIds` / `IsOwned` path (`PackStoreVM.cs:53-58`).
- [ ] The multiplier is applied at the **single** place the window is evaluated - not duplicated across call sites.
- [ ] ★ **The entitlement is validated server-side** where the save round-trips, OR the WO explicitly records
      that it ships client-trusted with a dated follow-up (M8). **It must not silently ship client-trusted
      with no record** - the fraud exposure is the ad account (sec.6.3). ★
- [ ] If both SKU shapes ship, the **combined effect is capped** (sec.6.4).

**Anti-abuse (sec.7):**
- [ ] The window clock reads `TimeSource`, not `DateTime.Now` (B2).
- [ ] Tamper detection (per D6) **refuses the skip silently** - never wipes state, never accuses the player,
      always logs.
- [ ] Reinstall check performed and recorded: sign in, burn watches, reinstall, sign in, verify the window
      persisted (sec.7.4).

**Measurement + hygiene:**
- [ ] Ad funnel events fire through `EventTracker.Track` (`EventTracker.cs:109`), including **no-fill**,
      **window-exhausted**, and **purchase-after-exhausted** (sec.13.4).
- [ ] ★ **Actual watches-per-user-per-day is measurable from day one** (sec.10.7). This is the single most
      valuable launch metric: it tells us whether the cap or the network is the binding constraint, and
      whether the sec.2.2 near-miss is reachable at all. ★
- [ ] Every seam step `FlowTrace`-instrumented per CLAUDE.md sec.12; a blocked or failed show logs **why**.
- [ ] Target SDK **pinned** in `ProjectSettings.asset` before any SDK is added (currently `0`, `:179`).
- [ ] A clean Android build succeeds with the ad SDK present - **specifically no duplicate-class failure and
      no `<queries>`/`AD_ID` manifest-merge conflict** against the MWA androidlib. Gradle log captured.
- [ ] Resolved `play-services-*` versions **recorded before and after** adding the SDK, so the `16+` dynamic
      range's movement is visible (sec.10.1).
- [ ] All SDK references confined to a leaf assembly behind a version-define; **the tree compiles GREEN with
      the package absent** (WO-754 sec.3.3 model).
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` + brace-check on every `.cs` touched.

**Owner close-out:**
- [ ] Written answers to **Q2a** (reward permitted) and **Q3a** (rewarded video without Play Services) filed
      in the repo **before the SDK is added** (sec.9.4). Q4/Q5 answered before a Play submission.
- [ ] **Seeker Play-Store check performed and recorded** (sec.10.3): is the Google Play Store app present and
      is the device GMS-certified? This single observation decides whether AdMob/Unity are truly eliminated.
- [ ] PO felt-verifies a **real rewarded ad** rendering and rewarding on the Seeker, and closes (CLAUDE.md sec.13).
- [ ] PO felt-verifies the **near-miss**: a ~2h build, 10 watches, 20 minutes short, crystal price visible.

---

## 15. DO NOT

- Do **not** add banners, interstitials, or app-open ads. Rewarded only.
- Do **not** grant any reward outside a verified completion callback.
- Do **not** "improve" the fixed window into a **sliding** window - it is a monetization regression (sec.2.3).
- Do **not** remove or soften the near-miss (sec.2.2) to be generous. It is the product.
- Do **not** bump the save schema version for the window - the wire shape already fits (sec.2.4).
- Do **not** put an SDK reference in `DeNelle.Core` - leaf assembly only.
- Do **not** touch the wallet / SKR rail. Ads and crypto payments stay separate code paths - architecture rule
  **and** the Q2 answer.
- Do **not** implement against `IPiPlatform` (sec.11).
- Do **not** re-spec `IAdService` - WO-754 sec.3 did it.
- Do **not** generalize the ad path to Train/Research here - WO-911 Phase A.
- Do **not** add any ad flag to the URL-activatable allow-list (monetization flags are barred - WO-754 sec.3.4).
- Do **not** commit app keys, ad-unit ids, publisher ids, or credentials. Client-side ad ids are publishable
  config, but they go in a config file the owner fills in - never in a doc or a commit message.
- Do **not** hand-edit `.unity` scene files.

---

## 16. FILES THIS WO WOULD TOUCH (lane planning - no edits made)

- `Assets/_Modules/Village/Monetization/RewardedAdManager.cs` (B1)
- `Assets/_Modules/Village/Buildings/BuildTimerService.cs` (window rewrite, B2, B3)
- `Assets/_Modules/Village/BuildMode/ObsidianQueueHud.cs` (four-state UX) - **shared with WO-911**
- `Assets/_Modules/Core/Catalog/BuildTimerConfig.cs` (B7 stale comment; remote-config fallback source)
- `Assets/_Modules/Core/State/GameState.cs` + `SaveSchema.cs` + `GameStateService.cs` (field rename +
  read-migration; **no version bump**)
- `Assets/_Modules/Core/CoreServices.cs` + new `Assets/_Modules/Core/Ads/` (M2, per WO-754)
- New leaf `Assets/_Modules/Ads/` + asmdef (M2)
- `Assets/Resources/Data/Canonical/ad-placements.json` (B5, B6) + a StreamingAssets twin (B4)
- `Assets/Resources/Data/Canonical/packs.json` (M7 ad-boost SKUs)
- `api/` - new config endpoint (D5) and/or entitlement + window validation (M8)
- `Packages/manifest.json`, `Assets/Plugins/Android/mainTemplate.gradle`,
  `ProjectSettings/ProjectSettings.asset` (SDK + target SDK pin)

**Lane conflict:** `ObsidianQueueHud.cs` and `BuildTimerService.cs` are touched by **both WO-911 and WO-912**.
Per CLAUDE.md sec.9/sec.11, **same-file work = one agent.** Sequence WO-911 first (it generalizes the channel
resolution these methods use), then WO-912 on top - or run them as one combined implementation pass.
