# WORK ORDER 1025 — Heart of Elarion reads amateurish: unidentified VFX + an unlit single-texture tree

**Status:** STEPS 1 + 2-FIREFLIES-SLICE LANDED IN WORKING TREE 2026-08-16 (pending committer gate)
— step 1: the §3 audit instrumentation in `HeartAuraController.cs` (WO-1025 AUDIT [EARLY/SETTLED]
passes). Step 2 fireflies slice (orchestrator GO on the 08-16 owner rulings, §4d): the existing
FireFlies loop re-enabled at the hub tree via a site-scoped `AmbientAuraPolicy` exemption;
`HubTreeAuraWithholdRegression` updated to the new canon. All files brace-balanced, NUL-clean,
uncommitted. STILL OPEN: the yellow-cone/starburst emitter hunt (gated on the captured AUDIT
trace) + step 3 (tree material maps).
**Minted:** 2026-08-15 (UI seat) — provenance stack bumped 1025 → 1026 in the same edit
**Lane:** World art / VFX presentation. Disjoint from WO-1021 (talent UI), WO-1022 (scene GUIDs),
WO-1024 (repair lifecycle).
**Provenance:** owner 2026-08-15, verbatim: **"these graphics on tree look amatuerish"**, with an
in-game screenshot of the Heart of Elarion at the hub centre.
**OWNER RULING (2026-08-16, verbatim):** **"For the tree of life use the butterflies or fireflies."**
— settles the tree's ambient direction: butterflies or fireflies, NOT auras/glows (consistent with
the WO-1002 removal of the yellow aura). See §4c research note.
**OWNER FOLLOW-UP (2026-08-16, verbatim):** **"that was already there"** — the butterfly/firefly
effect already exists in the project (it is the catalogued `TreeofLifeAura_Aura` → `FireFlies`
loop; see §4c). The implementation slice of step 2 is therefore REUSE-VERBATIM of the existing
effect — no new particle authoring, no creative substitution.
**Surface:** `Main_Castle_Overworld`, the world tree at scene centre — the game's single most-looked-at
object. It is the emotional centrepiece of the whole pillar (canon §7: the Heart is the world tree the
Echoes are drawn from), so its finish sets the perceived quality of everything around it.

---

## 1. What the owner is looking at

The screenshot shows, around the trunk base:

- a **hard-edged yellow cone / funnel** climbing the lower trunk
- a **white multi-point starburst** flat on the ground, with visible polygon spikes and no soft falloff
- the tree itself reading **flat and untextured** against a saturated green ground plane

Read as a whole it looks like placeholder particle art at the wrong scale — which, per §2, appears to
be exactly what it is.

## 2. ⚠ THE OBVIOUS DIAGNOSIS IS WRONG — read this before touching any VFX key

The auto-harvested context on F8 **seq=2398** (`capture-20260815-214846-seq2398.md`) carries this line:

```
[Flow:Heart] HeartAura 'HeartOfElarion': crown-tether LIVE -> anchorPos=(0.00, 0.00, 12.00),
  crown=(0.00, 6.29, 0.00), canopy=(0.00, 5.24, 0.00),
  whiteSwirlSuppressed=True, treeAuraSuppressed=True, treeHandle=none.
```

**Both authored loops are WITHHELD at this tree:**

| loop | state | withheld by |
|---|---|---|
| `Aura_HeartPulse` (white pulse nucleus) | `whiteSwirlSuppressed=True` | `HeartAuraController.cs:190` — `_hasTreeBody` |
| `TreeofLifeAura_Aura` (owner-tagged GREEN FireFlies) | `treeAuraSuppressed=True`, `treeHandle=none` | `:198` `ShouldWithholdTreeAura` (WO-1002) |

**Therefore the yellow cone and the white starburst in the screenshot are NOT the owner-tagged VFX.**
Those are suppressed and not spawning. Something *else* is drawing them.

⚠ **Do NOT "fix" this by re-tagging `Aura_HeartPulse` or `TreeofLifeAura_Aura`, and do NOT flip the two
suppression flags.** Those suppressions are deliberate — WO-1002 and the owner's repeated instruction
that the stray "heal VFX" be gone (`HeartAuraController.cs:113`). Un-suppressing them would restore art
she asked to have removed, on top of whatever is actually drawing here.

