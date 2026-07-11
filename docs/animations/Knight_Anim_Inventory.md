# Knight Animation Inventory & 4-Button Mapping — WO-KNIGHT-ANIM-001

**Author:** research pass (read-only). **Date:** 2026-07-10. **Status:** RESEARCH / recommendations only — owner decides final clip choices.
**Scope:** inventory the Knight combat animations across the three ActorCore/Mixamo packs, cross-reference what `KnightPackageControllerBuilder` already binds vs. what sits unused, and RECOMMEND which clip drives each of the 4 always-visible combat buttons.

> No code, `.controller`, `.anim`, or scene file was edited. All clip choices for buttons are marked **RECOMMENDED**.

---

## 1. Packs found (and what is on disk)

| Pack | Location | On disk? | Form | Notes |
|---|---|---|---|---|
| **Hero Motion (extracted, LIVE)** | `Assets/HeroPackages/Knight/Animations/Extracted/*.anim` | **YES — 60 clips present** | `.anim` retargeted to the live `Knight_Hero` (Paladin) Humanoid avatar | The only set already usable by the controller. Naming taxonomy `Combat_Weapon_WeaponSkill_*`, `Combat_Spell_*`, `Signature_*`, `Passive_*`. These are retargets of the loose Mixamo `Assets/Action/Knight/*.fbx` sword-&-shield + standing-melee takes. |
| **Old-rig Mixamo source** | `Assets/Action/Knight/*.fbx` (~100 loose FBX) | YES (tracked, ~384 KB each) | Humanoid `.fbx` (old rig) | Source the extracted set came from: `sword and shield *`, `standing melee *`, `standing block *`, `draw/sheath sword`, `standing taunt *`, locomotion. Referenced by name in `weaponskill-animations.json`. |
| **Sword and Shield Moves** | `Assets/Action/Knight/Motion/studio-mocap-sword-and-shield-moves/*.fbx` (45 FBX) | YES | ActorCore mocap `.fbx` — **NOT extracted / NOT retargeted** | The owner-cited "Sword and Shield Moves" pack. Cleanest dedicated S&S combat set (`atk_slash{left,right,up,down}`, `atk_stab`, `atk_spin`, `atk_shieldcharge`, `atk_shieldswipe0{1,2}`, `shield_block*`, `sword_parry*`, walk/run/turn). **Needs an import+retarget pass before the controller can use it.** |
| **Magical Moves** | `Assets/Action/Knight/Motion/studio-mocap-series-magical-moves/*.fbx` (44 FBX) | YES | ActorCore mocap `.fbx` — **NOT extracted** | The owner-cited "Magical Moves" pack. Spell-cast stances (`*-magespellcast-0N`, `magiccontrol`, `standby/warning idle+swap`, `damage`). m/f + h/ls/ss variants. Casting flavor for a spell-skill button; **needs import+retarget.** |
| Hero Motion raw FBX | `Assets/Action/Knight/Motion/studio-mocap-hero-motion/*.fbx` (~48 FBX) | YES | ActorCore mocap `.fbx` (weapon-showcase library) | `sword_shout_m`, `sword_judgment_m`, `sword_heroic_f`, `greatsword_optimus_m`, `spear_charge_m`, `swordshield_forgeahead_m`, `swordshield_ready_m`, etc. Signature/hero-pose material; source of some extracted `Signature_*` clips. |
| Wizard casts | `Assets/Action/Wizard/*.fbx` | YES | Humanoid `.fbx` | `Standing 1H/2H Magic Attack`, `2H Cast Spell`, `Area Attack`, `Wizard_Heal`. Cross-class spell reference if the Knight gains a cast button. |

**VFX-only (not animation) — ignore for this WO:** `Assets/Hovl Studio/*` (Magic circles, AOE Magic spells) and `Assets/Spells Pack` are particle/VFX assets, not character motion.

