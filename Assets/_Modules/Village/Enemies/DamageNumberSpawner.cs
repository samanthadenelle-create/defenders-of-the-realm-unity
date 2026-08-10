// =============================================================================
// DamageNumberSpawner — asset-free floating combat text for enemy hits.
// -----------------------------------------------------------------------------
// When a hero ability, pet or tower lands damage on an Enemy (everything routes
// through Enemy.TakeDamage(float)), we pop a world-space integer that floats up
// from the enemy's head and fades out, then self-destroys. This is the visible
// confirmation the player needs to SEE damage rise after unlocking a damage
// talent — bigger, brighter numbers for bigger hits.
//
// FULLY CODE-BUILT, NO ASSETS: the spawner mirrors the project's existing
// world-space text idiom (a TextMesh with characterSize / anchor / alignment,
// billboarded to the camera each LateUpdate — same shape as the dungeon portal
// sign and TownsfolkBubble). Nothing here needs a prefab, scene wiring or a
// PanelSettings, so it works in a fresh headless build with zero setup.
//
// CHEAP: each number is a short-lived GameObject that animates from cached
// start values (no per-frame allocation, no GetComponent in Update) and Destroys
// itself after the lifetime. The only allocation per hit is the GameObject +
// its TextMesh + the int->string of the damage value, which is unavoidable for
// floating text and is bounded by the hit rate.
//
// CAMERA-SAFE: spawning and billboarding both null-guard Camera.main, so a frame
// with no active camera simply skips the float without throwing.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Diagnostics;   // WO-219: FlowTrace on feedback-spawned

