# Week 6 — Dungeon systems (Healer's Cottage playable slice)

**Date:** 2026-05-19
**Slice:** Fleshing out the Healer's Cottage interactables/systems layer so the
dungeon plays end-to-end — lantern oil mechanic, Bryn the NPC, lore-stones,
scripted ATB encounters, checkpoints.
**Status:** Source + canonical data written. The integrator wires the scene
(see "Integration items for the scene builder" below); the Week-6 agent does
NOT touch `Assets/Editor/DungeonSceneBuilder.cs` (integrator-owned), nor
`DungeonController.cs` / `DungeonLayout.cs` / any hero/camera files (Week-5
agent-owned). This agent cannot run Unity/shell builds — source only.

## What was already there

The Week-5/6 Dungeons-module scaffolds were substantially complete on arrival:
`Lantern.cs`, `Bryn.cs`, `WandererDialogue.cs`, `LoreStone.cs`,
`EncounterTrigger.cs`, `Checkpoint.cs`, `RandomEncounterTable.cs`,
`DungeonRuntimeState.cs`, `DungeonLayout.cs`, `DungeonController.cs`. This slice
fleshed the GAPS — the lore data layer + loader, the ATB round-trip, the
checkpoint heal, and routing Bryn's / lore-stones' copy through the data file.

## Files created

| File | Purpose |
| ---- | ------- |
| `Assets/StreamingAssets/Data/Canonical/lore-fragments.json` | Canonical lore data — Bryn's cottage-entry dialogue + the five Healer's Cottage lore-stone journal texts. Port spec Part 4 lists this as a shared canonical data file. ALL prose canon-verbatim except one flagged placeholder (see "Canon sourcing"). |
| `Assets/_Modules/Dungeons/LoreFragments.cs` | Typed C# records (`LoreFragment`, `LoreFragmentSet`) + `LoreFragmentsLoader` — async `UniTask` read from StreamingAssets, Newtonsoft.Json. Mirrors the `DungeonLayoutLoader` / `WaveDataLoader` / `PackCatalog` canonical-data pattern. Caches the parsed set; `Invalidate()` for the Monday sync / tests. |
| `docs/port-notes/week6-dungeon-systems.md` | This file. |

## Files changed (all Dungeons-module — within ownership)

| File | Change |
| ---- | ------ |
| `Assets/_Modules/Dungeons/State/DungeonRuntimeState.cs` | Added the **ATB encounter handoff** (`_pendingEncounterId` + boss flag + resume position) — a runtime-only SO survives `SceneManager.LoadScene`, so it carries the encounter across the ATBBattle scene round-trip. Added **hero vitals** (`_heroHp/_heroMaxHp/_heroMana/_heroMaxMana`) so HP/mana survives the round-trip and a checkpoint heal has numbers to work from. New methods: `BeginEncounterHandoff`, `ResumeAfterEncounter`, `ClearPendingEncounter`, `SetHeroVitals`, `HealHeroToFull`. `StartRun` deliberately does NOT clear the handoff/vitals (must survive a dungeon-scene reload); `EndRun` + `OnEnable` do clear them. |
| `Assets/_Modules/Dungeons/EncounterTrigger.cs` | Added `ConfigureBoss(...)` — adapts a `DungeonMiniBoss` def into the scripted-encounter path so one `TickScripted` drives both ordinary fights and the mini-boss. Scripted/random firing now also calls `BeginEncounterHandoff` (stashes encounter id + hero resume position before the scene routes away). Added `ResumePendingEncounter(bool victory)` — the dungeon controller calls this on re-entry to settle the in-flight fight (a boss victory marks the boss defeated). Exposed `EncounterId`. |
| `Assets/_Modules/Dungeons/Checkpoint.cs` | The shrine's heal is now concrete — `CheckProximity` calls `DungeonRuntimeState.HealHeroToFull()` on first activation (in addition to the existing `Activated`/`ToastRequested` events and the `ReachCheckpoint` save). |
| `Assets/_Modules/Dungeons/Wanderer/WandererDialogue.cs` | Added `ResolveCottageEntryLine(LoreFragmentSet, fragmentId)` + `CottageEntryFragmentId` — sources Bryn's entrance line from `lore-fragments.json#bryn-cottage-entry`, falling back to the inlined canon `HealersCottageLine` when the data file is absent. |
| `Assets/_Modules/Dungeons/Wanderer/Bryn.cs` | Added `SetLoreFragments(LoreFragmentSet)`. `ChooseLine` now resolves the fresh-visit line in priority order: lore-fragments.json → layout JSON `firstEncounterLine` → inlined canon — all the same verbatim canon prose. |
| `Assets/_Modules/Dungeons/LoreStone.cs` | Added `SetLoreFragments(LoreFragmentSet)` + `IsPlaceholderFragment`. `Read()` now sources title/body from `lore-fragments.json` keyed by the stone id, falling back to the layout JSON's inline copy; a missing fragment logs a warning (cross-stream drift detector). |

