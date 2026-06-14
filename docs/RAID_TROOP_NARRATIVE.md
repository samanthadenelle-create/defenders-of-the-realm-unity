# Raid & Troop Narrative — Draft (Barracks, Briefings, Deploy, Echo Bond Shards)

**Status: NARRATIVE DRAFT — creative content only. DO NOT wire into the live Yarn
project yet.** The C# command hooks referenced below (`ShowTrainingUI`, `StartTraining`,
`ShowArmyOverview`, `BeginRaid`, `Deploy`, `Retreat`, `GrantBondShard`, etc.) **do not all
exist yet** — they are placeholders for a later wiring pass. This doc captures the *voice*
for WO-453 (troops) + the Raid Pillar (RAID_PILLAR_VISION.md) so the lines are written
once, in canon, and slotted into `.yarn` when the bridge commands land.

**Canon held (do not contradict):** village = **Elarion** · centerpiece = the **Heart of
Elarion** (the singing stone reliquary / spire over the burned Heart-Tree's stump; the
realm's last held note) · the enemy = the **Hollow Ones / the Hollow**, born of the
**Withering** · Choristers hold the chord, no king, no Keep · companions Thrain (Wizard),
Grom (Knight), Sylas (Ranger), Elara (Healer) — heroes ARE companions · **Echoes** =
spirit-bound companions attuned through the Heart (reverent, not pets); the verb is
**attune** · **Troops are finite and expendable; Echoes/hero/companions are persistent**
(WO-453 identity line). Tone = mobile-RPG, every noun fits on a banner.

**Voice reference:** matches `NPC_Forge.yarn` (Borin) — terse, grizzled, lore-anchored,
command-first nodes, stage-aware `<<if>>` greetings.

---

## 1. The Barracks-master — **Drillmaster Hedda Stonecroft**

**Name:** Hedda Stonecroft. **Role:** keeper of the Barracks; she trains "the line" — the
finite, mortal soldiery of Elarion (as opposed to the spirit-Echoes of the Hollow). A
Chorister's daughter who put down the hymn-book and picked up a drill-staff when the
Withering came. Grizzled, blunt, counts heads like a miser counts coin — **warm under it:**
she grieves every troop that doesn't walk home, and she means it when she says "train them
proper or don't waste their lives." She calls trainees "pups," veterans "the old line,"
and the player "Warden."

Yarn variables assumed (declare later): `$army_count`, `$army_cap`, `$army_full`,
`$has_trained_before`, `$wounded_count`, `$training_in_progress`, `$barracks_tier`.

---

### Node: `Barracks_FirstVisit` (intro — one-time)

```yarn
title: Barracks_FirstVisit
tags: barracks intro
---
// commands pending — wire later. First-visit only; set $has_trained_before downstream.
<<portrait Portraits/barracks>>
Hedda: <i>She doesn't look up from the whetstone.</i> So you're the Warden they keep singing about. Hm. Hands look soft.
Hedda: Heart of Elarion holds the note, Warden. But a note won't break a Hollow gate. That takes a line — flesh, iron, and somebody willing to bleed for the wall.
-> The Echoes fight for me already.
    Hedda: Echoes are spirit — they come back. <i>She taps the bench.</i> These don't. My pups are mortal as you and me. You spend them, they stay spent. So we don't spend them cheap.
-> Teach me how the line works.
    Hedda: Simple. You bring me wood, iron, and patience — I give you Footmen and Archers. They train while you sleep. They die when you're careless. Train them proper or don't waste their lives.
-> What do you need from me?
    Hedda: A reason to fire the forge-pit. Bring resources, I'll bring you soldiers. The rest is up to how you lead 'em.
Hedda: <i>She finally looks up.</i> The line's open, Warden. Let's see what you make of it.
<<jump Barracks_MainMenu>>
===
```

---

### Node: `Barracks_MainMenu` (hub — greeting varies by army state)

```yarn
title: Barracks_MainMenu
tags: barracks
---
<<portrait Portraits/barracks>>

// — Greeting shifts with the state of the line. —
<<if $training_in_progress>>
    Hedda: Forge-pit's lit and the pups are sweating. Come back when they're seasoned — or don't, I'll send word when they're ready.
<<elseif $army_full>>
    Hedda: Barracks is full, Warden — {$army_count} of {$army_cap}, not a cot to spare. Take 'em to a raid and earn the room before you ask for more.
<<elseif $wounded_count > 0>>
    Hedda: <i>Quiet.</i> {$wounded_count} came home on shields last raid. They'll mend — wounded, not dead, and that's down to you pulling them when you did. What now?
<<elseif $army_count == 0>>
    Hedda: Empty barracks, Warden. A wall with no line behind it. Let's fix that.
<<else>>
    Hedda: {$army_count} of {$army_cap} standing and ready. The old line's holding. What'll it be?
<<endif>>

-> Train the line. <<if not $army_full>>
    <<jump Barracks_TrainMenu>>
-> Train the line. <<if $army_full>>
    Hedda: Full house, Warden — no room to drill. Thin 'em in a raid first, then we talk recruits.
-> Show me the army. <<if $army_count > 0>>
    <<jump Barracks_ArmyOverview>>
-> Upgrade the Barracks.
    // command pending — wire later
    <<command>>OpenUpgrade("barracks")<</command>>
    Hedda: A bigger pit means a longer line and a faster drill. Costs you, mind. Worth it.
-> Nothing today.
    Hedda: Keep your blade sharp and your line fed. Off with you.
===
```

---

### Node: `Barracks_TrainMenu` (committing resources to the line)

```yarn
title: Barracks_TrainMenu
tags: barracks train
---
<<portrait Portraits/barracks>>
Hedda: Tell me what the wall needs. Footmen take the blows; Archers thin 'em before they land. A wise line carries both. #lastline

// commands pending — wire later. ShowTrainingUI() surfaces the count/cost picker;
// the bare <<StartTraining "id" count>> lines below are the placeholder commit verbs.
-> Footmen — bodies for the front. <<if not $army_full>>
    <<command>>ShowTrainingUI("footman")<</command>>
    <<StartTraining "footman" 5>>
    Hedda: Five Footmen in the pit. They'll be sore and ready by the time you need 'em. Bring me five MORE of a kind and they drill faster — a squad's got something a stray pup doesn't.
-> Archers — eyes and arrows. <<if not $army_full>>
    <<command>>ShowTrainingUI("archer")<</command>>
    <<StartTraining "archer" 5>>
    Hedda: Archers it is. Slower to season than Footmen, but worth the wait when a Dragon's overhead and you've nothing else that reaches.
-> Open the full drill-board. <<if not $army_full>>
    // Lets the player pick type + count + see cost/timer in the code UI.
    <<command>>ShowTrainingUI("")<</command>>
-> The barracks is full. <<if $army_full>>
    Hedda: No room, Warden. The line's at {$army_cap}. Go make some widows of the Hollow and free up the cots.
-> Back.
    <<jump Barracks_MainMenu>>
===
```

---

### Node: `Barracks_ArmyOverview` (his recruits — the standing line)

```yarn
title: Barracks_ArmyOverview
tags: barracks army
---
<<portrait Portraits/barracks>>
// command pending — wire later. ShowArmyOverview() opens the roster panel
// (counts, veterancy ranks, wounded). Hedda narrates over it.
<<command>>ShowArmyOverview()<</command>>

<<if $army_count == 0>>
    Hedda: Nothing to show. Empty cots and a cold pit. Go on, train me a line.
<<else>>
    Hedda: <i>She walks the rank, naming them under her breath.</i> {$army_count} souls, Warden. Every one of 'em trusts you to bring 'em home.
<<endif>>

<<if $wounded_count > 0>>
    Hedda: {$wounded_count} are mending in the back — half-strength 'til they're whole. Don't march the wounded into a hard raid to die proper. They earned their rest.
<<endif>>

-> Who are the veterans?
    Hedda: The old line — the ones who walked back from a raid still breathing. They hit harder every time they survive. Keep a veteran alive and you've got something worth more than its rebuild cost. That's the whole game, Warden: a line you'd hate to lose.
-> Why keep them so few?
    Hedda: Because a hundred green pups is a hundred funerals. Ten you've trained, blooded, and brought home? That's an army. Quality over the crowd — always has been.
-> Back.
    <<jump Barracks_MainMenu>>
===
```

---

## 2. Raid Briefings — escalating dread, Regular → Extreme

Briefing voice = **Hedda + the Scout** between them (she reads the scout-report, warns the
comp). Each hints the soft counter (walls→siege, choke→melee+banner, open→ranged,
air→AA/archers) without spelling out a tutorial. Spoken on the pre-raid Scout screen.

```yarn
title: RaidBrief_RaiderOutpost
tags: raid brief regular
---
// Raid 1 · Regular / tutorial · raider_camp_small · 3★ under 4:30
<<portrait Portraits/scout>>
Hedda: Bandits. Hollow-touched, but bandits — a wood palisade and a hired mage who's already regretting it. Two gaps in the wall; they'll funnel you and try to swarm the breach.
Hedda: Bring Footmen to soak the gate, Archers to clear the wall-tops before they loose. Nothing clever. Earn your footing here.
-> Read me the line.
    Hedda: Twelve, maybe sixteen. Corner archers, a soft mage in the middle. A clean start, Warden — but a start's where bad habits set. Do it right.
-> I'm ready.
    <<jump RaidDeploy_Common>>
===

title: RaidBrief_FortifiedGarrison
tags: raid brief hard
---
// Raid 2 · Hard · fortified_garrison · 3★ under 5:30
<<portrait Portraits/scout>>
Hedda: This one's no camp. A garrison — Wood AND Stone, one gate, and they WANT you at it. Archers on the walls, mages laid for crossfire. They'll let you in and rake you from two sides.
Hedda: A single gate means a single throat to choke. Mass at it, don't dribble in — and if that Stone wall holds you, you'll wish you'd brought something heavier than a sword to it.
-> How many hold it?
    Hedda: Two dozen and change. Archers up high, warriors at the gate, mages dug in the center where you can't reach without bleeding. They've thought about this. So think harder.
-> Sound the advance.
    <<jump RaidDeploy_Common>>
===

title: RaidBrief_MageEnclave
tags: raid brief extreme
---
// Raid 3 · Extreme · mage_enclave · 3★ under 7:00
<<portrait Portraits/scout>>
Hedda: <i>She's quiet a long moment.</i> The Hollow Spire. Don't let the word "enclave" soften it. Stone and Obsidian, walls inside the walls, a kill-zone for a courtyard. Arcane towers ring a shielded core — overlapping fire, nowhere that isn't somebody's range.
Hedda: And the necromancer at the heart of it raises your fallen against you. Every pup you lose, you fight twice. So you do NOT lose them carelessly here — you pull them the moment the read goes bad. Living to fight again is the only victory that compounds.
-> Tell me true — can it be done?
    Hedda: Forty of them, maybe more. Heavy mages, elite steel, and they move like one mind. It can be done, Warden. By someone who's earned the other two and learned what the Retreat horn is for. Not by a fool in a hurry.
-> <i>Nod. Move out.</i>
    <<jump RaidDeploy_Common>>
===
```

---

## 3. Deploy + The Moment — commit, retreat, victory

```yarn
title: RaidDeploy_Common
tags: raid deploy
---
// commands pending — wire later. The deploy/rally/retreat verbs are the mid-raid
// interaction loop (WO-453): Deploy-point drops the army, Rally-flag re-targets it,
// Retreat saves survivors. These nodes carry the VOICE that frames each verb.
Hedda: The line's yours now, Warden. I trained 'em. Leading 'em home is on you.
-> Deploy the army.
    <<command>>BeginRaid()<</command>>
    Hedda: Mark your ground and drop the line — Footmen first, Archers behind. Plant the Rally banner where you want them, and they'll answer it as one. Now GO.
-> One more breath.
    Hedda: Take it. Then don't waste theirs.
===
```

**In-raid one-liners** (HUD/banner barks — not a node, fire on the verb):

- **On Deploy-point drop:** *"The line is set. Hold the breach!"*
- **On Rally-flag planted:** *"Banner's up — on me, all of you, ON ME!"*
- **Wounded troop falls back:** *"One down — he's breathing, get him out!"*
- **Army at half strength:** *"Half the line's gone, Warden. Read it true — push or pull."*

```yarn
title: RaidRetreat
tags: raid retreat
---
// Retreat = honor in living to fight again. Saves survivors, forfeits stars (WO-453).
<<command>>Retreat()<</command>>
Hedda: <i>The horn sounds, low and long.</i> Fall BACK — every soul that can still run, RUN!
Hedda: <i>After, quietly.</i> No stars today. But the old line walks home, and a soldier who lives is worth ten you'd have buried. Knowing when to pull them — THAT'S the read, Warden. That's command. We rebuild, and we come back meaner.
===

title: RaidVictory_Clean
tags: raid victory
---
// Clean clear — full timer, 3★ pace. The triumphant beat.
<<command>>CompleteRaid()<</command>>
Hedda: <i>The Hollow banner comes down off the keep.</i> CLEAR. Gate to core, under time, and the line's still STANDING.
Hedda: That's not luck, Warden — that's a soldier who learned to lead. The veterans'll be insufferable about it for a week. Let 'em. They earned the bragging.
-> Count the cost.
    Hedda: Lighter than it had any right to be. The ones you brought home are harder for it now — blooded, ranked up, worth more than the iron it'd take to replace 'em. THIS is how an army's built. One clean fight at a time.
-> <i>Raise the horn.</i>
    Hedda: <i>She grins, finally.</i> Aye. Let Elarion hear it.
===
```

---

## 4. Echo Bond Shard — the 3★ emotional payoff (the family thread)

The 3★ drop is an **Echo Bond Shard** — NOT a troop. It feeds the persistent companion
chase (extends `Pet.cs` bond ranks). This is the warm beat. The family Echoes — **Train
Echo** (built from a child's drawings; tanky, taunt) and **Big Boy 4014** (train-themed
legendary) — are personal canon. Written warm. Hedda hands it to the player; the Echo
Warden (from the Echo Hollow) speaks the bonding.

```yarn
title: RaidReward_BondShard
tags: raid reward echo shard
---
// Fires ONLY on a 3★ clear (skill-gated, never bought). Grants a bond shard toward an
// Echo. command pending — wire later.
<<command>>GrantBondShard()<</command>>
Hedda: <i>She turns something over in her palm — a sliver of light, warm as a coal.</i> Three stars. Clean as it gets. And THIS came up out of the rubble — a Bond Shard. The Heart's own light, shook loose by the way you fought.
Hedda: I train the mortal line, Warden. This? This is for the ones that don't stay dead. Take it to the Echo Hollow. It belongs to the bond.
-> <i>Take the shard to the Hollow.</i>
    <<jump EchoBond_Shard>>
===

title: EchoBond_Shard
tags: echo bond shard
---
<<portrait Portraits/pet-house>>
// command pending — wire later. ShowBondCollection() opens the per-Echo progress bars;
// ApplyBondShard("<echo-id>") commits the shard to the chosen Echo's bond.
<<command>>ShowBondCollection()<</command>>
Echo Warden: <i>The shard rises off your palm before you offer it.</i> The Heart remembers what you carried home. Such a thing is only ever EARNED — never bought, never given lightly. Whose bond shall it strengthen?

// — The family thread. Keep these. Written warm. —
-> The Train Echo.
    <<command>>ApplyBondShard("train-echo")<</command>>
    Echo Warden: <i>A small spirit clatters forward — all crayon-bright lines and a wobbling smile, the way a child first drew it.</i> The Train Echo. Folk laugh, 'til it plants itself between them and the Hollow and will not move. It was loved into being, Warden. That's why it's the bravest of them. The bond grows.
-> Big Boy 4014.
    <<command>>ApplyBondShard("big-boy-4014")<</command>>
    Echo Warden: <i>Far off, a long low whistle rolls across Elarion, and the ground hums.</i> Big Boy 4014 stirs. The great one — a legend that runs on heart and steel and a name a child gave it. Shards like these are how it wakes, piece by piece. Keep them coming, and one day it answers in full. <i>quietly</i> That day will be something to see.
-> Let the Hollow choose.
    <<command>>ApplyBondShard("")<</command>>
    Echo Warden: Then the Heart will guide it to the bond that needs it most. Wisely done.
Echo Warden: Every shard a little more of them made whole. Go earn another, Warden — they're worth the earning.
===
```

---

## 5. Conditional flavor variants (life in the lines)

Drop-in `<<if>>` variants for when state makes the line land harder. Already used above;
collected here as the pattern to reuse when wiring.

```yarn
// — Army-full nag (Hedda won't recruit into a packed barracks) —
<<if $army_full>>
    Hedda: Full to the rafters, Warden — {$army_count} of {$army_cap}. I'll not stack 'em like cordwood. Spend some in a raid first.
<<endif>>

// — Returning after losses (warmth under the gruff) —
<<if $wounded_count > 0 and not $training_in_progress>>
    Hedda: The mending tent's full and the pit's cold. Give the old line a day, Warden — they bled for the wall, they've earned the breath.
<<endif>>

// — First raid, no troops trained yet (gentle gate, not a wall) —
<<if $army_count == 0>>
    Hedda: You'd raid the Hollow with what — harsh language? Train me a line first. Even a Warden can't take a keep alone.
<<endif>>

// — Veteran-heavy army (the earned power-fantasy beat) —
<<if $veteran_count >= 5>>
    Hedda: <i>She looks the rank over, almost proud.</i> Half this line's walked back from a raid still breathing. That's not pups anymore, Warden — that's the OLD line. The Hollow ought to be the ones afraid.
<<endif>>

// — Staked raid pending (post-grant SKR layer; framed as proven skill, not gambling) —
<<if $raid_staked>>
    Hedda: You've beaten this one dry, and now you're racing it for stakes. <i>She nods slow.</i> That's not a gamble — that's confidence with a wager on it. Clear it clean and the spoils are worth the nerve.
<<endif>>
```

---

## Wiring notes (for the later pass)

- **Command hooks are placeholders.** Confirm real verb names against `NPCCommandBridge` /
  `DialogueCommandBridge` when the troop+raid C# lands. Likely real verbs: a `Barracks`
  routing entry (cf. `DialogueService.PlayStructure`), training-UI open, raid begin/retreat/
  complete, bond-shard grant. Mirror the **command-FIRST** rule (see `StructureMenu.yarn`
  line 28-31): a line BEFORE an `<<OpenShop>>`-style command makes the VM pause and the
  command never fires.
- **One command at node entry** (the Yarn v3 re-entrancy trap, `StructureMenu.yarn` line
  13-19) — don't stack two synchronous commands back-to-back.
- **`#lastline`** on an opening line renders it inside the options view (WO-337) — used on
  `Barracks_TrainMenu` and `EchoBond_Shard` greetings.
- **Internal ids stay code-keyed** (cf. WO-338): if Echo ids for Train Echo / Big Boy 4014
  get minted, keep the player-facing names as canon and let code key the slugs
  (`train-echo`, `big-boy-4014` are guesses — confirm against the Echo catalog when built).
- **Portraits** referenced (`Portraits/barracks`, `Portraits/scout`) need art before wiring;
  `Portraits/pet-house` already exists (Echo Warden).
```
