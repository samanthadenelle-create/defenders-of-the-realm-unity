# Dungeons — Storyline & Questline Arcs

**Status:** Narrative companion to `docs/dungeons-system-design.md`. Locks the meta-arc that threads the dungeon system into the game's existing fiction (`docs/narrative-bible.md`). Provides per-questline narrative roles, beat structure, sample lore-stone text in canonical voice, and the endgame shape so Claude Code can transcribe it into the JSON quest data model defined in the system doc.

**Audience:** Claude Code (will turn this into questline JSON files in `src/content/quests/`), Samantha (vision lock + tuning), any future content author writing new dungeons.

**Tone reminder:** Mystical-medieval-fantasy. Cozy at the edges, real stakes at the core. Short sentences. Grounded vocabulary. Slightly archaic without being pretentious. _Studio Ghibli's Nausicaä by way of an old fairy tale._ Hopeful, not grim. The Keeper does not hate the Hollow Ones — she mourns them, even while ending them.

---

## 1. The shape of the whole

The dungeons exist because the Keeper, after enough waves, begins to ask the question the village cannot answer: **what happened out there?** The Hollowmouth portal opens not because she earned it but because the dark _recognized_ her — the same way the Heart did, the day she took the oath.

What she finds, dungeon by dungeon, is not a war story. It is a **mourning story**. Every Hollow One she ends was somebody. Every burned cottage was a hearth that once held a kettle. Every healer-mask in a vault belonged to a hand that meant well. The Wound is not evil. It is **what happens when something tries too hard to be good for too long, alone**.

This is the spine of the storyline: **the Keeper learns, slowly, what the previous Keepers did, why they did it, and why it was not enough.** And then she has to decide whether to walk the same road, or a different one.

The dungeons are her way of finding out.

---

## 2. The four acts

### Act I — _The Familiar Edges_ (Elarion outskirts, early Thornwood)

**Player level:** unlocked Wave 8. Hero level 3–6.
**Emotional beat:** _The world is bigger than the valley._
**Dungeons in this act:** The Healer's Cottage, The Folk's Old Granary.
**Driving questline:** _The Healer's Garden_ (6 beats).
**Revelation:** Alduin the Mournful was, once, a healer. The garden he kept in the Thornwood still grows the same herbs he planted. The Keeper finds his apothecary's journal — the early pages are full of the names of children he saved.

The Keeper does not yet know the journal is his. She thinks she has found a stranger's kindness.

### Act II — _The Old Wounds_ (Thornwood deep, Wintermere lower passes)

**Player level:** unlocked Wave 12. Hero level 6–10.
**Emotional beat:** _This war has been going on for a long time, and many have already lost._
**Dungeons in this act:** The Sunken Bell-Tower, The Wolfwarden's Vigil, Frost-Stair of the Cold-Wandered.
**Driving questlines:** _The Folk Who Forgot_ (5 beats), _The Cold-Wandered's Pack_ (8 beats — runs across both acts II and III).
**Revelations:**

- The Hollow Ones in the Bell-Tower are the villagers of Old Elarion, the settlement that stood before this one. They walked toward the Wound when the previous Keeper called for help. They never came back as themselves.
- The Ice Wolf is the last of a pack of frost-spirits who climbed down from Wintermere to seal the Wound from the north. The Frost-Stair is where they made their last stand. The Glass Cathedral is where one of them — _hers_ — still sleeps.

The Ice Wolf does not lead the Keeper to the Cathedral. The Keeper has to find it. When she does, the Wolf does not enter the inner chamber. It waits at the threshold.

### Act III — _The Master's Path_ (Wintermere highlands, Glass Cathedral inner, the road to the Hollow Deep)

**Player level:** unlocked Wave 18. Hero level 10–15.
**Emotional beat:** _I have to follow her, even though I know how this story ends._
**Dungeons in this act:** The Glass Cathedral (inner), The Apothecary's Vault, The Hollowmouth Antechamber.
**Driving questline:** _The Last Keeper's Walk_ (7 beats).
**Revelations:**

