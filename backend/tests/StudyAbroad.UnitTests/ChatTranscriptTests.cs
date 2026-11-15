using System.Text.Encodings.Web;
using System.Text.Json;
using StudyAbroad.Application.Advisor;
using StudyAbroad.Application.Profile.Flow;

namespace StudyAbroad.UnitTests;

public class ChatTranscriptTests
{
    private static readonly JsonSerializerOptions Readable = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    // Mỗi môn (HK1 + 2×HK2)/3, TB năm = trung bình các môn, làm tròn 2 chữ số khi gửi; môn, năm chưa có điểm thì bỏ
    [Fact]
    public void SendsYearAndSubjectAverages()
    {
        var step2 = new FlowTranscriptDraftDto(
        [
            new FlowAcademicYearDraftDto("Lớp 11", [new FlowSubjectDraftDto("Toán", 8m, 9m), new FlowSubjectDraftDto("Văn", 7m), new FlowSubjectDraftDto("Anh")]),
            new FlowAcademicYearDraftDto("Lớp 12", [new FlowSubjectDraftDto("Sử")]),
        ]);

        Assert.Equal(
            """{"years":[{"grade":"Lớp 11","average":7.83,"subjects":[{"name":"Toán","average":8.67},{"name":"Văn","average":7}]}]}""",
            ChatTranscript.From(step2)!.ToJsonString(Readable));
    }

    // Nhập GPA tổng thì không có học bạ, không ép học sinh nhập điểm từng lớp
    [Fact]
    public void NullWhenGpaIsOverall()
    {
        Assert.Null(ChatTranscript.From(new FlowTranscriptDraftDto(ManualGpa10Scale: 8.5m, GpaMode: "overall")));
        Assert.Null(ChatTranscript.From(null));
    }

    // Dòng "GPA Mục Tiêu" của THCS là mục tiêu: ghi target, không thành môn có điểm
    [Fact]
    public void MiddleSchoolTargetRow_IsTarget()
    {
        var step2 = new FlowTranscriptDraftDto(
        [
            new FlowAcademicYearDraftDto("Lớp 9", [new FlowSubjectDraftDto("GPA Mục Tiêu", 8.5m, 8.5m)]),
        ], GpaMode: "transcript");

        var year = ChatTranscript.From(step2)!["years"]![0]!;

        Assert.Equal(8.5m, year["target"]!.GetValue<decimal>());
        Assert.Null(year["subjects"]);
    }
}
