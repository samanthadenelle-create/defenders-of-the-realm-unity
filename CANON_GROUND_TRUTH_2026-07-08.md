# CANON GROUND TRUTH — 2026-07-08

> **Purpose:** the single anchor of *current reality*, derived ONLY from verified sources (the working
> tree, HEAD commits, headless gate/fleet captures, the exe on disk, live prep/handover docs, owner
> felt-verdicts). If a doc contradicts a line here, the doc is STALE. **Supersedes
> `CANON_GROUND_TRUTH_2026-07-03.md`** (now bannered SUPERSEDED).
> Sourced 2026-07-08 from: `git log`/`git status` (HEAD `d944d161`, 71 commits ahead of origin, clean
> tree), `CLI_PREP_2026-07-08_next-session.md` + `RESUME_2026-07-08_overnight-f8-sweep.md`, the exe on
> disk (`Builds/Windows/DefendersOfTheRealm.exe` stamped 2026-07-08 05:10:11), a fresh headless gate
> pass on HEAD (`COMPILE_GATE_OK` + `REGRESSION_OK`), and `SaveSchema.cs` (v28).

## Repo / git
- **Branch:** `wip/village2-and-f8-tickets`. **HEAD `d944d161`** (docs). Last CODE commit = `bb0094cc`
  (fleet compass probe). **71 commits ahead of `origin`, nothing pushed, working tree clean.**
  **Push HELD for owner word** (backup-to-remote vs. release-to-master/prod is the owner's call).
  Sole committer = CLI, by explicit path.
- **Save schema = v28** (`SaveSchema.CurrentVersion = 28`, WO-587 Population & Echo growth; additive
  nonNegInt default-on-read). The `CurrentVersion = 20` line in `docs/ARCHITECTURE.md §5` /
  `docs/MASTER_CATALOG` is STALE metadata — code is v28.

## Build / deploy state
- **Windows felt-pass exe:** `Builds/Windows/DefendersOfTheRealm.exe` stamped **2026-07-08 05:10:11**
  (reflects current code HEAD — only docs commits followed the `bb0094cc` build).
- **Headless gates green on HEAD (this session):** `COMPILE_GATE_OK :: scripts compiled clean` +
  `REGRESSION_OK`. (Logs: `Builds/compile-gate.log`, `Builds/data-regression.log`.)
- **Fleet on exe 05:10:11 = ZERO tickets, all probes PASS.** Re-verified this session (6 runs, seeds
  70000): `No breaks recorded — clean run` — tutorial arms, first-tower placement PASS, dialogue chain
  A→B survives + input releases (the P0), albedo 19/19 no WHITE HERO ROOT, 13/13 HUD panels, popup-close
  13/0, orient-modal releases, save round-trip PASS, vendor talk-route 0 violations, wave rules armed,
  compass pips PASS, combat invariants PASS. Overworld probes skip "no hero" = the hub-capped coverage
  (WO-453), expected. 218 render-artifact records filtered (`-nographics`).
- **WebGL PREVIEW (current):** https://defenders-of-the-realm-v2-h0h6hfsf5.vercel.app (READY; deployed
  from `bb0094cc`; **supersedes** the pre-morning `2dizrqgws` and the 07-03 `69mafg5pj` previews).
  **Production Vercel UNTOUCHED** — still the 07-01 verified Pi sign-in build; promotion is the owner's.
  itch web build remains live.

## Live thread — THE FEEL ARC + the F8 ticket program (current focus)
- Focus is still **THE FEEL ARC** (owner: "the most important thing is how it FEELS"; the ten-year-old
  test is the standing quality bar — headed felt-verify before claiming polish; headless proves binding,
  not feel).
- Running mode = the **F8 ticket program**: owner felt-tests, F8 flags a break, the F8 watcher
  auto-harvests the trace, CLI RCA's from captured data + fixes + headless-verifies, owner felt-verifies
  + closes (§13 pipeline, §14 watcher, RCA-proof-by-data pipeline rule 0).

## What landed since the 07-03 anchor (all LOCAL, push held)
- **P0 "still cant do the tower" — FIXED + owner felt-confirmed.** Dialogue `Closed` re-entrancy
  destroyed the successor dialogue's panel → `HeroLocomotion.InputSuppressed` stuck → build-mode Update
  frozen (zero PlaceConfirm evals). Fix = per-VM Closed identity guard (`82422d11`). Tutorial completes,
  placement works. 8 real-input verification probes PASS 4/4 in the fleet.