- The previous Keeper — the Keeper's master — did not die in the dark. She walked toward the Wound to slow the Withering, knowing she would not return. She got further than anyone knew. She left **letters** behind, addressed to the next Keeper, hidden at the threshold of each dungeon she passed through.
- One of those letters explains why the Aether Sprite came to the new Keeper the day she took the oath. The Sprite was the master's. It chose to stay.
- The Apothecary's Vault completes _The Healer's Garden_. The Keeper realizes the journal she has been reading is Alduin's. The handwriting changes in the last pages.

The master's last letter, found in the Antechamber, ends mid-sentence. The Keeper finds the rest of the page burned at the edges. She finishes it herself, in her own hand, in the questlog. The game records what she wrote.

### Act IV — _The Wound_ (The Hollow Deep)

**Player level:** unlocked Wave 24. Hero level 15+.
**Emotional beat:** _The cycle is older than me. My watch still matters._
**Dungeons in this act:** The Wound's Threshold.
**Driving questline:** _At the Edge_ (4 beats — short, weighty).
**Revelations:**

- Alduin is not at the Wound. Alduin **is** what is left of every Keeper and healer who walked to the Wound and was drank by it. He is many. The Mournful is the title; the soul is composite. He reforms because the Wound keeps making him.
- The Keeper cannot seal the Wound. No one can — that is the lesson of every soul before her. But she can do what her master did: **hold the line at the valley, so that the next Keeper has time to become themselves**.
- The endgame is not victory. The endgame is **the moment the Keeper writes her own letter, addresses it to the one who will come after her, and seals it inside the Heart**. The game records this as a permanent save artifact. New Game+ surfaces it for future Keepers.

The Wound's Threshold dungeon does not end in a boss fight. It ends in a **conversation**. Alduin is there — one of his faces. He recognizes the Keeper's master in the way she holds her staff. He does not attack. The Keeper chooses what to say.

There is no "wrong" choice. There is no "good ending" or "bad ending." There is only **what the Keeper decides her watch was for**.

---

## 3. The questlines — full register

The system doc (`dungeons-system-design.md` §10.3) lists three seed questlines. This doc locks the **complete** v1+v2 register with their narrative role.

| #   | Questline                  | Acts    | Dungeons                                                             | Beats | Theme                                                                   | Unlocks                                                         |
| --- | -------------------------- | ------- | -------------------------------------------------------------------- | ----- | ----------------------------------------------------------------------- | --------------------------------------------------------------- |
| 1   | _The Healer's Garden_      | I, III  | Healer's Cottage → Apothecary's Vault                                | 6     | Who Alduin was. The shape of his sorrow.                                | Wave 8 (auto)                                                   |
| 2   | _The Folk Who Forgot_      | II      | Old Granary → Sunken Bell-Tower                                      | 5     | Hollow Ones as villagers the Keeper might have known.                   | Healer's Cottage cleared                                        |
| 3   | _The Wolfwarden's Vigil_   | II      | Wolfwarden's Vigil (single)                                          | 3     | The Ice Wolf's first night in the valley, told from the Wolf's side.    | Ice Wolf bond rank 2                                            |
| 4   | _The Cold-Wandered's Pack_ | II, III | Frost-Stair → Glass Cathedral (outer + inner)                        | 8     | Where the Ice Wolf came from. Why it stayed. Who it lost.               | Wave 12, Wolfwarden's Vigil cleared                             |
| 5   | _The Last Keeper's Walk_   | III     | Glass Cathedral inner → Apothecary's Vault → Hollowmouth Antechamber | 7     | The Keeper's master — who she was, what she did, what she left for you. | _The Folk Who Forgot_ + _The Cold-Wandered's Pack_ both cleared |
| 6   | _At the Edge_              | IV      | The Wound's Threshold (single, weight-class boss dungeon)            | 4     | The cycle. The conversation. Your letter.                               | All five prior questlines complete + Hero level 15              |

### 3.1 Cross-cutting threads (not questlines — recurring devices)

These are **narrative motifs** the dungeons return to. They don't have their own questline IDs; they appear as **lore-stone clusters** distributed across dungeons.

