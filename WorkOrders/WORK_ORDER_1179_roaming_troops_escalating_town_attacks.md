# WO-1179 - Roaming troops that attack the town, escalating in size and smarts

**Status:** FIXED - landed 2026-08-25 at `4f4055043`, gated `COMPILE_GATE_OK` + `REGRESSION_OK 277/277`. Owner felt-close owed.
>  PRIOR: **Status:** BLOCKED - two owner questions. The implementation spec is written and every seam is verified at source (see the SPEC section); the design rulings are settled. ⛔ **Q1: does "offline towns can be attacked" mean the SHIPPED banked-pressure model, or a real absentia resolver (WO-430-F, explicitly unbuilt AND explicitly forbidden until stakes are ruled)? Q2: four spawn SIDES, or four damageable GATES?** ⚠ Those two answers change this ticket by an ORDER OF MAGNITUDE - it is not handable until they land.
>  PRIOR: **Status:** READY - all three open design questions RULED by the owner 2026-08-24 (existing wave system; offline towns CAN be attacked; losses are REPAIRABLE and bounded). ⛔ Build WO-513 first so pack behaviour is inherited, per this ticket's own note.

*(Board note 2026-08-24 — Ready-queue audit, `READY_FOR_REVIEW.md`: ⛔ **WO-513 is a COMPOSER, not a BLOCKER, and this ticket stays READY.** The 2026-08-24 audit read this ticket's own "build 513 first and this inherits it" as a hard dependency and proposed marking it BLOCKED; the lead reversed that as an over-read. The two compose — **513 is how a pack FIGHTS once it has arrived; 1179 is WHAT arrives, from where, and against how many gates** — so 1179 is independently handable and simply inherits richer pack behaviour if 513 lands first. Status unchanged; this note exists so the over-read does not recur.)*
**Silo:** Combat/AI. **Origin:** owner, 2026-08-24, verbatim:
> *"I still want to add roaming troops that attack the town, incrementally getting harder, smarter
> attacks one gate, maybe two gates same time, all 4 eventually"*

## Why this is its own ticket and not WO-513

⚠ **WO-513 is the nearest thing in the tree and it is NOT this.** 513 makes an orc *family* coordinate
once it has already arrived - surround, flank, express roles instead of three identical solo rushes.
It is about **how a group fights when it gets there**. This ticket is about **what arrives, from
where, how often, and against how many gates at once**. Handing 513 to a dev lane expecting roaming
troops would deliver a real feature that is not the one she asked for.

⭐ They compose well: 513 is the melee behaviour of a pack, this is the campaign that sends packs.
Build 513 first and this inherits it.

## The escalation ladder (the shape of the ask)

1. One gate, small band.
2. One gate, larger / better composition.
3. **Two gates simultaneously** - the first point at which the player cannot simply stand in one
   place, and therefore the first real difficulty step.
4. All four gates.

⚠ **The step that matters is 2 -> 3, not the numbers.** Everything before it is a bigger version of
the same fight; the two-gate attack is the moment the player must choose what to leave undefended.
Tune that transition, not the roster sizes.

## Seams that already exist - reuse, do not greenfield

- ⛔ **CORRECTION 2026-08-24: the `SpawnPoint` TAG DOES NOT EXIST** and reading it THROWS. I wrote
  that line here citing CLAUDE.md §7, which was itself wrong (now fixed). The real seam is the
  **`WaveSpawnPoint` COMPONENT**, and there are **20 markers, not 4** (5 per side x 4 sides), each
  carrying a populated **`GateIndex`** - so the data for per-gate targeting is already there.
- `WaveManager` already **generates rosters** (`waves.json` `_smartComposition:1`; the authored
  `enemies[]` batches are INERT and a re-add now FAILS a regression). ⛔ **Do not author batches** -
  extend the generator.
- Enemy AI finds the hero **by component** (`FindFirstObjectByType<HeroLocomotion>()`), not by tag.
- Gates are `IDamageableStructure` implementors already.

## Open design questions - the owner's, and they change the build

1. **Is this the wave loop escalating, or a SECOND system running alongside it?** A raid that arrives
   while the player is mid-wave is a different feature from a wave that gets harder.
2. **Can it arrive while the player is away / offline?** ⚠ This collides directly with the 48-hour
   shield product she proposed the same day - if roaming troops can hit an offline town, the shield
   has something to protect and a reason to exist; if they cannot, the shield protects nothing.
3. **What does losing a gate cost?** Escalation without a consequence is difficulty without stakes.

## Acceptance (provisional - do not implement until the questions above are answered)

- [ ] Attacks originate from the existing `SpawnPoint` markers, not a new spawner
- [ ] Composition comes from the `WaveManager` generator, not authored `enemies[]` batches
- [ ] The 2-gate step is reachable in a headless run and PROVEN by a captured trace, not by reading
      the tuning table

---

## ⭐ OWNER RULING 2026-08-24

All three open design questions are answered. This ticket moves **SPEC → READY**.

### Q1 — **Use the EXISTING wave system.** Not a second system.

Roaming attacks are the **wave loop escalating**, driven by the `WaveManager` generator. ⛔ Do not
stand up a parallel raid system, and ⛔ do not author `enemies[]` batches — `_smartComposition:1`
generates rosters and a re-add now **FAILS a regression** (CLAUDE.md §8). Extend the generator; reuse
the four `SpawnPoint` markers already placed 12m outside each gate.

### Q2 — **Offline towns CAN be attacked.**

This is what gives the 48-hour shield something to protect, and it is what makes Q3's stakes real
rather than theatrical.

### Q3 — **What losing a gate costs: REPAIRABLE losses, bounded.**

When a gate falls:

- the **gate is damaged** and **defensive capacity is reduced until repaired**
- the player pays **wood / stone / iron** to repair
- the **repair takes time**
- **possibly** the attacker steals a **small, bounded** amount of **stored basic resources**

### ⛔ NEVER — the hard list

- ⛔ destroyed **premium** items
- ⛔ lost **cosmetics**
- ⛔ lost **crystals**
- ⛔ permanent **building deletion**
- ⛔ a **troop wipe** while offline

Owner's reasoning, verbatim:

> *"without making somebody log back in Tuesday morning and discover that Saturday's $40 purchase was
> eaten by goblins."*

⭐ The line is: **losses must be repairable with time and basic resources.** Anything a player paid
for, or cannot get back by playing, is off the table.

### ⚠ THE STRUCTURAL CONSTRAINT — recorded verbatim from the lead, and it is BINDING

Offline theft plus a shield sold to prevent it is structurally **"selling the cure for a disease we
added"**. It is legitimate here — and only here — because **theft exists for STAKES** (Q3's whole
purpose) and **the shield is a TRAVEL CONVENIENCE**, not the only defence against a harm we
manufactured in order to sell it.

