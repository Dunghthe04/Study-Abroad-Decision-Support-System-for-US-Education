using StudyAbroad.Application.Profile.Flow;
using StudyAbroad.Application.Recommendations;
using StudyAbroad.Domain.Constants;
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
    // analyses: lưu câu đánh giá / lộ trình vào analysis_results (null thì không lưu, dùng trong test cũ)
    // recommender: chạy gợi ý trường khi advisor trả action "recommend" (null thì bỏ qua, dùng trong test cũ)
    // benchmarks: đọc dữ liệu trường để tính mốc GPA / tiếng Anh cho skill ho_so (null thì không gửi mốc, dùng trong test cũ)
    public class ChatService(
        IAdvisorClient advisor, IChatSessionRepository sessions, IProfileFlowService profileFlow,
        IChatAnalysisRepository? analyses = null, IRecommendationRunner? recommender = null,
        IChatBenchmarkRepository? benchmarks = null)
    {
        //Dữ liệu FE gửi lên
        // onEvent: chỉ truyền khi trả lời theo từng ý (stream); mỗi tin tiến độ / câu một ý của advisor gọi hàm này ngay.
        // Mọi bước còn lại (lưu state, nháp, gợi ý trường...) chạy như cũ sau khi advisor trả xong, kết quả cuối giống hệt
        public async Task<AdvisorChatResponse> ChatAsync(
            Guid userId, AdvisorChatRequest request, CancellationToken ct = default,
            Func<AdvisorStreamEvent, Task>? onEvent = null)
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

            // Đã có danh sách trường thì gửi kèm (lấy lần gợi ý mới nhất, kể cả khi học sinh vừa gợi ý lại ở trang khác)
            var (schools, otherSchools) = await LatestSchoolsAsync(userId, ct);

            // Học bạ theo năm gửi riêng, không nằm trong hồ sơ chat (chat chỉ đọc, chỉ form nhập được)
            var transcript = ChatTranscript.From(flow.Draft.Step2) is { } years ? JsonSerializer.SerializeToElement(years) : (JsonElement?)null;

            // Mốc so sánh GPA / tiếng Anh từ dữ liệu trường, cùng bậc học với gợi ý trường
            var marks = await BenchmarksAsync(flow.Draft.Step1?.TargetDegreeLevel, ct);

            //Gửi request sang Advisor
            var toAdvisor = request with
            {
                State = state, Profile = JsonSerializer.SerializeToElement(profile), Schools = schools, Transcript = transcript,
                Benchmarks = marks, OtherSchools = otherSchools, Simulation = null,   // giá trị giả định chỉ .NET dùng, không gửi advisor
            };
            // Nút trên giao diện gửi hành động có cấu trúc (mã trường): bỏ qua advisor, dựng sẵn lượt như thể advisor đã hiểu
            var response = (recommender is not null ? ChatActions.ToResponse(request.Action, state) : null)
                ?? (onEvent is null
                    ? await advisor.ChatAsync(toAdvisor, ct)
                    : await advisor.ChatStreamAsync(toAdvisor, onEvent, ct));
            var changed = await SaveProfileToDraftAsync(userId, response, ct);

            // C5: học sinh bấm "Đúng rồi" ở X1 thì chat rời bảng rà soát sang B3, lúc đó lưu hồ sơ chính thức.
            // Đánh giá chạy luôn trong lượt này nên chat có thể đã sang B4.
            // Lưu lỗi thì giữ nguyên trạng thái cũ (vẫn ở X1) và nháp, báo lỗi để học sinh sửa rồi xác nhận lại
            var nextStep = CurrentStep(response.State);
            if (QuestionOf(state) == ReviewQuestion && IsPastReview(nextStep) && !IsPastReview(session.CurrentStep))
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
                    response = response with { Skills = null }; // câu đánh giá đã bị thay, không lưu
                }
            }

            await SaveAnalysesAsync(userId, response, ct);

            // Lộ trình đại học thẳng: chạy gợi ý trường có sẵn rồi đưa danh sách vào câu trả lời.
            // Chạy sau khi lưu analysis để bản lưu lộ trình không lẫn danh sách trường
            if (response.Action == ChatRecommendation.Action && recommender is not null)
                response = await RecommendAsync(userId, response, ct);

            // Học sinh hỏi về một trường: lời tư vấn lấy từ phần tư vấn từng trường có sẵn, thay câu tạm của advisor
            if (response.Action == ChatExplanation.Action && recommender is not null)
                response = await ExplainAsync(userId, response, ct, onEvent, ExplainBasis(request));

            // Học sinh hỏi "nếu đổi X thì sao": chạy mô phỏng có sẵn, Gemini nói lại ngắn gọn, panel bên phải hiện chi tiết
            if (response.Action == ChatSimulation.Action && recommender is not null)
                response = await SimulateAsync(userId, response, ct);

            // Học sinh hỏi "cần gì để vào trường X": khớp tên trường, rồi hỏi lại hoặc chạy goal-seeking có sẵn
            if (response.Action == ChatGoalSeek.Action && recommender is not null)
                response = await GoalSeekAsync(userId, response, ct, onEvent);

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
                FormStep = FormStepOf(response.State),
                SessionId = session.Id.ToString(),
                Skills = null,
                Action = null,
                ActionData = null,
            };
        }

        private const string ReviewQuestion = "X1";

        // Chạy gợi ý trường (lọc, chấm, chọn, lưu) rồi nối bản tóm tắt vào câu lộ trình, bật hasResults, hiện nút xem đầy đủ.
        // Lỗi thì vẫn trả câu lộ trình kèm lời báo, không làm hỏng cả lượt chat
        private async Task<AdvisorChatResponse> RecommendAsync(Guid userId, AdvisorChatResponse response, CancellationToken ct)
        {
            try
            {
                var saved = await recommender!.CreateAsync(userId, null, ct);
                return response with
                {
                    Answer = $"{response.Answer}\n\n{ChatRecommendation.Summarize(saved)}",
                    State = ChatRecommendation.WithResults(response.State),
                    Widget = new Widget(ChatRecommendation.WidgetType),
                };
            }
            catch (InvalidOperationException ex)
            {
                // Lỗi do hồ sơ ("Chưa...", "Hồ sơ...") thì nói rõ; lỗi kết quả AI thì báo chung
                var reason = ex.Message.StartsWith("Chưa") || ex.Message.StartsWith("Hồ sơ")
                    ? $"Mình chưa tạo được danh sách trường: {ex.Message}"
                    : ChatRecommendation.Invalid;
                return response with { Answer = $"{response.Answer}\n\n{reason}" };
            }
            catch (Exception ex) when (ex is HttpRequestException
                                       || (ex is TaskCanceledException && !ct.IsCancellationRequested))
            {
                return response with { Answer = $"{response.Answer}\n\n{ChatRecommendation.Busy}" };
            }
        }

        // Danh sách trường của lần gợi ý gần nhất; null khi chưa có kết quả (không cần gửi).
        // Lấy từ DB chứ không dựa vào trạng thái chat: học sinh có thể đã gợi ý ở trang Gợi ý trường, chat chưa hề chạy gợi ý.
        // Advisor coi danh sách này là "đã có kết quả"
        // Hồ sơ chưa chọn bậc học thì dùng đại học; chưa có dữ liệu trường thì không gửi
        private async Task<JsonElement?> BenchmarksAsync(string? targetLevel, CancellationToken ct)
        {
            if (benchmarks is null)
                return null;
            var level = StudyLevels.IsValid(targetLevel) ? targetLevel! : StudyLevels.Undergraduate;
            return ChatBenchmarks.From(await benchmarks.ListAsync(level, ct)) is { } marks
                ? JsonSerializer.SerializeToElement(marks)
                : null;
        }

        // Trường đang hiện trong các khối (đếm vị trí được) và các trường đã chấm còn lại (chỉ để khớp tên)
        private async Task<(IReadOnlyList<ChatSchool>? Shown, IReadOnlyList<ChatSchool>? Others)> LatestSchoolsAsync(
            Guid userId, CancellationToken ct)
        {
            if (recommender is null)
                return (null, null);
            var last = await recommender.GetLatestAsync(userId, ct);
            return last is null ? (null, null) : (ChatRecommendation.Schools(last), ChatRecommendation.OtherSchools(last));
        }

        // Giải thích theo cái học sinh đang xem: có giá trị giả định đang kéo thì theo giả định, trừ khi nút "Xem theo hồ sơ thật" yêu cầu hồ sơ thật.
        // Không có giá trị nào thì null (hồ sơ thật)
        private static SimulationOverrides? ExplainBasis(AdvisorChatRequest request) =>
            request.Action?.Basis == "real" || !RecommendationService.HasValues(request.Simulation) ? null : request.Simulation;

        // Viết lời tư vấn một trường (advisor đã chọn trường, mã ở ActionData.code). Lỗi thì báo, không làm hỏng cả lượt chat
        private async Task<AdvisorChatResponse> ExplainAsync(
            Guid userId, AdvisorChatResponse response, CancellationToken ct, Func<AdvisorStreamEvent, Task>? onEvent = null,
            SimulationOverrides? overrides = null)
        {
            var code = ChatExplanation.CodeOf(response.ActionData);
            if (code is null)
                return response with { Answer = ChatExplanation.Failed };
            try
            {
                // Thẻ do code dựng gửi trước để học sinh thấy ngay, rồi mới chờ AI viết lời tư vấn (chậm hơn)
                var basis = ChatSimulation.Describe(overrides);
                var widget = ChatExplanation.WidgetOf(code, await recommender!.ExplainCardAsync(userId, code, ct, overrides), basis);
                if (onEvent is not null)
                    await onEvent(new AdvisorStreamEvent("card", Widget: widget));
                var result = await recommender.ExplainAsync(userId, code, ct, overrides, basis);
                return response with
                {
                    Answer = result.Status == "OK" ? ChatExplanation.Format(result) : ChatExplanation.Failed,
                    Widget = widget,
                };
            }
            catch (InvalidOperationException ex)
            {
                var reason = ex.Message.StartsWith("Chưa")
                    ? $"Mình chưa giải thích được trường này: {ex.Message}"
                    : ChatRecommendation.Invalid;
                return response with { Answer = reason };
            }
            catch (Exception ex) when (ex is HttpRequestException
                                       || (ex is TaskCanceledException && !ct.IsCancellationRequested))
            {
                return response with { Answer = ChatExplanation.Busy };
            }
        }

        // Chạy what-if (advisor đã kiểm tra giá trị, ở ActionData). Gemini lỗi hoặc lời không qua kiểm tra số liệu thì dùng câu mẫu bằng code.
        // Lỗi thì báo, không làm hỏng cả lượt chat
        private async Task<AdvisorChatResponse> SimulateAsync(Guid userId, AdvisorChatResponse response, CancellationToken ct)
        {
            var scenario = ChatSimulation.Parse(response.ActionData);
            if (scenario is null)
                return response with { Answer = ChatSimulation.Failed };
            try
            {
                var result = await recommender!.SimulateAsync(userId, scenario.Overrides, null, ct);
                var note = ChatSimulation.Note(result, scenario);
                return response with { Answer = await WriteNoteAsync(note, ct), Widget = ChatSimulation.WidgetOf(scenario) };
            }
            catch (ArgumentException ex)
            {
                return response with { Answer = $"Mình chưa thử được kịch bản này: {ex.Message}" };
            }
            catch (InvalidOperationException ex)
            {
                var reason = ex.Message.StartsWith("Chưa") || ex.Message.StartsWith("Hồ sơ")
                    ? $"Mình chưa thử được kịch bản này: {ex.Message}"
                    : ChatSimulation.Failed;
                return response with { Answer = reason };
            }
        }

        // Khớp tên trường (chữ thô của SLM). Chắc chắn: chạy goal-seeking. Mơ hồ: hỏi "Ý bạn là...?" kèm nút. Không thấy: báo.
        // Lỗi thì báo, không làm hỏng cả lượt chat
        private async Task<AdvisorChatResponse> GoalSeekAsync(
            Guid userId, AdvisorChatResponse response, CancellationToken ct, Func<AdvisorStreamEvent, Task>? onEvent = null)
        {
            var query = ChatGoalSeek.Parse(response.ActionData);
            if (query is null)
                return response with { Answer = ChatGoalSeek.Failed };
            try
            {
                var code = query.Code;
                if (code is null)
                {
                    var match = await recommender!.FindSchoolsAsync(userId, query.School, ct);
                    switch (match.Kind)
                    {
                        case NameMatchKind.NotFound:
                            return response with
                            {
                                Answer = ChatGoalSeek.NotFound(query.School),
                                QuickReplies = null,
                                Widget = ChatGoalSeek.SchoolPicker(query.Target),
                            };
                        case NameMatchKind.Ambiguous:
                            return response with
                            {
                                Answer = ChatGoalSeek.Clarify(query.School, match.Schools.Count),
                                QuickReplies = ChatGoalSeek.Choices(match.Schools, query.Target),
                                Widget = ChatGoalSeek.SchoolPicker(query.Target),
                            };
                    }
                    code = match.Schools[0].Code;
                }

                var result = await recommender!.GoalSeekAsync(userId, code, query.Target, ct);
                var names = (await recommender.ListSchoolOptionsAsync(userId, ct)).Select(o => o.Name).ToList();
                // Thẻ kết quả do code dựng: gửi ngay để học sinh thấy đáp án, rồi mới chờ Gemini viết lời (chậm hơn nhiều)
                if (onEvent is not null)
                    await onEvent(new AdvisorStreamEvent("card", Widget: ChatGoalSeek.WidgetOf(result)));
                var answer = await WriteGoalNoteAsync(ChatGoalSeek.Note(result, names), result, ct);
                return response with { Answer = answer, Widget = ChatGoalSeek.WidgetOf(result), QuickReplies = null };
            }
            catch (ArgumentException ex)
            {
                return response with { Answer = $"Mình chưa tính được: {ex.Message}" };
            }
            catch (InvalidOperationException ex)
            {
                var reason = ex.Message.StartsWith("Chưa") || ex.Message.StartsWith("Hồ sơ")
                    ? $"Mình chưa tính được: {ex.Message}"
                    : ChatGoalSeek.Failed;
                return response with { Answer = reason };
            }
        }

        // Gemini diễn đạt kết quả goal-seeking. Lỗi, hết lượt hoặc lời không qua kiểm tra thì dùng câu ghép bằng code (cùng dữ kiện)
        private async Task<string> WriteGoalNoteAsync(GoalNoteRequestDto note, SchoolGoalResult result, CancellationToken ct)
        {
            try
            {
                var written = await advisor.WriteGoalNoteAsync(note, ct);
                if (written.Status == "OK" && !string.IsNullOrWhiteSpace(written.Answer))
                    return written.Answer;
            }
            catch (Exception ex) when (ex is HttpRequestException
                                       || (ex is TaskCanceledException && !ct.IsCancellationRequested))
            {
                // Gemini không trả lời được: dùng câu ghép, số liệu vẫn đúng
            }
            return ChatGoalSeek.Summarize(result);
        }

        private async Task<string> WriteNoteAsync(ScenarioNoteRequestDto note, CancellationToken ct)
        {
            try
            {
                var written = await advisor.WriteScenarioNoteAsync(note, ct);
                if (written.Status == "OK" && !string.IsNullOrWhiteSpace(written.Answer))
                    return written.Answer;
            }
            catch (Exception ex) when (ex is HttpRequestException
                                       || (ex is TaskCanceledException && !ct.IsCancellationRequested))
            {
                // Gemini không trả lời được: dùng câu mẫu, số liệu vẫn đúng
            }
            return ChatSimulation.Fallback(note);
        }

        // Skill nào chạy thì lưu câu trả lời vào analysis_results với loại tương ứng
        private static readonly Dictionary<string, string> AnalysisKinds = new()
        {
            ["skill_danh_gia"] = "strengths_weaknesses",
            ["skill_lo_trinh"] = "strategy",
        };

        private async Task SaveAnalysesAsync(Guid userId, AdvisorChatResponse response, CancellationToken ct)
        {
            if (analyses is null || response.Skills is not { Count: > 0 } skills)
                return;
            var resultJson = JsonSerializer.Serialize(new { answer = response.Answer });
            foreach (var skill in skills.Distinct())
                if (AnalysisKinds.TryGetValue(skill, out var kind))
                    await analyses.SaveAsync(userId, kind, resultJson, $"chat:{skill}", ct);
        }

        // Bước sau bảng rà soát: B3 đánh giá, B4 lộ trình
        private static bool IsPastReview(string? step) => step is "B3" or "B4";

        // Mục 7: câu chat đang hỏi nằm ở trang nào của form, để form chuyển theo
        private static readonly Dictionary<string, int> FormSteps = new()
        {
            ["D1"] = 1, ["N2"] = 1, ["N3"] = 1,
            ["D2"] = 2,
            ["E1"] = 3, ["E2"] = 3, ["D3"] = 3,
            ["F1"] = 4, ["F2"] = 4, ["F3"] = 4,
            ["D4"] = 5, ["D5"] = 5,
            ["X1"] = 6,
        };

        private static int? FormStepOf(JsonElement? state)
        {
            if (QuestionOf(state) is not { } id)
                return null;
            if (id.StartsWith("B1.")) return 1;
            return FormSteps.TryGetValue(id, out var step) ? step : null;
        }

        // Câu chat đang hỏi (state.questionId), null nếu không có
        private static string? QuestionOf(JsonElement? state) =>
            state is { ValueKind: JsonValueKind.Object } s
            && s.TryGetProperty("questionId", out var q) && q.ValueKind == JsonValueKind.String
                ? q.GetString()
                : null;

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

