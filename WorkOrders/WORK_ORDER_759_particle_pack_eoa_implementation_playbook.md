> ## RECONCILED 2026-08-08 - true status is DONE
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: Boss_FireBreath.prefab shipped; a12c6d22 built 14 Particle Pack recipes.
> The previous Status line read "Status: SPEC - READY TO HAND TO ANY AGENT (owner validated structures in D:\Flames sandbox, 2026-08-05)." and was wrong; the board understated this.
> WARNING - WO NUMBER COLLISION: a SECOND work order is also numbered 759 (`WORK_ORDER_759_vfx_manual_picks_gameplay_wire.RESULT.md`, 'Wire VfxManualPicks into gameplay'). Do not treat 759 as a single ticket.

# WORK ORDER 759 — Particle Pack → EoA implementation playbook

**Status:** DONE  
**Classification:** VFX knowledge + implementation rules + first-ship checklist (Syndrath breath).  
**Silo:** Village combat / VFX.  
**PO:** Elden.  
**Audience:** Any AI/implementer working in **`D:\EoA`** (Echoes of Elarion / Defenders).  
**Unity:** 6000.4.8f1 + URP.  

**Related WOs (do not re-derive; this doc is the single shareable source):**

| WO | Role |
|----|------|
| **This (759)** | Full playbook: pack structures, sequence patterns, EoA wiring, anti-patterns |
| 758 | Older mental-model primer (superseded in detail by this doc; keep for history) |
| 757 | Syndrath breath ship checklist (timing numbers + acceptance) — still valid |
| 754 | Editor VFX Caster preview for Particle Pack (tooling only) |
| 66 | Boss phase VFX (`Boss_Telegraph`, phase auras) — style to mirror |

**Sandbox proof (optional open):** `D:\Flames` — same pack, isolated URP project.  
**Game project:** `D:\EoA` — **pack already imported**; do not re-download or re-import.

---

## 0. One-paragraph mission (paste into agent prompt)

```
EoA Particle Pack VFX rules (WO-759):
- Pack lives at Assets/UnityTechnologies/ParticlePack (ALREADY IMPORTED — never reimport).
- Prefab = multi-layer recipe. Duplicate whole tree into Resources/VFX; never flatten children.
- Two sequence families: CONTINUOUS (rateOverTime > 0 → PlayLoop/PlayAura + Stop)
  and BURST (rate=0, bursts at t=0 → PlayOneshot / VFXManager.Play).
- Inspector = look dials (scale, rate, speed, Shape ANGLE = width).
- Gameplay = socket transform (parent + local offset) + rotate so forward aims at target.
- Code = VFXType + VFXCatalog + VFXManager only — no second VFX bus.
- Soft particles need DeNelle-URP m_RequireDepthTexture: 1 (currently 0).
- First ship: Boss_FireBreath from FlameThrower via DragonBoss.FireBreath() (WO-757 numbers).
```

---

## 1. Import / asset caveat (read first)

| Fact | Action |
|------|--------|
| Pack is **already** under `Assets/UnityTechnologies/ParticlePack/` in EoA | **Do not** Asset Store re-import, duplicate pack folder, or copy from Flames binary-for-binary unless owner asks |
| ~55 effect prefabs under `EffectExamples/` | Use as **source recipes** only |
| Game runtime prefabs | **Duplicate** into `Assets/Resources/VFX/...` then wire catalog |
| Materials / textures | Stay on pack mats unless pink → convert **only broken** mats to URP Particles |
| Git / LFS | Pack binaries may be gitignored or large — do not “fix import” in CI; assume tree present on owner machine |
| Flames (`D:\Flames`) | Sandbox for visual study only; **implement in EoA** |
| Do not point GraphicsSettings at pack `URP.asset` | Keep `Assets/Settings/DeNelle-URP.asset`; flip flags only |

**Canonical pack root (EoA):**

```
Assets/UnityTechnologies/ParticlePack/EffectExamples/
```

**Game VFX home (EoA):**

```
Assets/Resources/VFX/          ← catalog prefabs live here (Projectiles/ already exists)
Assets/Resources/VFX/VFXCatalog.asset
```

