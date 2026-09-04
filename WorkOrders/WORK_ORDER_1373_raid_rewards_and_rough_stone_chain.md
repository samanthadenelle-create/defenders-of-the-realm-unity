# WORK ORDER 1373 - Raids pay big, and drop rough stone: the Jeweler chain closes the loop

**Status:** READY TO IMPLEMENT - ⛔ **BLOCKED ON ONE OWNER RULING** (§4, the exclusivity reversal)
**Silo / Lane:** Economy / raid rewards + loot tables + the Jeweler chain
**Type:** NEW FEATURE + BALANCE, owner-ruled
**Minted:** 2026-09-04 (CLI)
**Severity:** ⛔ **P1 - this is the CORE LOOP CLOSING.** See §2.

## §1. THE RULINGS

Owner, 2026-09-04, in sequence:

1. ***"we can add high percentage of rewards for successful raids, and higher tiers off rough stone drop"***
2. ***"its used to take to jeweler"***
3. ***"the make rings of power and necklace"***

## §2. ⭐ WHAT THIS ACTUALLY DOES: IT CLOSES THE CLASH SPINE, WITH AN RPG PAYOFF

```
   RAID  ->  rough stone  ->  JEWELER  ->  rings & amulets of power
     ^                                              |
     |                                              v
   harder targets  <-  stronger hero  <--------------
```

**That is the missing return arc.** The owner's earlier observation - *"we have raids but noone knows
about them"* - is a discoverability symptom of a loop that had no PAYOFF worth discovering. Raiding
currently pays resources the player is already drowning in (WO-1370/1371) and gold at 6-18 a battle
(WO-1372). **This gives raiding a reward nothing else in the game can produce.**

⭐ **AND ALMOST ALL OF IT IS ALREADY BUILT.** This is wiring plus balance, not construction:

| Piece | Status | Source |
|---|---|---|
| Rough stone item | **EXISTS** - `ing_rough_stone` | `Core/Catalog/DungeonExclusiveItems.cs:49` |
| Polish grade carried run -> bench | **EXISTS** (FIFO, PlayerPrefs) | `Core/Catalog/DungeonRunPayout.cs` |
| Polishing | **EXISTS** | `Village/Crafting/JewelPolishService.cs` |
| Refined gems | **EXIST** - ember crystal, aether shard, heartstone crystal | `DungeonExclusiveItems.cs:52-54` |
| Jeweler TIER-UP recipes | **EXIST** - base accessory + gems -> **higher-rarity accessory** | `jeweler-recipes.json` (`_note`, WO-553) |
| The accessories themselves | **EXIST** - 10 rows: `ring_iron`, `ring_steadfast`, `ring_embercoil`, `ring_heartward`, ... | `accessories.json` |
| Equipping + persistence | **EXISTS** - `equippedRingId` / `equippedAmuletId`, save **v26** | `SaveSchema` |
| Raid loot grant seam | **EXISTS** - single end-of-raid grant | `RaidVictoryController.GrantLoot` -> `RaidScoring.ComputeLoot` |

⛔ **So do NOT build a gem system, an accessory system, a polish system or a second loot grant.**
The work is: make raids DROP the stone, scale it by tier, and tune the payout.

## §2b. ⛔ OWNER RULING - THE RAREST RINGS ARE GAME-CHANGERS, NOT +2% STAT BUMPS

Owner, 2026-09-04: ***"I want the rewards on rings to be game changers at rarest ones"*** /
***"they need something to climb towards"***.

**This is the design intent for the whole ladder and it constrains the accessory table, not just the
drop.** The top of the chain must be worth months of climbing:

- ⛔ **A rarest-tier ring must CHANGE HOW THE HERO PLAYS**, not add a percentage. Percentages are what
  the low tiers are for. If the best ring in the game reads *"+8% attack"*, the ladder has no summit
  and the loop has no destination.
- ⭐ **"Something to climb towards" is a RETENTION requirement, stated in her own words.** Project
  memory already records retention as the business problem. This is the concrete instance: the reason
  to open the app on day 30.
