# Narrative Bible & In-Game Text Library

**Status:** v1 story package. Use the bible for any new dialogue you write; pull lines directly from the snippet library for in-game text.
**Tone:** Mystical medieval fantasy. Cozy at the edges, real stakes at the core. Studio Ghibli's _Nausicaä_ by way of an old fairy tale. Hopeful, not grim.
**Voice rule:** Short sentences. Grounded vocabulary. Slightly archaic without being pretentious. No "thee/thou" — modern English with old bones.

---

## 1. The world in a paragraph

In an age before memory, the world cracked open and a wound was left in its deep places. From this wound seeps the **Withering** — a slow cold rot that takes from living things until they are hollow, hungry, and remembering what they used to be. To answer the wound, the First Light planted a tree in a green valley and called it **Elarion**, the Heart-Grove. Crystal grew in its bark. Magic flowed up through its roots. The folk of the valley built their homes around it and lived under its shelter for a thousand years.

Now the Withering has crept close. The Hollow Ones march from the dark places, drawn to the Heart because the Heart is the only thing that can end what they have become — and only by breaking it. They do not hate. They have no breath left in them for hate. They want the silence.

You are the **Keeper** — youngest of a long line — and the Heart's song is your inheritance.

---

## 2. The Heart (Elarion)

### What it is

An ancient sentient tree, ten thousand summers old, planted at the founding of the realm by the First Light. Its bark is veined with crystal. Its roots descend into the wellsprings of the world's magic. Its canopy filters the sun into colors. Fruit grows in its branches that tastes like memory.

### What it does

- Holds back the Withering by its mere presence. Within the valley, things grow. Beyond the valley, things forget themselves.
- Feeds the village with light, warmth, and the slow drip of mined crystal magic.
- Speaks — but only to those bound to it. The Keeper hears its song the way you hear a held breath.

### Why it matters

If the Heart falls, the Withering does not stop at the valley. It does not stop anywhere. The Heart is the only barrier between the world the folk remember and the cold silent one already closing in. It is not "important." It is everything.

### How it shows feeling

The crystal in its canopy is the Heart's expression. When the realm is well: a steady violet glow. When threats mount: amber, then red, then a heavy purple-red when the worst comes. The tree itself answers more slowly — leaves yellow at half wound, brown and falling at deep wound. (See `heart-build-spec.md` for the technical states.)

### The Heart-Wing — Elarion's airborne aspect

Elarion sometimes walks the sky. The Folk call it the Heart-Wing. It is not a thing to fear. (See the LandingPage banner — `docs/launch-triage-2026-05-18.md` T53.)

---

## 3. The enemy — the Hollow Ones

### Who they are

They were folk, once. Each Hollow One was a person — a baker, a herder, a child — who walked too close to the Wound and was unmade. The Withering takes the soul first and leaves the bones to remember. They march in silence because their voices were the first thing the cold took.

### Why they attack

They want the Heart's song to end — not because the song is wrong, but because if the song stops, _everything_ stops, and "everything stopping" is the only quiet they remember wanting. The Hollow Ones are not evil. They are grief, wearing armor.

The Keeper does not hate them either. The Keeper mourns them, even while ending them.

### The leader — Alduin the Mournful

Once the realm's greatest healer. Walked to the Wound to try to seal it with his own light. The Wound drank him. He returned a Necromancer — still the most learned soul in the valley, now bent only on the Heart's end. He remembers being kind. That is the worst part.

He is the boss of every long march on the valley. He cannot be destroyed forever — he reforms wherever the Withering finds purchase. Every defense of the Heart only sends him back into the dark for a while.

---

## 4. The Mage — the Keeper

### The role

The Keeper is bound to the Heart by inheritance and by song. They draw mana from the Heart's wellsprings, hear its song in their bones, and feel its wounds as their own. When they cast, they are not summoning power — they are _spending_ the Heart's gift, carefully, on its behalf.

### The current Keeper (the player)

Young. The previous Keeper — your master — walked into the dark to slow the Withering's tide and did not return. The valley was yours before you were ready. The Heart accepted you anyway.

The pets came to you the day you took the oath. You did not ask them to. They did not say why.

### Why they fight in the valley, not in the Wound

The Heart cannot be moved. The Keeper cannot leave the Heart for long without losing the bond. So the war is here — at the Heart's roots — until the Withering breaks against it or the Heart breaks first.

---

## 5. The pets — the spirits who chose you

The three starter companions. Each chose to bind themselves to the Keeper, knowing the cost.

### Aether Sprite — "the Light's Child"

A tiny violet spirit born from a chip of the Heart's own crystal. The youngest soul in the valley — barely older than the Keeper. Loves to heal more than to harm. Hums when content. Mourns when the Heart is wounded.

