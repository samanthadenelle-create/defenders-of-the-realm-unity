<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-03
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-03) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-601 — NPC portrait art brief (the missing four)

**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.
**Lane:** Art/content — zero code (the speakers schema landed 2026-07-02; portraits are data)
**Origin:** owner F8 2026-07-02: "no image — if not one, look at others and have creative generate
or create a work order for me with a detailed idea"

## Style guide (derived from the 8 existing portraits in `Assets/Resources/Portraits/`)
Match `farm.jpg` / `lumbermill.jpg` / `barracks.jpg` etc.: **painted-fantasy bust portrait,
head-and-shoulders, 3/4 view, warm single-source lighting from upper-left, dark neutral
background** (deep brown-black vignette), rich but muted palette with one accent color per
character, square aspect (existing files render into a circular gold-ring medallion — keep the
face centered in the middle 60%). No text, no borders (the ring is drawn by the UI).

## The missing portraits (currently rendering the hooded silhouette)

~~1. **Sylas**~~ — **RESOLVED 2026-07-03 (owner spot):** a Sylas portrait already existed at
`Assets/_Modules/Onboarding/Resources/HeroPortraits/Sylas.jpg` (module-local Resources — the audit
only checked root Resources). Speaker record now points at `HeroPortraits/Sylas`, both mirrors.
No generation needed.

2. **Brom — Town Crier** (accent: brass/gold)
   Stout middle-aged crier, big lungs, ruddy cheeks, magnificent mustache, open-collared tunic
   with a brass horn slung on a strap. Expression: mid-breath, about to bellow good news.

3. **Sable (Sable Vey) — Jeweler's Bench** (accent: amethyst violet)
   Precise, elegant jeweler, 40s, dark hair pinned severely, a loupe pushed up on her brow,
   high-collared charcoal vest with fine silver pinstitching, one gloved hand holding a faintly
   glowing cut stone. Expression: appraising you like a gem.

4. **Companion (generic echo companion card)** (accent: tree-aura teal)
   A soft-featured echo-spirit villager — semi-luminous skin, eyes catching the Heart-tree's
   glow, simple homespun clothes. Reads gentle and slightly otherworldly; this card backs any
   unnamed companion line.

**Also wanted while generating (nice-to-have, same style):** an **Apothecary** portrait (herbalist
with dried bundles and a mortar, accent: sage green) — the Apothecary NPC is being added tonight.

## Hookup (after generation)
Drop files as `Assets/Resources/Portraits/{sylas,brom,jeweler,companion,apothecary}.jpg`
(square, ~512px is plenty), then set each speaker's `portrait` field in BOTH
`dialogues.json` mirrors (`Resources` + `StreamingAssets` — keep byte-identical).
`DataRegression.CheckDialogueSpeakers` will verify every path resolves; the silhouette fallback
retires itself per-NPC as each file lands.

## Acceptance
- [ ] 4 (+1) portraits in Portraits/, style-matched to the existing set
- [ ] Speaker records point at them; mirrors identical; REGRESSION portrait check green
- [ ] Felt-check: each card shows the face in the gold ring, no silhouettes left on named NPCs

> **AUDIT 2026-08-21 (agent fleet, read-only):** OPEN — NEEDS OWNER RULING. Evidence: `Portraits/ lacks companion` — art generation call. Status left at READY deliberately: this work is real and unbuilt. Verified against HEAD 2f0b97bb5, not against the ticket's own claims.

> **OWNER RULING 2026-08-21 (verbal, this session):** Portrait ART is NOT being commissioned. Keep the generic portrait for now; in the portrait slot render the NPC's TITLE as text (e.g. "Jeweler") in a font chosen to match the frame. The deliverable is a styled title plate, not art. Do not brief or generate faces.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
