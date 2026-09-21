using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace ReservationService.Tests;

public static class TestJwtFactory
{
    private const string Secret = "dev-only-local-secret-key-not-for-production-use-32chars";
    private const string Issuer = "LibraryManagementApi";
    private const string Audience = "LibraryManagementApiUsers";

    public static string CreateToken(Guid userId, string role)
    {
        var claims = new[]
        {
            new Claim("userId", userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(ClaimTypes.Role, role)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
