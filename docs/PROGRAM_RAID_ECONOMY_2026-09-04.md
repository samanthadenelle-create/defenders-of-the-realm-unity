# PROGRAM — Close the raid loop (owner design direction, 2026-09-04)

> # ⭐ THIS IS THE NORTH STAR MAP. IT TAKES PRECEDENCE.
>
> Owner, 2026-09-04: ***"these findings take presedence"*** / ***"this is the north star map"***.
>
> **Two things follow, and both are binding:**
>
> 1. ⛔ **PRECEDENCE.** Where this document conflicts with ANY earlier ruling, design note, work order
>    or canon line, **this document wins.** That includes rulings the same owner made earlier the same
>    day — see §12.1, where the 25/50/70 payout ladder is now formally SUPERSEDED.
> 2. ⭐ **NORTH STAR.** This is no longer one programme among many; it is **the map the roadmap is
>    measured against.** A ticket that does not serve `Collect -> Train -> Raid -> Get richer ->
>    Upgrade -> Unlock harder raid -> Get stronger -> Repeat` is, by definition, not on the critical
>    path — and should be able to say why it is being worked anyway.
>
> ⚠ **Load-bearing, per CLAUDE.md §15:** this file joins the read-first canon set. Any change to the
> loop, the reward table or the release order updates it IN THE SAME COMMIT, or adds a dated `STALE:`
> banner naming what is now wrong.

> ## ⭐ COMPANION CANON — the fiction that makes these numbers mean something (added 2026-09-04)
>
> `docs/CREATIVE_CANON_ELARION_2026-09-04.md` records the owner's creative direction of the same day.
> **The division is clean: this file rules NUMBERS, ladders and release order; that file rules FICTION,
> naming and copy.** They do not overlap, and the direction **changes not one economy ruling here.**
>
> ⛔ **But it DOES rename things this document names**, so read it before implementing any string:
> "Raid Orders" is retired in favour of **Heartfire** (a CHARGE, never a currency — §3 is not
> violated); the weekly ladder becomes the **Realm Vigil**, leaving `Threat` to the Iron Bastion
> ladder; the camps become **The Forsaken Camp / The Broken Garrison / The Veiled Enclave / The Iron
> Bastion**; and §2's free starter army is announced with authored copy instead of a placeholder.
>
> ⭐ It also supplies the reason §1's deliberately generous **15–20% failure payout** exists, which
> this document never had a fiction for: **you do not lose the memory, you fail to reclaim it.**

**Status:** CANONICAL · NORTH STAR · takes precedence. This document is the single home for the
design; WO-1374 / 1375 / 1376 execute it and **point at this file rather than restating it**
(CLAUDE.md §2/§5/§16 — a number copied into a second doc is a defect waiting for a date).

**Origin:** owner design direction delivered 2026-09-04, in response to the loops/rewards audit
(`https://claude.ai/code/artifact/af7d0e34-4fb0-42a7-86df-2a61967d5cdd`).

---

## §0. THE THESIS

> *"I would not solve this by spraying more loot everywhere. You have enough systems already. The
> better move is to make the systems you already built feed each other."*

**Target loop:**

```
Collect -> Train -> Raid -> Get richer -> Upgrade -> Unlock harder raid -> Get stronger -> Repeat
```

**Current loop:**

```
Collect -> Upgrade -> Collect -> Upgrade -> maybe raid for crystals -> shrug
```

> *"That second loop has the nutritional value of packing peanuts."*

⭐ **The framing that should govern every ticket in this program:** *"You don't have a 'my game has
nothing to do' problem. You have a 'the game isn't connecting the things it already has' problem."*
Raids, troops, buildings, research, waves, dungeons, quests, Season Pass, Realm Map and timers all
exist. **Several gears are spinning independently. Connect them.**

⛔ **THE RULE A GOOD RAID MUST SATISFY:**
> *"A good raid should fund the next raid + contribute meaningfully toward an upgrade + provide a
> bonus resource."*

