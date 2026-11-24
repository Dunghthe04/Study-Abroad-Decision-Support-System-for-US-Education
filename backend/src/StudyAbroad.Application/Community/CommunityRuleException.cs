namespace StudyAbroad.Application.Community;

public sealed class CommunityRuleException(string code, int status, string message) : Exception(message)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}
