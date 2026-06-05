# HUD — `DeNelle.HUD`

Village HUD layer. **Passive display only — references Core only, never Village.**
Village pushes data through `IVillageHud` / `CoreServices.Hud`.

## Files

- `VillageHudController` — implements `IVillageHud`, the main HUD surface
- `XPBarController`, `FloatingXpText`, `PlayerProgressPanel` — progression display
- Panel + bootstrap pairs (code-built UI, no UXML — UXML fails in builds):
  `AdminOverlay`, `ClanChatPanel`, `CompassHud`, `CosmeticShopPanel`,
  `DailyQuestHud`, `HelpMenu`, `HeroTalentPanel`, `PetSkillTreePanel`
- `PetUnlockTracker`

Pattern: every `XPanel.cs` has a matching `XPanelBootstrap.cs` that wires it
into the scene at runtime.

> Maintenance: update this README when files are added/removed.
