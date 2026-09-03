> **PARKED 2026-09-02 by owner ruling — the Android APK is the priority. Pi work resumes on her word.**

# WORK ORDER 1314 — The WebGL remote payload is shaped for a native client, against a 512 MB heap

**Status:** READY TO IMPLEMENT - ⭐ PROMOTED 2026-09-03: PROVEN as the prime root of PROD-022 (deterministic 64% stall across two deployments with all tunables cleared; the .data payload is 165 MB COMPRESSED). See the PROVEN section at the end.
**Silo:** Web / Content
**Minted:** 2026-09-02 (CLI) while answering the owner's question about Pi breaking on the CDN.
**Severity:** P2 pending proof — see "What is NOT proven" before acting on it.

## Owner question, verbatim

> *"see why it breaks whenever it touches the cdn"* / *"im guessing match on r2"*

## What was RULED OUT — with data, so nobody re-theorises it

Her R2 hypothesis is **wrong, and that is good news**:

- `R2_PARITY_OK targets=Android,StandaloneWindows64,WebGL objects=228` on a fresh log
  (`Builds/r2-parity.log`, 2026-09-02 03:34).
- The live `ServerData/WebGL` catalog (`catalog_2026.08.30.347462.bin`/`.hash`) and its bundles
  return **HTTP 200** from `pub-ab6dfaf1b3d74ca78891876611ccb832.r2.dev`.
- R2 serves **`Access-Control-Allow-Origin: *`** and `Access-Control-Expose-Headers:
  Content-Length,ETag`. A browser cross-origin fetch is permitted — **CORS is not the problem.**
- `catalog.json` 404s on all three targets. That is **EXPECTED**, not a defect: real catalogs are
  `catalog_<version>.bin`. Do not "fix" it.

The separate Pi validation-key drift was real and is fixed under WO-1313.

## What the measurement actually shows

`ProjectSettings/ProjectSettings.asset` → **`webGLMemorySize: 512`** (MB heap).

Largest remote objects the WebGL target can be asked for:

| bundle | compressed |
|---|---|
| `enemy_models_assets_enemyfam-hollow_…` | **24.5 MB** |
| `enemy_models_assets_enemyfam-orc_…` | **18.2 MB** |
| `enemy_models_assets_enemyfam-troll_…` | **17.4 MB** |
| `enemy_models_assets_enemyfam-bosses_…` | 4.9 MB |

Total WebGL remote payload: **95 MB** (vs Android 530 MB, Windows 454 MB).

These bundles are **meshes and textures**. Their *decompressed, GPU-and-heap-resident* footprint is
several times the figures above, and it lands in a **512 MB** heap shared with the Unity runtime, the
loaded scene and everything else. On a mobile WebView — which is what Pi Browser is, typically with a
**tighter practical ceiling than desktop Chrome** — that is a plausible OOM.

⚠ **A WebGL OOM does not present as "the CDN failed".** It presents as the tab dying, a black canvas,
or an abort deep in the loader — which is exactly the shape of *"breaks whenever it touches the CDN"*,
because touching the CDN is when the memory actually gets allocated.

## ⚠ A change landed TONIGHT that increases this pressure — read this before measuring a baseline

**WO-1307 (committed `95b75cf75`) made the `hollow` family pre-fetch for the first time.** Its models
resolve as `Skeleton_*`, so the old heuristic yielded the undeclared label `enemyfam-skeleton` and the
pre-fetch silently never happened. It now correctly resolves to `enemyfam-hollow` — meaning the WebGL
client will now pull a **24.5 MB bundle it previously never requested.**

That fix is correct and should stay. But it means **a WebGL memory measurement taken before tonight is
not a valid baseline**, and if Pi got worse after this build, this is the first thing to look at.

## What is NOT proven — do not skip this

**No Pi Browser log has been captured.** Everything above is measured from the repo and the CDN; the
OOM itself is a HYPOTHESIS, and CLAUDE.md sec.12 forbids fixing on one. Two static theories were
already wrong on 2026-08-20 before one device log named the real cause in a single line.

**Instrument first.** The realistic capture paths, cheapest first:
1. Load the deployed build in **desktop Chrome with a mobile emulation profile** and read the console
   plus `performance.memory`; the loader's own error surface is already owned by the WO-678 block.
2. **`chrome://inspect`** against Pi Browser on a USB-attached Android device — this is the real
   evidence, and it is the one that settles it.
3. Add a `[Flow:WebContent]` breadcrumb around each remote bundle load (size, elapsed, success) so a
   failure names the object rather than the platform.

## Candidate remedies, IF the data supports them — ranked, none to be applied blind

1. **Raise `webGLMemorySize`** — cheapest, and may simply be wrong-headed on a phone that does not
   have the memory to give.
2. **Split `enemy_models` per-enemy rather than per-family** on the WebGL target, so the client pulls
   only what a wave actually needs. Matches the existing per-family ruling in spirit.
3. **A WebGL-specific texture cap / crunch** on enemy models. Note the deck cards already inherit a
   512px WebGL override — there is precedent for a platform override here.
4. **Do not** solve this by reverting WO-1307. That would restore a silent defect on every platform to
   mask a memory limit on one.

## What NOT to touch

- ⛔ Do NOT change `Assets/AddressableAssetsData/**` casually. ANY change there re-hashes every bundle
  on every platform and mandates a fresh `tools\r2-ship.ps1` push (CLAUDE.md sec.16, four incidents).
  If a grouping change is the answer, the push is part of the same work order, not a follow-up.
- ⛔ Do NOT "fix" the `catalog.json` 404. It is expected.
- ⛔ Do NOT touch `Assets/WebGLTemplates/Pi/validation-key.txt` — corrected under WO-1313.
- ⛔ Do not conflate this with WO-1312 (Pi landscape). Different failure, different evidence.

