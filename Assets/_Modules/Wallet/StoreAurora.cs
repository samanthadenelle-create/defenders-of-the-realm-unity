// =============================================================================
// StoreAurora — the FOUR motion moments of The Night Market, and nothing else
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Wallet   Namespace: DeNelle.Wallet   (WO-1050 Lane G)
//
// The owner asked for "rolling colors or whatever is popular". The 2026 storefront
// look is drifting mesh gradient + iridescent sheen, and it reads as expensive when
// it is ONE HELD NOTE and as a slot machine when it is sprinkled everywhere. So the
// budget is spent on the spotlight and kept away from the shelf's information:
//
//   G1  aurora drift  — two offset soft gradients behind the spotlight art, on
//                       OPPOSED slow paths so the ground never repeats   (~22 s)
//   G2  light change  — on selection the aurora CROSSFADES from the old band's
//                       light to the new one instead of cutting          (400 ms)
//   G3  specular sweep— a narrow bright band travels across the CTA. The one
//                       element that must never look asleep    (every 6 s, 700 ms)
//   G4  patronage sheen— a slow iridescent roll along the patronage band head. The
//                       only motion anywhere on the shelf, which is what marks the
//                       tier                                             (~14 s)
//
// ⛔ THE THREE RULES THAT KEEP THIS FROM BECOMING NOISE — all enforceable, all read
//    as acceptance criteria, none of them a preference:
//
//  1. MOTION NEVER CARRIES MEANING. Band identity is the mark + the eyebrow + the
//     step in greyscale value (NightMarketPalette). Kill every animation and the
//     screen is still COMPLETE. That is the acceptance test: FeatureFlags.
//     ReducedMotion = 1 and nothing is lost but movement. If turning motion off
//     loses information, the information was in the wrong place.
//  2. NOTHING A PLAYER READS TO DECIDE EVER MOVES. No motion on prices, quantities,
//     ledger bars, badges, the balance chip or the trust strip. Nothing pulses
//     faster than 3 Hz and no motion sits under body text.
//  3. IT IS BUILT AS SCROLLING UVs OVER TWO TINY SHARED TEXTURES, not as per-frame
//     Color lerps across many Graphics — which is exactly how a store modal quietly
//     costs 4 ms a frame on a Seeker. Budget: <= 2 extra draw calls and
//     <= 1 ms/frame, and the tick is a FlowTrace.Measure scope so a regression
//     SELF-REPORTS instead of being felt. The budget is measured, never asserted.
//
// ⚠ NO ASSET FILES. The two gradients are generated at runtime (a 64x64 blob and a
// 64x4 strip, both ~5 KB, built once and shared by every moment) rather than
// imported as a .mat + .png. A .mat/.png pair cannot be authored without Unity, and
// a store that needs an asset import to draw its own background is a store that
// renders wrong the first time someone clones the repo.
// =============================================================================

using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.UI;      // ElarionUiKit — every uGUI surface on this screen is built by the kit

