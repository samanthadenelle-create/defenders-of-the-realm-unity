**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 238 — Sylas First Meeting: Narrative, Dialogue & Party Join

**Status: READY TO IMPLEMENT**
**Author:** UI (creative lane)
**WO Number:** 238
**Date:** 2026-06-02
**Ties to:** WO-231 (party assembly), WO-227 (companion system)

---

## The problem

Sylas currently spawns standing in the village with no introduction. The player has no reason to
trust him, no moment of connection, and no sense that a party is forming. He is furniture.

This WO writes the meeting, per hero, and specifies the trigger, the dialogue sequence, and the
join moment.

---

## The meeting — what happens

On game start (after hero select, scene loaded), Sylas is positioned near the Heartwood plaza.
He is not idle — he is crouched at the base of the Heartwood, examining the roots. He rises when
the hero approaches within ~5m. A short dialogue exchange plays. He joins.

The whole thing takes about 20–30 seconds. It is not a cutscene — the player can move during it.
Sylas speaks, the hero responds (text only, no hero VO required), Sylas joins party.

---

## Dialogue — per hero

All four versions share the same structure:
1. Sylas's opening line — reads the hero, not the situation
2. Hero response (one line, text bubble only)
3. Sylas's join line — the reason he stays

---

### Hero: Thrain (Wizard)

**Sylas** *(rising from the roots, dusting off his hands)*:
> *"I've been out on the roads for two weeks. Three bone-tags placed, two more sightings I didn't
> stop to mark. The Choir is closer than the Choristers think."*

**Thrain** *(player text)*:
> *"Then you'd better stay."*

**Sylas** *(beat — he looks at Thrain, then back at the tree)*:
> *"I was going to say the same thing to you."*

*Sylas joins party.*

---

### Hero: Grom (Knight)

**Sylas** *(rising, noting the armour)*:
> *"Sir Grom. I heard you were still here. I wasn't sure I believed it."*

**Grom** *(player text)*:
> *"Where else would I be?"*

**Sylas** *(dry, almost a smile)*:
> *"Fair. I've got eyes on the roads — you've got the wall. Between us,
> maybe the tree makes it through the night."*

*Sylas joins party.*

---

### Hero: Sylas (Ranger — Thrain meets YOU instead)

*Thrain is at the Heartwood when Sylas (you) arrives.*

**Thrain** *(not looking up from the roots)*:
> *"You're back earlier than expected. That usually means the news is bad."*

**Sylas** *(player text)*:
> *"It's bad."*

**Thrain** *(standing, quietly)*:
> *"Tell me while we walk. The east wall needs checking and I can't leave the
> Heartwood unattended. So now neither of us can."*

*Thrain joins party.*

---

### Hero: Elara (Healer)

**Sylas** *(rising, reading her kit)*:
> *"Healer. Good. The last wave took three people off their feet and I had to
> patch them myself. I am not good at it."*

**Elara** *(player text)*:
> *"I can see that. Walk with me."*

**Sylas** *(falling into step)*:
> *"I was already planning to."*

*Sylas joins party.*

---

---

## Elara joins — after wave 3 cleared

*The third wave breaks. The village is quiet for a moment. Elara walks out of the shrine doors — she has been inside the whole time, tending wounded. She crosses the plaza toward the Heartwood and stops near the hero.*

**Elara** *(matter-of-fact, not introducing herself — she assumes you know)*:
> *"Three waves. I've had eleven people through my hands since the first one. Two I'm not sure about."*

**Hero** *(player text)*:
> *"Then you should be out here."*

**Elara** *(a pause — she looks at the Heartwood, then back)*:
> *"That's what I decided. I work better when I can see what I'm keeping people alive for."*

*Elara joins party.*

**Elara ambient lines (travel — fires max once per 90s):**

> *"The shrine has more wounded than I've seen in one season. That's not nothing."*

> *"If someone takes a bad hit, tell me before they walk it off. Walking it off is how people die quietly."*

> *"I know what the Choir sounds like at the edge of the valley. I've been hearing it in my sleep."*

> *"The tree looks healthier than it did last month. I don't know if that's my eyes or something real."*

> *"Keep moving. Standing still in the open is an invitation."*

---

## Grom joins — on first return from the OuterWorld

*The hero returns through the gate after their first trip into the open world. Grom is waiting just inside — not pacing, not anxious. Standing. He has been standing there.*

**Grom** *(as the hero comes through the gate)*:
> *"How far did you get?"*

**Hero** *(player text)*:
> *"Far enough to know what's out there."*

**Grom** *(nods slowly)*:
> *"Then you'll need someone who can hold a line while you work. I've been holding this one for twenty years. I can hold yours too."*