---

## 2. Mental model (non-negotiable)

### 2.1 Same kitchen, different recipes

Every Particle Pack effect uses the **same Unity ParticleSystem toolkit**:

| Block | Role |
|-------|------|
| 1+ **ParticleSystem** components | Spawn short-lived particles |
| **Shape** (Cone / Sphere / Hemisphere / Circle / …) | Where particles emit |
| Modules (lifetime, speed, size, color, flipbook) | Per-particle behaviour |
| **Renderer** (Billboard / Stretch) | Draw mode |
| **Materials** (URP particles, soft, emission) | Look |
| **Prefab hierarchy** | Package layers as one drag-and-drop effect |

They are **not** separate engines for fire vs water vs goop.

### 2.2 Prefab vs Inspector vs code

| Layer | Owns | Does **not** own |
|-------|------|------------------|
| **Prefab** | Layers, materials, default rates/shapes | When it plays |
| **Inspector** | Scale, rate, speed, shape **width**, colors | Aim direction, damage |
| **Hierarchy socket** | Attach point + local offset + forward | Emission math |
| **Code (`VFXManager`)** | Play / Stop / pool / quality | Rebuilding particle graphs |

### 2.3 Multi-layer is intentional — never flatten

Example owner-validated breath art — **`FlameThrower.prefab`**:

```
FlameThrower                 ← main flame jet (hero layer, billboard + flipbook)
  ├─ FireEmbers (3)          ← stretched sparks (heat / detail)
  └─ Smoke                   ← soft dark volume (bulk / contrast)
```

| If you disable… | Result |
|-----------------|--------|
| Root flame | No breath body |
| Embers | Smooth CGI paint, no heat |
| Smoke | Thin, no bulk |

**Rules:**

1. Duplicate / pool the **whole prefab tree**.  
2. Never merge into one ParticleSystem “to simplify.”  
3. Debug by toggling **children** one by one.  
4. Quality tiers may **disable** children — not delete them from the source prefab.

### 2.4 Aim concepts — do not mix them up

| Concept | Controls | Where |
|---------|----------|--------|
| **Bone / socket parent** | Follows head/weapon motion | Hierarchy |
| **Local offset** | Starts outside mesh | Socket local Position |
| **Socket rotation** | Jet **direction** (cone forward) | `LookRotation(target - socket.pos)` |
| **Shape angle** | Spray **width** (narrow vs wide) | Particle Shape module (art; usually fixed) |

**“Cone toward target” = rotate the transform.**  
Do **not** rewrite Shape every frame. Do **not** parent the effect at the Heart.

---

## 3. Two sequence families (critical for API choice)

### Family A — CONTINUOUS stream / ambient loop

| Signal in prefab | `rateOverTime > 0`, bursts empty/0, systems often `looping` |
| Demo behaviour | Play On Awake + loop forever |
| EoA API | `VFXManager.Instance.PlayAura(type, socket)` or `PlayProjectile` / `PlayEnvironment` / `PlayLoop` |
| Catalog | `IsLoop = true`, pool size ≥ 1–2 |
| Lifecycle | Keep `VFXHandle` → `handle.Stop()` (or `Stop(immediate: true)` on death) |
| Gameplay timing | Start on skill open → hold duration → Stop |

**Examples:** FlameThrower, FlameStream, FireBall (orb trail), WildFire, Large/Medium/TinyFlames, Steam, torches, goop stream.

### Family B — BURST oneshot impact

| Signal in prefab | `rateOverTime = 0`, **bursts at t=0** with particle counts |
| Demo behaviour | May show `looping: 1` but emission is one burst per cycle |
| EoA API | `VFXManager.Play(type, worldPos)` / `PlayImpact` / oneshot path |
| Catalog | `IsLoop = false`, optional `LifetimeOverride` |
| Lifecycle | No handle needed; pool auto-reclaims after duration |
| Gameplay timing | Fire on hit / death frame at position |

**Examples:** Tiny/Small/Big/Energy/DustExplosion, MuzzleFlash, weapon impacts, EarthShatter.

