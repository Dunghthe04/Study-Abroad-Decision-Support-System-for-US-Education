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
        /// <summary>Newly registered accounts waiting for email OTP verification.</summary>
        public const string Unverified = "unverified";
        /// <summary>Center accounts wait for admin approval.</summary>
        public const string Pending = "pending";
        /// <summary>Temporarily locked after 5 consecutive failed login attempts; can be unlocked with email OTP.</summary>
        public const string TempLocked = "temp_locked";
        /// <summary>Locked permanently by an admin; login returns 423 and CANNOT be self-unlocked via OTP.</summary>
        public const string Locked = "locked";

    }
}
