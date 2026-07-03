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
    // WO-181: hero, townsfolk, pet, build menu, marketplace -- split out of VillageSceneBuilder.cs. Same partial class; moves only.
    public static partial class VillageSceneBuilder
    {
        private static GameObject BuildHero(Transform parent, Component heart, Vector3 heartPos)
        {
            var go = new GameObject("Hero (Blaise)");
            go.transform.SetParent(parent, false);
            // WO-102: tag the hero so HeroAbilities / camera / pet systems can
            // find it via GameObject.FindWithTag("Player") without a type lookup.
            go.tag = "Player";
            // Open plaza spot — far enough from every 4.5x-scaled building that
            // the OTS camera doesn't spawn looking into a wall. World coords
            // chosen against the building manifest at lines 928 / 1042-1095:
            // nearest neighbour (Tavern at 11,-8.5) is ~10 m away, comfortable.
            go.transform.position = new Vector3(6f, 0f, 4f);

            // Hero body — KayKit Protagonist_A.fbx (Mystery Series 5). The
            // primitive Capsule stays on the root as an INVISIBLE collider so
            // wall collision still works (the Protagonist mesh has its own
            // colliders, but those are stripped to keep nav clean).
            var collider = go.AddComponent<CapsuleCollider>();
            collider.height = 2f;
            collider.radius = 0.4f;
            collider.center = new Vector3(0f, 1f, 0f);

            // DEF-221: the baked placeholder is now the PEOPLE Mage body
            // (Resources/Heroes/Mage.fbx = Human_Wizard, Humanoid) + Mage.controller,
            // NOT the retired Assets/Models/Wizard/Wizard.fbx. HeroBodySwapper still
            // swaps to the chosen class's Resources/Heroes/<slug>.fbx at runtime; this
            // just makes the DEFAULT/Mage hero the new People body instead of the old one.
            const string HeroMeshPath = "Assets/Resources/Heroes/Mage.fbx";
            const string HeroAnimatorPath = "Assets/Resources/Heroes/Mage.controller";
            var heroModel = LoadModel(HeroMeshPath);
            GameObject body = null;
            if (heroModel != null)
            {
                body = (GameObject)PrefabUtility.InstantiatePrefab(heroModel);
            }
            if (body == null)
            {
                Debug.LogWarning("[VillageSceneBuilder] Hero mesh '" + HeroMeshPath +
                                 "' not found — falling back to violet capsule placeholder.");
                body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                ApplyColor(body, HexColor("9d6fff"));
            }
            body.name = "HeroBody";
            body.transform.SetParent(go.transform, false);
            body.transform.localPosition = Vector3.zero;
            // KayKit Protagonist FBX imports at a non-uniform native size that
            // dwarfs the hero rig when dropped in raw. Normalise to ~2 m so the
            // OTS camera framing (tuned against the original capsule placeholder)
            // still reads correctly.
            if (heroModel != null) NormalizeProp(body, 2.0f);
            // Strip native KayKit colliders on the mesh; the hero-root capsule
            // collider above is the single source of truth for wall collision.
            StripColliders(body);
            // Strip any default Rigidbody the Tripo / KayKit import may have
            // dropped on the root — gravity on the hero pulled the wizard
            // through the village floor (2026-05-20 PO ticket).
            StripRigidbodies(body);

            // Wire the AnimatorController (built by WizardAnimatorSetup) so the
            // hero plays Idle / Walk / Cast. HeroLocomotion drives SetFloat
            // "Speed"; HeroAbilities already drives SetTrigger "Cast" (line 88).
            if (heroModel != null)
            {
                var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(HeroAnimatorPath);
                if (ctrl != null)
                {
                    var anim = body.GetComponentInChildren<Animator>();
                    if (anim != null)
                    {
                        anim.runtimeAnimatorController = ctrl;
                        anim.applyRootMotion = false;
                    }
                }
                else
                {
                    Debug.LogWarning("[VillageSceneBuilder] Wizard.controller not found at " +
                                     $"'{HeroAnimatorPath}' — run Defenders > Animation > " +
                                     "Setup Wizard Animator first.");
                }
            }

            // Hero faces +Z by default (toward the open plaza). Explicit reset
            // so HeroLocomotion's LookRotation chain starts from a known yaw.
            go.transform.rotation = Quaternion.identity;

            // Runtime body swap — replaces the Wizard placeholder with the
            // FBX matching the player's chosen HeroClass (Knight / Ranger).
            // No-op for Mage. Loads from Resources/Heroes/<slug>.fbx.
            AddVillageComponent(go, TypeHeroBodySwapper);

            // Walking input — WASD / arrows / dpad / left stick (new Input System).
            AddVillageComponent(go, TypeHeroLocomotion);

            var comp = AddVillageComponent(go, TypeHeroAbilities);
            if (comp == null) return go;

            // Ability input — 1/2/3/4 + gamepad face buttons → TryCast (Q/W/E/R
            // slots). 1-4 chosen over Q-W-E-R to avoid the W movement conflict.
            AddVillageComponent(go, TypeHeroAbilityInput);

            // Cinemachine rig DISABLED 2026-05-20: was putting the camera in
            // unexpected positions (hero appeared to fall off the world when
            // viewed from camera). Falling back to the hand-rolled
            // VillageCamera which we tuned earlier.
            // AddVillageComponent(go, TypeHeroCinemachineRig);

            var so = new SerializedObject(comp);
            // _heart — Healing Beacon (E) restores Heart HP.
            if (heart != null) SetObjectField(so, "_heart", heart);
            // _enemyMask — the ability hit-tests sweep only the Enemy layer.
            SetLayerMaskField(so, "_enemyMask", 1 << EnemyLayer);
            so.ApplyModifiedPropertiesWithoutUndo();
            return go;
        }

        // =====================================================================
        //  Ambient townsfolk (Workstream D) — wandering / idle KayKit villagers
        // =====================================================================

        /// <summary>
        /// One townsperson placement: a world spot, an archetype, whether it
        /// wanders, and a facing yaw for idlers.
        /// </summary>
        private struct TownsfolkSpot
        {
            public Vector3 Pos;
            public int Archetype;   // TownsfolkDialogue.Archetype ordinal
            public bool Wander;
            public float FacingY;
        }

        /// <summary>
        /// Populates the village with ambient townsfolk — KayKit civilian models
        /// carrying an <see cref="TypeAmbientNpc"/> component and a self-building
        /// <see cref="TypeTownsfolkBubble"/> word bubble. Some wander the baked
        /// NavMesh, some stand idle at authored spots. A <see cref="TypeTownsfolkController"/>
        /// hands every villager the Keeper transform for the proximity dialogue.
        ///
        /// <para>All townsfolk types live in DeNelle.Village and are wired by
        /// full-name reflection — the Editor asmdef cannot reference that module.
        /// Returns the count placed.</para>
        /// </summary>
        /// <param name="root">The VillageRoot transform.</param>
        /// <param name="heartPos">The Heart's world position — the plaza centre.</param>
        /// <param name="hero">The hero rig the townsfolk watch (may be null).</param>
        private static int BuildTownsfolk(Transform root, Vector3 heartPos, GameObject hero)
        {
            var npcType = FindType(TypeAmbientNpc);
            var bubbleType = FindType(TypeTownsfolkBubble);
            var controllerType = FindType(TypeTownsfolkController);
            if (npcType == null || bubbleType == null)
            {
                Debug.LogError("[VillageSceneBuilder] DeNelle.Village.AmbientNPC / TownsfolkBubble " +
                               "not found -- is the DeNelle.Village assembly compiled? " +
                               "Ambient townsfolk skipped.");
                return 0;
            }

            var townsfolkRoot = NewChild(root, "Townsfolk");

            // The TownsfolkController coordinator — distributes the hero ref.
            Component controller = null;
            if (controllerType != null)
                controller = townsfolkRoot.gameObject.AddComponent(controllerType);
            else
                Debug.LogWarning("[VillageSceneBuilder] TownsfolkController type not found -- " +
                                 "townsfolk fall back to self-resolving the hero.");

            // ── Authored placements ──────────────────────────────────────────
            // Spots sit on the plaza / road network (on the baked NavMesh) and
            // around the named building quarters, so the village reads alive
            // from the camera. Archetype ordinals follow TownsfolkDialogue:
            //   0 Trader · 1 Villager · 2 Guard · 3 Child · 4 Elder
            // X grows east, Z grows north; the plaza is centred on the Heart.
            // Owner direction 2026-05-20: village feels crowded — cut the
            // ambient roster from 10 to 4. Keep one wanderer + one idler on
            // the plaza (the lively core), one off-duty guard near the gate
            // spine, and one trader at the market so each archetype still
            // appears once.
            var spots = new[]
            {
                // Plaza — the lively heart of the town.
                new TownsfolkSpot { Pos = heartPos + new Vector3( 4f, 0f,  5f), Archetype = 1, Wander = true,  FacingY = 200f },
                new TownsfolkSpot { Pos = heartPos + new Vector3( 2f, 0f, -6f), Archetype = 4, Wander = false, FacingY =   0f },
                // Market quarter (the church / market / tavern cluster).
                new TownsfolkSpot { Pos = new Vector3(-10f, 0f, -7f), Archetype = 0, Wander = false, FacingY =  90f },
                // DEF-220: the Blacksmith stands AT the Forge (plot 20,-10 / ForgeYard
                // front) by his anvil — stationary, facing the smithy. (Was an off-duty
                // guard wandering the N gate spine; moved here so the smith works the
                // forge. VillageNpcInjector mirrors this position by index.)
                new TownsfolkSpot { Pos = new Vector3( 17.5f, 0f, -8f), Archetype = 2, Wander = false, FacingY =  35f },
            };

            int placed = 0;
            for (int i = 0; i < spots.Length; i++)
            {
                if (BuildOneTownsperson(townsfolkRoot, spots[i], i, npcType, bubbleType))
                    placed++;
            }

            // Hand the hero transform to the controller (it broadcasts to every
            // NPC on Start); the NPCs also self-resolve as a fallback.
            if (controller != null && hero != null)
                InvokeConfigure(controller, "SetHero", hero.transform);

            Debug.Log($"[VillageSceneBuilder] Ambient townsfolk placed -- {placed}/{spots.Length} " +
                      "villagers (wanderers + idlers) with engage-on-approach word bubbles.");
            return placed;
        }

        /// <summary>
        /// Builds a single ambient townsperson at <paramref name="spot"/>: a
        /// KayKit civilian model (round-robin over the four catalog civilians),
        /// an <c>AmbientNPC</c>, a <c>TownsfolkBubble</c> and — for a wanderer —
        /// a NavMeshAgent. Returns true on success.
        /// </summary>
        private static bool BuildOneTownsperson(Transform parent, TownsfolkSpot spot,
            int index, Type npcType, Type bubbleType)
        {
            var go = new GameObject($"Townsperson_{index:00}");
            go.transform.SetParent(parent, false);
            go.transform.position = spot.Pos;
            go.transform.rotation = Quaternion.Euler(0f, spot.FacingY, 0f);

            // KayKit civilian model — round-robin over Protagonist A/B + Helper
            // A/B (the catalog's named townsfolk stand-ins). Placeholder capsule
            // on a miss, matching the rest of the builder's fallback discipline.
            //
            // NOTE — InstantiateModel() force-assigns the shared MEDIEVAL HEX
            // ATLAS material (correct for the Hexagon-pack buildings, wrong for
            // a character). Townsfolk are instantiated directly here so the FBX
            // importer's own character materials/textures are kept intact.
            string modelPath = TownsfolkModelPaths[index % TownsfolkModelPaths.Length];
            var model = LoadModel(modelPath);
            GameObject visual;
            if (model != null)
            {
                visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
                if (visual == null)
                {
                    visual = MakePlaceholderCube(
                        $"{Path.GetFileName(modelPath)} -> ambient townsperson");
                    model = null;   // fall through to the placeholder styling
                }
                else
                {
                    visual.name = model.name;
                }
            }
            else
            {
                visual = MakePlaceholderCube(
                    $"{Path.GetFileName(modelPath)} -> ambient townsperson");
            }
            visual.transform.SetParent(go.transform, false);
            visual.transform.localPosition = Vector3.zero;
            if (model == null)
            {
                // Placeholder body — a warm capsule so a missing model still
                // reads as a person standing in the town.
                visual.transform.localScale = new Vector3(0.55f, 0.95f, 0.55f);
                visual.transform.localPosition = new Vector3(0f, 0.95f, 0f);
                ApplyColor(visual, new Color(0.72f, 0.60f, 0.46f));
            }
            else
            {
                NormalizeProp(visual, 1.8f);   // size every civilian to ~human height
            }
            // The civilian mesh is decoration — its colliders must not block the
            // hero's tap-to-move raycast or shadow a structure ahead.
            StripColliders(visual);

            // AmbientNPC — the wander / idle + proximity-dialogue behaviour.
            var npc = go.AddComponent(npcType);

            // A NavMeshAgent for wanderers so they roam the baked village mesh.
            if (spot.Wander)
            {
                var agent = go.AddComponent<UnityEngine.AI.NavMeshAgent>();
                agent.radius = 0.34f;
                agent.height = 1.8f;
                agent.baseOffset = 0f;
                agent.speed = 1.6f;
                agent.angularSpeed = 240f;
                agent.acceleration = 12f;
                agent.stoppingDistance = 0.2f;
                agent.autoBraking = true;
                agent.obstacleAvoidanceType =
                    UnityEngine.AI.ObstacleAvoidanceType.LowQualityObstacleAvoidance;
            }

            // TownsfolkBubble — the self-building, billboarded word bubble.
            var bubble = go.AddComponent(bubbleType);

            // Wire the AmbientNPC's serialized fields + configure it.
            var so = new SerializedObject(npc);
            SetObjectField(so, "_bubble", bubble);
            so.ApplyModifiedPropertiesWithoutUndo();

            // Configure(archetype, wander, homeAnchor) — homeAnchor is the spot.
            InvokeConfigure(npc, "Configure", spot.Archetype, spot.Wander, spot.Pos);
            // SetBubble is belt-and-braces in case the serialized wire is skipped.
            InvokeConfigure(npc, "SetBubble", bubble);

            return true;
        }

        // =====================================================================
        //  PetDeployer
        // =====================================================================

        /// <summary>
        /// Adds the <c>PetDeployer</c> GameObject, sets its Heart position +
        /// enemy LayerMask, and flips on its <c>_autoDeployOnStart</c> flag so it
        /// deploys the three starter pets itself on Start() — no separate runtime
        /// caller needed (week4-hero-pets-gate.md item 4).
        /// </summary>
        private static void BuildPetDeployer(Transform parent, Vector3 heartPos)
        {
            var go = new GameObject("PetDeployer");
            go.transform.SetParent(parent, false);

            var type = FindType(TypePetDeployer);
            if (type == null)
            {
                Debug.LogError("[VillageSceneBuilder] DeNelle.Pets.PetDeployer not found -- " +
                               "is the DeNelle.Pets assembly compiled? Pet deployer skipped.");
                return;
            }
            var comp = go.AddComponent(type);

            var so = new SerializedObject(comp);
            // _heartPosition — the centre of the pet deploy ring (a plain Vector3
            // so DeNelle.Pets never references DeNelle.Village).
            var heartProp = so.FindProperty("_heartPosition");
            if (heartProp != null) heartProp.vector3Value = heartPos;
            // _enemyMask — pets hunt only the Enemy layer.
            SetLayerMaskField(so, "_enemyMask", 1 << EnemyLayer);
            // _autoDeployOnStart — the deployer runs DeployStarterPets() itself.
            var autoProp = so.FindProperty("_autoDeployOnStart");
            if (autoProp != null) autoProp.boolValue = true;
            else Debug.LogWarning("[VillageSceneBuilder] PetDeployer._autoDeployOnStart not found -- " +
                                  "pets will not auto-deploy; call DeployStarterPets() at runtime.");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // =====================================================================
        //  BuildMenu UIDocument
        // =====================================================================

        /// <summary>
        /// Builds the build-menu <c>UIDocument</c> GameObject: a UIDocument whose
        /// source asset is <c>BuildMenu.uxml</c> plus the <c>BuildMenu</c>
        /// component, with the build camera, ground LayerMask and the five
        /// building prefabs wired (week4-buildings.md items 1, 3, 4, 5). The
        /// panel hides itself in OnEnable until the HUD's Build button calls
        /// Open() (item 2 — the HUD button wire — is left to the HUD pass).
        /// </summary>
        private static void BuildBuildMenu(Transform parent, List<BuiltBuildingPrefab> buildingPrefabs)
        {
            var go = new GameObject("BuildMenu");
            go.transform.SetParent(parent, false);

            // UIDocument needs a PanelSettings asset; reuse the project's if one
            // exists. Without it the document still serializes — the integrator
            // can assign PanelSettings in the inspector.
            var uiDoc = go.AddComponent<UIDocument>();
            var uxml = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.VisualTreeAsset>(BuildMenuUxmlPath);
            if (uxml != null)
            {
                uiDoc.visualTreeAsset = uxml;
            }
            else
            {
                Debug.LogWarning($"[VillageSceneBuilder] BuildMenu.uxml not found at '{BuildMenuUxmlPath}' -- " +
                                 "assign the UIDocument source asset in the inspector.");
            }
            WirePanelSettings(uiDoc);

            // BuildMenu component — [RequireComponent(typeof(UIDocument))] is
            // already satisfied.
            var comp = AddVillageComponent(go, TypeBuildMenu);
            if (comp == null) return;

            var so = new SerializedObject(comp);
            // _document — the UIDocument on this GameObject.
            SetObjectField(so, "_document", uiDoc);
            // _buildCamera — leave blank: BuildMenu.Awake() defaults to Camera.main
            //   (the village Main Camera). Explicitly wiring it would need a
            //   FindAnyObjectByType<Camera>; the default is the documented behaviour.
            // _groundMask — the placement raycast must hit only the ground. The
            //   village ground tiles are on the Default layer (layer 0); restrict
            //   the mask to Default so the raycast does not snag on buildings.
            SetLayerMaskField(so, "_groundMask", 1 << 0);

            // _buildingPrefabs — the serialized List<BuildingPrefabEntry>. Each
            // entry is a struct { BuildingType Type; GameObject Prefab; }.
            WireBuildingPrefabList(so, "_buildingPrefabs", buildingPrefabs);

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // =====================================================================
        //  Onboarding / first-run tutorial (WO-133)
        // =====================================================================

        /// <summary>
        /// Builds the first-run tutorial GameObject (WO-133): a UIDocument hosting
        /// TutorialOverlay.uxml plus the <c>DeNelle.Onboarding.OnboardingFlow</c>
        /// component, sorting ABOVE the VillageHud document so the coach-marks paint
        /// over the HUD. <c>_runOnStart</c> stays true — OnboardingFlow.Start()
        /// checks GameState.Onboarded itself and shows the tutorial only on a first
        /// run; a returning player gets TutorialClosed raised immediately.
        ///
        /// The overlay's UXML is wired as an editor reference, but OnboardingFlow
        /// builds a code-built coach-mark overlay at runtime when the UXML loads no
        /// elements (PIPELINE_STATE §8 — UXML does not render in player builds), so
        /// the FTUE renders in the build. The five gameplay SEAMS are wired at
        /// runtime by OnboardingIntegrator (attached by VillageController) — the
        /// builder only PLACES the object (DeNelle.Editor cannot reference the
        /// Onboarding/Village runtime types beyond reflection).
        /// </summary>
        private static void BuildOnboardingFlow(Transform parent)
        {
            var go = new GameObject("OnboardingFlow");
            go.transform.SetParent(parent, false);

            var uiDoc = go.AddComponent<UIDocument>();
            var uxml = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.VisualTreeAsset>(TutorialOverlayUxmlPath);
            if (uxml != null) uiDoc.visualTreeAsset = uxml;
            else Debug.LogWarning($"[VillageSceneBuilder] TutorialOverlay.uxml not found at " +
                                  $"'{TutorialOverlayUxmlPath}' -- OnboardingFlow builds the overlay in code anyway.");
            WirePanelSettings(uiDoc);
            // Sort ABOVE the HUD (HUD = 0, BuildMenu adopts HUD+5) so the coach-marks
            // paint over everything (OnboardingFlow integrator note §1).
            uiDoc.sortingOrder = 100;

            var comp = AddVillageComponent(go, TypeOnboardingFlow);   // FindType resolves DeNelle.Onboarding
            if (comp == null)
            {
                Debug.LogWarning("[VillageSceneBuilder] OnboardingFlow type not found -- " +
                                 "is the DeNelle.Onboarding assembly compiled? First-run tutorial not placed.");
                return;
            }

            var so = new SerializedObject(comp);
            // _document — the UIDocument on this GameObject.
            SetObjectField(so, "_document", uiDoc);
            // _runOnStart stays at its serialized default (true); the gate is the
            // Onboarded check inside OnboardingFlow.Start/TryRun, not this flag.
            so.ApplyModifiedPropertiesWithoutUndo();

            Debug.Log("[VillageSceneBuilder] OnboardingFlow (FTUE) placed -- UIDocument sortingOrder 100 " +
                      "(above HUD); seams wired at runtime by OnboardingIntegrator.");
        }

        /// <summary>
        /// Places the village STORE: a hidden PackStoreUI (UIDocument + PackStore.uxml +
        /// PackStore) and a walk-up Marketplace trigger (MarketplaceInteractor) by the
        /// south-plaza market. Walk near it → "[F] The Realm Store" → opens the packs.
        /// MarketplaceInteractor auto-finds PackStore; we wire it explicitly anyway.
        /// </summary>
        private static void BuildMarketplace(Transform parent)
        {
            // 1) The hidden store UI document (UIDocument + PackStore.uxml + PackStore).
            var uiGo = new GameObject("PackStoreUI");
            uiGo.transform.SetParent(parent, false);
            var uiDoc = uiGo.AddComponent<UIDocument>();
            var uxml = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.VisualTreeAsset>(PackStoreUxmlPath);
            if (uxml != null) uiDoc.visualTreeAsset = uxml;
            else Debug.LogWarning($"[VillageSceneBuilder] PackStore.uxml not found at '{PackStoreUxmlPath}'.");
            WirePanelSettings(uiDoc);

            var ps = AddVillageComponent(uiGo, TypePackStore);   // FindType resolves DeNelle.Wallet too
            if (ps != null)
            {
                var pso = new SerializedObject(ps);
                SetObjectField(pso, "_document", uiDoc);
                pso.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning("[VillageSceneBuilder] PackStore type not found — store UI not added.");
            }
            uiGo.SetActive(false);   // starts hidden; MarketplaceInteractor enables it on F

            // 2) The walk-up trigger near the south-plaza market (WO-101 Market at ~(0,0,-20)).
            var mGo = new GameObject("Marketplace");
            mGo.transform.SetParent(parent, false);
            mGo.transform.position = new Vector3(0f, 0f, -18f);
            var mi = AddVillageComponent(mGo, TypeMarketplaceInteractor);
            if (mi != null)
            {
                var mso = new SerializedObject(mi);
                SetObjectField(mso, "_storeUiRoot", uiGo);
                mso.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[VillageSceneBuilder] Marketplace + PackStoreUI placed — walk up + F opens the store.");
            }
            else
            {
                Debug.LogWarning("[VillageSceneBuilder] MarketplaceInteractor type not found — store trigger not added.");
            }
        }

        /// <summary>
        /// Assigns a PanelSettings asset to a UIDocument — finds the first one in
        /// the project. UI Toolkit needs PanelSettings to render; without it the
        /// menu is invisible. No-op (with a warning) when none exists.
        /// </summary>
        private static void WirePanelSettings(UIDocument uiDoc)
        {
            if (uiDoc == null || uiDoc.panelSettings != null) return;
            var guids = AssetDatabase.FindAssets("t:PanelSettings");
            if (guids != null && guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                var panel = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.PanelSettings>(path);
                if (panel != null) { uiDoc.panelSettings = panel; return; }
            }
            Debug.LogWarning("[VillageSceneBuilder] No PanelSettings asset found -- assign one to the " +
                             "BuildMenu UIDocument in the inspector or the build menu will not render.");
        }

        /// <summary>
        /// Fills the serialized <c>List&lt;BuildingPrefabEntry&gt;</c> on BuildMenu
        /// — one entry per built building prefab, each with its BuildingType
        /// ordinal + the prefab reference.
        /// </summary>
        private static void WireBuildingPrefabList(SerializedObject so, string field,
            List<BuiltBuildingPrefab> prefabs)
        {
            var prop = so.FindProperty(field);
            if (prop == null || !prop.isArray)
            {
                Debug.LogWarning($"[VillageSceneBuilder] Serialized list '{field}' not found / not an array " +
                                 $"on {so.targetObject.GetType().Name} -- building prefabs not wired.");
                return;
            }

            prop.arraySize = prefabs.Count;
            for (int i = 0; i < prefabs.Count; i++)
            {
                var element = prop.GetArrayElementAtIndex(i);
                // BuildingPrefabEntry struct fields: `Type` (enum) + `Prefab` (GO).
                var typeProp = element.FindPropertyRelative("Type");
                var prefabProp = element.FindPropertyRelative("Prefab");
                if (typeProp != null) typeProp.enumValueIndex = prefabs[i].TypeOrdinal;
                if (prefabProp != null) prefabProp.objectReferenceValue = prefabs[i].Prefab;
            }
        }

        // =====================================================================
        //  NavMesh bake -- legacy UnityEditor.AI API (com.unity.modules.ai)
        // =====================================================================

        /// <summary>
        /// Marks the village ground + wall + building geometry navigation-static
        /// and bakes a NavMesh for the open Village scene. Uses the legacy
        /// <c>UnityEditor.AI.NavMeshBuilder</c> API -- the manifest carries
        /// <c>com.unity.modules.ai</c>, NOT the high-level
        /// <c>com.unity.ai.navigation</c> package (week4-waves.md item 1).
        ///
        /// REQUIRED for the wave loop: <c>Enemy</c> uses a NavMeshAgent and
        /// cannot move without a baked NavMesh.
        /// </summary>
        /// <summary>
        /// True when <paramref name="t"/> sits under an outer perimeter gatehouse
        /// (BuildWallPerimeter names them "Gate-North-Main" / "Gate-East-Side" etc.,
        /// parented to "WallPerimeter" under the "Walls" root). Those scale-10 arch
        /// meshes must be left OUT of the NavMesh bake or they voxelize solid across
        /// the opening and seal the gate — see DEF gate-nav fix in BakeVillageNavMesh.
        /// Walks parents up to (and stopping at) <paramref name="stopAt"/> so it never
        /// escapes the Walls subtree.
        /// </summary>
        private static bool IsUnderPerimeterGate(Transform t, Transform stopAt)
        {
            for (var p = t; p != null && p != stopAt; p = p.parent)
            {
                if (p.name.StartsWith("Gate-", System.StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// True for the moat WATER tiles and the DRAWBRIDGE planks (both live under
        /// "Walls/Moat"). DEF gate-nav fix (2026-05-30): BuildMoat parents the moat
        /// under the Walls root, so the nav-static sweep was baking the water surface
        /// (y = -0.4) and the raised plank lip into the NavMesh — fragmenting it right
        /// at each gate crossing, which dead-ended the hero "at the wood plank." The
        /// hero is meant to cross on the flat Ground/Approach surface through the 6 m
        /// opening (same philosophy as the excluded gate arch); the moat + bridge are
        /// decoration, so they stay OUT of the bake. Matched by name so it is robust to
        /// the Moat root's parenting.
        /// </summary>
        private static bool IsNonWalkableMoatPiece(Transform t)
        {
            for (var p = t; p != null; p = p.parent)
            {
                string n = p.name;
                if (n.StartsWith("MoatTile", System.StringComparison.Ordinal) ||
                    n.StartsWith("Drawbridge", System.StringComparison.Ordinal) ||
                    n == "Moat")
                    return true;
            }
            return false;
        }
    }
}
