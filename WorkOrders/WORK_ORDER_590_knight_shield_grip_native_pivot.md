**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

> ⚠ **NUMBER COLLISION — this document does not own WO-590; `WORK_ORDER_590_castle_water_dip_fill_and_fish.md` does.**
> Referred to hereafter as **WO-590-B (knight shield grip / native pivot)**.
> Flagged by the 2026-08-16 Sunday board-grooming pass (`python tools/board_build.py` → `DUPLICATE_WO_NUMBERS`);
> ownership decided by **first-on-disk** (`git log --follow --diff-filter=A`): the winner's file was created first.
> Banner only — nothing was renumbered or deleted.
> ⚠ **Work HAS shipped under this number** — commit messages and/or a `.RESULT.md` cite WO-590 for THIS document. It is deliberately **not renumbered**; a renumber would orphan those references. Use the alias above when you need to name it unambiguously.

# WORK_ORDER_590 — Knight shield grip: native Blink + Tripo fallback (implementation playbook)

**Status: IMPLEMENTED @ `C:\EoA` (shield path live in `EquipmentController` since 2026-06-19 Blink fix; carry-state/sheath 2026-07-04)** — this doc is the **CLI replication playbook** for Claude/UI. Apply verbatim; do **not** re-debug moat/castle geometry (closed).

**Type:** EXISTING · **Silo:** Hero/Equipment · **Pairs with:** WO-478 (sword — separate WO, same controller).

---

## Problem (closed RCA — do not re-investigate)

Knight off-hand = shield on **LeftHand** (`CC_Base_L_Hand` on KnightV3). Symptoms that were fixed:

| Symptom | Cause | Fix location |
|---|---|---|
| Shield floats beside forearm, not on arm | Legacy path loaded Tripo `shield_A` with foreign pivot + preset `gripPos (-0.05)` even for Blink addressable shields | `EquipOffHand` → Addressable branch + native seat |
| Shield 180° wrong (duplicate) | Paladin package **bakes** shield on body AND `EquipmentController` attached a second shield | `PackageBakedGear` skip in `EquipOffHand` |
| Shield on arm in town (should be on back) | Drawn seat shown out of combat | `ApplyHoldPose` carry-state (drawn=combat only) |
| Sheathed shield face wrong on back | Hand-guessed euler | `_sheatheOffHandLocalEuler = (0, 270, 192)` |

---

## Weapon ids (catalog)

| Id | Mesh | Load path | Seat path |
|---|---|---|---|
| `knight_shield_starter` | `shield_A` (via `Resolve` keyword `shield`) | Resources sync | **Tripo/non-native:** `NormalizeInto` + preset grip |
| `tripo_shield_a` | `shield_A` | Resources (`prefabPath` only, no `loadVia`) | Same — **non-native** preset grip |
| Blink shield rows | Blink prefab | `loadVia=="addressable"` + `gear/weapon/Shield1h_XX` | **Native:** `SeatNative` + zero hand offset |

`Resolve("knight_shield_starter")` hits keyword `shield` → `Shield("shield_A")` preset (non-native unless Addressable completion sets `native=true`).

---

## Fix summary — where each piece lives

| What | File | Method / field |
|---|---|---|
| Off-hand attach entry | `EquipmentController.cs` | `EquipOffHand(WeaponDef)` |
| Skip attach when body bakes gear | same | `PackageBakedGear` early return |
| Blink async load | same | `BeginAddressableOffHand` |
| Native vs Tripo seat | same | `AttachOffHandProp` |
| Shield visual preset (Tripo) | same | `Shield(string mesh)` static preset |
| LeftHand bone resolve | same | `EquipOffHand` (Humanoid + `RigAttachmentRegistry`) |
| Combat draw / town sheathe | same | `ApplyHoldPose`, `_sheatheOffHandLocal*` |
| Offset nudge (Tripo only) | `Assets/OffsetForge/offsets.json` | `shield_A` entry |
| Readable mesh in builds | `WeaponPropReadablePostprocessor.cs` | same as WO-478 |

---

## Step-by-step implementation (CLI — apply in this order)

### Step 1 — Shield visual preset (Tripo/Resources path)

**File:** `Assets/_Modules/Village/Hero/EquipmentController.cs`

```csharp
private static WeaponVisual Shield(string mesh) => new WeaponVisual
{
    mesh = mesh, leftHand = true, kind = WeaponClass.Shield,
    gripPos = new Vector3(-0.05f, 0f, 0f),
    gripEuler = new Vector3(-58f, 16f, -90f),   // owner felt 2026-06-23
    heldLength = 0.48f,
    tint = new Color(0.58f, 0.60f, 0.64f)
};
```

