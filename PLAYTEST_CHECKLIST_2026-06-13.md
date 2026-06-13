# Playtest Checklist — 2026-06-13 build

**Build:** Windows player (`build-windows.ps1`, fresh — `Builds/Windows` deleted first).
**Rule:** nothing is "done" until it passes here. Mark `PASS` / `FAIL (note)`. A FAIL bounces
back to flow-first triage, not a patch.

| # | What to test | What "pass" looks like | Fix that addresses it | Result |
|---|---|---|---|---|
| 1 | **Seam crossing** — South→OuterWorld + W/N/E outpost connectors | Walk into the seam: a **"Press F: Travel to …"** prompt appears; nothing crosses until you press F. **No accidental fade-to-black** just from walking near a gate. | Confirm-to-cross (`SceneTransitionTrigger.requireConfirm=true`); seam radius 12m | |
| 2 | **Dev tools after Yarn** | After ANY companion/vendor dialogue closes, the corner DEV panel + Settings→DevTools are **still clickable**. | `OnboardingPanelGuard` neutralises the onboarding UITK panel in gameplay scenes | |
| 3 | **Wave start timing** | First wave begins **~45s** after entering the castle (not 5 min). | `waves.json` wave-1 `countdownSeconds` 300→45 | |
| 4 | **Vendor categories + economy** | Each vendor lists **only its type** (armorer=armor, smith=weapons, market=potions); buying **deducts** the wallet and the **HUD number drops visibly**. | `VendorStockContract` + `HeartHudBridge` economy push | |
| 5 | **Hero faces target** | Hero **turns to face** the enemy on attack/cast (incl. AoE/Meteor at range). | `HeroAbilities.FaceCastTarget` via `ResolveBlastCentre` | |
| 6 | **Tree of Life grounded** | Tree sits **on the ground**, plaza walkable around it (no giant invisible blocker). | Baked trunk capsule (Heart collider-scale fix) | |
| 7 | **Knight no extreme-range snipe** | Knight melee (Strike/Snare) only connects **in reach** — no cross-map hits. | `HeroAbilities` InReach gate (WO-398) | |
| 8 | **Enemy variety** | A wave is **mixed** (goblins/orcs/skeletons/etc.), not all one type. | enemy roster / spawn variety | |
| 9 | **Build mode / tower placement** | Place an **archer tower** — it shows the **tower model**, NOT a lumber pile. Note any tower that still looks like a wood stack. | #22 catalog `Tower_Medieval_Wood`→`Tower_Castle_Round` | |
| 10 | **Through-wall targeting** | Hero **cannot** target/hit an enemy through a wall (LoS blocked). | `HeroTargetIndicator.HasLoS` linecast on Structure layer (WO-449) | |
| 11 | **Dialogue options clean** | Yarn option buttons are **readable, no overlap/ghosting**. | dialogue panel | |
| 12 | **General feel** | No **invisible walls** near south gate (or note where you snag); **return-point** lands you back where you left after a battle; flag **any new fade/black** screens. | seam nav lane + `SceneRouter` return-point | |

## Known-open going in (expected, not regressions)
- **South-gate ~34m nav reach:** the hero is a NavMeshAgent and stops at the navmesh edge; if
  you can't physically reach the south seam trigger, that's the open nav-bake item (the prompt
  needs walkable surface under it). Note exactly where you stop.
- **Archer-tower art:** the 4 `Tower_Tribal_T` prefabs you liked are outside `Resources/`
  (gitignored pack) so not runtime-loadable yet — the build uses `Tower_Castle_Round` as the
  stand-in. Moving the Tribal set into `Resources/Structures/` is a follow-up.
- **Audio:** music/SFX may be placeholder procedural — content pass is separate.

## After the playtest
- **UI styling pass** (your note: "messy, not cohesive") — the next focus. Specs already queued:
  WO-437 (combat HUD restyle from the Tech hud pack) + WO-438 (global rollout) via `RpgUiCatalog`.
