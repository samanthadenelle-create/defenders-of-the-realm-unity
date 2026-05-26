// =============================================================================
// PetAttackVfxBridge — a small "strike" spark on each pet attack, reusing the
// village's AbilityVfxKit via reflection (DeNelle.Pets can't reference
// DeNelle.Village). No-ops if the kit/assembly is absent. Owner WO-35.
// =============================================================================

using System;
using System.Reflection;
using UnityEngine;

namespace DeNelle.Pets
{
    internal static class PetAttackVfxBridge
    {
        private static bool s_resolved;
        private static MethodInfo s_spawn;
        private static object s_strikeKind;

        public static void Strike(Color color, Vector3 at)
        {
            Resolve();
            if (s_spawn == null || s_strikeKind == null) return;
            try { s_spawn.Invoke(null, new object[] { s_strikeKind, color, at, 0.8f, at }); }
            catch { /* best-effort cosmetic */ }
        }

        private static void Resolve()
        {
            if (s_resolved) return;
            s_resolved = true;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var kit = asm.GetType("DeNelle.Village.AbilityVfxKit", false);
                var eff = asm.GetType("DeNelle.Village.AbilityEffect", false);
                if (kit == null || eff == null) continue;
                s_spawn = kit.GetMethod("SpawnAbilityVfx", BindingFlags.Public | BindingFlags.Static);
                try { s_strikeKind = Enum.Parse(eff, "Strike"); } catch { }
                break;
            }
        }
    }
}
