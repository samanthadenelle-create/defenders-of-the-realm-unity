# WO-1443: Manage/Army - three stacked headings become one, empty troop portraits, and 40% dead space

**Status:** READY TO IMPLEMENT
**Silo:** `ManageScreenPanel` / `ManageScreenVM` + `ManageArt`. **⚠ A concurrent lane owns
`RaidSelectionScreen`** - different files, do not stray.
**Source:** owner felt-test 2026-09-06 on build **2026.09.06.358245**, verbatim:
> *"first UI screenahot that is off"* ... *"remove the manage army and sub line replace the manage top"*

**Evidence: `adb screencap` from her device this session** (scratchpad `now.png`), plus the device log.

---

## 1. THE OWNER RULING - implement exactly this

The screen currently stacks **THREE** headings down the top of the panel:
```
MANAGE                          <- panel title, centred
MANAGE / ARMY                    <- section heading
Every troop, unlocked or not.    <- sub line
```

**Her ruling: delete the section heading AND the sub line. The breadcrumb moves INTO the panel title.**
So the top reads `MANAGE / ARMY` and nothing repeats beneath it. One heading, not three.

This is not only tidiness - **it is where the reclaimed vertical space comes from**, which is what makes
section 3 fixable.

## 1B. SECOND OWNER RULING, same felt-test: *"remove heart level queue"*

The top row also carries a **`HEART L1`** chip beside BACK, and a **`QUEUE`** chip on the right with an
**`IDLE . 0 OF 5`** line under it. **Remove all three.**

Combined with section 1, the entire top of the screen becomes:
```
BACK        MANAGE / ARMY
```
and nothing else. That is four separate chrome elements deleted from one screen.

⚠ **The QUEUE chip is a DOOR, and doors are load-bearing here.** Before deleting it, establish that the
queue is still reachable - WO-1430 found three panels no player could open, and `PanelDoorRegression`
exists to stop a fourth. If this chip is the only route to the queue, **stop and report** rather than
stranding it. Canon (CLAUDE.md section 7) records the bar's `Upgrade` face as the single Queues entry and
the Builders chip as a status glance, so a route very likely survives - **prove that it does, do not
assume it.**

Same question for `HEART L1`: if it is the only door to the Heart surface (WO-2017), say so before
removing it.

## 2. EMPTY TROOP PORTRAITS - a predicted gap, now confirmed

In the capture, **Footman and Archer render as EMPTY frames**; only Spearman shows art.

**The WO-2001 lane called this shot before it happened** and recorded it as NOT VERIFIED rather than
claiming success:
> *"I set `PortraitKey = "RpgUi/troop/" + IconId` because `RpgUiCatalog` serves that role from
> `Resources/RpgUi/troop`. If those are sub-sprites of a sheet, `Resources.Load<Sprite>` on that path
> misses; `ManageArt` logs it once and the slot renders transparent (never a white box). Needs a capture."*

**This is that capture, and the miss is real.** Corroborated in the device log: BUILDING portraits
resolve and trace normally (`icon='Portraits/forge'`, `'Portraits/barracks'`, `'Portraits/farm'`), while
**no troop portrait line appears at all**.

**Establish whether those troop sprites are sub-sprites of a SHEET.** If they are,
`Resources.Load<Sprite>` on a bare path cannot reach them and the fix is the loading call, not the key.
Do not change the key until you know which. `ManageArt` already logs a miss once per key - read that
line first.

## 3. 40% OF THE SCREEN IS AN EMPTY BOX

Below the three troop tiles sits a very large bordered panel containing **one sentence**:
*"Pick one to see what it does, what it costs and what you can do."* It is roughly 40% of the screen
holding a single line of text.

That band is the SELECTION card, correctly reserved but empty because nothing is selected.

### OWNER RULING, same felt-test: *"dont need the bottom line, close button is enough"*

**Delete the hint sentence entirely.** She is right that it explains something the interface already
makes obvious - you tap a troop, you see the troop - and the CLOSE button is the only control that band
needs to carry.

**With the sentence gone the band has nothing to hold, so it must COLLAPSE when nothing is selected**
and the tiles take the room. Do not leave an empty bordered box with a lone button in it; that is the
same 40% of screen doing even less. Reserve the band only when a selection exists to fill it.

Together with sections 1 and 1B this removes SIX elements from one screen - a section heading, a sub
line, a Heart chip, a Queue chip, an idle counter and a hint sentence - and every one of them was
telling the player something the screen already showed.

## 4. ONLY THREE TROOPS ARE REACHABLE

The tile row shows Footman / Archer / Spearman and is **cut off at the right edge**. There are NINE
troops. The 3x3 grid is WO-2008 (Wave 2) and is not built yet, so this is the interim shape - **but note
it explicitly in the RESULT**, because "7 of 9 troops unreachable" is the exact defect the seam oracles
were written to catch (WO-1430). Do not let the interim state quietly become the shipped state.

**Do NOT build the 3x3 grid here.** That is WO-2008's job and it has its own spec. Fix the heading, the
portraits and the dead space; leave the grid.

## 5. CONSTRAINTS

- **Any text band under ~24 px renders BLANK, not small.** If reclaiming space shrinks a band past that,
  it deletes the text instead of scaling it. Documented trap in this codebase.
- Touch targets: `ElarionUiKit.MinTouchPx` (112).
- The owner is red/green colourblind - meaning in words and layout, never hue alone.
- Several suites read `ManageScreenPanel` body text as SOURCE TEXT (`ManageBuildingsCardRegression`,
  `ManageProgressiveDisclosureRegression`, `ManageQueueDrawerRegression` and others). **Expect to move a
  pin, and move it WITH the ruling recorded in-file** - never delete a case to go green.

## 6. ACCEPTANCE

- [ ] One heading only, reading `MANAGE / ARMY` in the title position. The old section heading and sub
      line are gone.
- [ ] Every troop tile shows art, proven by a **headless capture with the PNG opened and looked at**.
      A missing sprite must also LOG, never fail silently.
- [ ] The dead band is resolved by the option you chose, with the reasoning recorded.
- [ ] A regression asserts no heading is rendered twice - that is the general form of this defect and it
      would catch the same thing on BUILD and RESEARCH.
- [ ] `REGRESSION_OK n/n`.
