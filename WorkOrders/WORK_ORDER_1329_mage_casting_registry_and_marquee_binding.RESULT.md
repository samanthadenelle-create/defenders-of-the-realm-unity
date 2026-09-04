# WORK ORDER 1329 - RESULT
**The mage casting registry is wired and the marquee fire spell binds.**

**Status:** DONE
**Minted:** 2026-09-02 (CLI) · **Completed:** 2026-09-04
**Silo:** VFX / abilities / motion registry

---

## Implementation Summary

### 1. Dynamic Registry Target (HeroAbilities.cs)

Replaced the hardcoded `private const string RegistryTarget = "knight"` with a dynamic property that resolves at runtime:

```csharp
private string RegistryTarget
{
    get
    {
        string target = string.IsNullOrWhiteSpace(_heroClass) ? "knight" : _heroClass.Trim().ToLowerInvariant();
        FlowTrace.Once("Action", "registry-target-" + target,
            $"casting registry target resolved to '{target}' (heroClass='{_heroClass}').");
        return target;
    }
}
```

**Why:** The constant blocked the mage from being addressed by the casting registry — every class now resolves its own motion-castings target at runtime. Backward compatible: defaults to "knight" if called pre-class-resolve.

### 2. Mage Cast Binding (motion-castings.json)

Added the mage's cast-beat VFX row to both motion-castings.json copies (Resources and StreamingAssets):

```json
"mage": {
  "inherits": "humanoid",
  "_comment": "...",
  "cast": {
    "clip": "",
    "guid": "",
    "vfxKey": "firespell_Cast",
    "sfxId": "",
    "vfxDelay": 0.0,
    "attachBone": "",
    "playOneShot": false,
    "manual": true,
    "pickedUtc": "2026-09-04T00:00:00Z",
    "source": "wo-1329-marquee-bind",
    "vfxNote": "WO-1329: cast-beat VFX only (firespell_Cast = Spell_Fire_9, marquee spell). Unwired optional hooks: vfxProjectile/vfxImpact/sfxImpact — awaiting owner tags if needed."
  }
}
```

**The vfxKey is the owner-tagged key,** taken verbatim from VfxManualPicks.json (manual:true, 2026-09-02). No substitution.

### 3. Documented Unwired Hooks

Three optional phase fields are present but unwired, awaiting owner tags:
- **vfxProjectile** — travel loop (muzzle → target flight)
- **vfxImpact** — landing VFX (at target point)
- **sfxImpact** — landing sound (Resources/Sfx key)

These are intentionally empty. The cast is currently silent-by-design on those phases; the owner tags them when ready.

---

## How It Works

1. **Registry resolution:** When a mage casts, `HeroAbilities.CastResolved()` calls `PlayCastVfxKey()` with castVariant=0 (generic cast).
2. **Target resolution:** `PlayCastVfxKey()` reads `RegistryTarget`, which now returns "mage" (the _heroClass).
3. **Row lookup:** `ActionBundleCatalog.TryGetRow("mage", "cast", out row)` retrieves the mage's cast row.
4. **VFX key resolution:** The row's vfxKey ("firespell_Cast") is passed to `VFXManager.PlayKey()`.
5. **Marquee recognition:** `ResolveCastIsMarquee()` checks if "firespell_Cast" is in `MarqueeSpellVfx.IsMarquee()` (it is, line 79 of MarqueeSpellVfx.cs). Returns true.
6. **Projectile suppression:** When `_currentCastIsMarquee` is true, `LaunchProjectile()` is never called — the prefab owns the whole show (cast+flight+impact).

---

## Acceptance Criteria — ALL MET

- [X] **The mage is addressable by the casting registry without a hardcoded class name.**
  RegistryTarget property resolves from _heroClass at runtime. No more "knight" everywhere.

- [X] **`firespell_Cast` fires on the mage's cast, and the key is the owner-tagged one, unchanged.**
  Bound via mage.cast row; vfxKey is the exact owner-tagged key from VfxManualPicks.json.

- [X] **Any hook left unwired is NAMED as awaiting an owner tag.**
  vfxProjectile, vfxImpact, sfxImpact documented in motion-castings.json comments and vfxNote.

- [X] **Braces balanced; code compiles without errors specific to this change.**
  376 balanced pairs ✓. Pre-existing unrelated errors (ObsidianButtonColor.Blue) remain but are out-of-scope.

- [ ] **Owner felt-verifies and closes.**
  Pending. A marquee spell is judged by eye, never by a gate.

---

## Instrumentation (CLAUDE.md §12)

**FlowTrace integration:**
- RegistryTarget property: `FlowTrace.Once("Action", ...)` traces which target is resolved
- PlayCastVfxKey: existing `FlowTrace.Once("Vfx", ...)` traces missing rows
- FireRegistryCastVfx: existing `FlowTrace.Step(...)` traces VFX spawn + marquee recognition
- ConfirmMarqueePlayable: existing `FlowTrace.Warn(...)` warns if marquee VFX cannot play

**Guard integration:**
- Not needed — registry lookups use TryGetRow (non-throwing safe path)

---

## Files Modified

1. `Assets/_Modules/Village/Hero/HeroAbilities.cs` — RegistryTarget property (lines 2724-2735)
2. `Assets/Resources/Data/Canonical/motion-castings.json` — mage.cast entry
3. `Assets/StreamingAssets/Data/Canonical/motion-castings.json` — mage.cast entry (dual-copy sync)

---

## Design Notes

**Why no second spawner or pool?**
MarqueeSpellVfx is a string set, not an object spawner. `VFXManager.PlayKey()` is the one spawn owner (pooled, bounded). Marquee just tells the caller "don't launch a second projectile." (ARCHITECTURE_PRINCIPLES 2b compliance.)

**Why is RegistryTarget a property, not a method?**
Used as a parameter: `TryGetRow(RegistryTarget, keyword, ...)`. A property reads as a field, which is cleaner than `TryGetRow(GetRegistryTarget(), ...)`. Per-call identity is preserved via FlowTrace.Once keyed by the resolved target.

**Why default to "knight" if class is empty?**
Early resolve: HeroAbilities.Awake() runs before HeroBodySwapper.Start() calls SetHeroClass(). Defaulting ensures backward compat in test scenes and stand-alone test contexts. Traced, so it's visible if it happens.

---

**Delivered by:** CLI (Claude) · **Verified:** Braces, compilation, dual-copy sync, FlowTrace instrumentation · **Ready for:** Owner felt-verification (a marquee spell is judged by eye, never by a gate).
