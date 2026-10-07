using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.AcademicProfiles;
using StudyAbroad.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

public class AcademicProfileRepository(AppDbContext db) : IAcademicProfileRepository
{
    public async Task<StudentProfile?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        return await db.StudentProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, ct);
    }

    public async Task<List<TranscriptScore>> GetTranscriptScoresAsync(Guid studentProfileId, CancellationToken ct = default)
    {
        return await db.TranscriptScores
            .AsNoTracking()
            .Where(s => s.StudentProfileId == studentProfileId)
            .OrderBy(s => s.TermOrder)
            .ThenBy(s => s.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task AddProfileAsync(StudentProfile profile, CancellationToken ct = default)
    {
        await db.StudentProfiles.AddAsync(profile, ct);
    }

    public Task UpdateProfileAsync(StudentProfile profile, CancellationToken ct = default)
    {
        db.StudentProfiles.Update(profile);
        return Task.CompletedTask;
    }

    public async Task ReplaceTranscriptScoresAsync(Guid studentProfileId, IEnumerable<TranscriptScore> scores, CancellationToken ct = default)
    {
        // Xóa bảng điểm cũ của profile
        var existing = await db.TranscriptScores
            .Where(s => s.StudentProfileId == studentProfileId)
            .ToListAsync(ct);

        if (existing.Count > 0)
        {
            db.TranscriptScores.RemoveRange(existing);
        }

        // Thêm bảng điểm mới
        var scoreList = scores.ToList();
        if (scoreList.Count > 0)
        {
            await db.TranscriptScores.AddRangeAsync(scoreList, ct);
        }
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await db.SaveChangesAsync(ct);
    }
}
