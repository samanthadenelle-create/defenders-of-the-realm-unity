// =============================================================================
// PerfDiagnostic — runtime frame-timing instrument to ROOT-CAUSE the OuterWorld
// "frame by frame" (~1 fps). NOT a fix, NOT a gameplay change — it only READS
// Unity's built-in profiler counters and logs them to Player.log every 2 s, so we
// can see DEFINITIVELY whether the slowdown is CPU/main-thread (script/logic) or
// GPU/render bound, plus draw calls / triangles / GC-per-frame.
//
// Self-bootstraps (AfterSceneLoad), DontDestroyOnLoad. Delete after the cause is
// confirmed. Output line looks like:
//   [PERFDIAG] scene=Village2 loadedScenes=2 fps=1.0 worstFrame=980ms |
//     CPU-frame=970.0ms GPU-frame=18.0ms main=965.0ms ... => CPU-BOUND (script/main-thread)
// =============================================================================
using System.Collections.Generic;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Core.Debugging
{
    public sealed class PerfDiagnostic : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var go = new GameObject("[PerfDiagnostic]");
            DontDestroyOnLoad(go);
            go.AddComponent<PerfDiagnostic>();
            Debug.Log("[PERFDIAG] started — logging CPU-vs-GPU frame timing every 2s (diagnostic instrument, no gameplay change).");
        }

        private struct Rec { public string Label; public ProfilerRecorder R; public bool Ms; public bool Kb; }
        private readonly List<Rec> _recs = new List<Rec>();
        private float _accum; private int _frames; private float _worst;

        private void Add(ProfilerCategory cat, string stat, string label, bool ms, bool kb = false)
        {
            _recs.Add(new Rec { Label = label, R = ProfilerRecorder.StartNew(cat, stat), Ms = ms, Kb = kb });
        }

        private void OnEnable()
        {
            Add(ProfilerCategory.Internal, "CPU Total Frame Time", "CPU-frame", true);
            Add(ProfilerCategory.Render,   "GPU Frame Time",       "GPU-frame", true);
            Add(ProfilerCategory.Internal, "Main Thread",          "main",      true);
            Add(ProfilerCategory.Render,   "Render Thread",        "render",    true);
            Add(ProfilerCategory.Render,   "Draw Calls Count",     "draws",     false);
            Add(ProfilerCategory.Render,   "SetPass Calls Count",  "setpass",   false);
            Add(ProfilerCategory.Render,   "Batches Count",        "batches",   false);
            Add(ProfilerCategory.Render,   "Triangles Count",      "tris",      false);
            Add(ProfilerCategory.Memory,   "GC Allocated In Frame","gc",        false, true);
        }

        private void OnDisable()
        {
            for (int i = 0; i < _recs.Count; i++) _recs[i].R.Dispose();
            _recs.Clear();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _accum += dt; _frames++;
            if (dt > _worst) _worst = dt;
            if (_accum < 2f) return;

            var sb = new StringBuilder(256);
            sb.Append("[PERFDIAG] scene=").Append(SceneManager.GetActiveScene().name)
              .Append(" loadedScenes=").Append(SceneManager.sceneCount)
              .Append(" fps=").Append((_frames / _accum).ToString("0.0"))
              .Append(" worstFrame=").Append((_worst * 1000f).ToString("0")).Append("ms |");

            double cpu = -1, gpu = -1;
            for (int i = 0; i < _recs.Count; i++)
            {
                var rec = _recs[i];
                if (!rec.R.Valid) continue;
                long v = rec.R.LastValue;
                if (rec.Ms)
                {
                    double m = v * 1e-6;            // ns -> ms
                    sb.Append(' ').Append(rec.Label).Append('=').Append(m.ToString("0.0")).Append("ms");
                    if (rec.Label == "CPU-frame") cpu = m;
                    else if (rec.Label == "GPU-frame") gpu = m;
                }
                else if (rec.Kb)
                {
                    sb.Append(' ').Append(rec.Label).Append('=').Append(v / 1024).Append("KB/frame");
                }
                else
                {
                    sb.Append(' ').Append(rec.Label).Append('=').Append(v);
                }
            }

            if (cpu >= 0 || gpu >= 0)
                sb.Append(" => ").Append(cpu > gpu ? "CPU-BOUND (script/main-thread)" : "GPU-BOUND (rendering)");
            else
                sb.Append(" => (timing counters unavailable in this build — see fps/draws/gc above)");

            Debug.Log(sb.ToString());
            LogCensus();
            _accum = 0; _frames = 0; _worst = 0f;
        }

        // Object census — names WHAT is accumulating. Logs total live GameObjects +
        // total MonoBehaviours (incl. inactive) + the 8 most-common GameObject names.
        // If a name's count climbs each sample, THAT is the leak, named.
        private static readonly Dictionary<string, int> s_counts = new Dictionary<string, int>(1024);
        private void LogCensus()
        {
            var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            int mbs = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            s_counts.Clear();
            for (int i = 0; i < all.Length; i++)
            {
                string n = all[i].name;
                s_counts.TryGetValue(n, out int c);
                s_counts[n] = c + 1;
            }
            var sb = new StringBuilder(256);
            sb.Append("[PERFDIAG-OBJ] totalTransforms=").Append(all.Length)
              .Append(" totalMonoBehaviours=").Append(mbs).Append(" | top:");
            for (int rank = 0; rank < 8 && s_counts.Count > 0; rank++)
            {
                string bestName = null; int best = -1;
                foreach (var kv in s_counts) if (kv.Value > best) { best = kv.Value; bestName = kv.Key; }
                if (bestName == null) break;
                sb.Append(' ').Append(bestName).Append('x').Append(best);
                s_counts.Remove(bestName);
            }
            Debug.Log(sb.ToString());
        }
    }
}
