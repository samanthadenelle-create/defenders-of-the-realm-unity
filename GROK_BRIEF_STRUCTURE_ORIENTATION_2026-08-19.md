# GROK BRIEF — six hub structures lie on their faces. The owner's +90 is correct. Here is why, and where it must be authored.

**Date:** 2026-08-19 · **Branch:** `wip/village2-and-f8-tickets` · **HEAD:** `ef5695af2`
**Repo root is machine-dependent** (`C:\eoa` / `D:\eoa`) — all paths here are REPO-RELATIVE. Never hardcode the root.
**Every claim below was read at source on 2026-08-19.** Items marked **[NEEDS A RENDER]** cannot be settled
by measurement at all and require eyes on a screenshot — they are called out individually.

---

## 0. THE ONE-PARAGRAPH ANSWER

The Tripo FBXs are **not Z-up files**. Each is a Y-up FBX containing a single mesh node that carries its
own `Lcl Rotation (−90, 0, 0)`, with vertices whose **Z runs `[0, H]`** — the ground-plane-at-zero
signature. So the model's true vertical is its **local Z**, and the correction has always lived on the
**root transform**, never in the vertices. `bakeAxisConversion` did not bake a conversion into the mesh;
it flipped which axis Unity negates on import, which flipped the imported root's rotation from **270° to
+90°**. Then `VisualFactory.cs:236` **throws that root rotation away** (`localRotation = identity`) for
every row that has not opted into `PreservePrefabRotation`. The 2026-08-18 pass zeroed the catalog eulers
and the injector pitches on the belief that "the mesh now carries it" — **removing the only surviving
upright correction**. The five/six models are therefore fitted against their raw Y (their *depth*) and lie
on their faces, oversized ~1.5× in footprint. **`Euler(90, 0, 0)` — the owner's dialed value — is exactly
the rotation the imported root already carries, and it is the one that puts the model's true up at world
up.** `Euler(−90, 0, 0)` is bounds-identical and **upside-down**. The remaining question is not *what*, it
is ***where*** — see §4, which is the part that decides whether the fix is correct or merely upright.

---

## 1. THE SPEC — the owner's values, and they are AUTHORITATIVE

Dialed by the owner in a real build with the pieces placed. A measured target state, not a hypothesis.

> "i want you to run forge shopandcrafting through the unity tool with pos 0,0,0 x rotation of 90, y
> should always equal ground height, realm store as pos 0,0,0 rot 90,0,0 jeweler pos 0,0,0 x rotation
> of 90 y ground barracks pos 0,0,0 rot x 90 after set them all as flagged as fixed"

> "the values I gave you were from a new build and placed pieces, those are explicitly what they all
> should be."

> "the flag that it is fixed tells the code that the offset was corrected, and should clear the offset
> in the forge."

| model | position | rotation | flag |
|---|---|---|---|
| `Forge` | 0,0,0 (Y = ground) | X = 90 | fixed |
| `ShopAndCrafting` (catalog id **`workshop`**) | 0,0,0 (Y = ground) | X = 90 | fixed |
| `RealmStore` | 0,0,0 | 90, 0, 0 | fixed |
| `jeweler` | 0,0,0 (Y = ground) | X = 90 | fixed |
| `barracks` | 0,0,0 | X = 90 | fixed |
| `armorer` | — | — | **owner ruling needed** (§8) |

**The +90 is confirmed by four independent derivations** (§3). The owner's instinct and the geometry agree.

---

## 2. WHAT IS ACTUALLY IN THE FILES — measured, not assumed

### 2.1 The FBXs, parsed directly from the binary

| file | GlobalSettings `UpAxis` | node | node `Lcl Rotation` | vertex extents X / Y / Z |
|---|---|---|---|---|
| `RealmStore.fbx` | **1 (Y-up)** | `tripo_node_48de7d97` | **(−90, 0, 0)** | 0.7261 / 0.6169 / **Z ∈ [0, 1.0000]** |
| `Forge.fbx` | 1 | `tripo_node_da43aa09` | (−90, 0, 0) | 0.7263 / 0.6353 / **[0, 1.0000]** |
| `ShopAndCrafting.fbx` | 1 | `tripo_node_3eced461` | (−90, 0, 0) | 0.7098 / 0.6486 / **[0, 1.0000]** |
| `jeweler.fbx` | 1 | `tripo_node_8741c893` | (−90, 0, 0) | 0.7282 / 0.6180 / **[0, 1.0000]** |
| `barracks.fbx` | 1 | `tripo_node_9bee2207` | (−90, 0, 0) | 0.9980 / 0.9434 / **[0, 0.5215]** |
| `armorer.fbx` | 1 | `tripo_node_a9e3c51c` | (−90, 0, 0) | 0.7285 / 0.6332 / **[0, 1.0000]** |

X and Y are **symmetric about zero**; **Z runs `[0, H]`**. That is a model standing on the ground plane
with **Z as its vertical**. The six `bakeAxisConversion: 0` control files (`lumbermill`, `farm`, `store`,
`PetHouse2`, `GenericContainer`, `arcane tower`, `Ballista_L2`) have the **identical structure**.

