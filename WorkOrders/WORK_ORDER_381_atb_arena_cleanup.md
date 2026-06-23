# WO-381: ATB Arena Cleanup — Remove Structures, Fix Coloring, Enemy Facing

**Status:** READY TO IMPLEMENT  
**Estimated Effort:** P1 (0.5–1 day — scene setup + shader + AI)  
**Priority:** HIGH (combat immersion breaking)  
**Lane:** 2 Combat/AI

---

## Issue 1: Structures in Battle Arena

**Problem:** Village structures (stairs, platforms) visible during combat.

**Should be:** Clean arena with no environment objects.

**Fix:**
- Remove all buildings/props from battle scene
- Use dedicated battle arena (empty or minimal)
- Keep hero + enemies only
- No village structures in view

**Files to check:**
- `Assets/Scenes/BattleArena.unity` or equivalent
- Remove child objects that shouldn't be there

---

## Issue 2: Character Coloring Wrong

**Problem:** Hero appears white/pale instead of normal character colors.

**Likely cause:**
- Shader issue (wrong material assigned)
- Lighting too bright (blown out colors)
- Material colors set to white
- Texture missing/fallback

**Fix options:**

### A. Check Material
```
Hero mesh → Material
→ Albedo color should be skin tone (not white)
→ Check diffuse texture is assigned
```

### B. Check Lighting
```
Battle scene lighting too bright?
→ Reduce light intensity
→ Add rim lighting to show model
```

### C. Check Shader
```
Is hero using correct shader?
Should match village scene hero shader
Not a pure white/fallback material
```

**Test:**
- Hero should have visible skin tones, clothing colors
- Not blown out white
- Matches village scene appearance

---

## Issue 3: Enemy Facing Wrong Direction

**Problem:** Enemy (skeleton) not facing hero. Should face each other.

**Fix:**
- Set enemy rotation toward hero at combat start
- All enemies face center of arena (hero location)
- Use LookAt or rotate toward hero position

**Code fix:**

```csharp
public class EnemyBrain : MonoBehaviour
{
    void Start()
    {
        // Face the hero
        var hero = FindObjectOfType<HeroController>();
        if (hero != null)
        {
            // Look at hero position
            Vector3 dirToHero = (hero.transform.position - transform.position).normalized;
            transform.rotation = Quaternion.LookRotation(dirToHero);
        }
    }
}
```

**Or in ATB setup:**

```csharp
public class BattleController : MonoBehaviour
{
    void SetupBattle()
    {
        // Orient all enemies toward hero
        var hero = FindObjectOfType<HeroController>();
        var enemies = FindObjectsOfType<Enemy>();
        
        foreach (var enemy in enemies)
        {
            Vector3 dirToHero = (hero.transform.position - enemy.transform.position).normalized;
            enemy.transform.rotation = Quaternion.LookRotation(dirToHero);
        }
    }
}
```

---

## Testing Checklist

- [ ] Battle arena has no village structures visible
- [ ] Hero colors appear correct (not white/pale)
- [ ] Hero shader matches village appearance
- [ ] Enemies face toward hero
- [ ] Skeleton looks directly at hero (not sideways)
- [ ] Combat feels immersive (no visual disconnect)
- [ ] Works in WebGL build

---

## Files to Modify

### Scene
- `Assets/Scenes/BattleArena.unity` — Remove structures, verify lighting

### Code
- `Assets/_Modules/BattleATB/Enemy/EnemyBrain.cs` — Add facing logic
- `Assets/_Modules/BattleATB/BattleController.cs` — Setup enemy facing on battle start

### Materials
- Check hero material in battle (should match village scene)

---

## Acceptance Criteria

- [ ] No structures visible in battle arena
- [ ] Hero colors correct (skin, clothes visible)
- [ ] Enemies face hero at combat start
- [ ] Combat feels focused and immersive
- [ ] Skeleton orientation correct (facing player)

---

## Related

- WO-359: Combat Feedback (screen shake, VFX)
- BattleATB assembly integration
