# Enemy Codex — Defenders of the Realm (Unity v2)

**Status:** Definitive enemy + boss design codex. **Design document — review-and-approve before any implementation.** Read-only on code and data; nothing here changes a `.cs` file or a `.json` until the owner ratifies it.
**Game:** Stylized low-poly magical+medieval crossover — a tower-defense village (defend Elarion against the Hollow Ones) plus a 3D dungeon-crawler (the Healer's Cottage and six future dungeons). Unity 6 LTS, URP.
**Owner:** DeNelle Studios.
**Date:** 2026-05-19.
**Author:** Game-design agent. Non-canon names are flagged **(agent-authored — owner to ratify)**.

**Source docs:** `docs/kaykit-asset-catalog.md` (§3 enemies, §6 boss candidates), `docs/v2-unity-port-spec.md` (canon names, Week-8 gate), `docs/avalon-village-layout-spec.md`, `docs/dungeons-3d-unity-layout-spec.md`, `docs/dungeon-3d-healers-cottage-design.md`, `docs/dungeons-storyline.md`, `Assets/StreamingAssets/Data/Canonical/enemies.json`, `Assets/StreamingAssets/Data/Canonical/canon-strings.json`, `Assets/_Modules/Village/Enemies/Enemy.cs`, `Assets/_Modules/BattleATB/Engine/Defs.cs` (`ENEMY_DEFS`).

---

## 0. Canon lock — what this codex must not break

These names and facts are **canon-locked**. The codex designs *around* them; it never renames them.

| Locked item | Source | How the codex treats it |
| --- | --- | --- |
| **Alduin the Mournful** / **the Necromancer** | `canon-strings.json` `alduin` + `alduinTitle`; `dungeons-storyline.md` §Act IV | Designed AS canon — the realm's final antagonist. He **is** every Keeper and healer the Wound consumed; "the Mournful" is a title, the soul is composite. He does **not** end in a boss fight — `At the Edge` ends in a dialogue tree (`dungeons-storyline.md` §4.6). |
| **The Apprentice of the Apothecary** | `dungeon-3d-healers-cottage-design.md` §Beat 6; `Defs.cs` `hollow-apprentice` already in `ENEMY_DEFS` | The canon-locked Healer's Cottage mini-boss. Designed AS canon — stats already exist (`BaseHp 175`, `BaseAttack 24`, special "Tincture"). The codex expands the *encounter*, not the name. |
| **the Hollow Ones** / **the Hollowed** | `canon-strings.json`; `enemies.json` | The undead wave faction. They are **grief that walks** — risen Folk, not monsters. Tone rule: the Keeper mourns them even while ending them. |
| **the Necromancer of the Wound** (`necromancer` boss in `enemies.json`) | `enemies.json` `necromancer`, displayName "Necromancer of the Wound" | The canon village wave-boss (`hp 1700`). "A hand of Alduin." Designed AS canon. |
| **the Withering**, **the Wound** | `canon-strings.json` | The corruption (Withering) and its source (the Wound). All enemy origin lore traces here. |
| **the Mournful Alpha / the First Wolfwarden / the Vault Keeper / the Inn-Keeper / the Watcher** | `dungeons-3d-unity-layout-spec.md` §10 names these dungeon mini-bosses | Treated as **canon-adjacent**: the layout spec already named them. The codex designs their kits and **flags the name for owner ratification** where the spec gave only a title (per task: "where canon is unclear, design the kit and flag the name"). |

Everything else — second-faction enemy names, the new dungeon-lord bosses drawn from the unused Mystery Monthly slate — is **agent-authored** and explicitly marked for owner ratification.

---

## 1. Roster overview

The bestiary splits into **two enemy factions** plus a **set-piece boss tier**.

### 1.1 The Hollow Ones — the undead wave faction (primary)

Risen Folk of the realm, animated by the Withering. They march on Elarion's four gates. Skeleton-based; the catalog's Skeletons 1.1 pack is their entire body of models. They appear in **the village wave loop** and as **dungeon enemies** (the Hollow Ones lurk in every dungeon's dark).

### 1.2 The Wildlands — the living (non-undead) enemy faction (secondary)

The catalog (§3) explicitly recommends a *second, living* faction so the realm is not 100% skeleton. These are corrupted-by-proximity creatures of the lands the Withering bleeds into — orcs, beasts, cavemen. They are **realm-2+ wave content and dungeon-encounter variety**. Drawn entirely from the unused Mystery Monthly slate.

### 1.3 Set-piece bosses

Eight named bosses (§4) — two canon-locked, six agent-authored — built from the ~31 unused Mystery Monthly characters the catalog surfaced.

### Roster table

| # | Enemy | Faction | Tier / Role | Appears in |
| --- | --- | --- | --- | --- |
| 1 | Hollow Walker | Hollow Ones | Fodder / Walker | Village waves 1+; dungeons |
| 2 | Hollow Warrior | Hollow Ones | Standard melee / Walker | Village waves 3+; dungeons |
| 3 | Hollow Rogue | Hollow Ones | Fast flanker / Skirmisher | Village waves 4+; dungeons |
| 4 | Hollow Caster | Hollow Ones | Ranged caster | Village waves 6+; dungeons |
| 5 | Hollow Reaper | Hollow Ones | Elite / scythe-wielder | Village waves 9+; dungeon elites |
| 6 | Hollow Brute (the Bone-Golem) | Hollow Ones | Heavy / Charger | Village mini-boss waves; dungeons |
| 7 | Cellar Hollow | Hollow Ones | Sorrow variant (slow, sad) | Dungeons only (Healer's Cottage cellar etc.) |
| 8 | Orc Raider | Wildlands | Heavy raider / Charger | Realm-2 waves; dungeon brute |
| 9 | Wildlands Caveman | Wildlands | Brute / Walker | Realm-2 waves; cave dungeons |
| 10 | Feral Wolf | Wildlands | Fast pack-hunter / Skirmisher | Cold-biome dungeons; pack encounters |
| 11 | Tiefling Cultist | Wildlands (demonic) | Demonic skirmisher / caster | Deep-dungeon enemy near the Wound |
| — | **Necromancer of the Wound** | Hollow Ones | **Village wave-boss** (canon) | Every 6th village wave |
| — | **The Apprentice of the Apothecary** | Hollow Ones | **Dungeon mini-boss** (canon) | Healer's Cottage, boss room |
| — | The Mournful Alpha | Wildlands | Dungeon mini-boss | Cold-Wandered's Pack (D5) |
| — | The First Wolfwarden | Hollow Ones | Dungeon mini-boss | Wolfwarden's Vigil (D3) |
| — | The Vault Keeper | Hollow Ones | Dungeon mini-boss | Apothecary's Vault (D2) |
| — | The Inn-Keeper | Hollow Ones | Dungeon mini-boss | Folk Who Forgot (D4) |
| — | The Watcher | Hollow Ones | Dungeon mini-boss | Last Keeper's Walk (D6) |
| — | **Alduin the Mournful** | — | **Realm antagonist** (canon) | At the Edge (D7) — dialogue, not a fight |

**Roster size: 11 standard enemies + 8 named bosses/antagonists = 19 designed combat entities.**

A note on scope: the village wave loop today (`enemies.json`) ships **4 entries** (Walker, Warrior, Rogue, Necromancer). This codex is a *forward design* — items 4–11 and the dungeon mini-bosses are the v1.1+/dungeon-expansion roster the owner can pull from. v2-foundation Week 4 only needs 1–3 + the Necromancer; the codex flags that split per entry.

---

## 2. Per-enemy entries

Every entry below maps to a **real KayKit model named in the asset catalog**. Stats are an *anchor* — a starting point for the data-layer; the ATB engine (`Defs.cs ENEMY_DEFS`) and the village wave loop (`enemies.json`) tune the final numbers. Where a number already exists in canon data it is quoted verbatim and marked **(canon data)**.

Two stat contexts exist and must not be confused:
- **Village stats** — `hp / moveSpeed / contactDamage / attackInterval` — the NavMeshAgent wave enemy (`enemies.json`, `Enemy.cs`).
- **ATB stats** — `BaseHp / BaseAttack / Speed / Defense / Element / Special` — the turn-based combatant (`Defs.cs ENEMY_DEFS`). When a village enemy breaches, `Enemy.EngineDefId` maps it to an ATB def.

### Animation-set legend

Every rigged enemy needs the same baseline. The legend is used in every entry so the gap is obvious:

- **Idle** — standing loop.
- **Move** — walk and/or run locomotion.
- **Attack** — one or more melee/ranged strikes.
- **Hit-react** — flinch on taking damage.
- **Death** — collapse / dissolve.
- **Special** — archetype- or boss-unique clip (cast, summon, transform, etc.).

KayKit's **Character Animations 1.1** pack ships shared clip sets on the `Rig_Medium` / `Rig_Large` skeleton — `General`, `MovementBasic`, `MovementAdvanced`, `CombatMelee`, `CombatRanged`, `Special`, `Simulation`, `Tools`. Every KayKit humanoid (Skeletons, Adventurers, Mystery Monthly characters) shares that rig, so **idle / move / melee / ranged / hit-react / death are covered for the whole roster for free**. The gaps are in *bespoke special clips* — §5 collects them.

---

### 2.1 Hollow Walker — `Skeleton_Minion`

- **KayKit model:** `Skeleton_Minion.fbx` — `KayKit Skeletons 1.1/characters/fbx(unity)/Skeleton_Minion.fbx`. Live `.glb`: `Assets/Models/KayKit/enemies/Skeleton_Minion.glb`.
- **Visual concept:** Small, bare, fragile skeleton. No armor, maybe a single rusted weapon (assign `Skeleton_Blade` or none). The basic Hollow One — a risen villager with nothing left but the march. Reads as *pitiable*, not scary.
- **Combat role:** Fodder. Spawns in swarms. The first enemy the player ever fights (Wave 1 = 8 Walkers, per port spec).
- **Stats anchor — village (canon data):** `hp 52`, `moveSpeed 2.5`, `contactDamage 6`, `attackInterval 1.3`, `height 1.7`, `ai walker`.
- **Stats anchor — ATB:** maps to `ENEMY_DEFS["skeleton"]` (`BaseHp 70`, `BaseAttack 16`, `Speed 0.95`, `Defense 0.1`, Physical; Special "Bone Shard", 18 dmg + Bleed). **(canon data)**
- **Animation set:** Idle (slow, listless), Move (shamble walk), Attack ×1 (clumsy swing), Hit-react, Death (clatter-collapse). **Covered by the shared rig** — `General` + `MovementBasic` + `CombatMelee`. **No missing clip.**

### 2.2 Hollow Warrior — `Skeleton_Warrior`

- **KayKit model:** `Skeleton_Warrior.fbx` — `KayKit Skeletons 1.1/characters/fbx(unity)/`. Live `.glb`: `enemies/Skeleton_Warrior.glb`.
- **Visual concept:** Armored standard skeleton, helmet, shield optional. Heavier bone, slower stride. A wall of the dead. Assign `Skeleton_Blade` or `Skeleton_Axe` + `Skeleton_Shield_Small_A`.
- **Combat role:** Standard mid-wave melee. The reliable backbone enemy.
- **Stats anchor — village (canon data):** `hp 156`, `moveSpeed 2.2`, `contactDamage 6`, `attackInterval 1.3`, `height 1.88`, `ai walker`.
- **Stats anchor — ATB:** maps to `ENEMY_DEFS["skeleton"]` (shares the skeleton def). A future dedicated `"hollow-warrior"` def could read `BaseHp 110, BaseAttack 22, Defense 0.18`.
- **Animation set:** Idle, Move (weighted march), Attack ×2 (overhead + shield-bash if shielded), Hit-react, Death. **Covered by the shared rig** — `CombatMelee` has a shield set. **No missing clip.**

### 2.3 Hollow Rogue — `Skeleton_Rogue`

- **KayKit model:** `Skeleton_Rogue.fbx` — `KayKit Skeletons 1.1/characters/fbx(unity)/`. Live `.glb`: `enemies/Skeleton_Rogue.glb`.
- **Visual concept:** Hooded, lean, fast — low to the ground. Assign `Skeleton_Dagger` ×2 or `Skeleton_Crossbow`. The catalog notes the hooded silhouette is also the closest stand-in for an "aether bat" — keep it spry.
- **Combat role:** Fast flanker / skirmisher. Slips the line, picks the weakest ward (`ai skirmisher` — `Enemy.cs ProbeForStructure` already widens the skirmisher probe radius to peel toward walls).
- **Stats anchor — village (canon data):** `hp 88`, `moveSpeed 3.1`, `contactDamage 6`, `attackInterval 1.3`, `height 1.78`, `ai skirmisher`.
- **Stats anchor — ATB:** maps to `ENEMY_DEFS["skeleton"]`. A dedicated def would emphasize Speed (`Speed 1.2`) and low HP (`BaseHp 55`).
- **Animation set:** Idle (twitchy crouch), Move (fast run — `MovementAdvanced` run clip), Attack ×2 (twin quick slashes; or a crossbow ranged clip if armed), Hit-react, Death. **Covered by the shared rig** (`CombatRanged` covers the crossbow variant). **No missing clip.**

### 2.4 Hollow Caster — `Skeleton_Mage`

- **KayKit model:** `Skeleton_Mage.fbx` — `KayKit Skeletons 1.1/characters/fbx(unity)/`. Live `.glb`: `enemies/Skeleton_Mage.glb`.
- **Visual concept:** Robed, staff-wielding skeleton (`Skeleton_Staff`). A withered echo of a hedge-witch. Tint the staff-tip emissive a sick violet to read "Withering magic."
- **Combat role:** Ranged caster. Hangs back, hits towers / the Heart at distance. In the village this is a *ranged walker* — it stops out of melee range and casts; in ATB it is a caster archetype.
- **Stats anchor — village:** `hp 70`, `moveSpeed 2.0`, `contactDamage 9` (ranged bolt), `attackInterval 1.8`, `height 1.82`, `ai walker` (ranged stop variant — flag a new `caster` AI mode for `enemy-roles.json`). **(agent-authored stats — not yet in `enemies.json`)**
- **Stats anchor — ATB:** new def `"hollow-caster"` — `Archetype Caster`, `BaseHp 75`, `BaseAttack 18`, `Speed 1.05`, `Defense 0.08`, `Aether`; Special "Withering Hex" — 14 dmg AoE + Poison (mirrors the existing `necromancer` ATB def's Hex). **(agent-authored — owner to ratify the def)**
- **Animation set:** Idle, Move, **Attack — ranged cast** (point-staff projectile), Hit-react, Death, **Special — channel cast** (held two-handed staff channel for the Hex). The `Special` clip set in Character Animations 1.1 includes cast/channel-style clips — **covered**. **Note:** if the cast clip doesn't read clearly as "throwing a bolt," it is a *minor* gap — see §5.

### 2.5 Hollow Reaper — `Skeleton_Warrior` (scythe variant)

- **KayKit model:** `Skeleton_Warrior.fbx` re-skinned dark, wielding `Skeleton_Scythe` — `KayKit Skeletons 1.1/assets/fbx(unity)/Skeleton_Scythe.fbx`. The catalog explicitly flags the scythe as reading "elite / reaper."
- **Visual concept:** A taller, dark-tinted Warrior with the scythe — the Hollow Ones' executioner. The catalog suggests a per-spawn weapon-randomizer; the Reaper is the *deliberate* scythe assignment that signals "this one is dangerous."
- **Combat role:** Elite. Appears mid-to-late waves and as a dungeon elite. Higher HP, wide melee arc, a small fear/slow aura.
- **Stats anchor — village:** `hp 240`, `moveSpeed 2.3`, `contactDamage 14`, `attackInterval 1.5`, `height 2.0`, `ai walker`. **(agent-authored stats)**
- **Stats anchor — ATB:** new def `"hollow-reaper"` — `Archetype Tank`, `BaseHp 200`, `BaseAttack 28`, `Speed 0.9`, `Defense 0.2`, `Physical`; Special "Reaping Arc" — 30 dmg to all allies + Bleed. **(agent-authored — owner to ratify)**
- **Animation set:** Idle, Move, Attack ×2 (wide scythe sweep — needs a clip that arcs the scythe; `CombatMelee` two-handed set covers a generic sweep), Hit-react, Death, **Special — sweeping reap** (the AoE telegraph). The sweep is close enough to a generic 2H attack that the shared rig **covers it** — no bespoke clip *required*, but a dedicated wide-arc clip would polish it (minor gap, §5).

### 2.6 Hollow Brute (the Bone-Golem) — `Skeleton_Golem`

- **KayKit model:** `Skeleton_Golem.fbx` — `KayKit Skeletons 1.1/characters/fbx(unity)/Skeleton_Golem.fbx` (full pack — promote from the warehouse; the catalog notes the `.glb` is not in the live set). Weapon: `Skeleton_Golem_Axe_Large`.
- **Visual concept:** A large bone-construct — many skeletons fused, an oversized axe. The catalog's "brute" pick. Scale ~2.0–2.2×. Red-violet emissive in the joint-gaps to read "wrong."
- **Combat role:** Heavy / Charger. Slow, bulky, very high HP; ignores small structures and charges the Heart. A *wave mini-boss* — leads non-Necromancer escalation waves.
- **Stats anchor — village:** `hp 900`, `moveSpeed 1.6`, `contactDamage 24`, `attackInterval 1.8`, `height 3.0`, `ai charger`. **(agent-authored stats)**
- **Stats anchor — ATB:** maps to `ENEMY_DEFS["bruiser"]` (`BaseHp 140`, `BaseAttack 20`, `Speed 0.75`, `Defense 0.3`, Special "Patch Up" self-heal 40) **(canon data)** — or a dedicated `"hollow-brute"` def at `BaseHp 320, BaseAttack 30`.
- **Animation set:** Idle (heavy sway), Move (lumbering walk), Attack ×2 (overhead axe slam, ground-pound), Hit-react (barely flinches — a short stagger), Death (topple + bone-scatter), **Special — ground slam** (AoE shock). Uses the **`Rig_Large` skeleton** — the catalog confirms KayKit ships a `Rig_Large` clip set, so the Golem animates from the *large* shared controller. **Covered**, with the ground-slam landing close to a generic large-rig heavy attack. Minor polish gap (§5).

### 2.7 Cellar Hollow — `Skeleton_Minion` (sorrow variant)

- **KayKit model:** `Skeleton_Minion.fbx` — same model as the Walker, **different animator state**.
- **Visual concept:** Identical body to the Walker, but staged differently — it *kneels and rocks* rather than wanders (per `dungeon-3d-healers-cottage-design.md` §Beat 4: "kneels and rocks rather than wanders. Sad."). A villager who fled to the cellar long ago and was taken by the Withering where it hid.
- **Combat role:** Dungeon-only sorrow encounter. Slow, weak — a *deliberate de-escalation* of menace. The bible voice: "they are grief, not menace."
- **Stats anchor — ATB:** maps to `ENEMY_DEFS["skeleton"]` with a downward tune — `BaseHp 45`, `BaseAttack 10`, `Speed 0.8`. **(agent-authored tune)**
- **Animation set:** **Special — kneeling sorrow idle** (the kneel-and-rock loop), then Idle / Move / Attack / Hit-react / Death from the shared rig. **The kneel-and-rock idle is a real gap** — KayKit's `General` set has sit/idle variants but not a grief-rock. **This is the single most emotionally important missing clip** and the cheapest to commission. See §5 GAP-1.

### 2.8 Orc Raider — `OrcRaider`

- **KayKit model:** `OrcRaider.fbx` — `KayKit Mystery Monthly Series 4/1 - July 2023 - Orc Raider/character/OrcRaider.fbx`.
- **Visual concept:** Hulking green orc, axe or club, a war-drum prop. The catalog's anchor for the *living, non-undead* faction — a raider warband drawn toward the realm by the Withering's spread.
- **Combat role:** Heavy raider / Charger. The Wildlands faction's standard heavy. A great realm-2 wave enemy and dungeon brute.
- **Stats anchor — village:** `hp 320`, `moveSpeed 2.4`, `contactDamage 16`, `attackInterval 1.4`, `height 2.1`, `ai charger`. **(agent-authored stats)**
- **Stats anchor — ATB:** new def `"orc-raider"` — `Archetype Tank`, `BaseHp 160`, `BaseAttack 24`, `Speed 0.85`, `Defense 0.2`, `Physical`; Special "War-Drum" — buffs all allied Hastes (mirror `Haste` blueprint) — a *support* twist on a brute. **(agent-authored — owner to ratify)**
- **Animation set:** Idle, Move, Attack ×2, Hit-react, Death, **Special — war-drum beat** (a chest-thump / drum-strike rally). The drum is a flavor prop; the rally pose maps onto a `General` taunt/cheer clip. **Covered** by the shared rig (uses `Rig_Medium` or `Rig_Large` per the orc's bulk). Minor polish gap for the drum-specific clip (§5).

### 2.9 Wildlands Caveman — `Caveman`

- **KayKit model:** `Caveman.fbx` — `KayKit Mystery Monthly Series 5/8 - February 2025 - Caveman/characters/Caveman.fbx`. Ships with club / spear / axe.
- **Visual concept:** Primitive brute, fur, stone weapon. A non-undead wildlands creature — the deep-cave inhabitant who never left when the Withering rose.
- **Combat role:** Brute / Walker. A simpler, cheaper Wildlands body than the Orc — good swarm-with-weight for cave dungeons (Cold-Wandered's Pack approach, generic deep-dungeon).
- **Stats anchor — village/dungeon:** `hp 180`, `moveSpeed 2.6`, `contactDamage 11`, `attackInterval 1.3`, `height 1.9`, `ai walker`. **(agent-authored stats)**
- **Stats anchor — ATB:** new def `"caveman"` — `Archetype Grunt`, `BaseHp 95`, `BaseAttack 19`, `Speed 1.0`, `Defense 0.1`, `Physical`; Special "Reckless Swing" (reuse the `goblin` def's special verbatim — 22 dmg). **(agent-authored — owner to ratify)**
- **Animation set:** Idle, Move, Attack ×2, Hit-react, Death. **Covered by the shared rig.** **No missing clip.**

### 2.10 Feral Wolf — `Werewolf_Wolf`

- **KayKit model:** `Werewolf_Wolf.fbx` — `KayKit Mystery Monthly Series 4/4 - October 2023 - Werewolf/characters/fbx/Werewolf_Wolf.fbx`. The catalog confirms this is the collection's **only true quadruped beast**.
- **Visual concept:** A full feral wolf-beast. Used as a *cold-spirit gone savage* — a pack-hunter haunting the Wintermere dungeons (Cold-Wandered's Pack, Wolfwarden's Vigil). Lighter, frost-blue tint distinguishes it from the Mournful Alpha boss.
- **Combat role:** Fast pack-hunter / Skirmisher. Spawns in packs of 3–4; circles and lunges.
- **Stats anchor — dungeon:** `hp 95`, `moveSpeed 3.6`, `contactDamage 12`, `attackInterval 1.0`, `height 1.1`, `ai skirmisher`. **(agent-authored stats)**
- **Stats anchor — ATB:** new def `"feral-wolf"` — `Archetype Grunt`, `BaseHp 60`, `BaseAttack 20`, `Speed 1.3` (fast ATB fill), `Defense 0.05`, `Ice`; Special "Pack Lunge" — single-target 26 dmg, +damage per other living wolf in the fight (pack synergy). **(agent-authored — owner to ratify)**
- **Animation set:** Idle, Move (run + prowl), **Attack — lunge bite** (quadruped pounce), Hit-react, Death, **Special — howl** (pack-rally; doubles as a telegraph).
  **⚠ MAJOR GAP.** `Werewolf_Wolf` is a **quadruped** — it does **NOT** share the `Rig_Medium` / `Rig_Large` humanoid skeleton. None of the Character Animations 1.1 clips retarget to it. The Werewolf pack ships its *own* clips; they must be audited and, where short, the bite / lunge / howl commissioned on the wolf's own rig. **This is the largest structural animation gap in the roster — see §5 GAP-PRIMARY.**

### 2.11 Tiefling Cultist — `Tiefling`

- **KayKit model:** `Tiefling.fbx` — `KayKit Mystery Monthly Series 5/12 - June 2025 - Tiefling/characters/Tiefling.fbx`. Ships with `Tiefling_Sword` + back-scabbard.
- **Visual concept:** Horned demon-kin, twin swords. The catalog calls it "strongly magical." Used as a *cultist of the Wound* — a living thing that walked toward the Wound and chose it, the inverse of the Hollow Ones who were taken without choosing.
- **Combat role:** Demonic skirmisher / off-caster. A deep-dungeon enemy in the Wound-adjacent zones (Hollowmouth Antechamber, At the Edge approach). Fast, hits hard, applies Burn.
- **Stats anchor — dungeon:** `hp 140`, `moveSpeed 3.0`, `contactDamage 15`, `attackInterval 1.1`, `height 1.95`, `ai skirmisher`. **(agent-authored stats)**
- **Stats anchor — ATB:** new def `"tiefling-cultist"` — `Archetype Caster`, `BaseHp 105`, `BaseAttack 24`, `Speed 1.15`, `Defense 0.1`, `Flame`; Special "Wound-Brand" — single-target 30 dmg + Burn + Mark. **(agent-authored — owner to ratify)**
- **Animation set:** Idle, Move, Attack ×2 (dual-sword combo), Hit-react, Death, **Special — brand cast** (a one-handed sweep that throws a fire-mark). **Covered by the shared rig** — Tiefling is a standard humanoid on `Rig_Medium`; `CombatMelee` dual-wield + `Special` cast cover it. **No missing clip.**

---

## 3. Faction & biome map

How the roster distributes across the realm's content.

| Biome / scene | Faction present | Enemies | Boss |
| --- | --- | --- | --- |
| **Elarion village — wave loop** | Hollow Ones (realm 1); + Wildlands (realm 2+) | Walker, Warrior, Rogue, Caster, Reaper, Brute; Orc Raider, Caveman (realm 2+) | Necromancer of the Wound (every 6th wave); Hollow Brute (escalation waves) |
| **Healer's Cottage (D1)** — cozy-domestic | Hollow Ones | Walker, Cellar Hollow | **The Apprentice of the Apothecary** (canon) |
| **Apothecary's Vault (D2)** — apothecary-workshop | Hollow Ones | Walker, Warrior, Caster | The Vault Keeper |
| **Wolfwarden's Vigil (D3)** — stone-fortress | Hollow Ones; Wildlands (wolves) | Warrior, Reaper, Feral Wolf | The First Wolfwarden |
| **Folk Who Forgot (D4)** — ruined-village | Hollow Ones | Walker, Warrior, Rogue, Cellar Hollow | The Inn-Keeper |
| **Cold-Wandered's Pack (D5)** — cave-natural | Wildlands (wolves); Hollow Ones | Feral Wolf, Caveman | The Mournful Alpha |
| **Last Keeper's Walk (D6)** — stone-fortress / ruined | Hollow Ones | Warrior, Reaper, Caster | The Watcher |
| **At the Edge (D7)** — cosmic-void | Hollow Ones; Wildlands (Tieflings) | Reaper, Tiefling Cultist (breach-encounters only) | **Alduin the Mournful** (canon — dialogue, no fight) |

**Graveyard origin-zone:** the catalog (§Halloween Bits) reframes Halloween Bits as "the corrupted lands the Hollow Ones come from." When a graveyard biome or a Hollowmouth-origin dungeon ships, it draws the full Hollow Ones roster — no new models needed.

---

## 4. Boss design — the slate

Eight named encounters. **Two are canon-locked** (Necromancer of the Wound; the Apprentice of the Apothecary) and designed AS canon. **Five dungeon mini-bosses** were *named by* `dungeons-3d-unity-layout-spec.md §10` — the codex designs their kits and ratifies/flags the names. **Alduin the Mournful** is the realm antagonist — canon-locked, and canonically **not a boss fight**.

Every boss is built from a KayKit model named in the catalog — the catalog's §6 "boss & set-piece candidates" table is the source for the Mystery Monthly picks.

### Boss-encounter design vocabulary

- **Phases** — HP-threshold gates that change the boss's behavior.
- **Signature mechanic** — the one thing the player must learn to win.
- **Telegraph** — the visible/audible warning before a signature attack. The cozy register demands telegraphs be *readable* — these are mourning stories, not twitch tests.
- All boss fights run in the **ATB engine** (`Scenes/ATBBattle.unity`) — turn-based. "Phases" are HP-gated AI behavior switches, not real-time mechanics. Telegraphs are the ATB log line + a charge-up animation on the boss's turn before the special resolves.

---

### 4.1 BOSS — Necromancer of the Wound (CANON)

- **Identity:** The canon village wave-boss. `enemies.json` id `necromancer`, displayName **"Necromancer of the Wound"**, flavor: *"A hand of Alduin the Mournful. Where it walks, the Hollowed rise to walk beside it."* **Name canon-locked — do not rename.**
- **KayKit model:** `Necromancer.fbx` — `KayKit Skeletons 1.1/characters/fbx(unity)/Necromancer.fbx`. Live `.glb`: `enemies/Necromancer.glb`. Scale ~1.8× per catalog §6. A hooded, non-skeleton leader with a staff.
- **Visual concept:** A robed figure, face lost in shadow under the hood — *is it even a skeleton underneath?* Staff topped with a violet Withering-crystal (re-tint a `Resource Bits` gem, emissive). Where it stands, the ground should ghost faint violet runes.
- **Where it appears:** Leads **every 6th village wave** (`BOSS_EVERY = 6`, `BattleScaling`). The Week-8 acceptance gate's boss-wave content.
- **Stats anchor:** Village (canon data): `hp 1700`, `moveSpeed 1.5`, `contactDamage 17`, `attackInterval 1.3`, `height 2.7`, `boss true`. ATB (canon data): `ENEMY_DEFS["necromancer"]` — `Archetype Caster`, `BaseHp 85`, `BaseAttack 18`, `Speed 1.05`, `Defense 0.1`, `Aether`; Special "Hex" — 14 dmg AoE + Poison.
- **Encounter design:**
  - **Phase 1 (100–60% HP) — The March.** Standard caster attacks + the "Hex" special on cooldown. Establishes the threat.
  - **Phase 2 (60–25% HP) — The Raising.** Signature mechanic: on its turn, instead of attacking, the Necromancer **summons 1–2 Hollow Walkers** into the fight (the village data already notes the boss "can summon the minions" — `kaykit-asset-catalog.md` §6). In ATB terms: adds Walker combatants to the enemy side up to `MAX_ENEMIES = 8`. Forces the player to choose between clearing adds and pressuring the boss.
  - **Phase 3 (25–0% HP) — The Wound's Voice.** Hex fires more often; a new desperate special "Withering Surge" — heavy AoE Aether + Slow. The boss is *grieving louder*, not getting angrier.
  - **Telegraphs:** the summon is telegraphed by a two-handed staff-raise channel animation + the ATB log line *"The Necromancer lifts the dark."* Withering Surge telegraphs with a full-body crouch-charge.
- **Animation needs:** Idle, Move, Attack (ranged cast), Hit-react, Death — **shared rig, covered**. **Special clips needed: (a) Summon — staff-raise channel**, **(b) Withering Surge — crouch-charge release.** KayKit's `Special` clip set has cast/channel clips that **substitute adequately** for both — covered, but the Summon especially would benefit from a bespoke "raise the dead" gesture (§5, GAP-2, low priority — substitute works).

### 4.2 BOSS — The Apprentice of the Apothecary (CANON)

- **Identity:** The **canon-locked Healer's Cottage mini-boss.** Already in code: `Defs.cs ENEMY_DEFS["hollow-apprentice"]`, Name **"The Apprentice of the Apothecary"**. Lore (`dungeon-3d-healers-cottage-design.md` §Beat 6): *"a stronger Hollow One, an apprentice Alduin took in years ago and never spoke of."* **Name canon-locked — do not rename.**
- **KayKit model:** A **Hollow One body** — use `Skeleton_Mage.fbx` (robed, fits "apothecary's apprentice") or `Skeleton_Warrior` re-skinned, scaled ~1.2×. *Not* a Mystery Monthly model — this boss is one of the Hollow Ones, canonically. Give it an apron-tint and a `bottle`/`vial` held prop from `Dungeon Remastered` or the Witch's `Mortar`.
- **Visual concept:** A skeleton in a stained healer's apron, a glass vial clutched in one hand. The tragedy: it still performs the *motions* of healing — it doesn't know it's dead. It fights in Alduin's old apothecary among the bubbling glass.
- **Where it appears:** Healer's Cottage (D1), the Apothecary boss room (Beat 6 / Workshop in the expanded layout). The first boss a player ever fights.
- **Stats anchor (canon data):** `ENEMY_DEFS["hollow-apprentice"]` — `Archetype Boss`, `BaseHp 175`, `BaseAttack 24`, `Speed 1.0`, `Defense 0.12`, `Aether`; Special **"Tincture"** — 0 dmg, `SingleAlly`, applies `Slow` at 100% chance. The Healer's Cottage design doc specifies the encounter target: *"2.5× normal Hollow HP, +50% damage,"* and the Tincture *"briefly blinds the Keeper — shrinks light radius by 50% for 6 seconds."*
- **Encounter design:**
  - **Phase 1 (100–50% HP) — The Work.** Standard attacks + "Tincture" on cooldown — applies Slow (the ATB expression of the blind). Winnable on the default starting loadout (1 hero + 1 rank-1 pet) per the dungeon acceptance criteria — keep it gentle; this is a teaching fight.
  - **Phase 2 (50–0% HP) — The Spill.** A second special "Caustic Spill" — moderate Aether AoE + Poison, lore-framed as a beaker shattering. The Apprentice is *more frantic*, not more cruel.
  - **Telegraphs:** Tincture telegraphs with a "drinking / throwing a vial" gesture + log line *"The Apprentice mixes something."* Caustic Spill telegraphs with a vial-raise.
  - **Lantern tie-in:** the boss room is "deliberately dim even with maximum lantern coverage" (design doc) — and the Tincture shrinking light radius is the canon mechanic. The ATB expression (Slow) keeps it engine-clean while the dungeon scene shows the literal light-shrink.
- **Animation needs:** Idle, Move, Attack, Hit-react, Death — **shared rig (Hollow One body), covered**. **Special clip needed: a "mix / drink / throw a vial" gesture.** KayKit's `Tools` clip set has item-use / drinking-style clips that **substitute well** for the Tincture. Caustic Spill reuses the same vial gesture. **Covered with a substitute** — a bespoke "frantic apothecary" clip is a nice-to-have (§5, low priority).

### 4.3 BOSS — The Vault Keeper *(name from `dungeons-3d-unity-layout-spec.md §10.1` — owner to ratify)*

- **Identity:** Mini-boss of the **Apothecary's Vault (D2)**. The layout spec names "The Vault Keeper" but gives no character; the codex designs the kit. **Name treated as canon-adjacent — flag for owner ratification.** Lore lean: a Hollow One who was the Vault's archivist-warden, still locking doors that have no other side.
- **KayKit model:** **Black Knight** — `BlackKnight.fbx` — `KayKit Mystery Monthly Series 5/3 - September 2024 - Black Knight/characters/BlackKnight.fbx`, with `BlackKnight_Sword_Large` + `BlackKnight_Shield_Large`. The catalog (§3, §6) explicitly types the Black Knight as a "Gate Warden / elite boss — a fallen champion guarding a dungeon." A perfect Vault Keeper.
- **Visual concept:** A towering dark-armored knight, helmet sealed, a vault-key ring hanging at the belt (`RPG Tools` `key_*` props). It guards the deep vault out of a duty that outlived its mind.
- **Where it appears:** Apothecary's Vault (D2), boss room — the deep vault.
- **Stats anchor (ATB):** new def `"vault-keeper"` — `Archetype Boss`, `BaseHp 360`, `BaseAttack 30`, `Speed 0.9`, `Defense 0.32` (heavy armor), `Physical`; Special "Sealing Strike" — 36 dmg single-target + applies `Stun`. **(agent-authored — owner to ratify)**
- **Encounter design:**
  - **Phase 1 (100–55% HP) — The Watch.** Shield up. High Defense. "Sealing Strike" punishes the player's slowest unit with Stun. Teaches the player to break the shield.
  - **Phase 2 (55–0% HP) — The Breach.** The Keeper drops its shield to attack faster — Defense falls to 0.18, Speed rises. Signature mechanic: a 2-turn-telegraphed **"Vault Slam"** — a wind-up the player can interrupt with a Stun/Freeze; if it lands, heavy AoE Physical.
  - **Telegraphs:** Vault Slam telegraphs over a full turn — the Knight raises the great sword overhead and the log reads *"The Vault Keeper raises the sealing blade — strike now."* Clear interrupt window — cozy-register fair.
- **Animation needs:** Idle, Move, Attack ×2 (sword + shield-bash), Hit-react, Death — **Black Knight is a standard humanoid on `Rig_Large`, shared rig covers all**. **Special — a big overhead two-handed wind-up + slam.** The `CombatMelee` large-rig set has heavy two-handed attacks that **cover** the Vault Slam. **No bespoke clip required.**

### 4.4 BOSS — The First Wolfwarden *(name from `dungeons-3d-unity-layout-spec.md §10.2` — owner to ratify)*

- **Identity:** Mini-boss of the **Wolfwarden's Vigil (D3)**. The layout spec names "The First Wolfwarden." Lore (`dungeons-storyline.md` Act II): the Wolfwarden's Vigil is a watchtower; the First Wolfwarden was the warden who held the tower and walked toward the Wound and *never came back as himself.* A Hollow One — a fallen guardian, the tragic mirror of the player's own role. **Name treated as canon-adjacent — flag for owner ratification.**
- **KayKit model:** **Werewolf (Man form)** — `Werewolf_Man.fbx` — `KayKit Mystery Monthly Series 4/4 - October 2023 - Werewolf/characters/fbx/Werewolf_Man.fbx`. The catalog explicitly flags the Man-form as "a mini-boss with a transform beat: starts as Man, shifts to Wolf at half HP." The Wolfwarden bonded too closely with the wolves he guarded; the Withering twisted the bond into a curse.
- **Visual concept:** Phase 1 — a haggard human warden in a fur cloak, a watch-banner on his back, holding a spear/`axe`. Phase 2 — the **`Werewolf_Wolf` model** — the bond consumes him.
- **Where it appears:** Wolfwarden's Vigil (D3), the belfry/roof boss room. The layout spec gives the dungeon a "bell mechanic" — ringing the bell can call this boss to fight on the roof.
- **Stats anchor (ATB):** new def `"first-wolfwarden"` — `Archetype Boss`, `BaseHp 300` (Phase 1) → re-statting on transform, `BaseAttack 26`, `Speed 1.0`, `Defense 0.15`, `Physical`. Phase 2 (Wolf): `Speed 1.35`, `BaseAttack 32`, `Defense 0.08`. Special (Phase 1) "Warden's Call" — summons 2 Feral Wolves. Special (Phase 2) "Savage Lunge" — 38 dmg single-target + Bleed. **(agent-authored — owner to ratify)**
- **Encounter design:**
  - **Phase 1 (100–50% HP) — The Warden.** Fights as a man. "Warden's Call" summons Feral Wolves — the player fights the master through his pack.
  - **Transition (at 50% HP) — The Turning.** A scripted **transform beat** — the Man model swaps to the Wolf model. The dungeon design's whole reason for picking this Mystery Monthly pack. ATB-side: the combatant's def/stats hot-swap; the model crossfades.
  - **Phase 2 (50–0% HP) — The Wolf.** Faster ATB fill, higher attack, lower defense. "Savage Lunge." A frantic, sorrowful close.
  - **Telegraphs:** the transform is *the* telegraph — a full-turn howl-and-contort with the log line *"Something in him is breaking loose."* Savage Lunge telegraphs with a crouch.
- **Animation needs:** **Phase 1 (Man) — shared rig, fully covered** (`Werewolf_Man` is a standard humanoid). **The transform clip is a real gap** — there is no man→wolf morph in the collection; the practical solution is a *model swap* hidden behind a VFX burst (particle puff + light flash) rather than a true morph, so the gap is *designable-around*. **Phase 2 (Wolf) — inherits the `Werewolf_Wolf` quadruped-rig gap** (§5 GAP-PRIMARY): bite / lunge / howl on the wolf's own rig. See §5 GAP-3.

### 4.5 BOSS — The Inn-Keeper *(name from `dungeons-3d-unity-layout-spec.md §10.3` — owner to ratify)*

- **Identity:** Mini-boss of the **Folk Who Forgot (D4)**. The layout spec names "The Inn-Keeper" and flags the register: *"tragic register."* Lore: the keeper of the inn in the drowned village of Old Elarion, still setting tables for guests who will never arrive (`dungeons-storyline.md` Act II — the Bell-Tower Hollow Ones are "the villagers of Old Elarion"). **Name treated as canon-adjacent — flag for owner ratification.** This is the codex's most *gentle* boss — barely a monster at all.
- **KayKit model:** A **Hollow One** body — `Skeleton_Warrior` re-skinned in an innkeeper's apron, or the `Skeleton_Mage` robe. Held prop: a `mug` / `tankard` (`Adventurers` mugs, or `Dungeon Remastered` bar set). *Not* a Mystery Monthly model — the villagers of Old Elarion are Hollow Ones.
- **Visual concept:** A skeleton in an apron carrying a tray. It fights almost *apologetically*. The whole encounter is staged in the ruined inn, the bar set dressed with cobwebbed mugs.
- **Where it appears:** Folk Who Forgot (D4), the inn boss room — near the village well that is the corruption's source.
- **Stats anchor (ATB):** new def `"inn-keeper"` — `Archetype Boss`, `BaseHp 280`, `BaseAttack 20` (deliberately low — tragic, not threatening), `Speed 1.0`, `Defense 0.12`, `Aether`; Special "Last Call" — summons 2 Cellar Hollows (the inn's lost patrons) + a small all-ally Regen on itself. **(agent-authored — owner to ratify)**
- **Encounter design:**
  - **Phase 1 (100–40% HP) — Setting the Table.** Low damage. "Last Call" summons Cellar Hollows — *patrons* — who also fight gently. The fight feels like interrupting a memory.
  - **Phase 2 (40–0% HP) — Closing Time.** No rage phase. The Inn-Keeper *slows down* — its ATB Speed drops; its attacks weaken. It is the only boss that gets *easier* in its final phase, by design. The tragedy lands harder than a difficulty spike would.
  - **Telegraphs:** Last Call telegraphs with a "ringing a hand-bell / calling out" gesture + log line *"The Inn-Keeper calls last orders."*
- **Animation needs:** Idle, Move, Attack, Hit-react, Death — **shared rig (Hollow One body), covered**. **Special — a "calling out / ringing a bell / carrying a tray" gesture.** `General` (wave/cheer/call) + `Tools` (item-carry) clips **substitute well**. **Covered with substitutes** — a bespoke "weary innkeeper" idle would deepen it (§5, low priority).

### 4.6 BOSS — The Mournful Alpha *(name from `dungeons-3d-unity-layout-spec.md §10.4` — owner to ratify)*

- **Identity:** Mini-boss of the **Cold-Wandered's Pack (D5)**. The layout spec names "The Mournful Alpha" and notes it is *"not hostile until the final encounter."* Lore (`dungeons-storyline.md` Act II): the Ice Wolf is "the last of a pack of frost-spirits who climbed down from Wintermere to seal the Wound." The Mournful Alpha is the **old alpha of that pack** — the Ice Wolf companion's lost parent/leader. The fight is an act of grief, not aggression. **Name treated as canon-adjacent — flag for owner ratification.** ("Mournful" echoes "Alduin the Mournful" — flag this to the owner; it may be deliberate thematic rhyme or an accidental near-collision. The codex keeps the spec's name and surfaces the concern.)
- **KayKit model:** **Frost Golem** — `FrostGolem.fbx` — `KayKit Mystery Monthly Series 5/7 - January 2025 - FrostGolem/characters/FrostGolem.fbx`, with `FrostGolem_Axe_Large`. The catalog (§3, §6) types the Frost Golem as the "ice-biome boss." Design read: the old alpha frost-spirit, having held its post against the Withering for so long, has *frozen into* a golem-form — the last stand made literal. (Alternative: `Werewolf_Wolf` if the owner wants the Alpha to read as a true wolf — but the Frost Golem's set-piece scale and the ice theme make the stronger boss; the codex recommends Frost Golem and flags the choice.)
- **Visual concept:** A hulking figure of black ice, the suggestion of a wolf's silhouette frozen inside the glacier-body. It does not move like a beast — it moves like a mountain that remembers being a beast.
- **Where it appears:** Cold-Wandered's Pack (D5), the deepest cave — at "the Old Alpha's grave" (the layout spec's named vertical landmark for D5).
- **Stats anchor (ATB):** new def `"mournful-alpha"` — `Archetype Boss`, `BaseHp 400`, `BaseAttack 28`, `Speed 0.8`, `Defense 0.3`, `Ice`; Special "Glacier's Grief" — heavy AoE Ice + Freeze (50% chance). **(agent-authored — owner to ratify)**
- **Encounter design:**
  - **Phase 1 (100–60% HP) — The Sleeping Cold.** Slow, immense, defensive. "Glacier's Grief" punishes a clustered party with AoE + Freeze. Teaches the player to spread their actions.
  - **Phase 2 (60–25% HP) — The Waking.** The ice cracks (visible damage decals). Speed rises; a new special "Frostbound Howl" — applies Slow to all allies and the Alpha gains a Shield. The cornered-animal phase.
  - **Phase 3 (25–0% HP) — The Letting Go.** The Alpha stops defending. Defense drops to near-zero; it simply attacks, no specials — it is *done holding the line*. Mirrors the Inn-Keeper's "gets gentler" beat but kept as a damage race so it still reads as a climax. The Ice Wolf pet, if present, gets a unique post-fight ambient line (the storyline doc already hooks Ice Wolf behavior to D5).
  - **Telegraphs:** Glacier's Grief telegraphs with a slow arms-raise + the ground frosting over; Frostbound Howl with a head-back howl pose.
- **Animation needs:** Idle, Move, Attack ×2 (axe slam, ice-fist sweep), Hit-react (a heavy stagger), Death (shatter-collapse) — **Frost Golem is a large humanoid construct on `Rig_Large`, shared rig covers all**. **Special — arms-raise channel (Grief) + howl pose (Howl).** `Special` + `General` clip sets **cover** both. **No bespoke clip strictly required**; a bespoke "ice shatter" death would polish the set-piece (§5, low priority).

### 4.7 BOSS — The Watcher *(name from `dungeons-3d-unity-layout-spec.md §10.5` — owner to ratify)*

- **Identity:** Mini-boss of the **Last Keeper's Walk (D6)**. The layout spec names the final encounter "The Watcher" and is explicit: *"'The Watcher' — Mira's grief, NOT Mira herself."* Lore (`dungeons-storyline.md` Act III): the Last Keeper's Walk follows the previous Keeper's path; she walked toward the Wound to slow the Withering. The Watcher is the *grief she left behind* — a guardian-shape made of the things she could not finish. **Name treated as canon-adjacent — flag for owner ratification.** The codex stresses: this boss must **never be staged as Mira** — it is grief wearing a guardian's shape.
- **KayKit model:** **Paladin (with Helmet)** — `Paladin_with_Helmet` variant of `Paladin.fbx` — `KayKit Mystery Monthly Series 4/10 - April 2024 - Paladin/characters/fbx/`. The catalog (§4) types the Paladin as "a holy knight." Design read: the Watcher takes the *shape of a guardian* — armored, faceless behind the helmet, a hammer/shield. It looks like what a Keeper is *supposed* to become; that's the quiet horror of it. The sealed helmet is essential — it has no face because it is not a person. (The catalog also ships Paladin statue props for the boss-room vignette.)
- **Visual concept:** A luminous, slightly translucent armored figure — render with a faint emissive rim and partial transparency so it reads as *not-quite-real*, a memory given weight. Hammer (`Paladin` hammer prop) and shield.
- **Where it appears:** Last Keeper's Walk (D6), the Crossing — the path's end.
- **Stats anchor (ATB):** new def `"the-watcher"` — `Archetype Boss`, `BaseHp 380`, `BaseAttack 30`, `Speed 1.1`, `Defense 0.2`, `Aether`; Special "Unfinished Oath" — single-target 34 dmg + applies Mark; if the Mark target acts before the Watcher's next turn, the Mark deals a follow-up. **(agent-authored — owner to ratify)**
- **Encounter design:**
  - **Phase 1 (100–55% HP) — The Vigil.** Disciplined, patient. "Unfinished Oath" Marks a target — punishes the player for not respecting the Mark.
  - **Phase 2 (55–20% HP) — The Burden.** A second special "Weight of the Walk" — an all-ally Slow + the Watcher gains Regen (it is *carrying* something it will not put down). The fight gets attritional.
  - **Phase 3 (20–0% HP) — The Rest.** The Watcher's emissive *dims*; it stops using specials and fights plainly, then on death does not collapse — it **fades** (a dissolve, not a clatter). It was never alive to die. The village's ambient lines shift afterward (the storyline doc hooks this).
  - **Telegraphs:** Unfinished Oath telegraphs with a hammer-pointed gesture; Weight of the Walk with a slow shield-raise.
- **Animation needs:** Idle, Move, Attack ×2 (hammer swing, shield-press), Hit-react, Death — **Paladin is a standard humanoid on `Rig_Medium`/`Rig_Large`, shared rig covers all**. **Death must be a fade/dissolve, not a ragdoll** — this is a *shader/VFX* job (a dissolve material), not an animation clip, so it is **not an animation gap**. **Special clips — covered** by `Special`/`CombatMelee`. **No bespoke clip required.**

### 4.8 BOSS — Alduin the Mournful (CANON — the realm antagonist)

- **Identity:** **Canon-locked.** `canon-strings.json`: `alduin` = "Alduin the Mournful", `alduinTitle` = "the Necromancer". The realm's final antagonist. **Name absolutely locked — do not rename, do not paraphrase.**
- **Canon fact the codex must honor (`dungeons-storyline.md` §Act IV, §4.6):** Alduin **is not a boss fight.** *"Alduin is not at the Wound. Alduin is what is left of every Keeper and healer who walked to the Wound and was drank by it. He is many. The Mournful is the title; the soul is composite."* The `At the Edge` (D7) dungeon **ends in a conversation — a four-response dialogue tree — not combat.** Any future "Alduin boss" pitch contradicts canon; the codex does not design one.
- **KayKit model:** Alduin, *when seen at the Edge*, takes **one face** (the storyline: "one of his faces"). Recommended: a **hooded robed figure** — reuse the `Necromancer.fbx` body (it is already canonically "a hand of Alduin"; the antagonist showing one of his many faces *as* a Necromancer-shape is canon-coherent), OR the **Druid** model (`Adventurers 2.0` — robed, "wise, healer-coded"; Alduin was a healer first). The codex recommends the **Druid** body re-tinted with the Withering palette: it visually *says* "he was a healer," which is the whole tragedy. **Flag the model choice for owner ratification** — the canon locks the name and the no-fight rule, not the model.
- **Visual concept:** A robed figure at the edge of the Wound, lit from below by the Wound's violet glow. Read: weary, not wicked. He turns to the Keeper. He does not raise a hand.
- **Where it appears:** At the Edge (D7), the Wound's Threshold — the endgame.
- **Stats anchor:** **None — Alduin has no stat block.** He is a dialogue NPC. If the owner ever wants the D7 "survive 3 breach-encounters" beat (`dungeons-storyline.md` §4.6) to feel Alduin-adjacent, those encounters use **Hollow Reapers and Tiefling Cultists** (§2.5, §2.11) — *not* Alduin himself.
- **Encounter design:** The encounter is `dungeons-storyline.md`'s four-response dialogue tree. The codex's only contribution: ensure the D7 approach (the breach-encounters) is staffed from the existing roster, and that Alduin's *staging* (lighting, the turn-to-face, the walk-into-the-dark exit) is treated as a **cutscene/Timeline beat**, not an ATB scene.
- **Animation needs:** Idle (a weary stand), a **turn-to-face** the Keeper, a **gesture or two** during dialogue (a slow hand-lift, a head-bow), and a **walk-away into the dark** (`dungeons-storyline.md`: "he turns, after, and walks into the dark"). All of these are **covered by the shared rig** — `General` idle/gesture + `MovementBasic` walk. **No combat clips, no specials, no bespoke clip.** The hardest part of Alduin is *writing* and *lighting*, both already canon-locked elsewhere — not animation.

---

## 5. Animation strategy & gap list

### 5.1 The shared-rig advantage

The single most important fact for the whole roster: **every KayKit humanoid shares the `Rig_Medium` / `Rig_Large` skeleton** (catalog §Character Animations 1.1). The Skeletons pack, the Adventurers pack, and *all* the Mystery Monthly characters (Necromancer, Black Knight, Frost Golem, Werewolf-Man, Paladin, Orc Raider, Caveman, Tiefling, Witch, Vampire) animate from **one shared Animator Controller** built once.

**Build plan:** one `HumanoidEnemy.controller` keyed off the `Rig_Medium` clip sets, one `LargeEnemy.controller` for `Rig_Large` bodies (Skeleton Golem, Frost Golem, the bulkier orcs). The Character Animations 1.1 pack's `General` / `MovementBasic` / `MovementAdvanced` / `CombatMelee` / `CombatRanged` / `Special` / `Simulation` / `Tools` sets fill every baseline state. The two `Mannequin` characters in the pack are the preview rigs — validate retargeting on them first.

### 5.2 Per-archetype animation sets

| Archetype | Idle | Move | Attack | Hit-react | Death | Specials | Source |
| --- | --- | --- | --- | --- | --- | --- | --- |
| **Fodder** (Walker, Caveman) | listless idle | shamble walk | 1× clumsy melee | flinch | clatter/topple | — | `Rig_Medium` shared |
| **Standard melee** (Warrior, Orc Raider) | idle | weighted march | 2× (overhead, bash) | flinch | collapse | taunt/drum (substitute) | `Rig_Medium`/`Large` shared |
| **Skirmisher** (Rogue, Tiefling) | crouch idle | fast run | 2× quick combo | flinch | collapse | brand-cast (Tiefling) | `Rig_Medium` shared |
| **Caster** (Hollow Caster) | idle | walk | ranged cast | flinch | collapse | channel-cast (Hex) | `Special` set |
| **Elite** (Hollow Reaper) | idle | march | 2× 2H sweep | flinch | collapse | wide reap-arc | `CombatMelee` 2H (substitute) |
| **Heavy** (Hollow Brute, Frost Golem) | heavy sway | lumber | 2× slam | stagger | shatter/topple | ground-slam | `Rig_Large` shared |
| **Quadruped** (Feral Wolf, Wolf-phase boss) | beast idle | prowl/run | lunge-bite | flinch | collapse | howl | **Werewolf pack's OWN rig — see GAP-PRIMARY** |
| **Boss — humanoid** (Necromancer, Apprentice, Vault Keeper, Inn-Keeper, Watcher, Wolfwarden Ph.1, Alduin) | idle | walk | per role | flinch | collapse/**fade** | summon / mix-vial / wind-up-slam / call / oath / transform-howl | `Special`+`General`+`Tools` (substitutes) |

### 5.3 The gap list

The catalog flagged the two structural truths up front: **(a) the collection has no dedicated monster or pet creatures**, and **(b) the bestiary skews skeleton-heavy.** The codex's design *embraces* (b) — the Hollow Ones being all-skeleton is a feature, not a bug (they are risen Folk; uniformity is thematic). Gap (a) is the real problem, and it concentrates in one place.

**GAP-PRIMARY — the quadruped wolf rig (biggest gap).**
`Werewolf_Wolf` is the collection's **only true quadruped** and it does **NOT** share the humanoid `Rig_Medium`/`Rig_Large` skeleton. Nothing in Character Animations 1.1 retargets to it. It is needed for: **Feral Wolf** (§2.10, a whole enemy type), **The First Wolfwarden Phase 2** (§4.4, a boss phase), and — outside this codex — the **Ice Wolf companion pet** (catalog §4 names `Werewolf_Wolf` as the only quadruped pet candidate). **Action required:** audit the Werewolf Mystery Monthly pack's own bundled clips; whatever it ships (likely idle/walk/run/attack) is the baseline, and any missing **lunge-bite, howl, and hit-react/death** must be commissioned **on the wolf's own rig**. This is the single biggest animation gap in the project and it blocks three things at once. **Recommended priority: HIGH** — it gates the cold-biome dungeons (D3, D5) and the Ice Wolf pet.

**GAP-1 — the kneeling-sorrow idle (Cellar Hollow).**
The Cellar Hollow (§2.7) "kneels and rocks rather than wanders" — a canon emotional beat in the Healer's Cottage design doc. KayKit's `General`/`Simulation` sets have sit/idle variants but **no grief-rock**. This is one short looping clip on the *shared humanoid rig* — cheap to commission, and it carries real emotional weight (the bible's "they are grief, not menace" thesis made visible). **Recommended priority: MEDIUM** — small cost, high payoff; the dungeon ships D1 first and this is a D1 asset.

**GAP-2 — boss "special" gesture clips (polish, not blockers).**
Several boss specials currently ride on *substitute* clips from `Special`/`General`/`Tools` and work fine: the Necromancer's Summon (staff-raise), the Apprentice's Tincture (drink/throw-vial), the Inn-Keeper's Last Call (call-out), the Reaper's reap-arc (2H sweep), the Orc's war-drum. None *block* a build. A bespoke clip per boss would sharpen the read. **Recommended priority: LOW** — schedule as a polish pass after the encounters are tuned.

**GAP-3 — the man→wolf transform (First Wolfwarden).**
There is no morph animation between humanoid and quadruped rigs in the collection, and there cannot be one (different skeletons). **This is designed-around, not commissioned:** the transform is a **model swap masked by a VFX burst** (particle puff + light flash + camera shake) plus a full-turn "contort" pose on the Man rig before the swap and a "rise" pose on the Wolf rig after. No bespoke clip needed — but it depends on GAP-PRIMARY (the wolf rig must exist). **Recommended priority: tied to GAP-PRIMARY.**

**Non-gaps worth stating (so they aren't re-litigated):**
- The Watcher's "fade" death is a **dissolve shader**, not an animation — already a planned URP shader job.
- Boss "phases" are **ATB AI behavior switches**, not real-time mechanics — no extra clips.
- Enemy weapon variety (catalog's per-spawn weapon randomizer) is **prop-swap on a socket**, not animation — the shared melee clips already hold any one-handed/two-handed weapon.

### 5.4 Summary of what to commission

| Item | Type | Rig | Priority | Blocks |
| --- | --- | --- | --- | --- |
| Wolf lunge-bite, howl, hit-react, death (gaps after auditing the pack) | clips | Werewolf quadruped rig | **HIGH** | Feral Wolf, Wolfwarden Ph.2, Ice Wolf pet, D3 + D5 |
| Kneel-and-rock sorrow idle | 1 clip | shared humanoid rig | MEDIUM | Cellar Hollow polish (D1) |
| Per-boss bespoke special gestures (×5–6) | clips | shared humanoid rig | LOW | nothing — polish only |
| Withering dissolve material | shader (not animation) | n/a | MEDIUM | Watcher death, enemy death VFX |

Everything else in the 19-entity roster animates **for free** from the shared KayKit rig + Character Animations 1.1.

---

## 6. Open questions for owner ratification

1. **Mini-boss names** — `dungeons-3d-unity-layout-spec.md §10` named *the Vault Keeper, the First Wolfwarden, the Inn-Keeper, the Mournful Alpha, the Watcher*. The codex treated these as canon-adjacent and designed kits to fit. **Ratify the names** (or supply replacements) before the data layer hard-codes them.
2. **"The Mournful Alpha" vs "Alduin the Mournful"** — both carry "Mournful." Deliberate thematic rhyme, or rename the Alpha to avoid a near-collision? Codex kept the spec's name and flags it.
3. **Alduin's model at the Edge** — codex recommends the **Druid** body (re-tinted) so he visually reads "he was a healer first." Alternative: the `Necromancer` body. Canon locks the *name* and the *no-fight* rule, not the model. **Owner picks the face.**
4. **The Mournful Alpha's model** — codex recommends **Frost Golem** (set-piece scale, ice theme) over `Werewolf_Wolf`. If the owner wants the Alpha to read as a literal wolf, switch to `Werewolf_Wolf` — but that pulls the encounter into GAP-PRIMARY's quadruped-rig dependency.
5. **Agent-authored standard enemies (Caster, Reaper, Brute, Orc, Caveman, Wolf, Tiefling)** — these expand the roster well past v2-foundation's 4-enemy wave loop. Confirm they are wanted as the v1.1 / dungeon-expansion roster, or trim the slate.
6. **Second faction (the Wildlands)** — the codex adopted the catalog's recommendation of a living, non-undead faction. Confirm the realm wants a second faction, or keep the bestiary all-Hollow-Ones.

---

_The dead were people. The lantern shows you that, and asks you to fight them anyway. By lantern. By oath. By Heart._
