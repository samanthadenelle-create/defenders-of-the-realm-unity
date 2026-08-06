// =============================================================================
// HeroInjuredVignette - low-HP red screen-edge vignette. A SECONDARY cue.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// ## STATUS: DEMOTED TO SECONDARY (WO-888, 2026-08-05)
//   This used to be the ONLY low-HP tell in the game. The owner is red/green
//   colourblind, so the single signal telling her she was about to die was one she
//   could not reliably see - a real accessibility bug, not a nicety (registry
//   section 8 item 7).
//
//   The PRIMARY tell is now HeroHpStateAura: a world-space aura whose PULSE RATE and
//   GUTTERING SHAPE carry the severity, both of which survive greyscale. This vignette
//   is deliberately KEPT and not deleted - it is a genuinely useful redundant cue for
//   players who can see red, and redundancy is good accessibility. What was wrong was
//   colour ONLY. It is therefore turned DOWN (see the alpha defaults below) so it frames
//   without dominating, and it must never again be the only thing that changes at low HP.
//
// WHAT IT DOES (WO-493 #5 / WO-497, the HERO half of the injured stance):
//   * While the hero is "injured" (HP below the low-HP cutoff, ~30%) a RED
//     screen-EDGE vignette breathes in and out, framing the view in danger so
//     the player FEELS near-death without staring at the HP number.
//   * Driven by HeroHealth.SetInjuredVisual(bool) - HeroHealth owns the single
//     threshold-crossing detection (so the animator swap, the slow, the audio
//     and this vignette all flip together off one source of truth).
//
// DESIGN (deliberately self-contained + low-risk, mirrors HeroHitReaction):
//   * Rendered with IMGUI (OnGUI) using a generated radial-edge texture, NOT a
//     URP post-process Vignette: it needs no scene Volume / profile and always
//     renders in player builds (UI-Toolkit / post-fx have repeatedly come up
//     empty in this project - see HeroHealth's own IMGUI bar + HeroHitReaction).
//   * Added to the hero automatically by HeroHealthBootstrap alongside
//     HeroHealth, so it needs no prefab wiring.
//   * Flag-gated upstream (FeatureFlags.HeroInjuredStance): HeroHealth simply
//     never calls SetInjuredVisual(true) when the flag is off, so this stays dark.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>A breathing red screen-edge vignette shown while the hero is at low HP.</summary>
    [DisallowMultipleComponent]
    public sealed class HeroInjuredVignette : MonoBehaviour
    {
        [Header("Vignette feel")]
        // WO-888 DEMOTION: peak 0.42 -> 0.26, floor 0.16 -> 0.08. The world HP aura is the
        // primary read now; this frame supports it instead of carrying it. Turned down rather
        // than removed so the cue stays available to players who can see red (redundancy), and
        // so it stops competing for attention on a 2670x1200 landscape phone where a heavy red
        // frame eats the scarce vertical axis. Serialized bones - felt-tune freely.
        [Tooltip("Peak opacity of the red edge vignette at the trough->crest of the breath (0-1). SECONDARY cue.")]
        [SerializeField, Range(0f, 1f)] private float _peakAlpha = 0.26f;

        [Tooltip("Floor opacity of the breath so the frame never fully clears while injured (0-1). SECONDARY cue.")]
        [SerializeField, Range(0f, 1f)] private float _floorAlpha = 0.08f;

        [Tooltip("Breaths per second (the heartbeat-paced pulse of the vignette).")]
        [SerializeField, Min(0.1f)] private float _pulseHz = 1.1f;

        [Tooltip("Seconds for the vignette to fade in when injured begins / out when healed.")]
        [SerializeField, Min(0.05f)] private float _fade = 0.35f;

        private static readonly Color EdgeColor = new Color(0.72f, 0.04f, 0.04f);

        // The radial edge mask: opaque at the border, transparent at the centre,
        // so the red only ever frames the screen and never tints the play area.
        private Texture2D _edgeTex;

        private bool  _injured;       // target state set by HeroHealth
        private float _envelope;      // 0..1 fade envelope (eases toward _injured)
        private float _phase;         // breath phase accumulator
        private float _severity;      // 0..1 how deep below the injured cutoff (0 at cutoff, 1 at empty)

        /// <summary>Drives the vignette on/off. Called by HeroHealth on the HP threshold cross.</summary>
        public void SetInjured(bool injured) => _injured = injured;

        /// <summary>
        /// Sets how DEEP the wound is (0 at the injured cutoff, 1 at empty HP) so the red edge
        /// frame intensifies as the hero nears death — the "attention needed" escalation. Read-only
        /// health signal from HeroHealth; the vignette holds no game logic. Clamped for safety.
        /// </summary>
        public void SetSeverity(float severity01) => _severity = Mathf.Clamp01(severity01);

        private void OnDisable()
        {
            // Reset so a re-enable (scene reload / respawn) starts dark.
            _injured  = false;
            _envelope = 0f;
            _phase    = 0f;
        }

        private void OnDestroy()
        {
            if (_edgeTex != null) Destroy(_edgeTex);
            _edgeTex = null;
        }

        private void Update()
        {
            // Ease the fade envelope toward the target (in when injured, out when healed).
            float target = _injured ? 1f : 0f;
            float step = Time.unscaledDeltaTime / Mathf.Max(0.05f, _fade);
            _envelope = Mathf.MoveTowards(_envelope, target, step);

            // Advance the breath only while visible (cheap, and keeps phase from drifting).
            if (_envelope > 0f)
                _phase += Time.unscaledDeltaTime * _pulseHz * Mathf.PI * 2f;
        }

        private void OnGUI()
        {
            if (_envelope <= 0f) return;
            EnsureEdgeTexture();
            if (_edgeTex == null) return;

            // Breath: a sine that rides between the floor and the peak, scaled by the
            // fade envelope so it eases in/out cleanly at the edges of the injured state.
            float breath01 = 0.5f + 0.5f * Mathf.Sin(_phase);
            // Severity boost: deepen the frame as HP falls toward zero (0.8x at the cutoff,
            // ~1.25x near-death) so the danger reads louder the closer the hero is to dying.
            float sevBoost = Mathf.Lerp(0.8f, 1.25f, _severity);
            float alpha = Mathf.Clamp01(Mathf.Lerp(_floorAlpha, _peakAlpha, breath01) * _envelope * sevBoost);
            if (alpha <= 0.001f) return;

            var prev = GUI.color;
            GUI.color = new Color(EdgeColor.r, EdgeColor.g, EdgeColor.b, alpha);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _edgeTex);
            GUI.color = prev;
        }

        /// <summary>
        /// Build the radial edge mask once: a small square texture whose alpha is 0 in
        /// the centre and ramps to 1 at the border. IMGUI stretches it over the whole
        /// screen, so a tiny texture gives a smooth full-screen vignette for free.
        /// </summary>
        private void EnsureEdgeTexture()
        {
            if (_edgeTex != null) return;

            const int N = 64;
            _edgeTex = new Texture2D(N, N, TextureFormat.RGBA32, false)
            {
                wrapMode   = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name       = "HeroInjuredVignetteEdge",
            };

            var px = new Color[N * N];
            const float inner = 0.55f;   // radius (0..1) where the red starts ramping in
            const float outer = 1.0f;    // fully opaque at/after this radius
            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    // Normalised distance from centre on the longer "screen-ish" basis.
                    float nx = (x / (float)(N - 1)) * 2f - 1f;
                    float ny = (y / (float)(N - 1)) * 2f - 1f;
                    float r = Mathf.Sqrt(nx * nx + ny * ny) / Mathf.Sqrt(2f);
                    float a = Mathf.InverseLerp(inner, outer, r);
                    a = Mathf.Clamp01(a);
                    a = a * a;   // bias toward the edge for a tighter frame
                    px[y * N + x] = new Color(1f, 1f, 1f, a);
                }
            }
            _edgeTex.SetPixels(px);
            _edgeTex.Apply(false, false);
        }
    }
}
