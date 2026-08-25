# WORK ORDER 561 — 30s Skippable Intro + Lore/Storyline Polish

**Status:** DONE (intro implemented + skippable; lore bible written; image-prompt sidecar authored)
**Date:** 2026-06-28
**Branch:** wip/village2-and-f8-tickets (agent worktree)
**Related:** WO-557 (full Yarn removal — the intro rewrite is part of that rip).

---

## A. The new intro (owner: "rewrite the intro, images in the intro, ~30s, designed well but fast to skip")

### RCA of the old path (file:line)
- The opener was the **9-screen Yarn cinematic**: `IntroSequencePlayer.Play()` hosted
  `Resources/Dialogue/DialogueSystem` and ran `StartDialogue("Intro_Screen1")` from
  `Assets/Dialogue/Intro/IntroSequence.yarn`. Text-only (no images actually loaded), tap-to-advance,
  no Skip button. Transition out via `IntroCommandBridge.CmdTransitionTo` → `SceneRouter.GoHeroSelect()`.
- Triggered from the Title splash: **`TitleController.OnPlayIntro()` → `IntroLauncher.Play.Invoke()`**
  (`Assets/_Modules/Onboarding/TitleController.cs:322`/`:336`). Boot order = Title → HeroSelect → PetSelect.

### What was built
`Assets/_Modules/DialogueUI/IntroSequencePlayer.cs` — **fully rewritten, Yarn-free, code-built uGUI**:
- A static registrar (unchanged seam: sets `IntroLauncher.Play` so `TitleController` needs no edit) that
  spawns an `IntroSequenceDriver` MonoBehaviour (DontDestroyOnLoad).
- **5 image slates** (`IntroSlate[]`), ~5.5s each + dip-to-black transitions ≈ **~30s**. The hook: the Heart
  of Elarion (world-tree) blazing → the Dimming (grief siphons the light) → the Hollow Ones (broken, not evil)
  → Grom's call (carry an ember into the dark) → the reclaim + title card.
- **Skippable three ways:** (1) full-screen invisible tap target advances one beat (click straight through);
  (2) a visible gold **"Skip ›" button** top-right ends the intro immediately; (3) any keyboard key ends it
  (new Input System). All routes call `SceneRouter.GoHeroSelect()`.
- **Chrome = ElarionUiKit black+gold:** caption in a translucent black band with a gold rule; title card uses
  `ElarionUi.Gold`. No UXML (canon).
- **Art-driven, owner-tunable:** each slate names a `Resources/Intro/<name>` sprite path; the owner generates
  the 5 images per `docs/ART/INTRO_IMAGE_SLATES.md`. A missing sprite degrades to caption-on-black (LogWarning).
- Plays `MusicTrack.Title` on start.

`Assets/_Modules/DialogueUI/IntroCommandBridge.cs` (the Yarn fade/transition bridge) **deleted**.

### Acceptance
- [x] ~30s, 5 evocative image-slate beats with captions + holds.
- [x] Skippable (tap / Skip button / any key) → jumps straight to hero select.
- [x] Code-built uGUI on ElarionUiKit, NO UXML, NO Yarn.
- [x] Image slots + per-image generation prompts documented for the owner.

## B. Lore / storyline polish (owner: "have creative go over everything and polish lore and storyline")

`docs/NARRATIVE/STORY_BIBLE_POLISH.md` — the canon-consistency pass. Polished world premise; the
Heart/echo/reclaim loop (lore + life-force math in one breath); Grom's arc; the Hollow Ones as **grief, not
evil** (+ orc legion as the martial arm); vendor/NPC voice guides; a consistency matrix (intro + cover +
in-game dialogue agree); and a flagged list of lore contradictions in the older docs with resolutions.

### Contradictions flagged (resolutions in the doc)
- `docs/STORYLINE.md` — burned-tree/Cathedral-Spire premise, 3 playable heroes, "Sir Bram", bondable pets,
  Syndrath/Alduin antagonists, arcane-tower wave-defense loop → RETIRE/SUPERSEDE/RECONCILE to single-Knight +
  living world-tree + Hollow Ones.
- `docs/PARTY_OF_FOUR_STORYLINE.md` — entire party premise contradicts single controllable hero → RETIRE.
- `docs/dungeons-storyline.md` — "the Keeper" + pet companions + Alduin/Syndrath → RECONCILE to Grom; KEEP the
  on-tone "every Hollow One was somebody" mourning theme.
- `DESIGN_VENDOR_STORYLINES_AND_QUESTLINES.md` — resource creep (5 resources) → reconcile to Wood/Iron/Grain + Gold.
- Name note: Grom/Brom near-homophone flagged as an owner call.

## Files
**New:** `Assets/_Modules/DialogueUI/IntroSequencePlayer.cs` (rewritten), `docs/ART/INTRO_IMAGE_SLATES.md`,
`docs/NARRATIVE/STORY_BIBLE_POLISH.md`, this WO.
**Deleted:** `Assets/_Modules/DialogueUI/IntroCommandBridge.cs` (+ all Yarn intro content — see WO-557 RESULT).

## Owner-decision flags
- **Image art pending** — intro runs caption-on-black until the 5 slates are generated + dropped at
  `Assets/Resources/Intro/`.
- **Caption copy** is canon (story bible). Holds are tuned to ~30s; adjust `IntroSequenceDriver.Slates` to retime.
- **Grom vs Brom** naming (knight vs innkeeper) — flagged in the bible for an owner call.
