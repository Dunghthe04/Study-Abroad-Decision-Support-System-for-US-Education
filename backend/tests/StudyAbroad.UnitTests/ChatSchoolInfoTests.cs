using StudyAbroad.Application.Advisor;
using StudyAbroad.Application.Recommendations;

namespace StudyAbroad.UnitTests;

// Trả lời thông tin một trường bằng số thật, do code dựng: hiểu mục được hỏi, nói rõ khi hệ thống chưa có, không đoán.
public class ChatSchoolInfoTests
{
    private static SchoolInfoDto Full() => new(
        "S", "Boston University", "Boston", "MA", "private", "https://bu.example.edu",
        new SchoolCostInfo(59_000m, 21_000m, 4_000m, 2_000m, 86_000m),
        new SchoolEnglishInfo(7m, 100m, null),
        new SchoolTestInfo("optional", 1370, 1530, 31, 35),
        new SchoolAdmissionInfo(0.305m, 0.10m),
        new SchoolAidInfo("need_and_merit", 25_000m, 120),
        new SchoolOutcomeInfo(0.95m, 0.91m),
        [new SchoolDeadline("ED", "2026-11-01"), new SchoolDeadline("RD", "2027-01-05")],
        ["Computer Science", "Economics"],
        [new SchoolRanking("U.S. News 2027 – National Universities", "Tổng quát", 2027, 42, "#42", false),
         new SchoolRanking("U.S. News 2027 – Best Undergraduate Computer Science", "Theo ngành", 2027, 18, "#18", true)],
        "https://source.example.org", new DateOnly(2026, 10, 20),
        new SchoolGpaInfo(3.86m, 0.537m, 0.82m));

    private static SchoolInfoDto Empty() => new(
        "E", "Empty College", null, null, null, null,
        new SchoolCostInfo(null, null, null, null, null), new SchoolEnglishInfo(null, null, null),
        new SchoolTestInfo(null, null, null, null, null), new SchoolAdmissionInfo(null, null),
        new SchoolAidInfo(null, null, null), new SchoolOutcomeInfo(null, null), [], [], [], null, null);

    // ---------------------------------------------------------------- hiểu mục được hỏi

    [Theory]
    [InlineData("Học phí Harvard bao nhiêu?", InfoField.Cost)]
    [InlineData("hoc phi bu bao nhieu", InfoField.Cost)]                       // không dấu
    [InlineData("UCLA có học bổng cho du học sinh không", InfoField.Aid)]
    [InlineData("MIT cần IELTS bao nhiêu", InfoField.English)]
    [InlineData("trường này có yêu cầu TOEFL không", InfoField.English)]
    [InlineData("NYU có cần SAT không", InfoField.Test)]
    [InlineData("tỷ lệ nhận của Stanford là bao nhiêu", InfoField.Acceptance)]
    [InlineData("hạn nộp hồ sơ của Yale khi nào", InfoField.Deadline)]
    [InlineData("tỷ lệ tốt nghiệp của Purdue", InfoField.Outcomes)]
    [InlineData("Duke có những ngành nào", InfoField.Majors)]
    [InlineData("Duke xếp hạng bao nhiêu", InfoField.Ranking)]
    [InlineData("GPA đại học harvard là bao nhiêu", InfoField.Gpa)]
    [InlineData("điểm trung bình của sinh viên Yale", InfoField.Gpa)]
    [InlineData("ranking của Duke", InfoField.Ranking)]
    [InlineData("hạng mấy trong US News", InfoField.Ranking)]
    [InlineData("Rice ở bang nào", InfoField.Location)]
    [InlineData("website của Brown", InfoField.Website)]
    public void FieldsOf_RecognizesTheTopic(string question, InfoField expected)
    {
        Assert.Contains(expected, ChatSchoolInfo.FieldsOf(question));
    }

