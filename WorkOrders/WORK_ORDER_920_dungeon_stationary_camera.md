> ## RECONCILED 2026-08-08 - true status is DONE
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: shipped in `3b344919` - new `DungeonCameraProfile.cs` (118 lines) + `SmartMobileCamera.cs` (+134) + `DungeonFpvRegression.cs` (+219). Note the WO TEXT described the wrong pipeline: composed dungeons bake NO camera at all. WHY IT WAS MISLABELLED: this WO file was FIRST ADDED in the very commit that implemented it - it was BORN STALE, never neglected.
> The previous Status line read "READY TO IMPLEMENT" and was wrong.

# WORK ORDER 920 — Dungeon camera: stationary exploration (stop the bounce)

**Status: DONE** (`3b344919`; reconciled 2026-08-08, see banner; not felt-verified)  
**Minted:** 2026-08-07 (CLI / Grok — owner: “stationary camera view for in dungeons” + bounce)  
**Silo:** Dungeons / Camera (runtime code; no scene hand-edit)  
**Roles:** CLI implement; PO felt-closes motion sickness / stability  
**Depends on:** **WO-919 enclose strongly preferred first** — locked OTS over 2.8 m open walls still shows sky; stationary cam alone does not fix blue  
**Related:** `DungeonCameraRig`, `FeatureFlags.DungeonFpv` / `DungeonCameraIso`, `DungeonFpvRegression`, combat `SetCombatFraming`  
**Owner proof:** screenshots show elevated third-person over short maze; free-look / wall-pull reads as bounce.

---

## 0. One-line truth

Shipped dungeon default is **first-person free-look** (`ff.dungeonfpv` default **ON**), with arena fights snapping to **over-the-shoulder** and OTS **AvoidObstacles** yanking the camera off walls. Owner wants a **stable, stationary-feeling** dungeon view — not a look-around FPV that drifts and not a combat thrash that re-seats the rig every fight.

---

## 1. Grounded cause

| Behavior | Where | Felt effect |
|----------|--------|-------------|
| FPV default ON | `FeatureFlags.DungeonFpv` → `Get("dungeonfpv", defaultOn: true)` | Free yaw/pitch every LateUpdate |
| Mouse delta look (desktop) | `DungeonCameraRig.SampleLookDelta` — no RMB hold | Accidental drift / “camera keeps moving” |
| Right-half drag look (mobile) | Same sampler | Fine for FPV; wrong for stationary |
| Combat framing swap | `SetCombatFraming(true/false)` FPV ↔ OTS | Pop / bounce on stage + end |
| OTS AvoidObstacles | `ThirdPersonFollow.AvoidObstacles` when not FPV | Camera **pulls in/out** on walls in tight 6 m cells |
| OTS height ~2.9 m | shoulder Y 2.2 + arm 0.7 forced in `Bind` | Sits **above** pre-919 2.8 m walls (sky); after 919 still needs lower/stable seat |
| Regression locks FPV ON | `DungeonFpvRegression` | Any default change **must** update the suite deliberately |

Historical note in rig header: FPV was chosen **over raising the ceiling**. Owner now wants **both** enclose (WO-919) **and** a calm camera — reverse the “FPV instead of walls” trade for explore.

---

## 2. Product intent (owner language)

- **Exploration:** stationary / locked framing — camera **follows position**, does **not** free-orbit from mouse noise or wall collision thrash.
- **Readable combat:** may reframe for fights, but **no nauseating bounce**; prefer one calm OTS for both explore + fight **unless** a single staging pop is proven needed.
- FPV remains an **opt-in** A/B (`ff.dungeonfpv=1`), not the ship default.

---

## 3. Scope

### Phase A — Default mode = locked over-the-shoulder (stationary explore)

