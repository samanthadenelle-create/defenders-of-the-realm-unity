// =============================================================================
// HeroLoadout — the per-hero equipped-ability map (Knight skill-tree spine).
// -----------------------------------------------------------------------------
// The Knight heal+ranged skill tree equips its tier-1 SKILL nodes into the W/E/R
// ability slots. This component holds that mapping (slot -> abilityId) and
// persists it to PlayerPrefs. HeroAbilities.Resolve(slot) reads it: an equipped
// id resolves via AbilityCatalog.FindById; an empty slot falls back to the
// class's stock Q/W/E/R def — so with NO loadout set, behaviour is IDENTICAL to
// the pre-loadout baseline (the chooser is purely additive).
//
//   Q  — LOCKED. Always the class basic attack; never equippable (Equip rejects
//        it). The chooser only ever fills W/E/R.
//
// MODULE: DeNelle.Village — same asmdef as HeroAbilities / AbilityCatalog, so
// these are plain calls (no reflection seam). Lives on the hero rig alongside
// HeroAbilities; resolved by HeroAbilities/HeroAbilitiesHudBridge via
// GetComponent. Unity-object null checks are explicit (the project lints away
// ?./?? on UnityEngine.Object).
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// The hero's equipped-ability loadout: a slot -> abilityId map the skill-tree
    /// chooser writes and <see cref="HeroAbilities"/> reads. Persisted to
    /// PlayerPrefs. Q is the locked basic attack and is never stored here.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeroLoadout : MonoBehaviour
    {
        /// <summary>PlayerPrefs key for the Knight v1 loadout (slot=id;slot=id…).</summary>
        public const string PrefsKey = "dotr-loadout-knight-v1";

        // slot -> equipped abilityId. Q is never a key (it's the locked basic attack).
        private readonly Dictionary<AbilitySlot, string> _slots = new Dictionary<AbilitySlot, string>();

        /// <summary>Raised whenever the loadout changes (equip / clear / load).</summary>
        public event Action Changed;

        private void Awake()
        {
            Load();
        }

        /// <summary>
        /// Re-reads the saved loadout from PlayerPrefs into this instance (replays Load).
        /// Awake already does this for a freshly-added component; this public path covers a
        /// hero that PERSISTS across a scene load (DontDestroyOnLoad / carried hero) — its
        /// Awake does not re-run, so HeroControlEnsurer calls this to guarantee the saved
        /// W/E/R loadout is restored after every (re)ensure. Raises <see cref="Changed"/>.
        /// </summary>
        public void ReloadFromPrefs()
        {
            Load();
        }

        /// <summary>
        /// The abilityId equipped in <paramref name="slot"/>, or null when nothing is
        /// equipped (the slot then falls back to the class's stock def). Q always
        /// returns null — it is the locked basic attack, resolved from the class kit.
        /// </summary>
        public string AbilityIdForSlot(AbilitySlot slot)
        {
            if (slot == AbilitySlot.Q) return null;
            return _slots.TryGetValue(slot, out var id) ? id : null;
        }

        /// <summary>
        /// Equips <paramref name="abilityId"/> into <paramref name="slot"/>. Returns
        /// false (no change) when: the slot is Q (locked basic attack), the id is
        /// null/empty, or that same id is already equipped in another slot (no
        /// duplicate equips). On success, persists + raises <see cref="Changed"/>.
        /// </summary>
        public bool Equip(AbilitySlot slot, string abilityId)
        {
            if (slot == AbilitySlot.Q) return false;            // Q is the locked basic attack
            if (string.IsNullOrEmpty(abilityId)) return false;

            // Reject a duplicate equip — the same ability can't sit in two slots.
            foreach (var kvp in _slots)
            {
                if (kvp.Key == slot) continue;
                if (string.Equals(kvp.Value, abilityId, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            // No-op if it's already exactly here (avoid a redundant save / event).
            if (_slots.TryGetValue(slot, out var cur) &&
                string.Equals(cur, abilityId, StringComparison.OrdinalIgnoreCase))
                return false;

            _slots[slot] = abilityId;
            Save();
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Clears the ability equipped in <paramref name="slot"/> (the slot reverts to
        /// the class's stock def). No-op for Q. Returns true when something changed.
        /// </summary>
        public bool Clear(AbilitySlot slot)
        {
            if (slot == AbilitySlot.Q) return false;
            if (!_slots.Remove(slot)) return false;
            Save();
            Changed?.Invoke();
            return true;
        }

        // ── persistence ──────────────────────────────────────────────────────
        // Format: "w=knight.snare-arrow;e=knight.mending-salve;r=knight.suppressing-volley"
        // Q is never written. Unknown / Q keys on load are ignored defensively.

        private void Save()
        {
            var sb = new System.Text.StringBuilder();
            bool first = true;
            foreach (var kvp in _slots)
            {
                if (kvp.Key == AbilitySlot.Q || string.IsNullOrEmpty(kvp.Value)) continue;
                if (!first) sb.Append(';');
                sb.Append(SlotKey(kvp.Key)).Append('=').Append(kvp.Value);
                first = false;
            }
            PlayerPrefs.SetString(PrefsKey, sb.ToString());
            PlayerPrefs.Save();
        }

        private void Load()
        {
            _slots.Clear();
            string raw = PlayerPrefs.GetString(PrefsKey, string.Empty);
            if (!string.IsNullOrEmpty(raw))
            {
                foreach (var pair in raw.Split(';'))
                {
                    if (string.IsNullOrEmpty(pair)) continue;
                    int eq = pair.IndexOf('=');
                    if (eq <= 0 || eq >= pair.Length - 1) continue;
                    var slot = ParseSlot(pair.Substring(0, eq));
                    if (!slot.HasValue || slot.Value == AbilitySlot.Q) continue;
                    _slots[slot.Value] = pair.Substring(eq + 1);
                }
            }
            Changed?.Invoke();
        }

        private static string SlotKey(AbilitySlot slot)
        {
            switch (slot)
            {
                case AbilitySlot.W: return "w";
                case AbilitySlot.E: return "e";
                case AbilitySlot.R: return "r";
                default: return "q";
            }
        }

        private static AbilitySlot? ParseSlot(string s)
        {
            switch ((s ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "q": return AbilitySlot.Q;
                case "w": return AbilitySlot.W;
                case "e": return AbilitySlot.E;
                case "r": return AbilitySlot.R;
                default: return null;
            }
        }
    }
}
