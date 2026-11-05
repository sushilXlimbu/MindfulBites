
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FoodJournalApp.Domain;
using Microsoft.IdentityModel.Tokens;

namespace FoodJournalApp.Security;

// Issues signed JWTs for authenticated users. The only place that knows the signing
// key, claim shape, and expiry policy — everything else just calls CreateToken.
public class JwtTokenService
{
    private readonly SymmetricSecurityKey _signingKey;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _expiryMinutes;

    public JwtTokenService(string signingKey, string issuer, string audience, int expiryMinutes)
    {
        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        _issuer = issuer;
        _audience = audience;
        _expiryMinutes = expiryMinutes;
    }

    public (string Token, DateTime ExpiresAtUtc) CreateToken(ApplicationUser user)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? ""),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var expiresAtUtc = DateTime.UtcNow.AddMinutes(_expiryMinutes);
        var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expiresAtUtc);
    }
}
