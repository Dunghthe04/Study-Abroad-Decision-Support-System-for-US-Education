using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using StudyAbroad.Application.Recommendations;
using StudyAbroad.Application.Recommendations.Sensitivity;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Admin.Sensitivity;

public interface ISensitivityRunService
{
    /// <summary>
    /// Chạy phân tích độ nhạy trên bộ hồ sơ giả lập v1 và dữ liệu trường hiện tại, rồi lưu lần chạy kèm nhật ký (UC-97).
    /// Chỉ lưu khi chạy xong: đầu vào sai (ArgumentException, AF-1) hoặc lỗi giữa chừng thì không có gì được lưu (AF-3).
    /// Không gọi AI, không đổi tham số đang chạy, không đụng dữ liệu học sinh thật.
    /// </summary>
    Task<SensitivityRunDetail> RunAsync(Guid actorUserId, SensitivityRequest request, CancellationToken ct = default);

    /// <summary>Lịch sử các lần chạy, mới nhất trước.</summary>
    Task<IReadOnlyList<SensitivityRunListRow>> ListAsync(int limit = SensitivityRunService.DefaultListLimit, CancellationToken ct = default);

    /// <summary>Chi tiết một lần chạy; null nếu không có.</summary>
    Task<SensitivityRunDetail?> GetAsync(Guid id, CancellationToken ct = default);
}

public class SensitivityRunService(
    ISensitivityRunRepository runs, IRecommendationRepository recommendations, SensitivityProfileSet? profileSet = null)
    : ISensitivityRunService
{
    public const string RunAction = "sensitivity.run";
    public const string EntityType = "sensitivity_run";
    public const int DefaultListLimit = 50;
    public const int MaxListLimit = 200;

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };

    private readonly SensitivityProfileSet _profiles = profileSet ?? SensitivityProfileSet.LoadV1();

    public async Task<SensitivityRunDetail> RunAsync(Guid actorUserId, SensitivityRequest request, CancellationToken ct = default)
    {
        var watch = Stopwatch.StartNew();
        var schools = await recommendations.ListSchoolsAsync(StudyLevels.Undergraduate, ct);
        // Tính toàn bộ trước, chưa ghi gì. Ném lỗi ở đây thì không có dòng nào trong DB
        var report = SensitivityAnalyzer.Run(_profiles, schools, request);
        ct.ThrowIfCancellationRequested();
        watch.Stop();

        var parameters = SensitivityParameters.All.ToDictionary(p => p.Key, p => p.Get(RecommendationTuning.Current));
        var labels = report.Parameters.Select(p => p.Label).ToList();
        var run = new SensitivityRun
        {
            CreatedByUserId = actorUserId,
            AlgorithmVersion = report.AlgorithmVersion,
            ProfileSetVersion = report.ProfileSetVersion,
            ProfileSetSeed = _profiles.Seed,
            ProfileCount = report.ProfileCount,
            ProfileSetHash = report.ProfileSetHash,
            SchoolCount = report.SchoolCount,
            SchoolDataHash = report.SchoolDataHash,
            RequestJson = JsonSerializer.Serialize(request, Json),
            ParametersJson = JsonSerializer.Serialize(parameters, Json),
            ResultJson = JsonSerializer.Serialize(report.Parameters, Json),
            StableCount = labels.Count(l => l == SensitivityLabels.Stable),
            WatchCount = labels.Count(l => l == SensitivityLabels.Watch),
            SensitiveCount = labels.Count(l => l == SensitivityLabels.Sensitive),
            DurationMs = (int)Math.Min(int.MaxValue, watch.ElapsedMilliseconds)
        };

        // Nhật ký chỉ có mã tham số, mức nhích, điều kiện đo và tóm tắt nhãn; không có dữ liệu cá nhân
        var audit = new AuditLog
        {
            ActorUserId = actorUserId,
            Action = RunAction,
            EntityType = EntityType,
            EntityId = run.Id.ToString(),
            DetailJson = JsonSerializer.Serialize(new
            {
                parameterKeys = request.ParameterKeys,
                steps = request.Steps,
                stableBelow = request.StableBelow,
                watchUpTo = request.WatchUpTo,
                algorithmVersion = run.AlgorithmVersion,
                profileSet = run.ProfileSetVersion,
                schoolCount = run.SchoolCount,
                stable = run.StableCount,
                watch = run.WatchCount,
                sensitive = run.SensitiveCount,
                durationMs = run.DurationMs
            }, Json)
        };
        await runs.AddAsync(run, audit, ct);

        return new SensitivityRunDetail(Summary(run, null, request), request, parameters, report);
    }

    public async Task<IReadOnlyList<SensitivityRunListRow>> ListAsync(int limit = DefaultListLimit, CancellationToken ct = default)
    {
        var rows = await runs.ListAsync(Math.Clamp(limit, 1, MaxListLimit), ct);
        return rows.Select(r => Summary(r.Run, r.CreatedByName, ReadRequest(r.Run))).ToList();
    }

    public async Task<SensitivityRunDetail?> GetAsync(Guid id, CancellationToken ct = default)
    {
        if (await runs.GetAsync(id, ct) is not { } record) return null;
        var run = record.Run;
        var request = ReadRequest(run);
        var results = JsonSerializer.Deserialize<List<SensitivityParameterResult>>(run.ResultJson, Json) ?? [];
        var report = new SensitivityReport(
            run.AlgorithmVersion, run.ProfileSetVersion, run.ProfileSetHash, run.ProfileCount, run.SchoolCount,
            run.SchoolDataHash, request.StableBelow, request.WatchUpTo, results);
        var parameters = JsonSerializer.Deserialize<Dictionary<string, double>>(run.ParametersJson, Json) ?? [];
        return new SensitivityRunDetail(Summary(run, record.CreatedByName, request), request, parameters, report);
    }

    private static SensitivityRequest ReadRequest(SensitivityRun run) =>
        JsonSerializer.Deserialize<SensitivityRequest>(run.RequestJson, Json) ?? new SensitivityRequest([], []);

    private static SensitivityRunListRow Summary(SensitivityRun run, string? createdByName, SensitivityRequest request) => new(
        run.Id, run.CreatedAt, run.CreatedByUserId, createdByName,
        run.AlgorithmVersion, run.ProfileSetVersion, run.ProfileSetHash, run.ProfileCount,
        run.SchoolCount, run.SchoolDataHash,
        request.ParameterKeys, request.Steps,
        run.StableCount, run.WatchCount, run.SensitiveCount, run.DurationMs);
}
