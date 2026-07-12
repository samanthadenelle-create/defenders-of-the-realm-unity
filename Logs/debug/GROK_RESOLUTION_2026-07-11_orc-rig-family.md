# GROK RESOLUTION — Orc rig family escalation (2026-07-11)

**Answers the four asks in `GROK_ESCALATION_2026-07-11_orc-rig-family.md`.**
**Repo:** `C:\eoa` · branch `wip/village2-and-f8-tickets`

---

## Verdict (one line)

**OrcHumanoid trio (Warrior / Tank / Mage): AccuRig re-export is the only honest fix. Unity import cannot salvage unweighted Tripo mesh chunks. Berserker: one more import pass; if still GENERIC → same re-export lane.**

---

## Ask 1 — Any Unity-import salvage for Tripo orc FBXs?

**No** for the OrcHumanoid family. The RCA is asset-level, not controller/avatar-level:

| Evidence | Orc_Warrior (broken) | Skeleton_Warrior (working) |
|----------|----------------------|----------------------------|
| `tripo_part_*` at root | 21 chunks, **large world offsets** (y=4–7) | 6 chunks at **origin** (0,0,0) |
| SMR `rootBone` | `tripo_part_*` (rigid chunk) | `CC_Base_Hip` (armature) |
| SMR `bones[]` | No Hip / Pelvis / CC_Base | Includes `CC_Base_*` chain |
| `globalScale` / `useFileScale` | 5.1–7.9 / 0 | 1 / 1 |
| Runtime signature | `everPlayed=True boneMoved=False` | animates correctly |

Humanoid avatar + Mixamo clips animate the **buried** `ParentNode/Armature/Root/Hip` chain. The **visible** body is rigid `tripo_part_*` siblings parented to the FBX root — they never receive skin weights. No `ModelImporter` setting, donor avatar, or `humanDescription` copy fixes that; the mesh was never bound to the skeleton in the source file.

**What Unity import *can* still do (already in tree):**
- Detect and FAIL loudly: `OrcRigBindingAudit` (editor regression + `ImportOrcFamily` tail)
- Runtime discriminator: `EnemyPoseVerifier` now samples **both** SMR bone and `HumanBodyBones.Hips` — `hipMoved=True + boneMoved=False + smrRoot=tripo_part_*` → `verdict=NEEDS_ACCURIG_REEXPORT`

**What cannot work:**
- Skin-binding transfer in-editor (no weights in source)
- Cross-family donor avatar (OrcWarband ≠ OrcHumanoid rig)
- `CreateFromThisModel` on a posed/degenerate bind (fixes avatar label, not mesh)

---

## Ask 2 — AccuRig re-export pipeline (stable paths + materials)

Batch all Tripo orcs through the **same pipeline that produced Skeleton_*** (owner already owns this).

### Per model (Warrior, Tank, Mage — repeat for each)

1. **Source art:** `Assets/Art/Incoming_Tripo/Enemies/Orcs/<Model>/` (textures already there).
2. **AccuRig / Character Creator:**
   - Import Tripo mesh (or merged OBJ if Tripo ships multi-chunk).
   - Run AccuRig auto-rig → verify **one** skinned body bound to `CC_Base_Hip` chain (not loose parts).
   - Export FBX: **Humanoid skeleton, Y-up, scale 1, single skinned mesh preferred**.
3. **Blender sanity (optional):**
   - Confirm all vertices have armature weights (no orphan meshes parented to root).
   - Delete rigid `tripo_part_*` root children if AccuRig left debris.
4. **Stage without churning paths:**
   - Overwrite `Assets/Art/Incoming_Tripo/Enemies/Orcs/<Model>/<Model>.fbx` (staging).
   - Copy textures to same folder (`*_basecolor.jpg` etc.) — unchanged names.
5. **Promote to Resources (CLI, Unity closed):**
   ```powershell
   powershell -File run-unity-method.ps1 -Method DeNelle.Editor.PromoteOrcsToResources.Run -LogName promote-orcs.log
   ```
   Expect: `PROMOTE_ORCS_OK` + Humanoid valid.
6. **Import + controllers:**
   ```powershell
   powershell -File run-unity-method.ps1 -Method DeNelle.Editor.PeopleCharacterImporter.ImportOrcFamily -LogName orc-family.log
   powershell -File run-unity-method.ps1 -Method DeNelle.Editor.BuildOrcHumanoidController.Build -LogName orc-ctrl.log
   ```
