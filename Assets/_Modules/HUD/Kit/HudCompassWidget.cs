// =============================================================================
// HudCompassWidget — the COMMON, reusable compass widget for the HUD kit (A4).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.HUD   Namespace: DeNelle.HUD.Kit
//
// Owner redirect (2026-07-03): the compass must be a REUSABLE kit widget dropped
// into the HUD 9-slice via the posture occupancy rows — NOT a bespoke standalone
// canvas. This replaces the old DeNelle.HUD.CompassHud (its own ScreenSpaceOverlay
// canvas, spawned by CompassHudBootstrap), which was fragile: a scene-load spawn
// RACE (spawned only if a hero already existed at that instant, no retry), no
// DontDestroyOnLoad (died on any single-mode scene load), and NO objective bearing.
// Built into the kit, the compass now inherits HudKitController's ONE persistent
// canvas + posture-driven visibility, so it shows in BOTH calm(town) and
// calm(explore) the same way every other widget does (hud-areas.json rows).
//
// PRESENTATION-ONLY (§5 / owner): it READS heading + world positions, owns NO
// game state. Three provider delegates (wired by HudKitController via reflection,
// so DeNelle.HUD keeps its "HUD -> Core only" edge — no DeNelle.Village ref):
//   • HeroProvider      — the hero Transform (bearing origin + heading fallback).
//   • ObjectiveProvider — world pos of the nearest objective / region-gate seam
//     (the navigation cue the owner asked for: "favor navigation over currency").
//   • EnemyProvider     — live enemy transforms (threat bearing ticks on the strip).
// Providers are polled on a cheap throttle (reflection scans stay ~4 Hz), never
// per-frame. Heading itself comes from Camera.main's flattened forward each frame
// ("up on screen = where you're heading"), hero forward as the no-camera fallback.
//
// The strip is a compact top-center band: a dark-glass chip + gold rim, a centred
// cardinal label (N/NE/E/…), a GOLD objective chevron that slides to the seam's
// bearing (clamped to the fan edge so it never disappears), and red enemy ticks.
// Mobile-first + compact; matches the shared ElarionUi kit language.
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DeNelle.Core.UI;

namespace DeNelle.HUD.Kit
{
    /// <summary>The common HUD-kit compass widget (see header). Built by HudKitController,
    /// placed by the hud-areas.json occupancy rows, driven by provider delegates.</summary>
    [DisallowMultipleComponent]
    public sealed class HudCompassWidget : MonoBehaviour
    {
        // ── Provider seams (set by HudKitController; presentation reads the world) ──
        /// <summary>The hero transform — bearing origin + heading fallback.</summary>
        public Func<Transform> HeroProvider;
        /// <summary>World position of the nearest objective / region-gate seam, or null.</summary>
        public Func<Vector3?> ObjectiveProvider;
        /// <summary>Live enemy transforms for the threat ticks (may be null/empty).</summary>
        public Func<IReadOnlyList<Transform>> EnemyProvider;

        // The strip plots a ±FovDegrees fan centred on the heading; markers outside the
        // fan clamp to the nearest edge so they stay visible.
        private const float FovDegrees   = 120f;
        private const float TickWidthPx  = 4f;
        private const float ProviderPollInterval = 0.25f;   // reflection scans ~4 Hz, never per-frame

        // ── Runtime refs ──────────────────────────────────────────────────────
        private RectTransform _strip;
        private RectTransform _tickLayer;   // clipped layer holding enemy ticks + the objective chevron
        private TextMeshProUGUI _cardinal;
        private RectTransform _objMarker;    // gold objective chevron
        private TextMeshProUGUI _objGlyph;
        private readonly List<RectTransform> _tickPool = new List<RectTransform>();
        private readonly List<Transform> _enemyBuf = new List<Transform>();
        private Vector3? _objective;
        private Transform _hero;
        private Camera _camera;
        private float _pollTimer;
        private bool _built;

        /// <summary>The last-resolved hero (so a provider closure can read it without re-scanning).</summary>
        public Transform Hero => _hero;

