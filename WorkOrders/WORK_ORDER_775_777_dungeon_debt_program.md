# WO-775–777 — Dungeon-debt program (vitals · Granary-gate · door-consolidation)

**Status:** READY TO IMPLEMENT
**Minted:** 2026-07-26 (CLI, from read-only §12 RCA agent — evidence cited from HEAD)
**Source:** the three parked dungeon-debt items (770.10a / 770.6 / 770.5) from PAIN_POINTS_2026-07-26.md §4.

---

## WO-775 (=770.10a) — Dungeon hero vitals are hardcoded placeholders

**Problem:** dungeon seeds Keeper HP/mana from literals `_heroBaselineHp=120` / `_heroBaselineMana=60` (`DungeonController.cs:119,:122`; the `[Header]` at `:114` says "placeholder — no dungeon hero-stat type yet"). Real hero base/gear/talents/level are ignored.

**Root cause (evidence):** seed site = `DungeonController.EnterDungeon()` `:320-322` (`if (_runtimeState!=null && !resuming && !_runtimeState.HasHeroVitals) _runtimeState.SetHeroVitals(hp,hp,mana,mana)`). Canonical source EXISTS + is reachable from `DeNelle.Dungeons` (already refs `DeNelle.Village`): HP = `HeroHealth` (`HeroHealth.Instance` `:37`, `MaxHp` folds base100+gear+talent `:174`, live `Hp` `:175`); Mana = `HeroAbilities.Mana` `:140` / `MaxMana` `:143`. **Latent 2nd bug:** `DungeonRuntimeState.HealHeroToFull()` `:426-433` only mutates the SO — its doc-comment `:423` claims the hero re-reads, but nothing pushes back to `HeroHealth`/`HeroAbilities`, so checkpoint heals are cosmetic.

**Fix:**
1. In `EnterDungeon()` before `:320`, resolve live components off `_hero`. **Use `TryGetComponent`, NOT `?? ` — the `NoNullCoalesceOnGetComponent` lint (fixed this session) will FAIL the gate on `GetComponent<T>() ??`.** e.g. `HeroHealth hh = _hero.TryGetComponent<HeroHealth>(out var h) ? h : HeroHealth.Instance; _hero.TryGetComponent<HeroAbilities>(out var ha);`
2. Seed `SetHeroVitals(hh.Hp, hh.MaxHp, ha.Mana, ha.MaxMana)`. Keep 120/60 ONLY as a guarded fallback when neither resolves (`FlowTrace.Warn`).
3. Close the heal loop: on `Checkpoint` heal (`Checkpoint.cs:186`), after `HealHeroToFull()`, push restored values back to the live `HeroHealth`/`HeroAbilities` (null-guarded).
4. Demote the two `[SerializeField]` baselines to explicit `_fallbackHp/_fallbackMana`.

**Files:** `Assets/_Modules/Dungeons/DungeonController.cs` (~:114-122, :320), `Assets/_Modules/Dungeons/Checkpoint.cs` (~:186).
**Acceptance + oracle:** fresh run → `runtimeState.HeroMaxHp == HeroHealth.Instance.MaxHp` && `HeroMaxMana == HeroAbilities.MaxMana` (NOT 120/60); checkpoint heal restores LIVE Hp/Mana. Extend `Assets/Tests/EditMode/DungeonRuntimeStateResetTests.cs`: hero with gear MaxHp=155 → EnterDungeon → assert runtimeState vitals == hero's, != 120/60.
**Do NOT touch:** the ATB round-trip preservation (`!resuming`/`HasHeroVitals`/`BeginEncounterHandoff`) — vitals must survive the battle round-trip; village HeroHealth/HeroAbilities tuning constants.

---

## WO-776 (=770.6) — Folk's Granary is a walkable dead stub → GATE now, promote later

**Problem:** west world-portal routes to `Dungeon_FolksGranary`, a stub with no DungeonController, no layout JSON, zero lore stones, one `DungeonStubEncounter`, one exit pad, one canned NPC. A real door into a hollow room.

**Root cause (evidence):** built as a stub tier by `Assets/Editor/FolksGranaryBuilder.cs` (comment `:592-593` "no authored layout JSON"; `:1187` "granary ships none"). Reachable: portal row `AuthoredPortal("FolksGranary", (-140,0,-20), 82)` `DungeonWorldPortalSpawner.cs:116` + inline fallbacks `:585-586` and `DungeonEntranceBootstrap.cs:85`; `Resources/Dungeons/FolksGranary.asset`; self-registers Build Settings (`:1151`). Designed as D4 "The Folk Who Forgot" (Act II, mini-boss **The Inn-Keeper**; `docs/DUNGEON_DESIGNS.md:59`, `docs/dungeons-storyline.md:87,:99,:125-126,:132`) — none of that content built.

