namespace StudyAbroad.Application.Recommendations;

// Kiểm tra khuôn advisor trả về. Sai thì ném lỗi, không sửa số.
public static class CloudResultCheck
{
    public static void EnsureTier(int tier)
    {
        if (tier is < 1 or > 5)
            throw new InvalidOperationException($"Bậc ngoại khóa {tier} nằm ngoài 1–5.");
    }
}
