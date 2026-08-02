# HERO ANIMATION / ACTION DICTIONARY - known dictionary (2026-07-19)

> Hero "Grom" (Knight, KnightV3 + KnightMocap.controller). Every hero animation -> action; Right
> ActionBar (Attack + Q/W/E/R); Hot-Swap bar (AssignableSkillBar) placeable actions + mappings.
> Each row is source-cited (file:line). Refresh every Sunday (SUNDAY_HOUSEKEEPING.md).

# Hero "Grom" (Knight) — Right ActionBar, Hot-Swap Bar & Full Animation Audit

**Live hero:** single Tripo self-rigged Knight **"Grom"** (class id `knight`, KnightV3 CC/AccuRIG body). Runtime body/animator binding chain: `HeroClass.Knight` → `ff.knightv3` ON (default, `FeatureFlags.cs:463`) → loads `Resources/Heroes/KnightV3.fbx` → `ff.mocaploco` ON (default, `FeatureFlags.cs:476`) binds **`Assets/Resources/Heroes/KnightMocap.controller`** (`HeroBodySwapper.cs:472-486`), falling back to `Knight.controller` only if the mocap load misses. `applyRootMotion=false`, `anim.speed=1.0`.

The animator is **code-generated** by `DeNelle.Editor.HeroAnimatorFactory.BuildKnightMocapController` (`HeroAnimatorFactory.cs:300`), NOT hand-authored. Per-ability cast/skill/locomotion clips are owner-picked in `motion-castings.json` (target `knight`) and baked in via `MotionCastings.Resolve` at bake time. The Paladin lane (`KnightPackageControllerBuilder` + `weaponskill-animations.json`) is **DEAD/gated-out** for Grom (`ff.knightv3` cuts off its only consumer) — do NOT source Grom clips from `weaponskill-animations.json`.

---

## 1. RIGHT ACTIONBAR (right-thumb cluster)

