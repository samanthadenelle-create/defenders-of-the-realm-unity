# WO-1391: the building upgrade page - noise in the 3D preview, "Missing resources" with the resources on the strip, truncated face, off-kit styling

**Status:** IN PROGRESS 2026-09-05 00:29 - landed + gated (COMPILE_GATE_OK, REGISTERED_SECONDARY_CAPTURE_OK 33/33 touch=clean, `Builds/ui-capture/BuildingUpgrade_2670x1200.png`); awaiting the next APK + the Cathedral page on the Seeker. Finding for the owner: "Missing resources" was TRUE - the tier costs 800 gold and the page hid the gold line; and `arcane-tower` tier-1 authors `costCrystal: 1280` while the service charges WOOD by tier number (ruling needed). Found on the headed walk 2026-09-04 23:48 (build 355952); it is the page EVERY Research door and every Buildings-tab Upgrade lands on.

## Evidence
`docs/qa/UI_REVIEW_2026-09-05/14-research-door-result.png` - "Cathedral of Magic Enhancements", reached from
Manage - Research "UPGRADE CATHE..." (WO-1390's door; `BuildingUpgradePanelMvvm:Update()` running).
Owner: "what was that pixelated image on cathedral?"

## The five defects on the one page (each visible in the PNG)
1. **The 3D preview square is GPU noise** - random coloured blocks. That is an uninitialised RenderTexture:
   the off-screen rig (`BuildingUpgradePanelMvvm.cs:1953` "the model rig owns a RenderTexture + an off-screen
   camera/light/prefab instance") never drew into it. Two candidate causes, NOT yet proven: the Cathedral's
   model failed to resolve into the rig, or the rig camera never rendered (no `[Flow:UpgradeUI]` line at open
   in the device log - only `close`). Instrument the rig (model resolved? camera rendered? RT created?) and
   read the line before fixing; on failure show the building's catalog icon, never raw memory.
2. **"Missing resources" with 4000 wood on the strip and a 1280 Wood price** - the affordability predicate
   disagrees with the strip's own numbers (`UpgradeActionState.MissingResources`, `:366`). Read what the VM
   compares (a different ledger? crystals hidden in the cost? the 706 coins?) and make the sentence name the
   missing thing: "Short 300 iron", never a bare "Missing resources".
3. **The primary face reads `UPGRAD...`** - the label does not fit its plate at 2670x1200.
4. **An empty black box glyph** beside the sentence (the "EMPTY-BOX glyph" the header at `:48` documents)
   reads as a broken checkbox to a player.
5. **Off-kit styling**: flat dark plates, thin gold hairlines, a plain `Close` - not `BuildObsidianButton` /
   the medieval frame every other screen uses. Same building, two visual languages one tap apart.

## Rulings to honour
One upgrade page (ARCHITECTURE s6 - never a second family resolver or start path); state by words; ASCII;
touch >= MinTouchPx; kit primitives only; the preview shows the model or the icon, never noise.

## Acceptance
- [ ] Headless capture `BuildingUpgrade_2670x1200.png` regenerated: a model (or icon) in the preview, the
      cost line, the face fully readable, kit styling.
- [ ] On the Seeker: open the Cathedral page with >= its cost banked -> UPGRADE is enabled and starts the
      job; with less -> the sentence names the shortfall.
- [ ] `BuildingUpgradeRegression` / `PlacedUpgradePageTruthRegression` green; a pin that the preview RT is
      rendered-or-fallback (never left uninitialised); RED first.