**A known non-cause, for completeness:** `HeartAuraController` also adds a `Light` (`:211`). A Light
cannot render a hard-edged cone mesh or a polygonal starburst sprite, so it is not the source either.

## 2b. ⚠ SOURCE-VERIFIED CORRECTIONS (CLI + read-only verifier, 2026-08-15 late) — read before §3/§4

1. **The "particles baked into the tree prefab" hypothesis (§3 bullet / §4a) is REFUTED, two ways:**
   `Assets/Prefabs/Environment/TreeOfLife.prefab` is a 2,451-byte thin PrefabInstance over
   `Assets/Art/Tree_Of_Life.fbx` with ZERO ParticleSystem/VisualEffect/Light components (an FBX cannot
   carry Unity ParticleSystems) — and **the scene centrepiece is not that prefab anyway**:
   `CastleHubBuilder.cs:2438-2497` builds `TreeOfLife_Visual` under `HeartOfElarion` directly from the
   FBX (scale 7, Euler(-90,0,0), colliders stripped), and `:1731-1758` destroys orphan trees + dedups
   extra `TreeOfLife_Visual` instances. **So the cone/starburst is scene-attached or runtime-spawned.**
   Point the §3 instrumentation at the CHILDREN of `HeartOfElarion/TreeOfLife_Visual` specifically —
   and note a stale duplicate that escaped the dedup pass is itself a live suspect.
2. **The flat-texture finding (§5) is real but ALREADY HALF-OWNED — do not re-solve the solved half.**
   `Village2Generator.cs:114-120` records it as **DEF-267** ("the Tree_Of_Life FBX ships with ZERO
   usable materials -> renders flat") and `DeNelle.Core.TreeOfLifeMaterialFixer` (attached at
   `CastleHubBuilder.cs:2472`) already applies `Resources/Structures/Materials/TreeofLife_basecolor`
   (URP) on Start. **The remaining ask is narrower: that material is BASECOLOR-ONLY** — no
   normal/roughness/AO — so the surface still reads flat under URP lighting. Scope §5 to authoring the
   missing maps (or a normal-from-height bake) onto the EXISTING fixer material, citing DEF-267.

