# Knight Snipe — Root-Cause Analysis (2026-06-13)

**Status:** Root cause found (read-only RCA — no code changed).
**Verdict:** WO-398's InReach gate is **measured from the wrong origin**. It is present, it compiles, but in the live village it always evaluates "in reach" no matter how far the knight is. The Strike/Snare path is the snipe.

---

## 1. The ungated path (the snipe)

**File:** `Assets/_Modules/Village/Hero/HeroAbilities.cs`
**Method:** `ResolveEffect(...)`, the `Strike`/`Snare` case.

```
386   Vector3 atk = AimPointOverride ?? origin;
...
427   float maxR = def.Range + _enemyHitRadius;
428   var foe = InReach(LockedTarget, atk, maxR)
429       ? LockedTarget
430       : NearestHostile(atk, maxR);
```

`InReach` (lines 568–572):

```
568   private static bool InReach(IDamageable target, Vector3 origin, float maxRange)
569   {
570       if (target == null || !target.IsAlive) return false;
571       return (target.WorldPosition - origin).sqrMagnitude <= maxRange * maxRange;
572   }
```

The reach test compares the target's position to **`atk`**, not to the hero. `atk` is set on line 386 to `AimPointOverride ?? origin`.

The knight's Q "Shield Bash" is `effect: "strike"`, `range: 3.4` (verified in `Assets/Resources/Data/Canonical/abilities.json` → `classes.knight.abilities.q`). It is bound to **left-click / Space** as the universal primary attack (`HeroAbilityInput.cs:56-60`). So every click runs this Strike path.

---

## 2. Why the gate is bypassed (root cause, not symptom)

`AimPointOverride` is **never null in the live game.** `HeroTargetIndicator` writes it **every scan**, for the auto‑nearest target — not only on a manual Tab‑lock:

**File:** `Assets/_Modules/Village/Hero/HeroTargetIndicator.cs`, `LateUpdate()`:

```
155   CurrentTarget = _locked ?? NearestCandidate();
...
164   _abilities.AimPointOverride = CurrentTarget != null ? (Vector3?)CurrentTarget.WorldPosition : null;
167   _abilities.LockedTarget = CurrentTarget;
```

`_acquireRange = 45f` (HeroTargetIndicator.cs:46). So `AimPointOverride` is continuously set to the world position of whatever hostile the reticle is on — up to 45 m away.

Trace it through `ResolveEffect` Strike:

1. `atk = AimPointOverride` = the far enemy's own world position (≈ 45 m from the hero).
2. `LockedTarget` = that same far enemy.
3. `InReach(LockedTarget, atk, 4.25)` measures `(LockedTarget.WorldPosition − atk)` = **the enemy's distance from itself ≈ 0** → always `≤ 4.25`. → **always true.**
4. `foe = LockedTarget` → the knight Shield-Bashes (and now, after the projectile rework, lands instant damage — `LaunchProjectile` line 521 `knight → onArrive.Invoke()`) on an enemy 45 m away.

The gate is real but **self‑referential**: it asks "is the target near itself?" instead of "is the target near the hero?" The `3.4 m` melee value is correct; the origin it's measured from is wrong.

**Why WO-398 missed it:** its own comment (lines 419–426) assumed `atk` was the hero's position and that `def.Range` already covered "the locked target's distance." But `atk` was redefined to the aim point on line 386 for the DTT crosshair feature. WO‑398 gated against that aim point, which equals the target, so the comparison degenerates to zero distance. The DTT crosshair (`AimPointOverride`'s original purpose) was removed 2026‑06‑09, but `HeroTargetIndicator` kept feeding the field — so in the village it is **always** the reticle target, exactly the case WO‑398 thought it was guarding against.

### Contrast: the AoE/Cleave/Meteor fix DID work
`ResolveBlastCentre` (lines 613–619) measures `(atk − origin)` against `CastReach()` — i.e. aim‑point vs **real hero origin**. That comparison is correct, so the knight's W (Bulwark Slam) / R (Lantern Charge) cleaves are properly capped. **Only Strike/Snare regressed**, because it fed `atk` in as the origin instead of comparing `atk` to the origin.

### Same defect in FaceCastTarget (cosmetic, same root)
`FaceCastTarget` (lines 328–363) repeats the pattern: line 335 `Vector3 atk = AimPointOverride ?? origin;`, then line 353–354 `InReach(LockedTarget, atk, maxR) ? LockedTarget : NearestHostile(atk, maxR)`. This only turns the hero to face the far foe (no damage), so it's not the snipe — but it should be fixed with the same change for consistency.

---

## 3. Configured vs runtime range