⇒ **There was never a Z-up file to convert.** The premise the 2026-08-18 pass was built on is not
supported by the file data.

### 2.2 The import settings

All six `.fbx.meta` are **byte-identical apart from the GUID**: `globalScale: 1`, `meshCompression: 3`
(this is why Unity-measured bounds run ~0.3 % above raw vertex extents), `addColliders: 0`,
**`bakeAxisConversion: 1` (line 53)**, `useFileUnits: 1`, `useFileScale: 1`.

### 2.3 What `bakeAxisConversion` actually did — proven by two committed runtime captures

- **pre-bake:** `logs/f8-inbox/capture-20260816-065348-seq2411.md` —
  `[Flow:Xform] 'barracks' … after instantiate (prefab-native pose): euler=(270.00, 0.00, 0.00)`
- **post-bake:** `Builds/realmstore-oriented.log:696` —
  `[Flow:Xform] 'RealmStore' (entry='RealmStore') after instantiate (prefab-native pose): euler=(90.00, 0.00, 0.00)`

**It flipped the native root rotation 270 → +90.** The vertices are unchanged.

### 2.4 And then the pipeline throws that rotation away

`Assets/_Modules/Village/VisualFactory.cs:235-236`:

```csharp
else if (!opts.PreservePrefabRotation)
    go.transform.localRotation = Quaternion.identity;
```

⇒ The imported root's correct `(90,0,0)` is discarded for every row that has not opted in. Zeroing the
catalog eulers and the injector pitches therefore left **nothing** standing these models up.

---

## 3. THE REALM STORE, PROVEN FOUR WAYS

Scene facts (`Assets/Scenes/Main_Castle_Overworld.unity`): root `RealmStore_Storefront` at `(12,0,-32)`,
yaw −20.553°, unit scale, `BoxCollider m_Size (3.4003668, 4, 5.503104)` centre `(0,2,0)` (`:20909-20917`);
child `RealmStore(Clone)` `m_LocalRotation (0.7071068, 0.7071068, ~0, ~0)`, `m_LocalPosition (0, 2, −2.744247)`,
uniform scale `5.488491`, **no children — mesh on the root** (`:21173`).

**The quaternion is `Quaternion.Euler(0, 180, 90)`.** Unity composes ZXY: `q = q_y(180)·q_z(90)` →
`(0.70711, 0.70711, 0, 0)`. Matches to float residue, and is independently confirmed by
`Builds/realmstore-oriented.log:711`. It is a 180° rotation about `(1,1,0)/√2`, mapping **+X→+Y, +Y→+X,
+Z→−Z**.

**Raw extents X 0.7288 / Y 0.6195 / Z 1.0027, confirmed four independent ways:**

1. Collider ÷ scale, X/Y swapped back → `0.7288 / 0.6195 / 1.0027`.
2. The stale pre-fix collider is the *same mesh yaw-inflated*: at yaw 20.553°,
   `0.7288·cos + 1.00266·sin = 1.0344` vs recorded **1.034**; `0.7288·sin + 1.00266·cos = 1.1948` vs
   recorded **1.195**; Y untouched at **0.620**.
3. The proof doc's world bounds: `3.400·cos + 5.503·sin = 5.116 ≈ 5.12`; `3.400·sin + 5.503·cos = 6.347 ≈ 6.35`.
4. Raw FBX vertices `0.7261 / 0.6169 / 1.0000` — uniformly ~0.3 % smaller, exactly `meshCompression: 3`.

**Which sign of Z is up, settled exactly from the scene.** `SeatOnGround` (`VisualFactory.cs:394-400`)
forces the world bounds centre onto the host in X/Z, so `p.z = +c_z·s`. Observed `p.z = −2.744247`,
`s = 5.488491` ⇒ **`c_z = −0.500000` exactly** ⇒ the imported mesh occupies `z ∈ [−1.0013, +0.0013]`.
**Unity negated Z for the axis-baked import, so the imported mesh's up is local −Z.**
Cross-check on the *un-baked* import (`barracks`, pre-bake): `s = 4/0.9434 = 4.2400` ✓,
`p.y = 0.4717·4.24 = 2.000` ✓, `p.z = −0.26075·4.24 = −1.1056 ≈ −1.11` ✓, `c_z` **positive** — not
negated. The bake flips which axis is negated; hence 270 → 90.

---

## 4. ⛔ THE DECIDING QUESTION IS *WHERE*, NOT *WHAT*

`Fit` measures `bounds.size.y` (`VisualFactory.cs:383-389`, the `largest:false` arm). So a correction
applied **before** the fit is measured on the upright axis; a correction applied **after** it is not.

| channel | file:line | relative to Fit |
|---|---|---|
| `opts.LocalRotation` | `VisualFactory.cs:233-234` (Fit at `:264-265`, Seat at `:270`) | **BEFORE** ✅ |
| hub injector `pitchDeg` | `HubStructureVisualInjector.cs:304`, `:557` | **BEFORE** ✅ |
| catalog `entry.orientation.euler` | `StructureFactory.cs:151-158` (Skin at `:135`) | **AFTER** ❌ |

