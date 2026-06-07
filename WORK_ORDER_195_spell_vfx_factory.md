# WORK_ORDER_124: Unified VFX Factory (Three-Library Router) + Spell System + Polish

**Status:** READY TO IMPLEMENT  
**Owner:** Creative + Architecture  
**Priority:** High (foundational VFX infrastructure + spell system)  
**Related:** SPELL_BOOK_DESIGN.md, VFXManager.cs, AbilityVfxKit.cs, EnvironmentVFX.cs, VFXCatalog.cs, VFXType.cs  
**Acceptance:** VFX factory wired (spells, UI shield, water, towers), catalog-driven design locked, future-ready for enemies/bosses/environmental effects.

---

## Executive Summary

**Goal: Build a unified VFX routing system that scales to ANY effect type (spells, towers, enemies, bosses, environmental) without adding code.**

The spell book design (SPELL_BOOK_DESIGN.md) specifies **four new effect types** (buff, globalslow, dotzone, freeze) that unlock the creative roster. VFX coverage is thin — only procedural fallbacks exist today.

We have **massive untapped assets** from three sources:
- **Mirza Beig Ultimate VFX:** 553 prefabs across base pack + 5 expansions (CONSTR. KIT, ACTION, STORM, SHOCKWAVES, TITLES)
- **Lana Studio Casual RPG VFX:** 100+ prefabs (areas, bursts, impacts, backlight resources, rings, slides)
- **AbilityVfxKit:** procedural factory (built, fallback layer, never silent)

**The architecture:**
- **VFXManager** (singleton): pooling, quality gating, lifecycle (unchanged)
- **VFXCatalog** (ScriptableObject): maps VFXType → prefab config (catalog-driven)
- **VFXType enum:** the unified spine (all effect types live here)
- **SpellVfxFactory** (example pattern): routes (AbilityEffect, HeroClass) → VFXType

**Why this is future-proof:** To add a new effect category (enemies, bosses, UI transitions), you just:
1. Add new VFXTypes to the enum
2. Create new catalog entries (prefab + config)
3. Call VFXManager.Play(vfxType) from anywhere
4. Zero changes to factory logic — catalog is the source of truth

This is **catalog-driven, not code-driven**. Scales infinitely without adding complexity.

---

## Part A: VFX Asset Catalog

### A.1 Mirza Beig Ultimate VFX (553 prefabs)

**Location:** `Assets/Mirza Beig/Particle Systems/Ultimate VFX/`

**Core pack (base):**
- Oneshot effects (40+): smoke, impacts, flares, bursts, riffs
- Loop effects (20+): fire, sparks, twisters, portals, nebulae, warp gates
- Common utilities: line renderers, templates

**Expansions (5):**

| Expansion | Prefabs | Key Assets | Use |
|---|---|---|---|
| **XP - CONSTR. KIT** | ~150 | Shockwaves, explosions, smoke wisps, electricity, lightning, leaves, shards, sparks, rings, hitballs | Spell impacts, CC zones, reaction beats |
| **XP - ACTION** | ~40 | Explosions (realistic + stylized), dark smoke, flamethrower, fireworks | Boss attacks, big damage moments, charged spells |
| **XP - STORM** | ~50 | Rain, snow, storm clouds, fog, wind, cloud effects | Environment auras, global DoT zones, ambient threat |
| **XP - SHOCKWAVES** | ~20 | Crystal nova, distorted shockwaves, force blasts | Freezes, slow zones, area CC signals |
| **XP - TITLES** | ~10 | Fire, embers, streaks, core effects (UI-style, can layer) | Buff auras, team effects, status icons |

**Strengths:** breadth, polish, ready-to-use oneshots, lots of loop-friendly shapes.  
**Constraints:** broad (all game genres), not spell-focused; need curation.

---

### A.2 Lana Studio Casual RPG VFX (100+ prefabs)

**Location:** `Assets/Lana Studio/Casual RPG VFX/Prefabs/`

**Categories:**

| Category | Prefabs | Key Assets | Use |
|---|---|---|---|
| **Area effects** (generic) | 12 | Blue, green, orange, red (base + outbreak variants) | DoT zones, slow zones, element-coded areas |
| **Bursts** | 6+ | Sharp, rings, rainbow mist, circles | Impacts, spell triggers, visual beats |
| **Backlight resources** | 12+ | Coins, diamonds, health, hearts (drop variants) | Loot VFX, reward signals, collection beats |
| **Rings** | 8+ | Expanding, pulsing, solid, dashed | Ground marks, targeting indicators, zone boundaries |
| **Slides** & misc | 15+ | Streaks, blasts, particles | Projectiles, travel effects, movement trails |

