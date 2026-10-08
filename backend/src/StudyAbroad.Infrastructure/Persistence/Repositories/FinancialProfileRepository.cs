using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Profile.Financial;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

/// <summary>
/// [USAS-364] Implementation của IFinancialProfileRepository.
/// Thao tác trực tiếp trên bảng student_profiles và profile_activities thuộc schema app.
/// </summary>
public class FinancialProfileRepository(AppDbContext db) : IFinancialProfileRepository
{
    public Task<StudentProfile?> GetProfileByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        db.StudentProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);

    public async Task<StudentProfile> EnsureProfileExistsAsync(Guid userId, CancellationToken ct = default)
    {
        var existing = await db.StudentProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (existing is not null) return existing;

        var profile = new StudentProfile
        {
            UserId = userId,
            TargetLevel = "undergraduate"
        };
        db.StudentProfiles.Add(profile);
        await db.SaveChangesAsync(ct);
        return profile;
    }

    public async Task UpdateProfileAsync(StudentProfile profile, CancellationToken ct = default)
    {
        db.StudentProfiles.Update(profile);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ProfileActivity>> GetActivitiesByProfileIdAsync(Guid studentProfileId, string? kind = null, CancellationToken ct = default)
    {
        var query = db.ProfileActivities.Where(a => a.StudentProfileId == studentProfileId);
        if (!string.IsNullOrEmpty(kind))
            query = query.Where(a => a.Kind == kind);

        return await query.OrderByDescending(a => a.CreatedAt).ToListAsync(ct);
    }

    public Task<ProfileActivity?> GetActivityByIdAsync(Guid activityId, CancellationToken ct = default) =>
        db.ProfileActivities.FirstOrDefaultAsync(a => a.Id == activityId, ct);

    public async Task AddActivityAsync(ProfileActivity activity, CancellationToken ct = default)
    {
        db.ProfileActivities.Add(activity);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateActivityAsync(ProfileActivity activity, CancellationToken ct = default)
    {
        db.ProfileActivities.Update(activity);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteActivityAsync(ProfileActivity activity, CancellationToken ct = default)
    {
        db.ProfileActivities.Remove(activity);
        await db.SaveChangesAsync(ct);
    }
}
