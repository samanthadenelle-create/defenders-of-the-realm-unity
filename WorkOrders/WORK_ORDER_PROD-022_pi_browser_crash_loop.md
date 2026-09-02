# WORK ORDER PROD-022 — Pi Browser (iPhone) crash-loops: Unity restarts every 30-60s, unprovoked

**Status:** READY TO IMPLEMENT
**Silo / Lane:** Lane 10 Build/Deploy/Perf + Content Delivery (R2/Addressables) · Pi/WebGL
**Type:** EXISTING (built, now broken)
**Minted:** 2026-09-02 (CLI) from a LIVE owner felt-test in REAL Pi Browser on iPhone.
**Severity:** P0 — the game is unplayable on the published Pi build. Nothing can be done in a 30-60s window, purchases included.

> ### ⚠ THIS WO WAS RE-SCOPED WITHIN THE HOUR IT WAS MINTED. READ WHY.
> It was first written as *"tapping BUY resets the app"*, because the first trace we read
> (`wt-e111e63b2749`) showed a cold boot 12s after `PiAuthenticate(scopes=username,payments)`. **That
> premise was WRONG and would have sent a seat hunting the Pi payment path for a bug that is not
> there.** The owner then reported the reset happening while *"i was simply standing there"*, and a
> 25-session sweep showed the app cold-boots every 30-60s **with no input and no purchase attempt in
> any of them**. The Buy tap merely landed inside a loop that was already running. Correlation is not
> the cause — the purchase was the thing being *observed*, not the thing *causing*.

## Owner report

> *"keeps resetting unity"* / *"i was simply standing there"* / *"if i dont load from cdn it seems to
> be fine, or if it tryies to load to fast it crashes?"*

## RCA — proven from the database

Build `2026.09.02.352005@echoes-of-elarion.vercel.app` throughout. All times UTC 2026-09-02.

### 1. It is a crash loop, and it is input-independent

Session lifetimes, consecutive, from `?view=traces`:

```
wt-4c05e3f90812  lived  16s      wt-b707c3759529  lived  30s
wt-14a1a10c223b  lived  15s      wt-153f9471209c  lived   5s
wt-f8f18b9a3abd  lived   8s      wt-1b3fc89fb4a6  lived  53s
wt-60557d28eab9  lived  43s      wt-96eff0a3fd83  lived 283s
```

Every session opens with `[WebTrace] Remote trace sink active (session=…)` in `[Title]` — a COLD
BOOT, not a scene change — and the gap between one death and the next boot is a consistent ~12s (the
page reload). **No `Purchase requested` / `scopes=username,payments` line appears in any of them.**

### 2. It is Pi Browser, not the build

The same build, same day, differs only by host:

| session | boot line | outcome |
|---|---|---|
| `wt-fe4ef881cc2f` | `device='Chrome 134.0.6998.205' … tier='Desktop'` / `WebGL host is not Pi Browser` | **lived 3738s (62 min)** |
| `wt-1b3fc89fb4a6` | `device='Unknown browser Unknown version' … tier='Seeker_High'` / `inside Pi Browser` | died at 53s |

**Desktop Chrome is stable for an hour on the identical build.** This is not a regression in the
WO-1323/WO-1325 deploy.

### 3. It is NOT out-of-memory in the Unity heap

```
wt-fe4ef881cc2f (lived 62 min):  fps=58-60  mem=247MB gc=8MB
wt-b707c3759529 (died at 30s):   fps=33     mem=247MB gc=8MB
wt-1b3fc89fb4a6 (died at 53s):   fps=33     mem=247MB gc=9MB
```

Identical heap to the healthy session, flat right up to death, with **no error, exception, abort or
`FlowTrace.Fail` preceding any death**. Deaths land at unrelated points (mid structure-skin; on a
scene-context line; while a panel held `timeScale=0`). That is the signature of the **tab being
killed from outside**, not the app falling over. Addressables download/decompression happens largely
OUTSIDE the managed heap, which is exactly how a webview memory ceiling would look while `mem=` stays
flat — this is the leading candidate, NOT a proven root.

### 4. The owner's CDN hypothesis has direct support — the bundles are NOT resolving

`wt-b707c3759529`'s final lines are a retry storm:

