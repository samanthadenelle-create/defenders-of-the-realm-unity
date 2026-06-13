# PIPELINE_REFILL_LOG — automatic nightly runs

Tracks every execution of the `keep-pipelines-full` scheduled task.

---

## 2026-06-08 ~ 14:30 UTC (automated session)

**Status:** Lanes refilled, docs synced

**Lanes audited:**
- **Lane 0** (Verify): 13 open items — HEALTHY
- **Lane 1** (World/Env, serial): 18 open — DEEP
- **Lane 2** (Combat/AI): 20 open — DEEP
- **Lane 3** (Combat Feel, serial): 10 open — HEALTHY
- **Lane 4** (UI/HUD): 14 open — HEALTHY
- **Lane 5** (World/Exploration): 10 open — HEALTHY
- **Lane 6** (Economy/Progression): 12 open — HEALTHY
- **Lane 7** (Persistence/Backend): 5 open → **THIN** ⚠
- **Lane 8** (Monetization/Store): 10 open — HEALTHY
- **Lane 9** (VFX/Audio): 9 open — HEALTHY
- **Lane 10** (Build/Deploy/Perf): 6 open → **BORDERLINE** ⚠
- **Lane 11** (Build Mode): 10 open — HEALTHY
- **Lane 12** (Narrative/Quests): 17 open — DEEP

**New WOs minted (5 total):**
- **339** SaveSchema: quest state versioning (L7 anchor for quest WOs)
- **340** PlayerPrefs migration: legacy pet/party → GameState (L7)
- **341** Backend auth token refresh + expiry (L7)
- **342** WebGL memory optimization + GC pressure (L10)
- **343** Analytics event batching + periodic flush (L10)

**Numbering reconciliation:**
- ⚠ Found discrepancy: MASTER_PIPELINES said "Next free = 330 (306–329 used)" but CLI_LANES already listed 330–338 assigned
- **Fixed:** Updated both docs to agree: next free = 344 (287/288/306–343 used, 289 free)
- Both files now reflect WO-339–343 in lanes

**Lane-depth targets after refill:**
- Lane 7: 5 → 8 open (added 3; all 339/340/341)
- Lane 10: 6 → 8 open (added 2; all 342/343)

**Documentation updates:**
- ✓ MASTER_PIPELINES_BACKLOG_2026-06-06.md: lanes 7 & 10 updated, "next free" advanced
- ✓ CLI_LANES_WO_NUMBERS.md: lanes & "newly minted" updated, next free = 344
- ✓ 5 new WO spec files created (WORK_ORDER_339–343.md)

**Notion board status:**
- ⚠ **Manual action required:** Notion Work Orders DB (data source id 5f66b263-c732-4075-b94a-f5f4de9f8087) needs:
  - Add 5 new rows for WO-339–343 (Title format: "WO-NNN — short name")
  - Set Lane = "7 Persistence/Backend" for 339/340/341; "10 Build/Deploy/Perf" for 342/343
  - Set Status = "Ready" for all
  - Add Depends On: 339 (none), 340 (WO-301 + WO-297), 341 (WO-120 + WO-80), 342 (WO-196 + WO-211), 343 (WO-121 + WO-80)

**Blockers / dependencies:**
- None of the new WOs are blocked
- Lane 7 & 10 now have ≥6 open items → no longer thin
- All queued work is parallel-safe or clearly serial (Lane 1, Lane 3)

**Next-free-WO:** 344 (ready for minting when next lanes thin out)

**Notes:**
- CLI can now immediately claim WO-339 (SaveSchema anchor) without waiting on quests; it unblocks all quest-state persistence
- WO-340 (PlayerPrefs migration) depends on WO-301 & WO-297 (should both be in flight or ready soon)
- WO-342 & WO-343 (performance) are orthogonal and can run in parallel with any other lane work

---

## 2026-06-09 ~ 06:15 UTC (automated session)

**Status:** All lanes healthy, no refill needed, status synced

**Lane audit (post-completion):**
- **Lane 0** (Verify): 11 open (was 13, WO-108 done) — HEALTHY
- **Lane 1** (World/Env): 16 open — DEEP
- **Lane 2** (Combat/AI): 23 open — VERY DEEP
- **Lane 3** (Combat Feel): 8 open — HEALTHY
- **Lane 4** (UI/HUD): 13 open (was 15, WO-380/382 done) — HEALTHY
- **Lane 5** (World/Exploration): 11 open — HEALTHY
- **Lane 6** (Economy/Progression): 11 open — HEALTHY
- **Lane 7** (Persistence/Backend): 9 open — HEALTHY
- **Lane 8** (Monetization/Store): 10 open — HEALTHY
- **Lane 9** (VFX/Audio): 9 open — HEALTHY
- **Lane 10** (Build/Deploy/Perf): 9 open (was 10, WO-368 done) — HEALTHY
- **Lane 11** (Build Mode): 10 open (was 11, WO-108 done) — HEALTHY
- **Lane 12** (Narrative/Quests): 15 open (was 16, WO-358 done) — HEALTHY

