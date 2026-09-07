# WORK ORDER 1372 - Troops cost TIME, gold BUYS TIME, and surplus resources sell for gold

> # ✅ RESOLVED 2026-09-04 - THE MAP WINS, AND BOTH RULINGS SURVIVE INTACT.
>
> Owner, three times and in this order: ***"these findings take presedence"***, ***"this is the north
> star map"***, and ***"Make the goal when everything matches what i gave you"***. That settles the
> precedence question this banner was raised to ask. **`docs/PROGRAM_RAID_ECONOMY_2026-09-04.md` is
> the specification.** Troops COST GOLD.
>
> **The two rulings were never actually exclusive - the earlier one is a SPEED-UP, not a price.**
> Read together they compose:
>
> | Axis | Ruling | Source |
> |---|---|---|
> | Troops have a **gold price** | 1,650 for three starters at Camp I | the map §1, and it is what the raid reward is sized against |
> | Troops also take **TIME** | a training clock, one of the map's three clocks (§5) | WO-1372 |
> | **Gold BUYS the remaining time** | *"paying gold is like saying we hired mercenaries"* | WO-1372, owner verbatim |
> | **Surplus resources SELL for gold** | *"players should be able to sell extra resources to get gold, for troop building"* | WO-1372, owner verbatim |
>
> So the sink is the gold price, the clock is the pacing, and mercenary-gold is the impatience tax on
> top - three distinct knobs, not one contested one. Nothing in WO-1372 is discarded except the single
> line *"FREE. Time only."*, which the map supersedes.
>
> ⛔ **The one thing that must NOT happen:** shipping the map's gold table on top of free troops. That
> is a faucet with no sink, and it was the real risk this banner caught.


**Status:** CLOSED 2026-09-06 - owner felt-test PASS (validated 2026-09-07T00:49:56, build 2026.09.07.358574). PRIOR STATUS: FIXED - in build 2026.09.05.355872, installed on the Seeker 2026-09-04 22:22 (versionCode 355872); its regression suite(s) green on the same tree. Awaiting owner felt-test.
**Silo / Lane:** Economy - troop training + vendor sell path + `Resources.Coins`
**Type:** NEW FEATURE + BALANCE RESHAPE, owner-ruled
**Minted:** 2026-09-04 (CLI)
**Severity:** ⛔ **P1 - this is the CORE LOOP, not a convenience.** See §2.

## §1. THE RULINGS - two, in sequence, one conversation

1. ***"players should be able to sell extra resources to get gold, for troop building"***
2. ***"maybe troop building takes time no resources? gold can speed up"***

⭐ **Ruling 2 REFRAMES ruling 1 and is the stronger design.** Read them as one model:

| | Today | Ruled |
|---|---|---|
| Troop cost | **550 gold** (`troop-footman costGold: 550`) | ⚠ **SUPERSEDED - the gold price STAYS** (map, and owner 2026-09-04: *"gold buys hire mercenaries instead of waiting on time"*). A troop costs gold AND takes time; gold spent a SECOND time skips the remaining clock. |
| Gold's role | a hard GATE on owning any troop | a **speed-up**, never a gate |
| Surplus resources | capped, overflow DESTROYED | **sold for gold** |

**This is the Clash shape** - the standing design tie-breaker. Troops cost time, currency buys the
time back, and nobody is ever *locked out* of the core loop by a currency. ⛔ It dissolves the wall in
§2 rather than papering over it.

## §1b. ⭐ THE NARRATIVE FRAME - GOLD DOES NOT "SKIP A TIMER", IT HIRES MERCENARIES

Owner, 2026-09-04: ***"paying gold is like saying we hired mercenaries"***.

**Treat this as the design frame for the whole speed-up, not as decoration.** It is the difference
between a mechanic the player tolerates and one that makes sense in her world:

| | Fiction | Mechanic |
|---|---|---|
| **Train** | Your own people. They take time to become soldiers. | free, time-gated |
| **Hire** | Mercenaries. They are already soldiers - you are paying for that. | gold, immediate |

⭐ **Why this is worth more than a rename:** it answers the question every timer-skip in every game
begs - *"why does money make time move faster?"* Here it does not. **Money does not accelerate
training; it buys someone who is already trained.** The fiction and the mechanic agree, which is the
HP B2B lens applied to design: presentation and behaviour composed, not bolted together.

