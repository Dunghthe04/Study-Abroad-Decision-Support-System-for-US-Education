using System.Text.Json.Nodes;
using StudyAbroad.Application.Advisor;
using StudyAbroad.Application.Profile.Flow;

namespace StudyAbroad.UnitTests;

public class ChatProfileMapperTests
{
    private const string Activity =
        """{"name":"CLB Robotics","category":"activity","role":"leader","impactLevel":"school","startDate":"09/2024","endDate":null,"isOngoing":true}""";
    private const string Award = """{"name":"HSG Tin thành phố","category":"achievement","impactLevel":"province","year":2025}""";

    private static JsonObject Profile(string json) => JsonNode.Parse(json)!.AsObject();

    private static JsonObject Confirmed(string key, string valueJson) =>
        Profile("{\"" + key + "\":{\"value\":" + valueJson + ",\"status\":\"confirmed\"}}");

    private static ProfileFlowDraftDto ToDraft(JsonObject profile, ProfileFlowDraftDto? draft = null) =>
        ChatProfileMapper.ApplyChatProfile(draft ?? new ProfileFlowDraftDto(), profile, profile.Select(p => p.Key).ToList());

    private static void AssertSame(JsonObject expected, JsonObject actual) =>
        Assert.Equal(expected.ToJsonString(), actual.ToJsonString());

    private static FlowCertificationItemDto Cert(string category, string type, decimal overall, string? date) =>
        new(Guid.NewGuid().ToString(), category, type, Overall: overall, TestDate: date);

    // Vòng tròn: chat ghi vào nháp rồi đọc lại phải ra đúng giá trị cũ, không thì merge_profile ghi đè mỗi lượt
    [Theory]
    [InlineData("current_level", "\"high_school\"")]
    [InlineData("current_grade", "\"Lớp 12\"")]
    [InlineData("target_level", "\"undergraduate\"")]
    [InlineData("major", "\"Computer Science\"")]
    [InlineData("gpa", """{"value":8.5,"scale":10,"source":"overall"}""")]
    [InlineData("gpa", """{"value":3.6,"scale":4,"source":"overall"}""")]
    [InlineData("sat", "1450")]
    [InlineData("act", "32")]
    [InlineData("english", """{"type":"ielts","value":6.5,"date":"2026-05"}""")]
    [InlineData("english", """{"type":"toefl","value":95,"date":null}""")]
    [InlineData("english_plan", """{"planned":true,"date":"2027-06"}""")]
    [InlineData("english_plan", """{"planned":true,"date":null}""")]
    [InlineData("budget", "\"tier_20k_35k\"")]
    [InlineData("scholarship", "\"preferred\"")]
    [InlineData("activities", "[" + Activity + "," + Activity + "]")]
    [InlineData("awards", "[" + Award + "]")]
    [InlineData("preferred_states", """["CA","TX"]""")]
    [InlineData("school_type", "\"public\"")]
    [InlineData("education_system", "\"specialized\"")]
    [InlineData("funding_source", "\"bank_loan\"")]
    [InlineData("intake", "\"Fall 2027\"")]
    public void RoundTrip_EachKey(string key, string valueJson)
    {
        var profile = Confirmed(key, valueJson);

        AssertSame(profile, ChatProfileMapper.ToChatProfile(ToDraft(profile)));
    }

    [Fact]
    public void RoundTrip_FullProfile()
    {
        var profile = Profile($$"""
        {
          "current_level":{"value":"high_school","status":"confirmed"},
          "current_grade":{"value":"Lớp 12","status":"confirmed"},
          "target_level":{"value":"undergraduate","status":"confirmed"},
          "gpa":{"value":{"value":8.5,"scale":10,"source":"overall"},"status":"confirmed"},
          "sat":{"value":1450,"status":"confirmed"},
          "english":{"value":{"type":"ielts","value":7,"date":"2026-05"},"status":"confirmed"},
          "budget":{"value":"tier_35k_50k","status":"confirmed"},
          "scholarship":{"value":"mandatory","status":"confirmed"},
          "activities":{"value":[{{Activity}}],"status":"confirmed"},
          "awards":{"value":[{{Award}}],"status":"confirmed"}
        }
        """);
        AssertSame(profile, ChatProfileMapper.ToChatProfile(ToDraft(profile)));
    }