1. Set **`FeatureFlags.DungeonFpv` default to `false`** (OTS becomes default via `ResolveMode`).
2. **Disable free-look** when not in FPV (already: LateUpdate no-ops if `!_fpvActive`).
3. **AvoidObstacles OFF** for explore OTS (or CameraRadius + soft damping only after owner OK — default recommendation: **Enabled = false** so walls never yank). Document if a soft pull is re-enabled later.
4. **Damping:** calm, non-nauseating follow (existing `_otsDamping` ~0.18 is a starting point; increase slightly if still jittery; do not go rubber-band soft).
5. **Seat under ceiling** after WO-919: shoulder + arm total camera height must stay **clearly below** room ceiling (e.g. shoulder Y ≤ 1.8–2.0, arm ≤ 0.35, distance ~3–3.5) — remove or retune the 2026-07-26 “taller camera” force-assign in `Bind` that hardcodes 2.2 / 0.7 / 3.8 if it fights enclose.
6. **No mouse look** in OTS default. Optional later: hold-to-look only if owner asks — not V1.

### Phase B — Combat framing (reduce bounce)

Pick **one** (recommend **B1** unless combat unreadable):

| Option | Behavior |
|--------|----------|
| **B1 (recommended)** | **No combat reframe** — same locked OTS for explore + arena. `SetCombatFraming` becomes no-op or only adjusts FOV slightly. |
| **B2** | Keep OTS force on stage, but **do not** restore FPV (stay OTS); single pop into combat only. |
| **B3** | Keep FPV↔OTS only if FPV is player-opted ON. |

Wire `DungeonController` subscriptions accordingly; keep null-safe.

### Phase C — FPV opt-in (preserve, do not delete)

- When `ff.dungeonfpv=1`: current free-look + body hide + AvoidObstacles off still valid.
- Desktop: consider **RMB-hold to look** so idle mouse does not drift (nice-to-have; do if cheap).
- Update **`DungeonFpvRegression`**:
  - Default ON → **default OFF**.
  - Still assert FPV code path exists when flag ON (wiring, not default).
  - Assert combat framing behavior matches chosen B1/B2/B3.

### Phase D — Iso flag

- `ff.dungeoniso` stays OFF by default; still loses to FPV when both set (or document new priority: FPV > Iso > OTS).

### Phase E — Proof

- Play composed dungeon after WO-919 bake: walk a full corridor turn — camera follows hero without free spin; no wall yank.
- Enter/exit one arena fight — no multi-second thrash.
- Capture PNG: framing under ceiling, hero readable, **no sky** (WO-919).
- `COMPILE_GATE_OK` + `REGRESSION_OK` including updated `[dungeon-fpv]` suite.

### Phase F — Out of scope

- Room geometry / ceiling (WO-919).
- Hub / overworld camera.
- New Cinemachine brain stack rewrite — stay on existing `DungeonCameraRig` + ThirdPersonFollow.

---

## 4. Files (likely)

| File | Action |
|------|--------|
| `Assets/_Modules/Core/FeatureFlags.cs` | `DungeonFpv` defaultOff + comment rewrite |
| `Assets/_Modules/Dungeons/DungeonCameraRig.cs` | OTS seat, AvoidObstacles, remove tall force if needed; optional RMB look |
| `Assets/_Modules/Dungeons/DungeonController.cs` | Combat framing policy B1/B2/B3 |
| `Assets/Editor/Regression/DungeonFpvRegression.cs` | Default + framing assertions |
| `DungeonHero.cs` | Only if tap-to-move / look gates need OTS default tweak |

---

## 5. Acceptance

- [ ] Default explore cam is **locked OTS** (or documented stationary equivalent) — **not** free-look FPV.  
- [ ] No continuous camera orbit from idle mouse / accidental drag in default mode.  
- [ ] AvoidObstacles does not bounce the camera in tight rooms (off or proven soft).  
- [ ] Camera height stays **under** enclosed ceiling (post-919).  
- [ ] Combat transition does not thrash (B1/B2/B3 documented in RESULT).  
- [ ] `ff.dungeonfpv=1` still enables FPV free-look.  
- [ ] `DungeonFpvRegression` updated and green.  
- [ ] Owner felt: “stationary” / no bounce (PO closes).  
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK`.

---

## 6. Implement order

1. Prefer **WO-919** first (enclose).  
2. Then this WO (camera).  
If 920 ships first, accept temporary “stable cam looking at open sky” until 919 rebake.

---

## 7. RESULT

`WorkOrders/WORK_ORDER_920_dungeon_stationary_camera.RESULT.md`
