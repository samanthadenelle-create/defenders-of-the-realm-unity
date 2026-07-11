# WORK ORDER 670 — Motion Caster (standalone clip-casting tool)

**Status:** READY TO IMPLEMENT
**Minted:** 2026-07-11 (owner ask, verbatim: "create a separate tool stand alone where i load in the model, and you load the rig with all the motion options, and I can select what I want and tie it back to keyword that we can save to each type Enemy family or player")
**Lane:** Editor tools (`DeNelle.Editor` / `Assets/Editor`) — editor-only, no runtime code.
**Numbering:** 670 (Grok audit block ended 664; 665–669 suggested/reserved in `GROK_CLI_SESSION_HANDOFF_2026-07-09.md` §4 — confirm in `CLI_LANES_WO_NUMBERS.md` on mint).
**Precedent pattern:** Offset Forge (WO-490) / Seating Editor — owner-in-the-loop authoring tool; a `manual=true` owner pick is canon and is NEVER overwritten by an auto pass.
**Architecture (BINDING for this WO):** `docs/ACTION_KEYWORD_REGISTRY_ARCHITECTURE.md` — the keyword→action registry design (file shape §1, closed vocabulary §2, bake-time V1 resolution §3, tool write contract §8). The tool is the WRITER of that registry; the controller builders are the first READERS.

## The tool (one sentence)
An EditorWindow where the owner loads any character model, the tool stands it up on the humanoid rig with EVERY motion clip we own, the owner previews and picks, and each pick is tied to a KEYWORD and saved per target (enemy family or hero class) into a data file the animator-controller builders consume.

## Why EditorWindow, not a separate exe (easy-vs-right, named)
The retarget stack (Humanoid avatar mapping), clip sampling, and model preview all live in the Unity editor (`PeopleCharacterImporter`, `HeroPackageImporter`, AnimationMode/PreviewRenderUtility). A standalone exe would re-implement retargeting for zero gain. "Standalone" is honored as: its own menu entry, fully self-contained window, no dependency on any scene.

## Slice 1 (this WO)
1. **Window:** `Defenders → Animation → Motion Caster`.
2. **Load model:** ObjectField (FBX/prefab). Tool verifies/creates the Humanoid avatar (reuse the `PeopleCharacterImporter` verdict pass: OK Humanoid / WARN Generic / FAIL + bone-map repair). Show the verdict inline.
3. **Motion library:** scan the known clip sources into one searchable list (source-tagged):
   - `Assets/HeroPackages/*/Animations/Extracted/*.anim` (retargeted, ready)
   - `Assets/Action/**/*.fbx` (Mixamo + ActorCore packs: Sword & Shield Moves, Magical Moves, Hero Motion)
   - `Assets/Models/KayKit` clip library (gitignored — warn-not-error when absent, §4 rule)
   - Filter by name/pack/type; the `docs/animations/Knight_Anim_Inventory.md` taxonomy (Attack/Block/Cast/Locomotion/Reaction/Signature) as category chips.
4. **Preview:** sample the selected clip on the loaded model in-window (AnimationMode preview on a PreviewRenderUtility stage; play/pause/scrub + loop). Female/off-rig takes preview through the retarget so the owner judges the REAL read.
5. **Keyword binding:** rows of `keyword → clip`. Keyword vocabulary = the animator contract keys already consumed by the controller builders (idle, walk, run, combatIdle, attack0..attack3, heavy, skill1, skill2, cast, castChannel, hit, death*, taunt, dodge, block, parry, unsheathe…) — read the live set from the builders, don't invent a parallel list.
6. **Target + save:** target picker = enemy family (`hollow`, `orc`, `troll`, + future) or hero class (`knight`, …). Save writes `motion-castings.json`:
   ```json
   { "version": 1,
     "targets": { "orc": { "attack0": {"clip":"<path-or-guid>","manual":true,"pickedUtc":"..."}, ... },
                   "knight": { ... } } }
   ```
   - Owner picks are `manual: true` — canon, never overwritten by any auto pass (Offset Forge law).
   - Authoring copy under `Assets/StreamingAssets/Data/Canonical/motion-castings.json`; editor-consumed only (controller bakes), so no Resources mirror needed until runtime reads it — note that in the file header.
7. **Consumption seam (wire, don't big-bang):** `KnightPackageControllerBuilder` + `BuildOrcHumanoidController` + `AnimatorSetup` read `motion-castings.json` FIRST for any keyword present (falling back to their current hardcoded picks) — additive, behavior-identical when the file is empty.

## Out of scope (slice 2+)
Batch extract/retarget of un-imported packs from inside the tool (today: pick the FBX, tool warns "needs extraction" and lists the import menu to run); side-by-side A/B compare; per-clip trim/speed authoring.

## Acceptance
- [ ] Load Knight_Hero + an orc model; both preview clips from all three source kinds.
- [ ] Pick `attack0` for family `orc`, Save → `motion-castings.json` row with `manual:true`; re-running the orc controller bake uses the picked clip (log line proves it: `[MotionCaster] '<target>.<keyword>' -> '<clip>' (manual)`).
- [ ] Empty/absent JSON = every builder's output byte-identical to today (behavior-preserving gate).
- [ ] Missing gitignored pack = LogWarning, tool still opens.
- [ ] `COMPILE_GATE_OK`; brace/NUL clean; no runtime asmdef touched.

## Do NOT
- Touch runtime assemblies, scenes, or the existing controller outputs' current defaults.
- Overwrite an existing `manual:true` row from any code path.
- Hand-build a second clip-scan if `HeroPackageImporter`/inventory helpers already expose one — reuse.
