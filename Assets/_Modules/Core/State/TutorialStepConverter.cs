// =============================================================================
// TutorialStepConverter — JSON converter for the `1..7 | 'done'` union
// -----------------------------------------------------------------------------
// `tutorialStep` in the React save is a NUMBER for steps 1-7 and the STRING
// "done" when finished (saveSchema.ts `tutorialStepSchema`). Newtonsoft cannot
// express that with StringEnumConverter, so this converter:
//   - writes Step1..Step7 as raw JSON numbers 1..7,
//   - writes Done as the JSON string "done",
//   - reads either form back (and tolerates a numeric > 7 as Done).
// =============================================================================

using System;
using Newtonsoft.Json;

namespace DeNelle.Core.State
{
    /// <summary>Newtonsoft converter for <see cref="TutorialStep"/>.</summary>
    public sealed class TutorialStepConverter : JsonConverter<TutorialStep>
    {
        public override void WriteJson(JsonWriter writer, TutorialStep value, JsonSerializer serializer)
        {
            if (value == TutorialStep.Done)
                writer.WriteValue("done");
            else
                writer.WriteValue((int)value);
        }

        public override TutorialStep ReadJson(JsonReader reader, Type objectType,
            TutorialStep existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            switch (reader.TokenType)
            {
                case JsonToken.Integer:
                {
                    var n = Convert.ToInt32(reader.Value);
                    if (n < 1) return TutorialStep.Step1;
                    if (n > 7) return TutorialStep.Done;
                    return (TutorialStep)n;
                }
                case JsonToken.String:
                {
                    var s = (string)reader.Value;
                    if (string.Equals(s, "done", StringComparison.OrdinalIgnoreCase))
                        return TutorialStep.Done;
                    if (int.TryParse(s, out var parsed))
                        return parsed > 7 ? TutorialStep.Done
                             : parsed < 1 ? TutorialStep.Step1
                             : (TutorialStep)parsed;
                    return TutorialStep.Step1;
                }
                case JsonToken.Null:
                    // A migrated save may omit it; the migrator seeds 'done'. A
                    // fresh save starts at Step1 — caller default handles that.
                    return TutorialStep.Done;
                default:
                    return TutorialStep.Step1;
            }
        }
    }
}
