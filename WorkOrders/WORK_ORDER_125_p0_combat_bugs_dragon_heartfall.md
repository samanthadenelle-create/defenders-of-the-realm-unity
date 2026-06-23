# WORK ORDER 125 — P0 Combat Bugs: Dragon Unhittable + Heart-Fall No-Lose

**Status:** READY TO IMPLEMENT
**Priority:** P0 / URGENT (core lose condition is broken — game is unloseable AND the apex boss is invincible)
**Date:** 2026-05-30
**Owner:** Samantha (creative calls), CLI (code + build verify)
**Source:** Live playtest screenshot + owner report (hero & towers can't damage the dragon; dragon emptied the Elarion HP bar and nothing fired)

---

## Scope / Lanes

This WO touches the **Combat/AI lane** (code only) plus one **scene-wiring** field set in `VillageSceneBuilder.cs` (architect lane). The VillageSceneBuilder edit is a single field; coordinate so no other agent is mid-edit on that file (CLAUDE.md §9 — it is a serialization bottleneck).

Files in play:
- `Assets/_Modules/Village/Buildings/TowerCombat.cs` — Bug 2
- `Assets/_Modules/Village/Hero/HeroAbilities.cs` — Bug 1
- `Assets/_Modules/Village/Heart/HeartController.cs` — Bug 3
- `Assets/_Modules/Village/Waves/WaveManager.cs` — Bug 3 (subscriber/defeat path)
- `Assets/_Modules/Village/VillageController.cs` — Bug 3 (alt subscriber host — Samantha picks)
- `Assets/Editor/VillageSceneBuilder.cs` — Bug 1 (optional scene-wiring assist)

Respect CLAUDE.md §0/§1: edit `.cs` **only** via Write/Edit on the Windows path, run the brace-balance gate on every file touched, never hand-edit `.unity`.

---

## BUG 1 — Hero abilities (Q Arcane Bolt / R Meteor Strike) cannot damage the dragon

### Symptom
Casting Q or R at the circling dragon does no damage. Occasionally a cast registers (during a low swoop) but Q especially never connects while the dragon orbits.

### Root cause
The dragon **is** a valid `IDamageable` and **is** on the correct layer — those are not the failure. Confirmed:
- `Boss_Dragon.prefab` (`Assets/Resources/Enemies/Boss_Dragon.prefab`) line 14: `m_Layer: 8` (the Enemy layer). The hero's hit-test mask is `_enemyMask = 1 << EnemyLayer` (set in `VillageSceneBuilder.cs:3479` → `SetLayerMaskField(so, "_enemyMask", 1 << EnemyLayer)`, EnemyLayer = 8). So layer membership matches.
- The dragon has a `CapsuleCollider` (`Boss_Dragon.prefab:37-59`), `m_IsTrigger: 1`. `HeroAbilities.Blast`/`NearestHostile` query with `QueryTriggerInteraction.Collide` (`HeroAbilities.cs:346, 368`), so a trigger is fine.
- `DragonBoss` implements `IDamageable` directly (`DragonBoss.cs:75`), and the component sits on the prefab root alongside the collider, so `col.GetComponentInParent<IDamageable>()` in `HeroAbilities.AsHostile` (`HeroAbilities.cs:386-392`) resolves it.

**The actual blocker is RANGE / REACHABILITY in the village hit-test geometry.** Hero abilities sweep an `OverlapSphere` centred on the hero's feet (`origin = transform.position`, `HeroAbilities.cs:229`) with the ability's authored radius:
- **Q Arcane Bolt** — `effect: strike`, `range: 13` (`abilities.json` mage `q`). `ResolveEffect` Strike case calls `NearestHostile(atk, def.Range + _enemyHitRadius)` → sweep ≈ 13.85u (`HeroAbilities.cs:306`). In village mode `AimPointOverride` is null, so `atk = origin` (hero feet near the Heart, `HeroAbilities.cs:284`).
- The dragon orbits at `_orbitHeight = 22` and `_orbitRadius = 26` around the Heart (`Boss_Dragon.prefab:77-78`; `DragonBoss.TickOrbit`, `DragonBoss.cs:355-374`). Straight-line distance from a hero standing near the Heart to the orbiting dragon is ≈ √(26² + 22²) ≈ 34u — **far outside Q's ~13.85u sweep.** Q can therefore never reach the orbiting dragon and only ever clips it during the brief bottom of a swoop (low height 4.5, radius collapsing toward `_strikeRadius*0.5 = 3.5`), which is why it "sometimes" works.
- **R Meteor Strike** — `effect: meteor`, `range: 6`. The Meteor case calls `NearestHostile(atk, float.MaxValue)` (`HeroAbilities.cs:328`), which sweeps 1000u, so it *does* find the dragon and `Blast`s its position. **R should already damage the dragon.** If the owner reports R also failing, the most likely remaining cause is that the dragon collider sits high enough that the 1000u sphere from the hero's feet still encloses it (it does — 1000u » 34u), so re-test R in isolation. If R genuinely whiffs, see "CLI: verify" below.

So: **Q (and W Frost Nova at range 5.2, E is a heal) are range-bound and cannot reach an airborne boss.** This is a design-geometry gap: the hero kit was tuned for ground enemies that march to within melee/short range of the Heart; a boss that stays at altitude 22 is unreachable by the short-range slots.

### The fix
Samantha to pick ONE approach (recommend **A + C**):

**A. Give the hero an aim-at-boss path in village mode (preferred, minimal).**
In `HeroAbilities.ResolveEffect` (`HeroAbilities.cs:263-335`), the Strike/Snare and Aoe/Cleave cases resolve from `atk = AimPointOverride ?? origin`. Add a village-mode fallback so that when no `AimPointOverride` is set AND a live apex boss exists, offensive casts resolve from (or toward) the boss position. Concretely, in the **Strike** case, before `NearestHostile`, if `AimPointOverride == null` and `WaveManager.LiveApexBoss` is alive, set the sweep origin/target to the boss's `WorldPosition` (the dragon's `IDamageable.WorldPosition`, `DragonBoss.cs:203`). This keeps `HeroAbilities` talking only to the Core `IDamageable` seam (CLAUDE.md §5 — Village→Core only); resolve the boss via the existing `WaveManager` reference pattern (`FindObjectsByType<WaveManager>` once, cached) — do NOT add a HUD or cross-module ref. Guard every cross-module call with `?.` (CLAUDE.md §10).