    [Fact]
    public void FieldsOf_ReadsSeveralTopicsInOrderOfTheFixedList_NotOfTheSentence()
    {
        Assert.Equal([InfoField.Cost, InfoField.Deadline], ChatSchoolInfo.FieldsOf("hạn nộp và học phí của UCLA"));
    }

    [Theory]
    [InlineData("kể về Harvard đi")]
    [InlineData("")]
    [InlineData(null)]
    public void FieldsOf_NothingRecognized_IsEmpty(string? question)
    {
        Assert.Empty(ChatSchoolInfo.FieldsOf(question));
    }

    [Theory]
    [InlineData("ký túc xá của BU thế nào", true)]
    [InlineData("sinh viên Harvard ra trường lương bao nhiêu", true)]
    [InlineData("MIT có câu lạc bộ nào không", true)]
    [InlineData("xếp hạng của MIT", false)]          // xếp hạng có trong dữ liệu, không phải mục chưa lưu
    [InlineData("học phí Harvard bao nhiêu", false)]
    public void AsksUnstored_DetectsWhatTheTableDoesNotHold(string question, bool expected)
    {
        Assert.Equal(expected, ChatSchoolInfo.AsksUnstored(question));
    }

    // ---------------------------------------------------------------- câu trả lời

    [Fact]
    public void Answer_Cost_SaysEachPartAndTheTotalInDollars()
    {
        var answer = ChatSchoolInfo.Answer(Full(), [InfoField.Cost], false);

        Assert.True(answer.Complete);
        Assert.Contains("học phí 59.000 USD, sinh hoạt 21.000 USD, phí 4.000 USD, chi phí khác 2.000 USD, tổng khoảng 86.000 USD", answer.Text);
        Assert.StartsWith("Boston University:", answer.Text);
        Assert.Contains("kiểm tra lại trên trang chính thức", answer.Text);   // chi phí và hạn nộp có thể đổi
    }

    [Fact]
    public void Answer_Cost_DoesNotShowAZeroFeeLineButKeepsTheTotal()
    {
        var school = Full() with { Cost = new SchoolCostInfo(44_908m, 14_828m, 0m, 5_930m, 65_666m) };

        var text = ChatSchoolInfo.Answer(school, [InfoField.Cost], false).Text;

        Assert.DoesNotContain("phí 0", text);
        Assert.Contains("học phí 44.908 USD, sinh hoạt 14.828 USD, chi phí khác 5.930 USD, tổng khoảng 65.666 USD", text);
    }

    [Fact]
    public void Answer_Aid_NamesTheKindAndSaysTheAverageIsOfRecipientsNotAPromise()
    {
        var text = ChatSchoolInfo.Answer(Full(), [InfoField.Aid], false).Text;

        Assert.Contains("xét theo cả nhu cầu tài chính và thành tích", text);
        Assert.Contains("trung bình khoảng 25.000 USD mỗi năm (120 người)", text);
        Assert.Contains("không phải mức bạn sẽ nhận", text);
    }

    [Fact]
    public void Answer_Aid_WhenThePolicyIsNone_SaysThereIsNoAidAndNoAverage()
    {
        var school = Full() with { Aid = new SchoolAidInfo("none", 15_000m, 5) };

        var text = ChatSchoolInfo.Answer(school, [InfoField.Aid], false).Text;

        Assert.Contains("không có học bổng cho du học sinh", text);
        Assert.DoesNotContain("15.000", text);       // tin chính sách, không nói số trung bình mâu thuẫn
    }

    [Fact]
    public void Answer_English_ListsOnlyThePublishedTestsAndSaysOneIsEnough()
    {
        var text = ChatSchoolInfo.Answer(Full(), [InfoField.English], false).Text;

        Assert.Contains("Tiếng Anh tối thiểu: IELTS 7 hoặc TOEFL 100 (một bài là đủ).", text);
    }

    [Fact]
    public void Answer_Deadlines_AreInDayMonthYear()
    {
        var text = ChatSchoolInfo.Answer(Full(), [InfoField.Deadline], false).Text;

        Assert.Contains("ED 01/11/2026, RD 05/01/2027", text);
    }

