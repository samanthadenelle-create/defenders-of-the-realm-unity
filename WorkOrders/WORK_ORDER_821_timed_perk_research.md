# WORK ORDER 821 — Building perk research: timed + queued (WC3 style) + skills-tab timers

**Status: READY TO IMPLEMENT**
**Source: owner F8 seq 545 (2026-08-01, verbatim):** "Researching Skills should have queued timers as
well. Swift recuitment makes it sound like faster build times not troop health, troop health should be
something about advanced workouts or something"
**Silo:** Buildings/Progression/Queue

## Split
- The NAMING half shipped immediately (same session): `Swift Recruitment` -> `Conditioning Drills`
  (both building-tiers.json copies; id/iconId unchanged so owned-perk saves survive).
- THIS WO is the FEATURE half: building perk purchases become timed research on the Research channel.

## Current state (verified)
- Building perks (WC3-style tabbed Upgrade+Skills panel) are INSTANT gold purchases — no timer, no queue.
- The shared queue engine already has a Research channel (`ChannelId.Research`); per-troop stat
  upgrades already ride it (`BarracksService` ~line 210), and the Work panel already renders a
  RESEARCH tab (`ObsidianQueueHud.cs:67`). The engine needs ZERO new plumbing.

## Scope
1. Perk purchase routes through `BuildTimerService.Enqueue(JobKind.Research, "perk:<perkId>", seconds)`:
   charge at enqueue (LedgerSpend rules once that common lands — see commons audit cand. 1), grant the
   perk in the completion effect (JobEffectRegistry, mirroring TrainTroopEffect).
2. Research seconds: add `researchSeconds` per perk in building-tiers.json (both copies); default a
   sane curve by tier (e.g. T1 60s scaling up) — owner tunes numbers later.
3. Skills tab rows show live state like WC3: `Researching... m:ss` on the active perk row, `Queued (n)`
   on queued rows, both driven off `ObsidianQueueGate.Status` version polling (the HudKitController
   idiom). Buy button disabled while that perk is in flight.
4. Cancel = WO-799 refund rules (75% or the ruled rate) — reuse the cancel/refund engine, do not fork.
5. Fold the visual language into WO-817's production-glance spec (icon + ring) — 817 owns the LOOK,
   this WO owns the ENGINE routing + the minimum readable state text.

## Acceptance criteria
- [ ] Buying a perk starts a timed Research job; wallet charged once, at enqueue.
- [ ] Perk grants ONLY on completion (kill the app mid-research -> job resumes from save, no perk yet).
- [ ] Skills tab shows Researching/Queued states + timers; Work panel RESEARCH tab lists the same jobs.
- [ ] Cancel refunds per WO-799 rules and un-marks the row.
- [ ] Regression: a data check that every perk carries researchSeconds > 0 in both copies.

## Do NOT touch
- Troop stat upgrades (already on Research — unchanged).
- The queue engine itself (frozen per WO-817 note); this is a CALLER of the engine.
