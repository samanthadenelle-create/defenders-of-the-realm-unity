**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK_ORDER_481 — Knight + Orcs V1 Vertical (make the Knight perfect)

**Status: READY TO IMPLEMENT (Phase 1 gate first)**
**Owner directive:** 2026-06-22. North star: `docs/COMBAT_PIVOT_NORTHSTAR.md`.
**Canon:** memories [[tripo-roster-knight-orcs-first]], [[echo-workforce-drag-drop]], [[combat-pivot-single-hero-northstar]].

## Goal
Stand up the V1 single-hero vertical using the new **Tripo roster**: the **Knight** hero vs the **Orc**
enemy family (Mage / Tank / Warrior). Do the Knight **perfectly — everything about it** — before touching
Ranger/Wizard or the Skeleton/Troll families. This WO is the foundation; later families just reuse the rig
+ pipeline it proves.

## Assets (STAGED in repo, not yet imported/promoted)
Copied to `Assets/Art/Incoming_Tripo/` (outside Resources, non-destructive):
`Heroes/Knight/Knight.fbx` (+ PBR) · `Enemies/Orcs/{Orc_Warrior,Orc_Tank,Orc_Mage}/*.fbx` (+ PBR).
All Tripo, humanoid, skinned, ~6 sections each.

**CRITICAL RECONCILE (owner-confirmed 2026-06-22):**
- The **new** Knight is **ARMORED** — armor baked into the mesh = the pivot's static-armor body. This is the
  canonical playable Knight going forward. (Tiny FBX = external textures, not missing geometry.)
- The **existing** `Assets/Resources/Heroes/Knight.fbx` is **NAKED** (the bare-Tripo body that armor never fit —
  the reason for the Blink detour) BUT carries the **full animated `Knight.controller`** (Attack0-2/Block/Cast/
  Combo/Hit/Dead/Victory/**WindUp**; Base+UpperBody layers). **KEEP IT as the ANIMATION DONOR — do NOT overwrite.**
- Plan: import the new armored body → confirm Humanoid → **retarget the donor clip set onto it** (Humanoid muscle
  space is rig-agnostic). Promote the new armored Knight into the Resources runtime path ONLY once animated +
  verified (deliver-complete-verified). The same retarget feeds the 3 Orcs.
- History (why this is right): naked Tripo heroes wouldn't take rigged armor → Blink modular swap → Blink failed
  (ShareBaseSkeleton bone-map spam, disliked) → pivot = ONE pre-armored model, static armor, no swap. The new
  armored Tripo models ARE that resolution. NO armor bundle (same rig-fit wall a 3rd time). Equipment = weapon +
  shield visible (hand-bone attach, works) + rings/amulet/boots invisible stat slots; armor static/baked.
- Tripo material fix (extract → URP/Lit + 4 maps) needed on import to avoid magenta (existing Tripo FBX bake
  absolute texture paths that won't resolve).

---

## Phase 1 — IMPORT + RIG/ANIM AUDIT (the GATE — do this and STOP for owner review)
**No downstream work until this is captured + reported (CLAUDE.md §12 — instrument, don't guess).**
1. Import all 4 FBX. Set rig = **Humanoid**; generate/verify the avatar maps clean on each (no broken bones).
2. **Confirm the clip-set question:** does each FBX carry usable animation takes, or only a bind/T-pose?
   Report exactly what clips exist per model.
3. Confirm section count (owner says ~6) + material/texture setup; URP-convert the Tripo materials
   (reuse the existing Tripo/magenta material-fix path — do not hand-wire).
4. **Deliverable:** a short RESULT note stating, per model: avatar OK? clips present (list) or none? section
   count, material status. This decides Phase 2's shape (retarget existing vs source a clip set).

**ACCEPTANCE (Phase 1):** all 4 import without errors; avatars validated Humanoid; the clip-set verdict is
captured as DATA (not assumed); materials render (no magenta). Owner reviews before Phase 2.

## Phase 2 — Shared Humanoid clip set + retarget  (blocked by Phase 1)
- Author/assemble ONE Humanoid clip set: **idle · walk · attack · cast · block · hit-react · heal-cast ·
  ranged-cast · death**. Retarget to Knight + all 3 Orcs (one controller, mirrored per side).
- Animation-as-mechanics: the **attack wind-up must read as a telegraph**; **block** is a real mechanic clip.
- If Phase 1 finds no usable clips, source them (Mixamo/owner Tripo anims) — flag to owner which route.

## Phase 3 — Mesh Baker 6→1 + decimate/compress  (blocked by Phase 1; parallel-safe with Phase 2)
- Per character: Mesh Baker **Skinned Mesh Combiner** → ~6 sections to **1 SkinnedMeshRenderer + 1 atlas**
  (same skeleton; verify animation still drives the combined mesh). Per-character only — NO cross-character merge.
- Poly-decimate the dense Tripo meshes; downsize/compress the 2K PBR atlases for WebGL/mobile.
- Capture before/after: draw calls per character, tri count, texture memory.

## Phase 4 — Battle anchor + Knight kit + skill tree  (blocked by 2 & 3)
- **Battle anchor FIRST** (northstar's highest-leverage prototype): fixed hero stance + fixed Orc stance(s) +
  composed 3rd-person cam; every fight snaps to it; slight push-in on your turn / punch-in on big hits.
- Knight **ability kit** (every ATB turn is a decision): basic ranged · heal/sustain · burst/control · optional
  reposition. Fold the dead skill points + companion-heal/ranged into the **heal+ranged skill tree** (reconcile
  onto WO-432 / WO-476 perk work — do NOT greenfield).
- Equipment per northstar: **weapon + shield = visible flair/upgrade slots** (shield drives the block mechanic);
  **armor static** (stat only, baked — no mesh swap); rings/amulet/boots = invisible ability/stat slots.
- Knight-only via `ff.knightonly` (ON). Single-hero via `ff.singlehero` (ON).

---

## What NOT to touch
- **No companion / party / Blink-armor revival** — `ff.blinkarmor` stays OFF; do not re-add party combatants.
- **No base-building / waves / troops** — `ff.basebuilding` stays OFF (V2). Outpost reward = skill points/gear.
- **Do NOT regen MainCastle_Hall** (hand-dialed) or hand-edit any `.unity` (use builders).
- **Do NOT pull in Ranger/Wizard or Skeleton/Troll** assets this WO — Knight + Orcs only.
- **No echo work here** — echo workforce (cap 5, drag-drop) is a separate WO; this is hero+enemy art+combat.

## Notes
- §0: all `.cs` via Write/Edit on Windows path only; brace/NUL gate via `CompileGate.Run` before commit.
- The shared-rig + Mesh Baker pipeline this WO proves becomes the template for the other 2 families + 2 classes.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
