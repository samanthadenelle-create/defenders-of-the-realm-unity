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
    // WO-181: camera, event system, HUD-bridge wiring -- split out of VillageSceneBuilder.cs. Same partial class; moves only.
    public static partial class VillageSceneBuilder
    {
        private static void CreateCamera()
        {
            var cameraGo = new GameObject("Main Camera");
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            // Soft dawn pink-violet horizon tint (spec §14 Q4 default).
            camera.backgroundColor = new Color(0.74f, 0.66f, 0.72f);
            camera.farClipPlane = 600f;
            cameraGo.tag = "MainCamera";

            // ── Camera-angle: over-the-right-shoulder of Hero (Blaise) ───────
            // Hero spawns at world (2.5, 0, 2.5) facing +Z (toward open plaza).
            // Hero is a ~2m capsule (HeroBody at localPosition (0,1,0) on a
            // root at ground). Owner direction (2026-05-19): start over right
            // shoulder, ~2 feet up + 2 feet back. 2 ft ≈ 0.6 m (Unity units).
            //   • shoulder X offset: +0.3 right of hero center;
            //   • Y: hero head ~Y=2, +0.6 above = Y ≈ 2.6;
            //   • Z: 0.6 behind hero (hero faces +Z, so −0.6 in world Z) = 1.9.
            // FOV 60deg is the Unity default, comfortable for 3rd-person view.
            // Slight downward pitch (12deg) so the hero's back/shoulders frame
            // the lower-third of the screen.
            camera.fieldOfView = 60f;
            cameraGo.transform.position = new Vector3(2.8f, 2.6f, 1.9f);
            cameraGo.transform.rotation = Quaternion.Euler(12f, 0f, 0f);
            cameraGo.AddComponent<AudioListener>();

            // Attach the over-shoulder follow component. The hero transform is
            // wired later, after BuildHero returns (BuildVillage flow).
            AddVillageComponent(cameraGo, TypeVillageCamera);

            // DEF-53: also attach SmartMobileCamera so the adaptive follow /
            // combat-zoom / auto-framing logic is available. At runtime
            // SmartMobileCamera.EnforceSoleCamera() will disable VillageCamera
            // and take over as the sole screen camera. Target wired in
            // WireVillageCameraTarget along with VillageCamera's target.
            AddVillageComponent(cameraGo, TypeSmartMobileCamera);
        }

        /// <summary>
        /// Creates an EventSystem GameObject with the new-Input-System UI module
        /// in the active scene. UI Toolkit needs this to route pointer events to
        /// button.clicked handlers (HUD buttons silent without it). No-op when
        /// one already exists.
        /// </summary>
        private static void EnsureEventSystem()
        {
            var esType = FindType(TypeEventSystem);
            if (esType == null)
            {
                Debug.LogWarning("[VillageSceneBuilder] UnityEngine.EventSystems.EventSystem " +
                                 "type not resolvable — HUD button clicks will not fire.");
                return;
            }
            var existing = UnityEngine.Object.FindObjectOfType(esType);
            if (existing != null) return;

            var go = new GameObject("EventSystem");
            go.AddComponent(esType);

            var moduleType = FindType(TypeInputSystemUIInputModule);
            if (moduleType != null) go.AddComponent(moduleType);
            else Debug.LogWarning("[VillageSceneBuilder] InputSystemUIInputModule type not " +
                                  "resolvable — falling back to EventSystem-only routing.");
        }

        /// <summary>
        /// Wires VillageHudController.BuildRequested → BuildMenu.Open so the
        /// HUD's Build button actually opens the placement menu.
        /// </summary>
        private static void WireBuildMenuHudBridge()
        {
            var buildMenuType = FindType(TypeBuildMenu);
            var hudType = FindType(TypeVillageHudController);
            var bridgeType = FindType(TypeBuildMenuHudBridge);
            if (buildMenuType == null || hudType == null || bridgeType == null) return;

            var buildMenu = UnityEngine.Object.FindObjectOfType(buildMenuType);
            var hud = UnityEngine.Object.FindObjectOfType(hudType);
            if (buildMenu == null || hud == null) return;

            var menuGo = ((Component)buildMenu).gameObject;
            var bridge = menuGo.GetComponent(bridgeType) ?? menuGo.AddComponent(bridgeType);

            var so = new SerializedObject(bridge);
            SetObjectField(so, "_hud", (UnityEngine.Object)hud);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Wires VillageHudController.AbilityRequested → HeroAbilities.TryCast
        /// via a bridge component on the hero, so the HUD's Q/W/E/R buttons
        /// actually cast (clicks were dead before this — 2026-05-20).
        /// </summary>
        private static void WireHeroAbilitiesHudBridge()
        {
            var heroAbilitiesType = FindType(TypeHeroAbilities);
            var hudType = FindType(TypeVillageHudController);
            var bridgeType = FindType(TypeHeroAbilitiesHudBridge);
            if (heroAbilitiesType == null || hudType == null || bridgeType == null) return;

            var abilities = UnityEngine.Object.FindObjectOfType(heroAbilitiesType);
            var hud = UnityEngine.Object.FindObjectOfType(hudType);
            if (abilities == null || hud == null) return;

            var heroGo = ((Component)abilities).gameObject;
            var bridge = heroGo.GetComponent(bridgeType) ?? heroGo.AddComponent(bridgeType);

            var so = new SerializedObject(bridge);
            SetObjectField(so, "_hud", (UnityEngine.Object)hud);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Adds a <c>BuildingInteractable</c> to every <c>Building</c> in the
        /// scene so walking near one shows the "Press F" prompt + dispatches.
        /// </summary>
        private static void WireBuildingInteractables()
        {
            var buildingType = FindType(TypeBuilding);
            var interactableType = FindType(TypeBuildingInteractable);
            if (buildingType == null || interactableType == null) return;
            foreach (var b in UnityEngine.Object.FindObjectsByType(
                         buildingType, FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (b is Component c && c.GetComponent(interactableType) == null)
                    c.gameObject.AddComponent(interactableType);
            }
        }
    }
}
