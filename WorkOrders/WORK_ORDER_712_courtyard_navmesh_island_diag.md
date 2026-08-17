<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-13
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-13) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 712 — Courtyard navmesh island (mid-run PathPartial to ALL seams) — DIAGNOSIS FIRST

**Status: READY — instrument-only lane (§12 hard gate: cause NOT yet proven).**
**Lane:** World/navmesh diagnostics. Minted from banner 712 (bumped to 713 same edit).

## Captured signal (fleet 2026-07-13 evening, seeds 7000+, runs 1/6/7/8/9 of 12)
`[Flow:AutoTest] SEAM-UNREACHABLE: 'HomeReturnPortal_North' ... closest 46.7m / 31.7m` —
`path=PathPartial` from the hub to ALL FIVE seams simultaneously, onset MID-RUN (run 6: tower
placed 22:53:35 -> pet-house 22:53:45 -> SEAM burst 22:54:02; probe needs 2 consecutive bad
5s scans, onset ~= the placement window).

## What is NOT proven (do not fix on these theories)
- Placement-carve severing the courtyard: runs 3/4 placed NOTHING and still failed the exit
  leg; runs 0/2/5 placed both structures and PASSED. Correlation broken.
- Today's AddFootprintBlocker rendered-bounds change: shrink-only by construction
  (`Mathf.Clamp(rw, cellSize, w)` — never larger than the old claim).

## The diagnostic to build (one instrumented fleet pass answers it)
On each SEAM-UNREACHABLE strike (first strike per run), dump ONCE:
1. The hero's NavMesh island extents (sample flood: NavMesh.CalculatePath from hero to a fixed
   probe ring at 10/20/40/80m in 8 compass directions — log reachable/partial per ray).
2. Every NavMeshObstacle live in the scene at that moment: name, position, size, carving flag
   (FindObjectsByType dump — the invisible-blocker census idiom StructureFactory already has).
3. The run's BaseLayout records + placed-structure positions at strike time.
4. CarveOnlyStationary/carving settle state if obstacles report it.
All as `[Flow:NavDiag]` lines so the run self-reports; fleet run C reads the answer.

## Acceptance
- [ ] One fleet run with >=1 strike produces the dump; the dump names the severing geometry
      (or exonerates placement and points elsewhere — e.g. a carve settle race, a DDOL
      obstacle, the WO-602 portal colliders).
- [ ] No behavior changes in this WO — instrument only. The fix WO mints from the dump.

*Cross-refs:* fleet run A 2026-07-13 (seeds 7000+) RCA · known AttemptExitCastle/CavePortal
class (FLEET_TRIAGE_2026-07-12, WO-608/453 lineage — SEPARATE, do not conflate) ·
BaseLayoutLoader.AddFootprintBlocker (exonerated-by-construction, re-verify in the dump).
