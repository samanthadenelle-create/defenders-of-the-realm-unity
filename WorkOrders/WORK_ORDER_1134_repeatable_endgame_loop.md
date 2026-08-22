**Status:** READY TO IMPLEMENT

# WORK ORDER 1134 — The repeatable endgame: give the ratchet somewhere to keep going

**Minted:** 2026-08-21 (CLI, banner bumped 1134 -> 1135 in the SAME edit)
**Lane:** Progression / Raids. **Class:** PRODUCT — the retention answer.
**Owner ruling 2026-08-21:** *repeatable endgame loop*, chosen over authoring more content
and over accepting a short arc.

## THE MEASURED FACT THAT CREATED THIS TICKET

A full economy model (WO-1129 lane, 2026-08-21) proved the target is unreachable by tuning:

- **196 build actions + 26 research perks** is the entire authored sink.
- A committed player exhausts it in **4.0–6.8 days** across four curves from conservative
  to very aggressive.
- The absolute ceiling — *every* action clamped to the 24h maximum, walls included — is
  ~98 days, and that build would be unplayable.
- **Costs and timers are both exhausted as levers.** The cost pass shipped (x4/x10/x14) and
  the timer ladder went 5 -> 7 bands reaching the 24h clamp. Neither moves the total,
  because the binding limit is CONTENT VOLUME, not price or wait.

So: finite content cannot be stretched into 8–12 weeks with waits. Players read that as
padding, and correctly. The answer is a loop that does not run out.

## THE RULING

> Raids, sieges and dungeons already exist and are repeatable — they just do not feed a
> long-tail progression. Wire a repeatable SINK so play has somewhere to go.

Chosen over "author 5x the catalogue" (expensive, every row needs art + costs + balance)
and over "ship the honest 4–7 day arc" (viable, but she wants the tail).

## WHAT ALREADY EXISTS — COMPOSE, DO NOT GREENFIELD

⛔ Read every one of these before designing. Most of the machinery is built.

- **Raids**: deploy -> clear -> claim -> return is LIVE end to end (WO-932 phases 1-5).
  `RaidScoring` computes stars from destruction; `RaidDeployController` owns retreat /
  timeout / casualties; three tiered camps are authored in `scene-configs.json` with
  garrison recipes.
- **PvE siege** (WO-1026, landed today): `SiegeScheduler` opens a session on a cadence and
  drives `WaveManager.ForceBeginNextWave()`; `DefenseOutcomeRecord` persists what broke,
  how long each structure held, and where the first breach was. **Flag-gated OFF.**
- **Dungeons**: composed runs with oil/darkness risk-reward, real loot, a working exit.
- **WO-728 (repeatable raid economy)** is STILL VALID and unbuilt: per-camp cooldown +
  save persistence. That is a PREREQUISITE-shaped piece of this ticket, not a rival.
- **Troops**: `ArmyStorage` loadouts (save v38), training through the Obsidian Train channel.
- **Repairs**: broken structures persist and repair costs are already priced.

## THE DESIGN QUESTION TO ANSWER

**What does a player spend, and what do they get, on the 200th day?**

A repeatable loop needs a sink that consumes and a reward that ratchets, and neither may be
infinite in a way that trivialises the authored content. Candidate shapes, all using systems
above — pick and argue, do not build all of them:

1. **Troop attrition + escalating raid tiers.** Raids cost troops; troops cost resources and
   TIME (the Train channel is the existing pacing device). Camp difficulty escalates with
   clears. The sink is recurring, the reward is stars/loot, and the timer ladder we just
   extended becomes the pacing spine rather than a one-time gate.
