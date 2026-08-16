# WORK ORDER 1025 — Heart of Elarion reads amateurish: unidentified VFX + an unlit single-texture tree

**Status:** READY TO IMPLEMENT (⚠ step 1 is INSTRUMENTATION, not a fix — see §3)
**Minted:** 2026-08-15 (UI seat) — provenance stack bumped 1025 → 1026 in the same edit
**Lane:** World art / VFX presentation. Disjoint from WO-1021 (talent UI), WO-1022 (scene GUIDs),
WO-1024 (repair lifecycle).
**Provenance:** owner 2026-08-15, verbatim: **"these graphics on tree look amatuerish"**, with an
in-game screenshot of the Heart of Elarion at the hub centre.
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

- Do not flip `_suppressWhiteSwirl` / `_suppressTreeAura` (§2)
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
