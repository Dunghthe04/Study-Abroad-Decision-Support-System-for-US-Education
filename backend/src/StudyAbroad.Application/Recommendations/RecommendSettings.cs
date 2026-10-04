using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace StudyAbroad.Application.Recommendations
{
    //Số lượng trường tối đa theo từng loại (reach, match, safety) trong gợi ý trường (#6)
    public record PerCategoryLimits(int Reach =3, int Match=5 , int Safety = 4);

    //Cấu hình gợi ý trường, đọc từ AppSetting.ValueJson với Key = "recommend.weights"
    //Đổi trong db là có hiệu lực ngay, không cần sửa code
    public class RecommendSettings
    {

        public const string SettingKey = "recommend.weights";
        public decimal GpaBand { get; set; } = 0.3m; // Ngưỡng GPA để phân loại Reach, Match, Safety
        public decimal BudgetTolerance { get; set; } = 0.10m;// Cho vượt 10% chi phí
        public decimal ExtracurricularWeight { get; set; } = 0.5m; //Trọng số hoạt động ngoại khóa
        public decimal FinanceWeight { get; set; } = 1.0m;//Trọng số tài chính
        public int MaxResults { get; set; } = 12;//Số lượng trường tối đa trong gợi ý trường
        public PerCategoryLimits PerCategory { get; set; } = new ();

        // Đọc Json từ AppSetting.ValueJson với Key = "recommend.weights"
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        public static RecommendSettings Parse(string? json)
        {
            if (string.IsNullOrEmpty(json)) return new RecommendSettings();
            try
            {
                //Chuyển json về c#
                return JsonSerializer.Deserialize<RecommendSettings>(json, JsonOptions) ?? new RecommendSettings();
            }
            catch (JsonException)
            {

                return new RecommendSettings();
            }
        }

    }
}