*Grom joins party.*

**Grom ambient lines (travel — fires max once per 90s):**

> *"The east wall has a section that bothers me. I'll show you when there's time."*

> *"I've buried better knights than me on that road. They trusted the road. I don't."*

> *"The Folk watch how we walk. Walk like we know what we're doing."*

> *"I don't sleep much anymore. Turns out that's useful."*

> *"The Heartwood grew another hand's width since last week. I measured it."*

---

## Hero-specific variants — when you ARE Elara or Grom

### If player IS Elara — post-wave-3 (self-reflection version)

*Elara survives the third wave herself. A beat of quiet. She looks at her own hands, then at the Heartwood.*

**Elara** *(to herself, then to the nearest party member)*:
> *"Three waves. Eleven people through my hands. I keep telling myself I came back to help.*
> *I think I came back because I didn't know what else to do."*

**Party member** *(Sylas, player text)*:
> *"That's enough reason."*

**Elara** *(quietly)*:
> *"I'm choosing to believe that."*

*No join beat — she was already here. The party bar simply updates to show her portrait as active.*

---

### If player IS Grom — world-return variant (brash version)

*Grom returns from his first trip into the OuterWorld. He walks back through the gate and stops. Looks back at the dark. Then forward at the Heartwood.*

**Grom** *(to himself)*:
> *"Twenty years holding this wall. First time I've been on the other side of it.*
> *Turns out it's worse out there."*

**Party member** *(Sylas, player text)*:
> *"I've been trying to tell you."*

**Grom** *(a short laugh — the first one)*:
> *"I know. I needed to see it myself."*

*No join beat — Grom was already party-joined. The line just fires as ambient dialogue on world return.*

---

## The join moment

After the final line, a small UI beat plays:
- Sylas's portrait appears in the party bar (bottom of screen)
- Text fades in beneath his portrait: **"Sylas joined your party"**
- His nameplate appears above his head in the world
- He begins following the hero

No fanfare. No particle burst. The quiet acknowledgement IS the moment.

---

## Subsequent travel dialogue (ambient lines)

Once Sylas is in the party, he should occasionally say something as you move through the village
or approach the walls. These are short, unprompted, and fire no more than once per 90 seconds.

**Ambient lines (pool — pick at random):**

> *"The north treeline was clear this morning. It won't be tonight."*

> *"Someone left a gate lantern unlit. Small thing. Matters."*

> *"The tree looks different than it did last season. Taller, I think. Or I'm imagining it."*

> *"I've been marking the Choir for ten years. I've never seen them this organised."*

> *"There's a ridge east of the valley. If I were them, I'd come from there."*

> *"Don't let the Folk see you worried. They watch us to know whether to be scared."*

---

## Technical spec

```
Trigger:    On scene load, after hero spawns. Check distance hero→Sylas each frame.
            When distance < 5m AND firstMeetingPlayed == false → begin sequence.

Files:
  Assets/_Modules/Village/Companions/SylasCompanion.cs  (new)
  Assets/_Modules/Village/Companions/CompanionDialogue.cs (new — shared base)

Flow:
  1. Sylas plays "rise" animation (or snap-stand if no anim available)
  2. Dialogue box appears above Sylas — line 1
  3. Hero response appears as text bubble (no VO needed) — tap/click to advance
  4. Sylas line 2 — tap/click to advance
  5. Party join UI beat (portrait + "joined" text)
  6. Sylas enters follow state (stays within ~4m of hero)
  7. firstMeetingPlayed = true (save to PlayerPrefs — don't replay on re-enter)

Ambient dialogue:
  SylasCompanion.cs polls a cooldown timer (90s). On fire: pick random line
  from ambient pool, display above Sylas for 4s, fade out.

Hero-specific dialogue:
  Load correct dialogue set from HeroClass stored in GameSession.SelectedHero.

No UXML. No UIDocument. Code-built dialogue boxes (same pattern as NPC dialogue
— World-space Canvas, Text component, fade coroutine).
```

---

## Acceptance criteria

- [ ] Sylas says his opening line when hero walks within 5m on first load
- [ ] Correct dialogue loads based on hero class
- [ ] Party join UI beat plays after final line
- [ ] Sylas follows hero after joining
- [ ] Meeting does not replay on scene reload (PlayerPrefs flag)
- [ ] Ambient travel lines fire during normal play, max once per 90s
- [ ] No UXML/UIDocument used
- [ ] Brace balance check passed

---

*Dialogue copy is final. Do not paraphrase. VO is optional for hero responses — Sylas lines
are the priority if voice is being recorded.*

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
