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

---

## TRIAGE 2026-09-02 EVENING (quiet lane)

**Read-only background triage. No code edited, no batchmode run, no deploy, no commit.** The only
write made by this pass is this section. Scope was acceptance criterion #1 only: *does
`[PiLifecycle] boot= previous= navigation=` reach the database, and which table row does it match?*

**Answer: YES. The crumbs landed. Five of them. And they match NONE of the three table rows cleanly —
they are a HYBRID, and the hybrid is itself the finding.**

### 1. The crumbs exist — verbatim, all five

Read path: `GET https://defenders-of-the-realm-v2.vercel.app/api/admin/db?view=traces&session=<id>&order=asc&limit=50`,
header `x-admin-key` = `ADMIN_DASH_KEY` (resolved from `.env.local` per `docs/ACCESS_AND_SECRETS.md` §3;
value never printed, len=32). Session list read from the same endpoint with no `&session=`, `limit=50`
(reaches back to 2026-09-01T10:42Z, so the whole post-deploy window is covered with room to spare).

All five rows carry `build: 2026.09.02.352005@echoes-of-elarion.vercel.app`, `line_count: 1`,
`total_batches: 1`:

```
wt-pi515afdf777  2026-09-02T22:54:38.272Z
  "[PiTemplate] log: [PiLifecycle] boot=mtkp1de9-bumg5x previous=null navigation=navigate"

wt-pi83ffd41113  2026-09-02T22:54:48.972Z
  "[PiTemplate] log: [PiLifecycle] boot=mtkp1nh3-dror0i previous={\"id\":\"mtkp1de9-bumg5x\",\"phase\":\"unity-running\",\"at\":1788389688415} navigation=back_forward"

wt-pi61feaf456e  2026-09-02T22:55:06.018Z
  "[PiTemplate] log: [PiLifecycle] boot=mtkp20l0-t83rks previous={\"id\":\"mtkp1nh3-dror0i\",\"phase\":\"unity-running\",\"at\":1788389697668} navigation=navigate"

wt-pibaa54c0ca6  2026-09-02T22:55:15.998Z
  "[PiTemplate] log: [PiLifecycle] boot=mtkp28c0-t4jw3r previous={\"id\":\"mtkp20l0-t83rks\",\"phase\":\"unity-running\",\"at\":1788389715522} navigation=back_forward"

wt-pibdf00907cc  2026-09-02T23:41:01.286Z
  "[PiTemplate] log: [PiLifecycle] boot=mtkqoysn-l2z168 previous=null navigation=navigate"
```

**Lane A works. Acceptance criterion #1's first half is MET** (the second half — "names which table row
it matches" — is answered in §3, and the honest answer is "none of them, and here is what that means").

The live page is confirmed to be serving the instrumented template: `GET https://echoes-of-elarion.vercel.app/`
returns 200 with 15 occurrences of `PiLifecycle` and
`var PI_TRACE_ENDPOINT = 'https://defenders-of-the-realm-v2.vercel.app/api/trace';` at line 144,
`productVersion: "2026.09.02.352005"` at line 244.

### 2. The reconstructed boot chain (four consecutive deaths in 40 seconds)

`bootId` is `Date.now().toString(36) + '-' + rand` (`Assets/WebGLTemplates/Pi/index.html:275`), so the
prefix decodes to a wall clock. `previous.at` is the epoch-ms at which the PREVIOUS page wrote its last
breadcrumb. Both decoded (UTC):

| # | session | boot stamped | previous page | its last phase | phase written at | navigation |
|---|---|---|---|---|---|---|
| A | `wt-pi515afdf777` | 22:54:35.793 | — | — | — | `navigate` |
| B | `wt-pi83ffd41113` | 22:54:48.855 | A | **`unity-running`** | 22:54:48.415 | `back_forward` |
| C | `wt-pi61feaf456e` | 22:55:05.844 | B | **`unity-running`** | 22:54:57.668 | `navigate` |
| D | `wt-pibaa54c0ca6` | 22:55:15.888 | C | **`unity-running`** | 22:55:15.522 | `back_forward` |
| E | `wt-pibdf00907cc` | 23:40:56.231 | *(null)* | — | — | `navigate` |

Derived intervals:

```
page A lifetime (boot -> successor boot)      13.06 s      load time (boot -> unity-running)  12.62 s
page B lifetime                               16.99 s      load time                           8.81 s
page C lifetime                               10.04 s      load time                           9.68 s

A's unity-running -> B boots                   0.44 s
B's unity-running -> C boots                   8.18 s
C's unity-running -> D boots                   0.37 s
```

### 3. Which root the data supports

The WO's table expects one of three shapes. **The captured data is a hybrid of rows 1 and 2 and
therefore matches neither as written:**

| WO table row | expected | observed | verdict |
|---|---|---|---|
| jettison | `navigation=reload` + `previous=unity-running` | `previous.phase=unity-running` YES, but `navigation` is `navigate`/`back_forward`, **never `reload`** NO | half-matched |
| navigated away | `navigation=navigate`/`back_forward` + `previous=pagehide` | `navigation` matches YES, but `previous.phase` is **`unity-running`, never `pagehide`** NO | half-matched |
| teardown during boot | crumb never advances past `unity-loading` | crumb advanced to `unity-running` **every time** NO | **RULED OUT** |

**Three facts the data establishes on its own, independent of which row you prefer:**

**(a) Teardown-during-boot is dead.** `piBootCrumb('unity-running')` is written only inside the
`createUnityInstance(...).then(...)` resolution (`Assets/WebGLTemplates/Pi/index.html:270`). Three
separate pages reached it. The runtime was fully constructed each time. The WO's third table row —
"teardown during boot/asset residency" — is disproven for this window.