**B. OR raise the short-slot reach for airborne targets.** Bump Q's `range` in `abilities.json` (mage `q`) and/or add a vertical-tolerant sweep — least preferred, inflates ground-combat reach too.

**C. Make the dragon swoop low enough to be reachable as the intended counter-play.** The dragon's swoop low height is `_swoopLowHeight = 4.5` and during Phase 2/3 it dives often; the *design* (dragon-boss.md §4) is that the **swoop is the window to punish it.** If the team wants the encounter to read as "wait for the dive, then burst it," confirm the swoop brings the dragon within Q's 13u reach of the hero's standing position. At swoop bottom the dragon is at radius ≈ 3.5 from the Heart, height 4.5 → ≈ 5.7u from a Heart-adjacent hero — inside Q range. So **C already works during swoops**; the felt bug is that the player cannot hit it the other ~90% of the time it is aloft. Pair C with A so the player has both a reactive (swoop) and a ranged (aim) answer.

> CLI: implement **A** (the aim-at-boss fallback in `HeroAbilities.ResolveEffect`) as the load-bearing fix. Leave B/C as tuning for Samantha. Do not touch `abilities.json` unless Samantha greenlights B.

### CLI: verify
- After the fix, cast Q at the orbiting dragon — its HP bar (BossHealthBar) must drop.
- Confirm R already works pre-fix (it should). If R does NOT damage the dragon even though the 1000u sweep encloses it, the only remaining suspect is `AsHostile` rejecting it — check `DragonBoss.IsAlive` returns true (`DragonBoss.cs:209`) and `Faction == Hostile` (`DragonBoss.cs:200`); both look correct in source, so a failing R points to the boss not actually being spawned/registered (cross-check Bug 2 spawn path).