**CROSS-REF (2026-08-16):** the GOLD glow half is identified and owned by **WO-946** (PoiCalloutSystem
"Poi_NodeAura"/"Poi_Landmark" spawns uncovered by AmbientAuraPolicy's single-key gate); this WO keeps
the white starburst hunt + the tree material.

## 3. STEP 1 — INSTRUMENT. Do not edit until the trace names the emitter (CLAUDE.md §12)

The emitter is **unidentified**. Static reading located candidates and did not conclude, which is
exactly where §12 forbids an edit.

**Capture, in this order:**

1. At the hub tree, dump the **full child hierarchy** of the Heart/tree GameObject — every
   `ParticleSystem`, `MeshRenderer`, `Projector` and `Light`, with the prefab each came from.
   The two most likely sources, neither confirmed:
   - particle children **baked into the tree prefab itself** (they would render regardless of the
     controller's suppression flags, which would explain the contradiction exactly)
   - another system spawning at the Heart's transform — `HeartRegen` is live in the same capture
     (`[Flow:Heart] HeartRegen mode -> Full at HP 100.0/100`)
2. For each emitter found, log its **VFX catalog key** (or "not catalogued") plus world scale.
3. Only then decide.

**Record the emitter's identity in the RESULT.** The next person must not have to re-derive it.

## 4. STEP 2 — the fix, once the emitter is known

Two shapes, depending on what the trace says:

**(a) The emitters are uncatalogued placeholder art baked into the prefab** — most likely given §2.
Remove them from the prefab and let the catalogued, owner-tagged path own the tree's look. Whether the
tree then gets a replacement ambient loop is an **owner-tag decision**, not CLI's: per memory
`vfx-map-owner-tags-no-creative-pick`, the owner tags the VFX key and CLI wires it **verbatim** — no
creative substitution on the implementing side. If no key is tagged, ship the tree clean rather than
substituting a pick.

**(b) They are catalogued and simply mis-scaled / wrong blend.** Correct scale and soft-edge falloff
against the trunk's real bounds — the controller already computes `crown=(0.00, 6.29, 0.00)` and
`canopy=(0.00, 5.24, 0.00)`, so authored bounds exist to seat against rather than eyeballing.

⚠ **Sequenced/marquee VFX are sanctioned for special events** (memory
`sequenced-vfx-special-cases-for-special-events`) — the Heart plausibly qualifies as a marquee object.
But that licenses a richer **presentation**, never a second spawner or pool. Reuse `VFXManager`.

## 4b. STEP 1 LANDED (2026-08-16, working tree, pending committer gate)

`HeartAuraController.cs` now runs a read-only **`WO-1025 AUDIT`** pass at every hub-centerpiece
Heart (`_hasTreeBody` gate — combat/raid Hearts untouched), TWICE: `[EARLY]` at the end of
`BuildAura` and `[SETTLED]` at +4 s (after the ~2.5 s late ground-snap and any late spawner), so a
spawn/destroy between the two shows as a diff. Each pass emits `[Flow:Heart]` lines (measured
outcomes per INSTRUMENTATION_STANDARD §1.4b — resolved world pos/lossyScale, live
material/shader/mainTexture, isPlaying/particleCount, never authored intent):

- **CHILD** lines — every ParticleSystem / Renderer / Projector / Light under the Heart root
  (incl. inactive). The tree renderers' lines prove at runtime whether the DEF-267
  `TreeOfLifeMaterialFixer` material applied and that it is basecolor-only.
- **NEAR** lines — every non-child ParticleSystem / Projector within 25 m XZ of the anchor
  (suspect #2: another system spawning at the Heart's transform).
- **TREE** lines + a `Warn` when the scene-wide `TreeOfLife_Visual` count != 1 (stale-duplicate
  suspect, §2b).
- Each emitter line carries `key=` — the VFX catalog identity resolved from the live hierarchy
  (`[VFX_<type>]` / `[ProceduralLoop_<type>]` pool names), `NOT-POOLED clone '<name>'` for an
  Instantiate outside VFXManager (itself a finding: a second spawner), or `not catalogued`
  (scene-attached / baked art).

No presentation value changed; suppression flags untouched. The next graphics-enabled run at the
hub will NAME the yellow-cone/starburst emitter in the capture — that is the step 2 gate.

## 4c. RESEARCH NOTE — butterflies / fireflies on disk + live usage (2026-08-16, per owner ruling)

**Live, catalogued, already-praised — the "that was already there" effect:**
- Catalog key **`TreeofLifeAura_Aura` → prefab `FireFlies`** (isLoop; owner-tagged in
  `Assets/Editor/VfxManualPicks.json`; played via `VFXManager.PlayKey`).
- Wired to the Heart tree TODAY at `HeartAuraController.StartGreenTreeAura` (crown-seated,
  crown-tracked) — but **currently WITHHELD at the hub** by
  `AmbientAuraPolicy.WithheldAmbientAuraKey = "TreeofLifeAura_Aura"` (`AmbientAuraPolicy.cs:51`,
  WO-1002 / F8 seq 2306, the "rejected yellow plume").
- Owner verbatim on record, `PoiCalloutSystem.cs:57-58`: *"the fireflies are great at the tree"*.
  Nodes used the same key at scale 0.60 until the 2026-08-06 retag to `Poi_NodeAura`; the retag
  comment (`PoiCalloutSystem.cs:44-49`) also records the known constraint: the sparse
  low-luminance fireflies are **imperceptible in bright midday hub lighting** at small scale.
- **⚠ TENSION to resolve in step 2, owner-routed:** the 08-16 ruling asks for fireflies at the
  tree; WO-1002 withholds exactly that key there. §2 already shows the amateurish yellow cone is
  NOT the fireflies (they are suppressed and not spawning) — so the WO-1002 withhold may have
  removed the wrong thing while the real offender kept drawing. Reconciling (e.g. re-enabling the
  key at the hub once the true emitter is removed) is an OWNER decision; per §6 the flags are not
  flipped unilaterally.

**Disk assets (paths, git status):**
- `Assets/UnityTechnologies/ParticlePack/EffectExamples/Misc Effects/Prefabs/FireFlies.prefab` —
  the source prefab. **Gitignored pack.**
- `Assets/Resources/VFX/_Shared/` — `FireFly.mat`, `FireFlyTrail.mat`, `FireFly.fbx`,
  `FireFly.shader`, `FireFlyAlbedo/Emission.tif`, `FireFlyWings.png` — the **git-TRACKED runtime
  mirror** of the firefly art (survives fresh clone / CI).
- `Assets/Mirza Beig/Particle Systems/Ultimate VFX/Prefabs/Loop/pf_vfx-ult_demo_psys_loop_fireflies.prefab`
  — a second fireflies loop. **Gitignored, zero code references.**
- **Butterflies:** `Assets/polyperfect/Low Poly Ultimate Pack/_M/Prefabs_M/Animals_M/Butterfly.prefab`
  (+ `_T` tier, `SM_Butterfly.fbx`) — an animal MESH prefab, not a particle effect. **Gitignored
  pack, zero code references today.** No butterfly particle/VFX asset exists anywhere in the tree.

**Greyscale read (owner is colourblind — judged on value/motion, never hue):** fireflies = small
bright emissive points in motion against the dark canopy — strong value contrast, reads in
greyscale, best at night; known weak in bright midday at sparse scale (above). Butterflies would
be mesh agents needing new spawner/flight wiring (a second spawner — §7 forbids one) and their
greyscale read depends on material value, unverified.

**Working DEFAULT (not a lock): FIREFLIES** — already catalogued, already wired to the tree crown,
already owner-praised verbatim, night-visible, git-tracked art. The owner can flip to butterflies
in one word; that flip would need the polyperfect mesh through the mirror/Addressables pipeline
plus flight animation — a materially bigger slice.

## 4d. STEP 2 FIREFLIES SLICE LANDED (2026-08-16, orchestrator GO, working tree, pending gate)

**GO recorded:** the orchestrator seat issued GO for the fireflies slice on the strength of the two
08-16 owner rulings (quoted verbatim in Provenance above), which are NEWER than and specific
against the WO-1002 withhold at this one site. The OR in "butterflies or fireflies" resolved to
**fireflies** because the butterfly exists only as a gitignored polyperfect mesh prefab with zero
code references (a new spawner — §7 forbids one), while the fireflies are the already-catalogued,
already-crown-wired, owner-praised `TreeofLifeAura_Aura` → `FireFlies` loop ("that was already
there"). No new particle authoring; no creative substitution.

**WO-1002 reconciliation:** §2 proved the amateurish yellow cone is NOT the fireflies (they were
suppressed and not spawning while it drew). So the 08-10 "yellow glow" rejection and the 08-16
fireflies return do not conflict — the exemption is **fireflies-only, one key at one site**, and
every other WO-1002/WO-890 suppression stays byte-intact: the generic
`AmbientAuraPolicy.ShouldWithhold` is unchanged, harvest nodes still refuse the key, the
white-swirl withhold is untouched, and the aura ban stands everywhere else.

**Implementation (narrowest possible exemption):**
- `Assets/_Modules/Village/Vfx/AmbientAuraPolicy.cs` — new `HeartTreeFirefliesExempt = true`
  (static readonly, one flag to undo) + site-scoped `ShouldWithholdAtHeartTree(key)`; the generic
  `ShouldWithhold` is byte-identical.
- `Assets/_Modules/Village/Heart/HeartAuraController.cs` — `ShouldWithholdTreeAura` now routes
  through `ShouldWithholdAtHeartTree`; both branches (play / traced withhold) stay wired.
- `Assets/Editor/Regression/HubTreeAuraWithholdRegression.cs` — Case 2 hub assertion FLIPPED to
  the new canon (withholding at the hub is now the FAIL); Case 1 adds: exemption flag must be
  TRUE, `ShouldWithholdAtHeartTree(rejected)` must be FALSE, generic `ShouldWithhold(rejected)`
  must STAY TRUE (surgical scope). Marker unchanged: `HUB_TREE_AURA_OK`.

**EXPECTED AUDIT DELTA (what the verifier greps for on the next graphics-enabled hub run):**
1. Crown-tether line flips: `treeAuraSuppressed=False` and `treeHandle=live`
   (was `True` / `none` in F8 seq 2398); `whiteSwirlSuppressed=True` UNCHANGED.
2. A `[Flow:Heart]` start line appears: `TreeofLifeAura_Aura FireFlies ambient started at crown ...`.
3. `WO-1025 AUDIT[SETTLED]` header shows `treeAuraSuppressed=False`, and a CHILD ParticleSystem
   line appears under the Heart root with the FireFlies material, `playing=True`,
   `particles>0`, seated near the crown Y.
4. The yellow cone + white starburst emitter lines (whatever the AUDIT names them as) must NOT be
   the FireFlies instance — and that emitter hunt remains OPEN until the trace names it.
5. `HUB_TREE_AURA_OK` with the new reason text ("PLAYS on the hub centerpiece Heart").

## 5. STEP 3 — the tree MODEL itself (separate contributor, do not skip)

`Assets/Art/Tree_Of_Life/` contains exactly one texture:
`enchantedtree3dmodel_basecolor.JPEG` — **a single base-colour map, no normal, no roughness, no AO.**

Under URP that renders flat: no surface relief in the bark, no depth in the canopy, and it sits like a
sticker against the ground. That is a material contributor to "amateurish" independent of the VFX, and
fixing only the particles will leave the object still reading cheap.

**Options (owner call — she is red/green colourblind, so ask about SURFACE and DEPTH, never hue):**

- generate/author a normal + roughness map for the existing mesh (cheapest, biggest single gain)
- swap to a higher-fidelity tree from an owned pack
- ⚠ `Assets/Blink/Art/Textures` holds ~9 GB of stylised biome sets (`StylizedForestTextures`, 24 sets)
  — **currently ZERO code references** (BLINK_SME §2.5). A bark/foliage set there may fit directly.
  Gitignored, so it must go through the mirror/Addressables pipeline, never a direct path reference.

## 6. Do NOT

- Do not flip `_suppressWhiteSwirl` / `_suppressTreeAura` (§2). *(Amended 2026-08-16, §4d: the
  TREE-AURA half is superseded by the owner's fireflies ruling — it now resolves FALSE at the hub
  via the site-scoped policy exemption, not by flipping the flag. The white-swirl half stands.)*
- Do not delete or quieten the `[Flow:Heart]` traces — §12: instrumentation is permanent. **That trace
  is the only reason we know the obvious diagnosis was wrong.**
- Do not reference `Assets/Blink/**` directly at runtime — gitignored, breaks fresh clone / CI / WebGL
- Do not hand-edit `Main_Castle_Overworld.unity` (CLAUDE.md §3) — and note **WO-1022** is already
  repairing that scene; **coordinate, do not both open it**

## 7. Acceptance criteria

- [ ] The emitter of the yellow cone + white starburst is **named in the RESULT, from a captured trace**
- [ ] Neither artifact renders at the hub tree any more (or is deliberately kept, with the owner's
      stated reason recorded)
- [ ] `whiteSwirlSuppressed` / `treeAuraSuppressed` are **unchanged** — verify in the post-fix capture
- [ ] The tree reads with surface depth at gameplay camera distance — **judged from a screenshot, which
      is the primary evidence for a visual defect** (memory
      `screenshots-are-primary-evidence-for-visual-defects`)
- [ ] Before/after screenshots from the same camera position are attached to the RESULT
- [ ] No new spawner or pool introduced — `VFXManager` remains the single path

## 8. Verify

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites`
2. **Graphics-enabled capture** at the hub tree — the fleet is `-nographics` and shoots blank
3. Owner felt-verifies + closes (§13). This is a pure aesthetic judgement; headless cannot rule on it.
