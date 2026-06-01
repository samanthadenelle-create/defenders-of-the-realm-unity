// =============================================================================
// CC5KnightField — dev: force the playable hero to Knight so the CC5 fighter
// (now Resources/Heroes/Knight.fbx) loads as the hero via HeroBodySwapper, with
// the Knight ability kit auto-populating the HUD. Field-test harness for the
// CC5 character pipeline. Remove once hero-select drives the class.
// =============================================================================
#if DEVELOPMENT_BUILD || UNITY_EDITOR
using UnityEngine;
using DeNelle.Core.State;

namespace DeNelle.Village
{
    public static class CC5KnightField
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void ForceKnight()
        {
            try
            {
                var svc = GameStateService.Instance;
                if (svc != null)
                {
                    svc.ChooseHero(HeroClass.Knight);
                    Debug.Log("[CC5KnightField] forced HeroClass=Knight — CC5 fighter loads as the hero.");
                }
                else
                {
                    Debug.LogWarning("[CC5KnightField] GameStateService.Instance null; hero class not forced.");
                }
            }
            catch (System.Exception e) { Debug.LogWarning("[CC5KnightField] " + e.Message); }
        }
    }
}
#endif
