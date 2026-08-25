# WORK ORDER 1194 - a second "Bank full" surface still scolds a player who just paid

**Status:** READY TO IMPLEMENT
**Minted:** 2026-08-25 (CLI lead, main line; banner bumped 1194 -> 1195 in the same edit)
**Silo:** Village / UI
**Ruling:** `FOUNDATIONAL_RULINGS.md` section 7 - CITE it, never restate it.
**Sibling:** WO-1191 fixed the *clamp* framing. This is the surface it does not reach.

---

## The finding

`TownBankCapacity.HasHeadroom` returns **false** whenever a resource is at OR above its cap. Two
surfaces read it and say "Bank full":

- `Assets/_Modules/Village/HarvestBoostService.cs:350-352`
- the WO-900 collector "tap to collect" tell

Both are **read-only** - they clamp nothing and cannot destroy value, which is why WO-1191's clamp
audit correctly found nothing to fix there. But under `FOUNDATIONAL_RULINGS.md` section 7 a player can
now be above cap **because they paid to be**, and these two surfaces will tell them their bank is
full.

⭐ **That is the same defect WO-1191 just fixed, arriving through a different door.** The clamp warn
was taught the difference between "full" and "above capacity"; these were not, because they do not go
through the clamp at all.

## Why it matters more than it looks

The player's state is *"I bought something and it is all in my bank."* The message they get is
*"Bank full"* - which in every other context in this game means **you are losing income**. It is
technically true and reads as a scolding for a purchase. On a live store that is the worst possible
place for a mixed signal.

## What to build

1. Both surfaces distinguish **at cap** (the existing "full" wording stands - a surplus really is
   being discarded) from **above cap** (the WO-1191 framing: it is all theirs to spend, and harvests
   resume when they are back under).
2. ⭐ **Reuse, do not re-derive.** WO-1191 added `BankOverflowStatus.OverCap` (strictly
   `current > max`) and `Current` on the same struct. Read those. ⛔ Do not recompute `current > max`
   locally - a second copy of that comparison is this repo's dominant failure mode, and it will drift
   from the first the day someone changes the boundary case.
3. ⛔ The strict boundary is deliberate: `current == max` keeps the existing full-bank words. Do not
   "tidy" it to `>=`.
4. Exact player copy is the **OWNER'S**. Propose; do not treat your wording as final. WO-1191's
   proposal, also awaiting her word, is:
   `Wood is above storage - 3400 of 2000. All of it is yours to spend. Harvests and rewards add no
   wood until you are back under 2000.`
5. ⛔ Never carry the distinction by colour alone - the owner is red/green colourblind. Words.
6. ASCII-only strings.

## Acceptance criteria

1. With a resource ABOVE cap, neither surface says "full" or implies loss.
2. With a resource exactly AT cap, both surfaces are **byte-identical to today**.
3. An uncapped resource (crystals) is unaffected - enumerate from `IsCapped`, ⛔ never a
   resource-name list, which goes stale the day WO-1163 lands.
4. Proven by a measured assertion, not by reading the code.

## What NOT to touch

- ⛔ `TownBankCapacity.ClampGrant` and `BankOverflowToastPresenter` - WO-1191 owns those and they are
  already correct. This ticket is the surfaces that never go through them.
- ⛔ Do not change what `HasHeadroom` RETURNS. It is read by capacity logic as well as by copy, and
  flipping it above cap would be a mechanical change dressed as a wording fix. Branch at the caller.

---

# PART 2 - owner-added 2026-08-25: the collector tell is too vague to act on

> *"it's hard to tell what is full and what is empty. It says resources collected, but it's so vague
> that you don't know - is it wood, is it food, is it iron, or all of them full, or just one of them
> full, or are none of them producing... it's just '1 of 1', '0/2', '1/0' and collect. It's vague."*
> - owner, 2026-08-25

## Confirmed at source

The ambient chip reads **`Collectors 2/3 full`** + **`Tap to collect`**
(`Assets/_Modules/Core/UI/CollectorStatusGate.cs:22`).

⭐ **That fraction counts COLLECTOR BUILDINGS, not resources.** So `2/3 full` answers "how many of my
collector buildings have hit their own cap" and answers **nothing** about:

- WHICH resource each one holds,
- whether a given resource is full, part-full, or empty,
- whether anything is **producing at all** - a stalled faucet and a full one both stop the number
  moving, and the chip renders them identically,
