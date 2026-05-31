// =============================================================================
// PerfBudgetWindow — the TRUE (device-independent) mobile benchmark for QA.
// -----------------------------------------------------------------------------
// Desktop FPS is a flattering lie — a PC's GPU dwarfs a phone's. But the things
// that actually blow a mobile budget — draw BATCHES, SetPass calls, TRIANGLES,
// VERTICES, memory — are IDENTICAL on every device (a phone just runs each batch
// slower). So QA validates the BUDGET, not the framerate, right here on the dev
// machine — no APK, no device, repeatable in seconds.
//
// Use: Window > Defenders/QA/Perf Budget (Standard Phone)  ->  enter Play mode
//      ->  load it up with PerfStressTest (Enter = +1000)  ->  watch the colours.
//   GREEN = within standard-phone budget · YELLOW = watch · RED = over.
// Calibrate the thresholds occasionally against a real Seeker build.
// =============================================================================
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public sealed class PerfBudgetWindow : EditorWindow
    {
        // Standard-phone budget thresholds (warn, over). Tune against a real device.
        private const int BatchWarn = 150,  BatchOver = 300;
        private const int PassWarn  = 100,  PassOver  = 200;
        private const int TriWarn   = 300_000, TriOver = 600_000;
        private const int VertWarn  = 300_000, VertOver = 600_000;
        private const int MemWarn   = 512,  MemOver   = 1024;   // managed MB (proxy)

        [MenuItem("Defenders/QA/Perf Budget (Standard Phone)")]
        private static void Open() => GetWindow<PerfBudgetWindow>("Perf Budget");

        private void OnInspectorUpdate() => Repaint();   // refresh live during Play

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Standard-Phone Budget", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(Application.isPlaying
                ? "● PLAYING — live device-independent draw metrics"
                : "Enter Play mode to read live metrics.");
            EditorGUILayout.Space();

            Row("Draw calls",    UnityStats.drawCalls,    BatchWarn, BatchOver);
            Row("SetPass calls", UnityStats.setPassCalls, PassWarn,  PassOver);
            Row("Triangles",     UnityStats.triangles,    TriWarn,   TriOver);
            Row("Vertices",      UnityStats.vertices,     VertWarn,  VertOver);
            Row("Managed MB",    (int)(System.GC.GetTotalMemory(false) / (1024 * 1024)), MemWarn, MemOver);

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "These counts are the SAME on any device — a phone just renders each slower, so " +
                "GREEN here = within budget on mobile. (Desktop FPS is NOT a mobile reading.)\n\n" +
                "Load test: in Play mode, PerfStressTest spawns +1000 generic instanced objects on " +
                "ENTER/SPACE/F9 — watch whether instancing keeps Batches flat as objects climb.\n\n" +
                "Calibrate thresholds against a real Seeker build occasionally.",
                MessageType.Info);
        }

        private static void Row(string name, int value, int warn, int over)
        {
            var prev = GUI.color;
            GUI.color = value >= over ? new Color(1f, 0.4f, 0.4f)
                      : value >= warn ? new Color(1f, 0.85f, 0.3f)
                      :                  new Color(0.45f, 1f, 0.45f);
            EditorGUILayout.LabelField(name, $"{value:N0}   (budget < {warn:N0})");
            GUI.color = prev;
        }
    }
}
