# Quest rewards - directions worth building, and one decision that gates them

**Owner, 2026-08-25**, in conversation while ruling that quests should pay XP.
**Status: IDEAS, not work orders.** Nothing here is scheduled. It exists so the ideas are not
re-derived from scratch in three months, and so the schema decision below is made once.

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

## ⛔ THE DECISION THAT GATES ALL OF IT - make it once, now

Today a quest reward is a FIXED STRUCT: `crystals`, `food`, `magic`, `grantItemId`, `grantsKeystone`.
WO-1201 adds `xp`. Troops would add another field. Then a cosmetic, then a building.

⚠ **Adding one field per idea is how the shape rots**, and this repo has just been bitten by the
exact failure mode it produces: `packs.json` authored a `stone` key, the Unity client bound only
`food`, and Newtonsoft **silently dropped it** - three live SKUs would have granted nothing, with no
exception, no log and no red test (WO-1163, 2026-08-25). ⛔ A widening struct read by a strict DTO
fails SILENTLY, every time, and looks exactly like a correct parse.

**Option A - keep the struct.** Cheapest today. Charges you a schema edit, a DTO edit and a mirror
pass for every future reward kind, and each one is a chance to repeat the silent-drop bug.

**Option B - a typed reward list.** `[{kind:"xp", amount:500}, {kind:"troop", id:"pikeman"}]`.
Costs more today. Every future reward kind is then authoring-only, and an unknown `kind` can be made
to FAIL LOUD instead of vanishing.

⭐ **Lead's recommendation: Option B**, precisely because the owner named the second and third reward
kinds in the same breath as the first. A shape chosen for one new field is being chosen for four.

⚠ Whichever is chosen: **both canonical `quests.json` copies move together**, and ⛔ every surface
that reads a quest reward must be named - including any Unity-side DTO - because WO-1163 established
that a mirror law listing only what your tests can reach is not a law.
