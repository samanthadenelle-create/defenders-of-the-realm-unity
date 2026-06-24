# WORK_ORDER_510 — instantiation-time attachment override (JSON-driven, rig-agnostic)

**Status:** READY TO IMPLEMENT (owner greenlit 2026-06-24) · Hero/Equip lane · feeds WO-490 (Offset Forge product)
**Origin:** owner — "we don't care what the bone is called; do an override in JSON and tell it to use the
override... applicable to everything AT INSTANTIATION, not at the model." The cheap+correct fix to the
recurring "every new character = bone-name/count/seating pain" problem.

## 1. The principle (do not violate)
The MODEL is pristine read-only data — we NEVER rename its bones or modify the asset (that breaks anim
bindings + reverts on re-import). Attachment resolution is a RUNTIME concern applied ONCE at instantiation
(weapon/shield attach), through ONE code path every character flows through. The only per-model artifact is
an OPTIONAL one-line JSON pointer. Override is AUTHORITATIVE; avatar auto-map is the FALLBACK.

## 2. Resolution order (in EquipmentController at attach time)
For a given (rigId, leftHand):
1. **JSON override present + path resolves in the instantiated hierarchy** -> use that transform. (authoritative)
2. else **humanoid avatar** -> `animator.GetBoneTransform(leftHand ? LeftHand : RightHand)` (today's behavior).
3. else **name fallback** (contains "hand"), else hard-fail loud (see §4). Never silently attach to root.

`rigId` = the character's model/prefab name (same key style the codebase already uses, e.g. "Knight",
"Orc_Warrior") — pass it in at attach. `leftHand` = the existing shields->LeftHand flag.

## 3. Data + registry (new, light)
- **`Assets/OffsetForge/rig-profiles.json`** (editor authoring path) with a `Resources/OffsetForge/rig-profiles`
  TextAsset fallback for builds — SAME dual-path pattern as `AttachmentOffsetRegistry` (offsets.json).
  Schema: `{ "profiles": [ { "rigId": "Knight", "rightHand": "<transform path or unique name>", "leftHand": "<...>" } ] }`.
  Paths are transform HIERARCHY PATHS (unambiguous vs duplicate names) with a plain-name match fallback.
- **`RigAttachmentRegistry`** (new, mirrors AttachmentOffsetRegistry): loads once + caches + `Reload()`.
  API: `bool TryResolve(GameObject root, string rigId, bool leftHand, out Transform anchor, out string how)`
  where `how` is one of "json-override" / "missing" — registry only does tier 1; EquipmentController owns the
  avatar/name fallback. A profile entry whose path does NOT resolve in `root` returns false WITH a reason so
  the caller can FlowTrace.Fail (loud, never silent).
- Keep it self-contained JsonUtility (no new deps), game-agnostic enough to also back the Forge product.

## 4. Self-reporting (THE POINT — bake in, not extras; §12 instrument-first, no-silent-failure)
Data indirection is "hell to triage" ONLY if silent. Make the resolver narrate the full decision chain:
- **One trace line per attach:** `FlowTrace.Step("Offset", "attach rig=<rigId> hand=<R/L> -> '<bone>' (via json-override|avatar|name)")`.
- **Dead/missing override path SCREAMS:** if a profile names a bone absent from the model -> `FlowTrace.Fail`
  + fall to avatar (do NOT silently swallow). The bug self-reports to break-log.
- **Forge validation readout (slice 2):** a small panel listing each rig -> resolved bone with green-check /
  red-X if the path is unresolvable in the loaded model. A human SEES the mapping table without reading code.

## 5. Slices
- **Slice 1 (this WO, runtime):** rig-profiles.json + `RigAttachmentRegistry` + EquipmentController override-first
  resolution + the trace/loud-fail. DEFAULT BEHAVIOR UNCHANGED when no profile exists (avatar path) -> zero
  regression on the current Tripo hero. Headless-gate clean.
- **Slice 2 (Offset Forge authoring):** the hand-context picker WRITES the chosen transform path into
  rig-profiles.json (so authoring an override = clicking the bone, no typing), + the validation readout.
  Folds into WO-490 (sellable "map to any rig" feature = this exact override file).

## 6. Acceptance
- New character (hero or Tripo enemy) attaches weapons via the SAME instantiation resolver; an optional JSON
  line overrides the attach bone with zero model edits. Override path resolves -> used; absent -> avatar; dead
  -> loud FlowTrace.Fail. Every attach prints its resolution path. Gate-clean; no regression on Knight.
- BONES vs FINESSE: the resolver + registry + trace are CLI gate-provable; the Forge authoring/validation
  readout is owner felt (slice 2).

## 7. Do NOT
Rename or modify any model/bone. Silently fall through on a dead path. Greenfield a parallel attach system —
extend EquipmentController's existing attach path + mirror AttachmentOffsetRegistry. Hand-edit .unity scenes.