    [Fact]
    public void Answer_Gpa_SaysTheAverageAndHowManyAdmittedStudentsHaveAHighGpa()
    {
        var text = ChatSchoolInfo.Answer(Full(), [InfoField.Gpa], false).Text;

        Assert.Contains("GPA trung bình của sinh viên trúng tuyển khoảng 3,86 (thang 4)", text);
        Assert.Contains("Phân bố: 53,7% đạt 4.0, 28,3% từ 3,75 đến 3,99, 18% dưới 3,75.", text);   // ba nhóm tách rời, cộng lại 100%
    }

    [Fact]
    public void Answer_Gpa_WithoutData_SaysSo()
    {
        Assert.Contains("chưa có số liệu GPA", ChatSchoolInfo.Answer(Empty(), [InfoField.Gpa], false).Text);
    }

    [Fact]
    public void Answer_Acceptance_UsesCommasForDecimals()
    {
        var text = ChatSchoolInfo.Answer(Full(), [InfoField.Acceptance], false).Text;

        Assert.Contains("khoảng 30,5% hồ sơ", text);
        Assert.Contains("riêng du học sinh khoảng 10%", text);
    }

    [Fact]
    public void Answer_Test_SaysThePolicyAndTheRange()
    {
        var text = ChatSchoolInfo.Answer(Full(), [InfoField.Test], false).Text;

        Assert.Contains("test-optional", text);
        Assert.Contains("SAT 1370–1530, ACT 31–35", text);
    }

    [Fact]
    public void Answer_Majors_ShowsTenAtMostAndCountsTheRest()
    {
        var school = Full() with { Majors = Enumerable.Range(1, 14).Select(i => $"Ngành {i}").ToList() };

        var text = ChatSchoolInfo.Answer(school, [InfoField.Majors], false).Text;

        Assert.Contains("Ngành 10", text);
        Assert.DoesNotContain("Ngành 11", text);
        Assert.Contains("và 4 ngành khác", text);
    }

    [Fact]
    public void Answer_Ranking_ListsTheGeneralListFirstWithTheRankAndTheListName()
    {
        var text = ChatSchoolInfo.Answer(Full(), [InfoField.Ranking], false).Text;

        Assert.Contains("Xếp hạng: #42 trong U.S. News 2027 – National Universities; #18 (đồng hạng) trong U.S. News 2027 – Best Undergraduate Computer Science.", text);
    }

    [Fact]
    public void Answer_Ranking_ShowsFourListsAtMostAndCountsTheRest()
    {
        var many = Enumerable.Range(1, 6).Select(i => new SchoolRanking($"Bảng {i}", "Khu vực", 2027, i, $"#{i}", false)).ToList();

        var text = ChatSchoolInfo.Answer(Full() with { Rankings = many }, [InfoField.Ranking], false).Text;

        Assert.Contains("#4 trong Bảng 4", text);
        Assert.DoesNotContain("Bảng 5", text);
        Assert.Contains("và 2 bảng khác", text);
    }

    [Fact]
    public void Answer_EndsWithTheSourceAndTheDate()
    {
        var text = ChatSchoolInfo.Answer(Full(), [InfoField.Acceptance], false).Text;

        Assert.EndsWith("Số liệu: nguồn https://source.example.org, cập nhật ngày 20/10/2026.", text);
    }

    // ---------------------------------------------------------------- hệ thống chưa có

    [Fact]
    public void Answer_WhenTheSchoolHasNoDataForTheTopic_SaysSoInsteadOfGuessing()
    {
        var text = ChatSchoolInfo.Answer(Empty(), [InfoField.Cost, InfoField.Aid, InfoField.English, InfoField.Deadline, InfoField.Acceptance, InfoField.Ranking], false).Text;

        Assert.Contains("chưa có số liệu học phí", text);
        Assert.Contains("chưa có số liệu học bổng", text);
        Assert.Contains("chưa có mức tối thiểu", text);
        Assert.Contains("Hạn nộp: hệ thống chưa có", text);
        Assert.Contains("Tỷ lệ nhận: hệ thống chưa có", text);
        Assert.Contains("Xếp hạng: hệ thống chưa có", text);
    }

