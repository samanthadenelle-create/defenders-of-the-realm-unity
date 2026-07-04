// =============================================================================
// PaladinGearInspector — read-only editor probe for the imported Paladin hero FBX.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor   Namespace: DeNelle.Editor
//
// WHY THIS EXISTS:
//   The owner's Paladin FBX (Assets/HeroPackages/Paladin/Paladin_Hero.fbx, "WProp"
//   = with prop) has its SWORD and SHIELD baked into the model. The game equips
//   weapons/shields as SEPARATE props via EquipmentController + the paper-doll, so
//   baked gear risks a DOUBLE sword/shield and orientation conflicts (owner F8
//   2026-07-03 "holding two swords, shield 180 degrees wrong"). A prior fix added
//   PackageBakedGearMarker so EquipmentController SUPPRESSES prop-attach on package
//   bodies with baked gear.
//
//   This probe LOGS each FBX's full transform hierarchy (every child name + depth)
//   and each renderer's submesh/material names, flagging any node whose name reads
//   like SWORD / SHIELD / WEAPON / PROP. GOAL: reveal whether the sword/shield are
//   SEPARATE named transforms (togglable / removable / equippable) or FUSED into
//   the single body skinned mesh (one renderer, submeshes only). The orchestrator
//   runs it in batchmode via DeNelle.Editor.PaladinGearInspector.Inspect.
//
//   It inspects BOTH package bodies (coordinator 2026-07-03):
//     • Assets/HeroPackages/Knight/Knight_Hero.fbx  — the CURRENT package body
//       (textured via 'Paladin_MAT' -> Paladin_diffuse/normal/specular.png).
//     • Assets/HeroPackages/Paladin/Paladin_Hero.fbx — the owner's WProp body
//       (sword+shield baked, textures shipped in its .fbm).
//   KEY QUESTION: is Knight_Hero.fbx PROP-LESS (just the body)? If yes, the cleanest
//   fix is to bind those same three textures onto the CURRENT body — a fully textured
//   Paladin with NO baked-gear problem — and we may not need the WProp file at all.
//   If Knight_Hero.fbx ALSO carries baked gear, both have the issue and we handle it
//   via PackageBakedGearMarker (already wired).
//
// -----------------------------------------------------------------------------
// PLAN (recommendation — decide once the probe output for BOTH FBX is read):
//
//   FIRST DECISION — is the CURRENT body (Knight_Hero.fbx) prop-less?
//     • PROP-LESS (no sword/shield node, no gear-named material on the body mesh):
//       -> PREFERRED. Bind Paladin_diffuse/normal/specular onto the current body's
//          material (or a Paladin_MAT already referencing them). No baked-gear
//          conflict, no PackageBakedGearMarker needed, equipment/paper-doll props
//          attach normally through EquipmentController. Do NOT swap to the WProp FBX.
//     • HAS BAKED GEAR (sword/shield node or gear material on the body): both bodies
//       carry the problem -> keep PackageBakedGearMarker (Case A/B below).
//
//   Baked-gear handling (applies to whichever body carries the sword/shield):
//
//   CASE A — sword/shield ARE SEPARATE named transforms (own GameObject/renderer):
//     Options:
//       (a) HIDE the baked sword/shield nodes (SetActive(false) / strip in the
//           package-body builder) and let the equipment system attach the real
//           equippable props — full paper-doll fidelity, weapon swapping works.
//       (b) Treat the baked sword/shield AS the hero's default weapon/off-hand and
//           SUPPRESS prop-attach (keep PackageBakedGearMarker as-is).
//     RECOMMEND (a): the whole V1 value is swappable gear driven by GearLoadout /
//       the Gear Preview paper-doll (memory: gear-preview-design). If the nodes are
//       separable we get equippable weapons "for free" — hide the baked ones so the
//       body is a clean rig, then remove PackageBakedGearMarker for this body so the
//       real KayKit/Blink props attach and orient via the proven grip path. Keep the
//       marker as the safety default until hiding is verified to leave no stray mesh.
//
//   CASE B — sword/shield are FUSED into the body skinned mesh (one renderer, the
//     gear lives as submeshes of the body mesh, no separate node):
//     The hero is LOCKED to that sword/shield visually — you cannot hide gear that
//     is part of the body mesh without editing the mesh in a DCC tool.
//       -> Keep PackageBakedGearMarker on this body: EquipmentController already
//          suppresses ALL weapon (Equip) + off-hand (EquipOffHand) + LateAttachRetry
//          prop-attach for a marked body, so no second sword / no 180-degree shield.
//       -> STAT-only equipment still works: GearLoadout tracks the equipped weapon/
//          off-hand and the armor-tint (WO-567) stays active — only the VISIBLE prop
//          is suppressed, stats/loadout are unaffected.
//       -> For swappable WEAPON VISUALS the owner needs a PROP-LESS model variant
//          (a "no-prop" Paladin FBX export) so the equipment system can attach real
//          weapons — that is the owner's "solution" she is working on. Until then the
//          fused sword/shield is the fixed hero silhouette.
// =============================================================================

