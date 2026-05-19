# Owner Acceptance Checklist

Acceptance requirements the owner has called out for the build, beyond the
spec Part 9 gates. State: **DONE / PARTIAL / GAP / IN PROGRESS**.

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

## Related (already tracked in the audits)
- Performance check → `docs/audit/mobile-performance.md` + the memory audit (in progress)
- Mobile-first setup → `docs/audit/mobile-performance.md` (P0s applied)
- D-pad sensitivity / on-screen controls → `docs/audit/input-controls.md`

---

**Summary of new gaps this raises:** hero-select screen, pet-select screen, a
player-facing wall-repair mechanic, tutorial beats for force-fields + repair,
a battle-HUD mobile-layout pass, and a camera-placement review. All feed the
backlog; prioritise against Week 8.