**Nothing here is gitignored/absent on this machine** — all three motion packs and the 60 extracted clips are physically present.

---

## 2. Full inventory table

### 2A. Hero Motion — EXTRACTED (`…/Animations/Extracted/`, `.anim`, LIVE avatar)
Ready to bind by filename (drop the `.anim`). Length not parsed — "Quality" is qualitative from source take.

| Animation Name | Pack Source | Type | Recommended Use | Quality Notes |
|---|---|---|---|---|
| Combat_Weapon_WeaponSkill_Sword_And_Shield_Slash | Extracted | Attack (light) | **Basic (B1)** | Current `Attack0` + generic Cast. Clean one-handed S&S swing; the safe combo starter. |
| Combat_Weapon_WeaponSkill_Inward_Slash | Extracted | Attack (light) | Basic combo hit / Heavy | Left-to-right cross swing. Good combo-chain link. Currently `Cast_q`. |
| Combat_Weapon_WeaponSkill_Outward_Slash | Extracted | Attack (light) | Basic combo hit | Right-to-left backhand. Combo pair with Inward. Only reachable via JSON seam today. |
| Combat_Weapon_WeaponSkill_Downward_Slice | Extracted | Attack (heavy/overhead) | **Heavy (B2)** | Overhead chop — reads as a heavy. Currently `Cast_r`. Strong. |
| Combat_Weapon_WeaponSkill_Standing_Melee_Attack_360_High | Extracted | Attack (spin/AoE) | **Skill 1 / Cleave (B3)** | 360° high spin cleave — best "sweep everything" read. Currently `Cast_w`. |
| Combat_Weapon_WeaponSkill_GreatSword_Swing | Extracted | Attack (heavy) | Heavy alt (B2) | Big two-hand-style swing; heavier windup than Downward. Reachable via JSON seam. |
| Combat_Weapon_WeaponSkill_Sword_BackSwing | Extracted | Attack (backswing) | Combo finisher | **UNUSED.** Wide recovery swing — natural 3rd/4th combo beat. |
| Combat_Weapon_WeaponSkill_Swing | Extracted | Attack (light) | Combo hit | **UNUSED.** Generic swing; combo filler. |
| Combat_Weapon_WeaponSkill_Combo | Extracted | Attack (multi-hit) | Combo / finisher | Multi-hit combo take. Reachable via JSON seam. |
| Combat_Weapon_WeaponSkill_Stabbing | Extracted | Attack (thrust) | Combo (thrust) | Forward stab; good combo variety. Reachable via JSON seam. |
| Combat_Weapon_WeaponSkill_Fists | Extracted | Attack (unarmed) | — | **UNUSED.** Unarmed — off-theme for a S&S Knight. Skip. |
| Combat_Spell_Two_Hand_Spell_Casting | Extracted | Special (cast/buff) | **Skill 2 / Shout-Buff (B4)** | Two-hand buff/cast pose. Currently `Cast_e` (Oath Ward). Good "war-cry / ward" read. |
| Combat_Spell_Fireball | Extracted | Special (cast) | Spell skill (optional) | **UNUSED.** Directed cast — reserve if Knight ever gets a projectile skill. |
| Combat_Weapon_Combat_Movement_Locked_Standing_Aim_Idle_01 | Extracted | Locomotion (combat idle) | Combat idle | WIRED — `CombatLocomotion` idle @0. |
| Combat_Weapon_Combat_Movement_Locked_Standing_Aim_Idle_02_Looking | Extracted | Locomotion (combat idle) | Combat idle var | **UNUSED.** Idle variety for combat stance. |
| Combat_Weapon_Combat_Movement_Locked_Sheathing_Sword | Extracted | Special (sheathe) | Unsheathe (reversed) | WIRED — played speed −1 as the draw-weapon prebattle transition. |
| Combat_Weapon_Combat_Movement_Locked_Grab_And_Put_Back_Rifle | Extracted | Special | — | **UNUSED.** Rifle — off-theme. Skip. |
| Passive_Locomotion_Idle_2 | Extracted | Locomotion (idle) | Calm idle | WIRED — default idle @0. |
| Passive_Locomotion_Idle_3 / Idle_4 | Extracted | Locomotion (idle) | Idle variety | WIRED — timed idle variety states. |
| Passive_Locomotion_Walk | Extracted | Locomotion | Calm walk | WIRED — @2. |
| Passive_Locomotion_Run | Extracted | Locomotion | Calm run | WIRED — @6. |
| Passive_Locomotion_Walk_Backward | Extracted | Locomotion | Back walk | **UNUSED.** Add for strafe/back movement. |
| Passive_Locomotion_Motion_Standing_Walk_Forward | Extracted | Locomotion | Combat walk | WIRED — combat @2. |
| Passive_Locomotion_Motion_Standing_Walk_Left / Right | Extracted | Locomotion (strafe) | Strafe | **UNUSED.** For 2-D combat strafe blend. |
| Passive_Locomotion_Motion_Sword_And_Shield_Run | Extracted | Locomotion | Combat run | WIRED — combat @6. |
| Passive_Locomotion_Motion_Run_Forward_Left / Sprint_Forward_Right | Extracted | Locomotion | Sprint/strafe run | **UNUSED.** Directional run for 2-D blend. |
| Passive_Locomotion_Motion_Standing_Run_Forward_Stop | Extracted | Locomotion | Run stop | **UNUSED.** Deceleration polish. |
| Passive_Locomotion_Motion_Turn_90_Left / Passive_Reaction_Standing_Turn_Left_90 | Extracted | Locomotion (turn) | Turn-in-place | **UNUSED.** Turn-in-place polish. |
| Passive_Locomotion_Motion_Sword_And_Shield_180_Turn | Extracted | Locomotion (turn) | 180 turn | **UNUSED.** |
| Passive_Reaction_Hit_Reaction | Extracted | Reaction | Hit | WIRED — `Hit` state. |
| Passive_Reaction_Standing_React_Large_From_Left / Small_From_Right | Extracted | Reaction | Directional hit | **UNUSED.** Directional hit variety (HitDir int already exists). |
| Passive_Reaction_Dodging | Extracted | Reaction (dodge) | Dodge | **UNUSED.** Candidate for a dodge/roll input. |
| Passive_Reaction_Running_Dive_Roll | Extracted | Reaction (roll) | Dodge-roll | **UNUSED.** Strong dive-roll — good dodge button. |
| Passive_Reaction_Standing_Torch_Melee_Attack_01 | Extracted | Attack (torch) | — | **UNUSED.** Off-theme (torch). Skip. |
| Signature_Sweep_Fall | Extracted | Reaction (knockdown) | Knockdown | WIRED — `SweepFall` (Knockdown trigger, no driver yet). |
| Signature_Getting_Up | Extracted | Reaction (recover) | Get up | WIRED — pairs with SweepFall. |
| Signature_Taunt | Extracted | Special (taunt) | Taunt / Shout | **UNUSED.** Candidate for a shout/taunt skill (B4 alt). |
| Signature_* Deaths (Death, Death_From_Right, Death_Forward, Standing_Death_Left_01, Standing_Death_Backward_01, Two_Handed_Sword_Death_1, +duplicates) | Extracted | Special (death) | Directional death | WIRED — 6 directional deaths (see §3). Several extra death takes (`Passive_Death`, `Passive_Locomotion_Death`, `Combat_Spell_Death`, `Combat_Weapon_WeaponSkill_Death`, `Signature_Death_From_The_Front`, etc.) are **UNUSED** duplicates. |

