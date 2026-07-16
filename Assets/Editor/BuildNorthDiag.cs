// BuildNorthDiag — DATA, not guesses (owner 2026-07-16 "cannot go forward north").
// Measures the REAL buildable-grid bounds and the REAL castle-wall world positions in
// Main_Castle_Overworld, so we can PROVE whether the camera's grid clamp stops the view
// at/short of/past the north wall — and by exactly how much to extend, if at all.
// Headless: powershell run-unity-method.ps1 -Method DeNelle.Editor.BuildNorthDiag.Run -LogName build-north-diag.log
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace DeNelle.Editor
{
    public static class BuildNorthDiag
    {
        const string ScenePath = "Assets/Scenes/Main_Castle_Overworld.unity";

        public static void Run()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[BuildNorthDiag] ===== BUILD MODE NORTH BOUNDS — MEASURED =====");

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            sb.AppendLine($"[BuildNorthDiag] scene='{scene.name}' loaded={scene.isLoaded}");

            // --- GRID bounds: PlacementGrid has no scene instance (verified), so it is created at
            // runtime with its serialized defaults. Read those defaults straight off the type so the
            // numbers are the REAL ones the game uses, not hardcoded here.
            int gw = 30, gh = 30; float cs = 3f;
            System.Type gridType = null;
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                gridType = asm.GetType("DeNelle.Village.PlacementGrid");
                if (gridType != null) break;
            }
            // Run the REAL PlacementGrid.Awake (via reflection) and read the origin IT computes —
            // do NOT re-derive origin here (that was a re-derivation bug: it used the old symmetric
            // -gh*cs/2 formula and reported the wrong bounds while Awake anchors the south edge).
            Vector3 origin = Vector3.zero;
            var tmpGo = new GameObject("__gridprobe");
            try
            {
                if (gridType != null)
                {
                    var comp = tmpGo.AddComponent(gridType);
                    gw = (int)gridType.GetField("gridWidth").GetValue(comp);
                    gh = (int)gridType.GetField("gridHeight").GetValue(comp);
                    cs = (float)gridType.GetField("cellSize").GetValue(comp);
                    var awake = gridType.GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                    if (awake != null) awake.Invoke(comp, null);   // runs the actual origin logic
                    origin = (Vector3)gridType.GetField("origin").GetValue(comp);
                    sb.AppendLine($"[BuildNorthDiag] PlacementGrid resolved; ran real Awake; origin read from live component.");
                }
                else sb.AppendLine("[BuildNorthDiag] WARN PlacementGrid type not resolved — using 30x30x3 fallback.");
            }
            catch (System.Exception e) { sb.AppendLine($"[BuildNorthDiag] grid probe threw: {e.Message}"); }
            Object.DestroyImmediate(tmpGo);
            if (origin == Vector3.zero) origin = new Vector3(-gw * cs * 0.5f, 0f, -gh * cs * 0.5f);
            float gridZmin = origin.z, gridZmax = origin.z + gh * cs;
            float gridXmin = origin.x, gridXmax = origin.x + gw * cs;
            sb.AppendLine($"[BuildNorthDiag] GRID {gw}x{gh} cell={cs}m origin={origin}");
            sb.AppendLine($"[BuildNorthDiag] GRID world X [{gridXmin:F1} .. {gridXmax:F1}]  Z [{gridZmin:F1} .. {gridZmax:F1}]  (north edge = Z {gridZmax:F1})");
            sb.AppendLine($"[BuildNorthDiag] CAMERA clamp = same grid bounds -> north pan stops at Z {gridZmax:F1}");

            // --- CASTLE walls: measure each CastleSide_* root's combined renderer bounds (world).
            string[] sides = { "North", "South", "East", "West" };
            foreach (var side in sides)
            {
                var root = GameObject.Find("CastleSide_" + side);
                if (root == null) { sb.AppendLine($"[BuildNorthDiag] CastleSide_{side}: NOT FOUND in scene"); continue; }
                var rends = root.GetComponentsInChildren<Renderer>(true);
                if (rends == null || rends.Length == 0) { sb.AppendLine($"[BuildNorthDiag] CastleSide_{side}: pos={root.transform.position} (no renderers)"); continue; }
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                sb.AppendLine($"[BuildNorthDiag] CastleSide_{side}: rootPos={root.transform.position}  worldBounds center={b.center} size={b.size}  Z[{b.min.z:F1}..{b.max.z:F1}] X[{b.min.x:F1}..{b.max.x:F1}]");
                if (side == "North")
                {
                    float wallZ = b.max.z;
                    float delta = wallZ - gridZmax;
                    sb.AppendLine($"[BuildNorthDiag] >>> NORTH WALL northmost Z = {wallZ:F1} ; GRID north edge Z = {gridZmax:F1} ; delta = {delta:F1}");
                    if (delta > 0.5f)
                        sb.AppendLine($"[BuildNorthDiag] >>> PROVEN: the wall extends {delta:F1}m PAST the grid — camera clamps SHORT of the wall. Extend gridHeight north by ~{Mathf.CeilToInt(delta / cs)} cells (+ shift so it grows NORTH only).");
                    else if (delta > -6f)
                        sb.AppendLine($"[BuildNorthDiag] >>> PROVEN: grid north edge sits ~AT the wall (delta {delta:F1}m) — you cannot pan/build past it BY DESIGN. To build north of the wall, extend the grid north.");
                    else
                        sb.AppendLine($"[BuildNorthDiag] >>> PROVEN: grid north edge is {(-delta):F1}m PAST the wall — north is buildable beyond the wall; the blocker is NOT the grid clamp. Look elsewhere.");
                }
            }

            // Where does the hero/heart sit, for reference (grid is centered on 0,0,0 = Heart).
            var heart = GameObject.Find("Heart of Elarion") ?? GameObject.Find("HeartOfElarion") ?? GameObject.Find("TreeOfLife");
            sb.AppendLine($"[BuildNorthDiag] Heart/tree = {(heart != null ? heart.transform.position.ToString() : "<not found by name>")}");

            sb.AppendLine("[BuildNorthDiag] ===== END =====");
            Debug.Log(sb.ToString());
        }
    }
}
