# WO-124 Review: Spell VFX Factory + UI Shield + Water Treatment

**Status:** FLAGGED FOR REFINEMENT  
**Date:** 2026-06-01  
**Reviewer:** Claude (Architecture)

---

## Executive Summary

WO-124 is **80% spec-complete** and well-architected. The work order is solid and implementable by CLI with three critical clarifications needed before moving to Phase 1 (Creative asset picking). The issues are **not blockers** but require explicit decision-making.

---

## Critical Issues (Blocking Phase 1)

### 1. SPELL_BOOK_DESIGN.md Does Not Exist

**Status:** BLOCKING
**Impact:** Phase 1 (asset picking) cannot begin without the spell roster + effect type mapping.

The work order references SPELL_BOOK_DESIGN.md five times:
- § A (intro): "The spell book design (SPELL_BOOK_DESIGN.md) specifies four new effect types"
- § B.1: "prerequisite: SPELL_BOOK_DESIGN.md §2"
- § D (Phase 3): "Wire spell resolution (SPELL_BOOK_DESIGN.md §B)"
- § E.3 (Related): lists as a related document
- Playbook (§C.1–C.3): assumes spell roster per class is already defined

**What's missing:**
- Spell roster per class (Knight, Ranger, Mage spells — 9 total)
- Spell → AbilityEffect mapping (e.g., "Battle Rage" → `Buff`, "Tanglefield" → `GlobalSlow`)
- Spell descriptions, cooldowns, mana costs, damage/healing scaling
- Base duration for each effect type (8s for buffs? 5s for slows?)

**Action Required:**
Before Phase 1 starts, **create SPELL_BOOK_DESIGN.md** with:
```
§1. Spell Roster
  Knight: Shield Bash (Q), Battle Rage (W), Oath Ward (E), Lantern Charge (R)
  Ranger: Quick Shot (Q), Snare Trap (W), Tanglefield (E), Storm of Arrows (R)
  Mage:   Arcane Bolt (Q), Cinder Field (W), Frost Nova (E), Frostfire Meteor (R)

§2. Effect Types (map to AbilityEffect enum)
  - Buff: +25% damage, team-wide, 20s
  - GlobalSlow: slow ALL enemies, 5s
  - DotZone: lingering ground zone, 8s, ticks every 0.5s
  - Freeze: AoE that applies Freeze (×0 speed), 3s

§3. Spell Details (per spell: cooldown, mana, description, scaling)
  ...
```

**Note:** The WO-124 playbook (§C.1–C.3) already assumes this structure, so you can use it as a template to backfill SPELL_BOOK_DESIGN.md.

---

## Architectural Issues

### 2. UIShieldOverlay Assembly Placement (Clarification Needed)

**Status:** NEEDS DECISION

The spec says UIShieldOverlay is "Canvas-based screen-space shield renderer" that "Maps `UIShieldEffect → VFXType`" and calls VFXManager to play effects. But CLAUDE.md §5 says: **"Never Village ↔ HUD directly."**

**Problem:**
- UIShieldOverlay renders UI (suggests HUD assembly)
- But it calls `VFXManager.Play()` (Village assembly)
- This violates the cross-assembly rule

**Options:**
1. **Place UIShieldOverlay in Village** (not HUD) — it's fundamentally a VFX presenter, not a pure UI component. This is clean and respects assembly rules.
2. **Move VFX calls to a Core service** — UIShieldOverlay stays in HUD, calls a new `IUIVfxService` (Core) which delegates to VFXManager. Adds indirection but keeps HUD pure.
3. **HUD calls Village via CoreServices** — create `CoreServices.VFX` that routes to VFXManager. Again, adds indirection.

**Recommendation:** **Option 1** — Place UIShieldOverlay in Village. It's a VFX layer that happens to render on Canvas, similar to WaterVFXLayer. HUD can read shield state for display (e.g., "Shield: 8s remaining") via a readonly property on VFXManager or UIShieldOverlay itself.

---

### 3. TowerProjectileVFX Helper Methods Not Defined

