# WORK ORDER 838 — Raid-base material survivability: white walls, flat towers, magenta troops (F8 seq 606)

**Status:** CLOSED - owner felt-verified 2026-08-21: *"close 838 its done, raids work and not white"*. The white-walls / flat-towers / magenta-troops symptom is gone on device. Phase A probe is MOOT - the defect it was to instrument no longer reproduces, so do not run it.
line for the one remaining INFERRED link before any material edit lands)
**Minted:** 2026-08-02 (RCA agent, from owner F8 seq 606 + follow-up observations, RaidBase_mage_enclave desktop player build)
**Silo:** Art pipeline / raid-base bake. File-disjoint from gameplay lanes (§9).
**Ticket:** F8 seq 606 — "RaidBase_mage_enclave renders all pink"; screenshot t-1min: palisade walls =
untextured WHITE slabs, tower masses = flat untextured brown; follow-ups: "the troops were magenta",
"the raid is just a square room with 1 enemy" (log: 6 raidguards spawned).

---

## RCA — three distinct causes, one shared failure class

The shared class: **baked/spawned raid art references material state that does not survive this
machine's import or the URP player build**, and MagentaGuard's safety net structurally cannot see two
of the three. This is the same family as the 07-15 magenta terrain (`KEY_FACTS.md:208-218` —
`ExteriorTerrainMaterial.mat` never in git) and WO-785 (117/121 VFX rows dangling into gitignored packs).

### Finding 1 — WHITE wall slabs: the wall FBXes lost their textures at IMPORT, on every machine but the original author's (PROVEN)

- `RaidBase_mage_enclave.unity` contains 105 prefab instances from exactly 2 sources: **86×
  `steel_wall.fbx`** (guid `2098d40550a0c704a93f8f12410faace` = `Assets/Resources/Walls/steel_wall.fbx.meta:2`)
  and 19× `Tower_Medieval_Wood.prefab` (guid `a1f0092a5daf24641823b23303632bb8`). Config
  `mage_enclave` has `wallTier = ReinforcedSteel` (`Assets/Resources/Data/Canonical/scene-configs.json`),
  and `RaidBaseGenerator.PlaceSegment` loads the tier art via
  `Resources.Load<GameObject>(WallTierData.Get(tier).SegmentPrefabPath)`
  (`Assets/Editor/WallTools/RaidBaseGenerator.cs:497`) → `"Walls/steel_wall"`
  (`Assets/_Modules/Village/Walls/WallTierData.cs:85`).
- `steel_wall.fbx.meta` imports with **`materialImportMode: 2` and `externalObjects: {}`**
  (`steel_wall.fbx.meta:6-11`) — FBX-EMBEDDED materials, no remap to any tracked `.mat`.
- **The FBX binds its textures by ABSOLUTE PATH ON ANOTHER MACHINE.** Binary strings in
  `steel_wall.fbx`:
  `C:\Users\Kayden-Laptop\Documents\defenders-unity\Assets\Resources\Walls\steel_wall.fbm\fantasystonegateway3dmodel_basecolor…`
  (+ metallic/normal/roughness). The `steel_wall.fbm` folder **does not exist in the repo**, and
  `steel_wall.fbx` contains **ZERO embedded JPEG payloads** (0 × `FFD8FF` markers) — so on any machine
  without Kayden-Laptop's `.fbm` folder the importer cannot resolve any albedo. The imported embedded
  material is a textureless lit material → **white slabs, in the editor AND in every build**. This is
  import-time loss, NOT build-time shader stripping.
