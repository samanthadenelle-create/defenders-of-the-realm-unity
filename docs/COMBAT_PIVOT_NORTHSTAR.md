# Combat & Hero Pivot — North Star (owner decision, 2026-06-22)

**Status: ACTIVE NORTH STAR.** This supersedes the multi-hero/companion + Blink-armor
direction. Every combat/hero/art change is checked against this.

## Why we pivoted
- **AI companions were a net negative.** They didn't heal or engage as hoped; "single or
  with the companions felt the same, just less." They added screen clutter + a constant
  source of bugs (companion body-render / armor bone-mismatch spam in the F8 logs), with no
  felt upside.
- **Skill points were never used.** A half-built progression with no payoff.
- **Owner dislikes the low-poly packs and the Blink armor look + rig.** The Blink armored-body
  swap also spammed `ShareBaseSkeleton FAILED` (bone-mapping failures).

Two half-built systems (skill points + companions) collapse into ONE real one: a hero skill
tree that does the healing + ranged the companions were faking.

## The new shape
1. **Single hero.** No party, no companion combatants, no pets in battle.
   - DONE (step 1): `ff.singlehero` (default ON) — `BattleController.BuildParty` surfaces only
     the hero. Companion/party + `StoryCompanionInjector` get retired (flag-gate, then delete).
2. **Hero ability kit (the feel).** Removing companions alone = "same, just less." Each of the
   hero's turns must offer a DECISION: basic ranged attack, a heal/sustain, a burst/control
   ability, optionally a reposition. With one unit, every turn is the whole turn.
3. **Wider skill tree — heal + ranged.** This is where the dead skill points finally pay off.
   Folds the existing perk-research work (WO-432 / WO-476) into a hero-facing tree that unlocks
   + upgrades the ability kit.
4. **Fewer, meatier enemies.** 1–3 telegraphed threats that punish bad timing (heal-through /
   kite / time the burst), not a crowd. Less AI, less perf, more readable.
5. **Closer camera — tight over-the-shoulder / near-first-person.** Makes the single hero the
   star and makes hits land visibly. **PIN NEEDED:** true first-person (see hands/weapon, barely
   render the body → less rigging) vs tight over-shoulder. Default assumption: over-shoulder.
6. **Tripo art, self-rigged.** Replace the low-poly hero (and over time the low-poly packs) with
   Tripo models the owner creates + rigs for full control. One hero rig = a fraction of the old
   party+enemy art burden.
7. **Blink armor JUNKED.** DONE: `ff.blinkarmor` (default OFF) — `HeroArmorVisual` is inert (no
   swap, no bone-mapping, no error spam). Armor/cosmetics move to the Tripo direction.

## Combat staging + camera (owner 2026-06-22): a DEFINED battle anchor, not free-roam
ATB plays well with the camera "sitting back" ONLY if combat is a composed TABLEAU, not free movement
(the genre rule — FF/Persona/Octopath). So define a single **battle anchor / attack point**:
- Fixed HERO stance position + fixed ENEMY stance position(s) + a camera rig composed on that engagement.
- Define it ONCE; every fight snaps to it. Action = ability choices + watching the staged animations.
- Why: a consistent composed frame is what makes animation-as-mechanics READABLE (every wind-up /
  shield-block / hit reads the same legible way). Free camera would undercut it. Also massively simpler
  to build — no combat nav, no camera chase.

**Camera resolution (settles the earlier FP pin):**
- COMBAT = composed THIRD-PERSON battle stage (sitting back, framing hero + enemy). NOT first-person —
  FP suits twitch action, not turn-paced ATB where you must see both sides.
- EXPLORATION = a separate follow cam (walking to the fight).
- Flow: explore (follow cam) -> reach enemy -> combat STAGES at the battle anchor (hero+enemy snap to
  stances, camera composes) -> ATB resolves -> back to exploration.
- Keep a little life (slight push-in on your turn, punch-in on big hits / killing blow) so the locked
  frame isn't lifeless — but the ANCHOR stays defined + consistent.

The battle anchor is the highest-leverage thing to PROTOTYPE first: it's the frame everything else
(animation, telegraphs, shield-block, ability VFX) gets composed inside.