    [Fact]
    public void Answer_OnlyAnUnstoredTopic_IsNotCompleteAndPointsToTheWebsite()
    {
        var answer = ChatSchoolInfo.Answer(Full(), [], unstoredAsked: true);

        Assert.False(answer.Complete);           // để nhánh gọi thử kho kiến thức
        Assert.Contains("chưa có thông tin này của Boston University", answer.Text);
        Assert.Contains("https://bu.example.edu", answer.Text);
    }

    [Fact]
    public void Answer_AnUnstoredTopicBesideAKnownOne_AnswersTheKnownAndAdmitsTheRest()
    {
        var answer = ChatSchoolInfo.Answer(Full(), [InfoField.Cost], unstoredAsked: true);

        Assert.True(answer.Complete);
        Assert.Contains("học phí 59.000 USD", answer.Text);
        Assert.Contains("ký túc xá, đời sống) hệ thống chưa lưu", answer.Text);
    }

    [Fact]
    public void Answer_NoTopicRecognized_GivesAShortSummary()
    {
        var answer = ChatSchoolInfo.Answer(Full(), [], false);

        Assert.True(answer.Complete);
        Assert.StartsWith("Tóm tắt về Boston University:", answer.Text);
        Assert.Contains("Ở Boston, MA, trường tư thục", answer.Text);
        Assert.Contains("Chi phí mỗi năm", answer.Text);
        Assert.Contains("Trường nhận khoảng 30,5% hồ sơ", answer.Text);
        Assert.Contains("Tiếng Anh tối thiểu", answer.Text);
        Assert.Contains("Học bổng cho du học sinh", answer.Text);
    }

    // ---------------------------------------------------------------- so sánh từ hai trường

    private static SchoolInfoDto Cheaper() => Full() with
    {
        Code = "C",
        Name = "State University",
        Cost = new SchoolCostInfo(20_000m, 12_000m, 1_000m, 1_000m, 34_000m),
        Admission = new SchoolAdmissionInfo(0.65m, 0.4m),
    };

    [Fact]
    public void Compare_ListsEachFieldWithEverySchoolsNumberAndSaysWhoIsCheaper()
    {
        var text = ChatSchoolInfo.Compare([Full(), Cheaper()], [InfoField.Cost, InfoField.Acceptance]);

        Assert.StartsWith("So sánh Boston University và State University:", text);
        Assert.Contains("- Chi phí mỗi năm: Boston University 86.000 USD · State University 34.000 USD", text);
        Assert.Contains("State University rẻ hơn Boston University khoảng 52.000 USD mỗi năm.", text);
        Assert.Contains("- Tỷ lệ nhận: Boston University 30,5% (du học sinh 10%) · State University 65% (du học sinh 40%)", text);
        Assert.Contains("kiểm tra lại trên trang chính thức", text);   // có chi phí nên có lời nhắc
    }

    [Fact]
    public void Compare_DoesNotJudgeWhichSchoolIsBetterOnOtherFields()
    {
        var text = ChatSchoolInfo.Compare([Full(), Cheaper()], [InfoField.Acceptance]);

        Assert.DoesNotContain("rẻ hơn", text);
        Assert.DoesNotContain("tốt hơn", text);
        Assert.DoesNotContain("kiểm tra lại", text);   // không có chi phí hay hạn nộp nên không cần lời nhắc
    }