### Acceptance criteria
- Hero Q Arcane Bolt damages the dragon while it orbits (not only during a swoop).
- Hero R Meteor Strike damages the dragon while it orbits.
- Ground-enemy targeting behaviour is unchanged (Q still hits the nearest ground enemy when no boss is up; village casts with no boss behave exactly as before).
- `HeroAbilities.cs` still references enemies only through `DeNelle.Core.Combat.IDamageable` (no new concrete-type or HUD dependency); `?.` used on the WaveManager/boss lookups; brace gate passes.

---

## BUG 2 — Towers cannot damage the dragon

### Symptom
Built towers ignore the dragon entirely — they fire only at ground enemies and never at the boss, even when it orbits/swoops within tower range.

### Root cause — DEFINITIVE
`TowerCombat` acquires targets **exclusively from the wave's ground-enemy roster, and only via the `EnemyDamageable` component** — the dragon is in neither.

- `TowerCombat.FindNearestTarget` (`TowerCombat.cs:119-143`) iterates `_wave.LiveEnemies` (the `WaveManager._liveEnemies` list, `WaveManager.cs:206`) and, for each, does `enemy.GetComponent<EnemyDamageable>()` (`TowerCombat.cs:137`). It accepts the target only if that `EnemyDamageable` is non-null, alive, and Hostile.
- The dragon is **not in `LiveEnemies`.** Per `WaveManager.cs:778-781`: *"the dragon is not in `_liveEnemies` (it owns kinematic flight, not a NavMesh agent), so its life is tracked separately via `_liveApexBoss`."* The boss lives in the separate `_liveApexBoss` field (`WaveManager.cs:187, 569`), exposed as `WaveManager.LiveApexBoss` (`WaveManager.cs:209`).
- The dragon also has **no `EnemyDamageable` component** — it implements `IDamageable` directly (`DragonBoss.cs:75`; confirmed by dragon-boss.md §9: *"no `EnemyDamageable` adapter is needed"*). So even if it were somehow in the list, `GetComponent<EnemyDamageable>()` would return null and the tower would skip it.

Net: `TowerCombat` has **no code path that can ever see the dragon.** This is a hard miss, not a tuning/range issue.

