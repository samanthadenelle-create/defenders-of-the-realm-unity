# WORK ORDER 1129 — The art tree reconciliation: one derived path, no typed literals, and a coverage oracle

**Status:** READY TO IMPLEMENT — **owner-requested DEDICATED OVERNIGHT SESSION**
**Minted:** 2026-08-20 (CLI seat) — banner bumped 1129 → 1130 in the SAME edit
**Lane:** Asset organisation + the path-resolution seam + a new coverage gate. Touches
`Assets/EnemyContent/**`, `Assets/Art/Incoming_Tripo/**`, the resolvers, and ~111 call sites.
**Provenance:** owner, 2026-08-20, verbatim: *"I think we need one dedicated overnight session to
properly map everything into the proper structure of the tree. I know it's a big ask, but that's why
I was trying to get everybody to, uh, do it in a way of that we start replacing literals with string
variables. or constants."*

---

## 1. THE INCIDENT THAT PROVES THE NEED — read it, it is the whole argument

On 2026-08-20 the CLI searched `EnemyContent/` and `TripoTex/` for `Orc_Berserker`'s textures, found
none, and reported to the owner: **"no texture anywhere in the project"** — then recommended
commissioning art.

The owner found the answer in **five minutes**, by hand, with a different method: she searched every
filename containing the token **`basecolor`**. That immediately surfaced `Assets/EnemyContent/OrcTex/`
— *a folder the CLI did not know existed* — holding `Orc_Mage_basecolor.jpg`,
`Orc_Tank_basecolor.jpg`, `Orc_Warrior_basecolor.jpg`, plus already-delivered full PBR sets under
`Art/Incoming_Tripo/Enemies/Orcs/`.

Her words: *"the one thing that was common, almost all of them, was the word base color."*

**Two separate lessons, and this ticket exists because of the second one:**

1. **Method.** A name-first search can only ever CONFIRM a guess. A token-first sweep DISCOVERS. The
   CLI's search was also structurally incapable of finding the skeleton art, because those files are
   named `Material_Pbr_Diffuse.png` — no model-name query can hit that; a `diffuse` query hits it
   instantly. (Recorded in project memory as `search-by-token-not-by-name`.)
2. **Structure — THIS TICKET.** The only reason a token sweep was *necessary* is that the same job is
   done four different ways, because every seat that touched enemy art invented its own home for it.

## 2. THE MEASURED STATE

**FOUR competing conventions for one job:**

| convention | who uses it |
|---|---|
| `EnemyContent/TripoTex/<Model>_basecolor.jpg` | Troll, Troll_Mage, Troll_Overlord, Necromancer, Skeleton_Golem, Orc_Mage, Orc_Warlord |
| `EnemyContent/OrcTex/<Model>_basecolor.jpg` | Orc_Mage, Orc_Tank, Orc_Warrior |
| `EnemyContent/<Model>.fbm/Material_Pbr_Diffuse.png` | Skeleton_Warrior, _Rogue, _Healer, _Mage |
| `Art/Incoming_Tripo/Enemies/<Family>/<Model>/` | delivery staging, partially un-ingested |

Note `Orc_Mage` appears in **two** of them with **different textures** — so "which one wins at
runtime" is currently unanswerable without reading the resolver.

**111 distinct asset-path literals** in `.cs` under `Assets/_Modules` + `Assets/Editor`, including
hand-typed `"Enemies/OrcTex/Orc_Mage_basecolor"` sitting a few lines from `"Enemies/TripoTex/"`.

**Consequences already paid for, all on 2026-08-20:**
- 7 enemy ids render near-black wearing the Mage's diffuse (a `.fbx.meta` remap pointing past each
  body's own, unused texture).
- `Necromancer_NEW` / `Skeleton_Golem_NEW` render untextured because lookup is by MODEL NAME and the
  art kept the legacy name — the WO-954 replacements the owner specifically asked for.