### 2B. Sword and Shield Moves (`…/Motion/studio-mocap-sword-and-shield-moves/`, `.fbx` — NEEDS EXTRACTION)
Cleanest dedicated S&S set; recommend extracting the attack + shield + parry subset.

| Animation Name | Pack Source | Type | Recommended Use | Quality Notes |
|---|---|---|---|---|
| atk_slashright / atk_slashleft | S&S Moves | Attack (light) | **Basic combo 1–2 (B1)** | Purpose-built horizontal light swings — ideal opening two beats of a light combo. |
| atk_slashdown | S&S Moves | Attack (heavy/overhead) | Basic combo 3 / Heavy | Overhead — combo third beat or heavy. |
| atk_slashup | S&S Moves | Attack (rising) | Basic combo 4 / launcher | Rising cut — combo finisher / pop. |
| atk_stab | S&S Moves | Attack (thrust) | Combo (thrust) | Forward lunge stab; combo variety. |
| atk_spin | S&S Moves | Attack (spin/AoE) | **Skill 1 / Cleave (B3)** | Dedicated spin cleave — cleanest sweep in any pack. Prefer over extracted 360_High if extracted. |
| atk_shieldcharge | S&S Moves | Attack (charge) | **Skill 2 / Charge (B4)** | Shield-forward charge — best "gap-closer charge" read. |
| atk_shieldswipe01 / atk_shieldswipe02 | S&S Moves | Attack (shield bash) | **Heavy / Shield Bash (B2)** | Purpose-built shield bash — the correct clip for a "Shield Bash" button. |
| atk_kick | S&S Moves | Attack (kick) | Utility/interrupt | Front kick — stagger/interrupt option. |
| atk_jump | S&S Moves | Attack (leap) | Leap attack | Jump attack — optional special. |
| shield_block{up,down,left,right,backward,crouch} | S&S Moves | Block (directional) | Block/Guard | Full directional block set — upgrade over the shared-block package gap. |
| sword_parry{left,right,up,down,backward01–04,crouch} | S&S Moves | Block (parry) | Parry | Rich parry set — enables a real parry/counter mechanic. |
| idle_battle / idle_alert / idle_ready | S&S Moves | Locomotion (combat idle) | Combat idle | Native S&S combat idles — better than the aim-idle placeholder. |
| walkforward01/02, walkleft/right/back, runforward/back/left/right | S&S Moves | Locomotion | Combat gait | Full S&S walk/run set — native combat locomotion blend. |
| turnleft90/180, turnright90/180 | S&S Moves | Locomotion (turn) | Turn-in-place | Turn set. |