```
[Flow:VisualFactory]   -> Skin('Structures/GenericContainer')
error: [Flow:VisualFactory] model not found via Addressables OR Resources: 'Structures/GenericContainer'
       — returning null (caller falls back). Check the address exists in the Structure_Art group and
       that its bundle is uploaded to the CDN.
error: [Flow:Structure] 'silo': visual 'Structures/GenericContainer' is not resident yet — retaining a
       visible pending-art proxy and arming one WhenSettled retry
[Flow:VisualFactory]   <- Skin('Structures/GenericContainer') (0.0ms)
```

repeating. Same for `Structures/Tower_Wooden_Watchtower`, `Structures/farm`, `Structures/arcane tower`.
Under Pi Browser these stream on demand by policy — `[Flow:StructureAssets] Pi Browser policy: eager
structure download/residency disabled; 20s Addressables request timeout installed; assets load on
demand` — a path desktop never exercises. **So it is not that loading is too fast; loading is
FAILING and retrying.** Final-batch asset/CDN line counts: `b707`=40, `60557`=35 (but `1b3f` died on
Title with 0, so the retry storm is not present in every death).

This is the **CLAUDE.md §16 class** and is likely the same family as the still-open **PROD-021** (R2
catalog never pushed for `StandaloneWindows64`). `R2_PARITY_OK targets=Android,StandaloneWindows64,WebGL
objects=261` was green at 12:46 — so verify what that actually proved for the **WebGL** structure
bundles specifically, per §16's "push the parent, verify the explicit target" asymmetry.

### 5. ⛔ iOS MEMORY JETTISON IS RULED OUT — do not re-theorise it

Owner checked **Settings → Privacy & Security → Analytics & Improvements → Analytics Data** on the
iPhone (screenshot, 2026-09-02 14:19 local). `JetsamEvent-*.ips` reports present:

```
2026-08-28-123147   2026-08-31-003214
2026-08-28-150504   2026-08-31-053651
2026-08-28-172625   2026-08-31-214732
2026-08-29-071207   2026-09-01-022105
2026-08-30-024610   2026-09-01-122047   <- NEWEST
```

**There is NO JetsamEvent dated 2026-09-02.** The list is alphabetical, so a `2026-09-02` entry would
sort directly after `2026-09-01-122047` and before `Outlook-iOS-2026-09-01-190523`; it is absent. The
device demonstrably writes these reports (ten in the preceding five days), so this is a real negative,
not a collection gap.

The app died 10+ times in the 18:51-19:02Z window. **iOS killed nothing for memory in that window.**
So the flat 247MB Unity heap was NOT masking an out-of-heap spike, and the "webview jettisoned under
memory pressure" candidate — previously the leading one, and the reason the `PAYLOAD` classification
mentions a mid-inflate abort — is DISPROVEN. Do not re-open it without new evidence.

**This promotes the Addressables-init-ordering lead (Lane B, below) to prime suspect**, and raises a
second: if no process died, the page may have RELOADED rather than crashed — which is precisely what
Lane A's `navigation=` crumb distinguishes.

## ⛔ ROOT NOT PROVEN — instrument first (CLAUDE.md §12)

The signal that separates "webview killed the tab" from "the app tore itself down" exists in the page
already and **never reaches the database**. `Builds/WebGL/index.html:195`:

```js
console.info('[PiLifecycle] boot=' + bootId + ' previous=' + previousBoot + ' navigation=' + piNavType);
```

`piBootCrumb()` persists phases to `localStorage['eoa.pi.boot']` (`:128-130`). Grep of every session
returns **zero** `[PiLifecycle]` lines — it is `console.info` only. That is why the trace goes silent
instead of naming the cause.

| next-boot crumb | root it names |
|---|---|
| `navigation=reload`, `previous=unity-running` | webview/OS jettisoned the tab under memory pressure |
| `navigation=navigate`/`back_forward`, `previous=pagehide` | Pi Browser navigated the page away |
| crumb never advanced past `unity-loading` | teardown during boot/asset residency |

## The fix — two independent lanes (file-disjoint, run in parallel)

**Lane A — instrumentation (do this first; it is the gate on everything else).**
- Forward `[PiLifecycle]` boot/`pagehide`/`visibilitychange` to `/api/trace` via the existing sink.
  The boot line must post BEFORE Unity starts — the whole point is capturing a boot whose predecessor
  died. Use `navigator.sendBeacon` for the `pagehide` crumb; a normal `fetch` will not survive teardown.
