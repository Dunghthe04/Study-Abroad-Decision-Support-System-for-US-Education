using StudyAbroad.Application.Profile.Flow;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace StudyAbroad.Application.AcademicProfiles;

public class AcademicProfileService(
    IAcademicProfileRepository repository,
    IProfileFlowService? profileFlow = null) : IAcademicProfileService
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

        var certifications = await repository.GetCertificationsAsync(profile.Id, ct);
        var response = MapToResponse(profile, terms, certifications);
        // Đủ 5 bước cho thẻ hồ sơ ở Tài khoản và "Con của tôi"
        if (profileFlow is not null)
            response = response with { Details = await profileFlow.GetCommittedDetailsAsync(userId, ct) };
        return AcademicProfileResult.Ok(response);
    }

    private static AcademicProfileResponse MapToResponse(
        StudentProfile p, List<TranscriptTermDto> terms, IReadOnlyList<ProfileCertification> certifications)
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
            BestTaken(certifications, "ielts"),
            BestTaken(certifications, "toefl"),
            BestTaken(certifications, "duolingo"),
            BestTaken(certifications, "sat"),
            BestTaken(certifications, "act"),
            BestTaken(certifications, "gre"),
            BestTaken(certifications, "gmat"),
            BestTaken(certifications, "ap") is { } ap
                ? JsonSerializer.Serialize(new Dictionary<string, decimal> { ["ap"] = ap })
                : null,
            terms,
            p.CreatedAt,
            p.UpdatedAt,
            // Cùng điều kiện với luồng 6 bước; bảng điểm theo kỳ từ màn hình cũ cũng tính là đã điền
            ProfileFlowService.HasCommittedProfile(p) || terms.Count > 0
        );
    }

    // Điểm thi đọc từ profile_certifications: điểm cao nhất của loại đó trong các bài đã thi (bỏ dòng mục tiêu)
    private static decimal? BestTaken(IReadOnlyList<ProfileCertification> certifications, string testType) =>
        certifications.Where(c => !c.IsTarget && c.TestType == testType).Max(c => c.OverallScore);
}
