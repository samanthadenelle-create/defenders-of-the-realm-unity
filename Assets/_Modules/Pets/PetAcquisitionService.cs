// =============================================================================
// PetAcquisitionService — WO-297. The three ways a player WINS a new pet, plus
// the active-slot model for which owned pets are currently deployed.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Pets   Namespace: DeNelle.Pets
//
// Reconciles with existing pet code rather than greenfielding (DESIGN_PET_SYSTEM.md):
//   • Ownership ALREADY persists — GameState.Pets (List<PetData> roster: id /
//     species / level / xp / skills) + GameState.OwnedPets (List<PetSpecies>) +
//     StarterPetId. Both round-trip through SaveSchema/GameStateService (verified
//     in GameStateService.Snapshot()). So acquisition writes the roster through
//     GameStateService and calls Save() — it does NOT add a new save field.
//   • Per-species level / xp / skill unlocks live in PetUnlockTracker (HUD) and
//     PetSkillTreeCatalog (Pets). Acquisition seeds the roster entry; the tracker
//     keeps owning skills/levels (unchanged).
//   • Deployment stays with PetDeployer. This service owns the ACTIVE-SLOT roster
//     (which owned pets occupy a deploy slot) and asks PetDeployer to (re)deploy.
//
// THE THREE ACQUISITION PATHS (each returns true only on a NEW unlock):
//   1. Tame(species)         — won the bond mini-game on a wild pet in its region.
//   2. Hatch(species)        — an egg's care timer finished (offline-aware caller).
//   3. Rescue(species)       — freed a caged beast from a cleared enemy camp.
// All three funnel into Acquire(species, source) → roster + OwnedPets + (auto)
// slot assignment when a slot is free. Quest/region gating is the CALLER's job
// (QuestService, WO-299) — this service is the grant primitive with a clean seam,
// it authors no Yarn and references no dialogue.
//
// ACTIVE SLOTS: start with 1 deploy slot; SetMaxSlots() raises the cap (Fenn's
// questline → 2, village tier → 3). The slot->species assignment persists in
// GameState.PetActiveSlots (save v34, REDS #3): SyncSlotsFromState rebuilds the
// runtime map from it on load, and every slot mutation writes it back via
// PersistSlots. flag_17 FIXED — a multi-slot roster survives a reload (a pre-v34
// save with no persisted map falls back to the legacy starter-in-slot-0 rebuild).
//
// MODULE ISOLATION: DeNelle.Pets references DeNelle.Core only (asmdef). It reaches
// GameState via DeNelle.Core.State.GameStateService (same as PetDeployer already
// does). It does NOT reference DeNelle.Village (EconomyService) — resource costs
// of acquisition (e.g. food to hatch) are handled by the caller or via the
// GameState.Resources wallet through GameStateService, never a stale balance.
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.State;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.Quests;
using DeNelle.Core.Tutorial;
using UnityEngine;

namespace DeNelle.Pets
{
    /// <summary>How a pet entered the roster — drives flavour / analytics only.</summary>
    public enum PetAcquisitionSource
    {
        Starter,
        Tame,
        Hatch,
        Rescue,
        Gift,
    }

    /// <summary>
    /// Owns pet acquisition (tame / hatch / rescue → roster unlock) and the
    /// active deploy-slot model. Singleton MonoBehaviour bootstrapped on load,
    /// mirroring PetUnlockTracker. Stateless across scenes for the roster (that
    /// lives in GameState); only the runtime slot map is held here.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PetAcquisitionService : MonoBehaviour
    {
        public static PetAcquisitionService Instance { get; private set; }

        /// <summary>Raised after any roster change (acquire) or slot change.</summary>
        public event Action Changed;

        /// <summary>
        /// Raised when a NEW species is acquired. Arg = canonical species id
        /// ("ice-wolf"). QuestService / HUD can subscribe for the "you bonded a
        /// new pet!" beat without this service referencing them.
        /// </summary>
        public event Action<string> PetAcquired;

        // Default active-slot cap (design §3: "start with 1 deployed pet").
        public const int DefaultMaxSlots = 1;
        // Hard ceiling so a bad caller can't request an absurd cap.
        public const int AbsoluteMaxSlots = 6;

        [SerializeField] private int _maxSlots = DefaultMaxSlots;

        // slotIndex -> canonical species id occupying it. -1/absent = empty.
        private readonly List<string> _activeSlots = new List<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("PetAcquisitionService");
            UnityEngine.Object.DontDestroyOnLoad(go);
            Instance = go.AddComponent<PetAcquisitionService>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            SyncSlotsFromState();
        }

