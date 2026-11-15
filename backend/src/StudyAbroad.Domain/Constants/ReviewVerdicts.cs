namespace StudyAbroad.Domain.Constants;

/// <summary>Người duyệt có đồng ý với nhãn ý mà SLM gán cho câu hỏi không (cột review_verdicts.label_verdict).</summary>
public static class ReviewLabelVerdicts
{
    public const string Correct = "correct";
    public const string Wrong = "wrong";

    public static readonly string[] All = [Correct, Wrong];
}

/// <summary>Người duyệt đánh giá câu trả lời của hệ thống (cột review_verdicts.answer_verdict).</summary>
public static class ReviewAnswerVerdicts
{
    /// <summary>Trả lời đúng.</summary>
    public const string Correct = "correct";
    /// <summary>Trả lời sai (bịa, lạc đề, sai số liệu...). Khi chọn giá trị này thì ghi thêm nguyên nhân.</summary>
    public const string Wrong = "wrong";
    /// <summary>Không đánh giá được vì hệ thống thiếu dữ liệu hoặc kiến thức để trả lời.</summary>
    public const string MissingData = "missing_data";
    /// <summary>Câu hỏi ngoài phạm vi của hệ thống.</summary>
    public const string OutOfScope = "out_of_scope";

    public static readonly string[] All = [Correct, Wrong, MissingData, OutOfScope];
}

/// <summary>Vì sao câu trả lời sai (cột review_verdicts.cause), chỉ ghi khi answer_verdict là wrong.</summary>
public static class ReviewCauses
{
    /// <summary>Hiểu sai ý của học sinh (thường do nhãn ý sai).</summary>
    public const string MisreadIntent = "misread_intent";
    /// <summary>Thiếu kiến thức hoặc dữ liệu.</summary>
    public const string MissingKnowledge = "missing_knowledge";
    /// <summary>Bịa thông tin hoặc số liệu không có trong nguồn.</summary>
    public const string Hallucination = "hallucination";
    /// <summary>Lỗi kỹ thuật (bước AI lỗi, định dạng sai...).</summary>
    public const string Technical = "technical";

    public static readonly string[] All = [MisreadIntent, MissingKnowledge, Hallucination, Technical];
}

/// <summary>Sáu loại ý SLM gán cho câu hỏi (cùng danh sách với advisor). Dùng để kiểm tra nhãn đúng mà người duyệt chọn.</summary>
public static class SlmIntentTypes
{
    public static readonly string[] All =
        ["collect_profile", "profile_review", "scenario", "knowledge", "center_referral", "out_of_scope"];
}
