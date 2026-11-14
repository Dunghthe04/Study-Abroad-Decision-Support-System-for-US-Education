using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Recommendations;

// Điểm thi dùng để gợi ý trường, đọc từ bảng profile_certifications (không đọc các cột Ielts, Sat... trên student_profiles).
// Chỉ tính bài ĐÃ THI: dòng is_target (điểm mục tiêu, chưa thi) bị bỏ, nên học sinh chỉ có dự định thì coi như chưa có điểm.
// Một loại thi nhiều lần: lấy điểm cao nhất. Dòng không có điểm số (OverallScore null) bị bỏ.
public record ProfileScores(decimal? Ielts, decimal? Toefl, decimal? Duolingo, decimal? Sat, decimal? Act)
{
    public static readonly ProfileScores Empty = new(null, null, null, null, null);

    public static ProfileScores From(IEnumerable<ProfileCertification> certifications)
    {
        var taken = certifications.Where(c => !c.IsTarget && c.OverallScore is not null).ToList();
        return new ProfileScores(
            Best(taken, "ielts"), Best(taken, "toefl"), Best(taken, "duolingo"), Best(taken, "sat"), Best(taken, "act"));
    }

    // Bản sao của hồ sơ với 5 cột điểm thay bằng điểm này (null khi chưa thi). Bản gốc giữ nguyên.
    public StudentProfile ApplyTo(StudentProfile profile)
    {
        var copy = profile.ShallowCopy();
        copy.Ielts = Ielts;
        copy.Toefl = Toefl;
        copy.Duolingo = Duolingo;
        copy.Sat = Sat;
        copy.Act = Act;
        return copy;
    }

    private static decimal? Best(IEnumerable<ProfileCertification> taken, string testType) =>
        taken.Where(c => string.Equals(c.TestType, testType, StringComparison.OrdinalIgnoreCase)).Max(c => c.OverallScore);
}
