# WO-1443: Manage/Army - three stacked headings become one, empty troop portraits, and 40% dead space

**Status:** FIXED - ON THE SEEKER 2026.09.07.358574 - landed in `32659c0f6` + `949e848a0`; re-verified at source and
against a fresh capture 2026-09-06 (see section 7). No further code change required; owner felt-verify closes it.
The one defect found while verifying (section 7B, the unpainted QUEUE face count) is minted as WO-1444.
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

- [x] One heading only, reading `MANAGE / ARMY` in the title position. The old section heading and sub
      line are gone.
- [x] Every troop tile shows art, proven by a **headless capture with the PNG opened and looked at**.
      A missing sprite must also LOG, never fail silently.
- [x] The dead band is resolved by the option you chose, with the reasoning recorded.
- [x] A regression asserts no heading is rendered twice - that is the general form of this defect and it
      would catch the same thing on BUILD and RESEARCH.
- [x] `REGRESSION_OK n/n`.

---

## 7. VERIFICATION PASS 2026-09-06 (implementation lane) - ALREADY LANDED, NO EDIT MADE

This ticket was **written and implemented inside the same two commits** (`32659c0f6`, `949e848a0` - the
mockup capture loop). Its `Status` line was never flipped, so it read READY while the tree was done. An
implementation lane re-opened it, verified every criterion **at source and against a fresh capture**, and
found **nothing left to change in `ManageScreenPanel` / `ManageScreenVM` / `ManageArt`**. No `.cs` was
edited. Evidence, each read this session:

- **§1 one heading.** `ManageScreenPanel.ApplyWorkspaceTitle` (`:1117`) is the single title writer;
  `RenderWorkspace` binds the model's string at `:1218` (`ApplyWorkspaceTitle(workspaceVm.HeaderTitle)`).
  `HeaderSubtitle` survives **only inside comments** recording its deletion
  (`ManageViewContract.cs:458`, `ManageScreenVM.cs:3359`) - no live declaration, no live read.
- **§1B chrome.** `HEART L1` left the chrome row: `ManageScreenPanel.BuildHeartFace` (`:2449`) now calls
  `BuildHubHeartDoor()`, with `HeartSurfaceRegression`'s `[heart-has-a-door]` pin moved with it - **the
  door is NOT stranded**, which §1B required be proven before removal. The `IDLE . 0 OF 5` line is gone
  (`ManageScreenVM.cs:3451`). **The QUEUE pill deliberately SURVIVES** - `MANAGE_MOCKUP_8_SCREENS.png`
  draws it as a top-right pill with a red count badge, and the mockup is the spec; §1B's "remove all
  three" is **superseded on that one element**.
- **§2 portraits.** `Builds/ui-capture/ManageFlow_ARMY_gridtop_2670x1200.png` (18:39, post-commit) opened
  and looked at: **all nine troops render art** - Footman, Archer, Spearman, Field Cleric, Shieldguard,
  Outrider, Siege Catapult, Battlemage, Echo Legionnaire. The empty-frame defect is gone.
  `ManagePortraitCoverageRegression` is registered (`DataRegression.cs:1050`).
- **§3 dead band.** Hint sentence absent from live code; `ManageOneHeadingRegression`'s
  `[empty-band-collapses]` case pins both the `Selection.Visible` gate and the sentence's absence.
- **§4 nine troops.** **RESOLVED, not interim** - the capture shows a 3x3 grid of nine, so the
  "7 of 9 unreachable" state this section warned about no longer exists.
- **Regression.** `Assets/Editor/Regression/ManageOneHeadingRegression.cs` exists, self-tests all 11 of
  its patterns against positive AND negative fixtures, and is wired at `DataRegression.cs:1068`.
  `REGRESSION_OK 414/414` recorded on `Builds/reg-final2.log` in `949e848a0`.

### 7B. ONE REAL DEFECT FOUND WHILE VERIFYING - NOT WO-1443's, NOT FIXED HERE

§1B deleted the `IDLE . 0 OF 5` line on the understanding that **the count would ride on the QUEUE face
instead**. The model duly composes it - `ManageScreenVM.cs:3481`:

```csharp
FaceCountText = cap > 0 ? (full ? "FULL" : depth + "/" + cap) : null,
```

**Nothing paints it.** A repo-wide search for `FaceCountText` returns the declaration
(`ManageViewContract.cs:343`), two comments, and that one assignment - **no reader**. The pill's label is
hardcoded at `ManageScreenPanel.cs:2091` (`label.text = "QUEUE";`), and the red disc beside it is counted
by the **View's own** sum across channels (`ManageScreenPanel.cs:2368-2372`), not from the model's
per-channel depth. So there are two authorities on "how full is the queue", one of them unreachable -
which is precisely the composed-but-unpainted duplicated state `ManageViewContract.cs:337-341` claims to
have avoided. The word `FULL` never reaches the screen at all.

**Deliberately NOT fixed in this lane**, and the reason matters: the screen as it renders today MATCHES
the owner's mockup (bare `QUEUE` + red badge), so "fix" means choosing between painting `FaceCountText`
(contradicts the picture) and deleting it (drops the `FULL` affordance §1B assumed). That is an owner
call, not a lane call. **Needs its own ticket.**

**One instrument note for the lead, not a defect here:** `ManageFlow_ARMY_gridtop` and `_gridbottom` are
**byte-identical** (md5 `1b039c09...`), as are the RESEARCH pair (`7ba1192a...`). That is the expected
result of a grid that fits without scrolling, but WO-1444 made two identical frames a FAILURE condition -
confirm the harness exempts the no-scroll case, or those two frames will read as the stale-duplicate bug.
