using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StudyAbroad.Application.Recommendations
{
    /// <summary>Nhóm học thuật của một trường so với hồ sơ học sinh.</summary>
    public enum AdmissionCategory
    {
        Reach,
        Match,
        Safety,
        InsufficientData, // Không đủ dữ liệu (thiếu GPA hoặc SAT)
    }

    /// <summary>
    /// Xếp Reach/Match/Safety theo quy tắc #6:
    /// GPA so với GPA trung bình của trường (chênh từ gpaBand), SAT so với mốc 25%/75%,
    /// lấy mức thấp hơn của hai tiêu chí có dữ liệu. Không có tiêu chí nào thì InsufficientData.
    /// Hàm thuần: cùng đầu vào luôn cho cùng kết quả.
    /// </summary>
    
    public class AdmissionCategorizer
    {
        private enum Level { Low =0, Even=1, High=2}

        public static AdmissionCategory Categorize(
            decimal? studentGpa4, int? studentSat,decimal? avgGpa4, int? sat25, int? sat75, decimal band)
        {
            var gpaLevel = GpaLevel(studentGpa4, avgGpa4, band);
            var satLevel = SatLevel(studentSat, sat25, sat75);

            Level? level = (gpaLevel, satLevel) switch
            {
                ({ } g, { } s) => g < s ? g : s,// nếu cả hai có dữ liệu ==> lấy mức thấp hơn
                ({ } g, null) => g,// chỉ có GPA
                (null, { } s) => s,// chỉ có SAT
                (null, null) => null,// không có dữ liệu
            };

            return level switch
            {
                Level.Low => AdmissionCategory.Reach,
                Level.Even => AdmissionCategory.Match,
                Level.High => AdmissionCategory.Safety,
                _ => AdmissionCategory.InsufficientData,
            };
        }

        private static Level? GpaLevel(decimal? GpaStudent, decimal? schoolAvg, decimal band)
        {
            if(GpaStudent == null || schoolAvg == null) return null;
            var diff = GpaStudent.Value - schoolAvg.Value;
            if (diff >= band) return Level.High;
            if(diff <= -band) return Level.Low;
            return Level.Even;
        }

        private static Level? SatLevel(int? satStudent, int ? sat25, int? sat75)
        {
            if(satStudent == null || sat25 == null || sat75 == null) return null;
            if(satStudent < sat25) return Level.Low;
            if(satStudent >= sat75) return Level.High;
            return Level.Even;
        }
    }

}
