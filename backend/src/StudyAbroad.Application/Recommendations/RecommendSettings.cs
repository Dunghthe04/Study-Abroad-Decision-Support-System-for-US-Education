using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace StudyAbroad.Application.Recommendations
{
    //Số lượng trường tối đa theo từng loại (reach, match, safety) trong gợi ý trường
    public record PerCategoryLimits(int Reach =3, int Match=5 , int Safety = 4);

    //trọng số SAW 4 tiêu chí,tổng lại thành 1
    public record SawWeights(decimal Academic = 0.4m , decimal Finance =0.3m, decimal English=0.1m, decimal Extracurricular =0.2m);

    //Cấu hình gợi ý trường, đọc từ AppSetting.ValueJson với Key = "recommend.weights"
    public class RecommendSettings
    {

        public const string SettingKey = "recommend.weights";
        public decimal GpaBand { get; set; } = 0.3m; // Ngưỡng GPA để phân loại Reach, Match, Safety
        public decimal BudgetTolerance { get; set; } = 0.10m;// Cho vượt 10% chi phí
        public SawWeights Weights { get; set; } = new();//Trọng số SAW: học thuật, tài chính, tiếng Anh, ngoại khóa

        public int MaxResults { get; set; } = 12;//Số lượng trường tối đa trong gợi ý trường
        public PerCategoryLimits PerCategory { get; set; } = new ();
        //Bậc học tuyển sinh mở: trường không có số liệu học thuật thì xếp Safety thay vì "chưa đủ dữ liệu"
        public List<string> OpenAdmissionLevels { get; set; } = ["community_college"];
        public decimal MissingValue { get; set; } = 0.5m;   //Giá trị trung tính khi TRƯỜNG thiếu dữ liệu
        public bool AiEnabled { get; set; } = true;          //Tắt thì chỉ trả kết quả CRM + giải thích soạn sẵn
        public int AiTimeoutSeconds { get; set; } = 120;      //LLM quá thời gian này thì trả kết quả CRM

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
