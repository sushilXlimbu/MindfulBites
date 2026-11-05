
using FoodJournalApp.Rules;

namespace FoodJournalApp.Dtos;

// What the API returns after analyzing a logged meal photo.
public record LogMealResponse(
    Guid MealId,
    bool IsFood,
    string MealType,
    IReadOnlyList<LoggedItem> Items,
    MealTotals Totals,
    IReadOnlyList<Nudge> Nudges);

public record LoggedItem(string Name, int PortionGramsEst, string Confidence,
    int ProteinG, int CarbsG, int FatG, int FiberG, int CaloriesEst);

public record MealTotals(int ProteinG, int CarbsG, int FatG, int FiberG, int CaloriesEst);

// A single meal as returned by the history/list endpoints.
public record MealHistoryItem(
    Guid MealId,
    DateTime CapturedAt,
    string MealType,
    string PhotoUrl,
    string? Note,
    MealTotals Totals,
    IReadOnlyList<LoggedItem> Items);

// One calendar day's worth of meals, for the by-day grouped view.
public record MealDayGroup(
    string Date,
    string DayLabel,
    MealTotals DailyTotals,
    IReadOnlyList<MealHistoryItem> Meals);