    [Fact]
    public void Compare_WithoutFields_UsesTheDefaultSet()
    {
        var text = ChatSchoolInfo.Compare([Full(), Cheaper()], []);

        foreach (var label in new[] { "Chi phí mỗi năm", "Học bổng cho du học sinh", "Tiếng Anh tối thiểu", "Tỷ lệ nhận", "Xếp hạng" })
            Assert.Contains($"- {label}:", text);
    }

    [Fact]
    public void Compare_WhenOneSchoolHasNoData_SaysSoAndDoesNotInventAGap()
    {
        var text = ChatSchoolInfo.Compare([Full(), Empty()], [InfoField.Cost]);

        Assert.Contains("Empty College chưa có số liệu", text);
        Assert.DoesNotContain("rẻ hơn", text);       // chỉ một trường có chi phí: không có gì để so
    }

    [Fact]
    public void Compare_ThreeSchools_NamesTheCheapestAndTheMostExpensive()
    {
        var third = Full() with { Code = "T", Name = "Tech", Cost = new SchoolCostInfo(null, null, null, null, 60_000m) };

        var text = ChatSchoolInfo.Compare([Full(), Cheaper(), third], [InfoField.Cost]);

        Assert.Contains("State University rẻ hơn Boston University khoảng 52.000 USD mỗi năm.", text);
    }

    [Fact]
    public void CompareRows_AreTheSingleSourceOfTheTextTheCardAndTheFactsForGemini()
    {
        var schools = new[] { Full(), Cheaper() };

        var rows = ChatSchoolInfo.CompareRows(schools, [InfoField.Cost, InfoField.Acceptance]);

        var cost = rows[0];
        Assert.Equal(("cost", "Chi phí mỗi năm"), (cost.Field, cost.Label));
        Assert.Equal(["86.000 USD", "34.000 USD"], cost.Values);
        Assert.Contains("State University rẻ hơn Boston University", cost.Gap);
        Assert.Null(rows[1].Gap);
        // chữ và số liệu gửi Gemini nói cùng điều với các hàng
        Assert.Contains("Boston University 86.000 USD · State University 34.000 USD", ChatSchoolInfo.Compare(schools, [InfoField.Cost, InfoField.Acceptance]));
        Assert.Contains(ChatSchoolInfo.CompareFacts(schools, [InfoField.Cost]), f => f.Contains("rẻ hơn Boston University"));
    }

    [Fact]
    public void WidgetOfCompare_CarriesTheRowsTheAiNoteAndWhatTheCardCovers()
    {
        var schools = new[] { Full(), Cheaper() };

        var widget = ChatSchoolInfo.WidgetOfCompare(schools, [InfoField.Cost], "nhận xét", "toàn bộ chữ");

        Assert.Equal("school_compare", widget.Type);
        var data = widget.Data!.Value;
        Assert.Equal(["Boston University", "State University"], data.GetProperty("schools").EnumerateArray().Select(s => s.GetProperty("name").GetString()));
        var row = data.GetProperty("rows")[0];
        Assert.Equal("cost", row.GetProperty("field").GetString());
        Assert.Equal(["86.000 USD", "34.000 USD"], row.GetProperty("values").EnumerateArray().Select(v => v.GetString()));
        Assert.Equal("nhận xét", data.GetProperty("aiNote").GetString());
        Assert.Equal("toàn bộ chữ", data.GetProperty("coveredText").GetString());
        Assert.True(data.GetProperty("verify").GetBoolean());            // có hàng chi phí nên nhắc kiểm tra lại
    }

    [Fact]
    public void WidgetOfCompare_WithoutANote_HasNoAiNoteAndNoVerifyWhenThereIsNoCostOrDeadline()
    {
        var widget = ChatSchoolInfo.WidgetOfCompare([Full(), Cheaper()], [InfoField.Acceptance], null, "chữ");

        var data = widget.Data!.Value;
        Assert.False(data.TryGetProperty("aiNote", out _));
        Assert.False(data.GetProperty("verify").GetBoolean());
    }