⛔ **THEFT RATES MUST NEVER BE TUNED UPWARD TO MOVE SHIELD SALES.**

⚠ **If that trade is ever proposed, it is the TELL that the line was crossed.** Not a balance
discussion — a tell. Refuse it and surface it to the owner. Theft rates are tuned against the
difficulty curve and nothing else; shield sales are never an input to that number. Any tuning WO that
cites shield conversion as a reason to raise theft is refused by this ruling on sight.

### Acceptance — no longer provisional; implementation may start

- [ ] Attacks originate from the existing `SpawnPoint` markers, not a new spawner
- [ ] Composition comes from the `WaveManager` generator, not authored `enemies[]` batches
- [ ] The 2-gate step is reachable in a headless run and PROVEN by a captured trace
- [ ] An offline attack can damage a gate and steal a **bounded** amount of basic resources only
- [ ] A captured run proves **no** crystal, cosmetic, premium-item, building-deletion or troop-wipe
      loss is reachable by any offline-attack outcome
- [ ] Gate repair is completable with wood/stone/iron + time, with no premium requirement

---

## ⛔ 2026-08-24 SEAM MAP - THE THEFT LINE IS ALREADY BUILT, AND A NEW ONE WOULD FAIL THE BUILD

The ruling recorded here says the attacker *"possibly steals a small bounded amount of stored basic
resources."* ⚠ **Read literally as a BANK take, that is a re-litigation of an owner ruling dated
2026-08-22 - and it is guarded three ways:**

1. **The deletion is documented at source.** `Village/Waves/DefenseReportBuilder.cs:370-379`:
   *"⛔⛔ NOTHING IN THIS FILE MAY EVER DEBIT THE WALLET FOR A SIEGE."* An earlier pass shipped
   **a flat 15%-of-banked take through `EconomyService.TrySpend`** and it was **DELETED**, because
   `ResourceCollector.OnSiegeDestroyed` had **already** removed the resources - so a wallet debit
   charges the player **twice for one siege**. ⭐ *"That is not a balance question, it is a
   double-charge."*
