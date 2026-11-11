using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Advisor;
using ChatMessageRow = StudyAbroad.Domain.Entities.ChatMessage;   // trùng tên với ChatMessage (DTO) của Application.Advisor

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

public class ChatMessageRepository(AppDbContext db) : IChatMessageRepository
{
    public async Task AddRangeAsync(IReadOnlyList<ChatMessageRow> messages, CancellationToken ct = default)
    {
        db.ChatMessages.AddRange(messages);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            // DbContext dùng chung trong cả yêu cầu: bỏ các dòng hỏng ra khỏi bộ theo dõi để không làm hỏng các lần lưu sau cùng yêu cầu
            foreach (var message in messages)
                db.Entry(message).State = EntityState.Detached;
            throw;
        }
    }
}
