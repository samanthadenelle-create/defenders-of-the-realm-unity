// =============================================================================
// HeartAuraController -- the persistent sacred aura around the Heart of Elarion.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// PRESENTATION (read-only): a gentle, always-on aura that makes the Heart read as
// "the sacred thing you defend". This is the "add an aura effect to the tree" half
// of the owner request. It ONLY READS HeartController.Hp -- it never heals or
// mutates the Heart (that is HeartRegen's job). Presentation stays a separate layer.
//
// REUSE: plays the canonical VFXType.Aura_HeartPulse loop ("Heart of Elarion
// ambient pulse -- nucleus loop") through the existing VFXManager pool -- the enum
// value already existed for exactly this and was previously unwired. No new art is
// authored; if no prefab is catalogued the manager's procedural aura loop fills in.
//
// COLOR-FREE HEALTH TELL (the owner is colorblind -- never encode meaning by color
// alone; use motion / shape / luminance):
//   * SHAPE/SIZE   -- the aura swells when the Heart is healthy, shrinks when hurt.
//   * LUMINANCE    -- a soft light glows bright when healthy, dim when hurt.
//   * MOTION       -- a slow calm "breath" when healthy accelerates into a fast
//                     anxious flicker as HP falls.
// The light hue is a fixed neutral warm-white and NEVER changes with health, so no
// information is carried by color. A player who sees no color at all still reads the
// Heart's condition from size + brightness + pulse rate.
//
// ATTACH: self-bootstraps onto the HeartController GameObject at runtime (the
// canonical reactive-bridge pattern used by HeartwoodAmbientController) so no
// curated .unity scene is hand-edited.
//
// INSTRUMENTATION (CLAUDE.md section 12): FlowTrace.Step on aura start + on each
// health-tier transition (Healthy / Strained / Critical); FlowTrace.Once/Warn when
// the VFX loop cannot start so a missing manager/catalog self-reports instead of
// the Heart just looking auraless.
// =============================================================================

