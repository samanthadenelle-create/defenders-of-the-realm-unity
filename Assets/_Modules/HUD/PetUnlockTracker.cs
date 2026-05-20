// =============================================================================
// PetUnlockTracker — owns runtime state for "which pet skills the player has
// unlocked" and each pet's current level. Persists to PlayerPrefs under
// dotr-pet-unlocks-v1.
// -----------------------------------------------------------------------------
// Pattern model: Assets/_Modules/Core/Quests/DailyQuests.cs — singleton
// MonoBehaviour bootstrapped via RuntimeInitializeOnLoadMethod, JSON-serialised
// blob in PlayerPrefs. Cross-asmdef reach into PetSkillTreeCatalog is by
// reflection (DeNelle.HUD does NOT reference DeNelle.Pets) — see the bridge
// pattern in Assets/_Modules/Pets/PetHeroLeash.cs.
//
// Public API:
//   int LevelOf(species)
//   bool IsUnlocked(skillId)
//   bool TryUnlock(skillId)
//   void AddXp(species, amount)
//
// XP curve: xpForLevel(n) = 50 * n. Pet max level is 20 (matches the catalog
// constant). Starter skills are auto-granted on first observation of a species
// so the prerequisite chain has a root.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using UnityEngine;

namespace DeNelle.HUD
{
    [Serializable]
    public sealed class PetUnlockState
    {
        public int Level = 1;
        public int Xp = 0;
        public HashSet<string> Unlocked = new HashSet<string>();
    }

    [DisallowMultipleComponent]
    public sealed class PetUnlockTracker : MonoBehaviour
    {
        public const string PrefKey = "dotr-pet-unlocks-v1";
        public const int MaxLevel = 20;

        public static PetUnlockTracker Instance { get; private set; }
        public event Action Changed;

        private Dictionary<string, PetUnlockState> _byspecies =
            new Dictionary<string, PetUnlockState>();

        // ── Reflection bridge into DeNelle.Pets.PetSkillTreeCatalog ──────────
        private static Type s_catalogType;
        private static MethodInfo s_findSkill;
        private static MethodInfo s_canUnlock;
        private static MethodInfo s_getTree;
        private static PropertyInfo s_allTrees;
        private static bool s_bridgeResolved;
        private static bool s_bridgeWarned;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("PetUnlockTracker");
            UnityEngine.Object.DontDestroyOnLoad(go);
            Instance = go.AddComponent<PetUnlockTracker>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            Load();
        }

        // ── Public API ───────────────────────────────────────────────────────

        public int LevelOf(string species)
        {
            if (string.IsNullOrEmpty(species)) return 1;
            return GetOrCreate(species).Level;
        }

        public int XpOf(string species)
        {
            if (string.IsNullOrEmpty(species)) return 0;
            return GetOrCreate(species).Xp;
        }

        public IReadOnlyCollection<string> UnlockedFor(string species)
        {
            if (string.IsNullOrEmpty(species)) return Array.Empty<string>();
            return GetOrCreate(species).Unlocked;
        }

        public bool IsUnlocked(string skillId)
        {
            if (string.IsNullOrEmpty(skillId)) return false;
            string species = SpeciesFromSkillId(skillId);
            if (string.IsNullOrEmpty(species)) return false;
            return GetOrCreate(species).Unlocked.Contains(skillId);
        }

