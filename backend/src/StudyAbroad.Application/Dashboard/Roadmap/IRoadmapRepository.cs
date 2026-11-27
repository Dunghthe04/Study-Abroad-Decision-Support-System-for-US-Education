namespace StudyAbroad.Application.Dashboard.Roadmap;

/// <summary>Các cột của UniversityOffering mà lộ trình cần để so sánh và lấy hạn nộp.</summary>
public record RoadmapOfferingRow(
    Guid UniversityId,
    decimal? AvgGpa4,
    decimal? MinIelts,
    decimal? MinToefl,
    decimal? MinDuolingo,
    int? Sat25,
    int? Sat75,
    string? SatPolicy,
    string? DeadlinesJson);

/// <summary>Học bổng của một trường, đã lọc IsActive và dành cho du học sinh / người Việt.</summary>
public record RoadmapScholarshipRow(
    Guid Id,
    Guid UniversityId,
    string Name,
    decimal? MinGpa4,
    decimal? MinIelts,
    decimal? MinToefl,
    decimal? MinDuolingo,
    int? MinSat,
    DateOnly? Deadline);

/// <summary>Đọc số liệu trường cho lộ trình (chỉ đọc).</summary>
public interface IRoadmapRepository
{
    Task<IReadOnlyDictionary<Guid, RoadmapOfferingRow>> GetOfferingsAsync(
        IReadOnlyCollection<Guid> universityIds, string studyLevel, CancellationToken ct = default);

    Task<IReadOnlyList<RoadmapScholarshipRow>> GetScholarshipsAsync(
        IReadOnlyCollection<Guid> universityIds, string studyLevel, CancellationToken ct = default);
}
