> ⚠ **STALE — predates the 2026-06-22 single-Knight pivot.** Treat its Blink-hero / party-of-4 / tower-defense-pillar framing as SUPERSEDED (hero = single Tripo "Grom", Blink rig junked, base-defense V2-gated); some architecture/monetization content may still hold. Live reality: `CANON_GROUND_TRUTH_2026-06-26.md` + `docs/COMBAT_PIVOT_NORTHSTAR.md`.

# Onboarding — `DeNelle.Onboarding`

First-time user experience: Title → Hero Select → Pet Select → Story Intro → Village.

## Files

- `OnboardingFlow` — flow orchestrator
- `TitleController`, `TitleStarfield`, `SplashLoading` — title screen
- `HeroSelectController` + `HeroCatalog` — hero pick (cards: WO-223–226)
- `PetSelectController` + `IntroPetCatalog` — pet pick
- `StoryIntroController` — opening narrative beat
- `CanonStrings` — canonical naming (Elarion etc.)

Scenes: `Title.unity`, `HeroSelect.unity`, `PetSelect.unity`. FTUE wiring: WO-133.

> Maintenance: update this README when files are added/removed.
