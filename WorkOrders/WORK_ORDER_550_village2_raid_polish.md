# WORK ORDER 550 — Village2 Raid-Scene Polish

**Status: READY TO IMPLEMENT** (implemented in this worktree; awaiting orchestrator gate + commit + bake)
**Date:** 2026-06-28
**Branch base:** `wip/village2-and-f8-tickets` (HEAD `e4165b34`)
**Lane:** Combat/AI + HUD (file-disjoint silos) — single committer reconciles by explicit path.

> **WO-NUMBER NOTE:** 550 is a placeholder. The numbering authority is
> `MASTER_PIPELINES_BACKLOG_2026-06-06.md` + `CLI_LANES_WO_NUMBERS.md`, **NOT** the filesystem max.
> The owner must slot this into a lane in the master backlog and re-number if needed.

---

## Problem (data-grounded RCA — Player.log + code)

Village2 is an **enemy-owned raid scene** but still behaves like a town hub:
1. ~16 town/social/economy HUD panels self-bootstrap in Village2 (Player.log `SpawnInScene` lines).
2. The raid is **one-way** — no retreat; an unwinnable/abandoned raid soft-locks until the garrison is cleared.
3. `scene-configs.json` tagged Village2 `faction:"hollow"` + purple `#7a5fb0`, but the live garrison is all-orc / ruined → wrong banner identity.
4. The boss chamber + altar build even though `boss:null` → an empty room climbed to for nothing.
5. Minor drift: `Village2RaidController` claim key vs config id; a stale "8 spawn points" comment (actual 6).

---

## Files edited

### Item 1 — town/social/economy HUD suppressed in enemy scenes
There is **no shared bootstrap base** — each panel has its own `[RuntimeInitializeOnLoadMethod]` + `SpawnInScene`. The clean chokepoint is therefore a single **shared semantic test** every bootstrap gates on:
- `Assets/_Modules/Core/HubScenes.cs` — new `SuppressTownHud(sceneName)` (= `IsEnemyOwnedScene`). One source of truth.

Per-panel gate (early-return on `HubScenes.SuppressTownHud(SceneManager.GetActiveScene().name)`), gated on the **active** scene so the player's current context decides:
- `Assets/_Modules/HUD/ClanChatPanelBootstrap.cs` (social)
- `Assets/_Modules/HUD/CosmeticShopPanelBootstrap.cs` (economy/store)
- `Assets/_Modules/HUD/DailyQuestHudBootstrap.cs` (town quests)
- `Assets/_Modules/HUD/LeaderboardPanelBootstrap.cs` (social)
- `Assets/_Modules/HUD/QuestTrackerHudBootstrap.cs` (town/story quests)
- `Assets/_Modules/HUD/PetSkillTreePanelBootstrap.cs` (pets/town)
- `Assets/_Modules/Audio/MusicSelectionPanelBootstrap.cs` (town jukebox)
- `Assets/_Modules/Village/Crafting/VillageCraftingPanelBootstrap.cs` (town/economy crafting)
- `Assets/_Modules/Village/Items/CraftingPanelBootstrap.cs` (town/economy alchemy)
- `Assets/_Modules/Village/Hero/PartyShopPanelMvvmBootstrap.cs` (economy/store)
- `Assets/_Modules/Village/Buildings/Progression/BuildingUpgradePanelMvvmBootstrap.cs` (base-building)
- `Assets/_Modules/Village/Buildings/Progression/BuildingUpgradePanelBootstrap.cs` (base-building legacy)
- `Assets/_Modules/Village/Talents/HeroSkillTreePanelBootstrap.cs` — gates **only** the skill tree; **HeroLoadoutPanelMvvm still spawns** (combat-relevant hot-swap gear).
- `Assets/_Modules/Web3/JupiterSwapBootstrap.cs` — removed `"Village2"` from `AllowedScenes` (economy/web3 CTA).

