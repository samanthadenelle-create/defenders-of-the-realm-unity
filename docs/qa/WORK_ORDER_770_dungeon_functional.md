# WORK ORDER 770 — Make the Dungeons Fully Functional (read-only regression fixes)

**Status:** SPEC — PARTIALLY SHIPPED (770.1/.2/.3/.3b/.4/.7/.9 DONE; .5/.6/.8/.10/.11 open as WO-775/776/777). Sub-orders tagged **[CODE — safe to apply]** (pure C#, no scene
rebake) or **[OWNER-GATED]** (requires re-running an editor scene builder — the
curated-scene rule forbids autonomous scene edits; see WO-27/SESSION_HANDOFF).
**Date:** 2026-07-26
**Author:** dungeon-subsystem SME pass.
**Source:** `docs/qa/dungeon-regression-2026-07-26.md` (findings D1–D16). Read that
first — it has the evidence + file:line for every claim below.

**Problem (owner-facing):** The dungeon subsystem "always gets overlooked." The
regression confirms why it isn't fun/functional yet: you can **enter the Healer's
Cottage but never leave it**, its **lore is unreadable**, **every fight reports a
win and returns to the Cottage even from other dungeons**, **Folk's Granary is an
empty stub**, and the village has **two redundant door systems**. Nothing is
corrupted — it's wiring, one missing UI panel, content, and cleanup. These work
orders take it to a real end-to-end loop:
**Village → door → dungeon → explore/read lore/fight → win or lose → leave → Village.**

Sequenced by dependency. Do 770.1–770.4 first (they unlock the core loop), then
770.5–770.7 (polish/parity), then 770.8–770.9 (content + cleanup).

---

## WO-770.1 — Dungeon entrance + exit system (P0) [CODE + OWNER-GATED]

**Depends on WO-770.3** — until real battle outcomes exist (D4), `BossDefeated` is
always set, so the boss-gated back-door below cannot be verified in isolation. Build
770.3 first (or in the same pass).

**Fixes D1.** `DungeonController.ExitToVillage()` exists but has zero callers;
Healer's Cottage has no exit pad, no HUD leave-button, and `PauseController`
(Quit→Title only) isn't in the scene. The player is trapped after entering.

Make entrances **and** exits first-class, data-driven layout objects so every dungeon
(D1 today, secondaries later) gets a consistent enter/leave contract.

**Fix:**
1. **[CODE — data]** Add an **`entrance`** and an **`exit`** block to
   `healers-cottage.json` (and the schema in `DungeonLayout.cs`), each `{roomId,
   position{x,y,z}, facingY}`. The `entrance` doubles as the fresh-run spawn (today's
   `spawn` block); the `exit` is the normal walk-out. Grounding is objective: reuse the
   existing `spawn` block for the entrance, and place the `exit` at the **entrance room
   centre** (`Layout.FindRoom(entryRoomId).bounds.Center`, `DungeonController.cs:347`) so
   no invented coordinates are needed.
2. **[CODE]** Add a `DungeonExit` MonoBehaviour (mirror `DungeonStubReturn`) that, on
   hero proximity + interact key, calls `DungeonController.ExitToVillage()`. Two
   instances: (a) the always-open **entrance exit** at the `exit` block; (b) a
   **boss-gated back-door** at the **Workshop room** (`Layout.FindRoom("workshop").
   bounds.Center` — the room already exists in `healers-cottage.json`), whose
   `DungeonExit` starts disabled and is enabled by `DungeonController` once
   `RuntimeState.BossDefeated` (`DungeonRuntimeState.cs:171`) flips true. That flag is
   currently read by nothing; this WO is its first reader. **Meaningful only after
   WO-770.3** (else `BossDefeated` is always true).
3. **[CODE]** Add a **pause→leave** fallback: put `PauseController` in the dungeon
   scene and give it a "Return to Village" option (not just Quit→Title) when the active
   scene is a dungeon. Prevents a soft-lock if proximity detection misses.
4. **[OWNER-GATED]** Re-run `Defenders ▸ Dungeons ▸ Build Healer's Cottage (D1)` so the
   two exit objects + pause overlay are placed. `DungeonSceneBuilder.cs`.

