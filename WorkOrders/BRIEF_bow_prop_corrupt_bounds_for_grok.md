# BRIEF FOR GROK — `BowProp` corrupt renderer bounds (`bounds.min.y = -33.56`)

**Status:** diagnosed, NOT fixed. Root fix is an ASSET IMPORT setting + a frozen prefab AABB,
not application code. No WO number minted (the `CLI_LANES_WO_NUMBERS.md` banner is the sole
numbering authority — take one from there if this becomes a WO).

**Project:** Echoes of Elarion / Defenders of the Realm. Unity 6000.4.8f1, URP, Android/IL2CPP,
Solana Seeker (2670x1200 landscape). Repo root `D:\EoA`.

---

## 1. The symptom, as captured on device

Build 313794, pid 2418, 2026-08-06. Real logcat lines, not reconstructed:

```
[Flow:Enemy] SnapBodyToGround(ArenaEnemy_orc-shaman_0): footGap 36.56m is absurd
             (corrupt renderer bounds?) - lowest=BowProp (SkinnedMeshRenderer)
             bounds.min.y=-33.56, pivotY=3.00 - capped to 3m.
[Flow:Enemy] SnapBodyToGround(ArenaEnemy_orc-shaman_0) ground=0.00 footGap=3.00
             -> pivotY=3.00 (visible bottom rests on surface)
```

The engine believes a bow held by an orc extends **33.56 metres below the world origin**. The
ground-snap routine then tries to raise the enemy ~36 m so that its "lowest visible point" rests
on the ground.

**It has happened before.** `Enemy.cs:2990-2991` records a PRIOR incident on the same enemy family
with `footGap 53.58`. The clamp described in section 4 was added in response to that one. This is
the second occurrence.

---

## 2. Root cause — verified at source

### 2a. A static prop is being imported as a skinned mesh

`Assets/Resources/Heroes/Props/Bow.fbx.meta:101`

```
animationType: 2
```

`animationType: 2` = **Generic** rig. That instructs Unity to import the mesh as a
`SkinnedMeshRenderer` bound to a skeleton. The bow is a **static prop** — it has no animation and
no bones that matter. It should be `animationType: 0` (None), which yields a plain `MeshRenderer`.

This is NOT caused by the builder script. `Assets/Editor/BowPropBuilder.cs:50` merely instantiates
the FBX and `:66` saves the prefab. The defect is the import setting on the FBX.

### 2b. The prefab's bounds are FROZEN and half-written

`Assets/Resources/Heroes/Props/Bow.prefab`

| line | field | value | meaning |
|---|---|---|---|
| 51-54 | `m_DirtyAABB` | `0` | **Bounds are frozen.** Unity will never recompute them. |
| 63-74 | `m_AABB.m_Extent` | `(0.48348814, 0.07779948, 0.99185216)` | local size `(0.967, 0.156, 1.984)` |
| 56-62 | `m_AABB.m_Center` | `.x` and `.y` overridden, **`.z` is NOT** | a partially-written override |

So the stored bounds are (a) never refreshed and (b) internally inconsistent.

### 2c. Why that produces garbage at runtime

A `SkinnedMeshRenderer`'s world-space `bounds` are derived from its stored `localBounds` **via the
root bone**, *not* from the renderer's own transform. So a frozen/incorrect `localBounds` yields a
world AABB that does not track the visible geometry at all.

This interacts badly with the attach code:
`Assets/_Modules/Village/Hero/HeroBowAttachment.cs` — `NormalizeInto` **measures** with `r.bounds`
(`:213`) while **writing** `prop.transform.localScale` (`:196`). It measures one space and corrects
another. With a well-formed `MeshRenderer` that is merely sloppy; with this frozen skinned AABB it
is unsound.

**HYPOTHESIS (arithmetic, not executed):** if the frozen AABB does not rotate with the axis-align
at `:186-192`, then `b1.size.y` reads the thin `0.156` axis instead of the `1.984` long axis, giving
`0.92 / 0.156 ~= 5.9x` and a bow rendering ~11.7 m. The observed `-33.56` requires one further
compounding that cannot be derived by static reading. **The ingredients are verified; the exact
product is not.** Do not present the 5.9x figure as fact.

---

## 3. What is ALREADY FIXED — do not redo

Commit `7d79d3da` fixed a **separate, independent** defect on the same prop: the parent-scale
compensation. `HeroBowAttachment.cs:153` did `SetParent(bone, false)` without dividing out the
bone's `lossyScale`, so a 0.92 m bow rendered at `0.92 * 1.887 = 1.74 m` on an orc whose visual
root carries a 1.887x fit factor (`[Flow:EnemySize] orc-shaman ... scale=1.887`).

That is fixed. **It does not fix this ticket** — the corrupt bounds survive it.

---

## 4. The guard currently masking this — do not rely on it

`Assets/_Modules/Village/Enemies/Enemy.cs:2995-3009` — `MaxFootGap = 3f` clamps the absurd
correction. It is the only reason the orc is 3 m off the ground rather than 36 m in the air.

That clamp is a band-aid added after the previous incident. The bug must be fixed at the asset,
not left riding on the cap.

---

## 5. Requested fix

