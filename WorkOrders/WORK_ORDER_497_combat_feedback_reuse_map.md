# WORK_ORDER_497 — COMBAT FEEDBACK REUSE MAP (don't greenfield — it's built)

**Status:** REFERENCE / BINDING before any WO-493/494/496 work · inventory agent, 2026-06-23
**Owner directive:** "most of that already exists and is tested if you look back." CONFIRMED. The battle-feel
layer is almost entirely BUILT + TESTED and — because the arena reuses the existing combat stack (Enemy.cs,
HeroHealth, PlayerAttackController, HeroAbilities) — **nearly all of it already fires in the BattleArena
passively.** REUSE/TUNE/AUTHOR-DATA; do NOT rebuild. Read this BEFORE touching WO-493/494/496.

## Already EXISTS + fires in the arena (TUNE only, don't build)
- **Enemy hit-flash (red):** `EnemyHitReaction.cs` (auto via Enemy.cs:1338). **Hero hit-flash:** `HeroHitReaction.cs`.
- **Damage numbers:** `DamageNumberSpawner.cs` (Enemy.cs:1279).
- **Camera shake:** `SmartMobileCamera.Shake` via `CameraShakeBridge.Shake` (Tower.cs:952), fired Enemy.cs:1342/1579 + `CombatFeedbackManager.cs:254/297` (heavy-hit ≥20dmg + death — reserved-juice already partly honored).
- **Hit-stop + kill slo-mo:** `CombatFeedbackManager.cs:300/318` (rate-capped) via Enemy.cs:1350 Hit / :1580 Kill. (`HitStopManager.cs` mostly orphaned.)
- **Directional flinch + per-type hit VFX:** Enemy.cs:1300-1335 (DEF-46).
- **Combo/kill-streak:** `KillComboTracker.cs` + CombatFeedbackManager.
- **Perfect-hit / parry / riposte:** `PlayerAttackController.cs` (1.75x perfect, 3.0x riposte, 0.25s parry).
- **Attack-weight tempo + swing trail** (WO-217/219) + **attack-timing chain** `AttackTimingBonus.cs` ("CHAIN x2!").
- **Hero ability kit Q/W/E/R + VFX + HUD:** `HeroAbilities.cs`, `AbilityCatalog.cs` (JSON `abilities.json`), `AbilityVfxKit.cs`, `HeroAbilityInput.cs`, `HeroAbilitiesHudBridge.cs`.
- **Elite/boss aura + VFX:** `EliteVFXController.cs`. **Audio barks:** `AbilityAudioBridge`, `EnemyCombatAudio`, `GameSfx`.
- **Targeting / lock-on:** `HeroTargetIndicator.cs` — auto-locks NEAREST, Tab/right-shoulder to CYCLE, reticle red, feeds aim, auto-clears. (Gap: bind TAP/RIGHT-CLICK to the cycle — owner ask.)
- **Enemy injured stance (<30% HP):** Enemy.cs:715 -> `ActorAnimator.SetInjured`, flag `EnemyInjuredStance` — NOW shows (the new OrcHumanoid controller has the Injured param/clips, WO-491).
- **`CombatFeedbackManager.Hit(pos,dmg)` IS the unified ImpactEvent spine** (fans out shake+hitstop+combo). Reuse/rename — do NOT author a new event.

## GENUINELY NET-NEW (the only real building)
1. **Death-camera hold (~10s linger + push-in), enemy battle-winning kill AND hero death** — NO DeathCamera/killcam in repo; `BattleArena.Resolve()` (BattleArena.cs:571/575) fires INSTANTLY on last death -> cuts the death anim. Make Resolve/teardown WAIT for a camera-hold. **The owner's "death cycle only a few seconds."** (Per-enemy `Enemy.DeathHoldSeconds` bumped 1.6->3.5 so the body anim plays; the arena camera-hold is the separate net-new piece.)
2. **HERO injured stance** — enemy side exists; hero has NO low-HP locomotion swap / screen-edge vignette / heartbeat. Add the hero half.
3. **Post-fight count-up reward screen** — banner exists (`BattleArena.ShowResult`); the escalating count-up/loot-arc/stars screen (496 #14) is mostly net-new (separate reward-juice WO).

## CHEAP WIRES (exist but half-connected)
- **Rumble on LANDING a hit:** `HeroImpactFeedback.PlayHaptic(intensity,dur)` exists + on the hero, but only fires when the hero TAKES damage (HeroHealth.cs:195). Call it from `PlayerAttackController` impact + `HeroAbilities` resolve, scaled by damage.
- **Honor the ScreenShake SETTING:** `ScreenShakeSetting.Enabled` (SettingsModel.cs:334) is defined but NO shake path checks it -> the toggle is inert. Gate the shake calls on it.
- **Targeting tap/right-click:** add TAP (mobile) / RIGHT-CLICK (desktop) to `HeroTargetIndicator`'s existing Tab/shoulder cycle (+ tap-an-enemy to lock it directly). Tiny input add, not a rebuild.

## Knight kit (WO-494) = DATA, not a system
The Q/W/E/R cast/cooldown/mana/HUD/VFX pipeline is DONE. Add the 4 Grok abilities (Heroic Leap/Shield Bash/
Defender's Call/Radiant Strike) as `AbilityDef`s in `abilities.json` + add any missing effect shapes
(dash/knockback/taunt) as new `AbilityEffect` cases in `HeroAbilities.ResolveEffect`.

## NET-NEW SUMMARY (the whole build list)
**death-camera hold · hero injured stance · post-fight count-up reward screen · 3 cheap wires (rumble-on-hit,
honor-screenshake-setting, tap/right-click target) · author the Knight 4-ability data.** EVERYTHING else in
493/496 is reuse + tuning. Verify `AttentionGlow`/outline reuse before building role-readability (494 #2).
