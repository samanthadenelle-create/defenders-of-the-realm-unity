// =============================================================================
// EnemyRigColorRegression -- "call up every enemy, prove each is RIGGED + COLORED".
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor (editor-only)   Namespace: DeNelle.Editor
//
// THE JOB: a seconds-fast, no-scene / no-play asset oracle that enumerates EVERY
// enemy the game can spawn (enemies.json -> EnemyCatalog, resolved to a model prefab
// EXACTLY as the single creation path does: EnemyFactory.ModelForEnemy -> Resources.
// Load<GameObject>("Enemies/<model>")) and, per enemy, asserts two axes:
//
//   RIGGED  -- the prefab is an ANIMATED mesh (>=1 SkinnedMeshRenderer, not a static
//              primitive/capsule) AND resolves an animation rig: after the REAL runtime
//              apply (EnemyAnimatorFactory.Apply) the Animator carries a shared
//              runtimeAnimatorController OR a valid Avatar. A static/primitive body with
//              no skinned rig would T-pose or not animate -> FAIL.
//
//   COLORED -- the enemy renders with a real base colour, read from the material's
//              SERIALIZED PROPERTY SHEET (ungated GetTexture/GetColor -- NOT HasProperty).
//              CRITICAL LESSON (commit 7e663981, RESUME_2026-07-08 + memory): under a
//              headless -nographics device SHADERS DO NOT RESOLVE, so Material.HasProperty
//              reads FALSE for everything and a HasProperty-gated audit reports a false
//              "0 textured". The hero white-Paladin audit (HeroBodySwapper.SheetAlbedo /
//              AuditPackageAlbedo, AutoPilotDriver.AssertHeroHasAlbedo) fixed this by
//              reading the OWN serialized sheet, which loads with or without a GPU. This
//              oracle generalises that hero check to the whole roster.
//
// COLOUR AUTHORITY IS PER RIG FAMILY (read from EnemyFactory, not invented) so a
// legitimately-runtime-coloured enemy is NOT false-failed:
//   * OrcHumanoid family (Orc_Warrior/Tank/Mage, WO-482): the FBX ships an UNBOUND
//     _MainTex and is coloured at runtime by binding a per-orc OrcTex basecolor as the
//     TripoMaterialFixer fallback (EnemyFactory.cs L178-190). AUTHORITY = that basecolor
//     Resources texture must EXIST -> assert Resources.Load<Texture>("Enemies/OrcTex/
//     <model>_basecolor") != null (its absence is the real "solid white orc" bug).
//   * OrcWarband family + Troll (EnemyFactory.cs L192-223): NO committed texture (FBX
//     remaps point to deleted tripo_mat_*.mat); coloured at runtime by a HARD-CODED solid
//     SetFallbackTint. Coloured BY CONSTRUCTION -> exempt from the prefab-sheet audit
//     (auditing their intentionally-untextured prefab material would false-fail).
//   * Everyone else (skeleton family, Necromancer/Boss, Demon, Dragon, brutes): the PREFAB
//     material carries the colour (no runtime tint path touches them) -> audit the sheet.
//
// Contract mirrors MonetizationCovenantRegression.Run(out string reason):
//   true  = pass  (reason = "audited N enemies: all rigged + colored ...")
//   false = fail  (reason = compact list naming each enemy + failed axis + renderer/mat)
//
// Orchestrator (DataRegression.RunAll) registers it covenant-style (DataRegression owns
// the registration -- this file does NOT edit it):
//   if (!EnemyRigColorRegression.Run(out var enemyRigColorReason)) failures.Add(enemyRigColorReason); else log.AppendLine("[enemy-rig-color] " + enemyRigColorReason);
//
// Runs in SECONDS. Deterministic. Self-contained. Editor-only asset reads. No scene, no play.
// =============================================================================

using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using DeNelle.Village;

