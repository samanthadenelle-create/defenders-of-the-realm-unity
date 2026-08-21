<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 444 — Wrap the core DEEP: instrument + guard + watchdog every critical flow

**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

> **CHECKED 2026-08-14 (phantom sweep) - STAYS READY.** Only 1 of the 11 flows is done, and the commit
> that cites this WO is unrelated store work. This is real outstanding work, not a phantom.

**Status: STANDING STANDARD + phased coverage pass.** Owner (2026-06-17): "the whole core should be
wrapped deep… add a debugger and tries at all critical logic." Operationalizes `ARCHITECTURE_PRINCIPLES`/
`CLAUDE.md §12` + `docs/INSTRUMENTATION_STANDARD.md` as COVERAGE, not just per-bug. The wave-start
(WO-438/wave fix + stall watchdog) is the proven template.

## The principle (why)
A bug we can't *see* costs more than the bug. The wave proved it: a silent stall + lost `Step` traces =
hours of blind guessing ("works on my machine"). The fix is not a band-aid per bug — it's making **every
critical flow narrate itself and fail LOUD**, so the data names the dead step the first time, headless or
on a real web player's machine (via WebTrace, WO-443).

## The 3 layers — every critical flow gets all three
1. **TRACE (the "debugger")** — `FlowTrace.Step` at each meaningful branch: request → resolve → fallback →
   render. So a run reads like a narration of where it went.
2. **GUARD (the "tries")** — `Guard.Try` / `Guard.TryEach` around every risky op (parse, list-build,
   service-lookup, `Instantiate`, UI construction, await). A bad object **logs (`FlowTrace.Fail`) and is
   skipped**, never silently blanks a screen. NO catch swallows without logging (forbidden).
3. **WATCHDOG (for async/stalls)** — a stall is NOT a thrown exception, so guards can't catch it. Any
   async/state-transition flow gets a watchdog: if it doesn't reach its expected state within a window,
   emit a **captured `FlowTrace.Fail` with a full state dump** (like `WaveManager`'s `StallStateDump`).
**All anomalies → `FlowTrace.Fail` (→ `LogError` → break-log.jsonl + WebTrace).** Captured, queryable, off-device.

## Coverage — critical core flows (phased by demo-leverage)
Each flow: add the 3 layers, gate, verify via fleet/WebTrace that it narrates + captures failures.
- [x] **Wave/combat start** — DONE (the template: singleton + GuardedKickoff + RetryTillActive + stall watchdog).
- [ ] **Scene seams / transitions** — `SceneTransitionTrigger` cross, additive scene loads (the exit-seam class).
- [ ] **Data/catalog loads** — `GearCatalog`, `WaveDataLoader`, `QuestCatalog`, `CanonicalJson` (null/slow → Fail+watchdog).
- [ ] **Service lookups / singletons** — `CoreServices`, the `FindObjectOfType` resolutions (wrong-instance class → prefer singletons, Fail on null).
- [ ] **Save / load** — `GameStateService` round-trip (a swallowed save error = lost progress).
- [ ] **Economy** — `TrySpend`/`Grant` (no silent over/under-charge; the build-mode wall-pay, WO-442).
- [ ] **UI / panel construction** — the modal builders (`Guard.TryEach` per row/slot so one bad item never blanks the panel).
- [ ] **Companion / party** — the NRE class (destroyed-object access) — guard + Fail.
- [ ] **Raid clear / `OnCleared`** — the soft-lock class (no-subscriber → Fail-loud, not silent stall) (WO-441 A).
- [ ] **Equip / apply** — gear→visual pipeline.
- [ ] **Dialogue** — the no-node class (already mostly fixed; keep the guard/Fail on command verbs).

## How to run it (don't boil the ocean)
- Phased: one flow per WO/agent, demo-critical first (scene seams, data loads, save/load, economy).
- Each is ADDITIVE instrumentation/guards — no logic/balance change; gate-verified; behavior-preserving.
- Verify each by a fleet run (its Fails show in break-log) — the instrumentation is "done" when a
  deliberately-broken case surfaces a captured Fail with state.
- Toggle: `FlowTrace.Enabled` stays ON while a system stabilises; the captured `Fail`s are always-on.

## Acceptance (per flow)
- [ ] `FlowTrace.Step` at each branch; `Guard.Try` on every risky op; a watchdog on every async/transition.
- [ ] No silent catch anywhere in the flow (every catch logs a `Fail`).
- [ ] A forced failure in the flow produces a CAPTURED `Fail` with enough state to name the dead step.
- [ ] Compile gate green; no behavior/balance change.

*Cross-ref:* `CLAUDE.md §12`, `docs/INSTRUMENTATION_STANDARD.md`, `FlowTrace.cs`/`Guard.cs`/
`BreakCaptureHarness.cs`, WO-443 (WebTrace — the off-device channel), the wave fix + watchdog (the template).

> **AUDIT 2026-08-21 (agent fleet, read-only):** OPEN — STILL VALID. Evidence: `only vendor-contract hits = number collision` — 10 of 11 flows lack instrumentation. Status left at READY deliberately: this work is real and unbuilt. Verified against HEAD 2f0b97bb5, not against the ticket's own claims.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict. ⚠ NOTE FOR ANYONE REOPENING: the 2026-08-21 read-only audit had classified this one OPEN - STILL VALID, with the evidence cited above. The owner's review supersedes that call (owner statements are ground truth). The audit line is left in place deliberately, so if this work turns out to be needed, the evidence for it is still here rather than erased.
