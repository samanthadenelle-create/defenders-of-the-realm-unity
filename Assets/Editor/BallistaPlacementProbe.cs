// =============================================================================
// BallistaPlacementProbe (WO-1157) - run the REAL placement path for a structure
// row and print the transform at EVERY stage, beside an upright CONTROL row.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor.  Marker: BALLISTA_PROBE_OK.  Editor-only, no PlayMode.
//
//   .\run-unity-method.ps1 -Method DeNelle.Editor.BallistaPlacementProbe.Run `
//       -LogName ballista-probe.log -ExpectMarker BALLISTA_PROBE_OK
//
// =============================================================================
// WHY THIS EXISTS (owner F8 2026-08-24: "the ballista builds on its side")
// =============================================================================
// The orientation threads on this project keep being argued from STATIC READS -
// the catalog says X, the importer flag says Y, therefore the model must be Z -
// and they keep being wrong (three theories on the L3 archer tower, two more on
// 2026-08-23 whose stated remedy would have tipped five CORRECT buildings over).
// CLAUDE.md section 12 is explicit: static reading LOCATES candidates and never
// concludes. So this probe does not reason about the pipeline; it RUNS it.
//
// ONE THING MAKES IT DIFFERENT FROM StructureOrientationOracle, and it is the
// whole reason it is a separate file rather than another assert in there: the
// oracle REPLAYS the pipeline (it re-applies the euler and re-fits by hand, so it
// can measure without building anything). This calls StructureFactory.Create -
// the actual function the game calls when a player places a building - so
// anything Create does that a replay does not model shows up here and nowhere
// else. A replay can only ever confirm the model of the pipeline that was written
// into it; this confirms the pipeline.
//
// ⭐ THE CONTROL ROW IS NOT OPTIONAL. A single measurement of a suspect asset has
// no scale: "aspect 0.70" means nothing until an upright building measured
// through the identical path prints its own number next to it. Both earlier wrong
// theories came from reading one number alone. Every run prints the controls.
//
// It asserts NOTHING and fails NOTHING - it is an instrument, not a gate. The
// gate is StructureOrientationOracle; the render is StructurePoseCapture. This is
// the thing that tells you WHICH STAGE moved the model, which neither of those can.
//
// ASCII-only. Judge by the MARKER on a fresh log, never the exit code (CLAUDE.md 8).
// =============================================================================

