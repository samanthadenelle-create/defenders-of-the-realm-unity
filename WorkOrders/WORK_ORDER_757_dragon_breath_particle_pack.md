# WORK ORDER 757 — Syndrath fire breath using Unity Particle Pack (multi-layer VFX)

**Status:** SPEC — READY TO IMPLEMENT (owner validated sandbox 2026-07-23).  
**Classification:** combat VFX + boss feel (player-felt). Extends WO-66 boss phase VFX.  
**Silo:** Village combat / VFX.  
**PO:** Elden. Sandbox proof: `D:\flames` (Particle Pack only, URP 6000.4.8f1).  
**Source pack:** free Unity Technologies [Particle Pack](https://assetstore.unity.com/packages/vfx/particles/particle-pack-127325)  
  already imported under `Assets/UnityTechnologies/ParticlePack/` (EoA + flames).

> **READ FIRST:** `WORK_ORDER_758_particle_vfx_implementation_mental_model.md`  
> Owner spatial model: **chin bone → offset socket → rotate so cone forward aims at Heart**.  
> Prefab = recipe; Inspector = look dials; code = Play/Stop + damage timing. Do not flatten layers.

---

## 1. The problem

`DragonBoss.FireBreath()` currently:

1. Fires the Attack animator trigger  
2. Plays a **telegraph** oneshot via `VFXManager` (`Boss_Telegraph`)  
3. **Immediately** deals `_breathDamage` and plays `Boss_AttackImpact` at the Heart  

There is **no sustained breath cone** from the dragon’s mouth. The free Particle Pack’s
**`FlameThrower`** prefab is the correct art (owner felt-verified in the Flames sandbox Game view),
but it is **not one VFX** — it is **three ParticleSystems parented together**. Flattening or
wiring only the root renderer will look wrong.

Also: EoA’s `DeNelle-URP` has **Depth Texture / Opaque Texture / HDR off**. The pack’s fire mats
use soft particles + emission; soft edges can vanish or look flat in EoA until depth (and ideally
HDR) is enabled.

---

## 2. Owner design (2026-07-23) — what “done” looks like

When Syndrath does a fire-breath pass:

1. **Wind-up** — existing telegraph (keep).  
2. **Breath stream** — multi-layer fire jet from the **mouth**, aimed toward the Heart / attack
   direction, lasting a short window (not a single frame).  
3. **Impact** — existing or upgraded fire impact at the Heart when the breath “connects.”  
4. **Damage** — still deals `_breathDamage`, but timed to the breath window (not before the VFX
   starts).  
5. **Quality** — High = full FlameThrower stack; Medium = main flame only (drop smoke and/or embers);
   Low = skip stream, keep damage + optional simple impact.

**Canonical prefab (do not reinvent):**

```
Assets/UnityTechnologies/ParticlePack/EffectExamples/
  Fire & Explosion Effects/Prefabs/FlameThrower.prefab
```

Hierarchy (do **not** collapse):

```
FlameThrower                 ← main flame jet (billboard + flipbook + emission)
  ├─ FireEmbers (3)          ← stretched sparks
  └─ Smoke                   ← soft dark volume
```

**Optional later (OUT OF SCOPE for this WO):** `FireBall` as a spit projectile; `EnergyExplosion` /
`SmallExplosion` as a fancier impact. Prefer reusing existing `Boss_AttackImpact` / fire impact catalog
rows first.

**Sandbox reference (already working):** open `D:\flames` with Unity 6000.4.8f1 → demo scene →
`Effects/FlameThrower`. That is the visual bar.

---

## 3. Architecture (fit EoA patterns — do not invent a second VFX bus)

Follow existing WO-66 / `VFXManager` patterns:

| Concern | Where |
|--------|--------|
| Catalog / pool | `VFXType` + `VFXCatalog` + `VFXManager` |
| Boss drive | `DragonBoss.FireBreath()` (+ timing) |
| Attach point | Transform on `Boss_Dragon` (mouth / jaw / head socket) |
| Loop handle | `VFXHandle` from `PlayAura` / `PlayProjectile`-style loop; `Stop()` when breath ends |
| Quality | `VFXQuality` gates already in `VFXManager` |

### Recommended new VFXType entries

Add to `Assets/_Modules/Village/Vfx/VFXType.cs` (Boss section, after WO-66 aura types):

```csharp
/// <summary>Sustained multi-layer fire breath cone (Particle Pack FlameThrower). WO-757.</summary>
Boss_FireBreath,
/// <summary>Optional dedicated breath-impact at Heart (else reuse Boss_AttackImpact). WO-757.</summary>
Boss_FireBreathImpact,
```

Wire in `VFXCatalog` (ScriptableObject asset in project — find via search `VFXCatalog`):

- `Boss_FireBreath` → runtime prefab derived from **FlameThrower** (see §4 — do not point catalog at
  the raw demo prefab if it has demo-only junk; create a clean game prefab).  
- `Boss_FireBreathImpact` → either existing fire explosion prefab already used by
  `Impact_ExplosionFire` / `Boss_AttackImpact`, or leave unset and keep using `_strikeImpactVfx`.

### Attach + aim API (DragonBoss)

Add serialized fields on `DragonBoss` (mirrors phase VFX fields):

```csharp
[Header("Fire breath VFX (WO-757)")]
[SerializeField] private bool _breathVfxEnabled = true;
[SerializeField] private VFXType _breathStreamVfx = VFXType.Boss_FireBreath;
[SerializeField] private VFXType _breathImpactVfx = VFXType.Boss_AttackImpact; // or Boss_FireBreathImpact
[SerializeField] private Transform _breathSocket;      // mouth bone; if null, search / head / this.transform
[SerializeField] private float _breathDuration = 1.4f; // stream on-time
[SerializeField] private float _breathDamageDelay = 0.35f; // when damage lands within the window
[SerializeField] private float _breathAimLerp = 12f;   // rotate socket/aim toward Heart while breathing
```

Runtime:

- `_breathHandle` (`VFXHandle`) while stream is active.  
- Coroutine or elapsed timer for duration (project already uses coroutines elsewhere; match local style).  
- On death / disable: `Stop()` breath immediately.

**Aim:** while breathing, orient the stream so local forward (Particle System shape axis) points from
mouth toward `AnchorPosition()` (Heart). Do not require the whole dragon body to turn 180° if the
orbit already faces roughly inward — prefer socket rotation / child aim node.

---

## 4. Content pipeline (prefab)

### 4.1 Create game prefab (not demo)

Path suggestion:

```
Assets/Resources/VFX/Boss/Boss_FireBreath.prefab
```

(or the project’s existing Resources/VFX layout — match neighbors; dual-check Addressables only if
other boss VFX use them.)

Steps:

1. Duplicate `FlameThrower.prefab` into the game VFX folder (keep **all three** systems as children).  
2. Strip any demo-only scripts/lights if present (pack FlameThrower is pure particles — verify).  
3. Ensure root has a clear forward: Particle System **Shape** is a **Cone** along local axis used by
   the pack (do not reauthor shapes unless broken).  
4. Scale root for dragon size (Syndrath is large — start ~**2–4×** sandbox scale; owner felt-tunes).  
5. Materials stay pack materials (`fireball`, `Embers`, `SmokeDark`) unless pink → then URP particle
   convert only those mats (do not convert whole pack blindly).

### 4.2 Catalog row

Assign `Boss_FireBreath` prefab on the `VFXCatalog` asset. Confirm pool size ≥ 2 (in case two dragons
ever exist; flyby disables combat but be safe).

### 4.3 Mouth socket on Boss_Dragon

Prefab: `Assets/Prefabs/Village/Generated/Boss_Dragon.prefab` (path used by
`DragonCinematicFlyby`).

- Prefer an existing jaw/head bone.  
- If none is convenient: empty child `VFX_BreathSocket` under the head bone, nudged forward of the snout.  
- Assign that Transform to `DragonBoss._breathSocket` on the same prefab.  
- **Do not** leave null in the shipped prefab without a documented auto-find fallback
  (`Find("VFX_BreathSocket")` / name contains `Jaw`/`Mouth`/`Head`).

---

## 5. Code changes (files)

### Must touch

| File | Change |
|------|--------|
| `Assets/_Modules/Village/Vfx/VFXType.cs` | Add `Boss_FireBreath` (+ optional impact enum). |
| `VFXCatalog` asset | Wire prefab row(s). |
| `Assets/_Modules/Village/Vfx/VFXManager.cs` | Map new type(s) in any switch for procedural fallback / SFX if required; quality gating for breath. Prefer **catalog prefab** over procedural. Optional SFX: reuse existing fire/roar if any (`GameSfx.PlayDragonRoar` already exists). |
| `Assets/_Modules/Village/Enemies/DragonBoss.cs` | Rewrite `FireBreath()` timing: telegraph → start stream → delay damage/impact → stop stream. Clean stop on `Die()`. |
| `Boss_Dragon.prefab` | Socket + serialized field wiring. |

### Likely touch

| File | Change |
|------|--------|
| `Assets/Settings/DeNelle-URP.asset` | Set `m_RequireDepthTexture: 1` (soft particles). Prefer also `m_SupportsHDR: 1` if mobile budget allows; if HDR is intentionally off for mobile, document and rely on emission-only. Opaque texture optional for this effect. |
| `VFXManager` quality path | Low: no stream; Medium: main flame only (disable child `Smoke` / `FireEmbers` at spawn or use two catalog prefabs); High: full stack. |
| Editor / regression | Optional: DevPanel “force fire breath” already has spawn Syndrath — verify breath visible. |

### Do NOT

- Do not replace `DragonBoss` flight/orbit/swoop math.  
- Do not enable combat on `DragonCinematicFlyby` instances (flyby already disables `DragonBoss`).  
- Do not import a second copy of Particle Pack; use the existing
  `Assets/UnityTechnologies/ParticlePack` tree.  
- Do not hand-edit unrelated `.unity` village scenes if prefab wiring is enough.  
- Do not flatten FlameThrower to a single ParticleSystem.  
- Do not point GraphicsSettings at the pack’s demo `URP.asset` (keep `DeNelle-URP`); only enable the
  depth/HDR flags needed.  
- Do not block compile on missing socket — fallback + `FlowTrace` warning.

---

## 6. `FireBreath()` sequencing (spec)

Replace instantaneous `FireBreath()` with roughly:

```
FireBreath():
  if dead return
  animator Attack trigger (existing)
  PlayTelegraph() (existing)
  if breath VFX enabled and quality allows:
    resolve socket
    aim socket toward Heart
    _breathHandle = VFXManager.Instance.PlayAura(_breathStreamVfx, socket)
      // OR PlayProjectile if that better keeps local offset; prefer parented loop like auras
  start breath timer coroutine:
    wait _breathDamageDelay
    DealStrike(_breathDamage)   // impact VFX inside DealStrike today — keep or swap to _breathImpactVfx
    wait remaining (_breathDuration - _breathDamageDelay)
    stop _breathHandle
```

Notes:

- If `PlayAura` assumes “aura around unit,” confirm pooled instance inherits socket transform each frame
  (parented). That is required for orbiting dragon.  
- If catalog pool re-parents to a pool root, fix with the same pattern used by projectiles/auras that
  follow bones (read `PlayLoop` / `PlayProjectile` — use the API that **parents to the Transform**).  
- Multiple breath calls while one is active: either ignore or restart cleanly (stop old handle first).

---

## 7. Performance / mobile

- FlameThrower main emission ~30/s + embers ~100/s + smoke ~20/s — fine for **one** boss on desktop.  
- On `VFXQuality.Low`: skip stream.  
- On `VFXQuality.Medium`: disable Smoke + Embers children after spawn (or catalog variant
  `Boss_FireBreath_Medium` with only root system).  
- Cap: never more than one active breath handle per `DragonBoss` instance.  
- Stop particles on `Die()` and `OnDisable`.

---

## 8. Acceptance

### Felt (owner)

- [ ] In a live Syndrath fight, fire-breath shows a **visible multi-layer jet** from the mouth toward
      the Heart (flame + embers; smoke if High).  
- [ ] Breath lasts ~1–2s (tunable), not a single flash.  
- [ ] Damage / Heart reaction still happens; telegraph still readable.  
- [ ] Matches the Flames sandbox look at a dragon-appropriate scale (not tiny sparkler, not screen wipe).

### Engineering

- [ ] `COMPILE_GATE_OK` (or project’s usual batch compile).  
- [ ] No nullref if socket missing (fallback + trace).  
- [ ] `DragonCinematicFlyby` still does not breathe combat VFX (combat component disabled).  
- [ ] Quality Low does not spawn full particle stack.  
- [ ] Soft particles: with depth texture on, stream does not hard-clip into geometry as badly as before.  
- [ ] FlowTrace / log line when breath plays (throttled): socket name, duration, VFXType.

### Regression

- [ ] Swoop path unchanged.  
- [ ] Phase auras (WO-66) still swap correctly.  
- [ ] Death still stops breath if mid-stream.

---

## 9. Implementation order (for Claude)

1. **URP flags** on `DeNelle-URP` (depth on; HDR if safe).  
2. **Duplicate FlameThrower** → `Boss_FireBreath` game prefab; scale pass.  
3. **VFXType + catalog wire.**  
4. **Boss_Dragon socket** + serialize on `DragonBoss`.  
5. **Rewrite `FireBreath()`** timing + handle lifecycle.  
6. **Quality gating.**  
7. **Play-mode verify** (editor or DevPanel spawn Syndrath).  
8. **RESULT.md** under `WorkOrders/` with what shipped + remaining tune numbers.

---

## 10. Tune table (defaults — owner may change after felt)

| Param | Default | Notes |
|-------|---------|--------|
| `_breathDuration` | 1.4 s | Stream on-time |
| `_breathDamageDelay` | 0.35 s | Damage after stream starts |
| Prefab local scale | 2.5 | Start; adjust to snout width |
| Main emission rate | pack default (~30) | Don’t crank on mobile |
| Aim | toward Heart | Socket forward |

---

## 11. Context Claude should read first

- `Assets/_Modules/Village/Enemies/DragonBoss.cs` — `FireBreath`, phase VFX, `DealStrike`  
- `Assets/_Modules/Village/Vfx/VFXManager.cs` — `PlayAura` / `PlayProjectile` / quality  
- `Assets/_Modules/Village/Vfx/VFXType.cs` — enum append only  
- `Assets/Prefabs/Village/Generated/Boss_Dragon.prefab`  
- Pack prefab: `.../Prefabs/FlameThrower.prefab`  
- Sandbox proof (optional open): `D:\flames` README + demo scene  
- This WO; WO-66 comments already in `DragonBoss` for phase VFX style  

---

## 12. RESULT (fill when done)

_Agent: write `WorkOrders/WORK_ORDER_757_dragon_breath_particle_pack.RESULT.md` with:_

- Prefab path + scale used  
- Whether depth/HDR flags changed  
- Final duration / damage delay  
- Quality behavior  
- Known follow-ups (FireBall spit, impact upgrade, animator mouth sync)

---

**One-line mission for Claude:**  
Wire the validated multi-layer Particle Pack **FlameThrower** as a pooled, socket-parented
`Boss_FireBreath` stream driven by `DragonBoss.FireBreath()` with timed damage — without
flattening layers or breaking WO-66 phase VFX / flyby.