`Lantern.cs` and `RandomEncounterTable.cs` were reviewed and needed **no
change** — the oil mechanic, the flicker-audio guard, and the seeded
random-encounter math were already complete and correct.

No `.asmdef`, no `.meta`, no `.uxml` files were hand-created. `DeNelle.Dungeons`
asmdef + namespace unchanged. `docs/unity-decisions.md` not edited.

## The ATB hand-off API used

The dungeon module references `DeNelle.Core` (not `DeNelle.BattleATB`) — module
isolation (port spec Part 2). The encounter therefore reaches the battle by the
canonical Core route:

- **Launch:** `EncounterTrigger.LaunchBattle` builds a
  `DeNelle.Core.BattleParams { Wave = 0, BreachedIds = roster }` and calls
  `SceneRouter.GoBattle(p)`. `Wave == 0` is the **dungeon marker** — a village
  breach always carries `Wave > 0`. `SceneRouter.GoBattle` stashes the params on
  `SceneRouter.PendingBattle` and fades into the `ATBBattle` scene.
- **Handoff carrier:** the `DungeonRuntimeState` SO survives the scene load, so
  `BeginEncounterHandoff(encounterId, isBoss, resumePosition)` carries the
  encounter identity + hero resume point across into ATBBattle and back. Hero
  HP/mana ride along in the same SO (`SetHeroVitals`).
- **Return:** on dungeon re-entry the controller calls
  `EncounterTrigger.ResumePendingEncounter(victory)` →
  `DungeonRuntimeState.ResumeAfterEncounter(victory)` (clears the combat lock,
  marks the boss defeated on a boss victory, clears the handoff).

**Known limitation (flagged for the integrator / Week-2 battle owner):**
`Core/SceneRouter.cs` `BattleController.ReturnAfterResult` currently hard-codes
the post-battle return to `Village`, and `BattleParams` has no return-scene
field. Both are outside this agent's ownership (Core + BattleATB). For the
dungeon round-trip to close, the battle scene must return to
`Dungeon_HealersCottage` when `PendingBattle.Wave == 0`. The dungeon side is
fully wired to resume; the village-vs-dungeon return branch in `BattleController`
(or a `ReturnScene` field on `BattleParams`) is the remaining cross-module piece.

## Canon sourcing — what was lifted verbatim vs flagged

Sourced READ-ONLY from the React v1 repo + Unity docs (never written to):

- **Bryn's cottage-entry line** — VERBATIM from
  `defenders-of-the-realm/src/modules/dungeons/atmosphere/wandererCopy.ts`
  (`HEALERS_COTTAGE_BRYN_LINE`) and `docs/dungeon-3d-healers-cottage-design.md`
  §4 Beat 1. Matches the `firstEncounterLine` already in `healers-cottage.json`.
- **Lore stones `journal-1`…`journal-4`** — VERBATIM from
  `defenders-of-the-realm/src/modules/dungeons/3d/content/healersCottage.lore.ts`
  (`HEALERS_COTTAGE_LORE`) and design doc §4. Same bodies as the layout JSON.
