using System.Text.Json.Serialization;

namespace StudyAbroad.Application.Advisor;

public record EcActivityDto(string Text, int Scope, int Months, string? Role);

public record EcAwardDto(string Text, int Level);

public record EcRequestDto(IReadOnlyList<EcActivityDto> Act, IReadOnlyList<EcAwardDto> Awards);

public record EcActivityResultDto(double Role, int Scope, double Q);

public record EcResponseDto(
    IReadOnlyList<EcActivityResultDto> Act,
    [property: JsonPropertyName("tong_hd")] double TongHd,
    double Bonus,
    int Tier,
    [property: JsonPropertyName("skill_version")] string SkillVersion,
    string Model,
    [property: JsonPropertyName("ai_used")] bool AiUsed);

public record ScoreStudentDto(double? Gpa, int? Sat, double? Budget, int Tier);

public record ScoreSchoolDto(
    string C,
    double? Acc,
    double? Iacc,
    IReadOnlyList<int>? Sat,
    double? Subm,
    string? Satp,
    IReadOnlyList<double>? Gpa,
    double? Avg,
    [property: JsonPropertyName("L")] IReadOnlyList<int> L,
    double? Coa,
    double? Aid,
    double? Q);

public record ScoreRequestDto(ScoreStudentDto Student, IReadOnlyList<ScoreSchoolDto> Schools);

public record ScoreItemDto(
    string C,
    IReadOnlyList<double> W,
    double? Pg,
    string Conf,
    double? Ps,
    [property: JsonPropertyName("A")] double? A,
    double Adj,
    [property: JsonPropertyName("S")] double? S,
    string G,
    double? R,
    string G2,
    [property: JsonPropertyName("F")] double? F,
    [property: JsonPropertyName("H")] double? H,
    [property: JsonPropertyName("Q")] double? Q,
    double? Gap);

public record ScoreResponseDto(
    [property: JsonPropertyName("skill_version")] string SkillVersion,
    string Model,
    IReadOnlyList<ScoreItemDto> Items,
    IReadOnlyList<string> Failed);

public record PickSchoolDto(
    string C,
    string G2,
    string Blk,
    [property: JsonPropertyName("F")] double? F,
    [property: JsonPropertyName("H")] double? H,
    [property: JsonPropertyName("Q")] double? Q,
    double? Gap,
    double? Pg,
    double? Ps,
    double? Adj,
    double? R);

public record PickRequestDto(
    IReadOnlyList<int> Pref,
    [property: JsonPropertyName("needAid")] string NeedAid,
    IReadOnlyList<PickSchoolDto> Items);

public record PickEntryDto(
    string C,
    [property: JsonPropertyName("X")] double? X,
    IReadOnlyList<string> Why);

public record PickResponseDto(
    [property: JsonPropertyName("skill_version")] string SkillVersion,
    string Model,
    string Status,
    IReadOnlyList<PickEntryDto> Reach,
    IReadOnlyList<PickEntryDto> Match,
    IReadOnlyList<PickEntryDto> Safety,
    IReadOnlyList<PickEntryDto> Aid,
    IReadOnlyList<PickEntryDto> Other);