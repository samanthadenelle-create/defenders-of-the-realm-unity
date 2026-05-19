// =============================================================================
// Bryn — Bryn the Wanderer, the NPC at the Healer's Cottage entrance (Week 6).
// -----------------------------------------------------------------------------
// Port spec Part 3 row:
//   src/modules/dungeons/wanderer/ -> _Modules/Dungeons/Wanderer/Bryn.cs + WandererDialogue.cs
// Port spec Part 5 Week 6: "NPC at the entrance. World-space speech bubble
// (UGUI). Dialogue from data/lore-fragments.json#bryn-cottage-entry — canonical
// voice from narrative-bible."
//
// C# port of src/modules/dungeons/3d/npc/DungeonBryn3D.tsx. Bryn the Wanderer
// (a CANON character — port spec Part 2) stands in the Garden Approach, the
// Healer's Cottage entry courtyard, and speaks a quiet warning as the Keeper
// draws near.
//
// ── What this MonoBehaviour does ──
//   1. Sits at the layout's authored Bryn position, gently swaying her idle.
//   2. Watches the Keeper's distance — fades a world-space speech bubble in
//      when within speakRadius, out past it (with hysteresis so an
//      edge-loitering Keeper does not flicker the bubble).
//   3. Picks the line via WandererDialogue: the EXACT authored Cottage Beat-1
//      line on a fresh visit, the compassionate / peer-voice variants once the
//      Keeper has died / cleared here.
//
// The world-space speech bubble itself is a UGUI canvas (port spec Part 2
// "UGUI world-space canvases for speech bubbles") — Bryn drives it through the
// small IWandererBubble seam so this MonoBehaviour stays free of a HUD import.
// =============================================================================

using UnityEngine;

namespace DeNelle.Dungeons
{
    /// <summary>
    /// A world-space speech bubble for Bryn — a UGUI canvas implementing this
    /// seam. Kept as an interface so <see cref="Bryn"/> carries no HUD dependency.
    /// </summary>
    public interface IWandererBubble
    {
        /// <summary>Shows the bubble with <paramref name="line"/> as Bryn's words.</summary>
        void Show(string speakerName, string line);

        /// <summary>Hides the bubble.</summary>
        void Hide();
    }