- **`journal-vault` paragraph 1** (the `M.M. + A.M., 31st of Honeymonth`
  carving) — VERBATIM from design doc §4 Beat 5, line 206.

**Flagged as PLACEHOLDER (NOT canon — `"placeholder": true` in the JSON):**

- **`journal-vault` paragraph 2** (the struck-through draft prose). The Hidden
  Vault room is a Unity-side expansion from
  `dungeons-3d-unity-layout-spec.md` §9.3; there is **no verbatim source** for
  this draft in the narrative bible or the v1 repo (§10.5 references a
  "Letter 3a redacted draft" for D6, not D1). The fragment body carries an
  explicit `[PLACEHOLDER — NOT CANON]` marker and a `placeholderNote`. It must
  be sourced from the narrative team or the `journal-vault` stone cut before
  ship. `LoreStone.IsPlaceholderFragment` surfaces this at runtime.

The pre-existing `healers-cottage.json` already carries an invented
`journal-vault` body inline — `lore-fragments.json` now flags that same content
as a placeholder, and `LoreStone.Read()` prefers the (flagged) fragment-set copy
over the unflagged layout-JSON copy, so the placeholder is visible rather than
silently shipping as canon.

## Integration items for the scene builder

`Assets/Editor/DungeonSceneBuilder.cs` (integrator-owned — NOT touched here)
needs to, for `Dungeon_HealersCottage.unity`:

1. **Lantern** — child a `Light` (type Point) on the hero rig with a `Lantern`
   component; `DungeonController.ConfigureLantern` already hands it the oil
   stones. Optionally wire an `AudioSource` to `Lantern._flickerAudio` — IF a
   `lantern-flicker.mp3` exists. **It does not exist in `public/audio/`**
   (only battle/defeat/title/victory/village). Leave `_flickerAudio` null; the
   `Lantern` guards a null clip and stays silent. Do NOT invent audio.
2. **Bryn** — place a `Bryn` MonoBehaviour; give it a world-space UGUI speech
   bubble implementing `IWandererBubble` (assign to `_bubbleBehaviour`). After
   `LoreFragmentsLoader.LoadAsync()`, call `Bryn.SetLoreFragments(set)` and
   `Bryn.SetHero(hero)`.
3. **Lore stones** — spawn 5 `LoreStone` objects from `Layout.loreStones`;
   `Configure(def, runtimeState, hero, totalLoreCount=5)` each, then
   `SetLoreFragments(set)`. Build `LoreStoneModal.uxml` (UI Toolkit) + a HUD-side
   controller subscribing to each stone's `ReadRequested` event. The dungeon
   module raises the typed `LoreReadRequest`; it carries no HUD dependency.
4. **Checkpoints** — spawn 2 `Checkpoint` shrines from `Layout.checkpoints`;
   `Configure(def, runtimeState, hero)`. Wire a HUD toast to `ToastRequested`.
5. **Encounter triggers** — spawn `EncounterTrigger` objects:
   `ConfigureScripted(...)` per `Layout.scriptedEncounters` (4), and
   `ConfigureBoss(controller, Layout.miniBoss, triggerRadius, runtimeState,
   hero)` for the Workshop mini-boss.
6. **ATB round-trip** — on dungeon re-entry after a battle, `DungeonController`
   should check `DungeonRuntimeState.HasPendingEncounter`; if set, place the
   hero at `EncounterResumePosition`, read the battle outcome off the ATB
   runtime state, and call the matching `EncounterTrigger.ResumePendingEncounter
   (victory)`. (Controller is Week-5-agent-owned — noted, not edited.)
7. **Hero vitals** — the scene loop should call
   `DungeonRuntimeState.SetHeroVitals(hp, maxHp, mana, maxMana)` so the
   checkpoint heal + the ATB round-trip have live numbers. The dungeon module
   owns no hero-stat type; the hero component (Week-5-owned) re-reads
   `HeroHp`/`HeroMana` after `HealHeroToFull()`.
8. **Build Settings** — `Dungeon_HealersCottage` and `ATBBattle` must both be in
   Build Settings for `SceneRouter` round-trips.
