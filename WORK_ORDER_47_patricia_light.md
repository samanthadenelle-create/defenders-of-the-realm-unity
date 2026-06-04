# WORK ORDER 47 — "Patricia Light": Defend-the-Tower shooter (breach choice)

**Status:** READY TO IMPLEMENT (phased)
**Date:** 2026-05-26
**Author:** Owner design (Patricia Light) + Claude UI scaffold, adapted to the real codebase
**Priority:** High — the second half of the breach moment; the action-mode alt to ATB.

---

## Concept

When an enemy breaches the Heart, the player gets a **choice** (not an automatic ATB):

- **Enter the Last Stand** → the existing 2D turn-based **ATB** battle, OR
- **Defend the Tower** → **Patricia Light**: a real-time **shooter** where the Hero
  fights from inside the central spire while pets attack or repair.

Core fantasy: you (Hero) are up in the tower; enemies stream in; you spam your
attack to loose arrows/spells, fire special abilities, and decide whether each pet
is **attacking** or **repairing** the tower. Survive **5 waves + a boss** and you
**repel the assault** and earn a **Wisdom bonus**.

This is distinct from [[WORK_ORDER_46_tower_combat]] (auto-firing placed towers
during the normal wave). WO-46 = passive defense you build; WO-47 = the active
breach-time stand. They layer; this WO does not depend on WO-46.

---

## Orientation: PORTRAIT-FIRST (owner: "Tricia hates landscape")

The primary layout is **portrait**:

- The **tower/spire is centered**; the Hero stands on a balcony partway up it.
- Enemies advance from **both sides (left + right)**, in **two lanes each**: a
  **ground** lane and an **air/flying** lane.
- HUD: a tall **Tower Integrity** bar, the **wave/boss counter + timer**, a big
  thumb-reachable **Attack (trigger) button** bottom-centre, **special-ability**
  buttons beside it, and **pet Attack/Repair** toggles.
- Landscape is a nice-to-have fallback, not the target. Build the UIDocument with
  a portrait PanelSettings (match width/reference resolution e.g. 1080×1920) and
  anchor with flex so it also survives landscape.

## Hero: Ranger or Mage only

Only the **ranged** classes are playable here (Knight is melee). On entering
Patricia Light, if the saved hero is Knight, prompt/force a temporary Ranger or
Mage pick (or just disable the choice for Knight saves in v1 and note it).

The **Attack button is the SAME trigger as the village HUD** — reuse
`HeroAbilities.TryCast` for the basic shot (Ranger arrow / Mage bolt) and the
existing ability slots for **special abilities** (which scale with the talent
tree + the new XP level multiplier already in `HeroAbilities`/`HeroProgression`).
Spam-friendly: the basic shot is the low-cooldown slot. Difficulty ramps per wave.

## Pets: Attack vs Repair (one or the other)

Reuse the existing `DeNelle.Pets.Pet` — do NOT add a colliding `PetController`.
`Pet` already has `PetMode { Idle, Defend, Fortify }` and hunts `IDamageable`
hostiles. Add a **Repair** behaviour: a `Repair` mode (or reuse `Fortify`) in which
the pet moves to the tower and calls the tower's `Repair(amount*dt)` instead of
attacking. A per-pet toggle in the HUD flips each pet between Attack and Repair.

## Win / lose + Wisdom bonus

- **Lose:** Tower Integrity reaches 0 → the breach is lost (game-over or Heart
  damage, matching the ATB-defeat path).
- **Win:** survive **5 waves + 1 boss**; clearing the boss **repels all remaining
  enemies**. On win, grant a **Wisdom bonus** via `WisdomCurrencyService.Grant(...)`
  AND XP via the existing `ProgressionManager`/`DamageAttribution` (the hero's shots
  already record damage if routed through `IDamageable` + `DamageAttribution`).

---

## Architecture (adapt the scaffold — do NOT paste it)

- **New module** `Assets/_Modules/BattlePatriciaLight/` with
  `DeNelle.BattlePatriciaLight.asmdef` referencing `DeNelle.Core` (+ `DeNelle.Data`,
  Input System, UniTask). It may reference `DeNelle.Village` to spawn `Enemy`s, OR
  spawn them via a small spawner that talks to `Enemy.Configure` — prefer reusing the
  existing `Enemy` + `WaveManager` data (waves.json) so the assault uses real enemies.
- **Separate additive scene** `PatriciaLight.unity` (RECOMMENDED — mirrors how ATB
  is its own scene; isolates the portrait camera + UI + orientation). Routed via the
  existing `SceneRouter` pattern that ATB uses, with a `ReturnScene` back to Village.
- **Input:** the project uses the **Input System**, not legacy `Input`. The basic
  shot binds to the same action the HUD ability button uses; auto-aim picks the
  nearest hostile `IDamageable` via `Physics.OverlapSphere` (like Pet/HeroAbilities).