> _"It cried when the Heart first dimmed. I had not known a thing made of light could weep."_  
> — fragment, the Keeper's first journal

### Flame Pup — "the Hearth-Heir"

Descendant of the first hearth-fire the valley ever lit. Has lived in many bodies; this one is small and fierce. Sleeps curled on warm stones. Bites the cold when it comes too close.

> _"The Pup remembers every fire that ever kept the folk alive. It is older than its eyes suggest."_

### Ice Wolf — "the Cold-Wandered"

A frost-spirit from the northern peaks. Did not belong here. Came down the mountain the night the previous Keeper fell. No one asked why. It does not say.

> _"It pads silently. When the Hollow Ones come, it bares its teeth and does not look away."_

### The bond

Pets gain their power from the Heart's magic, same as the Keeper. A pet placed near a building tends to it. A pet placed in a defensive slot fights for it. A pet that loses heart can leave — and they have, before. The Keeper does not own them. They serve.

---

## 6. The places

| Name                     | What it is                                                                                                                         |
| ------------------------ | ---------------------------------------------------------------------------------------------------------------------------------- |
| **Elarion**          | The Heart. The World Tree at the valley's center.                                                                                  |
| **The Folk**             | The villagers. Quiet, grateful, watchful. They tend the Keeper.                                                                    |
| **The Crystal Mines**    | Caves at the valley's edges where the Heart's overflow gem-sap pools and crystallizes.                                             |
| **The Pet House**        | A warm hearth and many small beds. Built so spirits would have a place that felt theirs.                                           |
| **The Arcane Tower**     | A stone spire raised by the first Keepers. Houses the defensive ward-stones.                                                       |
| **The Workshop**         | Where the village's craftspeople forge what the Keeper needs.                                                                      |
| **The Farm**             | Wheat that grows because the Heart wills it. Apples that taste like the old summers.                                               |
| **The Wound** (offstage) | The buried tear in the world from which the Withering seeps. Far beyond the valley, in a place no one alive has seen and returned. |

---

## 7. In-game text library

All snippets below are ready to drop into UI strings. Each is tagged by trigger context. Multiple variants are provided where useful — pick at random for repeat triggers so the game feels alive.

### 7.1 Cold open (title screen + first-launch intro)

**Title tagline (under "Defenders of the Realm")**

> _Tend the Heart. Hold the dark._

**Three-line intro (auto-plays once on first launch, ~5s)**

> _In an old valley, an old tree._
> _In the dark beyond, a slow cold rot._
> _You are the Keeper. The song is yours now._

---

### 7.2 Tutorial messages

Used by the FTUE pop-up system. Each is short enough to read in 3 seconds.

| Trigger                      | Line                                                                                                   |
| ---------------------------- | ------------------------------------------------------------------------------------------------------ |
| First time the village loads | _"Welcome home, Keeper. The Heart hums for you. Walk a while — the song will tell you what it needs."_ |
| First approach to the Heart  | _"This is Elarion. Touch the dais. The crystal will remember you."_                                |
| First crystal claim          | _"The Heart gives what it can spare. Hold these — you will need them."_                                |
| First building unlock        | _"The Folk built these long before you. Each one tends to the Heart in its own way."_                  |
| First store visit            | _"What you cannot grow here, the wanderers bring. Spend wisely. Coins are slow to earn."_              |
| First defense run begins     | _"The dark has noticed us. Place your companions at the slots. They will know what to do."_            |
| First pet placement          | _"A spirit at a slot defends. A spirit at a building tends. They cannot do both at once."_             |
| First ability cast (Q)       | _"This is your magic, but it is the Heart's gift. Use what is given. It returns when it can."_         |
| First Frost Nova (W)         | _"For when a lane fails. The cold buys you a breath. Use the breath well."_                            |
| First Beacon (E)             | _"Plant light where the dark gathers thickest. The towers within will fight braver."_                  |
| First Meteor (R)             | _"This is the deep magic. Spend it only when the Heart calls for it."_                                 |

---

### 7.3 Wave warnings

Pre-wave callouts. Two variants per element so it doesn't repeat exactly.

#### Ice wave incoming

- _"They come from the cold places. They will move slowly, and bite hard."_
- _"The frost-marked are at the gates. Keep your fires lit."_

#### Fire wave incoming

- _"The ember-bound come, hungry for the green things. Hold the eastern lanes."_
- _"They burn as they march. Do not let them touch the Heart's roots."_

#### Aether wave incoming

- _"Wisps of the unmade — quick, half-here. Aim true."_
- _"The half-souls drift in. They feel the Heart's pull. Turn them back."_

#### Mixed wave

- _"A many-tongued tide. Read the lanes before you place."_

