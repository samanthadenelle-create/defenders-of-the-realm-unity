**Status:** SPEC — needs the owner's numbers before it can be built (see §6). Design is settled; balance is not.

# WORK ORDER 1154 — Elemental affinity as a Cathedral perk, earned at max tower level

**Minted:** 2026-08-23 (CLI, banner bumped 1154 -> 1155 in this SAME edit)
**Lane:** Combat / balance + data. Touches the damage path — treat with the care that implies.
**Parent:** WO-907 (elemental affinity system).
**Owner ask, 2026-08-23:** *"WO-907 … create a WO on how to offer this at max towers maybe tie into a magical perk at cathedral of magic."*

---

## 1. WHAT THIS WO IS, AND WHAT IT IS NOT

WO-907 rules **what affinity IS** — a match bonus that is never a lock, with the visual and the element
being the same fact. It is NOT STARTED and stays the parent.

This WO answers a narrower, later question: **how does a player come to HAVE affinity?** The answer the
owner proposed, and this spec adopts: **it is not a property towers are born with — it is a PERK,
unlocked at the Cathedral of Magic, and applied to a tower that has reached MAX LEVEL.**

⛔ **Do NOT implement WO-907's damage model from this ticket.** If affinity does not exist yet, this
delivers nothing on its own. Sequence: WO-907 first (the element on the tower and on the enemy, and the
match bonus), then this (how it is earned).

## 2. WHY THE CATHEDRAL IS THE RIGHT HOME — already canon, not a new invention

> **Owner ruling 2026-08-14, verbatim:** *"cathedral of magic is where all magic upgrades anre and can
> unlock new teirs of spells"*

That ruling is already load-bearing: it is the tiebreak that classified `arcane-tower` (displayName
**"Cathedral of Magic"**) as **MAGICAL** in the WO-947 cost-basket pass, over surface evidence pointing
the other way. Recorded on the `arcane-tower` row's `_costNote` and in `structures-catalog.json`'s
`_costBasketRule` header. **The distinction is written down and must not be re-litigated:** the jeweler
SELLS things that happen to be precious; the Cathedral is **where magic upgrades live and where new
spell tiers unlock** — the ENGINE of magical progression, not a vendor that deals in it.

So "affinity is unlocked at the Cathedral" is not a concept bolted on. It is the first system to
actually USE the role the owner already assigned that building. Today the Cathedral is
`behaviorId: GameplayBuilding` and unlocks nothing — this is the content that makes that ruling true.

## 3. THE SHAPE

1. **A tower becomes ELIGIBLE at max level.** `RepoProps.MaxStructureLevel` (`RepoProps.cs:69`) is the
   **single ceiling** and is currently **6**. ⛔ Read it; never hardcode a level. It moved 3 -> 6 once
   already (WO-1108b) and eight hardcoded 3s had to be hunted down afterwards.
2. **The Cathedral sells the PERK, not the element.** Buying the perk unlocks the *right* to attune; the
   player then chooses WHICH element on WHICH eligible tower.
3. **The player picks the element. Always.** ⚠ This is the WO-830 / WO-1108 grammar and it is binding:
   **a match bonus, NEVER a lock.** Do not assign a tower a fixed element, and do not gate a tower to a
   single choice. Same rule the Echoes already follow — the player picks, and matching pays a bonus.
4. **Re-attuning is allowed, at a cost.** A player who attunes wrong must not be permanently punished —
   the same "inefficient, never blocked" principle WO-907 §2 states.
5. **The visual follows the element** (WO-907 §1). The moment a tower is attuned it must LOOK attuned.
   That is what teaches the system without a table, and it is why the Arcane Spire bug — dealt Aether,
   rendered Fire — is the anti-pattern to design against.

## 4. WHY MAX LEVEL IS THE RIGHT GATE

- It gives level 6 a **reason to exist** beyond bigger numbers. Today the top of the ladder is a stat
  bump; this makes it a *decision*.
- It is a **late-game crystal sink** on the magical basket, which is where the economy needs depth — the
  owner's 2026-08-21 ruling was explicitly that **the lever is the SINKS, not the faucet**.
- It cannot trivialise early combat, because nothing is attunable until a tower is maxed.

## 5. ⛔ CONSTRAINTS

- **Cost basket (WO-947, BINDING):** the Cathedral is **MAGICAL** = **crystals + iron, NEVER wood**. The
  invariant is that no row's cost ever contains wood AND iron AND crystals together, and
  `CostBasketSeparationRegression` `[cost-basket]` FAILS the gate on a violation. A perk priced in wood
  is a red build, not a balance opinion.
- **Save schema:** attunement is per-structure persistent state, so it is a save concern. Read the
  version off `SaveSchema.CurrentVersion` (`SaveSchema.cs:41`), never off a doc. Prefer an ADDITIVE,
  default-on-read field (no bump). ⛔ **A schema bump is an OWNER decision — never make one to ship a
  feature.**
- **Owner is RED/GREEN COLOURBLIND.** An element may never be conveyed by hue alone — icon, shape and a
  WORD, with the greyscale check as the acceptance test. This is precisely the kind of feature that
  fails that test by default.
- **Instrument it (CLAUDE.md §12):** `FlowTrace` the eligibility check, the perk purchase, the attune and
  the re-attune. Permanent — never stripped.
- **UI:** code-built uGUI only (UXML does not work in player builds). `MinTouchPx = 112`.

## 6. ⚠ WHAT THE OWNER MUST RULE BEFORE THIS IS IMPLEMENTABLE

This ticket is deliberately SPEC, not READY. Five numbers are hers:

1. **Perk price** at the Cathedral (crystals + iron), and whether it is bought **once** (unlocking
   attuning game-wide) or **per tower**.
2. **Does the Cathedral need its own level** to sell it — is this gated on Cathedral level as well as on
   tower level?
3. **Re-attune cost**, and whether it carries a cooldown.
4. **Bonus size.** ⚠ Do not copy a number from another system. The Echo match bonus is an ADDITIVE term
   inside a sum (base 0.02, match +0.03), and a doc describing it as "DOUBLES the yield" was FALSE and
   nearly shipped a ~20x buff (CLAUDE.md §7). State this one as an explicit formula with its live values
   in `_authoringNotes`, from the first line.
5. **How many elements**, and whether every enemy family carries one — WO-907 §1 records the owner's
   *"They don't yet but should."*

## 7. ACCEPTANCE

- [ ] A tower below max level cannot be attuned, and the refusal is a WORD, not a tint
- [ ] The perk is purchasable only at the Cathedral, priced crystals + iron, `[cost-basket]` green
- [ ] The player CHOOSES the element; no tower is gated to one, and a mismatched tower still works
- [ ] An attuned tower LOOKS attuned, and the visual matches the element it actually deals
- [ ] Attunement survives save / load / relaunch, with no schema bump unless the owner rules one
- [ ] Greyscale screenshot: every element is still tellable apart
- [ ] `[Flow:*]` lines cover eligibility, purchase, attune and re-attune
