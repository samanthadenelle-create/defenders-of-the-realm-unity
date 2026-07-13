# WORK ORDER 684 — Outstanding items board (post 07-12 evening wave)

**Status: TRIAGE BOARD** (owner directive 2026-07-12: "give me a work order of outstanding items
and we tag them after these currently running land"). Tag/route each after the in-flight lanes
land. Numbering: this consumes 684 → **next free = 685** (bumped in CLI_LANES_WO_NUMBERS.md).

## A. In flight RIGHT NOW (land + gate + verify first — next session's step 1)
1. **WO-683 d-pad merge fix** — fleet proved the reflection merge dead
   (`AssertBuildMoveChain: FAIL at link DPAD`); fix agent's edits may be in the tree UNGATED.
2. **VFX Caster tag-to-catalog** (Tag & Catalog block + VfxManualPicks.json overlay; generator
   merges manual-wins) — edits in tree, UNGATED (`VfxCasterWindow.cs`, `HovlVfxCatalogGenerator.cs`).
3. **4 regression SME suites** (CoreSave / BuildEconomy / HudUi incl. the tofu-glyph oracle /
   DataWeb dual-copy diff) — new files under `Assets/Editor/Regression/`, UNGATED, and NOT yet
   wired into `DataRegression.RunAll` (one wire-line each, orchestrator integrates).
4. **db-viewer tool system** — `api/admin/db.js` (key-gated read endpoint) + `tools/db-viewer/`
   (committed). Needs: owner sets `ADMIN_DASH_KEY` in Vercel env + backend redeploy.
5. **Security audit report** — read-only findings (agent was in flight at capacity handoff; check
   its output/transcript next session).
6. **WebGL ship preview** — chain restarted 19:55; URL lands in `Builds/webgl-chain-status.txt`
   (`DEPLOY_URL` line). Owner felt-pass closes WO-677/678/682/683.

## B. Demo-lethal (P0 — sequence BEFORE polish; PM audit 2026-07-12)
7. **WO-602 home-return unwired** — a judge who leaves the castle cannot come back. Session-ender.
8. **Encounter-return strand (~7km, WO-453 class)** — fleet reproduces every run.
9. **Full Pi-Browser traced felt-run** — one end-to-end session in the ACTUAL Pi Browser with
   `?trace=1`; generate the real punch list. No evidence this has ever been done.
10. **Mobile load/perf budget** — define "loads in X s on Y network, holds Z fps"; measure the
    129MB data ship against it; un-park WO-545 Addressables if it fails. OuterWorld ~1fps open
    blocker (catalog P1 #2) still root-unproven.
11. **Combat touch completeness** — d-pad fixed build mode; verify move+aim+Q/W/E/R+interact all
    reachable by thumb on device (the input audit found TryCast once had NO input surface).

## C. Quiet-failure hardening (extends WO-682)
12. **Loader-error beacon** — errors before Unity boots reach no telemetry; ~10-line template JS
    POST to `/api/trace` closes the last blind spot.
13. **Desktop release still ships BuildOptions.Development** (`DesktopBuild.cs:178`) — DevTools
    leak into the release exe.
14. **Preview SSO friction** — standing protection-bypass for device testing (or disable
    protection on previews); ends the share-link dance every deploy.

## D. Compliance / release gates (before any public/commercial ship)
15. **⛔ Apex dragon model CC BY-NC** — license or replace. Hard legal blocker.
16. **Privacy note** — analytics + web traces collect session data; Pi listing needs a statement
    (player-privacy is a standing owner rule).
17. **Trace/track endpoint flood exposure + web_trace TTL cron** — confirm the 7-day cleanup
    exists; rate-limit the open POSTs (security agent's report will detail).

## E. Economy integrity (schema lane, one-at-a-time)
18. **B2 dual-wallet divergence** (failing oracle) · **pet active-slot resets on reload**
    (flag_17) · **broken-tower state not persisted**. Three known "my stuff disappeared" bugs.

## F. Hygiene
19. **Renumber `WORK_ORDER_677_asset_caster_toolkit_family.md`** → 685+ (banner rule).
20. **NOTION_SOURCE_OF_TRUTH.md** still says next-free 430 — refresh line.
21. **CLI_PREP_2026-07-08** pointer in SESSION_CANON_LOADER Key Files — supersede when a new prep
    doc exists (START_HERE.md now carries the role).