This preset is for **non-native** Tripo/Resources shields only. Do not apply these offsets on the native Blink path.

### Step 2 — `EquipOffHand` — package de-dupe (F8 "shield 180°")

At top of `EquipOffHand(WeaponDef def)`:

```csharp
if (PackageBakedGear)
{
    FlowTrace.Step("Equip",
        $"PACKAGE baked-gear hero '{name}' — SKIP off-hand/shield attach for '{id ?? "<null>"}' " +
        "(baked Paladin shield wins; no wrong-oriented attached shield).");
    return;
}
```

`PackageBakedGear` = `GetComponent<PackageBakedGearMarker>() != null` on hero root.

**KnightV3 does NOT use this marker** — attached shield prop is expected on KnightV3.

### Step 3 — `EquipOffHand` — load branch

After rig + LeftHand resolve:

1. **If `LoadsViaAddressable(def)`** → `BeginAddressableOffHand(def, vis, hand, id, _offHandGeneration)` (async)
2. **Else** → sync `LoadWeaponMesh(vis.mesh)` → `AttachOffHandProp(prop, vis, hand, id)`

`LoadsViaAddressable`: `def.loadVia == "addressable"` OR `prefabPath` starts with `gear/`.

### Step 4 — `BeginAddressableOffHand` — native on completion

On Addressables success:

```csharp
var nativeVis = CopyOf(vis);
nativeVis.native = true;
// instantiate prefab → AttachOffHandProp(prop, nativeVis, hand, id);
```

On failure: `FallbackResourcesOffHand` with `fb.native = false` (Tripo preset grip).

### Step 5 — `AttachOffHandProp` — three seat modes

**File:** `EquipmentController.cs` · `AttachOffHandProp`

```
if (fullOverride)     → NormalizeInto + saved vertical delta (WO-577 Seating Editor)
else if (vis.native)  → SeatNative + parent with localPosition=0, localRotation=identity
else                  → NormalizeInto + preset gripPos/gripEuler from Shield()
```

**Native Blink path (critical):**
```csharp
FlowTrace.Step("Equip", "off-hand seat: NATIVE (trust authored grip-at-origin, scale-only)");
SeatNative(prop, gripRoot.transform, vis.heldLength);
gripRoot.transform.SetParent(hand, false);
gripRoot.transform.localPosition = Vector3.zero;
gripRoot.transform.localRotation = Quaternion.identity;
```

**Tripo path:**
```csharp
FlowTrace.Step("Equip", "off-hand seat: NormalizeInto + preset grip (Tripo/Resources shield)");
NormalizeInto(prop, gripRoot.transform, vis.heldLength);
gripRoot.transform.SetParent(hand, false);
gripRoot.transform.localPosition = vis.gripPos;      // (-0.05, 0, 0)
gripRoot.transform.localRotation = Quaternion.Euler(vis.gripEuler);  // (-58, 16, -90)
```

### Step 6 — Render verify + §12 landed seat log

After seat, `VerifyWeaponRendersNow(gripRoot, hand, id)` — detach on fail.

Success log (tune against this if felt wrong):
```
AttachOffHandProp: off-hand '{id}' verified rendered + seated on '{hand.name}'
(native={vis.native}, localPos=..., worldPos=...).
§12: if the owner still sees it off the arm, this is the exact landed seat to tune...
```

### Step 7 — Carry state (town vs combat)

**Fields (serialize defaults):**
```csharp
_sheatheOffHandLocalPos   = (0.12f, 0.06f, -0.17f);
_sheatheOffHandLocalEuler = (0f, 270f, 192f);   // owner: Y+180, Z+180 from (0,90,12)
```

`ApplyHoldPose()`:
- **Combat (`_combatActive`):** off-hand parented to `_offHandHand` (LeftHand), use `_offHandDrawnLocalPos/Rot` captured at attach
- **Town:** parent to back socket (`ResolveBackSocket`), use sheathe pos/euler above

Same wave/arena signal as main weapon (`WavePhase.Countdown/Active` or `BattleArena.AnyBattleInProgress`).

### Step 8 — Offset Forge (Tripo calibration only)

**File:** `Assets/OffsetForge/offsets.json`

```json
{
  "id": "shield_A",
  "rot": { "x": -58.0, "y": 16.0, "z": -53.0 },
  "pos": { "x": 0.0, "y": 0.0, "z": 0.0 },
  "scale": 1.0,
  "fullOverride": false
}
```

