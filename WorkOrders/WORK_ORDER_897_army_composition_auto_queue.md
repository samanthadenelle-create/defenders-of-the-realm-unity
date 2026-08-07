# WORK ORDER 897 — Army composition presets that auto-queue the build-outs

**Status:** SPEC — READY (grounding note in §3) · **Silo:** Troops / queue / UI · **For:** CLAUDE CLI · **Date:** 2026-08-05
**PO:** Samantha (owner) · **Author:** UI seat
**Owner ruling:** *"create armies and they will auto-queue the build-outs."*

## 0. Idea
Instead of training troops one at a time, the player defines an **Army** — a composition of troop types + counts
(e.g. 5 Spearmen, 3 Archers, 2 Outriders) — and hitting **Muster army** **auto-enqueues every troop's training
into the Train queue** in one action. One decision musters a whole force; the queue does the rest.

## 1. Behavior
- **Army preset:** a named composition = a list of `{ troopId, count }`. The player builds/edits it (add troop rows, set counts), sees the **total cost** (sum of all troop costs) and **total time** (sum, or parallel-aware if the Train channel has multiple slots).
- **Muster army (the one action):** on click, enqueue each troop instance onto the **Obsidian Train queue channel** (memory §8: Builder/Train/Research is the single home for timed work) — `count` entries per troop row, in composition order. Spend resources per the queue's normal rules.
- **Partial affordability:** enqueue what's affordable in order; if the full army can't be afforded, muster what fits and surface a clear "Queued X of Y — short N wood" tell (shape/text, not colour). Never silently drop.
- **Feedback:** after muster, the button/state reflects it (like WO-895's stateful button): `Mustering · N in queue`, tied to the live Train queue; the army panel shows queued/among-training progress.
- **No second queue:** this is a batch ENQUEUE onto the existing Train channel — do NOT build a parallel training system.

## 2. UI (Obsidian kit)
A small "Armies" surface (reachable from the Barracks / a Muster button):
- Composition rows: troop icon + name + count stepper + per-row cost.
- Footer: total cost + total time + **Muster army** button (stateful per §1).
- Optional: save/name a preset to re-muster later.
Build with `ElarionUiKit` + `docs/UI_BLINK_TEMPLATE_CANON.md` chrome (same as the other panels).

## 3. Grounding for CLI (verify at source before wiring)
- The troop-creation authority: `Assets/_Modules/Village/Troops/TroopFactory.cs` (+ `TroopController`/`TroopDeployer`).
- The queue: the Obsidian multi-channel queue's **Train** channel (per memory §8 / the queue panel). Enqueue through the SAME API the single-troop-train path uses — find it, reuse it, do not fork.
- Troop cost/time data: the troop/building-tier catalog (which troops are unlocked gates the composition — only offer unlocked troop types).
- Confirm whether the Train channel runs one-at-a-time or parallel (drives the "total time" display).

## 4. Acceptance criteria
**Engineering:**
- [ ] An army preset holds `{troopId, count}` rows; total cost + total time computed correctly.
- [ ] **Muster enqueues every troop onto the existing Train queue** in order — no parallel/second queue.
- [ ] Only unlocked troop types are offered.
- [ ] Partial affordability musters what fits and reports the shortfall clearly (never silent).
- [ ] Muster button reflects live queue state (mustering / N queued), tied to the real queue authority.
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK`.
**Felt (owner closes):**
- [ ] Define an army (e.g. 5 Spearmen + 3 Archers), hit Muster once, and watch all 8 trainings land in the Train queue and process — without queueing each troop by hand.
- [ ] Headless capture of the army panel + the Train queue after a muster — open the PNGs, attach to RESULT.

## 5. RESULT
`WorkOrders/WORK_ORDER_897_army_composition_auto_queue.RESULT.md` — the Train-channel enqueue path used, and the muster→queue screenshots.

*(If Grok already drafted an army/auto-queue WO, this is the UI-refined version — reconcile to one number, first-on-disk-and-referenced wins.)*
