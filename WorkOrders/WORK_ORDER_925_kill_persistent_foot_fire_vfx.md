> ## RECONCILED 2026-08-08 - true status is NOT STARTED
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: commit `6e0cde93` added this `.md` file ONLY - no VFX code changed with it.
> The previous Status line read "READY TO IMPLEMENT" and was wrong.

# WORK ORDER 925 — Kill or condition permanent foot fire / spark VFX under the hero

**Status:** SPEC — NOT STARTED (reconciled 2026-08-08, see banner)  
**Minted:** 2026-08-07 (Grok Imagine visual review — Development Build 52s recording)  
**Silo:** VFX / Hero  
**Roles:** CLI implement; instrument first if multiple candidates  
**Related:** WO-888 `HeroHpStateAura` (NearDeath = TinyFlames); WO-921 dungeon fire cosmetic; VFX loop budget  

---

## 0. One-line truth

Owner recording shows **yellow/green fire or spark particles constantly under Grom’s feet** in explore **and** combat — reads as a permanent buff indicator or “standing in fire,” not a conditional low-HP tell.

---

## 1. Likely sources (instrument — do not guess-and-delete the wrong one)

| Candidate | Path | When it should run |
|-----------|------|---------------------|
| **`HeroHpStateAura` NearDeath** | `HeroHpStateAura.cs` → `Aura_NearDeath` / TinyFlames | Only HP below near-death cutoff |
| **`HeroHpStateAura` LowHealth** | SmokeEffect scaled to body | Only below wounded cutoff |
| **`HeroHpStateAura` Healing** | RisingSteam | Only while regen hold active |
| Dungeon torch / dress light | `DungeonDresser` | Should not parent to hero |
| Other aura (pet, gear, ability) | grep `VFXType.Aura_` / Play on hero feet | Conditional |

**First step (§12):** play dungeon with FlowTrace on `HeroHpStateAura` (slot, HP fraction, handle). If `_slot` is NearDeath at full HP → bug. If slot is None and particles still present → different system.

---

## 2. Product rules

1. **Healthy explore:** no fire under feet.  
2. Low-HP aura may use gutter flame **only** when HP is actually low (WO-888 accessibility — keep, don’t delete the system).  
3. Scale stays **body-sized** (already has ScaleMul*) — never room-filling.  
4. Must **Stop** on heal above cutoff, death, disable, scene unload (existing handle law).

---

## 3. Scope

### Phase A — Prove the emitter

Log once/sec while particles visible: `HeroHpStateAura` slot, `HeroHealth` fraction, active VFX handles on hero root.

### Phase B — Fix

| Finding | Fix |
|---------|-----|
| NearDeath while HP high | Fix Drive thresholds / false Drive calls |
| Loop never Stopped | Fix Apply/Drive exit paths |
| Wrong recipe scale | Lower NearDeath scale further; seat at ankles only if intentional |
| Different component | Disable or gate that emitter |

### Phase C — Acceptance

- [ ] Full HP walk in dungeon: **no** foot fire.  
- [ ] Drop HP below near-death in dev: gutter aura appears; heal: stops.  
- [ ] Combat at full HP: no constant foot sparks.  
- [ ] Capture before/after.  

## RESULT

`WorkOrders/WORK_ORDER_925_kill_persistent_foot_fire_vfx.RESULT.md`
