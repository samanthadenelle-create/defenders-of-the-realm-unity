**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 235 — Hero Death Screen + Heartwood Destroyed Screen

**Status: READY TO IMPLEMENT**
**Author:** UI (creative lane)
**WO Number:** 235
**Date:** 2026-06-02
**Closes:** DEF-102 (hero 0HP death — screen half), new feature

---

## Canon note — The Heartwood

The central protected object is now called **the Heartwood** throughout this game.

> The heartwood is the real word for the preserved inner core of a tree — the dense, ancient part that doesn't decay after the trunk is gone. It is what the stump of the Heart-Tree IS. The Folk raised pale stone over it, but the Heartwood is still down there: the root system still runs beneath Elarion, and the song still rises through the stone they built around it. The spire is the instrument. The Heartwood is the source.

**In gameplay sentences:** *Defend the Heartwood. The Heartwood sings. The Heartwood has gone quiet.*
**Replaces:** "the Spire," "the Cathedral Spire," "the Heart of Elarion" — all the same object, one name now.
**Code:** `HeartController` remains the class name (no rename needed). Display name in UI = "the Heartwood."

---

## Two screens, two different emotional registers

These are not the same event. One is a setback. One is a catastrophe.
They must feel completely different or the player doesn't understand the stakes.

---

## Screen 1 — Hero Death ("You Fell")

### What happened
The hero reached 0 HP. The Heartwood is still singing. This is recoverable.

### Emotional register
**Quiet. Not shameful.** The world didn't end. You just went down.
The Heartwood held without you — this time. Rest. Come back.

### Narrative copy (code-built UI, no UXML)

**Header line (large, centred):**
> *The chord held.*

**Body line (small, muted, beneath):**
> *You fell — but the Heartwood still sings. The Folk will find you.*

**Buttons:**
- `[Rise Again]` — respawn at last safe point, full HP
- `[Rest Here]` — quit to main menu

### Visual treatment
- Black background, fade in over ~0.6s
- Single violet glow point at centre (the Heartwood's note, visualised as a dim pulse) — reuse existing spell VFX, additive layer, very subtle
- Header text: white, ~28px, font-weight 500, centred
- Body text: muted grey, ~14px, below header with ~16px gap
- Buttons: minimal — outline style, no fill, spaced below body text
- No death sound. Silence for ~1s before a single soft musical tone (the Heartwood's chord) plays
- No skull icon, no blood, no dramatic sfx — the tone is elegiac, not punishing

### Technical spec

```
Trigger:    HeroHealth.OnDeath event fires (HP <= 0, _isDead = true)
Flow:       1. Disable HeroLocomotion + HeroAbilities
            2. Fade screen to black (0.6s)
            3. Show death UI (code-built — NO UIDocument/UXML)
            4. [Rise Again] → HeroHealth.Respawn() → fade back in
            5. [Rest Here] → SceneManager.LoadScene("MainMenu")

File:       Assets/_Modules/HUD/DeathScreenController.cs (new)
            Hook into HeroHealth.OnDeath
            Code-built Canvas: black panel → header label → body label → 2 buttons
            No UXML. No USS. Inline code-built styling only (CLAUDE.md §8).
```

---

## Screen 2 — Heartwood Destroyed ("The Root Went Silent")

### What happened
The Heartwood has been destroyed. The chord the Folk have held for a hundred winters is gone. The root that survived the burning of the Heart-Tree has finally been severed. This is the true game over.

### Emotional register
**Catastrophic. Irreversible. The world lost.**
Not "try again." This is the thing the whole game exists to prevent.
The player should feel the weight of a hundred years — and ten thousand before that — ending in a single beat.

### Narrative copy

**First line (large, slow fade in):**
> *The root went silent.*

**Pause (~2s). Then second line fades in (medium, white):**
> *A hundred winters of song. The Heartwood held the last of what the Tree knew — and now the valley learns what silence sounds like.*

**Pause (~1.5s). Then final line fades in (small, muted):**
> *The Withering does not stop at the valley. It does not stop anywhere.*

*(That last line is pulled verbatim from the narrative bible — it earns its place here.)*

**After all three lines are visible (~1s pause), buttons appear:**
- `[Begin Again]` — full restart from hero select
- `[Return to Elarion]` — load last autosave before the final wave (if one exists)
- `[Rest]` — quit to main menu

### Visual treatment
- Black background, but it arrives differently: the screen **dims from the edges inward** (vignette collapse, ~1.5s) rather than a flat fade — the world going dark at the periphery first
- No glow. No VFX. The absence of the violet pulse IS the design — the Heartwood light that was present on the hero death screen is simply gone here. Emptiness is the point.
- All three text lines animate in separately with the pauses above
- **Game over music plays as the screen arrives:** `Assets/Resources/Audio/Music/GameOver.mp3` — load via `Resources.Load<AudioClip>("Audio/Music/GameOver")`, play on a dedicated `AudioSource` (not through `AudioService` — bypasses the SFX pool, plays clean). Fade out over ~3s after buttons appear.
- Buttons appear without fanfare — just opacity fade after the text sequence completes

### Technical spec

```
Trigger:    HeartController.OnDestroyed event (Heartwood HP reaches 0)
            OR IDamageableStructure.TakeDamage → HeartController death path

Flow:       1. Freeze all game systems (Time.timeScale = 0 after ~0.5s delay)
            2. Play GameOver.mp3 on dedicated AudioSource
            3. Vignette collapse animation (~1.5s)
            4. Sequential text fade-in with timed pauses (Coroutine, ignoreTimeScale)
            5. Buttons appear
            6. [Begin Again] → full scene reload + hero select
            7. [Return to Elarion] → load autosave (if SaveSystem has one)
            8. [Rest] → main menu

File:       Assets/_Modules/HUD/HeartwoodDestroyedController.cs (new)
            Listens to HeartController.OnDestroyed
            Code-built Canvas: all inline, NO UXML/USS
            Sequential coroutine for text reveal (WaitForSecondsRealtime — timeScale = 0)
```

---

## What NOT to touch

- `Village.unity` — do not hand-edit
- `WaveManager` wave logic
- `HeartController` HP logic or class name — only ADD a listener to its death event

---

## Acceptance criteria

- [ ] Hero death at 0 HP triggers "The chord held" / "Heartwood still sings" screen
- [ ] `[Rise Again]` respawns hero at full HP, screen dismisses cleanly
- [ ] `[Rest Here]` returns to main menu
- [ ] Heartwood HP reaching 0 triggers "The root went silent" screen
- [ ] Three text lines appear in sequence with correct pauses
- [ ] GameOver.mp3 plays on Heartwood destroyed, not on hero death
- [ ] Neither screen uses UXML or UIDocument
- [ ] Brace balance check passed on both new `.cs` files

---

*Narrative copy: final. Do not paraphrase or shorten — the lines were written to land on specific beats. If a line must be cut for technical reasons, flag to UI before removing.*

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
