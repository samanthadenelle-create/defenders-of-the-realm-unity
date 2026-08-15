# RESULT — WO-989 Ballista id

**Status:** IMPLEMENTED — 2026-08-15

## Change

| Surface | Action |
|---------|--------|
| Both `structures-catalog.json` | `id: tower_ballista`, version **21**, dual-copy md5 match |
| `CatalogBootstrap` fallback | registers `tower_ballista` |
| `CatalogRegistry.Get` | aliases `tower_wall_wizard` → ballista entry + FlowTrace once |
| `CatalogRegistry.CanonicalStructureId` | write-path helper |
| `BaseLayoutLoader.Spawn` | rewrites persisted id on load |
| `DefenseTower` | projectile key for both ids |
| Cost basket / regressions / suite list | updated |

## Deferred

- Rename `Structures/WizardTower_1` asset (GUID churn) — flagged `_artNote` on row.