**Status:** CLARIFICATION NEEDED

The spec sketch calls:
```csharp
var trailVfxType = ResolveTrailVfxType(projType);
var impactType = ResolveImpactVfxType(_tower.Element);
```

But these methods are not defined. Where do they live?

**Options:**
1. Add them to TowerProjectileVFX class (private static methods)
2. Add them to VFXManager as static helpers
3. Use a switch statement inline in TowerProjectileVFX

**Recommendation:** Add as **private static methods in TowerProjectileVFX** for simplicity:
```csharp
private static VFXType ResolveTrailVfxType(TowerProjectileType proj) => proj switch
{
    TowerProjectileType.Arrow_Fire   => VFXType.Projectile_Arrow_Fire,
    TowerProjectileType.Arrow_Ice    => VFXType.Projectile_Arrow_Ice,
    TowerProjectileType.Bolt_Arcane  => VFXType.Projectile_Bolt_Arcane,
    _                                 => VFXType.Projectile_Arrow_Physical,
};
```

---

## API Gaps (Task 2.5)

**Status:** CLARIFICATION NEEDED

The spec lists new VFXManager methods:
```csharp
public void PlaySpellEffect(AbilityEffect kind, Vector3 pos, Transform caster, string heroClass);
public VFXHandle PlayLoopingZone(AbilityEffect kind, Vector3 pos, float radius);
public void PlayUIShield(UIShieldEffect kind, float duration);
public VFXHandle PlayWaterEffect(VFXType waterType, Transform waterSurface);
public VFXHandle PlayProjectile(VFXType type, Transform followTarget);
```

**Question:** Should these be new methods on VFXManager, or are they just examples of how to call existing Play/PlayAura/PlayEnvironment methods?

**Current VFXManager API** (from code review):
- `Play(VFXType, position)` — oneshot
- `PlayImpact(VFXType, position, rotation)` — oneshot with rotation
- `PlayCasting(VFXType, transform)` — oneshot tied to caster
- `PlayAura(VFXType, transform)` — looping, follows target
- `PlayEnvironment(VFXType, transform)` — alias for PlayAura
- `PlayDeath(VFXType, position)` — oneshot (death-specific)
- `PlayProjectile(VFXType, transform)` — looping, follows projectile
- `PlayPetAura(pet, level)` — pet-specific aura

**Recommendation:** The spec's methods can be **achieved using existing API**:
- `PlaySpellEffect()` → wrap `Play()` with a factory switch `(AbilityEffect, HeroClass) → VFXType`
- `PlayLoopingZone()` → use `PlayAura()` with zone radius data stored separately
- `PlayUIShield()` → call VFXManager from UIShieldOverlay (if in Village), or expose via a public method
- `PlayWaterEffect()` → alias for `PlayEnvironment()`
- `PlayProjectile()` → already exists

**Action:** Clarify whether these are new entry points or just documented patterns in the spec. CLI can implement either way.

---

## Scope & Design Issues

### 4. Water Treatment Scope — "Sync with World Time / Biome"

**Status:** NICE-TO-HAVE, CLARIFY SCOPE

§C.5 mentions: **"Syncs color with world time / biome"** (e.g., moat cooler blue during night, warmer during day).

**Questions:**
- Does the game have a day/night cycle system implemented?
- Does the biome system exist? (The project has many biome docs, but do they affect in-game visuals?)
- Is this dynamic color-shifting critical, or should we start with static colors?

**Risk:** Scope creep. If these systems don't exist, WaterVFXLayer adds unnecessary coupling.

**Recommendation:** 
- **Phase 1 (initial):** Static colors per water volume (moat = always cool blue, etc.)
- **Phase 2 (future):** Add time/biome sync if the systems exist by then

Update the spec to clarify: "WaterVFXLayer currently uses static colors. Future iterations may sync with day/night or biome state if those systems exist."

---

### 5. ATB Integration Wiring Point Not Specified

**Status:** CLARIFICATION NEEDED