2. **A regression FAILS THE BUILD if it returns.** `Editor/Regression/SiegeLossStakesRegression.cs:268-278`
   hard-rules on `StakeRules.Build` / `TakeFrom` / `ProtectedFloor` / `StealFraction` /
   `ProtectedFloorFraction` - ⭐ **including the CONSTANTS**, explicitly so *"there must be no
   bank-take constant for a future edit to hang arithmetic off."*
3. `SiegeScheduler.cs:311-312` asserts no second silent theft path.

### ⭐ AND THE RULING IS ALREADY SATISFIED - by the mechanism the 08-22 ruling KEPT

`ResourceCollector.OnSiegeDestroyed` (`Village/Buildings/Progression/ResourceCollector.cs:515`) takes
**`RaidLootFraction 0.5`** - half of a broken collector's **uncollected pending** - and it mutates
`_pending`, ⛔ **never the wallet** (WO-664).

That **IS** "a small bounded amount of stored basic resources": bounded (half of pending), basic
(wood/iron/food), and a real stake the player feels. **The two rulings agree; only the word "stored"
was ambiguous between *banked* and *uncollected*.**

⛔ **THEREFORE: this ticket proposes NO new theft path.** The stake already exists. Raid consequence
work routes to the collector path and touches `StakeRules` not at all.

## ⚠ The crystals question - resolved by the lead, not owner-owed

The seam map flagged that PROD-014's same-day amendment makes **crystals a universal REPAIR
currency**, while this ticket's ⛔ NEVER list bans **crystal LOSS**. ⭐ **These do not conflict:
spending crystals to repair is a CHOICE the player makes; losing crystals to a raid is something DONE
TO THEM.** The NEVER list governs **involuntary** loss. A player may elect to pay crystals to mend a
gate; a raid may never take crystals from them.

⚠ Note for whoever prices repair: **every authored repair row is wood+iron** - `gate_stone`
240w/200i, `wall_stone` 120w/240i, `repair_default` 120w/60i, code fallback 30w/15i. **No food on any
repairable row**, which independently confirms the wood/iron reading.

## ⚠ Two more seam facts a spec must not get wrong

- ⛔ **There is NO "this took damage" notification.** `WallRepairController` has no `NotifyDamaged`,
  no event - damage is discovered by a **rescan timer** (`:173`, `:226-229` -> `Rescan()` `:269`).
  A siege consequence **cannot call in**; it waits for the poll, or the spec adds the push seam.
  ⚠ And several call sites **self-install a controller at runtime** (`HubRepairAffordance.cs:239`,
  `WaveFeedbackDirector.cs:436`) - do not assume one scene-authored instance.
- ⭐ **An offline-aware repair path already exists**: `EchoRepairService.ApplyOfflineWindow(OfflineClaimWindow)`
  (`Village/Harvest/EchoRepairService.cs:192`), driving the **non-UI** verbs
  `TryPeekWorstDamaged` (`:872`) / `TryRepairWorst` (`:900`). **Offline resolution has a precedent -
  do not invent a second one.**

## ⛔ The shield is PROHIBITED from being pre-built

`WORK_ORDER_1026_IMPLEMENTATION_PLAN.md:527`, verbatim: *"What must NOT be pre-built while the ruling
is open: any shield/immunity timer, any revenge target..."* ⚠ And two identifiers are **NOT** shields
- do not cite them as one: `HeroAbilities._damageShieldUntil` (`:1506`) is a seconds-long in-combat
damage-reduction buff, and `TownSuspension._graceUntil` (`:114`) is a non-persisted `Time.time`
session grace.

---

# ⭐ IMPLEMENTATION SPEC (lead pass, 2026-08-24) — every seam verified at source

⛔ **Two owner questions below (§0) change the size of this ticket by an ORDER OF MAGNITUDE. It is not handable until they are answered.** Everything else is settled.

## 0. ⛔ THE TWO QUESTIONS

### Q1. "Offline towns can be attacked" — banked PRESSURE, or a real ABSENTIA RESOLVER?