- ⚠ **The rarest tier should be RARE.** A game-changer everybody has by week one is a balance patch,
  not a summit.

### ✅ RULED 2026-09-04 - THE THREE GAME-CHANGER EFFECTS

Owner, verbatim: ***"game changeers should be one cuts builder speed by 20% another adds 20% effort to
all pets (yield) another reduce troops ready time by 25%"***.

| # | Effect | Hits | Existing seam |
|---|---|---|---|
| 1 | **Build times -20%** | every Builder-channel job, forever | `BuildTimerConfig` / `BuildTimerService` |
| 2 | **All Echo yield +20%** | the dominant faucet in the game | `EchoBonusCalculator` / `EchoService.RatePerSecond` |
| 3 | **Troop ready time -25%** | every Train-channel job | `troops.json buildSeconds` / Train channel |

⭐ **WHY THESE ARE THE RIGHT SHAPE, recorded so nobody "simplifies" them into stat bumps later:** all
three are **permanent, economy-wide multipliers on TIME and YIELD**. They compound with everything the
player already owns, they are felt in every session rather than in one fight, and they get *better* as
the town grows. That is what makes a summit worth climbing for months - and it is precisely what a
`+8% attack` ring can never be.

⚠ **THEY ALSO LAND ON THE GAME'S REAL PROBLEM.** `BuildTimerConfig.cs:85-93` measures the whole
content ladder at **4-7 days** against an 8-12 week target. Ring 1 and ring 3 attack that directly;
ring 2 attacks the storage-overflow economy. **These are not side rewards - they are pacing levers
handed to the player as a prize.**

⛔ **THREE IMPLEMENTATION WARNINGS - each would silently void a ring:**

1. ⛔ **RING 2 MAY DO NOTHING TODAY.** The loops audit flagged (NOT PROVEN, needs a runtime capture)
   that `EchoService.RatePerSecond` (`:136`) **does not call `AggregateHarvestMultiplier()`** despite
   its own doc saying it does - which would make every specialization bonus in `echoes-balance.json`
   inert on the silo path. **If that is true, a +20% Echo ring is a no-op that ships green.**
   **VERIFY THAT SEAM BEFORE BUILDING RING 2**, and prove the ring's effect with a captured yield
   delta, not a code read.
2. ⚠ **RING 3 INTERACTS WITH WO-1372.** That ruling makes troops time-only with gold buying the time
   back. A -25% ready-time ring then stacks with a gold purchase - **decide the order of operations**
   (does the ring shorten the base before gold prices the remainder, or after?) or the two features
   will disagree about what a second costs.
3. ⚠ **RING 1 STACKS WITH THE INSTANT-FINISH CURVE** (WO-1129, convex). A -20% base time changes what
   every crystal buys. Not a blocker - but the two must be tuned together, not separately.

⛔ **ALL THREE PERCENTAGES ARE TUNABLES** (standing rule 2026-09-02). Register 20 / 20 / 25 on the
existing rail with those values as defaults; she will want to feel them and move them without a
rebuild.

⚠ **STILL OPEN, and she has not ruled it:** can a player wear more than one at once, and do they
stack? Three permanent multipliers on one hero is a very different game from one. **ASK.**

---

⛔ **WHAT A "GAME CHANGER" IS, IS HER CALL - DO NOT INVENT ONE.** Shapes that qualify, offered only so
the question is askable: a new ability or a second charge on an existing one; a rule change (revive
once per raid, no ammo cost, cleave on the primary); a resource rule (Echo yields double, no storage
cap on one resource); a raid rule (an extra deploy slot, keep loot on a failed raid). ⚠ Each of those
is a mechanic, not a number - which is exactly why she has to pick.

**What this ticket must therefore deliver alongside the drop:** the accessory ladder in
`accessories.json` needs a TOP that justifies the climb. Today it holds 10 rows (`ring_iron`,
`ring_steadfast`, `ring_embercoil`, `ring_heartward`, ...) - ⚠ **NOT PROVEN that any of them is a
game-changer; read their actual effects before assuming the ladder needs new content rather than
re-tuning.**

