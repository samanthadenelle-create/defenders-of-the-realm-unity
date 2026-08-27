// =============================================================================
// ThreatSkullPlate (WO-155 Phase 3) — Fallout-style red-skull readiness tell.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// ⛔ THE TELL IS OFF (owner ruling WO-1232, 2026-08-26) — this component now renders
// NOTHING. It used to float a RISKY / LETHAL word over a mob whose "level" out-paced
// the player's, where both numbers were round(HP / 25). Owner: "HP / 25 is not a level
// system. Dressing it up as one just produces very confident nonsense." The grading, the
// label copy and the billboard are DELETED; the delta computation and its FlowTrace stay
// as instrumentation (CLAUDE.md §12 — never stripped as cleanup), gated by DisplayEnabled.
// What the player reads instead is IDENTITY, not difficulty: the authored BOSS / ELITE
// word on the HUD target frame (HudModelProducers.TargetProducer.BadgeFor).
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
    /// Code-built world-space threat tell over a roaming mob, DISPLAY-OFF since WO-1232:
    /// <see cref="LateUpdate"/> still polls the supplied ThreatLevel + the player's level, but
    /// only to TRACE them — the canvas is force-disabled and no word is ever shown.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ThreatSkullPlate : MonoBehaviour
    {
        // =========================================================================
        // THE RISKY / LETHAL BANDING IS OFF (owner ruling WO-1232, 2026-08-26).
        // -------------------------------------------------------------------------
        // Owner verbatim: "The Lv5 vs Lv36 comparison is downstream of the fake level.
        // Retuning thresholds just polishes the wrong equation." The EQUATION - not the
        // numbers - was the defect: delta = enemyLevel - playerLevel, where enemyLevel is
        // round(HP / 25). hollow-brute (900 hp -> "Lv 36") therefore read LETHAL forever.
        // The player-facing tell is REMOVED: this plate never enables its canvas, and the
        // target frame's "!"/"!!" prefix is gone with it. The replacement the owner named -
        // a real Combat Rating (HP, damage, cadence, armour, abilities, encounter role ->
        // Low/Even/High/Deadly) - is a SEPARATE, UNBUILT spec. Do NOT stub it here, and do
        // NOT "fix" this by picking better thresholds.
        //
        // What survives below is DIAGNOSTIC ONLY, kept per CLAUDE.md §12: instrumentation is
        // PERMANENT and is never removed as cleanup - a stripped trace turns a logged failure
        // back into a silent one. The DISPLAY is flagged off; the math and the traces stay so
        // a future re-enable is one read instead of an archaeology dig.
        // =========================================================================

        /// <summary>
        /// WO-1232: the player-facing threat tell is OFF and stays off until a real difficulty
        /// model exists. Flipping this to <c>true</c> re-ships a warning graded on <c>HP/25</c>,
        /// which is the defect the owner ruled out - it is a diagnostic switch, not a tuning dial.
        /// </summary>
        public const bool DisplayEnabled = false;

        /// <summary>DIAGNOSTIC ONLY (see the block above): delta at/above which the retired
        /// CAUTION band began. No player-facing surface reads it.</summary>
        public const int RiskyDelta = 3;
        /// <summary>DIAGNOSTIC ONLY (see the block above): delta at/above which the retired
        /// DANGER band began. No player-facing surface reads it.</summary>
        public const int LethalDelta = 7;

        /// <summary>
        /// DIAGNOSTIC ONLY - the retired difficulty band for a delta (0 fair / 1 caution /
        /// 2 danger). Kept so the trace below can state what the old equation WOULD have
        /// claimed. Nothing player-facing may call it: it grades a fake level (HP/25) against
        /// the hero's real one, which is exactly what WO-1232 removed.
        /// </summary>
        public static int TierFor(int enemyDifficulty, int playerLevel)
        {
            int delta = enemyDifficulty - playerLevel;
            return delta >= LethalDelta ? 2 : delta >= RiskyDelta ? 1 : 0;
        }

        /// <summary>
        /// DIAGNOSTIC ONLY - the magnitude the retired tell graded, = <see cref="Enemy.Level"/>,
        /// which is ITSELF <c>round(def.Hp / 25)</c>. There is no authored level field anywhere,
        /// which is precisely why WO-1232 removed the display instead of re-tuning it.
        ///
        /// Historic note: this used to run the RETIRED HP/25 heuristic
        /// (<c>round-to-int of the runtime maxHp over 25</c>) that WO-611 F3 replaced on the target frame
        /// but never removed here. Because it read the RUNTIME maxHp, wave scaling inflated it
        /// every wave: an ordinary wave-7 enemy at 1700 HP read as "level 68", so
        /// <c>delta = 68 - 5</c> put EVERY enemy past <see cref="LethalDelta"/> and the warning
        /// carried no information at all. The heuristic is deleted, not re-tuned - there is an
        /// authored value and it must not survive in any form. Falls back to 1 for a null enemy.
        /// </summary>
        public static int EnemyThreatLevel(Enemy e)
        {
            if (e == null) return 1;
            return Mathf.Max(1, e.Level);
        }

        // Only the base label tint survives; LethalColor went with the deleted lethal pulse.
        private static readonly Color SkullColor   = new Color(0.92f, 0.16f, 0.13f, 1f);  // danger red

        // ── Config ────────────────────────────────────────────────────────────────
        private Func<int> _threatLevel;     // the mob's ThreatLevel (tier × depth)
        private float _heightOffset = 3.2f; // world-units above the unit pivot (above the HP bar)

        // ── Runtime refs ──────────────────────────────────────────────────────────
        private Canvas _canvas;
        private Text   _label;
        // _cam went with the deleted billboard - nothing is drawn to face the camera now.
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
        /// host and feeds its authored level (<see cref="EnemyThreatLevel"/>) as the threat
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

            // WO-1232: the DISPLAY is off. The canvas is force-disabled (never conditionally
            // enabled), the RISKY / LETHAL words never reach the label, and nothing player-facing
            // depends on the retired banding any more. The computation above and the trace below
            // are INSTRUMENTATION and stay per CLAUDE.md §12 - if the old equation ever starts
            // claiming something absurd again, the log says so instead of a felt-test.
            if (!DisplayEnabled)
            {
                if (_canvas.enabled) _canvas.enabled = false;
                if (_shownSkulls != 0)
                {
                    _shownSkulls = 0;
                    _label.text = string.Empty;
                }
                DeNelle.Core.Diagnostics.FlowTrace.Throttle("ThreatTell", "display-off", 5f,
                    "threat tell suppressed (WO-1232): the retired band would have said tier " +
                    TierFor(threat, playerLevel) + " from delta=" + delta +
                    " (HP-derived magnitude " + threat + " vs hero " + playerLevel +
                    "); the player sees nothing. Identity is shown instead, as the authored " +
                    "BOSS/ELITE word on the target frame.");
                return;
            }

            // NOTHING FOLLOWS. The skull grading, the RISKY / LETHAL label copy, the lethal pulse
            // and the billboard that served them were DELETED by WO-1232, not commented out and not
            // re-tuned: the owner ruled the equation itself out, so leaving a dormant copy of it
            // here would be the same fake precision waiting to be switched back on. Re-enabling the
            // tell means writing the Combat Rating model first (its own spec), and this component
            // then renders THAT - never a delta of HP/25 levels.
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
