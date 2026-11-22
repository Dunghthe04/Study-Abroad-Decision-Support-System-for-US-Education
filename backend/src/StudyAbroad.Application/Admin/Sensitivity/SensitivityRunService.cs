using System.Diagnostics;
using System.Text.Json;
using StudyAbroad.Application.Audit;
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

    /// <summary>
    /// So lần chạy id với lần chạy withId; withId null thì so với lần chạy ngay trước nó. Không có lần trước thì trả so sánh rỗng (AF-5).
    /// Null nếu id hoặc withId không tồn tại. Hai lần khác điều kiện (thuật toán, bộ hồ sơ, dữ liệu trường, ngưỡng nhãn) thì có cảnh báo.
    /// </summary>
    Task<SensitivityComparison?> CompareAsync(Guid id, Guid? withId, CancellationToken ct = default);

    /// <summary>Danh mục tham số được phép phân tích, kèm giá trị đang chạy, để giao diện hiện ô chọn.</summary>
    IReadOnlyList<SensitivityParameterInfo> Parameters();

    /// <summary>File CSV (chỉ số tổng hợp) của một lần chạy; ghi nhật ký người tải TRƯỚC khi trả file. Null nếu không có lần chạy.</summary>
    Task<SensitivityCsvFile?> ExportCsvAsync(Guid actorUserId, Guid id, CancellationToken ct = default);
}

public sealed record SensitivityCsvFile(string FileName, byte[] Content);

// [UC-85] Một tham số trong danh mục: mã, tên, loại và giá trị đang chạy thật.
public sealed record SensitivityParameterInfo(string Key, string Name, SensitivityKind Kind, double Current);

public class SensitivityRunService(
    ISensitivityRunRepository runs, IRecommendationRepository recommendations, IAuditLogRepository audits,
    SensitivityProfileSet? profileSet = null)
    : ISensitivityRunService
{
    public const string RunAction = "sensitivity.run";
    public const string ExportAction = "sensitivity.export_csv";
    public const string EntityType = "sensitivity_run";
    public const int DefaultListLimit = 50;
    public const int MaxListLimit = 200;

    // Kiểu tham số (SensitivityKind) tự ghi dạng chữ nhờ converter gắn trên enum
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

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

        // Đọc lại để có tên người chạy (bảng users); không đọc được thì vẫn trả kết quả vừa tính
        var name = (await runs.GetAsync(run.Id, ct))?.CreatedByName;
        return new SensitivityRunDetail(Summary(run, name, request), request, parameters, report);
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

    public async Task<SensitivityComparison?> CompareAsync(Guid id, Guid? withId, CancellationToken ct = default)
    {
        if (await GetAsync(id, ct) is not { } run) return null;
        if (withId == id) throw new ArgumentException("Cần chọn hai lần chạy khác nhau để so sánh.");

        var otherId = withId ?? await runs.FindPreviousIdAsync(id, ct);
        if (otherId is not Guid other) return SensitivityComparer.Alone(run);
        return await GetAsync(other, ct) is { } otherRun ? SensitivityComparer.Compare(run, otherRun) : null;
    }

    public async Task<SensitivityCsvFile?> ExportCsvAsync(Guid actorUserId, Guid id, CancellationToken ct = default)
    {
        if (await GetAsync(id, ct) is not { } run) return null;
        // Không bắt lỗi: ghi nhật ký lỗi thì ném lên, file không được trả
        await audits.AddAsync(new AuditLog
        {
            ActorUserId = actorUserId,
            Action = ExportAction,
            EntityType = EntityType,
            EntityId = id.ToString(),
            DetailJson = JsonSerializer.Serialize(new { parameterKeys = run.Request.ParameterKeys }, Json)
        }, ct);
        return new SensitivityCsvFile(SensitivityCsv.FileName(run), SensitivityCsv.Build(run));
    }

    public IReadOnlyList<SensitivityParameterInfo> Parameters() =>
        SensitivityParameters.All.Select(p => new SensitivityParameterInfo(p.Key, p.Name, p.Kind, p.Get(RecommendationTuning.Current))).ToList();

    private static SensitivityRequest ReadRequest(SensitivityRun run) =>
        JsonSerializer.Deserialize<SensitivityRequest>(run.RequestJson, Json) ?? new SensitivityRequest([], []);

    private static SensitivityRunListRow Summary(SensitivityRun run, string? createdByName, SensitivityRequest request) => new(
        run.Id, run.CreatedAt, run.CreatedByUserId, createdByName,
        run.AlgorithmVersion, run.ProfileSetVersion, run.ProfileSetHash, run.ProfileCount,
        run.SchoolCount, run.SchoolDataHash,
        request.ParameterKeys, request.Steps,
        run.StableCount, run.WatchCount, run.SensitiveCount, run.DurationMs);
}
