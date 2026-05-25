# WORK ORDER 10 — RESULT (PARTIAL — autonomous limits documented)

**Executed:** 2026-05-24 (evening) under Standing Authority #35 + WO-025
**Mode note:** WO-10 is a purely *observational* smoke test — every item requires watching the running game in Editor playmode **and** the built player, across the full Title→HeroSelect→PetSelect→Village→combat→ATB→dungeon flow. An autonomous agent cannot watch playmode or reliably drive the built player through that UI flow (the "build-side verification gate" documented in WO-06/07/08). **Per owner direction, this RESULT is an honest partial:** items are marked by what is *autonomously knowable* (build/compile + static/structural verification from WO-05–08), and every observational item is flagged for owner eyes-on. **No ✅ is claimed for anything not actually observed.**

**AC4 (clean build):** ✅ `[DesktopBuild] SUCCEEDED — 559 MB`, 0 errors, 0 warnings — builds all 7 scenes at current HEAD (post WO-05–08). The exe is at `Builds/Windows/DefendersOfTheRealm.exe`.

---

## Legend

| Mark | Meaning |
|---|---|
| ✅B | Confirmed at **build** level (compiles + scene builds into the player) |
| 🟢S | **Structurally verified** via code/static analysis or a prior WO (high confidence) — runtime eyes-on still recommended |
| 👁 | **Observational only** — owner must watch it run (Editor + build); not autonomously determinable |
| ❌S | **Static analysis shows a problem** — follow-up WO filed |

---

## Village scene

| Item | Mark | Basis |
|---|---|---|
| Village.unity loads w/o compile errors | ✅B | build SUCCEEDED building Village.unity; player boots Title clean (0 errors, WO-06) |
| Heart HP bar 100/100, decreases on breach | ❌S | `SetHeartHp` has no runtime caller except DevPanel → bar won't update. **→ WO-19** |
| Wave counter ticks (1→…→apex 4) | 🟢S / 👁 | `WaveHudBridge` pushes `SetWave` (WO-07); progression through waves is 👁 |
| 5-min countdown timer between waves | 🟢S / 👁 | same `SetWave(number,countdown)` path; actual countdown 👁 |
| Hero renders (no magenta, no T-pose) | 👁 / ❌S | render 👁; **T-pose/no-walk is a known bug → WO-18** |
| Hero WASD movement | 👁 | HeroLocomotion present (transform-move); motion is observational |
| Hero rotates to face movement | 👁 | observational |
| Hero Walk animation plays / Idle when still | ❌S | **known bug → WO-18** (locomotion sets Speed but no clip plays; no `.controller`) |
| Pets render (flame/ice/aether) | 🟢S / 👁 | WO-05 fix + assets present & names match; eyes-on pending |
| Pet labels hover above meshes | 🟢S / 👁 | `PetDeployer.AddPetNameTag` + billboard; eyes-on pending |
| Q/W/E/R abilities fire on keypress | 🟢S | WO-07 verified input→TryCast→effect path + scene wiring |
| Build button opens build menu | 🟢S / 👁 | HUD bridge + reflection `BuildMenu.Open` (WO-06/07); actual open 👁 |
| Daily Quests panel renders, legible | 🟢S / 👁 | UI Toolkit HUD renders (WO-06); content 👁 |
| Compass shows heading | 🟢S / 👁 | `CompassHud` wired; heading 👁 |
| Mana bar visible | 🟢S | WO-07 **fixed** the missing mana push — now fed each frame |

## Combat / waves

| Item | Mark | Basis |
|---|---|---|
| Wave 1 spawns 8 Hollow Walkers (N gate) | 👁 | `WaveManager` present; spawn count/behaviour observational |
| Enemies path toward Heart | 👁 | NavMeshAgent enemies; observational |
| Force-field gate blocks enemies | 🟢S / 👁 | Gate blocker BoxCollider (solid while up); eyes-on |
| Gate proximity-opens on hero approach | 🟢S / 👁 | **WO-08** implemented + build-clean; walk-through eyes-on pending |
| Enemy dmg → 25% collapse → path through | 🟢S / 👁 | `Gate.TakeDamage`/collapse intact (WO-08 preserved it); eyes-on |
| Breach → ATB transition (`SceneRouter.GoBattle`) | 👁 | observational |
| ATB scene loads, combat playable | ✅B / 👁 | ATBBattle.unity builds; playability 👁 |
| ATB returns to Village | 👁 | observational |
| Village resumes wave loop from same wave | 👁 | observational |

## Apex boss

| Item | Mark | Basis |
|---|---|---|
| Wave 4 spawns Black Dragon | 👁 | `WaveManager` boss path present; observational |
| Dragon orbits Heart at cruise altitude | 👁 | observational |
| Dragon HP bar depletes on hit | 👁 | observational |
| Dragon death drop + animation | 👁 | observational |

## Audio

| Item | Mark | Basis |
|---|---|---|
| Title / Village / Battle music | 👁 | GameAudioMixer corruption was fixed in the 2026-05-24 recovery; playback is observational |
| Master volume slider attenuates | 👁 | observational |
| Music / SFX sliders independent | 👁 | observational |

## Settings

| Item | Mark | Basis |
|---|---|---|
| Difficulty (E/N/H) persists across sessions | 👁 | observational (persistence) |
| Mute toggle persists | 👁 | observational |
| Hero class selector (3 options) persists | 👁 | observational |

## Dungeons

| Item | Mark | Basis |
|---|---|---|
| Dungeon scenes load from Village | ✅B / 👁 | Dungeon_HealersCottage + Dungeon_FolksGranary build; runtime load 👁 |
| Hero + pet present in dungeon | 👁 | observational |
| Enemies present + attackable | 👁 | observational |
| Return-to-village transition | 👁 | observational |

---

## Tally (autonomous pass)

- **✅B / 🟢S (build- or structurally-verified, high confidence):** 14 items
- **👁 owner eyes-on required (not autonomously determinable):** ~30 items
- **❌S static-confirmed problems:** 2 → **Heart HP/Crystals push (WO-19, new)**, **Hero walk animation (WO-18, pre-existing)**

This is **not** a "44/44 green" pass — it is an honest map of what could be confirmed without eyes-on. The build is clean and every system's *code/wiring* that I traced (WO-05–08) is sound; the bulk of the checklist is runtime behaviour that needs a human (or a future `-bootScene` dev hook) to observe.

## Follow-up WOs created / referenced

1. **WO-19 — `WORK_ORDER_19_hud_data_binding.md` (NEW):** Heart HP bar + Crystals counter have no runtime push (statically confirmed) → they show stale defaults. Mirror the WO-07 mana/cooldown bridge pattern.
2. **WO-18 — hero walk animation (pre-existing in master):** "locomotion sets Speed but no clip plays / no `.controller`." Covers the Walk-animation + T-pose checklist items.

## Recommendation to make this WO fully executable next time

The single highest-leverage unblock for WO-10 (and WO-11 ATB, and any build-side verification) is a **dev-only `-bootScene <name>` / "skip to Village" command-line hook** (flagged in WO-06/08). With it, an autonomous run could boot straight into Village/ATB/dungeon and a batchmode playmode + `ScreenCapture` pass could mark many of the 👁 items objectively. Without it, WO-10 is inherently an owner-run checklist.