using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// Drives the Heart's persistent sacred aura -- a pooled Aura_HeartPulse loop plus
    /// a soft glow light, whose size / brightness / pulse-rate read the Heart's health
    /// without relying on color. Read-only: it never mutates <see cref="HeartController"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(HeartController))]
    public sealed class HeartAuraController : MonoBehaviour
    {
        // Health tier, for Step-tracing a color-free readout transition (not for color).
        private enum AuraTier { Unknown, Healthy, Strained, Critical }

        [Header("Heart (auto-wired to the HeartController on this GameObject)")]
        [SerializeField] private HeartController _heart;

        [Header("Aura placement")]
        [Tooltip("Local offset from the Heart pivot where the aura + glow sit (mid-trunk).")]
        [SerializeField] private Vector3 _auraOffset = new Vector3(0f, 1.5f, 0f);

        [Header("Color-free health tell (size)")]
        [Tooltip("Aura scale when the Heart is critically hurt (HP 0).")]
        [SerializeField, Min(0.1f)] private float _scaleHurt = 0.7f;
        [Tooltip("Aura scale when the Heart is fully healthy (HP 100).")]
        [SerializeField, Min(0.1f)] private float _scaleHealthy = 1.15f;

        [Header("Color-free health tell (luminance)")]
        [Tooltip("Glow light intensity when the Heart is critically hurt.")]
        [SerializeField, Min(0f)] private float _lightHurt = 0.6f;
        [Tooltip("Glow light intensity when the Heart is fully healthy.")]
        [SerializeField, Min(0f)] private float _lightHealthy = 2.2f;
        [Tooltip("Glow light range (world units). Constant -- unaffected by the size pulse.")]
        [SerializeField, Min(0f)] private float _lightRange = 9f;

        [Header("Color-free health tell (motion)")]
        [Tooltip("Pulse frequency (Hz) when healthy -- a slow, calm breath.")]
        [SerializeField, Min(0f)] private float _pulseHzHealthy = 0.5f;
        [Tooltip("Pulse frequency (Hz) when hurt -- a fast, anxious flicker.")]
        [SerializeField, Min(0f)] private float _pulseHzHurt = 2.2f;
        [Tooltip("Pulse depth (fraction) when healthy -- barely breathing.")]
        [SerializeField, Range(0f, 1f)] private float _pulseDepthHealthy = 0.12f;
        [Tooltip("Pulse depth (fraction) when hurt -- a hard flicker.")]
        [SerializeField, Range(0f, 1f)] private float _pulseDepthHurt = 0.5f;

        // Fixed neutral warm-white -- the ONE thing that never changes with health, so no
        // meaning is ever carried by color. (Colorblind-safe: hue is a constant.)
        private static readonly Color GlowColor = new Color(1f, 0.96f, 0.9f);

        // Owner VfxManualPick: the Heart of Elarion IS the world tree -- a persistent ambient
        // Tree-of-Life aura loop sits at the trunk, held through the pooled VFXManager (the same
        // PlayKey/VFXHandle held-loop pattern as HealingFountain's HealingFountain_Aura) and
        // Stop()'d on destroy. It is ADDITIVE to the Aura_HeartPulse health-tell nucleus and is
        // parented to the Heart ROOT (not the health-size pivot) so the ambient glow reads
        // constant, unaffected by the color-free size pulse above.
        private const string TreeAuraKey = "TreeofLifeAura_Aura";

        // HP tier cut points for the Step-traced readout (mirrors HeartwoodAmbient tiers).
        private const float HealthyMin  = 75f;
        private const float StrainedMin = 40f;
        private const float FullHp      = 100f;

        private Transform _pivot;
        private Light _light;
        private VFXHandle _handle;
        private VFXHandle _treeHandle;   // persistent Tree-of-Life ambient loop (TreeofLifeAura_Aura)
        private AuraTier _tier = AuraTier.Unknown;
        private float _pulsePhase;   // accumulated so a frequency change never snaps the wave

        // ── CROWN TETHER + STRAY-HEAL FIX (owner felt-test 2026-07-24, on device) ─────────────────
        // Symptom: two auras float in the OPEN FIELD, offset from the visible Tree of Life; the WHITE
        // Aura_HeartPulse swirl reads as the stray "heal VFX" the owner has repeatedly asked be gone
        // from the static town. (The tree DOES render -- it is NOT missing.)
        // RCA (position offset): HeartAuraController lives on the CLEAN scale-1 anchor 'HeartOfElarion';
        // the visible tree is a CHILD 'TreeOfLife_Visual' authored at localScale 7 + localRotation
        // Euler(-90,0,0) (CastleHubBuilder.WireCastleHeart ~:2468-2470). The Tripo fbx geometry is
        // OFFSET from its pivot, so at scale 7 through a -90 X-rotation the rendered trunk/canopy lands
        // METRES from the anchor origin in world XZ. SeatOnGroundOnStart only shifts Y (never XZ), so
        // the tree renders far from the anchor while the auras -- seated at anchor + _auraOffset --
        // sit at the anchor: hence "auras float in the field, offset from the tree".
        // FIX: seat both auras on the tree's LIVE renderer bounds (crown), re-seated each throttle tick
        // so the LATE ground-snap can't strand them; and DO NOT spawn the white swirl on a hub Heart
        // (one that has a visible tree centerpiece). Parenting stays on the scale-1 ANCHOR (never the
        // scale-7 tree, which would inflate the pooled effect 7x) so the health-tell pulse/glow math
        // is unpolluted.
        private const float CrownTrackInterval = 0.5f;   // throttle for the live-crown re-seat
        private bool  _hasTreeBody;                       // a visible non-particle tree renderer exists (hub centerpiece)
        private bool  _suppressWhiteSwirl;                // hub static-town Heart: white Aura_HeartPulse withheld
        private float _crownTrackTimer;
        private bool  _crownReported;                     // §12 Once-trace guard

        // -- Self-bootstrap (attach onto the Heart at runtime; no scene edit) -----

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoadedStatic;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoadedStatic;
            AttachToHearts();
        }

        private static void OnSceneLoadedStatic(
            UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode mode)
            => AttachToHearts();

        private static void AttachToHearts()
        {
            var hearts = Object.FindObjectsByType<HeartController>();
            foreach (var heart in hearts)
            {
                if (heart == null) continue;
                if (heart.GetComponent<HeartAuraController>() == null)
                    heart.gameObject.AddComponent<HeartAuraController>();
            }
        }

        private void Reset() => _heart = GetComponent<HeartController>();

        private void Awake()
        {
            if (_heart == null) _heart = GetComponent<HeartController>();
        }

        private void Start()
        {
            BuildAura();
        }

        private void OnDestroy()
        {
            _handle?.Stop(immediate: true);
            _handle = null;
            _treeHandle?.Stop(immediate: true);
            _treeHandle = null;
        }

        // -- Aura construction ---------------------------------------------------

        private void BuildAura()
        {
            using var _ = FlowTrace.Enter("Heart", $"HeartAura BuildAura on '{name}'");

            // Is this the hub static-town Heart (a visible Tree-of-Life centerpiece under the
            // anchor)? Compute the crown FIRST -- before any pivot/particle is created -- so the
            // renderer scan sees only the tree. When a tree exists the white swirl is withheld and
            // both auras seat on the crown; otherwise the legacy anchor+offset seat is kept.
            _hasTreeBody = TryComputeCrown(out Vector3 crown, out Vector3 canopy);
            _suppressWhiteSwirl = _hasTreeBody;

            // A pivot child hosts the glow + owns the size pulse, so scaling the aura never pollutes
            // the pooled VFX GameObject's own localScale. Parent to the ANCHOR (scale 1, unrotated)
            // so the pulse/glow math is clean; when a tree exists the pivot sits on the canopy (world)
            // instead of anchor+offset, so the glow reads ON the tree, not out in the field.
            var pivotGo = new GameObject("[HeartAuraPivot]");
            _pivot = pivotGo.transform;
            _pivot.SetParent(transform, false);
            if (_hasTreeBody) _pivot.position = canopy;
            else              _pivot.localPosition = _auraOffset;

            // Soft neutral glow -- the luminance channel of the health tell.
            _light = pivotGo.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = GlowColor;
            _light.range = _lightRange;
            _light.intensity = _lightHealthy;
            _light.shadows = LightShadows.None;

            // (A) WHITE pulse nucleus (Aura_HeartPulse -- CLI-assigned "Buff white twist"). Owner
            // 2026-07-24: in the static town it reads as a stray heal VFX -> DO NOT spawn it on a hub
            // Heart. No key swap -- the emitter is simply withheld (VFX no-pick rule intact).
            if (_suppressWhiteSwirl)
                FlowTrace.Step("Heart",
                    $"HeartAura '{name}': hub centerpiece Heart -> WHITE Aura_HeartPulse swirl WITHHELD " +
                    "(owner: stray heal VFX removed from static town). Key unchanged; emitter not spawned.");
            else
                StartWhiteNucleus();

            // (B) GREEN Tree-of-Life ambient loop (TreeofLifeAura_Aura -- OWNER-TAGGED FireFlies; key
            // verbatim, NEVER swapped). Seated tightly on the tree CROWN (live renderer bounds) when a
            // tree exists, else the legacy anchor+offset. Crown-tracked each tick in Update.
            StartGreenTreeAura(_hasTreeBody ? crown : transform.position + _auraOffset);

            // Seed the readout so tier one is traced from the first frame.
            ApplyHealthTell(force: true);
        }

        // -- VFX loop lifecycle ---------------------------------------------------

        /// <summary>Spawn the WHITE Aura_HeartPulse pulse nucleus (CLI-assigned "Buff white twist"),
        /// parented to the health-size pivot. Only called on a NON-hub Heart (a hub centerpiece
        /// withholds it -- owner: stray heal VFX). Key is verbatim; never substituted.</summary>
        private void StartWhiteNucleus()
        {
            if (VFXManager.Instance == null)
            {
                FlowTrace.Once("Heart", $"aura-nomanager:{name}",
                    $"HeartAura '{name}': VFXManager.Instance is null -- the pooled Aura_HeartPulse loop " +
                    "will not appear (the glow light still reads the health tell).");
                return;
            }
            _handle = VFXManager.Instance.PlayAura(VFXType.Aura_HeartPulse, transform);
            if (_handle != null)
            {
                _handle.SetParent(_pivot, worldPositionStays: false);
                FlowTrace.Step("Heart",
                    $"HeartAura '{name}': Aura_HeartPulse nucleus started + parented to pivot (offset {_auraOffset}).");
            }
            else
            {
                FlowTrace.Warn("Heart",
                    $"HeartAura '{name}': PlayAura(Aura_HeartPulse) returned a NULL handle -- loop did not start " +
                    "(loop-cap hit or missing catalog prefab + failed procedural fallback); glow light still active.");
            }
        }

        /// <summary>Spawn the OWNER-TAGGED GREEN Tree-of-Life ambient loop (<see cref="TreeAuraKey"/>
        /// FireFlies) at <paramref name="worldSeat"/> (the tree crown when a centerpiece exists).
        /// Parented to the scale-1 ANCHOR -- NOT the scale-7 tree, which would inflate the pooled
        /// effect 7x -- and crown-tracked in Update via SetPosition. Key is verbatim; never swapped.</summary>
        private void StartGreenTreeAura(Vector3 worldSeat)
        {
            if (VFXManager.Instance == null)
            {
                FlowTrace.Once("Heart", $"tree-aura-nomanager:{name}",
                    $"HeartAura '{name}': VFXManager.Instance is null -- the pooled {TreeAuraKey} loop will not appear.");
                return;
            }
            _treeHandle = VFXManager.PlayKey(
                TreeAuraKey,
                worldSeat,
                Quaternion.identity,
                transform,          // anchor (scale 1) -- clean world scale; crown-tracked via SetPosition
                null,               // keep the authored aura color (no tint)
                1f);
            if (_treeHandle != null)
                FlowTrace.Step("Heart",
                    $"HeartAura '{name}': {TreeAuraKey} FireFlies ambient started at crown {worldSeat:F2} " +
                    $"(treeBody={_hasTreeBody}, anchorPos={transform.position:F2}) -- parented to anchor, crown-tracked.");
            else
                FlowTrace.Warn("Heart",
                    $"HeartAura '{name}': PlayKey('{TreeAuraKey}') returned a NULL handle -- ambient tree loop " +
                    "did not start (loop-cap hit or missing catalog row); the glow still reads.");
        }

        /// <summary>Compute the visible tree's CROWN + CANOPY-centre (world) from its LIVE non-particle
        /// renderer bounds. ParticleSystemRenderers are excluded so the aura's OWN FireFlies never
        /// count. Returns false when the Heart has no visible tree centerpiece (a bare combat/raid
        /// Heart) -> callers fall back to the legacy anchor+offset seat.</summary>
        private bool TryComputeCrown(out Vector3 crown, out Vector3 canopy)
        {
            crown = default; canopy = default;
            var rends = GetComponentsInChildren<Renderer>(false);
            bool have = false; Bounds b = default;
            for (int i = 0; i < rends.Length; i++)
            {
                var r = rends[i];
                if (r == null || r is ParticleSystemRenderer) continue;
                if (!r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (!have) { b = r.bounds; have = true; }
                else b.Encapsulate(r.bounds);
            }
            if (!have) return false;
            // Canopy centre = the enveloping glow seat; crown = high in the canopy for the FireFlies.
            canopy = new Vector3(b.center.x, Mathf.Lerp(b.center.y, b.max.y, 0.5f), b.center.z);
            crown  = new Vector3(b.center.x, Mathf.Lerp(b.center.y, b.max.y, 0.8f), b.center.z);
            return true;
        }

        // -- Per-frame color-free health tell ------------------------------------

        private void Update()
        {
            if (_heart == null) return;
            UpdateCrownTrack();
            ApplyHealthTell(force: false);
        }

        // Keep the tree-seated auras locked on the LIVE crown so the LATE ground-snap (SeatOnGround
        // OnStart re-seats the tree's Y up to ~2.5s after load) can never strand them back in the
        // field. Throttled; no-op on a bare (treeless) Heart. Parenting stays on the scale-1 anchor,
        // so SetPosition writes a clean world position (no scale/rotation pollution from the tree).
        private void UpdateCrownTrack()
        {
            if (!_hasTreeBody) return;
            _crownTrackTimer -= Time.deltaTime;
            if (_crownTrackTimer > 0f) return;
            _crownTrackTimer = CrownTrackInterval;

            if (!TryComputeCrown(out Vector3 crown, out Vector3 canopy)) return;

            if (_pivot != null) _pivot.position = canopy;   // glow follows the canopy centre
            _treeHandle?.SetPosition(crown);                // FireFlies follow the crown

            // §12: prove the tether -- anchor vs crown, and that the white swirl is suppressed.
            if (!_crownReported)
            {
                _crownReported = true;
                FlowTrace.Once("Heart", $"aura-crown:{name}",
                    $"HeartAura '{name}': crown-tether LIVE -> anchorPos={transform.position:F2}, crown={crown:F2}, " +
                    $"canopy={canopy:F2}, whiteSwirlSuppressed={_suppressWhiteSwirl}. Auras now seated on the tree " +
                    "renderer bounds (the offset from the anchor is the RCA and is now corrected).");
            }
        }

        private void ApplyHealthTell(bool force)
        {
            float hp = _heart != null ? _heart.Hp : 0f;
            float hpFrac = Mathf.Clamp01(hp / FullHp);

            // Motion: pulse rate + depth accelerate/deepen as HP falls. Advance the phase
            // by the CURRENT frequency so a rate change is smooth, not a snap.
            float hz    = Mathf.Lerp(_pulseHzHurt,    _pulseHzHealthy,    hpFrac);
            float depth = Mathf.Lerp(_pulseDepthHurt, _pulseDepthHealthy, hpFrac);
            _pulsePhase += Time.deltaTime * hz;
            float pulse = 1f + Mathf.Sin(_pulsePhase * 2f * Mathf.PI) * depth;

            // Luminance: baseline brightness scales with HP, then the pulse rides on top.
            if (_light != null)
            {
                float baseIntensity = Mathf.Lerp(_lightHurt, _lightHealthy, hpFrac);
                _light.intensity = Mathf.Max(0f, baseIntensity * pulse);
            }

            // Shape/size: the aura swells with health; a gentle share of the pulse "breathes".
            if (_pivot != null)
            {
                float baseScale = Mathf.Lerp(_scaleHurt, _scaleHealthy, hpFrac);
                float breath = 1f + (pulse - 1f) * 0.15f;
                _pivot.localScale = Vector3.one * (baseScale * breath);
            }

            // Step-trace only on a tier transition (color-free readout), not per frame.
            AuraTier next = TierForHp(hp);
            if (force || next != _tier)
            {
                _tier = next;
                FlowTrace.Step("Heart",
                    $"HeartAura readout -> {next} at HP {hp:F1}/100 " +
                    $"(size x{Mathf.Lerp(_scaleHurt, _scaleHealthy, hpFrac):F2}, " +
                    $"glow {Mathf.Lerp(_lightHurt, _lightHealthy, hpFrac):F2}, pulse {hz:F2}Hz) -- color-free.");
            }
        }

        private static AuraTier TierForHp(float hp)
        {
            if (hp >= HealthyMin)  return AuraTier.Healthy;
            if (hp >= StrainedMin) return AuraTier.Strained;
            return AuraTier.Critical;
        }
    }
}