`StructureFactory.OptsFor` (`:431-449`) sets `FitHeight = EffectiveVisualHeight` and
`PreservePrefabRotation`, and **never sets `LocalRotation`**. `YHeightVariable = 4f` (`:59`);
`EffectiveVisualHeight = YHeightVariable × repo.heightMul` (`:70-76`); all five ids have `heightMul`
unset ⇒ **target 4.00 m**.

### 4.1 RealmStore — the three candidates, arithmetic calibrated against the shipped scene

| candidate | pre-fit AABB (W,H,D) | scale | final W × H × D | aspect | mesh axis at world UP | verdict |
|---|---|---|---|---|---|---|
| `Euler(0,180,90)` **(shipped)** | 0.6195, 0.7288, 1.0027 | **5.4885** | **3.400 × 4.000 × 5.503** | 0.727 | local **+X** | **still on its side**, just rolled onto a different face |
| `Euler(90,0,0)` **(owner)** | 0.7288, **1.0027**, 0.6195 | **3.9892** | **2.907 × 4.000 × 2.471** | **1.376** | local **−Z** = true up | **UPRIGHT, right way up** ✅ |
| `Euler(−90,0,0)` | identical AABB | 3.9892 | 2.907 × 4.000 × 2.471 | 1.376 | local **+Z** = the floor | **upright but UPSIDE-DOWN** |

The shipped row reproduces the committed scene byte-for-byte, so the model is calibrated. **`+90` and
`−90` are AABB-identical — no measurement of extents can ever separate them.** Only the `[0,H]` vertex
asymmetry can, and it says **+90**.

### 4.2 The other five — via the PRE-fit channel (correct)

| model | scale | final W × H × D | aspect |
|---|---|---|---|
| Forge | 4.000 | 2.905 × **4.000** × 2.541 | 1.377 |
| ShopAndCrafting | 4.000 | 2.839 × **4.000** × 2.594 | 1.409 |
| jeweler | 4.000 | 2.913 × **4.000** × 2.472 | 1.373 |
| armorer | 4.000 | 2.914 × **4.000** × 2.533 | 1.373 |
| **barracks** | **7.670** | **7.655 × 4.000 × 7.236** | **0.523** ⚠ see §8 |

### 4.3 The other five — current shipped state (euler `[0,0,0]`, root zeroed = lying on their faces)

| model | scale | W × H × D | aspect |
|---|---|---|---|
| Forge | 6.296 | 4.573 × 4.000 × 6.296 | 0.635 |
| ShopAndCrafting | 6.167 | 4.377 × 4.000 × 6.167 | 0.649 |
| jeweler | 6.472 | 4.713 × 4.000 × 6.472 | 0.618 |
| armorer | 6.317 | 4.602 × 4.000 × 6.317 | 0.633 |
| barracks | 4.240 | 4.232 × 4.000 × 2.211 | 0.945 |

### 4.4 ⛔ The other five — via the POST-fit CATALOG channel: upright and WRONGLY SCALED

| model | scale | W × H × D | **height** |
|---|---|---|---|
| Forge | 6.296 | 4.573 × **6.296** × 4.000 | **6.30 m**, not 4.00 |
| ShopAndCrafting | 6.167 | 4.377 × **6.167** × 4.000 | **6.17 m** |
| jeweler | 6.472 | 4.713 × **6.472** × 4.000 | **6.47 m** |
| barracks | 4.240 | 4.232 × **2.211** × 4.000 | **2.21 m** |