**Intentionally NOT gated (justified):**
- `CompassHudBootstrap` — combat navigation (keep).
- `HeroLoadoutPanelMvvm` (via HeroSkillTree bootstrap) — combat gear/hot-swap (keep).
- `HelpMenuBootstrap` — universal utility, inert until hotkey (keep).
- `AdminOverlayBootstrap` — dev/admin, hidden until chord; useful for diagnosis (keep).
- `HeroTalentPanelBootstrap` — already RETIRED (early-returns); no gate needed.
- `BattleHud9Zone` / `VillageHudBootstrap` — combat HUD (untouched; already handled on base branch).

### Item 2 — Retreat affordance (anti-soft-lock)
- `Assets/_Modules/Village/World/Camps/Village2RaidController.cs` — added `_retreatUi`, `BuildRetreatButton()` (bottom-left, own ScreenSpaceOverlay canvas, **no scrim** so gameplay input isn't blocked, `ButtonKind.Danger`), and `Retreat()` which routes home via `SceneRouter.GoCastle()` (same path as `ReturnHome`/`AutoReturnRoutine`) **without** claiming (base stays enemy-owned). Built at the top of `BindRoutine` (available even if the garrison fails to bind); torn down on victory in `HandleCleared`.

### Item 3 — faction/theme alignment (both copies kept in sync)
- `Assets/Resources/Data/Canonical/scene-configs.json`
- `Assets/StreamingAssets/Data/Canonical/scene-configs.json`
  `village2_enemy_outpost`: `faction "hollow"→"orc"`, `themeColor "#7a5fb0"→"#5a8f3a"` (orc green, matches `raider_camp_small`).

### Item 4 — empty boss chamber
- `Assets/Editor/EnemyStrongholdBuilder.cs` — boss chamber + altar now build **only** when `recipe.boss` is non-empty. Current recipe is `boss:null` → chamber is **skipped** (no empty room). **Requires a re-bake** via `Defenders > World > Build Village2 Enemy Stronghold` (editor closed). `EnemyFactory.ModelForEnemy` confirms `orc-necromancer → Orc_Necromancer` (real model) if the owner later wants a boss.

### Item 5 — minor drift
- `Village2RaidController.cs` — header comment fixed ("8" → "6 spawn points (3 chokepoints + 2 courtyard + 1 keep)"); `ConfigId` left as `"Village2"` with a flag comment (see below).

---

## Acceptance criteria
- [ ] In Village2: none of the listed town/social/economy panels bootstrap (Player.log shows the WO-550 suppression `[Flow:UI]` lines instead). Compass + loadout + BattleHud9Zone still present.
- [ ] In MainCastle_Hall (home hub): all town panels still bootstrap (unaffected — `IsEnemyOwnedScene("MainCastle_Hall")==false`).
- [ ] A "Retreat" button is visible during a Village2 raid and routes to the castle without claiming the base.
- [ ] Village2 banner/accent reads orc green, not purple.
- [ ] After re-bake, Village2 has no empty boss chamber/altar.
- [ ] `CompileGate` clean; AutoPilot Village2 phase still reaches victory + return.

## What NOT to touch
- Do not undo base-branch fixes: orc white-color (EnemyFactory tint), force BattleHud9Zone + suppress old HUD, quest-board town-chrome gate, OuterWorld no longer streaming into Village2.
- Do not gate Compass / HeroLoadout / Help / AdminOverlay / BattleHud.
- Do not hand-edit `Village2.unity` — regenerate via the builder menu (editor closed).
- HUD→Core only; Village→Core only (all gates call `DeNelle.Core.HubScenes`, no Village↔HUD).

## Flagged for owner decision
- **ConfigId** (`Village2` vs `village2_enemy_outpost`): left as-is. It's the self-consistent persisted claim key (`dotr-raid-owner-Village2`), keys on scene name like the rest of ownership, and nothing external reads the config id as a claim key. Switching it would only orphan an existing saved claim. Change only on an explicit owner call.
- **Boss chamber as a real fight**: chose to SKIP the empty room (deterministic, zero soft-lock risk). Making it a real boss fight needs an authored boss + a reachability-verified boss spawn point + a bake — deferred (a defender on an unreachable raised platform would itself be a soft-lock, which can't be headless-verified here).
- **Retreat cost**: none applied (no trivial cost exists). Add a cost only if the owner wants one.
