using StudyAbroad.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace StudyAbroad.Application.AcademicProfiles;

public interface IAcademicProfileRepository
{
    Task<StudentProfile?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<List<TranscriptScore>> GetTranscriptScoresAsync(Guid studentProfileId, CancellationToken ct = default);
    Task AddProfileAsync(StudentProfile profile, CancellationToken ct = default);
    Task UpdateProfileAsync(StudentProfile profile, CancellationToken ct = default);
    Task ReplaceTranscriptScoresAsync(Guid studentProfileId, IEnumerable<TranscriptScore> scores, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