        // ── Active-slot cap ──────────────────────────────────────────────────

        /// <summary>The current active deploy-slot cap (start 1, raised by unlocks).</summary>
        public int MaxSlots => Mathf.Clamp(_maxSlots, 1, AbsoluteMaxSlots);

        /// <summary>
        /// Species ids currently occupying a deploy slot, in slot order. Read-only
        /// snapshot — entries are non-null/non-empty.
        /// </summary>
        public IReadOnlyList<string> ActiveSlotSpecies
        {
            get
            {
                var live = new List<string>();
                for (int i = 0; i < _activeSlots.Count; i++)
                    if (!string.IsNullOrEmpty(_activeSlots[i])) live.Add(_activeSlots[i]);
                return live;
            }
        }

        /// <summary>Number of slots currently filled.</summary>
        public int FilledSlotCount => ActiveSlotSpecies.Count;

        /// <summary>
        /// Raises (or lowers) the active-slot cap — e.g. Fenn's questline grants a
        /// 2nd slot, a village tier the 3rd (design §3). Clamped to [1, ceiling].
        /// Lowering the cap evicts the highest-index occupants. Idempotent.
        /// </summary>
        public void SetMaxSlots(int cap)
        {
            int clamped = Mathf.Clamp(cap, 1, AbsoluteMaxSlots);
            if (clamped == _maxSlots) return;
            _maxSlots = clamped;
            // Evict any slot now beyond the cap.
            bool evicted = _activeSlots.Count > _maxSlots;
            while (_activeSlots.Count > _maxSlots)
                _activeSlots.RemoveAt(_activeSlots.Count - 1);
            if (evicted) PersistSlots();   // flag_17 (v34) — persist the eviction so the trimmed map survives a reload
            Changed?.Invoke();
        }

        // ── Acquisition — the three paths ────────────────────────────────────

        /// <summary>
        /// Path 1 — TAME: the player tracked a wild pet and won the bond mini-game.
        /// Returns true on a NEW unlock, false if already owned (or invalid).
        /// </summary>
        public bool Tame(string species) => Acquire(species, PetAcquisitionSource.Tame);

        /// <summary>
        /// Path 2 — HATCH: an egg's care timer finished (the caller owns the
        /// offline-aware timer + any food cost). Grants the resulting species.
        /// Returns true on a NEW unlock.
        /// </summary>
        public bool Hatch(string species) => Acquire(species, PetAcquisitionSource.Hatch);

        /// <summary>
        /// Path 3 — RESCUE: the player cleared an enemy camp and freed a caged
        /// beast — it bonds instantly. Returns true on a NEW unlock.
        /// </summary>
        public bool Rescue(string species) => Acquire(species, PetAcquisitionSource.Rescue);

        /// <summary>True when the species already sits in the owned roster.</summary>
        public bool Owns(string species)
        {
            species = NormalizeSpecies(species);
            if (string.IsNullOrEmpty(species)) return false;
            var state = StateOrNull();
            if (state == null) return false;
            if (state.Pets != null)
                foreach (var p in state.Pets)
                    if (p != null && SpeciesIdOf(p) == species) return true;
            return false;
        }

