> ⚠ **NUMBER COLLISION — this document does not own WO-760; `WORK_ORDER_760_dragon_syndrath_fly_land_burn_tree.md` does.**
> Referred to hereafter as **WO-760-B (VFX common attach architecture)**.
> Flagged by the 2026-08-16 Sunday board-grooming pass (`python tools/board_build.py` → `DUPLICATE_WO_NUMBERS`);
> ownership decided by **first-on-disk** (`git log --follow --diff-filter=A`): the winner's file was created first.
> Banner only — nothing was renumbered or deleted.

> ## RECONCILED 2026-08-08 - true status is PARTIAL
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: the ADR is ratified but the three files section 8 mandates (Vfx.cs, VfxBones.cs, VfxSocket.cs) do NOT exist in the tree.
> The previous Status line read "Status: RATIFIED (owner 2026-08-05) - ARCHITECTURE DECISION. This WO is the ADR (why + apply matrix)." and was wrong; the board overstated this.
> WARNING - WO NUMBER COLLISION: a SECOND work order is also numbered 760 (`WORK_ORDER_760_dragon_syndrath_fly_land_burn_tree.md`), and that dragon WO owns EVERY commit that cites WO-760. Do not read 760 commits as evidence for this ADR.

# WORK ORDER 760 — Common VFX attach class (architect determination)