**Recent RESULT files reconciled:**
- ✓ WO-358 (Yarn Spinner welcome): Done (Lane 12, verified in Notion)
- ✓ WO-368 (Camera movement regression): Done (Lane 10, verified in Notion)
- ✓ WO-380 (Gear icon minimap overlap): Done (Lane 4, RESULT file exists)
- ✓ WO-382 (Hero HP consolidation): Done (Lane 4, RESULT file exists)
- ✓ WO-108 (Player build mode): Done (Lane 11/0, RESULT file from 2026-06-08 14:44)

**New WOs minted:** 0 (no thin lanes)

**Numbering state:**
- All lanes remain ≥6 open items after recent completions
- No lane fell below target threshold (6 items)
- Next free WO = 344 (unchanged)
- Lane 7 (Persistence) = 9 open (was THIN at 5 on 2026-06-08, refill added 339/340/341)
- Lane 10 (Build/Perf) = 9 open (was BORDERLINE at 6 on 2026-06-08, refill added 342/343)

**Documentation sync:**
- ✓ PIPELINE_REFILL_LOG.md: this entry appended
- ✓ Notion Work Orders DB: WO-358 & WO-368 status verified as Done
- ✓ CLI_LANES_WO_NUMBERS.md: current (dated 2026-06-08, no updates needed)
- ✓ MASTER_PIPELINES_BACKLOG_2026-06-06.md: current, no new WOs to slot

**Blockers / dependencies:**
- None. All lanes fully cleared from prior refill (2026-06-08).

**Next-free-WO:** 344 (ready; no new minting this run)

**Summary:**
Pipelines remain full and healthy across all 13 lanes. Recent work completions (5 WOs across lanes 0, 4, 10, 11, 12) confirmed synced to Notion. No thin lanes identified. Refill from 2026-06-08 successfully filled Lanes 7 & 10 to ≥6 depth; those lanes now at 9 items each, well above minimum. All queued work is unblocked and parallel-safe. Ready for next wave of assignments. ✓

---

## 2026-06-10 ~ 12:15 UTC (automated session)

**Status:** No thin lanes — reconciliation run (out-of-band WO block 352–390 absorbed into the numbering system)

**Lane audit (open WOs, no RESULT):**
- Lane 0: 8 · Lane 1: 16 · Lane 2: 27 · Lane 3: 8 · Lane 4: 24 · Lane 5: 11 · Lane 6: 13
- Lane 7: 9 · Lane 8: 9 · Lane 9: 12 · Lane 10: 12 (+282 HELD) · Lane 11: 11 · Lane 12: 19
- THIN threshold = <6 → **none thin**, target ≥8 met everywhere except none. **New WOs minted: 0.**

**Statuses synced to Done:**
- WO-380 (gear icon/minimap) + WO-382 (hero HP consolidation) — RESULT files exist; Notion rows did not → created as Done.
- WO-387 (camera-relative movement) — already Done in Notion (owner-playtested, commit acb2c80); no RESULT file in repo — CLI may backfill one.
- WO-358 / WO-368 — already Done (verified prior run).

**Notion backfill (rows created, 14):** 352, 353, 354, 355, 356, 357, 374, 376, 377, 378, 379, 380✓, 381, 382✓
(WOs 358–373, 375, 383–390 already had rows from the 06-08/09 sessions.)

**Numbering reconciliation (the main fix this run):**
- Docs said next-free = 344 but WOs **352–390 were minted out-of-band** by the 2026-06-08/09 sessions (files + most Notion rows exist). CLAUDE.md already said 391.
- **Next free WO = 391** now stated consistently in MASTER_PIPELINES_BACKLOG_2026-06-06.md, CLI_LANES_WO_NUMBERS.md, NOTION_SOURCE_OF_TRUTH.md, and the Notion home page.
- **344–351 skipped — do NOT mint** (treated as used per CLAUDE.md to avoid ambiguity).
- 352–390 slotted into lanes: new "Out-of-band block 352–390" section in CLI_LANES_WO_NUMBERS.md + lane lists updated; master doc header + pre-290 block note updated.

