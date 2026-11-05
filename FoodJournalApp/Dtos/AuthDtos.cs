
namespace FoodJournalApp.Dtos;

public record RegisterRequest(string Email, string Password);

public record LoginRequest(string Email, string Password);

public record AuthResponse(Guid UserId, string Email, string Token, DateTime ExpiresAtUtc);
