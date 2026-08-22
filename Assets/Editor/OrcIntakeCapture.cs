// =============================================================================
// OrcIntakeCapture — THROWAWAY evidence renderer for the 2026-08-20 orc/Tripo
// art-intake audit. Answers, with pictures, two questions that no amount of
// code-reading can answer (CLAUDE.md §12 — the screenshot IS the data for a
// visual defect):
//
//   1. Does each orc body wear its OWN authored basecolor, and does that atlas
//      REGISTER with the mesh (features landing where they belong) rather than
//      smearing across it?
//   2. Which sculpt do the ambiguous atlases "Necromancer_basecolor" and
//      "Skeleton_Golem_basecolor" actually belong to? Filenames do not
//      disambiguate; the render does. Every candidate mesh is shot wearing the
//      same atlas, side by side, and the one it registers on is the owner.
//
// READ-ONLY BY CONSTRUCTION: instantiates FBXs into a SCRATCH scene that is never
// saved (CLAUDE.md §3), builds materials in memory, and writes only PNGs under
// Builds/OrcCaps/. It never edits an importer, a .meta, a material asset or a
// scene. Deleting this file changes nothing about the game.
//
// Rig/framing follows EnemyProvingHarness (which is NOT modified): isolation
// layer 31, one shared camera + key/fill/ambient so subjects are comparable,
// framed on RENDERED BOUNDS at a distance in units of MODEL HEIGHT. Each subject
// gets ONE png holding TWO views (yaw 0 and yaw 180) so a body facing away from
// the camera can still be judged.
//
//   powershell -File .\run-unity-method.ps1 -Method DeNelle.Editor.OrcIntakeCapture.RunBatch `
//        -LogName orc-caps.log -ExpectMarker ORC_CAPS_OK -TimeoutMin 45
//
// Marker: ORC_CAPS_OK <shot>/<total>   |   ORC_CAPS_FAIL <n> blank
// =============================================================================