---

## §1. THE RAID REWARD TABLE (perfect 3★ / 100%, Camp I)

| Reward | Current | Target |
|---|---|---|
| Wood | 0 | **1,800** |
| Iron | 0 | **1,100** |
| Food | 120 | **3,000** |
| Gold | 0 | **2,200** |
| Crystals | 55 | **20–30** |

**The reasoning, preserved because it is the spec:** four-hour passive output is ~2,880 wood /
1,728 iron / 14,400 food, so the raid pays **60–65% of four hours of wood/iron production instantly,
without making collectors worthless**. And three starter troops cost 1,650 gold, so a 2,200 win is
**+550 gold of advancement** — *"now the player can actually raid again."*

⛔ **CRYSTALS GO DOWN, NOT UP.** *"Crystals are timer compression. If raids dump huge amounts of
crystals, you accidentally accelerate the already-too-short progression curve."* This is the one
number in the table that DECREASES.

### Performance scaling

| Result | Payout |
|---|---|
| Failed attack | 15–20% |
| 1★ | 50% |
| 2★ | 75% |
| 3★ | 100% |
| 3★ + 100% destruction | **110%** |

> *"Now getting better at raiding has an economic payoff."*

⚠ **A failed attack still pays 15–20%.** That is deliberate — it keeps a loss from being a dead end.

### Gold is sized to the army it replaces

> *"For every raid tier, the gold reward should be roughly **125–140% of the expected army replacement
> cost** — not the player's ACTUAL army cost, because that could be gamed. Each camp gets a designed
> army-cost target."*

| Raid | Expected army cost | Perfect gold |
|---|---|---|
| Camp I | 1,650 | 2,200 |
| Camp II | 2,300 | 3,100 |
| Camp III | 3,300 | 4,500 |
| Iron Bastion | 4,800 | 6,500 |

⛔ **THE MISSING ARROW, named explicitly:** *"You currently have Gold → troops but not troops → raids
→ gold. That arrow has to exist."*

---

## §2. THE FIRST ARMY IS FREE

> *"A player starts with 200 gold but needs 1,650 to participate in the thing you're trying to teach
> them. That's basically putting a nightclub behind a velvet rope and handing the player twelve cents."*

**On Barracks completion, grant 3 free Footmen (a starter raid squad).** Then the FTUE says:

> *Your army is ready. Journey → Raids.*

**The first raid must happen within MINUTES of unlocking Barracks, not hours.** After the victory:

> *Victory! Raids reward Gold, Wood and Iron. Use them to train troops and strengthen your realm.*

⭐ *"One raid teaches the entire economy."*

---

## §3. ⛔ DO NOT ADD ANOTHER CURRENCY

> *"No Raid Tokens, War Coins, Battle Marks, Raid Essence, Conquest Pebbles™. You already found
> Voidshards floating around the codebase with no job. Use the currencies you have."*

| Resource | Identity |
|---|---|
| Wood | construction |
| Iron | construction / advanced units |
| Food | passive economy / support |
| **Gold** | **army economy** |
| Crystals | premium / time convenience |

Raids primarily generate **Gold + Wood + Iron**, crystals as a bonus, food supplementary.

---

## §4. RAIDS MUST ESCALATE

> *"You currently have three camps. The player eventually says: I've already beaten those."*

| Tier | Target | Unlock |
|---|---|---|
| Raid I | Camp 1 | immediately after Barracks |
| Raid II | Camp 2 | after **3** victories |
| Raid III | Camp 3 | after **10** victories |
| Raid IV | **Iron Bastion** | after **20** victories |

⭐ `RaidBase_IronBastion.unity` is already baked and tooled — it is in neither `scene-configs.json` nor
Build Settings. **It is sitting there begging to be used.**

