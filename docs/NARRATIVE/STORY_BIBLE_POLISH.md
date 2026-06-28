# Story Bible — Polish Pass (Echoes of Elarion)

**Status:** ACTIVE narrative canon (polish-phase consolidation, 2026-06-28). Front-of-house
story bible for the **single-Knight north star**. Where this doc and any older lore doc
disagree, **this doc wins on premise, cast, and tone**; the older docs are flagged in §7.

**Reconciled to (authoritative sources):**
- `docs/COMBAT_PIVOT_NORTHSTAR.md` (single Knight; echoes/life-force reclaim loop; Heart = world-tree)
- `docs/ART/GAME_COVER_ART_DIRECTION.md` (tone, palette, the cover's emotional register)
- `CANON_GROUND_TRUTH_2026-06-26.md` + `SESSION_CANON_LOADER.md` (current reality)
- The canonical 5-slate intro (reproduced in §6) — the doc's premise agrees with it line-for-line.

> **Canon locks (do not deviate):**
> - World: **Elarion**, centred on the **Heart of Elarion** — a living **world-tree** at world centre `(0,0,0)` whose light/aether is *the breath of all living things.* It is **dimming** because the aether is being siphoned.
> - Hero: **ONE Knight, "Grom"** — single Tripo model, static armor, sword + shield the only visible flair. No party, no companions in combat. Everything else is autonomous.
> - Antagonist: **the Hollow Ones** — **grief, not evil.** Broken, emptied souls hollowed by loss, drawn to the Heart's last warmth. The **orc legion** is the martial arm of the threat.
> - Economy = lore: **echoes** (spirits released from the Tree) autonomously harvest **wood / iron / grain**. **Life force** is the keystone — drive the dark back → the Tree strengthens → echoes harvest faster → the world heals. Lore and math are one sentence.
> - Palette/tone: obsidian + gold, **life-force gold-green** as the soul accent. Melancholic heroic defiance — *grief and hope*, never cackling villainy.

---

## 1. World Premise (final)

Once, the **Heart of Elarion** — a colossal living world-tree at the centre of the realm —
blazed with a gold-green light that was, quite literally, the breath of every living thing:
its aether ran through root and river and lung alike, and while it shone, the realm was warm.
Then came **the Dimming** — a grief older than memory — and the Heart's light began to fail
as its aether bled out into a creeping grey. From that fading rose **the Hollow Ones**: not
monsters but the broken, souls emptied by loss and drawn helplessly toward the last warmth
they could still feel, with an **orc legion** marching as their martial spearhead to tear the
remaining light loose. One knight answered. **Grom**, sworn to carry a single ember back into
the dark, sets out alone to drive the grey back so the Tree can heal — and as it heals, the
spirits it releases stir, gather, and begin to rebuild the world. This is not a war story with
a villain to slay; it is a **reclamation** — the player is the one warm, defiant thing holding
the line while a wounded world learns to grow again.

---

## 2. The Heart / Echo / Reclaim Loop — lore and math in one breath

**The lore:** The Heart's **aether** is its life force. When the Hollow Ones and the orc legion
press in, they siphon it — the Tree dims, the land greys, the world dies a little. When Grom
drives them *back* and reclaims ground, the siphon eases and the Tree's life force **grows**.
A stronger Tree releases more **echoes** — small, drifting spirits of life (soft teal-green
wisps with a warm core) — who wander out, gather what the settlement needs, and drift home. A
brighter Tree births stronger, more numerous spirits. **Reclaiming the world *is* healing the
Tree *is* growing the workforce.** Darkness recedes → the Heart grows → the spirits multiply.

**The math (the same sentence, stated as systems):**
- **Life force = f(territory reclaimed / outposts cleared).** Offense is a *persistent world state*,
  not transactional loot: every cleared front permanently raises the meter. One meter, one cause,
  one effect — kept legible (a brighter Tree, multiplying spirits, a glanceable life-force bar).
- **Echoes harvest exactly three grounded resources — WOOD, IRON, GRAIN** (no fantasy-currency
  creep; **gold** is the separate *store* currency). Wood → structures/upgrades; iron → hero
  gear (weapon/shield/armor stats); grain → troop upkeep (V2 autonomous defenders).
- **Two scaling axes off the one life-force meter:** (a) **rate** — existing echoes gather faster
  as life force rises; (b) **breadth** — life-force thresholds *birth a new echo*, a real,
  visible event. The workforce is a small bounded crew (3 organic, one per resource, + up to 2
  flex; **cap 5**) — the game-feel cap and the perf cap are the same cap.
- **Player agency = one placement, then autonomy.** Drag a spirit onto a resource to assign it;
  it then harvests passively. "Render the flavor, fake the sim" — a wisp drifts out and back, a
  legible "+wood" — passive to play, engaging to watch. The return-hook: it works while you're away.
- **The chain closes:** reclaim → life force → echoes → wood/iron → gear & skills → reclaim further.

The Tree is a **living progress monument**: the player *sees* the world heal as they play. That
visible feedback is the emotional core — the cover's promise ("as long as one defender stands,
the light is not out") made into a loop.

---

## 3. The Knight's Arc — Grom

**Who he is.** A grounded, seasoned human knight — broad but practical, weary but unbroken. Not
a demigod, not a chosen-one prophecy; a *person* who turned toward the dark when others turned
away. He wears battle-worn dark steel over brown leather, matte and scratched. Helmet off (or
open-faced): the grief tone wants a face, jaw set, eyes catching the Tree-light. His armor is
**static canon** — no glowing magic plate, no mesh-swap; his sword and **kite shield** carry all
visible progression, and the shield is a *real mechanic* (the block).

**What he carries.** A single **ember** — a fragment of the Heart's living light, the last warmth
he is sworn to carry back into the dark. It is both literal (the quest object of the intro) and
thematic: he is one small, stubborn flame against a grey tide. The ember is the player's stake in
the world — every reclaimed front is the ember spreading.

**His motivation (grief mirrors the enemy).** Grom and the Hollow Ones are drawn to the *same*
warmth; the difference is what they do with the grief. The Hollow Ones collapse inward toward the
light to be filled. Grom carries the light *outward* to defend it. He does not hate them — he
**mourns** them, even while ending them. He has been at this a long time and he will stay. His
arc is not "defeat the dark lord"; it is *hold the line long enough for the world to heal itself.*

**V1 scope (what we actually build).** Solo offense: Grom raids enemy outposts/strongholds, fights
in the isolated real-time BattleArena (and the separate flat ATB), spends an ability kit
(basic/ranged/heal-sustain/burst, shield-block as a timed reaction to enemy wind-ups), and unlocks
a heal+ranged **skill tree** funded by reclaimed resources. Every cleared front raises life force,
which feeds the echoes, which fund the next push. **Base-building / autonomous troop defense is V2**
(gated `ff.basebuilding`) — Grom's home hub (the castle) is hero home + skill tree + raid launch in V1.

---

## 4. The Hollow Ones (and the orc legion)

**Grief, not evil — the binding tone.** The Hollow Ones are **the broken**: souls emptied by the
Dimming's loss, hollowed husks drawn toward the Heart's last warmth because it is the only thing
they can still *feel*. They are **not** a snarling demon horde and **not** a Sauron-style dark
lord with a plan. They are sorrowful, gaunt, half-there — mourning made visible. Decay, not malice;
a tide of cold grief leaching colour from the world simply by approaching it. The tragedy is that
they want the *same* thing Grom protects — the light — and cannot be reasoned out of reaching for
it. Some, at the edges, remember enough of who they were to be pitied; most are too far gone.

**The orc legion — the martial arm.** The Hollow Ones are a *tide*, not soldiers. The **orc legion**
(warrior / tank / mage, the V1 enemy family) is the *muscle*: a martial host that moves with the
grey, broad-shouldered and tusked, hide-and-iron, jagged weapons. They are the readable, fightable
threat at the front line — the thing Grom actually trades blows with — while the Hollow tide is the
mournful pressure behind them. (A driving intelligence behind the siphon — an orc necromantic
figure feeding on the aether — may anchor the threat as the arc deepens; keep it a *cause of grief*,
not a cackling overlord. See §7 for reconciling the older "Alduin / Necromancer" names.)

**How they should READ in combat (without betraying the tragedy):**
- **Telegraphs, not snarls.** The orc wind-up *is* the mechanic (the player reads it and blocks/
  heals/times the poke). Animations carry weight and threat through *force and fatigue*, not cartoon
  rage — a slow, heavy, inevitable advance reads more tragic and more dangerous than gnashing.
- **Colour does the storytelling.** The Hollow drain *desaturation* — where they are, the world goes
  grey and cold; where Grom holds, the gold-green returns. The enemy's presence is felt as the
  *world dimming*, which keeps the grief in the frame even mid-fight.
- **Quiet, mournful audio.** Low, sorrowful tones and a counter-key that drags against the Tree's
  note — the Hollow "sing" grief, they don't roar. A defeated Hollow One should read as *released*
  (a soft fade to light), not gorily destroyed — you are ending suffering, not scoring a kill.

---

## 5. Vendor / NPC Voices (hub dialogue guides)

**House style for all settlement folk:** grounded, weary-but-warm. These are people who have held
on through the Dimming — short sentences, plain old-bones vocabulary, dry humour at the edges, real
fondness underneath. They are *tired*, not grim; they tease the Knight because they're glad he's
here. No high-fantasy purple, no quest-giver exposition dumps. Hope is earned and understated.

### Brom — Innkeeper / quest-giver (the hub's hearth)
The connective tissue: every rumor and road-tale passes through his hearth. Warm, unhurried, a
little gossipy; the man who keeps morale alive by keeping the fire lit.
> - *"Pull up a chair, Grom. Every tale in Elarion comes through this hearth eventually — and you look like you've got a few."*
> - *"Road to the old outpost's gone quiet. Quiet's never good these days. Might be worth a look, if you're walking that way."*
> - *"You bring the light back a little further each time you go out. Folk notice. They don't say it, but they notice."*

### The Echo Warden — keeper of the spirits (pet-house)
Tends the echoes the Tree releases; speaks of them with quiet reverence, half-caretaker, half-priest.
Gentle, plainspoken, never mystical-for-its-own-sake. (This role supersedes the old "pet trainer.")
> - *"They're not pets, and they're not ours. The Heart lets them go, and they come to the work that needs doing. We just give them somewhere to come home to."*
> - *"Push the dark back and the Tree breathes easier — and when it breathes easier, more of them wake. You'll see it. A brighter tree, more little lights."*
> - *"Set one to the wood, one to the iron, one to the grain. After that, leave them be. They know the way better than we do."*

### The Forge / Smith (weapon & shield vendor)
Gruff, proud of the craft, sizes up your gear before they size up you. Grumbles, then helps.
A practical soul who measures hope in good steel.
> - *"Hmph. Starter blade. That edge'll fold the first time an orc means it. Bring me iron and I'll give you something that won't."*
> - *"A shield's not for show, lad — it's the difference between the next swing and the last one. Mind it."*
> - *"Good iron's coming up out of reclaimed ground again. First time in years. That's your doing. Don't waste it."*

### Generic vendors — tone note
Keep every incidental vendor in the same key: **weary settlement-folk, warm underneath.** One or
two lines each, grounded in *their* trade and the shared strain of the Dimming. They reference the
world *healing* as Grom reclaims ground ("colour's coming back to the market square," "first full
grain-cart in a season") — small, concrete signs, never grand speeches. No villain monologues, no
lore lectures: the lore lives in *how tired and how grateful* they sound.

---

## 6. Consistency Matrix

The canonical intro is the anchor; every surface below must agree with it and with the cover.

**Canonical intro (5 slates, ~30s):**
1. The Heart ablaze in golden light over a thriving realm — *"Once, the Heart of Elarion blazed — a world-tree whose light was the breath of all living things."*
2. The Dimming — the tree half-dark, aether bleeding into grey mist — *"Then came the Dimming: a grief older than memory, and the Heart's light began to fail."*
3. The Hollow Ones — sorrowful emptied silhouettes drifting toward the fading tree; an orc legion banner — *"The Hollow Ones rose — not monsters, but the broken, drawn to the last warmth they could feel."*
4. The Knight's call — Grom silhouetted against the dim tree, an ember in hand — *"One answered. A knight, Grom, sworn to carry a single ember back into the dark."*
5. The reclaim — Grom steps forward as a sliver of gold returns to the tree, echoes stirring — *"Drive back the dark. Let the Heart grow. Reclaim the light of Elarion."* → title card.

| Surface | Premise asserted | Agrees with intro + cover? |
|---|---|---|
| **Intro (5 slates)** | Living world-tree dims (grief); Hollow Ones = broken, not monsters; lone Knight Grom + ember; reclaim grows the Tree | ✅ Anchor |
| **Cover key art** (`docs/ART/GAME_COVER_ART_DIRECTION.md`) | One Knight Grom, static steel + shield, split living/dying world-tree, echo motes, Hollow as grey grief + orc silhouettes, obsidian/gold/gold-green | ✅ Same premise, same palette, same "grief not Sauron" |
| **This Story Bible** (§1–§5) | Identical premise, cast (Grom solo), antagonist framing, echo/life-force loop | ✅ By construction |
| **Combat north star** (`docs/COMBAT_PIVOT_NORTHSTAR.md`) | Single Knight; echoes = autonomous harvesters; life force links offense→economy; reclaim = world state | ✅ This doc is its narrative skin |
| **Ground truth** (`CANON_GROUND_TRUTH_2026-06-26.md`) | Knight "Grom"; Tripo single model static armor; V1 Knight + orcs | ✅ |
| **Hub NPC dialogue** (§5, Brom / Echo Warden / Forge / generics) | Weary-warm settlement folk; echoes-not-pets; world visibly healing as you reclaim | ✅ Reinforces the loop in-fiction |
| **Resource/UI strings** | Wood / Iron / Grain gathered; Gold = store currency; one life-force meter | ✅ No currency creep |

**One-line tonal north star (shared with the cover):** *a single weary knight holding the last
warm light of a dying world-tree against a grey tide of grief — defiance and mourning, not triumph.*

---

## 7. Lore Contradictions Found (older docs vs. current canon)

The following older narrative docs predate the 2026-06-22 single-Knight pivot and/or the 2026-06-26
"living world-Tree is canon" ruling. Each is flagged with a recommended resolution. **None should be
treated as live premise; cite this bible + the north-star/ground-truth instead.**

**`docs/STORYLINE.md` — "Elarion-of-the-Spire" (v2 storyline)**
- **Burned tree + Cathedral Spire premise.** The whole doc reframes the world around the Heart-Tree
  having *burned a hundred winters ago*, replaced by a singing **Cathedral Spire**. This directly
  contradicts the **living world-tree** canon (the Tree is *dimming, not dead*). It already carries a
  STALE banner (the Spire reversal, 2026-06-26). **→ RETIRE the Spire premise entirely.** Keep, at
  most, the *tone vocabulary* (mourning, "the dark was kind once") as flavour; supersede the body.
- **Three playable heroes (Wizard default = "the Keeper," Knight = "Sir Bram," Ranger = "Nessa").**
  Contradicts the single-Knight (Grom) canon. **→ SUPERSEDE:** V1 protagonist is Grom only; Ranger/
  Wizard are deferred classes, not a starting cast, and the "Keeper/Chorister" framing is retired.
- **Knight named "Sir Bram of the Last Banner."** Name drift — the canonical Knight is **Grom**.
  **→ RECONCILE: lock the Knight = Grom; retire "Sir Bram."**
- **Three bondable pets (Twilight/Aether Sprite, Flame Pup, Ice Wolf) drawing on the Spire's song.**
  Contradicts **echoes = anonymous autonomous harvester-spirits** (wood/iron/grain), not named
  combat/tending companions. **→ RETIRE the pet roster** as combat/bond pets; the spirit concept
  survives only as the (un-named, bounded) echo workforce. (See also the Echo Warden voice, §5.)
- **Syndrath the Devourer (apex dragon wave-boss / cinematic fly-by) and Alduin the Mournful
  (composite necromancer of dead Keepers) as the antagonists.** Contradicts the **Hollow Ones =
  grief + orc legion** antagonist framing. **→ RETIRE Syndrath; RECONCILE the antagonist** to the
  Hollow Ones (grief husks) fronted by the orc legion. If a singular driving intelligence is wanted,
  fold it into an **orc necromantic siphoner** (a *cause of grief*, tragic), not Alduin/Syndrath.
- **Buildable Arcane Tower / wall-repair as the only defences; wave-defence as the core loop.**
  Belongs to the retired tower-defence pillar; base/tower defense is **V2-gated (`ff.basebuilding`)**.
  **→ SUPERSEDE for V1** (V1 = solo offense; defense is gated/later).

**`docs/PARTY_OF_FOUR_STORYLINE.md` — "The Party of Four"**
- **Entire premise (assemble a 4-member party before leaving town; party-vs-party targeting AI).**
  Directly and wholly contradicts the **single controllable hero** north star (companions were *cut*
  — they were a net negative + bug source). **→ RETIRE this doc in full.** It is the clearest
  contradiction in the set. Its own §100 "canon reconciliation needed" list (premise fork, three
  apex antagonists, Avalon contamination, pet-name drift, Bram/Brom collision) is now *resolved by
  this bible* — keep that list only as a historical record of what we fixed.

**`docs/dungeons-storyline.md` — Dungeons questline arc**
- **Protagonist = "the Keeper" (Wizard/any class); pets (Aether Sprite, Ice Wolf) as story
  companions.** Contradicts single-Knight Grom + echoes-not-pets. **→ RECONCILE:** rewrite the
  protagonist to Grom; drop the pet-bond beats (or recast them as echo/life-force beats).
- **Alduin the Mournful as the central antagonist + the "composite of drank Keepers" mythology +
  Syndrath references.** Same antagonist contradiction as STORYLINE. **→ RECONCILE to the Hollow
  Ones / orc legion framing;** the "the dark was kind once / every Hollow One was somebody" *theme*
  is excellent and **on-tone — KEEP the theme, retire the Alduin/Keeper-cycle specifics.**
- **Premise leans on the burned-Tree / Spire world (Old Elarion, the Wound, the cycle).** **→
  SUPERSEDE** the burned-world framing; the *mourning-story spine* ("this is a mourning story, not a
  war story") is fully compatible with the living-Tree canon and should be **preserved and reused**.
- **Resource/economy drift (in `DESIGN_VENDOR_STORYLINES_AND_QUESTLINES.md`, the dungeons' sibling
  doc): Wood/Food/Iron/AetherCrystal/Glimmer.** Contradicts the locked **Wood/Iron/Grain + Gold**
  (no AetherCrystal/Glimmer currency creep). **→ RECONCILE** all economy references to the three
  gathered resources + gold. (That vendor doc is already STALE-bannered; its *NPC names* — Brom the
  innkeeper, the forge smith, etc. — remain usable and are carried into §5 here.)

**Cross-cutting name notes (for the polish pass):**
- **"Grom" (Knight, canon) vs "Brom" (Innkeeper, §5).** A near-homophone collision (and the old
  "Sir Bram / Old Bram" tangle feeds it). They are kept distinct per current canon, but flag for the
  owner: consider renaming the innkeeper if voiced lines or subtitles risk confusion. **→ OWNER CALL.**
- **"Avalon" contamination** (retired village name) reportedly still lingers in older docs/port-specs.
  Canon village name is **Elarion**. **→ PURGE on contact** wherever found (per DESIGN-DECISIONS #1).

---

*Living doc. As the world, the Knight's kit, and the hub NPCs get built, extend this — but keep the
premise (living dimming world-tree), the cast (Grom alone), the antagonist tone (grief, not evil),
and the loop (reclaim → life force → echoes → heal) locked. They are the spine the cover sells.*