⭐ **The shipped design already does something, and it may already be what you meant.** `SiegeScheduler.cs:20-31` states the design call verbatim: *"ApplyOfflineWindow does **NOT** resolve battles. It converts the away window into siege **PRESSURE**, and the siege then happens **LIVE, at the gate, with the player watching**."* Its reasoning: resolving in absentia under the interim would write a report whose rows and losses are both empty — *"a record that says nothing happened, which is worse than no record."*

- **(a) Banked pressure = ALREADY BUILT.** Away time produces **the attack you come home to**. Nothing is taken while you are gone. `SiegeScheduler` is already an `IOfflineClaimConsumer` with a **24 h cap** and `_maxPendingSieges = 1` (*"coming home to a queue of five assaults is a punishment for playing"*).
- **(b) True absentia resolution = WO-430-F, explicitly UNBUILT and explicitly FORBIDDEN.** `DefenseReport.cs:89-92`: *"Nothing produces this yet, and **nothing should until the stakes are ruled**."*

⚠ **This ticket's own acceptance line — *"an offline attack can damage a gate and steal a bounded amount of basic resources"* — CANNOT be satisfied by (a)**, because under banked pressure nothing is taken while away.

⭐ **RECOMMEND (a):** it is shipped, honest, needs no combat sim, and keeps `WaveManager` the single spawn authority. The acceptance line should then be rewritten to *"away time produces a harder homecoming."*

### Q2. Four spawn SIDES, or four damageable GATES?

⛔ **The live hub contains NO `Gate` objects.** `CastleHubBuilder` builds four cardinal **openings** — nav strips and masonry — and **never** calls `AddComponent<Gate>()` (verified: the only production site is `StructureFactory.cs:1133`, driven by the **player-buildable** `gate_stone` row). `CastleDefensePlansService.cs:270` says it plainly: *"The merged hub has no Gate objects."*

- **Sides** work today, at zero structural cost.
- **Damageable gates at four cardinal points is a HUB-LAYOUT change** — ⚠ and `CastleHubBuilder` / `VillageSceneBuilder` is the **serialization bottleneck lane** (§9): one agent at a time, and it is not this ticket's lane.

⭐ **RECOMMEND: sides now, gates later.** "Losing a gate" already has an object to happen to **if the player built one** — which is the more interesting version anyway: defences you chose to build are defences you care about losing.

## 1. ⭐ The escalation IS the work. There is no tuning value for two gates.

`SmartEnemySpawner.SpawnWave` (`:79`) hard-collapses to ONE gate: `WaveSpawnPoint gate = PickGate(spawnPoints, waveId);` (`:102`), where **`PickGate` is `private static`** (`:346`) and returns a **single** marker — `slot = (waveId-1) % gateCount`, rotating N→E→S→W across waves. Everything downstream (origin, heading, lateral fan-out, the completion log) derives from that one marker.

**Four things a multi-gate split must resolve:**

1. **Lift gate selection to the caller.** `PickGate` must stop being private-and-single: either `SpawnWave` takes a gate (or gate-set), or selection moves up. Today the caller cannot influence the gate except through `waveId`.
2. ⛔ **SPLIT the concurrency budget — do NOT duplicate it.** The cap is `_maxSimultaneousEnemies` via `SmartSpawnBudget()` (`WaveManager.cs:2003`) → `BudgetFor(cap, liveCount)` (`:311`). ⚠ **Calling `SpawnWave` twice hands EACH call the full budget and doubles the field**, silently defeating the WO-1113 cap whose entire purpose was **a phone frame-rate cliff**.
3. **Reinforcements must remember THEIR gate.** `DrainSmartReinforcements` re-calls `SpawnWave` with the **same `waveId`** (`:2119`), so `PickGate` returns the **same gate**. Under a split, each held remainder needs its own gate or reinforcements trickle back to one side. ⭐ There are exactly **two** `SpawnWave` call sites: `WaveManager.cs:1966` and `:2119`.
4. **Partition the composition.** `EnemyWaveComposition.Entries` is a flat list; splitting it is arithmetic, but **WHICH roles go to which gate is a design decision this ticket does not make** — and it is the decision that makes two gates *interesting* rather than just *twice as many*.

## 2. Roster — recompute, do not add an accessor