**Then do not stop — Threat Levels:** Iron Bastion I / II / III / IV… with **+8% enemy strength** and
**+5% loot** per level. *"Something that simple can create a lot of runway."* Cap by progression tier.

⛔ **You do not need PvP to make this loop work.**

---

## §5. THREE CLOCKS, NOT ONE

> *"Your comeback loop should operate on three clocks. This is the part I think will make the biggest
> difference to retention."*

**Every few hours — RAID ORDERS, and they STACK.**
> *"Instead of forcing COME BACK IN EXACTLY FOUR HOURS: Raid Orders 0/3. One restores every four
> hours. 4h = 1, 8h = 2, 12h = 3, stops at 3. Now somebody sleeping or working isn't punished."*
On return: **3 Raid Orders Ready** → instant activity.

**Daily — First Victory Bonus:** +50% gold, +25% resources on the first raid win of the day.
Plus a **Daily War Chest**: complete any 3 of {win a raid, get 2★+, train 3 troops, collect resources,
complete a build, clear a dungeon}. ⛔ *"Not: raid six times or lose your streak. Keep it flexible."*

**Weekly — Realm Threat ladder**, Threat 1→10, climb as high as you can, resets weekly:

| Level | Reward |
|---|---|
| 1, 2, 4, 6, 8 | resources |
| 3, 7 | small chest / chest |
| 5, 9 | crystals |
| **10** | **large weekly chest** |

> *"Now someone who finishes your build tree still has: how high can I clear this week? That is
> infinitely healthier than trying to stretch 196 building actions across twelve weeks with ridiculous
> timers."*

**Monthly — the Season Pass.** So the cadence becomes **4 hour → daily → weekly → monthly** instead
of *"one four-hour timer floating alone in space."*

---

## §6. RAIDS FEED THE SEASON PASS

> *"You have a 30-tier Season Pass already built. Use it. Don't invent a separate raid progression
> currency."*

Season XP from: completing raids, 3★, 100% destruction, clearing a new threat level, completing a
dungeon, finishing a building, research, daily quests.

| Event | Season XP |
|---|---|
| Raid completed | +50 |
| 3 stars | +25 |
| 100% destruction | +25 |
| First clear | +100 |

So one raid yields **resources + gold + crystals + season progression**. *"And put the Season Pass
somewhere the player can actually find the poor thing."*

---

## §7. THE IDEAL RETURNING SESSION

```
BUILD COMPLETE            -> collect
Resources full            -> collect
3 RAID ORDERS READY       -> raid
   2,200 gold · 1,800 wood · 1,100 iron · 25 crystals · 100 season XP
                          -> train troops
                          -> upgrade Archer Tower
Iron Bastion II unlocked
                          -> start a 2-hour build, leave
```

> *"That player leaves the game thinking: when I come back, something will be ready. That's the
> feeling you're trying to create."*

---

## §8. OPEN THE CONTENT YOU ALREADY BUILT

**Journey becomes five cards, not two** — *"Two cards makes Journey look unfinished."*

```
QUESTS      Follow the story
RAIDS       Attack enemy strongholds
DUNGEONS    Challenge powerful encounters
REALM MAP   Explore Elarion
SEASON      Earn seasonal rewards
```

**Dungeons become the BIG session** (15–20 min, rare rewards) while **raids are the repeatable
five-minute loop** and **waves defend the settlement**. *"Those activities serve different moods.
That's good game design."*

---

## §9. LATER, NOT NOW

**Troops should eventually defend.** *"Troops do nothing during waves"* — long term, add
**ASSIGN DEFENDERS** (Shieldguard/front gate, Archer/west tower, Cleric/village centre,
Battlemage/Arcane Spire) so army creation serves **offence AND defence** and troops stop being a raid
tax. ⚠ **Explicitly NOT P0.**

**⛔ Do not build PvP yet.** *"Real asynchronous PvP means snapshots, matchmaking, offline defence,
anti-cheat, reward exploits, base validation, trophy balancing, revenge logic, attack limits, shield
mechanics, potentially server authority. That's a whole dragon."* Get Raid → Reward → Upgrade →
Harder Raid working first.

