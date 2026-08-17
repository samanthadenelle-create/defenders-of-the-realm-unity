<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-227: Opening Cutscene & Story Companion System

**Status: READY TO IMPLEMENT**

**Date:** 2026-06-01  
**Priority:** 🟡 HIGH (narrative backbone, tutorial integration)  
**Owner:** CLI  
**Depends On:** WO-226 (all 4 heroes defined), WO-222 (tutorial structure)  
**Blocks:** WO-222 implementation (tutorial guide system)

---

## Vision

Game opens with:
1. **Hero Selection** — Player picks Knight/Archer/Mage/Cleric
2. **Opening Cutscene** — Hero meets a story companion (NPC)
3. **"The Two Set Out"** — Hero + companion journey begins
4. **Companion as Guide** — Second character provides tutorial prompts naturally

This replaces generic tutorial UI with **organic narrative guidance**. Companion directs player toward mechanics (tower placement, resource gathering, combat) through dialogue and quest pointers.

---

## Implementation

### Phase 1: Story Companion System

**Create companion framework:**
```csharp
public class StoryCompanion : MonoBehaviour
{
    public string companionName;
    public Sprite portraitSprite;
    public AudioClip voiceIntro;
    
    public void SayDialogue(string text, TutorialPhase phase)
    {
        // Show dialogue box with portrait
        // Play voice (optional)
        // Point to relevant UI/mechanic
    }
}
```

**Companions (one per hero):**
- **Grom (Knight):** Meets grizzled veteran mentor
- **Sylas (Archer):** Meets rogue scout partner
- **Thrain (Mage):** Meets apprentice or fellow scholar
- **Elara (Cleric):** Meets temple acolyte or pilgrim

### Phase 2: Opening Cutscene

**Structure:**
```
1. Hero select screen → Player chooses (Grom example)
2. Fade to: Village edge / forest path (cinematic angle)
3. Hero walks, cutscene camera pans
4. Mentor appears, dialogue exchange
5. Fade to: Map/first zone
6. Tutorial begins with companion at hero's side
```

**Dialogue example (Grom + mentor):**
```
Mentor: "So, you've answered the call to defend Elarion?"
Grom: "The heart of the village needs protection."
Mentor: "Then we'd best fortify those gates. Come, I'll show you how."
→ Tutorial Phase 1: Tower placement (companion explains green/red grid)
```

### Phase 3: Dynamic Tutorial Guidance

**Companion replaces generic UI prompts:**

Instead of:
```
[UI POP-UP] "Click on a grid spot to place a tower"
```

Use:
```
Companion: "See that green square? That's where we can build. 
           A tower here would cover the eastern gate."
→ Highlights grid spot
→ Quest marker appears
```

### Phase 4: Hero-Specific Narrative

Each hero gets personalized intro that hints at their playstyle:

**Grom intro:**
```
Mentor: "Your strength will be tested here. 
         Build towers where they'll have the most impact."
→ Emphasizes tanking/positioning strategy
```

**Sylas intro:**
```
Scout: "We need to scout ahead. Gather supplies from those camps,
        then we can build stronger defenses."
→ Emphasizes mobility/resource gathering
```

**Thrain intro:**
```
Scholar: "Ancient magic flows through these lands.
          My spells will protect the village while you build."
→ Emphasizes spell support
```

**Elara intro:**
```
Acolyte: "We mustn't let them reach the Heart.
         I'll mend what's broken while you fortify our walls."
→ Emphasizes healing/support role
```

---

## Integration with WO-222 (Tutorial)

**WO-222 phases become companion dialogue:**

| Tutorial Phase | Current (WO-222) | New (With Companion) |
|---|---|---|
| 1 | UI: "Attack enemies" | Companion: "Show them your skill!" |
| 2 | UI: "Place towers here" | Companion: "Build a tower on that green spot" |
| 3 | UI: "First wave incoming" | Companion: "Stay sharp, here they come!" |
| 4 | UI: "You need more crystals" | Companion: "We need supplies. Head to those camps." |
| 5 | UI: "Gather resources" | Companion: "Go. I'll hold the line here." |

---

## Technical Implementation

### Cutscene Pipeline
1. **After hero select:** Trigger opening cutscene
2. **Cutscene system:** Timeline/Cinemachine for camera + animation
3. **Dialogue system:** Dialogue box with companion portrait + text
4. **Audio:** Optional voice acting (can start with text-only)
5. **Transition:** Cutscene → Tutorial Phase 1 with companion present

### Save System Integration
- Store which hero + companion player chose
- Load companion state on resume
- Companion follows player (visual presence optional)

---

## Acceptance Criteria

- [ ] Story companion class created (name, portrait, dialogue system)
- [ ] 4 companions defined (one per hero)
- [ ] Opening cutscene playable after hero select
- [ ] Cutscene plays dialogue, shows companion meet hero
- [ ] Companion appears in tutorial phases (Phase 1–5)
- [ ] Companion dialogue replaces generic UI prompts
- [ ] Tutorial flows naturally (feels like story, not fetch quests)
- [ ] Each hero has personalized companion + narrative flavor
- [ ] WebGL tested: opening cutscene → tutorial feels cohesive
- [ ] Commit: "WO-227: add opening cutscene and story companion system"

---

## Benefits

- **Narrative immersion:** Tutorial feels like story, not mechanical checklist
- **Replayability:** Different hero = different companion = different narrative tone
- **Guided learning:** Companion naturally points player toward next mechanic
- **Character building:** Each hero's companion hints at their playstyle

---

## Notes

- Companion dialogue should be **brief** (1–2 sentences max per prompt)
- Companion should **point, not shout:** Highlight grid spots, show quest markers
- Voice acting optional initially (can add later, use text for now)
- Companion model can be low-poly (just needs portrait + maybe overworld presence)
- Each hero's intro should take ~30–60 seconds total

---

**Estimate:** 3–4 hours (cutscene setup, 4 companion systems, dialogue integration, testing)
