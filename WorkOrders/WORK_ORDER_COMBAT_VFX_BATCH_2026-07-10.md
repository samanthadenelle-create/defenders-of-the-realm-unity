# WORK ORDER BATCH — Combat + VFX Directive Burst (2026-07-10)

**Purpose:** durable capture of this session's combat + VFX directive burst so nothing
is lost. Each section below is a self-contained work order (its own Goal / directive /
files / seams / acceptance / not-touch / deps). Slot each into the master pipeline doc
(`MASTER_PIPELINES_BACKLOG_2026-06-06.md` / `CLI_LANES_WO_NUMBERS.md`) when minting final
WO numbers — these carry provisional `WO-VFX-*` handles.

**Standing context (applies to EVERY WO here):**
- Owner is **red/green colorblind** — all VFX callouts must read by **motion / shape /
  luminance / verticality**, NEVER by hue. Heal/shield/beacon distinctions are silhouette
  + brightness, not color.
- Battle happens in a **dedicated ARENA that is torn down after the fight** → VFX memory /
  perf cost is contained; we can **lean into rich effects**.
- **Enemies SHARE the humanoid rig with the hero** → every combat/anim/VFX approach here
  applies to BOTH hero and enemies (same sockets, same trigger seams, same catalog keys).
- Instrumentation is already threaded (`FlowTrace`, `Guard`) through these files — keep it;
  don't strip traces from systems still stabilizing (§12).

---

## Index

| Handle | Title | Priority | Status | Depends on |
|---|---|---|---|---|
| WO-VFX-CATALOG | Author the Hovl VFX catalog `.asset` (foundational) | P0 | READY TO IMPLEMENT | — (blocks all below) |
| WO-VFX-RANGED | Hero + enemy ranged projectiles → Hovl | P1 | READY TO IMPLEMENT | WO-VFX-CATALOG |
| WO-VFX-TOWERS | Towers firing → Hovl (3 tower paths) | P1 | READY TO IMPLEMENT | WO-VFX-CATALOG |
| WO-VFX-FOUNTAIN | Healing-fountain gold aura loop | P2 | NEAR-COMPLETE (catalog row only) | WO-VFX-CATALOG |
| WO-VFX-POI-CALLOUTS | Map POI auras + far-field fortress beacon | P2 | SCOPING (agent finalizing) | WO-VFX-CATALOG + scoping output |
| WO-VFX-WEAPON-TRAILS | Weapon trail/glow on ALL attack anims (hero+enemy) | P2 | SCOPING (agent finalizing) | WO-VFX-CATALOG + scoping output |
| WO-COMBAT-CAST-INTERRUPT | Movement cancels cast (NEW: adds cast wind-up) | P2 | READY TO IMPLEMENT | — (⚠ SERIALIZE on `HeroAbilities.cs`; land FIRST) |
| WO-KNIGHT-ANIM | Knight 4-button anim kit (cross-ref existing WO) | — | IN PROGRESS (001 done) | — |

**Parked (non-VFX)** — see final section: harvest WO-665/666/667 (serial `ResourceCollector.cs`
lane), KayKitChallengeOutpost repoint (committed, awaiting felt-test), currency-retirement remap (planned).

---

## WO-VFX-CATALOG — Author the Hovl VFX catalog `.asset` (P0, FOUNDATIONAL)

**BLOCKS EVERY OTHER VFX WO IN THIS BATCH.** Do this first.

### Goal
Author `Assets/Resources/VFX/HovlVfxCatalog.asset` so that `VFXManager.PlayKey(...)` resolves
keys instead of no-opping. Today the `.asset` does not exist, so **every** `PlayKey` call across
the game silently no-ops (throttled log only). All the downstream ranged/tower/fountain/POI/trail
work is invisible until this row-table exists.

### Owner directive (verbatim intent)
The catalog `.asset` does NOT exist yet, so every `VFXManager.PlayKey(...)` no-ops. Must run the
generator (needs the gitignored Hovl pack imported) to author it, and add the missing
`Fountain_Heal_Aura` row so the fountain aura has a key to resolve.