- **The master's letters.** One per dungeon from Act II onward, hidden behind a `lore` node. Each letter is short — three to five sentences in the master's voice. Collecting all of them unlocks a Questlog page titled simply _"For the One Who Comes After You."_
- **Alduin's journal.** Hidden across the two Healer's Garden dungeons. The handwriting in the early pages is steady and warm. By the final pages it is slanted, thinner, written in the dark. Collecting all entries unlocks one piece of the Apothecary's Vault — a small **gift** Alduin left for "whoever is next," in a sealed clay jar. (It is a seed. The Keeper can plant it at the Farm. It grows into a tree that does not exist anywhere else in the realm.)
- **The Folk's small things.** Each Sunken Bell-Tower / Old Granary lore stone surfaces a single quiet detail — a child's wooden horse left on a windowsill, a half-written grocery list, a name carved into a beam. No grand revelations. Just the texture of who lived there.

---

## 4. Per-questline beats (Claude Code: this is the JSON shape)

Beats below match the `Quest` interface in `dungeons-system-design.md` §10.1. **Each beat = one `objective` row.** Beat copy is the player-facing one-liner in the objective ticker. Resolution copy is the Heart's voice / NPC voice when the beat completes.

### 4.1 _The Healer's Garden_ (6 beats)

```
1. find_journal_p1     The Healer's Cottage  Find the journal on the upstairs desk.
2. clear_3_encounters  The Healer's Cottage  Three rooms still hold the Withering's gardeners.
3. read_lore_3         The Healer's Cottage  Read all three apothecary's notes.
4. find_garden_seed    The Healer's Cottage  Recover the sealed jar at the back of the garden.
5. clear_dungeon       The Apothecary's Vault  Reach the inner vault.
6. read_journal_end    The Apothecary's Vault  Read the last pages of the journal.
```