#### Boss wave (Wave 6) — give this real weight

- _"Alduin the Mournful walks at the head of this tide. He was a healer, once. Greet him with steel."_
- (Alternate, on repeat boss attempts) _"He returns when the Withering finds purchase. Send him back."_

---

### 7.4 The Heart's voice — state changes

Use these as a system-message line each time the Heart's threat state escalates. Keep them rare and quiet so they don't become noise.

| State                              | Line                                                       |
| ---------------------------------- | ---------------------------------------------------------- |
| Serene → Vigilant (wave begins)    | _"The song shifts. The dark is at the gates."_             |
| Vigilant → Warning (lane stressed) | _"A lane wavers. The Heart felt it before you did."_       |
| Warning → Danger (multiple lanes)  | _"The walls are thinning. The Heart calls for you."_       |
| Danger → Critical (Heart wounded)  | _"The song breaks. Defend, Keeper — defend now."_          |
| Critical → Serene (recovered)      | _"The Heart settles. The folk breathe again."_             |
| Enters Boss state                  | _"He stands within the gate. Time slows. The light dims."_ |
| Enters Victorious                  | _"The song rises. The valley holds."_                      |

---

### 7.5 Heart-damage flavor (HP threshold crossings)

One-time lines that fire when the Heart's HP crosses a threshold downward. Empathetic, never panicked.

| HP crosses | Line                                                          |
| ---------- | ------------------------------------------------------------- |
| 75%        | _"The crystal flickers. The Heart has taken its first blow."_ |
| 50%        | _"Leaves fall in the wrong season. The song has gone thin."_  |
| 25%        | _"The bark is cracking. Hold, Keeper. Hold."_                 |
| 10%        | _"The song is almost gone. Spend everything."_                |
| 0% (fall)  | _"…"_ (silence, no text — then the defeat screen)             |

---

### 7.6 Victory lines

#### After clearing a normal wave (between waves)

- _"They fall back. The Heart steadies. A breath, then the next."_
- _"The dark withdraws. Tend your companions. The song is not yet done."_
- _"The lanes hold. The folk light a candle for you."_

#### After clearing the boss wave (full victory)

- _"Alduin breaks apart at the Heart's threshold. The Withering crawls home."_
- _"The valley sings. The folk weep. You are still the Keeper. The work begins again tomorrow."_

#### Personal best / new record

- _"None before you held so long. The old Keepers would have nodded."_

---

### 7.7 Defeat lines

When the Heart falls. Empathetic. Mournful. Never punishing.

- _"The song stops. The folk look up. The wind feels cold for the first time."_
- _"The Heart breaks. But the bond does not. Begin again, Keeper — there is no other answer."_
- _"Elarion will return where it is needed. You will too."_

---

### 7.8 Ambient flavor — the Keeper's voice

For the village hub, fire one of these as a faint inner-thought subtitle every 60–90 seconds of inactive play.

- _"The Pup is dreaming of fire again. Its tail twitches."_
- _"The Mine is full. The Folk left a candle by the door this morning."_
- _"There is a song in the wood, faint and old. The Heart is humming."_
- _"The Sprite watches me as I work. I do not know what it sees."_
- _"The Folk leave bread in the workshop when they do not see me. I find it. I do not thank them yet."_
- _"The Wolf came back from the orchard with a scrap of frost on its muzzle. I asked. It did not say."_
- _"The Tower's lamp has been lit for ten thousand mornings. It is my turn to keep it."_
- _"I think the Heart sang me to sleep last night. I cannot tell anymore where its voice ends and mine begins."_

---

### 7.9 Pet vocalization captions

For accessibility / when sounds are muted, when a pet "speaks" (the cue described in `sound-design.md` §2.2 `sfx.pet.idle.coo`).

| Pet                   | Caption                           |
| --------------------- | --------------------------------- |
| Aether Sprite         | _(a soft musical hum)_            |
| Flame Pup             | _(a warm yip, like a small bell)_ |
| Ice Wolf              | _(a low, considered breath)_      |
| All pets, on level-up | _(a long, surprised note)_        |

---

### 7.10 Milestone / achievement lines

| Trigger                | Line                                                                                             |
| ---------------------- | ------------------------------------------------------------------------------------------------ |
| First pet levels up    | _"The Sprite glows brighter. Something it could not do yesterday, it can do today."_             |
| First skill unlocked   | _"A new shape in the song. The Heart taught it while you slept."_                                |
| First boss kill        | _"Alduin falls. The folk will sing about this evening for a hundred years."_                     |
| First store purchase   | _"The wanderer bows. The folk will know your name in three valleys."_                            |
| First building upgrade | _"The Workshop bell is louder now. The Folk are pleased."_                                       |
| Streak of 7 days       | _"Seven mornings tending the song. The old Keepers would have called you 'one of ours' by now."_ |

