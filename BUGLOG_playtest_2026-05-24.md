# Playtest bug log — 2026-05-24 (owner QA pass)

Owner reported these live while playtesting the Windows build. Triaged + root-caused
as far as static analysis + the Village runtime log allow. **Classification drives
who fixes what:** curated-scene geometry is filed as follow-up WOs (per owner
"scene fixes → follow-up WO" direction), code/material issues are fixed directly.

Runtime baseline (from `-bootScene Village` Player.log): WASD movement works;
`[WaveManager] Loop armed — wave 1, countdown 300.0s`; Active Input Handling = Both;
hero `animator/controller = null` (WO-18); AudioMixer params not exposed.

| # | Bug | Root-cause hypothesis | Class | Plan |
|---|---|---|---|---|
| 1 | HUD **Build button** does nothing | UI-Toolkit click not reaching HUD (shared w/ 4,5) — bridge wired by builder, but clicks dead | HUD-click cluster | investigate input/panel; one fix likely covers 1+4+5 |
| 2 | **Pets move in straight lines** | `PetHeroLeash` picks varied wander points, but `Pet` drifts to them linearly (MoveTowards) | **code** | smooth Pet motion (accel/curved steering) |
| 3 | **Dungeons load into a stub** | the `Dungeon_*` scenes themselves are placeholder stubs (recovery Agent 3) | **content** | file WO (needs dungeon geometry/KayKit dungeon pack) |
| 4 | **Abilities do nothing** | (a) HUD ability buttons = click cluster; (b) keyboard 1-4 fires but no enemy to hit (wave not started → see 5) | HUD-click cluster + linked to 5 | same fix as 1/5; verify with an enemy present |
| 5 | **No way to trigger wave** | the wave auto-starts at 300s; the *clickable wave-timer* (ForceBeginNextWave) is in the dead-click cluster | HUD-click cluster | same fix as 1/4 |
| 6 | **Force fields are a box in front of each entrance**, not on the door | Gate force-field renderer/collider offset/size wrong relative to the gap | **scene/builder geometry** | file WO (curated scene; builder can't re-run) |
| 7 | **South→SE gate doesn't touch the wall** | gate placement leaves a gap vs the wall segment | **scene/builder geometry** | file WO |
| 8 | **Force field should be more transparent** | `ForceFieldGate.mat` / shader alpha too high | **material** | lower the material's base alpha (tracked `.mat`) |
| 9 | **Can walk through the Spire (Heart)** | the Heart/Elarion GameObject has no solid collider | **code (runtime-add)** | add a collider to the Heart at runtime via VillageController (no scene edit) |
| A | **Volume sliders dead** (from log) | AudioMixer has no exposed `MasterVol`/`MusicVol`/`SfxVol` params | **mixer asset** | expose the params on GameAudioMixer (or fix the names the bridge expects) |
| B | Hero static / no walk anim (from log) | no `.controller` (FBX has no NLA tracks) | already **WO-18** | (Mixamo round-trip — owner) |

## Additional reports (same pass)

| # | Bug | Class | Plan |
|---|---|---|---|
| C | "?" object doesn't open the dev-tools / help screen | HUD-click cluster | same root cause as 1/4/5 (agent 1) |
| D | Leaving the village → no map / zones / world beyond | content/architecture (Agent-7) | file WO (exterior-world) — agent 4 |
| E | SE corner lets the village spawn OUTSIDE the wall | scene/builder geometry | file WO — agent 4 (SE layout) |
| F | City/buildings spawn IN the wall | scene/builder geometry | file WO — agent 4 (placement) |

Status: pet movement (#2) FIXED in code (Pet.cs — eased accel/decel + arrival damp + per-pet jitter). 4 triage agents running; fixes applied as they land.

## RESOLUTION STATUS (worklist)

**FIXED in code/asset (committed — verified compile, build clean):**
- #1 Build button, #4 ability buttons, #5 wave-skip, #C "?" help/dev-tools → ONE fix: `UIInputModuleFix.cs` (runtime `AssignDefaultActions()` when the package actions asset doesn't load — clears the whole HUD-click cluster in all scenes).
- #2 pets linear → `Pet.cs` eased accel/decel + per-pet speed + arrival damp.
- #9 walk-through Spire → `HeartController.cs` runtime solid `CapsuleCollider`.
- #8 force-field transparency → `ForceFieldGate.mat` `_BaseAlpha 0.42→0.2` (visible once #6's sheet exists).

**FILED as follow-up WOs (need builder/GUI/content — controlled execution):**
- #6 force-field-as-box, #7 SE gate-wall gap, #E spawn-outside-wall, #F city-in-wall, #G NW walls → **WO-22** (village wall + gate geometry).
- #3 dungeon stub → **WO-23** (import KayKit Dungeon pack — gitignored-Models content).
- #D no exterior world → **WO-24** (zone architecture, Agent 7).
- #A volume sliders → **WO-25** (rebuild GameAudioMixer in the Audio Mixer GUI).
- #B hero static / no walk anim → **WO-18** (Mixamo round-trip — pre-existing).

**NEEDS EYES-ON (couldn't root-cause blind):**
- #H "no bar showing hero life / mana / pet status" → **investigated (boot screenshot `wo-bugH-village-hud.png`): the HUD renders fine** (ability bar visible bottom-centre, buildings/hero/pets render). The Heart-HP ("Elarion") + mana panels exist in `VillageHud.uxml` and are now data-pushed (WO-07/20). What's genuinely missing is a **dedicated hero-character-health bar and a pet-status display** — neither exists in the HUD design. → **design gap, not a break.** Follow-up: add a hero-HP bar + pet-status widget to `VillageHud.uxml` + `VillageHudController` (small additive HUD WO; pet status would need a Pets→HUD bridge like the others). Not blocking.

## Fix routing
- **Code/material (I can do now, no curated-scene edit):** #2 pets, #8 force-field transparency, #9 Spire collider (runtime-add), #A audio mixer params.
- **HUD-click cluster (#1, #4, #5):** needs root-cause on UI-Toolkit click input; one fix likely resolves all three. **Pivotal:** does this reproduce in normal Title→HeroSelect→PetSelect play, or only via `-bootScene Village`? (`-bootScene` skips onboarding/integrator state and may falsely show dead HUD.)
- **Scene/builder geometry → follow-up WOs (per owner direction):** #6 force-field box position, #7 gate-wall gap.
- **Content → follow-up WO:** #3 dungeon stubs.

## Playtest 2026-05-25 (clean Windows build, Builds/Windows)

| # | Bug | Severity | Root-cause | Class | Plan |
|---|---|---|---|---|---|
| I | **Player hard-crashes loading Village** — `The file '.../level3' is corrupted! [Position out of bounds!] Crash!!!`, preceded by `d3d12: upload buffer was too small for the requested resource! Requested: ~37MB` | **CRITICAL** | Village instantiates raw un-decimated Tripo structure meshes (Cathedral 84MB, PetHome 54MB, LumberMill 52MB, Forge/Farm 29MB) — a single >35MB mesh overflows the D3D12 upload buffer, cascading into the level3-load corruption. NOT static batching (already off); not fixable by reverting Village. | **content / art (mesh size)** | decimate the TripoStructures + Cathedral meshes to game-res, OR swap to KayKit building meshes, OR (quick test) force D3D11 graphics API |
| J | **Particle Velocity curves must all be in the same mode** — logged repeatedly during the intro/Title sequence | **MEDIUM** | A ParticleSystem in the intro/Title scene has its Velocity-over-Lifetime module X/Y/Z curves set to mismatched modes (e.g. one Constant, one Curve). Non-breaking warning, no gameplay impact. | **scene/builder (particle setup)** | open the intro/Title particle FX, set all three Velocity-over-Lifetime axes to the same curve mode (or normalize in the scene builder) |

