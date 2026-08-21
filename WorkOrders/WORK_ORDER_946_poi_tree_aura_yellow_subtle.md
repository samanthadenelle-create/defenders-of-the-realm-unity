# WORK ORDER 946 - POI node auras + Tree of Life VFX: retire the strong yellow, go subtle

**Status:** DONE — owner-confirmed 2026-08-21.
**Minted:** 2026-08-10 (CLI seat, main line - banner bumped 945 -> 947 in the same edit, together with WO-945)
**Silo:** VFX policy (code gate) - Village/Vfx lane, no gameplay logic
**Type:** owner LOOK RULING (creative direction is hers; implementation maps it verbatim)
**Origin:** owner F8 seq 2252, 2026-08-10 10:17, scene Main_Castle_Overworld, verbatim:
*"remove the yelllow from the nodes and the tree of Life (its a vfx) but we want something subtle,
not so strong"*
**Spec updated:** 2026-08-16 (doc-SME agent) - verified RCA + fix shape below, superseding the old
"locate-level anchors" section. Re-confirmed live by the owner's 2026-08-16 morning Seeker felt-test
(unwanted gold glow on the Heart tree + every town resource node; device proof scratchpad `s1.png`,
gold glow at bottom-right corner + right edge of frame).

**AWAITS OWNER RULING BEFORE IMPLEMENTATION.** The mechanism below is verified, but the outcome
choice is hers: full withhold (glow gone) vs the existing one-flip
`ShrinkInsteadOfWithhold = true` alternative (glow plays at 0.2 scale, "subtle, not so strong").
Do not implement until she picks.

---

## 1. The ruling

The yellow aura on the resource/POI nodes AND on the Tree of Life (Heart) is too strong. Replace with
something SUBTLE - lower intensity/saturation presence, not a louder different color. "Remove the
yellow" + "subtle, not so strong" are the constraints; the specific replacement look is dialed until
the owner felt-passes it.

## 2. VERIFIED RCA (every cite re-read at source 2026-08-16)

The withhold machinery built for the earlier yellow-plume asks (WO-890 / WO-1002 / F8 seq 2306)
guards ONE key, and the glow the owner sees is spawned under DIFFERENT keys, so the gate never fires.

1. `Assets/_Modules/Village/Vfx/AmbientAuraPolicy.cs:51` -
   `public const string WithheldAmbientAuraKey = "TreeofLifeAura_Aura";` - a SINGLE key.
   `IsRejectedAmbientKey` (`:64-65`) is an exact `StringComparison.Ordinal` equality against that one
   string; `ShouldWithhold` (`:70-71`) is `IsRejectedAmbientKey(key) && !ShrinkInsteadOfWithhold`.

