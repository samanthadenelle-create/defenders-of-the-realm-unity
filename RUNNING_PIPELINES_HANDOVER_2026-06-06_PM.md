# Running Pipelines — Handover (2026-06-06 PM)

Branch: **feat/tower-core-loop**. This captures every live workstream as of this session,
each with state + the single next action. Companion to `CLI_HANDOVER_2026-06-06.md`
(which is the last-24h work-order digest). New code touched this session needs a CLI
Windows compile-verify before any build — see §A/§B.

> ⚠️ Mount caveat: the Linux-mount brace check is unreliable (it showed a truncated copy
> of a file the Windows Write tool had written correctly). Trust the Windows-side
> CompileGate, not a mount brace count.

---

## A. Combat HUD — party panel  ·  STATUS: code-complete, needs compile-verify

Reference art: landscape MMO HUD (party top-left, target top-centre, minimap, chat, action bar).
Owner scope this pass: **party list on the left that grows as members join, each row showing
Health + Mana.** Rest of the screenshot (chat/minimap/full action bar) treated as template extras, skipped.

- This session: **rewrote `Assets/_Modules/HUD/HUDManager.cs`.** Now landscape (ref 1920×1080),
  a dynamic vertical party list (VerticalLayoutGroup + ContentSizeFitter → auto-stacks as the
  party grows), HP (red) + Mana (blue) bars per member, plus a top-centre Target frame.
- Fixed the core bug: the old version set `Image.fillAmount` on a plain-colour Image (no sprite,
  no `Type.Filled`) so bars never filled. New bars are anchor-driven (child fill, width = `anchorMax.x`)
  — no sprite needed. Removed unused `System.Reflection` and the `Lean.Touch` dependency.
- Public API (passive display): `SetParty(...)`, `AddMember(...)`, `UpdateMember(i, hp, maxHp, mana, maxMana)`, `SetTarget(...)`. Currently shows demo values (Archer/Mage/Knight).
- **Next:** CLI compile-verify on Windows, then wire `SetParty/UpdateMember` to the real party/combat
  system instead of demo values (ties into §F persistence — GameState holds the roster, HUD renders it).

## B. Gear visuals / "square inside the models"  ·  STATUS: fixed, needs compile-verify

Root cause was NOT animation — it was the placeholder gear from WO-106 (Default Gear + Shop).
`GearVisualApplier` attaches **primitive cubes** (chest/sword/shield/pauldron) to every hero.
When a hero's Humanoid avatar is invalid (the §C rig issue), bone lookups return null and the
cube fell back to the body root → a cube dead-centre in the torso.

- This session: in `Assets/_Modules/Village/Hero/GearVisualApplier.cs` —
  added `public static bool EnablePrimitiveGear = false` (default OFF, so the cubes are gone next
  build; `Apply` still clears stale ones), and changed the weapon/armor bone fallbacks to **return**
  instead of parenting to the body root, so a cube can never sit on the pivot again.
- **Next:** when real low-poly gear meshes exist, flip `EnablePrimitiveGear = true` and swap the
  primitives for meshes. Until then heroes show no gear visuals (stats still apply via GearLoadout).

## C. Hero rig / animations — T-pose, "models won't load right"  ·  STATUS: needs CLI batchmode + playtest

This is the rig, not gameplay code. Animation chain WO-283/284/285 landed and build-verified
(library + ActorAnimator driver + 3D combat clips). WO-286 re-rigged the 4 heroes via AccuRIG and
its RESULT reports valid Humanoid import, but the "stand upright + animate, not T-pose" check was
left **pending a playtest**.

- **Next (CLI, Unity editor CLOSED):** confirm the AccuRIG'd FBX are in `Resources/Heroes/`, then run
  `DeNelle.Editor.HeroFbxImporter.FixHeroFbx` — it must log **`human=True`** per hero (anything else
  = the folder still has the old mesh) — then `HeroAnimatorFactory.BuildAll`, then CompileGate +
  Windows build, then playtest upright/scale. Recipe is in `WORK_ORDER_286_*.RESULT.md`.
  Known-good fallback FBX: `Backups/hero_fbx_20260606_005717/`. The §B fix already removes the
  in-torso square so heroes look less broken mid-debug.

## D. Floating health bar — giant green oval over enemies  ·  STATUS: NEW BUG, diagnosed

The green oval-with-gold-rim above the targeted orc is the enemy's **`FloatingHealthBar`**
(`Assets/_Modules/Village/Combat/FloatingHealthBar.cs`): full-HP green fill (`HealthyColor`),
gold rim (`RimColor`), drawn as a "slim rounded chip" (a pill → reads as an oval). It's hidden at
full HP but **revealed when targeted** (HeroTargetIndicator calls `SetBarTargeted`), which is why
it appeared. It's rendering **massively oversized** — the exact case the code comments call out
("HUGE green bar"): the host-scale cancel `canvasGo.transform.localScale = Vector3.one / hostScale`
(~line 191–193) is mis-computing for this enemy (likely a scaled/non-uniform Humanoid orc), so the
bar blows up instead of sitting small near the head.

- **Next:** review `FloatingHealthBar` host-scale compensation + `_heightOffset` (2.4) / `_barSize`
  for scaled Humanoid enemies (the People-orc family). Should render as a small slim bar, not a pill
  filling the view. (CLI fix + playtest.)
- Note: the small gold/red ring + centre-pip reticle (`HeroTargetIndicator`) is a separate,
  working system — it is NOT the green oval.

## E. Party persistence (wallet-keyed)  ·  STATUS: not started, design ready

Pieces already exist: a `Wallet` module (identifier) and `GameStateService` (single source of truth
that already saves/loads). Plan: store the party as a small **roster** in GameState (each member's
class + level/xp, not live HP), key the save profile by **wallet address** with a **local fallback id**
when no wallet is connected (matches "assume same wallet for now"); save on roster change, load on boot,
default starter party if empty. The §A HUD renders this roster.

- **Next:** write a work order for CLI to implement + build-verify (owner to confirm what's stored per member).

## F. Already-tracked threads (from the 24h digest, unchanged)

- **Store / monetization:** owner reports the store loads + works. (~70% built; do not greenfield.)
- **WO-282 Heroes → Addressables:** HELD for a daytime play-verified session (hero-spawn critical path).
- **WO-107–111 (castle/world/NPC/HUD/audio specs):** marked READY; `QA_CHECKLIST_FILLED` claims them
  wired but that's **code-inspection only — not build-verified**, and no RESULT files exist. Treat as
  unverified; rebuild Village + CompileGate + build before trusting.
- Numbering collisions to clean up: two WO-106, two WO-282; duplicate WO-110 spec. Next free WO ≥ 287.

---

## Files changed this session (CLI: compile-verify on Windows)

- `Assets/_Modules/HUD/HUDManager.cs` — full rewrite (landscape party HUD).
- `Assets/_Modules/Village/Hero/GearVisualApplier.cs` — primitive gear off by default + bone-null guards.
  (Both edits are statement/field-only — no brace-count change from the versions that already compiled.)
