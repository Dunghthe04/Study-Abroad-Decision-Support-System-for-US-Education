using Microsoft.EntityFrameworkCore;
using StudyAbroad.Application.Auth;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Repositories;

public class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<bool> EmailExistAsync(string email, CancellationToken cancellationToken = default) =>
        db.Users.AnyAsync(u => u.Email == email, cancellationToken);

    public Task<bool> PhoneExistAsync(string phone, CancellationToken cancellationToken = default) =>
        db.Users.AnyAsync(u => u.Phone == phone, cancellationToken);

    public Task<User?> GetUserByEmail(string email, CancellationToken cancellationToken = default) =>
        db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

    public Task<User?> GetUserById(Guid id, CancellationToken cancellationToken = default) =>
        db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task CreateSessionAsync(UserSession session, CancellationToken cancellationToken = default)
    {
        db.UserSessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<UserSession?> GetSessionByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        db.UserSessions.AsNoTracking().FirstOrDefaultAsync(s => s.TokenHash == tokenHash, cancellationToken);

    public async Task RevokeSessionAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        var session = await db.UserSessions.FirstOrDefaultAsync(s => s.TokenHash == tokenHash, cancellationToken);
        if (session != null)
        {
            session.IsRevoked = true;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public Task<List<User>> GetAllUsersAsync(CancellationToken cancellationToken = default) =>
        db.Users.AsNoTracking().OrderByDescending(u => u.CreatedAt).ToListAsync(cancellationToken);
}

