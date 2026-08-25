# Quest rewards - directions worth building, and one decision that gates them

**Owner, 2026-08-25**, in conversation while ruling that quests should pay XP.
**Status: SUPERSEDED for implementation by WO-1202** (2026-08-17). Schema = Option B typed list;
creative pack LOCKED by owner *"yes use your guidance"* — see
`WorkOrders/WORK_ORDER_1202_quest_rewards_scaled_by_placement_and_difficulty.md` §OWNER RULING.
This file remains as the decision history; do not re-derive from it against 1202.

---

## The ruling that started it

> **"add xp to quest rewards"** - and then: *"half the games I've played, that's the main reward."*

That second sentence is the load-bearing one. If XP is the MAIN reward rather than a garnish, it
must be authored at a scale where a player picks quest A over quest B because of the XP. A token
value fails the design even though it satisfies the schema. Tracked as **WO-1201**.

## The direction she named next

> **"we could add quests to give new offensive and defensive troops"**

Quests as the unlock path for troop types - offensive and defensive - rather than everything being
bought. The game already has troop training and a roster, so this is a new REWARD KIND, not a new
system.

## Others in the same family (unranked, not ruled)

- Cosmetics or a title, for quests whose payoff is status rather than power.
- A building or structure unlock, which is how a quest can teach a system rather than pay for one.
- A companion/Echo, tying questlines to the roster that already exists.
- Progression-only payoffs stated honestly - *"Unlocks Act 2"* is a real reward and reads better than
  90 food. **⭐ This one is already needed:** quest 3 (`forgemasters_act1`, "Honest Steel") has NO
  authored reward at all today.

---

## ✅ RULED 2026-08-25 — OPTION B, THE TYPED REWARD LIST

> **Owner, 2026-08-25.** Asked to choose between keeping the struct and moving to a typed list, she
> chose the **typed list**.

    "reward": [
      { "kind": "xp",       "amount": 500 },
      { "kind": "troop",    "id": "pikeman" },
      { "kind": "crystals", "amount": 220 }
    ]

**Why, recorded so it is not re-opened.** She named the second and third reward kinds — troops, then
*"all sorts of future ideas"* — in the same breath as the first. ⭐ A shape chosen for one new field
was really being chosen for four or more.

⛔ **And the deciding argument was not hypothetical.** A widening struct read by a strict DTO fails
**SILENTLY**, and it happened in this repo the same day: `packs.json` authored a `stone` key,
`PackCatalog.cs:64` bound only `[JsonProperty("food")]`, Newtonsoft dropped it at `:654`, and three
**LIVE** impulse SKUs would have granted literally nothing — no exception, no log, no red test,
indistinguishable from a correct parse (WO-1163, bounced 2026-08-25). ⭐ A typed list lets an unknown
`kind` **FAIL LOUD** instead of vanishing. That property is the entire point of paying for the change.

**Tracked as WO-1201**, which is now a reward-schema migration with `xp` as its first new kind — not
"add a field". ⛔ `kind: "troop"` is deliberately OUT OF SCOPE there: the shape must accommodate it,
but nobody builds it on that ticket.

### What the investigation then found

- ⭐ **Hero XP IS save-persisted**, since schema v29 (`HeroProgression` writes back on every `AddXp`).
  So `SaveSchema.CurrentVersion` stays **38** — quest rewards are catalog data, not save state.
- ⭐ **33 of the 63 stages pay NOTHING today.** Quest 3 was not an outlier; it was the one that got
  noticed. ⚠ Under an illustration that was invisible — under the reward slab it is half the board
  showing an empty hero element.
- The payout seam is already singular: `QuestService` raises, `QuestRewardBridge` dispenses. XP is one
  more branch there, ⛔ never a second path.
- The level curve exists — `150 + (L-1)*350 + (L-1)^2*500` — so *"primary reward, not a garnish"* is
  **enforceable** against a fraction of a level rather than authored by feel.
- ⚠ A sleeper: `UICaptureLaunch.cs:2374` constructs a `QuestReward` directly. The type change breaks
  it and reds the capture gate.

⚠ **Still open, and it is authoring rather than engineering:** what those 33 empty stages should pay.
The worksheet `docs/QUEST_REWARD_WORKSHEET.md` exists for exactly that. ⛔ The migration can land
before a single value is authored — do not couple them.

⚠ **And the class is only half closed.** This ruling fixes the shape for QUEST rewards.
`packs.json` still has the same widening-struct-plus-strict-DTO shape that caused the `stone` bug.
