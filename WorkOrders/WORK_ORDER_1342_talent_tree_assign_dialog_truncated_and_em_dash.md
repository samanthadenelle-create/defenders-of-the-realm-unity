# WORK ORDER 1342 - The talent-tree assign dialog cuts its own sentence off, and ships an em dash

**Status:** FIXED 2026-09-03 - shipped in 2026.09.03.353999. Description wraps and reads in full ("Assignable to the hot-swap bar."); the frame now encloses the modal - content-panel.png alpha starts at row 94 so its whole 96px top slice is transparent, and insetting content by the painted margin was the fix (growing the popup never could be, it needed H >= ~582). 67 em dashes + 1 section sign removed from both hero-talents.json twins. State no longer hue-only. Gates COMPILE_GATE_OK + REGRESSION_OK 358/358. Owner felt-verified the frame and the sentence on device. FINDING NOT FIXED: the right-rail Echoes chip shows through a full-screen panel (HUD kit lane).
**Silo / Lane:** HUD / talent tree - confirmation dialog text layout + canon strings
**Type:** EXISTING (built and shipped; text layout + one authored string)
**Minted:** 2026-09-03 (CLI) from a device screencap taken on the owner's ask.
**Severity:** P2 - it sits on the exact route WO-1340's FTUE is about to teach, and the missing half
of the sentence is the half that tells the player what the skill does.

## The owner's report, verbatim

> *"screenshot of the skill tree screen small ui bug"*

She named it small. **Two of the four defects in the capture are not, and she cannot have meant them -
she is red/green colourblind and cannot see (c) at all.** Fix all four; do not scope down to the one
she could see.

## The capture

Build `2026.09.03.353742`, Seeker, 2670x1200, `TALENT TREE` screen with the `Mend` node tapped, so the
assign-confirmation dialog is open over the tree. Capture is at
`Builds/` - re-take with `adb shell screencap -p /sdcard/x.png` then `adb pull` if you need it.
⚠ Do NOT capture with a PowerShell `>` redirect - it corrupts the PNG.

Verbatim, as rendered:

```
                       Mend
   Unlocks Mend - a small self-heal (25 HP, 12s cd). Assignable to
                 Owned - Active skill
        [ CANCEL ]            [ IN SLOT 1 ]
```

## THE FOUR DEFECTS

### (a) ⭐ The description is TRUNCATED MID-SENTENCE, with no ellipsis
`...(25 HP, 12s cd). Assignable to` - and then nothing. The line is drawn as a **single line that does
not wrap**, and it simply stops at the dialog's inner width. There is no `...`, so **nothing signals
to the player that text is missing** - it reads as a typo, not as an overflow.

The authored string almost certainly continues ("Assignable to a quick-swap slot" or similar - the hint
line below the dialog reads `Mend -> quick-swap 1.`). **Find the full authored string and confirm what
is being lost before you touch layout** - if the string genuinely ends at "Assignable to" then the
defect is the STRING, not the layout, and both need fixing.

**The fix is to WRAP, not to shrink and not to ellipsise.** The dialog has vertical room to spare -
there is empty space between the description and the state line, and more between that and the buttons.
Let the description wrap to two or three lines and grow the dialog's height to fit.

⚠ **`ElarionUiKit.FitSingleLine`'s `minSize: 0` silently resolves to `FontFloor` (30), NOT
`FontHardFloor` (20) - pass your floor EXPLICITLY.** That exact trap ellipsised a store label earlier
today. If this dialog is using `FitSingleLine` at all, that is likely the direct cause, and the answer
is to stop treating a sentence as a single line rather than to lower the floor.

### (b) ⭐ An EM DASH (U+2014) is shipping in a player-facing string
`Unlocks Mend - a small self-heal` is authored with a real em dash. **ASCII-only is binding in every
player-facing string** and a tofu oracle fails on exactly this. On a device without the glyph this
renders as a tofu box in the middle of the sentence.

- Replace with ` - ` (space hyphen space) or restructure the sentence.
- ⚠ **`Owned - Active skill` may be a second instance** - verify which dash character each is.
- While you are in that file, check its NEIGHBOURS: an authored string with an em dash is rarely alone,
  and every talent description lives in the same data file. Fix the ones in the file you are already
  editing. **Do NOT expand into a repo-wide sweep** - that is WO-1333's lane and it is 57 files.

### (c) State is carried by GREEN alone
`Owned - Active skill` is rendered in green. **The owner is red/green colourblind and cannot read
that.** The word "Owned" is already doing the work, so the colour is redundant rather than load-bearing
here - but confirm there is no sibling state (Locked / Unaffordable / Available) that is distinguished
from it ONLY by hue. If there is, that is the real bug: give each state a distinct WORD or shape,
and keep colour as reinforcement only.

### (d) A right-edge label is clipped outside the content rect
At the right edge, behind the dialog, a label reads `hires 1/6` - it is clipped by the panel frame.
The full word is likely a tier/spec name ending in "...hires" or a counter label. Something is laid out
past the tree's content width. **Report what it is; fix it only if it is in this dialog's own lane.**
If it belongs to the tree's node/lattice layout, ⛔ **STOP - that is WO-1310's lane** (see below) and it
goes back as a finding, not a fix.