**What it licenses in the UI** (verbs, not new systems): the Train channel's speed-up CTA reads
**`Hire`** rather than `Finish Now`; the cost line is a mercenary's fee; the completion beat can
acknowledge that these are hired blades, not your townsfolk.

⛔ **AND IT OPENS DESIGN QUESTIONS THAT ARE HERS, NOT OURS.** The frame is adopted; the consequences
are unruled. **Do not answer these in code** - they are in §5:
- Are mercenaries **identical** to trained troops once they arrive, or do they differ (temporary?
  no veterancy? cheaper to lose? a distinct model/nameplate)?
- Does hiring have a **roster or supply limit** - can you buy an entire army, or only fill gaps?
- Does the fiction extend to the **Builder and Research** channels, or is `Hire` unique to Train?
  ⚠ WO-1129's Finish-Now curve is shared; a Train-only verb needs a per-channel label, not a fork.

⚠ **The safest reading, pending her ruling:** adopt the frame for LANGUAGE now, and change no
mechanics beyond §3. A mercenary that behaves differently from a trained troop is a second troop
system, and that is a design decision she has not made.

## §2. WHY THE CURRENT MODEL IS A DEAD END (measured 2026-09-04)

| Fact | Value | Source |
|---|---|---|
| Troops priced in gold ONLY | `troop-footman costGold: 550`, `buildSeconds: 45` | `Assets/Resources/Data/Canonical/troops.json` |
| Gold IS `Resources.Coins` | *"the shop/sell/research wallet"* | `GameStateService.cs:1178` |
| New game starts with | **200 gold** | `GameStateService.cs:1177` (owner ruling 2026-08-26) |
| Gold's ONLY faucet | enemy `coinReward` 10-120 | `enemies.json`; NO gold keys in `quests.json` / `daily-quests.json` |
| Her REAL payouts today | **gold=18, 14, 6** per arena battle | `logs/device/freeze-20260904-095249.log` |

**~30-90 battles buy ONE Footman**, while wood/iron/stone cap out and the surplus is DESTROYED
(WO-1370, WO-1371). Troops gate the army, the army gates `PostureSignals.RaidCapable`, and
`RaidCapable` gates whether the `Raids` face appears on a **4-face** bar at all.

⭐ **The owner's two observations are ONE defect from opposite ends:** *"we have raids but noone knows
about them"* and *"players should be able to sell extra resources"*. **The player drowns in resources
they cannot spend while unable to afford the troops that unlock the main loop.**

⚠ `buildSeconds: 45` **already exists on every troop row** - the time axis is authored. This is a
reshape of existing fields, not a new system.

## §3. THE WORK - three parts, and the middle one is already built

### A. Troops become time-only
⚠ **SUPERSEDED - do NOT remove the gold requirement.** Gold stays the PRICE; the mercenary payment is a SEPARATE, second gold spend that skips the remaining training clock. Original text, kept for the record: ~~Remove gold as a REQUIREMENT to enqueue a troop.~~ ⛔ Do NOT delete `costGold` from the schema - it
becomes the input to the speed-up price (part B) and to the sell economy's balance. **Read-migrate;
never drop a live field.**

### B. Gold buys the remaining time - ⭐ THIS ALREADY EXISTS, DO NOT BUILD IT
The Obsidian queue's **Train channel** already runs troop training, and `ManageScreenPanel` already
renders a **`Finish Now`** CTA with a price and a `WatchAd` button per job (`:1846`, `:1872`).
⛔ **Extend that path to price in GOLD on the Train channel. Do not write a second speed-up.**

⛔ **HARD DEPENDENCY: WO-1368.** The Manage screen currently builds **`queueRows=0`**, so `Finish Now`
never renders at all. **This ruling is unreachable until 1368 lands. Sequence them.**

### C. Sell surplus resources for gold
Extend the EXISTING sell surface - `Village/Hero/ShopVM.cs:227,:586` (`ActionLabel = "Sell"`),
`PartyShopPanelMvvm.cs:1546`, and `BuildSelectionUI.cs:189` (which sells STRUCTURES). ⛔ Find the
right host and extend it; a second sell system is the duplicated-state defect this repo keeps paying
for (CLAUDE.md §2/§5/§16).

