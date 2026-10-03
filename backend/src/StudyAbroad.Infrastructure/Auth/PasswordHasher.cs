using StudyAbroad.Application.Auth;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudyAbroad.Infrastructure.Auth
{
    public class PasswordHasher : IPasswordHasher
    {
        //Chạy 2^12 vòng để hash
        private const int WorkFactor = 12;
        public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
    }
}
