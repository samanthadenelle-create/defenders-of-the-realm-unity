> ## RECONCILED 2026-08-08 - true status is DONE
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: shipped in `7225d897`; `TowerProjectileMapRegression.cs` gained +54 lines locking the element == visual mapping.
> The previous Status line read "READY TO IMPLEMENT" and was wrong.

# WORK ORDER 913 — Arcane Spire: element == visual regression (close the Flame-over-Aether gap)

**Status:** FIXED — shipped (`7225d897`; reconciled 2026-08-08, see banner; not felt-verified)  
**Minted:** 2026-08-07 (CLI / Grok — residual of the work-order audit five-findings fix)  
**Silo:** Combat / VFX / Regression (code only; no scene files)  
**Roles:** CLI implement + gate  
**Depends on:** commit `f329c8d5` (BoltVisualElement flipped Flame → Aether; travel + impact already Arcane)  
**Related:** WO-870 / WO-872 (Aether wins; no “deals Aether, looks Fire”); WO-907 (elemental affinity system — do NOT expand scope into enemy elements here)

---

## 0. One-line truth

The Arcane Spire **stopped looking like fire in source** (`ArcaneTower.BoltVisualElement = Aether`), but the regression that allowed the bug to ship for days still only checks **Hovl string keys** — it would still go GREEN if someone re-set `BoltVisualElement` to `Flame` tomorrow. The fix is incomplete without a **code gate** that asserts gameplay element and visual element stay the same, and that Aether never maps to Fire art.

---

## 1. Proven history (do not re-diagnose)

| Fact | Evidence |
|------|----------|
| Gameplay `Element` is `DamageElement.Aether` | `ArcaneTower.cs` field + catalog `tower_arcane_spire` |
| Was shipping Fire visuals | `BoltVisualElement = Flame` (fixed in `f329c8d5`) |
| Gate was green over the defect | `TowerProjectileMapRegression` only asserts absence of `*_Projectile` string literals in `ArcaneTower.cs` and catalogued keys in `DefenseTower.cs` — **never** reads `BoltVisualElement` / `Element` |
| Travel + impact Arcane art exist | `ProjectileVFXCatalog`: Aether → `Projectile_Arcane`, `Explosion_Arcane` under `Resources/VFX/Projectiles/` |
| Cast wind-up + extra swirl are EMPTY on purpose | No `Casting_Arcane` / `Spell_Arcane` on disk; fire hooks were cleared rather than re-used (owner creative pick = separate WO if tagged later) |

Owner ruling (verbatim class, WO-870/872): **do NOT ship “deals Aether, looks Fire.”**

---

## 2. Scope

### Phase A — Extend `TowerProjectileMapRegression` (required)

File: `Assets/Editor/Regression/TowerProjectileMapRegression.cs`  
Suite already registered in `DataRegression.RunAll` (`[tower-proj-map]`).

Add cases (source-lint style, same as existing — no PlayMode required):

1. **`Element` default is Aether**  
   - Parse / assert `ArcaneTower.cs` declares `public DamageElement Element = DamageElement.Aether` (or equivalent assignment at field init).  
   - Fail if it becomes Flame / Ice / None without a deliberate case rewrite.

2. **`BoltVisualElement` default equals `Element` (Aether)**  
   - Assert field init is `DamageElement.Aether`.  
   - Fail if `BoltVisualElement = DamageElement.Flame` (or any value ≠ Aether while Element is Aether).

3. **No Fire art strings on Arcane hooks**  
   - `BoltCastVfx` / `BoltImpactExtraVfx` must **not** contain `Fire` / `Casting_Fire` / `Spell_Fire` when Element is Aether.  
   - Empty string is **allowed** (current honest state until owner tags Aether cast art).  
   - Do **not** invent `Casting_Arcane` / `Spell_Arcane` keys in code if the prefabs do not exist (VFX law: owner tags, CLI maps verbatim).

4. **Catalog path still holds**  
   - Keep existing (b) “no `*_Projectile` literal in ArcaneTower.cs” **or** rewrite it deliberately if owner tags an Aether projectile key — never silently substitute.

5. **Log line** for the new case, e.g.  
   `(g) ArcaneTower Element==BoltVisualElement==Aether; cast/extra fire hooks forbidden`

Marker still: `TOWER_PROJECTILE_MAP_OK` / `TOWER_PROJECTILE_MAP_FAIL`.

### Phase B — Optional hardening (same PR if cheap)

- One-line comment at the top of `TowerProjectileMapRegression` header: the historical Flame-over-Aether defect and why (g) exists.  
- If `ProjectileVFXCatalog` has a public map Aether→keys, assert those two keys resolve under `Resources/VFX/Projectiles/` (file exists). Do not invent missing cast art.

### Phase C — Explicitly out of scope

- **Do not** pick / author `Casting_Arcane` or `Spell_Arcane` (owner creative; see WO-919 if minted for VFX tags).  
- **Do not** implement WO-907 enemy affinities.  
- **Do not** change DefenseTower fireball / mage cast lanes (WO-875 territory).  
- **Do not** hand-edit scenes.

---

## 3. Files

| File | Action |
|------|--------|
| `Assets/Editor/Regression/TowerProjectileMapRegression.cs` | Add Element==visual cases |
| `Assets/_Modules/Village/Buildings/ArcaneTower.cs` | Read-only unless a comment needs tightening; do not re-break Aether |
| `Assets/Editor/Regression/DataRegression.cs` | Only if registration/marker text must change (prefer no change) |

---

## 4. Acceptance

- [ ] `TowerProjectileMapRegression` fails if `BoltVisualElement` is re-set to `Flame` while `Element` is Aether (prove by temporary local flip, then revert — or equivalent static assertion).  
- [ ] `TowerProjectileMapRegression` fails if `BoltCastVfx` / `BoltImpactExtraVfx` re-introduce Fire keys.  
- [ ] Empty cast/extra strings still pass (honest “no art yet”).  
- [ ] Existing cases (a)–(f) still green.  
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` with `[tower-proj-map]` in the log.  
- [ ] RESULT file lists the new assertions and the deliberate empty-cast decision.

---

## 5. RESULT

`WorkOrders/WORK_ORDER_913_arcane_element_equals_visual_regression.RESULT.md`
