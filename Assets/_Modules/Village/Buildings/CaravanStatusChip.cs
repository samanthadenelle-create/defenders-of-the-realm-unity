// =============================================================================
// CaravanStatusChip — WO-991 slice 1: the ONE status chip over the Healing Caravan.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Design doc (DESIGN_SUGGESTIONS_OPEN_TICKETS_2026-08-15, WO-991 item 5):
//   "One chip: caravan HP + 'following / idle'. No complex relocate UI."
//
// HP half   -> reuses FloatingHealthBar (the existing delegate-driven world-space
//              bar; attached by HealingCaravanMobility.Start, NOT here) so the
//              caravan's glass HP reads in the same visual language as every
//              other combat unit. hideAtFull:false — a support unit you are
//              escorting must be locatable at a glance.
// State half -> this component: a small code-built world-space label directly
//              above the bar reading "FOLLOWING" while the crawl is active and
//              "IDLE" while parked. COLOURBLIND RULE (owner is red/green
//              colourblind): the state reads by TEXT + LUMINANCE (bright while
//              following, dim while idle) — never by hue alone.
//
// ZERO scene wiring: built entirely in code (uGUI world-space Canvas + Text,
// LegacyRuntime.ttf — UXML does not render in player builds). One owner:
// HealingCaravanMobility attaches it and its Die() path destroys the caravan
// GameObject, which tears this chip (and its canvas child) down with it.
// =============================================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.UI;

namespace DeNelle.Village
{
    /// <summary>
    /// World-space "FOLLOWING / IDLE" label over the Healing Caravan (WO-991).
    /// Pairs with the FloatingHealthBar HP half of the one-chip spec. Attached at
    /// runtime by <see cref="HealingCaravanMobility"/>; never scene-wired.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CaravanStatusChip : MonoBehaviour
    {
        // Sits just above the FloatingHealthBar (bar height 2.9 set by the attach
        // site in HealingCaravanMobility) so the pair reads as ONE chip.
        private const float LabelHeight = 3.25f;

        // Luminance pair (colourblind-safe: brightness difference, not hue).
        private static readonly Color FollowingColor = new Color(1f, 0.96f, 0.82f, 1f);   // bright parchment
        private static readonly Color IdleColor      = new Color(0.62f, 0.62f, 0.66f, 0.85f); // dim steel

        private HealingCaravanMobility _mobility;
        private Canvas _canvas;
        private TextMeshProUGUI _label;
        private Transform _cam;
        private bool _lastRolling;
        private bool _built;

        /// <summary>Attach (or reuse) the status chip on the caravan root.</summary>
        public static CaravanStatusChip Attach(HealingCaravanMobility host)
        {
            if (host == null)
            {
                FlowTrace.Warn("Caravan", "CaravanStatusChip.Attach called with null host — no chip");
                return null;
            }
            var chip = host.GetComponent<CaravanStatusChip>();
            if (chip == null) chip = host.gameObject.AddComponent<CaravanStatusChip>();
            chip._mobility = host;
            FlowTrace.Step("Caravan", $"CaravanStatusChip attached on '{host.name}' (state label + HP bar pair)");
            return chip;
        }

        private void Start()
        {
            // Guard the whole build: one bad font/canvas op logs + skips, never a
            // silent blank chip (INSTRUMENTATION_STANDARD Guard rule).
            Guard.Try("Caravan", "CaravanStatusChip.BuildUi", BuildUi);
        }

        private void BuildUi()
        {
            if (_built) return;
            _built = true;

            var canvasGo = new GameObject("CaravanStatusCanvas");
            canvasGo.transform.SetParent(transform, false);
            canvasGo.transform.localPosition = new Vector3(0f, LabelHeight, 0f);

            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            var crt = _canvas.GetComponent<RectTransform>();
            crt.sizeDelta = new Vector2(1.6f, 0.28f);

            // ROUTED THROUGH THE KIT (UI-OBSIDIAN conformance, 2026-08-20). This used to
            // hand-roll a legacy `Text` with Resources.GetBuiltinResource("LegacyRuntime.ttf"),
            // which the conformance oracle flags as a NEW hand-rolled widget — and rightly:
            // it was the only surface in the project still building legacy uGUI text, so it
            // could not inherit a font, palette or style change the rest of the UI got.
            //
            // ElarionUiKit.Label is parent-agnostic — it anchors by FRACTION of whatever
            // RectTransform it is handed — so it drops straight into this WORLD-SPACE canvas
            // with no screen-space assumption. EnsureFont() inside it also removes the TMP
            // first-generation NRE hazard the hand-rolled version had no guard for.
            //
            // The canvas itself stays hand-built: a bare world-space root Canvas is only a
            // WEAK smell in the oracle and is the legitimate pattern for a diegetic tell
            // (same shape as FloatingHealthBar / NodeFillIndicator, both sanctioned).
            _label = ElarionUiKit.Label(
                canvasGo.transform, "IDLE",
                y0: 0f, y1: 1f,
                color: IdleColor, size: 32, align: TextAlignmentOptions.Center,
                x0: 0f, x1: 1f, bold: true);
            _label.enableWordWrapping = false;
            _label.overflowMode = TextOverflowModes.Overflow;
            var labelGo = _label.gameObject;
            // World-space canvas renders sizeDelta as metres; a 32px font needs a
            // small uniform scale to land at ~0.10 m tall text over a ~2 m cart.
            labelGo.transform.localScale = Vector3.one * 0.0035f;

            FlowTrace.Step("Caravan",
                $"CaravanStatusChip UI built h={LabelHeight}m (text+luminance state — colourblind-safe, no hue coding)");
        }

        private void LateUpdate()
        {
            if (_canvas == null || _label == null) return;

            // Owner gone or dead — the caravan GO is being torn down (Die() destroys
            // it); hide immediately so a 0-HP cart never advertises "FOLLOWING".
            if (_mobility == null || !_mobility.IsAlive)
            {
                if (_canvas.enabled) _canvas.enabled = false;
                return;
            }

            bool rolling = _mobility.IsRolling;
            if (rolling != _lastRolling || _label.text.Length == 0)
            {
                _lastRolling = rolling;
                _label.text = rolling ? "FOLLOWING" : "IDLE";
                _label.color = rolling ? FollowingColor : IdleColor;
                FlowTrace.Throttle("Caravan", "chip-state", 2f,
                    $"status chip -> {_label.text}");
            }

            // Upright billboard toward the camera (same yaw-only rule as
            // FloatingHealthBar so the pair never tips edge-on from above).
            if (_cam == null)
            {
                var cm = Camera.main;
                _cam = cm != null ? cm.transform : null;
                if (_cam == null) return;
            }
            Vector3 toCam = _canvas.transform.position - _cam.position;
            Vector3 flat = new Vector3(toCam.x, 0f, toCam.z);
            if (flat.sqrMagnitude > 1e-6f)
                _canvas.transform.rotation = Quaternion.LookRotation(flat, Vector3.up);
        }
    }
}
