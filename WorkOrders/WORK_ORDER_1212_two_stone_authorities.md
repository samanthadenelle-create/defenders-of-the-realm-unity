# WORK ORDER 1212 - There are TWO Stone balances, and only one of them is the player's

**Status:** READY TO IMPLEMENT - ⛔ **P0 PRECONDITION for the Food-to-Stone program.** The authority
question is RULED (option A, 2026-08-25, lead): `Resources.Food` stays the Stone slot and
`GameState.Stone` is retired into it. ⛔ **SEQUENCING: this edits `GameStateService.cs`, which WO-1211
also edits. The two CANNOT run in parallel - WO-1211 lands first.**
**Minted:** 2026-08-25 (CLI lead, main line; banner bumped 1212 -> 1213 in the same edit)
**Silo:** Core/State + backend
**Found:** 2026-08-25, verifying a dev-seat sweep that flagged `api/game/save.js` and `load.js` as
"treating food and stone as separate balances". The sweep was right about the smoke. The mechanism
below is the fire, and it is worse than a naming problem.

---

## The two authorities, both real, both persisted

| | the PLAYER's Stone | the OTHER Stone |
|---|---|---|
| where | `EconomyService.Food` / `_state.Resources.Food` | `GameState.Stone` (`NestedTypes.cs:151`, `SaveSchema.cs:249`) |
| wire key | `food` | `stone` |
| HUD | shown, labelled **Stone** | shown NOWHERE except `DebugCanvasUI.cs:137` |
| spent by | build costs, upgrades, training | **nothing** |
| seeded | by play | **`GameStateService.cs:1026` sets `s.Stone = 20` on new game** |
| server | `GUARDED_BALANCES` + `TIME_DERIVED_BALANCES` | ALSO in both lists (`save.js:62`, `:492`) |

Both are guarded, both are time-derived-reconciled, both round-trip to Neon. The offline clamp mirror
maps them to different homes in the same switch:

```csharp
// GameStateService.ReadTimeDerivedBalance / WriteTimeDerivedBalance
case "stone": return _state.Stone;            // <- nothing displays or spends this
case "food":  return _state.Resources.Food;   // <- the balance the player calls Stone
```

WO-1163 deliberately REUSED the internal Food slot for Stone rather than migrating. That was the right
call and it is not in question here. What nobody noticed is that **a `Stone` field already existed**,
so the reuse created a second authority instead of replacing one.

## ⛔ WHY THIS IS A P0 AND NOT HOUSEKEEPING

Today the player is unharmed: every active grant routes to `food`, and `food` is what she sees. The
danger is the OBVIOUS next step - the one a sweep of the codebase will recommend, and did:

> *"make every active JSON key, UI label, grant, calculation, and runtime route use stone."*

⛔ **DO NOT DO THAT YET.** On this tree, migrating an active grant key from `food` to `stone` routes
that value into `GameState.Stone` - a balance the HUD never reads and no cost ever spends. The player
would earn or BUY stone and receive nothing, with no exception, no log and no red test. That is the
`packs.json` authored-`stone`-bound-only-to-`food` defect from earlier today, pointed the other way,
on a build that now takes real money.

⚠ It is also already leaking a little: **every new game seeds 20 into the invisible balance**
(`:1026`), and `ResetCarveOutTest` / `SaveLoadRoundTripTest` both assert that 20. Those tests pin the
dead field, so a naive deletion turns them red and looks like a regression.

## ⭐ RULED 2026-08-25 BY THE LEAD - OPTION (A). This ticket is now IMPLEMENTABLE.

**`Resources.Food` remains the Stone slot. `GameState.Stone` is RETIRED into it.**

This is an internal-authority call, not a creative one - the player sees the word "Stone" either way -
so the lead makes it rather than parking the ticket. ⚠ Reversible on the owner's word.

Why (A) and not (B), recorded so the question stays closed:

1. **It is WO-1163's own precedent.** That ticket kept the legacy persistence name and moved only the
   player's vocabulary. Doing the opposite here would split the project across two conventions.
2. **(B) is a migration of every cost, HUD read, pack grant, quest reward and the whole offline path,
   atomically, for zero player-visible benefit.** The name of a private field is not worth that risk on
   a build taking real money.
3. **(A)'s risk is one-shot and testable**: a single read-migration of a non-zero dead balance, provable
   on a real save.

⛔ **The one thing that must not be lost in (A): the seeded 20.** `GameStateService.cs:1026` writes
`s.Stone = 20` at new game and two tests pin it (`ResetCarveOutTest`, `SaveLoadRoundTripTest`). Fold it
into the live balance, re-point both tests at the live one, and do NOT simply delete the field before
the migration has run for existing saves - a deleted field cannot be read-migrated.

## The original two options, kept as the reasoning behind the ruling above

### (superseded framing - the choice below is now settled as A)

Choose ONE and write it into canon before any Food-to-Stone key moves:

**(A) ONE authority: `Resources.Food` stays the Stone slot; `GameState.Stone` is RETIRED.** Cheapest
and matches WO-1163's precedent (persistence keeps its legacy name, the player sees Stone). Requires:
read-migrate any non-zero `Stone` into the live balance exactly once so the seeded 20 is not stolen,
drop `stone` from the server's guarded/time-derived lists or alias it to `food`, and re-point the two
tests from "20 in the dead field" to "20 in the live one".

**(B) ONE authority: `GameState.Stone` becomes the real slot and `Resources.Food` is retired.**
Honest naming, and a far larger job: every cost, HUD read, pack grant, quest reward and the whole
offline path move together, with a save migration. ⛔ Do NOT take this route piecemeal - a partial
landing splits the player's balance in half, live.

⭐ **Either way the invariant is the same and it is the deliverable: ONE balance the player can see,
spend and be granted into. Never two.**

## Acceptance criteria

- A registered oracle that FAILS if any grant path can credit a resource the HUD does not read - the
  general shape, not a one-off assertion about stone.
- A migration proven on a real save: a pre-migration save carrying a non-zero dead balance loads with
  that value present in the live one, exactly once, and no double-credit on a second load.
- Client and server agree on the balance list. `TIME_DERIVED_BALANCES` in `api/game/save.js` and the
  `ReadTimeDerivedBalance`/`WriteTimeDerivedBalance` switch must not name a key the other lacks -
  that switch already `FlowTrace.Fail`s on the mismatch, which is how this class announces itself.
- Gates by marker on fresh logs, and the migration case proven RED first.

## What NOT to touch until this is ruled

- ⛔ **Any active JSON grant key** currently authoring `food` - `quests.json`, `realm-map.json`,
  `offline-storage.json`, `FarmUpgrades.json`. They are correct TODAY precisely because they say
  `food`. They become wrong only after this ticket picks an authority.
- ⛔ `legacySkus`' `impulse-food-*` aliases. They are purchase aliases and must keep resolving.
- ⛔ PROD-016's display-only lane, which is deliberately scoped to labels and the node model and
  touches no balance at all.