**Strengths:** thematic cohesion, color-coded zones (perfect for elements), lightweight performance.  
**Constraints:** smaller scale than Mirza; best for ground marks and areas.

---

### A.3 AbilityVfxKit (Procedural, built)

**Location:** `Assets/_Modules/Village/Hero/AbilityVfxKit.cs`

**Capabilities:**
- Strike (tracer + spark)
- Snare (strike + ground ring)
- Aoe (nova + shards + flash)
- Cleave (nova variant, heavier)
- Heal (rising column + pulse ring)
- Meteor (falling streak + ring + embers)
- Class variants: Knight (steel-gold, shockwave + sparks), Ranger (leaf-green, arrow + burst)
- Procedural fallback: **never silent, always reads, zero asset dependencies**

**Fallback colors:**
- Aether (arcane): #E6DFFF
- Flame (fire): #FF4500
- Ice (frost): #80CCFF
- Heal (green): #59FF8D
- Physical (steel): #B0B1A0
- Gold (celebration): #EBC64E

---

## Part B: Spell Effect Architecture

### B.1 New Effect Types (prerequisite: SPELL_BOOK_DESIGN.md §2)

Four new `AbilityEffect` enum values to add:

```csharp
public enum AbilityEffect
{
    // ... existing (Strike, Snare, Aoe, Cleave, Heal, Meteor)
    Buff,        // Rage: +25% damage, 20s, team-wide (hero + pets)
    GlobalSlow,  // Tanglefield: slow ALL enemies, 5s
    DotZone,     // Cinder Field: lingering ground zone, ticks damage over 8s
    Freeze,      // Frostfire: AoE that applies Freeze (×0 speed) for 3s
}
```

### B.2 Spell VFX Factory

New class: `SpellVfxFactory.cs` (paired with spell effect types, **not** procedural).

**Design:**
- Maps `(AbilityEffect + SpellClass) → VFXType` → pooled prefab
- Falls back to `AbilityVfxKit` if no prefab is wired
- Respects `VFXQuality` (skip expensive loops on Low)
- Integrates with `VFXManager` (shared pools, quality gating, audio bridges)

**API:**
```csharp
// Quick entry point (use from spell resolution)
VFXManager.PlaySpellEffect(AbilityEffect.Buff, position, casterTransform, heroClass);
VFXManager.PlaySpellEffect(AbilityEffect.DotZone, zoneCenter, radius: 3f);
VFXManager.PlaySpellEffect(AbilityEffect.Freeze, position, duration: 3f);

// Advanced (explicit handle for looping zones)
var zoneHandle = VFXManager.PlayLoopingZone(AbilityEffect.DotZone, position, radius);
zoneHandle.Stop(gracefulFadeTime: 0.5f);
```

---

## Part C: Creative Playbook (per spell class)

### C.1 Knight (bruiser-support)

| Spell | Effect Type | Asset Strategy | VFX Signature |
|---|---|---|---|
| Shield Bash (Q) | Strike | Mirza CONSTR shockwave + Knight procedural | Gold steel ring expanding |
| Battle Rage (W) | Buff | Lana backlight + gold aura loop | Swirling gold particles on hero + nearby pets |
| Oath Ward (E) | Heal | Heal procedural (rising column + ring) | Warm heal glow around tower |
| Lantern Charge (R) | Cleave + Aoe | Mirza ACTION explosion + procedural nova | Bright flare, shockwave ring, brief light flash |

**Palette:** Steel-gold (0.92, 0.86, 0.70), polished, armored reads.

---

### C.2 Ranger (control)

| Spell | Effect Type | Asset Strategy | VFX Signature |
|---|---|---|---|
| Quick Shot (Q) | Strike | Ranger procedural arrow | Leaf-green streak + impact burst |
| Snare Trap (W) | Snare | Snare procedural (strike + ground ring) | Ground mark with thorny ring |
| Tanglefield (E) | GlobalSlow | Mirza STORM vines/fog loop + Lana ring | Expanding slow-zone indicator (blue ring pulse) |
| Storm of Arrows (R) | Aoe + Projectile | Ranger procedural + multi-arrow cascade | Rain of arrows, ground burst, ring |

**Palette:** Leaf-green (0.48, 0.95, 0.55), nature-focused, fleet reads.

---

### C.3 Mage (AoE/DoT)

| Spell | Effect Type | Asset Strategy | VFX Signature |
|---|---|---|---|
| Arcane Bolt (Q) | Strike | Arcane procedural (tracer + spark) | Aether beam + bright impact |
| Cinder Field (W) | DotZone | Mirza ACTION fire loop + Lana area red | Burning ground zone, embers rising, heat shimmer |
| Frost Nova (E) | Freeze | Mirza SHOCKWAVES crystal nova + ice blue | Crystal burst, freeze flash, blue shards |
| Frostfire Meteor (R) | Meteor + Freeze | Meteor procedural + ice overlay | Falling fire-blue streak, ground ring, freeze pulse |