namespace DeNelle.Wallet
{
    /// <summary>
    /// Drives The Night Market's four motion moments off ONE Update over a handful of
    /// UV-scrolled <see cref="RawImage"/>s. Add the moments with <see cref="AddDrift"/> /
    /// <see cref="AddSweep"/>; <see cref="CrossfadeTo"/> is G2. When the player's
    /// reduced-motion preference is on, <see cref="MotionEnabled"/> is false, nothing is
    /// registered and the component disables itself — the flat lights remain.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StoreAurora : MonoBehaviour
    {
        /// <summary>The per-frame budget, in milliseconds, for every moment combined.</summary>
        public const float FrameBudgetMs = 1f;

        /// <summary>How often the rolling cost is reported (and one frame is Measure-scoped).</summary>
        private const float ReportEverySeconds = 5f;

        /// <summary>G2's crossfade length. Selecting a card feels like turning a lamp toward it.</summary>
        public const float CrossfadeSeconds = 0.4f;

        /// <summary>
        /// The player's preference, asked ONCE. False = build the flat lights and no ticker.
        /// <para>Read at BUILD time, not per frame: a motion moment that is never constructed cannot
        /// cost a draw call, whereas one that is built and skipped still batches and still allocates.
        /// Toggling the preference re-renders the store, which rebuilds.</para>
        /// </summary>
        public static bool MotionEnabled => !FeatureFlags.ReducedMotion;

        // ── the two shared gradients ─────────────────────────────────────────
        private static Texture2D s_blob;    // G1 aurora ground
        private static Texture2D s_strip;   // G3 sweep + G4 sheen

        private sealed class Drift
        {
            public RawImage Image;
            public Vector2 Velocity;        // uv per second
            public Color BaseTint;          // pre-crossfade tint (G1 participants only)
            public bool FollowsBandLight;   // true = this one crossfades with the band (G2)
        }

        private sealed class Sweep
        {
            public RawImage Image;
            public float PeriodSeconds;     // e.g. 6 s between passes
            public float TravelSeconds;     // e.g. 0.7 s per pass
        }

        private readonly List<Drift> _drifts = new List<Drift>();
        private readonly List<Sweep> _sweeps = new List<Sweep>();

        // G2 crossfade state.
        private Color _lightFrom = Color.white;
        private Color _lightTo = Color.white;
        private float _fadeElapsed = CrossfadeSeconds;   // starts settled

        // Perf accounting.
        private double _accumMs;
        private int _accumFrames;
        private float _nextReportAt;
        private int _textureSwitches;

        /// <summary>How many extra Graphics this component owns — the draw-call ceiling, reported.</summary>
        public int MomentCount => _drifts.Count + _sweeps.Count;

        // =====================================================================
        //  Registration
        // =====================================================================

        /// <summary>
        /// G1 / G4 — a continuously drifting gradient. <paramref name="loopSeconds"/> is how long one
        /// full pass takes (22 s aurora, 14 s sheen); <paramref name="direction"/> is the uv path, and
        /// the two aurora layers are given OPPOSED directions so their sum never repeats.
        /// </summary>
        public RawImage AddDrift(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
                                 Color tint, float loopSeconds, Vector2 direction, bool followsBandLight,
                                 bool strip = false)
        {
            if (!MotionEnabled || parent == null) return null;
            var img = MakeLayer(parent, name, anchorMin, anchorMax, tint, strip);
            if (img == null) return null;
            float inv = loopSeconds > 0.01f ? 1f / loopSeconds : 0f;
            _drifts.Add(new Drift
            {
                Image = img,
                Velocity = direction.normalized * inv,
                BaseTint = tint,
                FollowsBandLight = followsBandLight,
            });
            return img;
        }

        /// <summary>
        /// G3 — a narrow bright band that crosses <paramref name="parent"/> every
        /// <paramref name="periodSeconds"/> and takes <paramref name="travelSeconds"/> to do it. It is
        /// hidden (alpha 0) between passes, so it costs nothing to look at and never strobes: one pass
        /// every 6 s is 0.17 Hz, far under the 3 Hz ceiling.
        /// </summary>
        public RawImage AddSweep(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
                                 Color tint, float periodSeconds, float travelSeconds)
        {
            if (!MotionEnabled || parent == null) return null;
            var img = MakeLayer(parent, name, anchorMin, anchorMax, tint, strip: true);
            if (img == null) return null;
            var c = img.color; c.a = 0f; img.color = c;
            _sweeps.Add(new Sweep
            {
                Image = img,
                PeriodSeconds = Mathf.Max(1f, periodSeconds),
                TravelSeconds = Mathf.Clamp(travelSeconds, 0.15f, 2f),
            });
            return img;
        }

        /// <summary>
        /// G2 — crossfade every band-following layer from the current light to
        /// <paramref name="light"/> over <see cref="CrossfadeSeconds"/>. A no-op under reduced motion,
        /// where the layers do not exist and the flat lights are already correct.
        /// </summary>
        public void CrossfadeTo(Color light)
        {
            if (!MotionEnabled) return;
            _lightFrom = _fadeElapsed >= CrossfadeSeconds
                ? _lightTo
                : Color.Lerp(_lightFrom, _lightTo, _fadeElapsed / CrossfadeSeconds);
            _lightTo = light;
            _fadeElapsed = 0f;
        }

        /// <summary>Sets the light with no fade — the correct call for the FIRST focus on open.</summary>
        public void SetLightImmediate(Color light)
        {
            _lightFrom = light;
            _lightTo = light;
            _fadeElapsed = CrossfadeSeconds;
            ApplyLight(light);
        }

        // =====================================================================
        //  Tick
        // =====================================================================

        private void OnEnable()
        {
            _nextReportAt = Time.unscaledTime + ReportEverySeconds;
            if (!MotionEnabled)
            {
                FlowTrace.Step("Store", "aurora: reduced-motion preference is ON — the four motion " +
                                        "moments were NOT built; the store renders its flat lights.");
                enabled = false;
            }
        }

        private void Update()
        {
            if (MomentCount == 0) { enabled = false; return; }

            float now = Time.unscaledTime;
            float dt = Time.unscaledDeltaTime;

            // One sampled frame per report window is wrapped in a REAL Measure scope, so the log
            // carries a `[Flow:Store] ... took X.Xms` line with the budget attached rather than a
            // number this file asserts about itself. Every other frame is timed cheaply and folded
            // into the rolling average below — measuring every frame would be its own firehose.
            if (now >= _nextReportAt)
            {
                using (FlowTrace.Measure("Store",
                           $"aurora tick ({MomentCount} moments, {_textureSwitches} shared textures)",
                           warnAboveMs: FrameBudgetMs))
                {
                    Tick(now, dt);
                }
                Report();
                _nextReportAt = now + ReportEverySeconds;
                return;
            }

            long start = Stopwatch.GetTimestamp();
            Tick(now, dt);
            _accumMs += (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency;
            _accumFrames++;
        }

        private void Tick(float now, float dt)
        {
            // G2 — advance the crossfade and push the resulting light at the band-following layers.
            if (_fadeElapsed < CrossfadeSeconds)
            {
                _fadeElapsed += dt;
                float k = Mathf.Clamp01(_fadeElapsed / CrossfadeSeconds);
                ApplyLight(Color.Lerp(_lightFrom, _lightTo, k * k * (3f - 2f * k)));   // smoothstep
            }

            // G1 / G4 — scroll uvs. uvRect.position wraps on its own (the textures are Repeat), so
            // there is no modulo bookkeeping and no per-frame allocation.
            for (int i = 0; i < _drifts.Count; i++)
            {
                var d = _drifts[i];
                if (d.Image == null) continue;
                var r = d.Image.uvRect;
                r.position += d.Velocity * dt;
                d.Image.uvRect = r;
            }

            // G3 — the pulsed pass.
            for (int i = 0; i < _sweeps.Count; i++)
            {
                var s = _sweeps[i];
                if (s.Image == null) continue;
                float phase = Mathf.Repeat(now, s.PeriodSeconds);
                var col = s.Image.color;
                if (phase > s.TravelSeconds)
                {
                    if (col.a != 0f) { col.a = 0f; s.Image.color = col; }
                    continue;
                }
                float t = phase / s.TravelSeconds;
                var r = s.Image.uvRect;
                r.x = -1f + t * 2f;
                s.Image.uvRect = r;
                // Fade in and out across the pass so the band never pops on or off.
                col.a = Mathf.Sin(t * Mathf.PI) * 0.55f;
                s.Image.color = col;
            }
        }

        private void ApplyLight(Color light)
        {
            for (int i = 0; i < _drifts.Count; i++)
            {
                var d = _drifts[i];
                if (d.Image == null || !d.FollowsBandLight) continue;
                var c = light;
                c.a = d.BaseTint.a;      // authored opacity is the layer's, not the band's
                d.Image.color = c;
            }
        }

        private void Report()
        {
            if (_accumFrames <= 0) return;
            double avg = _accumMs / _accumFrames;
            string line = $"aurora cost: {avg:F3}ms/frame avg over {_accumFrames} frames, " +
                          $"{MomentCount} moments on {_textureSwitches} shared texture(s) " +
                          $"(budget {FrameBudgetMs:F0}ms/frame, <=2 extra draw calls)";
            if (avg > FrameBudgetMs) FlowTrace.Warn("Store", line + " — OVER BUDGET.");
            else FlowTrace.Step("Store", line);
            _accumMs = 0d;
            _accumFrames = 0;
        }

        // =====================================================================
        //  Layers + the two shared textures
        // =====================================================================

        private RawImage MakeLayer(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
                                   Color tint, bool strip)
        {
            var tex = strip ? StripTexture() : BlobTexture();
            if (tex == null)
            {
                FlowTrace.Warn("Store", $"aurora: could not build the '{name}' gradient — that moment " +
                                        "is skipped. The store still renders; only the motion is absent.");
                return null;
            }

            // Built by the KIT, not by hand: ElarionUiKit.AddRawImage is the sanctioned primitive
            // for a UV-scrolled surface (the kit is the one file allowed to touch raw uGUI — see
            // UiObsidianConformanceRegression). raycastTarget is false by the primitive's own
            // default, which is the rule this screen needs: decoration must never eat a tap, and
            // the card under an aurora layer has to stay tappable.
            var img = ElarionUiKit.AddRawImage(parent, name, anchorMin, anchorMax, tex, tint);
            if (img == null)
            {
                FlowTrace.Warn("Store", $"aurora: the kit did not return a layer for '{name}' — " +
                                        "that moment is skipped. The store still renders.");
                return null;
            }
            img.transform.SetAsFirstSibling();   // behind the content it decorates
            _textureSwitches = (s_blob != null ? 1 : 0) + (s_strip != null ? 1 : 0);
            return img;
        }

        /// <summary>The aurora ground: two soft offset blobs, wrapping. Built once, shared forever.</summary>
        private static Texture2D BlobTexture()
        {
            if (s_blob != null) return s_blob;
            const int N = 64;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false)
            {
                name = "NightMarketAuroraBlob",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    float a = Blob(x, y, N, 0.30f, 0.35f, 0.34f);
                    float b = Blob(x, y, N, 0.72f, 0.66f, 0.28f);
                    float v = Mathf.Clamp01(a + b * 0.75f);
                    byte w = (byte)(255f * v);
                    px[y * N + x] = new Color32(255, 255, 255, w);
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            s_blob = tex;
            return s_blob;
        }

        /// <summary>Toroidal soft blob so the texture tiles seamlessly in both axes.</summary>
        private static float Blob(int x, int y, int n, float cx, float cy, float radius)
        {
            float u = x / (float)n, v = y / (float)n;
            float dx = Mathf.Abs(u - cx); dx = Mathf.Min(dx, 1f - dx);
            float dy = Mathf.Abs(v - cy); dy = Mathf.Min(dy, 1f - dy);
            float d = Mathf.Sqrt(dx * dx + dy * dy) / Mathf.Max(0.01f, radius);
            return d >= 1f ? 0f : Mathf.Pow(1f - d, 2f);
        }

        /// <summary>The specular strip: one narrow bright band across the u axis. G3 + G4 share it.</summary>
        private static Texture2D StripTexture()
        {
            if (s_strip != null) return s_strip;
            const int W = 64, H = 4;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
            {
                name = "NightMarketSpecularStrip",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var px = new Color32[W * H];
            for (int x = 0; x < W; x++)
            {
                float u = x / (float)W;
                float d = Mathf.Abs(u - 0.5f) / 0.16f;
                float v = d >= 1f ? 0f : Mathf.Pow(1f - d, 3f);
                byte w = (byte)(255f * v);
                for (int y = 0; y < H; y++) px[y * W + x] = new Color32(255, 255, 255, w);
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            s_strip = tex;
            return s_strip;
        }
    }
}
