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
}
