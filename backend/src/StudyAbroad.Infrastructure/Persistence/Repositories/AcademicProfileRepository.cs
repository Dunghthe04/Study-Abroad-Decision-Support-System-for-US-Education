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
}
