# Environment — Assembly-CSharp (no asmdef)

Environmental lighting effects.

## Files

- `NightTorchLightSystem` — torches light up at night

**Removed 2026-08-21 (WO-992): `TorchFireController`.** Provably dead — its GUID appeared in
ZERO `.unity` / `.prefab` / `.asset` (raw-byte scan of the binary scenes included) and there was
no `AddComponent<TorchFireController>` anywhere, so nothing ever created one. Superseded by three
independent live torch paths, each with its own flicker: `NightTorchLightSystem` (this folder),
`DungeonDresser`'s seated torch meshes + Lights, and the per-builder torch code in
`DungeonComposer` / `KayKitChallengeOutpostBuilder` / `EnemyStrongholdBuilder`. Its only remaining
reference was `NightTorchLightSystem.AttachToExistingTorches()`, an always-empty
`FindObjectsByType` sweep, removed in the same change.

> Maintenance: update this README when files are added/removed.