namespace DeNelle.Village
{
    /// <summary>
    /// A single floating damage number. Built entirely in code by
    /// <see cref="Spawn"/> — a billboarded world-space <see cref="TextMesh"/>
    /// that rises while fading out, then destroys itself. No prefab or scene
    /// wiring required.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DamageNumberSpawner : MonoBehaviour
    {
        // ── Animation tuning ──────────────────────────────────────────────────

        /// <summary>Total seconds the number lives before it is destroyed.
        /// DEF-260 #6: shortened from 0.8s so the number pops and clears faster
        /// (owner: "fade faster").</summary>
        private const float Lifetime = 0.55f;

        /// <summary>World-units the number rises over its <see cref="Lifetime"/>.</summary>
        private const float RiseDistance = 1.0f;

        /// <summary>
        /// Base character size of the TextMesh (matches the village's other
        /// world-space text). Scaled up a little for bigger hits.
        /// DEF-260 #6: reduced from 0.18 — the prior "18" read as oversized on the
        /// PatriciaLight stage; this keeps numbers legible but no longer dominant.
        /// </summary>
        private const float BaseCharacterSize = 0.11f;

        /// <summary>Damage value that maps to a "full size" number (caps the scale ramp).</summary>
        private const float BigHitDamage = 40f;

        // Normal-hit colour (warm gold) and big-hit colour (hot orange-red). We
        // lerp between them by hit magnitude so a talent-buffed hit reads brighter
        // as well as bigger. No crit flag is exposed on the damage path, so we do
        // NOT invent one — magnitude is the honest signal we have.
        private static readonly Color NormalColor = new Color(1.0f, 0.92f, 0.45f, 1f);
        private static readonly Color BigColor    = new Color(1.0f, 0.45f, 0.20f, 1f);

        // ── Cached per-instance animation state ───────────────────────────────

        private TextMesh _text;
        private Transform _tf;
        private Camera _faceCamera;
        private Vector3 _startPos;
        private Color _startColor;
        private float _baseScale;
        private float _age;
        private float _lifetime = Lifetime;   // overridable so labels can linger longer
        private float _rise = RiseDistance;

        /// <summary>
        /// Spawns a floating damage number for <paramref name="amount"/> at
        /// <paramref name="worldPos"/>. No-op (returns null) when there is no
        /// active camera. Everything is built in code — call it and forget it.
        /// </summary>
        /// <param name="amount">Damage dealt; shown as a rounded integer.</param>
        /// <param name="worldPos">World position to float up from (the enemy's head).</param>
        public static DamageNumberSpawner Spawn(float amount, Vector3 worldPos)
        {
            if (amount <= 0f) return null;

            // Guard the camera up front — no point building a billboard with
            // nothing to face. Cheap early-out for any off-screen / no-camera frame.
            Camera cam = Camera.main;
            if (cam == null)
            {
                // §12 entry trace: a missing Camera.main makes EVERY damage number silently
                // skip — the player sees zero combat numbers. Once-report so a no-camera frame
                // self-detects instead of reading as "numbers feature broken".
                FlowTrace.Once("Feedback", "dmg-nocam",
                    "Spawn: Camera.main is null — damage numbers will not appear this frame.");
                return null;
            }

            // POOLED: reuse a dormant number (GameObject + TextMesh + material) instead
            // of new GameObject / AddComponent / Destroy per hit. In a busy wave this is
            // a hit-rate GC source; the pool drops it to ~zero in steady state. Same
            // shape as VfxPool — SetActive cycle under a DontDestroyOnLoad root.
            var num = Acquire(worldPos);
            num.Build(amount, cam);
            // WO-219 §12: confirm the visual-feedback layer spawned a damage number.
            // Throttled — a busy wave pops many per second; the trend is enough.
            FlowTrace.Throttle("Feedback", "dmg-num", 1f,
                $"damage number spawned amount={Mathf.RoundToInt(amount)}");
            return num;
        }

        /// <summary>As <see cref="Spawn(float,Vector3)"/> but tints the number to
        /// <paramref name="color"/> (source-coded: hero hits vs pet hits).</summary>
        public static DamageNumberSpawner Spawn(float amount, Vector3 worldPos, Color color)
        {
            var num = Spawn(amount, worldPos);
            if (num != null) num.ApplyColor(color);
            return num;
        }

        /// <summary>Overrides the built number's colour (source tint).</summary>
        public void ApplyColor(Color color)
        {
            _startColor = color;
            if (_text != null) _text.color = color;
        }

        /// <summary>
        /// Spawns a floating TEXT label (e.g. "LEVEL UP! Lv.3") at
        /// <paramref name="worldPos"/> — same rise/fade/billboard as a damage
        /// number but bold, coloured and lingering longer. Used by the XP system
        /// for level-up feedback. No-op (null) when there is no active camera.
        /// </summary>
        public static DamageNumberSpawner SpawnLabel(string label, Vector3 worldPos, Color color, float scale = 1.2f)
        {
            if (string.IsNullOrEmpty(label)) return null;
            Camera cam = Camera.main;
            if (cam == null)
            {
                // §12 entry trace: no camera => the level-up / status label silently never shows.
                FlowTrace.Once("Feedback", "label-nocam",
                    $"SpawnLabel('{label}'): Camera.main is null — text label will not appear.");
                return null;
            }

            // §12 entry trace: confirm the label layer fired (Throttled — bursts on level-up).
            FlowTrace.Throttle("Feedback", "label", 1f, $"text label spawned '{label}'");
            var n = Acquire(worldPos);
            n.BuildLabel(label, color, scale, cam);
            return n;
        }

        // =====================================================================
        //  WO-953 — "+N <resource>" income pops THROUGH THE SAME POOL.
        //  Owner ruling (verbatim): "we can use the same item that spawns the
        //  damage points." ONE pool, one owner — ResourceGainPopup (the old
        //  separate TMP stack) now forwards here; never build a second stack.
        //  Combat behavior above is untouched: this is an additive entry point.
        // =====================================================================

        /// <summary>Seconds within which repeat gains of the SAME resource MERGE into the
        /// live label instead of spawning another (burst throttle — a silo dump or a fast
        /// tick loop can never wallpaper the screen with popups).</summary>
        public const float GainMergeWindowSeconds = 0.6f;

        /// <summary>Per-resource running merge state (label + accumulated amount).</summary>
        private struct GainStream
        {
            public DamageNumberSpawner Label;
            public int Amount;
            public float LastTime;
        }
        private static readonly Dictionary<string, GainStream> s_gainStreams =
            new Dictionary<string, GainStream>(8);

        // The resource this pooled body is currently showing a gain label for, or null
        // when it is a damage number / plain label. Guards the merge path against a
        // recycled body that the pool has since re-leased to combat.
        private string _gainKey;

        /// <summary>
        /// Spawns (or MERGES into a live) floating "+N &lt;resource&gt;" income pop at
        /// <paramref name="worldPos"/> — the WO-953 felt moment for every delivery that
        /// lands in the wallet. Word+shape: the resource NAME rides in the text (the
        /// tint is a redundant channel only — colorblind law). Pooled + burst-merged:
        /// a repeat gain of the same resource within <see cref="GainMergeWindowSeconds"/>
        /// updates the live label's total instead of stacking a new popup.
        /// No-op (null) for non-positive amounts, empty labels, or no camera.
        /// </summary>
        public static DamageNumberSpawner SpawnResourceGain(int amount, string resourceLabel, Vector3 worldPos, Color tint)
        {
            if (amount <= 0 || string.IsNullOrEmpty(resourceLabel)) return null;

            float now = Time.unscaledTime;
            if (s_gainStreams.TryGetValue(resourceLabel, out var st)
                && st.Label != null
                && st.Label._gainKey == resourceLabel
                && st.Label.gameObject.activeSelf
                && now - st.LastTime < GainMergeWindowSeconds)
            {
                st.Amount += amount;
                st.LastTime = now;
                st.Label.RearmGainLabel("+" + st.Amount + " " + resourceLabel);
                s_gainStreams[resourceLabel] = st;
                // §12: the merge is a deliberate drop of a visual, not a lost grant —
                // trace it so a capture can tell "merged" from "never popped".
                FlowTrace.Throttle("Feedback", "gain-merge-" + resourceLabel, 1f,
                    $"resource pop merged +{amount} into running +{st.Amount} {resourceLabel} (burst throttle)");
                return st.Label;
            }

            var n = SpawnLabel("+" + amount + " " + resourceLabel, worldPos, tint, scale: 1.05f);
            if (n == null) return null;   // no camera — SpawnLabel already Once-traced
            n._gainKey = resourceLabel;
            s_gainStreams[resourceLabel] = new GainStream { Label = n, Amount = amount, LastTime = now };
            FlowTrace.Throttle("Feedback", "gain-pop", 1f,
                $"resource pop spawned +{amount} {resourceLabel} (damage-number pool, WO-953)");
            return n;
        }

        /// <summary>Updates a live gain label's text with the merged running total and
        /// restarts its rise/fade so the new number is readable from the start.</summary>
        private void RearmGainLabel(string text)
        {
            if (_text != null) _text.text = text;
            _age = 0f;   // LateUpdate re-derives position from _startPos — the label
                         // re-pops from its origin, reading as a refreshed counter.
        }

        // ── Pool (SetActive cycle under a DontDestroyOnLoad root) ─────────────
        // Mirrors VfxPool: a dormant number is just disabled + re-homed, then
        // re-armed by Build/BuildLabel on the next Spawn. Self-installs lazily.
        private static readonly Queue<DamageNumberSpawner> s_pool =
            new Queue<DamageNumberSpawner>();
        private static Transform s_root;

        private static Transform Root()
        {
            if (s_root == null)
            {
                var go = new GameObject("DamageNumberPool");
                Object.DontDestroyOnLoad(go);
                s_root = go.transform;
            }
            return s_root;
        }

        /// <summary>Leases a number GameObject from the pool (or builds one), positioned
        /// at <paramref name="worldPos"/> and active. Skips destroyed entries (scene-unload
        /// guard, per ProjectilePool).</summary>
        private static DamageNumberSpawner Acquire(Vector3 worldPos)
        {
            DamageNumberSpawner num = null;
            while (num == null && s_pool.Count > 0) num = s_pool.Dequeue();   // skip dead refs

            if (num == null)
            {
                var go = new GameObject("DamageNumber");
                go.transform.SetParent(Root(), false);
                num = go.AddComponent<DamageNumberSpawner>();
            }

            // Re-parent out of the pool root so the world-space number floats freely.
            num.transform.SetParent(null, false);
            num.transform.position = worldPos;
            num.gameObject.SetActive(true);
            return num;
        }

        /// <summary>Returns a spent number to the pool: deactivated, re-homed, alpha-reset.
        /// Replaces the old <c>Destroy(gameObject)</c> at end-of-life.</summary>
        private void Recycle()
        {
            // Clear the visible glyph so a dormant number can't flash stale text for a
            // frame before its next Build re-arms it.
            if (_text != null) _text.text = string.Empty;
            _gainKey = null;   // WO-953: a pooled body carries no gain identity while dormant
            transform.SetParent(Root(), false);
            gameObject.SetActive(false);
            if (!s_pool.Contains(this)) s_pool.Enqueue(this);
        }

        /// <summary>
        /// Builds the TextMesh and caches the animation start state. Bigger hits
        /// start larger and hotter so a damage-talent upgrade is visible at a glance.
        /// </summary>
        private void Build(float amount, Camera cam)
        {
            _tf = transform;
            _faceCamera = cam;
            _startPos = _tf.position;
            _age = 0f;
            _gainKey = null;   // WO-953: a recycled gain label re-leased to combat must never merge
            _lifetime = Lifetime;   // reset (a reused body may have been a longer label)
            _rise = RiseDistance;

            // 0..1 magnitude ramp → size + colour. A normal melee hit sits low on
            // the ramp; a buffed ability hit pushes toward the big end.
            float t = Mathf.Clamp01(amount / BigHitDamage);

            // DEF-260 #6: gentler scale ramp (was 0.85→1.5) so even a big hit no
            // longer balloons across the screen; bigger hits still read a touch larger.
            _baseScale = Mathf.Lerp(0.8f, 1.2f, t);
            _tf.localScale = Vector3.one * _baseScale;

            _startColor = Color.Lerp(NormalColor, BigColor, t);

            // POOLED: reuse the TextMesh on a recycled body; only build it once.
            if (_text == null) _text = gameObject.AddComponent<TextMesh>();
            _text.text = Mathf.RoundToInt(amount).ToString();
            _text.anchor = TextAnchor.MiddleCenter;
            _text.alignment = TextAlignment.Center;
            _text.characterSize = BaseCharacterSize;
            _text.fontSize = 96;            // crisp glyphs; size is driven by characterSize + scale
            _text.fontStyle = FontStyle.Normal;   // reset (a reused label was Bold)
            _text.richText = false;
            _text.color = _startColor;

            // Render on top of the world geometry so the number is never buried
            // behind the enemy mesh. TextMesh exposes its shared material's queue.
            MeshRenderer mr = GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                if (mr.sharedMaterial != null)
                    mr.sharedMaterial.renderQueue = 4000; // after Transparent (3000)
            }
        }