`AttachOffHandProp` only honours `fullOverride` offsets (not the main-hand nudge-on-geometry path) — avoids double-rotate on preset grip.

### Step 9 — Body rebind

`ReseatForBody()` must re-call `EquipOffHand` after `HeroBodySwapper` swaps KnightV3 body — otherwise shield stays on old bone ("hangs off arm" after armor/body swap).

---

## Files touched (complete list)

| File | Change |
|---|---|
| `Assets/_Modules/Village/Hero/EquipmentController.cs` | `Shield()`, `EquipOffHand`, `BeginAddressableOffHand`, `AttachOffHandProp`, `ApplyHoldPose`, sheathe fields, `PackageBakedGear` skip |
| `Assets/OffsetForge/offsets.json` | `shield_A` calibration entry |
| `Assets/Editor/WeaponPropReadablePostprocessor.cs` | Shared with WO-478 |

**Do NOT touch:** moat/castle/`VillageSceneBuilder`, sword main-hand WO-478 branches (except shared `SeatNative`), `GearLoadout` auto-best.

---

## Verification

### A. Compile + braces
Same as WO-478: `CompileGate.Run` → `COMPILE_GATE_OK`; brace-check `EquipmentController.cs`.

### B. Force shield for log proof

Registry / PlayerPrefs:
- `dotr-equip-offhand-knight` = `knight_shield_starter` (or ensure `EquippedOffHand` = `tripo_shield_a` in save)

Default knight loadout often equips `tripo_shield_a` via `BestWeapon` off-hand — both are valid Tripo-path tests.

### C. Player.log markers

**Tripo/Resources (`knight_shield_starter` / `tripo_shield_a`):**
```
[Flow:Equip] off-hand branch: RESOURCES map (mesh='shield_A')
[Flow:Equip] off-hand seat: NormalizeInto + preset grip (Tripo/Resources shield)
[Flow:Equip] AttachOffHandProp: off-hand '...' verified rendered + seated on 'CC_Base_L_Hand' (native=False, ...)
```

**Blink Addressable (when catalog row has `loadVia: addressable`):**
```
[Flow:Equip] off-hand branch: ADDRESSABLE ('gear/weapon/Shield1h_XX')
[Flow:Equip] off-hand seat: NATIVE (trust authored grip-at-origin, scale-only)
[Flow:Equip] AttachOffHandProp: ... (native=True, localPos=(0,0,0), ...)
```

### D. PO felt-verify (human path)

1. Build + launch `-bootScene MainCastle_Hall` (same as WO-478)
2. KnightV3 + shield equipped (`knight_shield_starter` or `tripo_shield_a`)
3. **In combat (wave active):** shield strapped to left forearm/hand — not floating beside arm
4. **In town (no wave):** shield on back, face reads correctly (not 180° flipped)
5. **No duplicate shield** on KnightV3 (single visible shield)
6. Body swap / re-enter scene: shield still on LeftHand, not dangling

---

## Acceptance criteria

- [ ] Tripo shield uses `NormalizeInto` + preset grip; logs `native=False`
- [ ] Blink addressable shield uses `SeatNative` + zero offset; logs `native=True`
- [ ] `PackageBakedGear` heroes skip off-hand attach (no double shield)
- [ ] Town = sheathed on back; combat = drawn on LeftHand
- [ ] `COMPILE_GATE_OK` + brace balance
- [ ] PO felt pass on KnightV3 shield (arm + back carry)

---

## Relationship to WO-478 (sword)

| | Sword (WO-478) | Shield (WO-590) |
|---|---|---|
| Hand | RightHand | LeftHand |
| Native flag | IdMap `Native(Sword("sword_A"))` | Set at Addressable completion |
| Tripo seat | Geometry inference (or `ff.weapongripinfer` for native sword) | Preset `Shield()` grip always |
| Feature flag | `ff.weapongripinfer` | None (path split by `vis.native`) |
| Shared helper | `SeatNative`, `NormalizeInto`, `VerifyWeaponRendersNow` | same |

Implement **both** WOs when replicating the full knight kit grip fix.

---

## Rollback / tuning

- **Tripo shield arm position:** tune `Shield()` `gripPos` / `gripEuler` OR `offsets.json` `shield_A` (keep `fullOverride: false` unless using Seating Editor vertical baseline)
- **Back carry:** tune `_sheatheOffHandLocalPos` / `_sheatheOffHandLocalEuler` only (owner-felt constants)
- **Do not** re-enable geometry inference on native Blink shields — use Addressable + native path instead

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
