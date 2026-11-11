using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> b)
    {
        b.ToTable("chat_messages", t => t.HasComment(
            "Tin nhắn của cuộc chat: tin của học sinh và tin trả lời của trợ lý. " +
            "Chứa nội dung chat nên là dữ liệu cá nhân (GPA, tài chính, có thể là học sinh vị thành niên): chỉ admin và moderator được xem."));

        b.HasKey(x => x.Id);
        b.HasOne<ChatSession>().WithMany().HasForeignKey(x => x.ChatSessionId).OnDelete(DeleteBehavior.Cascade);
        b.Property(x => x.ChatSessionId).HasComment("Cuộc chat chứa tin này (bảng chat_sessions). Sắp theo created_at ra đúng thứ tự hội thoại.");
        b.Property(x => x.Role).HasMaxLength(16).IsRequired().HasComment("user (học sinh), assistant (trợ lý) hoặc system.");
        b.Property(x => x.Content).IsRequired().HasComment("Nội dung tin nhắn.");
        b.Property(x => x.Step).HasMaxLength(8);
        b.Property(x => x.SourcesJson).HasColumnType("jsonb").HasComment("Nguồn trích dẫn của câu trả lời (title, url).");
        b.Property(x => x.SuggestedCentersJson).HasColumnType("jsonb");
        b.Property(x => x.ReviewStatus).HasMaxLength(16)
            .HasComment(
                "Admin đã xử lý chưa. Chỉ dùng khi outcome là no_answer hoặc failed: open (mới, chưa xem), " +
                "resolved (đã xử lý, ví dụ bổ sung tài liệu), ignored (xem rồi, bỏ qua). Null nếu hệ thống trả lời được.");
        b.Property(x => x.Outcome).HasMaxLength(16)
            .HasComment(
                "Kết quả lượt chat, hệ thống tự ghi và KHÔNG đổi sau đó: answered (trả lời được), " +
                "no_answer (không hiểu hoặc không có dữ liệu), failed (bước AI bị lỗi). Chỉ ở tin trả lời của trợ lý, tin của học sinh để null.");
        b.Property(x => x.MetaJson).HasColumnType("jsonb")
            .HasComment(
                "Tóm tắt lượt (JSON), mỗi thông tin ở tin nó mô tả. Tin của HỌC SINH: slm = nhãn model gán cho câu hỏi (modelIntents, intents, confidence, fallback). " +
                "Tin TRỢ LÝ: local và cloud (số lần gọi, token, thời gian của AI local và AI cloud), turnMs, action, skills, failedSteps. " +
                "Lọc được vào từng khóa, ví dụ meta_json -> 'slm' ->> 'fallback' = 'true'.");
        b.Property(x => x.ReplyToMessageId)
            .HasComment("Tin của học sinh mà tin trợ lý này trả lời, nối chính xác một câu hỏi với một câu trả lời. Chỉ có ở tin trợ lý.");
        b.HasOne<ChatMessage>().WithMany().HasForeignKey(x => x.ReplyToMessageId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(x => x.ReplyToMessageId);
        b.HasIndex(x => new { x.ChatSessionId, x.CreatedAt });
        b.HasIndex(x => x.ReviewStatus);
    }
}
