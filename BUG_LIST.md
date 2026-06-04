# Bug List — tagged to user stories, assigned to Claude (CLI)

> Live punch-list of open playtest bugs. Each is tagged to a **user story** ("As a player, I want…")
> and **assigned to Claude (CLI)** for implementation. Grouped by lane (parallel-safe per PARALLEL_LANES.md).
> Owner: Samantha. Updated 2026-05-31. ✅ = done (RESULT filed). ⛔ = P0 blocker.
>
> **Lane rule:** the **Village Builder lane is single-writer** (`VillageSceneBuilder.cs`) — those bugs are
> ONE sequential pass + rebake by one agent. ATB / UI / animation lanes run in parallel (own files).

---

## ⛔ LANE: VILLAGE BUILDER (single-writer — do as ONE wall/gate/terrain pass + rebake)
*All edit `VillageSceneBuilder.cs`. Assign to ONE CLI agent. Order: P0 first.*

| ID | Story | Bug | Status |
|---|---|---|---|
| **WO-173** ⛔P0 | *As a player, I want to see a world around my village* | World is a **black void** — exterior terrain orphaned by scene-split / wiped by rebake | OPEN |
| **WO-177** | *As a player, I want the castle walls to look right* | Wall segment **leaning / 180° off**; also **hero walks through wall** (collision not effective) + **south gate is wrong** (dungeon arch, not castle gate) | OPEN |
| **WO-158** | *As a player, I want to exit my castle* | Gates **impassable / not 4 gates** — add north gate, make all 4 passable | OPEN |
| **WO-167** | *As a player, I want gates that look built right* | Gatehouse **pillar clips through the ceiling** (all 4) | OPEN |
| **WO-168** | *As a player, I want enemies to path through gates* | **NavMesh seals the gate openings** — unseal so spawn→Heart works | OPEN |
| **WO-157** | *As a player, I want a clean village* | **Magenta crystal veins** (deleted content re-spawning) — strip | OPEN |
| **WO-176** | *As a player, I want towers that fit the art style* | Tower **functional but ugly** — swap to stylized polyperfect mesh, fix materials | OPEN |
| **WO-179** | *As a player, I want the moat to look like water* | Water sits **on top of ground** (should be **below grade in a channel**) + apply style-pass water material | OPEN (water style gated on VFX pass) |
| — | *(then)* | **Village rebake** after the above land (WO-137 pattern) | OPEN |
| WO-166 ✅ | *As a player, I want gates/walk-anim/pet to work* | base gates + walk-anim + pet + stairs | **DONE** |

---

## LANE: ATB / BATTLE (parallel-safe — own files, `DeNelle.BattleATB`)
*Assign to a CLI agent. WO-169 is the big one; start order is inside it.*

| ID | Story | Bug | Status |
|---|---|---|---|
| **WO-169** | *As a player, I want a real FF party battle* | Enemies are **purple capsules** (no model swap); **1-hero not party**; **layout backwards** (should be enemies-LEFT/heroes-RIGHT); **Skills opens nothing** (caster has no spell menu); HUD needs clean; **target one-or-all**; per-member command/AI; dynamic HUD; data-driven | OPEN |
| **WO-170** | *As a player, I want battles to feel like retro FF* | No **2D retro spell VFX / sprite animations** yet (pairs with WO-169) | OPEN |

> **ATB start order (from WO-169):** 1) swap enemy model (kills capsules) → 2) flip layout (enemies left) →
> 3) surface the party → 4) Skills/Item menus work + targeting → 5) dynamic HUD. Then WO-170 juice.

---

## LANE: ANIMATION (parallel-safe — ONE param-contract fix covers all three)
*Assign to a CLI agent. WO-163/174 + the pet are the SAME animator-param bug — fix once.*

| ID | Story | Bug | Status |
|---|---|---|---|
| **WO-174** | *As a player, I want my hero to move + animate right* | Hero **travels backwards** (orientation) + **no walk animation** (animator param) | OPEN |
| **WO-163** | *As a player, I want a clean, performant game* | **3,351 console errors** — `AmbientNPC` drives a missing animator param every frame; + AudioMixer exposed-param (breaks volume sliders) | OPEN |
| (pet, via 166/163) | *As a player, I want my pet animated* | **Pet T-pose** — same animator-param/controller bug | OPEN (fold into the anim pass) |

---

## LANE: UI / POLISH (parallel-safe — own files, code-built UI)
*Assign to a CLI agent.*

| ID | Story | Bug | Status |
|---|---|---|---|
| **WO-156** | *As a player, I want to see over my high walls* | **Camera fix NOT built** — pitched at horizon, hero off-screen; +camera conflict (3 controllers, HeroCinemachineRig priority 100); needs over-wall framing + orbit + wall-fade | OPEN |
| **WO-175** | *As a player, I want a shop that feels part of the game* | Store is a **generic dark box** — themed frame, real item icons, themed buttons/scrollbar | OPEN |
| **WO-178** | *As a player, I want a polished HUD* | **Health bars unstyled** (flat green) — restyle to match the themed quest-panel HUD | OPEN |

---

## Summary
- **Open bugs:** 14 (1 P0). **Done:** 1 (WO-166).
- **Critical path to a clean playtest:** WO-173 (P0 world) → the Village Builder pass (177/158/167/168/157/176/179) → rebake. In parallel: the Animation pass (174/163/pet), ATB (169→170), UI (156/175/178).
- **Biggest single visual win:** WO-173 (world exists) + WO-169 step 1 (enemy capsules → models).

## Notes for CLI
- Village-builder bugs = **one agent, one sequential pass, one rebake** (don't parallelize within the file).
- Animation bugs (174/163/pet) = **one animator-param fix** — don't implement three times.
- Camera (156): **decide the authoritative camera first** (HeroCinemachineRig vs SmartMobileCamera) before tuning.
- File a `*.RESULT.md` per WO; UI/owner re-checks via screenshot after the rebake.

🤖 Bug list compiled by UI lane from the open WO-15x/16x/17x set. Tagged to user stories, assigned to CLI.
