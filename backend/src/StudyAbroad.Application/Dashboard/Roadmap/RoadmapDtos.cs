namespace StudyAbroad.Application.Dashboard.Roadmap;

/// <summary>
/// Lộ trình theo trường mục tiêu (docs/KE_HOACH_LO_TRINH_THEO_TRUONG_MUC_TIEU.md mục 5).
/// Mode = "target" khi có trường mục tiêu, "average" khi chưa có (so với TB các trường, mốc ước chừng).
/// Hai thước đo tách riêng: mức sẵn sàng (Schools/Summary) và tiến độ nhiệm vụ (Progress).
/// </summary>
public record RoadmapDto(
    string Mode,
    string Intake,
    bool IntakeIsEstimate,
    IReadOnlyList<RoadmapSchoolDto> Schools,
    string Summary,
    IReadOnlyList<RoadmapMilestoneDto> Milestones,
    IReadOnlyList<RoadmapTaskDto> Tasks,
    RoadmapProgressDto Progress,
    bool ProvinceMissing);

/// <summary>Mức sẵn sàng với một trường; UniversityId null là dòng "TB các trường" ở chế độ average.</summary>
public record RoadmapSchoolDto(
    Guid? UniversityId,
    string Name,
    string? Category,
    IReadOnlyList<RoadmapCriterionDto> Criteria,
    int MetCount,
    int KnownCount,
    IReadOnlyList<RoadmapScholarshipDto> Scholarships);

/// <summary>Key: gpa | english | sat | budget. Status: met | near | below | unknown.</summary>
public record RoadmapCriterionDto(string Key, string? Type, decimal? Mine, decimal? Mark, string Status);

public record RoadmapScholarshipDto(Guid Id, string Name, DateOnly? Deadline, bool Eligible);

/// <summary>
/// Kind: deadline | scholarship | english_test | sat_test | visa | intake.
/// IsEstimate = true thì giao diện vẽ nét đứt kèm chữ "tham khảo".
/// </summary>
public record RoadmapMilestoneDto(
    DateOnly Date,
    string Kind,
    string Label,
    string? Round,
    Guid? UniversityId,
    bool IsEstimate);

/// <summary>
/// Status: done | todo | overdue. Checkable = false là nhiệm vụ không có dữ liệu để tự tích
/// (bài luận, visa, nộp học bổng): chỉ hiện như mốc, không tính vào Progress.
/// </summary>
public record RoadmapTaskDto(
    string Id,
    string Label,
    DateOnly? Due,
    string Status,
    bool Checkable,
    IReadOnlyList<Guid> SchoolIds,
    IReadOnlyList<string> Services,
    IReadOnlyList<RoadmapCenterDto> Centers);

/// <summary>Trung tâm gợi ý cho một nhiệm vụ (bước A3). Rating = 0 khi chưa có đánh giá.</summary>
public record RoadmapCenterDto(
    Guid Id,
    string Name,
    string? City,
    decimal Rating,
    int ReviewCount,
    bool Verified,
    string? FeeText,
    bool SameProvince);

public record RoadmapProgressDto(int Done, int Total);
