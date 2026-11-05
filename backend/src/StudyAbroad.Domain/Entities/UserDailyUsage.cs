using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Theo dõi hạn mức hàng ngày của người dùng (FE-19: UC-77).</summary>
public class UserDailyUsage : BaseEntity
{
    public Guid UserId { get; set; }
    public DateOnly UsageDate { get; set; }
    public int AiChatCount { get; set; } = 0;
    public int LeadsCount { get; set; } = 0;
}
