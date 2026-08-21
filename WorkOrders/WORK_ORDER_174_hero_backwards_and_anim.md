**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 174 — Hero travels backwards + walk animation not playing

**Status: READY TO IMPLEMENT**
**Priority:** HIGH — playtest blocker; hero locomotion looks broken (moves in reverse, static pose).
**Date:** 2026-05-31
**Lane:** Combat/Hero — code (`HeroBodySwapper.cs` / `HeroLocomotion.cs` + animator wiring). No bake required
for the orientation fix; pet/hero anim ties to the WO-163/166 animator-param fix.
**Source:** owner playtest — *"player travels backwards and animations not working."*

---

## Two bugs

### Bug 1 — Hero travels BACKWARDS (orientation mismatch)
**Root cause (found in code):** `HeroBodySwapper.cs:61-66` — *"Tripo FBXs export with their forward along
−Z (model faces the [opposite] way)… Wizard … −90° yaw to face +Z forward (owner field-test 2026-05-30).
Other classes keep…"*. The hero body meshes are authored **−Z-forward**, and the per-class yaw correction
is **inconsistent / incomplete** — so the active hero (Wizard) renders facing the opposite direction from
where `HeroLocomotion`'s facing-Slerp points it. Result: he **walks backward** relative to his facing.
- **Fix:** apply a **consistent forward-correction** so every hero body's visual forward = the locomotion
  forward (+Z). Either normalize all hero FBX import settings to +Z-forward, OR apply the correct per-class
  yaw so the *visual* faces the same way `HeroLocomotion` rotates the root. Verify: pressing "forward"
  moves the hero in the direction he visually faces, walk reads head-first, not moonwalking.
- Note the seam (`HeroBodySwapper.cs:115`): the body's local rotation must compose correctly with
  `HeroLocomotion`'s Slerp on the **parent** — the correction goes on the body child, the facing Slerp on
  the root; don't double-rotate or cancel them.

### Bug 2 — Walk animation not playing (static pose)
**Same family as the pet T-pose (WO-166) + AmbientNPC param spam (WO-163):** the hero's animator controller
isn't being driven — either the `Resources/Heroes/<slug>.controller` didn't load (`HeroBodySwapper.cs:98`),
or `HeroLocomotion` isn't pushing the **Speed** param the controller's Idle/Walk machine keys off, or the
controller lacks the param.
- **Fix:** ensure (a) the per-class controller loads (the `Resources/Heroes/<slug>` path resolves; log if
  null), (b) `HeroLocomotion` drives the `Speed` float each frame from move magnitude, and (c) guard the
  `SetFloat` with `HasParameter` so a missing param doesn't silently no-op (and doesn't spam). Verify: hero
  plays Idle when still, Walk when moving.
- **Reconcile with WO-163/166** — the hero, pet, and ambient NPC are all the same animator-param-contract
  bug. Fix them as **one animation pass** (route through the shared animator factory / consistent Speed
  param), don't implement three times.

## Acceptance criteria
1. Hero moves in the direction he **visually faces** — forward input = head-first travel, no backward/moonwalk; facing-Slerp and body orientation agree for **every** hero class (not just one).
2. Hero plays **Idle / Walk** animations driven by movement (Speed param); no static T-pose/frozen pose.
3. Per-class controller loads (logged if missing); `SetFloat` guarded with `HasParameter` (no silent no-op, no spam).
4. Reconciled with WO-163 (AmbientNPC) + WO-166 (pet) as one animation-param fix — no triple implementation.
5. Brace balance; Village→Core only; no UXML; (no bake needed unless a scene field changes — coordinate via Agent 1 if so).

## Done checklist (CLAUDE.md §10)
- [ ] Consistent hero forward-correction; moves head-first per facing, all classes; no backward travel
- [ ] Idle/Walk plays off Speed; controller loads; HasParameter-guarded
- [ ] Reconciled with WO-163/166 (one animator-param pass)
- [ ] Brace balance; no triple-implementation
- [ ] `WORK_ORDER_174_hero_backwards_and_anim.RESULT.md` when complete

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
