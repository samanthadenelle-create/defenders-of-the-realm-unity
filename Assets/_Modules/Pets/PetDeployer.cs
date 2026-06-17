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

        // WO-211 Phase 2 "lite pet visuals": the three starter pets' Tripo 3D
        // models are ~208 MB — the dominant WebGL build bloat. While this flag is
        // true the deployer NEVER loads a pet FBX (TryLoadPetMesh returns null)
        // and instead renders each pet as a lightweight camera-facing sprite
        // billboard from Resources/PetPortraits/<id>.png. Pet gameplay
        // (deploy/fight/leash) is unchanged — only the visual swaps. Flip to
        // false to restore the full 3D meshes once they're back in the build.
        // 2026-06-16: light decimated AccuRIG echos (ice-wolf ~1.3MB, aether-sprite ~4.6MB) replace the
        // old ~208MB pet-FBX bloat → 3D pets WebGL-safe again. Species lacking a Resources/Pets/<species>
        // model fall back to the billboard. (static readonly, not const, so the guard checks below stay
        // reachable — no CS0162.)
        private static readonly bool UseLitePetVisuals = false;

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
            // Deploy-once guard (WO-329): the tutorial fires <<spawn_starting_pet>> at START and
            // END (plus PetIntroduction) for robustness — this makes the redundant calls safe
            // no-ops so we never clear+respawn a LIVE pet (which would wipe its runtime state /
            // position / progression). A fresh scene load = a new deployer instance (_deployed
            // empty) so village reload still deploys. To intentionally redeploy, ClearDeployed() first.
            if (_deployed != null && _deployed.Count > 0) return;
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

            // PET-ACQUISITION REWORK (owner 2026-06-13): the pet is acquired ONLY from
            // the Echo Hollow pet-shop (PetHouse Yarn node → <<spawn_named_pet>> →
            // PetDeployer.DeployChosen, which records GameState.StarterPetId BEFORE this
            // runs). There is NO pre-granted default starter anymore — so if no pet has
            // been chosen/owned, DEPLOY NOTHING. This is the fix for both failure modes:
            //   • "never spawns" — the old tutorial <<spawn_starting_pet>> path was
            //     removed (CompanionMeeting.yarn) so nothing chose a pet; and
            //   • "always spawns a default ice-wolf" — the old ResolveStarterSpecies()
            //     fell back to "ice-wolf" with zero ownership, conjuring a pet the
            //     player never acquired.
            // The store path is unaffected: DeployChosen() sets StarterPetId first, so
            // HasChosenOrOwnedPet() is true by the time it calls through here.
            if (!HasChosenOrOwnedPet())
            {
                // No pet acquired yet — the Echo Hollow is the only opener. Nothing to deploy.
                return;
            }
            string starterSpecies = ResolveStarterSpecies();

            foreach (var def in defs)
            {
                if (def == null) continue;
                // SINGLE starter pet until the others are EARNED (owner 2026-05-30) —
                // only the player's chosen Warden deploys for now; unlock the rest
                // via bond progression.
                if (def.Species != starterSpecies) continue;
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

        /// <summary>
        /// Pet-house "name + select your pet" flow: deploy the species the player
        /// CHOSE in the pet-house dialogue. Records the pick to
        /// <c>GameState.StarterPetId</c> (so it persists + drives every later
        /// <see cref="DeployStarterPets"/>), then deploys via the normal path.
        ///
        /// Pass a species id ("ice-wolf" / "flame-pup" / "aether-sprite") OR the
        /// PetCatalog id ("pet-ice-wolf" …) — both resolve. Honors the deploy-once
        /// guard: if a pet is already live this is a safe no-op (does NOT respawn).
        /// </summary>
        public void DeployChosen(string species)
        {
            if (!string.IsNullOrEmpty(species))
            {
                // Normalise either a bare species or a catalog id to the canonical
                // PetCatalog id ("pet-<species>") that GameState.StarterPetId expects
                // (ResolveStarterSpecies() maps the id back to a species via Find()).
                var def = PetCatalog.Find(species);
                if (def == null)
                {
                    foreach (var d in (PetCatalog.Pets ?? new List<PetDef>()))
                        if (d != null && d.Species == species) { def = d; break; }
                }
                if (def != null && !string.IsNullOrEmpty(def.Id))
                {
                    var svc = DeNelle.Core.State.GameStateService.Instance;
                    if (svc != null && svc.State != null)
                    {
                        svc.State.StarterPetId = def.Id;
                        // Audit P1 fix (missing Save): persist the starter immediately —
                        // HasChosenOrOwnedPet checks StarterPetId first, so acquiring a
                        // pet then quitting without saving loses it. Mirrors
                        // PetAcquisitionService.Acquire which saves after mutation.
                        svc.Save();
                    }
                }
                else
                {
                    Debug.LogWarning($"[PetDeployer] DeployChosen('{species}') — no PetCatalog " +
                                     "match; falling back to the recorded/ default starter.");
                }
            }
            DeployStarterPets();
        }

        /// <summary>
        /// WO-297 (active slots): deploy exactly the set of owned species the
        /// <see cref="PetAcquisitionService"/> has assigned to active slots. Unlike
        /// <see cref="DeployStarterPets"/> (single chosen starter), this honours the
        /// multi-slot roster: it tears down any deployed pet whose species is no
        /// longer slotted and spawns one pet per newly-slotted species at its Heart-
        /// ring slot. Additive — the starter-only path is untouched; the acquisition
        /// service is the only caller, so the default flow is unchanged until a 2nd
        /// slot is unlocked.
        /// </summary>
        /// <param name="slotSpecies">Canonical species ids occupying active slots, in slot order.</param>
        public void SyncDeployedToSlots(IReadOnlyList<string> slotSpecies)
        {
            var wanted = new List<string>();
            if (slotSpecies != null)
                foreach (var s in slotSpecies)
                    if (!string.IsNullOrEmpty(s) && !wanted.Contains(s)) wanted.Add(s);

            var defs = PetCatalog.Pets;
            if (defs == null || defs.Count == 0)
            {
                Debug.LogWarning("[PetDeployer] SyncDeployedToSlots — PetCatalog empty.");
                return;
            }

            // 1) Remove deployed pets whose species is no longer wanted.
            for (int i = _deployed.Count - 1; i >= 0; i--)
            {
                var pet = _deployed[i];
                if (pet == null) { _deployed.RemoveAt(i); continue; }
                string sp = SpeciesOfDeployed(pet);
                if (string.IsNullOrEmpty(sp) || !wanted.Contains(sp))
                {
                    Destroy(pet.gameObject);
                    _deployed.RemoveAt(i);
                }
            }

            // 2) Spawn any wanted species not already deployed, at its slot.
            foreach (var species in wanted)
            {
                bool already = false;
                foreach (var pet in _deployed)
                    if (pet != null && SpeciesOfDeployed(pet) == species) { already = true; break; }
                if (already) continue;

                PetDef def = null;
                foreach (var d in defs)
                    if (d != null && d.Species == species) { def = d; break; }
                if (def == null) continue;

                Vector3 slot = PetCatalog.DeploySlotPosition(def.SlotIndex, _heartPosition);
                int bond = BondRankFor(def.SlotIndex);
                Pet spawned = SpawnPet(def, slot);
                spawned.Configure(def, bond, slot, _deployMode);
                spawned.SetEnemyMask(_enemyMask);
                if (spawned.GetComponent<PetProgression>() == null)
                    spawned.gameObject.AddComponent<PetProgression>();
                _deployed.Add(spawned);
            }
        }

        // Recover a deployed pet's canonical species id from its name
        // ("Pet_<species>"); SpawnPet sets pet.name = $"Pet_{def.Species}".
        private static string SpeciesOfDeployed(Pet pet)
        {
            if (pet == null) return null;
            const string prefix = "Pet_";
            string n = pet.name ?? "";
            return n.StartsWith(prefix) ? n.Substring(prefix.Length) : null;
        }

        /// <summary>
        /// WO-360 (Echo at the outpost): summon ONE pet — the player's chosen starter
        /// species (their "Echo") — at an arbitrary world position, independent of the
        /// Heart-ring starter deployment. Used by EchoAutoDeployTrigger when the player
        /// enters an enemy-outpost combat zone so the Echo fights alongside them and
        /// persists for the battle + exploration after.
        ///
        /// Idempotent per live summon: if this deployer already has a live summoned Echo
        /// it returns the existing one (no duplicate spawn). The summoned Echo is tracked
        /// in the same <see cref="_deployed"/> list so ClearDeployed() also tears it down.
        /// Returns null only if the catalog is empty.
        /// </summary>
        public Pet SummonAt(Vector3 worldPosition, PetMode mode = PetMode.Defend)
        {
            // Reuse a live summon if one already exists (don't double-summon the Echo).
            for (int i = 0; i < _deployed.Count; i++)
                if (_deployed[i] != null) return _deployed[i];

            // PET-ACQUISITION REWORK (owner 2026-06-13): only summon the Echo if the
            // player has actually acquired one from the Echo Hollow. No pet owned →
            // no Echo to summon at the outpost (don't conjure the default starter).
            if (!HasChosenOrOwnedPet())
                return null;

            var defs = PetCatalog.Pets;
            if (defs == null || defs.Count == 0)
            {
                Debug.LogWarning("[PetDeployer] SummonAt — PetCatalog empty; cannot summon the Echo.");
                return null;
            }

            string species = ResolveStarterSpecies();
            PetDef chosen = null;
            foreach (var def in defs)
            {
                if (def == null) continue;
                if (def.Species == species) { chosen = def; break; }
            }
            // Fall back to the first available def if the chosen species isn't in the catalog.
            if (chosen == null)
                foreach (var def in defs) { if (def != null) { chosen = def; break; } }
            if (chosen == null) return null;

            int bond = BondRankFor(chosen.SlotIndex);
            Pet pet = SpawnPet(chosen, worldPosition);
            pet.Configure(chosen, bond, worldPosition, mode);
            pet.SetEnemyMask(_enemyMask);
            if (pet.GetComponent<PetProgression>() == null)
                pet.gameObject.AddComponent<PetProgression>();

            _deployed.Add(pet);
            return pet;
        }

        // Fallback starter species used ONCE a pet is owned but the recorded id no
        // longer resolves (e.g. a renamed catalog entry). NOT used to conjure a pet
        // for a player who owns none — HasChosenOrOwnedPet() gates that (pet-acquisition
        // rework, owner 2026-06-13).
        private const string DefaultStarterSpecies = "ice-wolf";

        /// <summary>
        /// PET-ACQUISITION REWORK (owner 2026-06-13): true only when the player has
        /// actually acquired a pet — either the Echo Hollow store recorded the chosen
        /// starter (<c>GameState.StarterPetId</c>, set by <see cref="DeployChosen"/>)
        /// OR the canonical owned roster (<c>GameState.Pets</c>/<c>OwnedPets</c>, set by
        /// PetAcquisitionService.Acquire) holds at least one pet. When false, the deploy
        /// path is a no-op so NO default pet is ever conjured for a player who never
        /// visited the pet-shop. Null-guarded throughout (no GameState = nothing owned).
        /// </summary>
        private static bool HasChosenOrOwnedPet()
        {
            var svc = DeNelle.Core.State.GameStateService.Instance;
            var state = svc != null ? svc.State : null;
            if (state == null) return false;
            if (!string.IsNullOrEmpty(state.StarterPetId)) return true;
            if (state.Pets != null && state.Pets.Count > 0) return true;
            if (state.OwnedPets != null && state.OwnedPets.Count > 0) return true;
            return false;
        }

        /// <summary>
        /// WO-185: resolves the species of the player's chosen starter pet from
        /// <c>GameState.StarterPetId</c> (written by the onboarding pet-select
        /// screen). The id space matches <see cref="PetCatalog"/>
        /// ("pet-aether-sprite" etc.), so a <see cref="PetCatalog.Find"/> maps the
        /// id to its <see cref="PetDef.Species"/>. Falls back to
        /// <see cref="DefaultStarterSpecies"/> when no choice is recorded or the id
        /// is unknown.
        /// </summary>
        private static string ResolveStarterSpecies()
        {
            var svc = DeNelle.Core.State.GameStateService.Instance;
            string starterId = svc != null && svc.State != null ? svc.State.StarterPetId : null;
            if (string.IsNullOrEmpty(starterId)) return DefaultStarterSpecies;

            var def = PetCatalog.Find(starterId);
            if (def != null && !string.IsNullOrEmpty(def.Species)) return def.Species;

            Debug.LogWarning($"[PetDeployer] StarterPetId '{starterId}' did not resolve to a " +
                             $"PetCatalog species — falling back to '{DefaultStarterSpecies}'.");
            return DefaultStarterSpecies;
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
                    // FORWARD CORRECTION (DEF-95, owner field-test: "pet travels in
                    // reverse"). The pet meshes are Tripo exports (ice-wolf =
                    // icecrystalfox3dmodel) of the SAME family as the hero bodies,
                    // which import facing +X (EAST) in their bind pose — HeroBodySwapper
                    // applies a constant -90° yaw (+X→+Z) to align them with the root's
                    // +Z travel direction. Pet.FaceToward rotates the pet ROOT via
                    // LookRotation(dir), i.e. root +Z points along travel. With the old
                    // identity rotation the mesh's authored +X faced 90° off the travel
                    // direction, reading as "moves/faces the wrong way" (DEF-95). Apply
                    // the same single, consistent -90° yaw so visual forward == travel.
                    const float PetForwardYaw = -90f;  // +X (authored forward) → +Z (root forward)
                    visual.transform.localRotation = Quaternion.Euler(0f, PetForwardYaw, 0f);
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

                    // WO-184 (pet T-pose): the spawned pet FBX (e.g. ice-wolf) is
                    // imported WITHOUT an AnimatorController assigned, so its
                    // Animator has nothing to drive — it freezes in its bind/T-pose
                    // regardless of the Speed float Pet.cs feeds. Load a per-species
                    // controller from Resources/Pets/<species>.controller (built by
                    // the editor pet-animator tool from the species' OWN embedded
                    // clips — the ice-wolf is a Generic quadruped, so it needs its
                    // own controller, NOT the KayKit Pet.controller whose Generic
                    // clip bone-paths won't bind to this rig). If absent, fall back
                    // to the shared Pet.controller in Resources, then warn so the
                    // gap is visible rather than a silent statue.
                    WirePetAnimator(visual, def);
                }
                else
                {
                    // WO-211 Phase 2 lite-pet visuals: render the pet as a single
                    // camera-facing sprite billboard instead of its 3D mesh. A Quad
                    // (collider stripped) at chest height carries the pet's portrait
                    // on an unlit transparent material; PetBillboard turns it to face
                    // the camera each frame. Falls back to a TintColor-tinted quad
                    // when the portrait PNG isn't present yet.
                    BuildSpriteBillboard(go, def);
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

            // WO-229: auto-harvest. Added AFTER the leash so PetHarvester.Awake
            // finds it (PetHarvester suspends the leash while gathering and restores
            // it after). Always-on idle harvesting — the harvester self-yields to
            // combat for Defend pets and falls back to the leash when no node is in
            // range, so "pet gathers while you defend" needs no extra mode wiring.
            if (pet.GetComponent<PetHarvester>() == null)
                pet.gameObject.AddComponent<PetHarvester>();

            // WO-366: cute idle routines (sit / lie-down "play dead" / shake) once
            // the pet has been idle a while. Self-guards on the Animator's params —
            // no-op (and logs the authoring gap once) when the cute clips/params
            // aren't on the resolved controller, so it's safe on every pet today.
            if (pet.GetComponent<PetIdleRoutines>() == null)
                pet.gameObject.AddComponent<PetIdleRoutines>();

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
            // WO-211 Phase 2: lite-pet visuals path never loads the heavy Tripo
            // FBX — the caller falls through to the sprite-billboard else-branch.
            if (UseLitePetVisuals) return null;

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

        /// <summary>
        /// WO-184: assign an AnimatorController to a freshly-spawned pet FBX so it
        /// animates instead of standing in its bind/T-pose. Prefers a per-species
        /// controller (Resources/Pets/&lt;species&gt;.controller) built from THAT
        /// species' own clips, then the shared Resources/Pets/Pet.controller, then
        /// warns. Also guarantees the Animator keeps a valid Avatar (Generic FBXs
        /// occasionally instantiate without one) and rebinds so bones reconnect.
        /// </summary>
        private static void WirePetAnimator(GameObject visual, PetDef def)
        {
            if (visual == null) return;
            var anim = visual.GetComponentInChildren<Animator>();
            if (anim == null) return;   // no rig on this mesh — nothing to drive.

            string species = def != null ? def.Species : null;
            RuntimeAnimatorController ctrl = null;
            if (!string.IsNullOrEmpty(species))
                ctrl = Resources.Load<RuntimeAnimatorController>("Pets/" + species);
            if (ctrl == null)
                ctrl = Resources.Load<RuntimeAnimatorController>("Pets/Pet");

            if (ctrl != null)
            {
                anim.runtimeAnimatorController = ctrl;
                // Keep animating when the follow camera frames just past the pet,
                // else Unity freezes the rig and it re-T-poses on re-entry to view.
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                anim.Rebind();
                return;
            }

            // WO-184 FALLBACK — no .controller asset shipped for this species (the
            // ice-wolf is the GAP-PRIMARY quadruped: Generic rig, can't bind the
            // KayKit Rig_Medium Pet.controller). Rather than leave a T-pose statue,
            // play the FBX's OWN embedded take directly via a PlayableGraph — no
            // AnimatorController needed, fully build-safe. Only attach when the
            // species mesh actually carries an embedded clip.
            AnimationClip clip = TryLoadEmbeddedClip(species);
            if (clip != null)
            {
                var player = anim.gameObject.GetComponent<PetClipPlayer>();
                if (player == null) player = anim.gameObject.AddComponent<PetClipPlayer>();
                player.Initialize(anim, clip);
                Debug.Log(
                    "[PetDeployer] No .controller for pet '" + (species ?? "?") +
                    "' — playing its embedded clip '" + clip.name +
                    "' via PetClipPlayer (WO-184 fallback). Author a per-species " +
                    "controller for a proper idle<->walk blend.");
            }
            else
            {
                Debug.LogWarning(
                    "[PetDeployer] No AnimatorController at Resources/Pets/" +
                    (species ?? "<species>") + ".controller (nor Resources/Pets/Pet" +
                    ".controller) AND no embedded clip on the FBX — pet '" +
                    (species ?? "?") + "' will not animate (T-pose). Build a " +
                    "per-species controller from its embedded clips.");
            }
        }

        /// <summary>
        /// WO-184: loads the first <see cref="AnimationClip"/> embedded in the
        /// species' model at Resources/Pets/&lt;species&gt; (Tripo FBXs import their
        /// take as a sub-asset even when no clipAnimations are authored). Skips the
        /// editor-only "__preview__" clip Unity adds. Returns null if the species or
        /// its mesh carries no clip.
        /// </summary>
        private static AnimationClip TryLoadEmbeddedClip(string species)
        {
            if (string.IsNullOrEmpty(species)) return null;
            var all = Resources.LoadAll<AnimationClip>("Pets/" + species);
            if (all == null) return null;
            foreach (var c in all)
            {
                if (c == null) continue;
                // Unity injects a hidden "__preview__<name>" clip for the inspector;
                // never play that one.
                if (c.name.StartsWith("__preview__")) continue;
                return c;
            }
            return null;
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

        /// <summary>
        /// WO-211 Phase 2 "lite pet visuals": builds a camera-facing sprite
        /// billboard for the pet under <paramref name="root"/>. A Quad (collider
        /// stripped) at chest height shows the pet's portrait
        /// (Resources/PetPortraits/&lt;id&gt;.png, id = "pet-&lt;species&gt;") on an
        /// unlit transparent material; <see cref="PetBillboard"/> keeps it turned
        /// toward the camera. When the portrait is missing the quad is tinted with
        /// the species TintColor so the pet is still visible.
        /// </summary>
        private static void BuildSpriteBillboard(GameObject root, PetDef def)
        {
            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "Body";
            quad.transform.SetParent(root.transform, false);
            quad.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            quad.transform.localScale = Vector3.one * 1.4f;

            // No collider on a visual-only billboard (pets hunt via overlap sweeps,
            // not this quad).
            var col = quad.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var renderer = quad.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                // Prefer the URP unlit shader; fall back to the sprite/legacy
                // transparent shaders so the quad renders in any pipeline.
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                                ?? Shader.Find("Sprites/Default")
                                ?? Shader.Find("Unlit/Transparent");
                var mat = shader != null ? new Material(shader) : null;

                // id = "pet-<species>" — matches the PetCatalog id and the PNG the
                // PetPortraitRenderer writes.
                string id = "pet-" + (def != null ? def.Species : "");
                var sprite = Resources.Load<Sprite>("PetPortraits/" + id);
                Texture portraitTex = sprite != null
                    ? sprite.texture
                    : Resources.Load<Texture2D>("PetPortraits/" + id);

                if (mat != null)
                {
                    EnableMaterialTransparency(mat);
                    if (portraitTex != null)
                    {
                        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", portraitTex);
                        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", portraitTex);
                        // White base so the texture's own colours show through.
                        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
                        if (mat.HasProperty("_Color"))     mat.SetColor("_Color", Color.white);
                    }
                    else
                    {
                        // Portrait not baked yet — tint the quad with the species
                        // colour so the pet is at least visible (owner: keep the
                        // loop playable while the render runs).
                        Color tint = def != null ? def.TintColor : Color.white;
                        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
                        if (mat.HasProperty("_Color"))     mat.SetColor("_Color", tint);
                        Debug.LogWarning("[PetDeployer] No lite-pet portrait at " +
                            "Resources/PetPortraits/" + id + " — using TintColor " +
                            "quad. Run Defenders → Art → Render Pet Portraits.");
                    }
                    renderer.sharedMaterial = mat;
                }
            }

            quad.AddComponent<PetBillboard>();
        }

        /// <summary>
        /// Flips a URP/Unlit (or legacy) material into alpha-blended transparent
        /// mode so the portrait's transparent background reads through.
        /// </summary>
        private static void EnableMaterialTransparency(Material mat)
        {
            if (mat == null) return;
            // URP/Unlit surface keywords for transparent + alpha blend.
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // 1 = Transparent
            if (mat.HasProperty("_Blend"))   mat.SetFloat("_Blend", 0f);   // 0 = Alpha
            if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite"))   mat.SetInt("_ZWrite", 0);
            if (mat.HasProperty("_Cull"))     mat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off); // double-sided
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
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
