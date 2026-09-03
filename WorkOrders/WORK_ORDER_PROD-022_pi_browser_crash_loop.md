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
