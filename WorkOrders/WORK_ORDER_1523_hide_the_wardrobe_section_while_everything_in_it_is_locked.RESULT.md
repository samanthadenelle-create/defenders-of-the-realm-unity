# WO-1523 RESULT - the Wardrobe card is absent from the Hero screen until a cosmetic unlocks, then arrives carrying NEW

**Status:** SOURCE COMPLETE - uncommitted in the working tree as of 2026-09-06 21:45, awaiting the wave-two gate.
**Tree contradicts the ticket:** its Status line still reads `READY TO IMPLEMENT` while the work sits in the tree.
(Status line not edited here - RESULT-only lane.)
**Commit:** none. Edit-only lane.
**Files:** `Core/HudModel/HeroDeckWardrobeVM.cs` and `Core/HudModel/CosmeticSignals.cs` (both NEW, untracked),
`HUD/PlayerDeckWorkspace.cs:636,662,674-697`, `Cosmetics/CosmeticOwnershipService.cs`,
`Cosmetics/CosmeticApplier.cs`, `Assets/Editor/Regression/CosmeticShopReachabilityRegression.cs:38-41,62,265,281`
(cases G and H).
**Gates:** none. `Builds/cg-quiet.log` `COMPILE_GATE_OK` is 20:04 and the owner's ruling arrived 20:23, so the gate
predates the lane. `Builds/cg-aab.log` (20:54) is RED (42x `CS0103`, the Manage lane's half-written suites).

## 1. What landed

`HeroDeckWardrobeVM` is the one authority. `WardrobeHasUnlocked` (`:35`) is `OwnedCount > 0` (`:47`);
`WardrobeIsNew` (`:48`) is `WardrobeHasUnlocked && !seen`; `NewWord` (`:26`) is the literal `"NEW"` - a WORD, not a
hue, per the owner's colourblindness. A permanent FlowTrace line at `:63` reports `show=<bool> new=<bool>`, `:83`
logs `hero deck wardrobe: NEW cleared (opened)`, and `:87` exposes a test seam so a suite can clear the seen flag
and measure a genuine first unlock. `CosmeticSignals` is a new pure static in `DeNelle.Core.HudModel` mirroring
`PostureSignals` exactly - producer pushes, consumers read - so the HUD reads a fact rather than reaching into
Cosmetics.

`PlayerDeckWorkspace` builds the Hero block's fifth card conditionally: `:693-697` constructs
`HeroDeckWardrobeVM.FromCurrentState()` and adds `Route("Wardrobe", "Looks for your hero, Echo, and town",
"wardrobe", PanelId.CosmeticShop)` only when `WardrobeHasUnlocked` is true. The note at `:674-675` states the card
is **ABSENT from the list**, not collapsed - sec.3's requirement that a measured layout case must not still find
it. The stale purpose line promising the wardrobe unconditionally was corrected at `:88-92`.

## 2. Acceptance

- [x] With ZERO unlocked cosmetics the wardrobe section is ABSENT - source at `PlayerDeckWorkspace.cs:694`, pinned
      by case G (`CosmeticShopReachabilityRegression.cs:265,281`: the route is conditional on the VM and the card
      is absent rather than greyed).
- [x] With one unlocked cosmetic the section is present and carries `NEW` - case H (`:41`, "hide at owned=0,
      show + NEW at owned=1").
- [ ] A fresh Hero screen capture opened in the RESULT - **OPEN**, no capture run.
- [ ] `REGRESSION_OK n/n` on a fresh log - owed; the suite has never executed because the tree does not compile.

## 3. Not done, and confirmed not needed

The wardrobe itself is untouched (sec.3): `CosmeticShopPanel` and `PanelId.CosmeticShop` are unchanged, so the
feature returns intact the moment something unlocks. Nothing was collapsed to zero height.

## 4. Owed

The wave-two gate; then a headless Hero-screen capture at owned=0 and a second at owned=1, both opened, plus one
Seeker frame confirming the card arrives with NEW rather than appearing silently.