### Family C — Scripted multi-phase (rare)

Only a few pack effects (e.g. Dissolve/Respawn + `SpawnEffect.cs`) drive shader properties over time. Prefer not to port unless a WO explicitly needs character dissolve. Normal fire/explosion does **not** need this.

---

## 4. Fire & Explosion — exact structures (EoA pack paths)

**Folder:**

```
Assets/UnityTechnologies/ParticlePack/EffectExamples/
  Fire & Explosion Effects/Prefabs/
```

Shape enum (Unity): `0=Sphere`, `2=Hemisphere`, `4=Cone`, `8=ConeVolumeShell`, `10=Circle`.

### 4.1 Continuous directed fire

| Prefab | Layers (keep all) | Emission | Shapes (approx) | EoA use case | Suggested VFXType |
|--------|-------------------|----------|-----------------|--------------|-------------------|
| **`FlameThrower`** ★ | Root + `FireEmbers (3)` + `Smoke` | rates ~30 / 100 / 20 | Cone ~0.9° (narrow jet), embers/smoke cone-shell | **Syndrath breath**, flamethrower cone | `Boss_FireBreath` (loop) |
| **`FlameStream`** | Root + `FireEmbers` | ~50 / 100 | Narrow cone stream | Simpler stream (no smoke bulk) | Optional later |
| **`FireBall`** | `FireBall` + `FireEmbers (4)` | ~10 / 200 | Cone ball + sphere embers | **Projectile spit** / flying orb | Optional `Projectile_*` later |

### 4.2 Ambient / residual ground fire

| Prefab | Layers | Emission | Shapes | EoA use case | Suggested VFXType |
|--------|--------|----------|--------|--------------|-------------------|
| **`WildFire`** | Embers + WildFire + Fire | continuous | Hemisphere 90° ground | Residual burn zone after breath | Optional `Env_*` / boss zone |
| **`LargeFlames`** | FireEmbers + LargeFlames | continuous | Circle + shell | Large pyre / arena fire | `Env_TorchFlame` upgrade or new Env |
| **`MediumFlames`** | single | continuous | Circle | Brazier / mid torch | Env |
| **`TinyFlames`** | single | continuous | Circle | Candle / prop | Env |

### 4.3 Burst explosions

| Prefab | Layers | Bursts (all t=0) | EoA use case | Suggested VFXType |
|--------|--------|------------------|--------------|-------------------|
| **`TinyExplosion`** | Shockwave + core + Embers | small counts | Light hit / small fire pop | `Impact_Flame` / small boss hit |
| **`SmallExplosion`** | Embers + Shockwave + core + smoke | mid | Standard fire impact | `Impact_ExplosionFire` / `Boss_AttackImpact` |
| **`BigExplosion`** | 8 systems (core, dual embers, smoke trail, light, debris, extra smoke, shockwave) | large | Boss death / structure | `Boss_Death` / big set piece |
| **`EnergyExplosion`** | Embers + Energy + Lightning + Shockwave | stylized | Magic/plasma impact | Optional breath impact upgrade |
| **`DustExplosion`** | Dust + embers + small fire + shockwave + sand (~500) | debris-heavy | Ground/dirt hit | `Env_DestructionDust` family |

`ParticlesLight` is a light helper prefab, not a full effect recipe.

### 4.4 Other categories (same rules, map as needed)

| Category | Path under `EffectExamples/` | Typical family | Example uses in EoA |
|----------|------------------------------|----------------|---------------------|
| Weapon Effects | `Weapon Effects/Prefabs/` | Burst | Hits, muzzle, surface response |
| Water Effects | `Water Effects/Prefabs/` | Mix | Splash / leak / shower |
| Smoke & Steam | `Smoke & Steam Effects/Prefabs/` | Continuous | Fog, steam, rocket trail |
| Goop Effects | `Goop Effects/Prefabs/` | Continuous + pool | Acid spit / puddle |
| Magic Effects | `Magic Effects/Prefabs/` | Burst / projectile | EarthShatter, IceLance |
| Misc Effects | `Misc Effects/Prefabs/` | Mix | Sparks, dust motes, dissolve |
| Legacy Particles | `Legacy Particles/Prefabs/` | Mix | Prefer modern folders first |

