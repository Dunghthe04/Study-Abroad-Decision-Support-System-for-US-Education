namespace StudyAbroad.Application.Advisor;

/// <summary>
/// "Giỏ chung" gom số đo của MỌI lần gọi advisor trong một yêu cầu (một lượt chat có thể gọi nhiều lần: chat, giải thích trường,
/// what-if, goal-seek, so sánh). Đăng ký theo yêu cầu (scoped) nên mỗi lượt có giỏ riêng. Cuối lượt, ChatService lấy cả giỏ để
/// tính outcome, meta_json và ghi ai_calls. An toàn khi nhiều lần gọi chạy song song.
/// </summary>
public interface IAiUsageCollector
{
    /// <summary>Bỏ số đo của một lần gọi vào giỏ. Null hoặc rỗng (advisor cũ không gửi aiUsage) thì bỏ qua.</summary>
    void Add(IReadOnlyList<AiUsageDto>? usage);

    /// <summary>Bản sao các số đo đã gom, theo thứ tự thêm vào.</summary>
    IReadOnlyList<AiUsageDto> Snapshot();

    /// <summary>Đổ giỏ, gọi đầu mỗi lượt để không lẫn số đo của lượt trước.</summary>
    void Clear();
}

public class AiUsageCollector : IAiUsageCollector
{
    private readonly List<AiUsageDto> calls = [];
    private readonly object gate = new();

    public void Add(IReadOnlyList<AiUsageDto>? usage)
    {
        if (usage is not { Count: > 0 })
            return;
        lock (gate)
            calls.AddRange(usage);
    }

    public IReadOnlyList<AiUsageDto> Snapshot()
    {
        lock (gate)
            return calls.ToList();
    }

    public void Clear()
    {
        lock (gate)
            calls.Clear();
    }
}