**Palette:** Arcane (0.90, 0.87, 1.00) + element overlays (flame/ice), magical intensity.

---

## Part C.4: UI Shield (Protective Overlay)

**Purpose:** When the tower is warded, buffed, or under a protective effect (Oath Ward, team buffs), a screen-space shield glow signals the status to the player.

**VFX Strategy:**

| Trigger | Effect | Asset | Visual |
|---|---|---|---|
| **Oath Ward cast** (E) | Shield active | Mirza CONSTR ring pulse OR procedural glow | Faint protective aura framing screen edge, gold-silver shimmer |
| **Battle Rage active** (team buff) | Damage boost | Lana backlight glow + Mirza TITLES streaks | Warm gold overlay (subtle), pulsing at rhythm of combat |
| **Freeze / GlobalSlow zone enter** | Debuff signal | Mirza SHOCKWAVES crystal + ice blue | Brief blue flash on edges (warning: slowed), subtle frost vignette |
| **Heal over time** | Healing active | Heal procedural + green glow | Soft green pulse at screen bottom / HUD area |

**Implementation:**

New enum:
```csharp
public enum UIShieldEffect
{
    ProtectiveWard,    // Gold-silver shield frame
    DamageBoost,       // Warm gold pulse
    SlowZoneWarning,   // Ice blue flash edges
    HealingActive,     // Green pulse bottom
}
```

New UI component: `ScreenShieldOverlay.cs`
- Renders a **lightweight quad (2D Canvas layer)** with a shader that pulses/glows
- Maps `UIShieldEffect → VFX prefab` (could be Mirza loop or procedural shader)
- Fades in/out based on spell duration
- **Non-blocking:** sits behind HUD, doesn't obscure gameplay
- Respects `VFXQuality` (disabled on Low, subtle on Medium, full on High)

**Asset picks:**
- Mirza SHOCKWAVES: `crystalNova` (can be stretched to screen-space shimmer)
- Mirza TITLES: `streaks`, `core` (for warmth/pulse)
- Lana backlight: resource glows (can layer as screen tint)
- Procedural fallback: simple radial gradient (white → color) that pulses

**Example:** Oath Ward cast → gold ring appears at screen edges, held for 8s, fades out. During active duration, a faint shimmer pulses at 0.8 Hz (visual heartbeat of protection).

---

## Part C.5: Water Treatment (Environmental VFX)

**Purpose:** The village water (river, moat, fountains) is currently flat/dull. Layer water FX to signal flow, depth, and magical properties.

**VFX Strategy:**

| Water Location | Effect | Asset | Visual |
|---|---|---|---|
| **Moat/defensive water** | Flow + protection shimmer | Mirza STORM water loop + CONSTR ripples | Flowing water with subtle blue shimmer (defensive) |
| **Crystal Mine pool** | Resource glow | Lana backlight + Mirza SHOCKWAVES ripples | Glowing crystal-blue water, soft particles rising |
| **Fountain (village center)** | Ambient life | Mirza STORM rain reverse + sparkles | Water droplets catching light, gentle shimmer |
| **River (edge of map)** | Flow simulation | Mirza CONSTR liquid effects + procedural foam | Fast-flowing, foamy, directional current visual |

**Implementation:**

New component: `WaterVFXLayer.cs` (attaches to water meshes / planes)
- Spawns loop VFX at water surface (one per water volume)
- Adjusts particle density / speed based on `VFXQuality`
- Culls when camera is far (performance gate)
- Syncs color with world time / biome (moat is cooler blue, mine pool is crystal cyan)

**Asset organization:**
- Create `Assets/_Modules/Village/Vfx/WaterFXPrefabs/` folder
- Copy + adapt Mirza STORM water effects (rain reversed = bubbles rising)
- Copy Lana area effects (can tint blue for water)
- Procedural foam shader for rivers (if needed)

**Color palette per location:**
- **Moat:** Cool blue (0.50, 0.80, 1.00) with subtle shimmer (silver highlights)
- **Crystal Mine pool:** Cyan-blue (0.30, 0.85, 1.00), crystal sparkles overlay
- **Fountain:** White + rainbow spray (neutral, celebratory)
- **River:** Dark blue (0.25, 0.45, 0.70), foam white edges

**Performance gate:**
- Low quality: moat only (one loop)
- Medium: moat + fountain
- High: all four + particle density 100%

**Example:** Walking near the moat, you see water flowing with soft blue shimmer — signals "this is defensive water, part of the fortress." Crystal mine glows from within (resource richness). Fountain at village center has gentle spray catching light (village heart is alive).

---

## Part C.6: Tower Projectile Trail VFX

