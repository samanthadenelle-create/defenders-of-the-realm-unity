# Required Art Packs — runtime manifest + travel policy

**Authority:** `docs/PAIN_POINTS_2026-07-26.md` §1.2 — RULING *"Tracked runtime + zip travel"*.
**Scope:** This file is the human checklist half of that ruling. The machine half is
`tools/art/verify-runtime-art.ps1` (run it on every fresh clone — see the onboarding checklist at
the bottom).

**Why this exists:** the big character/environment packs are **gitignored** (owner policy: git never
holds the multi-GB source packs). On a machine that only did `git pull`, the packs are absent, so the
game rendered **generic silhouettes / capsules ("Bryn is a pill"), untextured bodies, and identical
unarmed enemies** — and CI saw zero art. The fix is *not* to commit the packs; it is to (a) guarantee
**runtime-playable fallbacks live in tracked `Resources/` paths** so a fresh clone still *runs* with
*distinct* enemies/NPCs, and (b) write down how a new machine obtains the full packs (**zip / local
copy, never `git pull`**).

---

## 1. The policy in one paragraph

Git holds only what must render on a bare clone: the `Resources/Enemies/*` AccuRig bodies, the
committed People NPC pack (`Assets/Models/People/`, kept via LFS after a source-shrink), the hero
bodies in `Resources/Heroes/*`, and prefabs that reference those tracked meshes. **Everything under
`Assets/Models/*` except `People/` is gitignored** (see `.gitignore` lines 106-114, 304-311). The full
KayKit / Mystery-Monthly source trees **travel by zip or local copy** between the owner's machines;
they are *upgrades* over the tracked fallback, not a boot requirement. If a pack is missing, the game
must still run on the fallback — it just looks less rich (skeletons instead of per-type-armed Hollow
Ones, box doorway instead of KayKit dungeon geometry).

---

## 2. Pack table

| Pack | Needed by | Expected on-disk path | Tracked? | How to get it |
|---|---|---|---|---|
| **KayKit Skeletons 1.1** | Hollow Ones enemies (Minion / Golem / Necromancer bodies + Mage/Warrior/Rogue variants) | `Assets/Models/KayKit/KayKit Skeletons 1.1/` (source) — *note the top-level `Assets/Models/KayKit Skeletons 1.1/` is an empty stub, the real tree is under `KayKit/`* | **gitignored** | Zip / local copy from owner. Runtime fallback = `Resources/Enemies/Skeleton_*.fbx` (tracked). |
| **KayKit Adventurers 2.0** | Troop bodies (WO-771.13) + hero test bodies | `Assets/Models/KayKit Adventurers 2.0/` **and/or** `Assets/Models/KayKit/KayKit Adventurers 2.0/` | **gitignored** | Zip / local copy. Runtime fallback = `Resources/Heroes/*` hero bodies (tracked). |
| **KayKit Dungeon Remastered 1.1** | Dungeon geometry (WO-770.8) — already staged per audit | `Assets/Models/KayKit/dungeon/` (fbx/gltf) + `Assets/Models/KayKit/KayKit Dungeon Remastered 1.1.zip` | **gitignored** | Zip present in-tree at `KayKit/…1.1.zip`; unzipped tree under `KayKit/dungeon/`. Fallback = box doorway / builder placeholders. |
| **People pack** (optimized NPC pack, DEF-91) | Bryn-class NPCs, Blacksmith / Merchant / Peasant, `FighterClass` body | `Assets/Models/People/` (FBX + per-model `*/Textures/`) | **TRACKED (LFS)** — the one `Models/*` exception (`.gitignore` line 107 `!/Assets/Models/People/`) | Comes with the clone. If LFS pointers didn't hydrate: `git lfs pull`. |
| **People shared skin textures** | `FighterClass` / CC_Base body skin (the "untextured-Bryn" half) | `Assets/Models/People/textures/` **(gitignored — line 310)** | **gitignored** | Zip / local copy. NOTE: per-model `People/<NPC>/Textures/*.png` and the `*.fbm/textures/` embedded skins ARE tracked, so the committed Blacksmith/Merchant/Peasant/Fighter NPCs are textured; this shared folder only bites bodies that reference it. |
| **People Human / Orc / Troll variants** | extra NPC/enemy body variants | `Assets/Models/People/{Human,Orc,Troll}/` **(gitignored — lines 304-309)** | **gitignored** | Zip / local copy. Not required to boot. |
| **AccuRig `Resources/Enemies` family** (the LIVE FALLBACK) | Every Hollow Ones / Orc / boss body that renders on a bare clone | `Assets/Resources/Enemies/*.fbx` + `Boss_Dragon.prefab` + controllers | **TRACKED** | Comes with the clone. This is what keeps enemies distinct when the KayKit source is absent. |
| **Unity Technologies Particle Pack** (VFX source) | **54 owner-tagged VFX keys** in `Assets/Editor/VfxManualPicks.json` point into this tree; `Enemy.cs` (travelling fireball body) and `PoiCalloutSystem.cs` (`TreeofLifeAura_Aura` = ParticlePack FireFlies) consume it | `Assets/UnityTechnologies/ParticlePack/` (191 MB / 886 files) | **gitignored** (2026-07-30) | Zip / local copy from the owner. ⚠ **NO runtime fallback** — unlike the character packs, a machine without this pack silently loses those 54 tagged effects (`VFXManager.PlayKey` finds no prefab). Follow-up: promote the used prefabs into a tracked `Resources/VFX/` path, or ship the zip alongside. |