**Fix (T0 = gate; matches PAIN_POINTS §4 "hide/gate until layout+controller exist"):**
1. Remove `"FolksGranary"` from `AuthoredPortals` (`DungeonWorldPortalSpawner.cs:116`) OR wrap in a flag defaulted OFF.
2. Remove `FolksGranary` from the two inline `LoadDefs` fallbacks (`DungeonWorldPortalSpawner.cs:585-586`, `DungeonEntranceBootstrap.cs:85`).
3. Leave the scene + builder in the repo (dev-only menu `Defenders/Dungeons/Build Folk's Old Granary`).
**Follow-up content WO (backlog, mint separately):** author `dungeons/folks-granary.json` + DungeonController + lore stones (incl. wooden-horse beat) + Inn-Keeper boss + resolution NPC, then re-enable the portal.

**Files:** `Assets/_Modules/Village/World/DungeonWorldPortalSpawner.cs` (:116, :585-586), `Assets/_Modules/Village/Dungeons/DungeonEntranceBootstrap.cs` (:85).
**Acceptance:** no world portal to `Dungeon_FolksGranary` in a default headless overworld run (only Healer's Cottage / `dg_starter_loop`); scene+builder remain buildable dev-only; add a §15 status note to `docs/DESIGN-DECISIONS.md #11` + `docs/MASTER_CATALOG.md:210`.
**Do NOT touch:** Healer's Cottage or `dg_starter_loop` (east) portal; `FolksGranaryBuilder.cs` internals / scene asset (keep for promotion).

---

## WO-777 (=770.5) — Two redundant entry systems + walk-by auto-teleport footgun

**Problem:** two "enter dungeon from world" components (`DungeonPortal` + `DungeonEntrance`); the canonical one teleports on walk-IN (accidental delve, no confirm).

**Root cause (evidence):** **System 1 `DungeonPortal`** (`Assets/_Modules/Village/Buildings/DungeonPortal.cs`) = canonical/active (spawned by `DungeonWorldPortalSpawner` default-ON `:159-172`). **System 2 `DungeonEntrance`** (`Assets/_Modules/Village/Dungeons/DungeonEntrance.cs`) = redundant + dormant (only placer `DungeonEntranceBootstrap` is a disabled no-op `VillageController.cs:204-212` "DISABLED 2026-05-27" AND self-gated dead on `TownRingRetired` `:34,:41`). **Footgun:** `DungeonPortal.OnTriggerEnter` `:150-161` calls `EnterDungeon()` immediately on walk-in (`:180` → `SceneManager.LoadScene`), despite a proper prompt/button path already wired (`MobileInteractButton.Request` `:126`). The dormant system has the SAFE pattern (prompt-only `DungeonEntrance.cs:93-99`, entry via button `:131`).

**Fix:**
1. Consolidate to ONE entry = `DungeonPortal`. Retire dead `DungeonEntrance` + `DungeonEntranceBootstrap` (delete or `[Obsolete]`-mark), plus `VillageController.EnsureDungeonEntrances()` `:204` and the `PortalVFXInjector` DungeonEntrance scan (`Assets/_Modules/Village/Dungeon/PortalVFXInjector.cs:33`).
2. Kill the walk-by: remove the `EnterDungeon()` call from `DungeonPortal.OnTriggerEnter` `:160`; keep `OnTriggerEnter` only to arm VFX/prompt (`_portalVfx?.OnHeroApproach()`); sole entry = the Interact button/prompt at `:126`.

**Files:** `DungeonPortal.cs` (:150-161), `DungeonEntrance.cs` + `DungeonEntranceBootstrap.cs` (retire), `VillageController.cs` (:204), `PortalVFXInjector.cs` (:33).
**Acceptance + oracle:** exactly one entry component (`DungeonPortal`) live; walking a hero collider through a portal trigger shows the prompt but does NOT change scene; Interact/button DOES route. Extend `Assets/Editor/Regression/SceneRoutingRegression.cs`: hero collider enters `DungeonPortal` trigger → assert NO scene change; explicit `EnterDungeon()`/button → asserts routes.
**Do NOT touch:** `DungeonWorldPortalSpawner` placement/discovery; in-dungeon `DungeonPortLink` (already button/[F]-only `:99-106` — the good pattern). **Related walk-by (follow-up decision, NOT this WO):** `DungeonExitInteractable.OnTriggerEnter:272-278`, `DungeonStubEncounter.cs:86-91`, `DungeonStubReturn.cs:23-45`.

**Sequencing:** 776 + 777 both touch `DungeonEntranceBootstrap.cs` → do 777 before 776, or one agent for both. 775 (Dungeons module) is disjoint from both.