**What this bar actually is:** the WO-611 combat HUD right-thumb cluster is **1 literal ATTACK pill + 4 Q/W/E/R ability medallions arced over it** — FIVE inputs, not four. "The ATTACK" is unambiguously the **ATTACK pill** (the only surface literally named `attack`, melee-icon'd, with no ability id/JSON row, wired to `PlayerAttackController.TriggerBasicAttack`). The task's "SKILL 1/2/3" is stale WO-609 residue (the old static W/E/R kit); WO-611 added Q (Sword Heroic) as a 4th medallion. I record the **live truth = ATTACK pill + Q/W/E/R (4 skills)**, with R as the ultimate, and flag the 3-vs-4 drift as an owner ruling.

| Slot | Ability id | NAME | What it does (effect / dmg / range / cd / cost) | Input (key / touch) | Animator trigger / state | Underlying clip | VFX |
|---|---|---|---|---|---|---|---|
| **ATTACK** (pill) | *(none — no ability id/JSON row)* | **Sword Wielding** (basic sword melee; owner-named 2026-07-19) | 3-swing melee combo (`ComboLength=3`, cycles 0→1→2), **dmg = `_baseDamage` 30** × weaponMult (×1.75 perfect-hit, ×3.0 riposte), reach ~3.2m, cd 0.6s. Gated `BattleLock.IsInBattle` + `InputSuppressed`. | ATTACK pill (bottom-right, `Pill611` rect) → `HudCommands.Attack` → `TriggerBasicAttack`. Also Space / LMB / gamepad-South via `PlayerAttackController.StartAttack` | `Attack` trigger + `Combo` int (`ActorAnimator.PlayAttack`, `ActorAnimator.cs:132-133`) | Combo0 = **`atk_slashright.fbx`** (guid cdc0b23…, on-disk KnightMocap Attack0) → Combo1 `atk_slashleft.fbx` → Combo2 `atk_spin.fbx`. *(Motion-castings humanoid-inherited attack0 `Combat_Weapon_WeaponSkill_Sword_And_Shield_Slash.anim` is dormant for KnightMocap.)* | **none on basic hit** (owner 2026-08-02 — `Weaponskillsword_Impact`/`KnightWeaponskill_Impact` deleted from the melee path); PERFECT hit = floating gold **PERFECT** stamp (`CombatText`); elemental brand burst only on element weapons (`WeaponVfxMap.ElementalOnHitKey`). `GameSfx.PlaySwordSwing` audio unchanged |
| **SKILL 1 / Q** | **`knight.q`** | **Sword Heroic** | effect=`dash`. Warps to reticle-locked/nearest in-range hostile (stops 1.6m short), **30 dmg** (Aether) + `Freeze` 1.0s stun. Falls back to live boss else whiffs. **range 10, cd 6s, 0 mana.** | LMB / Space / gamepad-South (primary) **and** Q medallion (slot 0) → `AbilityRequested.Invoke(0)` → `TryCast(Q)`. Gated `BattleLock.IsInBattle`. | `Cast` trigger + `CastVariant`=1 (`PlayCast`), plus `PlayAttack(0)` melee overlay. animVariant: dash→`leap`→unmapped(-1)→slot-fallback = **1** | `CastVariant=1` = knight `skill1` = **`fist_flyingkick_m.fbx`** (`motion-castings.json:166`, on-disk verified) | Registry-only: cast `skill1` vfxKey='' (**silent**); impact `Melee_Impact`. (abilities.json `Dash_Blink`/`Melee_Impact` suppressed) |
| **SKILL 2 / W** | *(none — no `id` in abilities.json; addressable `knight`/W. Proposed mint: `knight.w`)* | **Shield Charge** | effect=`knockback`. Forward cone (centre = origin+fwd×range×0.5, radius range+0.85): **26 dmg** + `Slow` 1.5s + `Freeze` 0.4s (guard-break) + 6-unit knockback impulse. **range 4.5, cd 9s, 2 mana.** | W medallion (slot 1) → `AbilityRequested.Invoke(1)` → `TryCast(W)`. Gamepad-East. HUD label "W" (JSON `key`=F). | `Cast` + `CastVariant`=2, plus `PlayAttack(0)` overlay. animVariant: knockback→`slam`→unmapped(-1)→slot-fallback = **2** | `CastVariant=2` = knight `skill2` = **`fist_whirlwindkick_m.fbx`** (`motion-castings.json:181`, on-disk verified) | Registry `skill2` vfxKey='' (**silent** cast); impact `Cleave_Impact` |
| **SKILL 3 / E** | *(none; addressable `knight`/E. Proposed mint: `knight.e`)* | **Warden's Grace** | effect=`taunt`. `Slow` for max(2, freeze=4)s to every hostile within range+0.85; **0 direct dmg**; grants Knight **+20 HP** stand-in shield. **range 6, cd 12s, 3 mana.** | E medallion (slot 2) → `AbilityRequested.Invoke(2)` → `TryCast(E)`. Gamepad-West. HUD label "E". | `Cast` + `CastVariant`=3, plus `PlayAttack(0)` overlay. animVariant: taunt→`shout`→unmapped(-1)→slot-fallback = **3** | `CastVariant=3` = knight `castHeal` = **`f-magiccontrol-01.fbx`** (Magic Spell Cast gesture, `motion-castings.json:203`, on-disk verified). ⚠ mismatched borrow — a taunt plays a heal-channel clip | Registry `castHeal` vfxKey=`Dash_Blink`, sfx `Heal`; held `Taunt_Aura` residual loop for freeze-seconds |
| **ULT / R** | *(none; addressable `knight`/R. Proposed mint: `knight.r`)* | **Radiant Strike** | effect=`meteor`. `castSeconds=0.5` interruptible wind-up (moving cancels+refunds); launches projectile to nearest cluster then Blast(radius=range) **220 dmg** on arrival. **blast range 9, cd 40s, 6 mana.** | R medallion (slot 3) → `AbilityRequested.Invoke(3)` → `TryCast(R)`. Gamepad-North. HUD label "R". | `Cast` + `CastVariant`=**2** (meteor→`skill2`→variant 2 overrides pressed-slot 4), plus `PlayAttack(0)` overlay | `CastVariant=2` = knight `skill2` = **`fist_whirlwindkick_m.fbx`** — **same whirlwind clip as W** | Cast VFX keyword for variant 4 = **null → SILENT by design**; meteor-landing impact `Cleave_Impact` |

**Source:** `abilities.json:66-74` (knight Q/W/E/R — only Q has `id`); `HeroAbilities.cs:400-448,562-643,1354,1369-1443`; `HudKitController.cs:377-393,623-684`; `HudKitCommandBridge.cs:58-64`; `PlayerAttackController.cs:36,70,177,368-419,540`; `motion-castings.json:163-227`.

---

## 2. HOT-SWAP BAR (AssignableSkillBar)

**What it is:** a **BUILT** 4-slot player-assignable quick-slot bar — `AssignableSkillBar.cs` (index→abilityId map, PlayerPrefs `dotr-skillbar-extra-v1`, edits battle-locked). Rendered bottom-CENTER as `AssignableSkillRow` (`HudKitController.cs:686-719`) firing `HudCommands.AssignableCast(slot)` → `HeroAbilities.TryCastExtra(abilityId)`. **It holds ABILITIES/SKILLS ONLY** — NOT weapons, NOT potions/consumables (those are separate fixed slots), NOT stances, NOT echoes. Slots are filled out-of-combat from the Skill-Tree loadout chooser. The chooser filter (proven complete + exclusive) exposes **exactly 16 placeable ids for a Knight**: 13 `knight-skills` + 3 `universal-skills`. `ranger.*`/`mage.*` stubs and base Q/W/E/R are excluded by construction and by `FindById` (no resolving def).

Animation rule: the extra bar always passes slot-fallback variant 0 into `ResolveAnimVariant`; effect-SHAPE keyword decides the clip. strike/snare→`skill1`(1); aoe/cleave/meteor→`skill2`(2); heal→`castHeal`(3); dot/hot/invuln/default→generic `cast`(0); dash/knockback/taunt/blink→-1→generic Cast(0).

| id | Name | Category | Mapped to (effect) | Animator trigger / state | Clip (registry keyword) |
|---|---|---|---|---|---|
| `knight.ranged-poke` | Throwing Spear | knight-skill / strike | 28 dmg, 16m, 1.2s cd, 1 mana, 0.35s wind-up | `PlayCast(1)` (Cast + CastVariant=1) | Cast_q / `skill1` = `fist_flyingkick_m.fbx` |
| `knight.mending-salve` | Mending Salve | knight-skill / heal | 42 HP self/Heart, 5m, 16s cd, 4 mana | `PlayCast(3)` | Cast_e / `castHeal` = `f-magiccontrol-01.fbx` |
| `knight.snare-arrow` | Snare Arrow | knight-skill / snare | 18 dmg + 2.5s slow, 14m, 11s cd, 3 mana | `PlayCast(1)` | Cast_q / `skill1` |
| `knight.suppressing-volley` | Suppressing Volley | knight-skill / cleave | 36 dmg 6m arc, 20s cd, 5 mana | `PlayCast(2)` | Cast_w / `skill2` = `fist_whirlwindkick_m.fbx` |
| `knight.shield-bash` | Shield Charge *(distinct from W-slot ability)* | knight-skill / snare | 22 dmg + 2.5s slow, 3.6m, 8s cd, 2 mana | `PlayCast(1)` | Cast_q / `skill1` |
| `knight.thunderbolt` | Thunderbolt | knight-skill / strike | 30 dmg, 16m, 3s cd, 2 mana | `PlayCast(1)` | Cast_q / `skill1` |
| `knight.emberbrand-throw` | Emberbrand Throw | knight-skill / dot | 12 dmg + 8 dps burn 4s, 14m, 10s cd, 3 mana | `PlayCast(0)` | generic Cast / `cast` |
| `knight.wardens-roar` | Warden's Roar | knight-skill / taunt | 6m taunt zone + 10 dmg, 14s cd, 3 mana, 6s taunt | `PlayCast(0)` (shout→-1→fallback 0) | generic Cast (no shout clip) |
| `knight.sweeping-cut` | Sweeping Cut | knight-skill / cleave | 30 dmg 5m sweep, 12s cd, 3 mana | `PlayCast(2)` | Cast_w / `skill2` |
| `knight.oathmend` | Oathmend | knight-skill / healOverTime | 10 HP/s × 5s (50), 20s cd, 4 mana | `PlayCast(0)` | generic Cast / `cast` |
| `knight.eternal-aegis` | Eternal Aegis | knight-skill / invuln | 8s full invuln, 90s cd, 6 mana | `PlayCast(0)` (invuln not in switch→`cast`) | generic Cast / `cast` |
| `knight.second-wind` | Second Wind | knight-skill / heal | 35 HP, 45s cd, 4 mana | `PlayCast(3)` | Cast_e / `castHeal` |
| `knight.champions-combo` | Champion's Combo | knight-skill / cleave | 120 dmg (3×40), 4m, 30s cd, 5 mana | `PlayCast(2)` | Cast_w / `skill2` |
| `universal.arcane-bolt` | Arcane Bolt | universal-skill / strike | 22 dmg, 14m, 2s cd, 0 mana | `PlayCast(1)` | Cast_q / `skill1` |
| `universal.mend` | Mend | universal-skill / heal | 25 HP, 12s cd, 0 mana, instant | `PlayCast(3)` | Cast_e / `castHeal` |
| `universal.dash` | Dash | universal-skill / blink | 6m blink dodge, 8s cd, 0 mana | `PlayCast(0)` (blink→-1→fallback 0) | generic Cast (no blink clip) |

**Source:** `AssignableSkillBar.cs:33-140`; `HudKitController.cs:686-719`; `HeroAbilities.cs:459-496,733-756`; `abilities.json:86-113`; `hero-talents.json:14-35,125-127`; `HeroLoadoutVM.cs:243-267`; `HeroSkillTreeVM.cs:698-712`.

---

## 3. EVERY HERO ANIMATION (live `KnightMocap.controller`)

| Action | Animator state / trigger | Clip asset | Call-site (file:line) | Notes |
|---|---|---|---|---|
| Idle | Locomotion (Speed≈0) | `m-standby-idle` (studio-mocap magical-moves) | `HeroLocomotion.cs:1030` `SetLocomotion` | 1-D BlendTree on Speed |
| Walk | Locomotion (Speed≈2) | `walk_normal_f.fbx` (guid 077e3889…, registry `knight.walk`) | `HeroLocomotion.cs:1030,1392` | registry override of Shared_Walk_Forward (on-disk verified) |
| Run | Locomotion (Speed≈6) | `move_run_m.fbx` (guid 75f128cb…, `knight.run`) | `HeroLocomotion.cs:1030` | registry override of Shared_Run_Forward |
| Combat Idle | CombatLocomotion (`InCombat`) | `idle_ready` | `HeroLocomotion.cs:1067` `SetCombatStance` | braced sword+shield stance |
| Combat Walk | CombatLocomotion | `walkforward01.fbx` (guid c6515351…, `knight.combatWalk`) | `HeroLocomotion.cs:1067` | on-disk verified |
| Combat Run | CombatLocomotion | `runforward_218667.fbx` (guid 2bf84055…, `knight.combatRun`) | `HeroLocomotion.cs:1067` | on-disk verified |
| Injured idle/walk/run | InjuredLocomotion (`Injured` bool) | `injured hurting idle` / `injured walk` / `injured run` | `HeroHealth.cs:1046` `SetInjured` | retargeted from Enemies/ |
| Unsheathe (draw) | Unsheathe (`InCombat` & Speed<0.5) | `sword_drawing_m.fbx` (guid 25157269…, `knight.unsheathe`) | `HeroLocomotion.cs:1067` (stance path) | auto→CombatLocomotion at exit 0.92 |
| Turn L/R 90 | TurnLeft/Right (`TurnDir` ±1, Speed<2) | `turnleft90` / `turnright90` | `HeroLocomotion.cs:244,1043` `PlayTurn` | cosmetic in-place pivot |
| Turn L/R 180 | TurnLeft180/Right180 (`TurnDir` ±2) | `turnleft180` / `turnright180` | `HeroLocomotion.cs:244` | |
| Basic attack combo | Attack0/1/2 (`Attack`+`Combo` 0/1/2) | `atk_slashright` / `atk_slashleft` / `atk_spin` (hardcoded MocapAttackClips) | `PlayerAttackController.cs:416` `PlayAttack(_comboIndex)` | on-disk verified; NOT registry-wrapped |
| Cast — generic (v0) | Cast / CastUpper (`Cast`, `CastVariant`=0) | **on-disk STALE = `atk_slashright.fbx`** (should be `Combat_Spell_MagicalMoves_SpellCast_02.anim`) | `HeroAbilities.cs:586,618`; `ActorAnimator.cs:148` | ⚠ needs re-bake — sword-swing on a cast = F8-48 violation |
| Cast Q (v1) | Cast_q / CastUpper_q (`CastVariant`=1) | `fist_flyingkick_m.fbx` (`skill1`) | `HeroAbilities.cs:618` `PlayCast(1)` | on-disk verified |
| Cast W (v2) | Cast_w / CastUpper_w (`CastVariant`=2) | `fist_whirlwindkick_m.fbx` (`skill2`) | `HeroAbilities.cs:618` `PlayCast(2)` | on-disk verified |
| Cast E (v3) | Cast_e / CastUpper_e (`CastVariant`=3) | `f-magiccontrol-01.fbx` (`castHeal`) | `HeroAbilities.cs:618` `PlayCast(3)` | on-disk verified |
| Cast R (v4) | Cast_r / CastUpper_r (`CastVariant`=4) | `atk_spin.fbx` (hardcoded MocapSpellClips[4]) | `HeroAbilities.cs:618` | NOT registry-wrapped by design |
| Hit / flinch | Hit (`Hit` trigger, standing) | `m-ss-damage-01.fbx` (guid 7356fd49…, hardcoded, state.speed=3) | `HeroHitReaction.cs:104` `PlayHit(Gut)` | `HitDir` int SET but NO transition reads it (dead) |
| Block / shield raise | Block (`Block` bool) | `shield_blockup.fbx` (guid 74e78822…, hardcoded MocapBlockClip) | `PlayerAttackController.cs:324` `SetBlocking` | RMB/Shift/LT held |
| Death (dir 0/Fall) | Death (`Dead`=true) | `Shared_Death` | `HeroHealth.cs:834` `Die(DeathDir)` | `Dead=false`→Revive→Locomotion |
| Death Left / Right | DeathLeft/Right (`DeathDir` 1/2) | `Shared_Standing_Death_Left` / `_Right` | `HeroHealth.cs:834` | |
| Death Front / Back | DeathFront/Back (`DeathDir` 3/4) | `Signature_Death_Forward.anim` / `Signature_Standing_Death_Backward_01.anim` | `HeroHealth.cs:834` | HeroPackages/Knight/…/Extracted |
| Revive | Locomotion (`Dead`=false) | (returns to Idle) | `HeroHealth.cs:866` `Revive` | |
| Victory pose | Victory (`Victory` trigger) | `Shared_Victory_Pose` | `HeroLocomotion.cs:573-574`; `HeroAbilities`/ActorAnimator | returns to Locomotion at exit 0.95 |
| Dance emote | *(bypasses controller — transient PlayableGraph)* | `ResolveEmoteClip("dance")` (runtime `Resources.LoadAll<AnimationClip>`) | `HeroEmote.cs:116-119` `AnimationClipPlayable.Create` | one-shot, restores locomotion after |
| Bow recoil (ranged) | (direct `BowRecoil` trigger) | — | `HeroImpactFeedback.cs:78` | upper-body reaction |
| Upper-body overlay | Upper Body layer (1, Override) forced weight=1 | CastUpper / CastUpper_q/w/e/r (mirror of base casts) | `ActorAnimator.cs:93,167` `SetLayerWeight` | arms swing/cast while legs walk |
| Attack tempo shaping | `Animator.speed` write | (no clip — speed curve) | `ActorAnimator.cs:267,284` `ShapeAttackTempo/LerpSpeed` | anticipation→impact→recovery |

**Declared-but-dead params:** `WindUp` (Trigger, driven by `PlayWindUp` at `ActorAnimator.cs:173`) and `HitDir` (Int, set at `:195`) have **NO consuming state/transition** on Knight/KnightMocap. `HeroVictoryPoseBridge` `VictoryPose` param does not exist on the hero controller (guarded no-op).

---

## OPEN GAPS

Genuinely unresolved after recursive passes (each is an owner/CLI ruling or a shipped-debt finding, not a discovery hole):

1. **STALE BAKE — generic Cast (v0) on-disk = `atk_slashright` (a sword slash), not the registry pick `Combat_Spell_MagicalMoves_SpellCast_02.anim`.** Root cause: the `knight.cast` row has an empty guid and pointed at an `.anim` not extracted until commit `124b293d` (2026-07-13), after the last bake — so `LoadRowClip` 404'd and fell to the builder default. Fix = re-run `BuildKnightMocapController` (the `.anim` now exists) + optionally backfill the row guid. This is a live F8-48 violation IF any equipped ability resolves to variant 0 at runtime (dot/hot/invuln/extra-bar Mend). *(8 of 9 registry-consumed slots ARE current — walk/run/combatWalk/combatRun/unsheathe/skill1/skill2/castHeal verified on-disk.)*

2. **No dedicated rig clips for dash / knockback / taunt / blink** (`leap`/`slam`/`shout`/`blink` keywords → `VariantForAnimKey` returns -1 → borrow the pressed-slot clip). WO-585 is a design spec only ("No code/scene/bake yet"). On-rig candidates exist for leap(`atk_jump`)/shout(`sword_shout_m`)/taunt(`sword_provocative_m`); **blink and slam have NO source asset anywhere.** Worst borrow: E/Defender's-Call (taunt) plays a heal-channel clip. This is un-accepted placeholder debt.

3. **Spec-vs-implementation drift: "3 skills" vs live 4.** The task/WO-609 comment says ATTACK + 3 skills (W/E/R); the live WO-611 cluster is ATTACK pill + Q/W/E/R = 4 medallions. Owner must rule whether to update the spec to 4 or reconcile the HUD. (The ATTACK-identity itself is NOT ambiguous — it is the pill.)

4. **Knight W/E/R carry no `id` in `abilities.json`** (only `knight.q` does). Addressable by class+slot but NOT `FindById`. Owner ruling: mint `knight.w`/`knight.e`/`knight.r` (NOT `knight.shield-bash`, a distinct pool ability) into both JSON mirrors, or present by slot-address. Design docs favor slot-addressing for fixed kits.

5. **R/Radiant Strike cast VFX is SILENT by design** — `CastVariantKeyword[4]=null`, so the ultimate's cast/projectile VFX renders nothing until the motion vocabulary grows an r-slot row. Confirm whether any equipped R ult is expected to show cast VFX.

6. **ATTACK-pill Attack0 clip has two candidate values** — the on-disk KnightMocap Attack0 = `atk_slashright.fbx` (binary-verified, authoritative for the live combo), while the motion-castings humanoid-inherited attack0 = `Combat_Weapon_WeaponSkill_Sword_And_Shield_Slash.anim` (dormant for KnightMocap). Stated as fact, not hedged; no owner action needed unless a re-bake is expected to switch lanes.

*Fully resolved this pass (no longer gaps): ATTACK-pill damage = 30 (`_baseDamage`, in source at `PlayerAttackController.cs:36`, no prefab override); hot-swap chooser pool = exactly 16 ids; `weaponskill-animations.json` confirmed dead for Grom; `KnightMocap` 8/9 registry slots current.*

## Owner naming + redesign rulings (2026-07-19)

Canonical names for the Right ActionBar (theme tracks the ability's weapon):

| Slot | Canonical name | Animation clip | Notes |
|---|---|---|---|
| ATTACK pill | **Sword Wielding** | `atk_slashright`/`atk_slashleft`/`atk_spin` | basic 3-swing sword melee |
| Q `knight.q` | **Sword Heroic** | `fist_flyingkick_m` | dash + 1s stun |
| W (mint `knight.w`) | **Shield Charge** | `fist_whirlwindkick_m` | cone knockback + slow + guard-break |
| E (mint `knight.e`) | **Warden's Grace** | **Mage Spell Cast 5** (owner-specified; supersedes `f-magiccontrol-01`) | REDESIGNED - see spec below |
| R/ult (mint `knight.r`) | **Radiant Strike** | **Jump into Slash Up** (owner-specified; replaces the `fist_whirlwindkick_m` dup shared with W) | 220 dmg meteor |

### SHIPPED — WO-750 (2026-07-19)
Landed in the data + hero-ability lane (compile pending the orchestrator batch-gate + a controller re-bake):
- **Names + ids:** the 5 canonical names are in `abilities.json` (knight q/w/e/r); **`knight.w` / `knight.e` / `knight.r` ids minted** (was Q-only) so hot-swap can address them. Right-bar medallions now render with **NO Q/W/E/R key-letter badge** (mobile-input ruling — `HudKitController.BuildAbilityRow`, null keyBadge).
- **E = Warden's Grace REDESIGN:** effect `taunt` -> new **`gracebuff`** shape (`HeroAbilities.ResolveWardensGrace`): heal **25% max HP** (authored in `damage`, read as a percent) + a **Defense (gear ArmorDefense)-scaled bonus**, then **Grace Shield** for `seconds` (8s): a **HoT 5% max HP / 2s** drip (shared `_hpOverTime` window) + a `grace-shield` HUD buff marker. Anim: `castAnim:"castHeal"` -> knight **castHeal rebound to Mage Spell Cast 5** (`f-ss-magespellcast-05.fbx`, guid d2be6fe2ab0d3704ca777bbb5c48e378). Support-cast safe (no melee swing / no foe-facing, F8-48).
- **R = Radiant Strike:** `castAnim:"r"` -> resolves to **CastVariant 4 (its own Cast_r state)**, de-duping the `fist_whirlwindkick_m` it shared with W.
- **SFX (`motion-castings.json`, Resources/Sfx real clips):** skill1/skill2 `sfxId` = `Swords_Clash`; castHeal `sfxId`/`sfxImpact` = `Heal`; castHeal cast VFX key `Dash_Blink` -> `Heal_Cast`.

**STILL OPEN (out of the data/hero-ability lane — needs the orchestrator):**
1. **R clip = the exact "Jump into Slash Up" (`atk_slashup`):** CastVariant 4's clip is the hardcoded `HeroAnimatorFactory.MocapSpellClips[4] = "atk_spin"`, NOT registry-resolved, and `atk_slashup` is attack-taxonomy so it can never be a cast-registry row (MotionCastings lint #5). Change that one array entry to `"atk_slashup"` + re-bake `BuildKnightMocapController`. Until then R plays `atk_spin` (distinct from W — de-dup already met).
2. **E -20% incoming-damage mitigation:** `HeroHealth.TakeDamage` has no external per-cast DR seam (only `_gear.ArmorDefense` + talent DR). Add a small `SetGraceShield(frac, until)` that `TakeDamage` multiplies by, and have `ResolveWardensGrace` call it. Heal + HoT + marker already ship; only the -20% window is pending.
3. **R cast/impact SFX (SpellCast/Spell_Impact):** CastVariant 4 has no registry keyword (`CastVariantKeyword[4]=null`, silent by design); wiring it needs an r-slot keyword, which requires extending the closed `ActionKeywords` vocabulary + factory consumption.
4. **Ability icons:** the owner's pixel-art sheet is not in the project yet — icon slicing/import deferred until the PNG lands.

### E = Warden's Grace - REDESIGN SPEC (owner, 2026-07-19) -> IMPLEMENTED (WO-750, see SHIPPED above)
Was "Defender's Call" (taunt). Now a **Hybrid Support: Active Targeted / Area Heal + Buff**.
- **Effect:** instantly heal the target ally (or self) for **25% of max HP + a small amount scaled by the Knight's Defense stat**. Applies **Grace Shield** for **8s**: **-20% incoming damage** + **HoT 5% max HP every 2s**.
- **Visual:** radiant golden light pulsing from the knight's sword/shield, floating runes + particle beams connecting to allies; subtle mobile screen glow (optimized VFX).
- **Cooldown:** 18-22s (scales with upgrades / pet rarity). **Cost:** low-to-medium mana.
- **Unlock/progression:** early via knight pet or class tree; upgrades raise heal %, cut cooldown, or add a team-wide mini-shield at higher levels.
- **Animation:** bind to **Mage Spell Cast 5** (resolves the old mismatched heal-clip borrow - a mage-cast gesture is now apt).
- **Implementation:** a redesign of the existing E ability (HeroAbilities + abilities.json + motion-castings knight castHeal -> Mage Spell Cast 5 + new Grace Shield buff/HoT/-20% dmg + VFX). CLI to mint a WO (next-free 750).

## Open gaps / owner rulings needed
- STALE BAKE: generic Cast (v0) on-disk KnightMocap = atk_slashright.fbx (a sword slash) instead of registry pick Combat_Spell_MagicalMoves_SpellCast_02.anim. Cause: knight.cast row has empty guid + .anim not extracted until commit 124b293d (2026-07-13) after last bake, so LoadRowClip 404'd -> builder default. Fix = re-run BuildKnightMocapController (the .anim now exists) + optionally backfill row guid. Live F8-48 violation IF any equipped ability resolves to variant 0 (dot/hot/invuln/extra-bar Mend). 8 of 9 registry slots ARE current (verified on-disk).
- No dedicated rig clips for dash/knockback/taunt/blink (leap/slam/shout/blink keywords -> VariantForAnimKey -1 -> borrow pressed-slot clip). WO-585 is design-spec only ('No code/scene/bake yet'). On-rig candidates exist for leap(atk_jump)/shout(sword_shout_m)/taunt(sword_provocative_m); blink and slam have NO source asset anywhere. Worst borrow: E/Defender's-Call taunt plays a heal-channel clip. Un-accepted placeholder debt.
- Spec-vs-implementation drift: task/WO-609 says ATTACK + 3 skills (W/E/R); live WO-611 cluster is ATTACK pill + Q/W/E/R = 4 medallions. Owner ruling: update spec to 4 or reconcile HUD. (ATTACK identity itself is NOT ambiguous — it is the pill.)
- Knight W/E/R carry no 'id' in abilities.json (only knight.q does); addressable by class+slot but NOT FindById. Owner ruling: mint knight.w/knight.e/knight.r into both JSON mirrors (NOT knight.shield-bash, a distinct pool ability), or present by slot-address. Design docs favor slot-addressing for fixed kits.
- R/Radiant Strike cast VFX is SILENT by design — CastVariantKeyword[4]=null, so the ultimate's cast/projectile VFX renders nothing until the motion vocabulary grows an r-slot row. Confirm whether an equipped R ult is expected to show cast VFX.
- ATTACK-pill Attack0 has two candidate clips: on-disk KnightMocap Attack0 = atk_slashright.fbx (binary-verified, authoritative for the live combo) vs motion-castings humanoid-inherited attack0 = Combat_Weapon_WeaponSkill_Sword_And_Shield_Slash.anim (dormant for KnightMocap). Stated as fact; no owner action needed unless a re-bake is expected to switch lanes.
