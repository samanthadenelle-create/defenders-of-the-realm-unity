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
//
// ── WO-1019 Part A — THE PER-CLASS FIX (owner felt-test 2026-08-10) ──────────
// Owner, verbatim, on Thrain (Mage): "he inherits the hotswap from previous character
// and has nothing explicit for dps". The authored data was never the problem —
// abilities.json classes.mage.abilities is a complete all-magic bar including the
// explicit DPS (mage.fireball, 30 dmg @14m). The BAR was.
//
// This bar persisted under ONE GLOBAL PlayerPrefs key, "dotr-skillbar-extra-v1". Every
// hero read and overwrote it, so switching Grom -> Thrain re-rendered the KNIGHT's
// assigned extras on the Mage. HeroLoadout (the W/E/R rail) had the identical defect and
// was fixed per-class in WO-861 Phase 0 (see its header); this bar — which the header
// above says "deliberately MIRRORS the HeroLoadout persistence pattern" — was left on the
// old global key, so the mirror was broken exactly where it mattered.
//
// TWO changes, both mirroring HeroLoadout:
//   1. PER-CLASS KEY (EquipPrefKeys.SkillBarKeyFor) + EnsureCurrentKey() on every read
//      and write, so a class change re-reads THIS hero's bar and a write can never land
//      in another hero's.
//   2. CLASS-VALIDITY DROP on load: an id that is not authored for the wearer's class
//      (AbilityCatalog.IsUsableByClass — the abilities.json class key, not the id prefix)
//      is DROPPED with a FlowTrace.Warn instead of rendering. A hero must never present
//      another class's kit, and a bar slot must never lie about what it will cast.
// The legacy global key is read ONCE per class through that same filter, so each hero
// inherits only the entries it actually owns and the contamination cannot survive.
// =============================================================================

