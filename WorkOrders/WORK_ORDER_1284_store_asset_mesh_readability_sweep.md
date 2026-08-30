# WORK ORDER 1284 — every Asset-Store mesh the orientation code measures must be Read/Write

**Status:** DONE 2026-08-30 — suite landed + all 405 MWP1 metas flipped. `checked=417 passed=417 offenders=0`, proven in BOTH directions. `COMPILE_GATE_OK`. Committed 9aa57546.
**Minted:** 2026-08-30 (CLI seat, main line; banner bumped 1284 -> 1285 in the same edit)
**Lane:** Editor regression / asset import (no gameplay code)
**Provenance:** owner, 2026-08-30: *"we need to add as test for all store assets"* — raised
immediately after PROD-019's root cause landed.
**Parent defect:** PROD-019. Guard for the single shield already shipped as
`AttachmentOffsetRegression.Case11_ShieldMeshIsReadable`. This WO generalises it.

---

## The defect class

`Fantasy_Shield.FBX` imported with `isReadable: 0`. **The Editor keeps mesh data CPU-side
regardless of that flag, so `mesh.vertices` reads fine in Play mode and the shield looks correct.
In a PLAYER BUILD a non-readable mesh returns ZERO vertices.**

`EquipmentController` measures meshes to derive orientation. With zero vertices it does not error —
it *degrades*, and says so:

```
ShieldHandleSide 'EquipmentProp_OffHand_Mesh': only 0 readable vertices — 1 of 1 mesh(es) have
Read/Write DISABLED ... so NO flip is applied ... it may be worn strap-outward.
```

```
SheatheSign 'EquipmentProp_Weapon': ... taper unavailable (0 readable vertices — Read/Write is OFF
on this prop, which is the SHIPPED state of the live weapons) ...
```

Read that second line again: **"the SHIPPED state of the live weapons."** The sword is guessing its
sheathe sign on the same coin-flip today and happens to land right. This is not one bad asset — it
is the default state of the gear catalogue, and it is invisible to every editor-side proof.

**Cost of not catching it:** PROD-019 consumed an evening and three commits
(`30a3e7a1e`, `ac40ab578`, `74d9e6546`) dialling the seat and adding orientation heuristics. The
seat was never wrong — a device capture on 2026-08-30 showed the authored Offset Forge row applying
byte-exact. Every proof available in the Editor was green while the device was broken.

---

## Scope

1. **New suite** — `Assets/Editor/Regression/StoreAssetMeshReadabilityRegression.cs`, registered in
   `DataRegression` alongside the others, emitting its own bracketed tag.
2. **Build the asset list FROM THE AUTHORITIES, never a hand-typed list** (a hand list goes stale
   the first time someone adds gear — that is the failure mode CLAUDE.md §2 and §5 both record):
   - the **Gear Addressable group** (`Assets/AddressableAssetsData/AssetGroups/Gear.asset`) — every
     entry, resolved GUID -> prefab -> its `MeshFilter`/`SkinnedMeshRenderer` meshes -> the source
     model's `.meta`.
   - `Assets/Resources/Heroes/Props/Weapons/` — the Resources branch
     `EquipmentController.LoadWeaponMesh` uses (e.g. `sword_A`, `staff_A`).
3. **Assert `isReadable: 1`** on every source model `.meta` behind those meshes.
4. **SKIP, never fail, on an absent file.** `Assets/Supercyan/`, `Assets/Blink/` and
   `Assets/polyperfect/` are gitignored paid packs; a clone that has not re-imported them
   legitimately has no file. Absent => `Debug.LogWarning` + counted as SKIPPED. **A skip must never
   read as a pass** — print the skipped count in the suite's summary line.
5. **Report every offender in one run**, with the path and the consuming prefab/address. Do not
   stop at the first.

## Acceptance criteria

- [ ] Suite registered; `DataRegression.RunAll` marker count increments by one.
- [ ] With the shield's `isReadable` flipped to `0`, the suite **FAILS** and names the file.
      **Prove this direction explicitly** — memory `prove-the-success-path-not-just-the-refusal`
      records a guard that aborted every good run while exiting 0 because only one side was tested.
- [ ] With packs absent, the suite SKIPS with a warning and does **not** fail.
- [ ] The summary line reports checked / passed / skipped counts.
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites`, judged by marker on a fresh log.

## Fixing what it finds

For each offender: enable Read/Write on the FBX importer, then **track its `.meta` by the
`.gitignore` exception pattern already added for the shield** (`.gitignore`, PROD-019 block) so the
setting survives a clone without any paid-pack binary entering git history.

⚠ **ON A FRESH CLONE, RE-IMPORT THE PACKS BEFORE OPENING UNITY.** A tracked `.meta` with no asset
beside it is an orphan, and Unity deletes orphan `.meta` files — silently restoring the defect with
no diff to explain it. This caveat belongs in `docs/SUPERCYAN_REIMPORT.md` too.

## What NOT to do

- ⛔ **Do not add a second face/orientation heuristic.** The trace forbids it in its own words:
  *"a single-renderer plate has no bounds-only signal that separates its two faces, so any such rule
  would be a coin-flip."* Three such heuristics already failed on PROD-019.
- ⛔ Do not enable Read/Write blindly on every mesh in the project. It keeps a CPU copy and costs
  memory. Scope strictly to meshes the orientation code actually measures.
- Do not commit pack binaries. `.meta` only.
- Do not weaken the skip-on-absent rule into a silent pass.
