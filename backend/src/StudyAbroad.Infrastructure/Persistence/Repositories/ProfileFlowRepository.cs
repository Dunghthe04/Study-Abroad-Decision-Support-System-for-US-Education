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

    public async Task CommitProfileFlowAsync(
        StudentProfile profile,
        IEnumerable<TranscriptScore> scores,
        IEnumerable<ProfileActivity> activities,
        IEnumerable<ProfileCertification> certifications,
        CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        await using var tx = await context.Database.BeginTransactionAsync(ct);

        profile.UpdatedAt = now;
        context.StudentProfiles.Update(profile);

        var oldScores = await context.TranscriptScores.Where(s => s.StudentProfileId == profile.Id).ToListAsync(ct);
        context.TranscriptScores.RemoveRange(oldScores);
        var oldActivities = await context.ProfileActivities.Where(a => a.StudentProfileId == profile.Id).ToListAsync(ct);
        context.ProfileActivities.RemoveRange(oldActivities);
        var oldCertifications = await context.ProfileCertifications.Where(c => c.StudentProfileId == profile.Id).ToListAsync(ct);
        context.ProfileCertifications.RemoveRange(oldCertifications);

        foreach (var s in scores)
        {
            s.StudentProfileId = profile.Id;
            s.CreatedAt = now;
            s.UpdatedAt = now;
            context.TranscriptScores.Add(s);
        }

        foreach (var a in activities)
        {
            a.StudentProfileId = profile.Id;
            a.CreatedAt = now;
            a.UpdatedAt = now;
            context.ProfileActivities.Add(a);
        }

        foreach (var c in certifications)
        {
            c.StudentProfileId = profile.Id;
            c.CreatedAt = now;
            c.UpdatedAt = now;
            context.ProfileCertifications.Add(c);
        }

        await context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
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

    public async Task<IReadOnlyList<ProfileCertification>> GetProfileCertificationsAsync(Guid profileId, CancellationToken ct = default)
    {
        return await context.ProfileCertifications
            .AsNoTracking()
            .Where(c => c.StudentProfileId == profileId)
            .OrderBy(c => c.SortOrder)
            .ToListAsync(ct);
    }
}
