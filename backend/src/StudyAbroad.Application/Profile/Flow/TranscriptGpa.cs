using System;
using System.Linq;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.Application.Profile.Flow;

/// <summary>
/// GPA bước 2 theo gpaMode (kế hoạch đồng bộ chat–form 3.1), dùng chung cho commit và chat.
/// overall = nhập GPA tổng; transcript = tính từ điểm môn, bỏ qua số GPA client gửi lên.
/// </summary>
public static class TranscriptGpa
{
    /// <summary>Nháp cũ chưa có gpaMode (hoặc mã lạ): có điểm môn thì là học bạ, không thì là GPA tổng</summary>
    public static string Mode(FlowTranscriptDraftDto? step2) =>
        step2?.GpaMode is { } mode && ProfileConstants.GpaModes.All.Contains(mode) ? mode
        : SubjectAverage10(step2) is null ? ProfileConstants.GpaModes.Overall
        : ProfileConstants.GpaModes.Transcript;

    /// <summary>Giống FE: trung bình các môn (mỗi môn = TB HK1, HK2 nếu có), thang 10; null nếu chưa có điểm</summary>
    public static decimal? SubjectAverage10(FlowTranscriptDraftDto? step2)
    {
        var averages = (step2?.AcademicYears ?? [])
            .SelectMany(y => y.Subjects ?? [])
            .Select(s => new[] { s.Semester1Score, s.Semester2Score }.OfType<decimal>().ToList())
            .Where(scores => scores.Count > 0)
            .Select(scores => scores.Average())
            .ToList();
        return averages.Count > 0 ? Math.Round(averages.Average(), 2) : null;
    }

    /// <summary>GPA thang 4 lưu vào hồ sơ. GPA tổng thang 10 thì quy đổi lại, không tin số thang 4 client gửi</summary>
    public static decimal? Gpa4(FlowTranscriptDraftDto? step2)
    {
        if (Mode(step2) == ProfileConstants.GpaModes.Overall)
            return step2?.ManualGpa10Scale is { } gpa10 ? To4(gpa10) : step2?.CalculatedGpa4Scale;
        return SubjectAverage10(step2) is { } average ? To4(average) : null;
    }

    public static decimal To4(decimal gpa10) => Math.Round(gpa10 / 10m * 4m, 2);
}
