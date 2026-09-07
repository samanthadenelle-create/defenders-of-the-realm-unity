# WO-1442 RESULT - the raid selection screen: no stray bar, no clipped card, no world bleed

**Status:** FIXED - ON THE SEEKER `2026.09.07.358574` (installed 2026-09-06 19:20). Awaiting the owner's
felt-verify and a post-fix headless PNG (the one acceptance item still open).
**Commit:** `32659c0f6` (2026-09-06 16:51), bundled under a `feat(manage,build)` title; the WO Status was not
flipped in that commit and this RESULT closes the gap.
**Files:** `Assets/_Modules/Village/Hero/RaidSelectionScreen.cs` (+366), `Assets/_Modules/Village/Hero/RaidSelectionVM.cs`
(+42), `Assets/Editor/Regression/RaidSelectionLayoutRegression.cs` (new, 831 lines, registered at
`Assets/Editor/Regression/DataRegression.cs:1414`).
**Gates on fresh logs postdating the commit:** `COMPILE_GATE_OK` (18:48), `REGRESSION_OK 414/414` (18:50)
including `RAID_SELECTION_LAYOUT_OK - 4 surfaces x {4,8} camps MEASURED on a live canvas`.

## Acceptance, verified at source (read-only re-verification 2026-09-06 19:20)

- **D1 stray bar** - it was `button-pressed-empty` swapped in by `MedievalUiSkin.ApplyButton`'s
  `Transition.SpriteSwap` (`MedievalUiSkin.cs:74-80`); the card now uses `StyleButtonColors`
  (`RaidSelectionScreen.cs:830-863`). Pinned by case S1.
- **D2 clipping** - the well band is derived from the live `chrome.layout.footer` / `.subHeader` via
  `ComputeWellBand` (`:552-577`), rail widened to 18 ref px (`:706-712`), a VM-owned camp-count sentence seats in
  FrameCore's sub-header band (`:735-757`; FrameCore authors that band, `ElarionUiKit.cs:456-457`).
- **D3 world bleed** - `withBackdrop` is no longer passed, so the kit's 0.94-alpha backdrop is built
  (`ElarionUiKit.cs:568,573-579`). Pinned by S4.
- **Lock copy** preserved verbatim (`RaidSelectionVM.cs:515`), pinned by S6.
- [ ] **Post-fix PNG opened at 4 and 8 camps** - `Builds/ui-capture/RaidSelection_2670x1200.png` is dated Sep 5
  23:56 (pre-fix). Owed by the next headless capture run.

## Findings carried forward
- Ticket line 12 cites `scratchpad raid-ui.png`, absent; the committed frame is
  `Logs/device/screens/seeker-357453-raids.png` (build 357453, ticket says 358245). All three defects are visible in it.
- `Logs/device/screens/owner-raid-ui-2026-09-06-143701.png` is a MID-RAID HUD capture, so the in-fight ticket the
  WO said was waiting on a capture can now be minted.
- `RaidDeployScreen.cs:144-145` still passes `withBackdrop: false` with `FrameCore` - the same D3 class on the
  sibling door, out of this silo, not covered by any suite. Candidate follow-up.
- Card 3's partial peek is deliberate: `VisibleCardCapacity` drives only the caption's words, never layout.
