# WORK ORDER 1380 - Echo Guides, and the memory lines

**Status:** READY TO IMPLEMENT
**Silo / Lane:** Echoes / raid entry - `EchoWorldPresence` voice + Guide selection
**Type:** NEW BEHAVIOUR on an EXISTING system, owner-ruled creative direction
**Minted:** 2026-09-04 (CLI)
**Source of truth:** `docs/CREATIVE_CANON_ELARION_2026-09-04.md` §7 - ⛔ points at that file, does not
restate it.

---

## §1. ⛔ READ THIS FIRST - FOUR CHARACTERS IN THE DIRECTION DO NOT EXIST

The creative direction named **Sylas, Thrain, Grom and Elara**. They are illustrative and **this game
has never had them.** Owner, asked directly: ***"Whatever we have use those."***

The real roster is **Aldwin, Elowen, Corvin, Bran, Doran, Maren** - verified at
`Assets/_Modules/Village/Harvest/EchoRosterCatalog.cs:149-222`. ⚠ **Identity lives in that CODE table**;
`echoes-balance.json` holds numbers only. A seat implementing the illustrative names ships four
characters the game has never had.

## §2. THE MECHANIC

Before each expedition the player picks an **Echo Guide**. **The Echo does not fight. It remembers.**
The Heart cannot see clearly beyond its protection; an Echo can recognise fragments of the world that
existed before.

⭐ **This is mostly not new work.** `EchoWorldPresence` (WO-1108 Lane B) **already** escorts the player
to the gate and returns once after the battle - one owner, one lifecycle. **Give the existing behaviour
a voice.** ⛔ Do not build a second spawner; `PetDeployer.DespawnEcho` is the one despawn path.

## §3. THE RECOGNITION DOMAINS ARE DERIVED FROM SHIPPING LORE, NOT INVENTED

Canon §7 carries the table; every domain is a quotation from the roster catalog. **Corvin is the
natural first Guide** - *"the scout who ranged the far dark for Elarion and never came home"* - the only
Echo who has already been out there.

⭐ **The hook worth protecting:** send **Doran**, the mason who laid Elarion's first stones, to the
**Iron Bastion** - the one place *the Heart remembers no fortress*. He recognises the stonework.
Nobody has to explain why that is frightening.

## §4. SCOPE FENCES - both ruled 2026-09-04, both deliberate

- ⛔ **NARRATIVE ONLY at launch.** A Guide grants **no mechanical bonus** in V1. Adding one later is a
  deliberate design decision, never a quiet one.
- ⛔ **ALL 24 LINES SHIP, OR THE FEATURE DOES NOT.** Six Echoes x four targets, one line each. A
  recognition system that fires for two Guides and stays silent for four does not read as depth; it
  reads as broken, and it teaches the player to stop noticing.

## §4b. ⛔ THE WRITING RULE - QUESTIONS BEFORE ANSWERS (canon §8.0, added 2026-09-04)

**Every one of the 24 lines is held to this, and it outranks completeness.** The worked example, from
the owner, is the difference between the two:

⛔ **NOT** *"These stones use the ancient masonry technique employed by the third-age wardens of..."*
Her verdict on that draft: **straight into the sea.**

✅ **Doran, at the Iron Bastion:**
> *"I know this stone."*
>
> *(a beat)*
>
> *"I laid it."*

⭐ **Four words, and the player starts the next raid.** It sits next to the target card's *"The Heart
remembers no fortress here"* and produces a fact that should not be possible - no lore dump, no villain
monologue, no explanation. **When in doubt, cut the second sentence.** A writer who fills all 24 slots
with explanations has failed this ticket more thoroughly than one who leaves a line short.

## §4c. ⛔ WHY "NARRATIVE ONLY" IS A HARD FENCE, NOT A SCOPE PREFERENCE

Owner, 2026-09-04: *"The second Corvin gives +8% scouting loot and Doran gives +5% stone, players stop
choosing whose memories they want and start choosing the spreadsheet answer."*

**A buff does not ADD to this feature - it REPLACES it.** The value of the Guide is that it makes
exploration personal; the moment one Guide is mathematically correct, the choice is over and the
characters become furniture. **Let them be characters first.** Mechanics can come later, once players
care who they are.

## §5. ⚠ BLOCKED SUB-ITEM - do not guess it

Canon §7 ends with **Aldwin:** *"...there's someone here."* The illustrative version addressed the
player by first name. ⛔ **NOT VERIFIED that the game knows it.** Read the code, find where a player
name would come from, and report. **No string uses a player name until that is proven** - if it is not
available, the line ships without it and still works.

## §6. ACCEPTANCE

- [ ] Guide selection before a raid, defaulting to **Corvin** when the player has made no choice
- [ ] 24 memory lines authored and reachable; a regression **counts them and FAILS below 24**
- [ ] A regression that FAILS if a Guide grants any stat, yield or combat effect (the scope fence)
- [ ] No second Echo spawner; `EchoWorldPresence` remains the single appearance owner
- [ ] The player-name question answered in writing, either way
- [ ] Registered in `DataRegression`; `REGRESSION_OK n/n suites` on a fresh log
