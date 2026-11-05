
using System.Globalization;
using System.Security.Claims;
using FoodJournalApp.Data;
using FoodJournalApp.Domain;
using FoodJournalApp.Dtos;
using FoodJournalApp.Rules;
using FoodJournalApp.Vision;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FoodJournalApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class MealsController : ControllerBase
{
    // Only these are accepted for meal photo uploads; the extension used on disk is
    // derived from the content type rather than trusted from the client's filename.
    private static readonly Dictionary<string, string> AllowedPhotoExtensionsByContentType =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = ".jpg",
            ["image/png"] = ".png",
            ["image/webp"] = ".webp"
        };

    private readonly AppDbContext _db;
    private readonly IVisionAnalyzer _vision;   // depends on the SEAM, not on Claude
    private readonly NudgeEvaluator _nudges;
    private readonly IWebHostEnvironment _env;

    public MealsController(AppDbContext db, IVisionAnalyzer vision, NudgeEvaluator nudges, IWebHostEnvironment env)
    {
        _db = db;
        _vision = vision;
        _nudges = nudges;
        _env = env;
    }

    // Pulls the authenticated user's ID out of the JWT. The "sub" claim issued by
    // JwtTokenService is remapped to ClaimTypes.NameIdentifier by the default
    // inbound claim mapping (MapInboundClaims is left on in Program.cs).
    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // Resolves the optional user-supplied capturedAt into a UTC DateTime. Empty/missing
    // defaults to "now". The input is assumed to already be UTC (frontend converts
    // local -> UTC before sending) — we never re-interpret it against server local time.
    private static bool TryResolveCapturedAt(string? input, out DateTime capturedAtUtc, out string? error)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            capturedAtUtc = DateTime.UtcNow;
            error = null;
            return true;
        }

        if (!DateTime.TryParse(input, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            capturedAtUtc = default;
            error = "capturedAt must be a valid ISO 8601 datetime (e.g. 2026-07-20T13:45:00Z).";
            return false;
        }

        var now = DateTime.UtcNow;
        if (parsed > now.AddMinutes(5))
        {
            capturedAtUtc = default;
            error = "capturedAt cannot be more than 5 minutes in the future.";
            return false;
        }

        if (parsed < now.AddYears(-2))
        {
            capturedAtUtc = default;
            error = "capturedAt cannot be more than 2 years in the past.";
            return false;
        }

        capturedAtUtc = parsed;
        error = null;
        return true;
    }

    // POST /api/meals  (multipart/form-data: photo file)
    // The core loop: receive photo -> analyze -> our code sums macros -> store -> run rules.
    [HttpPost]
    [RequestSizeLimit(6_000_000)] // ~6MB; compress client-side before upload
    public async Task<ActionResult<LogMealResponse>> LogMeal(
        IFormFile photo, [FromForm] string? note,
        [FromForm(Name = "capturedAt")] string? capturedAtInput, CancellationToken ct)
    {
        if (photo is null || photo.Length == 0)
            return BadRequest("No photo uploaded.");

        if (!AllowedPhotoExtensionsByContentType.TryGetValue(photo.ContentType, out var extension))
            return BadRequest("Unsupported photo type. Upload a JPEG, PNG, or WebP image.");

        if (!TryResolveCapturedAt(capturedAtInput, out var capturedAt, out var capturedAtError))
            return BadRequest(capturedAtError);

        var userId = GetUserId();

        // 1) The one job only the model can do: read the image -> structured attributes.
        MealAnalysis analysis;
        await using (var stream = photo.OpenReadStream())
        {
            analysis = await _vision.AnalyzeAsync(stream, photo.ContentType, capturedAt, note, ct);
        }

        if (!analysis.IsFood)
            return UnprocessableEntity("That photo doesn't look like food — try another shot.");

        // Only save to disk once we know it's a real food photo — no orphaned files
        // for rejected uploads.
        var fileName = $"{Guid.NewGuid()}{extension}";
        var uploadsDir = Path.Combine(_env.WebRootPath, "uploads");
        Directory.CreateDirectory(uploadsDir);
        await using (var fileStream = new FileStream(Path.Combine(uploadsDir, fileName), FileMode.Create))
        {
            await photo.CopyToAsync(fileStream, ct);
        }

        var photoUrl = $"/uploads/{fileName}"; // relative — frontend prepends the API origin

        // 2) OUR code does the arithmetic. Never ask the model to add up the plate.
        var items = analysis.Items.Select(i => new FoodItem
        {
            Name = i.Name,
            PortionGramsEst = i.PortionGramsEst,
            Confidence = i.Confidence,
            ProteinG = i.ProteinG,
            CarbsG = i.CarbsG,
            FatG = i.FatG,
            FiberG = i.FiberG,
            CaloriesEst = i.CaloriesEst
        }).ToList();

        var meal = new Meal
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PhotoUrl = photoUrl,
            CapturedAt = capturedAt,
            MealType = analysis.MealType,
            Note = string.IsNullOrWhiteSpace(note) ? null : note,
            IsFried = analysis.IsFried,
            IsHeavilyProcessed = analysis.IsHeavilyProcessed,
            IsSugarHeavy = analysis.IsSugarHeavy,
            IsVegetableLight = analysis.IsVegetableLight,
            JunkScore = analysis.JunkScore,
            ProteinG = items.Sum(i => i.ProteinG),
            CarbsG = items.Sum(i => i.CarbsG),
            FatG = items.Sum(i => i.FatG),
            FiberG = items.Sum(i => i.FiberG),
            CaloriesEst = items.Sum(i => i.CaloriesEst),
            Items = items,
            UserConfirmed = false // set true after the user's one-tap confirm step
        };

        _db.Meals.Add(meal);
        await _db.SaveChangesAsync(ct);

        // 3) Run the rules over the user's recent history (deterministic, LLM-free).
        // Shared with GET /api/nudges so "after logging" and "dashboard" can't drift.
        var nudges = await _nudges.EvaluateForUserAsync(userId, capturedAt, ct);

        return Ok(new LogMealResponse(
            meal.Id, true, meal.MealType,
            items.Select(i => new LoggedItem(i.Name, i.PortionGramsEst, i.Confidence,
                i.ProteinG, i.CarbsG, i.FatG, i.FiberG, i.CaloriesEst)).ToList(),
            new MealTotals(meal.ProteinG, meal.CarbsG, meal.FatG, meal.FiberG, meal.CaloriesEst),
            nudges));
    }

    // GET /api/meals?date={optional yyyy-MM-dd}
    // Flat, most-recent-first list of a user's meals, with items included for the
    // frontend's expand-to-detail view. `date` scopes to that single calendar day
    // (UTC); omitting it returns the last 14 days.
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MealHistoryItem>>> GetMeals(
        [FromQuery] string? date, CancellationToken ct)
    {
        var userId = GetUserId();

        DateTime rangeStart, rangeEndExclusive;
        if (!string.IsNullOrWhiteSpace(date))
        {
            if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var day))
                return BadRequest("date must be in yyyy-MM-dd format.");

            rangeStart = day.ToDateTime(TimeOnly.MinValue);
            rangeEndExclusive = rangeStart.AddDays(1);
        }
        else
        {
            rangeEndExclusive = DateTime.UtcNow.AddDays(1);
            rangeStart = DateTime.UtcNow.AddDays(-14);
        }

        var meals = await _db.Meals
            .Where(m => m.UserId == userId && m.CapturedAt >= rangeStart && m.CapturedAt < rangeEndExclusive)
            .Include(m => m.Items)
            .OrderByDescending(m => m.CapturedAt)
            .AsNoTracking()
            .ToListAsync(ct);

        return Ok(meals.Select(ToHistoryItem).ToList());
    }

    // GET /api/meals/by-day?days={int, default 14}
    // Meals grouped by calendar day (UTC), most recent day first, with per-day totals.
    // Feeds the Logs page: day headers with that day's meals listed underneath.
    [HttpGet("by-day")]
    public async Task<ActionResult<IReadOnlyList<MealDayGroup>>> GetMealsByDay(
        [FromQuery] int days = 14, CancellationToken ct = default)
    {
        if (days < 1)
            return BadRequest("days must be at least 1.");

        var userId = GetUserId();
        var rangeStart = DateTime.UtcNow.AddDays(-days);

        var meals = await _db.Meals
            .Where(m => m.UserId == userId && m.CapturedAt >= rangeStart)
            .Include(m => m.Items)
            .OrderByDescending(m => m.CapturedAt)
            .AsNoTracking()
            .ToListAsync(ct);

        var groups = meals
            .GroupBy(m => DateOnly.FromDateTime(m.CapturedAt))
            .OrderByDescending(g => g.Key)
            .Select(g => new MealDayGroup(
                g.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                g.Key.ToDateTime(TimeOnly.MinValue).ToString("dddd, d MMMM yyyy", CultureInfo.InvariantCulture),
                new MealTotals(
                    g.Sum(m => m.ProteinG),
                    g.Sum(m => m.CarbsG),
                    g.Sum(m => m.FatG),
                    g.Sum(m => m.FiberG),
                    g.Sum(m => m.CaloriesEst)),
                g.Select(ToHistoryItem).ToList()))
            .ToList();

        return Ok(groups);
    }

    // DELETE /api/meals/{mealId}
    // FoodItems cascade at the DB level, so deleting the Meal row is enough.
    [HttpDelete("{mealId:guid}")]
    public async Task<IActionResult> DeleteMeal(
        [FromRoute] Guid mealId, CancellationToken ct)
    {
        var userId = GetUserId();

        // Filtering by both id and userId in one query means "wrong owner" and
        // "doesn't exist" are indistinguishable — both just 404.
        var meal = await _db.Meals
            .FirstOrDefaultAsync(m => m.Id == mealId && m.UserId == userId, ct);

        if (meal is null)
            return NotFound();

        _db.Meals.Remove(meal);
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }

    private static MealHistoryItem ToHistoryItem(Meal meal) => new(
        meal.Id,
        meal.CapturedAt,
        meal.MealType,
        meal.PhotoUrl,
        meal.Note,
        new MealTotals(meal.ProteinG, meal.CarbsG, meal.FatG, meal.FiberG, meal.CaloriesEst),
        meal.Items.Select(i => new LoggedItem(
            i.Name, i.PortionGramsEst, i.Confidence, i.ProteinG, i.CarbsG, i.FatG, i.FiberG, i.CaloriesEst)).ToList());
}