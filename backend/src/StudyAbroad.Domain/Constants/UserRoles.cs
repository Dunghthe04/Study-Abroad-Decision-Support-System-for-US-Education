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
        public const string Moderator = "moderator";
        public const string Admin = "admin";

        /// <summary>Roles a visitor may pick on the register form. Admin/moderator do admin tạo;
        /// center do moderator tạo (trung tâm không tự đăng ký).</summary>
        public static readonly IReadOnlySet<string> SelfRegister = new HashSet<string> { Student, Parent };

        /// <summary>[UC-80] Vai trò được tự xóa tài khoản (đặc tả chỉ cho học sinh và phụ huynh).
        /// Job xóa hẳn cũng chỉ xử lý các vai trò này.</summary>
        public static readonly IReadOnlySet<string> SelfDelete = new HashSet<string> { Student, Parent };

    }
}