        /// <summary>
        /// Attempts to unlock a skill. Returns false when the catalog rejects
        /// it (level / prereqs not satisfied) or the species can't be derived.
        /// Auto-grants the starter on first touch so the prereq chain has a
        /// root.
        /// </summary>
        public bool TryUnlock(string skillId)
        {
            if (string.IsNullOrEmpty(skillId)) return false;
            string species = SpeciesFromSkillId(skillId);
            if (string.IsNullOrEmpty(species)) return false;
            var st = GetOrCreate(species);
            if (st.Unlocked.Contains(skillId)) return true; // already done

            if (!ResolveBridge()) return false;
            if (!InvokeCanUnlock(skillId, st.Level, st.Unlocked)) return false;

            st.Unlocked.Add(skillId);
            Save();
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Adds XP for a species, levelling up until either XP is exhausted or
        /// the cap (MaxLevel) is reached. xpForLevel(n) = 50 * n.
        /// </summary>
        public void AddXp(string species, int amount)
        {
            if (string.IsNullOrEmpty(species) || amount <= 0) return;
            var st = GetOrCreate(species);
            int remaining = amount;
            while (remaining > 0 && st.Level < MaxLevel)
            {
                int need = XpForLevel(st.Level) - st.Xp;
                if (need <= 0) need = 1;
                if (remaining < need)
                {
                    st.Xp += remaining;
                    remaining = 0;
                }
                else
                {
                    remaining -= need;
                    st.Level++;
                    st.Xp = 0;
                }
            }
            if (st.Level >= MaxLevel) st.Xp = 0;
            Save();
            Changed?.Invoke();
        }

        public static int XpForLevel(int level) => 50 * Mathf.Max(1, level);

        // ── Internals ────────────────────────────────────────────────────────

        private PetUnlockState GetOrCreate(string species)
        {
            if (!_byspecies.TryGetValue(species, out var st) || st == null)
            {
                st = new PetUnlockState();
                _byspecies[species] = st;
                // Auto-grant starter so prereq chains have a root. Costs us a
                // reflection hop but only on first observation per species.
                TryAutoGrantStarter(species, st);
            }
            if (st.Unlocked == null) st.Unlocked = new HashSet<string>();
            return st;
        }

        private void TryAutoGrantStarter(string species, PetUnlockState st)
        {
            if (!ResolveBridge()) return;
            try
            {
                var tree = s_getTree?.Invoke(null, new object[] { species });
                if (tree == null) return;
                var skillsProp = tree.GetType().GetField("Skills");
                if (skillsProp == null) return;
                var skills = skillsProp.GetValue(tree) as System.Collections.IEnumerable;
                if (skills == null) return;
                foreach (var s in skills)
                {
                    var tierField = s.GetType().GetField("Tier");
                    var idField = s.GetType().GetField("Id");
                    if (tierField == null || idField == null) continue;
                    string tier = tierField.GetValue(s) as string;
                    string id = idField.GetValue(s) as string;
                    if (string.Equals(tier, "starter", StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrEmpty(id))
                    {
                        st.Unlocked.Add(id);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[PetUnlockTracker] starter auto-grant failed: " + ex.Message);
            }
        }

        private static string SpeciesFromSkillId(string skillId)
        {
            if (string.IsNullOrEmpty(skillId)) return null;
            int dot = skillId.IndexOf('.');
            return dot > 0 ? skillId.Substring(0, dot) : null;
        }

        // ── Reflection bridge ────────────────────────────────────────────────

        private static bool ResolveBridge()
        {
            if (s_bridgeResolved) return s_catalogType != null;
            s_bridgeResolved = true;
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var t = asm.GetType("DeNelle.Pets.PetSkillTreeCatalog", false);
                    if (t != null) { s_catalogType = t; break; }
                }
                if (s_catalogType == null)
                {
                    if (!s_bridgeWarned)
                    {
                        Debug.LogWarning("[PetUnlockTracker] DeNelle.Pets.PetSkillTreeCatalog not found — unlock flow disabled.");
                        s_bridgeWarned = true;
                    }
                    return false;
                }
                s_findSkill = s_catalogType.GetMethod("FindSkill", BindingFlags.Public | BindingFlags.Static);
                s_canUnlock = s_catalogType.GetMethod("CanUnlock", BindingFlags.Public | BindingFlags.Static);
                s_getTree   = s_catalogType.GetMethod("GetTree",   BindingFlags.Public | BindingFlags.Static);
                s_allTrees  = s_catalogType.GetProperty("AllTrees", BindingFlags.Public | BindingFlags.Static);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[PetUnlockTracker] bridge resolve failed: " + ex.Message);
                return false;
            }
        }

        private static bool InvokeCanUnlock(string skillId, int level, HashSet<string> unlocked)
        {
            if (s_canUnlock == null) return false;
            try
            {
                object res = s_canUnlock.Invoke(null, new object[] { skillId, level, unlocked });
                return res is bool b && b;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[PetUnlockTracker] CanUnlock invoke failed: " + ex.Message);
                return false;
            }
        }

        // ── Persistence ──────────────────────────────────────────────────────

        [Serializable]
        private sealed class SaveBlob
        {
            public Dictionary<string, SaveEntry> Species = new Dictionary<string, SaveEntry>();
        }

        [Serializable]
        private sealed class SaveEntry
        {
            public int Level = 1;
            public int Xp = 0;
            public List<string> Unlocked = new List<string>();
        }

        private void Load()
        {
            _byspecies = new Dictionary<string, PetUnlockState>();
            if (!PlayerPrefs.HasKey(PrefKey)) return;
            try
            {
                var blob = JsonConvert.DeserializeObject<SaveBlob>(PlayerPrefs.GetString(PrefKey));
                if (blob?.Species == null) return;
                foreach (var kv in blob.Species)
                {
                    if (string.IsNullOrEmpty(kv.Key) || kv.Value == null) continue;
                    _byspecies[kv.Key] = new PetUnlockState
                    {
                        Level = Mathf.Clamp(kv.Value.Level, 1, MaxLevel),
                        Xp = Mathf.Max(0, kv.Value.Xp),
                        Unlocked = new HashSet<string>(kv.Value.Unlocked ?? new List<string>()),
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[PetUnlockTracker] load failed: " + ex.Message);
            }
        }

        private void Save()
        {
            try
            {
                var blob = new SaveBlob();
                foreach (var kv in _byspecies)
                {
                    if (kv.Value == null) continue;
                    blob.Species[kv.Key] = new SaveEntry
                    {
                        Level = kv.Value.Level,
                        Xp = kv.Value.Xp,
                        Unlocked = kv.Value.Unlocked != null
                            ? kv.Value.Unlocked.ToList()
                            : new List<string>(),
                    };
                }
                PlayerPrefs.SetString(PrefKey, JsonConvert.SerializeObject(blob));
                PlayerPrefs.Save();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[PetUnlockTracker] save failed: " + ex.Message);
            }
        }
    }
}
