using FoodJournalApp.Rules;

namespace FoodJournalApp.Dtos;

// What GET /api/nudges returns per active nudge. Serializes as
// { ruleId, message, suggestedAction, severity } — same shape as the
// nudges embedded in LogMealResponse.
public record NudgeDto(string RuleId, string Message, string SuggestedAction, string Severity)
{
    public static NudgeDto From(Nudge n) => new(n.RuleId, n.Message, n.SuggestedAction, n.Severity);
}