        /// <summary>
        /// Unified grant: adds <paramref name="species"/> to the roster
        /// (GameState.Pets + OwnedPets), auto-assigns a free slot, persists, and
        /// fires events. No-op + false when already owned, the species is unknown
        /// to the catalog, or there's no GameState. The single funnel all three
        /// acquisition paths use.
        /// </summary>
        public bool Acquire(string species, PetAcquisitionSource source)
        {
            FlowTrace.Step("PetAcquire", $"Acquire species='{species ?? "<null>"}' source={source}");
            species = NormalizeSpecies(species);
            if (string.IsNullOrEmpty(species))
            {
                FlowTrace.Warn("PetAcquire", $"Acquire rejected: empty/invalid species (source={source})");
                Debug.LogWarning($"[PetAcquisitionService] Acquire('{species}', {source}) — empty/invalid species.");
                return false;
            }

            // Reject species the catalog doesn't know — keeps acquisition data-driven.
            var def = PetCatalog.FindBySpecies(species);
            if (def == null)
            {
                FlowTrace.Warn("PetAcquire", $"Acquire rejected: '{species}' not in PetCatalog (pets.json) — data-driven gate");
                Debug.LogWarning($"[PetAcquisitionService] Acquire('{species}') — not in PetCatalog (pets.json). " +
                                 "Add the species to pets.json before it can be acquired.");
                return false;
            }

            var svc = GameStateService.Instance;
            var state = svc != null ? svc.State : null;
            if (state == null)
            {
                FlowTrace.Fail("PetAcquire", $"Acquire aborted: no GameState — cannot record acquired pet '{species}'");
                Debug.LogWarning("[PetAcquisitionService] no GameState — cannot record the acquired pet.");
                return false;
            }

            if (Owns(species)) { FlowTrace.Step("PetAcquire", $"Acquire no-op: '{species}' already owned (idempotent)"); return false; } // already owned — idempotent

            // 1) Roster entry (GameState.Pets) — id / species / fresh level.
            if (state.Pets == null) state.Pets = new List<PetData>();
            state.Pets.Add(new PetData
            {
                Id = def.Id,                 // canonical "pet-<species>" id
                Species = ToSpecies(species),
                Level = 1,
                Xp = 0,
                UnlockedSkillIds = new List<string>(),
                EquippedActiveIds = new List<string>(),
            });

            // 2) OwnedPets enum list (mirrors the React petsSlice #25). Only the
            //    three canonical starter species map to the PetSpecies enum today;
            //    species outside it still live in the Pets roster above, so no data
            //    is lost — the enum list just can't carry them yet (FLAG below).
            if (TryToSpeciesEnum(species, out var speciesEnum))
            {
                if (state.OwnedPets == null) state.OwnedPets = new List<PetSpecies>();
                if (!state.OwnedPets.Contains(speciesEnum)) state.OwnedPets.Add(speciesEnum);
            }
            else
            {
                // flag_15: only the 3 starters map to PetSpecies; other species live in the
                // Pets roster (by def.Id) but CANNOT be carried in the OwnedPets enum list.
                FlowTrace.Warn("PetAcquire", $"'{species}' has no PetSpecies enum mapping — roster-only, OwnedPets enum list will NOT carry it (flag_15)");
            }

            // 3) Auto-fill a free deploy slot so a freshly-won pet shows up.
            int slot = TryAssignFreeSlot(species);
            if (slot < 0)
                FlowTrace.Warn("PetAcquire", $"'{species}' acquired but no free deploy slot (MaxSlots={MaxSlots}, filled={FilledSlotCount}) — rests at the Stables");

            // 4) Persist through the unified save (never a stale balance). flag_17 FIXED
            // (v34): TryAssignFreeSlot above already wrote the slot map to
            // GameState.PetActiveSlots via PersistSlots, so the exact slot assignment now
            // survives a reload; this Save() also persists the new roster entry.
            svc.Save();

            FlowTrace.Step("PetAcquire", $"Acquired '{species}' via {source} — roster now {state.Pets.Count} pet(s), slot={slot}");
            Debug.Log($"[PetAcquisitionService] Acquired '{species}' via {source} → roster ({state.Pets.Count} pets).");

            // pet.bonded:<species> (WO-854 Silo E). Raised HERE, where the bond genuinely
            // completes: the roster entry is written, the deploy slot is assigned and the save
            // has flushed. Every acquisition path (Tame / Hatch / Rescue / Gift / Starter) funnels
            // through this method, and the Owns() guard above returns false before this line when
            // the species is already owned - so the id fires exactly once per NEW bond, never on a
            // panel open and never twice.
            //
            // The id is composed from QuestCompletion.PetBondedPrefix - the SAME constant
            // QuestCompletion.ToSignalId() uses to compose what a completeOn {kind:"pet"} stage
            // awaits - so the emitter and the matcher cannot drift apart. def.Species is the
            // catalog's own species string (PetCatalog.FindBySpecies matched it by ordinal
            // equality above), so the token is byte-identical to the pets.json id.
            TutorialSignals.Raise(QuestCompletion.PetBondedPrefix + def.Species);

            PetAcquired?.Invoke(species);
            Changed?.Invoke();
            return true;
        }

        // ── Active-slot assignment ───────────────────────────────────────────