using System;
using UnityEngine;
using DeNelle.Core.Diagnostics;   // FlowTrace (§12 instrument-first)
using DeNelle.Core.State;         // EquipPrefKeys + GameStateService (per-class key fallback)

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
        /// <summary>
        /// The PlayerPrefs key for a CLASS's assignable extras bar ("dotr-skillbar-mage-extra-v1",
        /// format idx=id;idx=id). Single-sourced from <see cref="EquipPrefKeys"/> so the New Game
        /// reset erases the same keys this component writes.
        /// WO-1019: was a single global const shared by every hero — see the file header.
        /// </summary>
        public static string PrefsKeyFor(string heroClass) => EquipPrefKeys.SkillBarKeyFor(heroClass);

        /// <summary>The key THIS hero persists under, resolved live from its class.</summary>
        private string PrefsKey => PrefsKeyFor(ResolveClass());

        /// <summary>Number of assignable slots on the bar.</summary>
        public const int SlotCount = 3;

        // index -> equipped abilityId (null/empty = an open "+" slot).
        private readonly string[] _slots = new string[SlotCount];

        // The key the currently-held _slots were read from. HeroAbilities resolves its class in
        // ITS Awake and HeroBodySwapper.SetHeroClass can land later still, so a component that
        // loaded in an undefined Awake order may have read the wrong key. EnsureCurrentKey
        // re-reads (once) the moment the resolved key disagrees. Verbatim the HeroLoadout pattern.
        private string _loadedKey;

        private HeroAbilities _abilities;

        /// <summary>Raised whenever the bar changes (assign / clear / load).</summary>
        public event Action Changed;

        private void Awake()
        {
            Load();
        }

        /// <summary>
        /// The wearer's lowercase class key. Byte-identical precedence to
        /// <see cref="HeroLoadout"/>.ResolveClass — the live HeroAbilities is the authority; when
        /// it is absent (a composed dungeon hero carries none) fall back to the SAME persisted
        /// GameState.HeroClass the body itself was built from, so the two bars and the body can
        /// never key off different classes. Only a save with no class lands on the catalog default.
        /// </summary>
        private string ResolveClass()
        {
            if (_abilities == null) _abilities = GetComponent<HeroAbilities>();
            string cls = _abilities != null ? _abilities.HeroClass : null;
            if (!string.IsNullOrEmpty(cls)) return cls;

            var svc = GameStateService.Instance;
            var opt = (svc != null && svc.State != null) ? svc.State.HeroClass.ToNullable() : null;
            if (opt.HasValue)
            {
                // Fully qualified: unqualified 'HeroClass' shadows to a string in this scope.
                switch (opt.Value)
                {
                    case DeNelle.Core.State.HeroClass.Knight: return "knight";
                    case DeNelle.Core.State.HeroClass.Ranger: return "ranger";
                    case DeNelle.Core.State.HeroClass.Mage:   return "mage";
                    // WO-226: the Cleric is a caster and reuses the Mage ability loadout,
                    // so she shares the Mage's bar key too.
                    case DeNelle.Core.State.HeroClass.Cleric: return "mage";
                }
            }
            return AbilityCatalog.DefaultClass;
        }

        // Re-read from PlayerPrefs when the resolved class key has changed since the last load
        // (Awake-order race, or a hot-swap to another hero on the same rig). THIS is the seam
        // that makes a hero switch rebind the bar: every read and every write funnels through it,
        // so no caller can observe the previous hero's bar. Raising Changed from Load is safe — a
        // handler that calls back in sees the keys now MATCHING, so the guard short-circuits.
        private void EnsureCurrentKey()
        {
            string key = PrefsKey;
            if (string.Equals(_loadedKey, key, StringComparison.Ordinal)) return;
            FlowTrace.Step("SkillBar",
                "class key changed '" + (_loadedKey ?? "<none>") + "' -> '" + key + "' - re-reading the hot-swap bar.");
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
            EnsureCurrentKey();   // WO-1019: self-heal an Awake-order / hero-switch class mis-read
            return _slots[slot];
        }

        /// <summary>The slot <paramref name="abilityId"/> currently occupies, or -1 when it's not on the bar.</summary>
        public int SlotOf(string abilityId)
        {
            if (string.IsNullOrEmpty(abilityId)) return -1;
            EnsureCurrentKey();
            for (int i = 0; i < SlotCount; i++)
                if (string.Equals(_slots[i], abilityId, StringComparison.OrdinalIgnoreCase)) return i;
            return -1;
        }

        /// <summary>
        /// Assign <paramref name="abilityId"/> to <paramref name="slot"/>. Returns false when:
        /// the slot is out of range, the id is null/empty, a battle is LIVE (battle-locked), or
        /// it's already exactly there. WO-574: a skill already on the bar in ANOTHER slot is
        /// MOVED here (its old slot is cleared) rather than rejected — so tap-to-move works and
        /// CONFIRM never silently dead-ends on "already on the bar". On success, persists +
        /// raises <see cref="Changed"/>.
        /// </summary>
        public bool Assign(int slot, string abilityId)
        {
            if (slot < 0 || slot >= SlotCount) return false;
            if (string.IsNullOrEmpty(abilityId)) return false;
            EnsureCurrentKey();   // WO-1019: never write this hero's pick into another class's bar
            if (HeroLoadout.EditsLocked)
            {
                FlowTrace.Warn("SkillBar", "Assign REJECTED — battle-locked (slot=" + slot + " id=" + abilityId + ")");
                return false;
            }

            // WO-1019: a hero may only bind an ability its OWN class (or the universal pool)
            // authors. Without this a cross-class assign path would re-contaminate the bar the
            // per-class key just cleaned up.
            string cls = ResolveClass();
            if (!AbilityCatalog.IsUsableByClass(abilityId, cls))
            {
                FlowTrace.Warn("SkillBar", "Assign REJECTED — '" + abilityId + "' is not authored for class '" +
                                           cls + "' (owner='" + (AbilityCatalog.OwningClassOf(abilityId) ?? "<unknown>") +
                                           "'). A hero must never present another class's kit.");
                return false;
            }

            if (string.Equals(_slots[slot], abilityId, StringComparison.OrdinalIgnoreCase))
                return false;   // already exactly here — no-op

            // MOVE semantics: if the skill sits in another slot, vacate it so the same id is
            // never duplicated across the bar (and re-tapping a new slot relocates the skill).
            for (int i = 0; i < SlotCount; i++)
            {
                if (i == slot) continue;
                if (string.Equals(_slots[i], abilityId, StringComparison.OrdinalIgnoreCase))
                {
                    _slots[i] = null;
                    FlowTrace.Step("SkillBar", "Assign MOVE — '" + abilityId + "' vacated slot " + i);
                }
            }

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
            EnsureCurrentKey();

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
            EnsureCurrentKey();
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
        // WO-1019: the key is now PER CLASS (EquipPrefKeys.SkillBarKeyFor), and every id read
        // back is checked against the wearer's class before it reaches a slot.

        private void Save()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < SlotCount; i++)
            {
                if (string.IsNullOrEmpty(_slots[i])) continue;
                if (sb.Length > 0) sb.Append(';');
                sb.Append(i).Append('=').Append(_slots[i]);
            }
            string key = PrefsKey;
            _loadedKey = key;
            PlayerPrefs.SetString(key, sb.ToString());
            PlayerPrefs.Save();
        }

        private void Load()
        {
            for (int i = 0; i < SlotCount; i++) _slots[i] = null;

            string cls = ResolveClass();
            string key = PrefsKeyFor(cls);
            _loadedKey = key;

            // WO-1019 MIGRATION (one-shot, per class, FILTERED): a save written before the
            // per-class split holds the whole roster's assignments under one global key. Read it
            // only when this class has no key of its own yet, and let the class-validity filter
            // below decide what this hero actually inherits — the ids that contaminated the bar
            // are dropped by construction, so there is no migration step that can get it wrong.
            string raw = PlayerPrefs.GetString(key, string.Empty);
            bool fromLegacy = false;
            if (string.IsNullOrEmpty(raw) && !PlayerPrefs.HasKey(key))
            {
                raw = PlayerPrefs.GetString(EquipPrefKeys.SkillBarLegacyGlobalKey, string.Empty);
                fromLegacy = !string.IsNullOrEmpty(raw);
            }

            int dropped = 0;
            string droppedNames = null;
            if (!string.IsNullOrEmpty(raw))
            {
                foreach (var pair in raw.Split(';'))
                {
                    if (string.IsNullOrEmpty(pair)) continue;
                    int eq = pair.IndexOf('=');
                    if (eq <= 0 || eq >= pair.Length - 1) continue;
                    if (!int.TryParse(pair.Substring(0, eq), out int idx) || idx < 0 || idx >= SlotCount) continue;
                    string id = pair.Substring(eq + 1);

                    // THE RULE (WO-1019): a bound ability that is not valid for the active hero's
                    // class is DROPPED — the slot goes empty rather than showing, and casting,
                    // another hero's spell. Never a silent drop (CLAUDE.md §12).
                    if (!AbilityCatalog.IsUsableByClass(id, cls))
                    {
                        dropped++;
                        droppedNames = droppedNames == null
                            ? id + "(owner=" + (AbilityCatalog.OwningClassOf(id) ?? "<unknown>") + ")"
                            : droppedNames + "," + id + "(owner=" + (AbilityCatalog.OwningClassOf(id) ?? "<unknown>") + ")";
                        continue;
                    }
                    _slots[idx] = id;
                }
            }

            if (dropped > 0)
                FlowTrace.Warn("SkillBar",
                    "hot-swap bar: DROPPED " + dropped + " id(s) not authored for class '" + cls + "' [" +
                    droppedNames + "] while loading '" + key + "'" + (fromLegacy ? " (from the legacy global key)" : "") +
                    ". A hero must never present another class's kit - WO-1019.");

            // Persist the class's OWN key the first time it is materialised from the legacy blob,
            // so the filtered result is what the next session reads (and the contaminating ids
            // never come back through the legacy path again for this class).
            if (fromLegacy) Save();

            Changed?.Invoke();
        }
    }
}