## §3. ⛔ THE COLLISION - AND IT IS THE WHOLE REASON THIS TICKET IS BLOCKED

**Rough stone is currently DUNGEON-EXCLUSIVE BY DESIGN, and an oracle enforces it.**

`Assets/_Modules/Core/Catalog/DungeonExclusiveItems.cs:42-44`, verbatim:

> *"Item ids that may only ever enter the player's inventory by descending - never sold by a vendor,
> never bundled in a purchasable pack, never granted as a quest/stake payout."*

Enforced by **`Assets/Editor/Regression/DungeonGemExclusivityRegression.cs`**, and consumed by
`VendorStockResolver` (shelf filtering) and `VillageInventory`.

⛔ **Making raids drop rough stone REVERSES a deliberate prior design decision. It is not a bug fix,
and it must not be done by quietly deleting a regression.** That invariant exists to give dungeons a
draw nothing else has. Removing it without replacing that draw makes dungeons strictly worse.

## §4. ⛔ THE OWNER RULING THIS TICKET IS BLOCKED ON

**Which shape do you want?** Each preserves the raid payoff; they differ in what happens to dungeons.

**(A) Raids drop rough stone directly.** Simplest, and literally what was said. ⚠ Requires retiring or
re-pointing `DungeonGemExclusivityRegression`, and **dungeons lose their exclusive draw** unless
compensated (e.g. dungeons drop it at a materially better rate or grade).

**(B) Raids drop rough stone, but dungeons keep a GRADE advantage.** `DungeonRunPayout` already
carries a per-stone **polish score** from the run that produced it. So raids could pay *stones*, while
dungeons pay *better stones*. ⭐ **This preserves both loops and reuses a mechanism that already
exists** - it is the shape the existing code most naturally supports.

**(C) Raids drop a DIFFERENT tier-up material**, leaving rough stone untouched. Safest for dungeons,
but it is a new material family and more work than (A) or (B).

⚠ **Recommendation, offered not assumed: (B).** It honours the ruling, keeps dungeons meaningful, and
reuses the grade mechanism rather than deleting an invariant. ⛔ **But this is a design call and it is
hers. Do not implement until she has picked.**

## §5. THE OTHER OPEN RULINGS - ASK, DO NOT ANSWER

1. ✅ **RULED 2026-09-04 - THE RAID PAYOUT LADDER IS 25 / 50 / 70.**
   Owner, verbatim: ***"25% normal run 50% hard 70% hardest"***.

   | Camp | difficulty mult | ruled payout |
   |---|---|---|
   | `raider_camp_small` | 1.0 | **25%** (normal) |
   | `fortified_garrison` | 1.5 | **50%** (hard) |
   | `mage_enclave` | 2.2 | **70%** (hardest) |

   ⭐ **This is the fix for the 120x inversion**, and the shape is right: a PERCENTAGE scales with the
   player's own economy, so the raid stays relevant at every stage - which a flat `_lootFoodBase 60`
   never can. It is also the Clash shape (you take a share of what is there, not a fixed pile).

   ⛔ **ONE THING NEEDS HER WORD BEFORE IMPLEMENTATION: 25% OF WHAT?** Do not guess this - the same
   number means wildly different things:
   - **(i) Of the player's own storage CAPACITY.** Caps are 2,000 per resource today
     (`storage-caps.json`), so 25% = 500 per resource per raid, vs the 120 food a perfect raid pays
     now. Scales automatically as containers are upgraded. ⭐ **Most likely reading, and the one that
     matches "high percentage" against a 4h cooldown** - but still a guess until she says so.
   - **(ii) Of the CAMP's notional holdings.** Truest to Clash, but these are baked PvE scenes with no
     authored bank - **something would have to author one per camp**, which is real new work.
   - **(iii) Of the player's CURRENT balance.** ⛔ Pathological - pays nothing when you are empty and
     most when you are already full, which is exactly backwards.

   ⚠ **AND IT MUST PAY THE RIGHT CURRENCIES.** The audit proved raids pay food + crystals ONLY, zero
   wood/iron/gold, while troops cost gold and upgrades cost wood+iron - *"the raid loop structurally
   cannot fund its own input"* (`RaidCooldownService.cs:84-87`, that file's own comment). **The
   percentage must land on wood, iron and gold too**, or the ladder is a bigger number on the wrong
   axes and the loop still does not close.
