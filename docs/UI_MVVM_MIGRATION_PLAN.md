# UI MVVM Migration Plan — Whole-Game (program spec)

**Status:** ACTIVE program (owner: "do the mvvm", 2026-07-17). Authoritative execution spec for bringing
all 36 non-compliant Views to strict MVVM. Produced by the architect agent (delegated to 8 SME sub-agents,
full file:line grounding). Gold standard = `BuildingUpgradeVM` + `BuildingUpgradePanelMvvm`.

**The law:** every View is a dumb skin that binds an `IPanelViewModel` and NEVER reads/reconciles game
state at runtime. All state/logic lives in the VM. §2c: a refactor is not done until an EditMode test
locks the prior behavior (permission gate).

## Seam types (all in `Assets/_Modules/Core/UI/Mvvm/`)
- `IPanelViewModel`: `string Title`, `event Action Changed`, `void Close()`, `void Dispose()`.
- `IPanelView`: `void Bind(IPanelViewModel)`, `void Unbind()`. `IsOpen`/`Open(...)` are per-View.
- `ItemVM` (readonly struct): `Id, Name, IconRole, IconName, Price, CurrencyId, Affordable, Rarity,
  Equipped, Locked, LockReason`. The sole shared repeating-unit type (reused for slots/tiles).
  **Phase 0 adds `IconPath` (string).**
- Registration: `PanelId` enum + `PanelRouter.Register/Open` + `PanelManager.Register/NotifyOpened/
  NotifyClosed` + `PanelHandle`.