⭐ `WaveCompositionBuilder.Build(waveId, waveHasAuthoredHeavy, catalog, seedSalt)` (`WaveCompositionBuilder.cs:169`) is **`public static` and PURE**, deterministically seeded (`:179`). There is **no public accessor** for the composed roster and **none should be added** — a recomputation is byte-identical to what spawned. ⭐ **`seedSalt` already exists and is the natural per-gate discriminator.** `TryDescribeUpcomingWave` (`WaveManager.cs:3343`) hands out exactly the three arguments `Build` needs.

⛔ `waves.json` `enemies[]` batches are **INERT** — confirmed, and `WaveDataTest.cs:59/:83` **FAILS** on a re-add. ⚠ Three legacy spawn paths still exist behind the smart one (`SpawnComposedFamilyGroups` `:1580`, the flat `SpawnBatch` loop `:1583-1590`, the `WaveEnemyGroup` asset spawner `:1595-1620`) — **do not re-awaken one by accident.**

## 3. The breach hook already exists, and it is a trap as written

⭐ **`Gate.ForceFieldCollapsed` (`Gate.cs:109`) has ZERO subscribers repo-wide** — verified. A live, unused hook.

⛔ **But it is NOT a clean breach signal.** `ApplyForceFieldState` computes `bool up = !_isOpenForHero && IsForceFieldUp` (`:400`), so it **also fires whenever the hero enters a `GateProximityOpener` radius**. ⚠ **A consequence hooked naively fires every time the player walks out of town.** Key on the **HP-fraction edge**, never the combined edge.

⭐ **The right hook for a consequence is `SiegeSession.RecordBreach(Enemy by)`** (`SiegeSession.cs:266`) — `public` precisely so a producer can report a breach without the ring observer, and its own doc names the intended caller: *"or **a real gate-destroyed event**"*.

⛔ **Do NOT hook `WaveManager`'s ring detector**, which WO-1026's plan points at: it sits behind `FeatureFlags.WaveBreachToAtb`, **OFF by default since WO-579** (`WaveManager.cs:2590`). Hooking it records **nothing, forever, silently**. The in-code comment says so, and adds *"(Do not 'simplify' this back.)"*

## 4. Offline — be the FIFTH consumer, never a new clock

`OfflineClaimCoordinator.Claim(reason)` (`:192`) computes **ONE** delta and fans the **identical** `OfflineClaimWindow` to every `IOfflineClaimConsumer`, each `Guard.Try`-wrapped. Four are registered: `OfflineHarvestService` (10 h), `EchoService`, `EchoRepairService` (4 h), and **`SiegeScheduler` (24 h)**.

⭐ **A raid consequence is a fifth consumer, not a new clock.** ⛔ The coordinator applies **no cap for anyone** — each consumer caps its own window.

**Server authority is split, deliberately:** the client clock prefers `ServerClock` (monotonic, so a wall-clock edit cannot move it), and the real authority is the **save-side refusal** `reconcileAccrual` (`api/game/save.js:515`), which clamps a claimed window to server-elapsed time plus grace.

## 5. ⛔ No new theft path, and no damage notification

Both recorded above: the bank-take is **deleted and regression-guarded**, the sanctioned stake is `ResourceCollector.OnSiegeDestroyed` (`RaidLootFraction 0.5`, `_pending` only, **never** the wallet); and there is **no "this took damage" event** — damage is found by a **rescan timer**, and several call sites **self-install a controller at runtime**.

## 6. Instrument first (§12)

Before any behaviour change, one headless hub load must answer what static reading cannot:

- `[CastleSpawnPointInjector] placed N wave spawn points` — ⚠ **are the 20 markers actually present?** The injector **self-suppresses if any `WaveSpawnPoint` already exists.**
- `[Flow:Enemy] SmartSpawner wave N: … gate='spawn-castle-<dir>-<i>'` — which gate actually served.
- `[Flow:Wave] wave N: concurrency cap … HOLDING M` — ⭐ **is there budget to divide at all?** If the cap already binds on a late wave, two gates means **half a wave each**, not two waves.
- Instrument the two edges in `ApplyForceFieldState` **separately**, and read one capture before hanging anything on `ForceFieldCollapsed`.

---

# ⭐ OWNER RULINGS 2026-08-24 — WO-1179 IS NOW BOUNDED AND HANDABLE

Owner's own summary, and it is the spec's north star:
> **"One encounter, one global cap, escalating side count 1 → 2 → 4, with away time banking pressure
> rather than simulating an unseen loss."**

## Q1 — RULED: **banked pressure, NOT absentia resolution**