---

## 5. EoA architecture (mandatory bus)

### 5.1 Files you must use

| Piece | Path |
|-------|------|
| Enum | `Assets/_Modules/Village/Vfx/VFXType.cs` |
| Catalog SO | `Assets/Resources/VFX/VFXCatalog.asset` (+ `VFXCatalog.cs`) |
| Runtime | `Assets/_Modules/Village/Vfx/VFXManager.cs` |
| Handle | `Assets/_Modules/Village/Vfx/VFXHandle.cs` |
| Pool | `Assets/_Modules/Village/Vfx/VfxPool.cs` |
| URP | `Assets/Settings/DeNelle-URP.asset` |
| Boss driver | `Assets/_Modules/Village/Enemies/DragonBoss.cs` |
| Boss prefab | `Assets/Resources/Enemies/Boss_Dragon.prefab` |
| Editor audition | **Defenders → Animation → VFX Caster** (`VfxCasterWindow.cs`, WO-754) |

### 5.2 API cheat sheet

```csharp
// Family B — oneshot at world point
VFXManager.Play(VFXType.Impact_ExplosionFire, hitPoint);
VFXManager.Instance.PlayImpact(VFXType.Boss_AttackImpact, pos, rot);

// Family A — loop parented to transform (follows bone/socket)
VFXHandle h = VFXManager.Instance.PlayAura(VFXType.Boss_FireBreath, breathSocket);
// … later …
h?.Stop();                 // graceful (particle tail)
h?.Stop(immediate: true);  // death / disable

// Projectile travel (same loop path, parented)
var ph = VFXManager.Instance.PlayProjectile(VFXType.Projectile_FlameArrow, projTransform);
```

`PlayLoop` parents with `SetParent(parent, worldPositionStays: true)` after setting world position to `parent.position` — **socket must be the parent** so the jet follows the head.

### 5.3 Catalog row fields

For each new `VFXType`:

| Field | Continuous | Burst |
|-------|------------|-------|
| `Type` | new enum | new enum |
| `Prefab` | game duplicate under `Resources/VFX/...` | same |
| `PoolSize` | ≥ 2 for boss | ≥ 4–8 for spam impacts |
| `IsLoop` | **true** | **false** |
| `MinQuality` | 1 or 2 (skip on Low as designed) | 0–1 |
| `LifetimeOverride` | 0 | optional if auto-detect short |

### 5.4 Quality tiers (`VFXQuality`)

| Quality | Continuous multi-layer | Burst |
|---------|------------------------|-------|
| **Low (0)** | Skip stream (damage/SFX only) | Smallest impact or skip secondary |
| **Medium (1)** | Root layer only (disable embers/smoke children after spawn) | Mid explosion |
| **High (2)** | Full stack | Full stack |

Implement medium by disabling named children on the **instance** after acquire, or by separate catalog prefabs (`Boss_FireBreath_Medium`). Do not strip source pack.

### 5.5 URP soft particles (required for pack fire to look right)

Current `DeNelle-URP.asset` (as of playbook write):

```
m_RequireDepthTexture: 0    ← must be 1 for soft particles
m_RequireOpaqueTexture: 0   ← optional
m_SupportsHDR: 0            ← prefer 1 if mobile budget allows
```

Without depth, soft fire edges hard-clip / look flat (owner saw this; Flames URP has depth/HDR on).

**Do not** switch the project to the pack’s demo `URP.asset`.

---

## 6. Content pipeline (any new pack effect)

```
1. Audition in VFX Caster (ParticlePack toggle) OR open Flames demo.
2. Expand Hierarchy → count ParticleSystems → note continuous vs burst.
3. Duplicate prefab → Assets/Resources/VFX/<Category>/<GameName>.prefab
4. Keep ALL children. Strip only demo-only junk if any (most fire prefabs are pure PS).
5. Scale root for gameplay size (boss breath ~2–4× demo).
6. Append VFXType enum value (Category_Descriptor naming).
7. Add VFXCatalog row (prefab, IsLoop, pool, MinQuality).
8. Call from gameplay via VFXManager only.
9. On death/disable: Stop any VFXHandle you hold.
10. FlowTrace when playing (throttled) for socket + type.
```

