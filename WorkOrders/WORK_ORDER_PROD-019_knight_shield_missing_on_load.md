# PROD-019 — Knight shield is equipped but missing on the body at load

**Status:** READY TO IMPLEMENT — **CAUSE narrowed from live Seeker logcat** (2026-08-29 ~18:29); not attach-fail  
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
