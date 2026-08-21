**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-13
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-13) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 705 — Duplicate OnboardingPanelSettings UIDocument survives the guard (fleet-captured)

**Status: READY TO IMPLEMENT — RCA-first (§12).** Minted 2026-07-13 from the post-WO-703/704 fleet
run (owner approved ticketing the signal same day).
**Lane:** Onboarding/UI. **Type:** EXISTING (known bug family — the guard already exists and is
being evaded, do NOT greenfield).

## Captured proof (§12 — the fleet run, exe 2026-07-13 12:02)

```
[Flow:BotUI] duplicate UIDocument: 2 ENABLED documents share PanelSettings 'OnboardingPanelSettings'
```
Fired ×3 across the harvested runs (seeds 1000–1003 window). Zero softlocks/talk violations in the
same runs — the duplicate is live but its input-eating consequence didn't trip this pass.

**NEW VARIANT (fleet 2026-07-13 evening, seeds 7000+, ×7 + ×3):** same class also fires for a SECOND
PanelSettings asset — `[Flow:BotUI] duplicate UIDocument: 2 ENABLED documents share PanelSettings
'DevRuntimePanelSettings' in scene 'Title' (cause=arm) — docs=[[DEV] QA Dev Console,JupiterSwapHost]`
(runs 9/11, seeds 7009/7011) — plus the known OnboardingPanelSettings pair now naming
`docs=[JupiterSwapHost,SplashLoading]`. The RCA must cover both assets; `JupiterSwapHost` appears in
every capture and is the common suspect.

## Known context (read BEFORE touching anything)

- `Assets/_Modules/Onboarding/OnboardingPanelGuard.cs` exists precisely for this: several runtime
  UIDocuments share the ONE `OnboardingPanelSettings` asset; a stale enabled doc's PanelRaycaster
  tops the pick stack and eats gameplay input (the historical "dead after Yarn" / DEF-211 family).
- `TitleController.cs:132` and `StoryIntroController.cs:95` both carry scars from the same bug.
- `AutoPilotLogGuards.cs` is the fleet detector that emitted the captured line.

## The question the RCA must answer (from data, not reading)

The guard exists, yet the fleet still sees 2 ENABLED docs. Instrument to determine which:
1. A code path creates/enables an OnboardingPanelSettings doc AFTER the guard's sweep runs
   (ordering hole), or
2. The guard's scene/lifecycle gate skips the bot-driven path entirely, or
3. The detector fires in a window where the duplicate is expected and about to be guarded
   (benign timing — then the DETECTOR needs the fix, not the guard).

Add `[Flow:BotUI]`/`[Flow:Onboarding]` step-in/step-out traces on guard sweep + every
OnboardingPanelSettings doc enable, re-run the fleet, cite the ordering from the trace.

## Acceptance
- [ ] Root cause cited from a captured trace line (which of 1/2/3, with the ordering shown).
- [ ] Post-fix fleet run: zero `duplicate UIDocument` lines (or, if case 3, the detector no longer
      false-fires and the guard's coverage is proven by the same trace).
- [ ] No regression in Title → StoryIntro → HeroSelect flow (fleet boot phase green).
- [ ] COMPILE_GATE_OK + DataRegression baseline (8 known reds, zero new).

## What NOT to touch
The UXML/UIDocument ban for gameplay UI stands — onboarding's sanctioned UIDocument surfaces are
the exception; do not migrate them to uGUI under this ticket. No PanelSettings asset edits without
the RCA naming them.

*Cross-refs:* fleet run 2026-07-13 (post-WO-703/704) · OnboardingPanelGuard.cs · AutoPilotLogGuards.cs ·
DEF-211 scar comments in TitleController/StoryIntroController.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
