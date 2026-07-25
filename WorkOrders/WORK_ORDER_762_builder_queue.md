# WORK ORDER 762 — Work-Job Queue (the common slots/queue for Build · Upgrade · Repair · Train)

**Status:** SPEC — READY (owner-designed 2026-07-25). FOLLOW feature, not a test-build fix.
**Lane:** BuildMode / Economy / HUD. Scope: **MODERATE** (timer engine + slot cap already exist; net-new = generalized queue + dynamic slots + HUD).

---

## 0. Verdict / why — this is a COMMON structure, not a builder-only feature

Owner ruling 2026-07-25: *"i think its a common structure we need … can be also used for repairs (if extensive and you have to pick what you can afford) … also for building troops eventually."*

So this is **one shared, GENERIC, JSON-driven job-queue bounded context**, not N parallel systems. Owner ruling 2026-07-25: *"the queue needs to be common and generic and styled in obsidian … the json queue comes in and can be consumed by build, by repair, by upgrade(building), unlock tiers, maybe learn new magic."*

The queue is a **generic `WorkJob` (a JSON record)** that ANY system enqueues, and the presentation layer renders it in **Obsidian**. The consumer list is open-ended — whoever subscribes:

```
WorkJob { kind, targetId, durationSec, cost, startedAt, ... }   // a generic JSON job
kind ∈ Build | Upgrade | Repair | UnlockTier | LearnMagic | Train | …   // open enum; consumers register
```

Consumers today: **Build · Repair · Upgrade(building) · UnlockTier · LearnMagic**; **Train** later. Build it ONCE: one generic queue engine + one set of **builder slots** + one **Obsidian HUD**, N consumers. Presentation is a SEPARATE layer (§ARCH) — the queue engine never touches UI; the Obsidian HUD renders queue state read-only. Do NOT greenfield a separate queue per consumer.

The engine is already half-built: the build/upgrade **timer service ships** (`BuildTimerService` + `GameState.BuildJobs`, persisted, offline-fair, on-building countdown via `UnderConstructionVisual`), and a **concurrency cap exists** — `BuildTimerConfig.freeBuildSlots = 2` (`BuildTimerConfig.cs:67`), enforced by `HasFreeSlot`/`StartJob` (`BuildTimerService.cs:152,170`), which today **rejects** a 3rd job ("All build slots busy", `BuildModeController.cs:2139`). This WO (a) makes the slot count a **progression/purchase dial**, (b) makes a full slot **QUEUE not reject**, (c) generalizes `BuildJob` → a typed `WorkJob`, (d) adds the **HUD**.

Pillars: **sell time, never power** (buying a builder = faster, not stronger); caps at the Echo ceiling; makes collecting Echoes matter for town-growth pace (WWCD, CoC builder-huts).

## 1. Owner-locked model

