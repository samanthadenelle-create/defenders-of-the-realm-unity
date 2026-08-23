using System;
using System.Reflection;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Wallet
{
    /// <summary>Optional cross-assembly bridge for the season-track tier-up celebration.</summary>
    public static class BattlePassLevelUpVfxBridge
    {
        private static Type _type;
        private static MethodInfo _play;
        private static bool _resolved;

        public static void Play(int tier)
        {
            if (tier <= 0) return;
            if (!_resolved) Resolve();
            if (_type == null || _play == null) return;
            try
            {
                var instance = _type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                if (instance != null) _play.Invoke(instance, new object[] { Vector3.zero, tier });
            }
            catch (Exception ex)
            {
                FlowTrace.Warn("BattlePass", "tier-up VFX no-op: " + ex.GetType().Name + ": " + ex.Message);
            }
        }

        private static void Resolve()
        {
            _resolved = true;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                _type = asm.GetType("DeNelle.Village.LevelUpVFXController", false);
                if (_type != null) break;
            }
            _play = _type?.GetMethod("PlayLevelUp", BindingFlags.Public | BindingFlags.Instance,
                null, new[] { typeof(Vector3), typeof(int) }, null);
        }
    }
}