- what a tap will actually bank.

**The owner's read is exactly right: the number is precise and uninformative.** It is a denominator
with no subject.

## ⭐ RULED 2026-08-25 - the owner designed it, and it SUPERSEDES the popup idea below

> *"three thin buttons or three lines up there - a symbol for iron, one for stone, one for wood, very
> small with little symbols, and then the count as the count increasing so that shows up. And where
> it says Collectors, put a tiny little button that says Harvest. This way they can see everything at
> once, they can see that they're collecting, and they can tell which one's full - because if you do
> 300 of 500 or whatever that capacity looks like, they can easily see it at a glance. And we can
> keep it very small because it's just side knowledge. It's nothing you need to know until it's full,
> or unless you're about to be raided, or you need to check if you have enough to build something."*

**The ruled shape:**
1. Three thin per-resource lines, always visible, each with a small icon.
2. The value renders as **current of capacity** (`300 of 500`), so FULL is legible at a glance and a
   rising number shows collection is happening.
3. The word **`Collectors`** is replaced by a tiny **`Harvest`** button.
4. Deliberately SMALL. It is side knowledge - it matters when something is full, when a raid is
   coming, or when checking affordability before building.

⭐ **Why this design resolves the WO-900 copy law instead of breaking it** (see the constraint section
below): the law exists because "Collectors 2/3 full" and "Bank full" are two different fulls competing
for one word. Under this design **"Collectors" stops being a noun with a count and becomes "Harvest",
a verb**. The resource line then owns the storage vocabulary unambiguously and the button owns the
action. There is no longer a second thing called "full". ⚠ That should be recorded as the reason if
the law is amended - it is amended by the design, not waived.

## ⛔ CORRECTION 2026-08-25 - the owner has SEEN it and I had not. It is a REDESIGN.

> *"There's just two big ugly boxes is what exists."* - owner, 2026-08-25

⛔ **I described the CODE and called this a small extension. That was wrong, and the correction is
the useful part.** The code path below is real, but the thing it renders is two big ugly boxes. The
owner has looked at it on a device; I had not looked at it at all.

⭐ **AND HERE IS WHY I COULD NOT: THE IN-GAME HUD HAS NO CAPTURE COVERAGE.** The harness captures
**29 distinct panels** (`Builds/ui-capture/`, 89 PNGs) and **not one of them is the town HUD**:

    BuildGhostChips_* BuildMenuUpgradeTower BuildPaletteDock_* DailyQuestHud DialogueOptions_*
    EchoCard EchoPetButton EchoUnlockDialogue_* EndStateWaveClear_* HelpMenu HeroSkillTree
    LoreReadingModal NightMarket PauseMenu QueueCardRail RaidDeploy RaidSelection RaidsFaceStates
    RealmMap RumorBoard RumorBoard_daily TowerManagerPanel

Every one is a MODAL. ⚠ `DailyQuestHud` is named "Hud" and is a modal panel too. **The resource chips,
the action bar and the ambient chips - the surface the player looks at for the entire game - have
never been photographed, never been reviewed, and no gate has ever seen them.**

⛔ **So the standing rule "open the PNGs" has a hole in it: there is no PNG to open.** That is the
WO-942 class (capture-case gaps) and it is the reason a felt-test keeps finding things every marker
calls fine. **The HUD needs a capture case before any redesign of it can be judged** - otherwise the
redesign is verified the same way the current state was, which is not at all.

## The scope, corrected

This is a **REDESIGN of the ambient resource surface**, not "add a capacity string to the existing
chips". The owner's three-thin-lines design replaces what is there. The section below stays because
the machinery is worth reusing - the concept-icon resolver and the economy update seam are sound -
but ⛔ do not treat the current LAYOUT as a starting point.

## What already exists and is worth reusing (machinery only, NOT the layout)

**The chips already exist.** `HudKitController.BuildResourceChips`
(`Assets/_Modules/HUD/Kit/HudKitController.cs:1562`, WO-431 + WO-440): Gold + Wood/Iron/Food/Crystal
chips in an OBSIDIAN dark frame, in a dock, with a collapsed variant that tap-expands, updated by
`SetResources` on the economy event. ⭐ Icons already resolve through the **CurrencyChip concept
resolver from `concept-icons.json`** - so "a little symbol per resource" is already DATA, not a
hardcoded sprite, and the owner's icon requirement is satisfied by the existing mechanism.