### 2C. Magical Moves — the CASTER pack (`…/Motion/studio-mocap-series-magical-moves/`, `.fbx` — NEEDS EXTRACTION)

**OWNER DIRECTIVE (2026-07-10): every CAST-type ability — heal, DoT/curse, ranged bolt, buff/shout, channel — MUST fire a real casting animation from THIS pack, never a sword swing.** Reserve §2A/§2B swings for melee only.

Naming key: `m-`=male take (prefer for the male Paladin), `f-`=female take (retargets onto the male rig but needs cleanup); `-h-`=one-handed conjure, `-ls-`=long-staff/two-hand cast, `-ss-`=sword-&-shield cast (casts WITHOUT stowing gear — ideal for a Knight), `magiccontrol`=channel/sustain, `standby/warning`=cast-stance idle + weapon-swap-to-cast.

| Animation Name | Pack Source | Type | Best caster ability fit | Quality Notes |
|---|---|---|---|---|
| m-h-magespellcast-01…06 | Magical Moves | Cast (one-hand conjure) | **Ranged bolt / DoT-curse cast** | Male, one-handed — short conjure→release read. Best for a quick ranged/hex cast that keeps the sword in hand. **6 variants** = per-spell variety. |
| m-ls-magespellcast-01…06 | Magical Moves | Cast (two-hand / big) | **Buff / shout / heal (big cast)** | Male, long-staff two-hand — bigger windup, "raise power" read. Best for a heal or party-buff moment. **6 variants.** |
| f-ss-magespellcast-01…08 | Magical Moves | Cast (sword & shield) | **In-combat cast (any) — Knight-ideal** | The ONLY sword-&-shield cast set (casts while holding sword+shield). **Female takes** — retarget onto Knight_Hero and QA; if clean, the single best fit for a Paladin who never stows gear. 8 variants. |
| f-h-magespellcast-01…04 / f-ls-magespellcast-01…04 | Magical Moves | Cast | Ranged / buff (female alt) | Female one-hand / long-staff casts — fallback variety if a male take doesn't fit a specific spell. |
| m-ss-magiccontrol-01 (f-magiccontrol-01/02) | Magical Moves | Channel (sustain) | **Channeled DoT / heal-over-time / beam** | Sustained "controlling magic" pose — the correct clip for a held/channeled ability (regen aura, drain, beam). |
| m-standby-idle / m-warning-idle (+ -swap) | Magical Moves | Cast-stance idle / swap | Caster idle & enter-stance | Idle in a caster stance + weapon-swap-into-cast transition. Use for the wind-up/hold between cast and release. |
| m-ss-damage-01…03 (f-damage-01/02) | Magical Moves | Reaction (spell-hit) | Caster hit reaction | Taking-damage-while-casting reactions — interrupt/flinch polish. |

