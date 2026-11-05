using System.Security.Claims;
using FoodJournalApp.Dtos;
using FoodJournalApp.Rules;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FoodJournalApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NudgesController : ControllerBase
{
    private readonly NudgeEvaluator _nudges;

    public NudgesController(NudgeEvaluator nudges) => _nudges = nudges;

    // Same pattern as MealsController: "sub" is remapped to NameIdentifier by default.
    private Guid GetUserId() =>
        Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    // GET /api/nudges
    // The user's currently active nudges, for the dashboard to show on any load.
    // Read-only: evaluates the rules over recent meals and returns; never writes.
    // No nudges firing -> 200 with [].
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NudgeDto>>> GetActiveNudges(CancellationToken ct)
    {
        var nudges = await _nudges.EvaluateForUserAsync(GetUserId(), DateTime.UtcNow, ct);
        return Ok(nudges.Select(NudgeDto.From).ToList());
    }
}
