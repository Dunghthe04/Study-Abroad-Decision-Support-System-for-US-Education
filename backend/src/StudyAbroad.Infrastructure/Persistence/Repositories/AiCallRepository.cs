using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Advisor;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

public class AiCallRepository(AppDbContext db) : IAiCallRepository
{
    public async Task AddRangeAsync(IReadOnlyList<AiCall> calls, CancellationToken ct = default)
    {
        db.AiCalls.AddRange(calls);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            // DbContext dùng chung trong cả yêu cầu: bỏ các dòng hỏng ra khỏi bộ theo dõi để không làm hỏng các lần lưu sau cùng yêu cầu
            foreach (var call in calls)
                db.Entry(call).State = EntityState.Detached;
            throw;
        }
    }
}