### 2D. Caster clips ALREADY extracted (`…/Extracted/`, `.anim`, LIVE avatar — READY NOW)
These carry the `Combat_Spell_*` taxonomy from `HeroPackageImporter` (the same pipeline as every other extracted clip) — they are **NOT** from the Magical Moves pack; they were retargeted from the Mixamo/hero-motion source and are on disk & bindable today.

| Animation Name | Type | Best caster ability fit | Ready? |
|---|---|---|---|
| Combat_Spell_Two_Hand_Spell_Casting | Cast (two-hand buff/ward) | **Buff / ward / shout — B4 caster option** | **READY** (already bound as `Cast_e`). |
| Combat_Spell_Fireball | Cast (directed release) | **Ranged bolt / DoT projectile** | **READY** on disk, but **UNUSED** — no state binds it yet. |
| Combat_Spell_Death | Death (caster) | Death variety | READY, unused duplicate death. |

---

## 3. Already wired vs. unused (cited to `KnightPackageControllerBuilder.cs`)

**Controller:** `Assets/Editor/KnightPackageControllerBuilder.cs` builds `HeroPackages/Knight/Controller/KnightPackage.controller` + `Resources/Heroes/KnightPackage.prefab`.

### Bound today (extracted clips → controller states)
| Clip constant | Clip | State | Cite |
|---|---|---|---|
| ClipIdle / IdleVar3 / IdleVar4 | Passive_Locomotion_Idle_2/3/4 | Locomotion + IdleVariety3/4 | `KnightPackageControllerBuilder.cs:83-85`, `:199-211` |
| ClipWalk / ClipRun | Passive_Locomotion_Walk / Run | Locomotion blend @2/@6 | `:86-87`, `:199-200` |
| ClipCombatIdle/Walk/Run | Aim_Idle_01 / Standing_Walk_Forward / S&S_Run | CombatLocomotion blend | `:88-90`, `:203-205` |
| ClipUnsheathe | …Sheathing_Sword (reversed) | Unsheathe | `:91`, `:218-252` |
| ClipHit | Passive_Reaction_Hit_Reaction | Hit | `:92`, `:318-326` |
| **ClipBasicSlash** | **Combat_Weapon_WeaponSkill_Sword_And_Shield_Slash** | **Attack0 + generic Cast + upper-body overlay** | `:93`, `:273-300`, `:407-408` |
| ClipSweepFall / ClipGettingUp | Signature_Sweep_Fall / Getting_Up | SweepFall→GettingUp (Knockdown trigger — **no runtime driver**) | `:94-95`, `:329-342` |
| Death set | Locked_Death, Standing_Death_Left_01, Death_From_Right, Death_Forward, Standing_Death_Backward_01, Two_Handed_Sword_Death_1 | Death / DeathLeft/Right/Front/Back/Assassinate | `:98-103`, `:349-354` |
| **SpellCastClips[1..4]** | Inward_Slash, 360_High, Two_Hand_Spell_Casting, Downward_Slice | **Cast_q / Cast_w / Cast_e / Cast_r** | `:108-115`, `:302-315` |
| JsonClipToPackage (combo seam) | Outward_Slash, Stabbing, Combo, GreatSword_Swing, 360_High, Two_Hand_Spell, Downward_Slice | Attack1/Attack2 **only if** `weaponskill-animations.json` knight rows resolve | `:121-131`, `:273-286`, `:426-466` |
| Package-gap retargets | Shared_Victory_Pose.fbx / Shared_Block.fbx / Action/Enemies injured set | Victory / Block / InjuredLocomotion | `:134-142`, `:357-404` |

