# WORK ORDER 1070 — Founder's Vow 2.0: the whale SKU sells permanence + prestige, and goods stop being the headline

**Status:** BLOCKED — both §4 rulings are complete and the "Named on the Heart" copy removal is done, but the CONTENTS depend on unfinished work: purchase limits + companion (WO-1176), Founder's Citadel + cosmetic rail (WO-1074), Capacity Deed (WO-1071, itself unresolved), and the crystal grant (WO-1072, needs an owner ruling). Unblocks when those land. *(Status audit 2026-08-24: lead-verified bucket correction; body unchanged.)*
**Minted:** 2026-08-24 (UI seat), banner header bumped 1069 → 1074 in the same edit (with 1069, 1071–1073).
**Provenance:** WO-1165 §4/§9.1 + the external review the owner ADOPTED 2026-08-24 (*"I would not try
to fix the $49.99 Founder's Vow by stuffing another 30,000 wood into it. That's putting a bigger
engine on a shopping cart."*). Refined against WO-1176 (the companion answers the same question from
the owner's own design conversation — the two are merged here, not competing).

---

## 1. RCA — why the top rung fails, in three verified facts

1. **The ladder inverts**: founders-vow $49.99 = 1,962 goods/$ vs patron-of-elarion $19.99 = 1,968
   (WO-1165 §4, computed from `packs.json`). The informed $50 play is 2× Patron + 1× Starter's Hand.
   A whale-deterrent at the exact price where whales self-identify.
2. **Its real differentiators were never built**: `cosmetics: []` — the cosmetic/banner/naming that
   were supposed to justify the rung are unauthored, so goods became the headline by default, and
   goods lose (fact 1) AND get discarded (capped resources + `GrantSpendablePurchased` bypassing the
   cap — WO-1165 §3: a big grant parks you above ceiling and your own production is thrown away).
3. **One sentence over-promises**: `packs.json:188` *"Founders are named on the Heart"* — a
   permanent forever-promise with no implementation anywhere (WO-1165 §9.1). Highest-risk copy in
   the store.

**The adopted principle:** a $19.99 buyer can recreate the current $49.99 by buying twice. The whale
SKU must contain things **no repetition of cheaper SKUs can recreate**. Don't sell more stuff; sell
*more ownership of the world*.

## 2. The 2.0 contents (adopted direction; owner signs off the final list)

| Line | Source of truth | State |
|---|---|---|
| Permanent **Founder title/badge** + profile treatment | new cosmetic rows + `CosmeticOwnershipService` | ⬜ unauthored |
| Exclusive **Founder banner / castle skin** | same cosmetic rail | ⬜ unauthored |
| **Companion** (the WO-1176 §4 product) — Founder-exclusive variant | WO-1176 §4, all its rules apply (one appearance owner; paid-asset CDN failure must be TOLD) | ⬜ WO-1176 |
| **Founder's Citadel** treatment (WO-1074 prestige collection: exclusive castle architecture, animated Founder banner, Founder Heart aura, monument, arrival animation) — the $49.99+ tier's real body once the cosmetic rail lands | WO-1074 §3 | ⬜ WO-1074 |
| **Permanent storage expansion** — Storehouse Deed included | WO-1071 | ⬜ WO-1071 |
| Sensible **crystal grant** | priced on the WO-1072 single valuation | ⬜ WO-1072 |
| **Moderate resources** — present, never the headline | `packs.json` | authored, shrink |
| **Founder number** (account-era identifier) | server-side, from `purchase_entitlements` order | ⬜ new, small |
| "Named on the Heart" | **remove the sentence now** (§4 open item 2 decides if it ever returns) | ⛔ undeliverable copy |

## 3. ⛔ Constraints this must not break

- **The covenant**: convenience and beauty, never combat power. Every line above is cosmetic,
  capacity, or currency — no stats, no gear, no troops.
- **Purchase limits** (WO-1176 §3) must exist first or "Founder number" and one-time exclusivity
  mean nothing. Sequenced after it.
- **Tempo stays capped**: nothing in the Vow touches ad-skip caps or queue timing beyond what gold
  already legally does (WO-1165 §1 — the caps are load-bearing covenant infrastructure).

## 4. ⚠ OPEN — owner rulings needed (two)

1. **+1 workforce/builder slot?** The adopted review proposes it. ⛔ It COLLIDES with the WO-911 Q6
   ruling: the extra queue slot is **Echo-gated** ("each Echo above 2 unlocks the RIGHT to buy,
   crystals complete it"). A Vow that grants the slot outright bypasses the Echo gate; a Vow that
   grants only the *crystals* toward it is covenant-clean but weaker copy. Owner picks: (a) bypass
   allowed for Founders, (b) crystals-toward-slot only, (c) omit the perk.
2. **"Named on the Heart"** — remove permanently, or build it (a Founder monument/plaque surface is
   also the natural home of the WO-1073 Patronage $500 tier). One decision covers both tickets.

## 5. What NOT to touch

- No price change to the other rungs; no `BEST VALUE` badge moves (WO-1072 re-examines it).
- `founders-vow` SKU id is frozen (entitlements may reference it); contents change, id does not.

## 6. Acceptance

- [ ] Vow contents cannot be recreated by any combination of cheaper SKUs (the uniqueness test —
      assert at least one granted line exists in no other SKU)
- [ ] `packs.json` carries no undeliverable promise (the §9 copy list re-audited)
- [ ] Goods/$ is no longer the card's implicit pitch: goods ≤ the $19.99 rung's, by design, stated
      in the authoring note so a future "fix the value" pass doesn't re-invert it
- [ ] Owner sign-off recorded on §4's two rulings before implementation starts

---

## ⭐ OWNER RULING 2026-08-24

Both §4 open items are answered. This ticket moves **SPEC → READY**.

### §4.1 — +1 workforce/builder slot: **(b) CRYSTALS TOWARD THE SLOT, never the slot itself.**

Owner, verbatim:

> *"Founder's Vow may accelerate the player toward gated permanent upgrades, but may not bypass
> their progression requirements."*

- The Vow grants **crystals**, which the player then spends through the existing
  `BuildTimerService.TryBuySlot` path.
- The **WO-911 Q6 Echo gate is untouched**: each Echo above 2 unlocks the *right* to buy; crystals
  complete it. A Founder with 2 Echoes gets a faster wallet, not an extra lane.
- ⛔ Do **not** add a Founder branch, flag, or bypass inside `TryBuySlot`. There is exactly one
  slot-purchase path and the Vow does not fork it.

⭐ **THIS IS NOW A GENERAL MONETIZATION RULE, NOT A ONE-OFF — a FOUNDATIONAL RULING.** It governs
every future SKU, not just this one: **paid products may ACCELERATE a player toward a gated
permanent upgrade; they may never BYPASS that upgrade's progression requirement.** Any SKU that
proposes to hand over a gated unlock outright is refused by this rule without needing a fresh owner
pass. Carry it forward into WO-1071, WO-1072, WO-1073 and every store ticket after them.

### §4.2 — "Named on the Heart": **REMOVE THE COPY NOW.**

- Strike the `packs.json:188` sentence in the next data pass. It is an undeliverable forever-promise
  on a **live** store listing — the highest-risk copy in the catalog.
- The promise is **not lost — it MOVES.** It becomes the **$500 Patron Monument** in WO-1073's
  Patronage ladder. One surface, one implementation, two tickets consume it — exactly what WO-1073
  §3.3 predicted.

⭐ **OWNER'S REFINEMENT — capture it, it is the load-bearing half:** the monument appears **NEAR the
Heart, and NEVER alters the Heart itself.** Verbatim reasoning:

> *"that protects your most important world object from becoming a NASCAR hood covered in sponsor
> names."*

⛔ So: no inscription surface **on** the Heart mesh, no per-patron decal on it, no name list rendered
on the world tree. The monument is a **separate placed object adjacent to** the Heart, and its
density/scale stays bounded no matter how many patrons exist.

### What this changes in this ticket

- §2's table row "Named on the Heart" → **remove the sentence now**; the capability re-appears as the
  WO-1073 $500 monument, sited near the Heart.
- The proposed builder-slot perk → the Vow's **crystal grant** is the vehicle; no slot is granted.
- §6's last acceptance box ("Owner sign-off recorded on §4's two rulings") → **satisfied by this
  section.**

---

## ⚠ 2026-08-24 - the general rule in this ticket is a COPY. Cite the source instead.

The accelerate-never-bypass rule was written into this ticket inline (the recording pass ran before
`FOUNDATIONAL_RULINGS.md` existed). ⛔ **That file now holds it, and it says explicitly: do NOT restate
these rulings inside tickets - cite the file.**

**Source of truth: `FOUNDATIONAL_RULINGS.md` §1.** If this ticket's wording and that file ever
disagree, **the file wins and this ticket is wrong.**

⚠ Kept here only because deleting a paraphrase mid-flight is how a seat loses the ruling entirely.
The next edit to this ticket should cut the inline copy down to the citation - a fact written twice is
this repo's dominant failure mode, and this is a fresh instance of it, created today, by the process
built to prevent it.
