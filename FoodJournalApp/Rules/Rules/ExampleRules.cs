
using FoodJournalApp.Rules;

namespace FoodJournalApp.Rules.Rules;

// "5+ fried meals in the last 7 days." Thresholds live here so they're easy to tune.
public class FriedFoodFrequencyRule : INudgeRule
{
    private const int WindowDays = 7;
    private const int Threshold = 5;

    public Nudge? Evaluate(RuleContext ctx)
    {
        var since = ctx.Now.AddDays(-WindowDays);
        var count = ctx.RecentMeals.Count(m => m.IsFried && m.CapturedAt >= since);
        return count >= Threshold ? ClaimLibrary.FriedFood(count) : null;
    }
}

// "3+ meals after 10pm in the last 7 days."
public class LateNightEatingRule : INudgeRule
{
    private const int WindowDays = 7;
    private const int Threshold = 3;
    private const int LateHour = 22; // 10pm local

    public Nudge? Evaluate(RuleContext ctx)
    {
        var since = ctx.Now.AddDays(-WindowDays);
        var count = ctx.RecentMeals.Count(m =>
            m.CapturedAt >= since && m.CapturedAt.ToLocalTime().Hour >= LateHour);
        return count >= Threshold ? ClaimLibrary.LateNight(count) : null;
    }
}