> ⚠ **STALE — predates the 2026-06-22 single-Knight pivot.** Treat its Blink-hero / party-of-4 / tower-defense-pillar framing as SUPERSEDED (hero = single Tripo "Grom", Blink rig junked, base-defense V2-gated); some architecture/monetization content may still hold. Live reality: `CANON_GROUND_TRUTH_2026-06-26.md` + `docs/COMBAT_PIVOT_NORTHSTAR.md`.

# BattleATB — `DeNelle.BattleATB`

Active-Time-Battle combat system. Split design: deterministic pure-C# engine
(`Engine/`) + Unity-facing controllers at root. Has its own test assembly.

## Layout

- **Root (Unity layer):** `ATBCombatManager`, `BattleController`, `BattleHud`,
  `BattleVfx`, `ATBBackgroundController`, `AtbCombatantSwapper`, `AtbControlModeStore`
- **`Engine/` (pure C#, no UnityEngine):** `BattleState`, `Combat`, `Turn`,
  `Actions`, `Ai`, `Targeting`, `Rng` (seeded/golden-vector tested),
  `BattleScaling`, `Types`, `Defs`, `CombatantDefSO`
- **`State/`:** `ATBRuntimeState` (carries party/encounter data across scene loads)
- **`Tests/`:** unit tests per engine file + `RngGoldenVectorTest`

Scene: `Assets/Scenes/ATBBattle.unity`. Debugging guide: root `ATB_DEBUGGING_GUIDE.md`.

## FF7-Style Active Battle HUD (this Work Order)
The previous HUD implementation (complex VisualElement cards + UIDocument) has been replaced for this ATB structure.
- New `BattleHudUgui.cs`: pure code-built uGUI (Canvas + Image + TMP + Vertical/Horizontal Layouts).
- Exact layout from the WO:
  - CommandPanel (bottom-left): Attack / Skills / Item / Defend vertical list (classic FF7 blue box). Skills click shows dynamic sub-panel with abilities (mapped from active hero class via existing HeroClass + engine defs / catalog).
  - PartyStatusPanel (bottom-right): 4 fixed slots (portrait placeholder, name, HP bar+text, MP bar+text, ATB bar+text). ATB uses visual fill simulation (engine is discrete; gives the "charging" feel).
  - BattleInfoPanel (top-center): "The Last Stand" title, WAVE X, "XXX's Turn".
- Blue/grey FF7 aesthetic (easy to skin later).
- Fully dynamic from ATBRuntimeState / BattleState (units, HP, active, wave, etc.).
- Callbacks (OnAction etc.) match what the controller/engine expect — drop-in.
- 3D arena stays clean (capsules + models from AtbCombatantSwapper). Floating damage numbers spawn in world space above models on hits (in BattleController).
- No UXML/UIDocument for the main HUD — guaranteed builds.

BattleController now creates/wires the uGUI HUD in Start() (self-contained, no scene edit required).

## Combat feel & animation (WO-284/285 + recent immersion pass)
- 3D hero/enemy models are swapped in at load by `AtbCombatantSwapper` (real class FBX for hero, enemy models or tinted fallback).
- `ActorAnimator` (Core.Combat, the guarded IActorAnimator) is now attached on the capsules so the models can be driven by verbs.
- `BattleController` drives the verbs on action submit (PlayAttack for knight swings / PlayCast for mage spells using the per-class clips from the canonical animation pipeline) and on turn resolution (PlayHit for flinches on direct hits, Die for collapse/fall on death).
- This gives visible, class-specific attack/cast animations + hit reactions / injury falls without new systems — everything routes through the existing ATB event bus (OnActionSubmitted / OnTurnResolved), the shared ActorAnimator driver, and the Hero/EnemyAnimatorFactory-built controllers (Shared + Knight/Wizard/Enemies clips, upper-body layers for moving+attacking, Hit/Death latches).
- Hit-stop / screen shake / damage numbers still come from BattleVfx + VFXManager. SmartMobileCamera combat zoom applies when in range.
- "Limp injured": PlayHit plays the reaction anim (flinch + temporary injured posture from Shared/Enemies clips); sustained low-HP limp can be extended via additional SetCombatStance or future Injured param if controllers grow it.
- Every fight now has audible/visible impact, class-appropriate motion, and death theatre. The pure engine stays untouched (data only); presentation lives in the Unity layer.

> Maintenance: update this README when files are added/removed.
