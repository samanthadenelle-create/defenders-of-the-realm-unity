# Asset Inventory — what we actually have (due-diligence map)

> **Why this exists:** most of our art is in **gitignored vendor packs** that never entered
> `docs/MASTER_CATALOG`, so every fresh agent (and asset searches) were blind to ~21,000 mesh
> files. Owner caught it 2026-06-24 ("we don't really even know what we have"). This is the
> correct map — **factual, not a recommendation**. Gitignored != invisible anymore.
>
> Interim level of detail: pack -> what it is, counts, notable items, gitignored?, used-in-game?.
> Full per-area detail in the section docs below.

## Sections
| # | Doc | Covers |
|---|-----|--------|
| 01 | [`01_kaykit.md`](01_kaykit.md) | All KayKit packs — characters, the shared anim rig, weapons, dungeon, skeletons, environment, bits |
| 02 | [`02_models_tripo.md`](02_models_tripo.md) | Models/People (NPCs), Pet, CastleGate, Cathedral, the Tripo hero + structures |
| 03 | [`03_polyperfect_quaternius_chars.md`](03_polyperfect_quaternius_chars.md) | polyperfect, Quaternius, Supercyan, Blink, Lana Studio |
| 04 | [`04_vfx_spells_audio.md`](04_vfx_spells_audio.md) | Mirza Beig, Spells Pack, Black Dragon, Action (anims), Audio, Shaders |
| 05 | [`05_resources_project_built.md`](05_resources_project_built.md) | Committed Resources (what the live game loads), Prefabs, Scenes, Data catalogs |

## One-page headline: what we actually have

### Characters / rigs (the big picture)
- **CURRENT SHIPPED HERO:** `Resources/Heroes/Knight.fbx` — Tripo self-rigged, Humanoid, loaded by
  `HeroBodySwapper`. Enemies = the Tripo **Orc family** (Warrior/Tank/Mage) via `EnemyAnimatorFactory`
  shared `OrcHumanoid` controller + per-role override clips. (This is V1, per the combat pivot.)
- **THREE shared-rig character libraries sit UNUSED (gitignored):**
  - **KayKit Adventurers + Mystery Monthly 4/5** — 9 + ~33 themed humanoids (Knight, Mage, Ranger,
    Barbarian, BlackKnight, OrcRaider, Paladin, Werewolf, Vampire, FrostGolem…) all retargeting off
    **`KayKit Character Animations 1.1`** (one shared rig: Rig_Medium 8 clip sets / Rig_Large 6 + Mannequins).
  - **Supercyan "Character Pack: Fantasy"** — 8 humanoids (Knight, Archer, Mage, Wizard, Barbarian,
    **Orc, Skeleton, Demon**) on one shared rig + ~51 combat anims.
  - **Blink** (~13 GB, JUNKED hero-armor pack) — ~292 char prefabs + rigged Orc/OrcBoss; only the armor
    ICON pngs reached Resources. **End-of-polish PURGE candidate.**
- **People NPC pack** (`Models/People`, LFS-committed, the DEF-91 exception) — 4 rigged Tripo townsfolk
  (Blacksmith, Merchant, 2 Peasants) + a 3-LOD FighterClass set.
- **Static, no rig:** polyperfect People_M / Animals_M (display meshes only). Quaternius = buildings only.

### Animations
- **`Assets/Action`** — 401 Mixamo Humanoid clip FBX (Knight 99, etc.), tracked — the retarget source
  feeding hero + orc controllers (LargeHumanoid/OrcWarband families).
- **`KayKit Character Animations 1.1`** + **Supercyan Fantasy anims** — two more shared retarget libraries (gitignored).

### Weapons / props
- **KayKit Fantasy Weapons Bits** (~48: swords A–G, axes/hammers A–D, daggers, bows, staves, spears,
  halberd, scythe, fistweapons, shields A–D) + **Adventurers weapon subset** (~58 incl. _Large + shield
  variants). Mesh names (`sword_A`, `shield_A`…) match what the equip/Offset-Forge system expects.
- **Tripo weapon props** in `Resources/Heroes` (current shipped weapons).

### VFX / Audio (the "wow" lever)
- **~1,000 VFX effects available, only ~38 wired.** Mirza Beig (564 prefabs: shockwaves, storm/lightning,
  fire/explosion, novas, portals) + Spells Pack (466: clean element matrix Casting/Projectile/Explosion/
  Aura/Buff/Shield × Arcane/Fire/Ice/Storm/Dark/Light/Nature). `VFXCatalog.asset` maps ~50 types -> 38
  GUIDs via 9 project-built wrapper prefabs in `Resources/VFX/Projectiles/`. Unmapped = procedural fallback.
- **Audio:** 18 music mp3s + 1 mixer; SFX is **procedural** (`ProceduralSfx.cs`) — no SFX clip files.
- **Black Dragon** = a single creature FBX (likely DragonBoss). **Shaders** = ForceFieldGate + RoundedChatBubble.

### Project-built / shipped
- **40 JSON data catalogs** in `Resources/Data/Canonical/` (mirrored to StreamingAssets): abilities,
  hero-talents, enemies, weapons, waves, buildings, region-gates, dialogues.
- **19 scenes:** MainCastle_Hall (home), OuterWorld (additive), Village2 (raid), Village.unity (abandoned),
  4 Garrison + 4 RaidBase + 3 Dungeon + ATB/Title/selects.

## Flags raised by the survey (tickets)
- `WaveManager` path-references `KayKit/enemies/*.glb` but those dirs are **empty placeholders** — dangling load target.
- **Blink ~13 GB** dead weight on disk — purge at end-of-polish (per `asset-purge-deferred-to-polish-end`).
- ~10 KayKit prop kits (City Builder, Board Game, Halloween, Platformer, Restaurant…) have **zero code references**.
- `Models/Cathedral/` = empty placeholder.

## Strategic note (NOT a decision — context only)
The recurring "every new character = bone-name/seating pain" worry is solved by **Humanoid import + a
valid avatar** (names/bone-count stop mattering; clips retarget in normalized muscle space). We already own
**three** libraries built exactly that way (KayKit, Supercyan, + the Action clip lib). Whether to stay
single-rig (Tripo) or adopt a shared library is an **art-direction decision for the owner** — this map just
makes it an informed one. Per-weapon grip offsets are rig-specific; one rig standard = one offset per weapon.
