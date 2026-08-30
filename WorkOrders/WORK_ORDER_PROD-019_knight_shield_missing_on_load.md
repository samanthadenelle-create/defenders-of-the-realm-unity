# PROD-019 — Knight shield is equipped but missing on the body at load

**Status:** ⛔ REOPENED 2026-08-30 — closed on EDITOR-ONLY evidence; still broken on device. See §0b.  
**Minted:** 2026-08-29 (CLI seat) — banner bumped with PROD-018/020 → PROD-021  
**Priority:** HIGH — first impression / class identity; bag says heater is worn, world does not  
**Provenance:** owner, 2026-08-29: *"the knight still does not render a shield on load"* / *"read the trace… on seeker screen now"*  
**Related (do not merge lanes blindly):** WO-1254 **D-SEAT** (same symptom; this PROD is the focused live fix), WO-994, PROD-005, WO-1215 (seat math), WO-1214 (mage/shield gate — different)

---

## 0. ★ LIVE TRACE READ (Seeker, 2026-08-29 18:29 local) — do not re-guess

Pulled `adb logcat` while owner had Blaise on screen (`logs/device/current-screen-review-20260829.png`: sword visible, **no readable shield**).

| Ruled OUT by the lines | Evidence |
|---|---|
| Loadout null / never equipped | Mesh key `ShieldWithItemLogic` active; continuous seat writes |
| Addressable InvalidKey / missing bundle | **0** InvalidKey in this session for the shield |
| PackageBaked skip | **0** PackageBaked hits |
| Attach never ran | `SURFACE-SEATED` + `seatWrite` every few seconds |

| What the lines DO say | |
|---|---|
| Parent | `SheatheSocket_ArmOff` (arm sheathe, not missing) |
| Seat write (WO-994) | `pos=(-0.01, 0.19, -0.19) scale=(1.04…) parentLossy=(1.67…)` — **not** identity |
| Derived pose | `faceOffOutward=180deg` · frame `size=(0.365, 0.45, 0.115)` · **trace’s own warning:** *if narrowest is not the plate’s thinness, the pose is right about a WRONG frame — that reads as a flat shield* |
| Offset fallback | `sheathed FALLBACK … pos=(0,0,0) rot=SKIPPED` every ~1s — **adds** zero (does not wipe); noise, not the killer |

**CAUSE class (named from live lines):**  
`attached-invisible` / **wrong-frame edge-on** — `faceOffOutward≈180` after a permanent `ShieldWithItemLogic` +180° LongAxis flip; code believed the seat was healthy; player saw sword only.

### Fix landed (code, 2026-08-29 — needs Seeker retest / new APK)
1. **Removed** the asset-specific +180° LongAxis flip that forced `faceOff≈180`.  
2. Stronger rearward cant on arm sheathe outward (`0.65` → `1.15` × `-body.forward`).  
3. `EnsureWeaponRenderersVisible` after sheathed off-hand pose (parity with main hand).  

**Still required before DONE:** owner felt on device — plate readable from rear/¾ town camera + combat draw. Paste a post-fix `faceOffOutward≈0` seat-proof line in RESULT.

---

## 0b. ★ REOPENED 2026-08-30 — device capture, build `2026.08.30.347947`

Closed on 2026-08-30 01:45 against **editor-only** proof (an isolated `debug.txt`, "Persist Play:
Inspector stayed on the row", and `AttachmentOffsetRegression`, which lives in `Assets/Editor/` and
asserts JSON numbers). Acceptance criteria **#2 (player-visible screenshots)** and **#6 (PO
felt-close)** were never met. Owner on device, 2026-08-30: still not readable.

### What a cold-boot device capture PROVES (do not re-derive any of this)

| Claim | Status |
|---|---|
| Offset Forge locked row applies on device | ✅ `AttachOffHandProp MEASURED after hold: parent='Socket_Shield' fullOverride=True GRIP lPos=(-0.103, 0.164, -0.238) lEuler=(1.9, 311.7, 232.1) lScale=(0.71,…)` — byte-exact vs the locked row (`311.7/232.1` = `-48.302/-127.941`) |
| Renderer is live | ✅ `VerifyWeaponRenders: renderers total=1 enabled=1 withMesh=1 inactiveGo=0 => renders=True seated=True` |
| Addressable loads | ✅ completes in ~630 ms; the 7 in-flight skips + 6 `LateAttachRetry` all fall inside that window and correctly decline to rebuild — **the WO-994 guard works** |
| Gear ships in the APK | ✅ `Gear` group LoadPath = `Local.LoadPath` (not R2). Zero gear bundles in the R2 catalog is CORRECT, not a §16 miss |
| Shield is on screen | ✅ visible in the zoom, hugging the left arm — **present but unreadable**, exactly Grok's `ac40ab578` note: *"the plate still vanishes under the arm/cape"* |

**⛔ THE SEAT IS NOT THE DEFECT. Stop dialling it.** Three commits on 2026-08-29 went at the seat and
at orientation heuristics (`30a3e7a1e` removed a +180 flip, `ac40ab578` canted outward + skin
clearance, `74d9e6546` locked the row). The row was already applying correctly on device.

