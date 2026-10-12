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

    public async Task ReplaceTranscriptScoresAsync(Guid profileId, IEnumerable<TranscriptScore> scores, CancellationToken ct = default)
    {
        var existing = await context.TranscriptScores
            .Where(s => s.StudentProfileId == profileId)
            .ToListAsync(ct);

        if (existing.Count > 0)
        {
            context.TranscriptScores.RemoveRange(existing);
        }

        var toAdd = scores.ToList();
        foreach (var s in toAdd)
        {
            s.StudentProfileId = profileId;
            s.CreatedAt = DateTime.UtcNow;
            s.UpdatedAt = DateTime.UtcNow;
        }

        if (toAdd.Count > 0)
        {
            await context.TranscriptScores.AddRangeAsync(toAdd, ct);
        }

        await context.SaveChangesAsync(ct);
    }

    public async Task ReplaceProfileActivitiesAsync(Guid profileId, IEnumerable<ProfileActivity> activities, CancellationToken ct = default)
    {
        var existing = await context.ProfileActivities
            .Where(a => a.StudentProfileId == profileId)
            .ToListAsync(ct);

        if (existing.Count > 0)
        {
            context.ProfileActivities.RemoveRange(existing);
        }

        var toAdd = activities.ToList();
        foreach (var a in toAdd)
        {
            a.StudentProfileId = profileId;
            a.CreatedAt = DateTime.UtcNow;
            a.UpdatedAt = DateTime.UtcNow;
        }

        if (toAdd.Count > 0)
        {
            await context.ProfileActivities.AddRangeAsync(toAdd, ct);
        }

        await context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<TranscriptScore>> GetTranscriptScoresAsync(Guid profileId, CancellationToken ct = default)
    {
        return await context.TranscriptScores
            .AsNoTracking()
            .Where(s => s.StudentProfileId == profileId)
            .OrderBy(s => s.TermOrder)
            .ThenBy(s => s.Subject)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ProfileActivity>> GetProfileActivitiesAsync(Guid profileId, CancellationToken ct = default)
    {
        return await context.ProfileActivities
            .AsNoTracking()
            .Where(a => a.StudentProfileId == profileId)
            .OrderByDescending(a => a.ImpactLevel)
            .ThenBy(a => a.Title)
            .ToListAsync(ct);
    }
}
