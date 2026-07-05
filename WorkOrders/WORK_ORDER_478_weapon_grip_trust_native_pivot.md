# WORK_ORDER_478 — Knight sword grip: trust native Blink pivot (implementation playbook)

**Status: IMPLEMENTED @ `C:\EoA` (2026-07-05)** — this doc is the **CLI replication playbook** for Claude/UI to route to CLI on any branch/worktree. Do **not** re-debug the moat or re-litigate WO-435; apply these steps verbatim.

**Type:** EXISTING · **Silo:** Hero/Equipment · **Supersedes:** WO-435 (banner only — do not reopen).

---

## Problem (closed RCA — do not re-investigate)

Default knight sword = `knight_starter` → IdMap → `sword_A` → `Assets/Resources/Heroes/Props/Weapons/sword_A.prefab` (Blink `Sword1h_01`, **grip-at-origin, identity transform**, `native=true`).

Pre-WO-478, `AttachLoadedProp` **discarded** that pivot for **all** melee and ran geometry inference (`NormalizeInto` + `SeatHiltLowerHalf` + `ComputeMeleeGripRotation`). Stylized Blink swords have no crossguard width spike → inference guesses wrong → 180° flip or mid-hilt float.

**Important catalog trap:** `GearLoadout.Refresh()` auto-equips `BestWeapon(job, level)` which at knight level 1 is often **`tripo_sword_f`** (higher `damageMult`), **not** `knight_starter`. Felt-verify and log capture must **force** `knight_starter` (see Verification §).

---

## Fix summary

| What | Where |
|---|---|
| Route native melee through `SeatNative` (scale-only, keep authored pivot) | `EquipmentController.AttachLoadedProp` |
| Gate legacy geometry inference behind `ff.weapongripinfer` (default **OFF**) | `FeatureFlags.WeaponGripInfer` |
| Mark `knight_starter` as native in IdMap | `EquipmentController.IdMap` |
| Rotation = `vis.gripEuler` × `MeleeGripNudge` only (no `ComputeMeleeGripRotation`) | `AttachLoadedProp` when `trustNativePivot` |
| §12 seat dump + branch logging | `LogGripSeatDiagnostics`, attach log line |
| Belt-and-braces: weapon FBX Read/Write in builds | `WeaponPropReadablePostprocessor` + `.meta` |
| Offset Forge: `sword_A` all-zero = pure native (no nudge) | `Assets/OffsetForge/offsets.json` |

---

## Step-by-step implementation (CLI — apply in this order)

### Step 1 — Feature flag (rollback only)

**File:** `Assets/_Modules/Core/FeatureFlags.cs`

Add near other `ff.*` gates:

```csharp
/// <summary>DEPRECATED weapon-grip geometry inference for NATIVE melee (WO-478).
/// Default OFF → native melee trusts SeatNative. PlayerPrefs "ff.weapongripinfer" = 1
/// restores the pre-WO-478 path.</summary>
public static bool WeaponGripInfer => Get("weapongripinfer", defaultOn: false);
```

Mark every restored inference branch in `EquipmentController` with:
`// DEPRECATED (WO-478): ff.weapongripinfer only — see WORK_ORDER_478_...`

### Step 2 — IdMap: native knight starter

**File:** `Assets/_Modules/Village/Hero/EquipmentController.cs`

In `IdMap`, knight starter must use `Native()`:

```csharp
{ "knight_starter", Native(Sword("sword_A")) },   // Blink Sword1h_01 (grip-at-origin)
```

`Native()` sets `vis.native = true`. Non-native knight tiers (`knight_iron` → `sword_D`, etc.) stay **non-native** (geometry path unchanged).

### Step 3 — `AttachLoadedProp` seating branch

**File:** `Assets/_Modules/Village/Hero/EquipmentController.cs` · method `AttachLoadedProp`

After offset resolution (`offsetKey`, `meleeSeat`, `fullOverride`), compute:

```csharp
bool trustNativePivot = vis.native && !fullOverride &&
    (!meleeSeat || !FeatureFlags.WeaponGripInfer);
```

**When `trustNativePivot` is true:**
1. `FlowTrace.Step("Equip", "seat: NATIVE melee (WO-478 trust grip-at-origin + scale)")` (or non-melee native variant)
2. Call `SeatNative(prop, gripRoot.transform, vis.heldLength)` — **do not** call `NormalizeInto` / `SeatHiltLowerHalf`

**When false (DEPRECATED / non-native):**
1. `NormalizeInto` + `SeatHiltLowerHalf` for melee
2. Log `infer={FeatureFlags.WeaponGripInfer}` on the trued+seated line

### Step 4 — Rotation after seat

**When `trustNativePivot && meleeSeat` (WO-478 default):**
```csharp
_baseGripRot = Quaternion.Euler(vis.gripEuler) * Quaternion.Euler(MeleeGripNudge(vis.kind));
```

**Do NOT** call `ComputeMeleeGripRotation` for native melee unless `ff.weapongripinfer=1`.

Non-native melee: unchanged `ComputeMeleeGripRotation`.

### Step 5 — Attach log + §12 diagnostics

On attach, log must include:
`trustNative={trustNativePivot} infer={FeatureFlags.WeaponGripInfer}`

