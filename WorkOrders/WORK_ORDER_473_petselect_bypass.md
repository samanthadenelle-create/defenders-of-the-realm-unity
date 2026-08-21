**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

# WORK_ORDER_473 — Bypass the PetSelect onboarding screen

**Status: READY TO IMPLEMENT** (held until editor closed) · F8 ticket (owner): "PetSelect should be bypassed."
**Type:** flow change (code-only, no scene rebuild/bake) · **Silo:** Onboarding/Core routing

## Key de-risk finding (RCA agent, code-confirmed)
Since the **PET-ACQUISITION REWORK (2026-06-13)**, `PetSelectController` **persists NOTHING** — the card tap is a
non-binding preview; `RouteToVillage()` is just `SceneRouter.GoCastle()` (no `StarterPetId` write, no save, no
spawn). Real pet bonding moved to the **Echo Hollow** in-town (`DialogueCommandBridge`→`PetAcquisitionService`→
`PetDeployer`). **So bypassing PetSelect loses nothing.** Clean cut.

## Live boot flow (verified, NOT the stale doc header)
`Title (hero-pick on Title screen) → RouteToPetSelect() → GoPetSelect() → PetSelect → GoCastle() → MainCastle_Hall`.
Only live entry into PetSelect = `TitleController.RouteToPetSelect()` (TitleController.cs:1328). Legacy/secondary =
`HeroSelectController.OnDiveVillageClicked()` (HeroSelectController.cs:831). `AutoPilotDriver` already boots straight
to MainCastle_Hall (unaffected).

## Implementation (flag-gated redirect — reversible, headless-greppable)
1. **FeatureFlags**: add `BypassPetSelect` (default **true**). Use the existing flags registry (grep `FeatureFlags`) — do NOT greenfield.
2. **TitleController.RouteToPetSelect() (:1328)**: keep `svc.ChooseHero(_selectedHero)`; when `BypassPetSelect`, call `SceneRouter.GoCastle()` instead of `GoPetSelect()`. Update the log + a `FlowTrace.Step("Onboarding","RouteToPetSelect: bypass→GoCastle")`.
3. **HeroSelectController.OnDiveVillageClicked() (:831)**: same flag check → `GoCastle()`.
4. **PetSelectController.OnEnable()**: as the FIRST check, `if (FeatureFlags.BypassPetSelect) { SceneRouter.GoCastle(); return; }` (belt-and-braces; matches the existing skip branch at :126).
5. Leave `SceneRouter.GoPetSelect()` + the scene + const INTACT (flag-off path; no Build Settings churn).

## Preserve / NOT touch
- Preserve hero-pick persistence (`svc.ChooseHero`); the Echo Hollow pet flow (untouched).
- Do NOT delete the PetSelect scene/UXML/controller/const; no `.unity` hand-edit (§3); no bake; no AutoPilotDriver change.

## Acceptance
- New game: after hero pick → straight to MainCastle_Hall, PetSelect never shown.
- Flag false restores old behavior. Hero choice survives save round-trip. Pet (Echo Hollow) flow unchanged.
- Headless trace proves PetSelect scene not entered (no `PetSelectController` Enter line). Brace gate + COMPILE_GATE_OK clean.

## INSTRUMENT-FIRST (§12)
Cite the captured `[Flow:Onboarding]` line showing Title→Castle with PetSelect skipped — don't claim done from code-read alone.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
