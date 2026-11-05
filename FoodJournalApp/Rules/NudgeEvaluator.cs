using FoodJournalApp.Data;
using Microsoft.EntityFrameworkCore;

namespace FoodJournalApp.Rules;

// The ONE place that decides "which meals do the rules see, and how is the engine called".
// Both POST /api/meals (right after logging) and GET /api/nudges (dashboard) go through
// here, so the two can never drift apart. Read-only: no tracking, no writes.
public class NudgeEvaluator
{
    // How far back (from the anchor) we load meals for rule evaluation. Individual rules
    // apply their own narrower windows (e.g. 7 days) against RuleContext.Now on top of this.
    public const int LookbackDays = 14;

    private readonly AppDbContext _db;
    private readonly RulesEngine _rules;

    public NudgeEvaluator(AppDbContext db, RulesEngine rules)
    {
        _db = db;
        _rules = rules;
    }

    // windowAnchorUtc: the point the lookback window is measured back from.
    // The meal-logging path anchors on the logged meal's capturedAt; the dashboard
    // anchors on "now". Rules themselves always evaluate against DateTime.UtcNow.
    public async Task<List<Nudge>> EvaluateForUserAsync(
        Guid userId, DateTime windowAnchorUtc, CancellationToken ct)
    {
        var since = windowAnchorUtc.AddDays(-LookbackDays);

        var recent = await _db.Meals
            .Where(m => m.UserId == userId && m.CapturedAt >= since)
            .AsNoTracking()
            .ToListAsync(ct);

        return _rules.Evaluate(new RuleContext(recent, DateTime.UtcNow));
    }
}
