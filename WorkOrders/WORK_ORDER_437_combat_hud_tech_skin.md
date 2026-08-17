> ⚠ **UNRESOLVED NUMBER COLLISION — WO-437 is claimed by more than one file and OWNERSHIP IS NOT DECIDED.**
> Co-claimants: `WORK_ORDER_437_combat_hud_tech_skin.md` (06-13, first-on-disk), `WORK_ORDER_437_input_state_gate.md` (06-17, marked DONE), `WORK_ORDER_437_bar_overflow_rectmask.md` (07-04)
> **This is one of a four-number group (WO-437 / 438 / 439 / 440) that collided the same way.** The June
> files are **first-on-disk**; the 2026-07-04 files are the ones **git history says shipped** — commit
> `0b0e0915c` reads *"UI-100% wave 1 — shared-kit parchment fix, WO-437/438/439/440, per-screen match"*,
> which names the 07-04 UI batch, and `aa931577b` separately records *"WO-437/438 landed"*. First-on-disk
> and referenced-by-commit point at DIFFERENT files, so the project rule resolves to neither.
> Flagged (not resolved) by the 2026-08-16 Sunday board-grooming pass — needs an **owner ruling**, ideally
> one ruling for all four at once. Nothing renumbered or deleted. Cite by FILENAME, never by bare number.

# WORK ORDER 437 — Combat HUD full restyle from "Tech hud elements" pack

**Status: READY TO IMPLEMENT** · Lane 4 UI/HUD · P1 · Owner directive 2026-06-12
**Notion:** WO-437 row in Work Orders DB. Felt change — **push only after owner retest.**

## Owner directive
Apply the "Tech hud elements" pack (Assets/Tech hud elements) to ALL combat HUD styling.
Global rollout to every other screen = WO-438 (depends on this). The pack is the canonical
skin source for the WO-405 ElarionUiKit chain — screens consume it via RpgUiCatalog only.

## Architecture constraints (BINDING)
- ONE sprite-source owner: extend the existing `RpgUiCatalog` (`Assets/_Modules/Core/UI/RpgUiCatalog.cs`)
  + the `Defenders/Art/Import RPG UI Pack` importer (commit 1c87a4e seam). Do NOT create a
  second catalog/atlas path. All sprites ship via `Resources/RpgUi/<role>/` (committed copies).
- Presentation layer only — zero gameplay-object edits. Code-built uGUI only (no UXML).
- Do NOT touch: `HeroAbilitiesHudBridge` logic (fixes 7817294/1f6aad0), `WardTetherService`,
  `CompanionDialoguePresenter` (WO-391 fix in flight), ShopPanel (WO-431 in flight).

## Step 0 — commit the pack source
`Assets/Tech hud elements/` is **untracked** (`??` in git status; NOT gitignored). Sole committer:
stage by explicit path and commit (private repo — license forbids redistribution, not VCS).
Without this, fresh clones lose the importer source.

## Importer + catalog extension (new roles)
| New role | Source sprites | Used for |
|---|---|---|
| `orb` | `Sprites/Badges/Badage 1–6` (`Badgae 1`) | skill-bar slot medallions (ability orbs) |
| `plate` | `Sprites/Profile tabs/P1–P6` (bg+fill+fill-1) | P1 hero plate · P2–P4 party · P5 target frame · P6 boss |
| `banner` | `Sprites/Score tabs/Tab 1–8` + `Level badage 1–3` | wave banner, score, level chip |
| `heal` | `Sprites/Healing Tabs/H1–H15` + `Magic healing/D1–D6` | heal/buff buttons, status-effect chips |
| `shield` | `Sprites/GreenUielements/Shield/*` + `Sprites/Skull/Skull.png` | armor/block indicator, boss/death marker |
| `bars` (extend) | `Loading 1–4, 8` bg/fill + `GreenUielements/Loading bar` | cast bar, XP bar, boss-HP fill variants |
| `button` (extend) | full `Play buttons/*` + `GreenUielements/Buttons/*` | combat CTAs (flee/retry/claim) |
Keep existing keys (bar_frame_red etc.) stable — additive only. Add consts for each new key.
Import `Animation/PopUp/PopUp.anim+.controller` → reward/level-up pop on kill/wave-clear.

## Files to edit
1. `Assets/_Modules/Core/UI/RpgUiCatalog.cs` — new role consts + named keys (additive).
2. `Assets/Editor/` RPG UI Pack importer — extend copy manifest to the table above.
3. `Assets/_Modules/HUD/VillageHudController.cs` — BuildSkillBar: orb-medallion slot frames
   (icon inside, radial cooldown overlay on orb); hero plate → `plate/P1` (dual fill = HP/mana);
   party plates → P2–P4; wave banner → `banner/Tab` sprite; XP bar → Loading slim variant;
   kill/wave pop → PopUp anim.
4. `FloatingHealthBar.cs` (Village) — enemy overhead bars → pack bar frame/fill (red), boss
   variant with Skull badge cap. Keep WO-302 scale fixes intact.
5. `Assets/_Modules/BattleATB/BattleHudUgui.cs` — same skin family for ATB battle scene.

## Acceptance
- All combat chrome (skill bar, hero/party/target plates, enemy+boss bars, wave banner,
  XP bar, cast/cooldown, pops) renders from pack sprites — zero procedural/glyph fallbacks
  visible in a combat session (castle, OuterWorld, Village2 raid, ATB).
- `UiSpriteRefValidator.Run` batchmode clean; brace gate on every .cs touched.
- Existing fallback behavior preserved for null lookups (never hard-fail).
- Owner felt retest passes → then push. Before/after screenshots attached to Notion row.