2. **Defence repairs as the sink** (pairs with WO-1026's visible-scar stakes recommendation).
   Sieges break structures; repairs cost resources; the cadence is already built.
3. **Prestige / seasonal reset of a bounded subsystem** — the classic answer. ⚠ Highest risk:
   it can invalidate a player's build, and it needs an explicit owner ruling on what resets
   and what is kept. Do not assume it.

## HARD CONSTRAINTS

- ⛔ **NO INFINITE FAUCET.** An Echo produces 5/5s; the storage caps and the sink curve
  (WO-1129) are the spine. A repeatable loop that pays unbounded resources destroys both,
  and the sink-cap oracle exists to catch exactly that class.
- ⛔ **NEVER SELL POWER.** The covenant: convenience and beauty, never combat power. A
  repeatable loop must not become a pay-to-win rail.
- **Save schema is v38** and additive default-on-read fields need no bump. A bump on a LIVE
  published game is an OWNER decision — stop and ask rather than bumping.
- **Reuse the single authorities**: `WaveManager` is the only attacker, `WaveDamageReport`
  the only damage aggregator, `TierForCost` the only duration source, `BattleLock` the only
  combat-state gate. Do not mint a rival to any of them.
- Anything the owner has not ruled — reset rules, loss stakes, escalation numbers — is
  **named and left**, not invented. WO-1026's stakes are still parked for exactly this reason.

## ACCEPTANCE

- A player with every building maxed still has a reason to play tomorrow, and that reason is
  stated in one sentence.
- The loop consumes something real and pays something real; both are bounded.
- No new currency (glimmer was stripped today precisely because it was not real).
- Regression pinning the bound: the loop cannot pay more than the sink can absorb.
- Owner felt-check: does day 30 feel like progress or like a treadmill?

## NOT IN SCOPE

Async PvP, matchmaking, trophies, shields, base snapshot export (WO-730). Ghost-PvP remains
a later SOURCE SWAP on WO-1026's record, not a system to build here.

---

# THE PROPOSAL — delivered 2026-08-21 (read-only design agent)

## RECOMMENDATION: shape 1 (troop attrition + per-camp cooldown)

Chosen because it is the only shape whose sink and reward are **already disjoint in shipped
code**, so the bound needs no invention:

> `RaidScoring.ComputeLoot` returns `new ResourceCost(food, crystals)` and nothing else.
> **Raids pay ZERO wood and ZERO iron.** Every troop costs wood + iron + food (`troops.json`).
> The loop therefore CANNOT FUND ITS OWN INPUT. That is the structural bound, and it shipped.

**The one-sentence acceptance answer:** *"My town is finished, so I raid - because raiding is
the renewable source of the crystals that buy the Cathedral and the queue slots, and my warband
is the thing that gets spent to get them."*

Satisfies the covenant exactly: the loop pays into CONVENIENCE (the Echo-gated 250-crystal queue
slot) and BEAUTY (the crystal-priced ladders the sink-cap oracle exempts) - never power.
Veterancy (max x1.30) is earned by playing well and is not for sale.

## THE BOUND - three layers, two already true

1. **Structural (shipped):** raids pay no wood/iron; training costs both. Net-negative by design.
2. **Already capped:** food is one of the three capped resources; 34,000 with a L6 container.
3. **MUST BE BUILT: the crystal bound.** Crystals are uncapped by design. With no cooldown,
   three camps at ~3 min each is an unbounded crystal faucet - and crystals buy instant-finish,
   which **defunds the very timer ladder that paces the game**. This is why the WO-728 cooldown
   is a PREREQUISITE, not a nicety: clears/day x loot/clear is the daily crystal ceiling.

## THE TREADMILL FAILURE MODE - named, so it can be avoided deliberately

**Escalation that scales reward in lockstep with difficulty.** Garrisons already scale to the
player (`enemyLevel = max(baseEnemyLevel, playerLevel + levelOffset)`). If the clear ladder also
lifts `rewardMultiplier` proportionally, day 200 is arithmetically identical to day 30 with
bigger numbers on both sides. **This is the DEFAULT outcome if nobody decides otherwise.**

Two guards:
- **Reward escalation sub-linear to difficulty, and the ladder TERMINATES.** `TribeManager`
  already has the terminating precedent (`ClearsUntilGone`, `RespawnReductionPerClear`) - a
  cleared tribe returns smaller and eventually stops. Without a terminal rung the loop ends by
  becoming unwinnable, which is worse than ending.
- **The ratchet lives OUTSIDE the loop.** Stars and loot are the loop's FUEL; they cannot also
  be its PROGRESS. Day 30 feels like progress only if the player can point at a building or a
  queue slot that raiding bought.

## REJECTED

- **Shape 2 (repairs) - REJECT as the loop, ADOPT as the stake.** It pays nothing ("you don't
  lose your town" is a stake, not a reward), and its sink is already drained PASSIVELY by
  `EchoRepairService` with no player decision in it. But once loss stakes are ruled, the repair
  bill becomes the consequence half of shape 1 at zero extra design cost.
- **Shape 3 (prestige) - REJECT.** No supporting machinery; would need an owner ruling on what
  resets on a LIVE published game where a wrong answer invalidates a real player's build, and
  would likely force a schema bump. Not costed.

## CORRECTIONS TO THIS TICKET'S OWN NUMBERS

- "26 research perks" is wrong: `building-tiers.json` is 6 buildings / **26 TIERS** / **17 PERKS**.
  Magnitude unchanged; say it correctly next time.
- "WO-728 is unbuilt" was true when the audit ran and is **now FALSE** - `RaidCooldownService.cs`
  + `RaidCooldownRecord.cs` are in the tree as of 2026-08-21, server-anchored via `TimeSource`.

## OWNER-OWED NUMBERS (nothing below is invented)

1. Cooldown hours per camp: Regular / Hard / Extreme. **This IS the crystal bound.**
2. Attrition: `_recoverySeconds` is **120s flat** today. At 120s attrition is effectively free
   and there is no loop. Does it scale with camp difficulty, and to what?
3. Does the clear ladder terminate, and where?
4. Does reward escalate with the ladder at all? Linear = treadmill by construction.
5. Loss stakes (parked from WO-1026) - not needed to ship, but shape 1 is better with it.

**No schema bump requested. No new currency. Nothing here sells power.**

---

# OWNER RULINGS — 2026-08-21 (answering the five open numbers above)

**1. COOLDOWN: Regular 4h / Hard 8h / Extreme 12h.** This is THE crystal bound. At
Extreme/3-star/100% razed a clear pays 121 crystals, so 12h = ~2 clears/day = ~242 crystals/day,
sitting alongside the 200-350/day committed income the WO-1129 model measured. Roughly DOUBLES
endgame crystal income without trivialising the 45,690-crystal content ladder. ⛔ Do not shorten
this without re-deriving the daily ceiling - crystals buy instant-finish, so a shorter cooldown
defunds the timer ladder that paces the entire game.

**2. ATTRITION SCALES WITH CAMP DIFFICULTY** - Regular ~5min / Hard ~20min / Extreme ~45min,
replacing the flat 120s at `RaidDeployController.cs:74`. Derived from authored troop
`buildSeconds` (270-600s/unit) so recovery is meaningfully cheaper than retraining but never
free. At 120s flat there is no sink and therefore no loop.

**3. REWARD ESCALATION: SUB-LINEAR, AND THE LADDER TERMINATES.** Difficulty may climb faster
than reward, never in lockstep. Copy `TribeManager`'s terminating precedent (`ClearsUntilGone`,
`RespawnReductionPerClear`): after N clears a camp stops escalating, so the loop can never become
unwinnable. ⛔ Linear escalation is a treadmill BY CONSTRUCTION and is the default outcome if
nobody decides otherwise - it is now decided otherwise.

**4 + 5 (ladder terminus, loss stakes)** remain owner-owed but neither blocks shipping shape 1.
Loss stakes stay parked from WO-1026; shape 1 is better with them and does not need them.

**Prerequisite status:** WO-728 is IN FLIGHT as of this ruling - `RaidCooldownService.cs` +
`RaidCooldownRecord.cs` are in the tree, server-anchored through `TimeSource` (never
`DateTime.UtcNow`), with the backwards-clock refuse-don't-punish pattern from WO-912 §7.3.

---

# OWNER RULING — the ladder terminus (2026-08-21, verbatim)

**PER-CAMP TERMINUS, not a flat count:**

| Camp | Cooldown | Escalates for | Days to master at max engagement |
|---|---|---|---|
| Regular | 4h (6 clears/day) | **12 clears** | ~2 days |
| Hard | 8h (3 clears/day) | **18 clears** | ~6 days |
| Extreme | 12h (2 clears/day) | **24 clears** | ~12 days |

- **After the terminus, camps REMAIN REPEATABLE at capped difficulty. They do NOT disappear.**
  ⛔ This is where the design diverges from `TribeManager`'s `ClearsUntilGone`: copy the SHAPE of a
  terminating ladder, NOT the vanishing. A camp that disappears removes the loop the ticket exists
  to create.
- **Rewards grow MUCH SLOWER than difficulty and STOP GROWING AT THE SAME TERMINUS.** Both curves
  stop on the same clear count, so there is never a rung where difficulty has moved and reward has not.

Owner's stated intent: *"a visible accomplishment without creating an endless stat treadmill."*
The 2 / 6 / 12-day mastery arc is the deliberate product of the ruled cooldowns above - if a
cooldown is ever retuned, the mastery arc moves with it and the terminus counts must be re-derived.
