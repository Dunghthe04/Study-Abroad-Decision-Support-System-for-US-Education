using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Profile.Financial;

/// <summary>
/// [USAS-364] Implementation của IFinancialProfileService.
/// Kết nối trực tiếp với bảng student_profiles và profile_activities của toàn dự án.
/// Đảm bảo tính nhất quán dữ liệu, validation chặt chẽ và chống IDOR.
/// </summary>
public class FinancialProfileService(IFinancialProfileRepository repository) : IFinancialProfileService
{
    // ==========================================
    // 1. TÀI CHÍNH (FINANCIAL PROFILE)
    // ==========================================

    public async Task<FinancialProfileDto?> GetFinancialProfileAsync(Guid userId, CancellationToken ct = default)
    {
        var profile = await repository.GetProfileByUserIdAsync(userId, ct);
        if (profile is null || profile.AnnualBudgetUsd is null)
            return null;

        return MapToFinancialDto(profile);
    }

    public async Task<FinancialProfileDto> SaveFinancialProfileAsync(Guid userId, SaveFinancialProfileRequest request, CancellationToken ct = default)
    {
        if (request.AnnualBudget < 0)
            throw new ArgumentException("Ngân sách hàng năm không được là số âm.", nameof(request.AnnualBudget));

        if (request.AnnualBudget > ProfileConstants.ValidationLimits.MaxAnnualBudget)
            throw new ArgumentException($"Ngân sách hàng năm không được vượt quá {ProfileConstants.ValidationLimits.MaxAnnualBudget:N0} USD.", nameof(request.AnnualBudget));

        if (string.IsNullOrWhiteSpace(request.FundingSource))
            throw new ArgumentException("Nguồn tài chính là bắt buộc.", nameof(request.FundingSource));

        var profile = await repository.EnsureProfileExistsAsync(userId, ct);

        profile.AnnualBudgetUsd = request.AnnualBudget;
        profile.FundingSource = request.FundingSource.Trim();
        profile.NeedsScholarship = request.NeedScholarship;

        await repository.UpdateProfileAsync(profile, ct);
        return MapToFinancialDto(profile, request.Notes);
    }

    // ==========================================
    // 2. NGOẠI KHÓA (EXTRACURRICULAR ACTIVITIES)
    // ==========================================

    public async Task<IReadOnlyList<ExtracurricularActivityDto>> GetActivitiesAsync(Guid userId, CancellationToken ct = default)
    {
        var profile = await repository.GetProfileByUserIdAsync(userId, ct);
        if (profile is null) return [];

        var list = await repository.GetActivitiesByProfileIdAsync(profile.Id, "extracurricular", ct);
        return list.Select(a => MapToActivityDto(a, userId)).ToList();
    }

