
namespace FoodJournalApp.Vision;

// THE SEAM. Everything in the app depends on this interface, never on "Claude" directly.
// - Today: ClaudeVisionAnalyzer calls the Claude API.
// - Swap providers (GPT/Gemini) by writing a new implementation.
// - Move analysis to a Python service later by writing PythonVisionAnalyzer — nothing
//   else in the app changes.
public interface IVisionAnalyzer
{
    Task<MealAnalysis> AnalyzeAsync(
        Stream image,
        string mediaType,          // e.g. "image/jpeg"
        DateTime capturedAt,
        string? note = null,       // optional user-supplied portion/quantity context
        CancellationToken ct = default);
}

// What the model returns. This is a plain data shape — no framework, no LLM math.
// Our own code sums the item macros into the meal total; we never trust the model to add.
public record MealAnalysis(
    bool IsFood,                   // false = blurry / empty / not-a-meal photo
    string MealType,               // breakfast/lunch/dinner/snack/unknown
    bool IsFried,
    bool IsHeavilyProcessed,
    bool IsSugarHeavy,
    bool IsVegetableLight,
    double JunkScore,              // 0..1
    List<AnalyzedItem> Items);

public record AnalyzedItem(
    string Name,
    int PortionGramsEst,
    string Confidence,             // low/med/high
    int ProteinG,
    int CarbsG,
    int FatG,
    int FiberG,
    int CaloriesEst);