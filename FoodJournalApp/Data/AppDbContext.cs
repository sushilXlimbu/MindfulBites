
using FoodJournalApp.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FoodJournalApp.Data;

// Identity owns AspNetUsers/AspNetRoles/etc. (via IdentityDbContext); the meal-journal
// tables below are ours. Guid keys throughout, so Meal.UserId needs no conversion.
public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Meal> Meals => Set<Meal>();
    public DbSet<FoodItem> FoodItems => Set<FoodItem>();
    public DbSet<DailySummary> DailySummaries => Set<DailySummary>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b); // must run first: sets up the Identity tables

        b.Entity<Meal>()
            .HasMany(m => m.Items)
            .WithOne(i => i.Meal!)
            .HasForeignKey(i => i.MealId)
            .OnDelete(DeleteBehavior.Cascade);

        b.Entity<Meal>()
            .HasOne(m => m.User)
            .WithMany(u => u.Meals)
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Fast lookups for the rules engine: "this user's meals in the last N days".
        b.Entity<Meal>().HasIndex(m => new { m.UserId, m.CapturedAt });

        b.Entity<DailySummary>().HasIndex(d => new { d.UserId, d.Date }).IsUnique();
    }
}