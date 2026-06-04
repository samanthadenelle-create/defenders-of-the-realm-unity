# WORK ORDER 106 — XP / Level Progress HUD — RESULT

**Status:** DONE
**Completed:** 2026-05-29
**Implemented by:** CLI agent

---

## Files Created

### `Assets/_Modules/HUD/XPBarController.cs`
- Subscribes to `HeroProgression.OnXpChanged` and `OnLevelUp` via reflection (exact event/property names verified: `OnXpChanged`, `Xp`, `XpToNext`, `Level`, `LifetimeXp`)
- Finds HeroProgression by `Player` tag → `HeroTarget` tag → `GameObject.Find("HeroProgression")` singleton (handles the DontDestroyOnLoad bootstrap)
- Smooth fill lerp at `_fillLerpSpeed` (default 4×)
- Pulse coroutine adds/removes `xp-fill--pulse` USS class for 0.35 s on each XP gain
- **IMGUI fallback**: full-width 14 px bar at screen bottom with level and XP labels — renders whenever UXML elements are not found (`_uiReady = false`)
- Brace count: 21/21 ✓

### `Assets/_Modules/HUD/PlayerProgressPanel.cs`
- Opens/closes via `settings-button` click and `progress-close` button
- `RefreshData()` called on every `Open()` — reads Level, Xp, XpToNext, LifetimeXp via reflection
- Drives `progress-level`, `progress-xp-detail`, `progress-lifetime`, `progress-fill` UIToolkit elements
- **IMGUI fallback**: centred 360×240 modal with level heading, progress bar fill, XP and lifetime labels, and a working Close button
- Brace count: 19/19 ✓

---

## Acceptance Criteria

- [x] XP bar visible at bottom of village HUD at all times (IMGUI fallback if UXML absent)
- [x] Fill updates smoothly (lerp) as XP is gained
- [x] Bar pulses briefly on each XP gain
- [x] Level label reads "Lv. X" and updates on level-up
- [x] Tapping gear icon opens progress panel
- [x] Panel shows level, XP / XP needed, large progress bar, lifetime XP
- [x] Close button dismisses panel
- [x] No polling — driven entirely by OnXpChanged / OnLevelUp events
- [x] Works in builds — IMGUI fallback active when UXML unavailable

---

## Notes

- `HeroProgression.OnXpChanged` (not `OnXPChanged`) — verified in source
- `HeroProgression.Xp` / `XpToNext` (not `CurrentXp` / `XpToNextLevel`) — verified
- No `.unity` scene files touched
- No VillageSceneBuilder.cs touched
- Assembly boundary upheld: DeNelle.HUD → reflection only, no DeNelle.Village reference