## 1. Extraction recipe (one no-VM View -> View + VM), 9 steps
**VM (pure C#, never references GameObject/Image/Sprite/RectTransform):**
1. `XVM : IPanelViewModel, IDisposable` — `Title`, `Changed`, `Close()` (invokes injected `_onClose`),
   `Dispose()` (detach every handler; null `Changed`).
2. Move ALL state+logic in: catalog reads, affordability, gating, formatting, selection. Expose as
   read-only data (properties + `IReadOnlyList<ItemVM>`) + per-item helpers (`CostFor/EffectFor/GateFor`).
3. Every player action -> a command method (`Select/Buy/Train/CollectAll`); mutate model, then `Raise()`.
4. Subscribe to model events in ctor (`EconomyService.OnChanged`, `svc.Changed`); each -> `Raise()`.
   Unsubscribe in `Dispose()`.
5. Static `CreateDefault(ctx, Action onClose)` resolves the service/catalog/scene handles itself
   (`EconomyService.Instance`, `FindObjectsByType<...>()`, catalog lookups). THE ONLY resolution site.

**View (dumb skin, `IPanelView`):**
6. `Awake`: `PanelManager.Register` + `PanelRouter.Register`. `OnDestroy` unregisters + disposes VM.
7. `Open(ctx)`: `Close(); _vm = XVM.CreateDefault(ctx, Close); BuildChrome(); Bind(_vm);
   PanelManager.NotifyOpened(...)` — ZERO service/Find/catalog symbols in the View.
8. `Bind/Unbind` toggle `_vm.Changed += Render`. `Render()` repaints from `_vm.*` only. Icons resolve
   from VM `IconRole`+`IconName`(+`IconPath`) via a presentation catalog, never a gameplay-catalog re-pull.
9. `Close()`: `Unbind(); _vm?.Dispose(); Destroy(_ui); PanelManager.NotifyClosed(...)`.

**§2c test per conversion:** `XVMTests` (EditMode) over fake/stub seams asserting (a) initial projection
matches pre-refactor fixture, (b) each command mutates + fires `Changed`, (c) locked/unaffordable/at-max
edges. Locks behavior before the View swap. Mirror `EconomyServiceTests`/`BuildingCatalogTest`.

## 2. Sequencing (leverage + risk)
- **Phase 0 — foundations/ratchet (bottleneck, first):** `ItemVM.IconPath`; `UiMvvmConformanceRegression`
  report-only with today's offenders as baseline. **NOTE:** a `WalletVM` DTO ALREADY EXISTS
  (`Core.UI.Mvvm.WalletVM` — a readonly-struct set of currency-chip `Entry`s that panel VMs expose, e.g.
  `ShopVM.Wallet`). Do NOT add a second `WalletVM`. The "clear the duplicated `EconomyService.Instance`
  reads in VM-less wallet views" task = a small LIVE WALLET SOURCE (non-colliding name, in `DeNelle.Village`)
  that owns the `IEconomy.OnChanged` subscription and produces the existing `WalletVM` DTO + a `Changed`
  event — designed WITH the BuildWalletRow conversion (WO-2), not as Phase-0 infra.
- **Phase 1 — systemic patterns (clears the 5 partials):** `GearIconCatalog` seam (icon leak in ShopPanel/
  InventoryGrid/EquipmentPanel/PartyShopPanelMvvm) + DI-in-Open hoist (`CreateDefault` on ShopVM/PartyShopVM/
  EquipVM).
- **Phase 2 — high-traffic no-VM:** Build/Tower silo (`StructureCardVM`, `PlacedTowerListVM`/`TowerUpgradeVM`;
  convert BuildMenu, BuildPaletteUI, BuildStructureInfoPanel, TowerManagerPanel, TowerUpgradeButton,
  TroopTrainingPanel); TalentTreePanel, CosmeticShopPanel.
- **Phase 3 — mid-traffic:** Arena (`ArenaVM` + shared `ArenaPaletteVM`), Raid (`RaidSelectionVM`,
  `RaidDeployVM`), RumorBoard, Quests (`DailyQuestVM`+`QuestTrackerVM`), Echo (`EchoWorkforceVM`+`EchoRosterVM`),
  Crafting (`WorkshopCraftVM`+`DungeonCraftVM` over a promoted Core `CraftRecipeVM`), Music, Leaderboard,
  ClanChat, StakeRewards, the 4 minor reconcilers.
- **Phase 4 — danger/long-tail (flag-gated, last):** BattleHudUgui (snapshot VM, flag), DialogueView
  (relocate the WO-702 truce; `DialogueViewModel : IPanelViewModel`), JupiterSwapPanelController,
  DungeonHudController, CampPromptUI, LevelUpSkillPopup. Then flip the oracle to HardFailOnNew.

## 3. Parallel silos (§9 file-disjoint; §11 single-committer gate)
Silo A (Core/UI seam) is the serialized bottleneck — lands + commits FIRST. Then B–G fan out edit-only,
orchestrator batch-gates once (`COMPILE_GATE_OK`) and commits each by explicit path.

| Silo | Files | Dep |
|---|---|---|
| A — Core/UI seam (first) | ItemVM.cs, GearIconCatalog.cs (new), WalletVM.cs, CraftRecipeVM (promoted), oracle | — |
| B — Shop/Equip/Inventory | ShopPanel/ShopVM, EquipmentPanel/EquipVM, HeroInventory*/InventoryVM, PartyShop*/PartyShopVM | A |
| C — Build/Tower | BuildMenu, BuildPaletteUI, BuildStructureInfoPanel, TowerManagerPanel, TowerUpgradeButton, BuildWalletRow + StructureCardVM/PlacedTowerListVM | A |
| D — Arena/Raid | ArenaPanel, Arena{Attack,Defense}PaletteUI + ArenaPaletteVM, RaidDeployScreen, RaidSelectionScreen, RumorBoardPanel | A |
| E — Quests/Social/Music | DailyQuestHud, QuestTrackerHud, ClanChatPanel, LeaderboardPanel, MusicSelectionPanel | A |
| F — Echo/Harvest/Crafting | EchoRosterView, EchoWorkforceHud, VillageCraftingPanel, Dungeons CraftingPanelController, StakeRewardsPanel | A |
| G — Danger | BattleHudUgui, DialogueView, JupiterSwapPanelController, DungeonHudController, CampPromptUI, LevelUpSkillPopup | A |

Shared VMs (WalletVM, ArenaPaletteVM, StructureCardVM, PlacedTowerListVM) each authored by ONE agent in
their owning silo. B and D both touch `Village/Hero` but different files — never share a VM across silos.

## 4. WO program (CLI mints numbers from the banner; each ships its §2c test + updates oracle baseline)
1. MVVM foundations — `ItemVM.IconPath` + `UiMvvmConformanceRegression` (report-only, baseline seeded).
2. `WalletVM` — convert BuildWalletRow.
3. `GearIconCatalog` seam — populate IconPath in ShopVM/EquipVM/InventoryVM/PartyShopVM; swap the 4 Views.
4. DI-in-Open hoist — `CreateDefault` on ShopVM/PartyShopVM/EquipVM.
5–6. Build/Tower shared VMs + conversions (remove `FindObjectsByType<Tower>()` polling -> registry/Changed;
   remove `BuildMenu.InvokeRepairNearestWall` reflection — NOT a sanctioned seam).
7. TalentTreeVM + CosmeticShopVM.
8. Arena — ArenaVM + shared ArenaPaletteVM.
9. Raid + Rumor — RaidSelectionVM, RaidDeployVM (power/army math out of View), RumorBoardVM.
10. Quests/Social/Music/Leaderboard.
11. Echo/Harvest/Crafting — EchoWorkforceVM, EchoRosterVM, WorkshopCraftVM + promoted CraftRecipeVM,
    DungeonCraftVM (Dungeons never references Village).
12. DungeonHud + Camps + LevelUp.
13. Jupiter swap (preserve the "not charged" guards + indeterminate Fail path).
14. DialogueView contract-fix + relocate the WO-702 builder truce (do NOT delete — re-freezes Build Mode).
15. BattleHudUgui (flag-gated; read-only snapshot; do NOT touch `_visualAtb`/`TickVisualAtb` feel-sim).
16. Ratchet: flip `UiMvvmConformanceRegression.HardFailOnNew=true` once baseline empty.

## 5. Risk register — do NOT touch / flag-gate
**Sanctioned/exempt (allowlist in the oracle):** the reflection `*HudBridge` PUSH seam (Village pushes INTO
HUD); controller PUSH seams (BattleHudUgui.Render/TickVisualAtb, DungeonHudController.SetLantern,
DialogueService.Opened) — keep push direction, only route the ref into the VM; `CollectorStackView`
(world-space diegetic decorator — conformant-by-exemption, do NOT migrate); dev tools (AdminOverlay,
OwnerDevToolsOverlay, DebugCanvasUI, HelpMenu) — OUT of scope, allowlisted.
**Danger (flag + regression):** BattleHudUgui (mutable BattleState drives a per-frame visual ATB sim —
splitting risks fill/turn/log desync), DialogueView (its `BuildModeState.DialogueHiddenForBuilder` write IS
the WO-702 truce — relocate, don't delete, or Build Mode re-freezes), JupiterSwapPanelController (money path).

## 6. Definition of done — the oracle
`UiMvvmConformanceRegression` (`Assets/Editor/Regression/`, `DeNelle.Editor`, `public static bool
Run(out string reason)`), wired into `DataRegression.RunAll`; source-lint sibling of
`UiObsidianConformanceRegression`.
- Candidate = a `.cs` under `_Modules/**` (excluding Tests/Editor) declaring `: IPanelView` (or `*Panel`/
  `*PanelMvvm` by name).
- Banned runtime symbols (regex): `EconomyService\.Instance`, `\bFind(Object|AnyObject|FirstObject|
  ObjectsBy)Type\b`, `GameStateService`, `ResourceLedger`, `VillageInventory\.Instance`, gameplay catalogs
  (GearCatalog|BuildingTierCatalog|AbilityCatalog|ArenaCatalog|CraftingRecipeCatalog|SceneConfigCatalog|
  QuestCatalog|DailyQuestCatalog).
- Exemptions: a candidate that references `IPanelViewModel` / calls `*VM.CreateDefault(` / declares
  `: IPanelViewModel` is exempt (banned symbols are legit inside a VM); filename allowlist
  (`*HudBridge.cs`, the dev tools, `CollectorStackView.cs`); `KnownBaseline` = today's offenders (tracked
  debt, non-failing).
- `HardFailOnNew=false` initially (PASS while naming offenders) -> flip to `true` when baseline empty.
- Known limitation: file-level exemption (a View that refs a VM AND still calls a banned symbol in `Open`
  passes) — tighten to per-method scanning later; the baseline still catches a brand-new no-VM View.

**Program done** = every one of the 36 Views binds an `IPanelViewModel` (whose §2c test locks it) or is
explicitly exempt; the oracle baseline is empty; `HardFailOnNew=true`.
