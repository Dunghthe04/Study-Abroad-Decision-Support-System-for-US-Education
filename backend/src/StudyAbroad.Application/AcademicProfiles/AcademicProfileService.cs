using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StudyAbroad.Application.AcademicProfiles;

public class AcademicProfileService(
    IAcademicProfileRepository repository) : IAcademicProfileService
{
    public async Task<AcademicProfileResult> GetAcademicProfileAsync(Guid userId, CancellationToken ct = default)
    {
        var profile = await repository.GetByUserIdAsync(userId, ct);
        if (profile == null)
        {
            var initialEmptyProfile = new AcademicProfileResponse(
                Id: Guid.Empty,
                UserId: userId,
                TargetLevel: StudyLevels.Undergraduate,
                CurrentSchool: null,
                EducationSystem: EducationSystems.Standard,
                GraduationYear: null,
                CurrentGrade: "Lớp 11",
                GradeScale: GradeScales.Scale10,
                OverallGpa: null,
                IntendedMajor: null,
                Ielts: null,
                Toefl: null,
                Duolingo: null,
                Sat: null,
                Act: null,
                Gre: null,
                Gmat: null,
                OtherTestsJson: null,
                Terms: [],
                CreatedAt: DateTime.UtcNow,
                UpdatedAt: null
            );
            return AcademicProfileResult.Ok(initialEmptyProfile, "Khởi tạo biểu mẫu hồ sơ học thuật mới.");
        }

        var scores = await repository.GetTranscriptScoresAsync(profile.Id, ct);
        var terms = scores
            .GroupBy(s => new { s.TermOrder, s.TermName })
            .OrderBy(g => g.Key.TermOrder)
            .Select(g => new TranscriptTermDto(
                g.Key.TermName,
                g.Key.TermOrder,
                g.Select(s => new TranscriptScoreItemDto(s.Subject, s.RawScore, s.Score, s.Credits)).ToList()
            ))
            .ToList();

        var response = MapToResponse(profile, terms);
        return AcademicProfileResult.Ok(response);
    }

    private static AcademicProfileResponse MapToResponse(StudentProfile p, List<TranscriptTermDto> terms)
    {
        return new AcademicProfileResponse(
            p.Id,
            p.UserId,
            p.TargetLevel,
            p.CurrentSchool,
            p.EducationSystem,
            p.GraduationYear,
            p.CurrentGrade,
            p.GradeScale,
            p.OverallGpa,
            p.IntendedMajor,
            p.Ielts,
            p.Toefl,
            p.Duolingo,
            p.Sat,
            p.Act,
            p.Gre,
            p.Gmat,
            p.OtherTestsJson,
            terms,
            p.CreatedAt,
            p.UpdatedAt
        );
    }
}
