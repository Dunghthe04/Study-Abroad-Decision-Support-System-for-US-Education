using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence.Configurations;

public class AiCallConfiguration : IEntityTypeConfiguration<AiCall>
{
    public void Configure(EntityTypeBuilder<AiCall> b)
    {
        b.ToTable("ai_calls", t => t.HasComment(
            "Nhật ký MỖI LẦN gọi AI (Claude hoặc Ollama). Một lượt chat gồm vài lần gọi, mỗi lần một dòng. " +
            "Để admin xem token, tốc độ, lỗi theo tính năng và theo tin nhắn. Chỉ chứa số đo, không chứa nội dung chat."));

        b.HasKey(x => x.Id);
        b.Property(x => x.UserId).HasComment("Học sinh gây ra lần gọi. Null nếu gọi ngoài phiên đăng nhập.");
        b.Property(x => x.Kind).HasMaxLength(16).IsRequired()
            .HasComment("Nguồn gọi: claude (trả phí theo token) hoặc ollama (model chạy trên máy mình, không tốn tiền).");
        b.Property(x => x.Purpose).HasMaxLength(64)
            .HasComment("Việc của lần gọi = tên file skill (slm_intents, skill_tra_loi...). Rỗng với embedding. Dùng để báo cáo token theo tính năng.");
        b.Property(x => x.SkillVersion)
            .HasComment("Số phiên bản ở dòng đầu file skill (ví dụ 3.0). Sửa nội dung skill thì phải tăng số này, nếu không không so sánh được trước và sau.");
        b.Property(x => x.ChatMessageId)
            .HasComment("Tin TRẢ LỜI của trợ lý do lần gọi này tạo ra. Một tin có nhiều lần gọi. Xóa tin thì giữ dòng này (token đã tiêu thật), chỉ bỏ liên kết.");
        b.Property(x => x.Model).HasMaxLength(64)
            .HasComment("Tên model đã gọi, ví dụ claude-haiku-5-5 hoặc usas-slm-v4.");
        b.Property(x => x.CostUsd)
            .HasComment("Chi phí tính bằng USD, tính và lưu lúc gọi. Ollama chạy local nên 0. Claude tính theo bảng giá; model Claude chưa có trong bảng giá thì để trống (chưa biết giá, không phải miễn phí).");
        b.Property(x => x.PromptTokens)
            .HasComment("Token VÀO: phần gửi lên AI. Với Claude, chưa gồm phần đọc từ cache (xem cached_tokens).");
        b.Property(x => x.CompletionTokens).HasComment("Token RA: phần AI viết trả về.");
        b.Property(x => x.CachedTokens)
            .HasComment("Token Claude đọc từ cache, tính tiền khác token thường. Luôn 0 với Ollama.");
        b.Property(x => x.TotalTokens).HasComment("Token vào cộng token ra.");
        b.Property(x => x.LatencyMs).HasComment("Thời gian của lần gọi, tính bằng mili giây.");
        b.Property(x => x.Success).HasComment("False nếu lần gọi lỗi (quá tải, hết thời gian, bị từ chối). Khi đó token bằng 0 và cột error có lý do.");
        b.Property(x => x.Error).HasMaxLength(1000).HasComment("Lý do lỗi ngắn, chỉ có khi success = false.");

        // Xóa tin nhắn thì giữ lại dòng nhật ký (số token đã tiêu thật), chỉ bỏ liên kết
        b.HasOne<ChatMessage>().WithMany().HasForeignKey(x => x.ChatMessageId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(x => x.CreatedAt);                          // màn tổng thể: lọc theo khoảng thời gian rồi nhóm
        b.HasIndex(x => x.ChatMessageId);                      // từ một lượt chat tra ra các lần gọi AI của nó
        b.HasIndex(x => new { x.UserId, x.CreatedAt });        // màn theo người: token của một học sinh theo thời gian
    }
}
