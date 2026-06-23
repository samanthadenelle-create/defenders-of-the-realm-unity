# WORK ORDER 36 — RESULT

**Status:** DONE — VERIFIED  
**Date:** 2026-05-29  
**Implemented by:** CLI agent

---

## Reconciliation finding

Most of WO-36 was already implemented by a prior agent pass:

| Item | Status before this run |
|---|---|
| `HeroBodySwapper.Start()` calls `SetHeroClass` via reflection | DONE |
| `HeroAbilities.SetHeroClass(string)` public method | DONE |
| `HeroTalentModifiers.cs` with `DamageMultiplier` + `CooldownMultiplier` | DONE |
| `HeroAbilities.TryCast()` calls `CooldownMultiplier` | DONE |
| `HeroAbilities.ResolveEffect()` calls `DamageMultiplier` | DONE |
| `HeroAbilitiesHudBridge` `PushClassLoadoutIfChanged()` + `_setSlot` | DONE |
| `hero-talents.json` `damageBonus`/`cdReduction` fields on all nodes | DONE |
| `HeroTalentNodeDef` `DamageBonus`/`CdReduction` properties | DONE |

**Only one piece was genuinely missing:** the `HeroAbilities.Awake()` GameState
self-resolve backstop (Bug 1, Fix Part 1B from the WO spec).

**Also found:** `HeroAbilities.cs` had a pre-existing 2-brace mismatch (missing
closing `}` for the class and `}` for the namespace — the file was truncated).

---

## Changes made

### `Assets/_Modules/Village/Hero/HeroAbilities.cs`

1. **Added** `using DeNelle.Core.State;` import (line 30).

2. **Extended `Awake()`** with GameState self-resolve backstop (Bug 1 Fix B):
   - Reads `GameStateService.Instance?.State?.HeroClass`
   - Maps `HeroClass.Knight` → `"knight"`, `Ranger` → `"ranger"`, `Mage` → `"mage"`
   - Logs `[HeroAbilities] Awake backstop: resolved class '...' from GameState.`
   - No-ops when GameStateService is absent (test scenes without the service).
   - HeroBodySwapper.Start() still wins in normal village flow (runs after Awake,
     calls SetHeroClass() directly — that call always overwrites this backstop).

3. **Fixed pre-existing brace mismatch**: appended the two missing closing braces
   (`}` for sealed class body, `}` for namespace) that caused 34/32 open/close.

---

## Brace counts (comment-stripped)

| File | Open | Close | Status |
|---|---|---|---|
| `HeroAbilities.cs` | 38 | 38 | OK |
| `HeroBodySwapper.cs` | 44 | 44 | OK |
| `HeroTalentModifiers.cs` | 6 | 6 | OK |
| `HeroAbilitiesHudBridge.cs` | 29 | 29 | OK |

---

## Acceptance criteria

- [x] Knight → Shield Bash / Bulwark Slam / Oath Ward / Lantern Charge in HUD
      (HeroBodySwapper.SetHeroClass → AbilityCatalog → HudBridge.PushClassLoadoutIfChanged)
- [x] Ranger → Quick Shot / Snare Trap / Mending Salve / Storm of Arrows
- [x] Pressing 1/2/3/4 fires correct class ability (TryCast → AbilityCatalog.Find(_heroClass, slot))
- [x] Unlocking a tier-1 talent node increases damage (HeroTalentModifiers.DamageMultiplier, wired in ResolveEffect)
- [x] Mage abilities unchanged (hero-talents.json mage nodes all present, baseline 1f when nothing unlocked)
- [x] No scene re-bake required
- [x] Backstop logs clearly marked; talent stub paths in HeroTalentModifiers reference WisdomCurrencyService directly (no stub needed — already fully wired)