    public async Task<ExtracurricularActivityDto> AddActivityAsync(Guid userId, CreateExtracurricularRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.ActivityName))
            throw new ArgumentException("Tên hoạt động không được để trống.", nameof(request.ActivityName));

        if (string.IsNullOrWhiteSpace(request.Role))
            throw new ArgumentException("Vai trò không được để trống.", nameof(request.Role));

        var profile = await repository.EnsureProfileExistsAsync(userId, ct);

        DateOnly? startDate = null;
        if (DateOnly.TryParse(request.StartDate, out var sDate)) startDate = sDate;

        DateOnly? endDate = null;
        if (DateOnly.TryParse(request.EndDate, out var eDate)) endDate = eDate;

        var activity = new ProfileActivity
        {
            StudentProfileId = profile.Id,
            Kind = "extracurricular",
            Title = request.ActivityName.Trim(),
            Role = request.Role.Trim(),
            Organization = request.Organization?.Trim(),
            Description = request.Description?.Trim(),
            StartDate = startDate,
            EndDate = endDate
        };

        await repository.AddActivityAsync(activity, ct);
        return MapToActivityDto(activity, userId, request.DurationMonths, request.ImpactLevel, request.IsOngoing);
    }

    public async Task<ExtracurricularActivityDto?> UpdateActivityAsync(Guid userId, Guid activityId, UpdateExtracurricularRequest request, CancellationToken ct = default)
    {
        var profile = await repository.GetProfileByUserIdAsync(userId, ct);
        if (profile is null) return null;

        var activity = await repository.GetActivityByIdAsync(activityId, ct);
        if (activity is null) return null;

        // BẢO MẬT CHỐNG IDOR: Kiểm tra activity thuộc profile của chính user đang gọi
        if (activity.StudentProfileId != profile.Id)
            return null;

        if (string.IsNullOrWhiteSpace(request.ActivityName))
            throw new ArgumentException("Tên hoạt động không được để trống.", nameof(request.ActivityName));

        if (string.IsNullOrWhiteSpace(request.Role))
            throw new ArgumentException("Vai trò không được để trống.", nameof(request.Role));

        activity.Title = request.ActivityName.Trim();
        activity.Role = request.Role.Trim();
        activity.Organization = request.Organization?.Trim();
        activity.Description = request.Description?.Trim();

        if (DateOnly.TryParse(request.StartDate, out var sDate)) activity.StartDate = sDate;
        if (DateOnly.TryParse(request.EndDate, out var eDate)) activity.EndDate = eDate;

        await repository.UpdateActivityAsync(activity, ct);
        return MapToActivityDto(activity, userId, request.DurationMonths, request.ImpactLevel, request.IsOngoing);
    }

    public async Task<bool> DeleteActivityAsync(Guid userId, Guid activityId, CancellationToken ct = default)
    {
        var profile = await repository.GetProfileByUserIdAsync(userId, ct);
        if (profile is null) return false;

        var activity = await repository.GetActivityByIdAsync(activityId, ct);
        if (activity is null) return false;

        // BẢO MẬT CHỐNG IDOR
        if (activity.StudentProfileId != profile.Id)
            return false;

        await repository.DeleteActivityAsync(activity, ct);
        return true;
    }

    // ==========================================
    // 3. THÀNH TÍCH / GIẢI THƯỞNG (ACHIEVEMENTS)
    // ==========================================

    public async Task<IReadOnlyList<StudentAchievementDto>> GetAchievementsAsync(Guid userId, CancellationToken ct = default)
    {
        var profile = await repository.GetProfileByUserIdAsync(userId, ct);
        if (profile is null) return [];

        var list = await repository.GetActivitiesByProfileIdAsync(profile.Id, "award", ct);
        return list.Select(MapToAchievementDto).ToList();
    }

    public async Task<StudentAchievementDto> AddAchievementAsync(Guid userId, CreateAchievementRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new ArgumentException("Tiêu đề thành tích không được để trống.", nameof(request.Title));

        var profile = await repository.EnsureProfileExistsAsync(userId, ct);

        DateOnly? issueDate = null;
        if (DateOnly.TryParse(request.IssueDate, out var iDate)) issueDate = iDate;

        var activity = new ProfileActivity
        {
            StudentProfileId = profile.Id,
            Kind = "award",
            Title = request.Title.Trim(),
            Role = request.Category.Trim(),
            Organization = request.Issuer?.Trim(),
            Description = request.Description?.Trim(),
            StartDate = issueDate
        };

        await repository.AddActivityAsync(activity, ct);
        return MapToAchievementDto(activity);
    }

    public async Task<bool> DeleteAchievementAsync(Guid userId, Guid achievementId, CancellationToken ct = default)
    {
        var profile = await repository.GetProfileByUserIdAsync(userId, ct);
        if (profile is null) return false;

        var activity = await repository.GetActivityByIdAsync(achievementId, ct);
        if (activity is null) return false;

        // BẢO MẬT CHỐNG IDOR
        if (activity.StudentProfileId != profile.Id)
            return false;

        await repository.DeleteActivityAsync(activity, ct);
        return true;
    }

    // ==========================================
    // 4. TỔNG QUAN HỒ SƠ PHI HỌC THUẬT (SUMMARY)
    // ==========================================

    public async Task<StudentProfileSummaryDto> GetFullProfileSummaryAsync(Guid userId, CancellationToken ct = default)
    {
        var financial = await GetFinancialProfileAsync(userId, ct);
        var activities = await GetActivitiesAsync(userId, ct);
        var achievements = await GetAchievementsAsync(userId, ct);

        int maxImpact = activities.Count > 0 ? activities.Max(a => a.ImpactLevel) : 0;

        return new StudentProfileSummaryDto(
            UserId: userId,
            Financial: financial,
            Activities: activities,
            Achievements: achievements,
            TotalActivitiesCount: activities.Count,
            TotalAchievementsCount: achievements.Count,
            MaxImpactLevel: maxImpact
        );
    }

    // ==========================================
    // MAPPERS
    // ==========================================

    private static FinancialProfileDto MapToFinancialDto(StudentProfile p, string? notes = null) =>
        new(p.Id, p.UserId, p.AnnualBudgetUsd ?? 0, p.FundingSource ?? "family", p.NeedsScholarship, null, "USD", notes, p.CreatedAt, p.UpdatedAt);

    private static ExtracurricularActivityDto MapToActivityDto(ProfileActivity a, Guid userId, int? durationMonths = null, int impactLevel = 1, bool isOngoing = false) =>
        new(a.Id, userId, a.Title, a.Role ?? string.Empty, a.Organization ?? string.Empty, durationMonths, a.StartDate?.ToString("yyyy-MM"), a.EndDate?.ToString("yyyy-MM"), isOngoing, impactLevel, a.Description ?? string.Empty, a.CreatedAt, a.UpdatedAt);

    private static StudentAchievementDto MapToAchievementDto(ProfileActivity a) =>
        new(a.Id, a.StudentProfileId, a.Role ?? "award", a.Title, a.Organization, a.StartDate?.ToString("yyyy-MM"), a.Description ?? string.Empty, a.CreatedAt);
}
