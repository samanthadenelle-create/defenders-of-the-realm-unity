# WORK ORDER 861 — Make Sylas + Thrain playable (simplified)

**Status:** READY. Architect-reviewed 2026-08-02. **Both heroes were already BUILT + PLAYABLE and deliberately gated
OFF while the platform stabilized (owner) — so this is RE-ENABLE + verify, not build-from-scratch. No new body art.**
**Author:** UI/QA triage + architect review (read-only, §13) — Claude UI
**Lane:** Hero/Class — gate-flips + content authoring; NO new class enum. (Cathedral of Magic ALREADY EXISTS — re-point
its perks to the mage; Phase 3 + Appendix A3.)
**SELF-CONTAINED:** the creative pass is DONE + owner-approved — the full implementation spec (kits, ids, costs,
effects, trees, arrows, Cathedral tier→unlock, new code, VFX) is the **Appendix (A0–A5)**. CLI implements from this WO
alone; no further creative input or owner decision is needed (all resolved: Grom canonical, all six calls = recommended).
**Origin:** owner 2026-08-02 — add **Sylas** + **Thrain** as playable; *"simplify the ask."* Clarified:
> "Thrain should be a mage (DPS class), glass cannon at close combat." · "Sylas: only different arrow types, maybe an
> offhand dagger." · "Thrain should be a staff and needs the Cathedral of Learning for leveling."

> **Corrections applied throughout (later owner clarifications — these WIN over the quotes above):** Thrain is a
> **RANGED** glass cannon (fragile UP CLOSE, not a melee fighter); the leveling building is the existing **"Cathedral
> of Magic"** (`arcane-tower`), distinct from the defensive Arcane Spire; both heroes were built + gated off, so this
> is re-enable, not build-new; Knight canonical name = **Grom**.

---

## Both heroes reuse EXISTING authored classes — no new class code
- **Sylas → the Ranger slot** (already authored: kit, tree, gear, `Ranger.controller`, `Portraits/Sylas.png`; he's
  already your raid companion).
- **Thrain → the Mage slot** — which the game **already names "Thrain"** (`SlugFor: Mage→"Thrain"`, `HeroCatalog.cs:126`).
  So there is NO rename and NO conflict; just un-gate the Mage and re-tune it (below).
Both = un-gate + tune/author, not a 7-site new-class thread.

**DESIGN PRINCIPLE (owner):** *every* playable hero has SOME way to self-heal/sustain — so no character is stuck with
no recovery. Sylas = Healing Shot (heal = damage dealt); Thrain = his Heal spell; **Knight = confirm he has a
self-heal/sustain too** (creative to verify/add if missing). Bake a heal/sustain into each hero's kit.

## Phase 0 — Unblock the roster (once; small SYS change)
Only the Knight is playable today, hardcoded. Flip the single-class gates:
- `GameStateService.ChooseHero` stops forcing Knight (`~:668-679`, `ff.knightonly`).
- `HeroSelectController.IsPlayable` widens past `PlayableHero = Knight` (`:107,:727`).
- `VendorStockResolver.RosterClasses()` follows the playable set, not the flag (`:102-107`).
- **Per-class hot-swap loadout key:** `HeroLoadout.PrefsKey` is a single global `"dotr-loadout-knight-v1"`
  (`HeroLoadout.cs:39`) → make it per-class, else heroes share one W/E/R bar.
- Roster EditMode test: each playable class resolves body + controller + kit + tree + gear + portrait; no coercion to Knight.

## Phase 0.5 — Tutorial-companion safety (MUST pass before Sylas is marked playable) — review-mandated
Higher-risk than a one-liner: `SylasStewardInjector` / `TutorialFlow` currently hardcode Sylas as the tutorial
companion/narrator. Before Sylas can be selectable:
- **Read the injector/flow for real** and make the tutorial companion a **dynamic pick** — the first non-player
  companion (fall back to Grom) — so it never resolves to the player's own hero.
- **Required regression:** the tutorial COMPLETES end-to-end when the player's chosen hero == Sylas (no dead
  narrator, no softlock). Treat this as a gating test, not a nice-to-have. Sylas is not "playable" until it passes.

## Phase 1 — Sylas the Ranger (LIGHT)
- Add Ranger to the playable set (Phase 0).
- **Owner-specified ability — "Healing Shot":** a ranged attack that deals damage AND **heals Sylas for the amount of
  damage it deals** (life-drain shot). Author as an `abilities.json` ranger ability with a heal-equals-damage-dealt
  effect; it drops into his HUD bar. (Full kit + arrow-types + tree = **Appendix A2** — authored & approved.)
