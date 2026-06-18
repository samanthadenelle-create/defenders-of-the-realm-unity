# Town Loop — CANON (the core FTUE → defend sequence)

**Status: CANON** (owner-validated 2026-06-17). The single authoritative definition of the town loop +
its current wired state. Was previously scattered across ~15 docs + the code + the yarn — this is the
one place it lives. Keep current as beats change.

## The loop (owner's words, 2026-06-17)
```
onboard  →  land in town  →  NPC guided tour  →  get a pet (at the Pet House)  →  spawn a wave + build & manage towers
```
Each beat earns its place: onboarding sets identity; the town is home; the tour teaches the space; the pet
is the first companion + the offline-economy teacher; the wave + buildable/manageable towers is the core
defend gameplay. **Constraints (owner): no colors breaking, no "No node" Yarn issues.**

## Wired state (FTUE audit 2026-06-17 — beat by beat)
| Beat | Component / file | State |
|---|---|---|
| Boot / splash (WebGL audio unlock) | `Onboarding/TitleController` | ✅ wired |
| Hero pick | `TitleController` + `HeroSelectController` → `SceneRouter.GoPetSelect()` | ✅ wired |
| **Pet SELECT screen** | `Onboarding/PetSelectController` → `GoCastle()` | ⚠️ **STILL IN PATH — owner wants it CUT** |
| Land in town | `SceneRouter.GoCastle()` → `MainCastle_Hall` | ✅ wired |
| NPC guided tour | `Village/Tutorial/TutorialDirector` + `CompanionMeeting.yarn` (camera-glances: forge/arcane tower/pet house/market) via `CompanionMeetingTrigger` | ✅ wired (Yarn narration; live walk-tour retired) |
| Get a pet AT the Pet House | `Dialogue/Structures/StructureMenu.yarn` PetHouse node + `<<spawn_named_pet>>` → `PetDeployer` | ✅ wired |
| Spawn a wave | `Village/Waves/WaveManager` (singleton + retry + watchdog, this session) | ✅ wired (note: bot-probe sees a flaky start; the game-side wave starts — instrument-confirmed) |
| Build & manage towers | build mode (`BuildModeController`) + `TowerManagerPanel` | ✅ wired |
| Clean UI | code-built (ElarionUi palette), `ff.blinkchrome` gating | ✅ no color breaks detected |
| No no-node | FTUE yarn (CompanionMeeting/PetHouse/StructureMenu) | ✅ mitigated (incl. WO-438 Inn C# node-start hook) |

## The cuts / gaps to a clean playthrough (not blockers)
1. **Remove the PetSelect screen** (owner intent) — route `HeroSelect` straight to `GoCastle()`; pet-pick
   ONLY at the Pet House. One wiring change + delete/retire `PetSelect.unity`. (Decision was open; owner
   leaning remove.)
2. **Pet naming UI** — TODO; the pet takes its catalog name now (no free-text entry panel yet).
3. **Build-mode wall-pay** — WO-442 (red-ghost when a wall drag exceeds funds).
4. **Input discipline** keeps the loop clean — WO-437 (battle-lock + no stray hotkeys) landed this session.

## The diagnostics goal (so this stays self-evident)
Today this state required an agent to READ the code. Per WO-444 (wrap core deep), the loop should EMIT its
own health: a single playthrough fires `[Flow:FTUE]` at each beat (boot → tour-started → each glance →
pet-spawned → wave-armed), captured (break-log + WebTrace). Then "is the town loop healthy?" is a trace
read, not an audit. **Instrument the loop's beats** as the loop is touched.

*Cross-ref:* `PIPELINE_STATE.md`, `docs/NORTH_STAR.md`, `docs/BUILD_MODE_ARCHITECTURE.md`,
`CompanionMeeting.yarn`/`StructureMenu.yarn`, WO-437 (input gate)/442 (wall-pay)/444 (wrap-core)/441
(the EXPANSION loop that follows: raid → rescue → capture → player base).
