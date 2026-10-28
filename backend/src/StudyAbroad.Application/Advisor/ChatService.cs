using StudyAbroad.Application.Profile.Flow;
using StudyAbroad.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace StudyAbroad.Application.Advisor
{
    /// <summary>Giữ trạng thái hội thoại: nạp từ DB gửi cho advisor, nhận trạng thái mới rồi lưu lại.</summary>
    /// nhiệm vụ quản lý phiên chat giữa người dùng và AI Advisor Python.
    // FE(Gửi câu hỏi + SessionId) -> ChatService(Tìm phiên trong DB, lấy StateJson)-> Advisor python (Nhận request, xử lý hội thoại, trả câu trả lời và State mới)-> ChatService(lưu trạng thái mới vào DB, cập nhập bước hiện tại)
    // C4: mỗi lượt gửi kèm hồ sơ đọc từ bản nháp form, chat đổi ô nào thì ghi ô đó vào nháp
    public class ChatService(IAdvisorClient advisor, IChatSessionRepository sessions, IProfileFlowService profileFlow)
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

            // Hồ sơ lấy từ nháp (form sửa thì chat thấy), không tin Profile trình duyệt gửi.
            // Truyền hồ sơ lượt trước để giữ các ô học sinh nói "không có" / "chưa rõ" mà nháp không lưu được
            var flow = await profileFlow.GetFlowStateAsync(userId, ct);
            var profile = ChatProfileMapper.ToChatProfile(flow.Draft, PreviousProfile(state));

            //Gửi request sang Advisor
            var response = await advisor.ChatAsync(
                request with { State = state, Profile = JsonSerializer.SerializeToElement(profile) }, ct);
            var changed = await SaveProfileToDraftAsync(userId, response, ct);

            // C5: chat chỉ sang B3 khi học sinh bấm "Đúng rồi" ở X1, lúc đó lưu hồ sơ chính thức.
            // Lưu lỗi thì giữ nguyên trạng thái cũ (vẫn ở X1) và nháp, báo lỗi để học sinh sửa rồi xác nhận lại
            var nextStep = CurrentStep(response.State);
            if (nextStep == ConfirmedStep && session.CurrentStep != ConfirmedStep)
            {
                var commitError = await CommitProfileAsync(userId, ct);
                if (commitError is not null)
                {
                    response = response with
                    {
                        Answer = $"Hồ sơ chưa lưu được: {commitError} Bạn sửa lại giúp mình rồi xác nhận lại nhé.",
                        State = state,
                        QuickReplies = null,
                        Widget = null,
                    };
                    nextStep = session.CurrentStep;
                }
            }

            //Sau khi advisor phản hồi ==> lưu trạng thái mới
            session.StateJson = response.State?.GetRawText();
            //Lưu bước hiện tại
            session.CurrentStep = nextStep ?? session.CurrentStep;
            session.UpdatedAt = DateTime.UtcNow;

            if (isNew)
                //Lưu phiên mới
                await sessions.AddAsync(session, ct);
            else
                await sessions.SaveAsync(ct);

            //Trả về trình duyệt: có mã phiên, ko lộ trạng thái nội bôk.
            // ProfileChanged để form biết mà nạp lại nháp (D2)
            return response with
            {
                State = null,
                Profile = null,
                ProfileChanged = changed,
                SessionId = session.Id.ToString(),
            };
        }

        private const string ConfirmedStep = "B3";

        private static string? CurrentStep(JsonElement? state) =>
            state is { ValueKind: JsonValueKind.Object } s && s.TryGetProperty("currentStep", out var step)
                ? step.GetString()
                : null;

        // Lưu hồ sơ chính thức; trả câu lỗi tiếng Việt nếu hồ sơ chưa đủ điều kiện, null nếu lưu được
        private async Task<string?> CommitProfileAsync(Guid userId, CancellationToken ct)
        {
            try
            {
                await profileFlow.CommitFlowAsync(userId, null, ct);
                return null;
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                return ex.Message;
            }
        }

        // Hồ sơ chat lượt trước nằm trong state.profile
        private static JsonObject? PreviousProfile(JsonElement? state) =>
            state is { ValueKind: JsonValueKind.Object } s
            && s.TryGetProperty("profile", out var profile)
            && profile.ValueKind == JsonValueKind.Object
                ? JsonNode.Parse(profile.GetRawText()) as JsonObject
                : null;

        // Ghi các khóa chat vừa đổi vào nháp, trả lại danh sách khóa đã ghi.
        // Nháp vượt giới hạn thì bỏ qua: lượt sau nháp gửi lại không có ô đó, chat tự hỏi lại
        private async Task<IReadOnlyList<string>> SaveProfileToDraftAsync(
            Guid userId, AdvisorChatResponse response, CancellationToken ct)
        {
            if (response.ProfileChanged is not { Count: > 0 } changed
                || response.Profile is not { ValueKind: JsonValueKind.Object } profile)
                return [];
            try
            {
                var chatProfile = JsonNode.Parse(profile.GetRawText())!.AsObject();
                await profileFlow.PatchDraftAsync(userId, chatProfile, changed.ToList(), ct);
                return changed;
            }
            catch (ArgumentException)
            {
                return [];
            }
        }
    }
}

