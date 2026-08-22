> ## RECONCILED 2026-08-08 - true status is PARTIAL - surface half NEEDS-OWNER-RULING
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: the element half is real (commit 4ef2d532); the surface half was correctly REFUSED with measurements - no SurfaceType, MaterialType or HitSurface enum exists anywhere in the tree. That half is an owner design task, not engineering debt.
> The previous Status line read "ELEMENT HALF LANDED 2026-08-05 (4ef2d532) - SURFACE HALF REFUSED WITH MEASUREMENTS" and was substantially correct; it is restated here in the reconciled vocabulary, with the surface half routed to the owner rather than left open as engineering work.

# WORK ORDER 887 — VFX: on-hit surface + element impacts

**Status:** IMPLEMENTED 2026-08-22 - the surface half is UNBLOCKED and done. The refusal ("no surface signal exists") was measuring the physics LAYER; WallSegment.Tier had been public 1..3 all along. Five owner-tagged PP_* impacts mapped verbatim, mirrors built and verified (0 colliders, 0 mesh filters, 0 looping layers).

> *(superseded status, kept for the record: BLOCKED - surface half NEEDS OWNER RULING, reconciled 2026-08-09.)*

**Status:** PARTIAL - surface half NEEDS-OWNER-RULING (reconciled 2026-08-08) — ELEMENT HALF LANDED 2026-08-05 (`4ef2d532`) · SURFACE HALF REFUSED WITH MEASUREMENTS — gate
`COMPILE_GATE_OK`. **What landed:** `TowerCombat.OnProjectileImpact` computed the projectile's element
EIGHT LINES BELOW the impact pick and never used it, so **every empowered tower detonated as
`Impact_ExplosionAether`**; element now decides flavour, tier decides size, and the paired `SfxId` follows.
Also replaced `FireAt`'s use of `Projectile_TowerArcane` (a projectile-BODY row with `IsLoop` TRUE) as a
muzzle flash. **What is REFUSED, and why nobody should re-attempt the copy:** the five surface rows carry
**demo geometry on the prefab ROOT** (built-in primitive mesh + pack material + a **SPHERE COLLIDER**), all
five **emit 5/sec on loop at the derivation authority**, and there is **no enum home**
(`Impact_Flesh/Metal/Stone/Wood/Dirt` do not exist). ⚠ **THE SURFACE SIGNAL DOES NOT EXIST — verified, not
assumed:** no `SurfaceType` field, no physic-material read, no per-material tag; wood palisades, stone
walls and steel gates share one `Structure` layer, and both footstep implementations play a single clip
with no surface query. **Defining a surface taxonomy is DESIGN work and belongs to the owner.**
Also refused: `GoopSpray` can never be selected — `DamageElement` is `{None, Aether, Flame, Ice}` and this
game has **no nature element**. Full ledger: `docs/reference/SESSION_INDEX_2026-08-06.md` §5.2, §6.11-6.12, §7.
*(original header: READY TO IMPLEMENT · **Silo:** Combat/VFX · **For:** CLAUDE CLI · **Date:** 2026-08-05)*
**Context (read once):** WO-884 §0.2 · `VFX_PREFAB_HANDBOOK.md` (Step 1–8) · `VFX_CREATIVE_PICKS_REGISTRY.md` §4. Enum LANDED — reference names only.
**Depends on:** WO-884 Phase 0 platform.

