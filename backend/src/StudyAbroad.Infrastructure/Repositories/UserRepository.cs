using StudyAbroad.Application.Auth;
using StudyAbroad.Domain.Entities;
using StudyAbroad.Infrastructure.Persistence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudyAbroad.Infrastructure.Repositories
{
    public class UserRepository(AppDbContext db) : IUserRepository
    {
        public async Task AddAsync(User user, CancellationToken cancellationToken = default)
        {
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        public async Task<bool> EmailExistAsync(string email, CancellationToken cancellationToken = default)=>
            db.Users.Any(u=> u.Email == email);

    }
}
