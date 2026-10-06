namespace StudyAbroad.Infrastructure.Auth;

/// <summary>[USAS-362] Cấu hình SecretKey, Issuer, Audience và thời hạn sống của JWT Token.</summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";
    public string SecretKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = "USAS";
    public string Audience { get; set; } = "USAS-Clients";
    public int ExpiryDays { get; set; } = 7;
}
