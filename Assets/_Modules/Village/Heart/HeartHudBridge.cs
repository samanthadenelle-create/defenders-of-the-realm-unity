// =============================================================================
// HeartHudBridge — WO-20: pushes Heart HP + the crystal balance into the village
// HUD every frame. Companion to WaveHudBridge (wave) and HeroAbilitiesHudBridge
// (mana/cooldowns). Before this, VillageHudController.SetHeartHp / SetCrystals
// had no runtime caller (only the DevPanel pushed Heart HP), so the Heart HP bar
// and crystal counter stayed frozen at their UXML defaults during normal play.
//
// Cross-asmdef: DeNelle.Village cannot reference DeNelle.HUD, so the HUD is
// discovered by component-type name and its setters invoked by reflection — the
// same seam WaveHudBridge / HeroAbilitiesHudBridge use. Attached at runtime by
// VillageController (the gates/hero/HUD are baked by the edit-time scene builder,
// which the curated-scene rule forbids re-running).
// =============================================================================

using System.Reflection;
using DeNelle.Core.State;
using UnityEngine;

namespace DeNelle.Village
{
    [DisallowMultipleComponent]
    public sealed class HeartHudBridge : MonoBehaviour
    {
        // HeartController.SetHp clamps to 0-100, so the Heart HP scale max is 100.
        private const float HeartMaxHp = 100f;

        private Object _hud;                 // VillageHudController (held as Object — no DeNelle.HUD ref)
        private MethodInfo _setHeartHp;      // SetHeartHp(float current, float max)
        private MethodInfo _setCrystals;     // SetCrystals(int amount)
        private HeartController _heart;
        private readonly object[] _hpArgs = new object[2];
        private readonly object[] _crystalArgs = new object[1];

        private void OnEnable() => Resolve();

        private void Resolve()
        {
            if (_hud == null)
            {
                foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
                {
                    if (mb != null && mb.GetType().Name == "VillageHudController") { _hud = mb; break; }
                }
            }
            if (_hud != null && _setHeartHp == null)
            {
                var t = _hud.GetType();
                _setHeartHp = t.GetMethod("SetHeartHp", BindingFlags.Public | BindingFlags.Instance, null,
                    new[] { typeof(float), typeof(float) }, null);
                _setCrystals = t.GetMethod("SetCrystals", BindingFlags.Public | BindingFlags.Instance, null,
                    new[] { typeof(int) }, null);
            }
            if (_heart == null) _heart = FindAnyObjectByType<HeartController>();
        }

        private void Update()
        {
            if (_hud == null || _heart == null)
            {
                Resolve();
                if (_hud == null || _heart == null) return;
            }

            if (_setHeartHp != null)
            {
                _hpArgs[0] = _heart.Hp;
                _hpArgs[1] = HeartMaxHp;
                _setHeartHp.Invoke(_hud, _hpArgs);
            }

            if (_setCrystals != null)
            {
                _crystalArgs[0] = CurrentCrystals();
                _setCrystals.Invoke(_hud, _crystalArgs);
            }
        }

        private static int CurrentCrystals()
        {
            var svc = GameStateService.Instance;
            var state = svc != null ? svc.State : null;
            return state != null ? state.Resources.Crystals : 0;
        }
    }
}
