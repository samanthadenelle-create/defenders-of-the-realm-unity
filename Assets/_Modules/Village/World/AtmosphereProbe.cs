// =============================================================================
// AtmosphereProbe — WO-1602. The FIRST-MINUTES ATMOSPHERE TIMELINE for the town.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.World
//
// WHY THIS EXISTS (CLAUDE.md §12 — instrument, don't guess):
//   Owner reset frames 2026-09-07 (Screenshot_20260907-132930 / -133243 /
//   break_00_possible_softlock): inside the first ~10 minutes of a NEW GAME the
//   town ground reads as shimmering blue-teal "water", and minutes later the whole
//   scene sits under a dense pale haze with washed-out walls. Both states are
//   TRANSIENT — the third frame is clear.
//
//   MEASURED, not assumed, before this file was written (2026-09-07):
//     • The device break-log for that session (logs/f8-inbox/device/
//       SM02G4061955851/break-log.jsonl, 11421 lines) contains ZERO [Flow:FloorDiag],
//       ZERO [Flow:WorldFeel] and ZERO fog lines. There is NO atmosphere timeline
//       in this project at all — only single-shot lines at scene load. A transient
//       that resolves itself is therefore STRUCTURALLY invisible: by the time the
//       owner reaches an F8, every writer has already been overwritten.
//     • The closest FloorDiag we DO have (logs/f8-inbox/capture-20260902-015016-
//       seq4639.md:60) reads: TERRAIN 'ExteriorTerrain' mat='ExteriorTerrainMaterial'
//       shader='Universal Render Pipeline/Terrain/Lit' broken=False 8 layer(s), every
//       one naming a real BaseColor texture. So "terrain layers streamed late" is
//       NOT supported by any captured line — but nothing samples it a minute later
//       either, which is the hole this closes.
//
// WHAT IT DOES:
//   On every OUTDOOR scene activation it runs ONE bounded ladder of samples —
//   SampleTimesSec below, 5 s .. 300 s — and prints a single [Flow:Atmos] "T+<n>s"
//   line per sample carrying every value that could produce either symptom:
//     fog on/mode/density/colour/start/end, ambient mode+light+intensity+trilight,
//     skybox material name, sun intensity/colour/euler, the active URP Volume stack
//     (name/priority/weight — post-exposure and bloom are the untested candidate the
//     ticket does not list), and the active Terrain's material + layer count.
//
//   The ladder is BOUNDED and then stops forever for that load. It is NOT a
//   per-frame log: §12 and memory `logcat-ring-buffer-destroys-evidence` — a
//   per-frame atmosphere dump would evict the boot window out of the 256 KiB
//   Android ring and destroy the evidence it was added to collect.
//
// WHAT IT DELIBERATELY DOES NOT DO:
//   It never WRITES a RenderSettings value. It is a read-only witness. Every writer
//   names itself at its own call site ([Flow:Atmos] in WorldFeelInjector, BattleArena,
//   Lantern, SkyProgressionController, NightTorchLightSystem); this file supplies the
//   TIMELINE those point events hang on, so "who last wrote fog before the frame the
//   owner saw" is answerable from one log instead of a theory.
//
// Lifecycle mirrors WorldFeelInjector exactly (RuntimeInitializeOnLoadMethod +
// DDOL singleton + sceneLoaded/activeSceneChanged re-arm), so it needs no scene edit
// (CLAUDE.md §3) and cannot be forgotten by a future builder. Every LOG STRING is ASCII
// (the comments are not, matching the rest of this tree): a device log is read through
// logcat and a stray multi-byte dash in a trace line is one more thing between the reader
// and the evidence.
// =============================================================================