- `Orc_Mage` and `Orc_Tank` `.fbm`s share one Tripo material (`tripo_mat_80c4114e`).
- The CLI told the owner art was missing when it was not.

## 3. WHAT TO BUILD

**3.1 — ONE DERIVED RESOLVER.** A single seam that answers *"where does model M's basecolor/normal
live?"* by DERIVING the path, never by a typed literal at the call site. It must handle the legacy
reality (suffix variants like `_NEW`, family sub-folders) explicitly and traceably, so a miss says
WHICH candidates it tried.

**3.2 — ONE PHYSICAL CONVENTION.** Pick it from the data, not taste, and MIGRATE to it. State the
rule in one sentence a future seat cannot misread. Moving art breaks GUID references, so migrate
through Unity (`AssetDatabase.MoveAsset`), never by shell `mv`.

**3.3 — REPLACE THE LITERALS.** The 111 path literals become derived calls or named constants. The
owner's stated goal; it is also what makes 3.1 enforceable rather than advisory.
**⚠ Scope it honestly:** not every literal is an art path. Report the true count after triage rather
than claiming 111.

**3.4 — A COVERAGE ORACLE.** `EnemyArtCoverageRegression` (a sibling may already exist — check
before duplicating). Every model referenced by `enemies.json` must resolve a basecolor. **It must
FAIL and NAME any model with none.** This is the gate whose absence let the whole thing sit
unnoticed, and it is the single most valuable deliverable here.

**3.5 — EXTEND BEYOND ENEMIES.** The owner: *"that same logic can be applied to any single thing
that's missing a texture. Almost all of it was built from Tripo, so all of it's gonna have those
files."* Structures, heroes and VFX share the pattern. Do enemies first and prove the shape, then
widen — but the oracle should be written so widening is adding a source list, not a rewrite.

## 4. DO NOT

- Do NOT change which body any enemy id wears. Model choice is an OWNER decision (the KayKit
  `Skeleton_Minion` replacement and the orc re-point are both hers, pending renders).
- Do NOT move art with shell commands. GUID references break silently and the failure surfaces days
  later as a null texture.
- Do NOT delete a texture because it looks unused. `Orc_Warlord_basecolor.jpg` has no live consumer
  today and is exactly the art a future re-point would want.
- Do NOT let this become a tidy-up. If the resolver and the oracle do not land, the folders will
  drift again within a month — that is what happened last time.
- Do NOT weaken the oracle to make the tree green. A named failure is the deliverable.

## 5. ACCEPTANCE CRITERIA

1. One derived resolver; no art path typed at a call site.
2. `enemies.json` models resolve their basecolor through it, and the resolver TRACES its candidates
   on a miss.
3. The coverage oracle FAILS, by name, for any model with no resolvable colour map — and its
   expected failure list at landing time is stated in the RESULT, not silenced.
4. Art physically consolidated to the chosen convention, moved via `AssetDatabase`, with GUIDs
   preserved (prove it: no new missing-reference warnings).
5. A render pass through `Assets/Editor/EnemyProvingHarness.cs` shows every enemy no worse than
   before, and the previously-broken ones fixed. **The picture is the evidence** — the owner is
   red/green colourblind and reviews renders with a second pair of eyes, so pair every image with a
   plain-language read.
6. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n>` (read the count off the marker; it is **225/225,
   0 red** as of 2026-08-20 — the first fully-green state this project has had. Do not re-red it
   except through the new oracle's intended, reported failures).
7. Canon updated in the same change (§15): the convention rule recorded where the next seat will
   find it, not only in this ticket.

## 6. WHY OVERNIGHT, AND WHY IT IS WORTH THE ASK

This is a wide, mechanical, high-blast-radius change that wants uninterrupted batchmode time and a
fleet working file-disjoint lanes — exactly the shape CLAUDE.md §11 describes. It is not urgent in
the way a P0 hang is, and it is not player-facing on its own. But every hour spent today on "does
this model have art" was a tax this ticket removes permanently, and the same tax has already been
paid more than once by more than one seat.