### Strong extracted clips sitting UNUSED (present on disk, never bound)
- **Combat_Weapon_WeaponSkill_Sword_BackSwing** — natural combo finisher, wasted.
- **Combat_Weapon_WeaponSkill_Swing** — combo filler.
- **Combat_Weapon_WeaponSkill_Outward_Slash** — only reachable via the JSON seam; not a first-class combo state.
- **Combat_Weapon_WeaponSkill_GreatSword_Swing** / **Combo** / **Stabbing** — JSON-seam-only, no guaranteed state.
- **Signature_Taunt** — no shout/taunt state.
- **Passive_Reaction_Dodging** / **Running_Dive_Roll** — no dodge state.
- **Combat_Spell_Fireball** — no projectile-cast state.
- Directional-hit and strafe/turn locomotion clips (Walk_Left/Right, Turn_90, 180_Turn, React_Large/Small) — unused polish.
- Whole **Sword and Shield Moves** + **Magical Moves** packs — **0% used** (not extracted).

### Stale slot labels to flag
- **WO-494 `SpellCastClips` labels are STALE** (`:107-115`): the comments call q/w/e/r "Shield Bash / Bulwark Slam / Oath Ward / Lantern Charge." Those names predate the current 4-button owner layout (Basic / Heavy-Bash / Skill1 / Skill2) and no longer describe the intended buttons. Treat the *clips* as the asset inventory, ignore the *labels*.
- The `weaponskill-animations.json` seam still names the **OLD `Assets/Action` clips** and remaps them (`:117-131`) — rows that don't resolve silently fall back to the basic slash, so `Attack1/Attack2` may currently be **identical to Attack0**. Worth verifying the JSON has knight combo rows.
- `Knockdown` trigger is declared with **no runtime driver** (`:22-23`, `:192`) — SweepFall/GettingUp are dead until something raises it.
- `Signature_Two_Handed_Sword_Death_1` assassinate mapping is **TENTATIVE** pending owner confirm (`:32`, `:103`, `:353`).

---

## 4. RECOMMENDED 4-button mapping (owner decides)

Owner layout: **B1 Basic Attack · B2 Heavy / Shield Bash · B3 Skill 1 (Cleave/Sweep) · B4 Skill 2 (Charge/Shout/Block)**.

Two tiers per button: a **Ready-now** choice (already-extracted `.anim`, controller can bind immediately) and a **Best-with-import** choice (dedicated S&S-Moves clip that needs an extract+retarget pass first).