2. `Assets/_Modules/Village/Vfx/PoiCalloutSystem.cs:63` -
   `public const string NodeAuraKey = "Poi_NodeAura";` - a DIFFERENT string. Therefore
   `AmbientAuraPolicy.ShouldWithhold(NodeAuraKey)` at `:226` is ALWAYS false, the withhold branch at
   `:226-233` is dead code by value (its own comment at `:222-225` says so: "this gate is dormant BY
   VALUE"), and the `VFXManager.PlayKey(NodeAuraKey, ...)` spawn at `:239` always runs for every
   in-range un-spent node (up to `MaxNodeAuras = 6` at a time).

3. `Assets/_Modules/Village/Vfx/PoiCalloutSystem.cs:64` -
   `private const string LandmarkKey = "Poi_Landmark";` - the far-field pillar. Its spawn in
   `EnsureLandmark` (`PlayKey` at `:251-253`) consults the policy NOWHERE - no gate at all, not even
   a dormant one.

   NOTE (path correction): PoiCalloutSystem lives in `Assets/_Modules/Village/Vfx/`, NOT
   `Assets/_Modules/Village/World/` as an earlier triage note said. Same file, wrong folder cite.

4. Consistent runtime evidence: F8 seq 2398 (2026-08-15 21:48, row 83 of
   `logs/f8-inbox/QUEUE.jsonl`; harvested `[Flow:Heart]` lines quoted verbatim in WO-1025 section 2 -
   the per-capture .md has since been pruned from the inbox) shows
   `whiteSwirlSuppressed=True treeAuraSuppressed=True treeHandle=none` WHILE the glow persisted -
   i.e. HeartAuraController's own two loops are correctly withheld and something else draws the gold.
   The 2026-08-15 source-verified corrections in WO-1025 section 2b establish the centrepiece tree is
   built straight from the FBX (which cannot carry Unity ParticleSystems) with zero scene-attached
   emitters - so the glow is runtime-spawned. PoiCalloutSystem is exactly such a runtime spawner, its
   keys are exactly the uncovered ones, and its "Poi_NodeAura" prefab is the gold "Magic circle sun
   loop" (per the retag history comment at PoiCalloutSystem.cs:50-55).

Conclusion: the withhold policy is correct machinery with the wrong coverage. The fix is coverage,
not new machinery.

## 3. Fix shape - promote AmbientAuraPolicy from ONE key to a SET

Files: `Assets/_Modules/Village/Vfx/AmbientAuraPolicy.cs`,
`Assets/_Modules/Village/Vfx/PoiCalloutSystem.cs`,
`Assets/Editor/Regression/HubTreeAuraWithholdRegression.cs`.

1. **AmbientAuraPolicy: key -> key set.** Replace the single-key membership with a set of the three
   rejected/gold ambient keys:
   - `"TreeofLifeAura_Aura"` (the original rejected FireFlies loop - keep)
   - `"Poi_NodeAura"` (harvest-node ground aura, the gold sun-loop circle)
   - `"Poi_Landmark"` (far-field landmark pillar)
   `IsRejectedAmbientKey` becomes set membership (still ordinal, still exact). ALL other semantics
   stay byte-for-byte: `ShouldWithhold` = member AND NOT `ShrinkInsteadOfWithhold`;
   `ShrinkInsteadOfWithhold` stays the ONE static readonly flip and now governs the WHOLE set;
   `ScaleFor` returns `ShrunkAmbientAuraScale` (0.2) for any member only under the flip, 1 for
   everything else. No second flag, no per-key flips. Update the header comment + `WithholdReason`
   (`:81-86`) so the reason string names the actual withheld key, not the hardcoded single constant.

2. **PoiCalloutSystem: no new logic on the node path, one gate added on the landmark path.**
   - `EnsureNodeAura` already consults the policy (`:226-233`, `:238`); once "Poi_NodeAura" is in the
     set that dormant gate goes LIVE and the node spawn at `:239` stops (or shrinks under the flip)
     with zero edits to the flow. DO update the `FlowTrace.Once` copy at `:228-231` - its current
     text ("NodeAuraKey is currently pointed at the rejected loop -- retag it in the VFX Caster")
     describes the old single-key world and would misdirect the next reader.
   - `EnsureLandmark` (`:246-255`) gets the SAME gate pattern as `EnsureNodeAura`: consult
     `ShouldWithhold(LandmarkKey)` with a `FlowTrace.Once` + `WithholdReason`, and pass
     `ScaleFor(LandmarkKey)` on the spawn. Traced, never a silent return (CLAUDE.md section 12).

3. **The WO-1002 oracle moves to the set IN THE SAME CHANGE**
   (`Assets/Editor/Regression/HubTreeAuraWithholdRegression.cs` - markers HUB_TREE_AURA_OK/FAIL).
   Two of its checks assert the OLD single-key world and would go RED (or worse, silently invert
   intent) against this fix:
   - Case1 `:109-112` asserts `WithheldAmbientAuraKey == "TreeofLifeAura_Aura"` -> becomes: the set
     CONTAINS all three keys ("TreeofLifeAura_Aura", "Poi_NodeAura", "Poi_Landmark").
   - Case3 `:174-177` asserts `IsRejectedAmbientKey(PoiCalloutSystem.NodeAuraKey)` is FALSE - under
     the set that membership is now TRUE BY DESIGN. The check INVERTS: assert NodeAuraKey IS a set
     member and `ShouldWithhold(NodeAuraKey)` is true while the flip is false, so a future removal of
     the key from the set is the thing that goes red.
   - Keep the surgical negatives (`:123-127`, `:137-139`): "Cathedral_Aura", "Aura_HeartPulse" and
     null must stay NON-members - the withhold must never widen past the named set.
   - Case3 `:181-185` (source lint that EnsureNodeAura consults the policy) stays; add the mirror
     lint for the new EnsureLandmark gate.
   - Also verify `Assets/Editor/Regression/VfxAuraDifferentiationRegression.cs` (`:55` extracts
     `NodeAuraKey` by regex) still passes - the key STRINGS do not change, only policy membership,
     so it is expected unaffected; confirm, do not assume.

4. **Blast-radius note:** HeartAuraController's hub predicate (`ShouldWithholdTreeAura`) and the
   combat/raid-Heart keep (WO-1002 section 1 non-goal) are untouched - "TreeofLifeAura_Aura" simply
   remains a member. Adding the two Poi keys removes the node ground rings and the landmark pillar
   town-wide while withheld; the colorblind-wayfinding role those callouts served (PoiCalloutSystem
   header, `:6-15`) is the cost of the removal arm and one more reason the shrink arm may be the
   owner's pick. Surface that trade-off to her with the ruling ask.

## 4. What is FORBIDDEN

- **Do NOT retag `Assets/Editor/VfxManualPicks.json`.** The owner owns key -> prefab tags (memory
  `vfx-map-owner-tags-no-creative-pick`; AmbientAuraPolicy.cs:22-24). This fix decides whether a hook
  PLAYS a key; it never edits, swaps or re-points a tag.
- Do not add a second flip, a per-key flag, or any re-implementation of the shrink path - the ONE
  `ShrinkInsteadOfWithhold` value governs the whole set.
- Do not strip or quieten the FlowTrace withhold lines (CLAUDE.md section 12 - instrumentation is
  permanent).
- Do not touch VFX pool/reclaim logic, the VFXType enum order, or the catalog generator (08-06
  rulings; the ONESHOT saturation is a separate open item).
- Do not hand-edit any .unity scene.

## 5. Acceptance

1. Owner ruling recorded first (withhold vs shrink-to-0.2). Implementation matches her pick verbatim.
2. Gold glow reads per the ruling (gone, or small/subtle) on the nodes, the landmark pillar and at
   the Heart tree in a DEVICE screencap - this class needs EYES, not markers (canon 08-09; memory
   `screenshots-are-primary-evidence-for-visual-defects`). Compare against scratchpad `s1.png`.
3. Post-fix capture still shows `whiteSwirlSuppressed=True treeAuraSuppressed=True` (WO-1025
   acceptance overlap) plus the NEW traced withhold lines for the Poi keys.
4. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` + `HUB_TREE_AURA_OK` with the set-shaped
   oracle; `VFX_ART_MIRROR_OK` unchanged (no art is touched, only code).
5. Owner felt-verify + CLOSE (a look ruling only she can pass).

## 6. Cross-references

- **WO-1025** (Heart tree presentation) owns the OTHER half of the same screenshot: the white
  starburst emitter hunt + the flat basecolor-only tree material (DEF-267). Its section 3 emitter
  instrumentation still stands for the starburst; the GOLD glow half is answered HERE.
- WO-890 / WO-1002 / F8 seq 2306 - the prior yellow-plume asks this policy was built for.

> **AUDIT 2026-08-21 (agent fleet, read-only):** OPEN — NEEDS OWNER RULING. Evidence: `AmbientAuraPolicy.cs:51` — withhold vs shrink is a look pick. Status left at READY deliberately: this work is real and unbuilt. Verified against HEAD 2f0b97bb5, not against the ticket's own claims.

> **OWNER RULING 2026-08-21 (verbal, this session):** POI node auras / Tree of Life VFX: owner states this is done. Closed on her word, not on a code read - if a strong yellow aura reappears in a build, reopen and cite the screenshot.
