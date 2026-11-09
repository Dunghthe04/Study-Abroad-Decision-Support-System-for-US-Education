using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Advisor;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StudyAbroad.Infrastructure.Persistence.Repositories
{
    // Cùng cách lọc với danh sách trường của gợi ý (đúng bậc học, trường đang hoạt động), chỉ lấy 4 cột
    public class ChatBenchmarkRepository(AppDbContext db) : IChatBenchmarkRepository
    {
        public async Task<IReadOnlyList<BenchmarkRow>> ListAsync(string studyLevel, CancellationToken ct = default) =>
            await db.UniversityOfferings
                .AsNoTracking()
                .Where(o => o.StudyLevel == studyLevel)
                .Join(
                    db.Universities.Where(u => u.IsActive),
                    offering => offering.UniversityId,
                    university => university.Id,
                    (offering, university) => new BenchmarkRow(
                        offering.AvgGpa4, offering.MinIelts, offering.MinToefl, offering.MinDuolingo))
                .ToListAsync(ct);
    }
}