Away time **schedules and escalates the fight the player returns to**. ⛔ **Nothing is stolen and
nothing is resolved while they are absent.** A true offline combat resolver belongs behind **WO-430-F**
and requires its own stakes ruling.

⭐ **This means the shipped `SiegeScheduler` model IS the answer** — no new offline machinery at all.
It is already an `IOfflineClaimConsumer` with a 24 h cap and `_maxPendingSieges = 1`.

### ⚠ ACCEPTANCE LINE REWRITTEN (the old one could not be satisfied)

- ⛔ **WAS:** *"An offline attack can damage a gate and steal a bounded amount of basic resources only."*
- ⭐ **NOW:** *"Away time BANKS PRESSURE, producing a harder homecoming — a larger and/or
  multi-sided assault that happens LIVE, with the player watching. Nothing is taken while away."*

## Q2 — RULED: **four spawn SIDES now, not four damageable gates**

Use the existing **`WaveSpawnPoint.GateIndex`** data as **side lanes**, ⛔ **without requiring live
`Gate` objects.** Player-built gates may affect the battle naturally — but the feature **must not
require four authored hub gates** and **must not enter the serialization-bottleneck lane** (§9).

## ⛔ CONFIRMED CONSTRAINTS — all six binding

1. ⛔ **NEVER use the nonexistent `SpawnPoint` tag.** It is undeclared and reading it **throws**.
   Resolve by the **`WaveSpawnPoint` component**.
2. ⭐ **DO NOT call `SpawnWave` multiple times. Partition ONE wave's composition across the active
   sides under ONE shared concurrency budget.**
   ⭐ *This is cleaner than the spec's own §1 and supersedes it.* It removes the doubled-field hazard
   by construction rather than by careful arithmetic — **and it dissolves §1.3 entirely**: with a
   single `SpawnWave` call, `DrainSmartReinforcements` re-derives the same side set, so reinforcements
   returning to their own sides is **correct behaviour**, not a bug to engineer around.
3. ⛔ **Do NOT use `Gate.ForceFieldCollapsed` as the breach signal** — it also fires when the hero
   enters a proximity opener, i.e. every time the player walks out of town.
4. ⛔ **Do NOT depend on WO-1026's ring detector** — behind a flag that has been **OFF since WO-579**;
   it would record nothing, forever, silently.
5. **The false §7 canon statement is replaced**; this ticket stays grounded in the **20
   `WaveSpawnPoint` markers** and their **populated `GateIndex`** values.
6. ⛔ **No new theft path** — the bank-take is deleted and regression-guarded; the sanctioned stake is
   `ResourceCollector.OnSiegeDestroyed` (`_pending` only, never the wallet).

## Lead decision — the one design choice the rulings leave open

**How to partition the composition across sides.** Not an owner call; a defensible default:

- ⭐ **Partition BY ROLE so each side is a COHERENT THREAT** — never "all ranged on one side, all
  melee on the other", which makes one side trivial and the other unfair. Each active side receives a
  proportional slice of every role present.
- **Determinism via the existing `seedSalt`** on `WaveCompositionBuilder.Build` — one salt per
  `GateIndex`. ⛔ No new randomness source; the builder is pure and must stay so.
- ⚠ **Remainders go to the FIRST active side** by ordinal, so a 3-enemy split across 2 sides is
  stable run to run rather than drifting with float rounding.

## ⚠ Still instrument first (§12)

The §6 capture list stands, and **one line in it now gates the whole feature**:
`[Flow:Wave] wave N: concurrency cap … HOLDING M`. ⭐ **If the cap already binds on a late wave, two
sides means HALF A WAVE EACH, not two waves** — the escalation would read as *weaker*, not harder.
**Read that capture before tuning anything.**

---

## LANDED 2026-08-25 - `4f4055043`

Side partitioning inside `SpawnWave`. ONE `int budget` local per invocation (`SmartEnemySpawner.cs:123`) decremented inside the side loop (`:253`), and exactly two call sites, both in `WaveManager` - verified at source, since a per-side call would hand each side the full cap. `WaveManager` remains the single spawn authority; WO-1026's ring detector untouched.

⚠ The status line was not flipped in the same commit as the work (CLAUDE.md section 2 / docs/BOARD.md section 2). Corrected here after the pipeline filler caught it - the board advertised finished work as available for several hours, which is the exact failure that got Batch 8 refused.
