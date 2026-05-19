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
                // (port spec Part 7). A small capsule reads as "a pet" in-scene.
                var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.transform.SetParent(transform, false);
                go.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
                var col = go.GetComponent<Collider>();
                if (col != null) col.isTrigger = true; // pets do not block pathing
                pet = go.AddComponent<Pet>();
                TintPlaceholder(go, def);
            }

            pet.name = $"Pet_{def.Species}";
            return pet;
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
            // URP/Lit uses _BaseColor; fall back to _Color for the built-in
            // pipeline. A MaterialPropertyBlock keeps the shared material clean.
            var mpb = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(mpb);
            mpb.SetColor("_BaseColor", def.TintColor);
            mpb.SetColor("_Color", def.TintColor);
            renderer.SetPropertyBlock(mpb);
        }
    }
}