- **⚠ CAVEAT — tutorial companion must not be the player (owner):** Sylas is currently the TUTORIAL companion/narrator
  (`ff.tutorialv2`, `SylasStewardInjector`/`TutorialFlow`). If the player CHOOSES Sylas as their hero, the tutorial
  guide can't also be Sylas — swap the tutorial companion to ANOTHER character (e.g. Grom or another companion) when
  the chosen hero == Sylas. Make the tutorial-companion pick dynamic (first non-player companion), not hardcoded Sylas.
  Needs a quick read of the tutorial-companion wiring; flag as a required sub-fix of making Sylas playable.
- **Weapons = arrow types + an offhand dagger** (owner): slim the ranger weapon list to a few **arrow-type variants**
  (the "weapon" is the arrow — e.g. plain / fire / poison / frost, each a `job:ranger` bow-ammo entry) + one
  **offhand dagger** (`job:ranger` off-hand). Trim/point the ranger `weapons.json` rows to this small set (fits the
  WO-860 "2-per-level, equippable-only" store thinning).
- **Body already exists** — activate it: a live `Resources/NPCs/KayKit/Ranger.fbx` (+ texture) exists, and the hero
  slot has a live `Ranger.controller`; only `Resources/Heroes/Ranger.fbx` is parked `.tripo-extracted`. Import that
  parked FBX as a live `.fbx` OR point `HeroBodySwapper` at the KayKit body; confirm the bow-attach path. NO new art.
- Smoke-test: select → spawn → cast (mana) → equip an arrow type / dagger → raid (run-defenders fleet).

## Phase 2 — Thrain the Mage: RANGED DPS glass cannon (tune the existing kit)
> **Hero differentiation (owner):** heroes differ by their **default combat-HUD skills (the W/E/R bar)** and their
> **skill trees** — both already data-driven (`abilities.json` per class, `hero-talents.json` `trees.<class>`). So
> "make Thrain feel different" = author his default skills + his tree, not new systems. Same lever differentiates Sylas.

- Add the Mage to the playable set (Phase 0) — card already reads "Thrain".
- **Owner-specified starting kit (drops into his HUD bar):** (1) an **easy Fireball** ranged attack as the
  starter/basic; (2) a **Heal** (self-heal); (3) a **Shell / defense buff** (the timed protection — a damage-reduction
  shield for X seconds). Author these three in the `abilities.json` mage block. (Full kit + tree = **Appendix A1** — authored & approved.)
- **RANGED DPS glass cannon (owner-corrected):** he attacks from RANGE (keep the mage's ranged abilities — do NOT
  shorten to melee), with **high damage but low HP/defense**. The danger is being **engaged up close** — if an enemy
  closes on him he **dies fast, especially when out of mana**. Tune the `mage` block in `abilities.json` (damage up,
  keep ranged) + Mage base stats (HP/defense down). Fragile, high-output, position-dependent.
- **Mana = the survival lifeline, not just a resource (owner):** enough mana → he can defend/escape a close engage;
  empty mana → he dies when swarmed. So **mana conservation + recovery is the core mechanic** — include mana-recovery
  (regen/restore ability + tree nodes) and mana-conservation (reduced cost / efficiency nodes). Managing mana IS the
  skill ceiling and the survival skill.
- **Defensive protection spells (owner):** at least one **timed protection** spell — a shield / damage-reduction buff
  for X seconds (an `AbilityDef` with a buff duration, mana-costed) — his cooldown to survive a close engage while he
  repositions. Out of mana = no protection = death.
- **Weapon = a staff** (owner): make the Mage starter + tier a **staff** line (`job:mage`, staff type — several
  already exist: `mage_oak`/`mage_arcane`/`tripo_staff_*`); the staff is his starter.
- **Body already exists** — activate it: live `Resources/NPCs/KayKit/Mage.fbx` (+ texture) + live `Mage.controller`;
  only `Resources/Heroes/Mage.fbx` is parked `.tripo-extracted`. Import/point-at it as with the Ranger. NO new art.
- Smoke-test: select → spawn → ranged cast + mana-recovery + defensive buff → equip staff; verify he's fragile when a mob closes in (dies fast without mana/protection).