| Button | Ready-now clip (on disk) | Path | Best-with-import clip | Source needing extraction |
|---|---|---|---|---|
| **B1 Basic Attack** (light / combo starter) | Combat_Weapon_WeaponSkill_Sword_And_Shield_Slash | `Assets/HeroPackages/Knight/Animations/Extracted/Combat_Weapon_WeaponSkill_Sword_And_Shield_Slash.anim` | atk_slashright (combo start) | `Assets/Action/Knight/Motion/studio-mocap-sword-and-shield-moves/atk_slashright.fbx` |
| **B2 Heavy / Shield Bash** | Combat_Weapon_WeaponSkill_Downward_Slice (heavy) — or Inward_Slash for a bash-read | `…/Extracted/Combat_Weapon_WeaponSkill_Downward_Slice.anim` | atk_shieldswipe01 (true shield bash) | `…/studio-mocap-sword-and-shield-moves/atk_shieldswipe01.fbx` |
| **B3 Skill 1 (Cleave/Sweep)** | Combat_Weapon_WeaponSkill_Standing_Melee_Attack_360_High | `…/Extracted/Combat_Weapon_WeaponSkill_Standing_Melee_Attack_360_High.anim` | atk_spin | `…/studio-mocap-sword-and-shield-moves/atk_spin.fbx` |
| **B4 Skill 2 (Charge/Shout/Block)** | Combat_Spell_Two_Hand_Spell_Casting (ward/shout pose) — or Signature_Taunt for a shout | `…/Extracted/Combat_Spell_Two_Hand_Spell_Casting.anim` (or `…/Signature_Taunt.anim`) | atk_shieldcharge (gap-closer charge) | `…/studio-mocap-sword-and-shield-moves/atk_shieldcharge.fbx` |

**Bottom line:** all four buttons can ship **ready-now** with zero import work — the clips exist as bound or bindable states (B1/B2/B3 are literally already `Attack0`/`Cast_r`/`Cast_w`). The **Best-with-import** column is the quality upgrade the owner should schedule: the *Sword and Shield Moves* pack has purpose-built bash (`atk_shieldswipe`), spin (`atk_spin`), and charge (`atk_shieldcharge`) that read far better than the generic melee takes.

> **Note:** the table above treats B1–B4 as **MELEE** actives (weapon strikes). B4 "Shout/Block" is a melee/pose action. Per the owner directive, if B4 (or any assignable skill-tree active) is instead a **CAST** ability, do **NOT** use a swing — bind it from the caster set in §4A below.

### 4A. CASTER abilities → Magical Moves pack (the melee/caster split)

**Hard rule (owner 2026-07-10):** melee buttons play sword-&-shield / hero-motion **swings** (§2A/§2B); every **cast-type** active — heal, DoT/curse, ranged bolt, buff/shout, channel — plays a **casting animation** (§2C/§2D), never a swing. This maps directly to the skill-tree respec direction (heal-regen + DoT + ranged as the smart single-player actives).

| Caster active | Ready-now clip (on disk) | Path | Best-with-import clip | Source needing extraction |
|---|---|---|---|---|
| **Ranged bolt** (e.g. spirit bolt) | Combat_Spell_Fireball (directed release — **currently unused, just needs a state**) | `…/Extracted/Combat_Spell_Fireball.anim` | m-h-magespellcast-01 (one-hand conjure→release) | `…/studio-mocap-series-magical-moves/m-h-magespellcast-01.fbx` |
| **DoT / curse** | Combat_Spell_Fireball (reuse until pack extracted) | `…/Extracted/Combat_Spell_Fireball.anim` | m-ss-magiccontrol-01 (channel) or m-h-magespellcast-03 | `…/studio-mocap-series-magical-moves/m-ss-magiccontrol-01.fbx` |
| **Heal / regen (HoT)** | Combat_Spell_Two_Hand_Spell_Casting (two-hand raise) | `…/Extracted/Combat_Spell_Two_Hand_Spell_Casting.anim` | m-ls-magespellcast-02 (big two-hand) or m-ss-magiccontrol-01 (channel for regen) | `…/studio-mocap-series-magical-moves/m-ls-magespellcast-02.fbx` |
| **Buff / shout / ward** | Combat_Spell_Two_Hand_Spell_Casting (already `Cast_e`) — or Signature_Taunt for a battle-shout | `…/Extracted/Combat_Spell_Two_Hand_Spell_Casting.anim` | m-ls-magespellcast-01 | `…/studio-mocap-series-magical-moves/m-ls-magespellcast-01.fbx` |
| **In-combat cast (Knight-ideal, keeps sword+shield)** | — (none extracted) | — | f-ss-magespellcast-01…08 (sword-&-shield casts) | `…/studio-mocap-series-magical-moves/f-ss-magespellcast-01.fbx` … `-08.fbx` |