        /// <summary>Builds a bold, longer-lived text label (level-up popups).</summary>
        private void BuildLabel(string label, Color color, float scale, Camera cam)
        {
            _tf = transform;
            _faceCamera = cam;
            _startPos = _tf.position;
            _age = 0f;
            _gainKey = null;   // WO-953: SpawnResourceGain re-stamps this after the build
            _lifetime = 1.6f;   // linger ~2x a damage number so the player reads it
            _rise = 1.6f;

            _baseScale = scale;
            _tf.localScale = Vector3.one * _baseScale;
            _startColor = color;

            // POOLED: reuse the TextMesh on a recycled body; only build it once.
            if (_text == null) _text = gameObject.AddComponent<TextMesh>();
            _text.text = label;
            _text.anchor = TextAnchor.MiddleCenter;
            _text.alignment = TextAlignment.Center;
            _text.characterSize = BaseCharacterSize;
            _text.fontSize = 96;
            _text.fontStyle = FontStyle.Bold;
            _text.richText = false;
            _text.color = _startColor;

            MeshRenderer mr = GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                if (mr.sharedMaterial != null)
                    mr.sharedMaterial.renderQueue = 4000;
            }
        }

        private void LateUpdate()
        {
            _age += Time.deltaTime;
            float k = _age / _lifetime;        // 0..1 progress
            if (k >= 1f)
            {
                Recycle();   // POOLED: return to the pool instead of Destroy
                return;
            }

            // Rise: ease-out so the number pops up then settles. Pure local math,
            // no allocation.
            float rise = (1f - (1f - k) * (1f - k)) * _rise;
            _tf.position = _startPos + Vector3.up * rise;

            // Fade: DEF-260 #6 — hold full opacity only briefly, then fade to zero
            // (owner: "fade faster"). Was holding for the first third of life.
            float alpha = k < 0.18f ? 1f : Mathf.InverseLerp(1f, 0.18f, k);
            Color c = _startColor;
            c.a = alpha;
            _text.color = c;

            // Gentle pop-then-shrink for extra readability on the way up.
            float scale = _baseScale * (1f + 0.15f * Mathf.Sin(k * Mathf.PI));
            _tf.localScale = Vector3.one * scale;

            // Billboard to the camera (same idiom as TownsfolkBubble). Re-resolve
            // Camera.main only if our cached camera went away (scene swap / death).
            if (_faceCamera == null) _faceCamera = Camera.main;
            if (_faceCamera != null)
            {
                Vector3 toCam = _tf.position - _faceCamera.transform.position;
                if (toCam.sqrMagnitude > 0.0001f)
                    _tf.rotation = Quaternion.LookRotation(toCam, Vector3.up);
            }
        }
    }
}