### 6.1 Game prefab naming

```
Assets/Resources/VFX/Boss/Boss_FireBreath.prefab          ← from FlameThrower
Assets/Resources/VFX/Boss/Boss_FireBreathImpact.prefab    ← optional Small/EnergyExplosion
Assets/Resources/VFX/Env/Env_WildFire.prefab              ← example later
```

Match existing neighbors under `Resources/VFX/Projectiles/` style.

---

## 7. First ship: Syndrath fire breath (implements WO-757)

### 7.1 Problem (current code)

`DragonBoss.FireBreath()` today:

```csharp
// Assets/_Modules/Village/Enemies/DragonBoss.cs
private void FireBreath()
{
    AnimTrigger(HAttack);
    PlayTelegraph();
    DealStrike(_breathDamage);  // instant damage + impact VFX — no mouth stream
}
```

Comment in file already points at WO-757 for the sustained cone.

### 7.2 Target sequence

```
FireBreath():
  if dead → return
  AnimTrigger(Attack)                          // existing
  PlayTelegraph()                              // Boss_Telegraph oneshot
  if breath VFX enabled && quality allows:
      resolve VFX_BreathSocket (or fallback)
      aim: LookRotation(Heart/target - socket.position)
      stop prior _breathHandle if any
      _breathHandle = PlayAura(Boss_FireBreath, socket)
  coroutine/timer:
      wait _breathDamageDelay                  // default 0.35s
      DealStrike(_breathDamage)                // impact at Heart (existing or breath impact type)
      wait remaining to _breathDuration        // default 1.4s total
      _breathHandle.Stop()
  On Die / OnDisable:
      _breathHandle.Stop(immediate: true)
```

### 7.3 New types (append only on `VFXType`)

```csharp
// -- WO-757 / WO-759 Particle Pack boss breath --------------------------------
/// <summary>Sustained multi-layer fire breath cone (pack FlameThrower). Loop + Stop.</summary>
Boss_FireBreath,
/// <summary>Optional dedicated breath impact at Heart; else reuse Boss_AttackImpact.</summary>
Boss_FireBreathImpact,
```

### 7.4 Socket on `Boss_Dragon`

Prefab: `Assets/Resources/Enemies/Boss_Dragon.prefab`

```
… head / jaw / chin bone …
  └─ VFX_BreathSocket          ← empty child (create if missing)
       localPosition = offset in front of snout (owner feels)
       localRotation = forward = breath direction (fix with LookRotation in code)
```

`DragonBoss` fields:

```csharp
[Header("Fire breath VFX (WO-757/759)")]
[SerializeField] private bool _breathVfxEnabled = true;
[SerializeField] private VFXType _breathStreamVfx = VFXType.Boss_FireBreath;
[SerializeField] private VFXType _breathImpactVfx = VFXType.Boss_AttackImpact;
[SerializeField] private Transform _breathSocket;
[SerializeField] private float _breathDuration = 1.4f;
[SerializeField] private float _breathDamageDelay = 0.35f;
[SerializeField] private float _breathAimLerp = 12f;
// runtime: VFXHandle _breathHandle;
```

Null socket fallback: name search `VFX_BreathSocket` / Jaw / Mouth / Head + `FlowTrace` warning — never hard-crash.

### 7.5 Files to touch (breath ship)

| File | Change |
|------|--------|
| `VFXType.cs` | Add `Boss_FireBreath` (+ optional impact) |
| `Resources/VFX/Boss/Boss_FireBreath.prefab` | Duplicate FlameThrower tree; scale ~2.5 |
| `Resources/VFX/VFXCatalog.asset` | Row: IsLoop=true, pool≥2, MinQuality=1 |
| `DragonBoss.cs` | Timed `FireBreath()` + aim + handle lifecycle |
| `Boss_Dragon.prefab` | Socket + serialized refs |
| `DeNelle-URP.asset` | Depth texture on; HDR if safe |