- **The 07-07/08 F8 board (30+ tickets) is fixed / spec'd / evidence-pinned**, each with its verbatim
  proving line (RCA-proof rule). Highlights: white-Paladin root fix (fbx-embedded textures extracted +
  durably remapped, `externalObjects=1`, audit `19/19` carry `_BaseMap` — the fleet's oldest ticket);
  F8-24 castle wall-stairs swept from the SHIPPED merged scene + navmesh rebaked (`13e85e12`); dialogue
  rebuilt on FrameCore (box-in-a-box gone); **"Tap to continue ▸" passive hint replaced the Continue
  chip** (owner 2026-07-08); scatter enemy families danger-banded; tower identity (Ballista bolts /
  Arcane casts, upright orientation, ground placement); F8-31/32 nameplate GUID repair + portrait
  circle-mask; F8-33/35 Victory rows + ability icons; combat HUD v8 default ON; harvest verbs; orient
  modal registers with PanelManager.
- **Wave 2 CLOSED (05:10)** — all morning lanes committed through `bb0094cc`, final fleet ZERO tickets,
  WebGL deployed to the `h0h6hfsf5` preview.
- **Step-in/step-out instrumentation shipped + stays in (toggleable):** every build-mode placement gate,
  the encounter spawner loop, and the dialogue bootstrap name themselves when they block — "nothing
  happened" is no longer capturable silently. `[Flow:DeathTrace]` forensics live for F8-15.

## Canon corrections carried forward (still true)
- V1 = **single controllable Knight "Grom"** in an overworld with isolated real-time **BattleArena**;
  base-defense + tower-defense are **V2-gated** behind `ff.basebuilding`. **ATB is flat/static, gated**
  (`ff.atbdungeon` OFF). Hero = **one Tripo self-rigged model**, static armor, NO mesh-swap (Blink hero
  rig JUNKED 06-22; Blink survives as a UI re-skin kit only).
- World: home hub `MainCastle_Hall`; `OuterWorld` streams additively; `Village2` = raid target;
  `Village.unity` ABANDONED. Two-scene navmesh seam = confirm-to-cross + WarpTo.
- Title: **"Echoes of Elarion"** (chapter) in the **"Defenders of the Realm"** series; tagline
  **"Hold the last light."** Village name = **Elarion**; Heart of Elarion = the living world-Tree at (0,0,0).

## Open tickets / decisions (board = source of truth; the load-bearing ones)
- **F8-37** arena pole — giant untextured cylinder in BattleArena (ArenaCentre 5000,0,5000); RCA not run.
- **F8-38** root-while-casting — enemies walk while channeling; ruling = cast is a rooted commitment window.
- **F8-39** towers vanish on death, all return on next placement — one-source-of-truth rebuild on respawn.
- **F8-40** max-tier tower identity (owner directive) — idle aura VFX + recolored/stronger projectiles +
  Ballista range at max tier; **not color-only** (colorblind rule) — shape/trail carries.
- **F8-41** waves must ATTACK the city (owner directive) — enemies path in and hit lane DEFENSES en route
  (all implement `IDamageableStructure`); RCA the `[Flow:EnemyAggro] ProbeForStructure null` capture.
- **F8-42** repair costs (owner directive) — damaged structures persist HP + costed Repair ("data only
  always"); destroyed = full rebuild cost. Ties to WO-432 tech-tree + WO-612 timers.
- **WO-614 skill-tree solo rework — RULED, READY TO IMPLEMENT (the big next lane).** New signature
  actives cut from premium mocap; bottom-right rail = signature moves (T1 = a RANGED Thunderbolt/Arcane
  Blast); **"data only always"** (no code hooks). `WorkOrders/WORK_ORDER_614_skill_tree_solo_rework.md`.
- **Design pins (owner):** F8-23/26 wave-countdown-as-Battle posture · WO-613B outpost chunk rebuild
  (spec READY, go pending) · 11 NPC portraits still card-framed art (PO call) · promote preview→prod ·
  push authorization.
- **Known pre-existers (fleet-named, unchanged):** WO-602 home-return unwired · CavePortal seam
  unreachable (closest 442.9m > 16m, bake gap) · WO-453 rep spawn-gate.

## Key docs (read order for a cold start)
`CANON_GROUND_TRUTH_2026-07-08.md` (this — the live anchor) → `SESSION_CANON_LOADER.md` →
`CLI_PREP_2026-07-08_next-session.md` (the wave-2 close prep) → `docs/MASTER_CATALOG.md` (relevant area)
→ `docs/ARCHITECTURE.md` → `CLAUDE.md` + `PREFLIGHT_GATE.md`. Ledger of the overnight sweep:
`RESUME_2026-07-08_overnight-f8-sweep.md`. Combat north-star: `docs/COMBAT_PIVOT_NORTHSTAR.md`.
</content>
</invoke>
