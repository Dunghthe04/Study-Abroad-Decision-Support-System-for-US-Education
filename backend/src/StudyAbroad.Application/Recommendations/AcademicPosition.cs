namespace StudyAbroad.Application.Recommendations;

// Vị trí học thuật của học sinh so với sinh viên đã nhập học của trường: số từ 0 đến 1,
// bằng tỉ lệ sinh viên có điểm thấp hơn học sinh.
public static class AcademicPosition
{
    // Trong phân phối chuẩn, khoảng từ P25 đến P75 rộng 2 · Φ⁻¹(0,75) ≈ 1,349 độ lệch chuẩn.
    private const double IqrInSigma = 1.3489795003921634;

    // Trường chỉ công bố P25 và P75. Giả định điểm phân bố chuẩn:
    //   μ = (P25 + P75) / 2,   σ = (P75 − P25) / 1,349,   p = Φ((điểm − μ) / σ)
    public static double FromQuartiles(double score, double p25, double p75)
    {
        if (p75 <= p25) throw new ArgumentException("P75 phải lớn hơn P25", nameof(p75));
        // Điểm giữa 2 mốc, mức điển hình của trường
        var mu = (p25 + p75) / 2;
        // Độ rộng hình chuông, Trong phân phối chuẩn, 50% ở giữa (từ P25 đến P75) luôn rộng đúng 1,349σ
        var sigma = (p75 - p25) / IqrInSigma;
        return Phi((score - mu) / sigma);
    }

    // Hàm phân phối chuẩn tích lũy: tỉ lệ nằm bên trái z.
    public static double Phi(double z) => 0.5 * (1 + Erf(z / Math.Sqrt(2)));

    // Abramowitz–Stegun 7.1.26, sai số dưới 1,5e-7 (.NET không có sẵn erf).
    private static double Erf(double x)
    {
        var sign = Math.Sign(x);
        x = Math.Abs(x);
        var t = 1 / (1 + 0.3275911 * x);
        var y = 1 - (((((1.061405429 * t - 1.453152027) * t) + 1.421413741) * t - 0.284496736) * t + 0.254829592) * t * Math.Exp(-x * x);
        return sign * y;
    }

    // Mốc GPA của 6 khoảng đầu (dưới 2,5 · 2,5–2,99 · 3,0–3,24 · 3,25–3,49 · 3,5–3,74 · 3,75–3,99);
    // khoảng thứ 7 là đúng 4,0.
    private static readonly double[] GpaEdges = [2.0, 2.5, 3.0, 3.25, 3.5, 3.75, 4.0];

    // Độ lệch chuẩn GPA giả định khi trường chỉ công bố GPA trung bình.
    public const double DefaultGpaSigma = 0.3;

    // Vị trí theo GPA. Ưu tiên phân bố GPA của trường (7 tỉ lệ từ thấp lên cao);
    // không có thì giả định hình chuông quanh GPA trung bình; không có cả hai thì trả null.
    public static double? FromGpa(
        double gpa, IReadOnlyList<decimal>? distribution, decimal? average, double sigma = DefaultGpaSigma)
    {
        if (distribution is { Count: 7 })
        {
            var total = distribution.Sum(x => (double)x);
            if (total > 0) return FromDistribution(gpa, distribution, total);
        }
        if (average is decimal avg) return Phi((gpa - (double)avg) / sigma);
        return null;
    }

    // Cộng dồn tỉ lệ các khoảng nằm dưới gpa, rồi lấy một phần của khoảng chứa gpa (nội suy tuyến tính).
    private static double FromDistribution(double gpa, IReadOnlyList<decimal> dist, double total)
    {
        double below = 0;
        for (var i = 0; i < 6; i++)
        {
            var share = (double)dist[i] / total;
            if (gpa < GpaEdges[i + 1])
                return below + share * Math.Clamp((gpa - GpaEdges[i]) / (GpaEdges[i + 1] - GpaEdges[i]), 0, 1);
            below += share;
        }
        // GPA từ 4,0 trở lên: thấp hơn mọi khoảng trước, ngang một nửa nhóm đúng 4,0.
        return below + (double)dist[6] / total * 0.5;
    }


}
