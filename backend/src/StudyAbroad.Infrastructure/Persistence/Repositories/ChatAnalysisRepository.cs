using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Advisor;
using StudyAbroad.Domain.Entities;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StudyAbroad.Infrastructure.Persistence.Repositories
{
    public class ChatAnalysisRepository(AppDbContext db) : IChatAnalysisRepository
    {
        public async Task SaveAsync(Guid userId, string kind, string resultJson, string modelVersion, CancellationToken ct = default)
        {
            var profileId = await db.StudentProfiles
                .Where(p => p.UserId == userId)
                .Select(p => (Guid?)p.Id)
                .FirstOrDefaultAsync(ct);
            if (profileId is null)
                return;
            db.AnalysisResults.Add(new AnalysisResult
            {
                StudentProfileId = profileId.Value,
                Kind = kind,
                ResultJson = resultJson,
                ModelVersion = modelVersion,
            });
            await db.SaveChangesAsync(ct);
        }
    }
}
