// =============================================================================
// BattleHudVM — the ATB combat HUD's READ-ONLY snapshot ViewModel (WO-744, MVVM
// migration landmine 1). Pure data: the controller PUSHES a BattleState snapshot
// in (via BattleHudUgui.Render), the VM PROJECTS the catalog resolves the View
// used to do itself — the active hero class + the usable ability/item lists (off
// Defs.HERO_ABILITIES / Defs.ITEM_DEFS). The View renders those instead of
// resolving them off its own held BattleState.
//
// DELIBERATELY DECOUPLED FROM THE FEEL-SIM: this VM carries NO ATB fill / no
// per-frame visual state. The View keeps its `_visualAtb` / `TickVisualAtb`
// feel-sim + its `OnAction` callback contract EXACTLY as before (the risk register
// forbids splitting those). This VM is only the discrete, data-side projection —
// flag-gated behind ff.battlehudvm (default OFF) so the owner can A/B the feel.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using DeNelle.BattleATB.Engine;

namespace DeNelle.BattleATB
{
    /// <summary>Read-only snapshot projection of the active battle for the combat HUD's
    /// Skills/Item submenus. Pure C# — no UnityEngine types, no ATB feel-sim, unit-testable
    /// without a scene. Fed by <see cref="PushSnapshot"/> from the existing controller push.</summary>
    public sealed class BattleHudVM
    {
        /// <summary>One usable inventory item projected for the Item submenu (count &gt; 0),
        /// with its display name already resolved off <see cref="Defs.ITEM_DEFS"/>.</summary>
        public readonly struct UsableItem
        {
            public readonly ItemKind Kind;
            public readonly int Count;
            public readonly string Name;
            public UsableItem(ItemKind kind, int count, string name)
            {
                Kind = kind;
                Count = count;
                Name = name;
            }
        }

        /// <summary>Id of the active unit at the last snapshot (mirrors BattleState.ActiveUnitId).</summary>
        public string ActiveUnitId { get; private set; }

        /// <summary>Hero class of the active unit, or null when the active unit is not a hero.</summary>
        public HeroClass? ActiveHeroClass { get; private set; }

        private readonly List<AbilityDef> _usableAbilities = new List<AbilityDef>();
        private readonly List<UsableItem> _usableItems = new List<UsableItem>();

        /// <summary>The active hero's full ability kit (mirrors the View's old
        /// GetAbilitiesForActiveHero: the class kit off HERO_ABILITIES, NOT cost/cd gated).</summary>
        public IReadOnlyList<AbilityDef> UsableAbilities => _usableAbilities;

        /// <summary>Usable items (count &gt; 0) from the battle's shared inventory, names resolved.</summary>
        public IReadOnlyList<UsableItem> UsableItems => _usableItems;

        /// <summary>Raised after each snapshot push so a bound View can re-read the projection.</summary>
        public event Action Changed;

        /// <summary>Controller PUSH: project a READ-ONLY snapshot from the live battle state.
        /// The View calls this from its existing Render() push — the push direction is kept.
        /// Never mutates <paramref name="state"/>; only reads it.</summary>
        public void PushSnapshot(BattleState state)
        {
            _usableAbilities.Clear();
            _usableItems.Clear();
            ActiveUnitId = state != null ? state.ActiveUnitId : null;
            ActiveHeroClass = null;

            // Active hero class + ability kit — mirrors GetActiveHeroClass + GetAbilitiesForActiveHero.
            if (state != null && !string.IsNullOrEmpty(state.ActiveUnitId) && state.Units != null)
            {
                var unit = state.Units.FirstOrDefault(u => u.Id == state.ActiveUnitId);
                if (unit != null && unit.HeroClass != null)
                {
                    ActiveHeroClass = unit.HeroClass;
                    if (Defs.HERO_ABILITIES.TryGetValue(unit.HeroClass.Value, out var defs) && defs != null)
                        _usableAbilities.AddRange(defs);
                }
            }

            // Usable items — mirrors GetUsableItems + the ITEM_DEFS name lookup.
            if (state != null && state.Inventory != null)
            {
                foreach (var kv in state.Inventory)
                {
                    if (kv.Value <= 0) continue;
                    string name = Defs.ITEM_DEFS.TryGetValue(kv.Key, out var def) && def != null
                        ? def.Name
                        : kv.Key.ToString();
                    _usableItems.Add(new UsableItem(kv.Key, kv.Value, name));
                }
            }

            Changed?.Invoke();
        }
    }
}
