// =============================================================================
// TowerData — WO-86 note.
// -----------------------------------------------------------------------------
// The canonical TowerData ScriptableObject already exists in DeNelle.Core.Data:
//   Assets/_Modules/Core/Data/TowerData.cs
//
// It contains towerName, cost, upgrades[] (each with range, damage, upgradeCost),
// empowerment, and buildTime. TowerCombat.cs reads Tower.CurrentRange and
// Tower.CurrentDamage live from that asset (via Tower.Data.upgrades[level-1]).
//
// DO NOT declare a second TowerData class here — it would create a naming
// conflict since both assemblies are auto-referenced. Use DeNelle.Core.Data.TowerData
// for all tower authoring.
//
// This file is intentionally empty of any class declaration.
// =============================================================================
