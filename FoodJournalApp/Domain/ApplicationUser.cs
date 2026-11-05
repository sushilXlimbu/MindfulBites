
using Microsoft.AspNetCore.Identity;

namespace FoodJournalApp.Domain;

// Identity's user record. Keyed by Guid (not the string default) so it lines up with
// Meal.UserId and every other UserId column without converting existing meal data.
// Extend with app-specific profile fields here as they're needed.
public class ApplicationUser : IdentityUser<Guid>
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Meal> Meals { get; set; } = new();
}