## Phase 3 — Cathedral of Magic (Thrain's spell/mana progression) — the building ALREADY EXISTS
**Owner-confirmed:** the **Cathedral of Magic** (`structures-catalog.json` id **`arcane-tower`**, a `GameplayBuilding`
with `npcModel:"Mage"`, singleton) IS Thrain's leveling building. **Do NOT build a new building.**
- **NOT to be confused with the DEFENSIVE arcane tower** — that's the separate **"Arcane Spire"** (`tower_arcane_spire`,
  behaviorId `ArcaneTower`, a real Tower with range/damage/fireRate). Arcane Spire = defending; Cathedral = leveling.
- **Wire the Cathedral's upgrades/Skills to Thrain (owner):** *"just needs to add learnable spells or better magic /
  mana / health logic as skills."* Its perk/skill tiers grant the mage **learnable SPELLS** (unlock `abilities.json`
  mage abilities into his W/E/R bar) and/or **better magic/mana/health** (mana pool + regen + spell power + HP nodes).
  Reuse the existing building perk system (`BuildingUpgradeVM` perks + the Skills tab) — route those perks to the
  mage's abilities/stats rather than tower stats. This IS Thrain's leveling seam — no new leveling system.
- **The exact tier→spell/buff mapping + tier titles are DONE — see Appendix A3** (owner-approved).

## Owner items — essentially none (everything already exists)
- **No new body art:** Ranger + Mage bodies exist (live `NPCs/KayKit/*.fbx` + live hero-slot `.controller`s; the
  hero-slot `.fbx` is parked `.tripo-extracted` — an import/wire task, not new art). Sylas portrait exists; if no
  `Portraits/Thrain.png` exists, point the Mage card at the existing mage portrait — **no owner input needed.**

## Creative pass — COMPLETE (owner-approved 2026-08-02)
The design content is authored, reviewed, and approved. It is the **Appendix (A0–A5)** below — Thrain's + Sylas's full
HUD kits (ids/costs/cooldowns/effects/ranges), skill trees, arrow set, the Cathedral tier→unlock mapping, the new
`ResolveEffect` cases + arrow rider + VFX-reuse notes, and the balance split. CLI authors the appendix directly; no
further creative input is needed.

## Core requirement (owner): abilities must DROP INTO the combat HUD and work for attacking
Each hero's class abilities must **auto-populate the combat-HUD skill bar (W/E/R)** on spawn and **function as their
attack kit** — **Thrain = spells** (ranged casts + the defensive/mana abilities), **Sylas = ranged/archer attacks**
(bow shots + arrow-type abilities). This is exactly what the per-class `HeroLoadout` key (Phase 0, B4) unblocks: today
the single global key would give every hero the Knight's melee bar. Verify each hero's own abilities load + fire.

## Implementation order & required tests (review-mandated 2026-08-02 — non-negotiable)
1. **New `ResolveEffect` cases FIRST, unit-tested** — they are the ONLY new combat code: `drainshot` (PINNED; run
   `strike`, heal caster = damage dealt), `shield` (**reuse the existing Warden's Grace mitigation path minus the
   heal — do NOT invent a second mitigation system**), `manaweave`. Unit tests + AutoPilot coverage before wiring kits.
2. **DataRegression golden (required):** assert (a) Sylas abilities load when class == Ranger, (b) Thrain abilities
   load when class == Mage, (c) **Knight abilities are UNCHANGED**.
3. **Arrow rider scoping:** the `ammoEffect` rider applies ONLY to the Ranger's arrow-using attack (Q) — **Knight and
   Mage basic attacks must be unaffected.** Guard it.
4. **Body-activation smoke test:** `select → spawn → weapon attach point correct` for Ranger (bow) and Mage (staff) —
   the `.tripo-extracted` vs live KayKit bodies are a classic silent-failure source (missing bones / wrong attach).
5. **Cathedral perk isolation:** the new mage perk keys (`mageManaMax`, `unlockSpell:<id>`, …) fold into
   `HeroAbilities`/`HeroTalentModifiers` ONLY — they must NOT leak into tower/defensive-structure logic.
6. **Grom rename = a real sub-task** (not an afterthought): `Garran → Grom` across `hero-talents.json` + any hardcoded
   strings + UI copy; grep for `Garran` and fix all.
7. Phase 0.5 (tutorial-companion) gates Sylas; Phase 0 (per-class loadout key) gates both.

## Acceptance
- [ ] **Each hero's abilities drop into the HUD bar and attack correctly:** Thrain casts spells (ranged) from his
      W/E/R; Sylas fires ranged/archer attacks from his — neither shows the Knight's melee kit.