- **Builder Queue Bar** with slots, shared across all `JobKind`. **Slot 1 free** (base builder). **Slots 2–N locked**, unlocked by **Echo-level milestones (progression)** OR **bought** (monetization = speed/time).
- Unlock is **harvest-independent** — you do NOT pull an Echo off harvest to build; Echo *levels* (or purchase) unlock slots, and Echoes keep gathering. (Supersedes the RCA's "Builder as a lane" Option A — cleaner, no opportunity cost.)
- **Max slots = 4** (owner-locked 2026-07-25: *"honestly I think 4 is enough"*). Deliberately UNDER the Echo ceiling (6) so builder slots stay a real scarcity gate.
- **Slot unlock = Echo acquisition milestones** (owner-locked 2026-07-25: *"we get a second echo at 10, another at 20"*). Each unlocked builder IS an Echo reporting to work — no opportunity cost (Echoes still harvest). Table:
  - **Slot 1** — start (1st Echo).
  - **Slot 2** — **level 10** (2nd Echo).
  - **Slot 3** — **level 20** (3rd Echo).
  - **Slot 4** — **purchase** (the "or bought = faster" monetization slot). *(Assumption: slot 4 is buy-only since owner named earn-milestones only through slot 3; alt = a level-30 earn — confirm.)*
  - NOTE: "level 10/20" = the player/account progression level that grants the Echo — confirm whether these are account-level or hero-level milestones against the actual Echo-acquisition code before wiring.
- Each builder works **one job**; when a slot frees it **auto-pulls the next queued job**; the bar shows each builder's **current job + kind + timer** and the **queue strip**.

### 1a. Repairs on the queue — "pick what you can afford" (owner)
- A **repair is a `JobKind.Repair` job** on the same slots/queue — reuse the whole engine.
- When damage is **extensive**, the player **can't repair everything at once** — limited **resources** AND limited **builder-slots** force a **triage**: choose which structures to repair first. This is the intended tension (a dragon-raided town rebuilt piece by piece) and it makes WO-761 (burning structures) bite — torched towers keep dropping while you spend scarce wood + slots on what you save.
- Repair cost scales with damage (extensive = expensive); the affordability choice is real. UI: a repair job shows its cost; unaffordable = can't queue it (surfaced, not silent).

### 1b. More consumers — same queue, no new engine
- **UnlockTier** (`kind=UnlockTier`) — unlocking a building/tech tier is a timed queued job on the same slots.
- **LearnMagic** (`kind=LearnMagic`) — learning a new spell is a timed queued job (pairs with the Wisdom-economy tuning below — magic should feel *earned*).
- **Train** (`kind=Train`, later) — the eventual training queue is the same slots/queue. Wire the seam now (no troop content); when troops land there's **no new architecture**, just content + a train HUD tab.
- Each new consumer = a `kind` + a start/complete handler that registers with the queue. The engine, slots, persistence, offline-fairness, and Obsidian HUD are shared and untouched.

## 2. What's net-new vs reuse

| Piece | Build / Reuse | Seam |
|---|---|---|
| Timed, persisted, offline-fair jobs | REUSE | `BuildTimerService`, `GameState.BuildJobs`, `BuildJobData` |
| The "N builders" cap | REUSE + make dynamic | `BuildTimerConfig.freeBuildSlots` -> read unlocked-slot count at `BuildTimerService.cs:157,170` |
| On-building countdown visual | REUSE | `UnderConstructionVisual` |
| Builder-hut HUD to clone | REUSE | `EchoWorkforceHud` (Obsidian modal, count line) |
| Rejection/busy feedback | REUSE | `BuildFeedbackToast` |
| **Generalize `BuildJob` -> typed `WorkJob` (`JobKind`)** | **NEW (light)** | add `JobKind` to `BuildJobData`; Build/Upgrade map to existing paths; Repair + Train are new kinds sharing the same start/complete/offline logic |
| **Pending-job queue + auto-pull** | **NEW** | new `GameState.PendingWorkJobs`; enqueue in `StartJob` when full; pull head in `CompleteJob` (`BuildTimerService.cs:335-353`); offline sweep must LOOP (a freed slot lets a queued job start + itself finish offline) |
| **Unlocked-slot count** (Echo-level + purchase) | **NEW** | persist `GameState.BuilderSlotsUnlocked`; unlock on Echo-level milestones + a store purchase; feed the dynamic cap read |
| **Repair-as-job + affordability triage** | **NEW** | `JobKind.Repair` job from the repair affordance; cost scales with damage; unaffordable can't queue (surfaced). Extinguishes WO-761 burn on complete |
| **Builder Queue Bar HUD** | **NEW (clone)** | slots + each job's kind/timer/progress (`ActiveJobs`, `RemainingSeconds`, `Progress`) + queue + per-slot ad/instant-finish (data already exposed) |
| **`JobKind.Train` seam** | **NEW (stub)** | enum + job path only, no troop content yet |

## 3. Acceptance criteria
- [ ] ONE queue/slots engine serves Build + Upgrade + Repair (+ a `Train` stub) via `JobKind` — no per-type duplicate system.
- [ ] Slot count = unlocked builders (1 base + Echo-level/bought), not a flat 2; a job past the free-slot count QUEUES (does not reject).
- [ ] A freed builder auto-pulls the next queued job; offline completion cascades (freed slot -> queued job starts + may finish offline).
- [ ] A repair is a queued job; when damage is extensive, limited resources + slots force a triage; unaffordable repairs are surfaced, not silently dropped; completing a repair extinguishes WO-761 burn.
- [ ] Builder Queue Bar shows each builder's job + kind + timer + the queue; earning/buying a slot updates it live.
- [ ] Buying a builder = faster only (never a power/stat advantage) — sell-time-not-power holds.
- [ ] Save-schema bump for `PendingWorkJobs` + `BuilderSlotsUnlocked` + `JobKind` on jobs (+ SaveMigrator step; old `BuildJobs` migrate to `JobKind.Build/Upgrade`).
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK`; regression covers enqueue-when-full, auto-pull-on-complete, offline cascade, and a repair job extinguishing a burn.

## 4. Owner decisions to confirm
- ~~Total slots~~ **LOCKED: 4** (slot 1 free + 2–4 earned/bought). ✔
- Which Echo levels unlock slots 2–4 (the milestone table) + purchase price per slot.
- Queue depth (unlimited vs a small cap).
- Repair cost curve vs damage (how "extensive" translates to cost — the affordability tension dial).

## 5. Note
- NOT in the current test builds. Reuse everything in the table; the only real new logic is the typed queue/auto-pull + the dynamic slot count + repair-as-job. Do NOT greenfield — the timer engine exists, this generalizes it.