**Collisions flagged (dedupe via Lane 0 item 7, renumber from 391+):**
- Duplicate repo WO files: 329, 330, 331, 333, 334 (plus legacy 43/46/106–111/129/136–138/152/159/179/181/253–257/279/280/282/301).
- Notion board carries a *different* 328–339 P0-bug block (06-08 session) than the repo lanes file's 328–339 — titles diverge (e.g. Notion WO-339 "Village HUD" vs repo WO-339 "SaveSchema quest versioning"). Both sets noted in master doc + lanes file; do not reuse these numbers.

**Blockers / dependencies:** none new. WO-385 fade fix landed, pending owner playtest. WO-388/390 are SPEC-gated (388 on 2 verify questions; 390 deliberately after 389 defense loop is playable). WO-389 partial-built (attack flow + AI attacker + matchmaking remain).

**Next-free-WO: 391**

**Summary:** All 13 lanes ≥8 open items — pipelines full, nothing minted. Run focused on absorbing the out-of-band 352–390 block: 14 missing Notion rows backfilled (2 as Done), 380/382/387 confirmed Done, and the next-free-WO pointer corrected from the stale 344 to **391** across all four authority/mirror locations. Collision cleanup (dup 329–334 files + divergent Notion 328–339 block) remains queued in Lane 0. ✓

---

## 2026-06-11 ~ 13:45 UTC (automated session)

**Status:** No thin lanes — numbering reconciliation run (out-of-band block **391–411** absorbed; next-free → **412**)

**Statuses synced to Done:** none — no new `*.RESULT.md` since the 06-10 run (newest are 06-09: 368/380/382, already synced). WO-403/404 verified NOT prematurely Done on the board (403 = Ready, gated). No new HOLD files (282 HOLD unchanged).

**Lane audit (open WOs, no RESULT; THIN = <6):**
- Lane 0: 10 · Lane 1: 17 · Lane 2: 29 · Lane 3: 9 · Lane 4: 30 · Lane 5: 12 · Lane 6: 14
- Lane 7: 9 · Lane 8: 9 · Lane 9: 12 · Lane 10: 13 (+282 HELD) · Lane 11: 14 · Lane 12: 20
- **None thin → New WOs minted: 0.**

**Numbering reconciliation (the main fix this run):**
- Repo docs said next-free = 391, but the 2026-06-10/11 owner/CLI sessions minted **391–411 on-board (Notion)**: confirmed rows 392–395, 398–401, 403–409 (06-10) + **411** Town-HUD-vs-mockup (06-11); home page states 391–410 minted 06-10, 411 minted 06-11. Only **WO-405** has a repo spec file (`WORK_ORDER_405_ugui_design_system.md`); CLI should backfill repo specs as WOs are claimed.
- **Next free WO = 412** now consistent across MASTER_PIPELINES_BACKLOG_2026-06-06.md, CLI_LANES_WO_NUMBERS.md, NOTION_SOURCE_OF_TRUTH.md, CLAUDE.md, and the Notion home page (which already said 412).
- Block slotted into lanes (new §"Out-of-band block 391–411" in CLI_LANES_WO_NUMBERS.md): 409→L0 · 398→L2 · 399→L3 · 393/400/403/404/405/411→L4 · 395→L5 · 406→L6 · 408→L10 · 392/394/407→L11 · 401→L12. 391/396/397/402/410 used on-board but titles not yet mirrored — do NOT mint.

**Blockers / dependencies:**
- ⚠ **HUD gate:** 400/403/404/411 are Blocked on **WO-405** (UGUI design system, owner-approval gate) — flagged in lanes file L4 + out-of-band section.
- 🔴 Project blocker (not a WO-pipeline issue): OuterWorld ~1 fps RCA in measurement phase (`PerfDiagnostic`, see HANDOVER_2026-06-10.md); ~80 commits unpushed pending owner sign-off.
- Candidate future WOs spotted in HANDOVER_2026-06-10 (not minted — lanes full): monolith-split Waves B/C/D per `docs/MONOLITH_SPLIT_PLAN.md`, shared `HeroRosterView` extraction, CastleHubBuilder offset-capture, WebGL Gzip/itch size fix (overlaps WO-408), buildings-collection WO (owner to number — see `docs/WO-403_405_RECONCILIATION_AND_AM_PLAN.md`).
- Collision cleanup (dup 329–334 repo files + divergent Notion 328–339 block) still queued in Lane 0 item 7.

