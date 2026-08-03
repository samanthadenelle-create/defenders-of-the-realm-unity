# Quest Cast Voices -- the six named-but-unbuilt characters

**DRAFT -- owner sign-off required.** Nothing in this document is canon until Samantha approves it.
No code, no `dialogues.json` edit, no `quests.json` edit is authorized by this file. It is copy for an
implementation agent to consume AFTER sign-off.

- **Date:** 2026-08-03
- **Author:** narrative agent (drafting only -- creative authority is the owner's)
- **Scope:** six characters named in `Assets/Resources/Data/Canonical/quests.json` objective text who do
  not exist as speakers in `Assets/Resources/Data/Canonical/dialogue/dialogues.json`:
  **Village Elder - Forgemaster - Borin Emberhand - Old Pell - Mother Wren - Fenn Wildmane**
- **Not touched:** Sable and Brom already ship as speakers. Their lines are the register I matched. They
  are quoted here only as a tuning fork; not one word of theirs is rewritten.

## Canon absorbed before writing

| Source | What I took from it |
|---|---|
| `docs/STORYLINE.md` | Tone, the Withering/Hollow Ones, Alduin, the cycle. **Its Spire-replaces-the-Tree frame is superseded** (banner + DESIGN-DECISIONS reversal). |
| `docs/DESIGN-DECISIONS.md` | Top banner: **the living world-Tree is canon** (owner 2026-06-26). #21: pets are **Echoes**, the verb is **attune**, keeper is the **Echo Warden**, tone reverent. |
| `Assets/Resources/Data/Canonical/canon-strings.json` | Elarion (never Avalon); the Keeper; the Heart; Glimmer; the Hollow Ones; the Folk. |
| `dialogue/dialogues.json` | The house voice (14 speakers). Brom and Sable read closest. |
| `KEY_FACTS.md` NORTH STAR | Tagline **"Echoes of a Forgotten Civilization"**; mobile-web phone panel is the target surface. |
| `DESIGN_FORGEMASTERS_SAGA_LEGENDARY.md` | Borin / Old Pell / Mother Wren want-wound-secret-voice, already authored. I did not invent over it. |
| `DESIGN_VENDOR_STORYLINES_AND_QUESTLINES.md` | Fenn Wildmane's role + his authored Stage-0 line. |
| `Assets/_Modules/Village/NPCs/TownsfolkDialogue.cs` | The existing ambient **"Village Elder"** archetype and its four lines -- the only prior voice the Elder has. |

## House-voice rules I worked to

Derived from the shipped lines, not invented:

1. Address the player as **"Keeper"** (Brom uses "defender" once; nobody uses "hero").
2. **Two short sentences, maximum.** Brom: *"Word travels in Elarion. What do you need, defender?"*
   Sable: *"Come, look closer."* Anything longer does not fit a phone panel.
3. The second sentence usually **turns** -- a warning, a wry deflation, or an invitation.
   Brom: *"If you mean to, the board has the threads. Mind the dark."*
4. Old bones, modern English. No "thee". No archaic inversions. Contractions are rare and land as warmth.
5. **Dash convention: the shipped file uses a single spaced hyphen** (`Careful hands, Keeper - everything
   on this bench remembers a purpose.`). Dialogue lines below match that file exactly. My own prose uses
   `--`. Both are pure ASCII, which is the hard constraint; the single hyphen inside quoted lines is a
   deliberate match to `dialogues.json`, not an oversight.
6. **ASCII only** throughout -- no smart quotes, no em-dashes, no ellipsis characters, no accents.
7. **Stage directions never carry meaning in colour.** Where a beat needs emphasis I name the shape, the
   sound, or the word, never the hue. (Prose words like "grey" and "green" inside a spoken line are
   description, not signal; no reading depends on telling one from the other.)

---

# 1. Village Elder

**Who / why he is here.** The oldest of the Folk still living, who has stood at the Heart's roots long
enough to have counted three Keepers before this one -- he is in Elarion because he outlived everyone
else who could tell a new Keeper what the Tree actually is.

**Voice in one sentence.** He is the only person in Elarion who calls the Keeper *child*, and he answers
every question in generations rather than in hours.

**Prior voice (do not contradict).** `TownsfolkDialogue.cs` already ships him as ambient archetype 4:
*"I've seen three Keepers stand where you stand. The realm endures, child."* My lines extend that
speaker; they do not replace those four ambient barks.

### Lines

- **Greeting:** "You came. I have kept a place at these roots a long while, child."
- **Quest-give:** "The horns will sound before dusk. Go down to the gate and let the Folk see who holds it."
- **In-progress nudge:** "The gate, child. Not the roots. I am old, but I am not the one in danger."
- **Turn-in:** "Three Keepers I have watched stand where you stand. The Tree has never answered one this fast."
- **Idle repeat:** "Sit if you like. These roots hold a body better than any chair in Elarion."

### Stages closed

`elarion.welcome` / **`meet-elder`** -- *"Speak with the Village Elder at the Heart of Elarion."*
(greeting + quest-give)

`elarion.welcome` / **`first-defense`** -- *"Survive the first wave at the gate."*
(nudge + turn-in)

---

# 2. The Forgemaster

**Who / why she is here.** The title-holder who keeps Elarion's working fire *now* -- not a legend, a
tradeswoman with a queue of commissions -- and she is here because somebody has to hand a new defender a
weapon that is not soft as cheese while the Emberhand forge stands cold.

**Voice in one sentence.** She talks to the work rather than to you, and every line she says has a
deadline attached to it.

**Deliberately unnamed.** Canon gives "Forgemaster" as a *title held by four* (`forgemasters_act1`:
*"Bond all four Forgemasters (Forge, Armory, Lumbermill, Mill)"*), and separately ships an ambient smith
named **Brunhild** and a generic **Blacksmith** speaker at the Forge. I did not pick which of those she
is -- see Owner Question 2. Every line below works whether she ends up Brunhild, a fifth person, or an
early mask on Borin.

### Lines

- **Greeting:** "Fire's up, Keeper. Say what you need and say it plain - the coals do not wait on talk."
- **Quest-give:** "Iron. That is the whole commission. Bring me iron and you walk out with something that bites."
- **In-progress nudge:** "No iron yet. My fire is eating good wood for nothing, Keeper."
- **Turn-in:** "There. Trued, balanced, honest. Do not thank me - go and dent it."
- **Idle repeat:** "Bring me back a blade you have used and I will tell you how you fight."

### Stages closed

`forgemaster.first-commission` / **`gather-iron`** -- *"Bring iron to the Forgemaster."*
(greeting + quest-give + nudge)

`forgemaster.first-commission` / **`claim-weapon`** -- *"Claim your forged weapon."*
(turn-in)

---

# 3. Borin Emberhand -- the Forge

**Who / why he is here.** Aldric Emberhand's grandson, heir to the fire that made the Aegis and to the
blame for the Dimming that started before he was born -- he stays in Elarion because leaving would look
like the confession the town has been waiting thirty years for.

**Voice in one sentence.** Gruff, dry, and pointed inward: he insults his own work before you get the
chance, and softens the instant somebody treats smithing as honest labour instead of legend.

**Canon anchors (authored, not invented).** He keeps the **snapped hilt of the legendary blade** on his
anvil and has never had the courage to reforge it. He knows the **Threefold Fold** and believes using it
again is what doomed Elarion. He would rather make a thousand honest swords than one great one.

### Lines

- **Greeting:** "Emberhand. Yes, that Emberhand. Mind the broken hilt on the anvil - it does not move."
- **Quest-give:** "My fire has been out a week and I have not hurried to light it. Bring wood and iron and I will have no excuse left."
- **In-progress nudge:** "Forge is still cold, Keeper. So am I. Wood and iron."
- **Turn-in:** "It is lit. First time in a week, and I have not decided yet whether that is good news."
- **Idle repeat:** "A thousand honest swords, Keeper. Not one great one. That is the bargain I keep."

### Extra beats his later stages need

- **`quench-blade` give:** "Steel is the easy half. Bring me a flawless crystal and we will see if it drinks."
- **`quench-blade` turn-in:** "Listen to it. That is a note this town has not heard since my grandfather. Take it and say nothing."
- **`field-test` give:** "A blade is a rumour until it is used. Take it to the west gate and find out what I have made."
- **`reforge-plate` (Halvard's quest, at his fire):** "Her plate. On my anvil. Do not stand there enjoying it, Keeper - hold the tongs."
- **`steel-truth` (Act II, at Brom's hearth):** "You want the truth of it? Say it in front of her, then. I have carried it alone long enough."

### Stages closed

`vendor.forge` / **`relight-forge`** -- *"Bring Borin Emberhand wood and iron to relight the forge."*
`vendor.forge` / **`quench-blade`** -- *"Bring a flawless Aether Crystal to quench the first true blade."*
`vendor.forge` / **`field-test`** -- *"Field-test the new blade: clear the Stonebelly raid at the west gate."*
`vendor.armorer` / **`reforge-plate`** -- *"Reforge the plate at Borin's now-lit forge."*
`forgemasters_act2` / **`steel-truth`** -- *"Broker peace between Borin and Halvard at Brom's hearth - the truth of the fallen Aegis."*

---

# 4. Old Pell -- the Lumbermill

**Who / why he is here.** The woodward who cut the Heartwood bough for Aldric's Aegis, who has heard the
Tree weep in the wind ever since -- he is in Elarion because he will not leave the thing he wounded.

**Voice in one sentence.** He speaks slowly, apologises to the ground, and is the only one who ever says
the quiet moral thing out loud.

**Canon anchors.** He swore never to cut the Heart again. He alone knows that Heartwood must be **given,
not taken**, and that the legendary craft is impossible without it.

### Lines

- **Greeting:** "Mind where you put your boots, Keeper. Young roots under there, and they have had a hard century."
- **Quest-give:** "The grove I grew up in has gone grey to the bark. Lift whatever is sitting on it and I will get the saws singing again."
- **In-progress nudge:** "The grey is still on my grove. I can hear it from here, and I would rather not."
- **Turn-in:** "Green. Only a little. I have not seen that on that ground in thirty years."
- **Idle repeat:** "I cut a bough from the Heart once, for a great man's great work. The wind has never let me forget it."

### Extra beats his later stages need

- **`plant-sapling` give:** "Carry it in both hands. It is a piece of the Heart, and it knows the difference."
- **`defend-sapling` give:** "One night, Keeper. If it stands one night it will stand a hundred years."
- **`bough-for-tree` (Act II):** "You want a living bough. Tell me what it is for - and if the answer is glory, do not say it at all."

### Stages closed

`vendor.lumbermill` / **`clear-grove`** -- *"Clear the blight from Old Pell's grove in the Verdant Forest."*
`vendor.lumbermill` / **`plant-sapling`** -- *"Carry a sapling from the Heart of Elarion and plant it."*
`vendor.lumbermill` / **`defend-sapling`** -- *"Defend the sapling through one night-raid."*
`forgemasters_act2` / **`bough-for-tree`** -- *"Reconcile Old Pell and the Forge - prove the bough is to defend the Tree, not bleed it."*

---

# 5. Mother Wren -- the Mill

**Who / why she is here.** The miller whose stones burned the night the Aegis fell and who rebuilt them
with her own hands without ever naming who was to blame -- she is in Elarion because she is waiting to
see the four of them share a fire again before she goes.

**Voice in one sentence.** Warm, plain, and completely immovable: she calls the Keeper *dearie* and still
manages to be the last word in any room.

**Canon anchors.** Her mill presses the **Last Pressing**, the aether-binding quench-oil without which
there is no Legendary. She is the keystone everybody forgot, and she is the one who finally says yes.

### Lines

- **Greeting:** "Come in out of the wind, dearie. There is bread, and there is always a chair."
- **Quest-give:** "Get my stones turning again. Empty bellies, empty walls - it has never been more complicated than that."
- **In-progress nudge:** "Still no mill, dearie. And still no supper for the folk standing the watch."
- **Turn-in:** "Listen to that. Thirty-year-old stones and they still know the tune."
- **Idle repeat:** "I have fed every one of them at this table. Not one of them will sit at it together."

### Extra beats her later stages need

- **`grow-population` give:** "Feed them and they stay. That is the entire secret, and grown men keep asking me for a better one."
- **`assign-workers` give:** "Put hands to the work and the work goes on without you. Even while you are away, dearie."
- **`one-hearth` (Act II):** "Four chairs, one fire, one night. I have had them set out since before you were born."
- **`the-choice` (Act IV):** "Whatever you choose, you eat first. I am not sending anyone to a choice like that hungry."

### Stages closed

`vendor.granary` / **`restore-mill`** -- *"Restore food flow: build or upgrade Mother Wren's mill."*
`vendor.granary` / **`grow-population`** -- *"Grow the population to the next threshold."*
`vendor.granary` / **`assign-workers`** -- *"Assign workers to a node and let it accrue while away."*
`forgemasters_act2` / **`one-hearth`** -- *"Gather all four to Mother Wren's hearth for one shared meal - the night the wound closes."*
`forgemasters_act4` / **`the-choice`** -- *"At Wren's hearth, choose the aether for the quench - draw from the Heart, or gather from the cleansed regions - and reforge the Aegis of Elarion."*

---

# 6. Fenn Wildmane -- the Stalls

**Who / why he is here.** The stablemaster whose whole herd walked off the night the Heart dimmed -- he
is in Elarion because he keeps the stalls swept and the doors open on the chance that one of them comes
back, and because he has decided the coming back is the only part worth teaching.

**Voice in one sentence.** He describes what the animal thinks of you, so every conversation quietly
turns into an assessment of the Keeper.

**Canon anchor.** His authored Stage-0 line is *"A beast'll follow a kind hand further than gold ever
could. Mine all ran off when the Heart dimmed."* -- everything below is tuned to that.

**Written deliberately narrow.** Fenn's quests are authored as *taming wild beasts*, which is not the
same thing as an **Echo** (the awakened essence of a person the Heart guards). I have kept every line of
his to the stalls and the animals and have not called anything he handles an Echo, nor used the verb
*attune*. See Owner Question 1 -- this is the biggest unresolved question in the set and I did not
resolve it by writing over it.

### Lines

- **Greeting:** "Stand still a moment. Not for my sake - for the ones watching you out of the stalls."
- **Quest-give:** "Go out past the gate and find one that has not given up on people yet. Do not catch it. Let it choose."
- **In-progress nudge:** "Still empty-handed? Good. Hurrying is how a body gets bitten, Keeper."
- **Turn-in:** "Look at that. Walked in behind you on its own four feet. That is the whole trick, and nobody ever believes me."
- **Idle repeat:** "Mine all ran off when the Heart dimmed. I never blamed them. I would have gone too."

### Extra beats his later stages need

- **`train-ability` give:** "It already knows how to fight, Keeper. What it does not know is when you want it to."
- **`put-to-work` give:** "Give it work. One with nothing to do is a sad creature, and a sad creature wanders."

### Stages closed

`vendor.stable` / **`tame-beast`** -- *"Track and tame a wild beast in its region."*
`vendor.stable` / **`train-ability`** -- *"Train a pet ability with Fenn Wildmane."*
`vendor.stable` / **`put-to-work`** -- *"Assign the pet to auto-harvest a node or guard an outpost."*

Fenn is also the authored umbrella for the eight `petbond.*` questlines (Sproutling, Craghound, Frostkit,
Emberpup, Mirewing, Glimmermoth, Stoneback, Aether Fox). I have written no `petbond` lines, because every
one of them depends on Owner Question 1.

---

# Owner questions -- things canon does not answer

Listed, not decided. Each one blocks or reshapes copy above.

### 1. Are Fenn's beasts Echoes, or a second companion system? (highest impact)

DESIGN-DECISIONS #21 and `CLAUDE.md` are explicit: pets are canonically **Echoes**, the awakened essence
of a person the Heart of Elarion guards; the verb is **attune**; the keeper is the **Echo Warden**; the
HUD word "Pets" is retired in favour of "Echoes". But `vendor.stable` and all eight `petbond.*` quests
are written as *taming wild animals* -- *"Track and tame a wild beast in its region"*, *"Train a pet
ability with Fenn Wildmane"* -- and `dialogues.json` already ships an **Echo Warden** at the Echo Hollow
who grants Echoes by a different route entirely. Either (a) Fenn's beasts are a separate, non-Echo
companion track, (b) the `petbond` quests are legacy wording that should be rewritten as Echo attunement
and Fenn's role changes, or (c) Fenn is retired into the Echo Warden. I wrote Fenn to (a) because it is
the reading that contradicts the least canon, and I flag it rather than assume it.

### 2. Who is "the Forgemaster", singular?

`forgemaster.first-commission` says *"Bring iron to the Forgemaster."* But `forgemasters_act1` says
*"Bond all four Forgemasters (Forge, Armory, Lumbermill, Mill)"* -- so Forgemaster is a title held by
four people, one of whom is Borin. Meanwhile `dialogues.json` already has a generic **Blacksmith** at the
Forge, and `TownsfolkDialogue.cs` ships an ambient **"Brunhild, the Smith"**. Is the tutorial Forgemaster
(a) Brunhild promoted to a speaker, (b) Borin before his own questline lights his fire, or (c) a fifth
tradesperson? The lines above are written to survive all three, but the answer decides whether she gets a
name and a portrait.

### 3. Who is the Village Elder, and does he have a name?

Every other speaker in the file has a proper name (Brom, Sable, Coppin, Sylas, Bryn). The Elder does not.
Separately, `DESIGN_VENDOR_STORYLINES_AND_QUESTLINES.md` gives the village-progression master quest to
**Warden Alric, the Resource Steward**, and `quests.json` ships that quest as `vendor.steward` "Rebuild
Elarion" with no named speaker. Is the Village Elder the same man as Warden Alric? And a third overlap:
the shipped tutorial gives the entire welcome-and-first-wave beat to **Sylas** -- `elarion.welcome`
stages 1 and 2 are already covered by `tut_meet_sylas` / `tut_town_wave` copy. Two mouths on one beat.

### 4. Dame Halvard is the seventh character, and Borin's Act II cannot close without her

`forgemasters_act2` / `steel-truth` reads *"Broker peace between Borin and Halvard at Brom's hearth."*
Halvard is fully authored in `DESIGN_FORGEMASTERS_SAGA_LEGENDARY.md` (the Armory, the Oathweld, the
dented breastplate) and is not a speaker either -- but she was not on the list of six. Borin's
reconciliation beat is a two-hander; I wrote his half only. Does Halvard get the same treatment?

### 5. The region names in quest text do not match the shipped realm map

`quests.json` names **Verdant Forest**, **Stone Mountains**, **Frost Peaks**, **Ashen Wastes**, and
**the Mire**. `Assets/Resources/Data/Canonical/realm-map.json` ships **The Thornwood**, **The Mirewood**,
**Hollowfrost Vale**, **The Emberwastes**, and **The Starfall Reach**. These are two different maps.
Old Pell's grove is *"in the Verdant Forest"* and Fenn's beasts are *"in their region"* -- both of those
lines point at a place the game does not have under that name. I did not name a region in any spoken
line for exactly this reason. Which set is canon?

### 6. "Stonebelly" exists only in quest text

Borin's field-test is *"clear the Stonebelly raid at the west gate"* and `petbond.stoneback` is
*"Clear a Stonebelly camp."* **Stonebelly appears nowhere else in the shipped canonical data** -- not in
`enemies.json`, not in `enemy-roles.json`. It is a faction with two quest references and no existence.
Keep it, rename it to a shipped enemy family, or author it?

### 7. The Spire framing is still sitting in quest text after the reversal

`vendor.steward` ends on *"Awaken the Spire and march on the Orc Necromancer"* and `petbond.aetherfox`
opens on *"the edge of the Spire's light."* The Cathedral-Spire-replaces-the-world-Tree ruling was
**reversed** (owner 2026-06-26; the living world-Tree is canon). If the Elder turns out to be the Steward
(Question 3), his final beat currently points at a superseded object. Does "the Spire" survive as a
built tower alongside the living Tree, or does that objective text need re-anchoring to the Heart?

### 8. Three of the six have no building to stand in

`structures-catalog.json` ships `mill` (Mill), `lumbermill` (Sawmill) and a forge -- so Wren, Pell and
Borin have somewhere to be. There is **no Stable or Stables structure at all**, so Fenn has no address;
there is no Granary despite the quest id being `vendor.granary`; and Wren's own stage says *"build or
upgrade Mother Wren's mill"* in a town that now starts blank, so on a fresh save she may be standing in
a field. Where do these people physically live before the player builds anything?

### 9. Portraits

All fourteen shipped speakers carry a `portrait` path. Six new speakers need six new portrait keys, and
four of them (Forgemaster, Borin, Old Pell, Mother Wren) sit at buildings whose existing portraits
(`Portraits/forge`, `Portraits/lumbermill`, `Portraits/farm`) are currently used by the *generic*
tradespeople they would replace. Reuse, or new art?

---

**DRAFT -- owner sign-off required.** On approval, an implementation agent takes the Lines and Extra
Beats sections into `dialogues.json` as new speakers plus stage-aware dialogue nodes. Nothing here should
be implemented while Owner Questions 1, 2 and 3 are open -- they change who says the words.
