# WO-1427: "Why can't I?" - every refused build or upgrade names its blocker in player words and offers the STEPS that clear it

**Status:** READY TO IMPLEMENT - minted 2026-09-06 (CLI) from the owner's playtest of build 2026.09.06.357599
**Silo:** Core (a new gating producer) + the three refusal surfaces. Presentation stays a separate layer.
**Owner rulings (2026-09-06, verbatim):**
- *"i want an audit of all items buildable upgradable. I want the user to be able to see the steps need to get there. So if upgrade village to lvl 1 is prerequsite, click here to see stpes needed"*
- *"the idea came while testing, i had no way to figure out why i couldnt upgrade"*
- *"i couldnt tell Oh im missing gold, ohhh i need a foundry, whatever"*

**Sequencing:** lands AFTER WO-1423 (village tier gate), WO-1424 (caravan), WO-1425 (cap-aware shortfall) and WO-1426
(end-state panel). Three of those touch the same files. **Do not dispatch this in parallel with them.**
**Companion artefact:** `docs/PREREQUISITE_REGISTRY_2026-09-06.md` - the measured audit of every rung and its gates.
That registry is the CONTENT this feature surfaces; it is not read at runtime.

---

## 1. The defect, in the owner's own words

She played her own game, could not upgrade something, and **had no way to find out why**. Not "the message was
unclear" - there was no path from the refusal to the cause. She could not tell the difference between:
- *"I simply have not gathered enough gold yet"* - wait, or go earn it, and
- *"my bank physically cannot hold this number until I build a Foundry"* - a completely different action, and
- *"this is village-tier gated"* - a third, which today paints a DEAD grey face with no door at all.

Today all three read as one flat sentence. `BuildModeController.ShortfallMessage` emits `"Not enough Wood (3150)"`
whether the player is 10 short or is standing at a bank that caps at 3000 and can never hold 3150. **A refusal that
cannot be acted on is worse than no refusal**, because the player concludes the game is broken - which is exactly what
happened.

Measured examples, all from tonight's investigation:
- **Archer Tower L2 to L3** costs 3150 wood. With a Lumberyard at level 1 the wood ceiling is **3000**. The bar sits
  full at 3000/3000 forever and the button never lights.
- **Every ladder's tier-2 upgrade** is village-tier gated, and the locked Manage card paints
  `"UNLOCKS AT VILLAGE LEVEL 1"` on a dead face and then `return`s before its door is built - the one card that names
  the gate is the one card with no route to the control that opens it.
- **`lumber-ancient-sawmill`** demanded village tier 4 when the maximum is 3 (fixed in WO-1423, listed here because it
  is the extreme case: a gate that could never open, and nothing told anyone).

## 2. The target

**Every refusal names its blocker in player words, and every blocker that has a path offers a door to that path.**

```
  ARCHER TOWER                                    LEVEL 2
  Upgrade: [wood] 3,150   [iron] 1,400
  Your bank tops out at 3,000 Wood.
  [ SHOW ME HOW ]                      [ UPGRADE TO L3 ] (disabled)
        |
        v
  WHAT ARCHER TOWER L3 NEEDS
  1. Build a Lumberyard            - not built           [ BUILD ]
  2. Raise it to level 2           - holds 4,000 Wood    [ 1,200 wood . 480 iron ]
  3. Gather 3,150 Wood             - you have 3,000
  4. Upgrade Archer Tower to L3    - 3,150 wood . 1,400 iron
```

The steps are ORDERED, each names its own cost, and each is itself actionable. A step the player can do right now
carries a live door; a step that is itself blocked shows its own blocker rather than a dead end.

## 3. ARCHITECTURE RULING - read before designing

### 3.1 ONE producer, not three copies of the same sentence
There are three refusal surfaces today: `BuildModeController.ShortfallMessage`, `BuildingUpgradeVM`'s status string,
and the Manage card. **Do NOT write the reasoning three times.** Author ONE Core producer - suggested
`DeNelle.Core.Progression.UnlockPath` - that answers, for any (thing, rung):

```csharp
public sealed class BlockerVM
{
    public BlockerKind Kind;       // Affordable | Capacity | VillageTier | BuildingTier | Research | Wave | Quest | Flag | None
    public string Sentence;        // player words, one line: "Your bank tops out at 3,000 Wood."
    public bool HasPath;           // is there anything the player can DO about it
}
public sealed class UnlockStepVM
{
    public string Text;            // "Raise the Lumberyard to level 2"
    public string Detail;          // "holds 4,000 Wood"
    public IReadOnlyList<CostPart> Cost;   // may be empty
    public bool DoableNow;         // the player can act on this step right now
    public Action Activate;        // null when there is nothing to open
}
public static BlockerVM FirstBlocker(string itemId, int targetLevel);
public static IReadOnlyList<UnlockStepVM> StepsFor(string itemId, int targetLevel);
```