**Purpose:** When towers fire (existing mechanic), projectiles now have visual trails. Towers built dynamically can be any type (fire/ice/arcane), so projectile VFX must match tower element and be cached per tower.

**VFX Strategy:**

New enum: `TowerProjectileType`
```csharp
public enum TowerProjectileType
{
    Arrow_Physical,    // plain arrow, no trail
    Arrow_Fire,        // flaming arrow, orange trail
    Arrow_Ice,         // icy arrow, blue trail
    Bolt_Arcane,       // arcane bolt, purple trail
}
```

Map to VFX:
| Tower Type | Projectile | Asset | Visual |
|---|---|---|---|
| **Basic Tower** | Arrow_Physical | Mirza CONSTR streaks (thin) OR procedural | Plain green arrow (ranger-style) |
| **Fire Tower** | Arrow_Fire | Mirza ACTION flamethrower / fire trail | Orange flame-trail, bright impact burst |
| **Frost Tower** | Arrow_Ice | Mirza SHOCKWAVES ice / Mirza STORM snow trail | Blue crystal trail, freeze burst on hit |
| **Arcane Tower** | Bolt_Arcane | Mirza base streaks / aether procedural | Purple arcane beam, arcane explosion |

**Implementation:**

New component: `TowerProjectileVFX.cs` (attaches to projectile GameObject at spawn)
- Reads tower's element type
- Spawns a loop VFX trail (parent = projectile transform, follows it to target)
- On hit → impact burst (from VFXManager.Play)

```csharp
public class TowerProjectileVFX : MonoBehaviour
{
    [SerializeField] private Tower _tower;  // owner tower
    private VFXHandle _trailHandle;
    
    private void OnEnable()
    {
        if (_tower == null) return;
        
        // Map tower element → VFXType
        var projType = _tower.Element switch
        {
            TowerElement.Fire   => TowerProjectileType.Arrow_Fire,
            TowerElement.Frost  => TowerProjectileType.Arrow_Ice,
            TowerElement.Arcane => TowerProjectileType.Bolt_Arcane,
            _                   => TowerProjectileType.Arrow_Physical,
        };
        
        // Spawn trail (follows this projectile)
        var trailVfxType = ResolveTrailVfxType(projType);
        _trailHandle = VFXManager.Instance?.PlayProjectile(trailVfxType, transform);
    }
    
    public void OnHit(Vector3 hitPos)
    {
        // Stop trail
        _trailHandle?.Stop(gracefulFadeTime: 0.2f);
        
        // Impact burst at hit position
        var impactType = ResolveImpactVfxType(_tower.Element);
        VFXManager.Play(impactType, hitPos, Quaternion.identity);
    }
}
```

**Asset picks:**
- Fire arrow trail: Mirza ACTION `flamethrower` loop (stretched to projectile path) or Mirza base `fireMouse` loop
- Ice arrow trail: Mirza STORM `mediumSnow` or SHOCKWAVES `crystalNova` (burst on hit)
- Arcane bolt: Mirza base `synapse` or `nucleus` (fits arcane vibe)
- Physical arrow: Ranger procedural arrow from §C.2 (no extra trail, just arc)

**Performance gate:**
- Low quality: no trails, impacts only
- Medium: trails + impacts (1 loop per projectile in flight)
- High: all above + trail particles at full density

---

## Part C.8: Shield of Elarion (Protective Spell — VFX Showcase)

**Purpose:** A dual-cast defensive spell: hero can cast it (class ability), tower can cast it (defensive ability). **Primary focus is visual impact** — screen-space shield UI + protective aura on tree. Mechanics (cooldown, cost, duration, damage reduction %) TBD by owner.

**VFX Design:**

This spell showcases the full VFX factory stack:
1. **UIShieldOverlay** — gold-silver protective frame at screen edges
2. **SpellVfxFactory** → world VFX aura on tree
3. **Graceful expire** — fade aura + shield break impact

| Layer | Asset | Visual | Duration |
|---|---|---|---|
| **Screen UI** | Mirza TITLES core loop | Gold-silver protective frame, subtle pulse (0.8 Hz) | Spell active |
| **World Aura** | Mirza CONSTR ring pulse + Lana backlight glow | Swirling protective aura around tree (blue-gold shimmer) | Spell active |
| **Impact (expire)** | Mirza CONSTR impact burst | Brief white flash + fading shield particles | 0.5s |

**Implementation (VFX-centric):**

Two paths: Hero casts / Tower casts (both trigger same VFX).

