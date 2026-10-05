using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudyAbroad.Application.Recommendations
{
    public sealed class MajorMatcher
    {
        //Dictionary lưu nhóm ngành và ngành con
        private readonly Dictionary<string, string> _groupOf = new();

        public MajorMatcher(IReadOnlyDictionary<string, List<string>> groups)
        {
            // duyệt từng nhóm ngành và ngành con để thêm vào dictionary
            foreach(var (group, members) in groups)
            {
                foreach (var member in members)
                {
                    //Từ key là ngành con, value là nhóm ngành
                    _groupOf[Normalize(member)] = Normalize(group);
                }
            }
        }

        public bool Matches(string? studentMajor, IReadOnlyList<string> schoolMajors)
        {
            // Chưa chọn ngành → không lọc theo ngành
            if (string.IsNullOrEmpty(studentMajor))
                return true;

            // Chuẩn hóa ngành sinh viên chọn
            var student = Normalize(studentMajor);

            // Lấy nhóm ngành mà ngành sinh viên thuộc về
            var studentGroup = _groupOf.GetValueOrDefault(student);

            // Kiểm tra từng ngành mà trường cung cấp
            foreach (var raw in schoolMajors)
            {
                var school = Normalize(raw);

                // 1. Student và trường có cùng ngành
                if (school == student)
                    return true;

                // 2. Student chọn NHÓM → trường có NGÀNH CON thuộc nhóm đó
                if (student == _groupOf.GetValueOrDefault(school))
                    return true;

                // 3. Student chọn NGÀNH CON → trường có NHÓM của ngành đó
                if (studentGroup is not null && school == studentGroup)
                    return true;
            }

            // Không có ngành nào phù hợp
            return false;
        }

        /// <summary>"Computer Science, BS" → "computer science".</summary>
        public static string Normalize(string major)
        {
            var name = major.Split(',')[0];// bỏ hậu tố

            //Loại bỏ các ký tự trắng đầu cuối và giữa
            return string.Join(" ", name.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }


    }
}
