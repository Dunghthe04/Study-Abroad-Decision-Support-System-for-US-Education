using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace StudyAbroad.Application.Advisor;

/// <summary>4 cột của một trường dùng để tính mốc so sánh (đọc từ UniversityOffering).</summary>
public record BenchmarkRow(decimal? AvgGpa4, decimal? MinIelts, decimal? MinToefl, decimal? MinDuolingo);

/// <summary>
/// Mốc so sánh cho skill ho_so (AdvisorChatRequest.Benchmarks), tính từ dữ liệu trường đã thu thập:
/// gpa4 = TB AvgGpa4 (làm tròn 2 chữ số); english = mốc Min* xuất hiện nhiều nhất của từng chứng chỉ
/// (hòa thì lấy mốc thấp hơn). Không trường nào có số thì bỏ khóa đó; không có gì thì null.
/// </summary>
public static class ChatBenchmarks
{
    public static JsonObject? From(IReadOnlyList<BenchmarkRow> rows)
    {
        var result = new JsonObject();
        var gpas = rows.Where(r => r.AvgGpa4 is not null).Select(r => r.AvgGpa4!.Value).ToList();
        if (gpas.Count > 0) result["gpa4"] = Math.Round(gpas.Average(), 2);

        var english = new JsonObject();
        AddMode(english, "ielts", rows.Select(r => r.MinIelts));
        AddMode(english, "toefl", rows.Select(r => r.MinToefl));
        AddMode(english, "duolingo", rows.Select(r => r.MinDuolingo));
        if (english.Count > 0) result["english"] = english;
        return result.Count > 0 ? result : null;
    }

    // Mốc nhiều trường dùng nhất; hòa thì lấy mốc thấp hơn để kết quả luôn cố định
    private static void AddMode(JsonObject target, string key, IEnumerable<decimal?> values)
    {
        var mode = values.OfType<decimal>()
            .GroupBy(v => v)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key)
            .Select(g => (decimal?)g.Key)
            .FirstOrDefault();
        if (mode is not null) target[key] = mode;
    }
}