- Edit the **WebGL template source** under `Assets/WebGLTemplates/` (verify which template is live).
  ⛔ `Builds/WebGL/index.html` is BUILD OUTPUT — never hand-patch it as the fix.
- Wrap every `localStorage` access in try/catch; `:123`'s own comment records that Pi Browser in
  hardened mode THROWS on the mere ACCESS of `window.localStorage`.

**Lane B — why the on-device fetch fails.**

> ### ✅ ALREADY DISPROVEN — DO NOT RE-INVESTIGATE THE PUSH. This is NOT §16.
> Measured 2026-09-02 by HTTP HEAD against `https://pub-ab6dfaf1b3d74ca78891876611ccb832.r2.dev/WebGL/`:
> ```
> 200         32 bytes   catalog_2026.09.02.352005.hash
> 200     136515 bytes   catalog_2026.09.02.352005.bin
> 200    1525130 bytes   structure_art_assets_structures_genericcontainer_72a4fe9bf243be69e09e3e807b2ceeba.bundle
> 200    2468767 bytes   structure_art_assets_structures_tower_wooden_watchtower_a9e08b33790deeee805caca04b8e4e97.bundle
> ```
> The catalog serial matches the running build (`352005`) and the failing addresses' bundles are
> present AND publicly readable. **This is NOT the §16 missing-push class and NOT PROD-021's family.**
> Do not run a push to "fix" this; do not touch `ServerData/` or `r2-ship.ps1`.

The content is reachable from the open internet, so the failure is **on the device**: Pi Browser is
not completing these fetches. The `model not found` line is the DOWNSTREAM symptom and does not carry
the network cause.

- **Instrument the fetch failure itself.** `model not found via Addressables OR Resources` is emitted
  after the fact and says nothing about WHY. Capture and `FlowTrace` the underlying
  `UnityWebRequest`/`RemoteProviderException` detail (status code, `result`, timeout-vs-error) on the
  Addressables failure path so the trace names it. §12: the log must state the cause, not the effect.
- Bound the retry storm: a failed residency request must not re-arm indefinitely (`b707` shows the
  same address cycling `-> Skin` / `not found` / `<- Skin` repeatedly in its final seconds).
- Consider concurrency: on a memory-capped webview, several simultaneous multi-MB bundle downloads +
  decompression is the most plausible way to blow the ceiling while the Unity heap stays flat at
  247MB. A serialised / capped-concurrency residency queue is a candidate mitigation — but land the
  instrumentation FIRST and let the data choose.

## Acceptance

- [ ] `[PiLifecycle] boot= previous= navigation=` appears in `?view=traces&session=<id>` for a Pi
      Browser session — RESULT quotes one verbatim and names which table row it matches.
- [ ] A Pi Browser session survives **>10 minutes** of the owner standing still (the current ceiling
      is 30-60s). Quote the session id and its measured lifetime.
- [ ] Zero `model not found via Addressables OR Resources: 'Structures/…'` lines in that session.
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` + `R2_PARITY_OK` on FRESH logs.
- [ ] Deployed so the owner can retest (`tools\command-centre.ps1`; **`VERCEL_TOKEN` must be in
      `.env.local`** or the chain refuses at step 5).
- [ ] **PO felt-verifies and closes. CLI does not close.**

## What NOT to touch

- **The Pi payment path is not implicated.** `PiBrowserPaymentProvider.cs` behaved correctly: its
  `InitTimeout=20s` / `AuthTimeout=60s` bounds (`:67-68`) were never reached because the tab died ~12s
  in. ⛔ Do not add or tune a timeout constant there — it would bake in the wrong diagnosis permanently.
- Do not change the requested scopes or the lazy payments-scope design (`:494-537`).
- Do not touch the WO-1323 spotlight or the `CanBuy` one-SKU rail ruling — both are correct in this trace.
- Do not "fix" the Pi Browser on-demand asset policy by re-enabling eager residency without proof;
  that policy exists for a reason and eager download may be strictly worse on a memory-capped webview.

## Loose thread (separate, low priority — do NOT fix here)

`[Flow:Store] -> Purchase pack='hearth-spark' Skr` labels the currency `Skr` on the Pi rail. Routing
was correct (PiPay quoted `54.58 Pi`), so this is a stale trace label only.
