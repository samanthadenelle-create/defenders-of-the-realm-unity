# Backlog Reconciliation — 2026-06-27 (RCA close-down pass)

**Supersedes `BACKLOG_RECONCILIATION_2026-06-21.md`** (that pass is frozen/valid as of HEAD
`52c8b677`; this one re-verifies at HEAD `7ba968cb`/`2cac6c59` and advances the frontier).

**Owner directive (2026-06-27):** "RCA anything still open so we can get things closed and the
ticket count down — everything doesn't need pushed but all can be RCA'd and the backlog clear."

**Method:** 6 read-only RCA agents in parallel. 5 swept the live frontier (WO-430→468) + the
7 new-feature specs and verified each against HEAD code (code wins on truth — the file "Status:
READY TO IMPLEMENT" line is an unreliable template default). 1 re-confirmed the 2026-06-21 bulk
close-list still holds at HEAD. **Notion SQL bulk-query/update is plan-gated for this session's
tools** — this doc is the reconciled truth; the owner (PO) bulk-closes the board FROM it (§13: PO closes).

---

## A. BULK CLOSE-LIST — re-confirmed valid at HEAD (no regressions found)

**DTT / PatriciaLight — CLOSE deprecated (17):** 46, 47, 48, 96, 98, 99, 100, 209, 221, 317,
318, 319, 320, 330, 331, 332, 333(dtt-sensitivity variant ONLY).

**Dungeon — FUTURE WORK (7):** 23, 29, 30, 65, 165, 250, 324. *(19, 59 already done.)*

**Done-in-code — CLOSE (code confirms at HEAD):** 290, 304, 339, 293, 295, 297, 298, 299, 108,
215, 282, 314, 131, 228, 424, 258, 276, 430(ui_mvvm_seam), 431(mvvm-shop variant), 434, 450, 358,
368, 380, 382, 387, 465.

**Stale / dead — CLOSE:** 101, 102, 103, 105, 126, 158, 167, 168, 177, 179, 183, 188, 247, 253,
256, 263, 278 (Village.unity-premised); 110, 307, 378, 411, 414, 301 (superseded); 291, 294
(Yarn specs, superseded by WO-455); 265, 328 (cancelled).

## B. NEW CLOSES this pass — frontier WOs done/superseded since 06-21 (8)

| WO | Verdict | Evidence |
|---|---|---|
| **437** input/battle-lock gate | CLOSE-DONE | `Core/Combat/BattleLock.cs` + `PanelManager.cs:112` gate + probes at ATB/Arena/BattleArena; hotkey polls removed. (residual: ESC centralization, optional S) |
| **438** base-loop RCA fixes | CLOSE-DONE | all 4 fixes in code w/ "WO-438 FIX" markers (Talk deregister, companion fallback, Party NRE guard, portrait warn) |
| **447** hero-select polish | CLOSE-DONE | superseded by WO-503 (`f0a10a3c`); stats+ability rows present, dragon stage stripped. (Obsidian skin residual → UiStyle phase-b) |
| **439** quest board collection | CLOSE-STALE | superseded by WO-454 kept tracker + the build-pipeline ban on Blink/UXML prefabs; code board already works |
| **448** hub↔OuterWorld seam | CLOSE-STALE | superseded by WO-467 `RuntimeRegionGate` (ff.runtimeworldseam ON) + broadened `GroundZFightFixer` hub path |
| **451** intro shorten | CLOSE-STALE | direction superseded by WO-446 v2 cold-open. *Salvage only:* comet removal (`TitleStarfield.BuildComets`, `WeatherManager._starIntervalMax=0`) — fold into 446 |
| **467** RegionGate primitive | CLOSE-DONE | runtime variant shipped: `region-gates.json` (4 gates) + `RuntimeRegionGate.cs` + PROBE 5 SEAM-REACHABLE (`f82e2b00`) |
| **437/438 tech-skin** (distinct files) | CLOSE-STALE | "Tech hud elements" pack gone/never committed; art pivoted to Blink Obsidian → UiStyle + 9-zone HUD |

## C. GENUINELY OPEN — EXISTING (actionable; RCA'd) (4)

- **WO-440 ATB wiring** — OPEN, **S–M, single file** `BattleATB/BattleHudUgui.cs`: (1) `OnItemClicked()` is an empty stub (`:431`); (2) no battle-log panel though the engine emits log events. Mirror the existing Skills submenu. *(ATB is a side-path; polish, not a blocker.)*
- **WO-454 unified quest system** — OPEN, **M**, Phase 1 shipped (`TrackedId`/`SetTracked` + tracker pin, `aaa15f91`); Phase 2/3 remain: add `QuestDef.Type` (`QuestCatalog.cs:51`) + board tab strip + daily tab + Type-routed pin. Watch `quests.json` Resources+StreamingAssets dual-copy.
- **WO-455 custom dialogue** — OPEN/in-flight, **L incremental**: engine 100% done (`Core/Dialogue/*`, flag OFF, 1 proof dialogue); remaining = content conversion (29 `.yarn` files), verb parity (~11 of ~40 in `DialogueCommandSink`), then rip Yarn from `manifest.json`. No defect — keep in-flight.
- **WO-468 seam redesign** — OPEN, **M**, Phase 1 shipped (`dec10543`: 1000u OuterWorld, south corridor, click-to-enter cave→Village2, `OuterWorldCavePortalBuilder.cs`); open: Phase 2 (un-stack + NavMeshLink + dual-navmesh kill), ranger appearance, SEAM-REACHABLE verify. **Instrument-first:** runtime-seam landing `~(-4.37,0.5,-66)` + 4-side gates (`f82e2b00`) may bypass the authored south corridor — capture `[Flow:RuntimeSeam]` + SEAM-REACHABLE before editing.

## D. NEW FEATURE → route to PO as spec (5; mostly unbuilt)

- **WO-431** raid rewards / victory screen / offline-lite — **L**, only anti-softlock backbone exists; scoring/payout/`StakedSkr`/screen unbuilt. Spec accurate.
- **WO-435** Blink merchant view — **M–L**, self-blocked on owner sign-off; *caution:* UiStyle/Obsidian (sprite reskin) may deliver this without instantiating Blink prefabs — confirm approach first.
- **WO-442** build-mode wall drag-pay — **M**; the economy-integrity bug it guarded is **already moot** (every tap-placed segment re-validates + atomically charges in `BuildModeController.Place()`; no drag-run path exists). Only the drag-to-build *UX* is unbuilt → feature decision, not a defect.
- **WO-452** autopilot hardening — **M, PARTIAL ~50%** (magenta + economy-deduct + nav probes built); missing: combat oracle, save round-trip, dup-panel guard, ticket reproducibility. Re-scope to the 4 missing tranches.
- **WO-453** dev capture toolkit — **L**, unifying spine/probes/processor essentially unbuilt (substrate FlowTrace/Guard/F8/AutoPilot exists). **Dedupe with WO-452 §A probes before minting.**
- **WO-454 faction base generator** — **DUPLICATE WO# collision** with WO-454 unified-quest; unbuilt feature, **renumber** before implementing.

## E. METADATA CLEANUP (carry-over, still true)
- **WO-430** five-way file collision — canonical = `ui_mvvm_seam`.
- **WO-454** number used by TWO divergent specs (unified-quest = keep; faction-base = renumber).
- **WO-333** — close only the dtt-sensitivity variant; `333_village_death_no_dtt_atb_trigger` is a separate live ticket.
- Dead-code cleanup (not a WO): `Core/SceneRouter.cs` still declares `PatriciaLightParams`/`GoPatriciaLight()` pointing at a removed scene — strip in a future pass.

## F. HEADLINE
Raw ~445-file / ~397-number archive → **live backlog ≈ 9 items**: **4 open-existing** (440, 454-quest,
455, 468) + **5 new-feature specs** (431, 435, 442, 452, 453 [+454-factionbase renumber]). Everything
else is **CLOSE** (deprecated / done-in-code / stale) or **FUTURE** (dungeon). This pass added **8 new
closes** to the 06-21 list. Clean single-file win available now: **WO-440**.