⛔ Route the credit through the EXISTING grant seam (`GrantSpendable` or equivalent). Canon records a
live dual-wallet hazard - Wood/Iron pooled in `EconomyService` while others read through to
`GameState` - so **never write `Resources.Coins +=` inline**; a third writer reproduces that bug.

## §4. EVERY NUMBER HERE IS A TUNABLE. BINDING.

Standing rule, owner 2026-09-02: *"be smart, dont make it need a code change, make it tweakable from a
db call"* - **a balance value is a TUNABLE, default answer YES.**

Register on the EXISTING rail: per-resource **sell rate**, the **gold-per-second speed-up rate**, and
any **training-time multiplier**. Four sources change in the SAME commit - `Core/Ops/RemoteTunables.cs`
Registry · `RemoteTunablesService.cs` · `TUNABLE_KEYS` in `api/_lib/tunables.js` · the Command Center
Balance tab - and `[tunable-defaults]` names any two that disagree. ⛔ Do not build a second rail.
Registered defaults must equal today's constants: no row / no network / no parse ⇒ today's behaviour
exactly.

⭐ **Why this matters more than usual:** she is about to re-feel the entire early game. Every value she
wants to move must reach her device in ~40s, not a 10-minute APK round trip.

## §5. OPEN OWNER RULINGS - ⛔ ASK, DO NOT ANSWER

1. **Does `buildSeconds: 45` stay?** With gold no longer gating, 45s may be far too fast to create any
   speed-up demand. **The time curve IS the economy now.**
2. **What does gold buy per second?** Flat, or the convex Finish-Now curve WO-1129 already shipped for
   the Builder channel? ⭐ Reusing that curve is the consistent answer.
3. **Sell rates per resource** - is stone worth the same as iron?
4. **Where does selling happen?** A vendor NPC, the Manage screen, or ⭐ **the HARVEST RESULT overflow
   modal itself** - *"your store is full, sell 90 stone for N gold?"* turns WO-1370's loss message into
   the loop's teaching moment.
5. **Is selling capped or rate-limited?** Uncapped selling makes gold trivial and voids the speed-up.
6. **The mercenary frame's consequences** - see §1b: are hired troops identical to trained ones,
   is hiring supply-limited, and does the `Hire` language extend beyond the Train channel?
7. **Do training SLOTS become the real constraint?** In Clash, barracks capacity and builder count are
   the gate once currency stops being one. `BuildTimerConfig.queueDepthPerLine` is 5, `freeBuildSlots`
   is 2 - ⚠ **never raise concurrency to implement a depth cap** (WO-773).

## §6. ACCEPTANCE

- [ ] A troop enqueues with **zero gold** and completes on time alone.
- [ ] Gold shortens a running troop job through the EXISTING `Finish Now` path on the Train channel.
      ⛔ No second speed-up surface.
- [ ] Surplus resources sell for gold through the EXISTING shop/vendor surface. Name which one, and why.
- [ ] Gold credited through the existing grant seam; no third writer to `Resources.Coins`.
- [ ] All rates live on the tunables rail; `[tunable-defaults]` green; **prove a knob change reaches a
      running client** without a rebuild.
- [ ] ⛔ **Proven from a capture, not a code read**: enqueue at 0 gold, spend gold to shorten it, sell
      stone and watch gold rise. Quote the `[Flow:*]` line for each.
- [ ] **State the measured time-to-first-troop before and after.** That number is the point of the ticket.
- [ ] Owner has ruled §5 and the implementation matches her answers.

## §7. WHAT NOT TO TOUCH

- ⛔ Do not DELETE `costGold` - re-migrate it into the speed-up price. Never drop a live schema field.
- ⛔ Do not change enemy `coinReward` or the storage caps here.
- ⛔ Do not build a second sell surface, gold writer, speed-up, or tunables rail.
- ⛔ Do not start part B before WO-1368 lands - you would be extending rows that are not built.

## §8. RELATED - this ticket is the junction of five
- **WO-1368** - Manage builds ZERO queue rows. ⛔ **HARD BLOCKER for part B.**
- **WO-1370** - the unreadable overflow modal. ⭐ Its screen is the natural home for a sell prompt.
- **WO-1371** - a new game inherits 14,089 resources; why the glut was visible today.
- **WO-1357** - the Journey Raids card locking on a barracks blocker: the far end of this chain.
- **The loops/rewards analysis commissioned 2026-09-04** - read it before finalising any number here.