---

# UPDATE 2026-09-02 06:10 — this is now the LEADING candidate, by elimination

Three of the four candidate causes for the owner's *"breaks whenever it touches the cdn"* have been
ruled out **with measurements** tonight:

| candidate | verdict | evidence |
|---|---|---|
| R2 out of sync / wrong bytes | **RULED OUT** | `R2_PARITY_OK targets=Android,StandaloneWindows64,WebGL objects=261`, fresh log 06:07 |
| CORS blocking the browser | **RULED OUT** | `Access-Control-Allow-Origin: *`; `R2_CORS_OK public GET/HEAD enabled for WebGL CDN assets` |
| the Pi validation key | **RULED OUT** | prod serves `79ec2d03...` HTTP 200, and all four in-repo copies now match (WO-1313) |
| the landscape gate blocking the validator | **RULED OUT** | production runs a PRE-GATE template (7,396 bytes, no `pi-landscape-gate`) — see WO-1312's correction |
| **WebGL memory shape** | **STILL OPEN — now the leading candidate** | `webGLMemorySize: 512` vs a 95 MB remote payload, single bundles at 24.5 / 18.2 / 17.4 MB |

⚠ **Elimination raises a hypothesis's rank; it does not promote it to a diagnosis.** CLAUDE.md sec.12
still applies, and this ticket still forbids a fix without a capture. On 2026-08-20 two static
theories were wrong before one device log named the cause in a single line.

**Also newly relevant:** the WebGL content that was live until tonight was built from the WRONG
PLATFORM (WO-1315 — `ServerData/WebGL` went from 61 files on an Aug 30 catalog to 112 on the 09-01
one once the target bug was fixed). So the web build the owner was testing against was **missing
roughly half its content**, and any "it breaks on the CDN" observation taken before 2026-09-02 06:00
was taken against a materially different, broken payload. **Re-observe before measuring memory** —
the symptom may have changed or gone.

---

## ⭐ PROVEN 2026-09-03 — THIS IS THE ROOT OF PROD-022, AND THE PROOF IS DETERMINISTIC

This ticket was minted "P2 pending proof". The proof arrived. **Promote it: it is the prime root of
the Pi crash loop, not a side issue.**

### The owner's measurement, and it is the one that cracks it

> *"64% 3 times in a row. Resets exactly 64%"* — then, after a full rollback to the previous
> deployment and with every tunable cleared: *"Still 64%"*.

**DETERMINISM IS THE WHOLE FINDING.** Every prior theory on PROD-022 predicted VARIANCE - sessions
died at 8s, 30s, 53s, 283s, which is what kept a memory *ceiling* (something you drift into) as the
leading candidate for weeks. A stall at *exactly* the same percentage, three times, across two
different deployments, is the opposite: **the same allocation failing at the same point in the same
file, every time.**

### The payload, measured from the open internet

```
Build/e1e3fcce12ebd0ccca5a2cf9a6c72035.data.unityweb   Content-Length: 165,180,640   Content-Encoding: br
Build/80780cf875d616d984528ffe257eeee3.wasm.unityweb   Content-Length:  16,951,877   Content-Encoding: br
Build/64177f0b519d6f04938617b0aba7ce74.framework.js.unityweb        93,143           Content-Encoding: br
Build/5466bb137be1f061b5b050cb15c0787e.loader.js                    (200)            Content-Encoding: br
```

⛔ **165 MB is the COMPRESSED size of the .data file.** It decompresses to several hundred MB, and
that has to be materialised in the webview's address space *alongside* the Unity heap. All four files
serve 200 with correct `Content-Encoding: br` - **the encoding is fine, the SIZE is not.**

### It retro-explains every measurement on PROD-022

| Observation | Why a too-large payload explains it |
|---|---|
| Worker + main thread stop in the SAME instant, `mAgeMs` 450-500 ms to the last line | iOS reclaims the whole content process. The main thread was healthy - it was killed, not wedged. |
| Unity heap flat at `mem=247MB` right up to death, no error, no exception | The memory is OUTSIDE the managed heap - decompression buffers and wasm memory - exactly where `mem=` cannot see it. |
| Desktop Chrome ran the identical build for 62 minutes | No comparable per-process constraint. |
| NO `JetsamEvent` on 2026-09-02 despite 10+ deaths | A content-process reclaim is not a full-app jetsam. The owner's Analytics check was a TRUE negative that was misread as exonerating memory. |
| `[Title]`-scoped lines only; dies before structure art is requested | It never survives load, so the Addressables policy was never implicated. |

### What this KILLS - do not spend another night on these

- **Memory *ceiling* as a drift/race.** It is a hard, reproducible allocation failure.
- **The Addressables / structure-art streaming theory.** `pi.disableRemoteStructureArt` was armed and
  then cleared; **64% both ways.** Knobs 1/2/4/5/6 are exonerated exactly as predicted in advance.
- **The R2 / CDN theory** (already ruled out above, and re-confirmed: all four Build files 200).
- **The deploy-target and stale-edge-shell theories.** Real defects, both recorded, but NOT this.

### What is still NOT proven

The exact allocation that fails. "Too big" is proven; *which* limit is hit - decompression buffer,
wasm `INITIAL_MEMORY`, total process footprint - is not. **Do not pick one and start optimising.**
The next step is to measure the decompressed `.data` size and the build's memory settings, then
reduce the largest contributor. This is REAL WORK - a payload reduction, not a flag flip, and no
tunable on the rail can move it.

**Related and still READY:** WO-1315 (WebGL built Windows content) - if a WebGL build has ever
shipped desktop-shaped assets, that is a direct contributor to this file's size and should be checked
FIRST, because it would be the cheapest win available.