**Shared humanoid animator path (per the ruling):** all humanoids retarget through the AccuRig
`SkeletonHumanoid` controller / KayKit `Rig_Medium` (see `EnemyAnimatorFactory.cs` +
`docs/SME/KAYKIT_SME.md`). Modular weapons attach to that one path — **one perfect armed type
(Hollow Warrior) first**, then variants. Do not enable multi-weapon spam before Warrior feels good.

---

## 3. Tracked vs gitignored (quick reference — source: `.gitignore`)

**Committed / LFS (must be present on a bare clone — the fallback cast):**
- `Assets/Resources/Enemies/*` — AccuRig bodies: `Skeleton_{Warrior,Rogue,Mage,Healer,Golem,Minion}.fbx`, `Orc_*.fbx`, `Necromancer.fbx`, `Boss_Dragon.prefab`, controllers
- `Assets/Resources/NPCs/*.prefab` — `NPC_{Blacksmith,Merchant,Peasant_Mevina,Peasant_Tob}`
- `Assets/Resources/Heroes/*` — `Knight`, `KnightV3.fbx`, `Mage`, `Cleric`, controllers
- `Assets/Models/People/` — optimized People pack (the sole `Models/*` exception), incl. per-model `*/Textures/*.png`

**Gitignored (travel by zip / local copy):**
- `Assets/Models/*` **except** `People/` — all raw KayKit trees, Mystery Monthly, medieval, weapons
- `Assets/Models/People/{textures,Human,Orc,Troll}/` — shared skins + variant bodies
- `Assets/polyperfect/`, `Assets/Quaternius/`, `Assets/Supercyan/`, `Assets/Tech hud elements/`, bulky VFX packs, `Assets/Resources/Structures/` (Tripo)

**Do NOT commit** the multi-GB source packs — that is a separate owner LFS/zip decision, not this manifest.

---

## 4. Onboarding checklist — new clone

Run these in order on a fresh machine before expecting art to render:

1. `git lfs pull` — hydrate the tracked People pack + any LFS bodies.
2. **`pwsh tools/art/verify-runtime-art.ps1`** (or `powershell -File tools\art\verify-runtime-art.ps1`)
   — proves the CRITICAL tracked runtime keys + committed People body/textures exist, and WARNS about
   any gitignored source pack that is absent. **Non-zero exit = a tracked fallback is missing → fix
   before building** (the build would render pills/magenta).
3. If step 2 WARNs about a gitignored pack you actually need (KayKit dungeon, skeleton source, People
   skins), **copy the pack in from the owner's zip / source folder** into the expected path from the
   table above. Do not `git pull` it — it isn't in git.
4. Re-import the humanoid skeleton family if you staged new KayKit bodies:
   **`Defenders → Animation → Import Skeleton Family`** (documented required onboarding step per the
   ruling).
5. If a pack uses Built-in shaders and renders magenta, run its URP fixer
   (`Defenders/Art/Fix Polyperfect URP Materials`, `Defenders/Art/Fix Supercyan URP Materials`, etc.).

A bare clone with **no** extra packs copied in should still boot and show **distinct** enemies/NPCs
via the tracked `Resources/` fallback — that is the invariant `verify-runtime-art.ps1` guards.