### (e) ⭐ THE FRAME DOES NOT ENCLOSE THE MODAL - the owner's own catch

> *"did you get the ui bug the frame around the modal"*

The ornate gold border wraps only the **lower band** of the dialog - roughly the `CANCEL` /
`IN SLOT 1` row - while the black content plate carrying `Mend`, the description and
`Owned - Active skill` extends **above the frame's top edge and past its right edge**. The modal reads
as a black rectangle with a frame stuck on its bottom-left. The frame's rect and the content's rect are
sized independently and only one of them is following the content.

**Treat (e) and (a) as ONE fix until proven otherwise.** If the black plate auto-sizes to the
overflowing single-line description while the frame is sized from an authored constant - or from a
`preferredHeight` read BEFORE the text is assigned, the classic one-frame-stale measure - then wrapping
the description and rebuilding the layout should bring both back into agreement. Verify; if they are
genuinely independent, say so.

Report: which rect owns the frame art and which owns the plate (file:line); whether the frame is a
nine-slice sprite or separate corner/edge pieces pinned to authored offsets (if the latter, that is why
it cannot follow content, and the fix is one sliced frame parented to the content rect); and where the
frame's size is read from relative to the text assignment.

## ⛔ BINDING - THE TREE ITSELF IS SOMEBODY ELSE'S LANE

Do NOT touch `HeroSkillTreePanelMvvm.cs`'s layout solver, axis rotation, lattice/pitch maths, extents,
or node-plate label sizing. That was rewritten under **WO-1310** and is awaiting the owner's
felt-verify. **This ticket is the CONFIRMATION DIALOG and the strings it draws.** If the fix appears to
require moving a node or changing the lattice, you are in the wrong file - come back with the finding.

## ⛔ OTHER LIVE LANES - stay out

- **WO-1341** - the Hero panel's duplicated labels (`Hero -> Skills` route, the four cards). An agent is
  in those files RIGHT NOW. ⚠ It is making the Hero panel's font and format **match the Manage
  screen**. If this dialog shares a style helper with it, **do not both edit that helper** - report the
  collision instead.
- **WO-1340** - Tutorial V2 / `tutorial-steps.json`. It is adding an FTUE beat that teaches spending a
  skill point, and it may attach a highlight to this very dialog or to the `IN SLOT 1` button.
  **Do not rename or re-parent any rect in this dialog** - a highlight resolves by name.
- **WO-1339** - `BOARD.html`, `tools/board_build.py`, `tools/owner_validations.py`,
  `proof/owner-validations.json`.
- The decimation lane (`Assets/HeroContent`, hero FBX + metas) and the store lane (`PackStore`,
  `NightMarket*`, `packs.json`, `canon-strings.json`, `hud-areas.json`).
- WO-1337's files: `Enemy.cs`, `BattleArena.cs`, `PanelManager.cs`, `BattleQuiescenceGate.cs`.

## Constraints

- UI is code-built uGUI via `ElarionUiKit`. **UXML DOES NOT WORK IN BUILDS** - do not reach for it.
- Phone-first landscape. Touch targets **>= 112px**. `CANCEL` and `IN SLOT 1` already clear that; do not
  shrink them to make room for the wrapped text - grow the dialog instead.
- ⚠ If any of these strings is baked into card/dialog ARTWORK rather than drawn as text, **say so
  plainly and do NOT re-author the art** - that is the owner's call. There is precedent from today: the
  store's network indicator turned out to be baked into `network-frame.png` and printed "Mainnet" over
  a Devnet session.
- Never hand-edit a `.unity` scene.
- Do not run a Unity gate, do not commit, do not build. The lead does all three.

## Oracle

Pin (1) that the description string is fully rendered - i.e. the dialog's text is allowed to WRAP and
its measured rendered length equals the authored string's length, so a future one-line regression is
caught; and (2) that no talent-tree player-facing string contains a non-ASCII codepoint.

**Prove it RED first and report the mutation.** (2) should already be red against the em dash on HEAD -
if it is not, your scan is not reaching the file that holds the string, and finding that out is the
point of proving red. Extend an existing HUD or canon-string suite rather than adding a new one.

## Acceptance

- [ ] The full description sentence is visible - wrapped, not shrunk, not ellipsised. Say what the
      authored string actually is and what was being lost.
- [ ] No em dash (or any non-ASCII codepoint) in any string this dialog draws; neighbours in the same
      file fixed; no repo-wide sweep.
- [ ] No state in this dialog is distinguished by hue alone. Report whether a sibling state existed
      that was.
- [ ] `hires 1/6` identified. Fixed if in-lane; reported as a WO-1310 finding if not.
- [ ] The frame encloses the content on all four edges, pinned numerically by the oracle. State whether
      (e) shared a root with (a).
- [ ] Oracle proven RED first, mutation reported.
- [ ] Brace + NUL check reported per `.cs` file touched.
- [ ] Owner felt-verifies on device and CLOSES - a text-layout defect is judged by eye.