After offset nudge (or no offset), call:
```csharp
LogGripSeatDiagnostics(prop, gripRoot.transform, hand, weaponId,
    trustNativePivot ? "WO-478-native" : "geometry-infer");
```

`LogGripSeatDiagnostics` dumps `prop.localPos`, `prop.localEuler`, `gripRoot` locals, hand bone name.

### Step 6 — Offset Forge (no override for sword_A)

**File:** `Assets/OffsetForge/offsets.json`

`sword_A` entry must be **all zeros**, `fullOverride: false`:
```json
{ "id": "sword_A", "rot": {"x":0,"y":0,"z":0}, "pos": {"x":0,"y":0,"z":0}, "scale": 1.0, "fullOverride": false }
```

Native path logs: `no offset stored for 'sword_A' — native pivot kept (WO-478).`

### Step 7 — Editor seating preview parity

**File:** `EquipmentController` · in-game Seating Editor replay path

When re-seating for preview, if `trustNative` and infer flag OFF, use `SeatNative` — **not** the old raw-pivot bypass.

### Step 8 — Belt-and-braces: readable meshes in player builds

**Files:**
- `Assets/Editor/WeaponPropReadablePostprocessor.cs` — force `isReadable=true` on `Assets/Resources/Heroes/Props/Weapons/**`
- Weapon prop `.meta` files — `isReadable: 1` committed

This fixes editor≠build divergence when the **deprecated** inference path runs (non-native or `ff.weapongripinfer=1`).

### Step 9 — Supersede WO-435

**File:** `WorkOrders/WORK_ORDER_435_weapon_grip_orientation.md`

Add top banner: **SUPERSEDED by WO-478** — do not implement WO-435 fixes.

---

## Files touched (complete list)

| File | Change |
|---|---|
| `Assets/_Modules/Core/FeatureFlags.cs` | `WeaponGripInfer` flag |
| `Assets/_Modules/Village/Hero/EquipmentController.cs` | IdMap, `AttachLoadedProp`, `LogGripSeatDiagnostics`, seating editor parity, DEPRECATED comments |
| `Assets/OffsetForge/offsets.json` | `sword_A` zeros (if not already) |
| `Assets/Editor/WeaponPropReadablePostprocessor.cs` | Read/Write postprocessor (if missing) |
| `WorkOrders/WORK_ORDER_435_weapon_grip_orientation.md` | Superseded banner |

**Do NOT touch:** `GearLoadout` auto-best logic, `VillageSceneBuilder`, moat/castle geometry, unrelated equip UI.

---

## Verification (CLI headless + PO felt)

### A. Compile gate
```powershell
powershell -ExecutionPolicy Bypass -File .\run-unity-method.ps1 -Method DeNelle.Editor.CompileGate.Run -LogName compile-gate-wo478.log
```
Expect: `COMPILE_GATE_OK`

### B. Brace balance (every `.cs` edited)
```python
python3 -c "p='Assets/_Modules/Village/Hero/EquipmentController.cs'; c=open(p).read(); print(c.count('{'), c.count('}'))"
```

### C. Force the correct weapon for log proof

Registry / PlayerPrefs before launch:
- `dotr-equip-weapon-knight` = `knight_starter`
- `ff.weapongripinfer` = `0` (default)

Without this, logs will show `tripo_sword_f` + `geometry-infer` (wrong weapon for this WO).

### D. Player.log markers (native path — must all appear)

```
[Flow:Equip] seat: NATIVE melee (WO-478 trust grip-at-origin + scale)
[Flow:Equip] attached 'knight_starter' ... trustNative=True infer=False
[Flow:Equip] WO-478 seat dump [WO-478-native] 'knight_starter': ...
[Flow:Offset] no offset stored for 'sword_A' — native pivot kept (WO-478).
```

### E. Rollback proof
`PlayerPrefs "ff.weapongripinfer" = 1` → logs show `DEPRECATED GEOMETRY` + `trustNative=False` for native melee.

### F. PO felt-verify (human path only)
1. Build: `build-windows.ps1` (wipe `Builds/Windows` first)
2. Launch: `Builds/Windows/DefendersOfTheRealm.exe -bootScene MainCastle_Hall`
3. KnightV3 body (`ff.knightv3` ON, default), equip **Squire's Blade** (`knight_starter`)
4. **Pass:** hilt in palm, blade forward, no 180° flip, no mid-hilt float
5. **Fail rollback test:** `ff.weapongripinfer=1` reproduces old wrong grip

---

## Acceptance criteria

- [ ] `knight_starter` / `sword_A` uses `SeatNative` by default (`trustNative=True`, `infer=False` in log)
- [ ] Non-native knight weapons (`tripo_sword_f`, `sword_D/F/G`) still use geometry path (unchanged)
- [ ] `ff.weapongripinfer=1` restores deprecated path without code revert
- [ ] `COMPILE_GATE_OK` + brace balance on all touched `.cs`
- [ ] PO confirms felt grip on KnightV3 + `knight_starter`

---

## Rollback

`PlayerPrefs.SetInt("ff.weapongripinfer", 1)` — no git revert required for emergency compare.