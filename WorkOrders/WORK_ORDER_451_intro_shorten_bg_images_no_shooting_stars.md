<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 451 — Shorten cold-open intro, soft background scenes, remove shooting stars

**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

> **PARTIAL - re-scoped 2026-08-14 (phantom sweep).** Most of this WO is present in HEAD; a named
> remainder is outstanding. No per-WO path:line was recorded here: see the 2026-08-14 phantom sweep for the
> implementation site and the remainder. Do not re-implement the shipped part.
> (Any prior dated reconciliation note on this file stands - see the preserved line below.)
> _Prior status line, preserved: Status: READY TO IMPLEMENT_

**Priority:** P2
**Lane:** 12 Narrative/Quests (touches 4 UI/HUD for the intro overlay)
**Date:** 2026-06-13
**Owner:** Samantha
**WO# 451 is provisional** — reconcile against the numbering authority
(`MASTER_PIPELINES_BACKLOG_2026-06-06.md` + `CLI_LANES_WO_NUMBERS.md`) before
minting; filesystem max is 438, but the master doc is the authority. Slot into a lane.

---

## Owner directive

1. **SHORTEN the first-launch intro / cold-open** — it runs too long. Tighten to
   roughly **~3 s total** with faster line pacing (down from the current **~73 s,
   14-beat** cinematic).
2. **Soft, game-appropriate background images** behind the lines — painterly scenes
   of Elarion (the Heart-Tree, the valley/village, the misty Withering treeline),
   **muted / desaturated, low opacity**, crossfading behind the text. Full-bleed
   backdrop, not the current corner-dancing 260×200 icon panel.
3. **REMOVE the shooting stars** — they don't fit the grounded medieval-fable tone.
   Drop them from the intro backdrop AND stop the WeatherManager auto shooting-star
   loop in the village ambient.

---

## Investigation findings — the exact knobs

### A. Cold-open length & pacing
`Assets/_Modules/Onboarding/StoryIntroController.cs`

- The intro is **NOT** the 3-line `CanonStrings.ColdOpenLines()` cold open. That
  loader still exists (`CanonStrings.cs:92-101`, keys `intro.coldOpen.line1..3`),
  **but the controller no longer uses it.** What actually plays is the hardcoded
  **`ReactOpeningCinematic`** array — **14 `CinematicBeat`s**, defined at
  **`StoryIntroController.cs:369-389`**.
- Per-beat hold values (the `HoldSeconds` arg) total **~73 s**:
  `5.0, 4.8, 5.2, 5.4, 5.0, 5.4, 5.0, 4.8, 4.8, 5.8, 5.0, 4.5, 5.8, 5.4` ≈ **72.9 s**
  of holds, plus per-beat in/out fades (`0.45 + 0.35` label, `0.55 + 0.4` image) and
  two whole-overlay fades (`_fadeSeconds = 0.5f`, line 50) at the ends.
- The play loop is `Play()` at **`StoryIntroController.cs:115-206`**; the per-beat
  hold is consumed by **`WaitBeatOrSkip(beat.HoldSeconds, …)` at line 189** (helper at
  **417-427**).
- `_lineHoldSeconds = 1.6f` (line 47) is a **dead serialized field** — `WaitLineOrSkip`
  (311-322) is no longer called by the cinematic loop; only `WaitBeatOrSkip` is. Note
  this in the result so the dead field/method can be pruned.
- Gate: **`ShouldAutoPlay`** (lines 97-107) — plays only when `!GameState.Onboarded`
  (or `_forcePlay`). `OnboardingFlow.Finish()` flips `Onboarded=true`, so the intro is
  first-launch only. No change needed there.