### Files to edit
- `Assets/Editor/HovlVfxCatalogGenerator.cs` — add one Map row (see seam below).
- **Run** `DeNelle.Editor.HovlVfxCatalogGenerator.Generate` in batchmode (menu:
  `Defenders/VFX/Generate Hovl VFX Catalog`) → writes the `.asset`, prints `HOVL_VFX_CATALOG_OK`.
  Requires the **gitignored Hovl Studio pack imported** (`Assets/Hovl Studio/...`); on a fresh
  clone the pack must be present or the generator can't resolve prefab paths.

### Key seams (file:line — confirmed 2026-07-10)
- `HovlVfxCatalogGenerator.cs:76` — `Map` dictionary opens.
- `HovlVfxCatalogGenerator.cs:124` — existing `Heal_Aura` row
  (`{ "Heal_Aura", new Pick(RPG + "Buff heal.prefab", recolorable: false, isLoop: true) }`).
  **Insert the new `Fountain_Heal_Aura` row immediately after this line**, e.g.:
  `{ "Fountain_Heal_Aura", new Pick(RPG + "Buff heal.prefab", recolorable: true, isLoop: true) }`
  (recolorable:true so the fountain can tint it HDR gold; isLoop:true — it's a held aura).
- `HovlVfxCatalogGenerator.cs:141` — `[MenuItem(...)] Generate()` entry (the batchmode target).
- The full curated key table lives in `HovlVfxCatalogGenerator.cs:76–139` — all keys referenced by
  the downstream WOs (`Arcane_Cast/_Projectile/_Impact`, `Fireball_*`, `Thunderbolt_*`, `Frost_*`,
  `Spear_*`, `Melee_Slash/_Impact`, `Cleave_Impact`, `Heal_*`, `Taunt_*`, `Aegis_*`, `Ember_Burn`,
  `Dash_Blink`, `Collector_Full`, `Raid_Explosion`, `LevelUp_Burst`) already exist there.

### Acceptance criteria
- `Assets/Resources/VFX/HovlVfxCatalog.asset` exists on disk (+ `.meta`).
- Generator prints `HOVL_VFX_CATALOG_OK` in the batchmode log.
- Every documented key (the WO-002 triplets + the WO-003 skill-tree keys + `Fountain_Heal_Aura`)
  is present as a row in the generated asset.
- A `PlayKey("Arcane_Cast", ...)` (or any listed key) resolves a prefab instead of no-op-logging.

### What NOT to touch
- Do not delete/re-point existing rows — only ADD the `Fountain_Heal_Aura` row.
- Do not modify `VFXManager` resolution logic here (that's already null-safe).

### Dependencies / sequencing
- **First in the batch.** No dependencies. All other VFX WOs depend on this producing the `.asset`.
- Requires Hovl pack imported + no editor open during the batchmode run (§3 project lock).

---

## WO-VFX-RANGED — Hero + enemy ranged projectiles → Hovl (P1)

### Goal
Route ranged attack **travel + impact** through Hovl keys for hero mage/ranger AND for enemy
casters. Hero cast+impact is already wired (WO-VFX-003); this fills the projectile-travel gap and
adds the entirely-missing enemy-ranged Hovl path.

### Owner directive (verbatim intent)
Hero cast+impact already wired; gaps are (1) mage/ranger projectile TRAVEL isn't a Hovl bolt,
(2) `HeroAbilities.LaunchProjectile` doesn't pass the key/tint through its mage/ranger branch
(and there's a latent double-impact to fix), (3) ENEMY ranged has NO Hovl at all. Keys already
exist in the generator table.

### Files to edit
- `Assets/_Modules/Village/Hero/RangedAttackVFX.cs` — projectile-travel Hovl.
- `Assets/_Modules/Village/Hero/HeroAbilities.cs` — pass key/tint through the mage/ranger branch.
- `Assets/_Modules/Village/Hero/MoverProjectilePool.cs` — (maybe) a no-FX body kind so the old
  Spells-Pack body can be suppressed when a Hovl follow-FX is used.
- `Assets/_Modules/Village/Enemies/Enemy.cs` — enemy `RootedCast` Hovl muzzle + travel + impact.
- Data: `EnemyTypeVfxSet` (the enemy VFX ScriptableObject/data type) — add `CastVfxKey`,
  `ProjectileVfxKey`, `ImpactVfxKey` fields defaulting to `Arcane_Cast`/`Arcane_Projectile`/`Arcane_Impact`.

### Key seams (file:line — confirmed 2026-07-10)
- `RangedAttackVFX.cs:95` — `FireArrow(Vector3 targetWorldPos, System.Action onArrive = null)`.
  Add optional `hovlProjectileKey` / `hovlImpactKey` / `tint` params; when a key is passed,
  `PlayKey(key, ..., follow: smover.transform)` on the leased mover and `Stop()` it on arrive;
  suppress the old Spells-Pack body visual.
- `RangedAttackVFX.cs:126` — `FireSpellOrb(...)` — same optional-param treatment (leases
  `ProjectileBodyKind.MageOrbVfx` at :137).
- `RangedAttackVFX.cs:108 / :137` — `LeaseMover(...)` calls (where the follow-transform comes from).
- `MoverProjectilePool.cs` — add a **no-FX body kind** (e.g. a `*_NoVfx` `ProjectileBodyKind`) so
  a Hovl-follow shot doesn't also render the pooled Spells-Pack particle body (double visual).
- `HeroAbilities.cs:1062` — `LaunchProjectile(Vector3 target, System.Action onArrive, string projectileKey = null, Color? tint = null)` (signature already carries the params).
- `HeroAbilities.cs:1069` — ranger branch (`FireArrow`) — pass `projectileKey` + `tint` through.
- `HeroAbilities.cs:1081` — else branch (`FireSpellOrb`, mage/other) — pass `projectileKey` + `tint`
  through. **Pass `impactKey = null`** here: the arrival closures already call `PlayImpactVfxKey`,
  so forwarding an impact key would DOUBLE the impact FX (the latent bug to fix).
- `HeroAbilities.cs:1078` — Knight thrown branch already flies `FlyCosmeticProjectile(projectileKey, ...)`;
  leave as the reference pattern for the follow-FX.
- `Enemy.cs:1559–1578` — `RootedCast` release block. Today it calls `vfx.FireSpellOrb(aim, land)`
  with no keys (`:1577`). Add: a **muzzle** cast burst (`PlayKey(castKey, ...)`) at cast start
  (near the `_actor?.PlayCast()` at `:1551`), pass `projectileKey`/`impactKey`/`tint` into
  `FireSpellOrb`, and a Hovl **impact** on `land` (`:1563–1576`). Keys pulled from `_typeVfxSet`
  (referenced at `:1574` as `RandomAttackClip()` — extend that data object with the 3 new key fields).

### Acceptance criteria
- Hero mage orb and ranger arrow show a **Hovl bolt travelling** muzzle→target, and a Hovl impact
  on arrival — with NO double-impact (impact fires exactly once).
- Enemy casters (shared rig) show a muzzle cast burst, a travelling arcane bolt, and an impact —
  driven by `EnemyTypeVfxSet` keys (default `Arcane_*`).
- No pooled Spells-Pack body renders alongside the Hovl follow-FX (no double visual).
- Damage timing UNCHANGED (still lands on arrival via the existing `onArrive`/`land` closures).

### What NOT to touch
- `ProjectileVFXCatalog.cs` — the tower/legacy catalog; not this lane.
- `ArcaneTower.cs`, `PooledProjectile.cs` — tower lane (see WO-VFX-TOWERS).
- Do not change projectile speed/arc or damage-on-arrival timing.

### Dependencies / sequencing
- Depends on **WO-VFX-CATALOG** (keys must resolve).
- File-disjoint from WO-VFX-TOWERS **as long as** neither reroutes `ProjectileVFXCatalog` internals
  (they won't per the not-touch rules) → the two can run as parallel edit-only silos, batch-gated together.

---

## WO-VFX-TOWERS — Towers firing → Hovl (P1)

### Goal
Give the three tower firing paths real Hovl muzzle + travel + impact FX, keeping the existing
`VFXType.Play` calls as a fallback.

### Owner directive (verbatim intent)
Three tower paths (`ArcaneTower`, `TowerCombat`, `DefenseTower`) should fire Hovl cast/projectile/
impact directly via `PlayKey`, keeping existing `VFXType.Play` as fallback. Do NOT reroute
`ProjectileVFXCatalog` internals.

### Files to edit
- `Assets/_Modules/Village/Buildings/ArcaneTower.cs`
- `Assets/_Modules/Village/Buildings/TowerCombat.cs`
- `Assets/_Modules/Village/Buildings/DefenseTower.cs`

### Key seams (file:line — confirmed / task-supplied 2026-07-10)
- `ArcaneTower.cs:262` — `FireBlast(IDamageable primary)`; muzzle computed at `:268`
  (`transform.position + up*2.5f`), impact at `:269`. Existing cast FX at `:272`
  (`ProjectileVFXCatalog.SpawnNamedOneShot(muzzle, BoltCastVfx)`). Add `PlayKey("Arcane_Cast", muzzle)`
  + an `Arcane_Projectile` follow-bolt on the spawned `ArcaneSpellBolt` (`:280`) + `Arcane_Impact` at
  impact. `ApplyBlast` (~`:325`, task-supplied) — impact-point Hovl burst.
- `TowerCombat.cs:355` — `FireAt` muzzle (`VFXManager.Play(muzzleType, firePos)`); add a `PlayKey`
  muzzle here keyed by element. `OnProjectileImpact` (~`:522`, task-supplied) — add `PlayKey` impact.
  Add an **element→key helper** (element enum → `Fireball_*`/`Thunderbolt_*`/`Frost_*`/`Arcane_*`).
- `DefenseTower.cs:596` — `PlayFireVfx(Vector3 muzzle, Vector3 targetPos)`; Spell style at `:600`
  (`VFXType.Cast_MageCharge`) + impact at `:601` (`ImpactVfxFor(Element)` at `targetPos`). Add a
  `PlayKey` muzzle + a hitscan-impact `PlayKey` at `targetPos`.

### Acceptance criteria
- All three tower types show a Hovl muzzle burst on fire; ArcaneTower's bolt carries a travelling
  Hovl body; impacts show a Hovl burst at the hit/target point.
- If a key is missing/unauthored, the existing `VFXType.Play(...)` still fires (graceful fallback —
  keep those calls).
- Element→key mapping is correct (fire/lightning/frost/arcane read distinctly by SHAPE+motion, not hue).

### What NOT to touch
- `ProjectileVFXCatalog.cs` — keep it as-is; do NOT reroute its internals (add `PlayKey` alongside).
- `PooledProjectile.cs` internals beyond what's needed to attach a follow-FX.

### Dependencies / sequencing
- Depends on **WO-VFX-CATALOG**.
- Runs parallel to WO-VFX-RANGED (disjoint files) provided neither reroutes `ProjectileVFXCatalog`.

---

## WO-VFX-FOUNTAIN — Healing-fountain gold aura loop (P2, NEAR-COMPLETE)

### Goal
A gold aura loop plays on the healing fountain while it heals the Heart out of battle, and stops
in battle. **Code is already wired** — the only remaining gap is the catalog row (folded into WO-VFX-CATALOG).

### Owner directive (verbatim intent)
`HealingFountain.cs` already calls `PlayKey("Fountain_Heal_Aura", ..., follow: transform, GoldAura)`
on the out-of-battle heal gate. ONLY gap = the `Fountain_Heal_Aura` catalog row. No `.cs` edit needed.

### Files to edit
- **None.** (The catalog row is added under WO-VFX-CATALOG.)

### Key seams (file:line — confirmed 2026-07-10)
- `HealingFountain.cs:221` — `StartAura()` called on the heal tick when `shouldHeal`.
- `HealingFountain.cs:228` — `StopAura()` when out-of-heal (battle started / no Heart / Heart full).
- `HealingFountain.cs:251–263` — `StartAura()` body: `_auraHandle = VFXManager.PlayKey(AuraKey,
  transform.position + up*1.2f, identity, transform, GoldAura, 1.0f)`. `AuraKey` = `Fountain_Heal_Aura`.
  Note the in-code comment: `PlayKey` returns null until the `.asset` row exists — healing still runs.
- `HealingFountain.cs:265–270` — `StopAura()` body (`_auraHandle.Stop()`).
- `HealingFountain.cs:238–247` — `IsOutOfBattle()` gate (BattleLock / WavePhase) that drives the aura.

### Acceptance criteria
- With the `Fountain_Heal_Aura` row authored, the fountain shows a **gold aura loop** while healing
  the Heart out of battle.
- The aura **stops** when a battle starts (or the Heart is full / absent).
- Colorblind-safe: the aura reads by its glow shape + luminance loop, not by hue alone.

### What NOT to touch
- `HealingFountain.cs` — no edits needed; do not refactor the aura handle logic.

### Dependencies / sequencing
- Depends on **WO-VFX-CATALOG** (the `Fountain_Heal_Aura` row). Otherwise complete.

---

## WO-VFX-POI-CALLOUTS — Map POI auras + far-field fortress beacon (P2, SCOPING)

> **Status: SCOPING** — an agent is finalizing the exact change-set. Mark this WO's file list /
> seams as PROVISIONAL and reconcile against that agent's output before implementing.

### Goal
One reusable callout affordance + registry that flags points of interest at two ranges:
(a) **near-field** looping ground auras on POIs, distance/discovery-gated; (b) **far-field** enemy
fortress **landmark beacons** (tall vertical light shaft / rising ember column) readable across the
overworld, NOT discovery-gated, that hand off to the near-field aura as the player approaches.

### Owner directive (verbatim intent)
Two tiers, ONE reusable callout affordance/registry. (a) Near-field auras on POIs (MineNode, harvest
sites, collectors, portals/dungeon entrances) — a looping ground aura via `PlayKey(follow)`,
distance/discovery gated (reuse the `DungeonWorldPortalSpawner` discovery pattern). (b) Far-field
ENEMY FORTRESS landmark beacon — a tall vertical light shaft / rising ember column at
KayKitChallengeOutpost + EnemyStronghold/Garrison/EnemyOutpost, readable across the overworld, NOT
discovery-gated, fades/hands off to near-field when close. COLORBLIND-SAFE: verticality/motion/
luminance, never hue.

### Files to edit (PROVISIONAL — confirm with scoping agent)
- **New** callout component + registry (e.g. `Assets/_Modules/Village/World/PoiCallout.cs` +
  a registry) — one affordance, two modes (near-aura vs far-beacon).
- `Assets/_Modules/Village/World/MineNode.cs` — near-field aura spawn hook.
- Portal / dungeon-entrance spawners (e.g. `DungeonWorldPortalSpawner`) — near-field aura hook +
  reuse its discovery-gating pattern.
- Fortress builders/spawners — `KayKitChallengeOutpost`, `EnemyStronghold`, `EnemyGarrison`,
  `EnemyOutpost` — far-field beacon spawn hook.
- Harvest-site / collector spawners — near-field aura hook.

### Key seams (to confirm during implementation)
- `MineNode.cs` — the POI's spawn/init path (attach the looping ground aura, `follow: transform`).
- `DungeonWorldPortalSpawner` — its discovery-gate predicate (reuse for near-field gating).
- Fortress builder Build()/spawn entry points — attach the vertical beacon at world-anchor.

### Acceptance criteria
- Near-field: approaching a MineNode / harvest site / collector / portal shows a looping ground aura,
  gated by distance/discovery (matching the portal discovery pattern).
- Far-field: each enemy fortress shows a tall vertical beacon (light shaft / ember column) visible
  from across the overworld, NOT discovery-gated.
- The far-field beacon fades out / hands off to the near-field aura as the player closes in (no
  double-callout).
- Colorblind-safe: readability is verticality + motion + luminance, never hue.
- One shared callout component/registry drives both tiers (no per-POI bespoke widget).

### What NOT to touch
- Do not hand-edit scene `.unity` files to place callouts — spawn them from the builders/spawners (§3).
- Do not gate the far-field fortress beacon behind discovery (it's a persistent landmark).

### Dependencies / sequencing
- Depends on **WO-VFX-CATALOG** (aura + beam keys may need authoring — add rows there if the beacon/
  aura needs new prefab keys).
- Depends on the **scoping agent's** finalized change-set (reconcile file list before edit).

---

## WO-VFX-WEAPON-TRAILS — Weapon trail/glow on ALL attack anims, hero+enemy (P2, SCOPING)

> **Status: SCOPING** — an agent is finalizing the change-set. File list/seams PROVISIONAL.

### Goal
A blade trail / weapon glow that fires whenever an attack animation plays (melee swings AND weapon
abilities), during the swing's active window — distinct from projectile impact FX. Data-driven so it
fires for EVERY attack, not per-ability, and follows the equipped-weapon bone/socket. Applies to
enemies too (shared rig). Arena-contained → lean into a rich trail.

### Owner directive (verbatim intent)
Blade trail/glow that fires whenever an attack animation plays (melee swings + weapon abilities),
during the swing's active window; distinct from projectile impact. Choke point = the attack-state
trigger in `HeroAbilities.cs` / `ActorAnimator.cs` / the Knight controller; follow the equipped-weapon
bone/socket transform; reusable data-driven hook so it fires for EVERY attack (not per-ability).
Applies to enemies too (shared rig).

### Files to edit (PROVISIONAL — confirm with scoping agent)
- `Assets/_Modules/Village/Hero/HeroAbilities.cs` — hero attack-state trigger.
- `ActorAnimator.cs` (the shared actor animator used by hero AND enemies) — the single attack-anim
  trigger choke point where the trail can be fired for every attacker on the shared rig.
- The Knight animator controller wiring / attack-state entry.
- A reusable trail component + data hook (equipped-weapon socket → trail spawn during active window).

### Key seams (to confirm during implementation)
- `HeroAbilities.cs` — the melee/weapon-ability attack trigger (where `_actor.Play*` attack is invoked).
- `ActorAnimator.cs` — `PlayCast()`/attack play methods (the shared choke point; cf. `Enemy.cs:1551`
  `_actor?.PlayCast()` and `HeroAbilities` cast path — a trail hook here covers both actors).
- Equipped-weapon bone/socket transform resolution (follow target for the trail).

### Acceptance criteria
- Every attack animation (hero melee swings, hero weapon abilities, AND enemy attacks on the shared
  rig) shows a weapon trail/glow during the swing's active window.
- The trail is spawned by ONE data-driven hook, not duplicated per-ability.
- The trail follows the equipped-weapon socket (moves with the blade).
- Distinct from projectile/impact FX (this is the swing arc, not the hit burst).
- Colorblind-safe: reads by the swept motion + luminance, not hue.

### What NOT to touch
- Do not add a per-ability bespoke trail call (must be the single shared hook).
- Do not change attack timing / hit windows.

### Dependencies / sequencing
- Depends on **WO-VFX-CATALOG** (trail/slash key — `Melee_Slash` exists at `HovlVfxCatalogGenerator.cs:116`;
  add a dedicated trail key if a swept-trail prefab differs from the one-shot slash).
- Depends on the **scoping agent's** finalized change-set.

---

## WO-COMBAT-CAST-INTERRUPT — Movement input cancels an in-progress cast (P2, READY TO IMPLEMENT)

> **Status: READY TO IMPLEMENT.**
> **⚠ CRITICAL REALITY — this is a NEW FEATURE, not a hook onto an existing channel.**
> **HERO CASTS ARE INSTANT TODAY.** `HeroAbilities.TryCast` (`HeroAbilities.cs:363`) commits the
> whole cast **synchronously in one frame** — cooldown + mana charged at `:380/:381`, and
> `CastResolved → ResolveEffect` commits that same frame (`:391/:495`). There is **NO** cast
> window / channel / `_casting` flag / coroutine, and `AbilityDef` has **no `CastTime` field**.
> So "movement interrupts casting" first requires **introducing an interruptible cast WIND-UP**.
> **Design note for the owner: casts will gain a brief wind-up — this is a combat-feel change.**

### Goal
Introduce a short, interruptible cast wind-up, then let movement input during that wind-up cancel
the cast so no effect fires. Responsive, not punishing: cancelling refunds near-full.

### Owner directive (verbatim intent)
When casting, movement input cancels the cast (no effect fires). Hero primary
(`HeroAbilities.cs` cast lifecycle + `HeroLocomotion.cs` movement input); note enemy `RootedCast`
applicability. Feel decision: self-interrupt should be forgiving on cooldown (recommend partial/no
cooldown so it's responsive not punishing).

### Files to edit
- `Assets/_Modules/Village/Hero/HeroAbilities.cs` — introduce the cast wind-up + cancel path.
- `Assets/_Modules/Village/Hero/HeroLocomotion.cs` — expose a movement-input accessor.
- `AbilityDef` + `abilities.json` — add the `CastSeconds` field.
- (SECONDARY) `Assets/_Modules/Village/Enemies/Enemy.cs` — enemy `RootedCast` interrupt parity.

### Change-set (verbatim from finalized scope)
- Add **`AbilityDef.CastSeconds`** (in `abilities.json`): `0` = instant/uninterruptible for snappy
  basics like Q; `0.3–0.5s` for spells.
- Convert **`TryCast`/`TryCastExtra`** to start a **`CastRoutine` coroutine**: charge the gate
  (cooldown + mana) up front, `yield` for `CastSeconds` polling movement each frame, and only call
  `CastResolved` AFTER the wind-up. `CastSeconds <= 0` commits immediately = **backward-compatible**.
- **Movement signal:** add `HeroLocomotion.WantsToMove => ReadMoveInput().sqrMagnitude > 0.02f`
  (public accessor near `HeroLocomotion.cs:49`; mirrors the WO-423 `hasMoveInput` deadzone at `:796`).
- **On move during wind-up:** cancel, do NOT commit, and **REFUND near-full** — mana back +
  cooldown reset, with a tiny (~0.2s) anti-flicker lockout. Self-interrupt should feel responsive,
  not punishing (feel-first — take the recommendation).
- **Enemy parity (SECONDARY):** `Enemy.RootedCast` (`Enemy.cs:1500`) already IS a real channel
  (windUp ≥ 1s, rooted). Add an interrupt check honoring **Freeze/stun/knockback displacement** to
  break before release — enemies are rooted, so their interrupt trigger is **external displacement,
  not input**.

### Key seams (file:line — supplied by finalized scope 2026-07-10)
- `HeroAbilities.cs:363` — `TryCast` (the synchronous commit today; becomes the coroutine launcher).
- `HeroAbilities.cs:380 / :381` — cooldown + mana charged up front (keep — charge before wind-up).
- `HeroAbilities.cs:391 / :495` — `CastResolved → ResolveEffect` (moves to AFTER the wind-up yield).
- `HeroLocomotion.cs:49` — where the new `WantsToMove` public accessor goes.
- `HeroLocomotion.cs:796` — the WO-423 `hasMoveInput` deadzone the accessor mirrors.
- `Enemy.cs:1500` — `RootedCast` (real channel; add displacement-based interrupt before release at `:1559`).

### Acceptance criteria
- Spells (`CastSeconds > 0`) show a brief wind-up; moving during it cancels — NO effect/damage/
  projectile fires — and refunds mana + resets cooldown (minus a ~0.2s anti-flicker lockout).
- Instant abilities (`CastSeconds <= 0`, e.g. basic Q) commit that frame exactly as today
  (backward-compatible; uninterruptible).
- Enemy `RootedCast` breaks on Freeze/stun/knockback displacement before release (external trigger,
  not input); un-displaced casts resolve normally.

### What NOT to touch
- Do not change the damage/effect payload of a successfully COMPLETED cast.
- Do not make instant (`CastSeconds <= 0`) abilities interruptible — snappy basics stay instant.

### Dependencies / sequencing
- **⚠ `HeroAbilities.cs` is HOT — SERIALIZE on this file.** It is shared with **WO-VFX-RANGED**
  (`PlayCastVfxKey` / `LaunchProjectile`) and **WO-KNIGHT-ANIM-002/003** (`PlayAttack` / `PlayCast`).
  Converting the synchronous commit into a coroutine **REORDERS the VFX/anim calls relative to the
  effect**, so this canNOT run as a parallel edit-only silo on that file. **Land cast-interrupt
  FIRST, then rebase the VFX/anim work onto it** (or strictly one agent per file at a time).
- No catalog dependency (does not need WO-VFX-CATALOG).

---

## WO-KNIGHT-ANIM — Knight 4-button animation kit (cross-reference, IN PROGRESS)

### Goal
Cross-reference the existing Knight animation work order so this batch is a complete index of the
combat/anim/VFX burst. Not a new WO — pointer + design-canon capture.

### Cross-references
- `WorkOrders/WORK_ORDER_KNIGHT_ANIM_4button.md` (existing spec).
- `docs/animations/Knight_Anim_Inventory.md` (task 001 DONE).

### Design canon (verbatim intent — preserve)
- Skill-tree **actives → hot-swap row**; the **Q/W/E/R arc = a FIXED class kit** built off the best mocap.
- **Casters → Magical Moves pack; melee → Sword & Shield pack.**
- The dedicated **Sword & Shield Moves (45 fbx)** + **Magical Moves (44 fbx)** packs still need
  **extract/retarget** — only **Hero Motion's 60 clips** are live today.
- The **dash effect has NO animation hook** — fix under WO-VFX-003.
- `SpellCastClips` labels are **stale WO-494 names** — rename/relabel when the packs are retargeted.

### Acceptance criteria
- (Tracked in the existing WO.) This section exists only as the batch's index pointer + canon capture.

### What NOT to touch
- Don't duplicate/fork the existing Knight anim WO — extend it in place.

### Dependencies / sequencing
- Anim retarget (Sword&Shield + Magical Moves packs) is upstream of the weapon-trail + cast-VFX feel;
  the dash-hook fix is tied to WO-VFX-003.

---

## Parked (non-VFX) — index only, not part of this VFX batch

- **Harvest WO-665 / WO-666 / WO-667** — SCOPED. All three share `ResourceCollector.cs` → this is a
  **SERIAL lane** (one agent at a time; §9 serialization-bottleneck discipline). Do not fan out in parallel.
- **KayKitChallengeOutpost outpost repoint** — **COMMITTED**, awaiting **felt-test** (PO closes per §13).
- **Currency retirement remap** — PLANNED (not yet scoped into WOs).

---

*Scribed 2026-07-10. Line numbers confirmed against working tree HEAD on branch
`wip/village2-and-f8-tickets` where marked "confirmed"; SCOPING sections carry PROVISIONAL
seams pending the finalizing agents' change-sets. No code was edited authoring this doc.*
