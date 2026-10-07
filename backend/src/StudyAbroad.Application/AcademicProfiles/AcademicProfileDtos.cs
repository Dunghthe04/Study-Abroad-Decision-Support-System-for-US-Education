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

public record SaveAcademicProfileRequest(
    string TargetLevel,
    string? CurrentSchool,
    string? EducationSystem,
    int? GraduationYear,
    string? CurrentGrade,
    string GradeScale,
    string? IntendedMajor,
    decimal? Ielts,
    decimal? Toefl,
    decimal? Duolingo,
    decimal? Sat,
    decimal? Act,
    List<TranscriptTermDto>? Terms
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
    List<TranscriptTermDto> Terms,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);
