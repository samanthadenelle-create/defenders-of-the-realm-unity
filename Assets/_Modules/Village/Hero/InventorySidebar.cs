// =============================================================================
// InventorySidebar — RETIRED in WO-434 Phase C.
// -----------------------------------------------------------------------------
// The selection detail-pane logic this file held (BuildWeaponSidebar /
// BuildArmorSidebar / BuildConsumableSidebar + the _selWeapon/_selArmor/
// _selConsumable selection state + ClearSelection) moved INTO InventoryVM
// (Selected / SelectedId / Select / Equip / Use). The inventory layout shows
// selection via grid-cell highlight + the paper-doll (no separate sidebar pane),
// so the View no longer renders a detail sidebar. This file is kept as an empty
// partial so its .meta + the assembly's file set are unchanged (no scene/asmdef
// churn); the type is the same merged HeroInventoryController partial.
// =============================================================================

using UnityEngine;

namespace DeNelle.Village
{
    public sealed partial class HeroInventoryController : MonoBehaviour
    {
        // Intentionally empty — see header. All former members live in InventoryVM now.
    }
}
