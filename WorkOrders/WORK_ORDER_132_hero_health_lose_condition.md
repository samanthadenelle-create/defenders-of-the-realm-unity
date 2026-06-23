# WORK ORDER 132 — Hero damage + a real village lose condition

**Status:** READY TO IMPLEMENT
**Priority:** P0 — without this (plus WO-125) the village is unloseable
**Date:** 2026-05-30
**Source:** docs/QA_player_sanity_pass_2026-05-30.md (P0-B)
**Lane:** Combat/AI (code) + one scene seam (GameOverUI presence — see below)

---

## Symptom

Enemies that reach the hero in the village do nothing lethal, and there is no
clear "you lost" beat. Combined with WO-125 (Heart-fall does not trigger defeat),
**the village cannot be lost.**

---

## Root cause (verified — and an important correction to the QA note)

The QA doc (P0-B) states the village hero "never adds HeroHealth/HeroHitReaction
(grep = 0)". **That is now out of date.** Re-verification of the live code:

- `HeroHealth.cs` ships a `HeroHealthBootstrap` MonoBehaviour with
  `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` that polls for a `HeroAbilities`
  object and **attaches BOTH `HeroHealth` AND `HeroHitReaction` to it at runtime**
  (`Assets/_Modules/Village/Hero/HeroHealth.cs:264-297`). The village hero gets
  `HeroAbilities` (`Assets/Editor/VillageSceneBuilder.cs:3469`), so the bootstrap
  attaches HeroHealth to it. HeroHealth pulls contact damage from nearby enemies
  each tick and runs a full death flow (`HeroHealth.cs:90-151`).

So the hero **can** take damage and die. The remaining real gap is the **lose-screen
seam**:

- On death, `HeroHealth.HandleDeath` tries to find a `GameOverUI` MonoBehaviour by
  type name across assemblies and `Show()` it reflectively; if none is present it
  **falls back to reloading the active scene** (`HeroHealth.cs:158-205`).
- `GameOverUI` exists (`Assets/_Modules/UI/GameOverUI.cs`) but a grep of
  `VillageSceneBuilder.cs` for `GameOverUI` returns **0** — it is **not placed in
  the Village scene**. So hero death in the village currently just **silently
  reloads the scene** (no defeat screen, no retry/quit choice).
- Additionally there is **no hero-HP bar in the UI-Toolkit HUD** (HUD binds only
  heart-hp/crystal/mana, `Assets/_Modules/HUD/VillageHudController.cs:356-365`).
  HeroHealth draws its own IMGUI bar (`HeroHealth.cs:234-261`) so the player isn't
  blind, but it's a placeholder.

---

## Fix (precise)

**Goal:** confirm the hero-death lose path actually presents a defeat screen, and
make the village's two lose conditions (hero down + Heart fallen) both terminate
the run cleanly.

1. **Place GameOverUI in the Village scene.**
   `VillageSceneBuilder` must instantiate/enable a `GameOverUI` so the reflective
   lookup in `HeroHealth.HandleDeath` (`HeroHealth.cs:172-179`) and any Heart-defeat
   path (WO-125) find it instead of falling back to a silent reload. Because
   `GameOverUI` is in the default Assembly-CSharp (global namespace), add it the same
   way other cross-assembly components are added in the builder (`AddVillageComponent`
   / type-name lookup helpers already used for the hero systems,
   `VillageSceneBuilder.cs:3464-3474`). This is a **VillageSceneBuilder edit →
   requires a scene rebake → CLI's job** (CLAUDE.md §3/§9: UI does not fire batchmode;
   VillageSceneBuilder is the serialization bottleneck — one branch at a time).
   - PIPELINE_STATE §8: UXML in builds does not render — verify `GameOverUI` is
     code-built / IMGUI-backed (it likely is, matching HeroHealth's IMGUI choice).
     If GameOverUI relies on a UIDocument that comes up empty in builds, switch its
     presentation to code-built before relying on it.

2. **Confirm hero death triggers defeat (not just a reload).**
   With GameOverUI present, `HeroHealth.HandleDeath` will `Show()` it (`:173-178`).
   Verify the defeat screen offers Retry/Quit and stops the wave loop. No HeroHealth
   logic change is needed if the bootstrap + GameOverUI presence are confirmed; if
   the bootstrap proves unreliable in the player build, fall back to adding
   `HeroHealth` explicitly in `VillageSceneBuilder.BuildHero` right after
   `TypeHeroAbilityInput` (`VillageSceneBuilder.cs:3474`) via `AddVillageComponent`.

3. **Coordinate the dual lose model with WO-125.**
   Decide and document the intended village lose model:
   **Heart-fall OR hero-down → defeat.** WO-125 owns the **Heart-fall** defeat fix
   (do NOT re-implement the Heart logic here). This WO owns the **hero-down** path
   and the **shared GameOverUI presence** both paths depend on. The two together
   close the lose condition.

(Optional, can fold to a small follow-up): add a hero-HP bar + pet-status widget to
the code-built HUD (`VillageHudController.cs` HUD-build region) so the IMGUI bar can
retire — QA P1-G. Not required to close the P0 lose condition.)

---

## Acceptance criteria

- [ ] When the village hero's HP reaches 0, a **GameOver/defeat screen appears**
      (not a silent scene reload) with a Retry and/or Quit choice, and the wave loop
      stops.
- [ ] `GameOverUI` is present in the Village scene (found by HeroHealth's reflective
      lookup and by WO-125's Heart-defeat path) — verified in the built player build,
      not only the editor.
- [ ] Hero contact damage from enemies is observable (HP bar drains as enemies close).
- [ ] The intended lose model (Heart-fall OR hero-down) is documented inline and both
      paths terminate the run via the same GameOverUI.
- [ ] No new `System.Reflection` introduced in bridge scripts beyond the existing
      pattern (CLAUDE.md §10); `?.` on cross-module calls; brace balance check passes.

## Files to edit

- `Assets/Editor/VillageSceneBuilder.cs` — place GameOverUI in the scene (and, if the
  runtime bootstrap is unreliable, add HeroHealth explicitly in `BuildHero`).
  **CLI only — requires a scene rebake** (`Defenders > Week 3 > Build Village Scene`).
- (Verify-only) `Assets/_Modules/Village/Hero/HeroHealth.cs` — confirm bootstrap +
  HandleDeath; edit only if explicit attach is needed.
- (Verify-only) `Assets/_Modules/UI/GameOverUI.cs` — confirm code-built/IMGUI, not
  UXML-dependent.

## Do NOT touch

- **The Heart-fall defeat logic** — that is WO-125. Reference it; do not duplicate.
- Any `.unity` scene file by hand. The GameOverUI placement goes through
  VillageSceneBuilder + a CLI bake — never hand-edit `Village.unity` (CLAUDE.md §3).
- HeroHealth's contact-damage tuning constants (gameplay-feel; out of scope here).

## Cross-dependencies

- **WO-125 (Heart/dragon + Heart-fall = no defeat)** — PAIRS with this WO. WO-125 =
  Heart-fall defeat; WO-132 = hero-down defeat + the shared GameOverUI both need.
  Sequence: land the GameOverUI placement once (this WO) and have WO-125 reuse it.
- **VillageSceneBuilder serialization bottleneck (CLAUDE.md §9)** — coordinate the
  scene edit with any other in-flight builder change (esp. WO-125 if it also edits
  the scene) so only one branch touches `VillageSceneBuilder.cs` at a time.
