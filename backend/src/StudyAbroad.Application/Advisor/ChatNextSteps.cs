namespace StudyAbroad.Application.Advisor;

/// <summary>
/// Nút bước tiếp theo dùng chung cho các câu trả lời tư vấn. Nút do code chọn, không để AI bịa.
/// Nút không nêu tên trường: bấm vào thì mở ô tìm trường để học sinh tự chọn (ChatGoalSeek.SchoolPicker).
/// </summary>
public static class ChatNextSteps
{
    public const string ExplainPickLabel = "Giải thích một trường";
    public const string GoalPickLabel = "Cần gì để vào trường em muốn?";
    public const string AnotherExplainLabel = "Giải thích trường khác";
    public const string CenterLabel = "Gợi ý trung tâm tư vấn";
    // Câu gửi qua SLM như mọi câu hỏi khác (cùng chữ với nút trung tâm của advisor)
    public const string CenterText = "Em muốn gặp tư vấn viên";

    public static QuickReply Center { get; } = new(CenterLabel, CenterText);

    // Mở ô chọn trường, chọn xong thì giải thích trường đó
    public static QuickReply ExplainPicker(string label = ExplainPickLabel, IReadOnlyList<string>? suggested = null) =>
        new(label, label, new ChatAction(ChatExplanation.Action, Suggested: suggested));

    // Mở ô chọn trường, chọn xong thì tính cần gì để vào trường đó
    public static QuickReply GoalPicker(IReadOnlyList<string>? suggested = null) =>
        new(GoalPickLabel, GoalPickLabel, new ChatAction(ChatGoalSeek.Action, Suggested: suggested));

    /// <summary>Sau khi giải thích một trường: cần gì để vào trường này, xem trường khác, gặp tư vấn viên.</summary>
    // suggested: vài trường đầu danh sách gần nhất (trừ trường vừa giải thích) để hiện sẵn khi mở ô chọn
    public static IReadOnlyList<QuickReply> AfterExplain(string schoolCode, IReadOnlyList<string>? suggested = null) =>
    [
        new QuickReply("Cần gì để vào trường này?", "Cần gì để vào trường này?", new ChatAction(ChatGoalSeek.Action, schoolCode)),
        ExplainPicker(AnotherExplainLabel, suggested),
        Center,
    ];

    /// <summary>Sau danh sách trường: chọn một trường để xem lý do, hoặc xem cần gì để vào trường em muốn.</summary>
    public static IReadOnlyList<QuickReply> AfterRecommend(IReadOnlyList<string>? suggested = null) =>
        [ExplainPicker(suggested: suggested), GoalPicker(suggested)];

    /// <summary>Thêm nút gặp tư vấn viên vào cuối danh sách nút có sẵn (không thêm lần hai).</summary>
    public static IReadOnlyList<QuickReply> WithCenter(IReadOnlyList<QuickReply>? steps) =>
        steps is null ? [Center] : steps.Any(s => s.Label == CenterLabel) ? steps : [.. steps, Center];
}
