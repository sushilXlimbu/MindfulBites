
namespace FoodJournalApp.Vision;

// The single tool the model is forced to call. Its input_schema defines exactly what
// shape the model must return. Anonymous objects serialize straight to the JSON Schema
// the API expects. Keep this in sync with ToolInput in ClaudeVisionAnalyzer.
public static class FoodAnalysisTool
{
    public static readonly object Definition = new
    {
        name = "record_meal_analysis",
        description = "Record the identified foods and nutrition estimate for a meal photo.",
        input_schema = new
        {
            type = "object",
            properties = new
            {
                is_food = new { type = "boolean", description = "False if the photo is blurry, empty, or not a meal." },
                meal_type = new { type = "string", @enum = new[] { "breakfast", "lunch", "dinner", "snack", "unknown" } },
                is_fried = new { type = "boolean" },
                is_heavily_processed = new { type = "boolean" },
                is_sugar_heavy = new { type = "boolean" },
                is_vegetable_light = new { type = "boolean" },
                junk_score = new { type = "number", description = "0.0 = whole foods, 1.0 = pure junk food." },
                items = new
                {
                    type = "array",
                    items = new
                    {
                        type = "object",
                        properties = new
                        {
                            name = new { type = "string" },
                            portion_grams_est = new { type = "integer" },
                            confidence = new { type = "string", @enum = new[] { "low", "med", "high" } },
                            protein_g = new { type = "integer" },
                            carbs_g = new { type = "integer" },
                            fat_g = new { type = "integer" },
                            fiber_g = new { type = "integer" },
                            calories_est = new { type = "integer" }
                        },
                        required = new[] { "name", "portion_grams_est", "confidence",
                                           "protein_g", "carbs_g", "fat_g", "fiber_g", "calories_est" }
                    }
                }
            },
            required = new[] { "is_food", "meal_type", "is_fried", "is_heavily_processed",
                               "is_sugar_heavy", "is_vegetable_light", "junk_score", "items" }
        }
    };
}