**TARGET (~3 s total):** the owner wants ~3 s. 14 beats cannot read in 3 s, so
**collapse to 3 short beats** (one line each) at **~0.9 s hold** with snappy fades
(in `0.18`, out `0.15`), plus a short overlay in/out. That lands ≈ **3.0–3.3 s** end
to end and keeps a three-line cadence. Suggested replacement `ReactOpeningCinematic`
(class-agnostic, canon-locked — Elarion, Heart-Tree burned, the Withering, "the chord
is yours now"; do NOT reintroduce a named heart or the Lantern motif):

```
new CinematicBeat("A hundred winters ago, the Heart-Tree burned.", 0.9f, 1),
new CinematicBeat("Elarion kept its last song behind pale walls.", 0.9f, 2),
new CinematicBeat("Welcome home, Keeper. The chord is yours now.", 1.0f, 4, true),
```

Also reduce `_fadeSeconds` from `0.5f` → **`0.25f`** (line 50) so the bookend fades
don't eat the 3 s budget. Keep the Skip button and the `_pointerSkipGraceSeconds`
grace window (1.25f, line 67) intact — but note at 3 s the grace nearly covers the
whole clip; **drop the grace to ~0.6f** so an intentional early tap can still skip.

> If the owner prefers to keep more than 3 lines, the alternative is "faster pacing"
> only: set every beat hold to ~1.0–1.2 s and trim to 4–5 beats. Flag both options;
> default to the 3-beat version above unless the owner says otherwise.

### B. Background images
- Current behaviour: `_imagePanel` is a **small absolute 260×200 panel** (built at
  `StoryIntroController.cs:275-282`) that **dances corner-to-corner** per beat via
  the `ImagePositions` table (399-415). Images load from
  **`Resources.Load<Texture2D>($"Intro/intro-{beat.ImageId}")`** (line 175) — i.e.
  `Assets/Resources/Intro/intro-N.jpg`. **That folder does not exist yet** (verified:
  no `Assets/Resources/Intro/`), so today every beat falls back to no image.
- The backdrop scrim is `new Color(0.027f, 0.016f, 0.063f, 0.55f)` (line 251).

**TARGET — soft full-bleed crossfading scenes:**
- Make `_imagePanel` a **full-screen** element: `position:Absolute; left/top/right/
  bottom = 0; width/height = 100%`, `unityBackgroundScaleMode = ScaleMode.ScaleAndCrop`.
  Remove the per-beat `ImagePositions` repositioning (the corner dance) — delete/ignore
  the `ImagePositions` table + `ImagePositionFor` (399-415) and the `left/top` set at
  183-184.
- **Low opacity / muted:** cap the image-panel target alpha at **~0.30** (not 1.0) so
  it reads as a soft, desaturated wash behind the text. Tint it down with a multiply:
  set the panel `unityBackgroundImageTintColor` to a muted grey (~`0.62, 0.62, 0.66`)
  so even un-desaturated source art reads muted. (UI Toolkit can't desaturate at
  runtime cheaply — the source art SHOULD ship pre-desaturated; the tint is a safety
  net.) Update `FadeImage` (430-443) so the "in" target is the 0.30 cap, not 1.0.
- **Crossfade between beats:** instead of fade-out-then-fade-in on a single panel,
  keep the previous scene visible while the next fades in over it (a true crossfade).
  Simplest code path: keep ONE panel but lengthen the image cross so the new
  `backgroundImage` swaps with a short fade and there is no hard black gap between
  beats — i.e. don't fade the image fully to 0 between beats; hold it at ~0.30 and
  swap the texture under a brief `0.0 → 0.30` re-fade. Two stacked panels ping-ponged
  is cleaner if time allows; ONE panel held-at-0.30 with texture swap is acceptable.
- **Art delivery:** **CLI drops the image files into `Assets/Resources/Intro/`** — UI
  provides the painterly Elarion scenes separately (Heart-Tree, valley/village, misty
  Withering treeline), named `intro-1.jpg / intro-2.jpg / intro-4.jpg` to match the
  `ImageId`s in the new 3-beat array above. CLI creates the `Resources/Intro/` folder
  if absent. **If the art is not yet present, the intro must still run** — the existing
  `Resources.Load` null-guard (line 176) already no-ops a missing texture, so beats
  simply show text on the scrim. Keep that guard.
- **Code-built only — NO UXML** (PIPELINE_STATE §8: UXML does not render in builds).
  All of the above stays in the runtime `BuildOverlay()` / `Play()` path.

### C. Shooting stars — two sources, both must go
**Source 1 — the intro backdrop "shooting stars" = the Title comets.**
`Assets/_Modules/Onboarding/TitleStarfield.cs`

- The intro plays over the Title scene. `TitleController.cs:162-163` spawns a
  `TitleStarfield`, which builds **two layers in `Awake()` (lines 27-31)**:
  `BuildStars()` (33-103, gentle twinkle field) and **`BuildComets()` (105-215)** —
  the **~4 slow comet streaks** (`maxParticles = 5`, `emission.rateOverTime = 0.35f`,
  per-particle trails). **These comets ARE the "shooting stars" the owner means in the
  intro backdrop.**
- **TARGET:** remove the comet layer. Either delete the `BuildComets()` call at
  **line 30** (and the method 105-215), or guard it behind a `[SerializeField] bool
  _comets = false`. **Keep `BuildStars()`** — the gentle static twinkle field is fine
  and grounded; only the moving streaks read as shooting stars. (Owner: the twinkle
  field stays unless you say otherwise.)

**Source 2 — the village ambient auto shooting-star loop.**
`Assets/_Modules/Village/Vfx/WeatherManager.cs`

- The auto loop is **`ShootingStarLoop()` (294-305)**, started in **`Start()` at
  271-272** and in `SetWeatherQuality` (181-182) / `SetShootingStars` (205-206),
  tracked by **`_starRoutine` (line 153)**.
- It is **gated on `_starIntervalMax > 0f`** in all three start sites. The serialized
  knobs are **`_starIntervalMin = 8f` (line 102)** and **`_starIntervalMax = 25f`
  (line 105)** — and the tooltip on line 104 already documents **"0 = disabled —
  manual only."**
- **TARGET:** set **`_starIntervalMax` default to `0f`** (line 105). With max=0 the
  loop never starts in `Start()`, `SetWeatherQuality`, or `SetShootingStars` (all three
  already check `> 0f`), so the village stops auto-spawning shooting stars. No coroutine
  logic change needed — just the default. (If any scene/prefab serializes an override
  value, CLI must also update that serialized value, or it will mask the new default;
  check the WeatherManager GameObject in the village prefab/scene.)
- **Keep the manual `SpawnShootingStar(...)` API (191-196)?** — **CUT is not required;
  keep it.** It's a deliberate dramatic-moment hook (boss intro / wave clear), only ever
  fires on explicit call, and never runs on its own once max=0. Leave the public API,
  the pool, and the procedural builder in place. Flag in the result that it remains
  available but un-wired by default.

---

## Files to edit

| File | Change |
|---|---|
| `Assets/_Modules/Onboarding/StoryIntroController.cs` | Replace `ReactOpeningCinematic` (369-389) with the 3-beat ~3 s array; set `_fadeSeconds=0.25f` (50); drop `_pointerSkipGraceSeconds` to ~0.6f (67); make `_imagePanel` full-bleed (275-282), remove corner-dance (`ImagePositions`/`ImagePositionFor` 399-415 + 183-184); cap image alpha ~0.30 + muted tint in `FadeImage` (430-443); prune dead `_lineHoldSeconds` (47) + `WaitLineOrSkip` (311-322) |
| `Assets/_Modules/Onboarding/TitleStarfield.cs` | Remove the `BuildComets()` layer (call at 30; method 105-215) or gate it off by default; keep `BuildStars()` |
| `Assets/_Modules/Village/Vfx/WeatherManager.cs` | Set `_starIntervalMax` default to `0f` (105) to disable the auto shooting-star loop; keep `SpawnShootingStar` API |
| `Assets/Resources/Intro/` (new) | CLI creates the folder + drops UI-provided painterly Elarion scene art (`intro-1.jpg`, `intro-2.jpg`, `intro-4.jpg`); UI supplies the images separately |

**Note:** if the WeatherManager component is serialized in a prefab/scene with an
explicit `_starIntervalMax` override, update that serialized value too (or it masks the
new default). Identify the owning GameObject (boot/village) before claiming done.

---

## Acceptance criteria

- [ ] First-launch intro plays in **~3 s** end-to-end (≤ ~3.5 s incl. fades), three
      short lines, faster pacing — verified by summing beat holds + fades.
- [ ] Background shows a **soft, muted, low-opacity (~0.30) full-bleed** Elarion scene
      behind the text, crossfading between beats — no corner-dancing icon panel.
- [ ] Intro still runs cleanly when `Assets/Resources/Intro/*.jpg` is **absent**
      (text-on-scrim fallback; no error, no black flash).
- [ ] **No moving shooting-star / comet streaks** in the intro backdrop
      (`BuildComets` removed/disabled); the gentle twinkle field may remain.
- [ ] Village ambient produces **no automatic shooting stars** (`_starIntervalMax=0`
      default; loop never starts) — confirmed against the serialized WeatherManager too.
- [ ] `SpawnShootingStar(...)` manual API still compiles and is callable (kept).
- [ ] Skip button + early-tap skip still work at the new short length.
- [ ] `ShouldAutoPlay` gate unchanged — intro is first-launch only (`!Onboarded`).
- [ ] Brace-balance check passes on all three `.cs` files; CLI build-verifies.

## Do NOT touch

- Do **not** hand-edit any `.unity` scene file (§3) — if WeatherManager's interval is
  serialized in a scene, change it through the rebuild/inspector path, not by editing
  the YAML.
- Do **not** reintroduce a named heart or the retired "Lantern" motif in the new beats
  (STORYLINE.md canon; Village = **Elarion**, never "Avalon").
- Do **not** delete `CanonStrings.ColdOpenLines()` / the `intro.coldOpen.*` en.json
  keys — out of scope (controller doesn't use them today; leave for a later cleanup).
- Do **not** touch `BuildStars()`, the rain system, or any other WeatherManager API.
- Do **not** introduce UXML for the intro — code-built UI only (PIPELINE_STATE §8).
- Do **not** change the `Onboarded` gate / `OnboardingFlow` persistence path.
- Do **not** add `System.Reflection` to any bridge script (§10).
```

> **AUDIT 2026-08-21 (agent fleet, read-only):** OPEN — STILL VALID. Evidence: `WeatherManager.cs:105, StoryIntroController.cs:77` — stars done; 3s + full-bleed remain. Status left at READY deliberately: this work is real and unbuilt. Verified against HEAD 2f0b97bb5, not against the ticket's own claims.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict. ⚠ NOTE FOR ANYONE REOPENING: the 2026-08-21 read-only audit had classified this one OPEN - STILL VALID, with the evidence cited above. The owner's review supersedes that call (owner statements are ground truth). The audit line is left in place deliberately, so if this work turns out to be needed, the evidence for it is still here rather than erased.