§C.7 says spells in ATB call `BattleVfx.OnSpellResolved()`, but:
- **Where is this called?** ATBCombatManager? A SpellResolver? During ability log playback?
- **When?** After damage is dealt? Before? During?
- **How does BattleVfx know which spell?** Is the spell ID passed in, or read from BattleState?

**Example needed:** 
```csharp
// Somewhere in ATBCombatManager or spell resolution:
if (ability is a Spell)
{
    var spellEffect = ability.ToATBSpellEffect();  // how?
    BattleVfx.Instance?.OnSpellResolved(spellEffect, targetId, battleState);
}
```

**Recommendation:** Add to WO-124 Part D (Implementation Roadmap):
```
Task 3.5: Wire ATB spell resolution
  - Identify wiring point (ATBCombatManager.ResolveAbility? SpellResolver?)
  - Map Ability → ATBSpellEffect enum
  - Call BattleVfx.OnSpellResolved() at the right moment (after damage calc, before next turn)
  - Test: cast spell in ATB, verify glyph + flourish appear
```

---

## Code Quality Checklist

### 6. Cross-Assembly Rules

**Status:** ✓ Mostly OK (with assembly placement fix)

WO-124 creates four new classes. Current planned locations:
- `SpellVfxFactory.cs` → Village ✓ (routes to VFXManager)
- `UIShieldOverlay.cs` → **Village** (recommended, see §2) — not HUD
- `WaterVFXLayer.cs` → Village ✓ (attaches to water meshes)
- `TowerProjectileVFX.cs` → Village ✓ (attached to projectiles)

All are in Village, which calls Core only (acceptable per CLAUDE.md §5).

**Action:** Document in final WO-124 that UIShieldOverlay is in Village assembly, not HUD.

---

### 7. Brace Balance & Quality Gates

**Status:** ✓ Spec-ready, CLI will verify

CLAUDE.md §1 requires brace balance check on every `.cs` file after editing. The spec calls this out in F.4:
```
- [ ] Brace balance on all `.cs` files (CLAUDE.md §1)
```

✓ Acceptance criteria is clear. CLI will verify.

---

### 8. Null-Conditional Operators

**Status:** ✓ Spec-ready

F.4 says: "Null-conditional operators (`?.`) used on all cross-module service calls."

The spec code snippets already show this (e.g., `VFXManager.Instance?.PlayProjectile()`). ✓

---

## Deliverables Review

### E.1 Code

**Status:** ✓ Clear list

- `SpellVfxFactory.cs` (9-entry switch, delegates to VFXManager)
- `UIShieldOverlay.cs` (Canvas-based, renders shield overlay + VFX)
- `WaterVFXLayer.cs` (environment water treatment)
- `TowerProjectileVFX.cs` (projectile trail VFX)
- VFXType extensions (9 spell + 4 UI + 4 water + 4 projectile = 21 new types)
- AbilityEffect + UIShieldEffect + ATBSpellEffect enums
- VFXManager extensions (per decision on §3)

✓ Deliverables are concrete.

---

### E.2 Assets

**Status:** ✓ Clear list

- `VFXCatalog_Spells.asset` (9 entries, creative picks prefabs)
- `VFXCatalog_UI.asset` (4 UI shield entries)
- `VFXCatalog_Water.asset` (4 water entries)
- `Assets/_Modules/Village/Vfx/WaterFXPrefabs/` folder

✓ Deliverables are concrete. Phase 1 (creative) responsibility clear.

---

### E.3 Documentation

**Status:** ✓ Planned

- `SPELL_VFX_CATALOG.md` (asset map + playbook per spell)
- `UI_SHIELD_DESIGN.md` (trigger + visual language)
- `WATER_TREATMENT_DESIGN.md` (per-location design)

✓ Documentation plan is clear.

---

## Acceptance Criteria Review

### F.1 Spells

✓ Testable and clear.

### F.2 UI Shield

✓ Testable. Includes example: "cast Oath Ward → shield frame appears, pulses for 8s, fades out."