### The actual blocker, in the code's own words

`Fantasy_Shield.FBX` had `isReadable: 0`, so on device the mesh measured as 0 vertices. **Fixed
under WO-1284** (405 meshes + the shield; `checked=417 passed=417 offenders=0`). With the mesh now
readable the device reports:

```
ShieldHandleSide 'EquipmentProp_OffHand_Mesh': AMBIGUOUS — smoothScore(+Z)=0.073 (-Z)=0.146
margin=0.072 < 0.1, and the dish is flat too (centroidBias=-0.018 < 0.05). Neither face reads as
the smooth one, so NO flip is applied and the existing pose stands.
This mesh needs an owner dial, not a derived guess (WO-1123).
```

**⚠ AND THE HONEST CORRECTION:** the Editor could always read this mesh, so it computed the same
AMBIGUOUS result. Read/Write was a real catalogue-wide defect, but it is **NOT** what made the
shield read correctly in Play mode and wrong on device. Owner confirmed the shield looks unchanged
on `347947`. Whatever made it look right in the editor was never captured — most likely the editor
proof verified Inspector persistence and a favourable viewing angle, not the follow-camera read.

### Next move — the ONLY sanctioned one

The plate is geometrically ambiguous; no derived rule can pick its outward face, and the trace
forbids inventing one: *"a single-renderer plate has no bounds-only signal that separates its two
faces, so any such rule would be a coin-flip."* So the face must be **owner-dialled** into the
Offset Forge row for `ShieldWithItemLogic` (rotate ~180° about the plate normal until the crest
faces outward), then **verified ON DEVICE**, never in Play mode.

⛔ **NO EDITOR-ONLY CLOSE.** This ticket has now been closed once on editor evidence and reopened.
Acceptance #2 and #6 are binding: a device screenshot from the follow camera, and a PO felt-close.

---

## 1. Defect

Loadout / bag **Off Hand** shows **Squire’s Heater** (`knight_shield_starter`).  
Hero body in **town / battle / cave** does not show a readable shield. Local projected-area “pass” was a **false green** (WO-1254).

---

## 2. Seams (read before editing)

| Seam | File (approx) | Role |
|---|---|---|
| Persist / bag truth | `GearLoadout` `EquippedOffHand` / `ApplyPersistedEquip` | What bag claims |
| World mesh | `EquipmentController.EquipOffHand` → `AttachOffHandProp` | What player sees |
| Package bake skip | `PackageBakedGear` / KnightV3 marker clear in `HeroBodySwapper` | Can skip attach |
| Addressable | `ShieldWithItemLogic` / `BeginAddressableOffHand` | Fail → blank slot |
| Re-seat | `ReseatForBody` vs scene-load clear+reequip | Idempotent skip / stale parent |
| Sheathe | `ApplyHoldPose` / `SheatheSocket_ArmOff` | Attached but buried / edge-on |

⛔ **Do not start by re-dialing Offset Forge `shield_A`.** Owner ruled that dial path on prior tickets.

---

## 3. Build order (binding)

### Phase A — Instrument (earn the edit)

Add permanent `[Flow:Equip] seat-proof … CAUSE=…` after attach / scene reapply / LateAttachRetry (spec already sketched in WO-1254 §5.3). Emit in **town, battle, cave**. Prove **prop + active meshed renderer + bounds**, not merely `EquippedOffHand != null`.

Paste three `CAUSE=` lines into the RESULT before any geometry fix.

### Phase B — Hardening that is correct regardless of CAUSE

1. **`ReseatForBody`:** clear `_currentOffHandId` (and main if needed) like `CoReapplyGearAfterSceneLoad` before re-equip — kill idempotent-skip on stale parent.  
2. **Off-hand parity:** after `ApplyHoldPose` in attach path, `EnsureWeaponRenderersVisible` on the **final** parent (main hand already does this; off-hand often does not).  
3. Keep KnightV3 stale `PackageBakedGearMarker` clear before equip; log `baked=` on seat-proof.

### Phase C — Cause-specific only

| `CAUSE=` | Fix |
|---|---|
| `baked-skip` | Clear marker; never skip off-hand on KnightV3 |
| `addr-fail` | Publish/ship Addressable; prove Resources fallback attaches |
| `attach-fail` | Mesh/renderer/layer — do not hide Off Hand in bag |
| `attached-invisible` | Sheathe seat / outward cant; require gameplay-camera proof |
| `loadout-null` | Different path (seed/prefs) — call out, don’t fake mesh |

---

## 4. Acceptance

1. Fresh Knight load: bag Off Hand matches **and** `EquipmentProp_OffHand` has ≥1 active meshed renderer, non-zero bounds.  
2. Shield **player-visible** in town (sheathed), battle (drawn), dungeon — screenshots.  
3. Three `seat-proof` lines with `CAUSE=ok` (or named fixed CAUSE) in RESULT.  
4. No bag presentation hack that hides Off Hand to paper over the mesh.  
5. `COMPILE_GATE_OK` + regression; headless alone is not enough.  
6. PO felt-close.

## 5. Not in scope

Bag redesign / Forge shelf (rest of WO-1254), mage shield job gate (WO-1214), global Offset Forge retune.
