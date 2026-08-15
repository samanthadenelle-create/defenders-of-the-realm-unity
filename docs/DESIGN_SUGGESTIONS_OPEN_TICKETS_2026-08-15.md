# Design / implementation suggestions — open tickets (2026-08-15)

Owner rulings locked: Blink no; armor 2D; weapons placed 3D; 910 full design; 986 CoC;  
**991** follow-hero glass caravan; **994** shield breaks only dungeon→town port.

Creative proposals below are **suggestions** for the seats to accept, amend, or reject — not shipped canon until implemented + PO feel.

---

## WO-991 — Healing Caravan (ruled: slow follow, fragile)

### Fantasy
A **field hospital cart** that trundles behind the hero: useful when you **set a defense**, useless as a permanent bodyguard for a full siege path. Enemies that ignore the hero and **delete the caravan** punish poor positioning.

### Implementation sketch
1. **Unit, not grid building while rolling**  
   - Spawn as a slow `NavMeshAgent` / CharacterController follower (leash like `PetHeroLeash` but **much** slower: ~15–25% hero walk).  
   - Deadzone: only start moving if hero is >N m ahead; stop when close — “crawl catch-up,” not sticky orbit.

2. **Glass stats**  
   - Low max HP, high damage taken mult, no block.  
   - Optional: enemies prefer it when within small radius (threat bump) so leaving it in a choke is lethal.

3. **Support field (HealerTower pattern)**  
   - Base: tiny passive aura (or none).  
   - Unlock: full `SupportFieldStructure` heal (Heart + troops) when researched / caravan L2.  
   - While moving: **half magnitude**; while hero stationary nearby: full.

4. **Offensive/defensive read**  
   - “Defensive”: heal field when parked at the Heart / wall line.  
   - “Offensive”: follows into the field for a push, but must be **escorted** — not tanky enough to sit mid-map alone.

5. **UI**  
   - One chip: caravan HP + “following / idle.” No complex relocate UI.

### Acceptance feel
- You cannot clear a full raid by parking the caravan on auto-follow.  
- You *can* win a hard wave by parking it behind a wall and fighting in its radius.  
- Leaving it outside the walls = dead cart, loud feedback.

---

## WO-994 — Shield (ruled: only breaks dungeon→town)

### Do not
- Re-dial `shield_A` globally.  
- Change AlignAxes again.

### Suggested RCA path (instrument first)
1. **On dungeon exit / hub load**, log once:  
   `parent, gripLocalEuler, propLocalEuler, lossyScale, height, DRAWN|SHEATHED`  
   (same MEASURED line as code C — already exists post-ApplyHoldPose).
2. Diff **pre-Leave dungeon** vs **first frame town**.  
3. Likely fixes (pick by data):  
   - **A.** Town re-equip path double-`NormalizeInto` after body/height retarget — skip if mesh key + fullOverride already seated.  
   - **B.** Dungeon height (2 m) vs town (1.8 m) re-runs scale compensate only on one path — apply same compensate policy both sides for fullOverride.  
   - **C.** Carry state flips to sheathed on town load with wrong socket — force re-`ApplyHoldPose` after hero height settle (one frame later).

### Acceptance
- Play dungeon with shield looking correct → exit to town → **identical seat** without opening Seating Editor.

---

## WO-910 remainder — full design (creative clusters)

Path B continues; **stats are live**. Remaining are **features**. Propose **named mini-kits** (each a child WO):

| Cluster | Nodes | Pitch |
|---------|--------|------|
| **Ranger Mark** | Hunter’s Mark | Debuff on target: +dmg taken from hero; VFX: soft mark ring (shape, not hue) |
| **Ranger Mobility** | Tumble Step | Short dodge impulse + i-frames (reuse parry window feel) |
| **Ranger Storm** | Arrow Storm Prep + Storm of Arrows | Channel rain of arrows in cone; uses troop archer VFX later |
| **Ranger Venom** | Deep Freeze / Emberhead / Precision | Ammo riders: slow / burn / pierce (modifyAbility with real `stat`) |
| **Ranger Ghost** | Shadow Veil / Phantom Hunter | Brief stealth opacity + enemy de-aggro radius shrink |
| **Ranger Beast** | Beast Companion | Summon **Ice Wolf** body (same as FTUE guide art!) as combat pet — reuses leash/rig, limited duration |
| **Mage Rift** | Void Rift / Reality Rift / Cataclysm | Big AoE channel; CastVariant + Hovl explosion keys |
| **Mage Economy** | Aether Form / Aether Surge | Mana cost + on-kill mana (partially near existing mana regen) |
| **Mage Shell** | Arcane Shield | Map to existing Arcane Shell absorb if any; else short DR window |
| **Mage Echo** | Spell Echo / Elarion’s Legacy | Proc: 20% chance free re-cast of last ability |

**Order suggestion:** Mark → Venom riders → Storm → Beast (art reuse) → Mage shell → Rift capstones.

---

## WO-935 — Paid anim/VFX (creative first slice)

**North star you already named:** `Cast("fireball")` / `Cast("heal", target)` for hero + enemy + troop.

### Suggested Phase 1 (one week, high ROI)
1. **`CombatCast.Play(spellId, caster, target?)`** in Village (not a second catalog):  
   - anim: `IActorAnimator.PlayCast(variant)`  
   - VFX: `SpellVfxFactory` / `PlayKey` cast → proj → impact  
   - effect: optional damage/heal callback  
2. **Wire three ids only:** `fireball`, `arcane_bolt`, `heal` (enemy RootedCast + mage troop + hero ability map).  
3. **Troop mage** first consumer (zero VFX today = worst pack ROI).  
4. Leave full Hovl matrix for later phases.

### Feel win
One API → packs finally show on the unit that never had VFX.

---

## WO-980 — Dungeon camera framing

Owner still needs WAI vs defect. **If defect**, creative options that respect “don’t break follow”:

| Option | Idea |
|--------|------|
| **Rim, not camera** | Soft rim/fill light on hero in dungeons so silhouette separates without moving OTS |
| **Torch discipline** | Cap torch bloom / exposure in dungeon volume; keep OTS distance |
| **Slight pull-back** | +0.3–0.5 m camera distance only in tight rooms (data: room size probe) |
| **Look-ahead bias** | Small look-at offset toward move heading so walls aren’t filling the frame |

Recommend: **rim + torch** first (doesn’t touch proven follow).

---

## Other backlog (creative, implementable without new pins)

| Ticket | Suggestion |
|--------|------------|
| **975 weapons** | Curated **Resources/Heroes/Props/Weapons** set as ship path; Gear.asset only lists those; Blink stays local placeholder never required |
| **992 dead code** | WeatherManager keep; Torch/Aura research; delete BattlePass/Crypto/Cosmetic if still unwired after owner confirm on “ideas not implementations” |
| **993 Echo faucet** | Aura/progression off; **keep wolf guide + leash**; harvest Echoes as pure HUD/economy — no world body |
| **1006 Manage launcher** | Already ruled option A — implement when free: Manage opens launcher, upgrades live in category browsers |
| **987 confirm** | Landed — add haptic / one-line bark “Leave?” for mobile feel |

---

## Suggested CLI order (no further owner pin)

1. **994** instrument dungeon→town equip reapply (data before dial).  
2. **991** follower shell + glass HP (heal field unlock later).  
3. **935** thin `CombatCast` + troop mage fireball.  
4. **910** Ranger Mark cluster (smallest full-ability fantasy).  
5. **980** only after you say “defect” vs “WAI.”