```csharp
// Hero ability (e.g., Knight's Oath Ward variant)
public void CastShieldOfElarion()
{
    float duration = 10f;  // TBD by owner
    
    // Play UI shield
    UIShieldOverlay.Instance?.Play(UIShieldEffect.ProtectiveWard, duration);
    
    // Play world aura on tree
    var auraHandle = VFXManager.Play(VFXType.Aura_ShieldOfElarion, _heartOfElarion.position);
    
    // After duration, cleanup + impact
    StartCoroutine(ShieldExpireAnimation(auraHandle, duration));
}

// Tower ability (e.g., HeartController.ActivateDefensiveShield)
public void ActivateDefensiveShield()
{
    float duration = 10f;  // same as hero for now, TBD
    
    // Exact same VFX calls
    UIShieldOverlay.Instance?.Play(UIShieldEffect.ProtectiveWard, duration);
    var auraHandle = VFXManager.Play(VFXType.Aura_ShieldOfElarion, this.transform.position);
    StartCoroutine(ShieldExpireAnimation(auraHandle, duration));
}

private IEnumerator ShieldExpireAnimation(VFXHandle auraHandle, float duration)
{
    yield return new WaitForSeconds(duration - 0.3f);
    auraHandle?.Stop(gracefulFadeTime: 0.3f);  // fade aura 0.3s before expire
    
    yield return new WaitForSeconds(0.3f);
    
    // Impact burst at tree position
    VFXManager.Play(VFXType.Impact_ShieldBreak, this.transform.position);
}
```

**VFX Types to add:**
```csharp
Aura_ShieldOfElarion,    // protective loop (Mirza ring + Lana glow)
Impact_ShieldBreak,      // expire burst (white flash + fading particles)
```

**Asset picks:**
- **Aura:** Mirza CONSTR `ringPulse` (loop) + Lana backlight `diamondGlow` (gold-blue layer)
- **Impact:** Mirza CONSTR `impactBright` or `shockwave` (white flash, fades fast)

**Mechanics (Placeholder — confirm with Creative):**
| Stat | Hero | Tower |
|---|---|---|
| **Cost** | 30 Essence | 50 Gold |
| **Duration** | 10s | 8s |
| **Cooldown** | 20s | 30s |
| **Effect** | Heart absorbs next 2 hits OR -50% damage for duration | Heart absorbs next 1 hit OR -30% damage for duration |

