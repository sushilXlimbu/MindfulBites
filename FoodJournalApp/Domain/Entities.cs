
namespace FoodJournalApp.Domain;

// One row per photo the user logs. The image itself lives in object storage;
// we keep only the URL here. Numbers are computed by OUR code from FoodItems,
// never by the LLM — see MealsController.
public class Meal
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public ApplicationUser? User { get; set; }

    public string PhotoUrl { get; set; } = "";
    public DateTime CapturedAt { get; set; }        // when the user actually ate (upload time)
    public string MealType { get; set; } = "unknown"; // breakfast/lunch/dinner/snack
    public string? Note { get; set; }                // optional user-supplied portion/quantity context

    // Attribute flags the vision model tags the meal with. These drive the rules engine.
    public bool IsFried { get; set; }
    public bool IsHeavilyProcessed { get; set; }
    public bool IsSugarHeavy { get; set; }
    public bool IsVegetableLight { get; set; }
    public double JunkScore { get; set; }           // 0.0 (whole foods) .. 1.0 (pure junk)

    // Rolled-up macros for the whole plate (sum of FoodItems). Approximate by design.
    public int ProteinG { get; set; }
    public int CarbsG { get; set; }
    public int FatG { get; set; }
    public int FiberG { get; set; }
    public int CaloriesEst { get; set; }

    public bool UserConfirmed { get; set; }         // did the user accept/edit the estimate?

    public List<FoodItem> Items { get; set; } = new();
}

// One row per distinct food identified on the plate.
public class FoodItem
{
    public Guid Id { get; set; }
    public Guid MealId { get; set; }
    public Meal? Meal { get; set; }

    public string Name { get; set; } = "";
    public int PortionGramsEst { get; set; }
    public string Confidence { get; set; } = "low"; // low/med/high — surface this in the UI

    public int ProteinG { get; set; }
    public int CarbsG { get; set; }
    public int FatG { get; set; }
    public int FiberG { get; set; }
    public int CaloriesEst { get; set; }
}

// Nightly rollup per user per day. Cheap to query, avoids recomputing history.
public class DailySummary
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateOnly Date { get; set; }

    public int MealCount { get; set; }
    public int TotalProteinG { get; set; }
    public int TotalCarbsG { get; set; }
    public int TotalFatG { get; set; }
    public int TotalFiberG { get; set; }
    public int TotalCaloriesEst { get; set; }
    public int FriedMealCount { get; set; }
    public int LateNightMealCount { get; set; }

    public string? InsightText { get; set; }        // LLM-written summary, generated in batch
}