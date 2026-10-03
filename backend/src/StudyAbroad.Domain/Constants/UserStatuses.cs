using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudyAbroad.Domain.Constants
{
    public static class UserStatuses
    {
        public const string Active = "active";
        /// <summary>Center accounts wait for admin approval.</summary>
        public const string Pending = "pending";
        /// <summary>Locked by an admin; login returns 423.</summary>
        public const string Locked = "locked";

    }
}
