// =============================================================================
// EnemyTypeVfxLibrary - RUNTIME resolution of an enemy's EnemyTypeVfxSet.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// THE DEFECT THIS CLOSES (verified at source 2026-08-16):
//   Enemy.cs carries [SerializeField] private EnemyTypeVfxSet _typeVfxSet. It was
//   supposed to be populated per PREFAB by the editor tool EnemyVfxSetup.Apply.
//   Two independent proofs it never was:
//     * _typeVfxSet appears in exactly ONE prefab in the whole tree
//       (Assets/Prefabs/Village/Generated/Enemy_HollowWalker.prefab:123) and its
//       value there is {fileID: 0} - null.
//     * The only EnemyTypeVfxSet asset on disk
//       (Assets/Resources/Enemies/EnemyVfxSet_Default.asset) has GUID
//       e6cfb68dcbf88f247bc64a568fb426d9, and that GUID appears in exactly one
//       file under Assets/ - its OWN .meta. Nothing references it.
//   So every _typeVfxSet branch in Enemy.cs took its hardcoded fallback forever:
//   no wind-up TELEGRAPH VFX (Enemy.cs ~:1584), PlayTypeSound(null) on attack /
//   ranged / hit / death, and no per-type hit VFX (~:2258).
//
// WHY A RUNTIME LIBRARY AND NOT A RE-RUN OF THE EDITOR ASSIGNMENT:
//   1. The prefab path is not even the live spawn path. Wave / roamer / tribe
//      enemies are BUILT AT RUNTIME by EnemyFactory, which does
//      go.AddComponent<Enemy>() (EnemyFactory.cs:362) - there is no prefab whose
//      serialized field could carry the set. Re-running EnemyVfxSetup would have
//      fixed nothing for the enemies the owner actually fights.
//   2. A serialized reference that nothing verifies is exactly how this got lost:
//      it silently failed to persist once already, and no runtime code and no gate
//      could tell. A path-addressed Resources load plus a synthesized last-resort
//      instance CANNOT un-assign - there is no serialized edge to break.
//   An authored prefab reference still WINS (Enemy latches it in Awake), so any
//   future per-prefab art authoring keeps working; this is the floor, not a lock.
//
// RESOLUTION ORDER (first hit wins, always non-null):
//   1. Resources/Enemies/VfxSets/EnemyVfxSet_<family>  - per-family art when authored
//   2. Resources/Enemies/EnemyVfxSet_Default           - the shipped default asset
//   3. A synthesized in-memory instance                - C# field initializers only
//      (_telegraphDuration 0.4s, and the owner-tagged Fire_* ranged keys that
//      EnemyTypeVfxSet now declares). NEVER null, so the telegraph window can
//      never collapse back to zero.
//
// INSTRUMENTATION (CLAUDE.md section 12): every resolution emits one FlowTrace.Once
// per family naming which rung answered; rung 3 is a Warn because it means the
// shipped asset is missing from Resources.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// The ONE place an enemy's <see cref="EnemyTypeVfxSet"/> is resolved at
    /// runtime. Address-based (Resources path), never a serialized reference, so
    /// it cannot silently regress to null the way the per-prefab assignment did.
    /// Always returns a non-null set.
    /// </summary>
    public static class EnemyTypeVfxLibrary
    {
        /// <summary>Resources path of the shipped default set (rung 2).</summary>
        public const string DefaultResourcePath = "Enemies/EnemyVfxSet_Default";

        /// <summary>Resources path FORMAT for an optional per-family override (rung 1).</summary>
        public const string FamilyResourcePathFormat = "Enemies/VfxSets/EnemyVfxSet_{0}";

        /// <summary>Family used when a def carries none (mirrors EnemyDef.Family's own default).</summary>
        public const string DefaultFamily = "hollow";

        // Resolved-per-family cache. Resources.Load is not free and Configure runs
        // for every enemy of every wave, so resolve once per family per session.
        private static readonly Dictionary<string, EnemyTypeVfxSet> s_byFamily =
            new Dictionary<string, EnemyTypeVfxSet>(System.StringComparer.OrdinalIgnoreCase);

        // Rung 3: the last-resort instance. Built once, kept alive for the session.
        private static EnemyTypeVfxSet s_synthesized;

        /// <summary>
        /// Resolves the VFX/audio set for <paramref name="def"/>'s family. Never null.
        /// A null def resolves the default family.
        /// </summary>
        public static EnemyTypeVfxSet Resolve(EnemyDef def)
            => Resolve(def != null ? def.Family : null);

        /// <summary>
        /// Resolves the VFX/audio set for <paramref name="family"/> ("hollow" / "orc" /
        /// "troll" / "ogre"). Never null - falls through the three rungs described in
        /// the file header.
        /// </summary>
        public static EnemyTypeVfxSet Resolve(string family)
        {
            string key = string.IsNullOrEmpty(family) ? DefaultFamily : family.Trim();
            if (key.Length == 0) key = DefaultFamily;

            if (s_byFamily.TryGetValue(key, out EnemyTypeVfxSet cached) && cached != null)
                return cached;

            EnemyTypeVfxSet set = null;
            string rung = "none";

            // Rung 1 - per-family override, when the art lane authors one.
            string familyPath = string.Format(FamilyResourcePathFormat, key.ToLowerInvariant());
            set = Guard.Try("EnemyVfx", "load family vfx set '" + familyPath + "'",
                            () => Resources.Load<EnemyTypeVfxSet>(familyPath), null);
            if (set != null) rung = "family:" + familyPath;

            // Rung 2 - the shipped default asset.
            if (set == null)
            {
                set = Guard.Try("EnemyVfx", "load default vfx set '" + DefaultResourcePath + "'",
                                () => Resources.Load<EnemyTypeVfxSet>(DefaultResourcePath), null);
                if (set != null) rung = "default:" + DefaultResourcePath;
            }

            // Rung 3 - synthesized. Field initializers give a 0.4s telegraph, so the
            // wind-up window survives even a Resources folder that lost the asset.
            if (set == null)
            {
                set = EnsureSynthesized();
                rung = "synthesized";
                FlowTrace.Warn("EnemyVfx",
                    "family '" + key + "': neither '" + familyPath + "' nor '" + DefaultResourcePath +
                    "' loaded from Resources - using a SYNTHESIZED EnemyTypeVfxSet. Telegraph timing " +
                    "still works, but no authored hit/death/attack clips or prefabs exist. Restore " +
                    "Assets/Resources/" + DefaultResourcePath + ".asset.");
            }

            s_byFamily[key] = set;

            FlowTrace.Once("EnemyVfx", "resolve-" + key,
                "family '" + key + "' resolved its EnemyTypeVfxSet via rung '" + rung +
                "' (telegraph=" + (set != null ? set.TelegraphDuration.ToString("0.##") : "?") + "s).");

            return set;
        }

        /// <summary>
        /// True when <paramref name="set"/> came out of this library rather than being
        /// authored on a prefab. Enemy uses it so a library-supplied set can be
        /// UPGRADED once the stat block names a family, while an authored prefab
        /// reference is never overwritten.
        /// </summary>
        public static bool IsLibrarySet(EnemyTypeVfxSet set)
        {
            if (set == null) return false;
            if (set == s_synthesized) return true;
            foreach (KeyValuePair<string, EnemyTypeVfxSet> kv in s_byFamily)
                if (kv.Value == set) return true;
            return false;
        }

        /// <summary>
        /// Drops the per-family cache. Editor/regression only - a data gate re-runs
        /// resolution after touching Resources, and a stale cache would mask that.
        /// </summary>
        public static void ClearCache() => s_byFamily.Clear();

        private static EnemyTypeVfxSet EnsureSynthesized()
        {
            if (s_synthesized != null) return s_synthesized;
            s_synthesized = ScriptableObject.CreateInstance<EnemyTypeVfxSet>();
            s_synthesized.name = "EnemyVfxSet_Synthesized";
            // Not an asset and never saved into a scene - it exists only for this session.
            s_synthesized.hideFlags = HideFlags.HideAndDontSave;
            return s_synthesized;
        }
    }
}
