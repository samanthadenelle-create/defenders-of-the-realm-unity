// =============================================================================
// ThreatSkullPlate (WO-155 Phase 3) — Fallout-style red-skull readiness tell.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// A small world-space nameplate floated over a roaming region mob that shows a RED
// SKULL when the mob's ThreatLevel out-paces the player's level — the "this one
// will wreck you" signal, graded:
//   delta < RiskyDelta            → no skull (a fair fight).
//   RiskyDelta..LethalDelta       → one skull  (risky — fights uphill).
//   delta >= LethalDelta           → two skulls (lethal — likely a one-sided loss).
// where delta = mobThreatLevel − playerLevel.
//
// Built ENTIRELY in C# (uGUI world-space Canvas + Text) — NO UXML / UIDocument,
// which do not render in player builds (CLAUDE.md memory, PIPELINE_STATE.md §8).
// Mirrors FloatingHealthBar's code-built world-space-bar pattern so the two read as
// one HUD language; sits a little ABOVE the HP bar so they don't overlap.
//
// PURE PRESENTATION: this only READS the threat math (a supplied ThreatLevel func)
// and HeroProgression.Level — it changes NO gameplay. The soft-wall difficulty curve
// (ZoneManager.ThreatLevel) is unchanged; this just surfaces it to the player so the
// open world telegraphs its danger instead of ambushing them.
//
// SELF-CONTAINED + ZERO PREFAB WIRING: RegionMobSpawner.Attach()es it and feeds the
// mob's ThreatLevel via one delegate; the plate resolves the player's level itself.
// =============================================================================
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DeNelle.Village
{
    /// <summary>
    /// Code-built world-space red-skull threat tell over a roaming mob. Configure via
    /// <see cref="Attach"/>; it polls the supplied ThreatLevel + the player's level each
    /// <see cref="LateUpdate"/> and shows 0 / 1 / 2 skulls accordingly.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ThreatSkullPlate : MonoBehaviour
    {
        // ── Grading thresholds (mob difficulty − player level) — OWNER-TUNABLE ────
        // THE single tuning point for the whole difficulty-warning tell. Both the
        // over-head plate (this component) AND the target frame (HudModelProducers.
        // TargetProducer, via TierFor) read these, so bumping them here moves every
        // surface at once. delta = enemyDifficulty − playerLevel.
        /// <summary>Delta at/above which the CAUTION tell shows (one skull — a risky, uphill fight).</summary>
        public const int RiskyDelta = 3;
        /// <summary>Delta at/above which the DANGER tell shows (two skulls — lethal, likely a loss).</summary>
        public const int LethalDelta = 7;

        /// <summary>
        /// Shared difficulty tier for a delta (enemyDifficulty − playerLevel):
        /// 0 = fair fight (no tell), 1 = caution, 2 = danger. THE single owner-tunable
        /// gate (<see cref="RiskyDelta"/> / <see cref="LethalDelta"/>) so the over-head
        /// plate and the target frame always agree.
        /// </summary>
        public static int TierFor(int enemyDifficulty, int playerLevel)
        {
            int delta = enemyDifficulty - playerLevel;
            return delta >= LethalDelta ? 2 : delta >= RiskyDelta ? 1 : 0;
        }

        /// <summary>
        /// Enemy "difficulty" derived from max HP (the Enemy carries no explicit Level).
        /// Mirrors HudModelHost.EnemyLevelStub so the target frame's "Lv" and this warning
        /// read exactly one value. Falls back to 1 when the enemy is null.
        /// </summary>
        public static int EnemyThreatLevel(Enemy e)
        {
            if (e == null) return 1;
            float maxHp = e.MaxHp > 0.001f ? e.MaxHp : e.Hp;
            return Mathf.Max(1, Mathf.RoundToInt(maxHp / 25f));
        }

        private static readonly Color SkullColor   = new Color(0.92f, 0.16f, 0.13f, 1f);  // danger red
        private static readonly Color LethalColor  = new Color(1f,    0.30f, 0.10f, 1f);  // hotter orange-red

        // ── Config ────────────────────────────────────────────────────────────────
        private Func<int> _threatLevel;     // the mob's ThreatLevel (tier × depth)
        private float _heightOffset = 3.2f; // world-units above the unit pivot (above the HP bar)

        // ── Runtime refs ──────────────────────────────────────────────────────────
        private Canvas _canvas;
        private Text   _label;
        private Transform _cam;
        private bool _built;
        private int _shownSkulls = -1;      // cache to avoid restyling every frame

        /// <summary>
        /// Attach (or reuse) a threat-skull plate on <paramref name="host"/>.
        /// </summary>
        /// <param name="host">GameObject the plate floats over.</param>
        /// <param name="threatLevel">Returns the mob's ThreatLevel (ZoneManager.ThreatLevel at spawn).</param>
        /// <param name="heightOffset">Plate height above the host pivot (world units).</param>
        public static ThreatSkullPlate Attach(GameObject host, Func<int> threatLevel,
            float heightOffset = 3.2f)
        {
            if (host == null || threatLevel == null) return null;
            var plate = host.GetComponent<ThreatSkullPlate>();
            if (plate == null) plate = host.AddComponent<ThreatSkullPlate>();
            plate._threatLevel = threatLevel;
            plate._heightOffset = heightOffset;
            if (plate._built && plate._canvas != null)
                plate._canvas.transform.localPosition = new Vector3(0f, heightOffset, 0f);
            return plate;
        }

        /// <summary>
        /// Universal self-resolving attach used by the enemy nameplate path
        /// (<see cref="FloatingHealthBar"/>) so EVERY enemy — not just RegionMobSpawner
        /// roamers — carries the difficulty tell. Resolves the <see cref="Enemy"/> on the
        /// host and feeds its HP-derived level (<see cref="EnemyThreatLevel"/>) as the threat
        /// versus the player's level. A no-op on a non-enemy host (e.g. the hero's HP bar).
        /// The explicit spawner <see cref="Attach"/> (ZoneManager threat) still overrides this
        /// later for region mobs — Attach reuses the same component, so nothing double-stacks.
        /// </summary>
        public static ThreatSkullPlate AttachAuto(GameObject host)
        {
            if (host == null) return null;
            var e = host.GetComponentInParent<Enemy>();
            if (e == null) return null;   // hero / non-enemy nameplate → no threat tell

            // Sit just above the floating HP bar. The plate's canvas is a child of the host,
            // so its localPosition.y is scaled by the host's lossyScale.y — convert the desired
            // WORLD offset into host-local units so wildly-scaled enemy meshes (orc/troll/People
            // family) place the plate at the same world height above the head.
            float worldTop = 2.4f;
            var rend = host.GetComponentInChildren<Renderer>();
            if (rend != null)
            {
                float top = rend.bounds.max.y - host.transform.position.y;
                if (top > 0.1f) worldTop = top;
            }
            float worldOffset = Mathf.Clamp(worldTop, 0.5f, 4f) + 0.6f;   // above the HP bar
            float scaleY = Mathf.Abs(host.transform.lossyScale.y);
            if (scaleY < 0.0001f || float.IsNaN(scaleY) || float.IsInfinity(scaleY)) scaleY = 1f;

            return Attach(host, () => EnemyThreatLevel(e), worldOffset / scaleY);
        }

        private void Start()
        {
            BuildUi();
            var c = Camera.main;
            _cam = c != null ? c.transform : null;
        }

        private void BuildUi()
        {
            if (_built) return;
            _built = true;

            var canvasGo = new GameObject("ThreatSkullCanvas");
            canvasGo.transform.SetParent(transform, false);
            canvasGo.transform.localPosition = new Vector3(0f, _heightOffset, 0f);

            _canvas = canvasGo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.WorldSpace;
            var crt = _canvas.GetComponent<RectTransform>();
            crt.sizeDelta = new Vector2(1.2f, 0.5f);
            // Constant ~1.2m wide regardless of host scale (mobs spawn at varied scales).
            float hostScale = Mathf.Max(0.0001f, transform.lossyScale.x);
            canvasGo.transform.localScale = Vector3.one / hostScale;

            var labelGo = new GameObject("Skulls");
            labelGo.transform.SetParent(canvasGo.transform, false);
            _label = labelGo.AddComponent<Text>();
            _label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _label.alignment = TextAnchor.MiddleCenter;
            _label.horizontalOverflow = HorizontalWrapMode.Overflow;
            _label.verticalOverflow   = VerticalWrapMode.Overflow;
            _label.fontSize = 1;                 // tiny font, scaled up by the rect (world-space)
            _label.fontStyle = FontStyle.Bold;
            _label.color = SkullColor;
            var lrt = _label.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;
            lrt.localScale = Vector3.one * 0.06f;  // world-space text needs a small scale

            _canvas.enabled = false;               // hidden until a skull is warranted
        }

        private void LateUpdate()
        {
            if (_canvas == null || _label == null) return;

            int threat = _threatLevel != null ? _threatLevel() : 0;
            int playerLevel = ResolvePlayerLevel();
            int delta = threat - playerLevel;

            int skulls = delta >= LethalDelta ? 2
                       : delta >= RiskyDelta  ? 1
                       :                         0;

            bool show = skulls > 0;
            if (_canvas.enabled != show) _canvas.enabled = show;
            if (!show) return;

            if (skulls != _shownSkulls)
            {
                _shownSkulls = skulls;
                // "☠" U+2620 skull-and-crossbones; legacy font may fall back — pair with
                // a "!!" so the tell still reads if the glyph is missing.
                _label.text = skulls >= 2 ? "LETHAL" : "RISKY";
                _label.color = skulls >= 2 ? LethalColor : SkullColor;
            }

            // Lethal pulse so a deadly mob reads at a glance.
            if (skulls >= 2)
            {
                float pulse = 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(Time.time * 5f));
                var c = LethalColor; c.a = pulse;
                _label.color = c;
            }

            // Billboard toward the camera.
            if (_cam == null)
            {
                var cm = Camera.main;
                _cam = cm != null ? cm.transform : null;
            }
            if (_cam != null)
                _canvas.transform.rotation =
                    Quaternion.LookRotation(_canvas.transform.position - _cam.position);
        }

        // Player level via HeroProgression (the hero XP/level owner). Falls back to 1
        // when no hero is present (e.g. headless / pre-hero scene) so the tell never
        // throws — it just reads everything as "level 1" until the hero exists.
        private static int ResolvePlayerLevel()
        {
            return HeroProgression.Instance != null
                ? Mathf.Max(1, HeroProgression.Instance.Level)
                : 1;
        }
    }
}