**Resolution line (Heart's voice, fired on beat 6):**

> _"You know his name now. The Withering took a healer first, Keeper. Walk carefully."_

### 4.2 _The Folk Who Forgot_ (5 beats)

```
1. clear_dungeon      The Folk's Old Granary    Walk it to its end.
2. find_wooden_horse  The Folk's Old Granary    A child left a small horse on a windowsill.
3. clear_dungeon      The Sunken Bell-Tower     Ring the bell at the top.
4. read_lore_5        The Sunken Bell-Tower     Read every name carved into the beams.
5. defeat_choirmaster The Sunken Bell-Tower     The choirmaster still conducts. End it kindly.
```

**Resolution line (Folk villager NPC at the Granary exit, fired on beat 5):**

> _"Thank you for ringing the bell, Keeper. They have not heard it in a long while. I think they slept."_

### 4.3 _The Wolfwarden's Vigil_ (3 beats — short, atmospheric)

```
1. clear_dungeon    The Wolfwarden's Vigil    Walk with the Wolf.
2. find_pawprint    The Wolfwarden's Vigil    A frozen pawprint, smaller than the Wolf's.
3. lore_at_summit   The Wolfwarden's Vigil    Read the inscription at the cairn.
```

**Resolution line (Ice Wolf, the only line it ever speaks in the game):**

> _"I remember her."_

### 4.4 _The Cold-Wandered's Pack_ (8 beats)

```
1. clear_dungeon       Frost-Stair       Climb to the top.
2. read_lore_4         Frost-Stair       Read the pack's last stand carved into the ice.
3. find_collar         Frost-Stair       Recover the small bone collar at the summit.
4. clear_dungeon       Glass Cathedral (outer)   Enter the Cathedral.
5. defeat_glass_guard  Glass Cathedral (outer)   The Glass Guardian stands at the inner door.
6. find_pack_carving   Glass Cathedral (inner)   A carving of the pack, with one figure smaller than the rest.
7. read_lore_at_altar  Glass Cathedral (inner)   The altar tells you who was lost.
8. lay_collar          Glass Cathedral (inner)   Place the collar at the altar.
```

**Resolution line (Heart's voice, fired on beat 8):**

> _"The Cold-Wandered came down the mountain alone, Keeper. Now you know why it stayed."_

(After this questline, the Ice Wolf's idle animation in the village gains a subtle change — it sometimes lies down where the village wall meets the snow, looking north. There is no dialogue. The change persists in save data.)

### 4.5 _The Last Keeper's Walk_ (7 beats)

```
1. find_letter_1   Glass Cathedral (inner)   The first letter, behind the altar.
2. find_letter_2   Apothecary's Vault        The second letter, inside Alduin's clay jar.
3. find_letter_3   Hollowmouth Antechamber   The third letter, pinned to the door with a knife.
4. read_letter_1   (in Questlog)             Read the first letter.
5. read_letter_2   (in Questlog)             Read the second letter.
6. read_letter_3   (in Questlog)             Read the third — it ends mid-sentence.
7. write_yours     (in Questlog)             Finish the page in your own words.
```

**Beat 7 resolution:** the game presents a textarea. Whatever the Keeper writes is saved as `gameStore.letterToTheNext` and persists. It is shown to the player on every subsequent save load, in a small reading frame at the top of the Questlog. New Game+ surfaces it on the Splash screen of the next Keeper.

(There is no "correct" answer. Anything the player writes is accepted, including silence — submitting a blank entry writes _"…"_ and the game accepts it.)

### 4.6 _At the Edge_ (4 beats)

```
1. clear_dungeon    The Wound's Threshold    Walk to the edge.
2. survive_3_enc    The Wound's Threshold    Hold against three breach-encounters.
3. reach_alduin     The Wound's Threshold    Find the figure waiting at the edge.
4. choose_response  The Wound's Threshold    Speak.
```

**Beat 4 is a dialogue tree, not a battle.** The player picks one of four responses to Alduin. All four are canonical. None unlock a "true ending." The game records the choice in `gameStore.atTheEdgeChoice` and the village's ambient lines change subtly afterward (Heart's voice gains a different cadence; the Folk reference "the Keeper who spoke at the edge" in passing).

**The four responses:**

1. _"I forgive you."_
2. _"I will not be drank."_
3. _"Tell me what you saw."_
4. _(silence — wait through the prompt)_

Alduin's response to each is unique. He does not attack. He turns, after, and walks into the dark. The dungeon clears. The Keeper returns to the village. The Hollowmouth portal closes behind her, permanently, until New Game+.

---

## 5. The voice — sample lore-stone text

Claude Code should use these as templates for tone, not as final copy. Final copy can be authored or LLM-generated to match.

**Healer's Cottage — Lore Stone 1 (above the kitchen hearth):**

> _"For the cough that does not break: thyme, slow water, a hand on the chest. Sing if the child will let you."_

**Old Granary — Lore Stone 2 (carved into a roof beam):**

> _"Mira — 11 years. Tomas — 8. Little Wren — 3. We will be back before the snow."_

**Frost-Stair — Lore Stone 4 (at the summit, in the old script of the frost-spirits, translated):**

> _"We came south because the song called us. We did not all answer the song again."_

**Glass Cathedral (inner) — Altar inscription:**

> _"Here lies the One Who Was Small. She crossed the mountain when she could not yet hunt alone. Her brother carried her until the cold took his footing. He carried her further. Then he set her down. Then he kept walking south."_

**Hollowmouth Antechamber — The third letter (fragment):**

> _"If you are reading this — yes, you. The Heart will have answered you by now. It always answers the next one. I am sorry I am not there to teach you the things I learned by hand. But you have the Sprite, and the Sprite watched me work for a very long time. Ask it. It will know the small ways. The big ways you will have to —"_
> _(The rest of the page is burned. The Keeper finishes it.)_

**Wound's Threshold — Alduin's first line (he is the one who speaks first):**

> _"You walk like she walked. I am glad someone still does."_

---

## 6. Endgame and New Game+

The game does **not** "end" when _At the Edge_ completes. The village continues. Waves continue. The Keeper continues her watch. Several things change:

- The **Hollowmouth portal** is sealed (visually: vines grow over the arch). Dungeons remain accessible from the Realm Map for replay.
- The Keeper's **letter** is pinned to the top of the Questlog, reflecting on every load.
- **Wave 30+** introduces _"Mournful echoes"_ — single, named, lore-bearing Hollow Ones who carry letters from earlier dungeons the Keeper might have missed. Each delivers a one-line callback to a piece of the storyline.
- A new build option appears at the Heart: **the Bell**. Ringing it once per real-world day plays a 3-second tone that the village's NPCs respond to with a small ambient line ("she rang the bell — must be morning"). It costs nothing. It does nothing mechanical. It is for the player.

**New Game+** is unlocked. Beginning a new game shows the prior Keeper's letter on the Splash screen, and the new Keeper's onboarding gains one extra line in the opening cinematic:

> _"There was a Keeper before you. She left a letter. The Heart kept it. Read it before you begin."_

The new save **does not inherit progress** — economy, levels, and dungeons reset. The letter, the _At the Edge_ choice, and the planted tree from Alduin's seed all carry forward as **cosmetic memory** — visible in the world, not affecting mechanics. The new Keeper passes the same dungeons. Finds the same lore. Writes her own letter at the end.

The cycle is the point.

---

## 7. Integration notes for Claude Code

**Where this turns into code:**

1. Each questline in §3 → one file in `src/content/quests/<questline-id>.ts`, matching the `Questline` shape defined in `dungeons-system-design.md` §10.1.
2. Each beat in §4 → one `objective` entry. Objective types in §4 (`find_*`, `clear_*`, `read_*`, `defeat_*`, `survive_*`, `lay_*`, `choose_response`, `write_yours`) map to objective verbs Claude Code will need to add to the quest system. Most are trivial (consult a flag in `dungeonProgress`); `write_yours` and `choose_response` need bespoke UI in the Questlog.
3. Lore-stone copy can ship as placeholders matching §5 voice; Samantha will pass over and tighten. Do not write copy in a different voice. If unsure, leave the lore stone empty with a `// TODO: voice` comment.
4. **Persistence additions** beyond the dungeon doc's `DungeonSlice`/`QuestSlice`:
   - `gameStore.letterToTheNext: string | null` — the player's written letter from _The Last Keeper's Walk_ beat 7.
   - `gameStore.atTheEdgeChoice: 1 | 2 | 3 | 4 | null` — the choice from _At the Edge_ beat 4.
   - `gameStore.bellRangAt: number | null` — last timestamp the post-endgame Bell was rung (for daily gating).
5. **New Game+ flag:** `gameStore.priorRunLetter: string | null` survives reset. Splash screen shows it if non-null. Everything else resets normally.

**Pet bond hooks:** Two questlines (#3 Wolfwarden's Vigil, #4 Cold-Wandered's Pack) involve the Ice Wolf specifically. After both clear, the Ice Wolf's idle animation in the village should reference the storyline (subtle — sometimes lies facing north). This is the only mechanical pet-behaviour change driven by the storyline. The Aether Sprite gains one new ambient line after _The Last Keeper's Walk_ clears (_"She was kind. She taught me a song. I will sing it for you when you are tired."_).

---

## 8. Out of scope (deferred to a later story doc)

- **Side quests off the main questlines** — small one-dungeon kindnesses (a Folk villager asks the Keeper to recover a lost wedding ring from the Sunken Bell-Tower). Worth doing eventually; not v1.
- **Branching paths within a questline.** Currently all questlines are linear. _The Cold-Wandered's Pack_ would be a natural place to introduce a branch (rescue the trapped frost-spirit at the Glass Cathedral vs. let it rest), but v1 ships linear.
- **Other heroes' storylines.** The current arc is written for any class — the Mage, Knight, and Ranger all find the same letters and have the same conversation at the edge. A v2 pass could write hero-specific reactions or alternate paths for Knight/Ranger (Sir Bram was the master's contemporary; the Ranger's mentor knew Alduin). Out of scope here; flag for a follow-up doc.
- **The other pets' origin stories** (Aether Sprite & Flame Pup deeper backstory beyond the bible). These should each get a dedicated short questline in v2 — _The Sprite's First Song_, _The Pup's First Hearth_ — to match the depth the Ice Wolf gets in v1. Out of scope here.

---

_This doc is the storyline contract. The system doc (`dungeons-system-design.md`) is the build contract. Together they're enough for Claude Code to author every quest JSON file and every lore stone in the dungeon MVP. Anything in this doc that conflicts with the system doc — the system doc wins on mechanics; this doc wins on voice and meaning._