### 7.6 Do NOT (breath + general)

- Flatten FlameThrower layers  
- Parent stream at the Heart  
- Rewrite Shape angle every frame for aim  
- Second VFX stack / direct `Instantiate` outside pool  
- Enable combat VFX on `DragonCinematicFlyby` (combat component stays off)  
- Reimport Particle Pack  
- Point project at pack `URP.asset`  
- Change swoop/orbit math  
- Block compile if socket missing  

### 7.7 Tune defaults

| Param | Default |
|-------|---------|
| `_breathDuration` | 1.4 s |
| `_breathDamageDelay` | 0.35 s |
| Prefab root scale | ~2.5 (owner retunes) |
| Aim | Toward Heart / current target (`AnchorPosition` / target tf) |

### 7.8 Acceptance (breath)

**Felt**

- [ ] Visible multi-layer jet from mouth toward Heart (flame + embers; smoke on High)  
- [ ] Lasts ~1–2 s, not a flash  
- [ ] Telegraph still readable; damage still lands  
- [ ] Scale reads on Syndrath (not sparkler, not screen wipe)  

**Engineering**

- [ ] Compile gate green  
- [ ] No nullref without socket  
- [ ] Flyby does not combat-breathe  
- [ ] Low quality skips full stream  
- [ ] Die/OnDisable stops handle  
- [ ] Depth texture on → soft edges improve  

---

## 8. Later mapping backlog (optional WOs — not this ship)

| Priority | Pack prefab | EoA role |
|----------|-------------|----------|
| P1 | FlameThrower | Boss_FireBreath (this WO) |
| P2 | SmallExplosion / EnergyExplosion | Breath impact upgrade |
| P3 | FireBall | Dragon spit projectile |
| P4 | WildFire | Ground residual burn |
| P5 | Large/Medium/TinyFlames | Env braziers if better than current torch |
| P6 | BigExplosion | Boss death punch-up |
| P7 | Weapon impacts / MuzzleFlash | When combat juice needs pack art |
| P8 | Goop / Magic / Water | Content-driven |

Each later item: same pipeline §6; new enum; catalog; no bus invention.

---

## 9. Agent implementation order

### Phase A — foundations (any pack VFX)

1. Read this entire WO.  
2. Confirm pack path exists; **do not reimport**.  
3. Flip `DeNelle-URP` depth (and HDR if approved).  
4. Audition FlameThrower in VFX Caster.

### Phase B — breath content

5. Duplicate FlameThrower → `Resources/VFX/Boss/Boss_FireBreath.prefab`.  
6. Scale; verify 3 layers in Hierarchy.  
7. Add `VFXType` + catalog row (`IsLoop=true`).

### Phase C — boss wiring

8. Author `VFX_BreathSocket` on `Boss_Dragon`.  
9. Rewrite `FireBreath()` timing + aim + `VFXHandle`.  
10. Quality gating Medium/Low.  
11. Stop on death/disable.

### Phase D — verify + RESULT

12. Play Mode / DevPanel spawn Syndrath.  
13. Write `WorkOrders/WORK_ORDER_759_particle_pack_eoa_implementation_playbook.RESULT.md`  
    (or `WORK_ORDER_757_...RESULT.md` if only breath shipped — cross-link).

---

## 10. Debug checklist (“looks wrong”)

| Symptom | Check |
|---------|-------|
| Flat / hard edges | `m_RequireDepthTexture` on DeNelle-URP |
| Pink materials | URP particle shader on **that** mat only |
| Only one layer visible | Children inactive / quality Medium stripped wrong child / flatten bug |
| Jet from wrong place | Socket offset; parent chain |
| Jet wrong direction | Socket rotation / LookRotation; one-time local fix on prefab if pack forward ≠ bone forward |
| Instant damage no stream | FireBreath still old path; handle never PlayAura |
| Stream never stops | Missing Stop on timer / Die |
| Stream freezes in world | Not parented to socket (Play without parent) |
| Cap skip | Loop/oneshot cap in VFXManager; FlowTrace throttle messages |
| Tiny on dragon | Root scale still 1 |

---

