# WO-1519 RESULT - the raid deploy screen, rebuilt around the decision

**Status:** IMPLEMENTED - 2026-09-06 uncommitted, awaiting gate. Edit-only lane: no Unity run, no gate,
no commit. Every number below was read at source or measured this session; nothing is asserted from a doc.

## WHAT LANDED

**Hierarchy (section 2).** The right column is now ENEMY BASE hero card (203 ref px) / SPOILS chips (40) /
SCOUT REPORT (157) instead of a 72 px stat pair, a 148 px guide block and a 181 px prose well. The card
carries the boss emblem at plate size plus Power and Recon as two big numerals with captions
(`RaidDeployScreen.cs:BuildEnemyHeroCard`, `BuildCardStat`). The left column gained a third band: the ARMY
row, its own plate, its word composed by the VM (`RaidDeployVM.ArmyBandText`, "ARMY 10 / 10 - FULL" -
WO-1517's grammar). Spoils are three icon+number chips through the kit's existing
`ElarionUiKit.CostRow` (`CostFormat.cs:95`), not a new chip authority.

**Echo Guide removed (section 2B).** `BuildGuideBand` / `RefreshGuideBand` / `OnCycleGuide`, both band
labels, `GuideTextX0/X1` and `GuideBandY0/Y1` are gone from `RaidDeployScreen.cs`. `EchoGuideService`, the
24 lines, `EchoWorldPresence` and the `NoteExpeditionTarget` seam (`RaidDeployScreen.cs:1053`) are intact,
and `EchoGuideMemoryRegression`'s `[no-effect]` scope fence was NOT touched.

**Colour (section 2.5).** `DifficultyColor` is deleted; the difficulty is the WORD plus the diamonds.

## EVIDENCE - three things measured, not assumed

1. **The old band budget was under its own law.** `ElarionUiKit.FitSingleLine` floors at
   `ElarionUiKit.FontFloor` = 30 (`ElarionUiKitObsidian.cs:3033`), and TMP's Ellipsis overflow CULLS the
   whole line when the floor's line exceeds the rect (`ElarionUiKitObsidian.cs:3096-3110`);
   `UiKitTextFitGuard` relaxes it at RUNTIME only, so it never runs in the headless capture the acceptance
   PNG comes from. `RaidSelectionScreen.NeedPx(30)` = **38.58**. The WO-1385/1403 banner claimed 36 px rows
   "seat WITHOUT the runtime relax guard". They do not. Every band is now >= 39 px on the 411 px body.
2. **There is no boss portrait.** `Assets/Resources/Portraits/` holds buildings, walls and `Sylas.png`;
   necromancer / orc / warlord / berserker return nothing. `Assets/Resources/RpgUi/emblem/Necromancer.png`
   WAS OPENED AND LOOKED AT - a full-colour dripping skull. All four camps' bosses resolve to it. WO-1509's
   missing-albedo finding is the FBX, a different pipeline. Section 3's open question is closed.
3. **There is no camp art either.** No per-camp image under `Resources`; `RaidSelectionScreen` loads only
   frame sprites. The card is boss-led, with a place to hang camp art when a pipeline exists.

## FILES

| File | +/- | What |
|---|---|---|
| `Assets/_Modules/Village/Hero/RaidDeployScreen.cs` | +386 / -246 | band table, hero card, chips, guide removal |
| `Assets/_Modules/Village/Hero/RaidDeployVM.cs` | +150 / -3 | ArmyBandText/ArmyFull, SpoilsChips, ScoutIntel, boss |
| `Assets/Editor/Regression/RaidDeployLayoutRegression.cs` | +NEW 640 | the measured suite |
| `Assets/Editor/Regression/RaidDeployUiRegression.cs` | +72 / -55 | case 4 reads the live table, not a regex |
| `Assets/Editor/Regression/EchoGuideMemoryRegression.cs` | +40 / -5 | `[tappable]` retargeted by the ruling |

## REGISTRATION (DataRegression.cs NOT edited - sole-committer's lane, and already dirty)

```
if (!RaidDeployLayoutRegression.Run(out var raidDeployLayoutReason))
    failures.Add(raidDeployLayoutReason);
else log.AppendLine("[raid-deploy-layout] " + raidDeployLayoutReason);
```
Place it beside the existing `raid-deploy-ui` / `raid-deploy-zero-army` lines.

## OWED - not claimed, and each one is a real gap

- **Headless `RaidDeploy_2670x1200.png` captured, OPENED, plus a GREYSCALE copy.** No Unity run in this lane.
- **Owner felt-verify.** Only she can judge "pop".
- **Cap-aware / repeat-aware spoils (section 2.4).** WO-1461 is READY, not landed. The chips quote what the
  selection row quotes; "the number shown is the number that will bank" is NOT met.
- **Guide selection has no UI home.** Recorded in `WORK_ORDER_1380`'s appended section; needs a new WO.
- **`REGRESSION_OK n/n` on a fresh log**, after registration.
- **WO-1464's in-raid half** (`RaidDeployController` tray + top band) - see that ticket's appended section.
