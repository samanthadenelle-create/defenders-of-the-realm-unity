# Worldbuilding — V1 (Echoes of Elarion)

**Status:** ACTIVE narrative expansion (2026-06-28). This doc **expands** — never overrides —
the front-of-house canon. Where premise / cast / tone are concerned, the authority order is:

1. `docs/COMBAT_PIVOT_NORTHSTAR.md` — the single-Knight north star (mechanics + economy spine).
2. `docs/NARRATIVE/STORY_BIBLE_POLISH.md` — the front-of-house story bible (premise, cast, tone).
3. `CANON_GROUND_TRUTH_2026-06-26.md` + `SESSION_CANON_LOADER.md` — current reality.

This file goes **deeper** on four things the bible names but does not fully build out:
the **world premise** (geography + the Dimming's mechanics-of-fiction), the **three enemy
families** (orcs V1 / skeletons + trolls deferred) within the *grief-not-evil* frame, the
**echo / life-force fantasy** as lived experience, a **V1 quest/arc outline**, and **ready-to-drop
dialogue hooks** authored to the real `dialogues.json` schema (`DeNelle.Core.Dialogue.DialogueModel`).

> **Canon locks inherited (do not deviate — see STORY_BIBLE_POLISH §"Canon locks"):**
> living **dimming** world-tree (not burned, not a Spire); **one Knight, Grom**; antagonist =
> **the Hollow Ones** (grief, not evil) fronted by the **orc legion**; economy =
> **echoes** harvest **wood / iron / grain**, **life force** is the keystone, **gold** = store
> currency; palette **obsidian + gold + life-force gold-green**; tone **grief and hope, never
> cackling villainy.** Retired everywhere: Cathedral Spire, Alduin/Syndrath, the pet-bond roster,
> the 4-member party, AetherCrystal/Glimmer currencies, "Avalon," "Sir Bram," "the Keeper."

---

## 1. The World — Elarion, and the Dimming

### 1.1 The premise, one breath
At the centre of the realm stands the **Heart of Elarion** — a colossal living **world-tree**
whose **aether** (a gold-green light) is, quite literally, the breath of every living thing: it
runs through root and river and lung alike, and while it shines, the realm is warm. A grief older
than memory — **the Dimming** — is bleeding that light into a creeping grey. As the Heart fades,
the broken rise: **the Hollow Ones**, souls emptied by loss, drifting toward the last warmth they
can still feel. An **orc legion** marches as their martial spearhead, tearing the remaining light
loose. **One knight, Grom**, sets out alone with a single **ember** to drive the grey back so the
Tree can heal — and as it heals, the spirits it releases (**echoes**) gather, rebuild, and the
world grows again. *Not a war story with a villain to slay — a **reclamation**.*

### 1.2 The fiction has a dial: warmth vs. grey
The single most important worldbuilding rule is that **the world's colour is a live readout of
the fiction.** Where the Hollow press, the world **desaturates** — gold-green leaches to cold grey,
sound thins, the Tree dims. Where Grom **holds and reclaims**, the gold-green floods back — grass,
banners, market awnings, the Tree's canopy. This is not decoration; it is the *life-force meter
rendered as the world itself.* Every other system (life force, echo count, harvest rate, the
Tree's brightness) is a different gauge on the **same** quantity. Lore and math are one sentence:
**darkness recedes → the Heart grows → the spirits multiply → the world heals.**

### 1.3 Geography (V1 footprint, room to grow)
V1 is intentionally small and legible. The map is **a warm centre and a grey frontier.**

| Place | What it is | Role in V1 |
|---|---|---|
| **The Heart of Elarion** | The living world-tree at world centre `(0,0,0)`; its canopy brightness = the life-force readout. | The emotional anchor + the progress monument. Always visible from the hub. |
| **The Hold** (hero home / castle hub) | Grom's home — a weary but holding settlement clustered in the Tree's remaining light. Inn, forge, echo hollow, skill tree, raid launch. | The safe centre. Where the player upgrades + launches raids. (Castle hub today.) |
| **The Echo Hollow** | A warm room of small beds where the Tree's released spirits gather between work. Tended by the **Echo Warden.** | Where the player meets + assigns echoes (drag-to-resource). |
| **The Greyline** | The shifting frontier where warmth gives way to grey — the front the player pushes back. | Travel-to-fight space; visibly recolours as fronts are reclaimed. |
| **Outposts / strongholds** | Grey-held positions (orc camps, watch-stones, a stronghold) along the Greyline. | The V1 raid targets. Clearing one **permanently** raises life force + recolours that ground. |
| **The Wound** *(offstage)* | The buried tear from which the grey seeps — far past the frontier, unseen in V1. | The source. Named, never shown in V1; the long-horizon destination. |

**Regions note (forward-compat, not V1 scope):** an older open-world roster
(`docs/REGION_ENEMY_ROSTER.md`) maps four cardinal regions (Goldfields/Stoneback = living edge,
Mirewood/Ashwood = deep grey). That structure is **compatible** with the Greyline frame (it *is*
the Greyline, expanded) but its "Tiefling Cultist / Necromancer of the Wound" roster predates the
single-Knight pivot — **for V1, the only enemy family built is the orc legion** (§3). Treat the
four-region map as a V2 expansion skin over this same warm-centre/grey-frontier spine.

---

## 2. The Echo / Life-Force Fantasy (as lived)

This is the heart of the *feel* — the bible states the loop; here is how it should **read minute
to minute.** (Mechanics authority: `COMBAT_PIVOT_NORTHSTAR.md §"Pets → Echoes"` + the Echo
Workforce settled-design block.)

### 2.1 What an echo *is*
An **echo** is a small drifting spirit of life — a soft teal-green wisp with a warm gold core —
**released by the Tree as it heals.** Echoes are **not pets, not tamed, not named, not combatants.**
They are anonymous and bounded (a crew capped at **5**). They have one nature: *find what the
settlement needs, gather it, drift home.* They are the Tree's gratitude made mobile — the world
literally putting itself back together around the player.

> *"They're not pets, and they're not ours. The Heart lets them go, and they come to the work that
> needs doing. We just give them somewhere to come home to."* — the Echo Warden

### 2.2 The loop, felt
1. **Grom reclaims a front** (clears an outpost). That ground recolours; **life force rises** — a
   persistent world gain, not loot that's spent.
2. **A brighter Tree releases more / stronger echoes.** Crossing a life-force threshold **births a
   new echo** — a real, visible event (the Warden remarks; a new light wakes in the Hollow).
3. **The player makes ONE choice:** drag an echo onto **wood / iron / grain.** That single placement
   *is* the strategy. After that, it is passive + autonomous (a wisp drifts out, a legible `+wood`
   floats home). "Render the flavor, fake the sim."
4. **Two scaling axes off the one meter:** *rate* (existing echoes gather faster as life force
   rises) and *breadth* (thresholds add a new echo / resource). **More echo, more resources.**
5. **The chain closes:** reclaim → life force → echoes → **wood/iron/grain** → gear & skills →
   reclaim *further.* The return-hook: it works while the player is away.

### 2.3 The three resources have three distinct jobs (no currency creep)
- **WOOD** → structures & building upgrades (the Hold grows back).
- **IRON** → hero gear — weapon / shield / armor stats (Grom gets harder to kill).
- **GRAIN** → troop upkeep (V2 autonomous defenders; in V1, grain is the *promise* of the standing
  army to come — the Hold's larder filling is a visible sign of recovery).
- **GOLD** is the **separate store currency** (wanderers, premium) — *never* gathered by echoes.

### 2.4 Why it's emotional, not idle-filler
The Tree is a **living progress monument.** The player *sees* the world heal: a darker tree with
two faint motes early; a blazing canopy ringed with drifting lights late. Passive **input** is not
forgettable **output** — the brightening tree, the multiplying spirits, the recolouring market are
the reward. This is the cover's promise made a loop: *"as long as one defender stands, the light is
not out."*

---

## 3. The Enemy Families — the Hollow Ones, fronted by three legions

**The binding tone (never break it):** the enemies are **grief, not evil.** Not a snarling demon
horde, not a dark lord with a plan. They are **the broken** — souls hollowed by the Dimming's loss,
drawn toward the Heart's last warmth because it is the only thing they can still *feel.* The tragedy
is that **they want the same thing Grom protects.** A defeated enemy reads as **released** (a soft
fade to light), not gorily destroyed — *you are ending suffering, not scoring a kill.* Threat is
carried by **force and fatigue** (a slow, heavy, inevitable advance), not cartoon rage. The orc
**wind-up IS the mechanic** (the telegraph the player blocks / heals / times against).

The **Hollow Ones** are the mournful *tide* — the pressure behind everything. The three **legions**
below are the *fightable martial arms* — Tripo humanoid families, each in **Warrior / Tank / Mage.**
**V1 builds the orc legion ONLY** (orcs first: living humanoids show animation clearly, and the
pivot rests on animation-as-mechanics). Skeletons and trolls are **deferred families** (lore set
here so V2 can drop them in on-tone without re-deriving).

### 3.1 The Orc Legion — *the muscle* (V1 enemy family)
**Fiction:** the orcs were the realm's old border-clans — hard people who lived closest to the
Wound and were the first the grey reached. They did not turn cruel; they turned **hollow and
purposeful**, the grief organising their strength into a march. Broad-shouldered, tusked,
hide-and-iron, jagged weapons. They move with the grey like a fatigue you can see — *inevitable,
not furious.* They are the readable, fightable threat at the Greyline; the thing Grom actually
trades blows with.

| Role | Read | Telegraph (the mechanic) | Counter |
|---|---|---|---|
| **Orc Warrior** | Front-line, paced aggressor. The baseline duel. | A heavy, slow **overhead wind-up** — clearly readable. | Shield-block on the wind-up; counter on recovery. |
| **Orc Tank** | Broad, armoured, immovable; soaks and shoves. | A **shoulder-charge** tell (plant, then advance). | Sidestep / kite; don't trade into the charge; chip with ranged. |
| **Orc Mage** | Back-line; calls grey-fire / drains warmth. | A **gathering-glow cast** (grey light pooling). | Burst it down / interrupt before the cast lands; heal through if it connects. |

**Audio/colour:** low sorrowful tones, a counter-key that *drags against the Tree's note.* Where
the legion stands, the world greys; where Grom wins the ground, gold-green returns.

### 3.2 The Skeleton Legion — *the forgotten* (deferred, V2)
**Fiction:** the skeletons are the Hollow Ones at their **furthest gone** — the dead of valleys the
grey took long ago, too hollowed to remember even their grief. They are **stillness given motion**:
brittle, patient, silent. Where orcs are fatigue, skeletons are **erosion** — they don't hit hard,
they simply *do not stop coming.* Tonally they are the saddest family (nothing of the person is
left), so V2 should lean on **quiet and number**, not menace. (Note: skeletons read stiff in
animation, which is *why* they're deferred behind the animation-forward orc family.)

| Role | Read (V2 intent) |
|---|---|
| **Skeleton Warrior** | Tireless attrition; many, weak, relentless. |
| **Skeleton Tank** | Bone-bulwark; a wall of the forgotten. |
| **Skeleton Mage** | Raises/mends others — the only one that "remembers" a craft. |

### 3.3 The Troll Legion — *the grieving giants* (deferred, V2)
**Fiction:** trolls are what the grey makes of the realm's old **gentle giants** — herders of the
high places, slow and kind once, now lumbering husks too vast for the Dimming to fully empty. A
troll **still half-feels** — the most pitiable enemy, mourning in a way the player can almost read.
They are **set-piece weight**: rare, heavy, the moment a fight stops being routine. V2 can use a
troll as a mini-boss gate without a "boss villain," staying inside the grief frame.

| Role | Read (V2 intent) |
|---|---|
| **Troll Warrior** | Ground-shaking single-target threat; huge readable swings. |
| **Troll Tank** | Living siege; the thing that breaks a line if ignored. |
| **Troll Mage** | Old earth-magic warped grey; area denial. |

### 3.4 On a singular antagonist (open, kept on-tone)
The bible permits a **driving intelligence behind the siphon** if the arc wants one — fold it into
an **orc necromantic siphoner** feeding on the aether: a *cause of grief*, tragic, **not** a
cackling overlord. **Retired:** Alduin the Mournful, Syndrath the Devourer (older docs). V1 does
**not** need a named boss; the *front line itself* is the antagonist. If V2 wants a face, the
siphoner is the slot — write it as the most-grieving thing in the world, not the most evil.

---

## 4. Grom — the one warm, defiant thing

(Full character authority: `STORY_BIBLE_POLISH §3`. Summary for writers, with the hooks V1 needs.)

A grounded, seasoned human knight — broad but practical, weary but unbroken. **Not** a chosen-one;
a *person* who turned toward the dark when others turned away. Battle-worn dark steel over brown
leather, matte and scratched; open-faced (the grief tone wants a face). His armor is **static
canon**; his **sword and kite shield** carry all visible progression, and the **shield-block is a
real mechanic** (a timed reaction to enemy wind-ups). He carries a single **ember** — a fragment of
the Heart's living light — both the literal quest object of the intro and the theme: *one small,
stubborn flame against a grey tide.* Every reclaimed front is the ember spreading.

**His grief mirrors the enemy:** Grom and the Hollow Ones are drawn to the *same* warmth. They
collapse inward to be filled; he carries it *outward* to defend it. He does not hate them — he
**mourns** them, even while ending them. His arc is **not** "defeat the dark lord"; it is *hold the
line long enough for the world to heal itself.*

---

## 5. V1 Quest / Arc Outline

V1 is a **single offense loop** dressed as a **reclamation arc.** No branching epic — a clean,
felt spine of "push the grey back, watch the world warm." Quests are **data-driven** (QuestCatalog,
sibling to DialogueCatalog) and each story beat is delivered through the **hub NPCs** (§6 dialogue
hooks). The arc is **gated by life-force thresholds**, so *story progress and economy progress are
the same axis* (the canon spine).

> Authority: this is the *narrative spine* over the existing offense loop (raid outpost → clear →
> life force up → echoes → gear/skills → raid harder). It introduces **no new systems** — it
> sequences the ones the north star already locks. Base-building / waves / troops are **V2-gated**
> and appear here only as the *promise* the arc points toward.

### Act 0 — The Ember (intro + first steps)
- **Cold open:** the canonical 5-slate intro (`STORY_BIBLE_POLISH §6`) — the Heart ablaze → the
  Dimming → the Hollow Ones rise → Grom answers with the ember → the first sliver of gold returns.
- **Arrive at the Hold.** Meet **Brom** (innkeeper / quest-giver) at the hearth — the world's
  rumor passes through here. He points Grom at *"the old outpost down the grey road."*
- **Meet the Echo Warden.** The Tree releases its **first echo.** Tutorial of the one interaction:
  **drag it onto WOOD.** The Warden frames it: *push the dark back, the Tree breathes, more lights
  wake.*
- **First raid (tutorial fight).** Clear the nearest orc watch-post. Teaches: travel → battle
  stages → ability kit (basic / ranged / heal / burst) → **shield-block on the orc wind-up.** On
  clear: that ground **recolours**, **life force ticks up** for the first time — the loop's promise
  shown, not told.

### Act 1 — The Greyline (the core loop, escalating)
- **The frontier opens.** A short chain of reclaimable fronts along the Greyline, each a step
  deeper into grey. Clearing each **permanently** raises life force.
- **Economy comes alive.** Life-force thresholds **birth echoes 2 and 3** (one each → wood/iron/
  grain) — each a real event the Warden marks. **The Forge (Smith)** opens: bring **iron** → better
  blade + shield (the visible progression). Iron + wood feed the **skill tree** (heal + ranged
  unlocks).
- **The world warms back, visibly.** NPCs remark on concrete signs — *colour returning to the market
  square, the first full grain-cart in a season, good iron coming up out of reclaimed ground.* The
  recovery is the reward; the player *sees* it.
- **The orc family deepens.** Warrior → Tank → Mage introduced across the chain so the combat
  vocabulary (block / kite / interrupt) is taught one telegraph at a time.

### Act 2 — The Stronghold (V1 capstone)
- **The orc stronghold** (the EnemyStrongholdBuilder Village2 set-piece) is the deepest, hardest
  front — the source of the local siphon. Optional flavour: a **grey-fire orc caster** anchoring it
  (the on-tone "siphoner" slot — a *cause of grief*, not a boss villain).
- **Reclaim it.** Clearing it is the V1 climax: the **largest life-force jump**, the Tree visibly
  surges, the **extra (flex) echoes** (4–5) become reachable, the Hold recolours fully.
- **The open horizon (sequel hook, not a wall):** with the local grey driven back, Brom names the
  far source — **the Wound** — and the *standing army* the recovered grain could feed. This **points
  at V2** (base-building / autonomous troop defense, `ff.basebuilding`) without requiring it. The V1
  beat lands as: *the light is holding; the work begins again tomorrow.*

### Quest hooks summary (for QuestCatalog authoring)
| Quest id (suggested) | Trigger | Beat | Reward |
|---|---|---|---|
| `q_ember_arrival` | First load | Meet Brom; world framed | unlock rumor board |
| `q_first_echo` | After arrival | Meet Warden; born echo → drag to wood | echo 1 (wood) |
| `q_old_outpost` | Brom rumor | Tutorial raid; first reclaim | life force +; first iron |
| `q_greyline_push` (chain) | Life-force tiers | Reclaim fronts; echoes 2–3 born | gear / skill points |
| `q_the_forge` | First iron | Smith upgrades blade + shield | weapon/shield tier-up |
| `q_the_stronghold` | Greyline cleared | Raid the orc stronghold | flex echoes; big life force |
| `q_the_far_wound` | Stronghold cleared | Brom names the Wound (V2 hook) | arc close + horizon |

---

## 6. Dialogue Hooks (data-driven; fits the MVVM dialogue system)

These are **ready to drop** into `Assets/Resources/Data/Canonical/dialogue/dialogues.json` (and its
StreamingAssets twin). They follow the real schema — `DialogueDef { id, startNode, nodes[] }`, each
node = `{ id, condition?, lines[{speaker,text}], commands[{verb,args}], options[{text,requires,goto}],
next? }` (`DeNelle.Core.Dialogue.DialogueModel`). `goto`/`next` of `"end"` or `""` ends the dialogue.

**Conventions used below (match existing data):**
- `requires` / `condition` are simple boolean condition **keys** resolved by the runner (e.g.
  `quest_*_active`, `lifeforce_tier_2`, `echo_grantable_wood`) — same style as the existing
  `pet_grantable_ice-wolf` / `quest_dimming_active` keys already in `dialogues.json`.
- Commands reuse the existing verb vocabulary (`OpenRumorBoard`, `StartQuest`, `portrait`, …).
  New verbs named below (`AssignEcho`, `OpenForge`, `GrantReward`) are **suggestions** for the
  command dispatcher; swap to whatever the live verb table exposes — the *text* is the deliverable.
- **Tone rule (binding):** weary-but-warm settlement folk; short sentences, old-bones plain
  vocabulary, dry warmth; the world *visibly healing* referenced in concrete small signs. No
  villain monologues, no lore lectures, no fake-archaic "thee/thou."

### 6.1 Brom — innkeeper / quest-giver (the hub's hearth)
```json
{
  "id": "brom_hub",
  "startNode": "greet",
  "nodes": [
    {
      "id": "greet",
      "commands": [ { "verb": "portrait", "args": ["Portraits/brom"] } ],
      "lines": [
        { "speaker": "Brom", "text": "Pull up a chair, Grom. Every tale in Elarion comes through this hearth eventually - and you look like you've got a few." }
      ],
      "options": [
        { "text": "Show me the rumor board.", "goto": "board" },
        { "text": "How fares the Hold?", "goto": "hold" },
        { "text": "Tell me of the Wound.", "requires": "quest_the_far_wound_active", "goto": "wound" },
        { "text": "Just warming my hands.", "goto": "end" }
      ]
    },
    {
      "id": "board",
      "commands": [ { "verb": "OpenRumorBoard", "args": [] } ]
    },
    {
      "id": "hold",
      "lines": [
        { "speaker": "Brom", "text": "Better than it was. There's colour back in the market square this morning - first time in a season." },
        { "speaker": "Brom", "text": "You bring the light a little further each time you go out. Folk notice. They don't say it. But they notice." }
      ],
      "next": "greet"
    },
    {
      "id": "wound",
      "lines": [
        { "speaker": "Brom", "text": "Drive the grey from the door and you start to wonder where it's coming from. There's a place past the frontier. A tear in the deep of things. The Wound." },
        { "speaker": "Brom", "text": "Nobody walks there and walks back. Not yet. But the grain's coming in again - and grain feeds soldiers. One day we won't send just one of you." }
      ],
      "next": "greet"
    }
  ]
}
```

### 6.2 The Echo Warden — keeper of the spirits (Echo Hollow)
```json
{
  "id": "echo_warden",
  "startNode": "warden",
  "nodes": [
    {
      "id": "warden",
      "commands": [ { "verb": "portrait", "args": ["Portraits/echo-warden"] } ],
      "lines": [
        { "speaker": "Echo Warden", "text": "They're not pets, and they're not ours. The Heart lets them go, and they come to the work that needs doing. We just give them somewhere to come home to." }
      ],
      "options": [
        { "text": "A new light woke in the Hollow.", "requires": "echo_unassigned", "goto": "assign" },
        { "text": "Why are there more of them now?", "goto": "why_more" },
        { "text": "Leave them to their rest.", "goto": "end" }
      ]
    },
    {
      "id": "assign",
      "lines": [
        { "speaker": "Echo Warden", "text": "Set one to the wood, one to the iron, one to the grain. After that, leave them be - they know the way better than we do." }
      ],
      "commands": [ { "verb": "OpenEchoAssign", "args": [] } ]
    },
    {
      "id": "why_more",
      "lines": [
        { "speaker": "Echo Warden", "text": "Push the dark back and the Tree breathes easier. When it breathes easier, more of them wake." },
        { "speaker": "Echo Warden", "text": "A brighter tree, more little lights. You did that. Don't let anyone tell you it was the season turning." }
      ],
      "next": "warden"
    }
  ]
}
```

### 6.3 The Forge / Smith (weapon & shield vendor)
```json
{
  "id": "smith_forge",
  "startNode": "greet",
  "nodes": [
    {
      "id": "greet",
      "commands": [ { "verb": "portrait", "args": ["Portraits/smith"] } ],
      "lines": [
        { "speaker": "Smith", "text": "Hmph. Let's see what you've been swinging." }
      ],
      "options": [
        { "text": "I brought iron.", "requires": "has_iron", "goto": "forge" },
        { "text": "Talk to me about the shield.", "goto": "shield" },
        { "text": "Another time.", "goto": "end" }
      ]
    },
    {
      "id": "forge",
      "lines": [
        { "speaker": "Smith", "text": "Good iron, too - coming up out of reclaimed ground again. First time in years. That's your doing. Don't waste it." }
      ],
      "commands": [ { "verb": "OpenForge", "args": [] } ]
    },
    {
      "id": "shield",
      "lines": [
        { "speaker": "Smith", "text": "A shield's not for show, lad. It's the difference between the next swing and the last one. Time the block. Mind it." }
      ],
      "next": "greet"
    }
  ]
}
```

### 6.4 Story-beat dialogue — the first echo (tutorial of the one interaction)
```json
{
  "id": "beat_first_echo",
  "startNode": "open",
  "nodes": [
    {
      "id": "open",
      "commands": [ { "verb": "portrait", "args": ["Portraits/echo-warden"] } ],
      "lines": [
        { "speaker": "Echo Warden", "text": "There. See it? The Heart's let one go. It woke because you pushed the grey back a step." },
        { "speaker": "Echo Warden", "text": "It only wants to be useful. Take it by hand and set it to the wood." }
      ],
      "commands": [
        { "verb": "StartQuest", "args": ["q_first_echo"] },
        { "verb": "OpenEchoAssign", "args": ["wood"] }
      ],
      "next": "after"
    },
    {
      "id": "after",
      "condition": "echo_assigned_wood",
      "lines": [
        { "speaker": "Echo Warden", "text": "It knows the way now. It'll drift out, gather, drift home. That's the whole of it - more light, more lights, more brought back." }
      ]
    }
  ]
}
```

### 6.5 Ambient one-liners (the world healing, fired on triggers)
These are short narration lines (empty `speaker` = the Keeper's-voice / system line) for the hub —
fire on life-force threshold crossings or idle, like the existing ambient bank. Author as a single
multi-node dialogue or as one-line UI strings; included here for the writers' bank.

| Trigger | Line |
|---|---|
| Life force tier up | *"The canopy caught the light a little brighter just now. The Hollow felt it before I did."* |
| New echo born | *"Another little light, drifting out past the gate. The Tree is grateful in the only way it knows."* |
| Front reclaimed | *"Colour, where there was grey. It will hold as long as I do."* |
| First iron gathered | *"Good iron, out of ground that was dead a week ago. Small thing. Means everything."* |
| Stronghold cleared | *"The grey pulled back from the whole frontier. For one evening, the Hold is warm to its edges."* |
| Idle (hub) | *"The work begins again tomorrow. Tonight, the light is holding."* |

---

## 7. Consistency & Cross-References

- **Premise / cast / tone:** `docs/NARRATIVE/STORY_BIBLE_POLISH.md` (authoritative front-of-house).
- **Mechanics / economy spine:** `docs/COMBAT_PIVOT_NORTHSTAR.md` (single Knight; echo/life-force).
- **Current reality:** `CANON_GROUND_TRUTH_2026-06-26.md`, `SESSION_CANON_LOADER.md`.
- **Cover / palette / emotional register:** `docs/ART/GAME_COVER_ART_DIRECTION.md`.
- **Dialogue data shape:** `Assets/_Modules/Core/Dialogue/DialogueModel.cs`; live data
  `Assets/Resources/Data/Canonical/dialogue/dialogues.json` (+ StreamingAssets twin).
- **Enemy data/models:** Tripo orc family (Warrior/Tank/Mage) per `WORK_ORDER_481` + the combat
  north star roster; EnemyStrongholdBuilder Village2 set-piece for the §5 Act 2 stronghold.
- **Region expansion (V2):** `docs/REGION_ENEMY_ROSTER.md` (treat as a V2 skin over the Greyline).

**Retired on contact (do not reintroduce):** Cathedral Spire / burned-Tree premise; Alduin the
Mournful; Syndrath the Devourer; the bondable pet roster (Aether Sprite / Flame Pup / Ice Wolf as
combat/tending companions — survives only as anonymous echoes); the 4-member party; "the Keeper" /
"Chorister" framing; "Sir Bram"; AetherCrystal / Glimmer / Food currencies (→ Wood / Iron / Grain +
Gold); "Avalon" (→ Elarion).

*Living doc. Extend it as Grom's kit, the orc family, and the hub NPCs get built — but keep the
spine locked: a living **dimming** world-tree, **Grom alone**, antagonists who are **grief not
evil**, and the loop **reclaim → life force → echoes → heal.***
