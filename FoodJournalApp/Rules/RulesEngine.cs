
using FoodJournalApp.Domain;

namespace FoodJournalApp.Rules;

// A nudge is pattern-based and trend-framed — never "this meal will hurt you".
// The message text comes from the vetted ClaimLibrary, not from an LLM improvising.
public record Nudge(string RuleId, string Message, string SuggestedAction, string Severity);

// Everything a rule needs to make a decision: the user's recent meals.
// Add more context (summaries, user goals) here as the app grows.
public record RuleContext(IReadOnlyList<Meal> RecentMeals, DateTime Now);

public interface INudgeRule
{
    // Return a Nudge if the pattern fired, otherwise null.
    Nudge? Evaluate(RuleContext ctx);
}

// Collects every registered rule and runs them. Deterministic, testable, LLM-free.
public class RulesEngine
{
    private readonly IEnumerable<INudgeRule> _rules;
    public RulesEngine(IEnumerable<INudgeRule> rules) => _rules = rules;

    public List<Nudge> Evaluate(RuleContext ctx)
        => _rules.Select(r => r.Evaluate(ctx)).OfType<Nudge>().ToList();
}

// The single source of truth for what health claims the app is willing to make.
// Every claim is hedged ("linked to", "over time", "may") and paired with a small action.
// Have a dietitian review this table before launch. The LLM may only REPHRASE these,
// never invent new medical assertions.
public static class ClaimLibrary
{
    public static Nudge FriedFood(int countThisWeek) => new(
        RuleId: "fried_food_frequency",
        Message: $"That's your {countThisWeek} fried meal(s) this week. Diets high in fried " +
                 "food are linked to higher cholesterol and heart-disease risk over time.",
        SuggestedAction: "Try aiming for a couple fewer fried meals next week.",
        Severity: "info");

    public static Nudge LateNight(int countThisWeek) => new(
        RuleId: "late_night_eating",
        Message: $"You've eaten late ({countThisWeek} time(s)) this week. Regular late meals " +
                 "are associated with poorer sleep and digestion for some people.",
        SuggestedAction: "If it works for you, try finishing dinner a bit earlier.",
        Severity: "info");
}