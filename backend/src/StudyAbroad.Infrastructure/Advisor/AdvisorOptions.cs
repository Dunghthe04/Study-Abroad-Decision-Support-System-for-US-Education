namespace StudyAbroad.Infrastructure.Advisor;

public class AdvisorOptions
{
    public const string SectionName = "Advisor";

    public string BaseUrl { get; set; } = "http://localhost:8000";
    public string? ApiKey { get; set; }
    public int TimeoutSeconds { get; set; } = 120;
}