- [ ] New effects (`drainshot`/`shield`/`manaweave`) unit-tested; `shield` reuses the Warden's Grace mitigation path.
- [ ] DataRegression golden green: Sylas(Ranger)+Thrain(Mage) abilities load; **Knight unchanged**.
- [ ] Body attach points correct on spawn (bow on Sylas, staff on Thrain).
- [ ] Tutorial completes when the player IS Sylas (Phase 0.5 regression).
- [ ] Arrow riders affect ONLY the Ranger's shot; Knight/Mage basics unchanged.
- [ ] `Garran`→`Grom` everywhere (grep clean).
- [ ] >1 hero selectable; a confirmed non-Knight stays that class. Roster test green.
- [ ] Sylas (Ranger) plays with arrow-type weapons + offhand dagger, casts using mana.
- [ ] Thrain (Mage) plays as a RANGED glass cannon (high damage, low HP, dies fast when engaged up close without
      mana), with mana-recovery + a timed defensive-protection spell, staff-wielding, mana-based.
- [ ] `CompileGate` + `DataRegression` green; `abilities.json`/`weapons.json` Resources+StreamingAssets byte-identical.

## Do NOT
- Do NOT add a new `HeroClass` enum member (both heroes reuse Ranger/Mage). Do NOT break the Knight path.
- Keep echoes-never-fight/hero-tag canon; ASCII-only TMP; colorblind law; data-file copies byte-identical.

---

# APPENDIX — Creative design (owner-approved 2026-08-02: "all recommended, Knight = Grom")
This is the finalized kit/tree/arrow/Cathedral spec for CLI to author. Verified against the real effect system
(`HeroAbilities.ResolveEffect`, mana pool 0..10 regen 0.9/s, HP on a 0-100 scale). ASCII-only; status by name/shape, never color.

## A0. Owner rulings locked
- **Knight canonical name = "Grom"** — rename the shipped `Garran` displayName → **Grom** in `hero-talents.json`/copy.
- Build the real **`shield`** effect (not the invuln shortcut) · include the **Manaweave** mana-restore active ·
  build the **arrow on-hit rider** hook · **Meteor 600→260** · Cathedral tier titles as below.
- Knight self-heal = ALREADY PRESENT (default E "Warden's Grace" + tree heals) — nothing to add.

## A1. Thrain (Mage) — ranged glass cannon
Stats vs Knight: **max HP ≈ 0.65×**, no innate armor/block, spell output ≥ Knight per-hit; mana 10 / regen 0.9/s (tree+Cathedral grow it). Death condition = engaged or out of mana.
HUD (auto-populate Q/W/E/R on spawn):
| Slot | Name | id | Effect | Mana | CD | Dmg/Heal | Range |
|---|---|---|---|---|---|---|---|
| Q | Fireball | `mage.fireball` | `strike` (ranged, fire VFX) | 0 | 0.6s | 30 | 14 |
| W | Arcane Shell | `mage.shell` | **`shield`** (new) −40% dmg taken, 4s | 3 | 16s | buff | self |
| E | Mend | `mage.heal` | `heal` (caster) | 3 | 14s | 45 heal | self |
| R | Meteor Strike | `mage.meteor` | `meteor` | 6 | 42s | **260** | 9 |
Tree (`trees.mage` retuned, displayName→"Thrain (Mage)"): **mana conservation+recovery** (Mana Flow +25% regen, Aether Surge +3/kill, Aether Form −30% cost, Eternal Arcana +40% regen, + a **Manaweave** unlock node) · **spell power** (Arcane Focus, Ascension, Flame Mastery, Runic Overload) · **survivability** (re-theme Frost Touch→"Warded Flesh" +20% HP; Blink node +15% HP; Arcane Shield boosts the Shell) · **learnable spells** (`mage.frost-nova`, `mage.arcane-bolt`, `mage.void-rift`, `mage.cataclysm`, `mage.blink`).