**Ready vs. import — caster clips:**
- **READY NOW (on disk, retargeted):** only **two** true caster clips — `Combat_Spell_Two_Hand_Spell_Casting` (bound as `Cast_e`) and `Combat_Spell_Fireball` (**on disk but UNUSED — needs a state**). These are enough to ship a heal/buff + a ranged/DoT active immediately, distinct from every sword swing.
- **NEEDS IMPORT (extract + retarget onto `Knight_Hero`, mirror `HeroPackageImporter.ImportKnight`):** the entire **Magical Moves** pack (`…/studio-mocap-series-magical-moves/*.fbx`) — this is where the variety lives (6× one-hand, 6× two-hand, 8× sword-&-shield casts, channel, cast-idle/swap). The full pack does **not** live in `Extracted/`; it must be imported. Prefer the `m-*` (male) takes; the sword-&-shield cast set (`f-ss-magespellcast`) is female and needs retarget QA but is the single best Knight-in-combat cast read.
- The `Assets/Action/Wizard/*.fbx` casts (`Standing 2H Cast Spell`, `Wizard_Heal`, `Standing 2H Magic Attack`) are a **cross-class fallback** — also unextracted, lower priority than the dedicated Magical Moves pack.

### RECOMMENDED light-combo chain (B1 held/tapped repeatedly)
A 3–4 hit chain reads best from the dedicated S&S swings. Two options:

- **Ready-now (extracted, no import):** Sword_And_Shield_Slash → Inward_Slash → Outward_Slash → Sword_BackSwing (finisher).
  Paths under `…/Extracted/`: `Combat_Weapon_WeaponSkill_Sword_And_Shield_Slash.anim` → `…_Inward_Slash.anim` → `…_Outward_Slash.anim` → `…_Sword_BackSwing.anim`. (Outward_Slash + BackSwing are currently unused — this puts them to work; wire as `Attack0→1→2→3` on the `Combo` int, not via the JSON seam.)
- **Best-with-import (S&S Moves pack):** atk_slashright → atk_slashleft → atk_slashdown → atk_slashup (rising finisher).
  Source FBX under `…/studio-mocap-sword-and-shield-moves/`: `atk_slashright.fbx`, `atk_slashleft.fbx`, `atk_slashdown.fbx`, `atk_slashup.fbx` — extract+retarget onto `Knight_Hero` first (mirror `HeroPackageImporter.ImportKnight`).

---

## 5. Suggested follow-up work orders (not part of this research WO)
1. **Extract the Sword-and-Shield Moves attack/shield/parry subset** onto `Knight_Hero` (import pass), then rebind B2/B3/B4 + the combo chain to the dedicated clips.
1b. **Extract the Magical Moves caster pack** onto `Knight_Hero` (heal/DoT/ranged/buff/channel casts) so caster skill-tree actives fire real spell-cast animations, not swings — and **bind the already-on-disk `Combat_Spell_Fireball`** into a caster state now (it is extracted but unused).
2. **Author a real light-combo chain** (`Attack0→1→2→3` on `Combo`) instead of the JSON-seam fallback that can leave Attack1/2 == Attack0.
3. **Wire the `Knockdown` driver** or drop the dead SweepFall/GettingUp pair.
4. **Refresh the WO-494 `SpellCastClips` comment labels** to the current 4-button names.
5. Owner confirm the **TENTATIVE assassinate death** mapping.