    // Trạng thái nháp không lưu được thì lấy lại từ profile lượt trước
    [Fact]
    public void NoneAndUnknown_KeptFromPreviousProfile()
    {
        var previous = Profile("""
        {
          "target_level":{"value":"unknown","status":"unknown"},
          "major":{"value":"unknown","status":"unknown"},
          "english":{"value":null,"status":"none"},
          "english_plan":{"value":{"planned":false,"date":null},"status":"confirmed"},
          "scholarship":{"value":null,"status":"declined"},
          "activities":{"value":null,"status":"none"}
        }
        """);

        AssertSame(previous, ChatProfileMapper.ToChatProfile(ToDraft(previous), previous));
    }

    [Fact]
    public void ValueClearedOnForm_NotKeptFromPrevious()
    {
        var previous = Confirmed("budget", "\"tier_20k_35k\"");

        Assert.Empty(ChatProfileMapper.ToChatProfile(new ProfileFlowDraftDto(), previous));
    }

    [Fact]
    public void FormValues_ComeAsConfirmed_AndSaidInB0IsWritten()
    {
        var profile = Profile("""{"budget":{"value":"tier_50k_70k","status":"said_in_B0"}}""");

        var draft = ToDraft(profile);

        Assert.Equal("tier_50k_70k", draft.Step4!.AnnualBudgetTier);
        Assert.Equal("confirmed", ChatProfileMapper.ToChatProfile(draft)["budget"]!["status"]!.GetValue<string>());
    }

    // Nhiều chứng chỉ tiếng Anh: ngày thi mới nhất, rồi điểm cao nhất (ApplyLegacyCertificationColumns)
    [Fact]
    public void ManyEnglishCerts_PicksLatestThenHighest()
    {
        var draft = new ProfileFlowDraftDto(Step3: new FlowCertificationDraftDto(
        [
            Cert("english", "ielts", 8m, "2025-01"),
            Cert("english", "toefl", 90m, "2026-03"),
            Cert("english", "duolingo", 120m, "2026-03"),
            Cert("standardized", "sat", 1500m, "2026-09"),
        ]));

        var english = ChatProfileMapper.ToChatProfile(draft)["english"]!["value"]!;

        Assert.Equal("duolingo", english["type"]!.GetValue<string>());
        Assert.Equal(120m, english["value"]!.GetValue<decimal>());
    }

    [Fact]
    public void ChatEnglish_ReplacesSameType_KeepsOtherCerts()
    {
        var old = new ProfileFlowDraftDto(Step3: new FlowCertificationDraftDto(
        [
            Cert("english", "ielts", 6m, "2025-01") with { Sections = new() { ["reading"] = 6m } },
            Cert("english", "toefl", 80m, "2024-05"),
            Cert("standardized", "sat", 1400m, null),
        ]));

        var certs = ToDraft(Confirmed("english", """{"type":"ielts","value":7,"date":"2026-06"}"""), old).Step3!.Certifications!;

        Assert.Equal(3, certs.Count);
        var ielts = Assert.Single(certs, c => c.Type == "ielts");
        Assert.Equal((7m, "2026-06"), (ielts.Overall!.Value, ielts.TestDate));
        Assert.Equal(old.Step3!.Certifications![0].Id, ielts.Id);
        Assert.Null(ielts.Sections); // điểm thành phần cũ không khớp điểm mới
    }

    [Fact]
    public void EnglishNone_RemovesEnglishCerts_KeepsSat()
    {
        var old = new ProfileFlowDraftDto(Step3: new FlowCertificationDraftDto(
            [Cert("english", "ielts", 6m, null), Cert("standardized", "sat", 1400m, null)]));

        var certs = ToDraft(Profile("""{"english":{"value":null,"status":"none"}}"""), old).Step3!.Certifications!;

        Assert.Equal("sat", Assert.Single(certs).Type);
    }

    [Fact]
    public void Gpa10_WritesBothScales()
    {
        var step2 = ToDraft(Confirmed("gpa", """{"value":8.5,"scale":10,"source":"overall"}""")).Step2!;

        Assert.Equal((8.5m, 3.4m, "overall"), (step2.ManualGpa10Scale!.Value, step2.CalculatedGpa4Scale!.Value, step2.GpaMode));
    }

