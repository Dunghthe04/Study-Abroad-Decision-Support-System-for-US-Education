using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Profile.Flow;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

/// <summary>
/// [USAS-640] Implementation của IProfileFlowRepository.
/// </summary>
public class ProfileFlowRepository(AppDbContext context) : IProfileFlowRepository
{
    public async Task<string?> GetAppSettingValueAsync(string key, CancellationToken ct = default)
    {
        var setting = await context.AppSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == key, ct);

        return setting?.ValueJson;
    }

    public async Task<StudentProfile?> GetProfileByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        return await context.StudentProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);
    }

    public async Task UpdateProfileAsync(StudentProfile profile, CancellationToken ct = default)
    {
        profile.UpdatedAt = DateTime.UtcNow;
        context.StudentProfiles.Update(profile);
        await context.SaveChangesAsync(ct);
    }

    public async Task<StudentProfile> EnsureProfileExistsAsync(Guid userId, CancellationToken ct = default)
    {
        var profile = await GetProfileByUserIdAsync(userId, ct);
        if (profile is not null)
            return profile;

        profile = new StudentProfile
        {
            UserId = userId,
            TargetLevel = StudyLevels.Undergraduate,
            GradeScale = GradeScales.Scale10,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await context.StudentProfiles.AddAsync(profile, ct);
        await context.SaveChangesAsync(ct);
        return profile;
    }

    public async Task<IReadOnlyList<string>> GetOfferingMajorsAsync(CancellationToken ct = default)
    {
        var offerings = await context.UniversityOfferings
            .AsNoTracking()
            .Select(o => o.Majors)
            .ToListAsync(ct);

        return offerings
            .SelectMany(m => m)
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Select(m => m.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(m => m)
            .ToList();
    }
}
