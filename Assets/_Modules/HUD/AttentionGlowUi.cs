using UnityEngine;
using UnityEngine.UI;

namespace DeNelle.HUD
{
    /// <summary>
    /// Subtle "chasing comet" for a UI element — a bright head with a fading, shrinking tail that
    /// travels around the target RectTransform's border. Reusable attention cue (talk box, a heal
    /// hint, a parry window, a tutorial step). Show/hide via the GameObject's active state.
    /// </summary>
    public sealed class AttentionGlowUi : MonoBehaviour
    {
        private RectTransform _target;
        private RectTransform[] _seg;
        private Image[] _img;
        private Color _tint;
        private float _t;

        public float Speed       = 0.45f;    // laps per second
        public int   TailCount   = 10;       // head + trail segments
        public float TailSpacing = 0.022f;   // gap between segments, in perimeter fraction

        public static AttentionGlowUi Attach(RectTransform target, Color tint, Sprite dot)
        {
            var go = new GameObject("AttentionGlowUi", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(target, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = Vector2.zero;
            var g = go.AddComponent<AttentionGlowUi>();
            g._target = target; g._tint = tint;
            g.Build(rt, dot);
            return g;
        }

        private void Build(RectTransform self, Sprite dot)
        {
            _seg = new RectTransform[TailCount];
            _img = new Image[TailCount];
            for (int i = 0; i < TailCount; i++)
            {
                var go = new GameObject("Seg" + i, typeof(RectTransform));
                var rt = (RectTransform)go.transform;
                rt.SetParent(self, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                float s = Mathf.Lerp(15f, 6f, i / (float)(TailCount - 1));   // head, tail tapers (thinner)
                rt.sizeDelta = new Vector2(s, s);
                var img = go.AddComponent<Image>();
                if (dot != null) img.sprite = dot;                       // null-guard the disc sprite
                img.raycastTarget = false;
                img.color = new Color(_tint.r, _tint.g, _tint.b, 0f);    // start invisible — Update fades it in (no first-frame black flash)
                _seg[i] = rt; _img[i] = img;
            }
        }

        private void Update()
        {
            if (_target == null) return;
            _t += Speed * Time.deltaTime;
            Vector2 size = _target.rect.size;
            for (int i = 0; i < TailCount; i++)
            {
                float ti = _t - i * TailSpacing;
                ti -= Mathf.Floor(ti);                                   // wrap 0..1
                _seg[i].anchoredPosition = PerimeterPoint(ti, size.x, size.y);
                float a = Mathf.Pow(1f - i / (float)TailCount, 1.6f);    // bright head → faded tail
                var c = _tint; c.a *= a; _img[i].color = c;
            }
        }

        // Point on the rect border at fraction t (0..1), relative to centre.
        private static Vector2 PerimeterPoint(float t, float w, float h)
        {
            float hw = w * 0.5f, hh = h * 0.5f, per = 2f * (w + h), d = t * per;
            if (d < w) return new Vector2(-hw + d, hh);   d -= w;        // top  L→R
            if (d < h) return new Vector2(hw, hh - d);    d -= h;        // right T→B
            if (d < w) return new Vector2(hw - d, -hh);   d -= w;        // bottom R→L
            return new Vector2(-hw, -hh + d);                            // left B→T
        }
    }
}