---

## §10. THE RELEASES

### P0 — close the economy loop (**WO-1374**)
Raid rewards wood · iron · gold · rebalance crystals DOWN · free starter army · raid daily requires
Barracks · Arena Herald respects the raid gate · guide says Journey → Raids · FTUE introduces
Barracks → army → raid · refusal message says explicitly what is missing.
> *"This alone could dramatically change behavior."*

### P1 — give raids progression (**WO-1375**)
Enable Iron Bastion · clear-count unlock ladder · increasing difficulty · increasing loot · raid
charges stack to 3 · first-win daily bonus · raid XP feeds the Season Pass.

### P2 — build retention around it (**WO-1376**)
Weekly Threat ladder · Journey Dungeons card · first dungeon accessible · Season Pass navigation ·
Realm Map navigation · dungeon rewards · troops participate in wave defence.

---

## §11. ⭐ THE METRIC — watch the funnel, not DAU

> *"Don't start with DAU or retention percentages. Watch one simple funnel:"*

```
Barracks unlocked -> Army trained -> First raid attempted -> First raid won
   -> Raid reward spent -> SECOND RAID ATTEMPTED WITHIN 24H
```

> *"That last one is the gold nugget. If someone raids once and chooses to raid again, your loop is
> beginning to work. If they don't, more tutorial text probably isn't the answer."*

⛔ **This is an INSTRUMENTATION REQUIREMENT, and it belongs in P0.** Six events, emitted through the
existing analytics rail (`EventTracker` -> `/api/events/track` -> Neon `analytics_events`) — ⛔ do not
build a second telemetry path. Without it the whole programme is unmeasurable and the next redesign
is guesswork.

---

## §12. ⚠ CONFLICTS AND DEPENDENCIES — read before implementing

1. ✅ **RESOLVED — THE 25 / 50 / 70 RULING IS SUPERSEDED.** Earlier on 2026-09-04 the owner said
   *"25% normal run 50% hard 70% hardest"* (WO-1373 §5.1). She then delivered this document and ruled
   ***"these findings take presedence"***. **So 25/50/70 is DEAD.** The live model is the **absolute
   per-camp reward table in §1** plus the **performance ladder** (fail 15–20% / 1★ 50% / 2★ 75% /
   3★ 100% / 3★+100% 110%). ⛔ Do not resurrect 25/50/70 from WO-1373 — that section is bannered.
2. ⚠ **WO-1372 (troops cost TIME, gold buys time) interacts directly.** If troops become time-only,
   the 1,650-gold wall partly dissolves and the gold-sized-to-army-replacement maths in §1 changes its
   meaning. **These two rulings must be reconciled before either ships.**
3. ⚠ **WO-1373's rough-stone / Jeweler chain** is the *other* raid reward axis, still blocked on the
   dungeon-exclusivity question. It is compatible with this programme but must not double-pay.
4. ⛔ **The Season Pass has NO navigation entry BY RULING, and a regression enforces it** —
   `PublicNavigationRetirementRegression`. §8's Journey card **re-points that oracle**; it does not
   delete it.
5. ⛔ **All six dungeons are fail-closed** behind a live `/api/dungeon-status` row
   (`DungeonStatusCatalog.cs:20-48`). §8's "first dungeon accessible" requires that endpoint verified
   or the gate changed. ⚠ Endpoint state NOT PROVEN.
6. ⚠ **`RaidBase_IronBastion.unity` is in neither `scene-configs.json` nor Build Settings.** Adding a
   scene to Build Settings is a `ProjectSettings` change, not a data edit.
7. ⛔ **Every number in this document is a TUNABLE** (standing rule 2026-09-02). Register them on the
   existing rail with these values as defaults. ⛔ Do not build a second rail.