## A2. Sylas (Ranger) — archer
HUD:
| Slot | Name | id | Effect | Mana | CD | Dmg/Heal | Range |
|---|---|---|---|---|---|---|---|
| Q | Quick Shot | `ranger.q` | `strike` (fires equipped arrow) | 0 | 0.45s | 25 | 15 |
| W | Snare Trap | `ranger.w` | `snare` (root+slow) | 3 | 11s | 18+slow | 12 |
| E | Healing Shot | `ranger.healing-shot` | **`drainshot`** (new) dmg + heal self = dmg dealt | 4 | 12s | 34 | 15 |
| R | Storm of Arrows | `ranger.r` | `aoe` | 7 | 42s | 72 | 6.5 |
Arrow types (slim ranger `weapons.json` to these; `job:ranger`, `category:"arrow"`, ride Q via the rider hook) + offhand dagger:
| id | Name | Rider | Tuning |
|---|---|---|---|
| `ranger_arrow_plain` | Field Arrows | none | mult 1.0 |
| `ranger_arrow_fire` | Emberhead | burn (dot) | 6 dps × 4s |
| `ranger_arrow_poison` | Venomtip | poison (dot ×2) | 5 dps × 6s |
| `ranger_arrow_frost` | Rimeshot | slow (snare rider) | −35% × 2.5s |
| `tripo_dagger_a` (exists) | Bramblefang | offhand melee | mult 1.25 |
Tree (`trees.ranger` retuned, displayName→"Sylas (Ranger)"): **sustain** (Nature's Gift +regen, + "Bloodbound Draw" Healing Shot +30%) · **arrow mastery** branch (Emberhead/Venomcraft/Deep Freeze buff the matching rider) · **ranged DPS** (Quick Draw, Eagle Vision, Precision Strike, Multishot, pierce/chain) · **mobility/evasion** (Tumble Step dash, +move, stealth).

## A3. Cathedral of Magic (`arcane-tower`) → Thrain's leveling (re-point tier perks from tower stats to mage)
| Tier | Cathedral title | Unlock (spell) | Buff (mage stat) |
|---|---|---|---|
| 1 | Awaken the Cathedral | base kit active | +spell power (small) |
| 2 | Wellspring of Mana | Frost Nova | +max mana +1 / +regen |
| 3 | Rite of Resonance | Manaweave + Arcane Bolt | +max HP / +Shell strength |
| 4 | Cathedral Ascendant | Cataclysm | −mana cost + spell power |

## A4. NEW code the implementer must add (small, self-contained)
- **3 `ResolveEffect` cases:** `shield` (−value% incoming for `seconds`, mirror Warden's Grace mitigation minus heal) ·
  `manaweave` (restore ~5 mana over 3s via the existing `_manaOverTimeRate/Until` drip) · `drainshot` (run `strike`,
  capture damage dealt, `Heal(dealt)` on caster — PINNED).
- **Arrow rider hook:** add `ammoEffect` (+`ammoDps/ammoSeconds/ammoSlowPct`) to `weapons.json` arrows + apply the
  equipped arrow's rider on the basic-attack path (reuse existing `dot`/`snare` StatusEffect primitives).
- **Ability id fields:** add `id` to the mage/ranger Q/W/E/R defs; author the learnable-spell defs the trees reference
  (`mage.frost-nova/arcane-bolt/void-rift/cataclysm/blink`, `ranger.hunters-mark/tumble-step/multishot/precision-strike/storm-of-arrows`).
- **Cathedral perk consumer:** new perk keys (`mageManaMax`, `mageManaRegenMult`, `mageSpellPowerMult`, `mageHpBonusPct`,
  `unlockSpell:<abilityId>`) folded into `HeroAbilities`/`HeroHealth`/`HeroTalentModifiers` (Phase 3 wiring).
- **VFX — reuse existing on-hit VFX (owner: "there are vfx for on hit to build off of"):** the arrow riders
  (burn / poison / frost impacts) AND the new effects (shield / drainshot / manaweave / the learnable spells) wire to
  EXISTING on-hit/impact VFX keys — do NOT author new VFX. Per the VFX-tagging workflow, the owner tags the intended
  VFX key and CLI maps key→hook **verbatim** (never creative-pick/substitute; hold any untagged hook). Existing hooks
  the creative pass already spotted: `FireImpact_Impact`, `Fire_Cast`, `FireballTower_Projectile`, `NoneMageHealingCast_Cast`.
- Keep `abilities.json`/`hero-talents.json`/`weapons.json`/`building-tiers.json` Resources+StreamingAssets byte-identical.

## A5. Balance (role split, all self-sustaining)
Knight (Grom) = durable melee anchor (Warden's Grace) · Sylas = ranged sustain-DPS/kiter (Healing Shot + evasion) ·
Thrain = burst glass cannon (Mend + mana management; fragile). Costs/cds in existing bands. No hero wins every axis.
