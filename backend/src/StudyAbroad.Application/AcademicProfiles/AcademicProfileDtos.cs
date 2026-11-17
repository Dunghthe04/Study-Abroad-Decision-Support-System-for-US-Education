using System;
using System.Collections.Generic;

namespace StudyAbroad.Application.AcademicProfiles;

public record TranscriptScoreItemDto(
    string Subject,
    string? RawScore,
    decimal Score,
    decimal? Credits
);

public record TranscriptTermDto(
    string TermName,
    int TermOrder,
    List<TranscriptScoreItemDto> Scores
);

public record AcademicProfileResponse(
    Guid Id,
    Guid UserId,
    string TargetLevel,
    string? CurrentSchool,
    string? EducationSystem,
    int? GraduationYear,
    string? CurrentGrade,
    string GradeScale,
    decimal? OverallGpa,
    string? IntendedMajor,
    decimal? Ielts,
    decimal? Toefl,
    decimal? Duolingo,
    decimal? Sat,
    decimal? Act,
    decimal? Gre,
    decimal? Gmat,
    string? OtherTestsJson,
    List<TranscriptTermDto> Terms,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    // Đã gửi qua luồng 6 bước hoặc có dữ liệu cũ: tài khoản không báo "chưa điền" nữa
    bool IsComplete = false
);
