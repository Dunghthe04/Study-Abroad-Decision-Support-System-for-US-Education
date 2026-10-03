using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudyAbroad.Application.Auth
{
    public interface IPasswordHasher
    {
        string Hash(string password);
        bool VerifyPassword(string hash,string password);
    }
}