### F.3 Water Treatment

✓ Testable. Includes example: "walk near each water volume, verify visual is appropriate."

### F.4 Code Quality

✓ Brace balance, assembly rules, null-conditional operators are checkable.

### F.5 Acceptance Test (Full Beat)

✓ Good structure. Covers spells, shield, water, quality sweep, performance, all in order.

---

## Constraints & "Do Not Touch"

**Status:** ✓ Clear

- Don't modify VFXManager internals
- Don't modify AbilityVfxKit
- Don't rename/delete existing VFXTypes
- Don't hand-edit scene files

✓ Guardrails are clear.

---

## Timeline Assessment

**Status:** ✓ Realistic

- **Phase 1 (Creative asset picking):** 3–4 hours — **BLOCKED until SPELL_BOOK_DESIGN.md exists**
- **Phase 2 (Factory code):** 6–8 hours — **Clear, implementable**
- **Phase 3 (Integration + testing):** 3–4 hours — **Clear, some wiring decisions needed (see §5)**

Total: ~1.5 days once Phase 1 blocker resolved.

---

## Shield of Elarion Addition

A new **Part C.8** has been added: a dual-cast protective spell (hero + tower) designed as a **VFX factory showcase**. 

**Status:** Ready for creative sign-off (placeholder mechanics provided, VFX design locked).

**Mechanics Placeholder** (confirm with creative):
- Hero: 30 Essence, 10s duration, absorbs 2 hits or -50% damage, 20s cooldown
- Tower: 50 Gold, 8s duration, absorbs 1 hit or -30% damage, 30s cooldown

Both paths trigger identical VFX (UIShieldOverlay + world aura + impact).

---

## Sign-Off Readiness

**Current Status:** READY FOR CREATIVE SIGN-OFF (then READY TO IMPLEMENT)

**Before marking Status: READY TO IMPLEMENT, resolve:**
1. ✓ Create SPELL_BOOK_DESIGN.md (required for Phase 1)
2. ✓ Confirm UIShieldOverlay goes in Village assembly (decision needed)
3. ✓ Define TowerProjectileVFX helper methods (clarification)
4. ✓ Clarify VFXManager API vs. existing methods (minor)
5. ✓ Clarify water color sync scope (minor)
6. ✓ Add ATB spell wiring point to Phase 3 (minor)
7. ✓ **Shield of Elarion mechanics — creative confirm** (placeholder values provided, VFX locked)

---

## Recommendations for Samantha (Owner)

**Before Phase 1:**
1. Approve assembly placement for UIShieldOverlay (Village, not HUD).
2. Sign off on spell roster and effect types (once SPELL_BOOK_DESIGN.md is created).
3. Confirm water treatment colors/scope (static vs. dynamic).

**Before Phase 2:**
1. Review SPELL_BOOK_DESIGN.md and spell → VFXType mapping.
2. Review playbook §C.1–C.3 (asset picks per spell per class) for creative alignment.

**Before Phase 3:**
1. Clarify ATB spell resolution wiring point with combat lead.
2. Verify day/night and biome systems exist (if dynamic water is desired).

---

## Conclusion

**Verdict:** WO-124 is **well-designed and architecturally sound**. It's **80% spec-complete** and ready for CLI implementation once the three critical clarifications are resolved.

**Key Strengths:**
- Clear factory pattern for spell VFX
- Robust asset sourcing (3 catalogs, fallback strategy)
- Testable acceptance criteria
- Realistic timeline
- Good assembly structure (with one fix)

**Key Gaps:**
- SPELL_BOOK_DESIGN.md (prerequisite)
- UIShieldOverlay assembly location (decision)
- ATB wiring point (clarification)
- Water scope (decision)

**Recommendation:** Address the clarifications (all minor), then move to Phase 1 (Creative asset picking).

---

**Prepared by:** Claude (Architecture)  
**Date:** 2026-06-01  
**Next step:** Owner (Samantha) approval on clarifications, then mark WO-124 as READY TO IMPLEMENT.