**(b) The page dies WITHOUT an orderly teardown.** `piBootCrumb` overwrites a single localStorage key
with the newest phase, so the successor always reads the predecessor's LAST phase. That phase is
`unity-running` in all three chained cases. The `pagehide` handler
(`Assets/WebGLTemplates/Pi/index.html:568-573`) writes `piBootCrumb('pagehide')` **and** beacons a line;
the `visibilitychange` handler (`:574-580`) beacons on `hidden`. **Not one `pagehide` crumb was
persisted, and not one `pagehide` or `visibility` row reached the database** — every `wt-pi` session
has `total_batches: 1` and that one batch is the boot line. A browser-initiated navigation-away fires
`pagehide`. This did not. **So "Pi Browser navigated the page away" (row 2) is NOT supported as
written**, unless `sendBeacon` is itself being dropped by Pi Browser — a possibility this data cannot
exclude (see §5 for the one instrument that would settle it).

**(c) The death is time-locked to Unity finishing its load.** In two of the three measurable cases the
successor page booted **within half a second** of the predecessor writing `unity-running`
(0.44 s and 0.37 s). The third was 8.18 s. That is the moment of peak resident footprint — the loader's
buffers, the freshly decompressed heap and the newly resident bundles all coexisting — and it is
consistent with an out-of-process memory ceiling. It is **not** consistent with a steady-state leak,
and it is not consistent with anything input-driven (the owner reported standing still).

**The supported reading, stated as strongly as the data allows and no further:** the tab is being
terminated abruptly at/near the completion of Unity's load, without firing `pagehide`, and the browser
then re-enters the history entry — reporting `back_forward` twice and `navigate` twice — rather than
issuing a `reload`. That is closer to the jettison row than to the navigated-away row, **but the
`navigation=reload` signature the WO predicted for jettison did not appear, so the jettison row cannot
be marked proven either.** §12 forbids me closing that gap by inference. One more crumb closes it —
named in §5.

WARNING: this does **not** re-open the disproven iOS-jetsam finding of WO §5. That negative was measured
against the **18:51-19:02Z** window. This window is **22:54-22:55Z**, five hours later; the Analytics
Data list has not been re-read for it. A `JetsamEvent-2026-09-02-2254*.ips` would be decisive and is a
five-second owner check.

### 4. A second, unexpected finding: Unity's OWN trace sink produced NOTHING in this window

The `?view=traces` session summary for the whole 7-day window, top 50 by recency, contains **no `wt-`
session at all between `wt-94f61e332ce1` (latest 2026-09-02T22:07:09.212Z) and now** — only the five
`wt-pi` template rows. Yet §2 proves Unity reached `unity-running` three times in that window, and page
B ran for **8.18 s** afterwards.

`WebTrace` flushes "every `FlushSeconds` OR when `FlushThreshold` entries queue"
(`Assets/_Modules/Core/Diagnostics/WebTrace.cs:30`), with `FlushThreshold = 50` (`:81`) and
`FlushSeconds = 5f` (`:82`). Comparable Pi sessions earlier the same day emitted hundreds of lines in
their first seconds (`wt-1b3fc89fb4a6`: 146 batches / 2743 lines in a 53 s life). An 8.18 s run that
posts **zero** batches is therefore anomalous.

**I am not concluding a cause for this.** Candidates the data does not separate: the runtime being
killed before the first flush window in the two 0.4 s cases (plausible for A and C, **not** for B); the
sink never activating on this deploy; or posts being made and rejected. It matters because it means the
usual Unity-side evidence stream for this P0 is currently **dark**, and any RESULT claiming the Unity
trace shows something for this window would be reading an empty set.

**Cross-reference: `WorkOrders/WORK_ORDER_1324_webtrace_loses_the_crash_window.md`** (READY, currently
PARKED behind the Android APK) is the same instrument and names the mechanism: up to 5 s of lines sit
in a RAM ring when the tab dies, and `WebTrace.cs:33` records *"On failure the batch is DROPPED (no
retry)"*. That fully accounts for pages A and C (0.44 s and 0.37 s of post-`unity-running` life — the
ring never reached a flush). It does **not** account for page B's 8.18 s, which spans at least one 5 s
cadence. So WO-1324 explains most of §4 but not all of it, and the residue is still unexplained.

### 5. The ONE instrument that closes the remaining ambiguity (Lane A follow-up, spec only)

`pageshow` is logged to console but **is not forwarded**: `Assets/WebGLTemplates/Pi/index.html:564-567`
calls `console.info('[PiLifecycle] pageshow boot=' + bootId + ' persisted=' + !!e.persisted)` with no
`piTraceEmit`. Every other lifecycle hook forwards; this one does not.

`persisted` is exactly the discriminator the boot line lacks:

| next boot reads | means |
|---|---|
| `navigation=back_forward` + `pageshow persisted=true` | the document survived in bfcache and the browser genuinely navigated back — the app was **not** killed |
| `navigation=back_forward` + `pageshow persisted=false` | a **fresh document** was built for an existing history entry — the previous content process is gone, i.e. it was terminated |

One `piTraceEmit(line, false)` on that handler (plain fetch — `pageshow` is a load-time event, not a
teardown one, so no beacon is needed) turns tonight's hybrid into a named root. It is a two-line change
to the same file Lane A already owns, and it needs no new plumbing.

Two smaller gaps worth folding into the same edit:
- `piBootCrumb`'s **write** failure path is `console.info` only (`:214-220`), so a blocked write is
  invisible in the database while a blocked **read** is forwarded (`:226-232`). That asymmetry is why
  §6's `previous=null` cannot be fully resolved.
- Nothing forwards a heartbeat, so "the page was alive at T" is only ever inferable from the *next*
  boot's crumb.