using System.Text;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>
    /// Read-only probe: dumps the Paladin hero FBX transform hierarchy + renderer
    /// submesh/material names, flagging sword/shield/weapon/prop nodes. Reveals whether
    /// baked gear is separable (own transforms) or fused into the body skinned mesh.
    /// </summary>
    public static class PaladinGearInspector
    {
        // Inspect BOTH package bodies (coordinator 2026-07-03): the KEY question is whether
        // the CURRENT body (Knight_Hero.fbx, textured via 'Paladin_MAT') is prop-LESS — if so
        // the cleanest fix is to bind Paladin_diffuse/normal/specular onto it (no baked-gear
        // problem at all) rather than swapping to the owner's WProp Paladin_Hero.fbx.
        private static readonly string[] FbxPaths =
        {
            "Assets/HeroPackages/Knight/Knight_Hero.fbx",    // CURRENT package body — prop-less?
            "Assets/HeroPackages/Paladin/Paladin_Hero.fbx",  // owner's WProp (sword+shield baked)
            // OWNER 2026-07-03 "try this" (V3 is the real one; V2 was a just-in-case) — the NEW Knight
            // body. Character Creator / AccuRIG export (CC_Base_* skeleton -> STANDARD humanoid, 17
            // tripo_part meshes, one shared 'Material_Pbr' with an embedded diffuse) and EMBEDDED CLIPS
            // (a default WALK + a CUSTOM DANCE). Inspect reveals: does the avatar build Humanoid (CC_Base
            // bones auto-map), does it carry SEPARATE grip/gear nodes (Sword/Shield joints) or is it a
            // bare body, which materials/submeshes it ships, and the EMBEDDED CLIP NAMES (so the dance can
            // be exposed as a hero emote and weapons mapped to its grip joints next).
            "Assets/Resources/Heroes/KnightV3.fbx",          // NEW Knight body (CC/AccuRIG, "try this")
            "Assets/Resources/Heroes/knightV2.fbx",          // V2 just-in-case backup (same rig family)
        };

        [MenuItem("Defenders/Heroes/Inspect Paladin Gear")]
        public static void InspectMenu() => Inspect();

        /// <summary>
        /// Batchmode-callable entry point that inspects ONLY the new KnightV3 body (owner "try this").
        /// Run via <c>DeNelle.Editor.PaladinGearInspector.InspectKnightV3</c>. Logs its full transform
        /// hierarchy (rig bones — expect CC_Base_* Character-Creator naming that Unity humanoid
        /// auto-maps), the imported Avatar's Humanoid validity, every renderer's submesh count + material
        /// names, the EMBEDDED ANIMATION CLIP names (walk / custom dance), and flags any sword/shield/
        /// weapon/grip node so gear attachment (AttachmentOffsetRegistry) can be mapped afterwards.
        /// </summary>
        [MenuItem("Defenders/Heroes/Inspect KnightV3")]
        public static void InspectKnightV3() => InspectOne("Assets/Resources/Heroes/KnightV3.fbx");

        /// <summary>
        /// Batchmode-callable probe for the FIRST AccuRIG ENEMY — the orc berserker (mirrors the
        /// KnightV3 hero pipeline). Run via <c>DeNelle.Editor.PaladinGearInspector.InspectOrcBerserker</c>.
        /// Confirms the enemy body BEFORE retargeting attack anims onto it: dumps the rig-bone hierarchy
        /// (expect AccuRIG Hip/Spine01/L_Upperarm/... standard-humanoid naming that Unity auto-maps),
        /// the imported Avatar's isHuman/isValid (must build Humanoid like KnightV3 for the shared enemy
        /// controllers to retarget), every renderer's submesh + material names (verify the committed
        /// Orc_Berserker.mat with the Orc_Warrior basecolor/normal/metallic is bound — not blank), and
        /// any embedded clip names. Reuses the exact same InspectOne dump the hero pipeline proved out.
        /// </summary>
        [MenuItem("Defenders/Enemies/Inspect Orc Berserker")]
        public static void InspectOrcBerserker() => InspectOne("Assets/Resources/Enemies/Orc_Berserker.fbx");

        /// <summary>
        /// Batchmode-callable entry point. Loads each package FBX and logs its full
        /// hierarchy + renderer submesh/material names, flagging any gear-like node.
        /// </summary>
        public static void Inspect()
        {
            foreach (var path in FbxPaths)
                InspectOne(path);
        }

        private static void InspectOne(string FbxPath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("========================================================================");
            sb.AppendLine("[PaladinGearInspector] Inspecting: " + FbxPath);
            sb.AppendLine("========================================================================");

            var prefab = AssetDatabase.LoadMainAssetAtPath(FbxPath) as GameObject;
            if (prefab == null)
            {
                sb.AppendLine("FAIL: could not load FBX main asset at path (not imported / wrong path?).");
                Debug.LogError(sb.ToString());
                return;
            }

            // Traverse the imported prefab hierarchy directly (no scene instantiation needed —
            // the FBX asset is a full transform tree). Track gear-like flags for a summary.
            int nodeCount = 0, rendererCount = 0, gearNodeCount = 0;
            var gearHits = new StringBuilder();

            WalkHierarchy(prefab.transform, 0, sb, ref nodeCount, ref rendererCount,
                          ref gearNodeCount, gearHits);

            // ── AVATAR + EMBEDDED CLIPS (KnightV3 needs both proven) ──────────────────────────
            // 1) The imported Avatar: is it HUMANOID and valid? A CC/AccuRIG rig should auto-map its
            //    CC_Base_* bones to the Unity humanoid rig — if isHuman is false or the avatar is
            //    invalid the shared hero animations will NOT retarget (flag it LOUD for a rig-mapping fix).
            // 2) Embedded AnimationClips: KnightV3 ships a default WALK + a custom DANCE baked into the
            //    FBX. List every clip sub-asset's NAME + length + humanoid flag so the exact clip names
            //    are known (to expose the dance as an emote and pick the walk where it fits).
            var subAssets = AssetDatabase.LoadAllAssetsAtPath(FbxPath);
            Avatar avatar = null;
            int clipCount = 0;
            var clipLines = new StringBuilder();
            foreach (var a in subAssets)
            {
                if (a is Avatar av) avatar = av;
                else if (a is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    clipCount++;
                    clipLines.AppendLine(
                        $"    - clip '{clip.name}'  length={clip.length:0.00}s  humanoid={clip.isHumanMotion}  " +
                        $"looping={clip.isLooping}  frameRate={clip.frameRate:0}");
                }
            }
            sb.AppendLine("------------------------------------------------------------------------");
            if (avatar != null)
                sb.AppendLine($"[PaladinGearInspector] AVATAR '{avatar.name}': isHuman={avatar.isHuman} isValid={avatar.isValid} " +
                              (avatar.isHuman && avatar.isValid
                                  ? "=> OK: humanoid avatar built — shared hero anims WILL retarget."
                                  : "=> FLAG: NOT a valid humanoid avatar — the shared hero anims will NOT retarget; " +
                                    "check animationType=Humanoid + CC_Base bone auto-map (may need a manual Avatar mapping)."));
            else
                sb.AppendLine("[PaladinGearInspector] AVATAR: NONE found on the asset — animationType is likely Generic/None " +
                              "(set Humanoid so shared hero anims retarget).");
            sb.AppendLine($"[PaladinGearInspector] EMBEDDED CLIPS: {clipCount} found" + (clipCount > 0 ? ":" : " (none imported — check importAnimation + the FBX's baked takes)."));
            if (clipCount > 0) sb.Append(clipLines);

            sb.AppendLine("------------------------------------------------------------------------");
            sb.AppendLine($"[PaladinGearInspector] SUMMARY: nodes={nodeCount} renderers={rendererCount} " +
                          $"gear-flagged-nodes={gearNodeCount}");
            if (gearNodeCount > 0)
            {
                sb.AppendLine("[PaladinGearInspector] GEAR-FLAGGED NODES (candidate SEPARATE sword/shield):");
                sb.Append(gearHits);
                sb.AppendLine("  => VERDICT LEAN: gear appears SEPARABLE (Case A) — nodes named like gear exist.");
                sb.AppendLine("     Confirm each flagged node has its OWN renderer/submesh before deciding.");
            }
            else
            {
                sb.AppendLine("[PaladinGearInspector] NO gear-named nodes found.");
                sb.AppendLine("  => VERDICT LEAN: gear is likely FUSED into the body mesh (Case B) — check the");
                sb.AppendLine("     renderer submesh/material list above; a 'sword'/'shield' MATERIAL on the body");
                sb.AppendLine("     SkinnedMeshRenderer confirms fusion (locked hero silhouette; keep the marker).");
            }
            sb.AppendLine("========================================================================");

            Debug.Log(sb.ToString());
        }

        private static void WalkHierarchy(Transform t, int depth, StringBuilder sb,
            ref int nodeCount, ref int rendererCount, ref int gearNodeCount, StringBuilder gearHits)
        {
            nodeCount++;
            string indent = new string(' ', depth * 2);
            bool gearNode = IsGearName(t.name);
            string flag = gearNode ? "   <<< GEAR-LIKE NODE (sword/shield/weapon/prop)" : "";
            sb.AppendLine($"{indent}[d{depth}] {t.name}{flag}");
            if (gearNode)
            {
                gearNodeCount++;
                gearHits.AppendLine($"    - '{t.name}' (depth {depth})");
            }

            // Log any renderer on THIS node: submesh count + per-material names. A renderer that
            // carries a gear-named material while sitting on the body node = FUSED gear evidence.
            var renderers = t.GetComponents<Renderer>();
            foreach (var r in renderers)
            {
                if (r == null) continue;
                rendererCount++;
                int subMeshCount = -1;
                Mesh mesh = null;
                if (r is SkinnedMeshRenderer smr) mesh = smr.sharedMesh;
                else { var mf = t.GetComponent<MeshFilter>(); if (mf != null) mesh = mf.sharedMesh; }
                if (mesh != null) subMeshCount = mesh.subMeshCount;

                string rType = r.GetType().Name;
                sb.AppendLine($"{indent}    <renderer {rType}> mesh='{(mesh != null ? mesh.name : "<null>")}' " +
                              $"submeshes={subMeshCount}");

                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    string mn = mats[i] != null ? mats[i].name : "<null>";
                    bool gearMat = IsGearName(mn);
                    sb.AppendLine($"{indent}      submesh[{i}] material='{mn}'" +
                                  (gearMat ? "   <<< GEAR-LIKE MATERIAL (possible FUSED sword/shield submesh)" : ""));
                }
            }

            for (int i = 0; i < t.childCount; i++)
                WalkHierarchy(t.GetChild(i), depth + 1, sb, ref nodeCount, ref rendererCount,
                              ref gearNodeCount, gearHits);
        }

        // Keyword flag: does this transform/material name read like weapon/shield/prop gear?
        private static bool IsGearName(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return false;
            string n = raw.ToLowerInvariant();
            return n.Contains("sword") || n.Contains("shield") || n.Contains("weapon") ||
                   n.Contains("prop")  || n.Contains("blade")  || n.Contains("hilt")   ||
                   n.Contains("scabbard") || n.Contains("sheath") || n.Contains("buckler");
        }
    }
}
