# WORK ORDER 684 — Outstanding items board (post 07-12 evening wave)

**Status: TRIAGE BOARD** (owner directive 2026-07-12: "give me a work order of outstanding items
and we tag them after these currently running land"). Tag/route each after the in-flight lanes
land. Numbering: this consumes 684 → **next free = 685** (bumped in CLI_LANES_WO_NUMBERS.md).

## A. In flight RIGHT NOW (land + gate + verify first — next session's step 1)
1. **WO-683 d-pad probe fix — IN TREE, UNGATED.** RCA proved the PROBE was wrong, not the merge:
   `ProbeArmedGhostCell` read the GhostPreview HOST transform (never moves — ghost cell stuck at
   grid-centre (15,15), the WorldToCell(origin) constant) while `MoveTo` moves the CHILD visual
   (GhostPreview.cs:167). Fix: new `GhostPreview.CurrentPosition` (tracked visual) consumed by the
   probe (BuildModeController.cs). The reflection merge itself verified sound (type string matches
   HudMoveInput.cs:16; dead-zone cleared). NEXT: gate → build → fleet re-run for the DPAD PASS
   line (the run now carries discriminating `[Flow:Build]` d-pad Step/Warn lines either way).
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
0. **⭐ NEW ORACLE FINDING (DataWebRegression, ships RED correctly): live dual-copy DRIFT in 6
   canonical files — `weapons.json` is 256,029 B in StreamingAssets (~433 weapons) vs 19,093 B in
   Resources (~16 weapons), and RESOURCES WINS at runtime → the shipped game plays the tiny
   catalog.** Also drifted: armor.json, daily-quests.json, skin.json, stake-rewards.json,
   tower-perks.json. **OWNER RULING 2026-07-12: for gear the SMALL set is deliberate — "we don't
   really have anything decent to use yet" — so RESOURCES (curated, ~16 weapons) is truth and the
   433-weapon StreamingAssets copy is the stale side. Sync direction Resources → StreamingAssets
   for weapons/armor.** STEPS: copy Resources gear jsons over the StreamingAssets pair → rule the
   other four files' direction the same way (check which side is current per file) → re-run
   DataRegression → `DATAWEB_OK` flips green.
   **⚠ UPDATED 2026-07-12 (later): DO NOT delete/overwrite the 446-item StreamingAssets gear set.**
   Owner wants a Gear Imaging + Offset tool (in flight) listing all 446 with per-item
   include/exclude checkboxes + "NEEDS PNG" flags to CURATE the shipped set FROM them. Resolution =
   the curation overlay picks the included set → that becomes runtime truth; the 446 stay the
   source pool. DATAWEB gear drift stays flagged (expected) until curation lands — no auto-sync.
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

## D2. Security (from `docs/SECURITY_AUDIT_2026-07-12.md` — exact asks + steps)
- **S1 (HIGH):** Create the missing TTL cron. STEPS: add `api/admin/cleanup.js` (DELETE web_trace
  rows >7d + spent/expired auth_nonces) → add `crons` entry in vercel.json (daily) → redeploy →
  verify row counts fall in the db-viewer Overview tab.
- **S2 (HIGH):** Rate-limit the open POSTs. STEPS: Vercel WAF rules on /api/trace,
  /api/events/track, /api/bug-report (per-IP) → verify with a scripted burst.
- **S3 (HIGH, before ANY public build):** gate the HelpMenu 5-tap grant behind
  `#if DEVELOPMENT_BUILD || UNITY_EDITOR` (HelpMenu.cs:70-75,155-175,234-276) — it currently
  ships self-grant of 25k crystals in release.
- **S4 (MED):** nonce-gate promo/redeem + referral/claim + referral/generate with the existing
  `verifyAndConsume` (install-brag.js:89 is the working precedent).
- **S5:** confirm the Neon credential flagged at api/DEPLOY.md:11 was rotated. Note: audit proved
  `api/` is git-TRACKED (correct the 07-12 anchor's "gitignored" line).

## G. db-viewer activation (exact steps for the owner)
1. Vercel dashboard → project `defenders-of-the-realm-v2` → Settings → Environment Variables →
   add `ADMIN_DASH_KEY` = a long random string (Production, Sensitive).
2. Redeploy the backend (any `vercel deploy --yes` from C:\EOA ships `api/admin/db.js`; promotion
   to prod is the owner's).
3. Double-click `tools\db-viewer\index.html` → paste base URL + key → Save. Tabs: Overview
   (row counts), Players (latest saves; `player=<id>` for one full record), Metrics (7-day
   events/sessions/error-lines per day), Traces (per-session web_trace lines). Rotate the env var
   to revoke access.

## R. Regression suite — status + what's left (owner focus 2026-07-12)
**DONE this session (wired into DataRegression.RunAll, compile-gated):** SFX_WEBGL (green),
CORESAVE (Core/Save SME), BUILDECON (BuildMode/Economy SME), DATAWEB (Data/Web SME), HUDUI
(HUD/UI SME incl. the tofu-glyph oracle). Baseline run = **7 failures, ALL truthful** (no false
positives) — the suite is now catching real defects the fleet couldn't.

**What the new oracles caught (route each — these are the "what's left" to make it GREEN):**
- **HUDUI: 47 real tofu sites** — genuine non-ASCII glyphs that render as □ on device. Worst:
  `TowerPlacementRotateMenu.cs` (32 — the rotate menu the owner photographed), TowerSwapMenu,
  SeatingEditorOverlay, InventoryPaperDoll, RaidDeployScreen, BuildingInteractable/CrystalMine.
  → ASCII-sweep pass (same fix as WO-683 Lane C, project-wide). BIG one.
- **DATAWEB: dual-copy drift** — armor/weapons/daily-quests/skin/stake-rewards/tower-perks (gear
  ruling = Resources truth, §B.0). Sync → green.
- **CORESAVE: 3 fail-by-design** — Tribes/Wards/Arena W-L not persisted (schema lane §E).
- **BUILDECON: 2** = the known B2 dual-wallet + pet-slot flag_17 (§E) — surfaced via the shared
  economy oracles, not new.

**Coverage GAPS still open (paths/classes NOT yet covered — the real "what's left"):**
1. **PlayMode paths none of the headless SMEs can reach** (documented in each SME's skip list):
   backend delta-sync, PersistenceBridge save-triggers, OnApplicationQuit ordering, live upgrade
   charge/ApplyTierStats on ticking components, full BaseLayoutLoader.Spawn (collider strip +
   NavMeshObstacle + under-construction re-arm), Obsidian panel pixel/color invariants. → needs a
   PlayMode test asmdef suite (WO candidate).
2. **Combat/ATB path** — no SME suite authored (5th architect path). → WO candidate.
3. **Dialogue/Yarn path** — no SME suite. → WO candidate.
4. **Audio mixer/routing** — SFX import covered; the 5-group mixer stub (catalog P1 #6) unasserted.
5. **Web/loader runtime** — DataWeb covers dual-copy + WebGL-omission statically; NO oracle proves
   a clip actually DECODES on WebGL (only a real browser/self-heal-bot run can). → ties to the
   web-bot loop (§ web self-heal, ~60% built).
6. **CI wiring** — every gate is still manual discipline; no automated pre-commit/PR gate runs
   REGRESSION_OK. → the single biggest coverage-leverage item.

## F. Hygiene
19. **Renumber `WORK_ORDER_677_asset_caster_toolkit_family.md`** → 685+ (banner rule).
20. **NOTION_SOURCE_OF_TRUTH.md** still says next-free 430 — refresh line.
21. **CLI_PREP_2026-07-08** pointer in SESSION_CANON_LOADER Key Files — supersede when a new prep
    doc exists (START_HERE.md now carries the role).