namespace DeNelle.Editor
{
    public static class EnemyRigColorRegression
    {
        public static bool Run(out string reason)
        {
            var failures = new List<string>();   // one entry per (enemy, failed axis)
            var notes    = new List<string>();   // per-family colour authority annotations (pass-side)
            int audited = 0, orcTexColored = 0, warbandTinted = 0, sheetColored = 0;

            // --- load the roster THROUGH the real loader path (mirror CheckEnemies) -----
            string json = DeNelle.Core.CanonicalJson.Read(WaveDataLoader.EnemiesRelativePath);
            EnemyCatalog catalog = null;
            if (!string.IsNullOrEmpty(json))
            {
                try { catalog = JsonConvert.DeserializeObject<EnemyCatalog>(json); }
                catch (System.Exception ex)
                {
                    reason = $"enemies.json failed to parse: {ex.Message} (cannot audit rig/colour).";
                    return false;
                }
            }
            if (catalog == null || catalog.Enemies == null || catalog.Enemies.Count == 0)
            {
                reason = "enemies.json deserialized to 0 EnemyDef objects (mapping break or empty 'enemies') -- nothing to audit.";
                return false;
            }

            foreach (var e in catalog.Enemies)
            {
                // Skip the schema-doc placeholder row (its id is the field description, not a
                // real enemy) -- exactly as CheckEnemies does. Also skip a null/empty id.
                if (e == null) continue;
                if (e.Id != null && e.Id.Contains(" ")) continue;
                if (string.IsNullOrEmpty(e.Id)) continue;

                string id = e.Id;
                string model = EnemyFactory.ModelForEnemy(e);
                string resPath = "Enemies/" + model;

                var prefab = Resources.Load<GameObject>(resPath);
                if (prefab == null)
                {
                    // A null load means this enemy ships as a tinted-capsule fallback at runtime.
                    failures.Add($"'{id}' -> model '{model}': prefab MISSING at Resources/{resPath} (would spawn as a tinted capsule -- not rigged, not the real art)");
                    audited++;
                    continue;
                }

                audited++;
                EnemyRig rig = EnemyAnimatorFactory.RigFor(model);

                // Instantiate far below the world so we can (a) run the REAL runtime animator
                // apply and read the resolved controller/avatar, and (b) read live renderer/
                // material state. Torn down in finally -> no leak, no scene residue.
                GameObject inst = null;
                try
                {
                    inst = Object.Instantiate(prefab);
                    inst.name = "__EnemyRigColorAudit_" + model;
                    inst.transform.position = new Vector3(0f, -10000f, 0f);

                    // ---------- RIGGED ----------
                    var skinned = inst.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                    bool hasSkinned = skinned != null && skinned.Length > 0;

                    // Drive the SINGLE runtime rig path so we assert exactly what the game does:
                    // EnemyAnimatorFactory.Apply picks the shared controller by rig family and
                    // stamps it on the Animator (adding one if absent).
                    EnemyAnimatorFactory.Apply(inst, model);
                    var anim = inst.GetComponentInChildren<Animator>(true);
                    bool ctrlResolved = anim != null && anim.runtimeAnimatorController != null;
                    bool hasAvatar    = anim != null && anim.avatar != null;

                    if (!hasSkinned)
                    {
                        failures.Add($"'{id}' -> '{model}': NOT RIGGED -- no SkinnedMeshRenderer (static/primitive body; would T-pose or not animate)");
                    }
                    else if (!ctrlResolved && !hasAvatar)
                    {
                        // Both the shared controller AND the imported Avatar are absent: nothing
                        // can drive the mesh -> it stands in bind pose / slides.
                        failures.Add($"'{id}' -> '{model}' (rig {rig}): NOT RIGGED -- SkinnedMeshRenderer present but no runtimeAnimatorController AND no Avatar resolved (would stand in bind pose)");
                    }
                    else if (!ctrlResolved)
                    {
                        // Passes the OR contract (a valid Avatar is a rig) but the shared controller
                        // did not load -> it would idle with no walk/attack clip. Name the gap loudly
                        // rather than fail (the controller set may not be built in every tree).
                        notes.Add($"'{id}'->'{model}' rigged via Avatar but shared controller 'Enemies/*' ({rig}) did NOT resolve (would idle -- run EnemyAnimatorSetup)");
                    }

                    // ---------- COLORED (per-family authority) ----------
                    if (rig == EnemyRig.OrcHumanoid &&
                        (model == "Orc_Warrior" || model == "Orc_Tank" || model == "Orc_Mage"))
                    {
                        // Coloured at runtime by binding the per-orc OrcTex basecolor as the Tripo
                        // fixer fallback (EnemyFactory L178-190). AUTHORITY = that texture must exist.
                        string texPath = "Enemies/OrcTex/" + model + "_basecolor";
                        var tex = Resources.Load<Texture>(texPath);
                        if (tex == null)
                            failures.Add($"'{id}' -> '{model}': UNCOLORED -- OrcTex basecolor Resources/{texPath} MISSING (the FBX ships an unbound _MainTex -> would render solid WHITE)");
                        else
                            orcTexColored++;
                    }
                    else if (rig == EnemyRig.OrcWarband || model == "Troll")
                    {
                        // Coloured by construction: EnemyFactory binds a HARD-CODED solid
                        // SetFallbackTint for this family (no committed texture -- deleted tripo
                        // remaps). Auditing the intentionally-untextured prefab sheet would
                        // false-fail, so this family is EXEMPT from the sheet audit and passes.
                        warbandTinted++;
                        notes.Add($"'{id}'->'{model}' ({rig}) coloured via runtime SetFallbackTint (exempt from prefab-sheet audit by design)");
                    }
                    else
                    {
                        // The prefab material carries the colour (no runtime tint path touches this
                        // family). Audit every renderer's serialized sheet.
                        AuditColorSheet(inst, id, model, rig, failures, ref sheetColored);
                    }
                }
                finally
                {
                    if (inst != null) Object.DestroyImmediate(inst);
                }
            }

            if (failures.Count > 0)
            {
                var sb = new StringBuilder();
                sb.Append($"enemy rig/colour audit FAILED ({failures.Count} finding(s) across {audited} enemy(ies)): ");
                sb.Append(string.Join(" | ", failures));
                reason = sb.ToString();
                return false;
            }

            var ok = new StringBuilder();
            ok.Append($"audited {audited} enemies: all rigged + colored");
            ok.Append($" (colour authority: {sheetColored} prefab-sheet textured/tinted, {warbandTinted} OrcWarband/Troll runtime tint, {orcTexColored} OrcHumanoid OrcTex basecolor)");
            if (notes.Count > 0)
                ok.Append(". Notes: " + string.Join("; ", notes));
            reason = ok.ToString();
            return true;
        }

