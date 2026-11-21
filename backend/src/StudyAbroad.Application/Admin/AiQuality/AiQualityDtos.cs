namespace StudyAbroad.Application.Admin.AiQuality;

/// <summary>
/// Một lượt chat đã rút gọn đủ để tính báo cáo chất lượng AI (UC-90). Tạo từ tin trả lời của trợ lý (chat_messages) cùng tin hỏi của nó.
/// Outcome là cột chat_messages.outcome (answered, no_answer, refused, failed); null hoặc mã lạ thì báo cáo tính là "chưa phân loại".
/// TurnMs: thời gian thật của cả lượt (meta_json.turnMs); null nếu dòng không có.
/// Feature: loại ý SLM đã chạy đầu tiên của lượt (meta_json.skills[0], ví dụ knowledge, profile_review); null nếu không có.
/// FailedSteps: các bước AI hỏng của lượt (meta_json.failedSteps). SlmFallback: SLM hai lần đều sai nên dùng kết quả dự phòng (tin hỏi, slm.fallback).
/// UsedFallback: lượt có ít nhất một lần gọi AI phải dùng đường dự phòng (ai_calls.fallback_used).
/// </summary>
public record AiQualityTurn(
    DateTime CreatedAt,
    string? Outcome,
    long? TurnMs,
    string? Feature,
    IReadOnlyList<string> FailedSteps,
    bool SlmFallback,
    bool UsedFallback);

/// <summary>
/// Số lượt theo kết quả. Answered + NoAnswer + Refused + Failed + Unclassified luôn bằng Total, nên các tỉ lệ cộng lại đúng 100%
/// (trừ khi Total bằng 0: mọi tỉ lệ là 0, không chia cho 0). Unclassified là tin cũ chưa có outcome.
/// </summary>
public record OutcomeCounts(int Total, int Answered, int NoAnswer, int Refused, int Failed, int Unclassified)
{
    public double AnsweredRate => Rate(Answered);
    public double NoAnswerRate => Rate(NoAnswer);
    public double RefusedRate => Rate(Refused);
    public double FailedRate => Rate(Failed);
    public double UnclassifiedRate => Rate(Unclassified);

    private double Rate(int part) => Total == 0 ? 0 : (double)part / Total;
}

/// <summary>
/// Thời gian phản hồi của cả lượt. Measured là số lượt có đo được (có turnMs); AvgMs, P50Ms, P95Ms là null khi Measured bằng 0.
/// Phân vị theo cách "thứ hạng gần nhất": sắp tăng dần, lấy phần tử thứ ceil(p × n) (đếm từ 1). Chỉ một lượt thì cả hai bằng thời gian lượt đó.
/// </summary>
public record LatencySummary(int Measured, double? AvgMs, long? P50Ms, long? P95Ms);

/// <summary>Một ngày (UTC) của biểu đồ. Ngày không có lượt nào vẫn có dòng (toàn số 0) để biểu đồ liên tục.</summary>
public record AiQualityDay(DateOnly Date, OutcomeCounts Counts, LatencySummary Latency);

/// <summary>Theo tính năng (loại ý SLM đã chạy đầu tiên). Lượt không có thông tin này vào nhóm "other".</summary>
public record AiQualityFeature(string Feature, OutcomeCounts Counts, LatencySummary Latency);

/// <summary>Số lượt có bước AI này bị hỏng (mỗi lượt chỉ đếm một lần cho mỗi bước).</summary>
public record FailedStepCount(string Step, int Turns);

/// <summary>
/// Báo cáo chất lượng AI trong khoảng From đến To (tính cả hai đầu, theo ngày UTC).
/// SlmFallbackTurns: lượt SLM phải dùng kết quả dự phòng. FallbackTurns: lượt có lần gọi AI dùng đường dự phòng.
/// </summary>
public record AiQualityReport(
    DateOnly From,
    DateOnly To,
    OutcomeCounts Total,
    LatencySummary Latency,
    int SlmFallbackTurns,
    int FallbackTurns,
    IReadOnlyList<AiQualityDay> ByDay,
    IReadOnlyList<AiQualityFeature> ByFeature,
    IReadOnlyList<FailedStepCount> FailedSteps);
