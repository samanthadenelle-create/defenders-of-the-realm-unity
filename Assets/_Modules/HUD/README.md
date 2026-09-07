> ⛔ **THE "Rich Dark Fantasy Battle HUD (HUD-001)" SECTION BELOW DESCRIBES DELETED FILES** (WO-1482,
> verified 2026-09-07: `find Assets -name HUDManager.cs -o -name VirtualDPadLean.cs` returns NOTHING).
> ⚠ Their histories DIFFER: `VirtualDPadLean.cs` was real and was deleted (253 lines) by commit
> **`925464df7`** (2026-07-03, *"Blink Obsidian HUD kit — total presentation rebuild on the posture
> tree"*), but **`HUDManager.cs` WAS NEVER COMMITTED** — `git log --all --diff-filter=D --
> '*HUDManager.cs'` returns nothing, because there was never an add. `GAP_AUDIT_2026-07-18.md:46`
> flagged that in July and the fix never reached this file. **The live HUD is
> `Kit/HudKitController.cs`** (`docs/MASTER_CATALOG/hud.md`); `README_HUD.md`, listed below as a live
> deliverable, is history-only for the same reason. Body kept unrewritten per CLAUDE.md §15.
> ⚠ The ONE rule in this file that is still binding and enforced: **`DeNelle.HUD` never references
> `DeNelle.Village`** (CLAUDE.md §5) — the reflection notes below are evidence of that rule, not of a
> live HUD.
>
> ⚠ **SUPERSEDED — Defend-the-Tower / PatriciaLight was REMOVED 2026-06-09; not a live system.** Kept for history. Live reality: the one root `CANON_GROUND_TRUTH_*.md` with no `SUPERSEDED` banner (the `2026-06-26` file this line used to name is now at `docs/_archive/root/`).

# HUD — `DeNelle.HUD`

Village HUD layer. **Passive display only — references Core only, never Village.**
Village pushes data through `IVillageHud` / `CoreServices.Hud`.

## Unified Mobile-First HUD (lean vital-only)
- Single code-built Canvas (ScreenSpaceOverlay) in VillageHudController is the persistent village HUD.
- **Only** key vital information: banked resources (Wood/Food/Iron/Crystals) + wave/heart status. All crafting lives in shops (Yarn + NPCCommandBridge + ShopPanel). Resource gathering lives in the world (Harvest nodes, camps).
- Large thumb-friendly controls, fantasy/medieval theme, adaptive anchors.
- Implements `IVillageHud`, registers via `CoreServices.RegisterHud`. Passive display only (Core-only deps).

## Rich Dark Fantasy Battle HUD (HUD-001)
- Separate drop-in `HUDManager` + `VirtualDPadLean` for battle / map / PatriciaLight / Defend-the-Tower scenes.
- Full reference layout: dark misty + metallic silver/gold ornate frames, red/blue bars, glowing skills, top compass (rotating needle + pulsing red enemy dots), top-right mini-map + "Unleash the Horde", left stacked Tree/Hero/Allies + resources, bottom-left Lean D-Pad (radius knob), right Build button that swaps to 4-skill diamond on hero select (class-specific: Knight/Healer/Wizard), bottom action bar with hotkeys + cooldowns, centered Build Modal (tabs + grid + costs + drag hint).
- **Lean Touch exclusive** for every interaction (D-Pad via LeanFingerDown/Update/Up; all taps via LeanFingerTap). No Button / legacy Input.
- Ties into existing via public setters + events + loose reflection-by-name (same pattern as HeartHudBridge / WaveHudBridge). No `DeNelle.HUD` → `DeNelle.Village` asm ref. D-Pad feeds `HeroLocomotion` via reflection static read (no hard dep the other way either).
- Coexists with the lean `VillageHudController` (use rich HUD only in battle/map views).

## Files
- `VillageHudController` — lean vital village HUD (code Canvas only, IVillageHud)
- `HUDManager` — rich dark fantasy battle HUD (HUD-001)
- `VirtualDPadLean` — Lean Touch D-Pad (static Move + pre-wired LeanFinger* components)
- `README_HUD.md` — setup, Lean notes, integration wiring, acceptance checklist
- Other: `XPBarController`, `FloatingXpText`, panel + bootstrap pairs (AdminOverlay*, ClanChatPanel*, CompassHud*, CosmeticShopPanel*, DailyQuestHud*, HelpMenu*) — on-demand popups. Legacy UXML (VillageHud.uxml/uss) retained only for reference (not used at runtime). Deleted 2026-07-03 (dead-surface sweep): `HeroTalentPanel`+bootstrap (superseded by `Village/Talents/HeroSkillTreePanelMvvm`), `PlayerProgressPanel` (orphan, no opener). Deleted 2026-07-08 (pet skill-tree retire, dead content): `PetSkillTreePanel`+bootstrap, `PetUnlockTracker` — pets are harvest/companion only (docs/COMBAT_PIVOT_NORTHSTAR.md).
- `XPBarController`, `FloatingXpText` — progression
- Panel + bootstrap pairs (code-built, popups):
  `AdminOverlay`, `ClanChatPanel`, `CompassHud`, `CosmeticShopPanel`,
  `DailyQuestHud`, `HelpMenu`

Pattern: every `XPanel.cs` has a matching `XPanelBootstrap.cs` that wires it
into the scene at runtime.

> Maintenance: update this README when files are added/removed.
