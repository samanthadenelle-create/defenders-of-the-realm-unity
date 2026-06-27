// =============================================================================
// AssignableSkillBar — the player-assignable EXTRA skill bar (bottom-middle HUD).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// SEPARATE from HeroLoadout. HeroLoadout is the 4 STATIC DEFAULT abilities
// (thrust/parry/heal/charge = the class kit) rendered in the bottom-RIGHT of the
// battle HUD. THIS bar is the bottom-MIDDLE row of player-assignable slots the
// player fills from the Skill Tree with EXTRA (non-default) unlocked skills.
//
// It deliberately MIRRORS the HeroLoadout persistence + battle-lock pattern (so the
// two bars share behaviour without sharing state): a slotIndex -> abilityId map,
// persisted to PlayerPrefs, raising Changed, and REJECTING edits while a battle is
// live (HeroLoadout.EditsLocked — the single Core HUD-context signal both bars use).
// Assignment is therefore an OUT-OF-COMBAT action; the battle HUD only renders it.
//
// Lives on the hero rig alongside HeroLoadout (auto-added by AssignableSkillBarAccess
// when absent). Unity-object null checks are explicit (the project lints away ?./??).
// =============================================================================

using System;
using UnityEngine;
using DeNelle.Core.Diagnostics;   // FlowTrace (§12 instrument-first)

namespace DeNelle.Village
{
    /// <summary>
    /// The hero's player-assignable EXTRA skill bar: a fixed set of slots
    /// (index -> abilityId) the Skill Tree fills and <see cref="Arena.BattleHud9Zone"/>
    /// renders in the bottom-middle. Persisted to PlayerPrefs; edits are battle-locked.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AssignableSkillBar : MonoBehaviour
    {
        /// <summary>PlayerPrefs key for the assignable extras bar (idx=id;idx=id…).</summary>
        public const string PrefsKey = "dotr-skillbar-extra-v1";

        /// <summary>Number of assignable slots on the bar.</summary>
        public const int SlotCount = 4;

        // index -> equipped abilityId (null/empty = an open "+" slot).
        private readonly string[] _slots = new string[SlotCount];

        /// <summary>Raised whenever the bar changes (assign / clear / load).</summary>
        public event Action Changed;

        private void Awake()
        {
            Load();
        }

        /// <summary>Re-reads the saved bar from PlayerPrefs (for a hero that persists across a scene load).</summary>
        public void ReloadFromPrefs()
        {
            Load();
        }

        /// <summary>The abilityId assigned to <paramref name="slot"/>, or null when empty / out of range.</summary>
        public string AbilityIdForSlot(int slot)
        {
            if (slot < 0 || slot >= SlotCount) return null;
            return _slots[slot];
        }

        /// <summary>
        /// Assign <paramref name="abilityId"/> to <paramref name="slot"/>. Returns false when:
        /// the slot is out of range, the id is null/empty, a battle is LIVE (battle-locked), the
        /// id is already on the bar in another slot, or it's already exactly there. On success,
        /// persists + raises <see cref="Changed"/>.
        /// </summary>
        public bool Assign(int slot, string abilityId)
        {
            if (slot < 0 || slot >= SlotCount) return false;
            if (string.IsNullOrEmpty(abilityId)) return false;
            if (HeroLoadout.EditsLocked)
            {
                FlowTrace.Warn("SkillBar", "Assign REJECTED — battle-locked (slot=" + slot + " id=" + abilityId + ")");
                return false;
            }

            for (int i = 0; i < SlotCount; i++)
            {
                if (i == slot) continue;
                if (string.Equals(_slots[i], abilityId, StringComparison.OrdinalIgnoreCase))
                {
                    FlowTrace.Warn("SkillBar", "Assign REJECTED — '" + abilityId + "' already on the bar (slot " + i + ")");
                    return false;
                }
            }
            if (string.Equals(_slots[slot], abilityId, StringComparison.OrdinalIgnoreCase))
                return false;

            _slots[slot] = abilityId;
            Save();
            FlowTrace.Step("SkillBar", "Assign slot=" + slot + " id=" + abilityId + " SAVED (" + PrefsKey + ")");
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Add <paramref name="abilityId"/> to the FIRST empty slot (the Skill-Tree "add to bar"
        /// action). Battle-locked + instrumented. Returns false when the id is empty/already on
        /// the bar, a battle is live, or every slot is full.
        /// </summary>
        public bool TryAdd(string abilityId)
        {
            if (string.IsNullOrEmpty(abilityId)) return false;
            FlowTrace.Step("SkillBar", "TryAdd requested id=" + abilityId);

            int firstEmpty = -1;
            for (int i = 0; i < SlotCount; i++)
            {
                if (string.Equals(_slots[i], abilityId, StringComparison.OrdinalIgnoreCase))
                {
                    FlowTrace.Warn("SkillBar", "TryAdd no-op — '" + abilityId + "' already on the bar (slot " + i + ")");
                    return false;
                }
                if (firstEmpty < 0 && string.IsNullOrEmpty(_slots[i])) firstEmpty = i;
            }
            if (firstEmpty < 0)
            {
                FlowTrace.Warn("SkillBar", "TryAdd — no free slot for id=" + abilityId);
                return false;
            }
            return Assign(firstEmpty, abilityId);
        }

        /// <summary>Clears the ability in <paramref name="slot"/> (battle-locked). True when something changed.</summary>
        public bool Clear(int slot)
        {
            if (slot < 0 || slot >= SlotCount) return false;
            if (HeroLoadout.EditsLocked)
            {
                FlowTrace.Warn("SkillBar", "Clear REJECTED — battle-locked (slot=" + slot + ")");
                return false;
            }
            if (string.IsNullOrEmpty(_slots[slot])) return false;
            _slots[slot] = null;
            Save();
            FlowTrace.Step("SkillBar", "Clear slot=" + slot + " SAVED (" + PrefsKey + ")");
            Changed?.Invoke();
            return true;
        }

        // ── persistence ──────────────────────────────────────────────────────
        // Format: "0=knight.snare-arrow;2=knight.mending-salve". Empty slots are skipped.

        private void Save()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < SlotCount; i++)
            {
                if (string.IsNullOrEmpty(_slots[i])) continue;
                if (sb.Length > 0) sb.Append(';');
                sb.Append(i).Append('=').Append(_slots[i]);
            }
            PlayerPrefs.SetString(PrefsKey, sb.ToString());
            PlayerPrefs.Save();
        }

        private void Load()
        {
            for (int i = 0; i < SlotCount; i++) _slots[i] = null;
            string raw = PlayerPrefs.GetString(PrefsKey, string.Empty);
            if (!string.IsNullOrEmpty(raw))
            {
                foreach (var pair in raw.Split(';'))
                {
                    if (string.IsNullOrEmpty(pair)) continue;
                    int eq = pair.IndexOf('=');
                    if (eq <= 0 || eq >= pair.Length - 1) continue;
                    if (int.TryParse(pair.Substring(0, eq), out int idx) && idx >= 0 && idx < SlotCount)
                        _slots[idx] = pair.Substring(eq + 1);
                }
            }
            Changed?.Invoke();
        }
    }
}
