using System.Text.Json;

namespace StudyAbroad.Application.Recommendations
{
    //Số lượng trường tối đa theo từng loại (reach, match, safety) trong gợi ý trường
    public record PerCategoryLimits(int Reach = 3, int Match = 5, int Safety = 4);

    //Cấu hình gợi ý trường, đọc từ AppSetting.ValueJson với Key = "recommend.weights".
    //Các tham số của công thức mới (file GOI_Y_TRUONG_TONG_QUAN) sẽ thêm vào đây khi viết lại.
    public class RecommendSettings
    {
        public const string SettingKey = "recommend.weights";
        public decimal BudgetTolerance { get; set; } = 0.10m;   // Trần ngân sách = ngân sách × (1 + 10%)
        public int MaxResults { get; set; } = 12;               // Số lượng trường tối đa trong gợi ý trường
        public int MaxCandidates { get; set; } = 40;            // Số trường tối đa gửi AI chấm sau lọc cứng (40 trường ≈ 5 lô ≈ 2,5 phút)

        public PerCategoryLimits PerCategory { get; set; } = new();
        public bool AiEnabled { get; set; } = true;
        public int AiTimeoutSeconds { get; set; } = 120;

        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        public static RecommendSettings Parse(string? json)
        {
            if (string.IsNullOrEmpty(json)) return new RecommendSettings();
            try
            {
                return JsonSerializer.Deserialize<RecommendSettings>(json, JsonOptions) ?? new RecommendSettings();
            }
            catch (JsonException)
            {
                return new RecommendSettings();
            }
        }
    }
}
