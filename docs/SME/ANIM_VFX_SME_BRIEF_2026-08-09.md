# SME Brief — Animation catalogs + VFX catalogs (2026-08-09)

**Purpose:** True-SME snapshot after reading the live doc set + measuring disk.  
**Not** a purchase recommendation. **Not** an implementation.  
**Implement via:** `WorkOrders/WORK_ORDER_935_paid_anim_vfx_connection_program.md`.

---

## 1. What "catalog" means here (three layers)

| Layer | Animation | VFX |
|-------|-----------|-----|
| **A. Pack stock** | Action Mixamo, Studio Mocap sets, KayKit Char Anim, Supercyan anims | Hovl, Spells, Mirza, Particle Pack, Lana |
| **B. Game catalog** | Controllers in Resources/Heroes + Enemies; motion-castings.json | VFXType enum + VFXCatalog + HovlVfxCatalog |
| **C. Call sites** | ActorAnimator / TroopController / Enemy | VFXManager / facade / towers / hero |

**Law:** Pack stock is worthless without B+C. Layer B without C is "catalog cosplay."

---

## 2. Animation SME synthesis

### Canon pipeline
- Everything Humanoid where possible (`ANIMATION_PIPELINE.md`).  
- Shared `Assets/Action/` + per-class folders; factories bake controllers.  
- Param vocabulary: `AnimParams` only.

### Hero (highest paid mocap ROI)
- Live: KnightV3 + **KnightMocap** + motion-castings.  
- Studio Mocap S&S: **~9/45 clips used** — defensive matrix and strafe tree mostly idle.  
- Known debt: generic Cast baked as sword slash; skill1/2 kung-fu kicks; R cast VFX silent.

### Enemies (enemy-codex is the roster contract)
- Hollow Ones: AccuRig family + SkeletonHumanoid (Action Mixamo) **baseline complete**.  
- KayKit Generic for Minion/Golem/Necromancer.  
- **GAP-PRIMARY:** Werewolf quadruped — blocks wolf enemy, boss phase, ice pet.  
- Wildlands **deferred** by owner; Hollow Ones ratified.

### Troops
- Controllers: Knight / Ranger / Mage by animator field (2026-08-09).  
- Strike: Attack vs Cast correct.  
- **Zero VFX** on troop hits — disconnect from paid VFX investment.

---

## 3. VFX SME synthesis

### Canon pipeline
- Handbook: never runtime-load gitignored packs; mirror into Resources; Family A/B; facade → VFXManager.  
- Creative registry: element × 6-beat kit, owner-ratified.  
- Direction doc: loop-cap leak + HDR off + scale=1 — kill feel even when prefabs are good.

### Pack roles (what you bought them for)
| Pack | Job in this game |
|------|------------------|
| **Spells Pack** | Clean elemental Casting → Projectile → Explosion matrix (mage/towers) |
| **Hovl Bundle** | Stylized projectiles, AOE, magic circles, markers (demo-quality when scripts/HDR right) |
| **Particle Pack** | Recipe kitchen (fire thrower, muzzle, earth shatter, steam) |
| **Mirza** | Volume shockwaves/storms — use sparingly; URP risk |
| **Lana** | Lightweight RPG accents |

### Utilization honesty
Docs historically: ~1000 available, ~38 wired. Measured Resources/VFX gameplay prefabs ~54.  
That is normal if **every combat beat** is covered — abnormal if towers/troops/mages still silent.

---

## 4. Enemy codex as the joint anim+VFX matrix

For each codex archetype, both columns must eventually be green:

| Archetype | Anim | VFX |
|-----------|------|-----|
| Melee fodder | Attack | Impact_Physical / death |
| Caster / healer | **Cast** | Charge + bolt + impact |
| Boss channel | Special | Aura + channel |
| Wolf | Own rig | Telegraph howl |

Troops should **mirror** the same archetype table (raid friendly side).

---

## 5. SME verdict (before any more code)

1. **Do not buy more packs** until connection phases close.  
2. **Biggest waste today:** Spells/Hovl not on **troop mage/archer/melee**, and Studio Mocap defense/strafe unused on hero.  
3. **Biggest risk:** treating catalog rows as shipped when loop flags / HDR / caps hide them.  
4. **Source of truth for "who needs what":** enemy-codex roster + hero dictionary + creative picks registry — not freeform taste.  
5. Implementation order (ROI): **VFX health (caps) → troop raid feedback → hero mocap remap → enemy specials → mage showcase polish.**

---

## 6. Doc map (bookmark)

```
Animation
  ANIMATION_PIPELINE.md          ← canon method
  HERO_ANIMATION_DICTIONARY.md   ← live knight truth
  SWORD_SHIELD_MOCAP_SME.md      ← paid mocap underuse
  enemy-codex.md §5              ← enemy anim strategy + gaps
  CHARACTER_PACKS / KAYKIT SME

VFX
  VFX_PREFAB_HANDBOOK.md         ← canon pipeline
  VFX_CREATIVE_PICKS_REGISTRY.md ← element×beat picks
  VFX_DIRECTION_2026-08-05.md    ← P0 loop/HDR/scale
  HOVL_STUDIO_SME.md
  VFX_PACKS_SME.md
  MAGIC_VFX_LIBRARY.md
  ASSET_STORE_LEDGER_2026-07-12.md ← purchase list
```

_End brief._
