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
        public void DeployStarterPets()
        {
            ClearDeployed();

            var defs = PetCatalog.Pets;
            if (defs == null || defs.Count == 0)
            {
                Debug.LogError("[PetDeployer] PetCatalog has no pet defs — pets.json missing or empty.");
                return;
            }

            foreach (var def in defs)
            {
                if (def == null) continue;
                Vector3 slot = PetCatalog.DeploySlotPosition(def.SlotIndex, _heartPosition);
                int bond = BondRankFor(def.SlotIndex);

                Pet pet = SpawnPet(def, slot);
                pet.Configure(def, bond, slot, _deployMode);
                pet.SetEnemyMask(_enemyMask);
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
                // Placeholder primitive — the KayKit pet meshes import later
                // (port spec Part 7). Owner direction 2026-05-20: pets were
                // invisible because the 0.5x capsule sat 15m from the hero at
                // the Heart slots. Bump to chest-height (~1.4 units) so they
                // read as "a pet" from the new wider camera, and let the
                // HeroLeash drag them to the hero each frame.
                var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.transform.SetParent(transform, false);
                go.transform.localScale = new Vector3(0.8f, 0.9f, 0.8f);
                var col = go.GetComponent<Collider>();
                if (col != null) col.isTrigger = true; // pets do not block pathing
                pet = go.AddComponent<Pet>();
                TintPlaceholder(go, def);

                // A small floating glyph (·species·) above the capsule so the
                // owner can tell the trio apart even before the KayKit meshes
                // drop in.
                AddPetNameTag(go, def);
            }

            // Attach the hero-leash so the pet trails the hero around the
            // village instead of holding the Heart slot. Idle-mode pets just
            // walk to it; Defend-mode pets snap back to it when no enemy.
            pet.gameObject.AddComponent<PetHeroLeash>();

            pet.name = $"Pet_{def.Species}";
            return pet;
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