Every surface renders these. **Presentation never re-derives a gate** - that is the architecture law in this repo, and
duplicated gating logic is exactly the drift CLAUDE.md sections 2, 5 and 16 were each corrected for.

### 3.2 The ORDER of blockers is a design decision, and it is this
When several things block at once, report the one the player must fix FIRST, not the first one the code happens to
test. Ruled order:
1. **Hard gates that need a different action** - village tier, building tier, research, wave, quest, flag.
2. **Capacity** - the bank cannot hold the cost. This outranks affordability because gathering more will never work.
3. **Affordability** - the ordinary "keep playing" case.
A single refusal line, never a list. The full list lives behind the door.

### 3.3 Capacity is already solved - REUSE IT, do not re-derive
WO-1425 adds a public helper on `TownBankCapacity` that, for a resource and an amount, returns the container level
required and the capacity at that level. **Call it.** `TownBankCapacity.MaxOf` is the single authority on a resource's
ceiling; `ContainerNameFor` already resolves the container's display name from data. Crystals and Coins are UNCAPPED
(`UncappableResources`) - never emit a capacity blocker for them.

### 3.4 The door goes where the dead face is today
The locked Manage card's dead grey face is replaced by the pattern WO-1422 and WO-1423 established for the locked
Research card: **the blocker as a body TEXT LINE, and ONE full-width live door.** A lock reason is a sentence and does
not belong on a button - that was proven on the device tonight when two half-width faces both ellipsized.

### 3.5 Do not invent a new panel if a deck will do
The steps view is a list of rows with costs and doors. Look at what already exists before authoring a new panel type -
the Manage workspace, the player deck, and the upgrade panel all render rows of exactly this shape. Reuse the kit.
⚠ **Any text band under about 24 px renders BLANK**, not small: TMP culls a line whose `fontSizeMin` cannot seat in the
rect. That mechanism cost three separate defects tonight. Do not author a band under that.

## 4. Scope
**In:** the Build palette refusal, the Manage card (all four tabs), the Enhancements/upgrade panel, and the steps view
they all open. The `UnlockPath` producer and its oracle.
**Out:** changing any cost, cap, tier requirement or balance value - **the owner rules on balance, and the audit proved
the numbers are internally consistent**; new art; the raid/army surfaces; the tutorial.

## 5. Regression - `UnlockPathRegression`, marker `UNLOCK_PATH_OK` / `_FAIL <case>`
Author each case with a one-line REVERT RECIPE comment; the CLI proves RED then GREEN.
1. `[every-gate-speaks]` for every gated rung in the game, `FirstBlocker` returns a non-empty `Sentence`. Iterate the
   real catalogs, not a fixture. **A rung that blocks silently is the whole defect.**
2. `[capacity-outranks-affordability]` a cost above the achievable cap reports `Capacity`, never `Affordable`.
3. `[hard-gate-outranks-capacity]` a village-tier-gated rung whose cost also exceeds the cap reports `VillageTier`.
4. `[steps-terminate]` `StepsFor` always terminates and never returns a cycle. Guard against a prerequisite loop.
5. `[steps-are-ordered]` each step's prerequisites appear before it.
6. `[no-dead-door]` every step with `Activate != null` opens something registered; every step with `HasPath == false`
   states why in `Sentence` rather than offering nothing.
7. `[reachable-or-named]` every rung is REACHABLE or its blocker `Kind` explains it. Cross-check against the same
   reachability rule WO-1423 added, so an unreachable rung cannot be silently reintroduced.
8. `[one-producer]` no refusal surface re-derives a gate: the three surface files contain no direct comparison against
   `VillageTier`, `MaxOf`, or a tier requirement - they call `UnlockPath`. RED: inline a comparison in one of them.

## 6. Acceptance
- [ ] Brace balance + NUL scan on every `.cs`; new `.meta` guids unique.
- [ ] `COMPILE_GATE_OK`; `REGRESSION_OK n/n` with `UnlockPathRegression` green and all eight RED proofs recorded.
- [ ] Headless capture of the refusal on a Manage card AND the steps view, at 2670x1200 and 1920x1080, OPENED by the
      CLI: the blocker sentence reads in full, the door is >= 112 px, no band is blank.
- [ ] **The owner's own case, end to end:** an Archer Tower at L2 with a level-1 Lumberyard reports the capacity
      blocker, and the steps view names the Lumberyard upgrade with its cost.
- [ ] Owner felt-test closes it.

## 7. Open for the owner
1. Where the steps door lives on each surface - one entry per card, or a persistent "what do I need?" affordance.
2. Whether the steps view should also show things she has ALREADY satisfied (a checklist that ticks off) or only what
   remains. A checklist teaches the system; a remainder list is shorter.
3. Whether an unreachable rung should be hidden entirely rather than shown with an honest blocker.
