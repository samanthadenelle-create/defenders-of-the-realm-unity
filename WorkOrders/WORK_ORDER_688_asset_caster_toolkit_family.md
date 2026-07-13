# WO-677 — Asset Caster Toolkit Family (owner vision, 2026-07-12)

**Status: SPEC DRAFT — gated on the SME dossier fleet (docs/SME/) completing + per-pack applicability assessment**
**Owner words:** "assess if the logic can be applied to what we have and implemented successfully?
i would love a set of unity toolkits that i can explore the different assets in a custom window
as tools to select from and then have the proper implementation available."

## Vision

A family of owner-facing editor windows — one per asset domain — where the owner browses a
pack's contents, previews them properly (the way the pack author intended), selects, and the
tool performs the CORRECT implementation automatically. The proven pattern:

- **Motion Caster** (SHIPPED) — clips: library → preview on rig → keyword bind → registry row → rebake hint
- **VFX Caster** (SHIPPED) — effects: catalog+raw library → play/loop/orbit → shader audit → Copy Key

Each new tool = the same three-panel formula (library / preview / action), with the pack's SME
dossier defining what "proper implementation" means for that domain.

## Phase 0 (BLOCKING): applicability assessment

When the overnight SME fleet lands (docs/SME/README.md router), run one assessment per dossier:
**can the pack's intended logic be applied to our systems successfully?** Output per pack:
APPLICABLE-NOW (list the wiring) / APPLICABLE-WITH-WORK (name the gap + effort) / NOT-APPLICABLE
(why). This assessment picks which tools below get built and in what order.

## Candidate toolkit windows (order by owner value, refine after Phase 0)

1. **Icon Caster** — the Blink 500 RPG Spell Icons + 25 class emblems + action-bar slots:
   browse/search sheets, preview at bar size, assign to ability id → concept-icons.json row
   (the existing ConceptIconResolver seam). Kills the icon_combat placeholder problem forever.
2. **Gear Caster** — the Blink 400 Low Poly RPG Weapons + KayKit Fantasy Weapons Bits: browse,
   preview seated in the hero's hand (the EquipmentController preview pattern already in Motion
   Caster), add to weapons.json with grip data → Offset Forge handoff for calibration.
3. **Audio Caster** — every SFX/music clip: audition (AudioUtil preview), map to SfxId /
   MusicTrack, write the SfxClipLibrary row; silent-gap table from AUDIO_SME.md as a checklist.
4. **Character Caster** — KayKit Adventurers / Blink humans+orcs / Supercyan / People models:
   browse, rig verdict (the ImportOrcFamily audit logic), preview animated on our shared-rig
   clips, promote to NPC/enemy roster (Addressables per family, WO-545 pattern).
5. **Texture/Environment Caster** — the Blink 70+ texture bundles + polyperfect landmarks:
   browse, preview on a material ball / terrain patch, apply to environment material slots.

## Non-negotiables

- Every tool reads its pack's SME dossier conclusions (proper implementation = what the dossier
  says the author intended, not what's expedient).
- Editor-only, DeNelle.Editor assembly, NO DeNelle.Village references (reflection/SerializedObject
  seams — the boundary that bit MotionCasterWindow on 2026-07-11, see commit 108d2cf9).
- Data-registry outputs (JSON rows, manual:true = owner canon) — never scene edits, never
  hand-wired prefabs (owner thinks in data structures).
- Text-flagged states, never color-only (owner is red/green colorblind).
- Each tool ships with its EditorPrefs reads in OnEnable (not field initializers).

## Acceptance (per tool)

Owner can open the window, find any asset in under 30 seconds via search/filters, preview it
correctly (animated/seated/audible as appropriate), click one action, and the asset is properly
live in-game data with provenance — no CLI round-trip.