⭐ **THE ACTUAL GAP IS ONE THING: the chips render a VALUE and no CAPACITY.** `TownBankCapacity` is not
read there at all. Everything the owner asked for except `of 500` is already on screen.

⚠ That file records a hard-won 2026-08-05 device rebuild (the word "Resources" rendered twice, three
different right edges, tab/panel overlap). ⛔ Read those notes before moving anything - that layout was
measured on the Seeker and paid for once already.

## ⛔ Blockers and open questions this design raises

1. **STONE DOES NOT EXIST YET.** The chips are Gold/Wood/Iron/**Food**/Crystal. The ruled three are
   iron/**stone**/wood. `WO-1163` (food retires, stone takes its save slot) is **BLOCKED on the owner's
   tier-basket ruling**. So either this ships against `food` and renames when 1163 lands, or it
   sequences behind 1163. ⛔ Do NOT introduce a second stone concept ahead of 1163 - that is the
   duplicated-state failure this repo keeps paying for.
2. **What happens to GOLD and CRYSTALS?** The owner named three lines. Gold and Crystal chips exist
   today. ⚠ **Crystals are UNCAPPED**, so `current of capacity` is meaningless for them - they need a
   different treatment or no capacity figure at all. Gold's placement is unstated. **Owner ruling
   owed.**
3. Where exactly "up there" is, against the existing dock and the collapsed/expanded behaviour -
   whether this replaces the collapse entirely (always visible) or lives inside it.

## The earlier popup framing (SUPERSEDED by the ruling above, kept for reasoning)

> *"maybe just like a little widget or something where they click it and a little pop-up appears that
> gives them the details and the breakdowns, and let them choose what they want to empty, or empty
> all. I don't know, but I think it's worth looking at because it's too vague."*

So, as requirements rather than a design:

1. **Per-resource state, not a building count.** The player should be able to see, per resource,
   whether it is full / part-full / empty, and whether it is producing.
2. **A stalled producer must be distinguishable from a full one.** These are opposite problems with
   opposite fixes and they currently look the same.
3. **Selective collection**, plus a collect-all - the player chooses what to bank.
4. ⭐ **This reads together with PART 1 and section 7.** Once a resource can be legitimately ABOVE cap
   because it was purchased, a breakdown surface has a third state to express: not "full and losing",
   but "above storage, and not accepting harvest until you spend down". Design PART 1 and PART 2 as
   one surface, not two that disagree.

## ⛔ The constraint that will bite whoever designs this

`CollectorStatusGate.cs:20-26` carries an explicit **COPY LAW** from WO-900, the "two-fulls" problem:

> *"Storage / Bank / current-max belongs to the WALLET (WO-857). Collectors belongs HERE. The chip
> says `Collectors 2/3 full`. **The word Storage must never appear on this surface.**"*

⚠ A per-resource breakdown is **precisely the surface where those two meanings meet** - "this
collector is full" and "your bank is full" are different facts with different remedies, and the law
exists because conflating them already confused people once.

⛔ So do NOT resolve this by deleting the law. Either the popup is a **third surface** that is allowed
to speak both vocabularies while keeping them visibly distinct, or the law is amended deliberately
with its reason updated. **Whichever is chosen, it is an owner ruling and must be written down.**

## Non-negotiables for any design

- ⛔ **State in WORDS or shape, never colour alone** - the owner is red/green colourblind. A
  full/empty/stalled distinction carried by a bar's hue is unreadable to her.
- ⛔ ASCII-only strings (non-ASCII is tofu in TMP on device).
- Controls at or above `ElarionUiKit.MinTouchPx` (112). A per-resource row with a tappable collect
  control has to be authored at the floor, not clamped into it.
- ⛔ Reuse the existing collection path: `ResourceCollectorService.CollectAll()` via
  `CollectorStatusGate.RequestCollectAll`. That gate deliberately **adds no collection logic of its
  own** (`:75-77`) - a second collect path is how two systems start disagreeing about what was banked.
- Read per-resource capacity state from `TownBankCapacity` (`IsCapped`, and PART 1's `OverCap` /
  `Current`). ⛔ Never a hardcoded resource-name list - it goes stale the day WO-1163 lands.

## Deliverable

⚠ **This half is a DESIGN task before it is an implementation task**, and the owner has said the shape
is open. It wants a spec + mockup (UI seat) covering the states above, then implementation. It should
NOT be handed to an implementation seat as-is.
