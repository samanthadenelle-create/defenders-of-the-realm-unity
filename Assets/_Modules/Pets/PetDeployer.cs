// =============================================================================
// PetDeployer — spawns the three starter pets at slots ringing the Heart.
// -----------------------------------------------------------------------------
// Port spec Part 5 Week 4: "Pets: deploy the three starter pets (Aether Sprite
// / Flame Pup / Ice Wolf) at slots near the Heart." PetDeployer reads the
// starter pet defs from PetCatalog (pets.json) and instantiates one Pet per
// def at its deploy slot — PetCatalog.DeploySlotPosition() is the verbatim
// port of petData.ts petPost() (a ring at radius 11 around the Heart).
//
// MODULE ISOLATION (port spec Part 2): the deployer takes the Heart's world
// position as a plain Vector3 (and the enemy LayerMask), so DeNelle.Pets never
// references DeNelle.Village. The integrator wires the Heart position +
// per-species bond ranks from VillageController; see week4-hero-pets-gate.md.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace DeNelle.Pets
{
    /// <summary>
    /// Spawns + tracks the three starter guardian pets. Given the Heart's world
    /// position, it places one <see cref="Pet"/> per starter def at its deploy
    /// slot on the ring around the Heart. The integrator calls
    /// <see cref="DeployStarterPets"/> once the village scene is up.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PetDeployer : MonoBehaviour
    {
        [Header("Pet prefab")]
        [Tooltip("Prefab carrying a Pet component. When null, the deployer builds " +
                 "a placeholder primitive (the KayKit pet meshes import later).")]
        [SerializeField] private Pet _petPrefab;

        [Header("Deploy config (wired by the integrator)")]
        [Tooltip("World position of the Heart — the centre of the pet deploy ring. " +
                 "Passed as a plain Vector3 so Pets never references the Village module.")]
        [SerializeField] private Vector3 _heartPosition = Vector3.zero;

        [Tooltip("Layers the spawned pets sweep for IDamageable enemies. Set to the village Enemy layer.")]
        [SerializeField] private LayerMask _enemyMask = ~0;

        [Tooltip("Per-species bond ranks, indexed [Aether, Flame, Ice]. " +
                 "The integrator copies these from GameState.petBonds.")]
        [SerializeField] private int[] _bondRanks = { 0, 0, 0 };

        [Tooltip("Deploy mode for the starter pets — Defend = hunt the nearest enemy.")]
        [SerializeField] private PetMode _deployMode = PetMode.Defend;

        [Header("Auto-deploy")]
        [Tooltip("When true, the deployer runs DeployStarterPets() itself on Start() " +
                 "using the serialized Heart position / enemy mask / bond ranks above. " +
                 "The village scene builder sets this so a fresh scene deploys its " +
                 "pets without needing a separate runtime caller. Off: the integrator " +
                 "calls DeployStarterPets() by hand once the scene context is ready.")]
        [SerializeField] private bool _autoDeployOnStart;

        private readonly List<Pet> _deployed = new List<Pet>();

        /// <summary>The pets currently deployed in the scene.</summary>
        public IReadOnlyList<Pet> DeployedPets => _deployed;

        /// <summary>
        /// Sets the Heart's world position — the centre of the deploy ring.
        /// The integrator calls this from VillageController before deploying.
        /// </summary>
        public void SetHeartPosition(Vector3 heartPosition) => _heartPosition = heartPosition;

        /// <summary>Sets the enemy LayerMask the spawned pets hunt against.</summary>
        public void SetEnemyMask(LayerMask enemyMask) => _enemyMask = enemyMask;

        /// <summary>
        /// Sets per-species bond ranks (indexed [Aether, Flame, Ice]) — copied
        /// from GameState.petBonds by the integrator.
        /// </summary>
        public void SetBondRanks(int aether, int flame, int ice)
        {
            _bondRanks = new[] { aether, flame, ice };
        }

        /// <summary>
        /// When <see cref="_autoDeployOnStart"/> is set (the village scene
        /// builder enables it), deploys the starter pets from the serialized
        /// config. Otherwise the integrator calls <see cref="DeployStarterPets"/>
        /// by hand once the scene context is ready.
        /// </summary>
        private void Start()
        {
            if (_autoDeployOnStart) DeployStarterPets();
        }

        /// <summary>
        /// Instantiates the three starter pets (Aether Sprite / Flame Pup / Ice
        /// Wolf) at their deploy slots ringing the Heart. Safe to call once per
        /// village-scene load; clears any prior deployment first.
        /// </summary>
        // TEMP DIAG 2026-05-25 (owner: "try with no pet"): hard-skip all pet
        // deployment so the village has ZERO pets — isolates the camera/hero so
        // there's nothing else on screen to mistake the follow target for.
        // REVERT to false to restore the three starter pets.
        private const bool DIAG_SKIP_ALL_PETS = false;

        public void DeployStarterPets()
        {
            ClearDeployed();
            if (DIAG_SKIP_ALL_PETS)
            {
                Debug.Log("[PetDeployer] DIAG_SKIP_ALL_PETS — no pets deployed (camera/hero isolation test).");
                return;
            }

            var defs = PetCatalog.Pets;
            if (defs == null || defs.Count == 0)
            {
                Debug.LogError("[PetDeployer] PetCatalog has no pet defs — pets.json missing or empty.");
                return;
            }

            foreach (var def in defs)
            {
                if (def == null) continue;
                // SINGLE starter pet until the others are EARNED (owner 2026-05-30) — only the
                // ice-wolf (CC5 companion) deploys for now; unlock the rest via bond progression.
                if (def.Species != "ice-wolf") continue;
                Vector3 slot = PetCatalog.DeploySlotPosition(def.SlotIndex, _heartPosition);
                int bond = BondRankFor(def.SlotIndex);

                Pet pet = SpawnPet(def, slot);
                pet.Configure(def, bond, slot, _deployMode);
                pet.SetEnemyMask(_enemyMask);

                // Level progression: attach AFTER Configure so PetId is set when
                // PetProgression enables and registers under it (XP system).
                if (pet.GetComponent<PetProgression>() == null)
                    pet.gameObject.AddComponent<PetProgression>();

                _deployed.Add(pet);
            }
        }

        /// <summary>Destroys every deployed pet (e.g. on village-scene teardown).</summary>
        public void ClearDeployed()
        {
            foreach (var pet in _deployed)
                if (pet != null) Destroy(pet.gameObject);
            _deployed.Clear();
        }

        private Pet SpawnPet(PetDef def, Vector3 slot)
        {
            Pet pet;
            if (_petPrefab != null)
            {
                pet = Instantiate(_petPrefab, slot, Quaternion.identity, transform);
            }
            else
            {
                var go = new GameObject($"Pet_{def.Species}_root");
                go.transform.SetParent(transform, false);
                go.transform.position = slot;

                // Try a hand-imported FBX first (Resources/Pets/<species>.fbx)
                // before falling back to the tinted-capsule placeholder.
                // 2026-05-20: aether-sprite (fairy) + ice-wolf (fox) Tripo FBXs
                // landed; flame-pup still capsule until its mesh ships.
                GameObject visual = TryLoadPetMesh(def);
                if (visual != null)
                {
                    visual.transform.SetParent(go.transform, false);
                    visual.transform.localPosition = Vector3.zero;
                    // CC5 / game-export models already face +Z, so NO yaw flip. The old
                    // hardcoded 180° (for Tripo FBXs that exported facing -Z) is what made
                    // the CC5 pet "always travel backwards" (owner 2026-05-30). Identity =
                    // forward matches Pet.FaceToward's LookRotation (+Z forward).
                    visual.transform.localRotation = Quaternion.identity;
                    NormalizePetHeight(visual, 1.1f);
                    StripPetColliders(visual);
                    // Tripo FBXs embed a CAMERA node (and sometimes an
                    // AudioListener). Left in, a pet's camera renders to the
                    // screen FROM the pet, so the view "follows the pet" instead
                    // of the hero's VillageCamera (root cause, 2026-05-25). Strip
                    // them so only the hero camera ever drives the display.
                    foreach (var cam in visual.GetComponentsInChildren<Camera>(true))
                        if (cam != null) Destroy(cam);
                    foreach (var al in visual.GetComponentsInChildren<AudioListener>(true))
                        if (al != null) Destroy(al);
                    // Strip any baked-in light / particle "aura" too — the affinity
                    // glow below is the single controlled source (owner 2026-05-25).
                    foreach (var lt in visual.GetComponentsInChildren<Light>(true))
                    {
                        if (lt == null) continue;
                        // URP's UniversalAdditionalLightData has [RequireComponent(Light)],
                        // so destroying the Light directly logs "Can't remove Light because
                        // UniversalAdditionalLightData depends on it" (WO-28 §4). Remove the
                        // dependent first. GetComponent(string) avoids a URP asmdef reference.
                        var lightData = lt.GetComponent("UniversalAdditionalLightData");
                        if (lightData != null) Destroy(lightData);
                        Destroy(lt);
                    }
                    foreach (var ps in visual.GetComponentsInChildren<ParticleSystem>(true))
                        if (ps != null) Destroy(ps);
                    // Tripo FBXs import with Phong materials URP can't render
                    // (owner 2026-05-20). The fixer rebuilds them as URP/Lit
                    // on Awake; if Tripo's embedded textures didn't extract,
                    // fall back to the species basecolor PNG (Tripo Send-To-
                    // Unity extract shipped to Resources/Textures/<species>.png)
                    // and then the species tint as the last-resort colour.
                    var petFixer = visual.AddComponent<DeNelle.Core.TripoMaterialFixer>();
                    if (petFixer != null && def != null)
                    {
                        petFixer.SetFallbackTexture("Textures/" + def.Species);
                        petFixer.SetFallbackTint(def.TintColor);
                        // Owner 2026-05-25: dim the pet "aura/beams" to a minimal
                        // affinity-coloured glow (fire red / ice white / aether violet).
                        petFixer.SetEmissionOverride(AffinityGlow(def.Element));
                        // WO-34 (2026-05-25): TripoAssetPostprocessor extracts the
                        // pet's materials as URP — but those extracted URP mats
                        // render washed-out/grey, and the fixer SKIPS already-URP
                        // materials by default, leaving them broken. Force a full
                        // rebuild from each material's real _BaseMap so every pet
                        // (incl. ice-wolf, which has no fallback PNG) gets its true
                        // texture. This is why pets kept coming back grey.
                        petFixer.ForceRebuildAll();
                    }
                }
                else
                {
                    // Placeholder primitive — owner direction 2026-05-20: pets
                    // were invisible because the 0.5x capsule sat 15m from the
                    // hero. Bump to chest height and let HeroLeash drag them.
                    var capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    capsule.name = "Body";
                    capsule.transform.SetParent(go.transform, false);
                    capsule.transform.localScale = new Vector3(0.8f, 0.9f, 0.8f);
                    var col = capsule.GetComponent<Collider>();
                    if (col != null) col.isTrigger = true;
                    TintPlaceholder(capsule, def);
                }

                pet = go.AddComponent<Pet>();
#if UNITY_EDITOR
                AddPetNameTag(go, def);
#endif
            }

            // Attach the hero-leash so the pet trails the hero around the
            // village instead of holding the Heart slot. Idle-mode pets just
            // walk to it; Defend-mode pets snap back to it when no enemy.
            pet.gameObject.AddComponent<PetHeroLeash>();

            pet.name = $"Pet_{def.Species}";
            return pet;
        }

        // Affinity glow colour for a pet's minimal aura (owner 2026-05-25:
        // "fire red, ice white"). Matches pets.json glow intent per element.
        private static Color AffinityGlow(string element)
        {
            switch ((element ?? "").ToLowerInvariant())
            {
                case "flame": return new Color(1.00f, 0.33f, 0.16f); // fire red  (#ff5630)
                case "ice":   return new Color(0.90f, 0.96f, 1.00f); // icy white (#e6f5ff)
                case "aether":return new Color(0.62f, 0.44f, 1.00f); // violet    (#9d6fff)
                default:      return Color.white;
            }
        }

        /// <summary>
        /// Tries to load a hand-imported FBX matching this pet's species from
        /// Resources/Pets/&lt;species&gt;.fbx — or, if the player has a cosmetic
        /// pet skin equipped, Resources/Cosmetics/Pets/&lt;cosmetic-id&gt;.fbx.
        /// Returns an instantiated GameObject (parent-less) or null if no mesh
        /// exists for that species.
        /// </summary>
        private static GameObject TryLoadPetMesh(PetDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.Species)) return null;

            // Cosmetic pet skin (Glimmer shop) overrides the base mesh. The
            // cosmetic service lives in DeNelle.Cosmetics which DeNelle.Pets
            // cannot reference directly — resolve via reflection so the asmdef
            // stays decoupled.
            string equipped = TryGetEquippedCosmeticForCategory("pet");
            if (!string.IsNullOrEmpty(equipped))
            {
                var skin = Resources.Load<GameObject>("Cosmetics/Pets/" + equipped);
                if (skin != null) return Instantiate(skin);
            }

            var prefab = Resources.Load<GameObject>("Pets/" + def.Species);
            if (prefab == null) return null;
            return Instantiate(prefab);
        }

        private static System.Type _glimmerType;
        private static object _glimmerInstance;
        private static System.Reflection.MethodInfo _equippedForMethod;

        private static string TryGetEquippedCosmeticForCategory(string category)
        {
            try
            {
                if (_glimmerType == null)
                {
                    foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                    {
                        var t = asm.GetType("DeNelle.Cosmetics.GlimmerCurrencyService", false);
                        if (t != null) { _glimmerType = t; break; }
                    }
                }
                if (_glimmerType == null) return null;
                if (_glimmerInstance == null)
                {
                    var inst = _glimmerType.GetProperty("Instance",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    _glimmerInstance = inst?.GetValue(null);
                }
                if (_glimmerInstance == null) return null;
                if (_equippedForMethod == null)
                    _equippedForMethod = _glimmerType.GetMethod("EquippedFor",
                        new[] { typeof(string) });
                return _equippedForMethod?.Invoke(_glimmerInstance, new object[] { category }) as string;
            }
            catch
            {
                return null;
            }
        }

        private static void NormalizePetHeight(GameObject go, float targetHeight)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0) return;
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            if (b.size.y <= 0.01f) return;
            float scale = targetHeight / b.size.y;
            go.transform.localScale *= scale;

            // Owner 2026-05-20 ("pets appearing halfway under the surface"):
            // Tripo pet FBXs pivot at the mesh centre. After the scale the
            // feet sink below the parent's Y=0. Recompute bounds post-scale
            // and lift the body so feet rest on the ground.
            Bounds b2 = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b2.Encapsulate(renderers[i].bounds);
            float feetOffset = b2.min.y - go.transform.position.y;
            if (feetOffset < 0f)
                go.transform.localPosition -= new Vector3(0f, feetOffset, 0f);
        }

        private static void StripPetColliders(GameObject go)
        {
            foreach (var c in go.GetComponentsInChildren<Collider>(true))
                if (c != null) Destroy(c);
        }

        private static void AddPetNameTag(GameObject parent, PetDef def)
        {
            if (def == null) return;
            var tag = new GameObject("NameTag");
            tag.transform.SetParent(parent.transform, false);
            tag.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            tag.transform.localScale = Vector3.one * 0.04f;
            var tm = tag.AddComponent<TextMesh>();
            tm.text = def.Species ?? def.Id ?? "Pet";
            tm.characterSize = 0.5f;
            tm.fontSize = 96;
            tm.alignment = TextAlignment.Center;
            tm.anchor = TextAnchor.LowerCenter;
            tm.color = def.TintColor;
            tag.AddComponent<PetNameTagBillboard>();
        }

        private int BondRankFor(int slotIndex)
        {
            if (_bondRanks == null || slotIndex < 0 || slotIndex >= _bondRanks.Length)
                return 0;
            return Mathf.Clamp(_bondRanks[slotIndex], 0, 4);
        }

        private static void TintPlaceholder(GameObject go, PetDef def)
        {
            var renderer = go.GetComponent<Renderer>();
            if (renderer == null || def == null) return;
            // Replace the GameObject.CreatePrimitive default material — it's a
            // Standard-shader asset that URP can't render (→ magenta blob,
            // owner 2026-05-20). Hand the renderer a fresh URP/Lit material
            // tinted to the pet's species colour. Falls back to legacy shaders
            // in non-URP builds.
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                            ?? Shader.Find("Standard")
                            ?? Shader.Find("Sprites/Default");
            if (shader != null)
            {
                var mat = new Material(shader);
                mat.name = $"Pet placeholder ({def.Species})";
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", def.TintColor);
                if (mat.HasProperty("_Color"))     mat.SetColor("_Color", def.TintColor);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);
                if (mat.HasProperty("_Metallic"))   mat.SetFloat("_Metallic", 0f);
                renderer.sharedMaterial = mat;
            }
            else
            {
                var mpb = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(mpb);
                mpb.SetColor("_BaseColor", def.TintColor);
                mpb.SetColor("_Color", def.TintColor);
                renderer.SetPropertyBlock(mpb);
            }
        }
    }
}