**Acceptance:**
- `healers-cottage.json` carries `entrance` + `exit` blocks; `DungeonLayout` parses them.
- From inside Healer's Cottage the player reaches the Village by (a) the entrance exit
  and (b) the pause menu — verified in play mode.
- With WO-770.3 applied: the Workshop back-door `DungeonExit` is **disabled until a boss
  victory**, then enabled; a boss *defeat* leaves it disabled.
- No path leaves `DungeonRuntimeState.RunActive` stuck true after exit (`EndRun` runs).

**Key files:** `Assets/_Modules/Dungeons/DungeonController.cs` (`ExitToVillage`),
new `Assets/_Modules/Dungeons/DungeonExit.cs`, `Assets/_Modules/Settings/PauseController.cs`,
`Assets/Editor/DungeonSceneBuilder.cs`, `Assets/StreamingAssets/Data/Canonical/dungeons/healers-cottage.json`.

---

## WO-770.2 — Return to the CORRECT dungeon after a fight (P0) [CODE — safe to apply]

**Fixes D3.** `EncounterTrigger.LaunchBattle` hardcodes
`ReturnScene = SceneRouter.DungeonHealersCottage` (`EncounterTrigger.cs:312`), so
any non-Cottage dungeon fight returns the player to the Cottage.

**Fix:** set `ReturnScene` to the **current** dungeon scene, e.g.
`UnityEngine.SceneManagement.SceneManager.GetActiveScene().name` (or thread the
owning `DungeonController._dungeonId` → `SceneRouter.Dungeon(id)` through
`ConfigureScripted`/`ConfigureBoss`). Village breaches are unaffected (they build
`BattleParams` without touching `ReturnScene`, default `Village`).

**Acceptance:**
- A scripted/boss fight started in scene X fades to `ATBBattle` and returns to
  **scene X**, for any dungeon — verified with a second dungeon (see 770.6).
- Village wave breach still returns to the Village (regression check).

**Key files:** `Assets/_Modules/Dungeons/EncounterTrigger.cs:302-318`.

---

## WO-770.3 — Real battle outcome, not assumed victory (P1) [CODE — safe to apply]

**Fixes D4.** `DungeonController.ResolvePendingEncounter` hardcodes
`bool victory = true` (`:627`); a **lost** fight (and a **lost boss**) still
"wins," unlocking the exit and marking `BossDefeated`.

**Fix:** add a Core-level battle-result carrier both modules can see (the dungeon
module references `DeNelle.Core`, not `DeNelle.BattleATB`). **Decided design** (no
open choice left to the implementer):
1. Add `public BattleOutcome LastOutcome` (`enum {None,Victory,Defeat}`) to
   `SceneRouter.PendingBattle` (`SceneRouter.cs:39-56`), defaulting `None`.
2. In `BattleController.HandleOutcome` (`BattleController.cs:228`, which already
   branches on `AtbBattleResult`), set `SceneRouter.PendingBattle.LastOutcome` to
   Victory/Defeat **before** `ReturnAfterResult` (`:417`) routes back. This is the one
   pinned edit in `DeNelle.BattleATB`.
3. In `DungeonController.ResolvePendingEncounter` (`:616-650`) replace
   `bool victory = true` (`:627`) with `victory = SceneRouter.PendingBattle?.LastOutcome
   == BattleOutcome.Victory`. `EncounterTrigger.ResumePendingEncounter(victory)` already
   forwards it faithfully.
4. **Defeat behavior (decided):** on defeat the run ends and the player returns to the
   Village — `ResolvePendingEncounter` calls `ExitToVillage()` after clearing the
   handoff; the encounter is NOT re-armed (no free retry, no free boss kill). Victory
   keeps today's resume-in-place behavior.

**Acceptance:**
- Losing a dungeon fight does NOT set `BossDefeated`, does NOT unlock the back-door,
  ends the run, and returns to the Village.
- Winning behaves exactly as today (resume in place). Village battle result path
  unchanged (its `LastOutcome` is ignored; village return is by `ReturnScene`).

**Key files:** `Assets/_Modules/Dungeons/DungeonController.cs:616-650`,
`Assets/_Modules/Dungeons/State/DungeonRuntimeState.cs:377-390`,
`Assets/_Modules/Core/SceneRouter.cs` (result carrier), `Assets/_Modules/BattleATB/BattleController.cs`.

