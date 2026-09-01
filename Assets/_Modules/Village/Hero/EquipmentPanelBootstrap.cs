// Equipment is a destination, not a progression unlock. This host exists before
// PlayerDeckWorkspace checks PanelRouter, so its card is available in every town.

using DeNelle.Core.Diagnostics;
using UnityEngine;

namespace DeNelle.Village.Hero
{
    public static class EquipmentPanelBootstrap
    {
        private static EquipmentPanel _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _instance = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void Install()
        {
            if (_instance != null) return;
            Guard.Try("Equip", "install equipment panel", () =>
            {
                var existing = Object.FindAnyObjectByType<EquipmentPanel>(FindObjectsInactive.Include);
                if (existing != null)
                {
                    _instance = existing;
                    return;
                }

                var go = new GameObject("EquipmentPanelHost");
                Object.DontDestroyOnLoad(go);
                _instance = go.AddComponent<EquipmentPanel>();
                FlowTrace.Step("Equip", "EquipmentPanel installed (PanelId.EquipmentPanel; no unlock gate).");
            });
        }
    }
}