- **Configured knight melee reach:** `def.Range = 3.4` (Shield Bash Q) → gate budget `maxR = 3.4 + 0.85 (_enemyHitRadius) = 4.25 m`. Correct value.
- **Runtime reach actually enforced:** effectively **unbounded** (≈ the 45 m reticle acquire range), because the distance is measured target‑to‑self.
- The knight does **not** fall through to a ranged catalog value, and `CastReach()`/`MeleeDefaultReach` (3.4) are correct. The numbers are fine — the **origin argument** is the defect.
- `PlayerAttackController` (the 360° melee swing also on left‑click) is correctly gated — `Physics.OverlapSphere(transform.position, EffectiveRange()≈3.2)`. It is **not** the snipe. The snipe is purely the `HeroAbilities` Strike cast that fires on the same click.

---

## 4. Runtime log evidence

`Player.log` (197,961 lines) has **no** combat/reach FlowTrace lines — the HeroAbilities cast path is not instrumented (only `[Flow:UI]`, `[Flow:Gear]`, `[BREAK]` appear). The latest session shows a 75 s soft‑lock in `MainCastle_Hall`, i.e. no village combat was exercised in that capture. **Runtime distance is therefore not log‑proven; the RCA rests on the code path above.** A headless combat capture with a `FlowTrace.Step` at line 428 (logging `origin`, `atk`, `LockedTarget.WorldPosition`, and the measured distance) would confirm in one run — recommended as a regression guard.

---

## 5. The fix

The fix belongs in the **ability path** (target acquisition in `HeroTargetIndicator` is working as intended — it should feed the reticle target; the ability is responsible for its own reach). Measure reach from the **hero `origin`**, never from `atk`.

### Fix A (correct + minimal) — RECOMMENDED
**File:** `Assets/_Modules/Village/Hero/HeroAbilities.cs`, lines 428–430. Change the origin argument of both `InReach` and `NearestHostile` from `atk` to `origin`:

```csharp
float maxR = def.Range + _enemyHitRadius;
var foe = InReach(LockedTarget, origin, maxR)   // was: atk
    ? LockedTarget
    : NearestHostile(origin, maxR);             // was: atk
```

Rationale: `origin` (line 263 = `transform.position`) is the hero. This makes the 3.4 m gate measure hero→target, which is what WO‑398 intended. `NearestHostile(origin, maxR)` also becomes correct (it was sweeping a 4.25 m sphere centred on the far enemy, which is why a melee whiff returned nothing useful before falling to `LiveBoss`).

Apply the identical `atk → origin` change in `FaceCastTarget` lines 353–354 (the Strike/Snare `default:` branch) for consistency.

**Do NOT** change line 386 / the `atk` used by AoE/Cleave/Meteor or by `ResolveBlastCentre` — those compare `atk` to `origin` correctly and the cleave caps already work. The DTT `AimPointOverride` semantics for blast centres stay intact.

### Fix B (broader, optional) — only if ranged classes must also honor aim
If the intent is that ranged classes (mage/ranger) still hit the 45 m reticle target while melee classes are capped, that already holds with Fix A: for mage/ranger `def.Range` is large (their abilities.json range covers the target), so `InReach(LockedTarget, origin, large)` is true; for the knight `def.Range = 3.4` correctly fails at distance. **No per‑class branch is needed** — Fix A is sufficient for both. Listed only to note it was considered and rejected as redundant.

### Ranking
1. **Fix A** — one‑line origin correction on lines 428–430 (+ mirror in FaceCastTarget 353–354). Minimal, surgical, matches WO‑398's stated intent, no behavioural change for ranged classes or for the working AoE caps.
2. Fix B — unnecessary; Fix A already differentiates melee vs ranged via `def.Range`.

### Regression guard (recommended alongside the fix)
Add `FlowTrace.Step("Combat", ...)` at the Strike resolution logging `origin`, `atk`, target distance, `maxR`, and whether `InReach` passed — then a headless combat capture proves the knight no longer connects beyond ~4.25 m. This is the missing instrumentation that let WO‑398 ship a self‑referential gate undetected.

---

## Summary

| | |
|---|---|
| **Ungated path** | `HeroAbilities.cs:428` — `InReach(LockedTarget, atk, maxR)` |
| **Root cause** | `atk` (line 386) = `AimPointOverride` = the reticle target's own position (set every scan by `HeroTargetIndicator.cs:164`, range 45 m). `InReach` measures target‑to‑`atk` = target‑to‑itself ≈ 0, so the 3.4 m gate always passes. |
| **Configured range** | 3.4 m (Shield Bash, correct) |
| **Enforced range** | ~45 m (the snipe) |
| **Fix** | `HeroAbilities.cs:428-430` change `atk` → `origin` in the `InReach`/`NearestHostile` calls; mirror in `FaceCastTarget` 353-354. |
| **Layer** | Ability path (not target acquisition — the reticle is meant to feed the target; the ability owns its reach gate). |
