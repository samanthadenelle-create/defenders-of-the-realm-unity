<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 190 — Import + optimize the Orc Necromancer as an enemy (OVERNIGHT)

**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

**Status: QUEUED for overnight** · **Date:** 2026-05-31 · **Lane:** Art/Enemy pipeline (CLI gatekeeper gates the build)
**Source folder (NOT yet in project):** `C:\Users\Kayden-Laptop\Downloads\orc+necromancer` (~99.6 MB)
**Owner ask:** "the size is big and i cant decimate it and keep color" — deferred to overnight.

---

## What's in the download
- `orc necromancer.fbx` — **27.7 MB** high-poly Tripo mesh (the decimation target).
- `orc necromancer.fbm/` textures: `orcnecromancer_basecolor.PNG` (0.5 MB) + normal/metallic/roughness,
  and **`orcnecromancer_rm.PNG` = 14.1 MB** (one oversized roughness/metallic map).
- Animations (each re-exports the full rigged mesh, ~13 MB): `walk.fbx` (+ `walk.json`) and a
  `casual walk/` set — `walk-relaxed-start/loop/end-*.fbx`. The **loop** is the one to use for the walk state.

## ⚠ THE "keep color" GOTCHA (don't rediscover this)
The color is the **`orcnecromancer_basecolor.PNG` texture mapped through the mesh UVs — NOT vertex color.**
So decimation keeps the color **as long as UVs are preserved.** "Losing color" on decimate = the tool is
dropping UVs or re-meshing. **Decimate in a UV-preserving mode** (Blender Decimate → Collapse keeps UVs;
or the InstaLOD path validated for the CC5 heroes — see memory `catalog-thesis-validated-live`), then the
basecolor PNG re-applies to the low-poly mesh.

## Steps
1. **Decimate** `orc necromancer.fbx` to a mobile budget (~5–15k tris; the CC5 hero target was ~7.8k),
   **preserving UVs** so the basecolor texture still maps. (Blender Decimate-Collapse or InstaLOD.)
2. **Shrink textures** — the quick win independent of the mesh: drop `orcnecromancer_rm.PNG` (14 MB) and
   the others to **1024 (or 512) Max Size** on Unity import. Tools present: `Defenders/Optimize All Assets
   (FBX + Textures)` (`UniversalAssetOptimizer`) + `Defenders/Tripo/Force re-extract all textures`.
3. **Import** the decimated mesh into the project (Resources/Enemies or Resources/Enemy/Orc), attach
   `DeNelle.Core.TripoMaterialFixer` (memory `tripo-fbx-material-fixer`) so the Phong material renders URP.
4. **Animation** — the orc is a Tripo with its OWN clips (the `walk*` FBXs). Build a **Generic** animator
   from its own walk-loop clip (NOT Mixamo retarget — that fails on Tripo rigs; and a prior Generic test
   showed clip bone-paths must bind to the SAME hierarchy, so author the controller against this exact FBX).
   Drive it from the enemy's Speed param (Enemy.cs already drives Speed/Attack/Hit/Dead).
5. **Wire as an enemy** via the enemy factory/roster (EnemyAnimatorFactory / VisualFactory / the HollowRoster
   or region-enemy spawning WO-155). Confirm it walks/attacks/dies in a wave.

## Acceptance
- Orc necromancer appears in-game as an enemy, **correctly colored** (basecolor texture intact after
  decimation), at a mobile-reasonable tri count + asset size, animating its own walk.
- Build stays green (CLI gatekeeper builds + commits by explicit path; commit only the decimated mesh +
  shrunk textures, not the raw 100 MB download).

## ⭐ Bigger picture — this is character #1 through a CHARACTER HARNESS / FACTORY (owner vision 2026-05-31)
The orc is NOT a one-off; it's the first asset through a reusable **CharacterFactory** (same pattern the
project already uses: VisualFactory / EnemyAnimatorFactory / StructureFactory / catalog->factory). Build the
**harness once**, then every paid character flows through one automated path; the owner adds advanced
posing/art ON TOP later.

**Harness steps (automate these, in order):**
1. **Import** the paid Tripo FBX + its .fbm textures.
2. **Bake the color to a texture** (owner's method): load into an empty Unity scene, bake the appearance
   down to a single albedo/basecolor texture. The look then lives in the TEXTURE, not the geometry.
3. **Decimate** the mesh to a mobile budget (~5-15k tris) **preserving UVs** -> the baked texture re-maps,
   so color survives any reduction. (Blender Decimate-Collapse or InstaLOD; CC5 heroes hit ~7.8k.)
4. **URP material** via TripoMaterialFixer; **shrink** the oversized maps (e.g. the 14 MB rm.PNG -> 1024/512).
5. **Animator from the model's OWN clips** (Generic, authored against THIS exact hierarchy so bones bind --
   NOT Mixamo retarget, which fails on Tripo rigs).
6. **Register** in a character library/factory (by role: hero / enemy / boss) so the spawner/catalog can
   instantiate it like any other.
**Output:** game-ready character, correctly colored, animated, mobile-sized -- repeatable for every future
paid character. Owner keeps the creative posing/advanced animation as a layer on top; the harness never
blocks it and never discards the color.

## Notes
- Do NOT commit the raw 27 MB FBX or the 14 MB rm.PNG — commit the decimated/shrunk versions only (keep git lean).
- Relates to the hero/pet animation work: heroes = Humanoid+Mixamo (failing), enemies/Tripo = own Generic clips.
- The orc necromancer is a PAID asset (Tripo + its own walk animation) -> it's a MAIN character, worth the harness polish.

> **AUDIT 2026-08-21 (agent fleet, read-only):** OPEN — STILL VALID. Evidence: `no orc/necromancer FBX imported` — source asset off-repo. Status left at READY deliberately: this work is real and unbuilt. Verified against HEAD 2f0b97bb5, not against the ticket's own claims.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict. ⚠ NOTE FOR ANYONE REOPENING: the 2026-08-21 read-only audit had classified this one OPEN - STILL VALID, with the evidence cited above. The owner's review supersedes that call (owner statements are ground truth). The audit line is left in place deliberately, so if this work turns out to be needed, the evidence for it is still here rather than erased.