using System;
using System.Collections.Generic;
using System.Text;
using DeNelle.Core;
using DeNelle.Core.Catalog;
using DeNelle.Core.Diagnostics;
using DeNelle.Village;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DeNelle.Editor
{
    /// <summary>WO-1157 - step-in/step-out instrument over the real structure placement path.</summary>
    public static class BallistaPlacementProbe
    {
        private const string CatalogRelPath = "Data/Canonical/structures-catalog.json";

        /// <summary>
        /// The SUBJECT first, then the CONTROLS. Controls are rows the owner has confirmed
        /// stand correctly in town, measured through the identical path so the subject's
        /// numbers have something to be wrong RELATIVE TO.
        /// </summary>
        private static readonly (string Id, string Role)[] Rows =
        {
            ("tower_ballista",     "SUBJECT  - owner reports it builds on its side"),
            ("tower_ground_archer","CONTROL  - wooden watchtower, upright, tower class"),
            ("jeweler",            "CONTROL  - Tripo shop family, owner-confirmed upright"),
            ("barracks",           "CONTROL  - Tripo shop family, owner-confirmed upright"),
        };

        [MenuItem("Defenders/Art/Probe Ballista Placement")]
        public static void Run()
        {
            FlowTrace.Enabled = true;

            var report = new StringBuilder();
            report.AppendLine("=== BALLISTA PLACEMENT PROBE (WO-1157) ===");

            try
            {
                var entries = ParseCatalog();
                if (entries == null)
                {
                    Debug.LogError("BALLISTA_PROBE_FAIL - structures-catalog.json unreadable/unparsable.");
                    return;
                }

                var addressToPath = BuildAddressMap();
                if (addressToPath == null)
                {
                    Debug.LogError("BALLISTA_PROBE_FAIL - no AddressableAssetSettings; structure art " +
                                   "cannot be resolved, so there is no geometry to measure.");
                    return;
                }

                var byId = new Dictionary<string, CatalogEntry>(StringComparer.Ordinal);
                foreach (var e in entries)
                    if (e != null && !string.IsNullOrEmpty(e.id)) byId[e.id] = e;

                int probed = 0;
                foreach (var row in Rows)
                {
                    if (!byId.TryGetValue(row.Id, out CatalogEntry entry) || entry == null)
                    {
                        report.AppendLine($"[{row.Id}] NOT IN CATALOG - skipped.");
                        continue;
                    }
                    report.AppendLine();
                    report.AppendLine($"--- {row.Id}   ({row.Role}) ---");
                    ProbeRow(entry, addressToPath, report);
                    probed++;
                }

                Debug.Log(report.ToString());
                if (probed == 0)
                {
                    Debug.LogError("BALLISTA_PROBE_FAIL - zero rows probed. That is a failure, not a pass.");
                    return;
                }
                Debug.Log($"BALLISTA_PROBE_OK {probed} row(s) probed through StructureFactory.Create.");
            }
            catch (Exception ex)
            {
                Debug.Log(report.ToString());
                Debug.LogError("BALLISTA_PROBE_FAIL - " + ex.GetType().Name + ": " + ex.Message +
                               "\n" + ex.StackTrace);
            }
        }

        // =====================================================================

        private static void ProbeRow(CatalogEntry entry, Dictionary<string, string> addressToPath,
                                     StringBuilder report)
        {
            string address = entry.visualPrefabPath;
            report.AppendLine($"  catalog: visualPrefabPath='{address}' " +
                              $"manual={(entry.orientation != null && entry.orientation.manual)} " +
                              $"euler={(entry.orientation != null ? Fmt(entry.orientation.Euler) : "<none>")} " +
                              $"preservePrefabRotation={(entry.repo != null && entry.repo.preservePrefabRotation)} " +
                              $"heightMul={(entry.repo != null ? entry.repo.heightMul : 0f):0.###}");

            if (string.IsNullOrEmpty(address) ||
                !addressToPath.TryGetValue(address, out string assetPath) || string.IsNullOrEmpty(assetPath))
            {
                report.AppendLine($"  UNRESOLVED address '{address}' - nothing to measure.");
                return;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefab == null)
            {
                report.AppendLine($"  address '{address}' -> '{assetPath}' loads no GameObject.");
                return;
            }

            bool baked = AnyModelAxisBaked(assetPath, out string bakedPath);
            report.AppendLine($"  asset: '{assetPath}' bakeAxisConversion={baked}" +
                              (baked && bakedPath != assetPath ? $" (via '{bakedPath}')" : ""));

            // ---- STAGE A: the ASSET's own authored pose, untouched ------------------
            // This is what StructurePoseCapture photographs. It is the only stage that
            // says what the ART believes, independent of every catalog channel.
            MeasureStandalone(prefab, keepNativeRotation: true, out Vector3 nativeEuler,
                              out Vector3 nativeSize, out float nativeAspect);
            report.AppendLine($"  A. asset native pose        : euler={Fmt(nativeEuler)} " +
                              $"size=({nativeSize.x:0.###} x {nativeSize.y:0.###} x {nativeSize.z:0.###}) " +
                              $"aspect={nativeAspect:0.###}   {Verdict(nativeAspect)}");

            // ---- STAGE B: the same asset FORCED to identity -------------------------
            // This is exactly what VisualFactory's DEF-232 reset does to the clone root,
            // measured in isolation. Comparing A and B is what settles whether the reset
            // is a no-op for this asset or the thing that tips it over.
            MeasureStandalone(prefab, keepNativeRotation: false, out _,
                              out Vector3 idSize, out float idAspect);
            report.AppendLine($"  B. same asset at IDENTITY   : euler=[0,0,0] " +
                              $"size=({idSize.x:0.###} x {idSize.y:0.###} x {idSize.z:0.###}) " +
                              $"aspect={idAspect:0.###}   {Verdict(idAspect)}");

            // ---- STAGE C: the REAL path -------------------------------------------
            // StructureFactory.Create, the function a placed building actually goes
            // through. The [Flow:Xform] / [Flow:Structure] lines it emits are the
            // step-in/step-out record; this block reports where it LANDED.
            GameObject host = null;
            try
            {
                host = new GameObject("__ballista_probe_host__");
                host.transform.position = Vector3.zero;

                GameObject built = null;
                try
                {
                    built = StructureFactory.Create(entry, new Pose(Vector3.zero, Quaternion.identity),
                                                    host.transform);
                }
                catch (Exception ex)
                {
                    report.AppendLine($"  C. StructureFactory.Create THREW {ex.GetType().Name}: {ex.Message}");
                }

                if (built == null)
                {
                    report.AppendLine("  C. StructureFactory.Create returned NULL - no structure to measure " +
                                      "(missing art / render-verify rollback). That is itself the finding.");
                    return;
                }

                if (!TryBounds(built, out Bounds b))
                {
                    report.AppendLine("  C. built structure has NO measurable renderer bounds.");
                    return;
                }

                float widest = Mathf.Max(b.size.x, b.size.z);
                float aspect = widest > 0.0001f ? b.size.y / widest : 0f;

                var visual = built.transform.childCount > 0 ? built.transform.GetChild(0) : built.transform;
                report.AppendLine($"  C. AFTER StructureFactory.Create (the real path):");
                report.AppendLine($"       visual local euler = {Fmt(visual.localEulerAngles)}  " +
                                  $"scale = {Fmt(visual.localScale)}");
                report.AppendLine($"       world bounds       = ({b.size.x:0.###}w x {b.size.y:0.###}h x " +
                                  $"{b.size.z:0.###}d)  aspect={aspect:0.###}  minY={b.min.y:0.###}");
                report.AppendLine($"       expected height    = {StructureFactory.OptsFor(entry).FitHeight:0.###} m " +
                                  $"(StructureFactory.OptsFor)");
                report.AppendLine($"       VERDICT            = {Verdict(aspect)}");

                // ---- STAGE D: the PICTURE ------------------------------------------
                // "For visual/spatial defects the screenshot IS the data." Every number
                // above can be right while the thing still looks wrong, and this project
                // has shipped exactly that. StructurePoseCapture photographs the ASSET;
                // this photographs THE STRUCTURE THE GAME JUST BUILT, which is the only
                // subject the owner's complaint is actually about.
                string png = RenderToPng(built, entry.id, b);
                report.AppendLine($"       render             = {(png ?? "<failed>")}");

                // ---- STAGE E: THE TIER RUNGS ---------------------------------------
                // ⛔ THE UPGRADE LADDER RUNS A DIFFERENT ROTATION CHANNEL, and that is not a
                // detail — it is a second place the same defect can live, invisibly.
                // StructureFactory.ReskinForLevel calls OptsFor(entry, applyManualEuler:FALSE)
                // ("tier models rely on their prefab-native orientation"), so the row's euler
                // NEVER reaches L2/L3: they get VisualFactory's identity reset. A base visual can
                // therefore be fixed while the thing the player sees after paying for an upgrade
                // is still on its side. StructureOrientationOracle's A2 cannot see this — for a
                // tier model it fits to height with no orientation applied, so the assert is
                // satisfied by construction whichever way up the mesh is. The number that decides
                // it is the tier model's IDENTITY aspect, printed here beside the base row's.
                ReportTiers(entry, addressToPath, report);

                // ---- STAGE F: the REAL UPGRADE path --------------------------------
                // Same discipline as stage C: not a replay of what ReskinForLevel is believed
                // to do, but a call to it. This is the structure a player sees after paying.
                for (int lvl = 2; lvl <= 3; lvl++)
                {
                    bool reskinned = false;
                    try { reskinned = StructureFactory.ReskinForLevel(built, entry, lvl); }
                    catch (Exception ex)
                    {
                        report.AppendLine($"  F. ReskinForLevel(L{lvl}) THREW {ex.GetType().Name}: {ex.Message}");
                        break;
                    }
                    if (!reskinned)
                    {
                        report.AppendLine($"  F. ReskinForLevel(L{lvl}) returned false - no tier model for this row.");
                        break;
                    }
                    if (!TryBounds(built, out Bounds ub))
                    {
                        report.AppendLine($"  F. after ReskinForLevel(L{lvl}): no measurable bounds.");
                        break;
                    }
                    float uw = Mathf.Max(ub.size.x, ub.size.z);
                    float ua = uw > 0.0001f ? ub.size.y / uw : 0f;
                    var uv = built.transform.childCount > 0 ? built.transform.GetChild(0) : built.transform;
                    string upng = RenderToPng(built, entry.id + "_L" + lvl, ub);
                    report.AppendLine(
                        $"  F. AFTER ReskinForLevel(L{lvl}) (the real upgrade path): " +
                        $"visual euler={Fmt(uv.localEulerAngles)} " +
                        $"bounds=({ub.size.x:0.###}w x {ub.size.y:0.###}h x {ub.size.z:0.###}d) " +
                        $"aspect={ua:0.###}  {Verdict(ua)}  render={(upng ?? "<failed>")}");
                }
            }
            finally
            {
                if (host != null) Object.DestroyImmediate(host);
            }
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        /// <summary>
        /// BLAST-RADIUS SWEEP over EVERY authored upgrade rung in the catalog (WO-1157).
        /// <para>
        /// ReskinForLevel's stated contract is "tier models rely on their prefab-native
        /// orientation" - but it reaches VisualFactory through OptsFor(applyManualEuler:false),
        /// which sets neither a LocalRotation NOR PreservePrefabRotation, so the clone root is
        /// FORCED TO IDENTITY and the native orientation the comment relies on is destroyed. That
        /// is invisible for a rung whose native root is already identity, and it lays the rung
        /// down for any rung that is not. This sweep prints which rungs are which, so the size
        /// of that contradiction is a measured number instead of an argument.
        /// </para>
        /// </summary>
        [MenuItem("Defenders/Art/Sweep Upgrade Rung Native Poses")]
        public static void SweepTierPoses()
        {
            try
            {
                var entries = ParseCatalog();
                var addressToPath = BuildAddressMap();
                if (entries == null || addressToPath == null)
                {
                    Debug.LogError("TIER_POSE_SWEEP_FAIL - catalog or Addressable settings unreadable.");
                    return;
                }

                var sb = new StringBuilder();
                sb.AppendLine("=== UPGRADE RUNG NATIVE-POSE SWEEP (WO-1157) ===");
                sb.AppendLine("A rung with a TILTED native root is one the identity reset lays down.");
                int rungs = 0, tilted = 0;

                foreach (var e in entries)
                {
                    var ladder = e?.repo?.upgradeVisualPath;
                    if (ladder == null) continue;
                    for (int i = 0; i < ladder.Length; i++)
                    {
                        string addr = ladder[i];
                        if (string.IsNullOrEmpty(addr)) continue;
                        if (!addressToPath.TryGetValue(addr, out string p) || string.IsNullOrEmpty(p)) continue;
                        var pf = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                        if (pf == null) continue;

                        rungs++;
                        float tilt = Vector3.Angle(pf.transform.localRotation * Vector3.up, Vector3.up);
                        MeasureStandalone(pf, true,  out Vector3 nEuler, out _, out float nAspect);
                        MeasureStandalone(pf, false, out _,              out _, out float iAspect);
                        bool isTilted = tilt > 1f;
                        if (isTilted) tilted++;
                        sb.AppendLine($"  {(isTilted ? "TILTED " : "identity")} {e.id} L{i + 2} '{addr}': " +
                                      $"native euler={Fmt(nEuler)} rootTilt={tilt:0.0}deg " +
                                      $"nativeAspect={nAspect:0.###} identityAspect={iAspect:0.###}");
                    }
                }

                sb.AppendLine($"  -> {tilted} of {rungs} authored rung(s) carry a tilted native root.");
                Debug.Log(sb.ToString());
                Debug.Log($"TIER_POSE_SWEEP_OK {rungs} rung(s), {tilted} tilted.");
            }
            catch (Exception ex)
            {
                Debug.LogError("TIER_POSE_SWEEP_FAIL - " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        /// <summary>
        /// Measure every authored upgrade rung the way ReskinForLevel will actually seat it:
        /// prefab-native pose vs the identity reset it really gets. A rung whose native pose is
        /// tilted and whose identity aspect collapses is an upgrade that lies down.
        /// </summary>
        private static void ReportTiers(CatalogEntry entry, Dictionary<string, string> addressToPath,
                                        StringBuilder report)
        {
            var ladder = entry.repo != null ? entry.repo.upgradeVisualPath : null;
            if (ladder == null || ladder.Length == 0) return;

            for (int i = 0; i < ladder.Length; i++)
            {
                string addr = ladder[i];
                if (string.IsNullOrEmpty(addr)) continue;
                if (!addressToPath.TryGetValue(addr, out string p) || string.IsNullOrEmpty(p))
                {
                    report.AppendLine($"  E. tier L{i + 2} '{addr}': UNRESOLVED address.");
                    continue;
                }
                var tierPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(p);
                if (tierPrefab == null)
                {
                    report.AppendLine($"  E. tier L{i + 2} '{addr}' -> '{p}': loads no GameObject.");
                    continue;
                }

                MeasureStandalone(tierPrefab, true,  out Vector3 tNative, out Vector3 tnSize, out float tnAspect);
                MeasureStandalone(tierPrefab, false, out _,               out Vector3 tiSize, out float tiAspect);
                report.AppendLine(
                    $"  E. tier L{i + 2} '{addr}': native euler={Fmt(tNative)} " +
                    $"aspect={tnAspect:0.###} ({tnSize.x:0.##} x {tnSize.y:0.##} x {tnSize.z:0.##})  |  " +
                    $"AS SEATED (identity, what ReskinForLevel gives it) aspect={tiAspect:0.###} " +
                    $"({tiSize.x:0.##} x {tiSize.y:0.##} x {tiSize.z:0.##})  {Verdict(tiAspect)}");
            }
        }

        /// <summary>Where the probe's renders of the BUILT structure land.</summary>
        private const string OutDir = "docs/ui-evidence/ballista-placement-2026-08-24";

        private const int Size = 900;

        /// <summary>
        /// Photograph the structure StructureFactory.Create just built, framed from its own
        /// bounds so every row in a run is directly comparable. Mirrors StructurePoseCapture's
        /// camera exactly (same fov, same 3/4 direction, same background) so an image from here
        /// can be laid beside one from there without an apples-to-oranges argument.
        /// Returns the path written, or null.
        /// </summary>
        private static string RenderToPng(GameObject built, string id, Bounds b)
        {
            Camera cam = null;
            RenderTexture rt = null;
            var prevActive = RenderTexture.active;
            try
            {
                System.IO.Directory.CreateDirectory(OutDir);

                float radius = Mathf.Max(b.size.magnitude * 0.5f, 0.001f);
                var camGo = new GameObject("__ballista_probe_cam__");
                cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.10f, 0.10f, 0.12f, 1f);
                cam.orthographic = false;
                cam.fieldOfView = 35f;
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = radius * 40f + 100f;

                Vector3 dir = new Vector3(0.75f, 0.42f, -1f).normalized;   // 3/4 view, slightly above
                cam.transform.position = b.center +
                    dir * (radius / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.25f);
                cam.transform.LookAt(b.center);

                rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                cam.Render();

                RenderTexture.active = rt;
                var tex = new Texture2D(Size, Size, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                tex.Apply();

                string outPath = OutDir + "/" + id + "__placed.png";
                System.IO.File.WriteAllBytes(outPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                return outPath;
            }
            catch (Exception ex)
            {
                FlowTrace.Warn("Structure",
                    $"BallistaPlacementProbe: render of '{id}' failed ({ex.GetType().Name}: {ex.Message}) " +
                    "- the NUMBERS above still stand, but there is no picture for this row.");
                return null;
            }
            finally
            {
                RenderTexture.active = prevActive;
                if (cam != null) { cam.targetTexture = null; Object.DestroyImmediate(cam.gameObject); }
                if (rt != null) Object.DestroyImmediate(rt);
            }
        }

        /// <summary>
        /// Instantiate the asset alone and measure it. <paramref name="keepNativeRotation"/> false
        /// forces the root to identity - the DEF-232 reset, reproduced in isolation.
        /// </summary>
        private static void MeasureStandalone(GameObject prefab, bool keepNativeRotation,
                                              out Vector3 euler, out Vector3 size, out float aspect)
        {
            euler = Vector3.zero; size = Vector3.zero; aspect = 0f;
            GameObject inst = null;
            try
            {
                inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                if (inst == null) return;
                inst.transform.position = Vector3.zero;
                if (!keepNativeRotation) inst.transform.localRotation = Quaternion.identity;
                euler = inst.transform.localEulerAngles;

                if (!TryBounds(inst, out Bounds b)) return;
                size = b.size;
                float widest = Mathf.Max(b.size.x, b.size.z);
                aspect = widest > 0.0001f ? b.size.y / widest : 0f;
            }
            finally
            {
                if (inst != null) Object.DestroyImmediate(inst);
            }
        }

        /// <summary>
        /// Words, not a pass/fail. The aspect band is only meaningful ACROSS the rows in one
        /// run - a ballista is legitimately wider than tall, which is exactly the false
        /// positive StructureNativePoseProbe produced on this asset. Never read one alone.
        /// </summary>
        private static string Verdict(float aspect) =>
            aspect >= 1.0f ? "tall-and-narrow" :
            aspect >= 0.5f ? "squat/wide (NOT conclusive on its own - compare the controls)" :
                             "very flat (candidate lying-down)";

        private static bool TryBounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            if (go == null) return false;
            var rends = go.GetComponentsInChildren<Renderer>(true);
            if (rends == null || rends.Length == 0) return false;
            bool any = false;
            foreach (var r in rends)
            {
                if (r == null) continue;
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return any;
        }

        private static string Fmt(Vector3 v) => $"[{v.x:0.##},{v.y:0.##},{v.z:0.##}]";

        private static bool AnyModelAxisBaked(string assetPath, out string bakedModelPath)
        {
            bakedModelPath = null;
            if (string.IsNullOrEmpty(assetPath)) return false;

            if (AssetImporter.GetAtPath(assetPath) is ModelImporter self)
            {
                if (self.bakeAxisConversion) { bakedModelPath = assetPath; return true; }
                return false;
            }

            var deps = AssetDatabase.GetDependencies(assetPath, true);
            if (deps == null) return false;
            foreach (var d in deps)
            {
                if (string.Equals(d, assetPath, StringComparison.OrdinalIgnoreCase)) continue;
                if (AssetImporter.GetAtPath(d) is ModelImporter mi && mi.bakeAxisConversion)
                { bakedModelPath = d; return true; }
            }
            return false;
        }

        private static Dictionary<string, string> BuildAddressMap()
        {
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) return null;

            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var group in settings.groups)
            {
                if (group == null) continue;
                foreach (var entry in group.entries)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.address)) continue;
                    map[entry.address] = AssetDatabase.GUIDToAssetPath(entry.guid);
                }
            }
            return map;
        }

        [Serializable]
        private sealed class StructuresFile
        {
            [JsonProperty("version")] public int Version;
            [JsonProperty("entries")] public List<CatalogEntry> Entries = new List<CatalogEntry>();
        }

        private static List<CatalogEntry> ParseCatalog()
        {
            string json = CanonicalJson.Read(CatalogRelPath);
            if (string.IsNullOrEmpty(json)) return null;

            var settings = new JsonSerializerSettings
            {
                Converters = { new StringEnumConverter() },
                NullValueHandling = NullValueHandling.Ignore,
                MissingMemberHandling = MissingMemberHandling.Ignore,
            };
            var file = JsonConvert.DeserializeObject<StructuresFile>(json, settings);
            return file?.Entries;
        }
    }
}