    [Fact]
    public void ParseCompare_ReadsTheSchoolsAndFields_AndNeedsAtLeastTwo()
    {
        var ok = System.Text.Json.JsonDocument.Parse("""{"question":"q","schools":["BU","NYU"],"fields":["chi_phi"],"kb_answer":"tạm"}""").RootElement;
        var one = System.Text.Json.JsonDocument.Parse("""{"schools":["BU"]}""").RootElement;

        var query = ChatSchoolInfo.ParseCompare(ok)!;

        Assert.Equal(["BU", "NYU"], query.Schools);
        Assert.Equal(["chi_phi"], query.Fields);
        Assert.Equal("tạm", query.KbAnswer);
        Assert.Null(ChatSchoolInfo.ParseCompare(one));
        Assert.Null(ChatSchoolInfo.ParseCompare(null));
    }

    // ---------------------------------------------------------------- mục do model trả (SLM đã đọc câu)

    [Fact]
    public void FieldsFromModel_MapsTheCodesInTheFixedOrderAndDropsUnknownOnes()
    {
        var fields = ChatSchoolInfo.FieldsFromModel(["xep_hang", "chi_phi", "khac", "ma_la", "gpa"]);

        Assert.Equal([InfoField.Cost, InfoField.Gpa, InfoField.Ranking], fields);
    }

    [Fact]
    public void ModelSaysUnstored_OnlyForTheKhacCode()
    {
        Assert.True(ChatSchoolInfo.ModelSaysUnstored(["chi_phi", "khac"]));
        Assert.False(ChatSchoolInfo.ModelSaysUnstored(["chi_phi"]));
    }

    [Fact]
    public void WidgetOf_CarriesTheNumbersAndTheFieldsToShow()
    {
        var widget = ChatSchoolInfo.WidgetOf(Full(), [InfoField.Cost, InfoField.Ranking]);

        Assert.Equal("school_info", widget.Type);
        var data = widget.Data!.Value;
        Assert.Equal("Boston University", data.GetProperty("info").GetProperty("name").GetString());
        Assert.Equal(86_000m, data.GetProperty("info").GetProperty("cost").GetProperty("total").GetDecimal());
        Assert.Equal(["cost", "ranking"], data.GetProperty("fields").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(42, data.GetProperty("info").GetProperty("rankings")[0].GetProperty("rank").GetInt32());
    }

    [Fact]
    public void WidgetOf_TellsTheUiWhichTextTheCardAlreadyCovers()
    {
        var withText = ChatSchoolInfo.WidgetOf(Full(), [InfoField.Cost], "câu trả lời");
        var without = ChatSchoolInfo.WidgetOf(Full(), [InfoField.Cost]);

        Assert.Equal("câu trả lời", withText.Data!.Value.GetProperty("coveredText").GetString());
        Assert.False(without.Data!.Value.TryGetProperty("coveredText", out _));
    }

    [Fact]
    public void WidgetOf_WithoutFields_ShowsTheSummaryFields()
    {
        var widget = ChatSchoolInfo.WidgetOf(Full(), []);

        Assert.Equal(
            ["location", "cost", "acceptance", "english", "aid"],
            widget.Data!.Value.GetProperty("fields").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public void FieldButtons_EachAsksAFullQuestionAboutTheSameSchool()
    {
        var buttons = ChatSchoolInfo.FieldButtons("UCLA");

        Assert.Equal(6, buttons.Count);
        Assert.All(buttons, b => Assert.Contains("UCLA", b.Value));
        Assert.Equal([InfoField.Cost], ChatSchoolInfo.FieldsOf(buttons[0].Value));
        Assert.Equal([InfoField.Aid], ChatSchoolInfo.FieldsOf(buttons[1].Value));
        Assert.Contains(InfoField.English, ChatSchoolInfo.FieldsOf(buttons[2].Value));
        Assert.Contains(InfoField.Deadline, ChatSchoolInfo.FieldsOf(buttons[3].Value));
    }
}