    // GPA tính từ học bạ: chat đọc ra source = transcript và không được ghi đè
    [Fact]
    public void GpaFromTranscript_IsReadOnlyForChat()
    {
        var old = new ProfileFlowDraftDto(Step2: new FlowTranscriptDraftDto(
        [
            new FlowAcademicYearDraftDto("Lớp 10", [new FlowSubjectDraftDto("Toán", 8m, 9m), new FlowSubjectDraftDto("Văn", 7m)]),
        ]));

        var gpa = ChatProfileMapper.ToChatProfile(old)["gpa"]!["value"]!;
        Assert.Equal("""{"value":7.75,"scale":10,"source":"transcript"}""", gpa.ToJsonString());

        var draft = ToDraft(Confirmed("gpa", """{"value":9,"scale":10,"source":"overall"}"""), old);
        Assert.Same(old.Step2, draft.Step2);
    }

    // gpaMode = transcript nhưng chưa có điểm môn: chat ghi GPA tổng và chuyển sang overall
    [Fact]
    public void TranscriptModeWithoutScores_ChatWritesOverall()
    {
        var old = new ProfileFlowDraftDto(Step2: new FlowTranscriptDraftDto([], GpaMode: "transcript"));

        var step2 = ToDraft(Confirmed("gpa", """{"value":3.6,"scale":4,"source":"overall"}"""), old).Step2!;

        Assert.Equal(("overall", 3.6m), (step2.GpaMode, step2.CalculatedGpa4Scale!.Value));
    }

    // gpaMode = overall thì điểm môn còn sót lại không được tính
    [Fact]
    public void OverallMode_IgnoresLeftoverSubjects()
    {
        var draft = new ProfileFlowDraftDto(Step2: new FlowTranscriptDraftDto(
            [new FlowAcademicYearDraftDto("Lớp 10", [new FlowSubjectDraftDto("Toán", 5m)])],
            CalculatedGpa4Scale: 3.4m, ManualGpa10Scale: 8.5m, GpaMode: "overall"));

        var gpa = ChatProfileMapper.ToChatProfile(draft)["gpa"]!["value"]!;

        Assert.Equal("""{"value":8.5,"scale":10,"source":"overall"}""", gpa.ToJsonString());
    }

    // C2 chỉ ghi khóa chat báo là đã đổi
    [Fact]
    public void OnlyChangedKeysAreWritten()
    {
        var old = new ProfileFlowDraftDto(Step4: new FlowFinancialDraftDto("tier_20k_35k", "optional"));
        var profile = Profile("""
        {
          "budget":{"value":"tier_50k_70k","status":"confirmed"},
          "scholarship":{"value":"mandatory","status":"confirmed"}
        }
        """);

        var draft = ChatProfileMapper.ApplyChatProfile(old, profile, ["scholarship"]);

        Assert.Equal(("tier_20k_35k", "mandatory"), (draft.Step4!.AnnualBudgetTier, draft.Step4.NeedFinancialAid));
    }

    [Fact]
    public void ChatActivities_KeepAwardsAndIds()
    {
        var oldActivity = new FlowActivityDraftDto("a1", "Cũ", "activity", "member", "school", "01/2024", IsOngoing: true, DurationMonths: 10);
        var oldAward = new FlowActivityDraftDto("w1", "Giải cũ", "achievement", ImpactLevel: "national", Year: 2024);
        var old = new ProfileFlowDraftDto(Step5: new FlowActivitiesDraftDto([oldActivity, oldAward]));

        var items = ToDraft(Confirmed("activities", "[" + Activity + "," + Activity + "]"), old).Step5!.Activities!;

        Assert.Equal(3, items.Count);
        Assert.Equal("a1", items[0].Id);                 // giữ id theo vị trí
        Assert.Equal("CLB Robotics", items[0].Name);
        Assert.Null(items[0].DurationMonths);            // .NET tự tính số tháng khi commit
        Assert.NotNull(items[1].Id);
        Assert.Equal(oldAward, items[2]);                // giải thưởng không bị đụng
    }

    [Fact]
    public void UnknownMajorAndTargetLevel_ClearDraft()
    {
        var old = new ProfileFlowDraftDto(Step1: new FlowGoalDraftDto(TargetDegreeLevel: "master", TargetMajor: "Law"));

        var step1 = ToDraft(Profile("""
        {
          "target_level":{"value":"unknown","status":"unknown"},
          "major":{"value":"unknown","status":"unknown"}
        }
        """), old).Step1!;

        Assert.Null(step1.TargetDegreeLevel);
        Assert.Null(step1.TargetMajor);
    }
}