7. **Verify binding (must pass before owner playtest):**
   ```powershell
   powershell -File run-unity-method.ps1 -Method DeNelle.Editor.OrcRigBindingAudit.RunMenu -LogName orc-binding.log
   ```
   Expect: `ORC_BINDING_OK`. Any `UnboundTripoChunks` = re-export not done correctly.
8. **Materials:** `TripoEnemyMaterialExtractor` + `externalObjects` remaps stay valid — texture paths `Resources/Enemies/OrcTex/<Model>_basecolor` unchanged. Re-export does **not** require catalog/spawner row changes (`Orc_Warrior` slug stable).

### Batch order (owner)

| Model | Priority | Notes |
|-------|----------|-------|
| Orc_Warrior | P0 | Most spawned (WO-481 leader) |
| Orc_Tank | P0 | Same family, same defect |
| Orc_Mage | P0 | Same family |
| Orc_Berserker | P1 | OrcWarband — separate rig; see Ask 3 |
| Orc_Shaman / Necromancer | P2 | Warband — Shaman animates; verify after Humanoid trio fixed |

---

## Ask 3 — Berserker: anything left before re-export?

Berserker is **OrcWarband** (People biped), not OrcHumanoid. Failure mode differs:

```
rig=GENERIC vs Humanoid clips on controller 'OrcWarband'
```

All three repair passes failed (CreateFromThisModel / donor sourceAvatar / donor humanDescription).

**Try once more (CLI):**
```powershell
powershell -File run-unity-method.ps1 -Method DeNelle.Editor.PeopleCharacterImporter.ImportOrcFamily -LogName orc-family.log
```

Check log for `Orc_Berserker: OK Humanoid` vs `WARN Generic` / `FAIL`.

**If still GENERIC:**
- Open `Assets/Resources/Enemies/Orc_Berserker.fbx` in Unity → Rig tab → Humanoid → **Configure** → manual map (same bone names as Orc_Shaman which works).
- If hand-map fails (deformed mesh / wrong skeleton): re-export Berserker through AccuRig **or** swap spawn to a working warband donor silhouette temporarily.

**No Tripo `tripo_part` issue** on Berserker meta — this is avatar-type mismatch only, salvageable in-editor if bone names match Shaman.

---

## Ask 4 — Discriminator sanity (`avatarValid` vs `sampleBone`)

| Case | `avatarValid` | `smrRoot` / `sampleBone` | `hipMoved` | `boneMoved` | Verdict |
|------|---------------|--------------------------|------------|-------------|---------|
| Healthy Skeleton | True | `CC_Base_*` | True | True | OK |
| Tripo unbound (current bug) | True | `tripo_part_*` | True | False | **NEEDS_ACCURIG_REEXPORT** |
| Degenerate avatar | False | any | False | False | DEGENERATE_AVATAR |
| Generic + Humanoid clips | True/False | any | False | False | DEGENERATE_AVATAR / re-import |
| Headless fleet | n/a | n/a | n/a | n/a | **skipped** (CullUpdateTransforms) |

**False positive guard:** `tripo_part_*` alone is NOT sufficient to fail — `Skeleton_Warrior` meta also lists `tripo_part_*` at root but SMRs bind `CC_Base_Hip` at origin. Discriminator requires **`smr.rootBone` or `bones[]` lacks armature** AND/OR **`hipMoved && !boneMoved`** at runtime.

**False negative guard:** sampling only SMR bones missed Hip motion; verifier now samples both.

---

## Code landed (this resolution)

| File | Change |
|------|--------|
| `Assets/Editor/Regression/OrcRigBindingAudit.cs` | Asset oracle — fails headless if OrcHumanoid SMRs unbound |
| `Assets/Editor/Regression/DataRegression.cs` | Registers `[orc-binding]` check |
| `Assets/_Modules/Village/Enemies/EnemyAnimatorFactory.cs` | `EnemyPoseVerifier`: hip+smr dual sample, `verdict=` tag |
| `Assets/Editor/PeopleCharacterImporter.cs` | `ImportOrcFamily` appends binding audit |

---

## Owner next step

1. Batch re-export Orc_Warrior / Tank / Mage through AccuRig (section Ask 2).
2. CLI runs promote → ImportOrcFamily → binding audit → compile gate → build.
3. Owner felt-verify one arena fight — expect `[Flow:EnemyPose] pose OK` on all three.
4. Berserker: import pass → hand-map if needed → separate felt-verify.

**Do NOT spend another import-repair cycle on OrcHumanoid Tripo FBXs without a new AccuRig export — two failures + asset RCA prove the mesh is unweighted.**