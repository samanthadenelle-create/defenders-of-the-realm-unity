# RESULT — WO-826 Realm Map parchment UI

**Shipped:** 2026-08-01, commit `eb5d0710` (oracle registration `1371e70a`).
**Gates:** COMPILE_GATE_OK + REGRESSION_OK + UI_CAPTURE_OK 23 with RealmMap PNGs eyeballed
(parchment field, gilt ELARION, five fogged nodes, detail pane correct).

## What shipped
- `RealmMapCatalog.cs` (Core/World) — typed loader over the dual-copy `realm-map.json`,
  Guard-wrapped, no second region list.
- `RealmMapVM.cs` — strict MVVM; derives real state from `GameState.Regions` + `BestWave`;
  discovery writer stubbed with `FlowTrace.Once` -> WO-827.
- `RealmMapPanel.cs` — code-built parchment panel: Elarion gilt home node + 5 fog regions at
  `mapPoint`, detail pane with text-encoded state, Travel CTA disabled
  "Travel - coming with discovery".
- `RealmMapPanelBootstrap.cs`; PanelRouter `PanelId.RealmMap=15`; HUD Map button (hidden until
  Onboarded per WO-825 R4 default); DevPanel entry.
- `CaptureRealmMap` in UICaptureLaunch; `RealmMapRegression` (`REALM_MAP_OK` — 5 regions, home
  Elarion, dual-copy parity + no-Avalon check); `RealmMapVMTests` (8 EditMode tests).

## Notes
- `mapPoint` y is read as percent-from-top (single-line flip in `BuildNode` if felt-mirrored).
- Fog nodes are rounded squares (disc polish optional).
- Wayshrine entry not built (no such structure); adjacency connectors skipped per spec.
- Same commit carries the owner's Queues bar-button retirement.
- Rulings R1-R4 defaults live (WO-825).

## PO felt-verify still open
- [ ] Open the Realm Map from the HUD Map button post-Onboarded; parchment look + detail pane
      + disabled Travel CTA read correctly in the felt build.