This is the identical trap the `tower_ground_archer` catalog note walks through ("scale becomes
4.8/0.519 = 9.25x instead of 4.80x"). **Writing `euler: [90,0,0]` into the catalog and stopping there
produces six upright, wrongly-sized buildings and a green gate.**

---

## 5. THE CHANNEL MAP — who reads what

| channel | authority for | notes |
|---|---|---|
| **A. `Assets/OffsetForge/offsets.json`** | **NOTHING, for structures** | `rot` is read by no code for any of these six ids. Readers are `AttachmentOffsetRegistry.cs:41-43` (hero/enemy attachment mesh ids), `CastleMoatBuilder.cs:640` (`bridge_south`), `WoodenWatchtowerBuilder.cs:895`, `BridgeDeckMeasure.cs:74`, and `RealmStorePlacer.cs:189-227` (**`pos` only**). Schema `OffsetTable.cs:63-77` — **`axisBaked` is not even a field on the class.** ⚠ Any save through the Offset Forge window (`OffsetForgeWindow.cs:866`) or Gear Caster (`GearCasterWindow.cs:764`) **strips `axisBaked` from every row** and resets `fullOverride`/`scaleXyz`. |
| **B. `TripoAxisBake.cs`** | the `.fbx.meta` flag + row zeroing | `axisBaked:true` rows are **EXCLUDED, not zeroed** (`:102`). Its candidate regex (`:99`) matches only an exact `rot {x:-90,y:0,z:0}`. **For all six ids it is a no-op** (`TRIPO_AXIS_BAKE_OK 0 baked`). |
| **C. `structures-catalog.json` `entry.orientation`** | `workshop`, `forge`, `jeweler`, `barracks` (+`armorer` if `manual` flipped) | Applied **post-fit** at `StructureFactory.cs:151-158`, **only when `manual == true`**. `manual` is the "flagged as fixed" flag: `CatalogOrientationBaker.cs:60-61` skips such rows; `GhostPreview.cs:120` mirrors the gate. Live catalog is **v23**; dual copies must stay byte-equal. |
| **D. `HubStructureVisualInjector.cs`** | the four **baked hub storefronts** | **Pre-fit** (`:304`, `:557`). Pure runtime (`RuntimeInitializeOnLoadMethod` + `sceneLoaded`), hub-only, touches named baked objects only. **Contributes nothing to the baked navmesh ⇒ no navmesh re-bake needed for a change here.** |
| **E. `RealmStorePlacer.cs`** | `RealmStore` alone | Hardcoded `AuthoredCorrection = Quaternion.Euler(0f,180f,90f)` (`:146`) → `opts.LocalRotation` (`:342`), i.e. **pre-fit**. Position from `offsets.json` `pos`, where **`(0,0,0)` means NOT AUTHORED** (`:217`) and falls back to `(12,0,-32)` (`:102`). |
| **F. device `structure-orientations.json`** | overrides C entirely | `StructureOrientationLocalStore` → `CatalogBootstrap.cs:72` `ApplyAll` **REPLACES `entry.orientation` with `manual=true` before any Create/ghost read — LOCAL WINS.** ⚠ **Checked on the Seeker 2026-08-19: the file is NOT present** (17 entries in persistentDataPath, none matching). This device is not being overridden. But note the sibling `attachment-offsets.json` has a **PlayerPrefs mirror that restores it after deletion** — check whether this store does too before assuming a delete sticks. |
| **G. `.fbx.meta bakeAxisConversion`** | which axis Unity negates | §2.3. `TripoAssetPostprocessor.cs:124` sets it on new Tripo imports. |

---

## 6. ⛔ THE EIGHT `-90`s THAT MUST STAY — do not sweep

```
pet-house · market · arcane-tower · collector_farm
collector_lumbermill · lumberyard · foundry · silo
```

**The rule is not "-90 is legacy". The rule is "-90 is legacy IFF that FBX's meta says
`bakeAxisConversion: 1`." Check the meta, per asset, every time.** A "tidy up the remaining -90s" pass
lays all eight down — including **`collector_lumbermill`, the FTUE's first building**.

**Id traps that have already caused wrong edits:**
- Catalog row **`market`** → model `Structures/store` — **keeps its −90**. It is not the Realm Store.
- **`RealmStore` is not a catalog row at all** (28 entries, none matches `store`/`realm`).
- Row **`workshop`** → model `Structures/ShopAndCrafting`; row **`forge`** has displayName "Armorer".
- **`lumbermill`** (`Watermill_Medieval`, manual:false) ≠ **`collector_lumbermill`** (`Structures/lumbermill`, −90, manual:true).

**The negative control for this whole fix:** the `bakeAxisConversion:0` buildings — pet house, arcane
tower, market, lumbermill, windmill — **must look IDENTICAL to before. If the lumbermill changes at all,
revert the change wholesale.**

---

## 7. THE GATE CANNOT SEE THIS — and the one that was built is a tautology

`Assets/Editor/Regression/StructureOrientationOracle.cs` (633 lines), markers
`STRUCTURE_ORIENTATION_OK` / `_FAIL`, menu `Defenders/Build/Audit Structure Orientation (PROD-008)`,
headless `RunStandalone()`.

- **A1 channel collision** (`:299-320`) — data-only: `bakeAxisConversion == true` **and** a manual euler
  that tips world-up > 1°, or `preservePrefabRotation` over an already-tilted prefab root.
- **A2 height fidelity** (`:341-355`) — `bounds.size.y` vs `StructureFactory.OptsFor(entry).FitHeight`, ±0.05 m.
- **A3 tower aspect** (`:359-379`) — `h / max(w,d) ≥ 1.2`, scoped to `type == Tower && heightMul >= 1.2`.
- Thresholds: `HeightToleranceM = 0.05` (`:166`), `TiltEpsilonDeg = 1.0` (`:173`), `UprightAspectMin = 1.2`
  (`:181` — *"MEASURED, not chosen: WoodenWatchtowerBuilder.cs:271-277 records 1.70-1.92 upright and
  0.52-0.59 lying down"*).

> ### ⛔ A2 IS A TAUTOLOGY FOR EXACTLY THESE ROWS.
> `TryMeasure` (`:419-489`) scales by `target / pre.size.y`, then applies the catalog euler only when
> `manual`, and A2 is **skipped whenever the post-fit tilt > 1°** (`:344-350`). So A2 is only asserted on
> rows whose rotation does not move world-up — and no such rotation can change `bounds.size.y`. It
> reduces to `target == target`. **All five ids carry `euler:[0,0,0]`, so A2 cannot fail on them no
> matter which way the mesh faces.** The log's *"A2 height fidelity asserted on 26 model(s)"* with zero
> failures is fully consistent with 26 models lying flat on their faces. **A green
> `STRUCTURE_ORIENTATION_OK` after this fix would prove nothing until A2 is re-authored to measure
> against the model's own vertical.**

**Registration: NO.** Zero references to `StructureOrientationOracle` anywhere outside its own file — not
in `DataRegression.RunAll`, not in `RegressionSuite`, not in `tools/regression/*.ps1`. Its own header
(`:8-15`): *"regression-registry: standalone ← TEMPORARY … an oracle that stays 'standalone' is an oracle
that never runs."* Its ticket is still `Status: READY TO IMPLEMENT`. It also **cannot see RealmStore at
all** — not a catalog row (header `:107-114`).

Its one real catch to date (`Builds/struct-orient.log:571`, 2026-08-18 21:43):
`tower_ballista` aspect **0.70**, `tower_ballista L2` **0.94**. At X=90 the ballista's aspect goes to
**~0.55 — worse**. The oracle is right to flag it; **the fix is reclassifying the row, not moving the
threshold**, and that is an owner ruling.

**A validated pipeline model:** all eight `A2 NOT ASSERTED` heights in that log reproduce exactly from FBX
vertex extents via `h = (Z_raw / Y_raw) × YHeightVariable × heightMul` — lumbermill 3.139 vs **3.14**,
market 3.984 vs **3.98**, pet-house 4.310 vs **4.31**, arcane-tower 7.379 vs **7.36**, farm 2.181 vs
**2.19**, lumberyard/foundry/silo 2.896 vs **2.90**, ballista W/D 6.854/3.752 vs **6.85/3.76** and
5.112/2.749 vs **5.11/2.75**. **8/8.** The arithmetic in §4 rests on the same validated model.

---

## 8. THE THINGS ONLY THE OWNER CAN DECIDE

1. **`barracks` at X=90 measures 7.66 × 7.24 m** — larger than any building in the cadence
   (`House_Medieval_Medium` is 5.562 m across), aspect 0.523. Its Z (0.5215) is its **shortest** axis, so
   it is not the same silhouette family as the four storefronts. Standing it on Z is geometrically what
   was asked and may still be visually wrong. **[NEEDS A RENDER]**
2. **`jeweler` carries explicit non-uniform scale** in the injector — `scaleX 5.4 / scaleY 3.6 /
   scaleZ 3.77` (`HubStructureVisualInjector.cs:113`), which the comment says *"supersedes the height
   fit"*. Those three numbers were conjugated for the **old** pose and must be re-conjugated
   (`S_new = B·S_old·B⁻¹`, permuting Y and Z) — not re-dialled by hand.
3. **`armorer` — in or out of scope?** Its catalog row is `manual: false`, which means (a) its orientation
   is **never applied**, and (b) `CatalogOrientationBaker.Bake` will **overwrite** the row on its next run.
   It was not named in the ask but is in the identical state.
4. **Which way each front faces.** **No door/front-direction data exists anywhere in the repo.** The
   per-row `yawDeg` hand-dials (Forge 180, armorer 90, jeweler 110.4, barracks 180) are owner-dialled
   *facings* for the **old** pose and every one will need re-dialling by eye. **[NEEDS A RENDER]**
5. **`tower_ballista` reclassification** — unblocks oracle registration (§7).
6. **Storefront height: 4 m vs the 1.25 landmark tier** — a standing open ruling; the Realm Store
   currently measures exactly 4.00 m.

---

## 9. THE RUN PROCEDURE

**The Unity Editor must be CLOSED for every batchmode step** (project lock).

**Hub storefronts (`Forge`, `armorer`, `jeweler`, `barracks`) — channel D, pre-fit, correct sizing:**
1. Set `pitchDeg = 90` on the matching `Swap` rows (`HubStructureVisualInjector.cs:105-115`).
2. Re-conjugate the jeweler's non-uniform scale (§8.2).
3. **No scene bake, no navmesh bake** — the injector is pure runtime and contributes nothing to the navmesh.
4. **⛔ Do NOT run `CastleHubBuilder.BuildCastleHub`** — it reverts the owner's hand-dialled hub offsets.

**Catalog rows (`workshop`, `forge`, `jeweler`, `barracks`, +`armorer`):**
- If a catalog euler is used at all, **it must be moved upstream of the fit first** (§4.4), otherwise the
  models come out 6.3 m / 2.2 m tall. Three options: hand the manual euler to `SkinOptions.LocalRotation`
  in `StructureFactory` (cleanest, but changes behaviour for every row already carrying a manual euler —
  gate it and regression-prove it); a prefab wrapper with `PreservePrefabRotation` per row; or leave the
  catalog at `[0,0,0]` and let channel D own the hub pose.
- Edit **both** catalog copies byte-equal:
  `Assets/StreamingAssets/Data/Canonical/structures-catalog.json` and
  `Assets/Resources/Data/Canonical/structures-catalog.json`.
- **Keep `manual: true`.** Clearing it invites the auto-baker to re-author the row.
- Mirror any orientation change into `CatalogBootstrap.RegisterFallback` for the three tower fallback rows,
  or `[fallback-parity]` goes red.
- **`ReskinForLevel` (`StructureFactory.cs:384-390`) must NOT re-apply `entry.orientation`** — tier models
  rely on their prefab-native orientation. The carve-out is deliberate.

**RealmStore:**
1. `Assets/Editor/RealmStorePlacer.cs:146` — `AuthoredCorrection` → `Quaternion.Euler(90f, 0f, 0f)` plus
   whatever yaw the owner rules for facing. It is already applied pre-fit, so sizing will be correct.
2. `-executeMethod DeNelle.Editor.RealmStorePlacer.Run` (`:283`; menu `:176`). Idempotent.
3. **Then** `-executeMethod DeNelle.Editor.NavMeshBakeFinal.Run` (`NavMeshBakeFinal.cs:63-66`) — mandated,
   because this one **is** baked into the scene and its footprint changes.

**Not required:** `TripoAxisBake` — no-op for all six.

**Then:** `COMPILE_GATE_OK` → `REGRESSION_OK <n>/<n> suites` (read the count off the marker, never a doc;
known-red baseline is 4) → `UI_CAPTURE_OK` (open the PNGs) → `python tools/r2_sync.py --push ServerData`
(**`ServerData`, NOT `ServerData/Android`** — the docstring at `tools/r2_sync.py:22` still teaches the
wrong form; push AFTER the build, BEFORE the device install) → `R2_PARITY_OK` → APK → install → **a
screenshot of every one of the six.**

---

## 10. ACCEPTANCE CRITERIA

1. Upright **and right way up** — a screenshot per model. Nothing else can prove this (§4.1).
2. `bounds.size.y == StructureFactory.OptsFor(entry).FitHeight` → **4.00 m ±0.05** for
   forge/workshop/jeweler/barracks; **4.80 ±0.05** for tower_ballista. *If a measured height comes back
   near the short axis, the model is still lying down and the correction did not take.*
3. **Footprints SHRINK** for the four storefronts (~1.5× oversize today → ~2.9 m across). A footprint that
   does not change is evidence the fix did not take. Predicted finals are in §4.2 — publish the prediction
   before the run and diff it after.
4. The eight `-90` rows are **byte-unchanged**, and the lumbermill looks identical (§6).
5. The correction lives in **exactly one channel** per model — prove it by grepping the trace for
   `opts.LocalRotation` vs `prefab rotation PRESERVED (WO-928, opt-in row)` vs
   `LocalRotation identity (DEF-232 default)`.
6. Ghost preview matches the placed result (`GhostPreview.cs:120` reads the same gate).
7. Navmesh re-baked for RealmStore only; still reachable (last measured: nearest walkable 0.08 m).
8. A2 re-authored so the oracle can actually fail (§7), and the oracle **registered** — proven red against
   the pre-fix state. *A gate that does not fail the known-bad state is not a gate.*

---

## 11. TRAPS — every way this exact problem has already gone wrong

1. **Fixing the file you can see instead of the file that is read.** `f995c4706` zeroed ten rows in
   `offsets.json`; those rows are **inert for structures**. Net effect on the town: zero.
2. **Fixing one of two live channels.** Catalog (post-fit, `manual` only) and hub injector (pre-fit) are
   independent. `armorer` is `manual:false`, so a catalog-only fix never touches it.
3. **Believing "the mesh now carries it."** §2 disproves it from the file bytes. The correction lives on
   the root transform, and `VisualFactory.cs:236` discards it.
4. **The "tidy up the remaining -90s" sweep** — §6.
5. **A global `PreservePrefabRotation`.** `bb6dc010` applied it to all structures and **laid the whole town
   on its side** (13 manual −90s composed to 180). Reverted by `70a86c17`. It reproduced **only on the
   dungeon → town return path**, with every marker green throughout. The **one** sanctioned opt-in is
   `tower_ground_archer`.
6. **Trusting a green gate.** Headless gates cannot see orientation — `f995c4706` conceded it about
   itself. Every orientation defect this project shipped went out compile-green and regression-green, into
   a live store build.
7. **Confusing the ids** — §6.
8. **Parking a hand-dialled rotation in Offset Forge** — inert for structures, and destructively rewritten.
9. **The third correction route.** `Tower_Wooden_Watchtower_L3` is double-corrected via a prefab child
   override kept alive by `preservePrefabRotation: true`. Reported, deliberately not fixed.
10. **Code-vs-JSON drift.** `CatalogBootstrap.RegisterFallback` carried the same stale −90 for
    `tower_ballista` and went red on `[fallback-parity]`.
11. **Re-baking the hub to "apply" the fix** — unnecessary and destructive (reverts hand-dialled offsets).
12. **Shipping an APK whose bundles were never uploaded.** Happened twice on 2026-08-18. `--check` proves
    credentials only; `--push` skips by **size, not hash**; `catalog_*.hash` is always exactly 32 bytes, so
    a reused `bundleVersion` silently skips the file that says which content is current.
13. **"Going local" to dodge the CDN.** `m_DisableCatalogUpdateOnStart: 0` ⇒ installed APKs adopt the new
    remote catalog at launch, so re-pointing to local = **invisible buildings for every existing player**;
    re-grouping rehashes bundles = full re-download for everyone. Ruled. Do not re-litigate.
14. **A stale device overlay defeating the catalog fix** — not present on this device today (§5F), but it
    replaces `entry.orientation` with `manual=true` before any read when it exists.
15. **Trusting an "eyes-on" claim that isn't.** The 2026-08-18 proof README's own LIMITS section: *"The
    FOUR hub storefronts fixed tonight (Forge, armorer, jeweler, barracks) are NOT visually confirmed …
    derived + gate-green but UNSEEN."*
16. **Landing an oracle that never runs** — §7.
17. **A single global aspect threshold.** It false-positives on wide buildings: `House_Medieval_Medium`
    measures 4.0 / 5.562 = **0.72 upright** and is perfectly correct. Height fidelity must be the primary
    assert; the aspect band is secondary and tower-scoped.
18. **Judging a build from a stale artifact.** *Existence proves nothing. Freshness does.*

---

## 12. DEVICE EVIDENCE, 2026-08-19

- Installed `versionName=2026.08.19.331367`, `versionCode=331367`, installed `2026-08-18 21:53:42`,
  package `com.denellestudios.echoesofelarion` — this build **does** contain the PROD-003 fix, and the
  Realm Store is still on its side, exactly as §4.1 predicts for `Euler(0,180,90)`.
- persistentDataPath: 17 entries, **no `structure-orientations.json`** (§5F).
- `break-log.jsonl` (467 KB, last write 13:58) ends with a live, unrelated defect currently blocking
  felt-testing: `[Flow:Tutorial] STEP-STUCK :: founding_timers — no 'dialogue.ended:tut_founding_timers'
  after 120s in-step (bound 120s, builder time excluded; ff.tutorialv2 on; builderOpenedThisStep=False)`,
  with error captures at 13:54 / 13:56 / 13:58 in `Main_Castle_Overworld`.
- ⚠ **The §14 F8 watcher has no Android arm.** `f8-check-inbox.ps1` reported `NO_CAPTURE ack=2535
  ping=2535` while the Seeker was writing three captures. Every F8 pressed on the device is invisible to
  the hook meant to wake the seat.
- ⚠ **`logcat` is unusable as evidence on this build.** A per-frame `[Flow:Equip] BowOrient` line from
  `DeNelle.Core.Geometry.WeaponBoundsOrient:ComputeBowHeldRotation` floods the 256 KiB ring and evicts
  everything else. It needs `FlowTrace.Throttle`.

---

## APPENDIX — FILE MAP

| what | where |
|---|---|
| The six models | `Assets/StructureContent/{RealmStore,Forge,ShopAndCrafting,jeweler,barracks,armorer}.fbx` (+ `.meta`, `bakeAxisConversion` line 53) |
| Addressable group | `Assets/AddressableAssetsData/AssetGroups/Structure_Art.asset` (jeweler `:23`, ShopAndCrafting `:53`, barracks `:78`, armorer `:153`, Forge `:168`) |
| Offset table + schema | `Assets/OffsetForge/offsets.json` (mirror `Assets/Resources/OffsetForge/offsets.json`); `Assets/OffsetForge/Runtime/OffsetTable.cs:63-77` |
| Catalogs (byte-equal, v23) | `Assets/StreamingAssets/Data/Canonical/structures-catalog.json`, `Assets/Resources/Data/Canonical/structures-catalog.json` |
| Orientation baker + `manual` | `Assets/Editor/CatalogOrientationBaker.cs:40,60-61,138-147` |
| Axis bake | `Assets/Editor/TripoAxisBake.cs:53,80-88,99,102,147-154` |
| Tripo import postprocessor | `Assets/Editor/TripoAssetPostprocessor.cs:124` |
| Skin / fit / seat pipeline | `Assets/_Modules/Village/VisualFactory.cs:233-236,264-265,270,383-389,394-400` |
| Structure factory | `Assets/_Modules/Village/Catalog/StructureFactory.cs:59,70-76,135,151-158,174,384-390,431-449,610-626` |
| Ghost parity | `Assets/_Modules/Village/BuildMode/GhostPreview.cs:120-126` |
| Hub injector | `Assets/_Modules/Village/HubStructureVisualInjector.cs:66-68,105-115,143,304,354-364,557` |
| Realm Store placer | `Assets/Editor/RealmStorePlacer.cs:102,146,176,189-227,283,326-330,335-337,342,423-428` |
| Castle hub builder (⛔ do not regen) | `Assets/Editor/CastleHubBuilder.cs:134` |
| Navmesh bake | `Assets/Editor/NavMeshBakeFinal.cs:55,63-66` |
| Hub scene | `Assets/Scenes/Main_Castle_Overworld.unity` |
| Device overlay | `Assets/_Modules/Village/Catalog/StructureOrientationLocalStore.cs:40,77,106,127`; applied `CatalogBootstrap.cs:72` |
| The oracle (UNREGISTERED) | `Assets/Editor/Regression/StructureOrientationOracle.cs:8-15,166,173,181,299-320,341-355,359-379,419-489,541` |
| Its evidence log | `Builds/struct-orient.log:571` |
| The PROD-003 bake log | `Builds/realmstore-oriented.log:696,711,726,786,919` |
| Proof doc + its LIMITS | `docs/proof/2026-08-18-overnight-gear-structures/README.md` |

---

## 13. WHAT THIS FIX MAY NOT DO — the architecture law, cited

Read from `docs/ARCHITECTURE_PRINCIPLES.md`, `docs/ARCHITECTURE.md`, `PREFLIGHT_GATE.md`,
`docs/INSTRUMENTATION_STANDARD.md` and the MASTER_CATALOG area files.

1. **No blanket/global orientation change.** Shipped and reverted once: `PreservePrefabRotation` on all
   structures "laid the whole town on its side" with every gate green (`bb6dc010` → `70a86c17` → narrow
   `439e03ee`).
2. **No overwriting a `manual=true` correction.** Law 4: *"A `manual=true` correction is canon and is
   NEVER overwritten by the auto pass"* (`ARCHITECTURE_PRINCIPLES.md:207-208`). And never zero an euler
   while clearing `manual` — that invites the auto-baker to re-author the row.
3. **No "tidy the remaining −90s"** — the eight rows in §6.
4. **No touching the `tower_ground_archer` / `preservePrefabRotation` opt-in as a side effect.**
5. **No second orientation reader and no per-id code branch.** *"Capability is a property on the entry,
   never bespoke per-type code"* (`:84-87`); *"ONE owner per concern (no double-stacks)"* (`:113-116`).
   `StructureFactory.OptsFor` is the established single reader.
6. **No hand-typed Euler or scale "to make it look right."** Law 4 again: transforms are DERIVED from
   bounds + name — *longest axis → primary/up, narrowest → flat*. Note this cuts **for** the owner's
   value: the longest axis of these meshes is Z, and X=90 is exactly what puts Z up.
7. **No new width/scale number.** `heightMul × YHeightVariable(4f)` is the only knob; *"There is no width
   dial and none is needed"*; `repo.visualHeight` is dead — do not author against it.
8. **State the before/after grid cell claim.** Grid claim is `ceil(measured / 3 m)`. A *shrink* can only
   reduce a claim (safe); standing a model up **raises** the measured axis, which is the dangerous
   direction. `barracks` (§8.1) is the one that must be checked against saved placements.
9. **Do not "fix" `collector_farm` 1.4 to 1.0** — it is a compensation for windmill-blade bounds.
10. **Do not touch wall/gate heights or footprints in this pass** — a wall shrink opens pathable gaps in
    saved wall runs and shrinks the NavMeshObstacle with them, invisible to shrink-is-safe reasoning.
11. **Do not hand-edit a `.unity` scene, and do not regenerate the hub.** `BuildCastleHub()` is
    destructive and reverts the owner's hand-dialled offsets.
12. **Do not bake with the Unity editor open**; re-bake binary scenes only in an isolated worktree.
13. **Do not edit only one copy of `structures-catalog.json`** — the `Resources/` copy wins at load.
14. **Do not edit `offsets.json` for structures** — inert; that is the exact mistake `f995c4706` made.
15. **Do not re-apply the base euler in `ReskinForLevel`** — tier models rely on prefab-native orientation.
16. **An oracle measures; it never authors.** And no global aspect band —
    `House_Medieval_Medium` is 4.0/5.562 = **0.72 upright** and perfectly correct.
17. **No code edit before a captured line proves the cause** (`CLAUDE.md §12`). For this ticket the
    proving lines are `Builds/realmstore-oriented.log:696,711,726` and
    `logs/f8-inbox/capture-20260816-065348-seq2411.md`.
18. **Never strip FlowTrace or Guard.** Instrumentation is permanent; flag it off, never delete.
19. **Do not declare this verified on `COMPILE_GATE_OK` / `REGRESSION_OK` alone.** *"HEADLESS GATES
    CANNOT SEE ORIENTATION … this defect class needs eyes, not markers. Say so out loud whenever a change
    touches transforms."* The PO closes, not the CLI.
20. **Do not write an assertion that cannot fail** — the current A2 is exactly that (§7). Assert the
    **measured** `bounds.size.y` against `YHeightVariable × heightMul`.
21. **Do not smuggle the structural tier in.** Three separate WOs live under this, and each needs its own
    measurement and its own playtest:
    - **R1 fit-before-upright** — `Fit` measures the raw pre-correction mesh, so scale and footprint are
      derived from different-orientation measurements. This is the §4 trap, and fixing it properly is the
      structural tier.
    - **F41 ambiguous `Resources.Load`** — three ids carry both a `.fbx` and a same-stem `.prefab`
      (`Tower_Wooden_Watchtower`, `_L2`, `_L3` — the whole `tower_ground_archer` ladder). Winner is
      resolution-order dependent and **undefined by Unity contract**; if the `.fbx` wins, the baked −90 and
      the materials are gone. Latent today. Likely the real root under WO-928 defects B and C.
    - **R3 ghost-vs-placed scale divergence** — `GhostPreview` applies uniform scale, the factory per-axis.
22. **Sole committer, explicit paths, no `git add -A`, no push before the owner felt-verifies.**
23. **Canon updated in the same commit** — a state change with no canon update is incomplete.
