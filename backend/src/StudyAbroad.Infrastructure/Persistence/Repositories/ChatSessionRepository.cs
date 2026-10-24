using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Advisor;
using StudyAbroad.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudyAbroad.Infrastructure.Persistence.Repositories
{
    public class ChatSessionRepository(AppDbContext db) : IChatSessionRepository
    {
        public async Task AddAsync(ChatSession chatSession, CancellationToken ct = default)
        {
            db.ChatSessions.Add(chatSession);
            await db.SaveChangesAsync(ct);
        }

        public Task<ChatSession?> FindAsync(Guid id, Guid userId, CancellationToken ct = default)
        {
            return db.ChatSessions.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, ct);
        }

        public Task SaveAsync(CancellationToken ct = default)
        {
            return db.SaveChangesAsync(ct);
        }
    }
}