        /// <summary>
        /// Assigns an OWNED species to deploy slot <paramref name="slotIndex"/>
        /// (0-based, &lt; MaxSlots). Returns false if the species isn't owned, the
        /// slot is out of range, or the species already occupies a slot. Triggers a
        /// redeploy through PetDeployer.
        /// </summary>
        public bool AssignSlot(int slotIndex, string species)
        {
            species = NormalizeSpecies(species);
            if (slotIndex < 0 || slotIndex >= MaxSlots) return false;
            if (string.IsNullOrEmpty(species) || !Owns(species)) return false;

            // Already deployed elsewhere — no duplicate slotting.
            for (int i = 0; i < _activeSlots.Count; i++)
                if (_activeSlots[i] == species && i != slotIndex) return false;

            while (_activeSlots.Count <= slotIndex) _activeSlots.Add(null);
            _activeSlots[slotIndex] = species;
            PersistSlots();   // flag_17 (v34) — the slot assignment now survives a reload
            RequestRedeploy();
            Changed?.Invoke();
            return true;
        }

        /// <summary>Clears the species out of slot <paramref name="slotIndex"/> (recall).</summary>
        public bool ClearSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _activeSlots.Count) return false;
            if (string.IsNullOrEmpty(_activeSlots[slotIndex])) return false;
            _activeSlots[slotIndex] = null;
            PersistSlots();   // flag_17 (v34) — persist the recall
            RequestRedeploy();
            Changed?.Invoke();
            return true;
        }

        /// <summary>Assigns the species to the first free slot, if any. Returns the slot or -1.</summary>
        public int TryAssignFreeSlot(string species)
        {
            species = NormalizeSpecies(species);
            if (string.IsNullOrEmpty(species) || !Owns(species)) return -1;

            // Already slotted?
            for (int i = 0; i < _activeSlots.Count; i++)
                if (_activeSlots[i] == species) return i;

            // First empty slot under the cap.
            for (int i = 0; i < MaxSlots; i++)
            {
                while (_activeSlots.Count <= i) _activeSlots.Add(null);
                if (string.IsNullOrEmpty(_activeSlots[i]))
                {
                    _activeSlots[i] = species;
                    PersistSlots();   // flag_17 (v34) — persist the auto-assignment
                    RequestRedeploy();
                    Changed?.Invoke();
                    return i;
                }
            }
            return -1; // all slots full
        }

        /// <summary>The species in <paramref name="slotIndex"/>, or null if empty/out of range.</summary>
        public string SpeciesInSlot(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= _activeSlots.Count) return null;
            return _activeSlots[slotIndex];
        }

        // ── Internals ────────────────────────────────────────────────────────

        /// <summary>
        /// Rebuilds the runtime slot map from persisted state on load. flag_17 fix
        /// (REDS #3, save v34): the EXACT slot→species assignment now persists in
        /// <see cref="GameState.PetActiveSlots"/>, so a multi-slot roster is restored
        /// verbatim (each still-owned species back in its slot; a null entry = an empty
        /// slot; a species no longer owned is dropped to null). When that map is absent
        /// or empty (a pre-v34 save, or a fresh game), we fall back to the LEGACY rebuild:
        /// the chosen starter (GameState.StarterPetId) takes slot 0 and everything else
        /// rests at the Stables until slotted — exactly the prior behaviour.
        /// </summary>
        private void SyncSlotsFromState()
        {
            _activeSlots.Clear();
            var state = StateOrNull();
            if (state == null) { FlowTrace.Warn("PetAcquire", "SyncSlotsFromState: no GameState — slots left empty"); return; }

            // ── Preferred: rebuild the EXACT persisted slot→species map (v34) ──
            if (state.PetActiveSlots != null && state.PetActiveSlots.Count > 0)
            {
                int restored = 0;
                for (int i = 0; i < state.PetActiveSlots.Count; i++)
                {
                    string sp = NormalizeSpecies(state.PetActiveSlots[i]);
                    // Keep empties as null; drop any species no longer in the roster.
                    if (!string.IsNullOrEmpty(sp) && Owns(sp)) { _activeSlots.Add(sp); restored++; }
                    else _activeSlots.Add(null);
                }
                FlowTrace.Step("PetAcquire", $"SyncSlotsFromState: restored {restored} persisted slot(s) from GameState.PetActiveSlots (flag_17 fixed — v34)");
                return;
            }

            // ── Legacy fallback: pre-v34 save (no slot map) → restore the starter to slot 0 ──
            string starter = null;
            if (!string.IsNullOrEmpty(state.StarterPetId))
            {
                var def = PetCatalog.Find(state.StarterPetId);
                if (def != null) starter = def.Species;
                else FlowTrace.Warn("PetAcquire", $"SyncSlotsFromState: StarterPetId '{state.StarterPetId}' not in PetCatalog — slot 0 empty");
            }
            if (!string.IsNullOrEmpty(starter))
            {
                _activeSlots.Add(starter); // slot 0
                FlowTrace.Step("PetAcquire", $"SyncSlotsFromState: no persisted slot map — restored starter '{starter}' to slot 0 (legacy pre-v34 rebuild)");
            }
        }

        /// <summary>
        /// flag_17 fix (v34) — writes the runtime slot map back to the persisted
        /// <see cref="GameState.PetActiveSlots"/> and saves, so the exact slot→species
        /// assignment survives a reload / scene transition. Called after every slot
        /// mutation. No-op when no GameState is available.
        /// </summary>
        private void PersistSlots()
        {
            var svc = GameStateService.Instance;
            var state = svc != null ? svc.State : null;
            if (state == null) { FlowTrace.Warn("PetAcquire", "PersistSlots no-op: no GameState — slot map not persisted"); return; }
            state.PetActiveSlots = new List<string>(_activeSlots);
            svc.Save();
        }

        /// <summary>
        /// Asks the live PetDeployer to re-sync deployed pets with the active-slot
        /// map. Today PetDeployer deploys the single chosen starter; this is the
        /// clean seam for multi-slot deployment (PetDeployer.SyncDeployedToSlots,
        /// added additively in this WO). No-op when no deployer is in the scene.
        /// </summary>
        private void RequestRedeploy()
        {
            var deployer = FindAnyObjectByType<PetDeployer>();
            if (deployer == null) { FlowTrace.Warn("PetAcquire", "RequestRedeploy no-op: no PetDeployer in scene (slot change not reflected in deployment)"); return; }
            FlowTrace.Step("PetAcquire", $"RequestRedeploy -> PetDeployer with {FilledSlotCount} active slot(s)");
            deployer.SyncDeployedToSlots(ActiveSlotSpecies);
        }

        private static GameState StateOrNull()
        {
            var svc = GameStateService.Instance;
            return svc != null ? svc.State : null;
        }

        // Canonical species id of a roster PetData. The roster's TRUE identity is
        // PetData.Id (the catalog "pet-<species>" id) — the Species enum only
        // distinguishes the 3 starters and defaults for everything else, so resolve
        // via the catalog first and fall back to the enum.
        private static string SpeciesIdOf(PetData p)
        {
            if (p == null) return null;
            if (!string.IsNullOrEmpty(p.Id))
            {
                var def = PetCatalog.Find(p.Id);
                if (def != null && !string.IsNullOrEmpty(def.Species)) return def.Species;
            }
            return FromSpeciesEnum(p.Species);
        }

        // ── species id ↔ PetSpecies enum (EnumMember kebab values) ───────────

        private static string NormalizeSpecies(string speciesOrId)
        {
            if (string.IsNullOrEmpty(speciesOrId)) return null;
            string s = speciesOrId.Trim();
            // Accept a catalog id ("pet-ice-wolf") and map it back to its species.
            var byId = PetCatalog.Find(s);
            if (byId != null && !string.IsNullOrEmpty(byId.Species)) return byId.Species;
            return s; // assume it's already a bare species id ("ice-wolf")
        }

        // The PetData.Species field is a PetSpecies enum. The roster's canonical
        // identity is the species STRING, so set the enum when it maps and leave it
        // at the default otherwise (the def.Id still carries the true identity).
        private static PetSpecies ToSpecies(string species)
        {
            return TryToSpeciesEnum(species, out var e) ? e : default;
        }

        private static bool TryToSpeciesEnum(string species, out PetSpecies value)
        {
            switch ((species ?? "").ToLowerInvariant())
            {
                case "aether-sprite": value = PetSpecies.AetherSprite; return true;
                case "flame-pup":     value = PetSpecies.FlamePup;     return true;
                case "ice-wolf":      value = PetSpecies.IceWolf;      return true;
                default:              value = default;                 return false;
            }
        }

        private static string FromSpeciesEnum(PetSpecies species)
        {
            switch (species)
            {
                case PetSpecies.AetherSprite: return "aether-sprite";
                case PetSpecies.FlamePup:     return "flame-pup";
                case PetSpecies.IceWolf:      return "ice-wolf";
                default:                      return null;
            }
        }
    }
}