        /// <summary>Kit-builder factory: create the compass under a parent RectTransform
        /// (HudKitController wraps + registers it like every other widget).</summary>
        public static HudCompassWidget Create(Transform parent)
        {
            var go = new GameObject("CompassWidget", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var w = go.AddComponent<HudCompassWidget>();
            w.Build();
            return w;
        }

        private void Build()
        {
            if (_built) return;
            _built = true;

            // ── Compass strip: a thin top-centre band within the mount ──
            _strip = NewRect("CompassStrip", (RectTransform)transform);
            _strip.anchorMin = new Vector2(0.06f, 0.60f);
            _strip.anchorMax = new Vector2(0.94f, 0.98f);
            _strip.offsetMin = Vector2.zero; _strip.offsetMax = Vector2.zero;

            // Dark-glass chip — the shared kit's primary panel glass tint.
            var stripBg = _strip.gameObject.AddComponent<Image>();
            stripBg.color = ElarionUiKit.Glass;
            ElarionUiKit.ApplyRounded(stripBg);
            stripBg.raycastTarget = false;

            // Thin faint-gold rim (a slightly larger plate behind the chip).
            var rim = NewRect("CompassRim", _strip);
            rim.anchorMin = Vector2.zero; rim.anchorMax = Vector2.one;
            rim.offsetMin = new Vector2(-1f, -1f);
            rim.offsetMax = new Vector2(1f, 1f);
            rim.SetAsFirstSibling();
            var rimImg = rim.gameObject.AddComponent<Image>();
            rimImg.color = ElarionUiKit.AccentSoft;
            ElarionUiKit.ApplyRounded(rimImg);
            rimImg.raycastTarget = false;

            // Marker layer (enemy ticks + objective chevron): clipped to the strip so
            // markers slide along it and never spill past the chip. Below the cardinal
            // label so the heading text stays legible.
            var layerGo = new GameObject("CompassMarkers", typeof(RectTransform), typeof(RectMask2D));
            layerGo.transform.SetParent(_strip, false);
            _tickLayer = layerGo.GetComponent<RectTransform>();
            _tickLayer.anchorMin = Vector2.zero; _tickLayer.anchorMax = Vector2.one;
            _tickLayer.offsetMin = new Vector2(2f, 2f);
            _tickLayer.offsetMax = new Vector2(-2f, -2f);

            // Gold objective chevron — the navigation cue (nearest region-gate / seam).
            _objMarker = NewRect("ObjectiveMarker", _tickLayer);
            _objMarker.anchorMin = new Vector2(0.5f, 0.5f);
            _objMarker.anchorMax = new Vector2(0.5f, 0.5f);
            _objMarker.pivot     = new Vector2(0.5f, 0.5f);
            _objMarker.sizeDelta = new Vector2(22f, 22f);
            _objGlyph = AddText(_objMarker, "▲", 20f, ElarionUi.Gilt, TextAlignmentOptions.Center);
            _objGlyph.fontStyle = FontStyles.Bold;
            _objMarker.gameObject.SetActive(false);

            // Centred cardinal label.
            _cardinal = AddText(_strip, "N", 18f, ElarionUi.Parchment, TextAlignmentOptions.Center);
            _cardinal.fontStyle = FontStyles.Bold;
            _cardinal.characterSpacing = 12f;
        }

        private void LateUpdate()
        {
            if (!_built) return;
            if (_camera == null) _camera = Camera.main;

            // Cheap throttled provider polls (reflection scans stay off the hot path).
            _pollTimer -= Time.unscaledDeltaTime;
            if (_pollTimer <= 0f)
            {
                _pollTimer = ProviderPollInterval;
                if (HeroProvider != null && (_hero == null || !_hero)) _hero = HeroProvider();
                else if (HeroProvider != null) _hero = HeroProvider();   // keep fresh across scene swaps
                _objective = ObjectiveProvider != null ? ObjectiveProvider() : (Vector3?)null;
                RefreshEnemies();
            }

            Vector3 fwd = HeadingForward();
            UpdateCardinal(fwd);
            UpdateObjective(fwd);
            UpdateEnemyTicks(fwd);
        }

        // Heading = camera flattened forward ("up on screen = where you're heading");
        // hero forward is the fallback when there is no camera.
        private Vector3 HeadingForward()
        {
            Vector3 fwd;
            if (_camera != null)   fwd = _camera.transform.forward;
            else if (_hero != null) fwd = _hero.forward;
            else                    fwd = Vector3.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            return fwd.normalized;
        }

        // Clockwise bearing from +Z (North): +Z=N, +X=E, -Z=S, -X=W.
        private void UpdateCardinal(Vector3 fwd)
        {
            if (_cardinal == null) return;
            float yaw = Mathf.Atan2(fwd.x, fwd.z) * Mathf.Rad2Deg;
            if (yaw < 0f) yaw += 360f;

            string heading;
            if      (yaw < 22.5f  || yaw >= 337.5f) heading = "N";
            else if (yaw < 67.5f)                   heading = "NE";
            else if (yaw < 112.5f)                  heading = "E";
            else if (yaw < 157.5f)                  heading = "SE";
            else if (yaw < 202.5f)                  heading = "S";
            else if (yaw < 247.5f)                  heading = "SW";
            else if (yaw < 292.5f)                  heading = "W";
            else                                    heading = "NW";
#if UNITY_EDITOR
            _cardinal.text = heading + "   (" + Mathf.RoundToInt(yaw) + "°)";
#else
            _cardinal.text = heading;
#endif
        }

        // Slide the gold chevron to the objective's bearing relative to the heading.
        private void UpdateObjective(Vector3 fwd)
        {
            if (_objMarker == null || _tickLayer == null) return;
            if (_objective == null || _hero == null || !_hero)
            {
                if (_objMarker.gameObject.activeSelf) _objMarker.gameObject.SetActive(false);
                return;
            }

            Vector3 to = _objective.Value - _hero.position; to.y = 0f;
            if (to.sqrMagnitude < 1e-4f)
            {
                if (_objMarker.gameObject.activeSelf) _objMarker.gameObject.SetActive(false);
                return;
            }
            to.Normalize();

            float bearing = Vector3.SignedAngle(fwd, to, Vector3.up);   // + = to the right
            float x = BearingToStripX(bearing, out bool clamped);

            if (!_objMarker.gameObject.activeSelf) _objMarker.gameObject.SetActive(true);
            _objMarker.anchoredPosition = new Vector2(x, 0f);
            // Point the chevron up when the objective is within the fan; tilt toward the
            // clamped edge when it is off-screen behind/beside you (a "turn this way" cue).
            float tilt = clamped ? (bearing >= 0f ? -70f : 70f) : 0f;
            _objMarker.localRotation = Quaternion.Euler(0f, 0f, tilt);
        }

        private void UpdateEnemyTicks(Vector3 fwd)
        {
            if (_tickLayer == null || _hero == null || !_hero) { HideAllTicks(); return; }

            int n = _enemyBuf.Count;
            EnsureTickPool(n);
            Vector3 heroPos = _hero.position;

            for (int i = 0; i < _tickPool.Count; i++)
            {
                var tick = _tickPool[i];
                if (i >= n || _enemyBuf[i] == null)
                {
                    if (tick.gameObject.activeSelf) tick.gameObject.SetActive(false);
                    continue;
                }
                Vector3 to = _enemyBuf[i].position - heroPos; to.y = 0f;
                if (to.sqrMagnitude < 1e-4f) { if (tick.gameObject.activeSelf) tick.gameObject.SetActive(false); continue; }
                to.Normalize();

                float bearing = Vector3.SignedAngle(fwd, to, Vector3.up);
                float x = BearingToStripX(bearing, out _);
                if (!tick.gameObject.activeSelf) tick.gameObject.SetActive(true);
                tick.anchoredPosition = new Vector2(x, 0f);
            }
        }

        // Map a signed bearing (deg, + = right) to an X offset across the strip width,
        // clamping off-fan bearings to the nearest edge (so a marker never disappears).
        private float BearingToStripX(float bearing, out bool clamped)
        {
            float halfFov = FovDegrees * 0.5f;
            clamped = bearing < -halfFov || bearing > halfFov;
            float c = Mathf.Clamp(bearing, -halfFov, halfFov);
            float t = (c + halfFov) / FovDegrees;                 // 0..1 across the strip
            float stripW = _tickLayer != null ? _tickLayer.rect.width : 0f;
            return (t - 0.5f) * stripW;
        }

        private void RefreshEnemies()
        {
            _enemyBuf.Clear();
            var list = EnemyProvider != null ? EnemyProvider() : null;
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null) _enemyBuf.Add(list[i]);
        }

        private void HideAllTicks()
        {
            for (int i = 0; i < _tickPool.Count; i++)
                if (_tickPool[i].gameObject.activeSelf) _tickPool[i].gameObject.SetActive(false);
        }

        private void EnsureTickPool(int count)
        {
            while (_tickPool.Count < count)
            {
                var go = new GameObject("EnemyTick", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(_tickLayer, false);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot     = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(TickWidthPx, Mathf.Max(4f, _tickLayer.rect.height - 6f));
                var img = go.GetComponent<Image>();
                img.color = ElarionUi.Danger;
                img.raycastTarget = false;
                go.SetActive(false);
                _tickPool.Add(rt);
            }
        }

        // ── uGUI helpers (mirror the kit conventions) ──────────────────────────
        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<RectTransform>();
        }

        private static TextMeshProUGUI AddText(Transform parent, string text, float size,
            Color color, TextAlignmentOptions align)
        {
            var go = new GameObject("Txt");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TMPro.TextWrappingModes.NoWrap;
            tmp.overflowMode = TextOverflowModes.Overflow;
            return tmp;
        }
    }
}
