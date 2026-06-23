# Changelog

All notable changes to Offset Forge are documented here.
This project follows [Semantic Versioning](https://semver.org/).

## [1.0.0] — Unreleased

### Added
- Initial release.
- `Tools ▸ Offset Forge` Editor window.
- Load any model/prefab into a preview viewport (orbit / zoom / pan).
- Rotation X/Y/Z and Position X/Y/Z controls with live two-way binding to the model.
- Optional 5°/15° rotation snap.
- Live exact offset readout (euler + local position, 2 decimals).
- Copy Rotation / Copy Position to clipboard as paste-ready `Vector3`.
- Save to JSON (`offsets.json`) — append/update per-model offset by id.
- Optional dependency-free `OffsetTable` runtime loader.

### Notes
- Editor-only. No runtime footprint unless the optional loader is used.
- Never modifies source assets.
