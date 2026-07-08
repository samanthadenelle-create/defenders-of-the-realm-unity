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
using DeNelle.Core.Diagnostics;   // F8-16: FlowTrace on the enemy-tick data path (HUD -> Core edge only)

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
        // F8-16: 4px hairlines over a 120° fan were imperceptible — the enemy mark is now a
        // 10px-wide RED TRIANGLE PIP (apex up), a distinct SHAPE vs the gold objective needle
        // (owner is red/green colorblind — meaning never by color alone, §7 canon).
        private const float TickWidthPx  = 10f;
        private const float ProviderPollInterval = 0.25f;   // reflection scans ~4 Hz, never per-frame

        // ── Runtime refs ──────────────────────────────────────────────────────
        private RectTransform _strip;
        private RectTransform _tickLayer;   // clipped layer holding enemy ticks + the objective chevron
        private TextMeshProUGUI _cardinal;
        private RectTransform _objMarker;    // gold objective chevron (legacy; superseded by the needle)
        private TextMeshProUGUI _objGlyph;
        private RectTransform _needle;       // WO-438: rotating gold needle (points to the objective bearing)
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

            // WO-438: the compass is now a compact Blink-Obsidian OCTAGON (dark octagon frame + gold
            // rim + centred cardinal label + a rotating gold needle) built from the shared kit helper,
            // replacing the old wide dark-glass strip. A compact centred square region in the mount.
            // TODO(dedup): there are TWO compass implementations — this HUD-kit one and the standalone
            // DeNelle.HUD.CompassHud (uGUI NSEW strip, its own canvas). The CLI should reconcile them
            // (this kit widget is the intended survivor per the header); left untouched here to avoid
            // removing a live widget without proof.
            _strip = NewRect("CompassOct", (RectTransform)transform);
            _strip.anchorMin = new Vector2(0.42f, 0.34f);
            _strip.anchorMax = new Vector2(0.58f, 0.99f);
            _strip.offsetMin = Vector2.zero; _strip.offsetMax = Vector2.zero;

            var compass = ElarionUiKit.BuildCompass(_strip, Vector2.zero, Vector2.one);
            _cardinal = compass.cardinal;
            _needle   = compass.needle;
            if (_cardinal != null) _cardinal.characterSpacing = 6f;

            // Marker layer (enemy threat pips): clipped to the octagon so pips never spill past it.
            // F8-16: the band now spans the strip's FULL usable width (was x 0.12–0.88 — the 120° fan
            // was squeezed into ~12% of widget width and marks were imperceptible) and is tall enough
            // to hold a clearly-visible triangle pip. Above the octagon face, below the cardinal label.
            var layerGo = new GameObject("CompassMarkers", typeof(RectTransform), typeof(RectMask2D));
            layerGo.transform.SetParent(_strip, false);
            _tickLayer = layerGo.GetComponent<RectTransform>();
            _tickLayer.anchorMin = new Vector2(0.02f, 0.36f); _tickLayer.anchorMax = new Vector2(0.98f, 0.64f);
            _tickLayer.offsetMin = Vector2.zero; _tickLayer.offsetMax = Vector2.zero;

            // Legacy objective chevron kept but DISABLED — the rotating needle is the objective cue now.
            _objMarker = NewRect("ObjectiveMarker", _tickLayer);
            _objMarker.anchorMin = new Vector2(0.5f, 0.5f);
            _objMarker.anchorMax = new Vector2(0.5f, 0.5f);
            _objMarker.pivot     = new Vector2(0.5f, 0.5f);
            _objMarker.sizeDelta = new Vector2(22f, 22f);
            _objGlyph = AddText(_objMarker, "▲", 20f, ElarionUi.Gilt, TextAlignmentOptions.Center);
            _objGlyph.fontStyle = FontStyles.Bold;
            _objMarker.gameObject.SetActive(false);
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

        // WO-438: rotate the gold NEEDLE to the objective's bearing relative to the heading
        // (0 = straight ahead/up, + = clockwise/right). Hidden when there is no objective.
        private void UpdateObjective(Vector3 fwd)
        {
            if (_needle == null) return;
            if (_objective == null || _hero == null || !_hero)
            {
                if (_needle.gameObject.activeSelf) _needle.gameObject.SetActive(false);
                return;
            }

            Vector3 to = _objective.Value - _hero.position; to.y = 0f;
            if (to.sqrMagnitude < 1e-4f)
            {
                if (_needle.gameObject.activeSelf) _needle.gameObject.SetActive(false);
                return;
            }
            to.Normalize();

            float bearing = Vector3.SignedAngle(fwd, to, Vector3.up);   // + = to the right
            if (!_needle.gameObject.activeSelf) _needle.gameObject.SetActive(true);
            // Tip points to the objective bearing (negate: uGUI +Z is counter-clockwise).
            _needle.localRotation = Quaternion.Euler(0f, 0f, -bearing);
        }

        private void UpdateEnemyTicks(Vector3 fwd)
        {
            if (_tickLayer == null || _hero == null || !_hero) { HideAllTicks(); return; }

            int n = _enemyBuf.Count;
            EnsureTickPool(n);
            Vector3 heroPos = _hero.position;
            int clampedCount = 0;   // enemies outside the ±60° fan this frame (edge-arrow mode)

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
                float x = BearingToStripX(bearing, out bool clamped);
                if (!tick.gameObject.activeSelf) tick.gameObject.SetActive(true);
                tick.anchoredPosition = new Vector2(x, 0f);

                // F8-16 edge-arrow port (old CompassHud.UpdateArrows): an enemy OUTSIDE the fan
                // pins to the band edge with the triangle ROTATED to point the shorter way around
                // (apex-up sprite: -90° = points right, +90° = points left). Inside the fan the
                // pip stands apex-up at its bearing. Same pooled Image — zero extra alloc.
                if (clamped)
                {
                    clampedCount++;
                    tick.localRotation = Quaternion.Euler(0f, 0f, bearing > 0f ? -90f : 90f);
                }
                else if (tick.localRotation != Quaternion.identity)
                {
                    tick.localRotation = Quaternion.identity;
                }
            }

            // One Step on the ENGAGE transition only (never per-frame spam).
            if (clampedCount > 0 && _lastClampedCount == 0)
                FlowTrace.Step("Compass", $"edge-arrows engaged ({clampedCount} enemies outside the ±{FovDegrees * 0.5f:0}° fan).");
            _lastClampedCount = clampedCount;
        }

        private int _lastClampedCount;

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

        private int _lastEnemyCount = -1;

        private void RefreshEnemies()
        {
            _enemyBuf.Clear();
            var list = EnemyProvider != null ? EnemyProvider() : null;
            if (list != null)
                for (int i = 0; i < list.Count; i++)
                    if (list[i] != null) _enemyBuf.Add(list[i]);

            // F8-16 instrumentation ("red ticks exist but the owner reports none render"):
            // log ON CHANGE only (never per-poll) so the next capture PROVES which half fails —
            // data-empty (count stays 0 => provider/type resolution) vs built-but-invisible
            // (count > 0 while the owner sees nothing => a render/layout issue in this widget).
            if (_enemyBuf.Count != _lastEnemyCount)
            {
                FlowTrace.Step("Compass",
                    $"enemy tick source count {_lastEnemyCount} -> {_enemyBuf.Count} (provider={(EnemyProvider != null ? "wired" : "NULL")}, hero={(_hero != null ? "ok" : "NULL")}).");
                _lastEnemyCount = _enemyBuf.Count;
            }
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
                // F8-16 hardening (kept from the earlier landing): the layer rect can still be
                // 0-height when the FIRST enemies appear on the same frame the pool grows (layout
                // not yet run) — the old Mathf.Max(4f, ...) then baked a 4x4px sliver FOREVER
                // (sizeDelta is set once per pooled tick). Floor at 16px so a pip is never
                // sub-visible on mobile.
                rt.sizeDelta = new Vector2(TickWidthPx, Mathf.Max(16f, _tickLayer.rect.height - 4f));
                var img = go.GetComponent<Image>();
                // F8-16 colorblind-safe SHAPE: red apex-up TRIANGLE pip (procedural sprite,
                // built once, static-cached) — a distinct silhouette vs the gold objective
                // needle even with red/green desaturated. Doubles as the edge ARROW when
                // rotated ±90° by UpdateEnemyTicks.
                img.sprite = EnemyPipSprite();
                img.color = ElarionUi.Danger;
                img.raycastTarget = false;
                go.SetActive(false);
                _tickPool.Add(rt);
            }
        }

        // Procedural white apex-up triangle, tinted by Image.color. Built ONCE per app run
        // (static cache, HideAndDontSave) — no per-frame or per-tick allocation.
        private static Sprite _pipSprite;
        private static Sprite EnemyPipSprite()
        {
            if (_pipSprite != null) return _pipSprite;
            const int W = 16, H = 16;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var px = new Color32[W * H];
            float cx = (W - 1) * 0.5f;
            for (int y = 0; y < H; y++)
            {
                float t = (float)y / (H - 1);                 // 0 = bottom (wide) .. 1 = top (apex)
                float half = (1f - t) * cx;
                for (int x = 0; x < W; x++)
                {
                    bool inside = Mathf.Abs(x - cx) <= half;
                    px[y * W + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            _pipSprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect);
            _pipSprite.hideFlags = HideFlags.HideAndDontSave;
            return _pipSprite;
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
