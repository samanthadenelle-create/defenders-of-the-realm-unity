# WO-1179 - Roaming troops that attack the town, escalating in size and smarts

**Status:** READY - all three open design questions RULED by the owner 2026-08-24 (existing wave system; offline towns CAN be attacked; losses are REPAIRABLE and bounded). ⛔ Build WO-513 first so pack behaviour is inherited, per this ticket's own note.
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

- `SpawnPoint` tags are already placed **12m outside each gate** (CLAUDE.md §7) - the four attack
  origins exist.
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
