// =============================================================================
// WallRepairStrings — user-facing copy for the player wall-repair mechanic.
// -----------------------------------------------------------------------------
// Workstream B note (task brief): new user-facing strings are added here as
// clearly-marked `// LOCALIZE:` constants in the workstream's own code — the
// shared en.json is NOT edited; the integrator consolidates these into the
// localization table. Every constant below is a candidate localization key.
//
// Format strings use String.Format placeholders:
//   {0} in GateNameFormat       -> GateDirection (North / East / South / West)
//   {0} in RepairCostFormat     -> the crystal cost (int)
//   {0} in InsufficientFormat   -> the crystal cost (int)
//   {0} in SelectPromptFormat   -> the structure display name
// =============================================================================

namespace DeNelle.Village
{
    /// <summary>
    /// Static user-facing copy for the wall-repair mechanic. LOCALIZE — these
    /// constants are consolidated into the localization table by the integrator
    /// (the workstream does not edit the shared strings file).
    /// </summary>
    public static class WallRepairStrings
    {
        // ── Structure display names ──────────────────────────────────────────
        // LOCALIZE: village.repair.name.wallSegment
        public const string WallSegmentName = "Wall Section";
        // LOCALIZE: village.repair.name.wallCorner
        public const string WallCornerName = "Wall Corner";
        // LOCALIZE: village.repair.name.gateFormat  (placeholder {0} = direction)
        public const string GateNameFormat = "{0} Gate";
        // LOCALIZE: village.repair.name.gateGeneric
        public const string GateGenericName = "Force-Field Gate";
        // LOCALIZE: village.repair.name.buildingGeneric
        public const string BuildingGenericName = "Building";
        // LOCALIZE: village.repair.name.structureGeneric
        public const string StructureGenericName = "Structure";

        // ── Repair-prompt copy ───────────────────────────────────────────────
        // OWNER RULING 2026-07-11: repair is priced in IN-KIND MATERIALS (the
        // structure's own build cost scaled by damage), never crystals. A fully
        // destroyed structure is a REBUILD at full build cost. Copy uses plain
        // hyphens only (tofu rule — the build font drops fancy glyphs).
        // LOCALIZE: village.repair.prompt.title
        public const string PromptTitle = "Repair Structure";
        // LOCALIZE: village.repair.prompt.subtitleFormat  ({0} = structure name)
        // Legacy cost-less form — kept for compatibility; the live prompt uses
        // SubtitleWithCostFormat / RebuildSubtitleFormat below.
        public const string SubtitleFormat = "Repair the {0}?";
        // LOCALIZE: village.repair.prompt.subtitleWithCostFormat  ({0} = name, {1} = materials, e.g. "12 wood, 4 iron")
        public const string SubtitleWithCostFormat = "Repair the {0}? Cost: {1}";
        // LOCALIZE: village.repair.prompt.rebuildSubtitleFormat  ({0} = name, {1} = materials)
        public const string RebuildSubtitleFormat = "Rebuild the {0}? Cost: {1}";
        // LOCALIZE: village.repair.prompt.costFormat  ({0} = the composed materials list)
        public const string CostFormat = "Cost: {0}";
        // LOCALIZE: village.repair.prompt.confirm
        public const string ConfirmLabel = "Repair";
        // LOCALIZE: village.repair.prompt.confirmRebuild
        public const string RebuildLabel = "Rebuild";
        // LOCALIZE: village.repair.prompt.cancel
        public const string CancelLabel = "Cancel";

        // ── Feedback copy ────────────────────────────────────────────────────
        // LOCALIZE: village.repair.feedback.success
        public const string SuccessMessage = "Structure repaired!";
        // LOCALIZE: village.repair.feedback.rebuilt
        public const string RebuiltMessage = "Structure rebuilt!";
        // LOCALIZE: village.repair.feedback.insufficientFormat  ({0} = the composed materials list)
        public const string InsufficientFormat = "Not enough materials - need {0}.";
        // LOCALIZE: village.repair.feedback.intact
        public const string IntactMessage = "That structure is undamaged.";
        // LOCALIZE: village.repair.hint
        public const string SelectHint = "Tap a damaged wall, gate or building to repair it.";
    }
}
