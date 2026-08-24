# Rulings owed — 2026-08-24

**Recommendation first on every one.** Most should be a yes/no. ⚠ **These are the pipeline's binding
constraint, not capacity** — after batch 4 there is roughly one batch of work left in the repo until
these are answered. Ten rulings, and three of them are really one.

---

## 1. Founder's Vow: does it grant a builder slot? (WO-1070 §4.1)

> ⭐ **RECOMMEND: (b) the Vow grants CRYSTALS TOWARD the slot, not the slot itself.**

⛔ The straight grant collides with your own WO-911 Q6 ruling: the extra queue slot is **Echo-gated**
("each Echo above 2 unlocks the RIGHT to buy, crystals complete it"). A Vow that hands over the slot
**buys past a progression gate** — the exact pay-to-skip shape that costs a live store its
credibility, and we are live. Crystals-toward-it is covenant-clean: the Vow **speeds your hand, it
does not skip the queue.**

Weaker copy, and worth it. Alternative (c) omit the perk entirely is also defensible; (a) bypass is
the one I would not do.

## 2. "Named on the Heart" — remove or build? (WO-1070 §4.2 **+ WO-1073's $500 monument**)

> ⭐ **RECOMMEND: remove the sentence NOW; build the monument LATER, as one surface.**

⚠ **These two tickets are the same feature.** One ruling covers both.

The sentence is **undeliverable copy on a live storefront** — a promise we are not currently keeping,
which is worse than not promising. And WO-1073's own rule says *a threshold whose cosmetic cannot
render yet is not authored yet — never a dead unlock.* The cosmetic rail is WO-1176 §4 / WO-1074; the
monument returns when it renders.

## 3. Storehouse Deeds: percentage multiplier, or a fourth container? (WO-1071 §4)

> ⭐ **RECOMMEND: percentage multiplier.**

A fourth physical container touches **placement, `BaseLayout`, and the container singleton rules** —
structural risk on a live save schema, bought for an "estate feel" win. ⚠ And the estate **already
grows visibly**: WO-1108b took containers to **six levels** (1k → 32k), so a maxed store already
reads as a bigger estate without new placement.

⚠ Honest tradeoff: the multiplier is **invisible**. If "max the estate" means you want to *see* it,
that is a real reason to overrule me — and it costs a placement/singleton change.

## 4. Patronage ladder thresholds (WO-1073)

> ⭐ **RECOMMEND: author the ARCHITECTURE now, the THRESHOLDS later — do not set numbers today.**

Its own §3.2 says a tier whose cosmetic cannot render is not authored yet. Setting dollar thresholds
before the cosmetic rail exists creates **dead unlocks**, which are worse than no ladder.

When it is time, the shape I would propose against your live $1.99–$49.99 ladder: **$50 / $150 /
$500**, with the $500 tier being the monument from ruling 2.

⛔ Two invariants to keep whatever the numbers: it grants **cosmetics only** (a regression asserts no
resource/currency/timer grant), and lifetime totals **only ever grow** — an SPL transfer cannot
reverse, so nobody should build clawback logic for a rail that cannot claw back.

## 5. Cathedral affinity perk: the numbers (WO-1154 §6)

> ⭐ **RECOMMEND: I bring you numbers with the crystal-sink curve, not in isolation — then you rule.**

Design is settled; only balance is open. ⚠ Constraints already binding: the Cathedral is **magical =
crystals + iron, NEVER wood** (a wood price is a red build, not an opinion), and attunement is
per-structure persistent state, so it is **save-adjacent** — ⛔ a schema bump is *your* decision and
must never be made just to ship a feature.

## 6. ⭐ The tutorial names the wrong buildings (WO-1161 §5) — the one I would fix today

> ⭐ **RECOMMEND: correct the two references. This is not a creative rewrite.**

`tutorial-steps.json` triggers the armour beat on **`workshop`** and points the nudge at **`forge`**.
Both are the wrong rows. ⚠ **The beat only ever read correctly because the LABELS were crossed to
match it** — straightening the names is what exposed it. The truthful chain is: weapons roof =
`forge` (role `weaponsmith`) → then suggest armour = `armorer`.

⛔ **Fix ONLY those two ids.** That file carries your creative pin: *"the chain's order past these two
beats is an OWNER creative pin — propose the full sequence before authoring more."* Correcting two
wrong ids is not authoring a sequence.

⚠ Related but **separate**: `EchoCardVM.FaucetBuildingIdFor` still routes iron to `collector_forge`.
Repointing the cue without moving the faucet binding **swaps one lie for another** — the cue would
name a building that, once built, still would not open the gate. Its own change, with a captured run.

## 7. What do the Farm and the Silo become? (WO-1163 §6.1)

> ⭐ **RECOMMEND: `collector_farm` → **Quarry**, `silo` → **Stoneyard**.**

Stone needs quarry/mason vocabulary and both current names are food-flavoured. Quarry is where stone
comes *from*; Stoneyard is where it is *kept* — mirroring the existing producer/storage split exactly.
⛔ **Ids stay frozen** (`collector_farm`, `silo`) — they are live save keys; only `displayName` changes.

## 8. Does 1,800 food become 1,800 stone? (WO-1163 §6.2)

> ⭐ **RECOMMEND: yes, 1:1 — and say so out loud.**

A rename converts balances automatically, so the only question is whether that is *intended*. It is
the right answer — a player who banked 1,800 units of the basic building material keeps 1,800 units
of the basic building material. ⚠ But it must be a **stated decision**, because on a live build a
player wakes up holding a different resource than they went to sleep with.

## 9. Where does the Store's HUD entry live? (WO-1164 §5, sub-question)

> ⭐ **RECOMMEND: put it in Bag as a tab, not on the action bar.**

You already ruled **both doors** — the building stays in town AND a HUD entry opens **the same
panel** (⛔ one destination, two doorways, never two implementations).

The bar cannot take it cleanly: calm(town) is at **six visible faces** and `MaxVisibleFaces` was
deliberately cut 7 → 6. ⚠ There is precedent for exactly this move — **Map left the bar and became a
tab inside Bag** (WO-911). Adding a seventh face reopens the problem that cut solved.

## 10. Roaming troops — three questions (WO-1179)

> ⭐ **RECOMMEND: yes it can hit an offline town, and it is the WAVE LOOP escalating, not a second
> system.**

1. **Wave loop or a second system?** The wave loop. A second system means two things spawning
   attackers on separate difficulty curves, and they will fight each other for the player's
   attention. `WaveManager` already generates rosters and the four gate `SpawnPoint`s already exist.
2. ⭐ **Can it hit an offline town?** **Yes — and this is the load-bearing one**, because it decides
   whether your **48-hour shield** has anything to protect. If offline towns are safe, the shield
   protects nothing and should not be sold.
3. **What does losing a gate cost?** Owed. Escalation without a consequence is difficulty without
   stakes. ⚠ Keep it **repairable, never permanent** — a permanent loss taken while offline is how a
   returning player quits.

⭐ **The step that matters is 2 gates, not the roster sizes** — everything before it is a bigger
version of the same fight; two gates is the first time the player must choose what to leave
undefended.
