**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

> ⚠ **UNRESOLVED NUMBER COLLISION — WO-253 is claimed by more than one file and OWNERSHIP IS NOT DECIDED.**
> Co-claimants: `WORK_ORDER_253_split_village_scene_builder.md`, `WORK_ORDER_253_tutorial_speech_bubble_overlay.md`
> Both files were added in the SAME commit (first-on-disk is a dead tie) and neither is cited by any other doc, RESULT file, or commit message — there is no evidence on either side.
> Flagged (not resolved) by the 2026-08-16 Sunday board-grooming pass — a wrong ownership call is worse
> than a flagged unknown, so this needs an **owner ruling**. Nothing renumbered or deleted. Until it is
> ruled, cite this WO by FILENAME, never by bare number.

# WORK ORDER 253 — Tutorial Speech-Bubble Overlay (Visual Presentation)
**Status: READY TO IMPLEMENT**
**WO:** 253 | **Lane:** HUD (parallel safe)
**Depends on:** WO-133 (OnboardingFlow wiring — must land first)
**Closes:** DEF-153

---
## Context

WO-133 wires `OnboardingFlow` into the village scene and notes that the overlay
**must be code-built** (UXML does not render in builds — PIPELINE_STATE §8).
This WO specifies the visual presentation of that code-built overlay per the
creative direction in DEF-153.

---
## Spec

Build the tutorial overlay as a **centered speech bubble** using Unity's
code-built UI (IMGUI or runtime-created Canvas, matching existing HUD pattern).

### Visual requirements

1. **Bubble:** rounded-rect panel, semi-transparent dark background (RGBA 20,20,30,0.85),
   8px corner radius, 2px gold border (#C8A84E). Anchored to screen center
   (anchor min/max = 0.5, pivot = 0.5).

2. **Text:** white, 24sp minimum (auto-scale up on tablets). TextMeshPro or
   built-in Text with `BestFit` between 20–32sp. Center-aligned, max width = 80%
   of screen width so it wraps cleanly in portrait.

3. **Tap-to-continue:** small "Tap to continue" label below the main text,
   14sp, 50% alpha, pulsing opacity (0.4–1.0 over 1.2s sine).

4. **Responsive layout:**
   - Landscape: bubble max-width = 50% of screen, vertically centered
   - Portrait: bubble max-width = 85% of screen, positioned at 40% from top
     (above d-pad / action buttons)
   - Recalculate on `Screen.orientation` change or `RectTransform` rebuild

5. **Sort order:** Canvas sortOrder must be ABOVE VillageHud (sortOrder 10+)
   and ABOVE NPC dialogue (see WO-252). Use sortOrder = 100.

6. **Dim background:** full-screen overlay behind the bubble at RGBA(0,0,0,0.4)
   to focus attention. Raycast target = true to block taps on gameplay behind it.

### Files to edit

- `Assets/_Modules/Onboarding/OnboardingFlow.cs` — replace UXML overlay
  creation with code-built Canvas + speech bubble panel
- Create helper: `Assets/_Modules/Onboarding/TutorialBubbleUI.cs` — owns the
  bubble GameObject, text update, responsive layout, tap-to-continue pulse

### What NOT to touch

- `VillageSceneBuilder.cs` (WO-133 handles placement)
- `VillageHudController.cs`
- Any `.unity` scene files

---
## Acceptance criteria

- [ ] Tutorial speech bubble is visible and centered on first load into Village scene
- [ ] Bubble text is readable at 375px portrait width (iPhone SE) — no clipping or overflow
- [ ] Bubble text is readable at 812px landscape width — no excessive stretching
- [ ] Rotating device between landscape ↔ portrait repositions bubble correctly within 1 frame
- [ ] "Tap to continue" label pulses and advances to next tutorial beat on tap
- [ ] Bubble canvas sortOrder > VillageHud sortOrder — tutorial renders on top of all HUD elements
- [ ] Background dim blocks gameplay input while tutorial bubble is active
- [ ] Skipping tutorial (tap skip button) dismisses bubble and dim immediately
- [ ] Confirmed in WebGL build — bubble renders (no UXML dependency)
- [ ] `OnboardingFlow` still functions identically (same beat progression, same events fired)

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
