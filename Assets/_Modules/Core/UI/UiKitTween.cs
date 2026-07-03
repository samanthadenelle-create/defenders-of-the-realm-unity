// =============================================================================
// UiKitTween — the kit's ONE tiny tween runner (HUD_OBSIDIAN P1 support file).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.UI
//
// Owner smoothness directive 2026-07-02: state changes EASE (0.12s), never snap.
// This runner backs the kit's eased updates: BarHandle.SetValue fill easing,
// CurrencyChip count-tweens, cast-bar Show/Hide fades. Deliberately minimal:
//   - ONE hidden DontDestroyOnLoad host, one Update loop, quad ease-out.
//   - Keyed cancel-and-replace: starting a tween for a key kills the previous
//     one for that key (a bar swept twice never double-drives).
//   - Mobile lens: tween objects are POOLED (no steady-state allocation); the
//     Update loop allocates nothing.
//   - Failure-safe: a destroyed fill Image simply ends its tween (Unity null
//     semantics); outside play mode every call applies the end value directly.
// NOT a general animation system — widgets needing real choreography (CombatText
// rise/fade) run their own Update.
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DeNelle.Core.UI
{
    /// <summary>The kit's single lightweight tween runner (eased 0.12s state changes).</summary>
    public sealed class UiKitTween : MonoBehaviour
    {
        private sealed class Tween
        {
            public object key;               // cancel-and-replace identity
            public Image fill;               // fill mode target (null => value mode)
            public bool isFill;
            public Action<float> onUpdate;   // value mode
            public Action onComplete;
            public float from, to, duration, elapsed;
        }

        private static UiKitTween _instance;
        private readonly List<Tween> _active = new List<Tween>(16);
        private readonly Stack<Tween> _pool = new Stack<Tween>(16);

        private static UiKitTween Host
        {
            get
            {
                if (_instance == null && Application.isPlaying)
                {
                    var go = new GameObject("~UiKitTween");
                    go.hideFlags = HideFlags.HideAndDontSave;
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<UiKitTween>();
                }
                return _instance;
            }
        }

        // ── Public API ─────────────────────────────────────────────────────

        /// <summary>Ease an Image's fillAmount to <paramref name="target"/> (§1.1 — fillAmount is
        /// the ONLY mutation). Outside play mode / zero duration: applied immediately.</summary>
        public static void FillTo(Image img, float target, float duration)
        {
            if (img == null) return;
            if (!Application.isPlaying || duration <= 0f)
            {
                img.fillAmount = target;
                return;
            }
            var host = Host;
            if (host == null) { img.fillAmount = target; return; }
            var t = host.Take(img);
            t.fill = img; t.isFill = true;
            t.from = img.fillAmount; t.to = target;
            t.duration = duration; t.elapsed = 0f;
            host._active.Add(t);
        }

        /// <summary>Cancel any live fill tween on the Image (leaves fillAmount where it is).</summary>
        public static void CancelFill(Image img)
        {
            if (_instance != null && img != null) _instance.Kill(img);
        }

        /// <summary>Ease an arbitrary float and push it through <paramref name="onUpdate"/> — count
        /// tweens, alpha fades. Keyed: a new tween for the same key replaces the old one.</summary>
        public static void Value(object key, float from, float to, float duration,
                                 Action<float> onUpdate, Action onComplete = null)
        {
            if (onUpdate == null) return;
            if (!Application.isPlaying || duration <= 0f)
            {
                onUpdate(to);
                onComplete?.Invoke();
                return;
            }
            var host = Host;
            if (host == null) { onUpdate(to); onComplete?.Invoke(); return; }
            var t = host.Take(key);
            t.onUpdate = onUpdate; t.onComplete = onComplete; t.isFill = false;
            t.from = from; t.to = to;
            t.duration = duration; t.elapsed = 0f;
            host._active.Add(t);
        }

        /// <summary>Cancel any live tween for a key.</summary>
        public static void Cancel(object key)
        {
            if (_instance != null && key != null) _instance.Kill(key);
        }

        // ── Internals ──────────────────────────────────────────────────────

        private Tween Take(object key)
        {
            Kill(key);   // cancel-and-replace
            var t = _pool.Count > 0 ? _pool.Pop() : new Tween();
            t.key = key;
            return t;
        }

        private void Kill(object key)
        {
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (!ReferenceEquals(_active[i].key, key)) continue;
                Recycle(i);
            }
        }

        private void Recycle(int index)
        {
            var t = _active[index];
            t.key = null; t.fill = null; t.onUpdate = null; t.onComplete = null;
            _active.RemoveAt(index);
            _pool.Push(t);
        }

        private static float EaseOutQuad(float x) { float inv = 1f - x; return 1f - inv * inv; }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var t = _active[i];
                t.elapsed += dt;
                float k = t.duration <= 0f ? 1f : Mathf.Clamp01(t.elapsed / t.duration);
                float v = Mathf.LerpUnclamped(t.from, t.to, EaseOutQuad(k));

                if (t.isFill)
                {
                    if (t.fill == null) { Recycle(i); continue; }   // target destroyed mid-tween
                    t.fill.fillAmount = v;
                }
                else
                {
                    t.onUpdate?.Invoke(v);
                }

                if (k >= 1f)
                {
                    var done = t.onComplete;
                    Recycle(i);
                    done?.Invoke();
                }
            }
        }
    }
}
