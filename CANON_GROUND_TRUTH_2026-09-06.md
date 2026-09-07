# CANON GROUND TRUTH - 2026-09-06 (the Sunday sweep anchor)

**This supersedes `CANON_GROUND_TRUTH_2026-09-03.md`.** Keep exactly ONE current; supersede by date
(CLAUDE.md section 15). Every session and every agent checks docs against THIS file.

> EVERY NUMBER ON THIS PAGE WAS READ AT SOURCE ON 2026-09-06, per CLAUDE.md section 11B. Each row
> names the command or the `file:line` it came from. Where a thing could not be proven from this
> repo, the page says "NOT PROVEN" instead of tidying it into a fact. Read the source, never the
> summary - including this page.

---

## 1. TREE AND BUILD

| Fact | Value | Where it was read |
|---|---|---|
| Branch | `feat/synty-art-retheme` | `git status -sb` |
| Unpushed commits | **103** | `git rev-list --count origin/feat/synty-art-retheme..HEAD` |
| HEAD | `815c628e9`, committed 2026-09-06 19:55:30 -0500 | `git rev-parse --short HEAD`, `git log -1 --format=%ci` |
| `bundleVersion` | `2026.09.07.358574` | `ProjectSettings/ProjectSettings.asset:148` |
| `AndroidBundleVersionCode` | `358574` | `ProjectSettings/ProjectSettings.asset:177` |
| Live PUBLIC store release | `2026.08.17.328845` | `publishing/SUBMIT_CHECKLIST.md:41`, read at source today |
| Save schema | `SaveSchema.CurrentVersion` = **41** (v41 = WO-823 Phase E `everCompletedRaid`) | `Assets/_Modules/Core/State/SaveSchema.cs:41` |
| Assembly definitions under `_Modules` | **25** | `find Assets/_Modules -name '*.asmdef' \| wc -l` |
| Working tree | DIRTY - regression `.cs` and `api/` edits in flight; the lead commits | `git status -sb` |

**The tester build `2026.09.07.358574` on her Seeker: NOT PROVEN from this repo.** What is proven is
the `bundleVersion` above. No `adb dumpsys` output and no device log carrying `358574` was found on
disk this session. The install is the lead's report, exactly as the 09-03 anchor recorded its own.

**The public store build is twenty days and hundreds of commits behind that.** `2026.08.17.328845` is
what a player can install today; it is the number every growth statement has to start from.

---

## 2. THE FOUR GATE MARKERS - read off MARKERS on the newest logs, never an exit code

| Marker (verbatim) | Log | Log mtime |
|---|---|---|
| `COMPILE_GATE_OK :: scripts compiled clean` | `Builds/cg-lanes2.log` | 2026-09-06 19:58 |
| `REGRESSION_OK 414/414 suites -- 414 green, 0 red, 0 skipped` | `Builds/reg-final2.log` | 2026-09-06 18:50 |
| `UI_CAPTURE_OK 91` | `Builds/ui-capture.log` | 2026-09-05 07:51 |
| `R2_PARITY_OK targets=Android,StandaloneWindows64,WebGL objects=271` | `Builds/r2-parity.log` | 2026-09-06 19:20 |

`Builds/r2-parity.log` is UTF-16LE; a plain `grep` finds nothing in it. Decode it
(`iconv -f UTF-16LE`) before judging it absent.

**FRESHNESS, stated because a marker alone is not a pass.** HEAD is 19:55:30. Only `cg-lanes2.log`
(19:58) postdates HEAD. `reg-final2.log` (18:50) and `r2-parity.log` (19:20) predate it, and
`ui-capture.log` is from 2026-09-05 - a day and a half old. So the compile gate covers HEAD; the
regression, the UI capture and the R2 parity proof cover an earlier tree and would have to be re-run
before anything ships.

---

## 3. WORK-ORDER NUMBERING - and a live discrepancy the lead must settle

| Series | Banner says next free | Row |
|---|---|---|
| Main line | **1446** | `CLI_LANES_WO_NUMBERS.md:190` (ninety-fifth pass, 2026-09-06) |
| UI seat | **1084** | `CLI_LANES_WO_NUMBERS.md:2005`, dated 2026-08-26 - eleven days old, verify before minting |
| PROD | **PROD-023** | `CLI_LANES_WO_NUMBERS.md:3` |
| Manage redesign (2000 block) | **WO-2018** | `CLI_LANES_WO_NUMBERS.md:183` |

