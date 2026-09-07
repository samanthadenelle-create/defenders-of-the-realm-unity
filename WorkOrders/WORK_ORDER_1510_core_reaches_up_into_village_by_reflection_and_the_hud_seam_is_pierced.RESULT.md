# WO-1510 RESULT - the Core-to-Village inversion is closed and the silent catch logs; the HUD/Village resolver was NOT built

**Status:** HALF DONE - uncommitted in the working tree as of 2026-09-06 21:45, awaiting the wave-two gate.
**Commit:** none. Edit-only lane.
**Files:** `Assets/_Modules/Core/Bridging/IVillageBridge.cs` (NEW, untracked),
`Assets/_Modules/Village/VillageBridgeService.cs` (NEW, untracked), `Core/CoreServices.cs:237`,
`Core/SceneRouter.cs`, `Core/State/PersistenceBridge.cs:71`, `Core/Diagnostics/BreakCaptureHarness.cs`,
`Village/Hero/HeroLocomotion.cs:2036-2060`, `Assets/Editor/Regression/CoreReflectionSourceRegression.cs`
(NEW, untracked).
**Gates:** none cover this. `Builds/cg-quiet.log` `COMPILE_GATE_OK` is 20:04 and predates these edits;
`Builds/cg-aab.log` (20:54) is RED on the Manage lane's half-written suites (42x `CS0103`), so the tree as a whole
does not compile and no `REGRESSION_OK` exists for this work.

## 1. Half A - Core no longer names a Village type (DONE)

The three listed sites are re-seamed through an interface on `CoreServices`. `IVillageBridge.cs:9-11` names the
retired sites in its own header (SceneRouter 510/523, PersistenceBridge 174, BreakCaptureHarness 491);
`VillageBridgeService.cs` implements the Village side; `PersistenceBridge.cs:71` now holds only a comment naming
the `Type.GetType` it replaced. **Residual, and a site the ticket never listed:**
`Core/Promo/PromoCodeService.cs:367` still does `asm.GetType("DeNelle.Village.EconomyService")`.

## 2. Half B - the HUD/Village resolver (NOT BUILT)

`grep -rn 'GetType("DeNelle.Village' Assets/_Modules/HUD --include=*.cs | wc -l` returns **18**, unchanged:
`AdminOverlay.cs:459,551,659,864,995,1021,1042`, `HelpMenu.cs:613`, `Kit/HudKitController.cs:1553,1570`, and four
`*Bootstrap.cs` (`BenefactorsWallPanel:82`, `ClanChatPanel:66`, `CosmeticShopPanel:64`, `DailyQuestHud:64`).
Sec.2's "twenty-one call sites become one" has not happened in either direction.

## 3. Half C - the silent catch (DONE)

`HeroLocomotion.ReadHudDpadMove`'s catch (`:2055+`) now takes `System.Exception e` and traces at WARN, guarded by
the static first-hit latch `_hudDpadReadThrewTraced` (`:2040`) because the read runs every movement frame. Its
in-file comment quotes CLAUDE.md sec.12 verbatim.

## 4. Acceptance

- [ ] Zero `DeNelle.Village` literals in Core, zero in HUD outside one resolver - **OPEN**: 1 in Core, 18 in HUD.
- [ ] The bare catch logs, proven by forcing the failure - source landed (sec.3); the forced failure is uncaptured.
- [ ] A regression fails on a new `Type.GetType("DeNelle.Village` outside the resolver -
      `CoreReflectionSourceRegression.cs` exists untracked; its RED proof is unrun (tree does not compile).
- [ ] `REGRESSION_OK n/n` on a fresh log - owed.

## 5. Owed

The wave-two gate; then the HUD/Village resolver as its own pass (the larger half, ten files); plus a ruling on
`PromoCodeService.cs:367`. No device capture applies - nothing player-visible changed.