### The fix
In `TowerCombat.cs`, make the target scan include the apex boss as an `IDamageable`, resolved through the Core seam (towers are in `DeNelle.Village`, dragon's `IDamageable` is `DeNelle.Core.Combat` — Village→Core is allowed, CLAUDE.md §5).

Precise change in `FindNearestTarget` (and mirror in `FindHighestHpTarget` for TrueAim):
1. After the existing `_wave.LiveEnemies` loop computes `best`/`bestSq`, also consider `_wave.LiveApexBoss`:
   - `var boss = _wave?.LiveApexBoss;`
   - if `boss != null` and `boss.IsAlive` and `((IDamageable)boss).Faction == CombatFaction.Hostile`, compute `sq = (boss.WorldPosition - myPos).sqrMagnitude` and, if `sq <= maxSq && sq < bestSq`, set `best = boss` (cast to `IDamageable`) and `bestSq = sq`.
2. Return `best` as before. The rest of the fire pipeline already operates on `IDamageable` (`FireAt` / `FireSingleProjectile` / `proj.Initialize(target, damage, element)` at `TowerCombat.cs:230-236`), and `PooledProjectile` calls `target.TakeDamage(...)` — which `DragonBoss.TakeDamage` (`DragonBoss.cs:518-523`) honours. No projectile change needed.

Notes for CLI:
- `DragonBoss` exposes `IsAlive`, `Faction`, `WorldPosition`, `Hp` via `IDamageable` (`DragonBoss.cs:200-209`). Use the `IDamageable` view so `TowerCombat` does not take a concrete `DragonBoss` reference beyond the `WaveManager.LiveApexBoss` property type (which already lives in `DeNelle.Village`, so no boundary crossed).
- Use `?.` on `_wave?.LiveApexBoss` (CLAUDE.md §10).
- The dragon at orbit height 22 will be **out of most towers' range** much of the time (tower `CurrentRange` is the 3D distance — the `sqrMagnitude` includes the 22u Y term). That is acceptable/intended: towers chip the dragon when it swoops low and when a high-range tower can reach the orbit. If Samantha wants towers to reliably hit the orbiting boss, that is a separate range/elevation tuning decision — flag it, do not silently widen tower range.

### Acceptance criteria
- A built tower within range of the dragon (e.g. during a swoop, or a long-range tower vs the orbit) fires at it and its HP bar drops.
- Ground-enemy targeting and priority are unchanged when no boss is present.
- TrueAim secondary targeting also considers the boss (mirror change applied to `FindHighestHpTarget`).
- No concrete-type leak beyond `WaveManager.LiveApexBoss`; `?.` on the wave lookup; brace gate passes.

---

## BUG 3 — Dragon destroyed the Heart (Elarion) but NO defeat / game-over fired  (WORST — core lose condition broken)

### Symptom
The dragon's swoop/fire-breath drained the Elarion HP bar to empty (top-left bar reads 0). The Heart is destroyed, but no defeat screen, no game-over, no scene reload — the game just continues in a dead state.

### Root cause — DEFINITIVE
**Heart HP reaching 0 has no defeat path. Nothing subscribes to it, and the only `GameOverUI.Show()` caller is the HERO's death, not the Heart's.**

Chain of evidence:
- The dragon damages the Heart via `DragonBoss.DealStrike` → `_heartStructure.ApplyContactDamage(amount)` (`DragonBoss.cs:502-507`). `_heartStructure` is the Heart resolved through `IDamageableStructure` in `Configure` (`DragonBoss.cs:236-237`).
- `HeartController.IDamageableStructure.ApplyContactDamage` → `SetHp(_hp - amount)` (`HeartController.cs:269`).
- `HeartController.SetHp` clamps to [0,100], fires `OnHealthChanged` and derives a `HeartState` (`HeartController.cs:204-215`). **It does NOT raise any death/defeat event when `_hp` hits 0, and there is no "Heart died" event at all.** `IDamageableStructure.IsAlive` simply returns `_hp > 0f` (`HeartController.cs:268`) but nothing polls or reacts to it flipping false.
- **No subscriber checks for Heart HP == 0 anywhere.** `WaveManager` only ever enters its `Breached` phase from *enemy proximity to the Heart* (`WaveManager.TriggerBreach` is called from the inner-ring proximity check `WaveManager.cs:770-774` and `HandleEnemyReachedHeart` `WaveManager.cs:1036-1044`) — both keyed on a ground `Enemy` crossing `_innerRingRadius`, **never on Heart HP.** The dragon is not in `_liveEnemies` and never crosses the breach ring (it stays aloft), so it can drain the Heart to 0 without ever arming a breach.
- `GameOverUI.Show()` (`Assets/_Modules/UI/GameOverUI.cs:17`) — the actual "Elarion has fallen. The chord is lost." screen — is invoked from exactly ONE place: `HeroHealth.HandleDeath()` (`HeroHealth.cs:158-185`), gated on the **hero** dying (`HeroHealth.cs:138-145`). There is **no Heart-death → GameOverUI path.** So when the Heart dies but the hero lives, nothing fires.

So the lose condition is wired to the hero's HP and to ground-enemy breaches — but **not to the Heart's own HP hitting 0**, which is the thing the dragon attacks.

### The fix
Add a Heart-death event and a single subscriber that fires the defeat flow.

**Step 1 — `HeartController.cs`: raise a death event when HP hits 0.**
- Add `public event System.Action OnHeartDestroyed;` near `OnHealthChanged` (`HeartController.cs:147`).
- In `SetHp` (`HeartController.cs:204-215`), after `_hp = next; OnHealthChanged?.Invoke(_hp);`, add: if `_hp <= 0f` and not already flagged, set a private `bool _destroyed` guard and invoke `OnHeartDestroyed?.Invoke();` (fire once). Guard prevents repeat firing from subsequent 0-damage `SetHp` calls. Keep `IDamageableStructure.IsAlive` as-is.
- Keep `using DeNelle.Core.Combat;` present (already there, `HeartController.cs:37`) — the file implements `IDamageableStructure` (CLAUDE.md §10).

**Step 2 — subscribe + fire defeat. Samantha picks the host:**
- **Option (preferred): `WaveManager`.** It already holds `Heart` (`WaveManager.cs:212`) and owns the run-state machine. In `BeginLoop`/wherever the Heart is resolved, subscribe `_heart.OnHeartDestroyed += HandleHeartDestroyed;` and add `HandleHeartDestroyed()` that: stops the wave loop (set `_phase` to a terminal state — reuse `Breached` or add a `Defeated` enum value), and triggers the same game-over flow `HeroHealth` uses. Because `WaveManager` is `DeNelle.Village` and `GameOverUI` is in the default `Assembly-CSharp` (unreachable by asmdef — see `HeroHealth.cs:167-205`), **reuse the existing reflective `FindGameOverUi()` bridge pattern**: factor that lookup so both `HeroHealth` and the new handler can call it, OR have `HandleHeartDestroyed` call into a shared helper. Do NOT introduce new `System.Reflection` beyond the already-established bridge (CLAUDE.md §10 forbids *new* reflection in bridge scripts — reuse the existing pattern, don't add a second mechanism).
- **Option (alt): `VillageController`.** It also holds `HeartController _heart` (`VillageController.cs:54`). Subscribe there instead if Samantha wants defeat handling separate from the wave loop.
- Unsubscribe in `OnDisable`/`OnDestroy` to avoid stale-delegate leaks (the pattern `HeartController` documents at `HeartController.cs:139-146`).

**Step 3 — defeat presentation.** Call `GameOverUI.Show()` via the existing reflective bridge (same as `HeroHealth.HandleDeath`), with the same fallback (scene reload) when no `GameOverUI` is present. The `GameOverUI` message "Elarion has fallen. / The chord is lost." (`GameOverUI.cs:21`) is already the Heart-fall copy — confirming the screen was *designed* for this case but never wired to it.

### CLI: verify
- In play, let the dragon drain the Heart to 0 (or use a dev cheat / `DevPanelController`): the game-over screen must appear with "Elarion has fallen."
- Confirm the wave loop stops (no further spawns / no further dragon strikes after defeat).
- Confirm the existing hero-death game-over still works (do not regress `HeroHealth.HandleDeath`).
- Confirm `OnHeartDestroyed` fires exactly once (guard works) even if `ApplyContactDamage` is called again at 0 HP.

### Acceptance criteria
- When the Heart's HP reaches 0 (from the dragon or any source), the defeat / game-over flow fires (GameOverUI shown, or scene reload fallback) — within the same beat the hero-death path uses.
- The wave loop halts on defeat (terminal phase).
- Hero-death game-over is unaffected.
- No NEW reflection mechanism added (reuse the existing `FindGameOverUi` bridge); `?.` on cross-module calls; `using DeNelle.Core.Combat;` retained in `HeartController.cs`; brace gate passes on every file.

---

## Do NOT touch

- Do **not** hand-edit any `.unity` scene file (CLAUDE.md §3). The only scene-affecting change permitted here is the optional `VillageSceneBuilder.cs` field assist in Bug 1 (a `.cs` edit, applied via Write/Edit, then re-baked by CLI in a *separate* WO — UI does not fire batchmode, CLAUDE.md §3).
- Do **not** rename Elarion or reintroduce "Avalon"/"Keep" (CLAUDE.md §7). Note: source comments in `DragonBoss.cs` still say "Avalon" — leave them; this WO does not do a canon-string cleanup.
- Do **not** widen tower range or hero ability ranges silently for Bug 1/2 — flag any range/elevation tuning to Samantha as a separate creative call.
- Do **not** fold `EnemyDamageable` into `Enemy` or change the adapter pattern — out of scope.
- Do **not** add a HUD or `DeNelle.HUD` reference to `HeroAbilities`/`TowerCombat`/`HeartController` (Village→Core only; HUD→Core only — CLAUDE.md §5).
- Do **not** modify `abilities.json` unless Samantha greenlights Bug 1 option B.

---

## Cross-file checklist before RESULT (CLAUDE.md §10)
- [ ] Brace balance check passed on TowerCombat.cs, HeroAbilities.cs, HeartController.cs, WaveManager.cs (and VillageController.cs if used).
- [ ] No `.unity` scene file hand-edited.
- [ ] No NEW `System.Reflection` usage introduced (Bug 3 reuses the existing GameOverUI bridge).
- [ ] `using DeNelle.Core.Combat;` present in HeartController.cs (implements IDamageableStructure).
- [ ] `?.` used on all cross-module / WaveManager / boss service lookups.
- [ ] Acceptance criteria for all 3 bugs reviewed line by line.
