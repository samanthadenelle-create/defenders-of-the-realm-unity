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