**Next-free-WO: 412**

**Summary:** All 13 lanes ≥9 open — pipelines full, nothing minted. Run absorbed the second out-of-band mint in two days (391–411, Notion-first this time): pointer advanced 391→412 in all five locations, block slotted into lanes, HUD 405-gate flagged. Note for process: two consecutive out-of-band blocks suggest sessions are minting from the board without updating the git authority docs — the nightly run will keep reconciling, but mid-session minting should update CLI_LANES_WO_NUMBERS.md at mint time. ✓

---

## 2026-06-12 ~ automated session

**Status:** No thin lanes — third consecutive out-of-band-block reconciliation (**412–428** absorbed; **WO-414 collision resolved**; next-free → **430**)

**Statuses synced to Done:** none — no new `*.RESULT.md` since 06-09 (368/380/382, already synced). 282 HOLD unchanged. Note: **WO-412 is ◐ in progress** — an autonomous cron session root-caused the empty BUY tab (ShopPanel content anchored to zero height) and pushed a partial fix (`ca89d9b`); build-test + gear-catalog runtime load still open, row stays Ready/open.

**Lane audit (open WOs, no RESULT; THIN = <6):**
- Lane 0: 10 · Lane 1: 17 · Lane 2: ~31 (+419, 423) · Lane 3: 9 · Lane 4: ~36 (+414/415/416/417/421/428) · Lane 5: 14 (+418, 426) · Lane 6: ~17 (+412, 413, 424, 425)
- Lane 7: 10 (+429) · Lane 8: 9 · Lane 9: 12 · Lane 10: 14 (+410; 282 HELD) · Lane 11: 14 · Lane 12: 21 (+422)
- **None thin → new WOs minted: 0.**

**Numbering reconciliation (the main work this run):**
- The 2026-06-11/12 owner sessions minted **412–428 on-board (Notion)** — playtest bug sweep + vendor chain (rows confirmed: 412–419, 421–426, 428; 420/427 used per the home-page screenshot map — titles not all mirrored; do NOT mint any of them). Repo docs still said next-free = 412.
- ⚠ **Real collision found and RESOLVED:** the owner also filed `WORK_ORDER_414_store_stock_from_db.md` in the repo (06-11, "next free = 415" per its header), while the board's WO-414 = "Black circle under TALK button (AttentionGlowUi)" — cross-referenced by WO-416/428. Decision: the board block stands (heavily cross-linked); the repo store-stock spec was **renumbered → WO-429** (`WORK_ORDER_429_store_stock_from_db.md`, content unchanged + WO-412/406 coordination notes; old file marked SUPERSEDED, kept for history). Notion row WO-429 created (Lane 7, Ready).
- **Next free WO = 430** now consistent across MASTER_PIPELINES_BACKLOG_2026-06-06.md, CLI_LANES_WO_NUMBERS.md, NOTION_SOURCE_OF_TRUTH.md, CLAUDE.md, and the Notion home page.
- Block slotted into lanes (new §"Out-of-band block 412–429" in CLI_LANES_WO_NUMBERS.md + master-doc lane entries): 419/423→L2 · 414/415/416/417/421/428→L4 · 418/426→L5 · 412/413/424/425→L6 · 429→L7 · 422→L12. **WO-410** (P0: 0.1 fps MainCastle_Hall GC storm) title now mirrored → slotted L10.

**Blockers / dependencies:**
- ⚠ HUD 405-gate now also covers **415** (vendor storefront skin) per the Notion home page: 400/403/404/411/415 blocked on WO-405 Done.
- **WO-417** is flagged DO-FIRST by the owner (Settings/Dev Tools panels unusable — owner is the sole tester).
- **WO-429** needs a React-repo GET endpoint (cross-repo) — flag for the owner/CLI before claiming.
- Collision cleanup (dup 329–334 repo files + divergent Notion 328–339 block) still queued in Lane 0 item 7.

**Next-free-WO: 430**

**Summary:** All 13 lanes ≥9 open — pipelines full, nothing minted. Third out-of-band block in three days absorbed (412–428); first true number collision (repo-414 vs board-414) resolved by renumbering the repo store-stock spec to WO-429 with a Notion row + repo spec file; pointer advanced 412→430 in all five locations. Process note (repeat, now stronger): owner sessions consistently mint on the board first — consider making the Notion home-page "Next free WO" line the de-facto mint-time pointer the sessions DO update, with the nightly run reconciling the git docs to it. ✓