*Rationale:* Hero version is stronger (more essence cost, longer duration, better effect). Tower version is conservative (can't spam, lower power). Both use same VFX, so visuals don't change if mechanics shift.

**Why this spell:**
- Tests UIShieldOverlay + SpellVfxFactory together (primary goal)
- Dual-cast path (hero + tower) exercises factory flexibility
- VFX-first: mechanics are placeholder, visuals are the deliverable
- Shows quality gating in action (aura scales Low/Medium/High)

---

## Part C.7: ATB Battle (Retro 2D VFX)

**Purpose:** The same spells resolve in ATB combat (turn-based), but in a retro 2D UI style. Spells need ATB-appropriate feedback: colored text effects, screen flashes, and retro glyph bursts (not particle systems).

**VFX Strategy:**

The ATB **BattleVfx** system is data-driven (effect id → catalog entry). Extend it for spells:

| Spell (ATB mode) | ATB Effect | Glyph | Color | Flourish |
|---|---|---|---|---|
| **Battle Rage** | Team buff signal | ✦ | Gold (0.92, 0.78, 0.30) | Brief screen tint (warm overlay) |
| **Tanglefield** | Global slow warning | ◆ | Blue (0.50, 0.80, 1.00) | Screen flash edges (vignette pulse) |
| **Cinder Field** | DoT zone active | ◉ | Flame (1.00, 0.45, 0.18) | Screen pulse (heat shimmer effect) |
| **Frostfire Meteor** | Freeze + impact | ❄ | Ice (0.55, 0.85, 1.00) | Crit-style flash (bright white) |

**Implementation:**

New enum: `ATBSpellEffect`
```csharp
public enum ATBSpellEffect
{
    Rage,         // Gold buff signal
    Tanglefield,  // Blue slow warning
    CinderField,  // Flame DoT glyph
    Frostfire,    // Ice freeze burst
}
```

Extend `BattleVfx._catalog` with spell entries:
```csharp
// In BattleVfx._catalog — add:
{ "spell_rage",       new VfxEntry { Color = _goldColor, Glyph = "✦" } },
{ "spell_slow",       new VfxEntry { Color = _iceColor, Glyph = "◆" } },
{ "spell_cinder",     new VfxEntry { Color = _flameColor, Glyph = "◉" } },
{ "spell_freeze",     new VfxEntry { Color = _iceColor, Glyph = "❄" } },
```

Add spell-specific flourishes to `BattleVfx`:
```csharp
// Call from ATBCombatManager when spell resolves
public void OnSpellResolved(ATBSpellEffect spell, string targetId, BattleState state)
{
    // Catalog glyph + number
    Burst(targetId, "spell_" + spell.ToString().ToLower());
    
    // Spell-specific flourishes (beyond standard hit/heal feedback)
    switch (spell)
    {
        case ATBSpellEffect.Rage:
            ScreenTint(new Color(1f, 0.86f, 0.55f, 0.2f)); // warm gold overlay
            break;
        case ATBSpellEffect.Tanglefield:
            ScreenEdgeFlash(_iceColor, 0.3f); // blue vignette pulse
            break;
        case ATBSpellEffect.CinderField:
            ScreenPulse(_flameColor, 0.25f); // heat shimmer 2–3 frame pulse
            break;
        case ATBSpellEffect.Frostfire:
            ScreenFlash(); // crit-style bright white
            break;
    }
}
```

**Asset Note:** ATB is entirely code-built (no external VFX assets — glyphs are Unicode characters, colors are hardcoded). Spells inherit this style. No catalog assets needed for ATB.

**Integration points:**
- `ATBCombatManager` or spell resolution → call `BattleVfx.OnSpellResolved()`
- BattleVfx reads active spell effect from the engine's state (similar to how it reads element)
- Per-spell flourishes are conditional (buff doesn't shake, cinder doesn't flash, etc.)

---

## Part D: Implementation Roadmap

### Phase 1: Asset Catalog + Wiring (Creative + LD)

**Task 1.1:** Create `VFXCatalog_Spells.asset` (ScriptableObject)
- One entry per spell per class (9 total)
- Wire prefabs from Mirza / Lana (see playbook §C)
- Set PoolSize (4–8), IsLoop (if dotzone/buff/aura), MinQuality

**Task 1.2:** Create `VFXCatalog_UI.asset` (UI shield effects)
- One entry per `UIShieldEffect` (4 total: ProtectiveWard, DamageBoost, SlowZoneWarning, HealingActive)
- Wire from Mirza TITLES + SHOCKWAVES + Lana backlight
- Set as loops (durations driven by spell runtime)

**Task 1.3:** Create `VFXCatalog_Water.asset` (environmental water)
- One entry per water location (4 total: moat, mine pool, fountain, river)
- Adapt Mirza STORM water loops + Lana area effects
- Set MinQuality (Low = moat only, High = all four)

**Task 1.4:** Create spell VFXType enum extensions
```csharp
// Add to VFXType.cs
Cast_BattleRage, Cast_FrostNova, DotZone_CinderField, Aura_RageTeam, Zone_Slow_Tanglefield, // spells (9 total)
UI_Ward_Protective, UI_Boost_Damage, UI_Warning_Slow, UI_Active_Healing, // UI shield (4 total)
Water_Moat_Flow, Water_MinePool_Glow, Water_Fountain_Spray, Water_River_Current, // water (4 total)
```

### Phase 2: Factory Architecture (CLI code, core)

**Task 2.1:** Wire enums
- `AbilityEffect` enum (4 new: Buff, GlobalSlow, DotZone, Freeze)
- `UIShieldEffect` enum (4 new: ProtectiveWard, DamageBoost, SlowZoneWarning, HealingActive)

**Task 2.2:** Build `SpellVfxFactory.cs`
- Maps `(AbilityEffect, HeroClass) → VFXType`
- Delegates to `VFXManager.Play*()` (pooling, quality gating, audio)
- Procedural fallback when prefab is null

**Task 2.3:** Build `UIShieldOverlay.cs`
- Canvas-based screen-space shield renderer
- Maps `UIShieldEffect → VFXType`
- Pulses/glows based on spell duration (fades in/out gracefully)
- Respects `VFXQuality` (disabled on Low, subtle on Medium, full on High)

**Task 2.4:** Build `WaterVFXLayer.cs`
- Attaches to water meshes in village
- Spawns loop VFX at surface (one per water volume)
- Adjusts particle density by `VFXQuality`
- Culls when camera is far (perf gate)

**Task 2.5:** Extend `VFXManager` with entry points
```csharp
// Spells
public void PlaySpellEffect(AbilityEffect kind, Vector3 pos, Transform caster, string heroClass);
public VFXHandle PlayLoopingZone(AbilityEffect kind, Vector3 pos, float radius);

// UI shield
public void PlayUIShield(UIShieldEffect kind, float duration);

// Water (auto-spawned by WaterVFXLayer, but exposed for testing)
public VFXHandle PlayWaterEffect(VFXType waterType, Transform waterSurface);
```

### Phase 3: Integration (owner + CLI)

**Task 3.1:** Wire spell resolution (SPELL_BOOK_DESIGN.md §B)
- Enemies act on Slow/Freeze (EnemyDamageable flag checks)
- Spell effects trigger factory calls (HeroAbilities.Cast → SpellVfxFactory.Play)

**Task 3.2:** Wire shield UI trigger
- Oath Ward cast → `UIShieldOverlay.Play(ProtectiveWard, 8.0f)`
- Battle Rage cast → `UIShieldOverlay.Play(DamageBoost, 20.0f)`
- Slow/Freeze enter zone → brief `SlowZoneWarning` flash

**Task 3.3:** Wire water layer into village
- Add `WaterVFXLayer` component to each water mesh (moat, mine pool, fountain, river)
- Set waterType in inspector (Water_Moat_Flow, etc.)
- Test culling / performance at different quality levels

**Task 3.4:** Test beat suite
- Spell casting: 5 min per spell (visuals + audio + duration)
- Shield UI: 3 min per trigger (overlay appears, pulses, fades)
- Water: 3 min per location (performance check on Low/Med/High)
- Cross-check: no z-fighting, no overdraw, shield doesn't obscure HUD

---

## Part E: Deliverables

### E.1 Code
- `SpellVfxFactory.cs` (routing layer for spells)
- `UIShieldOverlay.cs` (canvas-based protective UI layer)
- `WaterVFXLayer.cs` (environmental water treatment)
- VFXType extensions (9 spell + 4 UI shield + 4 water = 17 new types)
- AbilityEffect + UIShieldEffect enum extensions
- VFXManager extensions (spell + UI + water entry points)

### E.2 Assets
- `VFXCatalog_Spells.asset` (9 catalog entries, prefabs wired)
- `VFXCatalog_UI.asset` (4 UI shield entries, loops + fades)
- `VFXCatalog_Water.asset` (4 water treatment entries, quality-gated)
- `Assets/_Modules/Village/Vfx/WaterFXPrefabs/` folder (adapted Mirza + Lana water loops)

### E.3 Documentation
- **`SPELL_VFX_CATALOG.md`** (asset map + creative playbook per spell)
- **`UI_SHIELD_DESIGN.md`** (when + how shield overlay triggers, visual language)
- **`WATER_TREATMENT_DESIGN.md`** (location + color + vibe per water volume)

---

## Part F: Constraints & Acceptance Criteria

### F.1 Must-Have (Spells)
- [ ] All 9 spells have a prefab wire in VFXCatalog_Spells OR a procedural fallback
- [ ] Spell effects sync with damage/control (damage pop + VFX at same time, slow visual + actual slow, etc.)
- [ ] Quality gating respected (Low quality skips expensive loops)
- [ ] Audio bridges in place (VFXManager.VfxToSfx extended for spell types)

### F.2 Must-Have (UI Shield)
- [ ] UIShieldOverlay renders on Canvas layer (behind HUD, non-blocking)
- [ ] 4 shield effects wired (ProtectiveWard, DamageBoost, SlowZoneWarning, HealingActive)
- [ ] Fades in/out gracefully (no jarring pop-in)
- [ ] Respects VFXQuality (disabled on Low, subtle on Medium, full on High)
- [ ] Test: cast Oath Ward → shield frame appears, pulses for 8s, fades out

### F.3 Must-Have (Water Treatment)
- [ ] All 4 water volumes have WaterVFXLayer component + wired effect type
- [ ] Water loops play at startup, no pop-in
- [ ] Performance: culls when camera is > 30m away (or config'd distance)
- [ ] Quality gating: Low = moat only, Medium = moat + fountain, High = all four
- [ ] Test: walk near each water volume, verify visual is appropriate (moat = defensive blue, mine = resource glow, fountain = celebratory, river = flow)

### F.4 Code Quality
- [ ] Brace balance on all `.cs` files (CLAUDE.md §1)
- [ ] No cross-assembly calls (Village → Core only; no Spells → elsewhere, per CLAUDE.md §5)
- [ ] Null-conditional operators on service calls
- [ ] All new VFXTypes added to VFXManager.VfxToSfx() switch (even if mapped to SfxId.None)

### F.5 Acceptance Test (Full Beat)
- [ ] Play village scene, test in order:
  - **Spells:** Cast each (9 total), verify VFX + audio + duration matches spec
  - **Shield UI:** Cast Oath Ward, verify overlay appears/pulses/fades; cast Battle Rage, verify damage boost glow
  - **Water:** Walk past moat (blue shimmer), mine pool (glow), fountain (spray), river (flow)
  - **Quality sweep:** Low/Medium/High settings, verify culling and loop counts match expectations
  - **Performance:** Monitor frame rate (should be no noticeable impact; water loops are cheap)

---

## Part G: What NOT to Touch

- `VFXManager.cs` internals (pool logic, quality gating) — factory routes TO it, doesn't modify it
- `AbilityVfxKit` (procedural layer is stable, fallback is final)
- Existing VFXTypes (no rename, no deletion)
- Scene bakes (VillageSceneBuilder stays untouched)

---

## Notes for Creative

**Asset pick guidance:**
- **Buff aura:** look for Mirza TITLES loop effects (fire, embers, streaks) or Lana backlight. Keep it subtle (team knows they're buffed from the UI hint + numbers; VFX shouldn't overwhelm).
- **Slow zone:** Mirza SHOCKWAVES or STORM for the "thing is slowing you" signal. Blue/cyan reads as freeze-y.
- **DoT zone:** Mirza ACTION fire loop (cinder) or CONSTR liquid pools (could be poison). Ground-painted, not flying.
- **Freeze cast:** Mirza SHOCKWAVES crystal effects. Bright, crystalline, "stopping you" visual.

**Color discipline:**
- Knight: always steel-gold (0.92, 0.86, 0.70), never the incoming element colour.
- Ranger: always leaf-green (0.48, 0.95, 0.55).
- Mage: inherit element colour OR arcane (0.90, 0.87, 1.00).

**Performance tier (for QA):**
- Low: no auras, no looping zones, impact-only.
- Medium: one loop max per screen (Aura_RageTeam OR a single Zone_Slow, not both).
- High: all effects at full fidelity.

---

## Notes on Shield of Elarion Architecture

**Q: Do we need a generic `IAbility` interface?**

**Answer: Not yet.** Keep it simple for now:
- **Hero:** Add `ShieldOfElarion` as a Knight ability (or shared across hero classes via HeroAbilities)
- **Tower:** Add `ActivateDefensiveShield()` method to HeartController
- **Both:** Route to the same VFX factory calls

**Why simple is better for now:**
1. We're validating VFX factory, not building a generic ability system
2. Both paths are just method calls that invoke VFXManager and UIShieldOverlay
3. If we need generic IAbility later (for gates, walls, etc.), we refactor; the VFX calls stay the same

**Future (post-WO-124):** If we want towers + gates + buildings to have shared ability logic, create IAbility then. But for this spell, explicit methods are fine.

---

## Notes for Architecture

**Future-proof, catalog-driven design:**

The factory is NOT spell-specific. It's a generic three-library router that scales to any effect type:

| Caller | Use Case | Code |
|---|---|---|
| **Spells** | Hero/tower abilities | `VFXManager.Play(VFXType.Cast_BattleRage, position)` |
| **Towers** | Projectile trails | `VFXManager.Play(VFXType.Projectile_Arrow_Fire, position)` |
| **Enemies** | Boss attacks, summons | `VFXManager.Play(VFXType.Boss_ChargePulse, position)` |
| **Environment** | Water, wind, events | `VFXManager.Play(VFXType.Env_RiverFlow, position)` |
| **UI** | Transitions, effects | `VFXManager.Play(VFXType.UI_ScreenFlash, position)` |

All use the **same routing**: VFXType enum → VFXCatalog → VFXManager. No code changes needed to add new categories.

**Why this stays light:**
1. **Catalog-driven, not code-driven.** New effect type = new row in catalog. No factory logic changes.
2. **Reuse existing pooling.** VFXManager handles all pooling, quality gating, audio bridges.
3. **Procedural fallback never silent.** If prefab is null, AbilityVfxKit creates a placeholder. No missing effects.
4. **One place to tune quality.** Change MinQuality in a catalog row, all systems respect it.
5. **No effect-specific code.** Each system just calls VFXManager with the right VFXType. No tight coupling.

**Example: Adding enemy boss VFX in 3 months**
```
Today:  Add VFXTypes: Boss_FireBurst, Boss_SummonPortal, Boss_ChargeAttack
        Create VFXCatalog_Enemies.asset, wire Mirza/Lana prefabs
        
Tomorrow: BossAI calls VFXManager.Play(VFXType.Boss_FireBurst, pos)
         Works instantly, respects quality gating, bridges audio, pools correctly
         Zero changes to SpellVfxFactory or VFXManager
```

---

## Timeline

- **Phase 1 (Asset Catalog):** 3–4 hours (creative picks prefabs for spells + UI shield + water)
- **Phase 2 (Factory Architecture):** 6–8 hours (code: SpellVfxFactory + UIShieldOverlay + WaterVFXLayer)
- **Phase 3 (Integration):** 3–4 hours (enemy slow/freeze logic, shield trigger wiring, water layer placement, full beat test)

**Total:** ~1.5 days (half day creative picking, day+ code + integration). Ready for spell + defensive polish playtest by EOD next sprint.

---

## Sign-Off

**Creator:** Claude (Architecture + Spec)  
**Owner Sign:** (Samantha — approve playbook §C, decide asset picks per spell)  
**CLI Sign:** (when Phase 2 tests pass)
