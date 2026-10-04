using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Profile.Academic;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

/// <summary>
/// [USAS-365] Repository thao tác cơ sở dữ liệu cho chức năng phân tích điểm học thuật:
/// transcript_scores, analysis_results, app_settings, student_profiles.
/// </summary>
public class AcademicAnalysisRepository : IAcademicAnalysisRepository
{
    private readonly AppDbContext _context;

    public AcademicAnalysisRepository(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// [USAS-365] Lấy hồ sơ học sinh theo UserId (chống IDOR).
    /// </summary>
    public async Task<StudentProfile?> GetProfileByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        return await _context.StudentProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);
    }

    /// <summary>
    /// [USAS-365] Đảm bảo luôn tồn tại bản ghi student_profiles cho học sinh.
    /// </summary>
    public async Task<StudentProfile> EnsureProfileExistsAsync(Guid userId, CancellationToken ct = default)
    {
        var profile = await _context.StudentProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);

        if (profile == null)
        {
            profile = new StudentProfile
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                GradeScale = "10"
            };
            await _context.StudentProfiles.AddAsync(profile, ct);
            await _context.SaveChangesAsync(ct);
        }

        return profile;
    }

    /// <summary>
    /// [USAS-365] Lấy toàn bộ bảng điểm của hồ sơ học sinh.
    /// </summary>
    public async Task<IReadOnlyList<TranscriptScore>> GetScoresByProfileIdAsync(Guid profileId, CancellationToken ct = default)
    {
        return await _context.TranscriptScores
            .Where(s => s.StudentProfileId == profileId)
            .OrderBy(s => s.TermOrder)
            .ThenBy(s => s.Subject)
            .ToListAsync(ct);
    }

    /// <summary>
    /// [USAS-365] Lưu danh sách đầu điểm bảng điểm (hỗ trợ cả thêm mới và sửa điểm).
    /// </summary>
    public async Task<IReadOnlyList<TranscriptScore>> SaveScoresAsync(Guid profileId, IEnumerable<TranscriptScore> scores, CancellationToken ct = default)
    {
        var scoreList = scores.ToList();
        var incomingIds = scoreList.Select(s => s.Id).ToList();

        var existingScores = await _context.TranscriptScores
            .Where(s => s.StudentProfileId == profileId && incomingIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, ct);

        foreach (var score in scoreList)
        {
            if (existingScores.TryGetValue(score.Id, out var existing))
            {
                // Cập nhật bản ghi điểm đã có
                existing.TermName = score.TermName;
                existing.TermOrder = score.TermOrder;
                existing.Subject = score.Subject;
                existing.Score = score.Score;
                existing.Credits = score.Credits;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                // Thêm bản ghi điểm mới
                score.StudentProfileId = profileId;
                score.CreatedAt = DateTime.UtcNow;
                await _context.TranscriptScores.AddAsync(score, ct);
            }
        }

        await _context.SaveChangesAsync(ct);
        return await GetScoresByProfileIdAsync(profileId, ct);
    }

    /// <summary>
    /// [USAS-365] Xóa các đầu điểm thuộc quyền sở hữu của hồ sơ.
    /// </summary>
    public async Task DeleteScoresAsync(Guid profileId, IEnumerable<Guid> scoreIds, CancellationToken ct = default)
    {
        var toDelete = await _context.TranscriptScores
            .Where(s => s.StudentProfileId == profileId && scoreIds.Contains(s.Id))
            .ToListAsync(ct);

        if (toDelete.Count > 0)
        {
            _context.TranscriptScores.RemoveRange(toDelete);
            await _context.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// [USAS-365] Lấy cấu hình thang điểm từ bảng app_settings.
    /// </summary>
    public async Task<AppSetting?> GetAppSettingByKeyAsync(string key, CancellationToken ct = default)
    {
        return await _context.AppSettings
            .FirstOrDefaultAsync(s => s.Key == key, ct);
    }

    /// <summary>
    /// [USAS-365] Lưu kết quả phân tích học thuật và cập nhật điểm OverallGpa cho hồ sơ học sinh.
    /// </summary>
    public async Task<AnalysisResult> SaveAnalysisResultAsync(AnalysisResult result, decimal overallGpa, CancellationToken ct = default)
    {
        await _context.AnalysisResults.AddAsync(result, ct);

        // Đồng bộ điểm trung bình vào student_profiles để tiện truy vấn nhanh
        var profile = await _context.StudentProfiles.FindAsync(new object[] { result.StudentProfileId }, ct);
        if (profile != null)
        {
            profile.OverallGpa = overallGpa;
            profile.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(ct);
        return result;
    }

    /// <summary>
    /// [USAS-365] Lấy kết quả phân tích học thuật mới nhất của hồ sơ.
    /// </summary>
    public async Task<AnalysisResult?> GetLatestAnalysisAsync(Guid profileId, CancellationToken ct = default)
    {
        return await _context.AnalysisResults
            .Where(a => a.StudentProfileId == profileId && a.Kind == AcademicConstants.KindGpa)
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }
}