2. **Does the drop scale by raid TIER, by STAR rating, or both?** `RaidScoring` already computes
   stars. ⭐ Stars are the natural axis and already exist.
3. **Drop RATE vs GUARANTEED.** A guaranteed stone per raid is legible; a rare drop is exciting.
   ⚠ Clash's answer is guaranteed loot + rare bonus.
4. **Does a FAILED raid pay anything?** WO-728 already ships per-camp cooldowns and difficulty-scaled
   attrition; a total loss on failure plus a cooldown may be too punishing for a loop we are trying to
   make attractive.
5. **`siege-stakes.json` carries an `_ownerPending` block** - the theft FLOOR and CAP are explicitly
   placeholders awaiting her ruling. ⚠ That is the LOSS side of the same economy. **Worth ruling in
   the same sitting so gains and losses are set against each other**, not months apart.

## §6. EVERY NUMBER IS A TUNABLE. BINDING.

Standing rule (owner, 2026-09-02): **a balance value is a TUNABLE, default answer YES.** Register the
raid payout multipliers, the rough-stone drop rate/count, and any tier or star scaling on the EXISTING
rail - `Core/Ops/RemoteTunables.cs` Registry, `RemoteTunablesService.cs`, `TUNABLE_KEYS` in
`api/_lib/tunables.js`, the Command Center Balance tab - **all four in the SAME commit**, with
`[tunable-defaults]` naming any two that disagree. ⛔ Do not build a second rail. Registered defaults
must equal today's constants: no row / no network / no parse => today's behaviour exactly.

⭐ This matters more here than anywhere: **she is setting the reward curve for the game's main loop by
feel.** Every value must reach her device in ~40s, not a 10-minute APK round trip.

## §7. ACCEPTANCE

- [ ] A successful raid pays materially more than the equivalent time spent farming.
      ⛔ **State the measured ratio** - raid payout vs collector yield over the same wall-clock. That
      number IS the ticket.
- [ ] Rough stone drops from raids per the ruled shape (§4), scaled per the ruled axis (§5.2).
- [ ] The stone polishes at the Jeweler and tiers up an accessory, **end to end, proven by capture** -
      quote the `[Flow:*]` lines from raid grant -> inventory -> polish -> accessory granted.
- [ ] ⛔ `DungeonGemExclusivityRegression` is **re-pointed to the new ruled invariant, never deleted**
      (the WO-1159 precedent: a ruling moved, so the pin was re-pointed and made STRICTER).
- [ ] Dungeons still have a reason to exist. **Say what it is**, in one sentence, in the RESULT file.
- [ ] All payout numbers live on the tunables rail; `[tunable-defaults]` green; **prove a knob change
      reaches a running client** without a rebuild.
- [ ] Owner has ruled §4 and §5 and the implementation matches her answers.

## §8. WHAT NOT TO TOUCH

- ⛔ Do not build a new gem, accessory, polish or loot-grant system. All four exist.
- ⛔ Do not delete `DungeonGemExclusivityRegression`. Re-point it.
- ⛔ Do not edit `loot-tables.json` blind - `jeweler-recipes.json`'s `_note` warns that boss-gem loot
      wiring is owned elsewhere. Find the current owner before editing.
- ⛔ Do not tune `siege-stakes.json`'s theft numbers here - they are a separate owner ruling (§5.5).

## §9. RELATED - the loop this completes
- **WO-1372** - troops cost time, gold buys time. The ARMY side of the same loop.
- **WO-1368** - zero queue rows. ⛔ Blocks WO-1372's speed-up, not this ticket.
- **WO-1370 / WO-1371** - why the player is drowning in resources that mean nothing.
- **WO-1357** - the Journey Raids card that locks on a barracks blocker: the discoverability end.
- **The loops/rewards analysis commissioned 2026-09-04** - ⚠ **read it before setting any number here.**