using System.Collections;
using System.Text;
using DeNelle.Core;
using DeNelle.Core.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace DeNelle.Village.World
{
    /// <summary>
    /// Read-only per-minute atmosphere sampler for the first five minutes of an
    /// outdoor scene. Prints [Flow:Atmos] T+&lt;n&gt;s lines; writes nothing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AtmosphereProbe : MonoBehaviour
    {
        public const string Sys = "Atmos";

        /// <summary>
        /// The sample ladder, seconds after the outdoor scene became active. Dense
        /// early (both owner frames land inside the first four minutes) and then
        /// per-minute out to five, which is the window the ticket names. Bounded on
        /// purpose: after the last entry this coroutine ENDS and costs nothing.
        /// </summary>
        internal static readonly float[] SampleTimesSec = { 5f, 15f, 30f, 60f, 120f, 180f, 240f, 300f };

        public static AtmosphereProbe Instance { get; private set; }

        private Coroutine _ladder;
        private string _ladderScene;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject(nameof(AtmosphereProbe)).AddComponent<AtmosphereProbe>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;
            Rearm();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Rearm();
        private void OnActiveSceneChanged(Scene from, Scene to) => Rearm();

        /// <summary>
        /// Restart the ladder when (and only when) a DIFFERENT outdoor scene became
        /// active. An additive load inside the same town must not reset the clock —
        /// the ticket's window is measured from the town entry the player felt.
        /// </summary>
        private void Rearm()
        {
            string active = SceneManager.GetActiveScene().name;
            bool outdoor = !string.IsNullOrEmpty(active) && HubScenes.IsOverworld(active);

            if (!outdoor)
            {
                if (_ladder != null)
                {
                    StopCoroutine(_ladder);
                    _ladder = null;
                    _ladderScene = null;
                    FlowTrace.Step(Sys, $"ladder STOPPED - active scene '{active}' is not an outdoor/overworld " +
                                        "scene, so the first-minutes town window does not apply here.");
                }
                return;
            }

            if (_ladder != null && _ladderScene == active) return;   // same town, additive load: keep the clock

            if (_ladder != null) StopCoroutine(_ladder);
            _ladderScene = active;
            _ladder = StartCoroutine(SampleLadder(active));
        }

        private IEnumerator SampleLadder(string sceneName)
        {
            float t0 = Time.realtimeSinceStartup;
            FlowTrace.Step(Sys, $"ladder ARMED for '{sceneName}' - {SampleTimesSec.Length} sample(s) at " +
                                $"T+{string.Join("/", System.Array.ConvertAll(SampleTimesSec, s => s.ToString("0")))}s. " +
                                "Every line below is a READ; this probe never writes RenderSettings.");

            // Sample zero: the state the scene entered with, before any deferred
            // injector ladder (WorldFeelInjector re-applies on every sceneLoaded,
            // MagentaGuard re-sweeps at 1/3/8 s) has had a chance to move it.
            Emit(sceneName, 0f);

            for (int i = 0; i < SampleTimesSec.Length; i++)
            {
                float due = SampleTimesSec[i];
                while (Time.realtimeSinceStartup - t0 < due) yield return null;

                // The active scene can change mid-wait (a raid, a dungeon, a battle
                // scene). Emitting then would attribute another scene's authored mood
                // to the town and is exactly the kind of mis-read §11B forbids.
                if (SceneManager.GetActiveScene().name != sceneName)
                {
                    FlowTrace.Step(Sys, $"ladder ENDED early at T+{due:0}s - active scene left '{sceneName}' " +
                                        $"for '{SceneManager.GetActiveScene().name}'. Remaining samples skipped so no " +
                                        "other scene's RenderSettings are recorded as the town's.");
                    _ladder = null;
                    _ladderScene = null;
                    yield break;
                }

                Emit(sceneName, Time.realtimeSinceStartup - t0);
            }

            FlowTrace.Step(Sys, $"ladder COMPLETE for '{sceneName}' - the first-{SampleTimesSec[SampleTimesSec.Length - 1]:0}s " +
                                "atmosphere timeline is in this log. It stops here and costs nothing further.");
            _ladder = null;
            _ladderScene = null;
        }

        /// <summary>
        /// One timeline line. Guarded end-to-end: a probe that throws would take the
        /// coroutine down and silently end the timeline (§12 — no silent failures).
        /// </summary>
        private void Emit(string sceneName, float elapsed)
        {
            Guard.Try(Sys, "sample atmosphere", () =>
            {
                var sb = new StringBuilder(512);
                sb.Append("T+").Append(elapsed.ToString("0")).Append("s '").Append(sceneName).Append("' ");
                AppendRenderSettings(sb);
                sb.Append(' ');
                AppendVolumes(sb);
                sb.Append(' ');
                AppendTerrain(sb);
                FlowTrace.Step(Sys, sb.ToString());
            });
        }

        /// <summary>
        /// Everything a fog / haze / washed-out-wall symptom can come from on the
        /// RenderSettings side. fogStart/End are printed even in exponential modes:
        /// a writer that flips only fogMode (Lantern does exactly that) leaves the
        /// linear pair behind as the evidence of who touched it.
        /// </summary>
        internal static void AppendRenderSettings(StringBuilder sb)
        {
            sb.Append("FOG on=").Append(RenderSettings.fog)
              .Append(" mode=").Append(RenderSettings.fogMode)
              .Append(" density=").Append(RenderSettings.fogDensity.ToString("0.00000"))
              .Append(" color=").Append(Fmt(RenderSettings.fogColor))
              .Append(" start=").Append(RenderSettings.fogStartDistance.ToString("0.0"))
              .Append(" end=").Append(RenderSettings.fogEndDistance.ToString("0.0"));

            sb.Append(" | AMBIENT mode=").Append(RenderSettings.ambientMode)
              .Append(" light=").Append(Fmt(RenderSettings.ambientLight))
              .Append(" intensity=").Append(RenderSettings.ambientIntensity.ToString("0.00"))
              .Append(" sky=").Append(Fmt(RenderSettings.ambientSkyColor))
              .Append(" eq=").Append(Fmt(RenderSettings.ambientEquatorColor))
              .Append(" ground=").Append(Fmt(RenderSettings.ambientGroundColor));

            var sky = RenderSettings.skybox;
            sb.Append(" | SKY mat=").Append(sky != null ? sky.name : "<NULL>")
              .Append(" reflection=").Append(RenderSettings.defaultReflectionMode);

            var sun = RenderSettings.sun;
            if (sun != null)
            {
                sb.Append(" | SUN '").Append(sun.name).Append("' intensity=").Append(sun.intensity.ToString("0.00"))
                  .Append(" color=").Append(Fmt(sun.color))
                  .Append(" euler=").Append(sun.transform.eulerAngles.ToString("0.0"))
                  .Append(" enabled=").Append(sun.isActiveAndEnabled);
            }
            else
            {
                sb.Append(" | SUN <NULL - RenderSettings.sun unset; the skybox sun disc has nothing to track>");
            }
        }

        /// <summary>
        /// The URP Volume stack. THE TICKET DOES NOT NAME THIS AND IT SHOULD:
        /// WorldFeelInjector's global grade ships Bloom intensity 4.5 / threshold 1.1
        /// and a +0.75 EV post-exposure (WorldFeelInjector.cs:126-135). A blown, pale,
        /// low-contrast frame is what a doubled or unclamped exposure looks like, and
        /// it is indistinguishable from "dense fog" in a screenshot. Printing every
        /// live Volume's priority and weight is what separates the two.
        /// </summary>
        internal static void AppendVolumes(StringBuilder sb)
        {
            var volumes = Object.FindObjectsByType<Volume>(FindObjectsSortMode.None);
            sb.Append("| VOLUMES n=").Append(volumes != null ? volumes.Length : 0);
            if (volumes == null) return;

            for (int i = 0; i < volumes.Length; i++)
            {
                var v = volumes[i];
                if (v == null) continue;
                sb.Append(" [").Append(v.name)
                  .Append(" global=").Append(v.isGlobal)
                  .Append(" prio=").Append(v.priority.ToString("0.0"))
                  .Append(" weight=").Append(v.weight.ToString("0.00"))
                  .Append(" on=").Append(v.isActiveAndEnabled)
                  .Append(" profile=").Append(v.sharedProfile != null ? v.sharedProfile.name : "<NULL>")
                  .Append(']');
            }
        }

        /// <summary>
        /// The terrain side of the ticket. MagentaGuard's FloorDiag already dumps this
        /// ONCE at scene load; the open question is whether it is still true a minute
        /// later, which only a timeline can answer. A layer whose diffuseTexture is
        /// null is the "placeholder / streamed late" state the ticket asks about, and
        /// it is called out by name rather than left to be inferred from a count.
        /// </summary>
        internal static void AppendTerrain(StringBuilder sb)
        {
            var terrains = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
            sb.Append("| TERRAIN n=").Append(terrains != null ? terrains.Length : 0);
            if (terrains == null || terrains.Length == 0)
            {
                sb.Append(" <none active - the ground the player sees is NOT a Terrain here>");
                return;
            }

            for (int i = 0; i < terrains.Length; i++)
            {
                var t = terrains[i];
                if (t == null) continue;
                var mat = t.materialTemplate;
                sb.Append(" ['").Append(t.name).Append("' mat=")
                  .Append(mat != null ? mat.name : "<NULL - draws with the engine default>")
                  .Append(" shader=").Append(mat != null && mat.shader != null ? mat.shader.name : "<null>");

                var data = t.terrainData;
                if (data == null)
                {
                    sb.Append(" terrainData=<NULL>]");
                    continue;
                }

                var layers = data.terrainLayers;
                int nullTex = 0;
                if (layers != null)
                {
                    for (int k = 0; k < layers.Length; k++)
                        if (layers[k] == null || layers[k].diffuseTexture == null) nullTex++;
                }
                sb.Append(" layers=").Append(layers != null ? layers.Length : 0)
                  .Append(" layersMissingBaseColor=").Append(nullTex);
                if (nullTex > 0)
                    sb.Append(" <-- PLACEHOLDER/UNSTREAMED LAYER(S): this is the 'ground shows its base colour' state");
                sb.Append(']');
            }
        }

        private static string Fmt(Color c) =>
            $"({c.r:0.000},{c.g:0.000},{c.b:0.000},{c.a:0.000})";
    }
}