---

## WO-770.3b — Real-time encounter settlement + defeat parity (P0) [CODE]
**Depends on:** WO-770.3 (shared settlement) · **Couples with:** WO-770.1 (boss back-door). · **Canonical-tree only** — the real-time `BattleArena` path does not exist on the read-only tree; verify the seam on `wip/village2-and-f8-tickets`.

**Problem (surfaced during 770.3 implementation).** On the read-only tree the **entire**
encounter settlement is **scene-reload-coupled**: `DungeonController.EnterDungeon →
ResolvePendingEncounter → EncounterTrigger.ResumePendingEncounter → DungeonRuntimeState.
ResumeAfterEncounter` — the only place `_inCombat` clears, `MarkBossDefeated` fires, and the
run settles, and it runs **on scene load**. On canonical the **default** dungeon battle is the
real-time `BattleArena` (`ff.dungeonrealtime` ON), which `EncounterTrigger.LaunchBattle`
starts **fire-and-forget with no scene reload** — so **nothing triggers that settle**:
`_inCombat` stays latched, `BossDefeated` never sets on a win, the run never ends on a loss.
(This is the "real-time combat resume broken — combat lock never released, rewards/boss-state
never settle" item both audits independently flagged.) Consequence: **770.1's boss back-door
can never open on the default path**, and a lost real-time fight is silently survivable.

**Fix — one shared settlement, both paths routed through it (no duplicate logic).**
1. Extract `DungeonController.SettleEncounter(bool victory, bool wasBoss)`:
   - **victory** → `_runtimeState.ResumeAfterEncounter(true)`; if `wasBoss`
     `MarkBossDefeated()`; `DungeonLootGrant.GrantEncounter(wasBoss)`; resume in place.
   - **defeat** → the **WO-770.3 LOCKED** path: `ResumeAfterEncounter(false)` (clears
     `_inCombat`, no boss credit), no loot, `ExitToVillage()`.
2. **ATB path:** refactor `ResolvePendingEncounter` (the 770.3 carrier read) to call
   `SettleEncounter(carrier==Victory, wasBoss)` — behavior-preserving.
3. **Real-time path (seam CONFIRMED on canonical):** `BattleArena.OnBattleEnded`
   (`event Action<EncounterParams,bool>@225`, the `bool` = won) **already exists** — the gap
   is that **no Dungeons-module code subscribes** (only a tutorial adapter does) and
   **`MarkBossDefeated()` has zero callers.** Fix: subscribe in the Dungeons module (in
   `DungeonController` on enter, unsubscribe on destroy) and route into the shared
   `SettleEncounter(won, wasBoss)`. Derive `wasBoss` from `EncounterParams` (or read
   `_runtimeState.PendingEncounterIsBoss` if the handoff is still set). Confirm whether
   `OnBattleEnded` is static or instance (get the `BattleArena` ref accordingly).

   **Symptom this explains (assertable):** because `_inCombat` gates encounter re-firing (the
   `EncounterTrigger.Update` `if (InCombat) return;` guard) and the encounter clock, a
   real-time win that never clears `_inCombat` means **no further encounters ever fire** —
   the dungeon goes permanently quiet after the first fight. The oracle can assert `_inCombat`
   is cleared post-settle.

**Acceptance:**
- Both battle paths settle through the single `SettleEncounter` routine (no path-specific
  win/loss logic remains).
- A **real-time** win clears `_inCombat`, credits the boss (opening 770.1's back-door), and
  grants loot; a **real-time** loss ends the run, does NOT credit the boss, grants no loot, and
  returns to the Village — identical to the ATB path (WO-770.3).
- New oracle `[dungeon-defeat-realtime]` asserts the real-time loss ends the run and does not
  set `BossDefeated`; the existing `[dungeon-defeat]` (ATB) stays green.
- `WORK_ORDER_770_3b_*.RESULT.md`.

**Key files (canonical):** `BattleArena` (completion signal), `EncounterTrigger.LaunchBattle`
(real-time branch), `DungeonController` (`SettleEncounter` + both callers), `DungeonRuntimeState`,
new `[dungeon-defeat-realtime]` oracle.

---

## WO-770.4 — Make lore stones readable (P1) [CODE + OWNER-GATED]

**Fixes D6.** Triple gap: `LoreStone.Read()` has **no caller**, `ReadRequested`
has **no subscriber**, and `LoreStoneModal.uxml` **does not exist**. Lore is
authored (`lore-fragments.json`, 5 stones) but unreachable in gameplay.

**Fix:**
1. **[CODE]** Input: on hero proximity + interact key (reuse the `E` pattern from
   `CraftingPedestal`), call `LoreStone.Read()`. Add this to `LoreStone.Update()`
   or a small proximity-interact seam.
2. **[CODE + OWNER-GATED]** Build `Assets/_Modules/Dungeons/UI/LoreStoneModal.uxml`
   (+ `.uss`) and a `LoreStoneModalController` that subscribes to each stone's
   `ReadRequested`/`LoreReadRequest` and shows title+body (with the
   `IsPlaceholderFragment` marker surfaced for `journal-vault`). Wire it in
   `DungeonSceneBuilder` alongside the crafting panel (same pattern).

**Acceptance:**
- Approaching a lore stone shows the prompt; interacting opens a panel with the
  canon text; closing resumes movement; the read is recorded (questline beat
  advances) and re-reading is allowed but not double-counted.
- The `journal-vault` placeholder fragment is visibly flagged, not shipped as canon.

**Key files:** `Assets/_Modules/Dungeons/LoreStone.cs:172-205`, new
`Assets/_Modules/Dungeons/UI/LoreStoneModal.uxml`+`.uss`+`LoreStoneModalController.cs`,
`Assets/Editor/DungeonSceneBuilder.cs`.

---

## WO-770.5 — Consolidate the two Village→Dungeon entry systems (P1) [OWNER-GATED]

**Fixes D5 + D7.** The Village runs **both** 2 baked `DungeonPortal`s **and** 2
runtime `DungeonEntrance` ring doorways → ~4 doors for 2 dungeons. `DungeonPortal`
also **auto-routes on trigger-touch with no confirm** (`DungeonPortal.cs:84`), and
its serialized default `_dungeonId = "Dungeon_HealersCottage"` is a latent
double-prefix footgun (`:27,117`).

**DECIDED — keep the data-driven ring, retire the portal.** Keep `DungeonEntrance` +
`DungeonEntranceBootstrap` (the WO-19 ring): it's `DungeonDef`-driven, uses
`SceneRouter.LoadScene(full name)` (no prefix math), needs an explicit interact, and
scales to the scaffold dungeons. Remove the baked `DungeonPortal`s from
`VillageSceneBuilder.SpawnDungeonPortal` and retire `DungeonPortal.cs`. Result:
**exactly one** door per dungeon, explicit interact, no walk-by teleport, no
double-prefix footgun.

**Canonical-verify FIRST (seam may differ):** the "two systems" finding is from the
read-only tree. On `wip/village2-and-f8-tickets` the village is more evolved (raid
`EnemyOutpost`s, walk-to loop) — confirm which entry components the current Village
actually instantiates before deleting anything. If canonical already consolidated to
one, this WO is just the double-prefix/auto-route hardening on whichever survives.

**Acceptance:**
- The Village has one door per existing dungeon (2 today), each requiring an
  explicit interact; no accidental teleport on walk-by; no duplicate doors.
- Re-bake the Village via the owner-gated builder; verify in play mode.

**Key files:** `Assets/Editor/VillageSceneBuilder.cs:1765-1800`,
`Assets/_Modules/Village/Buildings/DungeonPortal.cs`,
`Assets/_Modules/Village/Dungeons/DungeonEntrance*.cs`,
`Assets/_Modules/Village/VillageController.cs:154`.

---

## WO-770.6 — Folk's Granary = the first-torch tutorial dungeon (P2) [CODE + OWNER-GATED]

**Fixes D2 by giving the stub a purpose (owner decision, 2026-07-26).** Instead of
deleting the dead-end or authoring a second full dungeon, make Folk's Granary the
**onboarding dungeon** that teaches the two core dungeon systems — the **lantern/torch
darkness mechanic** and **crafting** — by having the player **gather an ingredient and
craft their first torch**, then use it to light the dark and reach the exit. It reuses
the Healer's Cottage systems verbatim (no new mechanic code), in a small guided layout.

**Spec.**
1. **Author `StreamingAssets/Data/Canonical/dungeons/folks-granary.json`** — small +
   deliberately dark: `entrance`/`exit` blocks (WO-770.1 schema), 2–3 rooms, low ambient
   so the torch matters, `disableRandomEncounters: true`, at most one gentle scripted
   encounter (tutorial-safe — or none). Place one **oil/ingredient pickup** and one
   **crafting pedestal**.
2. **Add a "first torch" recipe** to `crafting-recipes.json` (gathered ingredient → torch /
   lantern fuel) — the craft target the tutorial guides toward.
3. **Wire a real `DungeonController`** into `Dungeon_FolksGranary` — extend
   `FolksGranaryBuilder` to match `DungeonSceneBuilder`'s controller wiring (hero, camera,
   `Lantern`, `DungeonInventory`, `CraftingPedestal`, `CraftingPanelController`, HUD, and
   the `DungeonExit` from 770.1). All of these already exist; this is wiring, not new systems.
4. **Guided prompts** via the 770.7 toast/Obsidian seam: "gather the oil-moss" → "craft a
   torch at the pedestal (E)" → "light the dark and find the way out."
5. **Prologue link (DECIDED):** completing the Granary **leads into `Dungeon_HealersCottage`** —
   the Granary is the on-ramp that teaches torch + craft, then hands the player into the first
   real dungeon. On the Granary exit, route to the Cottage (not the Village). This is the
   onboarding arc: learn the mechanics in the Granary → use them in the Cottage. (A "return to
   Village" exit can still exist as a bail-out, but the *forward* path is into the Cottage.)

**Acceptance.**
- Entering the Granary the player is in a dark space; the prompt teaches gathering the
  ingredient; crafting at the pedestal (`E`) yields the **first torch**; the torch visibly
  lights the dark (lantern reach/intensity change); the player reaches the exit and returns
  to the Village (via 770.1/770.2/770.3).
- Uses only existing `Lantern` + crafting systems (no new mechanic code); the torch recipe
  lives in `crafting-recipes.json`.
- No reachable empty-stub dead end remains; fights (if any) round-trip to the Granary (770.2).
- (If owner confirms the prologue) completing the Granary leads into the Healer's Cottage.
- `WORK_ORDER_770_6_*.RESULT.md`.

**Key files:** new `folks-granary.json`, `crafting-recipes.json` (torch recipe),
`Assets/Editor/FolksGranaryBuilder.cs` (wire `DungeonController`); reuse `Lantern.cs`,
`Crafting/CraftingPedestal.cs`, `Crafting/IngredientPickup.cs`, `Crafting/DungeonInventory.cs`,
`UI/CraftingPanelController.cs`, `DungeonExit` (770.1). `Resources/Dungeons/FolksGranary.asset`.

---

## WO-770.7 — Player feedback / toast layer (P2) [CODE + OWNER-GATED]

**Fixes D13 (+ D14).** `Checkpoint.ToastRequested`, `Checkpoint.Activated`, and
`CraftingPedestal.ToastRequested` fire into the void (0 subscribers): checkpoints
heal silently, crafting completes with no confirmation. `WandererDialogue`'s
`FirstMeet[]`/`Idle[]` canon lines are never surfaced by `Bryn`.

**Fix:**
1. Add a lightweight dungeon toast view and subscribe it to the three toast events
   (`Checkpoint.ToastRequested`, `Checkpoint.Activated`, `CraftingPedestal.ToastRequested`).
   **Build it code-built on the Obsidian kit, NOT uxml** — mirror the `LoreReadingModal`
   pattern CLI built for 770.4 (canonical CLAUDE.md §8: uxml does not work in builds). Reuse
   the same `DeNelle.Dungeons` UI seam the lore modal uses.
2. **D14 — decided, not optional.** Surface Bryn's first-meet line: on first
   proximity `Bryn` calls `WandererDialogue.PickFirstMeetLine(...)` and raises it
   through the same toast; idle lines use `PickIdleLine(...)`. **This WO firmly owns
   D14** — WO-770.9 no longer touches it. (If the owner instead wants those lines
   cut, that is a one-line deletion recorded here — but the default is to wire them.)

**Acceptance:** reaching a checkpoint, crafting an item, and meeting Bryn each show a
brief on-screen confirmation with the authored canon copy; Bryn's first-meet + idle
lines are surfaced (the `FirstMeet[]`/`Idle[]` pickers are no longer dead code).

**Key files:** `Assets/_Modules/Dungeons/UI/DungeonHudController.cs`,
`Checkpoint.cs`, `Crafting/CraftingPedestal.cs`, `Wanderer/Bryn.cs`, `DungeonSceneBuilder.cs`.

---

## WO-770.8 — Content assets: KayKit geometry, ambient BGM, asmdef (P2) [CONTENT/OWNER]

**Fixes D8 + D9 + D16.** Content/asset gaps (D10/D12 hero-integration split out to
WO-770.10). **Requires off-repo staged assets** — a developer cannot complete the first
two items from repo contents alone.
- **KayKit Dungeon geometry** — placeholder primitives in both scenes until the pack is
  staged. **`KayKit Dungeon Pack 1.0.zip` is already in Downloads** (CLI spotted it) — stage
  it into `Assets/Models/KayKit/` (gitignored, per WO-23) and re-run the dungeon builders in
  batchmode; placeholders resolve to real meshes. **Keep the bake %YAML** — for the RoomForge/
  composed scenes note the binary-scene gotcha (batchmode `SaveScene` can't honor ForceText);
  Healer's Cottage / Village / Granary bake clean.
- **`echoes-beneath-elarion.mp3`** absent → silent dungeon. Import to
  `Assets/Audio/dungeons/` and assign `DungeonController._ambientBgmClip` (guarded null
  today; no code change).
- **Newtonsoft.Json** (D16, self-contained, no asset needed) — add to
  `DeNelle.Dungeons.asmdef` `references` explicitly rather than relying on auto-reference.

**Acceptance:** with the pack staged, dungeons render KayKit geometry (not
`[PLACEHOLDER]`); the ambient loop plays; `DeNelle.Dungeons.asmdef` lists Newtonsoft
(this item verifiable with no external asset).

**Key files:** `.gitignore`, `DungeonSceneBuilder.cs`/`FolksGranaryBuilder.cs`,
`DungeonController.cs`, `DeNelle.Dungeons.asmdef`.

---

## WO-770.10 — Hero integration: real vitals + walk animation (P2) [CODE + CONTENT]

**Fixes D10 + D12** (previously uncovered / buried in content). The dungeon runs on
**placeholder** hero stats and a static hero.
- **D10 — real vitals.** `DungeonController` seeds `_heroBaselineHp=120`/`_heroBaselineMana=60`
  (`DungeonController.cs:118`) into `DungeonRuntimeState.SetHeroVitals`; the checkpoint
  heal + the ATB round-trip run off these constants. Replace with the real hero stats:
  read the selected hero's HP/mana from the save/hero-stat source and drive
  `SetHeroVitals` each frame (per the Week-6 checklist item 7 the doc-notes call out).
- **D12 — walk animation.** `DungeonHero` exposes `IsMoving`/`CurrentSpeed` (`DungeonHero.cs`)
  but has no Animator. Add an Animator Controller (idle↔walk↔run blend) driven by
  those fields. **Use the shared Animator Controller defined in WO-772** (the KayKit
  troop/enemy rig controller) so hero + troops + enemies share one clip set — build it
  once there, consume it here. Clips are off-repo (KayKit Adventurers / Mixamo), so this
  item is CONTENT-gated on the staged rig.

**Acceptance:**
- Checkpoint heal and the ATB round-trip use the **actual** hero's HP/mana, not 120/60
  (verified by entering with a non-default hero and reading the values).
- With the shared rig + controller staged, the dungeon hero animates idle/walk/run;
  the pure-code vitals change is verifiable without the rig.

**Key files:** `DungeonController.cs:113-121,260-266`, `DungeonHero.cs`, the shared
Animator Controller (WO-772), the hero-stat source (`GameState`/hero select).

---

## WO-770.9 — Housekeeping (P3) [CODE — safe to apply]

**Fixes D11 + D15.** Low-risk cleanups:
- `DungeonRuntimeState.OnEnable` should clear `_dungeonId`/`_currentRoomId`/lists
  (or document why not) to remove the stale-read window (`:518`).
- Retire the builder overlap on Folk's Granary: `DungeonStubBuilder.cs` and
  `FolksGranaryBuilder.cs` both target `Dungeon_FolksGranary.unity`. Pick one
  source of truth (see 770.6) and delete/annotate the other.

(D14 — the dead dialogue pickers — is owned entirely by **WO-770.7**; this order no
longer references it, removing the prior circular handoff.)

**Acceptance:** no two builders write the same scene; no stale-read window.

**Key files:** `DungeonRuntimeState.cs`, `Assets/Editor/DungeonStubBuilder.cs`,
`Assets/Editor/FolksGranaryBuilder.cs`, `Wanderer/WandererDialogue.cs`.

---

## WO-770.11 — Dungeon enemy-placement system (P2) [CODE + OWNER-GATED]
**Depends on:** WO-772 (enemy model), WO-770.2 + WO-770.3 (encounter return/result), WO-771.13 (shared rig/anim). · **Reuse:** `EncounterTrigger`, `DungeonController` hydration pattern.

**Owner ask (2026-07-26):** dungeons need an **enemy-placement system** — visible
placed/roaming enemies per room, not only the current invisible scripted `EncounterTrigger`
zones (which just fire an ATB fight when the hero crosses a radius).

**Fix.**
1. **[CODE — data]** Add an `enemyPlacements[]` block to the dungeon layout JSON (+ schema
   in `DungeonLayout.cs`): each `{ enemyId (resolves via WO-772 EnemyResolver), roomId,
   position{x,y,z}, behavior ("stationed"|"roaming"), patrolPath?[], encounterId }`.
2. **[CODE]** `DungeonEnemy` MonoBehaviour — a stationed or patrolling actor built from the
   resolved `EnemyDef` (WO-772) on the shared rig/Animator (WO-771.13). Roaming enemies
   patrol their `patrolPath` (simple deterministic waypoint loop). On hero proximity/contact
   it launches its ATB encounter by feeding its roster into the existing `EncounterTrigger`
   path (`ConfigureScripted`), so combat still routes through the ATB round-trip fixed by
   WO-770.2 (correct return scene) + WO-770.3 (real result).
3. **[CODE]** `DungeonController.HydrateEnemyPlacements()` — spawn one `DungeonEnemy` per
   layout entry, in layout order (mirror `HydrateLoreStones`/`HydrateEncounters`,
   `DungeonController.cs:522,573`).
4. **[OWNER-GATED]** Re-run the dungeon builder so placements spawn.

**Acceptance.**
- Healer's Cottage shows placed enemies per `enemyPlacements[]`; roaming ones patrol.
- Approaching/contacting one launches its ATB encounter and returns to the **correct**
  dungeon with the **real** result (via WO-770.2/770.3); a cleared placed enemy does not respawn.
- Roster + positions + patrol are fully data-driven (no hard-coded enemies).
- `WORK_ORDER_770_11_*.RESULT.md`.

**Key files:** `DungeonLayout.cs` (schema), new `Assets/_Modules/Dungeons/DungeonEnemy.cs`,
`DungeonController.cs` (`HydrateEnemyPlacements`), `EncounterTrigger.cs` (reuse),
`healers-cottage.json` (`enemyPlacements`), `DungeonSceneBuilder.cs`. Enemy model: WO-772.

---

## Regression smoke test (run after any sub-order)

Headless load-check (no game window), per SESSION_HANDOFF:
`DefendersOfTheRealm.exe -batchmode -nographics -bootScene Dungeon_HealersCottage -logFile <log>`
— expect "run live", no "Layout failed to load", no null-ref. Then in play mode
walk the full loop: **enter → read a lore stone → trigger a fight → win AND lose →
verify correct return scene + result → reach a checkpoint (toast) → beat the boss →
leave via the back-door → land back in the Village.** Every ✗ in the reachability
map (regression doc) must be a ✓.
