// =============================================================================
// MeshBudgetReport -- READ-ONLY diagnostic (WO-1291 lane, owner question
// 2026-09-01: "the ones they are pointing to now, are they small and
// lightweight?").
// -----------------------------------------------------------------------------
// Answers with CAPTURED DATA instead of file-size guesswork (CLAUDE.md sec 12):
// for every Synty wrapper prefab under Assets/StructureContent/Synty/ AND every
// legacy Tripo model at Assets/StructureContent root, walk all MeshFilter +
// SkinnedMeshRenderer sharedMeshes and sum vertices / triangles / unique
// sharedMaterials. Changes NOTHING -- no importer writes, no SaveAssets.
//
// Batchmode entry point:
//   DeNelle.Editor.MeshBudgetReport.Run
// Output: Builds/mesh-budget-report.txt + the same table in the log.
// Marker:  MESH_BUDGET_OK counted=<n>   (judge on the marker, not exit code)
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class MeshBudgetReport
    {
        private const string ReportRelPath = "Builds/mesh-budget-report.txt";
        private static readonly string StructureRoot = DeNelle.Core.AssetRoots.StructureContent;
        private static readonly string SyntyRoot = StructureRoot + "/Synty";

        public static void Run()
        {
            var sb = new StringBuilder();
            int counted = 0;

            sb.AppendLine("MESH BUDGET REPORT  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            sb.AppendLine("prefab/model | verts | tris | uniqueMats | renderers");
            sb.AppendLine("---- NEW: Synty wrappers (Assets/StructureContent/Synty) ----");
            counted += Measure(SyntyRoot, "t:Prefab", sb);

            sb.AppendLine("---- OLD: legacy models at StructureContent root ----");
            counted += MeasureRootModels(sb);

            var text = sb.ToString();
            Debug.Log(text);
            Directory.CreateDirectory(Path.GetDirectoryName(ReportRelPath));
            File.WriteAllText(ReportRelPath, text);
            // No EditorApplication.Exit here: run-unity-method.ps1 passes -quit,
            // and a second Exit during domain-reload teardown throws an NRE that
            // fails the wrapper's LOG_SCAN even though the marker printed.
            Debug.Log("MESH_BUDGET_OK counted=" + counted);
        }

        private static int Measure(string folder, string filter, StringBuilder sb)
        {
            int n = 0;
            foreach (var guid in AssetDatabase.FindAssets(filter, new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) { sb.AppendLine(path + " | LOAD FAILED"); continue; }
                AppendStats(go, Path.GetFileNameWithoutExtension(path), sb);
                n++;
            }
            return n;
        }

        private static int MeasureRootModels(StringBuilder sb)
        {
            int n = 0;
            // Root-level FBX only (the old Tripo art) -- not subfolders.
            foreach (var file in Directory.GetFiles(StructureRoot, "*.fbx", SearchOption.TopDirectoryOnly)
                                          .OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                var path = file.Replace('\\', '/');
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (go == null) { sb.AppendLine(path + " | LOAD FAILED"); continue; }
                AppendStats(go, Path.GetFileNameWithoutExtension(path), sb);
                n++;
            }
            return n;
        }

        private static void AppendStats(GameObject go, string label, StringBuilder sb)
        {
            long verts = 0, tris = 0;
            int renderers = 0;
            var mats = new HashSet<Material>();

            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                verts += mf.sharedMesh.vertexCount;
                tris += CountTris(mf.sharedMesh);
                renderers++;
                var r = mf.GetComponent<MeshRenderer>();
                if (r != null) foreach (var m in r.sharedMaterials) if (m != null) mats.Add(m);
            }
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.sharedMesh == null) continue;
                verts += smr.sharedMesh.vertexCount;
                tris += CountTris(smr.sharedMesh);
                renderers++;
                foreach (var m in smr.sharedMaterials) if (m != null) mats.Add(m);
            }

            sb.AppendLine(string.Format("{0} | {1} | {2} | {3} | {4}", label, verts, tris, mats.Count, renderers));
        }

        private static long CountTris(Mesh mesh)
        {
            long t = 0;
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                // GetIndexCount works without CPU-readable vertex data.
                t += (long)mesh.GetIndexCount(i) / 3;
            }
            return t;
        }
    }
}