### 6. `previous=null` on boot E is a real signal, and it is ambiguous by exactly one instrument

Boot D wrote a `template` crumb at 22:55:15.888. Boot E at 23:40:56.231 read `previous=null`. The
storage READ did not throw — `piReadBootCrumb` forwards a
`[PiLifecycle] breadcrumb read unavailable (storage blocked)` line to the trace when it does
(`:226-232`), and no such line exists in `wt-pibdf00907cc`. Writes demonstrably worked at 22:54-22:55
(boots B, C and D each read a non-null predecessor). So either the site's localStorage was **cleared**
between 22:55 and 23:41, or D's write silently failed — and per §5 a failed write leaves no database
trace. Do not build on this either way until the write path is forwarded.

### 7. `client_tunables` — reachable and empty, which is the correct resting state

Per `docs/PROD022_TUNABLE_FLAGS.md` ("An empty `client_tunables` table is the correct resting state,
and it is what ships"):

```
GET https://defenders-of-the-realm-v2.vercel.app/api/client-tunables
http=200   {"ok":true,"version":1,"readOk":true,"reason":"OK","values":{}}
```

`readOk:true` with `values:{}` = the query ran against the table and returned no rows. Migration
`api/migrations/20260902_0018_client_tunables.sql` (commit `0c607c27a`) is live. **The table is
reachable and empty. No flag is armed.** Whatever is happening in §2 is happening at the build's
shipped defaults, unmodified.

WARNING — **one thing to be aware of, NOT a defect in the current path.** The same endpoint on the other
host answers differently:

```
GET https://echoes-of-elarion.vercel.app/api/client-tunables
http=200   {"ok":true,"version":1,"readOk":false,"reason":"NO_SQL_HANDLE","values":{}}
```

`echoes-of-elarion.vercel.app` — the host the game is actually served from — has **no database handle
bound**, so its copy of the endpoint can never return an override. This does not affect the client,
because `RemoteTunablesService` pins the other host:
`private const string BackendBase = "https://defenders-of-the-realm-v2.vercel.app";`
(`Assets/_Modules/Core/Ops/RemoteTunablesService.cs:93`), and the fail-soft design means both answers
resolve to the build default anyway. **But it is a trap for the next seat**: anyone who flips a tunable
and then verifies it by curling the host the game is served from will read `readOk:false` and conclude
the flip failed. Verify tunables against `defenders-of-the-realm-v2.vercel.app` only.

### 8. What this pass did NOT do

- Did not run any Unity batchmode, gate, build or deploy; no `.cs` file was opened for edit.
- Did not re-investigate the R2 push (WO already disproved it) and did not touch `ServerData/`.
- Did not measure a >10-minute Pi session (acceptance #2). The longest post-deploy Pi page in the
  captured window lived **16.99 s**. Criterion #2 remains **unmet**.
- Did not close the WO. Root is still **NOT PROVEN** — narrowed, one crumb short.

### 9. Recommended next step, in priority order

1. **Forward `pageshow persisted=`** (§5). Two lines, same file, same lane. It is the single remaining
   discriminator between "terminated" and "navigated".
2. **Owner re-check Analytics Data for a `JetsamEvent-2026-09-02-2254*`** (§3). Five seconds, and it
   independently confirms or kills the jettison reading for *this* window without a rebuild.
3. **Explain the dark Unity sink** (§4) before trusting any Unity-side evidence for this window.
4. Only then flip a tunable. Flipping now would bisect against an unproven root, and §7 shows the table
   is clean — so today's captures are a valid baseline that should not be disturbed until #1 lands.


---

## LANE A ADDENDUM — 2026-09-02 evening: the `pageshow persisted=` discriminator is now forwarded

**Owner-authorised while the APK lane gates** (Pi is otherwise PARKED; this is a two-line telemetry
edit, not a resumption of the Pi lane).

`Assets/WebGLTemplates/Pi/index.html` — the `pageshow` handler was `console.info`-only while every
other lifecycle hook forwarded to `/api/trace`, which is precisely why the evening triage narrowed
the root to "abrupt termination without pagehide" and then ran out of evidence. It now emits through
`piTraceEmit(line, false)` and carries `navigation=` alongside `persisted=`.

**Why `persisted` is the discriminator, and why nothing else we capture substitutes for it:**

| next-boot signal | root it names |
|---|---|
| `navigation=back_forward` + `persisted=true` | the document was restored from the **bfcache** — Pi Browser navigated away and back; **the content process lived** |
| `navigation=back_forward` + `persisted=false` | the document was **REBUILT** — the content process was killed while the page sat in the back/forward list |

The five post-deploy sessions showed `previous.phase='unity-running'` with
`navigation='navigate'`/`'back_forward'` and **no `pagehide` crumb anywhere**, matching no row of the
original diagnostic table above. `persisted` splits that hybrid in one field.

⛔ **Deliberately NOT a beacon.** `pageshow` is a restore, not a teardown, so an ordinary `fetch`
survives; sending it by beacon would put a non-teardown crumb on the same queue `pagehide` and
`visibilitychange:hidden` depend on.

**Not yet proven — this is an instrument, not a fix.** It changes no game behaviour and cannot
shorten a session. It requires a WebGL rebuild + deploy to reach the device, which is NOT being done
tonight: the Android APK is the priority (owner ruling, `KEY_FACTS.md`). Until that deploy happens,
the shipped Pi build still lacks this crumb.

⚠ **Verify a tunable flip against `defenders-of-the-realm-v2.vercel.app`, NOT
`echoes-of-elarion.vercel.app`.** The latter is the host the game is *served* from, but its
`client-tunables` endpoint answers `readOk:false / NO_SQL_HANDLE` — no DB handle bound. The client
pins the former (`RemoteTunablesService.cs:93`), so this is harmless at runtime and purely a trap for
whoever verifies a flip by hand.

---

## OVERNIGHT SME PASS — 2026-09-02/03: the loop is now MEASURED, and two of the three candidate roots are DEAD

**Scope:** Pi/WebGL template, the Pi Addressables policy, the tunables, `api/`. No `.cs` edited, no
commit, no Android/APK lane, nothing under `Assets/_Modules/Village`.

> ### ⭐ THE HEADLINE, BEFORE ANY THEORY
> A **41-boot chain** landed in the database at **2026-09-03T00:21:52-00:28:05Z** — thirteen times
> the evening pass's sample. In it, **40 consecutive deaths carry `previous.phase="unity-running"`,
> `navigation=navigate`, and NOT ONE `pagehide` and NOT ONE `visibilitychange`** — while those very
> handlers **fired six times in the same window, on the same build, on the same device**. That is a
> real negative on a demonstrably working instrument, and it kills the "Pi Browser navigated the
> page away" row outright.
>
> **The page is destroyed with no teardown event of any kind, a median 1.35 s after Unity finishes
> loading, 40 times out of 40.**

### 1. The capture — verbatim, and the arithmetic

Read path: `GET https://defenders-of-the-realm-v2.vercel.app/api/admin/db?view=traces[&session=<id>]`,
header `x-admin-key` = `ADMIN_DASH_KEY` from `.env.local` (never printed; len=32). 49 `wt-pi`
sessions pulled, 61 rows. All rows `build: 2026.09.02.352005@echoes-of-elarion.vercel.app`.

Three verbatim rows that carry the whole finding:

```
00:22:08.327Z  wt-pi7af66fa897
  "[PiTemplate] log: [PiLifecycle] boot=mtks5y7g-kg4bw1 previous={\"id\":\"mtks5rbx-n522pe\",\"phase\":\"unity-running\",\"at\":1788394927076} navigation=navigate"

00:28:02.114Z  wt-pi14a61d89ac
  "[PiTemplate] log: [PiLifecycle] pagehide boot=mtksdefo-286awt persisted=true"

02:05:18.413Z  wt-pi1a316b9a07
  "[PiTemplate] log: [PiLifecycle] pagehide boot=mtkvudqf-024gu0 persisted=false"
```

`bootId` decodes to a wall clock (`Date.now().toString(36)`), and `previous.at` is the epoch-ms of
the predecessor's last crumb, so the whole chain is measurable:

| measure | n | value |
|---|---|---|
| consecutive boots in one unbroken chain | 41 | 00:21:51.239 - 00:28:05.252Z |
| `previous.phase == "unity-running"` | **40 of 43** | the other 3 are `unity-loading` |
| `navigation` | **49 of 49** | `navigate`. **Never `reload`. Never `back_forward`.** |
| `pagehide` crumbs inside the 40-death chain | **0** | six landed elsewhere in the same window |
| predecessor's `unity-running` -> successor's boot | 40 | min **0.85 s**, median **1.35 s**, max **4.21 s** |
| boot -> boot cycle | 40 | min 8.22 s, median **8.71 s**, max 11.43 s |

### 2. Which diagnostic row it matches — and the table row that has to be REWRITTEN

| WO table row | verdict tonight | the measurement that decided it |
|---|---|---|
| jettison (`navigation=reload` + `previous=unity-running`) | **half-matched, and the `reload` half is now positively FALSIFIED** | `navigation=navigate` on 49/49. The predicted `reload` signature does not occur at all. |
| navigated away (`previous=pagehide`) | ⛔ **DEAD** | A browser-initiated navigation *always* fires `pagehide`. 40 consecutive deaths fired none, on an instrument that fired 6 times in the same window. |
| teardown during boot (never past `unity-loading`) | ⛔ **DEAD** (re-confirmed at 13x the sample) | 40/43 predecessors reached `unity-running`, which is written only inside `createUnityInstance().then()`. |

⚠ **The `navigation=` column of the original table is not a reliable discriminator and should not be
used as one.** It predicted `reload` for a jettison and `navigate`/`back_forward` for a navigation-away;
we observe `navigate` for a death that is provably neither. What the host does *after* the page dies
is the host app's choice — Pi Browser evidently issues a **fresh navigation**, which is exactly what an
iOS host does from `webViewWebContentProcessDidTerminate`. **The presence or absence of `pagehide` is
the load-bearing signal; `navigation=` is colour.**

### 3. Hypotheses KILLED tonight, each with the measurement that killed it

| # | Hypothesis | Killed by |
|---|---|---|
| 1 | Pi Browser navigated the page away | §1: zero `pagehide` across 40 deaths, six `pagehide` rows in the same window prove the handler and `sendBeacon` both work in Pi Browser. |
| 2 | The page reloaded itself (`location.reload`, a JS-side restart) | Same zero-`pagehide` negative, plus `navigation=navigate` never `reload` on 49/49. |
| 3 | bfcache / back-forward restore | `navigation=back_forward` occurred **zero** times in this window; the only `persisted=true` values sit on the **orderly** exits at 00:28 and 01:38 (the owner backgrounding the app), never inside the loop. |
| 4 | Teardown during boot / asset residency | 40/43 predecessors reached `unity-running`. |
| 5 | The payload is being JS-inflated, double-buffering ~200 MB | **Direct HEAD measurement**: all four `/Build/*` files answer `Content-Encoding: br` (loader.js, framework, wasm, and the 165,180,640-byte data file). Commit `3622e4d3c`'s fix is live and healthy; the browser decompresses natively. |
| 6 | A blocking `WaitForCompletion` wedging the main thread in the Pi warmer | Source read: the Pi branch of `StructureContentWarmer.Boot` is fully async and contains no `WaitForCompletion`; the repo already lints for it in four regressions. **This is LOCATING, not concluding** — it excludes this file, not the whole runtime. |
| 7 | "The shipped build has no tunables, so a flag flip cannot reach it" | Nearly written up as a finding, and **wrong**. The desktop session `wt-6b0185078310` (508 lines) contains **zero** `[Flow:Tunables]` lines — but a byte grep of the decompressed shipped payload finds `pi.disableRemoteStructureArt`, `client-tunables` and `tunables.cache.v1`. See §4. |

### 4. ⚠ `[Flow:Tunables] CONFIG` NEVER REACHES THE DATABASE — the doc's felt-test instruction cannot be followed as written

`docs/PROD022_TUNABLE_FLAGS.md` says *"Quote this line in any felt-test report — a run whose
configuration cannot be reconstructed afterwards proves nothing."* **On the default path that line is
unquotable.** `RemoteTunables.LogConfiguration("StructureContentWarmer.Boot")` is the first statement
of a `RuntimeInitializeOnLoadMethod(AfterSceneLoad)` hook, and `WebTrace`'s sink activates in the same
undefined-ordered `AfterSceneLoad` batch. In `wt-6b0185078310` the sink's own
`[WebTrace] Remote trace sink active` is line 1 and `[Flow:StructureAssets] -> WarmRoutine` (a frame
later, from the coroutine Boot starts) is present — so Boot ran, and its **synchronous** lines were
emitted into a sink that was not yet listening.

**What still works:** the `LogConfiguration("payload accepted, rows=N ...")` call fires after the
network round trip, long after the sink is up. **So a session with a row set WILL show a
`[Flow:Tunables] CONFIG (payload accepted, ...)` line, and a session at defaults will show nothing at
all.** Presence of that line is the confirmation that a flip reached the device; absence is *not*
evidence the build lacks tunables — row 7 above is the trap that nearly caught this pass.

### 5. What was deployed — the instrument, and why it needed no rebuild of its own

`Assets/WebGLTemplates/Pi/index.html` (template SOURCE; `Builds/WebGL/index.html` was never
hand-patched) gains **PROD-022 Lane C**, validated before it shipped:

- **A Worker-thread heartbeat.** The main thread stamps its liveness every 500 ms and does nothing
  else; the worker owns all posting, at 1 s for the first 60 beats then 10 s forever. Line shape:
  `[PiLifecycle] hb w=<workerBeat> m=<mainTick> mAgeMs=<age of the main stamp> vis=<state> upMs=<uptime>`.
- **`webglcontextlost` / `webglcontextrestored`** on `#unity-canvas`, observational only
  (`preventDefault` is never called, so the browser's own restore behaviour is unchanged). Context
  loss is the door WebKit uses to reclaim GPU memory *before* it resorts to killing a process.
- Plus the already-committed `pageshow persisted=` forwarding (`c35a1e037`), which had never reached
  a device.

**Why a worker.** Exactly two mechanisms produce "destroyed with no teardown event", and no crumb we
capture separates them: **(a)** the content process is terminated from outside — nothing runs because
there is no process left; **(b)** the **main thread** is wedged, so no handler *can* run, and the host
tears the page down afterwards. (a) is a build/footprint problem. (b) is our code. **They have
opposite fixes.** A worker has its own thread, so the last rows of a dead session decide it:

| last heartbeat rows | root it names |
|---|---|
| `w` rising while `m` is **frozen** and `mAgeMs` climbing | **(b)** the main thread wedged first |
| `w` and `m` stop in the same instant, `mAgeMs` small | **(a)** the whole content process died at once |

**Validation before shipping** (a broken inline worker would blank the page): the substituted
template parses (`node --check` → `NODE_SYNTAX_OK`); the worker source string was then extracted,
re-parsed, and **executed** against a stubbed `fetch`, producing
`[PiLifecycle] hb w=1 m=7 mAgeMs=2238 vis=visible upMs=1004` in the exact
`{sessionId, buildId, entries[{utcMs,kind,tag,message,scene}]}` shape `api/trace.js` reads.

**One known bound, stated now so nobody reads it as a death later.** The worker's beats use
`fetch(..., {keepalive: true})`, and a document's keepalive quota is **64 KB**. At ~230 bytes a beat
that is roughly **278 beats** - 60 at the 1 s cadence plus ~36 minutes at the 10 s one. A session that
runs past about **37 minutes** may therefore stop emitting `hb` rows *while still alive*. The
acceptance bar is 12 minutes so this does not bite tomorrow; the fix (drop `keepalive` from the worker,
which does not need it - a beat lost to process death is lost either way) was deliberately NOT made
tonight because the template was being read by an in-flight Unity build and a partial read would have
shipped a corrupt page.

**Cost, stated rather than hidden:** one worker (~1-2 MB) and one POST/sec, against a 165 MB data
file. Too small to be the thing that tips a memory ceiling — but it is a real addition, and it is
named here so nobody has to wonder later.

**It needed no rebuild of its own.** The shipped `Builds/WebGL/index.html` differs from the template
by exactly six macro substitutions and nothing else (verified by diff), so this is a pure page-layer
change that pairs with an unchanged binary. In the event it rode the WebGL build inside the lead's
`command-centre.ps1` production chain, which was already running when the edit landed.

### 6. The flag armed for the morning, and the prediction — written BEFORE the run

**`pi.disableRemoteStructureArt = 1`** (knob 3, the big hammer). One knob, not several, so the answer
is unambiguous; and it is the only one decisive in *both* directions.

> **PREDICTION, recorded in advance: this will NOT stop the loop.** The chain dies a median 1.35 s
> after the first frame, and every trace line in these sessions is scoped `[Title]` — the app is dying
> on the **title screen**, before the town scene where structure art is requested at all. If that
> prediction holds, the entire asset-streaming lane (knobs 1, 2, 4, 5, 6) is exonerated in **one
> session**, because knob 3 suppresses every remote structure request outright and therefore subsumes
> them. If the prediction FAILS and the loop stops, streaming is implicated beyond argument and the
> follow-up bisect is knob 2 (`pi.awaitInitBeforeFirstLoad`) against knob 4
> (`assets.maxConcurrentRequests`).

Either outcome is a good morning. One is a diagnosis; the other is a **playable game**.

### 7. The two survivors, and the honest state of the root

**ROOT IS STILL NOT PROVEN.** It is narrowed from three candidates to two, and the instrument that
separates them is now on the device:

- **(a) The content process is terminated from outside.** Consistent with everything measured. The
  structural numbers that make it plausible, offered as **LOCATING and explicitly not as a
  conclusion**: a **165,180,640-byte** compressed data file decompressing to **209,534,992 bytes**,
  `webGLDataCaching: 1` (Unity mirrors that payload into IndexedDB and reads it back every boot),
  `webGLMaximumMemorySize: 2048`, and a Unity heap that settles at 247 MB — every one of which peaks
  at precisely the instant the deaths cluster.
- **(b) The main thread wedges and the host tears the page down.** Not excluded. The Pi branch of
  `StructureContentWarmer` is clean, but that is one file out of the whole boot path.

⚠ **The `JetsamEvent` negative in §5 of the RCA does NOT cover this window.** That check was made at
14:19 local against the **18:51-19:02Z** window. This chain is **00:21-00:28Z on 2026-09-03** — that
is **19:21-19:28 local on 2026-09-02**, hours after the screenshot was taken, so its absence from that
list proves nothing about it. A `JetsamEvent-2026-09-02-19*` would be decisive for (a).

### 8. ⛔ A TOOLING HAZARD FOUND IN PASSING — `-Tunables` CLOBBERS THE SHIP CHAIN'S OWN LOG

`tools/command-centre.ps1` opens with `Set-Content -LiteralPath $runLog` on
`Builds\command-centre.log` **before** it branches into `-Maintenance` / `-Tunables`. Those two modes
are documented as surfaces that "run and EXIT; never touch the ship chain below" — but they
**destroy the ship chain's marker record** if one is running. Tonight the lead's chain held
`STEP_1_OK`..`STEP_4_OK` in that file while a `-Tunables` flip was queued; running it would have
erased the gate evidence for an in-flight production deploy. **The flag flip was deliberately held
until the chain finished.** Worth a one-line fix (a separate log per mode) in whoever's lane owns that
script — it is not this pass's lane and was not changed.

### 9. Acceptance criteria — honest status

- [x] **#1 `[PiLifecycle] boot= previous= navigation=` in the database, with the row it matches.** Met
      at 41x the original sample. Row: **none of the three as written** — see §2, which explains why
      the `navigation=` column cannot carry that job and `pagehide` can.
- [ ] **#2 A Pi session survives >10 minutes.** **UNMET.** The longest page in tonight's chain lived
      **~8.7 s**. This pass did not fix the crash; it measured it and killed two of the three
      candidate roots.
- [ ] **#3 Zero `model not found` lines.** Not evaluable: Unity's own sink produced nothing for the
      loop window (WO-1324 — the RAM ring dies with the tab, and at 1.35 s of post-load life it never
      reaches a flush).
- [x] **Deployed** — the instrument rides the lead's `command-centre.ps1` production chain.
- [ ] **PO felt-verifies and closes.** CLI does not close.

### 10. ⭐ THE MORNING TEST — one action, and what each outcome proves

**Do this and nothing else:**

1. Open the game in Pi Browser.
2. **Stand still for 12 minutes.** Do not tap anything. If it resets, let it keep resetting — every
   reset writes a row. If it survives, just leave it sitting.
3. Say "done" and hand it back. **Optional, five seconds, and genuinely decisive:** Settings → Privacy
   & Security → Analytics & Improvements → Analytics Data → look for any file named
   `JetsamEvent-2026-09-02-19…` or `JetsamEvent-2026-09-03-…`.

**What comes back and what it proves** (read the LAST `hb` rows of the last dead session):

| what the trace shows | what it proves | what happens next |
|---|---|---|
| `w` still rising while `m` is frozen, `mAgeMs` climbing past ~2000 | The **main thread wedged first** — this is OUR code, in the boot path. | Bisect the boot path; the fix is ours and is a code fix. |
| `w` and `m` stop together, `mAgeMs` small | The **content process was killed outright** — a footprint problem, not a logic bug. | The lever is the 165 MB payload: strip/split content, turn off `webGLDataCaching`, cut texture budget. Not a flag; a build change. |
| a `webglcontextlost` line before the last beat | **GPU/memory reclamation** named directly. | Same lever as above, with a concrete first target. |
| `heartbeat worker unavailable: …` | Pi Browser refuses blob: workers. | The `w=NA` fallback still measured lifetime; the wedge-vs-kill split needs a different instrument. |
| the loop **stops** and she plays past 12 minutes | Something in **{the new build, knob 3}** fixed it - ⚠ **NOT knob 3 alone**, because the deploy also moves the build from `352005` to `352921`. | She PLAYS. Then attribute it cheaply: `-Clear` the flag and retest. Still fine ⇒ it was the build. Loop returns ⇒ it was streaming, and the bisect is knob 2 vs knob 4. |
| the loop continues, unchanged | **Asset streaming is exonerated** — knobs 1/2/4/5/6 with it. | Clear the flag, and all attention goes to the load-completion footprint. |

**How to read it tomorrow, exactly** (the crumbs and the heartbeats share one session id, so one
query returns both, oldest first):

```
# 1. list the sessions - the wt-pi* rows are the template's, newest first
GET https://defenders-of-the-realm-v2.vercel.app/api/admin/db?view=traces&limit=60
# 2. open the LAST wt-pi session that died, and read its final rows
GET https://defenders-of-the-realm-v2.vercel.app/api/admin/db?view=traces&session=<id>&order=asc&limit=200
     header  x-admin-key: <ADMIN_DASH_KEY from .env.local - never printed>
```

The line to look at is the **last `hb`** of a dead session. `w` is the worker's beat, `m` is the main
thread's tick, `mAgeMs` is how long the main thread had been silent when the worker sent it.

**To put the art back at any time — one word, and it is `-Clear`, not `-Value 0`:**

```powershell
tools\command-centre.ps1 -Tunables -Key pi.disableRemoteStructureArt -Clear
```

### 11. ⛔ URGENT, AND BIGGER THAN THIS TICKET — `command-centre.ps1` PUSHES TO R2 *BEFORE* IT BUILDS, SO EVERY WebGL DEPLOY SHIPS AN UNPUSHED CATALOG

Found while waiting for the lead's chain, **measured, not inferred**, and it is the CLAUDE.md §16
class wearing a new face — the one §16 warns about in capitals: *"EVERY content build needs ITS OWN
push. A push from a previous build can never cover this one."*

**The ordering defect.** In `tools/command-centre.ps1`:

- **STEP 2** runs `tools\r2-ship.ps1` (push + verify) — the comment above it says *"every
  command-centre run ships Builds/WebGL and api/, so it always touches shipped content."*
- **STEP 5** runs `build-webgl.ps1`, and `AddressableAssetSettings.asset` carries
  `m_BuildAddressablesWithPlayerBuild: 1`, so **the player build regenerates the Addressables
  content** into `ServerData/`.

So the push happens **three steps before** the content it is supposed to be pushing exists.

**The consequence, and why it is silent.** `m_overridePlayerVersion:
'[UnityEditor.PlayerSettings.bundleVersion]'` — the remote catalog is **named after
`bundleVersion`**, which the Android lane bumped tonight to `2026.09.03.352921`. Measured on this
run:

```
ServerData/WebGL/catalog_2026.09.03.352921.bin    written 21:31   (the build)
ServerData/WebGL/catalog_2026.09.03.352921.hash   written 21:31
Builds/r2-parity.log                              written 21:29   (the push, TWO MINUTES EARLIER)
```

```
GET https://pub-ab6dfaf1b3d74ca78891876611ccb832.r2.dev/WebGL/catalog_2026.09.02.352005.hash -> 206  (the OLD, live build)
GET https://pub-ab6dfaf1b3d74ca78891876611ccb832.r2.dev/WebGL/catalog_2026.09.03.352921.hash -> 404  (the build about to be promoted)
GET https://pub-ab6dfaf1b3d74ca78891876611ccb832.r2.dev/WebGL/catalog_2026.09.03.352921.bin  -> 404
```

**The build now going to production cannot fetch its own catalog.** Per §16 it will install, launch
and play — with placeholder art and no error on screen. `R2_PARITY_OK` was green at 21:29 and proved
only that the *previous* build's content was intact.

⭐ **The one mitigating detail, and it is why the fix is cheap:** the **bundles** were NOT re-emitted
(newest bundle in `ServerData/WebGL` is 05:05) because their content is unchanged and their names are
content-hashed. **Only the two catalog files are new.** So the whole gap is 136,547 bytes.

**The remedy is the single sanctioned path, and it is safe to run after the deploy** — R2 is
independent of the Vercel alias and the catalog is fetched at runtime, so pushing it repairs a
already-promoted build immediately:

```powershell
tools\r2-ship.ps1        # judge R2_PUSH_OK + R2_PARITY_OK on a FRESH log, never the exit code
```

⚠ **THIS ALSO CONFOUNDS THE MORNING TEST IF IT IS NOT DONE.** A 404ing catalog produces *exactly* the
same observable as `pi.disableRemoteStructureArt = 1` — no remote structure art. Run tonight's flag
experiment against a 404ing catalog and its result is unreadable in **both** directions. The push must
land before the owner opens the app, or the flag must be cleared and the session treated as a
streaming-disabled run by accident rather than by design.

**The structural fix (not made here — `command-centre.ps1` is not this pass's lane):** move the
`r2-ship.ps1` call to **after** STEP 5's build, or call it twice. As written, *every* WebGL deploy
this chain has ever made shipped an unpushed catalog; it only became visible tonight because the
Android lane moved `bundleVersion` between the last content push and this build.

### 11a. CORRECTION to §11, made the same night — the 404 closed itself, the ORDERING DEFECT DID NOT

Honesty first: by the time `tools\r2-ship.ps1` was run at 21:56, both catalog files were **already on
R2** — the push reported `R2_PUSH_OK 0 uploaded (0.0 MB), 732 unchanged`, and the URLs that answered
**404** at ~21:40 answer **200** now (re-checked with a cache-buster). **I cannot attribute who put
them there**; the most likely explanation is that another seat pushed between the build finishing and
my run. The 404 measurement was real and timestamped; its repair was not mine.

**What is NOT corrected, because it was verified in source rather than inferred:** the ordering defect
stands. `tools\r2-ship.ps1` is invoked at **STEP 2 only**; `build-webgl.ps1`, `WebGLBuild.cs` and
command-centre STEPs 5-6 contain **no** R2 push (grepped). So the chain still, by construction, pushes
before it builds. Tonight it happened to be papered over by a manual push.

Final verified state, on a fresh log:
`R2_PARITY_OK targets=Android,StandaloneWindows64,WebGL objects=261` +
`R2_CORS_OK`, with WebGL's line reading *"catalog the player will request:
catalog_2026.09.03.352921"*. **R2 is correct right now.**

⚠ Worth reading beside `Assets/Editor/WebGLBuild.cs:127-145`, which records **occurrence FIVE** of
this same CLAUDE.md §16 class (Addressables built for the wrong active target, WebGL player shipped
against a three-day-old catalog, every marker green). This is the same family in a new place: the
gate is not wrong, it is **early**.

---

### 12. ⛔ THE FINDING THAT CHANGES THE CONCLUSION — THE DEPLOY DOES NOT REACH THE OWNER'S GAME

**The instrument is live. It is live on the wrong host.**

`tools/command-centre.ps1` completed and promoted cleanly:

```
STEP_5_OK marker=CANDIDATE_CONTENT_MATCH    id=dpl_8wWpXV633cpbnJFBx31NUfjdsB3W
STEP_6_OK marker=PRODUCTION_ALIAS_MATCH     id=dpl_8wWpXV633cpbnJFBx31NUfjdsB3W
STEP_7_OK marker=PRODUCTION_DB_WRITE_OK
COMMAND_CENTRE_OK deployment=dpl_8wWpXV633cpbnJFBx31NUfjdsB3W rollback=dpl_C6PhR3T3GYV1JPoMDtJffDNBYW5d
```

Every marker green. Then measure the two hosts:

```
GET https://defenders-of-the-realm-v2.vercel.app/   -> 40100 bytes  productVersion "2026.09.03.352921"  Lane C instrument PRESENT
GET https://echoes-of-elarion.vercel.app/           -> 32609 bytes  productVersion "2026.09.02.352005"  Lane C instrument ABSENT
                                                       X-Vercel-Cache: HIT   Age: 10165   Last-Modified: 2026-09-03T00:04:59Z
```

**The owner plays on `echoes-of-elarion.vercel.app`.** That is not a guess — `WebTrace.MakeBuildId`
stamps the serving host into every row, and all 61 crumb rows read
`build: 2026.09.02.352005@echoes-of-elarion.vercel.app`.

**Why the chain structurally cannot reach it.** `.vercel/project.json` links this repo to
`projectName: "defenders-of-the-realm-v2"`. `echoes-of-elarion` is a **separate Vercel project**
(`samanthadenelle-creates-projects/echoes-of-elarion`) — `Builds/vercel-deploy-echoes-run.log` (17:30
today) shows it being deployed by its own `vercel deploy` run. `command-centre.ps1` defaults
`-ProductionUrl https://defenders-of-the-realm-v2.vercel.app` and STEP 6's `PRODUCTION_ALIAS_MATCH`
verifies **that** host. **It is a true marker about the wrong deployment.**

This is the same shape as everything else this ticket has turned up: the gate is honest, and it is
pointed one step off the thing that matters.

> **CONSEQUENCE FOR THE MORNING, STATED PLAINLY:** the Lane C heartbeat **has not reached the
> owner's device.** Until `echoes-of-elarion` is deployed, a Pi session will emit the old crumbs
> (boot / pagehide / visibility) and **no `hb` rows at all**. The wedge-vs-kill question stays open
> for one more deploy.

**What DOES reach her tonight:** the remote tunables. `RemoteTunablesService` pins
`https://defenders-of-the-realm-v2.vercel.app` as a `const` (`:93`) regardless of serving host, and a
byte grep of the **shipped 352005 payload** finds `pi.disableRemoteStructureArt`, `client-tunables`
and `tunables.cache.v1` (§3 row 7). **So the flag works on the build she already has.** That is why
the flag was armed rather than held for the deploy — it is the only lever tonight that can actually
touch her device.

**Armed and verified:**

```
STEP_9_OK marker=TUNABLES_SET_OK log=D:\eoa\Builds\client-tunables.log     (fresh, 21:56:59)

GET https://defenders-of-the-realm-v2.vercel.app/api/client-tunables   <- the host the CLIENT pins
  {"ok":true,"version":1,"readOk":true,"reason":"OK","values":{"pi.disableRemoteStructureArt":"1"}}

GET https://echoes-of-elarion.vercel.app/api/client-tunables           <- the host the GAME is served from
  {"ok":true,"version":1,"readOk":false,"reason":"NO_SQL_HANDLE","values":{}}    (expected; see the Lane A addendum)
```

⚠ **One caveat on the flag reaching a crash-looping client, stated so a null result is not
over-read.** Knob 3 is consumed at boot from the PlayerPrefs cache
(`RemoteTunablesService` reads it at `BeforeSceneLoad`; the network payload is fetched at
`AfterSceneLoad` and written with `PlayerPrefs.Save()`, which on WebGL flushes to IndexedDB). A page
that dies **1.35 s after its first frame** may not survive long enough to persist the cache. It only
has to win **once** across a 40-boot loop, and it then applies to every launch after — but if the
morning trace shows no `[Flow:Tunables] CONFIG (payload accepted…)` line anywhere, **the knob never
armed on the device and the run proves nothing about streaming.** Check for that line before reading
the result either way.

### 13. THE SINGLE NEXT ACTION FOR THE OWNER

> ## Deploy `echoes-of-elarion`.
> Everything else this pass produced is already in place and waiting on it. That host is still serving
> `2026.09.02.352005` from 00:04Z; the build with the heartbeat is built, promoted and proven — on the
> other project. One deploy of the right project puts the instrument on the phone, and the very next
> crash writes the line that names the root.

After that, the §10 morning test costs twelve minutes of standing still.
