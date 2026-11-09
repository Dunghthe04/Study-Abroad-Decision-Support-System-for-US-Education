using System;
using System.Linq;
using StudyAbroad.Application.Profile.Academic;
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

    /// <summary>
    /// TB cả năm một môn theo cách tính Việt Nam: HK2 hệ số 2 → (HK1 + 2×HK2) / 3. Chỉ có một kỳ thì lấy kỳ đó,
    /// không có kỳ nào thì null. Không làm tròn ở đây, chỉ làm tròn số cuối. Giống yearAverage bên FE
    /// </summary>
    public static decimal? YearAverage(decimal? semester1, decimal? semester2) => (semester1, semester2) switch
    {
        ({ } s1, { } s2) => (s1 + 2 * s2) / 3,
        ({ } s1, null) => s1,
        (null, { } s2) => s2,
        _ => null,
    };

    /// <summary>Giống FE: trung bình TB cả năm (YearAverage) của các môn, thang 10; null nếu chưa có điểm</summary>
    public static decimal? SubjectAverage10(FlowTranscriptDraftDto? step2)
    {
        var averages = (step2?.AcademicYears ?? [])
            .SelectMany(y => y.Subjects ?? [])
            .Select(s => YearAverage(s.Semester1Score, s.Semester2Score))
            .OfType<decimal>()
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

    /// <summary>
    /// Quy đổi thang 10 sang thang 4 theo bảng WES mặc định (AcademicAnalysisService.GetDefaultWesScaleConfig), giống
    /// toGpa4 bên FE và gpa10_to_4 bên advisor. Chỉ xét cận dưới (MinScore) để số như 8.995 không rơi vào khe giữa
    /// hai khoảng: 8.99 vẫn là 3.7
    /// </summary>
    public static decimal To4(decimal gpa10) =>
        AcademicAnalysisService.GetDefaultWesScaleConfig().Rules
            .OrderByDescending(rule => rule.MinScore)
            .FirstOrDefault(rule => gpa10 >= rule.MinScore)?.Gpa4 ?? 0m;
}
