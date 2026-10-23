using StudyAbroad.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudyAbroad.Application.Advisor
{
    public interface IChatSessionRepository
    {
        /// <summary>Tìm phiên chat của đúng người dùng này; không thấy (hoặc của người khác) thì trả null.</summary>
        Task<ChatSession> FindAsync(Guid id, Guid userId, CancellationToken ct = default);
        Task AddAsync(ChatSession chatSession, CancellationToken ct = default);
        Task SaveAsync(CancellationToken ct = default);
    }
}