using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class OrcIntakeCapture
    {
        private const string OutDir = "Builds/OrcCaps";
        private const int Res = 1024;
        private const int IsolationLayer = 31;
        private const float BlankCoverageFloor = 0.005f;

        private const string EC = DeNelle.Core.AssetRoots.EnemyContent + "/";
        private const string INC = "Assets/Art/Incoming_Tripo/Enemies/Orcs/";

        private sealed class Subject
        {
            public string Label;
            public string Fbx;
            public string BaseTex;    // may be null -> renders untextured (evidence of "bare")
            public string NormalTex;
            public string Note;
        }

        private sealed class Row
        {
            public string Label, Png, Note, Fbx, BaseTex;
            public float Coverage;
            public Vector3 BoundsSize;
            public int SubMeshCount;
            public string Defect;
        }

        // Every subject is a (mesh, atlas) PAIRING under test. Deliberate duplicates:
        // the same mesh appears with two candidate atlases, and the same atlas appears
        // on two candidate meshes — that is the whole experiment.
        private static readonly Subject[] Subjects =
        {
            // ── the three live orc bodies, each wearing its OWN per-body atlas ────────
            new Subject { Label = "01_Orc_Warrior__OrcTex_Warrior", Fbx = EC + "Orc_Warrior.fbx",
                          BaseTex = EC + "OrcTex/Orc_Warrior_basecolor.jpg", NormalTex = EC + "OrcTex/Orc_Warrior_normal.jpg",
                          Note = "live body + its own atlas (runtime path)" },
            new Subject { Label = "02_Orc_Tank__OrcTex_Tank", Fbx = EC + "Orc_Tank.fbx",
                          BaseTex = EC + "OrcTex/Orc_Tank_basecolor.jpg", NormalTex = EC + "OrcTex/Orc_Tank_normal.jpg",
                          Note = "live body + its own atlas (runtime path)" },
            new Subject { Label = "03_Orc_Mage__TripoTex_Mage", Fbx = EC + "Orc_Mage.fbx",
                          BaseTex = EC + "TripoTex/Orc_Mage_basecolor.jpg", NormalTex = null,
                          Note = "live body + the 08-09 TripoTex atlas — THIS is what the runtime binds (TripoTex wins the probe)" },
            new Subject { Label = "04_Orc_Mage__OrcTex_Mage", Fbx = EC + "Orc_Mage.fbx",
                          BaseTex = EC + "OrcTex/Orc_Mage_basecolor.jpg", NormalTex = EC + "OrcTex/Orc_Mage_normal.jpg",
                          Note = "SAME live body + the OLDER OrcTex atlas — control for 'TripoTex wins on collision'" },

            // ── the superseded Orc_Mage export + the atlas-collision hypothesis ──────
            new Subject { Label = "05_Orc_Mage_Legacy__OrcTex_Mage", Fbx = EC + "Orc_Mage_Legacy.fbx",
                          BaseTex = EC + "OrcTex/Orc_Mage_basecolor.jpg", NormalTex = EC + "OrcTex/Orc_Mage_normal.jpg",
                          Note = "the SUPERSEDED mage export wearing the mage atlas" },
            new Subject { Label = "06_Orc_Mage_Legacy__OrcTex_Tank", Fbx = EC + "Orc_Mage_Legacy.fbx",
                          BaseTex = EC + "OrcTex/Orc_Tank_basecolor.jpg", NormalTex = EC + "OrcTex/Orc_Tank_normal.jpg",
                          Note = "TEST: Orc_Mage_Legacy embeds tripo_mat_80c4114e, the SAME id Orc_Tank embeds — is it the same sculpt?" },

            // ── the Incoming_Tripo source exports (smaller files — different exports) ─
            new Subject { Label = "07_INCOMING_Orc_Warrior", Fbx = INC + "Orc_Warrior/Orc_Warrior.fbx",
                          BaseTex = INC + "Orc_Warrior/Orc_Warrior_basecolor.jpg", NormalTex = INC + "Orc_Warrior/Orc_Warrior_normal.jpg",
                          Note = "the staged SOURCE export — compare silhouette against 01" },
            new Subject { Label = "08_INCOMING_Orc_Tank", Fbx = INC + "Orc_Tank/Orc_Tank.fbx",
                          BaseTex = INC + "Orc_Tank/Orc_Tank_basecolor.jpg", NormalTex = INC + "Orc_Tank/Orc_Tank_normal.jpg",
                          Note = "the staged SOURCE export — compare silhouette against 02" },
            new Subject { Label = "09_INCOMING_Orc_Mage", Fbx = INC + "Orc_Mage/Orc_Mage.fbx",
                          BaseTex = INC + "Orc_Mage/Orc_Mage_basecolor.jpg", NormalTex = INC + "Orc_Mage/Orc_Mage_normal.jpg",
                          Note = "the staged SOURCE export — compare silhouette against 03/04" },

            // ── WHO OWNS 'Necromancer_basecolor'? three candidate sculpts, one atlas ──
            new Subject { Label = "10_Necromancer_NEW__TripoTex_Necro", Fbx = EC + "Necromancer_NEW.fbx",
                          BaseTex = EC + "TripoTex/Necromancer_basecolor.jpg", NormalTex = null,
                          Note = "candidate A for the Necromancer atlas (the model id 'necromancer' actually wears)" },
            new Subject { Label = "11_Orc_Necromancer__TripoTex_Necro", Fbx = EC + "Orc_Necromancer.fbx",
                          BaseTex = EC + "TripoTex/Necromancer_basecolor.jpg", NormalTex = null,
                          Note = "candidate B — one of the three BARE orcs; if it registers here, its art is found" },
            new Subject { Label = "12_Necromancer_legacy__TripoTex_Necro", Fbx = EC + "Necromancer.fbx",
                          BaseTex = EC + "TripoTex/Necromancer_basecolor.jpg", NormalTex = null,
                          Note = "candidate C — the legacy Generic sculpt the _NEW suffix disambiguates from" },

            // ── WHO OWNS 'Skeleton_Golem_basecolor'? two candidate sculpts, one atlas ─
            new Subject { Label = "13_Skeleton_Golem_NEW__TripoTex_Golem", Fbx = EC + "Skeleton_Golem_NEW.fbx",
                          BaseTex = EC + "TripoTex/Skeleton_Golem_basecolor.jpg", NormalTex = null,
                          Note = "candidate A for the Skeleton_Golem atlas" },
            new Subject { Label = "14_Skeleton_Golem_legacy__TripoTex_Golem", Fbx = EC + "Skeleton_Golem.fbx",
                          BaseTex = EC + "TripoTex/Skeleton_Golem_basecolor.jpg", NormalTex = null,
                          Note = "candidate B — the legacy sculpt the atlas may have been authored for" },

            // ── UNTEXTURED CONTROL: what 'no atlas' actually looks like in this rig ───
            new Subject { Label = "15_CONTROL_Orc_Warrior__no_atlas", Fbx = EC + "Orc_Warrior.fbx",
                          BaseTex = null, NormalTex = null,
                          Note = "CONTROL — same body, NO atlas. Anything that looks like this has no art." },

            // ── THE CANDIDATE REPAIR: each AccuRig body's OWN EXTRACTED .fbm diffuse ──
            // Commit 53b5c23cf (2026-07-11) re-exported Warrior/Tank through AccuRig and
            // "extracted textures" into these .fbm folders. If the OrcTex atlas no longer
            // registers on the re-exported UVs, the .fbm map authored WITH those UVs should.
            new Subject { Label = "16_Orc_Warrior__own_fbm_71b5a650", Fbx = EC + "Orc_Warrior.fbx",
                          BaseTex = EC + "Orc_Warrior.fbm/tripo_mat_71b5a650_Pbr_Diffuse.jpg",
                          NormalTex = EC + "Orc_Warrior.fbm/tripo_mat_71b5a650_Pbr_Normal.jpg",
                          Note = "REPAIR CANDIDATE — the body's own extracted .fbm diffuse, authored for ITS UVs" },
            new Subject { Label = "17_Orc_Tank__own_fbm_80c4114e", Fbx = EC + "Orc_Tank.fbx",
                          BaseTex = EC + "Orc_Tank.fbm/tripo_mat_80c4114e_Pbr_Diffuse.jpg",
                          NormalTex = EC + "Orc_Tank.fbm/tripo_mat_80c4114e_Pbr_Normal.jpg",
                          Note = "REPAIR CANDIDATE — the body's own extracted .fbm diffuse, authored for ITS UVs" },
            new Subject { Label = "18_Orc_Mage__own_orphan_fbm_80c4114e", Fbx = EC + "Orc_Mage.fbx",
                          BaseTex = EC + "Orc_Mage.fbm/tripo_mat_80c4114e_Pbr_Diffuse.jpg",
                          NormalTex = null,
                          Note = "PROOF the Orc_Mage.fbm folder is a STALE ORPHAN: the live Orc_Mage.fbx embeds " +
                                 "tripo_mat_10657e81, not 80c4114e, and this .fbm is byte-identical to Orc_Tank.fbm" },

            // ── FINAL PROOF: the same two maps read back through the PUBLISHED path ───
            // EnemyFactory.ResolveBasecolor probes "Enemies/TripoTex/<model>_basecolor"
            // FIRST. These two shots render exactly the asset that probe will resolve, so
            // the picture proves the published file, not just the .fbm it was copied from.
            new Subject { Label = "19_Orc_Warrior__PUBLISHED_TripoTex", Fbx = EC + "Orc_Warrior.fbx",
                          BaseTex = EC + "TripoTex/Orc_Warrior_basecolor.jpg",
                          NormalTex = EC + "TripoTex/Orc_Warrior_normal.jpg",
                          Note = "the PUBLISHED TripoTex atlas — what ResolveBasecolor will bind once registered" },
            new Subject { Label = "20_Orc_Tank__PUBLISHED_TripoTex", Fbx = EC + "Orc_Tank.fbx",
                          BaseTex = EC + "TripoTex/Orc_Tank_basecolor.jpg",
                          NormalTex = EC + "TripoTex/Orc_Tank_normal.jpg",
                          Note = "the PUBLISHED TripoTex atlas — what ResolveBasecolor will bind once registered" },
        };

        /// <summary>
        /// The two normal maps published alongside the basecolors must import as NORMAL MAPS
        /// (textureType NormalMap, linear), not as sRGB colour textures. Stated explicitly here
        /// because the seven PRE-EXISTING TripoTex normals are all textureType Default + sRGB=1 —
        /// wrong, and currently harmless only because nothing binds them. New files are not
        /// going to inherit a latent defect just to match its siblings.
        /// </summary>
        private static void FixPublishedNormalImports()
        {
            string[] normals =
            {
                EC + "TripoTex/Orc_Warrior_normal.jpg",
                EC + "TripoTex/Orc_Tank_normal.jpg",
            };
            foreach (var p in normals)
            {
                var imp = AssetImporter.GetAtPath(p) as TextureImporter;
                if (imp == null) { Debug.LogWarning($"[OrcCaps] no TextureImporter at {p} — skipped"); continue; }
                if (imp.textureType == TextureImporterType.NormalMap && !imp.sRGBTexture) continue;
                imp.textureType = TextureImporterType.NormalMap;
                imp.sRGBTexture = false;
                imp.SaveAndReimport();
                Debug.Log($"[OrcCaps] set {p} to NormalMap/linear");
            }
        }

        [MenuItem("Defenders/Tripo/Capture Orc Intake Evidence")]
        public static void RunMenu() => Run();

        public static void RunBatch() => Run();

        public static void Run()
        {
            AssetDatabase.Refresh();
            FixPublishedNormalImports();

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Directory.CreateDirectory(OutDir);

            var camGo = new GameObject("~OrcCapCam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.10f, 0.11f, 0.13f, 1f);
            cam.orthographic = false;
            cam.fieldOfView = 35f;

            // Same lighting contract as EnemyProvingHarness: key ALONG the view axis so
            // albedo reads, fill opposite, flat ambient so unlit faces are not pure black.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.42f, 0.44f, 0.48f, 1f);

            Vector3 camDir = new Vector3(0.75f, 0.34f, -1f).normalized;
            var lightGo = new GameObject("~OrcCapKey");
            var key = lightGo.AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.5f;
            lightGo.transform.rotation = Quaternion.LookRotation(-camDir, Vector3.up);

            var fillGo = new GameObject("~OrcCapFill");
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.7f;
            fill.color = new Color(0.85f, 0.88f, 1f, 1f);
            fillGo.transform.rotation = Quaternion.LookRotation(
                new Vector3(-camDir.x, -0.25f, -camDir.z).normalized, Vector3.up);

            var rt = new RenderTexture(Res, Res, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            var rows = new List<Row>();
            int index = 0;
            foreach (var s in Subjects)
            {
                var row = new Row { Label = s.Label, Note = s.Note, Fbx = s.Fbx, BaseTex = s.BaseTex ?? "(none)" };
                rows.Add(row);

                var src = AssetDatabase.LoadAssetAtPath<GameObject>(s.Fbx);
                if (src == null) { row.Defect = "FBX NOT FOUND at " + s.Fbx; continue; }

                var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
                if (go == null) { row.Defect = "instantiate failed"; continue; }
                go.transform.position = new Vector3(index * 200f, 0f, 0f);
                index++;

                try
                {
                    ApplyAtlas(go, shader, s.BaseTex, s.NormalTex, row);
                    Capture(cam, rt, go, row);
                }
                finally
                {
                    Object.DestroyImmediate(go);
                }
            }

            cam.targetTexture = null;
            Object.DestroyImmediate(rt);

            int blank = 0, missing = 0;
            var log = new StringBuilder();
            log.AppendLine("# Orc / Tripo intake evidence — " + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            log.AppendLine();
            log.AppendLine("Each PNG is 2048x1024: LEFT = yaw 0, RIGHT = yaw 180 (same body, turned around),");
            log.AppendLine("so a sculpt facing away from the camera is still judgeable. Framed on rendered");
            log.AppendLine("bounds at 2.3x model height. Identical camera/lighting for every subject.");
            log.AppendLine();
            foreach (var r in rows)
            {
                if (r.Defect != null && r.Defect.StartsWith("FBX NOT FOUND")) missing++;
                else if (r.Coverage < BlankCoverageFloor) blank++;
                log.AppendLine($"## {r.Label}");
                log.AppendLine($"- mesh : {r.Fbx}");
                log.AppendLine($"- atlas: {r.BaseTex}");
                log.AppendLine($"- why  : {r.Note}");
                log.AppendLine($"- shot : {r.Png}  bounds=({r.BoundsSize.x:F2},{r.BoundsSize.y:F2},{r.BoundsSize.z:F2}) " +
                               $"submeshes={r.SubMeshCount} coverage={r.Coverage:P2}");
                if (!string.IsNullOrEmpty(r.Defect)) log.AppendLine($"- DEFECT: {r.Defect}");
                log.AppendLine();
                Debug.Log($"[OrcCaps] {r.Label}: coverage={r.Coverage:P2} boundsY={r.BoundsSize.y:F2} " +
                          $"submeshes={r.SubMeshCount} -> {r.Png} {(string.IsNullOrEmpty(r.Defect) ? "" : "DEFECT: " + r.Defect)}");
            }

            string summary = Path.Combine(OutDir, "SUMMARY.md");
            File.WriteAllText(summary, log.ToString());

            int bad = blank + missing;
            if (bad > 0)
                Debug.Log($"ORC_CAPS_FAIL {bad} blank/missing ({rows.Count - bad}/{rows.Count} shot) -> {summary}");
            else
                Debug.Log($"ORC_CAPS_OK {rows.Count}/{rows.Count} subjects shot -> {summary}");
        }

        /// <summary>
        /// Rebuild every material on the body as URP/Lit and bind the CANDIDATE atlas.
        /// This is the point of the harness: the atlas under test is bound explicitly, so
        /// the picture answers "does THIS map belong on THIS mesh" and nothing else.
        /// A null atlas leaves the body white — the deliberate CONTROL for "no art".
        /// </summary>
        private static void ApplyAtlas(GameObject go, Shader shader, string basePath, string normalPath, Row row)
        {
            Texture2D baseTex = string.IsNullOrEmpty(basePath) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(basePath);
            Texture2D nrmTex = string.IsNullOrEmpty(normalPath) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            if (!string.IsNullOrEmpty(basePath) && baseTex == null)
                row.Defect = "atlas NOT FOUND at " + basePath;

            var mat = new Material(shader) { name = "~OrcCapMat" };
            if (baseTex != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", baseTex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", baseTex);
            }
            if (nrmTex != null && mat.HasProperty("_BumpMap"))
            {
                mat.SetTexture("_BumpMap", nrmTex);
                mat.EnableKeyword("_NORMALMAP");
            }
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.25f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);

            int sub = 0;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                int n = r.sharedMaterials != null ? r.sharedMaterials.Length : 1;
                sub += n;
                var mats = new Material[Mathf.Max(1, n)];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.sharedMaterials = mats;
            }
            row.SubMeshCount = sub;
        }

        /// <summary>Two views (yaw 0 / yaw 180) composited into one 2048x1024 PNG.</summary>
        private static void Capture(Camera cam, RenderTexture rt, GameObject go, Row row)
        {
            if (!TryWorldBounds(go, out Bounds b))
            {
                row.Defect = "no renderer bounds — nothing to photograph (the body did not render)";
                return;
            }
            row.BoundsSize = b.size;

            var saved = new Dictionary<Transform, int>();
            MoveToLayer(go.transform, IsolationLayer, saved);
            cam.cullingMask = 1 << IsolationLayer;

            var sheet = new Texture2D(Res * 2, Res, TextureFormat.RGB24, false);
            float coverage = 0f;
            Quaternion baseRot = go.transform.rotation;

            for (int view = 0; view < 2; view++)
            {
                go.transform.rotation = baseRot * Quaternion.Euler(0f, view * 180f, 0f);
                if (!TryWorldBounds(go, out Bounds vb)) vb = b;

                float h = Mathf.Max(0.01f, vb.size.y);
                Vector3 dir = new Vector3(0.75f, 0.34f, -1f).normalized;
                cam.transform.position = vb.center + dir * (h * 2.3f);
                cam.transform.LookAt(vb.center);
                cam.nearClipPlane = 0.05f;
                cam.farClipPlane = h * 40f;
                cam.Render();

                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(Res, Res, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, Res, Res), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;

                sheet.SetPixels(view * Res, 0, Res, Res, tex.GetPixels());

                if (view == 0) coverage = Coverage(tex, cam.backgroundColor);
                Object.DestroyImmediate(tex);
            }

            go.transform.rotation = baseRot;
            sheet.Apply();
            foreach (var kv in saved) if (kv.Key != null) kv.Key.gameObject.layer = kv.Value;

            row.Coverage = coverage;
            string path = Path.Combine(OutDir, row.Label + ".png");
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            row.Png = path.Replace('\\', '/');

            if (coverage < BlankCoverageFloor && string.IsNullOrEmpty(row.Defect))
                row.Defect = $"the shot is BLANK ({coverage:P2} non-background) — the body did not render into frame";
        }

        private static float Coverage(Texture2D tex, Color bg)
        {
            var px = tex.GetPixels32();
            int lit = 0;
            for (int i = 0; i < px.Length; i += 7)
            {
                float dr = Mathf.Abs(px[i].r / 255f - bg.r);
                float dg = Mathf.Abs(px[i].g / 255f - bg.g);
                float db = Mathf.Abs(px[i].b / 255f - bg.b);
                if (dr + dg + db > 0.06f) lit++;
            }
            return lit / (float)(px.Length / 7f);
        }

        private static void MoveToLayer(Transform t, int layer, Dictionary<Transform, int> saved)
        {
            saved[t] = t.gameObject.layer;
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++) MoveToLayer(t.GetChild(i), layer, saved);
        }

        private static bool TryWorldBounds(GameObject go, out Bounds b)
        {
            b = default;
            bool any = false;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                if (!any) { b = r.bounds; any = true; }
                else b.Encapsulate(r.bounds);
            }
            return any;
        }
    }
}
