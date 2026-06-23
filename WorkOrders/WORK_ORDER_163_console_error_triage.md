# WORK ORDER 163 — Console Error/Warning Triage (clear the spam to a clean boot)

**Status: READY TO IMPLEMENT**
**Priority:** HIGH — 3,300+ console errors/frame spam; buries real errors + costs perf. (Game still boots — none are crashes.)
**Date:** 2026-05-30
**Lane:** mixed (code + a couple asset/mixer fixes). No `VillageSceneBuilder` rewrite required; some items are asset-side.
**Source:** UI triaged the uploaded `Editor.log` (2026-05-30). **No runtime exceptions / NullRefs exist** — the noise is the buckets below.

---

## Triage summary (from Editor.log — exact counts)

| # | Count | Severity | Bucket |
|---|---|---|---|
| **1** | **3,351** | **ERROR (the real one)** | `AmbientNPC.UpdateAnimator` calls `Animator.SetFloat("Hash …")` **every frame** with a parameter the animator controller doesn't have → `Parameter 'Hash -823668238' does not exist.` spam |
| 2 | 16 | Warning | Trees "must use the Nature/Soft Occlusion shader" (terrain tree prototypes on a non-billboard shader) |
| 3 | 9 | Error (benign, self-silencing) | `AudioMixerBridge` — mixer has no exposed params `MasterVol`/`MusicVol`/`SfxVol` (name mismatch; "further calls silenced") |
| 4 | 4 | Warning | Mirza Beig "Terrain Rain" shaders missing dependency `ASESampleShaders/SimpleTerrainBase` (VFX pack shader dep) |
| 5 | 3 | Info-ish | "page could not be found" / DevPanel.uxml not found (DevPanel uxml not in a Resources folder) |
| — | many | Benign | "Native extension for X target not found" (normal Unity editor noise — ignore) |

**Bucket 1 is ~99% of the spam and the only one that matters for perf/noise.** The rest are
low-priority warnings.

---

## Fixes

### 1. AmbientNPC animator parameter mismatch (THE fix — kills 3,351 errors/run)
**File:** `Assets/_Modules/Village/NPCs/AmbientNPC.cs:347` (`UpdateAnimator`), called from `Update` (:201).
**Cause:** `SetFloat(<hashed param>, …)` is driving a parameter (likely "Speed"/locomotion) that the
ambient NPC's animator controller **does not define** — so every NPC logs the error every frame.
Likely fallout from the WO-140 hero-animator / Humanoid-rig change: the NPCs got a controller (or had
theirs swapped) that lacks the param the code drives, OR they have no valid controller at all.
**Fix options (CLI's call):**
- Guard the call: cache the param hash and **check `HasParameter` before `SetFloat`** (iterate
  `animator.parameters` once at init; skip the drive if absent). Cheapest, robust — no more spam even if
  a controller lacks the param.
- AND/OR ensure ambient NPCs get a controller that **has** the locomotion param (route them through the
  shared animator factory / `AnimatedObjectFactory` direction so NPC + hero share the param contract).
**Acceptance:** zero `Parameter 'Hash …' does not exist` lines after a play session.

### 2. AudioMixer exposed-param name mismatch (9 errors → 0; also unblocks volume control)
**File:** `Assets/_Modules/Settings/AudioMixerBridge.cs:111/118`. The bridge sets `MasterVol`/`MusicVol`/
`SfxVol` but the **AudioMixer asset doesn't expose those names**. Either (a) expose those exact params on
the Master/Music/SFX groups in the mixer asset, or (b) rename the code's strings to match what the mixer
*does* expose. **This also means volume sliders currently do nothing** — fixing it restores audio
settings (and supports WO-162 music selection). Pick one source of truth for the names; document it.

### 3. Tree soft-occlusion shader warnings (16 → 0; low priority)
Terrain tree prototypes (Tree_*_Color1) want the `Nature/Soft Occlusion` shader for billboarding. Either
swap those tree prototypes to the soft-occlusion shader, or (if they're not used as billboarded terrain
trees) accept/suppress. Cosmetic — lighting/billboard correctness only.

### 4. Mirza Beig "Terrain Rain" shader dep (4 → 0; low priority)
The VFX pack's Terrain-Rain shaders reference a missing `ASESampleShaders/SimpleTerrainBase`. If Terrain
Rain isn't used, ignore/strip; if used, import the missing ASE sample shader or repoint the dependency.

### 5. DevPanel.uxml not found (1; benign)
`DevPanel.uxml` isn't under a Resources folder so the QA dev console won't auto-spawn. Move it to
`Assets/_Modules/DevTools/Resources/` if auto-spawn is wanted; else ignore (dev-only).

---

## Priority within this WO
- **Do #1 first** — it's the 3,351-error spam, the perf cost, and what hides any future real error.
- **#2 second** — quick, and restores volume control (+ helps WO-162).
- #3/#4/#5 are warnings — clear if time, fine to leave for last.

## Constraints
- Guard pattern (`HasParameter` before `SetFloat`) — no `System.Reflection` added; brace-gate.
- Mixer fix may be an **asset edit** (expose params) — do it in-editor, not a scene bake.
- Village→Core only; `?.` on cross-module; no UXML added.

## Done checklist (CLAUDE.md §10)
- [ ] Zero `Parameter 'Hash …' does not exist` after a play session (AmbientNPC guarded / controller fixed)
- [ ] AudioMixer params resolve (no "Exposed name does not exist"); volume sliders work
- [ ] Tree + Mirza-rain shader warnings cleared or consciously accepted (note which)
- [ ] DevPanel handled or ignored (noted)
- [ ] Console boots clean (no error spam); brace balance on edited `.cs`
- [ ] `WORK_ORDER_163_console_error_triage.RESULT.md` when complete
