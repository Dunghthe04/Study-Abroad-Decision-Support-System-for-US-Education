using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Recommendations.Sensitivity;

// [UC-85] Một hồ sơ giả lập để thử độ nhạy tham số. Không phải học sinh thật (BA chốt không dùng dữ liệu người dùng).
// Các cột *Level là mức trong lưới (low | mid | high, điểm thi có thêm none) để báo cáo tách theo mức được.
public record SensitivityProfile(
    string Id,
    string GpaLevel, decimal Gpa,
    string TestLevel, int? Sat,
    string BudgetLevel, string BudgetTier, decimal BudgetMinUsd, decimal BudgetMaxUsd,
    string AidLevel, string ScholarshipNeed,
    string EnglishLevel, decimal Ielts,
    int EcTier)
{
    // Đổi thành hồ sơ mà hàm gợi ý đọc được. Phần không thuộc lưới để cố định: đại học, một ngành phổ biến,
    // không lọc bang, không lọc loại trường (ưu tiên 3 thanh kéo [2,2,2] là mặc định của EvaluateWith khi pref null).
    public StudentProfile ToStudentProfile(string major) => new()
    {
        TargetLevel = StudyLevels.Undergraduate,
        IntendedMajor = major,
        OverallGpa = Gpa,
        Sat = Sat,
        Ielts = Ielts,
        ExtracurricularTier = EcTier,
        BudgetTier = BudgetTier,
        BudgetMinUsd = BudgetMinUsd,
        BudgetMaxUsd = BudgetMaxUsd,
        ScholarshipNeed = ScholarshipNeed,
        NeedsScholarship = ProfileConstants.ScholarshipNeeds.NeedsScholarship(ScholarshipNeed),
        PreferredStates = [],
        TargetSchoolType = "all"
    };
}

// [UC-85] Bộ hồ sơ giả lập đóng băng: sinh một lần bằng Generate(seed), lưu thành file JSON nhúng trong dll,
// mọi lần phân tích đọc lại đúng file đó. Hash là SHA-256 của danh sách hồ sơ (dạng chuẩn hóa), nên sửa một con số là lộ.
public record SensitivityProfileSet(string Version, int Seed, string Major, string Hash, IReadOnlyList<SensitivityProfile> Profiles)
{
    public const string V1 = "v1";
    public const int V1Seed = 85;
    // Ngành có mặt ở nhiều trường nhất trong dữ liệu (241/249 trường) và là ngành học sinh thật hay chọn
    public const string V1Major = "Computer and information sciences";
    // Hash của file v1. Đổi file mà không đổi hằng số này thì LoadV1 báo lỗi
    public const string V1Hash = "834622128a681a66608e08bedee63e217fdae1b4db0fe7f605738043a2ec14f2";

    public const string Low = "low", Mid = "mid", High = "high", None = "none";

    private const string V1Resource = "StudyAbroad.Application.Recommendations.Sensitivity.sensitivity-profiles-v1.json";

    private static readonly JsonSerializerOptions Canonical = new(JsonSerializerDefaults.Web) { WriteIndented = false };

    // Khoảng giá trị của từng mức. GPA thang 4, bước 0,01; SAT bước 10; IELTS bước 0,5.
    private static readonly (string Level, int From, int To)[] GpaBands = [(Low, 280, 320), (Mid, 330, 360), (High, 370, 400)];
    private static readonly (string Level, int From, int To)[] SatBands = [(Low, 100, 119), (Mid, 120, 139), (High, 140, 156)];
    private static readonly (string Level, decimal[] Steps)[] IeltsBands =
        [(Low, [5.5m, 6.0m]), (Mid, [6.5m, 7.0m]), (High, [7.5m, 8.0m])];
    // Ngân sách dùng đúng ba mức trên form hồ sơ (bỏ dưới 20k vì gần như không còn trường, bỏ trên 70k vì không có trần để lọc)
    private static readonly (string Level, string Tier, decimal Min, decimal Max)[] BudgetBands =
        [(Low, "tier_20k_35k", 20_000m, 35_000m), (Mid, "tier_35k_50k", 35_000m, 50_000m), (High, "tier_50k_70k", 50_000m, 70_000m)];
    // Nhu cầu học bổng: thấp = không cần, vừa = muốn có, cao = bắt buộc
    private static readonly (string Level, string Need)[] AidBands =
    [
        (Low, ProfileConstants.ScholarshipNeeds.Optional),
        (Mid, ProfileConstants.ScholarshipNeeds.Preferred),
        (High, ProfileConstants.ScholarshipNeeds.Mandatory)
    ];
    private static readonly int[] EcTiers = [1, 3, 5];

