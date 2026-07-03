# Onboarding Flow Audit — 2026-07-03 (code-verified, read-only)

## HEADLINE: two tutorials exist; the flag decides — and TODAY the wrong one runs.
`ff.tutorialv2` defaults OFF (FeatureFlags.cs:348). A real new player gets the LEGACY
TutorialDirector "fast path": 3 spoken bubble lines, then free play (~30-45s total from
app-open, fast player). The owner-designed 7-step Tutorial V2 (tutorial-steps.json) is
FULLY BUILT but dark behind the flag.

## The flow today (Start New): splash static card 1.5s → Title (Continue/Start New/Play
Intro) → HeroSelect (KnightOnly) → PetSelect BYPASSED → castle load → 3 legacy bubble
lines → free play. TOTAL ~30-45s fast / ~1.5-2min reading.
Play Intro adds the 14-beat Stone Choir cinematic (~35-45s, skippable).

## The flow when V2 flips ON (7 steps, ~4.5min fast / ~8-9min reading):
1 move_to_sylas (proximity 6m) → 2 meet_sylas (dialogue) → 3 first_tower (UNSKIPPABLE,
owner directive) → 4 town_wave (UNSKIPPABLE, combat mode 1) → 5 world_encounter
(UNSKIPPABLE, combat mode 2 — "out there is DIFFERENT") → 6 return_home → 7 freedom
(only V2 caller of FinishOnboarding → wave loop arms). +4 contextual one-shots post-tutorial.
120s per-step watchdog (STEP-STUCK self-report, never hard-blocks).

## Coverage vs owner goals:
- First-tower guided build: PARTIAL — step authored + "this first one is on me" dialogue,
  but the prepaidTower GRANT IS A NO-OP (TutorialFlow.cs:221-223, WO-T3 undone): player
  must afford the tower with real economy.
- Town wave taught: PARTIAL — step + dialogue exist; interpreter NEVER calls
  TutorialWaveSpawner.SpawnAt (no scripted gentle wave); wave auto-arm is gated off until
  step 7, so completion depends entirely on the player pressing Start Wave. Fragile.
- World combat taught separately: PARTIAL — deliberate contrast arc authored; no staged
  fixed-anchor rep spawn — player must FIND an encounter or the step strands to watchdog.
- Harvest: contextual-only by design, AND its trigger (echo.born:2) has no source event —
  can never fire. Same for gear_added:first + skillpoint.earned:first (3 of 4 contextual
  triggers dead; only can_afford_upgrade is wired).
- NPC name/guild/portrait: COVERED (WO-583 speakers block; Sylas portrait FIXED —
  HeroPortraits/Sylas.jpg present, silhouette fallback, never the tan disc).
- Duplicate-UIDocument Title input-eater: FIXED by today's WO-C conversions.

## Top 3 improvements (ranked):
1. Flip ff.tutorialv2 ON — but only AFTER #2 (today it teaches nothing she asked for).
2. Make steps 3-5 self-driving: implement the prepaidTower grant (WO-T3), call
   TutorialWaveSpawner.SpawnAt for town_wave, stage a fixed-anchor rep for
   world_encounter. These are the unskippable steps — a strand is a session-ender.
3. Wire the 3 dead contextual triggers (one adapter line each) or cut their hints.

(Per-beat timing table, signal wiring map, and file:line evidence in the session transcript
+ PM board; steps live in Assets/Resources/Data/Canonical/tutorial/tutorial-steps.json.)