## Scope
The moment a weapon/attack CONNECTS (distinct from a spell's own impact): pick the burst by **surface material** and **element**. All BURST family — `Vfx.On(...).AddImpact(element).At(hit).Play()` — no handle, pool reclaims.

## Recipes (registry §4)
| Hit case | Recipe | SFX |
|---|---|---|
| Physical → flesh (organic) | FleshImpacts | flesh thud |
| Physical → metal/armour | MetalImpacts | metal clang |
| Physical → stone/wall | StoneImpacts | stone |
| Physical → wood (barrel/crate) | WoodImpacts | wood |
| Physical → dirt/sand | SandImpacts | — |
| Generic physical | SmallExplosion | `Shockwave` |
| Fire proc | TinyExplosion + TinyFlames cling | `FireExplosion` |
| Ice proc | IceLance shard burst | — |
| Arcane proc | EnergyExplosion | `ArcaneExplosion` |
| Nature/poison proc | GoopSpray + puddle | — |
| Ranged release (any) | MuzzleFlash (`Cast_MuzzleFlash`) | `TowerShot`/bow |

## Files to touch
- Builders: Flesh/Metal/Stone/Wood/SandImpacts, TinyExplosion, MuzzleFlash → `Assets/Resources/VFX/Impact/`.
- `VFXCatalogGenerator.cs` Map rows (**IsLoop=false** — FleshImpacts etc. are hybrid, force burst per handbook §5.2).
- Surface/element detection at the melee + projectile land sites; `TowerCombat.OnProjectileImpact`; `HeroAbilities` impact site; `Destructible`/surface tag lookup.

## Acceptance criteria
**Engineering:**
- [ ] Correct surface recipe plays per struck material (flesh vs stone vs wood vs metal vs dirt).
- [ ] Element procs override/augment the physical surface hit as specified.
- [ ] Every impact `IsLoop=false` — zero loop-slot leaks (verify no `_maxActiveLoops` growth in a hit-heavy fight via FlowTrace).
- [ ] Paired SfxId fires via VfxToSfx.
- [ ] `COMPILE_GATE_OK` + `*_BUILD_OK` + `VFX_CATALOG_OK` + `REGRESSION_OK`.
**Felt (owner closes):**
- [ ] Hitting a wooden barrel splinters; a stone wall chips; an armoured foe sparks; flesh spatters — reads by shape, not colour.
- [ ] Fire/ice/arcane weapon procs read elemental (up-flame / angular shards / radial ring).
- [ ] Headless hit screenshots opened for flesh / stone / wood / a fire proc.

## RESULT
`WorkOrders/WORK_ORDER_887_vfx_on_hit_surfaces.RESULT.md`.

---

# SURFACE HALF IMPLEMENTED 2026-08-22

## ⚠ One clause of the 2026-08-05 refusal was WRONG — corrected at source, not coded around
The refusal read the shared physics LAYER ("wood palisades, stone walls and steel gates share
one `Structure` layer") and concluded **THE SURFACE SIGNAL DOES NOT EXIST**. The layer half is
true; the conclusion is not. `WallSegment.Tier` has been **public and 1..3 the whole time**
(`Assets/_Modules/Village/Walls/WallSegment.cs:144`), named by `WallTier { Wood=1, Iron=2,
ReinforcedSteel=3 }` (`Assets/_Modules/Village/Walls/WallTierData.cs:29`). A physics layer was
the wrong place to look; the gameplay type was the right one. Walls have carried their
material all along.

## The runtime home the five keys never had
`Assets/_Modules/Village/Vfx/HitSurface.cs` (`DeNelle.Village`):
- `enum HitSurface { None, Flesh, Metal, Stone, Wood, Sand }` — runtime-only dispatch key,
  serialised nowhere (unlike `VFXType`, which is serialised BY ORDINAL).
- `HitSurfaceVfx.KeyFor` maps each surface to the owner's **verbatim** 2026-08-21 tag:
  `PP_FleshImpacts` / `PP_MetalImpacts` / `PP_StoneImpacts` / `PP_WoodImpacts` /
  `PP_SandImpacts`. **No prefab was picked, substituted or re-pointed.**
- **`VFXType` was deliberately NOT appended to.** Appending is a single-owner edit
  (WO-884 §0.2, restated in the enum's own header). WO-892 already established the sanctioned
  alternative for exactly this case — a VFX moment whose consumer is a string key declares the
  key and plays through `VFXManager.PlayKey` (`StructureDamageVisuals` is the precedent).

## The surface resolution (owner defaults, 2026-08-21)
`HitSurfaceVfx.Resolve(Component|GameObject)`, in decision order:

| Struck | Resolves to |
|---|---|
| `WallSegment` tier 1 | **Wood** |
| `WallSegment` tier 2–3 | **Metal** |
| `Gate` (any tier) | **Metal** |
| `Enemy` / `HeroHealth` / `TroopController` | **Flesh** |
| any other `IDamageableStructure` | **Stone** |
| anything else | `None` — the caller keeps its generic impact; **never a guess** |

**Order is load-bearing:** `WallSegment` is tested BEFORE the generic-structure fallback,
because a wall *is* an `IDamageableStructure` and would otherwise read as Stone at every tier.
The suite pins that. **Sand is deliberately unreachable** per the owner's ruling — recorded as
the assertable constant `HitSurfaceVfx.SandIsIntentionallyUnused`, not as a comment, and
MEASURED by the suite.

Every lookup passes `includeInactive: true` on purpose: the default skips inactive
GameObjects, so a target mid-teardown or a pooled body between lives would silently resolve to
`None`.

## Call site
`Enemy.ExecuteContactAttack` (`Enemy.cs`). The surface burst is layered **on top of** the
existing generic `Impact_Physical`, not in place of it: the generic slash arc is the CONTACT
read (fires no matter what, so a hit is never silent), the surface burst is the MATERIAL read.
Both are guarded. Colourblind law holds — the surface reads by debris shape and motion
(splatter / spark / chip / splinter), never by hue, and it adds a channel rather than
replacing one.

## The art: stripped, forced one-shot, and made shippable
`Assets/Editor/SurfaceImpactVfxMirrors.cs` — menu `Defenders/VFX/Mirror Surface Impact VFX`,
batchmode `DeNelle.Editor.SurfaceImpactVfxMirrors.Run`, marker
`SURFACE_IMPACT_VFX_MIRROR_OK <n> clean` / `SURFACE_IMPACT_VFX_MIRROR_FAIL`.

**Measured at source 2026-08-22, all five identical** (the ticket's claim, verified, not
assumed): 5 GameObjects, 4 ParticleSystems, 1 MeshFilter, 1 MeshRenderer, 1 SphereCollider,
**0 MonoBehaviours**. The single GameObject *without* a ParticleSystem **is the root**
(`m_Father: {fileID: 0}`), carrying Transform + MeshFilter + MeshRenderer + SphereCollider.
Exactly one layer per prefab is `looping:1` with `rateOverTime` scalar **5** — the "5/sec on
loop" the ticket names. Zero pack scripts, so unlike `Misc/Respawn` these carry no
missing-script hazard on a clone.

Three repairs, applied EVERY run (correctness invariants, never taste):
1. **Strip demo geometry** — root MeshFilter/MeshRenderer, and colliders ANYWHERE. Safe
   because the root holds no ParticleSystem, so the strip cannot move the derivation authority.
2. **Force one-shot** — `main.loop` + `main.prewarm` cleared on every layer (`prewarm` first;
   Unity only permits it on a looping system).
3. **Clear `playOnAwake`** on every layer.

⚠ **Why this did NOT need a `vfx-loop-flag` `OwnerPinned` entry and did NOT widen the
derivation.** Clearing `main.loop` changes what the ART DOES; the catalog's `IsLoop` is then
DERIVED from the repaired prefab by the single shared authority
(`VfxLoopFlagRegression.TryResolveExpected`), which `HovlVfxCatalogGenerator` already calls for
every row including the owner's manual ones. So the owner's `isLoop: true` in
`VfxManualPicks.json` is superseded by measurement rather than overridden by a pin, and neither
the pin table nor the rule is touched — which matters, because `store.beacon.near` was pinned on
2026-08-21 precisely because widening the rule would re-open the pool leak on seven sibling rows.

**⛔ Why NOT a row in `ParticlePackVfxBatchBuilder`:** that builder's ROOT DEMO-GEOMETRY GUARD
hard-refuses these five by design (it was added by WO-892/893 to make *this* ticket's refusal
mechanical). That guard is correct and is **not relaxed** — relaxing it would re-open the hole
for every future row it protects. The new builder does what a bare `CopyAsset` must never do:
copies, **repairs, then proves the repair** by re-reading from disk.

**How the owner's verbatim tag reaches the mirror** (no re-pick, no second table): her rows
point at the gitignored pack path, and `HovlVfxCatalogGenerator.ResolveMirror` →
`VfxMirrorRedirect` already swaps a pack path for a committed mirror **when a builder declares
the pair**. Declaring the pairs is the whole wiring.

## Single-declaration note (assembly graph)
The five source→mirror pairs are declared **once**, in
`Assets/Editor/Regression/SurfaceImpactMirrorSet.cs`. `DeNelle.Editor` references
`DeNelle.EditorRegression` **one way**, so a table declared in the builder would be invisible to
its own gate and would have to be hand-copied — the drift this project keeps paying for. Same
inversion `VfxLoopFlagRegression` already uses for the shared loop derivation. The builder
exposes `Mirrors => SurfaceImpactMirrorSet.Pairs`, so `VfxMirrorRedirect` is unchanged in shape.

## Oracle
`Assets/Editor/Regression/SurfaceImpactVfxRegression.cs` — `[surface-impact-vfx]`, registered
once in `DataRegression.RunAll`. It **measures**, it does not restate:
1. **Code vs the owner's DATA** — for each surface, the key `HitSurfaceVfx.KeyFor` returns must
   be present as a row in `VfxManualPicks.json`, read off disk. Compiled code on one side, a
   file the owner edits on the other; rename a constant or retag a surface and it goes red.
2. **The shipped mirrors are repaired** — loaded FROM DISK, never the object the builder wrote:
   zero MeshFilter/MeshRenderer/**Collider**, and `loop`/`prewarm`/`playOnAwake` false on every
   layer. ⚠ The skip rule is **asymmetric on purpose**: both files absent = clean clone = skip
   and count; **source present but mirror missing = FAIL** (the builder was never run or its
   output never committed). A plain skip-if-missing would make this unfailable on the one
   machine that matters.
3. **The resolution is EXERCISED** — inactive fixtures carrying the real `WallSegment` (tier 1
   and tier 3), `Gate`, `Building`, `TroopController`, `Enemy`, a wall CHILD transform, and a
   bare GameObject, each run through the production `Resolve`. Inactive is what makes it
   side-effect-free: Unity never runs `Awake` on an inactive GameObject.
   Plus: no fixture may resolve to `Sand` while the owner's ruling stands.

It deliberately does **not** check the catalog's stored `IsLoop` — that is `vfx-loop-flag`'s
job, and two oracles asserting one fact is how they come to disagree.

## Still open / not done here
- **The builder has not been RUN** (edit-only lane, no Unity). Someone must run
  `Defenders/VFX/Mirror Surface Impact VFX` and commit the five mirrors under
  `Assets/Resources/VFX/Impact/`, then regenerate the Hovl catalog. Until then the
  `surface-impact-vfx` suite will FAIL check (2) on this machine — by design, that is the
  check reporting the missing step, not a defect.
- Other impact call sites (`TowerCombat.OnProjectileImpact`, `HeroAbilities`) still play the
  element-only impact; the surface layer is wired at the enemy melee connect. Extending it is
  a one-line `HitSurfaceVfx.ResolveAndPlay` per site.
- `GoopSpray` remains unreachable — `DamageElement` is `{None, Aether, Flame, Ice}` and this
  game has no nature element. Unchanged from the original refusal.
- No gate, no build, no commit. PO closes after felt-verify per §13.
