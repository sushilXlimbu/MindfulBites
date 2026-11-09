
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FoodJournalApp.Security;

namespace FoodJournalApp.Vision;

// Calls the Claude Messages API and forces the model to answer by "calling" a single
// tool whose input_schema IS our food schema. Forcing tool_choice guarantees the model
// returns arguments that match the schema — no prose, no markdown fences, no regex parsing.
//
// Verified against the Claude API as of July 2026:
//   - endpoint:      POST https://api.anthropic.com/v1/messages
//   - headers:       x-api-key, anthropic-version: 2023-06-01, content-type: application/json
//   - model:         claude-sonnet-4-6  (sweet spot for image workloads)
//   - image block:   { type: "image", source: { type: "base64", media_type, data } }
//   - forced tool:   tool_choice = { type: "tool", name: "record_meal_analysis" }
// If Anthropic revises the wire format, this is the ONLY file that changes.
public class ClaudeVisionAnalyzer : IVisionAnalyzer
{
    private const string Model = "claude-sonnet-4-6";
    private const string ToolName = "record_meal_analysis";

    private readonly HttpClient _http;
    private readonly string _apiKey;

    public ClaudeVisionAnalyzer(HttpClient http, IConfiguration config)
    {
        _http = http;
        _apiKey = SecretConfig.Require(config, "Anthropic:ApiKey");
    }

    public async Task<MealAnalysis> AnalyzeAsync(
        Stream image, string mediaType, DateTime capturedAt, string? note = null, CancellationToken ct = default)
    {
        // Base64-encode the (already client-compressed) image.
        using var ms = new MemoryStream();
        await image.CopyToAsync(ms, ct);
        var base64 = Convert.ToBase64String(ms.ToArray());

        var localTime = capturedAt.ToLocalTime().ToString("h:mm tt");

        var noteInstruction = string.IsNullOrWhiteSpace(note)
            ? ""
            : $"\nThe user added this note about what they ate: '{note}'. Use it to refine portion and " +
              "quantity estimates — it overrides what the photo alone suggests about amount (e.g. if they " +
              "say they ate half, estimate for half).";

        var requestBody = new
        {
            model = Model,
            max_tokens = 1024,
            tools = new[] { FoodAnalysisTool.Definition },
            tool_choice = new { type = "tool", name = ToolName },
            messages = new[]
            {
                new
                {
                    role = "user",
                    // Image FIRST, then text — the API performs best with image-then-text.
                    content = new object[]
                    {
                        new { type = "image", source = new { type = "base64", media_type = mediaType, data = base64 } },
                        new { type = "text", text =
                            $"This photo was taken at {localTime}, when the user is about to eat.\n" +
                            "Identify each distinct food on the plate and estimate portion and macros. " +
                            "Estimates are approximate by design — set confidence honestly (low/med/high). " +
                            "If the image is not food or is unusable, set is_food to false and return an empty items list. " +
                            "Do not add up totals yourself; only report per-item values." +
                            noteInstruction }
                    }
                }
            }
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages");
        req.Headers.Add("x-api-key", _apiKey);
        req.Headers.Add("anthropic-version", "2023-06-01");
        req.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        using var resp = await _http.SendAsync(req, ct);
        var json = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
            throw new HttpRequestException($"Claude API {(int)resp.StatusCode}: {json}");

        // The response content is an array of blocks. Find the tool_use block and read its input.
        using var doc = JsonDocument.Parse(json);
        var content = doc.RootElement.GetProperty("content");
        foreach (var block in content.EnumerateArray())
        {
            if (block.GetProperty("type").GetString() == "tool_use")
            {
                var input = block.GetProperty("input").GetRawText();
                var parsed = JsonSerializer.Deserialize<ToolInput>(input, JsonOpts)
                             ?? throw new InvalidOperationException("Empty tool input.");
                return parsed.ToMealAnalysis();
            }
        }

        throw new InvalidOperationException("Model did not return a tool_use block.");
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    // Mirrors the tool input_schema below; converts snake_case JSON into our MealAnalysis.
    private record ToolInput(
        [property: JsonPropertyName("is_food")] bool IsFood,
        [property: JsonPropertyName("meal_type")] string MealType,
        [property: JsonPropertyName("is_fried")] bool IsFried,
        [property: JsonPropertyName("is_heavily_processed")] bool IsHeavilyProcessed,
        [property: JsonPropertyName("is_sugar_heavy")] bool IsSugarHeavy,
        [property: JsonPropertyName("is_vegetable_light")] bool IsVegetableLight,
        [property: JsonPropertyName("junk_score")] double JunkScore,
        [property: JsonPropertyName("items")] List<ToolItem> Items)
    {
        public MealAnalysis ToMealAnalysis() => new(
            IsFood, MealType ?? "unknown", IsFried, IsHeavilyProcessed, IsSugarHeavy,
            IsVegetableLight, JunkScore,
            (Items ?? new()).Select(i => new AnalyzedItem(
                i.Name, i.PortionGramsEst, i.Confidence,
                i.ProteinG, i.CarbsG, i.FatG, i.FiberG, i.CaloriesEst)).ToList());
    }

    private record ToolItem(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("portion_grams_est")] int PortionGramsEst,
        [property: JsonPropertyName("confidence")] string Confidence,
        [property: JsonPropertyName("protein_g")] int ProteinG,
        [property: JsonPropertyName("carbs_g")] int CarbsG,
        [property: JsonPropertyName("fat_g")] int FatG,
        [property: JsonPropertyName("fiber_g")] int FiberG,
        [property: JsonPropertyName("calories_est")] int CaloriesEst);
}