## 11. Anti-patterns (fail review if done)

| Don’t | Do |
|-------|-----|
| Rebuild fire from empty ParticleSystem | Duplicate pack prefab |
| Flatten multi-layer | Keep children; quality-disable |
| Parent effect to Heart | Mouth socket → aim **toward** Heart |
| Confuse Shape angle with aim | Angle = width; rotation = direction |
| Play On Awake looping on combat prefab | Explicit Play/Stop from code |
| `Instantiate` outside VFXManager | Catalog pool |
| Reimport pack / second copy | Use existing tree |
| Switch to pack URP asset | Toggle flags on DeNelle-URP |
| Instant damage with no stream window | Timed delay inside duration |

---

## 12. Context files for the implementing agent

**Must read**

- This WO (759)  
- `Assets/_Modules/Village/Enemies/DragonBoss.cs` — `FireBreath`, `DealStrike`, phase VFX, `AnchorPosition`  
- `Assets/_Modules/Village/Vfx/VFXManager.cs` — `PlayAura` / `PlayLoop` / quality  
- `Assets/_Modules/Village/Vfx/VFXType.cs` — append-only enum  
- `Assets/Resources/VFX/VFXCatalog.asset`  
- Pack: `.../Fire & Explosion Effects/Prefabs/FlameThrower.prefab`  
- `Assets/Resources/Enemies/Boss_Dragon.prefab`  
- `Assets/Settings/DeNelle-URP.asset`  

**Optional**

- `D:\Flames\README.md` + demo scene (visual bar)  
- WO-757 (same breath numbers)  
- WO-754 RESULT (VFX Caster multi-layer preview)  

---

## 13. RESULT template (agent fills when done)

Write: `WorkOrders/WORK_ORDER_759_particle_pack_eoa_implementation_playbook.RESULT.md`

```markdown
# WO-759 RESULT

## Shipped
- [ ] URP depth / HDR flags (state before → after)
- [ ] Boss_FireBreath prefab path + scale
- [ ] VFXType + catalog row (IsLoop, pool, MinQuality)
- [ ] Socket name + parent bone
- [ ] FireBreath timing (duration, damage delay)
- [ ] Quality behaviour Low/Med/High

## Not shipped (backlog)
- FireBall spit / WildFire / impact upgrade / …

## Verify notes
- Play Mode steps taken
- Screenshots / felt notes

## Follow-ups
- …
```

---

## 14. Copy-paste agent system block

```
You are implementing VFX in D:\EoA (Unity 6000.4.8f1 URP).

WO-759 Particle Pack rules:
1. Assets already at Assets/UnityTechnologies/ParticlePack — NEVER reimport or duplicate the pack.
2. Prefab = multi-layer recipe. Duplicate whole tree to Assets/Resources/VFX/. Never flatten.
3. CONTINUOUS (rateOverTime>0): VFXCatalog IsLoop=true; PlayAura/PlayLoop; Stop via VFXHandle.
4. BURST (rate=0 + bursts): IsLoop=false; VFXManager.Play / PlayImpact at world point.
5. Aim = socket parent + local offset + LookRotation toward target. Shape angle = width only.
6. Only bus: VFXType + VFXCatalog + VFXManager. Mirror WO-66 boss VFX style.
7. Enable soft particles: DeNelle-URP m_RequireDepthTexture=1 (do not switch to pack URP.asset).
8. First deliverable: Boss_FireBreath from FlameThrower; DragonBoss.FireBreath timed stream
   (defaults 1.4s duration, 0.35s damage delay); socket VFX_BreathSocket on Boss_Dragon.
9. Quality: Low skip stream; Medium root only; High full FlameThrower stack.
10. Write RESULT.md when done. Do not change swoop/orbit math or flyby combat.
```

---

**One-line mission:**  
Map Unity Particle Pack multi-layer prefabs into EoA’s existing `VFXManager` bus by **duplicating recipes (not reimporting)**, choosing **loop vs oneshot** from emission structure, and shipping **FlameThrower → Boss_FireBreath** on Syndrath first — chin socket, aim transform, Play/Stop, depth texture on.
