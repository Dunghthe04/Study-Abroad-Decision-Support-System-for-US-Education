using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudyAbroad.Application.Grading
{
    /// <summary>
    /// Quy GPA của hồ sơ về thang 4 (để so với GPA trung bình của trường Mỹ).
    /// Bản chính thức do chức năng quy đổi điểm (#13, Đức) cài đặt.
    /// Trả null nếu không quy đổi được, tuyệt đối không tự đoán.
    /// </summary>
    public interface IGpaConverter
    {
        Task<decimal?> ToGpa4Async(decimal? gpa, string? gradeScale,CancellationToken ct=default);
    }
}
