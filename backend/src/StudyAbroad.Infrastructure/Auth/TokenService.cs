using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using StudyAbroad.Application.Auth;
using StudyAbroad.Domain.Entities;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StudyAbroad.Infrastructure.Auth;

/// <summary>
/// [USAS-362] Triển khai dịch vụ tạo JWT Token và băm Token theo chuẩn SHA-256.
/// </summary>
public class TokenService(IOptions<JwtOptions> options) : ITokenService
{
    /// <summary>Tên claim "người đang làm thay" theo RFC 8693.</summary>
    public const string ActorClaim = "act";

    private readonly JwtOptions _jwt = options.Value;

    public (string Token, DateTime ExpiresAt) GenerateToken(User user) => Create(user, null);

    public (string Token, DateTime ExpiresAt) GenerateToken(User user, Guid actingParentId) => Create(user, actingParentId);

    private (string Token, DateTime ExpiresAt) Create(User user, Guid? actingParentId)
    {
        var expiresAt = DateTime.UtcNow.AddMinutes(_jwt.AccessTokenMinutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Role, user.Role),
            new("status", user.Status)
        };
        if (actingParentId is { } parentId)
            claims.Add(new Claim(ActorClaim, JsonSerializer.Serialize(new { sub = parentId.ToString() }), JsonClaimValueTypes.Json));

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAt,
            Issuer = _jwt.Issuer,
            Audience = _jwt.Audience,
            SigningCredentials = creds
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return (tokenHandler.WriteToken(token), expiresAt);
    }

    public string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
