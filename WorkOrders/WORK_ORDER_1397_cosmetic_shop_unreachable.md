# WO-1397: the Cosmetic Shop (PanelId.CosmeticShop) is unreachable by any player - its only opener is a dialogue verb no dialogue uses

**Status:** READY TO IMPLEMENT - minted 2026-09-05 from the UI screen graph (overnight STRETCH)

## Evidence
- Graph: `docs/qa/UI_SCREEN_GRAPH_2026-09-04.md:56` (verb `OpenCosmetics :107 -> CosmeticShop (NOT present)`), `:228` (node row) and `:246` (dead end 4).
- Registration: `Assets/_Modules/HUD/CosmeticShopPanel.cs:76` `PanelRouter.Register(PanelId.CosmeticShop, OpenOverlay)`; header `:2-3` says "Opened via its world interactable (Marketplace)" - no such interactable exists: `Assets/_Modules/Village/Buildings/Building.cs:30-54` `enum BuildingType` has no Marketplace member, and `BuildingInteractable.TryPanelFor` (`Assets/_Modules/Village/Buildings/BuildingInteractable.cs:480-522`) has no Cosmetic case.
- Only opener: `Assets/_Modules/Village/Tutorial/DialogueCommandSink.cs:107` `case "OpenCosmetics": ... PanelRouter.Open(PanelId.CosmeticShop)`. Grep of `OpenCosmetics` in BOTH `dialogues.json` copies (`Assets/Resources/Data/Canonical/dialogue/`, `Assets/StreamingAssets/Data/Canonical/dialogue/`) = 0 hits this session.
- Other `PanelId.CosmeticShop` hits under `Assets/_Modules`: the panel's own unregister (`:95`), the old capture harness `UICaptureMode.cs:279`, a doc comment `PanelRouter.cs:79`. Nothing a player taps.
- The content is real: `Assets/Resources/Data/Canonical/cosmetics.json` holds 37 `id` entries across hero / pet / village categories with `unlockMethod` per item; `Assets/_Modules/Cosmetics/` (CosmeticCatalog, CosmeticOwnershipService, CosmeticApplier) is live.
- It is spawned every session anyway: `CosmeticShopPanelBootstrap.cs:16-27` (AfterSceneLoad, any scene with a hero) - a panel built for nobody.

## What the player experiences
Thirty-seven authored looks, an ownership service, an applier - and no screen. Achievement-unlocked cosmetics are earned and never seen, so an entire reward axis gives no reason to do anything.

## Fix shape (one mechanism)
One door, on the Hero deck, next to the screen that already dresses the hero: a fifth card "Wardrobe" -> `PanelId.CosmeticShop` in `PlayerDeckWorkspace.cs` case `PlayerDeckKind.Hero` (`:577-583`), `Available = () => PanelRouter.IsRegistered(PanelId.CosmeticShop)`. The `OpenCosmetics` verb stays (a vendor line may use it later); the stale "Marketplace" header in `CosmeticShopPanel.cs:2-3` is corrected. Card copy from canon-strings, not literals. If the owner rules the shop belongs in the Night Market instead (WO-1164 "one store"), the SAME PanelId is opened from a store tab - the door moves, the panel does not.

```
HERO DECK  [Bag] [Equipment] [Skills] [Loadout] [Wardrobe]
                                                  |
                                                  v PanelRouter.Open(PanelId.CosmeticShop)
                                    COSMETIC SHOP (CosmeticShopPanel: Hero / Pet / Village tabs)
```
Trace: `FlowTrace.Step("Cosmetics", "shop opened from Hero deck; owned=<n>/<total> equipped=<id|none>")` in `OpenOverlay`; `FlowTrace.Warn("Cosmetics", "shop unavailable: <reason>")` on the existing reflection miss path.

## Acceptance
- [ ] RED first: a `CosmeticShopReachabilityRegression` - source scan that at least one NON-verb caller of `PanelId.CosmeticShop` exists in a player-facing file (PlayerDeckWorkspace.cs), and that `CosmeticShopPanel.cs` no longer claims a Marketplace interactable. Fails on the current tree.
- [ ] Headless: `HeroWorkspace_2670x1200.png` shows five cards; a new `CosmeticShop` case in `UICaptureLaunch.cs` (the old `UICaptureMode` route is not the gate) - the panel renders 37 cards with owned/locked words, no colour-only state.
- [ ] Device: Hero -> Wardrobe opens; equip one owned look; the hero's body changes (screencap) and the trace line is in logcat.

## Not in scope
Cosmetic pricing / entitlements; new cosmetics; the pet skill tree (retired); back-to-deck (WO-1400).

## Owner question
Hero-deck "Wardrobe" card (default proposed - dressing belongs beside Equipment) or a Cosmetics tab inside the Night Market under WO-1164? One word.
