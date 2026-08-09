> ## RECONCILED 2026-08-08 - true status is PARTIAL
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: phases 0 / A / B / C / D AND F all shipped; only phase E is deferred. CRITICAL: the phase table below still said row F was "WITHHELD" - but commit `177b24a7` landed `TownBankCapacity.cs` (708 lines) + `storage-caps.json` + `TownBankCapRegression.cs` (779 lines) the SAME DAY. That row was the most misleading line on the board: it would have sent a session off to rebuild the town bank cap that already exists. Row F is corrected in place below.
> The previous Status line read "IN PROGRESS (collector half implementing 2026-08-04; bank half withheld)" and was wrong.

# WORK ORDER 901 — THE COLLECTOR LOOP (umbrella)

**Status:** READY TO IMPLEMENT - partial (reconciled 2026-08-09, per this file's own 08-08 banner - phases 0, A, B, C, D and F all shipped (F landed in `177b24a7`: `TownBankCapacity.cs` plus `storage-caps.json` plus `TownBankCapRegression.cs`); only phase E remains)

**Status:** PARTIAL — phases 0/A/B/C/D and F shipped; only E deferred (reconciled 2026-08-08, see banner)
**Owner directive, 2026-08-04:** *"consolidate those into one idea and implement."*
**Supersedes as a PLAN:** WO-857, WO-858 (Grok), WO-859, WO-900 (CLI). Those files remain as the
**detailed appendices** — this document is the single idea, the sequence, and the overlap ruling.

---

## 1. THE ONE IDEA

> **Your town keeps producing while you are away, into containers that visibly fill to a cap and then
> stop — and storage buildings raise what the town can hold.**

Four WOs were each speccing a slice of that single player-felt loop. Split four ways they would have
shipped **four different notions of "full"**, two icon systems and two HUD surfaces. Consolidated here.

The loop, as one sentence of plumbing:

```
harvest tick / offline catch-up
    -> [ per-collector pending pool,  cap = repo.capacity ]      <- PHASE A (WO-859)
    -> Collect tap
    -> [ town wallet,  cap = baseCap + sum(storageCapacity) ]     <- PHASE D (WO-857)
    -> spend
```

**Two ceilings, one reader.** They are genuinely different mechanisms — confirmed at source:
`RepoProps.cs:155` `storageCapacity` (a raidable stock CONTAINER; **zero readers** —
`IsStorageContainer:174` has no callers) versus `RepoProps.cs:188` `capacity`, whose own doc at `:185`
says *"Distinct from `storageCapacity`… this sizes a collector's pending buffer"*. Only `capacity` has a
live reader (`ResourceCollector.cs:314-320`). **They must not be merged — but one `CapacityService`
should eventually read both.**

---

## 2. ⚠ THE DUPLICATION RULING (why consolidating was necessary, not just tidy)

**Grok's WO-858 ("collector resource icons — billboard wood/iron/food/crystal when pending, tap =
Collect") and CLI's WO-900 ("wire the full tell") are THE SAME FEATURE.**

`CollectorStackView.cs` (437 lines) **already implements it**: pooled prop pile, world-space fill bar,
near-full amber band at 85% (`:53`), `N/20` readout (`:276-289`), `"!"` bang when full (`:337`), glint VFX
(`:363-368`), and a one-time *"{Building} is full — collect it, or upgrade it to hold more"* toast
(`:370-377`).

**`CollectorStackView.Attach` has ZERO CALLERS.** Recorded at `WORK_ORDER_783_sme_findings_fix_wave.md:186`
and `UiObsidianConformanceRegression.cs:168` — and never fixed.

**Ruling: WIRE IT, do not build it.** Building WO-858's icons from scratch would have been writing dead
code a second time. WO-858's genuinely NEW content — `siegeValue` / `highValueTarget` for raid targeting —
is unrelated to the tell and survives as its own ticket.

---

## 3. PHASES — sequence and status

| # | What | Source spec | Status |
|---|---|---|---|
| **0** | **Prove the P0 headless** before any edit (§12) | WO-859 §2 R4 | implementing |
| **A** | Close the existence-gate **back door** | WO-859 §4 | implementing |
| **B** | Capacity expressed in **hours**, not units | WO-859 §5 | implementing |
| **C** | **Offline / away accrual** | WO-859 §4 | implementing |
| **D** | Wire the **"I am full" tell** (Part A only) | WO-900 §3 | implementing |
| **E** | Ambient **HUD collector chip** | WO-900 §4 | DEFERRED |
| **F** | **Town bank cap** + `current/max` chips | WO-857 | **SHIPPED** (reconciled 2026-08-08) — `177b24a7` landed `TownBankCapacity.cs` (708L) + `storage-caps.json` + `TownBankCapRegression.cs` (779L). This row previously read "WITHHELD — see §5" and was wrong; §5 below is SUPERSEDED. |
| **G** | `siegeValue` / `highValueTarget` raid targeting | WO-858 (Grok) | not started |

### Phase A — the P0 the last fix missed
`ResourceCollectorBootstrap.EnsureFallbackCollector` (`:60-89`) creates live collectors for farm,
lumbermill and forge **unconditionally**, never consulting `everBuiltStructureIds`, and
`ResourceBuildingHarvester.MayHarvest` (`:236`) opens the instant a live collector exists. Commit
`35485f31` closed the *rule*; this path bypasses the rule. Two consequences: a **blank town earns again**
(the bootstrap is `RuntimeInitializeOnLoadMethod(AfterSceneLoad)` and runs before `BaseLayoutLoader`), and
**full town income accrues while the player is in a DUNGEON** (the harvester is suppressed in raids but
not dungeons).

### Phase B — the capacity curve runs BACKWARDS
Capacity grows x3 from L1->L5 while throughput grows x5.6, so **upgrading a collector shortens how long it
runs unattended**. A 6-echo L5 farm fills in **5.7 minutes**. `RepoProps.cs:183`'s authoring intent (*"a
farm at ~150/min fills 1000 in ~7 min"*) is stale by **9.6x** post-WO-855. Fix: scale capacity on the same
basis as rate — the in-repo precedent is `EchoService.cs:142-149`, which already solved exactly this for
the silo.

**Owner decision taken: 8 hours** — farm 7500 / lumbermill 5760 / forge 3456. WWCD: a morning and an
evening check-in both pay. Data, retunable; 4h halves it and still doubles today's window.

### Phase C — the highest-risk detail, stated once more
**The last-accrual stamp must advance even when the pool is at cap.** If it freezes while full, tapping
Collect instantly refills from a frozen backlog and the cap bounds nothing. Pinned by regression case 8
`[stamp-advances-at-cap]`.

**Per-collector capacity IS the offline cap. No second time-based cap.** One mechanism, tuned by one dial.

---

## 4. THE COPY LAW — so the player never sees two "full"s

- **"Storage" / "Bank" / `current/max`** = the **wallet** (Phase F).
- **"Collectors N/M full"** = the **pending pools** (Phases D/E). **Never the word "Storage".**
- **Cross-phase dependency:** once Phase F lands, a full bank means the Collect tap cannot bank. The
  collector tell must then read `Bank full` instead of `tap to collect`. **Phase F owns adding that
  headroom check.** Named here so neither phase ships a lie.

---

## 5. ⚠ WHY PHASE F IS WITHHELD

> **SUPERSEDED 2026-08-08.** Phase F SHIPPED in `177b24a7`. The reasoning below is preserved as the
> historical record of why it was held; it no longer describes current state. Do not rebuild the bank cap.

Phase F clamps `EconomyService.Grant` — the single path every income source in the game flows through.
Getting it wrong makes resources **silently vanish**, and it interacts with quest rewards, raid loot, Echo
dumps and the founding sequence, which starts at **0 wood / 0 iron**. It is the one piece in this cluster
that is not safe to ship unsupervised, so it was deliberately held for owner review rather than folded
into an autonomous implementation pass.

**✅ RULED (owner, 2026-08-04): CLAMP AND WARN.** Overflow is lost, and the player is warned. This is
WO-857 §4.3 as originally written, and it applies **everywhere** — including where a holder exists.

For the record, because the reasoning should survive the decision: the WO-859 analysis argued this is
not literally the CoC model (CoC storages *refuse*, and the collector keeps holding), and recommended
hold-back where a holder exists with clamp-and-warn only where none does (Echo Dump, raid loot, quest
rewards). **The owner considered that and ruled clamp-and-warn uniformly.** One rule everywhere is
simpler to reason about, simpler to signal, and cannot produce the confusing half-state where some
overflow survives and some does not.

**Implementation consequence — do not lose this:** the warn is now load-bearing, because it is the
*only* thing standing between the player and silently vaporised resources. It must fire on every
clamped grant, it must name the resource, and it must not be swallowed (§12). A clamp that loses
resources without telling the player is the defect this ruling is one line away from.

**Also blocking Phase F:** WO-837 step 1 has **not shipped** — `lumberyard` is still in
`BuildModeController.FoundingKit` (`:2697-2704`), contradicting the WO-837 ruling and
`CANON_GROUND_TRUTH_2026-08-02.md:197`.

---

## 6. Binding constraints carried across every phase

- **Crystals are UNCAPPED** (owner ruling 2026-08-04 — premium currency; CoC precedent, gems uncapped).
  Collectors yield Food/Wood/Iron only. Enforced by regression case 11 `[no-crystal-faucet]` — a test,
  not a comment.
- **The existence gate is not to be back-doored.** Income requires the building to have been built
  (`everBuiltStructureIds`, v36) AND a live registered collector.
- **No save-schema bump** anywhere in the collector half. If an implementer believes one is needed, stop
  and escalate.
- **No new reflection bridge.** No new `static_gate.py` allowlist entry.
- **Presentation is a separate layer** — the gameplay object never builds UI. `CollectorStackView` is the
  correct pattern (injected with the model); the WO-856 `CrystalMine` bubble is the anti-pattern.
- **`UI_CAPTURE_OK` before any UI ships** — compile-green never proves a panel looks right.

---

## 7. Appendices (the detailed specs — still authoritative for implementation detail)

- `WORK_ORDER_859_collector_capacity_hours_and_offline_accrual.md` — Phases 0/A/B/C, full RCA + regression
- `WORK_ORDER_900_collector_full_tell.md` — Phases D/E, the tell + the HUD gate design
- `WORK_ORDER_857_coc_resource_storage_caps_hud.md` (Grok) — Phase F
- `WORK_ORDER_858_collector_resource_icons_and_siege_value.md` (Grok) — Phase G survives; the icon half is
  superseded by §2 above