- The repo DOES track textures — `Assets/Resources/Walls/Textures/steel_basecolor.JPEG` (+ metallic/
  normal/roughness, all in `git ls-files`) — but under **different filenames**
  (`steel_basecolor` vs the FBX's `fantasystonegateway3dmodel_basecolor`), and with no
  `externalObjects` remap, so they are wired to NOTHING. The owner's art shipped; the wiring never did.
- Same class in the sibling scenes: `RaidBase_raider_camp_small` = 34× `wood_wall.fbx`
  (guid `7b00f839afa16244dbdb88377a256959`), `RaidBase_fortified_garrison` = 43× `iron_wall.fbx`
  (guid `236e256666b94244993e2150af75aede`); both FBXes carry the same Kayden-Laptop `.fbm` paths
  (`wood_wall.fbx` has exactly 1 embedded JPEG — at most partial texture survival).
- **Why MagentaGuard can't catch it:** a textureless-but-valid URP/Lit material passes
  `IsBrokenShader` (`Assets/_Modules/Core/MagentaGuard.cs:335-349` — only null / unsupported /
  Standard / Legacy / InternalError count as broken), and the colorless-repaint branch only applies to
  **ground-like** renderers (`IsGroundLike`, `MagentaGuard.cs:51-60`: name tokens or footprint
  >8m×>8m and <2m tall) — a 1.5×3×1.5 wall segment matches neither. White-but-valid is invisible to it
  **by design**. (Scene ground is fine: `RaidGround` carries a scene-embedded URP/Lit dark-earth
  material, `RaidBase_mage_enclave.unity` local id `&998714562`, baked by
  `Assets/Editor/RaidNavBake.cs:96-104`.)

### Finding 2 — flat BROWN towers: gitignored polyperfect swatches on a legacy shader, recovered at load to plain URP/Lit (PROVEN)

- `Assets/Resources/Structures/Tower_Medieval_Wood.prefab:64-66` references two material guids
  `6e3ac6e47b4d7a44685dfef9bf209413` / `d07dc9a550022a74792d211f343e0b16` =
  `Assets/polyperfect/Low Poly Ultimate Pack/Materials/Colors/M_12_Brown_LPUP.mat` /
  `M_10_Brown_Dark_LPUP.mat` — **gitignored** (`.gitignore:128`), present locally, and **on the
  built-in legacy shader** (`m_Shader: {fileID: 45, guid: 0000000000000000f000000000000000}` in both
  `.mat` files): the "Fix Polyperfect URP Materials" pass has not been (re-)applied to the current
  local pack copy.
- These are **exactly the "2 lost-shader materials" MagentaGuard recovered** at scene load
  (19 tower instances share 2 unique broken mats → 2 recoveries → fresh plain URP/Lit carrying the
  flat brown `_Color`, `MagentaGuard.cs:196-221` + `BuildRecoveredMaterial:361-390`). Result: towers
  render, but as recovered flat-brown masses. LPUP swatches are flat-color by design, so this is the
  mildest of the three — but it still ships through a safety net instead of through source art.

### Finding 3 — MAGENTA troops: Supercyan materials are Standard-shader on disk AND deploy happens after the only sweep MagentaGuard ever runs (PROVEN)

- Footman body chain: `troops.json` `troop-footman → model SC_Footman` →
  `TroopFactory.Build` → `VisualFactory.Skin(go.transform, "Heroes/" + model, …)`
  (`Assets/_Modules/Village/Troops/TroopFactory.cs:86`) → `Assets/Resources/Heroes/SC_Footman.prefab`,
  which is a **thin variant with NO material overrides** (only transform + name modifications) of
  `Assets/Supercyan/Prefabs/Fantasy/Base/High Quality/Knight.prefab` (guid
  `f2781c7095395174f93242b31054ccd1`) — a **gitignored pack** (`.gitignore:131-138`, which itself says:
  re-import then *run Defenders/Art/Fix Supercyan URP Materials "so it isn't magenta"*).
- On this machine the Supercyan mats are **built-in Standard**:
  `Assets/Supercyan/Materials/Fantasy/High Quality/fantasy_knight_body.mat` / `fantasy_archer_body.mat`
  both read `m_Shader: {fileID: 46, guid: 0000000000000000f000000000000000}`. Standard under URP =
  magenta, editor and player alike. The fixer exists (`Assets/Editor/SupercyanUrpMaterialFix.cs`) and
  was simply not run after the pack last landed (pack is gitignored, so git cannot preserve the fixed
  state across re-imports).
- **The MagentaGuard coverage gap (structural):** Standard IS in its broken set
  (`MagentaGuard.cs:344`), so a scene-placed Supercyan body would be recovered — but MagentaGuard runs
  **only** at boot + `SceneManager.sceneLoaded` (`MagentaGuard.cs:62-72`). Troops are spawned
  **mid-raid** by `TroopDeployer.SpawnFromArmy → TroopFactory.Build`
  (`Assets/_Modules/Village/Troops/TroopDeployer.cs:65-88`). **Any renderer spawned after scene load is
  never swept** — runtime-spawned magenta is invisible to the guard. Same gap applies to every other
  runtime spawner.

### Disproven / separated

- **Destroyed-state material swap (t=919s, structures DEAD): DISPROVEN as a cause.**
  `StructureDamageVisuals` is presentation-only — VFX loops + floating health bars via
  `VFXManager.PlayKey`, zero material writes (`Assets/_Modules/Village/Vfx/StructureDamageVisuals.cs`);
  the walls lane contains no destroyed-material swap; `RepairHighlight` builds its own null-guarded
  unlit marker (`Assets/_Modules/Village/Walls/RepairHighlight.cs:153-172`). The scene looked wrong
  from the first frame, not from a death event.
- **"All pink":** composite read of the above — 86 untextured white slabs under scene lighting +
  magenta troop bodies. Same perceptual class as the proven castle-floor precedent (colorless lit
  surface reads pink/lavender under tinted light, `MagentaGuard.cs:141-149`).

### Scope separation — sparseness is NOT this WO

"The raid is just a square room with 1 enemy" splits in two. Base **content/depth** (single ground
plane, layout density, tower fire, art ladder) is owned by the already-open raid arc — WO-774 polish,
WO-802 stakes, WO-771.10 tower-fire, WO-772 art ladder, sequenced in WO-824 wave 3 — and is
explicitly **out of scope here**. WO-838 owns **only material/visibility survivability**. The log
datum — **6 raidguards spawned, owner perceived 1** — is treated as *visibility evidence* for this
WO (bodies unreadable against white slabs / broken materials), and the acceptance below re-checks it
after the material fixes land.

### PROVEN vs INFERRED

| Claim | Status |
|---|---|
| Wall FBX textures bind to a nonexistent other-machine `.fbm`; zero embedded JPEGs in steel_wall.fbx; no externalObjects remap; tracked textures have unmatched names | **PROVEN** (disk + binary dump, cited above) |
| Supercyan mats are Standard-shader on disk; SC_Footman variant adds no material overrides; MagentaGuard sweeps only at scene load | **PROVEN** (file citations above) |
| Tower mats = 2 legacy-shader LPUP swatches = the 2 recovered materials | **PROVEN** (guids + shader ids + recovery count match) |
| The imported wall material's exact runtime state (shader name, `_BaseMap == null`, base color ≈ white) | **INFERRED** — Phase A probe captures it before the fix lands (§12 hard gate) |

---

## Fix plan

**BINDING constraint: never substitute owner art silently** (memory
`vfx-map-owner-tags-no-creative-pick`). Every fix below wires art the owner already shipped
(`Walls/Textures/*` landed with the FBXes on 07-14) or runs the project's established conversion
tools. Anything requiring a creative choice is HELD and reported.

### Phase A — probe first (MANDATORY, before any material edit)

Add a `RaidBaseMatDiag` batchmode editor method (FloorDiag pattern, `MagentaGuard.cs:231-321`):
open each `Assets/Scenes/RaidBase_*.unity`, and for every Renderer dump one `[Flow:RaidBaseMatDiag]`
line: hierarchy path, mesh, material name, shader name, `_BaseMap`/`mainTexture` null-or-name,
`_BaseColor`. Run headless via run-unity-method (verify MARKER + log freshness, not exit code).
**Expected proof line:** the 86 wall renderers on an embedded lit material with a null base map.
If the probe instead shows a bound texture, STOP — the RCA's inferred link is wrong; re-triage before
Phase B.

### Phase B — walls: tracked survivable materials + importer remap (the core fix)

1. Create three tracked URP/Lit materials, e.g. `Assets/Resources/Walls/Materials/{wood,iron,steel}_wall.mat`,
   binding the **already-tracked** `Assets/Resources/Walls/Textures/<tier>_basecolor.JPEG`
   (+ normal / metallic / roughness maps as URP slots).
2. Remap each wall FBX's embedded material to its tracked `.mat` via ModelImporter
   `externalObjects` (`AddRemap`) — an editor script, no hand-edited `.meta`.
3. Re-bake the three raid scenes: `RaidBaseGenerator.BuildAllRaidScenes` then `RaidNavBake.BakeAll`
   (never hand-edit `.unity`). The scene's prefab instances then serialize refs to tracked materials +
   tracked textures — survivable on every machine and every build.

### Phase C — towers: convert at source, then stop trusting the safety net

Run the existing `Defenders/Art/Fix Polyperfect URP Materials` pass (CLAUDE.md §4 — the sanctioned
conversion) so `M_12_Brown_LPUP` / `M_10_Brown_Dark_LPUP` are URP on this machine, and re-bake.
Because the pack is gitignored (conversion state is machine-local and dies on re-import), the durable
protection is the Phase E oracle, which fails the gate whenever a baked scene references a
legacy/Standard material again. (Optional hardening, owner call — NOT done silently: retarget
`Tower_Medieval_Wood.prefab` to tracked same-color URP swatch materials under `Assets/Resources/`.)

### Phase D — troops: convert at source + close the runtime-spawn coverage gap

1. Run `Defenders/Art/Fix Supercyan URP Materials` (`Assets/Editor/SupercyanUrpMaterialFix.cs`; the
   `.gitignore:131-138` instruction + `docs/SUPERCYAN_REIMPORT.md`) and verify the `fantasy_*` mats
   read URP shader ids afterward.
2. Close the structural gap: expose a public `MagentaGuard.SweepGameObject(GameObject)` (reusing the
   existing recover path) and call it from the spawn seams after the visual is built —
   `TroopFactory.Build` (`TroopFactory.cs:112` area) and `EnemyFactory`'s equivalent — so a
   runtime-spawned broken body self-recovers AND self-identifies in the log exactly like scene-load
   catches do. Keep it idempotent + cheap (per-spawn, renderers of one root only).

### Phase E — the loud drift oracle (07-15 mitigation pattern; the regression this WO must leave behind)

Extend the gate (DataRegression / CompileGate family) with a **build-scene material survivability
check** that FAILS LOUD when, for any scene in build settings + any prefab under `Assets/Resources/`:
- a Renderer/material reference resolves to a guid whose asset file is **not tracked by git**
  (`git ls-files` check — the ExteriorTerrainMaterial class, `KEY_FACTS.md:208-218`), or dangles
  entirely;
- a referenced material sits on a **Standard / Legacy** shader (the magenta class);
- a ModelImporter-embedded opaque lit material has a **null base map** (the white-slab class) on a
  mesh instantiated by a scene.
Failures name asset path + scene + material (self-identifying, §12). This is the "never again at
that scale" guard for the whole class, not just these three scenes.

---

## Acceptance criteria

1. **Probe first:** `RaidBaseMatDiag` output committed to the RESULT file; the pre-fix run shows the
   86 textureless wall materials (the §12 proof line); the post-fix run shows every wall renderer on
   a tracked material with a bound basecolor texture.
2. Desktop player build: `RaidBase_mage_enclave` walls render the steel basecolor texture (not white);
   `raider_camp_small` / `fortified_garrison` likewise wood / iron. Headless screenshot-verify
   (RunCaptureHeadless, open the PNGs) BEFORE handing to the owner.
3. Deployed troops (Footman + Archer bodies) are NOT magenta in the player build, deployed
   **mid-raid** (i.e. after scene load) — screenshot proof; `[Flow:MagentaGuard]` shows zero
   Standard/Legacy recoveries on the troop bodies (fixed at source), and the new spawn-seam sweep is
   exercised by test.
4. Towers no longer depend on MagentaGuard recovery: post-fix load log shows **0** "recovered
   lost-shader material" lines for the raid scenes.
5. **Regression (required):** a gate test that FAILS when a build-scene references an untracked
   material file — proven by a red run against a deliberate untracked-mat fixture, then green on HEAD.
6. Re-check the visibility datum: with materials fixed, an owner (or screenshot-verified headless) run
   of mage_enclave shows the 6 spawned raidguards are visually locatable; if not, that residue routes
   to the WO-824 wave-3 lane, not here.
7. No creative substitution anywhere: only owner-shipped textures wired, only established conversion
   tools run; any judgment call surfaced to the owner in the RESULT.

## What NOT to touch

- Base layout / density / garrison composition / tower fire (WO-774 / WO-802 / WO-771.10 / WO-772,
  WO-824 wave 3).
- No hand-edits to any `.unity` (re-bake via the generators only; §3).
- `VfxManualPicks.json` / VFX keys (WO-785's lane).
- `WallSegment` gameplay stats / tier toughness (cosmetic path only).
- Stale-flag, don't rewrite: `WallTierData.cs:85` comment "PENDING owner art (runic steel)" is stale
  (steel_wall.fbx landed 07-14) — fix the comment in the same commit as Phase B.


---

# CLOSED BY OWNER FELT-VERIFY 2026-08-21

Owner verbatim: **"close 838 its done, raids work and not white"**.

This ticket demanded a Phase A instrumentation probe as a MANDATORY first step before any fix
(canon 12: no edit until captured data proves the cause). That gate is now moot - the raid bases
render correctly on device and the symptom does not reproduce, so there is no live defect left to
instrument. **Do not run the probe and do not re-open on the strength of the old F8 seq 606
capture** - that capture is a point-in-time record of a state that no longer exists.

Related work that landed in the same era and plausibly resolved it (recorded as context, NOT as a
proven cause - nobody bisected this): the raid-base material diagnostic + wall-material fixer
(`Assets/Editor/RaidBaseMatDiag.cs`, `Assets/Editor/WallTools/RaidWallMaterialFixer.cs`) and the
enemy/structure art migration onto the R2 content pipeline.

NOTE for whoever reads this next: a SEPARATE and still-open gap was found on 2026-08-21 - the
three wall TIERS have no tracked materials at all and render from embedded FBX materials
(**WO-1135**). That is a different defect from this one and closing 838 does not close it.
