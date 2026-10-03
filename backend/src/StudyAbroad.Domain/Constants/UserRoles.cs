using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudyAbroad.Domain.Constants
{
    public static class UserRoles
    {
        public const string Student = "student";
        public const string Parent = "parent";
        public const string Center = "center";
        public const string Admin = "admin";

        /// <summary>Roles a visitor may pick on the register form. Admin accounts are created by seeding only.</summary>
        public static readonly IReadOnlySet<string> SelfRegister = new HashSet<string> { Student, Center, Parent };

    }
}
