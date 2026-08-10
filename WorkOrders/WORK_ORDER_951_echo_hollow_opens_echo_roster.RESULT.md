# WO-951 RESULT — Echo Hollow repurposed: interact opens the Echo roster

**Status:** IMPLEMENTED — owner felt-verify owed
**Landed:** 2026-08-10 (wave-3 lane; verified, gated and committed by the CLI seat)

## What changed and why

Owner ruling, verbatim: *"so then when they go to the store they open the echos pop up on right?
Simple and easy."* The Echo Hollow (`pet-house`) is neither removed nor a skins store — it is the
Echoes' building, and interacting with it opens the EXISTING Echo roster popup. One verb, no new UI.

Both interact surfaces agree through ONE chokepoint:

- `CastleNpcInteractable.ResolveRoute` — the pure routing decision both `Interact()` and the AutoPilot
  `AssertVendorTalkRoute` oracle read — answers `"echo-roster"` for `pet-house`, checked FIRST so
  neither the upgrade short-circuit nor the legacy Yarn StructureMenu can steal it
  (`CastleVendorNpcInjector.cs:1283-1287`, constants + null-safe predicate at `:1268-1272`).
- The keeper NPC's Talk branches on that route and calls `EchoRoster.Open()` (`:1175-1180`).
- The building TAP branches on `IsEchoHollowId(hookId)` before the upgrade/Yarn routes and calls the
  same opener (`BuildingInteractable.cs:280-285`).

No new open path was invented: `EchoRoster.Open()` is the pre-existing static opener the HUD "Echoes"
button already uses (`EchoRosterView.cs:29-43`). It self-traces `[Flow:Echo] RosterOpen` and registers
with `PanelManager`, so single-modal discipline holds and a battle-lock rejection is a logged Warn,
never a silent no-op.

The superseded Yarn `pet-house` grant menu is left in place, unreferenced — Echoes unlock by level now
and the starter grant rides the founding arc. Deleting authored content was not in scope.

## Files

- `Assets/_Modules/Village/NPCs/CastleVendorNpcInjector.cs`
- `Assets/_Modules/Village/Buildings/BuildingInteractable.cs`
- `Assets/Editor/Regression/EchoHollowRouteRegression.cs` (new) + registration in
  `Assets/Editor/Regression/DataRegression.cs`

## Gate (real, this run)

- `Builds/gate-settle4.log` → `COMPILE_GATE_OK`, zero `error CS`
- `Builds/regression-settle3.log` → `REGRESSION_OK 143/143 suites` (`[echo-hollow-route]` green)

## Oracle — what it proves

`EchoHollowRouteRegression` (`ECHO_HOLLOW_ROUTE_OK`): `ResolveRoute("pet-house") == "echo-roster"`,
case-insensitively, with `IsEchoHollowId(null/"")` false; the `EchoRoster.Open` opener seam exists;
neighbours are not stolen (`barracks` and `market` still resolve `"talk-dialogue"`);
`RoleForBuildingId("pet-house") == "EchoHollow"` so a keeper still seats; and a source lint that the
TAP surface consults `IsEchoHollowId(hookId)` and calls `EchoRoster.Open()`.

## Honest limits

- Neither `Interact()` is EXECUTED — both are private, instance, scene-bound. The tap branch is proven
  by source lint, the keeper branch only through the shared route decision it reads.
- The popup never OPENS headlessly: nothing proves a panel is drawn, that `PanelManager` admits it, or
  how it behaves under a battle-lock rejection in situ.
- Nothing proves the Hollow is TAPPABLE in the live hub — interact volume, HUD TALK prompt
  registration and the keeper's spawn are scene facts.
- The recommended extensions (capacity job, awakening stage, skins counter) were NOT adopted — they
  remain the owner's call.

## Owner felt-verify

1. Tap the Echo Hollow: the Echoes popup opens on the FIRST tap, no Yarn menu flash.
2. Talk to the Hollow keeper: the same popup, the same one tap.
3. Nothing else drifted: market/forge Talk still opens Buy/Sell, the drillmaster still opens training.
4. Does the roster READ as the Hollow's own screen? That judgement is yours; the copy pass follows it.
