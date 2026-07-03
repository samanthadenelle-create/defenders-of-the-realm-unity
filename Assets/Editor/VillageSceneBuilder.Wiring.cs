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

            var wave = UnityEngine.Object.FindAnyObjectByType(waveType);
            var hud = UnityEngine.Object.FindAnyObjectByType(hudType);
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
            var wave = UnityEngine.Object.FindAnyObjectByType(waveType);
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
            // DEF-109 (2026-06-03): the village read murky/dark in a live screenshot — a
            // 1.05-intensity sun at only 16° above the horizon cast long, flat, gloomy light.
            // Lift it to a brighter MID-MORNING sun: warmer-white, ~1.55 intensity, raised to
            // ~42° elevation so the stylized geometry catches clean daylight without blowing
            // out (kept under 2.0, soft shadows on, so highlights stay readable).
            light.color = new Color(1f, 0.96f, 0.89f);
            light.intensity = 1.55f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.6f;   // softer shadows so shaded faces don't read as black
            // ~42° above the horizon — mid-morning sun, removes the low-angle gloom.
            lightGo.transform.rotation = Quaternion.Euler(42f, -35f, 0f);

            // Environment / ambient + fog lift (DEF-109). No RenderSettings were configured
            // before, so the scene inherited a dim default skybox-ambient that left shaded
            // faces and the terrain murky.
            ConfigureSceneLighting();
        }

        /// <summary>
        /// DEF-109: lift ambient/environment lighting so the village reads clearly in daylight.
        /// Gradient ambient (sky / equator / ground) brightens shaded surfaces without washing
        /// out the directional key light. Mobile-cheap — no realtime GI, no extra lights.
        /// </summary>
        private static void ConfigureSceneLighting()
        {
            // Gradient (trichromatic) ambient — brighter than the dim default skybox ambient.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor     = new Color(0.62f, 0.66f, 0.74f); // soft blue sky bounce
            RenderSettings.ambientEquatorColor = new Color(0.52f, 0.52f, 0.50f); // neutral horizon fill
            RenderSettings.ambientGroundColor  = new Color(0.34f, 0.33f, 0.30f); // warm earth bounce
            RenderSettings.ambientIntensity = 1.0f;

            // Light, distance-only fog to keep depth/mood without darkening the foreground.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.70f, 0.74f, 0.80f);
            RenderSettings.fogStartDistance = 80f;
            RenderSettings.fogEndDistance   = 220f;
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