---

### 7.11 Element badge flavor (re-write of existing ELEMENT_BLURB)

Replace the current `ELEMENT_BLURB` lines with these tonally-aligned versions:

```ts
const ELEMENT_BLURB: Record<PetElement, string> = {
  aether: "Aether. The Heart's own light, shared and reshared. Mends what the dark has frayed.",
  flame: "Flame. The hearth's old defiance. Burns what would creep into the warm places.",
  ice: 'Ice. The patient cold that holds the dark still until the song catches up.',
};
```

---

### 7.12 Resource flavor (re-write for ResourceBar tooltips)

```ts
const RESOURCE_BLURB = {
  crystals: "Crystals. The Heart's slow gift. Mined where its overflow seeps up through the stone.",
  food: 'Food. What the Folk gather while you keep watch. It feeds the spirits as it feeds the bond.',
  coins:
    'Coins. Quiet currency, brought by the wanderers from valleys still standing. Spend them on what cannot be grown here.',
};
```

---

### 7.13 Building short descriptions (replace `BUILDING_DEFINITIONS.bonus` strings)

| Building     | Flavor                                                                              |
| ------------ | ----------------------------------------------------------------------------------- |
| Crystal Mine | _"Where the Heart's gem-sap seeps into stone. Pets here tend the seam."_            |
| Pet House    | _"Warm beds. Small windows. A place spirits feel theirs."_                          |
| Arcane Tower | _"Built by the first Keepers. Holds the ward-stones that answer your call."_        |
| Farm         | _"Wheat that grows because the Heart wills it. Apples taste like the old summers."_ |
| Workshop     | _"Hammer and forge. The Folk make what the Keeper needs and ask nothing."_          |

---

## 8. Tone reference — what to keep, what to avoid

### Yes

- Short sentences with weight
- Quiet stakes
- The Keeper's mourning for the Hollow Ones (the enemies are tragic, not evil)
- "The song" as a recurring metaphor for the Heart's voice
- Small concrete details (bread on the workbench, candles by the door, a tail twitching in sleep)

### No

- "Hark!" / "Verily!" / "Thou shalt" — no fake old English
- Grimdark nihilism — the world is hopeful even in danger
- Long exposition dumps — every line earns its place
- Heroes vs. villains language — the Hollow Ones are _grief_, not Sauron
- Joke breaks — the tone is consistent; humor can come from the pets' cuteness, not from the prose

---

## 9. Names we've used (canon registry)

Keep these consistent across all future text.

| Term                                                       | Meaning                                                         |
| ---------------------------------------------------------- | --------------------------------------------------------------- |
| Elarion                                                | The Heart, the World Tree                                       |
| The Keeper                                                 | The Mage/player                                                 |
| The Folk                                                   | The villagers                                                   |
| The Heart's song                                           | The Keeper's bond with the tree                                 |
| The Withering                                              | The corruption seeping from the Wound                           |
| The Wound                                                  | The buried tear in the world (offstage; the source)             |
| The Hollow Ones                                            | The skeletal enemies — once people, taken by the Withering      |
| Alduin the Mournful                                        | The Necromancer boss; once a healer who tried to seal the Wound |
| The First Light                                            | The being(s) who planted Elarion                            |
| Aether Sprite — "the Light's Child"                        | Starter pet, aether                                             |
| Flame Pup — "the Hearth-Heir"                              | Starter pet, flame                                              |
| Ice Wolf — "the Cold-Wandered"                             | Starter pet, ice                                                |
| The Crystal Mines, Pet House, Arcane Tower, Workshop, Farm | The five buildings                                              |

---

## 10. Open questions for you

1. **Should the previous Keeper have a name?** I've left them un-named so far ("your master"). Naming them invites questions; not naming them keeps a quiet sorrow. My pick: leave un-named for v1, name them if a story-mode quest brings them back.
2. **Should the player name themselves?** A small "What shall the Heart call you?" prompt at first launch. My pick: yes, very gently — input field with placeholder "Keeper" so players who skip get a default.
3. **Are there other villages in other valleys?** I've implied yes ("brought by wanderers from valleys still standing") to leave room for future content. Confirm or cut.
4. **Should pets have lines of their own** (the Sprite literally speaks, etc.) or stay non-verbal with described vocalizations? My pick: non-verbal in v1. They are spirits, not characters. Their feelings come through the Keeper's narration.

Defaults if unanswered: master un-named, optional Keeper name with default, other valleys implied, pets non-verbal.

---

_Living doc. The world should grow as you do. Add to the canon registry whenever new terms land._
