using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using StudyAbroad.Application.Advisor;

namespace StudyAbroad.UnitTests;

/// <summary>
/// So các hành động của chat trong code với bảng chính sách chung shared/answer_policy.json (mỗi hành động do AI hay do code viết
/// chữ trả về học sinh). Advisor (Python) có test cùng file này (tests/test_answer_policy.py) cho phần loại ý và writer.
/// Thêm hành động mới mà quên khai báo trong bảng, hoặc bảng còn hành động không còn trong code, thì test đỏ.
/// </summary>
public class AnswerPolicyTests
{
    private static readonly JsonElement Policy = Load();
    private static readonly string[] Who = ["code", "claude", "mixed"];

    private static JsonElement Load([CallerFilePath] string thisFile = "")
    {
        // Đi ngược từ file test này lên gốc repo để tìm shared/ (bin có thể nằm ngoài repo khi chạy dotnet test -o)
        var dir = new FileInfo(thisFile).Directory;
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "shared", "answer_policy.json")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return JsonDocument.Parse(File.ReadAllText(Path.Combine(dir!.FullName, "shared", "answer_policy.json"))).RootElement;
    }

    private static HashSet<string> DeclaredActions() =>
        Policy.GetProperty("actions").EnumerateObject().Select(p => p.Name).ToHashSet();

    // Mọi hằng số tên kết thúc bằng "Action" trong các lớp tĩnh của Application.Advisor (ChatRecommendation.Action, ChatSchoolInfo.SearchAction...)
    private static HashSet<string> ActionsInCode() =>
        typeof(ChatService).Assembly.GetTypes()
            .Where(t => t.Namespace == "StudyAbroad.Application.Advisor" && t.IsAbstract && t.IsSealed)
            .SelectMany(t => t.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(f => f.IsLiteral && f.FieldType == typeof(string) && f.Name.EndsWith("Action", StringComparison.Ordinal))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToHashSet();

    [Fact]
    public void EveryActionInTheCode_IsDeclaredInThePolicy()
    {
        Assert.Empty(ActionsInCode().Except(DeclaredActions()));
    }

    [Fact]
    public void EveryDeclaredAction_StillExistsInTheCode()
    {
        Assert.Empty(DeclaredActions().Except(ActionsInCode()));
    }

    [Fact]
    public void ThePolicyKnowsTheActionsThisTestExpects()
    {
        // Chốt cứng để phát hiện test phản xạ trượt (đổi tên hậu tố) mà không ai hay
        Assert.Equal(
            ["explain", "goal_seek", "premium_required", "recommend", "school_compare", "school_fit_compare", "school_info", "school_search", "set_province", "simulate"],
            ActionsInCode().OrderBy(a => a, StringComparer.Ordinal));
    }

    // Lời dự phòng bảng nhắc tới (AdvisorMessages.X, ChatXxx.Y) phải là thành viên có thật, để bảng không nói sai về code
    [Fact]
    public void EveryCodeMemberNamedInAFallback_Exists()
    {
        var assembly = typeof(ChatService).Assembly;
        var named = Policy.GetProperty("actions").EnumerateObject()
            .Where(a => a.Value.TryGetProperty("fallback", out _))
            .SelectMany(a => Regex.Matches(a.Value.GetProperty("fallback").GetString()!, @"(AdvisorMessages|Chat[A-Za-z]+)\.([A-Z][A-Za-z]+)").Select(m => (Action: a.Name, Type: m.Groups[1].Value, Member: m.Groups[2].Value)))
            .ToList();

        Assert.NotEmpty(named);
        foreach (var (action, typeName, member) in named)
        {
            var type = assembly.GetType($"StudyAbroad.Application.Advisor.{typeName}");
            Assert.True(type is not null, $"{action}: không có lớp {typeName}");
            Assert.True(type!.GetMember(member, BindingFlags.Public | BindingFlags.Static).Length > 0, $"{action}: {typeName} không có thành viên {member}");
        }
    }

    [Fact]
    public void EveryAction_SaysWhoWritesAndWhy_AndAClaudeActionAlwaysHasAFallback()
    {
        foreach (var action in Policy.GetProperty("actions").EnumerateObject())
        {
            var who = action.Value.GetProperty("who").GetString();
            Assert.Contains(who, Who);
            Assert.False(string.IsNullOrWhiteSpace(action.Value.GetProperty("why").GetString()), action.Name);
            if (who != "code")
                Assert.False(string.IsNullOrWhiteSpace(action.Value.GetProperty("fallback").GetString()), $"{action.Name} thiếu lời dự phòng");
        }
    }
}