**Status:** READY TO IMPLEMENT - partial (reconciled 2026-08-09 - this file's own line records the architecture decision as ratified but the implementation as not shipped, and no `.RESULT.md` exists. Note the WO-760 commits in git (`27de1aff`, `08b912bf`, `3dd024a9`) belong to the OTHER WO-760, `WORK_ORDER_760_dragon_syndrath_fly_land_burn_tree.md`. DUPLICATE NUMBER: two files claim 760)

**Status:** PARTIAL — ARCHITECTURE DECISION ratified, implementation not shipped. This WO is the ADR (why + apply matrix).
**⚠ The LOCKED implementation contract lives in WO-884 §0.2** and OVERRIDES this WO where they differ:
(1) canonical API = the fluent `Vfx.On(root).Add{Family}(element).OnBone(...).Play()` (the flat `Vfx.Projectile(...)`
here is optional 1:1 sugar); (2) resolution goes through `VfxElementTables` which DELEGATES to `SpellVfxFactory`
for Cast/Projectile/Impact; (3) **prefab policy = WO-884 §3 (duplicate pack → committed Resources via builder)
for shipped P1 — NOT §5.5's "prefer pack path"** (owner ruling, WO-785 survivability). Read WO-884 §0.2 first.  
**Classification:** VFX platform / low-cost reuse (not a single-content ship).  
**Silo:** Village combat / VFX / dungeon dress.  
**PO:** Elden.  
**Unity:** 6000.4.8f1 URP · project = this repo (root is machine-dependent; never hardcode a drive).  
**Depends on:** WO-759 (Particle Pack playbook), existing `VFXManager` bus (no second stack).  
**Supersedes for wiring style:** ad-hoc per-system VFX calls; **does not** replace `VFXManager` pooling.

**Owner steers (locked):**

1. **Architect-first** — one common, low-cost class applied everywhere; not five bespoke wirings.  
2. **API shape** — declarative one-liner: element + socket/bone, e.g. projectile fire on jaw.  
3. **Scope beyond breath** — turrets, spell cast, weapon skills (fire/ice/…), dungeon steam, flickering candles, boss breath as first continuous consumer.

---

## 0. One-paragraph mission (paste into agent prompt)

```
EoA WO-760: Design+implement ONE common VFX attach surface (VfxAttach / Vfx)
on top of VFXManager only — no second bus.
API: Vfx.Projectile(Fire, jawOrRoot) / Vfx.Cast / Vfx.Impact / Vfx.Loop / Vfx.Stream
with bone resolve by name (humanoid + deep search, shared with ActionBundlePlayer).
Recipes stay in VFXCatalog + Hovl catalog; ParticlePack is gitignored so catalog
may reference pack paths (null prefab → procedural, same as Spells/Mirza).
Apply the same class to: towers, HeroAbilities/weapons, dungeon candles+steam,
DragonBoss breath. Do not flatten multi-layer prefabs. Depth texture on DeNelle-URP.
```

---

## 1. Verified ground truth (do not re-guess)

| Fact | Evidence |
|------|----------|
| Canonical bus | `VFXManager` + `VFXType` + `VFXCatalog` + Hovl `PlayKey` string space |
| Catalog authored by script | `Assets/Editor/VFXCatalogGenerator.cs` → `VFX_CATALOG_OK` (not inspector drag) |
| ParticlePack **gitignored** | `.gitignore` → `Assets/UnityTechnologies/` |
| Implication | Catalog may **point at pack prefabs**; fresh clone → null prefab → procedural fallback (same as Spells/Mirza). **Do not require duplicating pack trees into Resources** for CI. Optional Resources copies only when you need a **game-tuned** variant (scale/layer strip). |
| Element router already exists | `SpellVfxFactory` + `SpellElement` → Cast/Projectile/Impact `VFXType` |
| Tower VFX already layered | `TowerCombat.FireAt` — VFXType muzzle + `PlayKey` element cast/impact |
| Spell cast path | `HeroAbilities` → cast/impact keys + `SpellVfxFactory` |
| Env component exists, underused | `EnvironmentVFX` — loop + offset; dungeon candles are meshes+lights today |
| Bone attach already solved once | `ActionBundlePlayer.ResolveAttachBone` (humanoid map + deep name search) — **extract, don’t fork** |
| Boss breath type exists | `VFXType.Boss_FireBreath` (append-only; FlameThrower recipe) |
| Soft particles | `DeNelle-URP` still `m_RequireDepthTexture: 0` until flipped |

**Landmine (WO-759 / WO-760):** inventing a third pool, direct `Instantiate` of pack prefabs outside `VFXManager`, or per-feature copy-paste of bone resolve.

---

## 2. Problem statement

Today VFX is **correctly centralized in `VFXManager`**, but **call-site UX is fragmented**:

| Surface | How callers attach |
|---------|-------------------|
| `VFXManager.Play` / `PlayAura` / `PlayProjectile` | Know `VFXType` + Transform |
| `VFXManager.PlayKey` | Know Hovl string keys |
| `SpellVfxFactory` | Element → type, but cast is **position-only** (hardcoded +1.2 Y), not bone |
| `EnvironmentVFX` | MonoBehaviour + `VFXType` only; no element, no bone name |
| `ActionBundlePlayer` | Keyword bundles; ability double-fire guard; bone resolve **private** |
| `TowerCombat` | Local `CastKeyFor` / `ImpactKeyFor` maps |

Owner wants **one cheap vocabulary**:

```text
add projectile (Fire) → attach to jaw bone
add cast (Ice) → hand.r
loop candle flame → tip socket
stream breath (Fire) → VFX_BreathSocket
impact (Fire) → hit point
```

…without each system re-learning catalogs, bones, loop vs oneshot, or quality.

---

## 3. Architecture decision (ADR)

### Decision

Introduce a **thin façade + shared bone resolver** in `DeNelle.Village`:

| Piece | Kind | Responsibility |
|-------|------|----------------|
| **`Vfx`** (static) | Façade | One-liners: Cast / Projectile / Impact / Loop / Stream / Burst |
| **`VfxSocket`** (MonoBehaviour) | Declarative component | Inspector: mode + element/type/key + bone name + offset; lifecycle Play/Stop |
| **`VfxBones`** (static) | Shared utility | Extracted from `ActionBundlePlayer.ResolveAttachBone` (+ jaw/mouth aliases) |
| **`VfxRecipe`** (optional small enum) | Semantic mode | Cast · Projectile · Impact · Loop · Stream · Burst |

**All spawning still goes through `VFXManager` only** (`Play`, `PlayAt`, `PlayProjectile`, `PlayAura`/`PlayEnvironment`, `PlayKey`).

`SpellVfxFactory` remains the **element → VFXType** map (or is called by `Vfx`).  
`EnvironmentVFX` either **delegates to `VfxSocket`** or is marked obsolete once `VfxSocket` covers it (migration, not big-bang delete).

### Why not alternatives

| Alternative | Reject reason |
|-------------|----------------|
| New pool / second manager | Violates one-bus rule; caps/quality already in VFXManager |
| Only extend EnvironmentVFX | Name is env-only; combat/projectile/cast won’t fit cleanly |
| Only JSON / ActionBundle for everything | Abilities already own cast VFX; towers aren’t action-keywords; overkill for candles |
| Per-system helpers forever | Owner explicitly rejected cost of N wirings |
| Always duplicate pack → Resources | Pack gitignored; generator null-safe path is the project norm |

### Cost model (why this is “low cost”)

| Cost | How we keep it low |
|------|---------------------|
| Runtime | Zero extra particles; one handle path already paid |
| Code | ~1 new file set; call sites become 1 line |
| Content | Reuse existing catalog rows; add Map lines in generators only when new types needed |
| Clone/CI | Missing pack → null prefab → procedural; no hard fail |
| Mobile | Existing `VFXQuality` + MinQuality + caps |

---

## 4. Target API (owner-shaped)

Namespace: `DeNelle.Village`. Prefer short name **`Vfx`** at call sites.

### 4.1 Static one-liners (combat / code)

```csharp
// --- Bone resolve once ---
Transform jaw = VfxBones.Resolve(dragonRoot, "jaw");      // aliases: jaw, mouth, chin, head
Transform hand = VfxBones.Resolve(heroRoot, "hand.r");

// --- Element recipes (delegate SpellVfxFactory → VFXManager) ---
VFXHandle trail = Vfx.Projectile(SpellElement.Fire, jaw);   // PlayProjectile(Projectile_FlameArrow, jaw)
Vfx.Cast(SpellElement.Fire, hand);                          // PlayCasting / Play at hand
Vfx.Impact(SpellElement.Fire, hitPoint);                    // oneshot world
Vfx.Impact(SpellElement.Ice, hitPoint);

// --- Semantic continuous / env (VFXType or future element→env map) ---
VFXHandle flame = Vfx.Loop(VFXType.Env_TorchFlame, candleTip);
VFXHandle steam = Vfx.Loop(VFXType.Env_GroundFog, floorAnchor); // or new Env_Steam type
VFXHandle breath = Vfx.Stream(VFXType.Boss_FireBreath, breathSocket); // PlayAura; aim socket yourself

// --- Optional Hovl key escape hatch (towers already use keys) ---
VFXHandle muzzle = Vfx.Key("PP_MuzzleFlash", firePos, parent: muzzleTf, scale: 1.2f);

// --- Stop ---
trail?.Stop();
breath?.Stop(immediate: true);
```

**Fluent form (optional sugar, same implementation):**

```csharp
Vfx.On(dragonRoot).Bone("jaw").Projectile(SpellElement.Fire);
Vfx.On(heroRoot).Bone("hand.r").Cast(SpellElement.Ice);
Vfx.At(hitPoint).Impact(SpellElement.Fire);
```

Implement fluent only if it stays a thin wrapper (no new state machine).

### 4.2 Component form (`VfxSocket`) — props / prefabs / dungeon dress

```
[AddComponentMenu("Defenders/VFX/Vfx Socket")]
VfxSocket
  Mode: Loop | Stream | (optional OnEnableBurst)
  Source: VFXType | HovlKey | Element+Recipe
  BoneName: "" | "jaw" | "hand.r" | "VFX_BreathSocket"
  LocalOffset: Vector3
  Follow: bool (default true)
  PlayOnEnable: bool (default true)
  Scale: float (1)
```

**Usage:**

| Prop | Setup |
|------|--------|
| Dungeon candle | Mode=Loop, Type=`Env_TorchFlame` (or Candles pack via catalog), Bone="", Offset tip |
| Steam vent | Mode=Loop, Type=new `Env_Steam` → RisingSteam/PressurisedSteam pack path |
| Weapon elemental idle (optional) | Mode=Loop, Element=Fire, Recipe=Projectile or Loop, Bone=`hand.r` |
| Boss breath socket marker | Mode=Stream, Type=`Boss_FireBreath`, PlayOnEnable=**false** (code drives) |

Lifecycle: `OnEnable` → start if PlayOnEnable; `OnDisable`/`OnDestroy` → `Stop`. Same pool discipline as `EnvironmentVFX`.

### 4.3 What the façade does **not** do

- No damage, no hit detection, no ability cooldowns  
- No rewriting Particle Shape for aim (caller rotates socket; WO-759)  
- No flattening multi-layer prefabs  
- No Animator ownership (that stays `ActionBundlePlayer` / `ActorAnimator`)

---

## 5. Internal design

```
                    ┌─────────────────────────────────────┐
  Call site         │  Vfx / VfxSocket / SpellVfxFactory  │
  (1 line)          │  VfxBones.Resolve(root, "jaw")      │
                    └───────────────┬─────────────────────┘
                                    │ VFXType or Hovl key
                                    ▼
                    ┌─────────────────────────────────────┐
                    │           VFXManager                │
                    │  Play / PlayAura / PlayProjectile   │
                    │  PlayKey / quality / pools / URP    │
                    └───────────────┬─────────────────────┘
                                    │
                    ┌───────────────┴─────────────────────┐
                    │ VFXCatalog (VFXType)  HovlVfxCatalog │
                    │ Lana / Spells / Resources / Pack*   │
                    └─────────────────────────────────────┘
                    * ParticlePack paths OK; gitignored → null-safe
```

### 5.1 Recipe → manager method

| Recipe | Continuous? | Manager API | Catalog `IsLoop` |
|--------|-------------|-------------|------------------|
| **Cast** | No (short) | `Play` / `PlayCasting` / `PlayAt` | false |
| **Impact** / **Burst** | No | `Play` / `PlayImpact` | false |
| **Projectile** | Yes (until hit) | `PlayProjectile` | true |
| **Loop** (env/aura) | Yes | `PlayEnvironment` / `PlayAura` | true |
| **Stream** (breath) | Yes (until Stop) | `PlayAura` | true |
| **Key** | Row decides | `PlayKey` | row.IsLoop |

### 5.2 Element maps (reuse, don’t fork)

| Element | Cast | Projectile | Impact |
|---------|------|------------|--------|
| Fire | `Cast_FireCharge` | `Projectile_FlameArrow` | `Impact_Flame` / explosion for meteor |
| Frost | `Cast_FrostNova` | `Projectile_FrostBolt` | `Impact_Ice` |
| Arcane | `Cast_MageCharge` | `Projectile_ArcaneBolt` | `Impact_Aether` |
| Holy | `Cast_Heal` | (none / soft) | `Impact_Heal` |
| Physical | `Cast_KnightSlam` | Arrow if ranger | `Impact_Physical` |

**Implementation:** `Vfx.Cast/Projectile/Impact(SpellElement, …)` **calls `SpellVfxFactory`** (extend factory with bone-aware overloads if needed). One map only.

### 5.3 Bone aliases (`VfxBones`)

Extract from `ActionBundlePlayer` + add pack-relevant names:

| Alias | Resolve |
|-------|---------|
| `hand.r` / `hand_r` / `righthand` | HumanBodyBones.RightHand |
| `hand.l` / … | LeftHand |
| `head` | Head |
| `jaw` / `mouth` / `chin` | name search (non-humanoid dragons) then Head fallback |
| `VFX_BreathSocket` | exact name search (authored empty) |
| `chest` / `spine` / `hips` | existing |
| empty | actor root |

**Single implementation:** `ActionBundlePlayer` calls `VfxBones.Resolve` (delete private duplicate).

### 5.4 Aim (streams / cones)

Not part of `Vfx` spawn API beyond parenting:

```csharp
// Caller owns aim (DragonBoss / tower muzzle orient)
socket.rotation = Quaternion.LookRotation(target - socket.position);
var h = Vfx.Stream(VFXType.Boss_FireBreath, socket);
```

Optional later: `Vfx.Aim(socket, target, lerp)` helper — pure math, no particles.

### 5.5 Catalog / Particle Pack policy (amends WO-759 §1 slightly)

| WO-759 said | WO-760 refinement |
|-------------|-------------------|
| Duplicate pack → Resources always | **Prefer catalog Map → pack path** when gitignored pack is present |
| | Duplicate to `Resources/VFX/...` only for **tuned** variants (boss scale, medium quality child strip) |
| Never reimport pack | Unchanged |
| Never flatten | Unchanged |

Generator rule for new rows (example):

```csharp
// VFXCatalogGenerator.Map — ParticlePack paths allowed; null on clone is OK
private const string PP = "Assets/UnityTechnologies/ParticlePack/EffectExamples/";

{ "Boss_FireBreath", new Pick(
    PP + "Fire & Explosion Effects/Prefabs/FlameThrower.prefab",
    isLoop: true, minQuality: 1, poolSize: 2) },

{ "Env_TorchFlame", new Pick( /* existing Lana OR */ 
    PP + "Misc Effects/Prefabs/Candles.prefab", isLoop: true, minQuality: 1) },

// New types as needed:
// Env_Steam → Smoke & Steam Effects/Prefabs/RisingSteam.prefab or PressurisedSteam
// Projectile tower fire already via Spells/Lana — only remap if owner wants pack FireBall
```

Hovl generator may add `PP_*` keys for muzzle/steam if towers stay on `PlayKey` — **or** migrate towers to `Vfx.Cast`/`Vfx.Impact` over time.

---

## 6. Apply matrix (same class, five domains)

| Domain | Today | After WO-760 |
|--------|-------|----------------|
| **Boss breath** | Instant `DealStrike` | `Vfx.Stream(Boss_FireBreath, VfxBones.Resolve(root,"VFX_BreathSocket"\|\|"jaw"))` + timer Stop (WO-757 numbers) |
| **Hero spells / weapons** | `SpellVfxFactory.PlayCast` at torso+1.2 | `Vfx.Cast(element, VfxBones.Resolve(hero,"hand.r"))`; impact unchanged world; projectile `Vfx.Projectile(element, projTf)` |
| **Towers** | `Play` + `PlayKey(CastKeyFor)` | Prefer `Vfx.Cast`/`Vfx.Impact` by `DamageElement`→`SpellElement` map **or** `Vfx.Key` wrapper so maps live in one place |
| **Dungeon candles** | Mesh + point light | DressRoom adds `VfxSocket` Loop torch/candles + keep light |
| **Dungeon steam** | None / fog only | `VfxSocket` Loop `Env_Steam` on vent markers |
| **Action bundles** | Private bone resolve | Use `VfxBones`; optional `Vfx.Key(row.vfxKey, bone)` |

### 6.1 Element bridge for towers

```csharp
// Vfx or small helper
SpellElement FromDamage(DamageElement d) => d switch {
    DamageElement.Flame  => SpellElement.Fire,
    DamageElement.Ice    => SpellElement.Frost,
    DamageElement.Aether => SpellElement.Arcane,
    _                    => SpellElement.Physical,
};
```

---

## 7. File plan (implementation phase)

| Action | Path |
|--------|------|
| **Add** | `Assets/_Modules/Village/Vfx/Vfx.cs` — static façade |
| **Add** | `Assets/_Modules/Village/Vfx/VfxBones.cs` — bone resolve |
| **Add** | `Assets/_Modules/Village/Vfx/VfxSocket.cs` — component |
| **Optional** | `Vfx.Fluent.cs` partial if sugar wanted |
| **Edit** | `ActionBundlePlayer.cs` — call `VfxBones` |
| **Edit** | `SpellVfxFactory.cs` — bone/Transform overloads; keep maps |
| **Edit** | `EnvironmentVFX.cs` — delegate to `Vfx.Loop` **or** obsolete + migrate |
| **Edit** | `VFXCatalogGenerator.cs` — pack rows for breath/steam/candles as approved |
| **Edit** | `VFXType.cs` — only append (`Env_Steam`, etc.) |
| **Edit** | Consumers: `DragonBoss`, `HeroAbilities` (cast attach), `TowerCombat` (thin), dungeon dress |
| **Edit** | `DeNelle-URP.asset` — depth on |
| **Do not** | New manager, new pool, pack reimport |

Approx size: façade ~150–250 LOC; bones ~80 LOC; socket ~120 LOC. Call-site diffs should shrink over time.

---

## 8. Implementation order (for coding agent)

### Phase 0 — accept architecture (this WO)

Owner ACK on API names (`Vfx` vs `VfxAttach`) and component name.

### Phase 1 — platform (no gameplay feel change required)

1. `VfxBones` extract + unit-style smoke (editor or regression)  
2. `Vfx` façade wrapping existing manager + SpellVfxFactory  
3. `VfxSocket` component = EnvironmentVFX feature parity  
4. Point `EnvironmentVFX` internals at façade (compat)  
5. `ActionBundlePlayer` → `VfxBones`

### Phase 2 — catalog recipes (Particle Pack where wanted)

6. URP depth flag  
7. Generator rows: `Boss_FireBreath` → FlameThrower; `Env_Steam`; candle/torch pick  
8. Run `VFXCatalogGenerator.Generate` → `VFX_CATALOG_OK`

### Phase 3 — apply (same API)

9. **DragonBoss.FireBreath** stream (WO-757 timing) via `Vfx.Stream`  
10. **HeroAbilities** cast → hand bone via `Vfx.Cast`  
11. **TowerCombat** route element flashes through `Vfx` (keep fallback)  
12. **DungeonSceneBuilder.DressRoom** / LitFixture → add `VfxSocket` for candles + steam markers  

### Phase 4 — RESULT

13. `WORK_ORDER_760_vfx_common_attach_architecture.RESULT.md`  
14. Cross-link WO-757/759 RESULT if breath ships here

---

## 9. Acceptance

### Architecture

- [ ] Any new VFX call site can use `Vfx.*` without touching pools  
- [ ] Bone resolve lives in **one** type (`VfxBones`)  
- [ ] Element maps live in **one** place (`SpellVfxFactory`)  
- [ ] Zero new VFX buses  

### Functional samples (minimum)

- [ ] `Vfx.Projectile(Fire, transform)` attaches trail; Stop cleans pool  
- [ ] `Vfx.Cast(Ice, handBone)` plays at hand, not feet  
- [ ] `VfxSocket` on a candle loops without custom script  
- [ ] Boss breath uses `Vfx.Stream` + socket aim (if Phase 3)  
- [ ] Fresh clone without ParticlePack still runs (procedural / missing row soft)  

### Non-goals (this WO)

- Replacing all historical `PlayKey` strings in one PR  
- Full Hovl→VFXType unification  
- Authoring new particle graphs from scratch  

---

## 10. Anti-patterns (review fail)

| Don’t | Do |
|-------|-----|
| `Instantiate(FlameThrower)` in gameplay | `Vfx.Stream` / catalog pool |
| Copy bone search into TowerCombat / DragonBoss | `VfxBones.Resolve` |
| Second element→prefab dictionary in towers | Bridge to `SpellElement` + factory |
| Flatten FlameThrower children | Quality-disable children only |
| Require pack present to compile | Null-safe catalog |
| `Vfx` owns damage timing | Boss/ability code owns timers; Vfx only Play/Stop |

---

## 11. Relationship to other WOs

| WO | Relation |
|----|----------|
| **759** | Recipe/sequence knowledge; pack tables; URP; breath spatial model |
| **757** | Breath timing numbers + acceptance; implement **using `Vfx.Stream`** after Phase 1 |
| **754** | VFX Caster audition of pack multi-layer |
| **504** | Catalog generator pattern this extends |
| **195** | SpellVfxFactory — keep as map, extend call surface |
| **671** | ActionBundlePlayer bone logic → shared `VfxBones` |

---

## 12. Open choices for owner (pick if you care; defaults in bold)

1. Static class name: **`Vfx`** vs `VfxAttach` vs `VfxApi`  
2. Component name: **`VfxSocket`** vs `VfxBind`  
3. Towers: migrate to element façade **now** vs wrap `PlayKey` only first  
4. Candles art: **Lana torch** vs Particle Pack `Candles` prefab  
5. Steam: **RisingSteam** vs PressurisedSteam  

Defaults keep shipping unblocked.

---

## 13. RESULT template

```markdown
# WO-760 RESULT

## Platform
- [ ] Vfx.cs / VfxBones.cs / VfxSocket.cs paths
- [ ] ActionBundlePlayer uses VfxBones
- [ ] SpellVfxFactory bone overloads (yes/no)

## Catalog
- [ ] Generator rows added (list)
- [ ] URP depth flag

## Applied call sites
- [ ] DragonBoss / HeroAbilities / TowerCombat / Dungeon dress

## API final names
- …

## Follow-ups
- …
```

---

## 14. Copy-paste for implementing agent

```
Implement WO-760 in the EoA repo (paths repo-root-relative; the root is machine-dependent):

1) Add VfxBones (extract ActionBundlePlayer bone resolve; add jaw/mouth/chin/VFX_BreathSocket).
2) Add static Vfx façade: Cast/Projectile/Impact/Loop/Stream/Key → VFXManager only;
   element methods call SpellVfxFactory maps.
3) Add VfxSocket MonoBehaviour (Loop/Stream, type or key or element, bone name, offset,
   PlayOnEnable, Stop on disable) — EnvironmentVFX parity.
4) Do NOT add a second pool/manager. Do NOT reimport ParticlePack.
5) Catalog: optional Map lines to gitignored ParticlePack paths (FlameThrower→Boss_FireBreath
   IsLoop; steam/candles as approved). Generator null-safe.
6) Then wire: DragonBoss breath Stream; hero cast on hand bone; dungeon VfxSocket candles/steam;
   towers thin-wrap through Vfx if cheap.
7) Soft particles: DeNelle-URP m_RequireDepthTexture=1.
8) RESULT.md when done.

API target:
  Vfx.Projectile(SpellElement.Fire, VfxBones.Resolve(root, "jaw"));
  Vfx.Stream(VFXType.Boss_FireBreath, socket);
  Vfx.Cast(SpellElement.Ice, hand);
  Vfx.Impact(SpellElement.Fire, hitPos);
  Vfx.Loop(VFXType.Env_TorchFlame, tip);
```

---

**One-line decision:**  
**`Vfx` + `VfxBones` + `VfxSocket` are the single low-cost attach layer; `VFXManager` remains the only engine; element recipes stay in `SpellVfxFactory`; Particle Pack is catalog-referenced (gitignored-safe); every feature (breath, towers, spells, candles, steam) is an application of that one API.**
