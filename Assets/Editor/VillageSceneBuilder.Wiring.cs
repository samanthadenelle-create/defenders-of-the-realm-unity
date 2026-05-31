using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeNelle.Editor
{
    // WO-181: wave/quest/camera-target/light/controller wiring -- split out of VillageSceneBuilder.cs. Same partial class; moves only.
    public static partial class VillageSceneBuilder
    {
        private static void WireWaveHudBridge()
        {
            var waveType = FindType(TypeWaveManager);
            var hudType = FindType(TypeVillageHudController);
            var bridgeType = FindType(TypeWaveHudBridge);
            if (waveType == null || hudType == null || bridgeType == null) return;

            var wave = UnityEngine.Object.FindObjectOfType(waveType);
            var hud = UnityEngine.Object.FindObjectOfType(hudType);
            if (wave == null || hud == null) return;

            var waveGo = ((Component)wave).gameObject;
            var bridge = waveGo.GetComponent(bridgeType) ?? waveGo.AddComponent(bridgeType);

            var so = new SerializedObject(bridge);
            SetObjectField(so, "_wave", (UnityEngine.Object)wave);
            SetObjectField(so, "_hud", (UnityEngine.Object)hud);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Adds the DailyQuestCombatBridge to the WaveManager GameObject so
        /// OnWaveCleared ticks the daily-quest service. Idempotent.
        /// </summary>
        private static void WireDailyQuestCombatBridge()
        {
            var waveType = FindType(TypeWaveManager);
            var bridgeType = FindType(TypeDailyQuestCombatBridge);
            if (waveType == null || bridgeType == null) return;
            var wave = UnityEngine.Object.FindObjectOfType(waveType);
            if (wave == null) return;
            var waveGo = ((Component)wave).gameObject;
            if (waveGo.GetComponent(bridgeType) == null)
                waveGo.AddComponent(bridgeType);
        }

        /// <summary>
        /// Finds the Main Camera built by <see cref="CreateCamera"/>, locates its
        /// VillageCamera follow component (added there), and sets the target
        /// transform to <paramref name="hero"/>. No-op if either is missing.
        /// </summary>
        private static void WireVillageCameraTarget(GameObject hero)
        {
            if (hero == null) return;
            var cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[VillageSceneBuilder] No Camera.main found — " +
                                 "skipping VillageCamera target wiring.");
                return;
            }
            var camType = FindType(TypeVillageCamera);
            if (camType == null) return;
            var follow = cam.GetComponent(camType);
            if (follow == null) return;
            var so = new SerializedObject(follow);
            SetObjectField(so, "_target", hero.transform);
            so.ApplyModifiedPropertiesWithoutUndo();

            // DEF-53: wire SmartMobileCamera._target to the same hero.
            var smcType = FindType(TypeSmartMobileCamera);
            if (smcType != null)
            {
                var smc = cam.GetComponent(smcType);
                if (smc != null)
                {
                    var smcSo = new SerializedObject(smc);
                    SetObjectField(smcSo, "_target", hero.transform);
                    smcSo.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }

        private static void CreateDirectionalLight()
        {
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            // Soft dawn — warm low sun (spec §14 Q4 default, §9.5 register).
            light.color = new Color(1f, 0.93f, 0.82f);
            light.intensity = 1.05f;
            light.shadows = LightShadows.Soft;
            // ~15° above the horizon (spec §9.5).
            lightGo.transform.rotation = Quaternion.Euler(16f, -35f, 0f);
        }

        // =====================================================================
        //  Controller wiring (SerializedObject -- no compile-time dependency)
        // =====================================================================

        private static void WireController(Component controller, Transform wallRoot,
            Transform gateRoot, Transform buildingRoot, Component heart)
        {
            if (controller == null) return;
            var so = new SerializedObject(controller);
            SetObjectField(so, "_wallRoot", wallRoot);
            SetObjectField(so, "_gateRoot", gateRoot);
            SetObjectField(so, "_buildingRoot", buildingRoot);
            SetObjectField(so, "_heart", heart);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