- **Damage routing:** the hero's shots call `IDamageable.TakeDamage(dmg, element)`
  AND `DamageAttribution.Record(target, "hero", dmg)` so kills feed the XP system
  (floating damage numbers already pop from `Enemy.TakeDamage`).
- **Tower HP:** reuse `HeartController` (it already has `Hp`/`SetHp`) as the tower's
  integrity, or a thin `TowerIntegrity` wrapper that drives the same value — do NOT
  invent a parallel HP the rest of the game can't see.
- **Breach choice UI:** a small two-button prompt ("Last Stand" / "Defend the Tower")
  shown on breach, replacing the auto-load-to-ATB. Picking ATB = current behaviour;
  picking Defend = load PatriciaLight.

## New files (phase-tagged)

| File | Phase |
|---|---|
| `BattlePatriciaLight/DeNelle.BattlePatriciaLight.asmdef` | 1 |
| `BattlePatriciaLight/PatriciaLightController.cs` (orchestrator: spawn hero on balcony, run the 5-wave+boss assault, win/lose, Wisdom+XP payout) | 1–3 |
| `BattlePatriciaLight/TowerIntegrity.cs` (wraps Heart HP; TakeDamage/Repair/events) | 1 |
| `BattlePatriciaLight/HeroTurretController.cs` (portrait aim + spam-fire via HeroAbilities; special abilities) | 2 |
| `BattlePatriciaLight/PatriciaLightHud.cs` (portrait UIDocument: integrity bar, wave/timer, Attack + ability buttons, pet toggles) | 2 |
| `BattlePatriciaLight/PatriciaLightSpawner.cs` (left/right × ground/air lanes; reuses Enemy + waves.json) | 2–3 |
| Pet Repair behaviour (extend `DeNelle.Pets.Pet`, no new controller) | 3 |
| Breach choice prompt (ATB vs Defend) in the breach path | 1 |
| `Assets/Scenes/PatriciaLight.unity` + portrait PanelSettings | 1 |

## Animations & VFX (Phase 2 "feel")

Ranger = fast satisfying archery; Mage = flashy impactful casting; mobile-friendly
(few particles, pooled). **Reuse what exists — do NOT add a parallel `VFXManager`
or `HeroAnimatorController`:**

- **Casts already animate**: `HeroAbilities.TryCast` fires the hero Animator's cast
  trigger; the shooter's spam-fire goes through `TryCast`, so draw/release & spell
  casts animate for free. Add per-slot triggers only if a distinct anim is needed
  (Multi-Shot burst, charged Power Shot, Fireball wind-up, Arcane Blast channel).
- **VFX**: extend the existing `AbilityVfxKit` (class-keyed, WO-37) — it already does
  Ranger arrow streak + leaf burst and Mage impacts. Add: arrow trail, impact
  burst/sparks, bow/staff charge glow + muzzle flash, ground impact decal. SFX via
  the existing `AbilityAudioBridge`.
- **Juice (cheap, high-impact)**: brief screen flash + ~0.3s slow-mo + small camera
  push on the charged Power Shot / big spell; muzzle flash on every shot.
- **Perf**: low max-particles, **pool** VFX (don't Instantiate/Destroy per shot),
  Shader Graph glow over many sprites. Matches the project's asset-free, code-built idiom.

## Phases

1. **Skeleton & choice** — module + scene + breach choice prompt + spawn hero in the
   tower + TowerIntegrity + a trivial enemy stream + lose-on-zero + return-to-village.
2. **Shoot & HUD** — portrait HUD (integrity bar, wave/timer, Attack/ability buttons),
   hero auto-aim + spam-fire through HeroAbilities, damage→XP routing, damage numbers.
3. **Assault & pets & win** — 5 waves + boss from waves.json, left/right air+ground
   lanes, pet Attack/Repair toggle + repair behaviour, win→repel+Wisdom/XP bonus.

## Acceptance criteria

- [ ] Breach → a choice appears: **Last Stand (ATB)** or **Defend the Tower**.
- [ ] Defend loads a **portrait** scene: tower centered, hero on the balcony, enemies
      from **left + right** on **ground + air** lanes.
- [ ] Only **Ranger/Mage** are usable; the **Attack button = the HUD trigger**, spam
      fires arrows/bolts; special abilities fire and scale with talents + level.
- [ ] Each **pet** can be toggled **Attack** ⇄ **Repair**; Repair restores tower HP.
- [ ] **Tower Integrity** bar drains on hits; 0 = lost.
- [ ] Surviving **5 waves + boss** repels the assault and grants a **Wisdom bonus**
      (+ XP), then returns to the village.
- [ ] Input via the Input System; damage routed through `IDamageable` +
      `DamageAttribution`; no parallel HP/economy; portrait-first UIDocument.

## Open decision (defaulting unless told otherwise)
- Separate additive **scene** (recommended, mirrors ATB) vs in-village state switch.
  Defaulting to **separate scene**.
