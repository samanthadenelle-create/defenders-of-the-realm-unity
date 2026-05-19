# Owner Acceptance Checklist

Acceptance requirements the owner has called out for the build, beyond the
spec Part 9 gates. State: **DONE / PARTIAL / GAP / IN PROGRESS**.

---

## Prioritization — owner decision (2026-05-19)

The owner confirmed these features were **always part of the design** — they
are in scope for the polished Solana Foundation grant submission, not deferred.
Effort runs on the autonomous-agent timescale (code + regression + QA per
feature, ~1 hr ceiling each), not calendar days — see `grant-submission-goal`.

**IN SCOPE — building now, 4 parallel workstreams:**
- **A** — Intro flow (hero-select + pet-select screens), difficulty system
  (Easy/Normal/Hard), force-field tutorial beat
- **B** — Player wall-repair mechanic, between-wave countdown styling
- **C** — Dungeon crafting system, lantern oil/duration HUD readout
- **D** — Ambient townsfolk NPCs + dialogue, camera-angle tune

**Still v2.1 / next-build (genuine external blockers, not scope cuts):**
- Weapon selection + upgrades — owner-scoped to v2.1 earlier; a multi-hour
  per-weapon-animation content effort (`docs/roadmap/next-build.md`).
- Voiced tutorial — hard-blocked on VO audio assets (none in project; cannot
  be produced autonomously). The text tutorial ships complete.
- Live-ops / admin backend; real Solana SDK; unique boss models;
  exterior-wilderness polish.

---

## Intro & first-run flow
Intended flow: **intro music (first screen) → DeNelle Studios bumper →
hero select → pet select → launch into the world.**

| Step | State | Note |
|------|-------|------|
| Intro music on the title screen | IN PROGRESS | Title scene exists; per-scene BGM is being wired by the audio system agent. |
| "DeNelle Studios" bumper | DONE | The studio-bumper scene exists (Week 1). |
| Hero-select screen | **GAP** | No hero-selection UI exists. The hero class is data only (`HeroClassOpt`); there is no pick-a-hero screen in the flow. |
| Pet-select screen | **GAP** | No pet-selection UI. The 3 starter pets + `PetDeployer` exist, but no pick-a-pet screen. |
| Launch into the world | PARTIAL | `SceneRouter` reaches the village, but the select screens above are not in the chain. |

## Tutorial / onboarding
| Item | State | Note |
|------|-------|------|
| First-run tutorial flow | IN PROGRESS | Onboarding agent building: welcome → the Heart → build → place a pet → Wave 1. UI-prompt based. |
| Explain the magical force-field gates keep enemies out | **GAP** | Not in the current tutorial scope. To add as a tutorial beat. |
| A method to fix / repair walls | **GAP** | `WallSegment` / `Gate` / `Building` have a `Repair()` API, but there is **no player-facing repair mechanic** — no repair action, cost, or UI. The mechanic must be built before the tutorial can teach it. |
| Walking tutorial vs. audio/voiced tutorial | TBD | Current onboarding is UI-prompt based. A voiced tutorial needs VO assets (none in the project). |

## Real-time battle
| Item | State | Note |
|------|-------|------|
| Battle system tested | **GAP** | Not runtime-tested — no playable build yet (Week 8). The ATB engine has 64 unit tests; the *scene* is unverified at play. |
| Mobile-friendly screen real estate | PARTIAL | `BattleHUD` is UI Toolkit; the input-controls audit flagged HUD touch targets too small + no on-screen controls. Needs a mobile-layout pass. |
| Combat animations tied to weapons | PARTIAL (by design) | This build uses **basic combat animations** (`AnimatorSetup` Attack/Hit/Death) with no weapon variety — owner-decided default. Weapon selection + per-weapon animation + upgrades → **v2.1** (see `docs/roadmap/next-build.md`). |

## Camera
| Item | State | Note |
|------|-------|------|
| Camera angle properly placed | PARTIAL | The dungeon has a fixed-tilt Cinemachine rig (`DungeonCameraRig`). The village + battle camera placement needs a review/tune pass. |

## Difficulty & waves
| Item | State | Note |
|------|-------|------|
| Between-wave countdown timer — small, top of screen | PARTIAL | `VillageHud` has a wave indicator with a countdown (`SetWave(number, countdown)`) in the top strip — the display exists; confirm it styles as a small top timer. |
| Difficulty toggle — Easy / Normal / Hard | **GAP** | No difficulty system exists. Owner spec: a toggle (in the help / settings menu) that scales the between-wave countdown duration — **Easy 10 min · Normal 5 min · Hard 3 min** (Normal 5 min ≈ the current 300 s default). Needs a Difficulty setting + the `WaveManager` / `waves.json` countdown scaled by it. |

## Village life
| Item | State | Note |
|------|-------|------|
| Ambient townsfolk — NPCs moving about / standing in town | **GAP** | The village has static "city dressing" (buildings, props) but no ambient-people system. Needs wandering/idle NPC agents (KayKit characters) populating the village. |
| Engage dialogue with townsfolk via word bubbles (appear / disappear) | PARTIAL | The word-bubble tech exists — `WandererBubble` (Bryn's billboarded speech bubble, shows + hides). Reusable for townsfolk; the engage-on-approach interaction, the dialogue content, and the NPC system itself are the GAP. |

## Dungeon — lantern & crafting
| Item | State | Note |
|------|-------|------|
| Torch / lantern duration mechanic | PARTIAL | `Lantern.cs` has the oil mechanic — light falls over time, refill at oil stones. The mechanic exists; player-facing clarity (how to light it, a duration / oil readout in the HUD, a tutorial beat) is a GAP. |
| Crafting a torch — recipe + items needed | **GAP** | No crafting system exists. The dungeon spec hints at a "crafting shard pedestal" (currently a placeholder primitive). A crafting system + item / recipe data must be built. |

## Related (already tracked in the audits)
- Performance check → `docs/audit/mobile-performance.md` + the memory audit (in progress)
- Mobile-first setup → `docs/audit/mobile-performance.md` (P0s applied)
- D-pad sensitivity / on-screen controls → `docs/audit/input-controls.md`

---

**Summary of new gaps this raises:** hero-select screen, pet-select screen, a
player-facing wall-repair mechanic, tutorial beats for force-fields + repair,
a battle-HUD mobile-layout pass, and a camera-placement review. All feed the
backlog; prioritise against Week 8.
