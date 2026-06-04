// =============================================================================
// LevelUpVFXController — celebratory burst + gold screen flash on a level-up.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHAT IT DOES:
//   Adds the "that felt like an achievement" beat to a level-up: a Juice_LevelUp
//   VFX burst at the character, a brief gold full-screen flash, and a light
//   hit-stop punch. This LAYERS ON TOP of the existing floating "LEVEL UP! Lv.X"
//   label that ProgressionManager already spawns — it does not replace it.
//
// DESIGN (deliberately self-contained + low-risk):
//   • Single central hook — ProgressionManager.Grant() calls PlayLevelUp() for
//     every IXpEarner that levels, so hero AND pets celebrate uniformly with no
//     asmdef bridge and no double-fire.
//   • Self-bootstraps a persistent instance, so the null-safe call site just works
//     without scene wiring.
//   • Gold flash via IMGUI (no scene Volume / UI object needed; always renders in
//     player builds — same approach as HeroHitReaction).
//   • Uses VFXType.Juice_LevelUp, which already exists in the catalog with a
//     procedural fallback, so it shows something even before art is wired.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>Plays the level-up celebration (VFX burst + gold screen flash).</summary>
    [DisallowMultipleComponent]
    public sealed class LevelUpVFXController : MonoBehaviour
    {
        public static LevelUpVFXController Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("[LevelUpVFXController]");
            DontDestroyOnLoad(go);
            go.AddComponent<LevelUpVFXController>();
        }

        [Tooltip("Peak opacity of the gold celebratory screen flash (0-1).")]
        [SerializeField, Range(0f, 1f)] private float _flashPeak = 0.30f;

        [Tooltip("Seconds for the flash to fade from peak to nothing.")]
        [SerializeField, Min(0.05f)] private float _flashFade = 0.40f;

        private float _flashAlpha;
        private static readonly Color GoldColor = new Color(1f, 0.85f, 0.35f);

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Fire the celebration at <paramref name="worldPos"/>. <paramref name="level"/>
        /// is accepted for future tuning (e.g. bigger burst on milestone levels);
        /// the floating "Lv.X" label is already spawned by ProgressionManager.
        /// </summary>
        public void PlayLevelUp(Vector3 worldPos, int level)
        {
            VFXManager.Play(VFXType.Juice_LevelUp, worldPos + Vector3.up * 1.2f);
            HitStopManager.DoImpact(HitTier.Light);   // small punch — shake only, no freeze
            _flashAlpha = _flashPeak;
        }

        private void Update()
        {
            if (_flashAlpha > 0f)
                _flashAlpha = Mathf.Max(0f,
                    _flashAlpha - (_flashPeak / _flashFade) * Time.unscaledDeltaTime);
        }

        private void OnGUI()
        {
            if (_flashAlpha <= 0f) return;
            var prev = GUI.color;
            GUI.color = new Color(GoldColor.r, GoldColor.g, GoldColor.b, _flashAlpha);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }
}
