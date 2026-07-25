# WORK ORDER 758 — Particle Pack VFX: mental model for Claude (how to implement them)

**Status:** KNOWLEDGE + IMPLEMENTATION PRIMER — READ BEFORE WO-757.  
**Classification:** design knowledge / VFX authoring rules (not a standalone content ship).  
**PO:** Elden (owner learning session 2026-07-23, Flames sandbox + Particle Pack demo).  
**Companion ship WO:** `WORK_ORDER_757_dragon_breath_particle_pack.md` (Syndrath breath).  
**Sandbox:** `D:\flames` — Unity 6000.4.8f1 URP; demo `FlameThrower` validated visually.

---

## 0. Mission for Claude

Internalize **how** Unity Particle Pack effects are meant to be used, then implement Syndrath breath
(WO-757) **using that model** — not by rebuilding fire from scratch, not by flattening layers, not by
treating “cone toward target” as a Shape-module rewrite every frame.

**One sentence the owner wants Claude to share:**

> Prefab = the recipe (layers already built). Inspector = dials for look.  
> Chin bone + offset socket = where it starts. Rotate socket so forward aims at target = where the cone goes.  
> Code = when to Play/Stop (and damage timing).

---

## 1. The principle (whole Particle Pack demo)

Almost every effect in the free [Particle Pack](https://assetstore.unity.com/packages/vfx/particles/particle-pack-127325)
uses the **same toolkit**:

| Building block | Role |
|----------------|------|
| One or more **ParticleSystem** components | Spawn many short-lived particles |
| **Shape** (cone / sphere / circle / …) | Where particles are emitted |
| Modules (lifetime, speed, size, color, flipbook, …) | How each particle behaves |
| **Renderer** (billboard / stretch) | How each particle is drawn |
| **Materials** (URP Particles, soft particles, emission) | Glow, fade, soft edges |
| **Prefab hierarchy** | Package multiple systems as one drag-and-drop effect |

They are **not** different engines for fire vs water vs goop. They are different **recipes**
(textures + rates + layer count) on the same kitchen.

Stats from the pack under `Assets/UnityTechnologies/ParticlePack` (Flames copy):

- ~55 effect prefabs  
- Most “hero” effects use **2–4+** ParticleSystems parented together  
- A few simple ones use **1** system (steam, tiny flames, fog)  
- Big set pieces use **6–8** (e.g. BigExplosion, EarthShatter)

**Same principle as FlameThrower; different complexity.**

---

## 2. Multi-layer is intentional — do not flatten

Owner-validated breath art: **`FlameThrower.prefab`**

```
FlameThrower                 ← main flame jet (hero layer)
  ├─ FireEmbers (3)          ← sparks (detail / heat)
  └─ Smoke                   ← volume / contrast
```

| Layer | Job if disabled |
|-------|-----------------|
| Root flame | Loses the breath body |
| Embers | Looks like smooth CGI paint |
| Smoke | Loses bulk / “heat” contrast |

**Rules for Claude:**

1. Duplicate / pool the **whole prefab tree**.  
2. Never merge into a single ParticleSystem “to simplify.”  
3. When debugging “looks wrong,” toggle **children** one by one — one layer may be broken (material/depth), not the whole effect.  
4. Quality tiers may **disable children** (Low = no stream; Medium = root only; High = full stack) — that is allowed; deleting children from the source prefab is not.

---

## 3. Prefab + Inspector vs code (what the owner learned)

### 3.1 Adding an effect

1. Find the prefab in Project (e.g. `.../Fire & Explosion Effects/Prefabs/FlameThrower`).  
2. Drag into Hierarchy / scene (or spawn via `VFXManager` / pool).  
3. The **layers, materials, and default tuning are already inside** the prefab.

You do **not** recreate systems from `GameObject → Effects → Particle System` unless authoring a brand-new effect.

### 3.2 Inspector = look & feel dials

With the prefab (or a child) selected, the right-hand Inspector is how you **tune** the recipe:

| Dial (typical) | Meaning |
|----------------|---------|
| **Transform Scale** | World size (biggest win for dragon vs demo) |
| **Start Lifetime / Speed / Size** | How long / hard / big each puff is |
| **Emission → Rate over Time** | Density of the stream |
| **Shape → angle** (cone) | **Width** of the spray (narrow jet vs wide thrower) |
| **Color over Lifetime** | Fade / cool |
| **Texture Sheet Animation** | Flicker (usually leave pack defaults) |
| **Material** | Leave pack URP particle mats unless pink |
| **Looping / Play On Awake** | Demo always-on vs game-controlled |

Tune **per child** when needed (embers rate vs smoke size).

### 3.3 Code / hierarchy = when & where (gameplay)

Inspector does **not** replace:

| Need | Mechanism |
|------|-----------|
| Follow the dragon head | Parent under chin/jaw bone (socket) |
| Start outside the mesh | Local **offset** on the socket |
| Point at Heart | **Rotate** socket so forward → target |
| Only during breath | `Play` / `Stop` or enable + VFXHandle lifecycle |
| Damage timing | Coroutine / timer in `DragonBoss.FireBreath` |
| No alloc spam | `VFXManager` pool + catalog |

---

## 4. Owner’s attach model (chin → offset → cone toward target)

This is the **canonical spatial model** for Syndrath breath. Claude must implement this shape.

```
Boss_Dragon
  └─ … skeleton …
       └─ Chin / Jaw / Head bone          ← follow animation
            └─ VFX_BreathSocket           ← EMPTY you create
                 • local Position = offset in front of snout
                 • rotation: local forward = breath direction
                      └─ Boss_FireBreath instance (FlameThrower stack)
                           Shape cone emits along system forward
```

### 4.1 Three different “aim” concepts — do not mix them up

| Concept | What it controls | Where you set it |
|---------|------------------|------------------|
| **Chin bone parent** | Effect follows head motion | Hierarchy parenting |
| **Offset** | Spawn point outside skull/mesh | Socket **local Position** |
| **Direction toward target** | Which way the jet points | Socket **rotation** (`LookRotation(target - socket.pos)`) |
| **Cone angle** | How **wide** the spray is | Particle **Shape** module on prefab (art tune, usually fixed) |

**Critical:** “Cone toward target” means **orient the transform** so the existing cone’s forward
points at the Heart — **not** rewriting Shape every frame to a new mathematical cone, and not
parenting the effect at the Heart.

### 4.2 Aim code pattern (reference)

```csharp
// When breath starts (and optionally each frame while breathing):
Vector3 toTarget = (heartWorldPos - breathSocket.position).normalized;
if (toTarget.sqrMagnitude > 1e-6f)
    breathSocket.rotation = Quaternion.LookRotation(toTarget, Vector3.up);

// Then Play the pooled FlameThrower instance parented to breathSocket
// (or PlayAura/PlayProjectile-style API that parents to the Transform).
```

If the jet shoots sideways/backward relative to the mouth:

1. Fix with a **one-time local rotation** on the prefab child (pack forward ≠ bone forward).  
2. Then keep aiming the **socket**, not random trial-and-error on Shape.

### 4.3 Socket authoring checklist

- [ ] Empty child under chin/jaw (name e.g. `VFX_BreathSocket`).  
- [ ] In Scene view, nudge local position until fire starts **outside** the snout.  
- [ ] Drop FlameThrower as child, Play, adjust socket rotation until jet leaves the mouth cleanly.  
- [ ] Assign socket to `DragonBoss` serialized field; null → name search fallback + FlowTrace warning.  
- [ ] Scale prefab root (~2–4× demo) so it reads on Syndrath’s size.

---

## 5. Demo vs game (same principle, different control)

### Demo / Flames sandbox (owner already did this)

```
Open demo scene → select FlameThrower → Play
→ tweak Inspector if desired
```

Play On Awake + Looping = always visible. Pure art exploration.

### Game (what Claude ships in WO-757)

```
1. Duplicate FlameThrower → game prefab Boss_FireBreath (keep 3 layers)
2. Tune scale / rates in Inspector (or leave defaults)
3. Author VFX_BreathSocket on chin + offset
4. Catalog as VFXType.Boss_FireBreath; pool via VFXManager
5. FireBreath(): telegraph → Play stream on socket → aim at Heart
                 → delay damage/impact → Stop stream
6. Die/OnDisable: Stop handle
7. DeNelle-URP: enable Depth Texture (soft particles); HDR if budget allows
```

---

## 6. How to learn any pack prefab in 2 minutes (for future WOs)

When owner or Claude picks a new effect (`FireBall`, `BigSplash`, `MuzzleFlash`, …):

1. Drag prefab into empty scene.  
2. Expand Hierarchy — **count ParticleSystems**.  
3. Disable children one by one — learn each layer’s job.  
4. On root: change only **Scale, Rate, Start Speed, Shape angle** first.  
5. Duplicate into game folder when happy; wire Play/Stop + attach point.

Same mental model as breath — only recipes change.

---

## 7. Anti-patterns (Claude must avoid)

| Don’t | Do instead |
|-------|------------|
| Rebuild fire with one new ParticleSystem | Use pack `FlameThrower` tree |
| Flatten multi-layer into one system | Keep children; quality-gate by disable |
| Parent effect to Heart and hope | Parent to **mouth socket**, aim **toward** Heart |
| Confuse Shape angle with aim direction | Angle = width; rotation = direction |
| Play On Awake looping in combat prefab | Explicit Play/Stop from `FireBreath` |
| Ignore soft particles in URP | Depth texture on `DeNelle-URP` |
| Instant damage with no stream duration | Timed window (see WO-757 defaults) |
| Edit pack prefab in place forever | Duplicate to `Resources/VFX/Boss/` (or project convention) |

---

## 8. Relationship to WO-757

| Doc | Role |
|------|------|
| **This WO (758)** | Knowledge: how particle pack VFX work + chin/offset/aim model |
| **WO-757** | Ship checklist: files, VFXType, timing numbers, acceptance, quality tiers |

Claude implementing breath **must read 758 first**, then execute **757**.  
If 757 and 758 conflict on spatial model, **758 wins** (owner intent).  
If they conflict on file paths / pool API, match existing EoA `VFXManager` patterns and note in RESULT.

---

## 9. Acceptance for this knowledge WO

This WO is “done” when the implementer (Claude) can answer yes:

- [ ] Explains prefab vs Inspector vs code in one short paragraph.  
- [ ] Names the three FlameThrower layers and why flattening is wrong.  
- [ ] Implements chin socket + offset + LookRotation aim (not Shape rewrite).  
- [ ] Distinguishes cone **width** (Shape) from cone **direction** (transform).  
- [ ] Ships breath via WO-757 without inventing a second VFX bus.

**RESULT:** No separate content ship. Note in  
`WORK_ORDER_757_dragon_breath_particle_pack.RESULT.md` that 758 was applied  
(socket name, offset used, aim method).

---

## 10. Copy-paste block for Claude’s system/session prompt

```
Particle Pack VFX model (owner, WO-758):
- Prefab = complete recipe (often multi ParticleSystem children). Do not flatten.
- Inspector = look tuning (scale, rate, speed, shape WIDTH).
- Game = socket on chin/jaw + local offset out of mouth + rotate socket so
  forward aims at target (LookRotation). Shape cone angle stays art.
- Code = Play/Stop timing + damage delay + VFXManager pool.
- Breath art: FlameThrower (flame + FireEmbers + Smoke).
- Implement Syndrath per WO-757; spatial model per WO-758.
- Sandbox proof: D:\flames FlameThrower in demo scene.
```

---

**One-line mission:**  
Teach and enforce: **drop the prefab, tune in Inspector, attach to chin with offset, aim the transform (cone) at the target, drive Play/Stop from gameplay** — same principle for the whole Particle Pack, applied first to dragon breath in WO-757.
