# WORK ORDER 671 — Action Bundle Rows (VFX + SFX + timing on the keyword registry)

**Status:** DONE

> **DONE - verified in HEAD 2026-08-14 (phantom sweep).** The work is present at ActionBundleCatalog.cs:50-60 + HeroAbilities.cs:2009,2077.
> Status had read READY because the landing commit did not flip this line in the same commit
> (CLAUDE.md §2), so the DERIVED board (BOARD.html) kept re-serving finished work.
> _Prior status line, preserved: Status: READY TO IMPLEMENT_

**Minted:** 2026-07-11 (owner WO, Grok-drafted + owner-approved, reconciled to `docs/ACTION_KEYWORD_REGISTRY_ARCHITECTURE.md`)
**Depends on:** WO-670 slice 1 (registry foundation, committed `941ef16c`)
**Lane split:** (A) editor/authoring — row model + Motion Caster window · (B) runtime presentation consumer

## Goal
One keyword = one full action bundle. `PlayAction("Slash_Left")` triggers animation + pooled VFX (timed, bone-attached) + SFX for hero and enemies alike, authored as JSON rows through the Motion Caster.

## 1. Row schema extension (arch doc §1 fields +)
- `vfxKey` (string, existing in schema) — key into HovlVfxCatalog via `VFXManager.PlayKey` (pooled; never a prefab ref)
- `sfxId` (string, existing in schema) — key into the audio system (`SfxId` / `IAudioService`)
- `vfxDelay` (float, default 0) — seconds after animation start to fire the VFX
- `attachBone` (string, optional) — humanoid bone/attach name ("hand.r", "weapon", "spine")
- `playOneShot` (bool, default false) — one-shot overlay (hit reactions, impacts) that must not disturb the base state
Extend `MotionCastings.CastingRow` + WriteRow validation; dual-copy stays in sync; empty fields = today's behavior.

## 2. Motion Caster window (WO-670 slice 2, folded in)
Authoring UX per WO-670 §Slice-1 items 1–6 PLUS the bundle: pick VFX key (from HovlVfxCatalog keys), pick SFX id, set vfxDelay/attachBone/playOneShot, and **preview the full bundle together** (clip scrub + VFX key named + SFX audition). Save = `manual:true` canon per the write contract (§8).

## 3. Runtime consumer — presentation-side bundle player
`PlayAction(target, keyword)` on a thin presentation binder (Village side, per arch §4 — gameplay objects never play effects):
- Animation: route through the EXISTING animator trigger/state for that keyword (ActorAnimator / current drive paths). **No runtime clip swap — that is Phase 2** (arch §3); the bundle player does not CrossFade raw clips.
- VFX: after `vfxDelay`, `VFXManager.PlayKey(vfxKey, resolvedBone)` — pooled, one owner.
- SFX: `CoreServices.Audio?.Play...(sfxId)` per the existing seam.
- `playOneShot` honored (overlay, base state undisturbed).
- Reads rows at runtime via CanonicalJson from the Resources copy (exists since `941ef16c`+mirror).
- Ability-driven casts keep abilities.json vfx authority (no double-fire — arch §4 one-owner rule).

## 4. Logging (binding)
`[MotionCaster] '<target>.<keyword>' -> '<clip>' (manual)` on save and consume; `FlowTrace.Step("Action", "bundle '<target>.<keyword>': anim=<state> vfx=<key>@<delay>s bone=<bone> sfx=<id>")` on runtime play; misses warn per §1.4 — never silent.

## Deliverables
- [ ] Row schema + CastingRow + validation updated (both JSON copies)
- [ ] Motion Caster window with bundle authoring + preview
- [ ] Presentation bundle player + example usage (hero + enemy)
- [ ] Adopted/rejected ledger stays in arch doc §9a (done 2026-07-11)
- [ ] COMPILE_GATE_OK + EditMode tests extended (schema fields round-trip; playOneShot default)

## Do NOT
- ScriptableObject-per-action assets (owner-rejected; JSON rows are canon)
- `Instantiate` VFX / direct AudioClip refs (pooling law)
- Runtime CrossFade of raw clips (Phase 2 substrate decision stands)