**DISCREPANCY, recorded not resolved:** `WorkOrders/` holds `WORK_ORDER_1446_...` through
`WORK_ORDER_1477_...` on disk, and `WORK_ORDER_1467_...md:6` states in its own header that it bumped
"main line 1467 -> 1468 in the same edit". The banner's top main-line row still reads 1446. That is
the CLAUDE.md section 2 shape exactly: mints written to disk without the banner bumped in the same
edit. **Do not mint from 1446.** The lead is minting live tonight and owns the reconciliation; this
page only names it. Collisions resolve first-on-disk-and-referenced-wins.

---

## 4. THE LIVE P0 LIST

Read from `docs/GET_WELL_PLAN_2026-09-06.md` section 1, in its stated order. That plan is the live
sequencing document; this list is a pointer, not a second copy.

1. **Do NOT deploy `api/` until `auth_sessions.signed_at` exists on live Neon.** The sweep says
   `MISSING ON LIVE DB: auth_sessions.signed_at` while `wallet-auth.js:315` INSERTs it on the normal
   mint path - deploying would 500 every wallet session. Owner runs `tools/run-schema-repair.mjs`;
   the uncommitted work waits at `WorkOrders/patches/wo1441-api-renewal-cap.UNCOMMITTED.patch`.
   Ticket WO-1446.
2. **Cloud LOAD restores seven currency fields, not the town** (`GameStateService.cs:2099-2145`), so
   a reinstall loses the base. Ticket WO-1447 (and WO-1448 for the scene-enter overwrite).
3. **`ADMIN_OPS_KEY` is unset on the deployment** - the command centre Fail is that key answering
   `OPS_WRITE_NOT_CONFIGURED`. One env var, owner. WO-1244 lane.
4. **`builders-hour` is unbuyable on both rails** - mirror it into `USD_ANCHORS` and
   `GooglePlayProductCatalog`, add the two node suites to the packs.json gate. Ticket WO-1449.
5. **The device log is unreadable and 260 BREAKs fired in 144 s** - throttle the EnemyAggro probe
   trace and drop its stack frames (WO-1450); fix the `TowerPreviewCamera` MSAA mismatch (WO-1451).
6. **Ship the passing tester build to the store as the UPDATE.** Owner supplies four screenshots and
   the 512 icon and approves the release notes; the CLI executes `publishing/SUBMIT_CHECKLIST.md` to
   the end, as written. Checklist is unticked from `:186` onward. WO to mint on the owner's word.

---

## 5. THE TWO RCA DOCUMENTS THIS ANCHOR SITS ON

- `docs/READY_RCA_2026-09-06.md` - the root causes behind the READY backlog, from the read-only audit
  fleet. Every claim carries its measuring line.
- `docs/GROWTH_RCA_2026-09-06.md` - why nobody is arriving: a twenty-day-old public build, no
  analytics on the landing project, a `solanadappstore://` deep link as the only call to action, and
  a name that loses its own search results.
- `docs/GET_WELL_PLAN_2026-09-06.md` - the plan that sequences both. LIVE.

---

## 6. STATE - read every number from its source, never from this page

| Fact | Authority |
|---|---|
| Branch, HEAD, unpushed count | `git status -sb`, `git rev-list --count origin/<branch>..HEAD` |
| Save schema version | `SaveSchema.CurrentVersion` (`Assets/_Modules/Core/State/SaveSchema.cs`) |
| Suite count | the `REGRESSION_OK` marker on a fresh log |
| Assembly count and dependencies | `find Assets/_Modules -name '*.asmdef'` and the files themselves |
| Action bar faces | `HudActionBarModel.MaxVisibleFaces` and the live dock - see WO-1467 |
| Next free WO number | the `CLI_LANES_WO_NUMBERS.md` banner rows, plus a look at `WorkOrders/` |
| Board / ticket status | `BOARD.html`, regenerated by `python tools/board_build.py` |
| Home hub scene | `SceneRouter.CastleCandidates` - flag-dependent on `FeatureFlags.MergedWorld` |
| Gate results | the MARKER on a FRESH log, checked against HEAD's commit time |

**Carried forward from the 09-03 and 09-02 anchors rather than restated:** the Android APK is the
priority lane and Pi/WebGL is parked (owner, 2026-09-02); the pay path IS activated (owner,
2026-08-23) so an economy removal is no longer a clean purge; the 180s ceiling stays on wallet
signing (owner, 2026-09-04); the signing certificate still cannot be proven to match the live release
and the cheap close is an in-place update on a device. Read those pages for the detail.

---

## 7. THE LESSON OF THE SWEEP

Every stale line fixed today was a **copied number**: a save schema stated as 38 while the const read
41, an assembly count stated as 19 while `find` returned 25, five documents calling the branch
"pushed" while 103 commits sat unpushed, and a numbering banner three dozen mints behind its own
`WorkOrders/` directory. The cure has never been a better copy. It is deleting the copy and pointing
at the thing that knows.