1. Set `Assets/Resources/Heroes/Props/Bow.fbx.meta` `animationType: 0` (None) so the prop imports
   as a `MeshRenderer`, not a `SkinnedMeshRenderer`.
2. Let the prefab's bounds recompute — i.e. remove the frozen `m_DirtyAABB: 0` / repaired the
   half-written `m_AABB` in `Bow.prefab:51-74`. Re-generating the prefab via
   `Assets/Editor/BowPropBuilder.cs` after the import change is likely cleaner than hand-editing
   the prefab YAML.
3. Re-verify which axis `HeroBowAttachment.NormalizeInto` measures afterwards (`:186-213`) — the
   axis-align assumption should be re-checked against real bounds, not assumed correct.

**Acceptance:** on device, `SnapBodyToGround` for a bow-carrying enemy reports a `footGap` under
0.5 m with NO "absurd (corrupt renderer bounds?)" warning, and the bow reads at roughly
`BowHeldLength` (0.92 m; `HeroBodySwapper.cs:940`, `RangerBodyBuilder.cs:219`) against a 1.90 m orc.

**Do NOT:** hand-edit `.unity` scene files (corruption history). Do not reimport the Particle Pack.
Verify by GATE MARKER (`COMPILE_GATE_OK`) and captured device data, never by an exit code.

---

## 5b. BLAST RADIUS — answered (added 2026-08-06, per Grok's "scan other props" ask)

Grok asked for a sweep so this does not return next week as staff/axe. Done, and the answer is
**narrower than feared but includes one asset nobody has reported yet.**

**Risk factor, NOT proof — 136 FBXs under `Assets/Resources` import with a rig
(`animationType: 1/2/3`)**, including every weapon prop (`axe_A`, `bow_A/B/C`, `dagger_A`,
`hammer_A`, `shield_A`, `staff_A..D`, `sword_D`), the Arena rocks/trees, `enemy_outpost`, and the
four harvest node meshes (`crystals`, `food`, `iron`, `wood`). A rigged import is what ALLOWS a
static prop to become a `SkinnedMeshRenderer` — but a bone-less FBX can still import as a plain
`MeshRenderer`, so this list is a watchlist, not a defect list. Do not "fix" 136 assets.

**ACTUAL DEFECT — exactly TWO prefabs in the whole Resources tree override renderer bounds:**

```
dirtyAABB=1  aabb=5   Assets/Resources/Heroes/Props/Bow.prefab
dirtyAABB=4  aabb=4   Assets/Resources/Enemies/Boss_Dragon.prefab   <-- SAME PATTERN, x4
```

`Boss_Dragon.prefab` has NOT been reported by the owner and is not covered by this ticket's
symptom, but it carries the same frozen-bounds override pattern four times. **Inspect it in the
same pass.** If the dragon ever ground-snaps oddly or floats, this is why.

**The Bow overrides in full** (`Bow.prefab`, all targeting fileID `-3887185075125053422`, the
imported SkinnedMeshRenderer in `Bow.fbx` guid `778fe16face8699458445e44ab7c50a5`):

| propertyPath | value |
|---|---|
| `m_DirtyAABB` | `0`  (frozen - never recomputed) |
| `m_AABB.m_Center.x` | `0.35529214` |
| `m_AABB.m_Center.y` | `-0.000000007450581` |
| **`m_AABB.m_Center.z`** | **NOT OVERRIDDEN - inherits the FBX value** |
| `m_AABB.m_Extent.x` | `0.48348814` |
| `m_AABB.m_Extent.y` | `0.07779948` |
| `m_AABB.m_Extent.z` | `0.99185216` |

Note the asymmetry: **all three extents are written, but only two of three centres.** The bow's LONG
axis is z (extent 0.992), and it is precisely the z CENTRE that is left inheriting. A frozen AABB
whose long-axis centre comes from a different source than its extents is the shape that produces a
wild world-space minimum. This is a strong lead for the `-33.56` figure that could not be derived
from the extents alone.

These are PrefabInstance `m_Modifications`, not raw components - the `SkinnedMeshRenderer` itself
lives in the imported FBX. That is why a naive `grep SkinnedMeshRenderer` or `!u!137` over the
prefab finds nothing; search `m_DirtyAABB` / `m_AABB` instead.

## 6. Related but SEPARATE — context only

**Why an orc shaman had a bow at all.** `EnemyBrain.cs:373` folds `mage|caster|shaman` into
`EnemyRole.Ranged`, and `:738-744` gives every `Ranged` enemy a bow — so `EnemyRole.Ranged` is
doing double duty as "attacks at distance" AND "is an archer". The owner has ruled that this enemy
carries **no weapon**, plus a last-minute scale sanity check that removes a weapon that fails it.
**That work is being handled in code by CLI and is NOT part of this brief.**

Path divergence worth knowing: `BattleArena.cs:1432-1433`, `OverworldEncounterSpawner.cs:838` and
`RaidGarrisonSpawner.cs:332-333` all call `RoleForId` (so their casters get bows), while the pooled
village wave path (`EnemyGroupSpawner.cs:175`, `SmartEnemySpawner.cs:194`) uses
`WaveCompositionBuilder.cs:227-228`, which tags casters `EnemyRole.Healer` and gets no bow.
