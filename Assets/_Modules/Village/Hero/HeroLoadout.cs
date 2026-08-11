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
using DeNelle.Core;               // CoreServices (battle-context lock signal)
using DeNelle.Core.HudModel;      // HudContext
using DeNelle.Core.Diagnostics;   // FlowTrace (§12 instrument-first)
using DeNelle.Core.State;         // GameStateService + HeroClassOpt.ToNullable (per-class key fallback)

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
        // ── PER-CLASS PERSISTENCE (WO-861 Phase 0) ───────────────────────────
        // This used to be ONE global const "dotr-loadout-knight-v1". With more than one
        // playable hero that is a bug with a name: every hero would load, and overwrite,
        // the KNIGHT's W/E/R bar — Sylas would spawn holding Grom's melee kit and saving
        // over it. The key is now derived from the wearer's class.
        //
        // MIGRATION — deliberately a NO-OP, by construction. The new shape is
        // "dotr-loadout-" + <class> + "-v1", so the Knight's key resolves to
        // "dotr-loadout-knight-v1" — BYTE-IDENTICAL to the old global key. An existing
        // save's Knight bar is therefore read back unchanged with no copy step, no
        // version bump and no window in which a mid-upgrade crash could lose it. Other
        // classes start empty, which is correct: the old value was never theirs (it was
        // the Knight's melee kit), and an empty slot falls back to that class's own stock
        // Q/W/E/R def — exactly the behaviour WO-861 wants.
        //
        // CLERIC NOTE: HeroAbilities aliases Cleric -> the "mage" loadout, so a Cleric
        // shares the Mage's bar key. That mirrors the ability system and is intended.

        /// <summary>
        /// The PlayerPrefs key for a class's W/E/R loadout ("dotr-loadout-knight-v1").
        /// Single-sourced from <see cref="DeNelle.Core.State.EquipPrefKeys"/> so the New
        /// Game reset erases the same keys this component writes.
        /// </summary>
        public static string PrefsKeyFor(string heroClass) =>
            DeNelle.Core.State.EquipPrefKeys.LoadoutKeyFor(heroClass);

        /// <summary>The key THIS hero persists under, resolved live from its class.</summary>
        private string PrefsKey => PrefsKeyFor(ResolveClass());

        // The key the currently-held _slots were read from. HeroAbilities resolves its
        // class in ITS Awake and HeroBodySwapper.SetHeroClass can land later still, so a
        // component that loaded in an undefined Awake order may have read the wrong key.
        // EnsureCurrentKey re-reads (once) the moment the resolved key disagrees.
        private string _loadedKey;

        private HeroAbilities _abilities;

        /// <summary>
        /// The wearer's lowercase class key. HeroAbilities is the authority; when it is absent
        /// (the HeroControlEnsurer EMERGENCY stand-in hero adds HeroLoadout on its own) or has
        /// not resolved yet, fall back to the SAME source HeroAbilities' own Awake backstop
        /// reads - GameState.HeroClass - so the two can never key off different classes. Only
        /// a save with no class at all lands on the catalog default.
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
                // Fully qualified: unqualified 'HeroClass' shadows to a string in this scope
                // (same note HeroAbilities.Awake carries).
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

        // Re-read from PlayerPrefs when the resolved class key has changed since the last
        // load (Awake-order race, or a hot-swap to another hero on the same rig). Raising
        // Changed here is safe: a handler that calls back in sees the keys now MATCHING,
        // so the guard short-circuits and cannot recurse.
        private void EnsureCurrentKey()
        {
            string key = PrefsKey;
            if (string.Equals(_loadedKey, key, StringComparison.Ordinal)) return;
            FlowTrace.Step("Loadout",
                "class key changed '" + (_loadedKey ?? "<none>") + "' -> '" + key + "' - re-reading the bar.");
            Load();
        }

        // slot -> equipped abilityId. Q is never a key (it's the locked basic attack).
        private readonly Dictionary<AbilitySlot, string> _slots = new Dictionary<AbilitySlot, string>();

        /// <summary>Raised whenever the loadout changes (equip / clear / load).</summary>
        public event Action Changed;

        /// <summary>
        /// True while a battle is LIVE (the Core HUD context == Battle). The assignable
        /// battle bar is an OUT-OF-COMBAT editor: <see cref="Equip"/> (and therefore
        /// <see cref="Assign"/>/<see cref="TryAdd"/>) is rejected while this is true so a
        /// player can't re-slot mid-fight. Degrades to UNLOCKED when no HUD model is
        /// registered (headless / menus / pre-battle) so tests + the normal town flow are
        /// unaffected. Reads the SAME signal BattleHud9Zone gates its canvas on.
        /// </summary>
        public static bool EditsLocked
        {
            get
            {
                var hm = CoreServices.HudModel;
                if (hm == null) return false;
                var ctx = hm.Context;
                return ctx != null && ctx.Context == HudContext.Battle;
            }
        }

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
            EnsureCurrentKey();   // WO-861 Phase 0: self-heal an Awake-order class mis-read
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
            EnsureCurrentKey();   // never write this hero's pick into another class's bar

            // §12 instrument-first: prove each step. Battle-lock is the single invariant the
            // model owns (every assign path funnels through Equip), so no UI/path can bypass it.
            if (EditsLocked)
            {
                FlowTrace.Warn("Loadout", "Equip REJECTED — battle-locked (slot=" + slot + " id=" + abilityId + ")");
                return false;
            }

            // WO-1019: a hero may only bind an ability its OWN class (or the universal pool)
            // authors — the same invariant Load enforces on restore, applied at the write side so
            // a cross-class equip path can never re-contaminate the bar.
            string wearerClass = ResolveClass();
            if (!AbilityCatalog.IsUsableByClass(abilityId, wearerClass))
            {
                FlowTrace.Warn("Loadout", "Equip REJECTED — '" + abilityId + "' is not authored for class '" +
                                          wearerClass + "' (owner='" +
                                          (AbilityCatalog.OwningClassOf(abilityId) ?? "<unknown>") +
                                          "'). A hero must never present another class's kit.");
                return false;
            }

            // Reject a duplicate equip — the same ability can't sit in two slots.
            foreach (var kvp in _slots)
            {
                if (kvp.Key == slot) continue;
                if (string.Equals(kvp.Value, abilityId, StringComparison.OrdinalIgnoreCase))
                {
                    FlowTrace.Warn("Loadout", "Equip REJECTED — '" + abilityId + "' already in slot " + kvp.Key);
                    return false;
                }
            }

            // No-op if it's already exactly here (avoid a redundant save / event).
            if (_slots.TryGetValue(slot, out var cur) &&
                string.Equals(cur, abilityId, StringComparison.OrdinalIgnoreCase))
                return false;

            _slots[slot] = abilityId;
            Save();
            FlowTrace.Step("Loadout", "Equip slot=" + slot + " id=" + abilityId + " SAVED (" + PrefsKey + ")");
            Changed?.Invoke();
            return true;
        }

        /// <summary>
        /// Assign <paramref name="abilityId"/> to <paramref name="slot"/> on the assignable
        /// battle bar — a named alias of <see cref="Equip"/> over the SAME persisted W/E/R
        /// model (no parallel store). Battle-locked + instrumented. Returns false when the
        /// slot is Q, the id is empty, a battle is live, the id is already equipped elsewhere,
        /// or it's already exactly there.
        /// </summary>
        public bool Assign(AbilitySlot slot, string abilityId)
        {
            FlowTrace.Step("Loadout", "Assign requested slot=" + slot + " id=" + abilityId);
            return Equip(slot, abilityId);
        }

        /// <summary>
        /// Add <paramref name="abilityId"/> to the FIRST empty assignable slot (W → E → R).
        /// The Skill-Tree "add to battle bar" one-tap action. Battle-locked + instrumented.
        /// Returns false when the id is empty/already-on-the-bar, a battle is live, or every
        /// W/E/R slot is full. Icon is NOT stored here — it derives from the slot at render
        /// (BattleHud9Zone.AbilitySprite), so this stays a single-source slot→id model.
        /// </summary>
        public bool TryAdd(string abilityId)
        {
            if (string.IsNullOrEmpty(abilityId)) return false;
            FlowTrace.Step("Loadout", "TryAdd requested id=" + abilityId);

            AbilitySlot? firstEmpty = null;
            foreach (var slot in new[] { AbilitySlot.W, AbilitySlot.E, AbilitySlot.R })
            {
                string id = AbilityIdForSlot(slot);
                if (!string.IsNullOrEmpty(id) &&
                    string.Equals(id, abilityId, StringComparison.OrdinalIgnoreCase))
                {
                    FlowTrace.Warn("Loadout", "TryAdd no-op — '" + abilityId + "' already on the bar (slot " + slot + ")");
                    return false;
                }
                if (firstEmpty == null && string.IsNullOrEmpty(id)) firstEmpty = slot;
            }
            if (firstEmpty == null)
            {
                FlowTrace.Warn("Loadout", "TryAdd — no free W/E/R slot for id=" + abilityId);
                return false;
            }
            return Equip(firstEmpty.Value, abilityId);
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
            string key = PrefsKey;
            _loadedKey = key;
            PlayerPrefs.SetString(key, sb.ToString());
            PlayerPrefs.Save();
        }

        private void Load()
        {
            _slots.Clear();
            string cls = ResolveClass();
            string key = PrefsKeyFor(cls);
            _loadedKey = key;
            string raw = PlayerPrefs.GetString(key, string.Empty);
            int dropped = 0;
            string droppedNames = null;
            if (!string.IsNullOrEmpty(raw))
            {
                foreach (var pair in raw.Split(';'))
                {
                    if (string.IsNullOrEmpty(pair)) continue;
                    int eq = pair.IndexOf('=');
                    if (eq <= 0 || eq >= pair.Length - 1) continue;
                    var slot = ParseSlot(pair.Substring(0, eq));
                    if (!slot.HasValue || slot.Value == AbilitySlot.Q) continue;
                    string id = pair.Substring(eq + 1);

                    // WO-1019 THE RULE: a bound ability that is not valid for the active hero's
                    // class is DROPPED and the slot falls back to that class's stock def (see
                    // HeroAbilities.Resolve — an empty slot IS the class default). A hero must
                    // never present another class's kit.
                    //
                    // The per-class KEY (WO-861 Phase 0, above) already keeps the classes apart in
                    // the normal flow, so this is the belt-and-braces half: it also covers a key
                    // written before that split, a pool entry that later moved class, and any
                    // future writer that forgets the rule. Never a silent drop (CLAUDE.md §12).
                    if (!AbilityCatalog.IsUsableByClass(id, cls))
                    {
                        dropped++;
                        droppedNames = droppedNames == null
                            ? id + "(owner=" + (AbilityCatalog.OwningClassOf(id) ?? "<unknown>") + ")"
                            : droppedNames + "," + id + "(owner=" + (AbilityCatalog.OwningClassOf(id) ?? "<unknown>") + ")";
                        continue;
                    }
                    _slots[slot.Value] = id;
                }
            }

            if (dropped > 0)
                FlowTrace.Warn("Loadout",
                    "W/E/R bar: DROPPED " + dropped + " id(s) not authored for class '" + cls + "' [" +
                    droppedNames + "] while loading '" + key + "' - those slots fall back to the class " +
                    "stock def. WO-1019.");

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