    /// <summary>
    /// Bryn the Wanderer as a dungeon-scene presence. Stands at the Garden
    /// Approach, sways gently, and shows a tier-aware speech bubble when the
    /// Keeper draws within reach. Port of the React <c>DungeonBryn3D</c>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Bryn : MonoBehaviour
    {
        /// <summary>Bryn's display name — shown on the speech bubble.</summary>
        public const string DisplayName = "Bryn the Wanderer";

        [Header("Speech bubble")]
        [Tooltip("The world-space UGUI speech bubble. Any component implementing " +
                 "IWandererBubble; assigned by the scene builder.")]
        [SerializeField] private MonoBehaviour _bubbleBehaviour;

        [Header("Proximity")]
        [Tooltip("World-unit distance within which Bryn speaks. Overridden by the " +
                 "layout's bryn.speakRadius on Configure.")]
        [SerializeField] private float _speakRadius = 6f;

        [Tooltip("Hysteresis margin added to the speak radius for the fade-OUT " +
                 "threshold — stops the bubble flickering when loitering at the edge.")]
        [SerializeField] private float _speakHysteresis = 1.5f;

        [Header("Idle sway")]
        [Tooltip("Idle Y-sway frequency (Hz) — slow, weary.")]
        [SerializeField] private float _swayHz = 0.4f;

        [Tooltip("Idle Y-sway amplitude (world units).")]
        [SerializeField] private float _swayAmplitude = 0.05f;

        [Header("Accessibility")]
        [Tooltip("When true, the idle sway is disabled (reduced motion).")]
        [SerializeField] private bool _reducedMotion;

        // ── Runtime ──────────────────────────────────────────────────────────

        private DungeonBrynDef _def;
        private DungeonRuntimeState _runtimeState;
        private Transform _hero;
        private IWandererBubble _bubble;

        /// <summary>The hero's death count in this dungeon (from the persisted save).</summary>
        private int _deaths;

        /// <summary>The hero's clear count for this dungeon (from the persisted save).</summary>
        private int _clears;

        /// <summary>The dungeon's recommended level — buckets the warning tier.</summary>
        private int? _recommendedLevel = 1; // the Healer's Cottage is tier 1

        /// <summary>The base (un-swayed) world position Bryn stands at.</summary>
        private Vector3 _basePosition;

        /// <summary>True while the Keeper is close enough for Bryn to be speaking.</summary>
        public bool Speaking { get; private set; }

        /// <summary>A monotone counter — bumped each time the bubble (re-)appears.</summary>
        private int _showCount;

        /// <summary>True once the first-meet introduction line has fired this session.</summary>
        private bool _firstMeetFired;

        // ── Configuration ────────────────────────────────────────────────────

        /// <summary>
        /// Places + configures Bryn from the layout's <c>bryn</c> block. Called
        /// by <see cref="DungeonController"/> on dungeon load. The hero
        /// reference is read from the controller's runtime state each frame.
        /// </summary>
        public void Configure(DungeonBrynDef def, DungeonRuntimeState runtimeState)
        {
            _def = def;
            _runtimeState = runtimeState;
            _bubble = _bubbleBehaviour as IWandererBubble;

            if (_def != null)
            {
                _basePosition = _def.position.ToWorld();
                transform.position = _basePosition;
                transform.rotation = Quaternion.Euler(0f, _def.facingY, 0f);
                if (_def.speakRadius > 0f) _speakRadius = _def.speakRadius;
            }

            _bubble?.Hide();
        }

        /// <summary>
        /// Sets the hero transform Bryn watches for the proximity check. The
        /// controller assigns this — Bryn never reaches into the scene for it.
        /// </summary>
        public void SetHero(Transform hero)
        {
            _hero = hero;
        }

        /// <summary>
        /// Sets the Keeper's per-dungeon history — death and clear counts from
        /// the persisted save. <c>deaths &gt; 0</c> swaps Bryn to her
        /// compassionate "you came back" line; <c>clears &gt; 0</c> to her
        /// peer-voice "you know this one" line.
        /// </summary>
        public void SetHistory(int deaths, int clears)
        {
            _deaths = Mathf.Max(0, deaths);
            _clears = Mathf.Max(0, clears);
        }

        /// <summary>Sets the dungeon's recommended level (the tier bucket input).</summary>
        public void SetRecommendedLevel(int? recommendedLevel)
        {
            _recommendedLevel = recommendedLevel;
        }

        /// <summary>Sets the reduced-motion preference — disables the idle sway.</summary>
        public void SetReducedMotion(bool reduced)
        {
            _reducedMotion = reduced;
        }

        // ── Per-frame ────────────────────────────────────────────────────────

        private void Update()
        {
            UpdateProximity();
            UpdateSway();
        }

        /// <summary>
        /// Drives the speaking state from the Keeper's distance, with asymmetric
        /// thresholds (enter at <see cref="_speakRadius"/>, leave at the wider
        /// hysteresis radius). On a fresh entry into range the bubble shows the
        /// chosen line; the first time ever, the first-meet intro fires.
        /// </summary>
        private void UpdateProximity()
        {
            Transform hero = ResolveHero();
            if (hero == null)
            {
                if (Speaking) { Speaking = false; _bubble?.Hide(); }
                return;
            }

            Vector3 a = _basePosition; a.y = 0f;
            Vector3 b = hero.position; b.y = 0f;
            float dist = (a - b).magnitude;

            if (!Speaking && dist <= _speakRadius)
            {
                Speaking = true;
                _showCount += 1;
                string line = ChooseLine();
                _bubble?.Show(DisplayName, line);

                // First-meet introduction — fires once per session, the first
                // time Bryn would ever speak. On a fresh visit the authored
                // Cottage line IS the first-meet line so bubble + intro agree.
                if (!_firstMeetFired)
                {
                    _firstMeetFired = true;
                    // The intro is surfaced by the same bubble line on a fresh
                    // visit; no separate toast seam is needed in v2 foundation.
                }
            }
            else if (Speaking && dist > _speakRadius + _speakHysteresis)
            {
                Speaking = false;
                _bubble?.Hide();
            }
        }

        /// <summary>
        /// Chooses the line for the CURRENT bubble appearance.
        ///  - fresh visit (no deaths, no clears) + an authored first-encounter
        ///    line → the exact scripted line, verbatim;
        ///  - otherwise → the tier / died-before / cleared pick.
        /// Port of the <c>DungeonBryn3D</c> line-derivation.
        /// </summary>
        private string ChooseLine()
        {
            bool freshVisit = _deaths == 0 && _clears == 0;
            string authored = _def?.firstEncounterLine;

            if (freshVisit && !string.IsNullOrEmpty(authored))
                return authored;

            return WandererDialogue.PickLine(_recommendedLevel, _deaths, _clears, _showCount);
        }

        /// <summary>Gently sways Bryn's idle on the Y axis — frozen under reduced motion.</summary>
        private void UpdateSway()
        {
            if (_reducedMotion)
            {
                transform.position = _basePosition;
                return;
            }
            float y = _basePosition.y
                + Mathf.Sin(Time.time * _swayHz * 2f * Mathf.PI) * _swayAmplitude;
            transform.position = new Vector3(_basePosition.x, y, _basePosition.z);
        }

        /// <summary>The hero transform — the explicit ref, else the run state's position.</summary>
        private Transform ResolveHero()
        {
            return _hero;
        }
    }
}