    // Lưới đầy đủ GPA(3) x điểm thi(4) x ngân sách(3) x học bổng(3) x tiếng Anh(3) = 324 hồ sơ.
    // Ngoại khóa xoay vòng theo tổng chỉ số các mức (mod 3) thay vì nhân thêm 3 lần: mỗi bậc 1/3/5 vẫn chia đều
    // với từng yếu tố và từng cặp yếu tố, mà bộ hồ sơ giữ ở 324 (BA muốn khoảng 300).
    // Con số cụ thể trong mỗi mức bốc bằng SplitMix64 với seed cố định: cùng seed thì ra cùng bộ trên mọi máy, mọi bản .NET.
    public static SensitivityProfileSet Generate(int seed)
    {
        var random = new SplitMix64((ulong)seed);
        var profiles = new List<SensitivityProfile>();
        for (var g = 0; g < GpaBands.Length; g++)
        for (var t = 0; t <= SatBands.Length; t++)
        for (var b = 0; b < BudgetBands.Length; b++)
        for (var a = 0; a < AidBands.Length; a++)
        for (var e = 0; e < IeltsBands.Length; e++)
        {
            var gpa = GpaBands[g];
            var gpaValue = random.Between(gpa.From, gpa.To) / 100m;
            // t = 3 là mức "không có điểm thi"; vẫn bốc số để dãy ngẫu nhiên không phụ thuộc mức
            var satRaw = t < SatBands.Length ? random.Between(SatBands[t].From, SatBands[t].To) : random.Between(0, 0);
            var budget = BudgetBands[b];
            var english = IeltsBands[e];
            var ielts = english.Steps[random.Between(0, english.Steps.Length - 1)];
            profiles.Add(new SensitivityProfile(
                $"P{profiles.Count + 1:000}",
                gpa.Level, gpaValue,
                t < SatBands.Length ? SatBands[t].Level : None, t < SatBands.Length ? satRaw * 10 : null,
                budget.Level, budget.Tier, budget.Min, budget.Max,
                AidBands[a].Level, AidBands[a].Need,
                english.Level, ielts,
                EcTiers[(g + t + b + a + e) % EcTiers.Length]));
        }
        return new SensitivityProfileSet(V1, seed, V1Major, HashOf(profiles), profiles);
    }

    // Bộ v1 nhúng trong dll, đã kiểm tra hash. Đọc một lần rồi giữ lại.
    public static SensitivityProfileSet LoadV1() => V1Cache.Value;

    private static readonly Lazy<SensitivityProfileSet> V1Cache = new(() =>
    {
        using var stream = typeof(SensitivityProfileSet).Assembly.GetManifestResourceStream(V1Resource)
            ?? throw new InvalidOperationException($"Thiếu file bộ hồ sơ UC-85 ({V1Resource}).");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return Parse(reader.ReadToEnd(), V1Hash);
    });

    // Đọc nội dung file và kiểm tra: hash ghi trong file khớp với hash tính lại, và khớp hash mong đợi trong code
    public static SensitivityProfileSet Parse(string json, string expectedHash)
    {
        var set = JsonSerializer.Deserialize<SensitivityProfileSet>(json, Canonical)
            ?? throw new InvalidDataException("File bộ hồ sơ UC-85 rỗng.");
        var actual = HashOf(set.Profiles);
        if (actual != set.Hash || actual != expectedHash)
            throw new InvalidDataException(
                $"Bộ hồ sơ UC-85 {set.Version} đã bị sửa: hash tính lại {actual}, trong file {set.Hash}, mong đợi {expectedHash}.");
        return set;
    }

    // SHA-256 (hex chữ thường) của danh sách hồ sơ ở dạng JSON gọn. Tính trên đối tượng chứ không trên byte của file,
    // nên đổi xuống dòng CRLF/LF hay thụt lề không làm lệch hash, còn đổi một giá trị thì có.
    public static string HashOf(IReadOnlyList<SensitivityProfile> profiles) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(profiles, Canonical))).ToLowerInvariant();

    // Nội dung file để lưu vào repo: phần đầu thụt lề, mỗi hồ sơ một dòng cho dễ xem diff
    public string ToFileJson()
    {
        var sb = new StringBuilder();
        sb.Append("{\n");
        sb.Append($"  \"version\": {JsonSerializer.Serialize(Version)},\n");
        sb.Append($"  \"seed\": {Seed},\n");
        sb.Append($"  \"major\": {JsonSerializer.Serialize(Major)},\n");
        sb.Append($"  \"hash\": {JsonSerializer.Serialize(Hash)},\n");
        sb.Append("  \"profiles\": [\n");
        for (var i = 0; i < Profiles.Count; i++)
            sb.Append("    ").Append(JsonSerializer.Serialize(Profiles[i], Canonical)).Append(i < Profiles.Count - 1 ? ",\n" : "\n");
        sb.Append("  ]\n}\n");
        return sb.ToString();
    }

    // Bộ sinh số giả ngẫu nhiên SplitMix64: vài dòng, công thức cố định, không phụ thuộc System.Random
    // (System.Random có seed vẫn có thể đổi thuật toán giữa các bản .NET).
    private sealed class SplitMix64(ulong seed)
    {
        private ulong _state = seed;

        private ulong Next()
        {
            var z = _state += 0x9E3779B97F4A7C15UL;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        // Số nguyên trong [from, to] (gồm cả hai đầu)
        public int Between(int from, int to) => from + (int)(Next() % (ulong)(to - from + 1));
    }
}
