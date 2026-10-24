using StudyAbroad.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace StudyAbroad.Application.Advisor
{
    /// <summary>Giữ trạng thái hội thoại: nạp từ DB gửi cho advisor, nhận trạng thái mới rồi lưu lại.</summary>
    /// nhiệm vụ quản lý phiên chat giữa người dùng và AI Advisor Python.
    // FE(Gửi câu hỏi + SessionId) -> ChatService(Tìm phiên trong DB, lấy StateJson)-> Advisor python (Nhận request, xử lý hội thoại, trả câu trả lời và State mới)-> ChatService(lưu trạng thái mới vào DB, cập nhập bước hiện tại)
    public class ChatService(IAdvisorClient advisor, IChatSessionRepository sessions)
    {
        //Dữ liệu FE gửi lên
        public async Task<AdvisorChatResponse> ChatAsync(Guid userId, AdvisorChatRequest request, CancellationToken ct = default)
        {
            // SessionId do trình duyệt gửi chỉ dùng để tìm phiên; State của trình duyệt bị bỏ, luôn lấy từ DB
            var isNew = false;
            ChatSession? session = null;
            //Chuyển đổi SessionId về dạng Guid sau đó tìm database
            if (Guid.TryParse(request.SessionId, out var sessionId))
                session = await sessions.FindAsync(sessionId, userId, ct);

            //Nếu chưa có phiên -> tạo mới
            if (session is null)
            {
                session = new ChatSession { UserId = userId, StudyLevel = request.StudyLevel };
                isNew = true;
            }
            JsonElement? state = null;
            if (!string.IsNullOrEmpty(session.StateJson))
                state = JsonDocument.Parse(session.StateJson).RootElement.Clone();

            //Gửi request sang Advisor
            var response = await advisor.ChatAsync(request with { State = state }, ct);
            //Sau khi advisor phản hồi ==> lưu trạng thái mới
            session.StateJson = response.State?.GetRawText();
            if (response.State is { ValueKind: JsonValueKind.Object } s && s.TryGetProperty("currentStep", out var step))
                //Lưu bước hiện tại
                session.CurrentStep = step.GetString();
            session.UpdatedAt = DateTime.UtcNow;

            if (isNew)
                //Lưu phiên mới
                await sessions.AddAsync(session, ct);
            else
                await sessions.SaveAsync(ct);

            //Trả về trình duyệt: có mã phiên, ko lộ trạng thái nội bôk
            return response with { State = null, SessionId = session.Id.ToString() };
        }
    }
}