## THE UNIFYING PRINCIPLE (owner, 2026-06-22)
**You directly control exactly ONE thing — the hero. Everything else is AUTONOMOUS. Allies exist only
where autonomy is a feature, never something you micro.** This explains every call:
- Companions-as-followers (micro'd) = BUST. Troops-as-auto-defenders (autonomous) = good.
- Pets-as-ATB-combatants (micro'd) = bust. Echoes-as-autonomous-harvesters = good.
Anything that requires the player to babysit a second unit in live combat is OUT. Allies are systems
you SET UP and then WATCH/benefit from.

## Pets -> Echoes / Spirits of the Tree of Life (owner, 2026-06-22)
Pivot pets into **echoes/spirits released from the Tree of Life (the Heart of Elarion, world-tree at
center)** — autonomous beings that **harvest resources** (and "assist somehow" — define later). Pets
failed as ATB combatants; as autonomous harvesters, autonomy IS the feature (same pattern as
companions->troops). Lore writes itself: the world tree releases life that gathers.
- **Economy — three resource streams:** raid loot (active), wave rewards (trigger+watch), echo harvest
  (passive/idle while away). Feeds "one feeds the other."
- **LIFE FORCE = the keystone that links offense -> economy (owner 2026-06-22).** Driving the enemy
  BACK strengthens the Tree of Life's life force; the stronger the life force, the FASTER/MORE the echoes
  harvest. So offense becomes a PERSISTENT WORLD STATE, not transactional loot: every outpost cleared
  permanently raises your harvest. Reframes the game as RECLAIMING THE WORLD (darkness recedes -> tree
  heals -> more/stronger spirits). Lore + math are the same sentence (enemy encroachment weakens the
  tree; pushing back heals it; a healed tree births stronger life). Gives the region/territory system
  (WO-453, outpost convert) a real job: reclaimed territory = life force = harvest rate; pushing the
  front line back IS progression. Keep the meter LEGIBLE: life force = f(outposts cleared / territory
  reclaimed); harvest rate (then maybe echo count) scales off it. One meter, one cause, one effect.
- **GROWING WORKFORCE — ONE ECHO PER RESOURCE (owner 2026-06-22):** echoes AUTO-find & gather so the
  player NEVER hand-gathers (manual gathering = tedious filler, removed). Each echo automates ONE
  resource stream. RESOURCES = EXACTLY THREE (owner locked 2026-06-22): WOOD, IRON, GRAIN. No essence,
  no crystal — grounded settlement resources, instantly legible, no fantasy-currency creep. So EXACTLY
  3 echoes: echo 1 = wood; next life-force threshold -> echo 2 = +iron; next -> echo 3 = +grain. Each
  has a DISTINCT job so all three matter: wood -> structures/building upgrades; iron -> weapons/shield/
  armor (hero gear); grain -> troops/upkeep (feed the autonomous defenders). (Gold/coin stays the
  separate STORE currency; wood/iron/grain are the GATHERED ones.) Every new echo is a real event. The tree is a LIVING PROGRESS MONUMENT —
  you SEE spirits multiply around a brighter tree (emotional feedback + return-hook: it works while away).
  TWO scaling axes off the same life-force meter: (a) RATE — existing echoes gather faster as life force
  grows; (b) BREADTH — thresholds birth a new echo = new resource TYPE. Feeds CRAFTING: wood+iron are the
  inputs to weapon/shield upgrades + skill-tree costs, closing the chain (reclaim -> life force -> echo ->
  resource -> gear/skills -> reclaim further). SCOPE: define the resource list FIRST (sets echo count +
  recipes + costs — the economy spine). Keep the sim ABSTRACT — "echo finds wood" = a RATE + flavor
  movement (a spirit drifting out + back), NOT a node-discovery sim. Render the flavor, fake the sim.
- **Phasing (refined):** echoes + life force are the **V1 offense economy hook** (clear territory ->
  life force up -> harvest up -> fund the skill tree) — a LIGHT version ships in V1 so V1 offense has a
  living, responsive economy. The heavier autonomous-being MANAGEMENT layer (echo types/bonds/upgrades/
  many) + the base-DEFENSE build stay V2-gated, only if they show value.
- **Scope:** start dead simple — tree releases N echoes, passive resource gen. No echo-sim until earned.
- **Cleanup:** retires PetSelect onboarding (#19 bypass) + pet-ATB (already dormant under ff.singlehero).
- **NOTE:** an echo system already EXISTS — be SME on it before "redo better" (no blank-slate guessing).

## Loop reward swap
The outpost loop's reward was "unlock the next companion" (`Village2RaidController` /
`RaidVictoryController`). With no companions, the reward becomes **skill points / gear** so the
loop stays closed and feeds the skill tree. (Load-bearing for WO-475 convert-on-clear.)

## What gets retired (flag-gate first, delete once proven)
- Party/companion combatants in ATB (`ff.singlehero`).
- `StoryCompanionInjector`, companion follow/roster, companion-render verification.
- Blink armored-body swap (`ff.blinkarmor` off) + the `blink_armor_*` addressables for the hero.
- Pet-in-battle members.
- The "unlock next companion" reward path → re-point to skill points/gear.

## Open pins for the owner
- **Camera:** true first-person vs tight over-shoulder?
- **Low-poly:** Tripo hero now — do the low-poly enemies/world stay for a while, or purge?
- Confirm: this is the north star (rewrite the affected docs/catalog), not a trial branch.

## Finalized equipment model (owner, 2026-06-22)
**ONE polished hero model, no mesh swapping ever.** Equipment is:
- **Weapon + Shield = the VISIBLE flair / upgrade slots.** These read great on a Knight under a
  close camera and carry the visible progression (and let us finally fix the weapon grip, WO-435).
  The shield also drives the **block mechanic** (see animation note).
- **Armor = STATIC** — baked into the one model. Stat value only (no visible change). No Blink swap.
- **2 Rings + Amulet + Boots = INVISIBLE ability/stat slots.** They grant abilities/stats, no mesh.
  This is the build-depth layer (classic ARPG accessory model) that feeds the ability kit + skill tree.

We lose only buggy, disliked visual armor-swapping; we gain stability, full art control, and build
depth at near-zero art cost. (Item model already supports this — docs/ITEM_MODEL.md capability flags.)

## Animation is now MECHANICS, not flair (owner insight, 2026-06-22)
With 1 fight at a time + the close camera, every animation cycle is SEEN and has high value:
- **Feel:** each hit has weight because you watch it land.
- **Mechanics:** the enemy attack WIND-UP is a readable telegraph → the player reacts (shield-block /
  heal / time the ranged poke). The shield-block clip is a *mechanic*, not decoration.
- Economical: a tight reused clip set (hero: idle/attack/shield-block/hit-react/heal-cast/ranged-cast/
  death; same core per enemy type), one Tripo rig animated once + retargeted. Highest-leverage art.

## Hero scope: KNIGHT first (owner, 2026-06-22)
Do ONE class well, then fold in the others. `ff.knightonly` (default ON) forces the class to Knight
(GameStateService.ChooseHero). Build the whole single-hero loop — kit, skill tree, art, animation,
weapon/shield flair — around the Knight; generalize the class once it's polished.

## PHASING — V1 = offense only; base/CoC defense is gated to V2 (owner, 2026-06-22)
Both modes fully exist EVENTUALLY and one feeds the other (offense earns resources -> build base ->
base protects/generates -> funds more raiding). But the base layer is a **gated phase-2 feature, NOT
the first version** — it can't come first because it has nothing to feed on until the offense loop is
real and generating something worth protecting. Dependency order, not compromise.
- **V1 (build now, polish):** solo Knight OFFENSE — raid outposts, ATB, skill tree, weapon/shield
  flair, rewards (skill points / gear / resources). Hub (castle) = hero home + skill tree + raid launch.
- **V2 (gated `ff.basebuilding` OFF — REVISIT ONLY IF IT SHOWS VALUE):** build base defenses, troops
  auto-defend, the watch/continue raid-on-base event, resources feed base-building. Existing barracks /
  WaveManager / towers / GarrisonController AI sit DORMANT (flag-gated) until/unless V2 is greenlit.
  NOT a commitment to build — gate it, polish the offense loop, then let evidence decide.

**`ff.basebuilding` (default OFF) also gates convert-on-clear (WO-475):** "cleared outpost -> your
base" IS base-creation, so it waits behind this gate. In V1 the outpost reward is **skill points /
gear**, not a base. The critical path stays: raid -> get stronger -> raid harder. No base machinery on it.

## Two-mode game + watch/continue (the V2 base layer, owner 2026-06-22)
The game is two loops that feed each other, and allies exist ONLY where autonomy is the point
(nothing micro'd in live combat — that's what made companions a bust):

- **OFFENSE — solo Knight.** Active, player-controlled, ATB + skill tree + weapon/shield flair.
  Raid enemy outposts (the existing outpost loop: port -> clear -> convert).
- **DEFENSE — your base, CoC-style.** Troops you train (barracks / WO-432) + towers AUTO-defend.
  You do NOT lead them. The hero is NOT in the defense.

**Defense is ORGANIC + PLAYER-TRIGGERED, not scripted/random (owner refinement 2026-06-22).** RIP OUT
the current auto/scripted `WaveManager` waves. New model: you START in town, build up troops + defenses,
then YOU choose to TEST them by triggering waves of enemies — **the more waves you trigger, the further
they push in** (a self-paced siege/horde test, escalating risk->reward). You never LEAD the troops (they
auto-defend, you watch); your agency is build -> choose to test -> choose how hard -> read the weak points
-> upgrade -> test deeper. Replayable in a way scripted waves never were. (Reuses WaveManager re-pointed
from auto-spawn to player-triggered escalating; towers; GarrisonController defenders.)

**FULL ECONOMIC WEB (owner 2026-06-22):** as you build defenses stronger + repel stronger waves with
AUTOMATED troops (from town + defense upgrades), the MORE rewards come out. So the defense loop scales
with investment, mirroring the offense/life-force scaling. The complete web:
- SOURCES (in): hero raids -> life force -> echo harvest (passive); player-triggered waves -> rewards
  (scale with defense/troop strength).
- SINK (out): UPGRADES — town, defense, troops, hero gear, skill tree.
- AMPLIFIER: upgrades strengthen the sources (stronger defense -> harder waves -> more rewards; stronger
  hero -> more territory -> more life force -> more harvest). Sink feeds back into sources; investment compounds.
- Every PRODUCER is autonomous (echoes harvest, troops defend); only the hero is directly controlled.
  Troops get STRONGER via upgrades but still fight themselves.
PHASING HOLDS: V1 = the LEFT half (hero offense -> life force -> light echo harvest -> hero upgrades) —
whole + shippable on its own. V2 (gated, earn-it) = the RIGHT half (town/defense/troop upgrades,
escalating triggered waves, wave rewards). Balance/number-tuning is a LATER problem; the design is coherent.

**EACH WAVE DROPS REWARDS (owner 2026-06-22)** — this is the engine that makes the loop worth doing.
Cleared wave -> loot; deeper waves -> bigger loot. This is the "one feeds the other" resource source
(rewards -> upgrade defenses + feed the hero -> push deeper -> more rewards). For the decision to STOP to
be real, pushing deeper must carry PRESS-YOUR-LUCK risk: if your line breaks you lose some/all unbanked
wave rewards (and/or take base damage). "Bank what I've got or push for one more?" every wave — the same
tension as a roguelike cash-out. Without the risk bite, players just always push to the end.

**Watch-or-continue (for the away-raid case):** if a base attack can also fire while you're out raiding,
a NON-BLOCKING in-world prompt — "Your village is under attack. [Watch] / [Continue your journey]" — the
defense is autonomous so it resolves the SAME either way:
- **Continue** -> auto-resolve the battle (troop-vs-raid math via the headless sim infra), apply
  result, notify.
- **Watch** -> render that same result playing out (spectator camera over the base).

REQUIREMENTS for it to work:
1. **Identical outcome watched vs skipped** — "resolve the math, optionally render it." Watching
   must never change the odds, or skipping is a penalty.
2. **Real stakes** — looted resources / building damage — so the choice matters and the *intel*
   from watching ("trolls wreck my east towers") drives upgrades. That's the CoC hook.

Scope = **watch-defend first.** No forced base-management; add base-layout/economy depth only if
it earns it.

REUSE (mostly wiring, little new): barracks/troop-upgrade (WO-432) = the defenders; WaveManager +
towers + HeartController = the raid-on-base loop; **GarrisonController + EnemyBrain (the enemy-
stronghold defender AI we already built) flipped to FRIENDLY defenders** = the base AI; the
headless battle sim = the auto-resolve.

## Done so far (2026-06-22)
- `ff.singlehero` (ON) — single-hero ATB party. Commit 07ada028.
- `ff.blinkarmor` (OFF) — Blink armor junked (HeroArmorVisual inert; kills ShareBaseSkeleton spam).
- `ff.knightonly` (ON) — hero locked to Knight (ChooseHero forces it).