        // Audit every surface renderer's SHARED materials via the serialized sheet (never
        // HasProperty -- see header). A slot is COLORED if it binds a base texture
        // (_BaseMap/_MainTex) OR carries a deliberate non-white base colour (_BaseColor/_Color).
        private static void AuditColorSheet(GameObject inst, string id, string model, EnemyRig rig,
                                            List<string> failures, ref int sheetColored)
        {
            var renderers = inst.GetComponentsInChildren<Renderer>(true);
            int slotsChecked = 0, slotsColored = 0;

            foreach (var r in renderers)
            {
                if (r == null) continue;
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;

                var mats = r.sharedMaterials;
                string rpath = PathOf(r.transform, inst.transform);

                if (mats == null || mats.Length == 0)
                {
                    failures.Add($"'{id}' -> '{model}' renderer '{rpath}': NO materials (renders magenta/untextured)");
                    continue;
                }

                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    string slot = mats.Length > 1 ? $"[slot {i}]" : "";
                    slotsChecked++;

                    if (m == null)
                    {
                        failures.Add($"'{id}' -> '{model}' renderer '{rpath}'{slot}: sharedMaterial NULL (URP renders MAGENTA)");
                        continue;
                    }
                    string matName = m.name ?? "<unnamed>";
                    var shader = m.shader;
                    string shaderName = shader != null ? shader.name : "<null shader>";

                    if (shader == null || shaderName.Contains("InternalErrorShader") || shaderName.Contains("Hidden/InternalError"))
                    {
                        failures.Add($"'{id}' -> '{model}' renderer '{rpath}'{slot}: material '{matName}' has a broken/error shader ({shaderName}) -> magenta");
                        continue;
                    }
                    if (matName == "Default-Material" || matName == "Default-Diffuse")
                    {
                        failures.Add($"'{id}' -> '{model}' renderer '{rpath}'{slot}: builtin '{matName}' (untextured default surface -- authored art missing)");
                        continue;
                    }

                    // Serialized-sheet reads -- deliberately NOT HasProperty-gated (dead in -nographics).
                    Texture baseTex = SheetBaseTex(m);
                    bool tinted = IsSheetTinted(m, "_BaseColor") || IsSheetTinted(m, "_Color");
                    if (baseTex == null && !tinted)
                    {
                        failures.Add($"'{id}' -> '{model}' renderer '{rpath}'{slot}: material '{matName}' ({shaderName}) is UNCOLORED -- no _BaseMap/_MainTex texture and no non-default base colour (would render white/magenta)");
                        continue;
                    }
                    slotsColored++;
                }
            }

            // If the whole body had at least one coloured slot and no failures were added for it,
            // count it as a sheet-coloured enemy for the pass summary.
            if (slotsColored > 0) sheetColored++;
            else if (slotsChecked == 0)
                failures.Add($"'{id}' -> '{model}': NO surface renderers/materials to colour (empty body -- would be invisible)");
        }

        // Sheet base texture: ungated _BaseMap then _MainTex (mirrors HeroBodySwapper.SheetAlbedo).
        private static Texture SheetBaseTex(Material m)
        {
            if (m == null) return null;
            Texture t = m.GetTexture("_BaseMap");
            if (t == null) t = m.GetTexture("_MainTex");
            return t;
        }

        // Sheet tint probe (mirrors HeroBodySwapper.IsSheetTinted): a non-white colour with any
        // alpha is a deliberate tint. An absent sheet entry reads back clear (a=0), so an
        // untinted material can never masquerade as tinted.
        private static bool IsSheetTinted(Material m, string prop)
        {
            if (m == null) return false;
            Color c = m.GetColor(prop);
            return c.a > 0f && c != Color.white;
        }

        private static string PathOf(Transform t, Transform root)
        {
            var stack = new List<string>();
            var cur = t;
            while (cur != null && cur != root)
            {
                stack.Add(cur.name);
                cur = cur.parent;
            }
            stack.Reverse();
            return stack.Count == 0 ? "<root>" : string.Join("/", stack);
        }
    }
}
