using StudyAbroad.Application.Profile.Academic;
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
    // gradeScale: bảng quy đổi grade_scale.10 trong DB cho what-if GPA thang 10 (null thì bảng mặc định, dùng trong test cũ)
    public class ChatService(
        IAdvisorClient advisor, IChatSessionRepository sessions, IProfileFlowService profileFlow,
        IChatAnalysisRepository? analyses = null, IRecommendationRunner? recommender = null,
        IChatBenchmarkRepository? benchmarks = null, IGradeScaleProvider? gradeScale = null)
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

            // Bảng quy đổi điểm thang 10 sang GPA thang 4 trong DB (moderator sửa) để advisor đổi GPA giống .NET và form.
            // Luôn lấy từ server, không tin bảng trình duyệt gửi; không có provider thì null, advisor dùng bảng mặc định
            var scaleRules = gradeScale is null ? null : (await gradeScale.GetAsync(ct)).Rules;

            //Gửi request sang Advisor
            var toAdvisor = request with
            {
                State = state, Profile = JsonSerializer.SerializeToElement(profile), Schools = schools, Transcript = transcript,
                Benchmarks = marks, OtherSchools = otherSchools, Simulation = null,   // giá trị giả định chỉ .NET dùng, không gửi advisor
                GradeScale = scaleRules,
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

            // SLM đôi khi xếp câu hỏi số liệu một trường ("UCLA xếp hạng bao nhiêu", "MIT cần IELTS bao nhiêu") vào giải thích hoặc goal-seeking:
            // câu tự gõ (không phải nút) mà chỉ hỏi số liệu thì chuyển sang trả lời số liệu
            if (request.Action is null && recommender is not null
                && response.Action is ChatExplanation.Action or ChatGoalSeek.Action
                && request.Messages.LastOrDefault(m => m.Role == "user")?.Content is { } typed
                && ChatSchoolInfo.Reroute(response, typed) is { } rerouted)
                response = rerouted;

            // Học sinh hỏi về một trường: lời tư vấn lấy từ phần tư vấn từng trường có sẵn, thay câu tạm của advisor
            if (response.Action == ChatExplanation.Action && recommender is not null)
                response = await ExplainAsync(userId, response, ct, onEvent, ExplainBasis(request));

            // Học sinh hỏi "nếu đổi X thì sao": chạy mô phỏng có sẵn, Gemini nói lại ngắn gọn, panel bên phải hiện chi tiết
            if (response.Action == ChatSimulation.Action && recommender is not null)
                response = await SimulateAsync(userId, response, ct);

            // Học sinh hỏi "cần gì để vào trường X": khớp tên trường, rồi hỏi lại hoặc chạy goal-seeking có sẵn
            if (response.Action == ChatGoalSeek.Action && recommender is not null)
                response = await GoalSeekAsync(userId, response, ct, onEvent);

            // Học sinh hỏi tìm trường theo điều kiện ("trường nào ở California có học bổng"): lọc bằng số liệu thật, sắp theo xếp hạng hoặc theo ý học sinh
            if (response.Action == ChatSchoolInfo.SearchAction && recommender is not null)
                response = await AnswerSchoolSearchAsync(userId, response, ct);

            // Học sinh hỏi so sánh sự thật của từ hai trường ("học phí BU và NYU chênh nhau bao nhiêu"): lấy số liệu từng trường, dựng bảng so sánh
            if (response.Action == ChatSchoolInfo.CompareAction && recommender is not null)
                response = await AnswerSchoolCompareAsync(userId, response, ct);

            // Học sinh hỏi thông tin một trường ("học phí Harvard bao nhiêu"): lấy số thật từ cơ sở dữ liệu, không có mới giữ câu của kho kiến thức
            if (response.Action == ChatSchoolInfo.Action && recommender is not null)
                response = await AnswerSchoolInfoAsync(userId, response, ct);

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
                AiUsage = null,   // số token là việc nội bộ, học sinh không thấy
                Slm = null,       // nhãn model gán chứa chữ của học sinh, chỉ lưu cho admin
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
                    QuickReplies = ChatNextSteps.AfterRecommend(Suggested(ChatRecommendation.Schools(saved))),
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

        // Vài trường đầu danh sách gợi ý để hiện sẵn trong ô chọn trường (bỏ trường vừa nói tới nếu có)
        private static IReadOnlyList<string>? Suggested(IReadOnlyList<ChatSchool>? shown, string? except = null)
        {
            var codes = (shown ?? []).Select(s => s.Code)
                .Where(c => !string.Equals(c, except, StringComparison.OrdinalIgnoreCase))
                .Take(ChatActions.MaxSuggested).ToList();
            return codes.Count == 0 ? null : codes;
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
                    QuickReplies = result.Status == "OK"
                        ? ChatNextSteps.AfterExplain(code, Suggested((await LatestSchoolsAsync(userId, ct)).Shown, code))
                        : null,
                };
            }
            catch (InvalidOperationException ex)
            {
                // Trường không có trong danh sách (thường vì bị bộ lọc cứng loại): thay vì báo lỗi, nói đúng lý do bằng thẻ goal-seeking
                if (ex.Message.StartsWith("Chưa"))
                {
                    var data = JsonSerializer.SerializeToElement(new { code });
                    var why = await GoalSeekAsync(userId, response with { ActionData = data }, ct, onEvent);
                    if (why.Widget is not null)
                        return why;
                }
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
            var scale = gradeScale is null ? null : await gradeScale.GetAsync(ct);
            var scenario = ChatSimulation.Parse(response.ActionData, scale);
            if (scenario is null)
                return response with { Answer = ChatSimulation.Failed };
            try
            {
                // Ưu tiên mới tính từ ưu tiên của lần gợi ý đã lưu (mức học sinh không nhắc thì giữ nguyên)
                IReadOnlyList<int>? basePref = null, newPref = null;
                if (scenario.Priorities is { } asked)
                {
                    var latest = await recommender!.GetLatestAsync(userId, ct);
                    basePref = ChatSimulation.BasePref(latest?.CriteriaJson);
                    newPref = ChatSimulation.MergePref(basePref, asked);
                }
                var result = await recommender!.SimulateAsync(userId, scenario.Overrides, newPref, ct);
                var parts = new List<string>();
                string? beforeItems = null;
                if (scenario.HasProfileValues)
                    parts.Add(await WriteNoteAsync(ChatSimulation.Note(result, scenario), ct));
                if (scenario.Priorities is { } priorities && basePref is not null && newPref is not null)
                {
                    // So hai danh sách cùng hồ sơ giả định, chỉ khác ưu tiên: thứ tự đổi là do ưu tiên
                    var before = await recommender.SimulateAsync(userId, scenario.Overrides, basePref, ct);
                    beforeItems = before.ItemsJson;
                    parts.Add(ChatSimulation.PriorityNote(basePref, newPref, priorities, before.ItemsJson, result.ItemsJson));
                }
                // Bước tiếp theo của quy trình: hiểu vì sao đổi, rồi xem cần gì để vào trường khó
                var steps = ChatSimulation.NextSteps(result, beforeItems);
                return response with
                {
                    Answer = string.Join("\n\n", parts),
                    Widget = ChatSimulation.WidgetOf(scenario, newPref),
                    QuickReplies = steps.Count == 0 ? null : ChatNextSteps.WithCenter(steps),   // không có bước nào thì không có nút nào
                };
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

        // Hỏi thông tin một trường. Advisor xếp câu vào loại kiến thức và đã chạy kho kiến thức (câu trả lời ở KbAnswer); ở đây thử cơ sở dữ liệu trước.
        // Câu không hỏi mục nào có số liệu (học phí, học bổng, tiếng Anh...), hoặc không tìm được trường nào chắc chắn: giữ nguyên câu của kho (kiến thức chung).
        // Lỗi cơ sở dữ liệu cũng giữ câu của kho, không làm hỏng cả lượt chat.
        private async Task<AdvisorChatResponse> AnswerSchoolInfoAsync(Guid userId, AdvisorChatResponse response, CancellationToken ct)
        {
            var query = ChatSchoolInfo.Parse(response.ActionData);
            if (query is null)
                return response;
            // Nút trên giao diện (chọn trường khi tên mơ hồ) gửi thẳng mã trường, không có câu trả lời của kho
            var fromButton = query.Code is not null && query.KbAnswer is null;
            try
            {
                // Mục hỏi: tin điều model đã đọc và trả; chỉ khi model không trả mục nào mới đoán bằng từ khóa trong câu
                var modelFields = ChatSchoolInfo.FieldsFromModel(query.SlmFields);
                var fields = modelFields.Count > 0 ? modelFields : ChatSchoolInfo.FieldsOf(query.Question);
                var unstored = ChatSchoolInfo.ModelSaysUnstored(query.SlmFields) || ChatSchoolInfo.AsksUnstored(query.Question);
                // Không nhận ra mục nào: kho kiến thức có nguồn thật thì giữ câu của kho. Kho không có mà biết đúng trường thì tóm tắt kèm nút chọn mục
                var noTopic = !fromButton && fields.Count == 0 && !unstored;
                if (noTopic && query.KbFound)
                    return response;

                var code = query.Code;
                if (code is null && query.School is { } typed)
                {
                    var match = await recommender!.FindSchoolsAsync(userId, typed, ct);
                    if (match.Kind == NameMatchKind.Ambiguous)
                    {
                        // Mơ hồ: hỏi lại, mỗi nút chạy thẳng theo mã trường và giữ câu hỏi gốc
                        var choices = match.Schools
                            .Select(c => new QuickReply(c.Name, c.Name, new ChatAction(ChatSchoolInfo.Action, c.Code, Question: query.Question)))
                            .ToList();
                        return MergeSchoolInfo(response, query, ChatGoalSeek.Clarify(typed, match.Schools.Count), clearKb: true) with { QuickReplies = choices };
                    }
                    if (match.Kind == NameMatchKind.Found)
                        code = match.Schools[0].Code;
                }
                // Model đã trả trường và mục (không chạy kho kiến thức) mà không khớp được trường nào trong hệ thống: nói rõ, không để câu tạm
                if (code is null && query.KbSkipped && query.School is { } unknown
                    && (await recommender!.FindMentionedSchoolsAsync(userId, query.Question, ct)).Count != 1)
                    return MergeSchoolInfo(response, query, ChatSchoolInfo.SchoolNotFound(unknown), clearKb: true);
                if (code is null)
                {
                    // SLM không trích được tên: quét câu, chỉ nhận khi đúng một trường chắc chắn (nhiều trường là so sánh, làm sau)
                    var mentioned = await recommender!.FindMentionedSchoolsAsync(userId, query.Question, ct);
                    if (mentioned.Count == 1)
                        code = mentioned[0].Code;
                }
                if (code is null)
                    return response;

                var info = await recommender!.SchoolInfoAsync(userId, code, ct);
                var answer = ChatSchoolInfo.Answer(info, fields, unstored);
                if (noTopic)
                    answer = answer with { Text = $"{ChatSchoolInfo.NoTopicLead(info.Name)}\n{answer.Text}" };
                if (!answer.Complete)
                {
                    // Chỉ hỏi điều cơ sở dữ liệu không có: kho kiến thức có nguồn thật thì giữ, không thì nói chưa có và chỉ website
                    return query.KbFound ? response : MergeSchoolInfo(response, query, answer.Text, clearKb: true);
                }
                // Thẻ nói đủ số liệu nên giao diện bỏ đoạn chữ này cho gọn; còn lời về điều hệ thống không lưu thì thẻ không nói, giữ chữ
                var merged = MergeSchoolInfo(response, query, answer.Text, clearKb: true)
                    with { Widget = ChatSchoolInfo.WidgetOf(info, fields, unstored ? null : answer.Text) };
                return fields.Count == 0 && !unstored
                    ? merged with { QuickReplies = ChatSchoolInfo.FieldButtons(info.Name) }
                    : merged;
            }
            catch (InvalidOperationException)
            {
                // Có kho kiến thức làm dự phòng thì giữ câu của kho; model đã bỏ qua kho thì báo chưa tra được thay vì để câu tạm
                return query.KbSkipped ? MergeSchoolInfo(response, query, ChatSchoolInfo.Unavailable, clearKb: true) : response;
            }
        }

        private async Task<string?> WriteCompareNoteAsync(CompareNoteRequestDto note, CancellationToken ct)
        {
            try
            {
                var written = await advisor.WriteCompareNoteAsync(note, ct);
                if (written.Status == "OK" && !string.IsNullOrWhiteSpace(written.Answer))
                    return written.Answer;
            }
            catch (Exception ex) when (ex is HttpRequestException
                                       || (ex is TaskCanceledException && !ct.IsCancellationRequested))
            {
                // Gemini không trả lời được: chỉ hiện bảng số liệu, không có nhận xét
            }
            return null;
        }

        // Tìm trường theo điều kiện. Advisor đã chuẩn hóa điều kiện; điều chưa đọc chắc chắn thì nói rõ và nhờ học sinh nói lại,
        // không tìm với phần điều kiện còn lại (danh sách sẽ sai ý học sinh). Không cần Gemini: đây là tra cứu sự thật.
        private async Task<AdvisorChatResponse> AnswerSchoolSearchAsync(Guid userId, AdvisorChatResponse response, CancellationToken ct)
        {
            var query = ChatSchoolInfo.ParseSearch(response.ActionData);
            if (query is null)
                return response;
            var asQuery = new ChatSchoolInfo.Query(query.Question, null, null, query.KbAnswer, false, [], true);
            if (query.Unresolved.Count > 0)
                return MergeSchoolInfo(response, asQuery, ChatSchoolInfo.SearchUnresolved(query.Unresolved), clearKb: true);
            if (!ChatSchoolInfo.HasAnyFilter(query.Filters))
                return MergeSchoolInfo(response, asQuery, ChatSchoolInfo.SearchNoFilter, clearKb: true);
            try
            {
                var result = await recommender!.SearchSchoolsAsync(userId, query.Filters, query.Sort, ct);
                var text = ChatSchoolInfo.SearchText(result, query.Filters, query.Sort);
                var merged = MergeSchoolInfo(response, asQuery, text, clearKb: true);
                if (result.Total == 0)
                    return merged;
                // Thẻ nói đủ (5 trường đầu và "Xem thêm"), nên giao diện bỏ đoạn chữ này; kèm nút bước tiếp theo
                return merged with
                {
                    Widget = ChatSchoolInfo.WidgetOfSearch(result, query.Filters, query.Sort, text),
                    QuickReplies = ChatSchoolInfo.SearchNextSteps(result),
                };
            }
            catch (InvalidOperationException)
            {
                return MergeSchoolInfo(response, asQuery, ChatSchoolInfo.Unavailable, clearKb: true);
            }
        }

        // So sánh từ hai trường: mỗi tên khớp bằng bộ khớp tên chặt, trường nào chưa chắc thì nói rõ để học sinh gõ lại (không đoán).
        // Mục lấy từ điều model trả; model không trả mục thì dùng bộ mục mặc định. Lỗi dữ liệu thì báo chưa tra được, không để câu tạm.
        private async Task<AdvisorChatResponse> AnswerSchoolCompareAsync(Guid userId, AdvisorChatResponse response, CancellationToken ct)
        {
            var query = ChatSchoolInfo.ParseCompare(response.ActionData);
            if (query is null)
                return response;
            var asQuery = new ChatSchoolInfo.Query(query.Question, null, null, query.KbAnswer, false, [], true);
            try
            {
                var typedNames = query.Schools.Take(ChatSchoolInfo.MaxCompare).ToList();
                var matches = new List<NameMatch>();
                foreach (var typed in typedNames)
                    matches.Add(await recommender!.FindSchoolsAsync(userId, typed, ct));
                // Trường không có trong hệ thống: không có ứng viên để chọn, nói rõ tên
                var unresolved = typedNames.Where((_, i) => matches[i].Kind == NameMatchKind.NotFound).ToList();
                if (unresolved.Count > 0)
                    return MergeSchoolInfo(response, asQuery, ChatSchoolInfo.CompareUnresolved(unresolved), clearKb: true);
                // Một tên mơ hồ ("Boston"): hỏi lại đúng trường đó, giữ nguyên các trường đã rõ. Mỗi nút chạy lại so sánh theo mã, không qua model.
                // Hai tên mơ hồ thì hỏi lần lượt từng tên (tên mơ hồ còn lại giữ nguyên chữ để lần sau hỏi tiếp)
                var ambiguousAt = matches.FindIndex(m => m.Kind == NameMatchKind.Ambiguous);
                if (ambiguousAt >= 0)
                {
                    var entries = typedNames.Select((t, i) => matches[i].Kind == NameMatchKind.Found ? matches[i].Schools[0].Code : t).ToList();
                    var choices = matches[ambiguousAt].Schools.Select(c =>
                    {
                        var picked = entries.ToList();
                        picked[ambiguousAt] = c.Code;
                        return new QuickReply(c.Name, c.Name, new ChatAction(
                            ChatSchoolInfo.CompareAction, SchoolCodes: picked, Question: query.Question, Fields: query.Fields));
                    }).ToList();
                    var clarify = ChatGoalSeek.Clarify(typedNames[ambiguousAt], matches[ambiguousAt].Schools.Count);
                    return MergeSchoolInfo(response, asQuery, clarify, clearKb: true) with { QuickReplies = choices };
                }
                var codes = matches.Select(m => m.Schools[0].Code).ToList();
                codes = codes.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (codes.Count < 2)
                    return MergeSchoolInfo(response, asQuery, ChatSchoolInfo.CompareNeedsTwo, clearKb: true);

                var infos = new List<SchoolInfoDto>();
                foreach (var code in codes)
                    infos.Add(await recommender!.SchoolInfoAsync(userId, code, ct));
                var fields = ChatSchoolInfo.FieldsFromModel(query.Fields);
                var table = ChatSchoolInfo.Compare(infos, fields);
                // Bảng số liệu do code dựng luôn có; Gemini viết thêm nhận xét ngắn, lỗi hoặc không qua kiểm tra thì chỉ hiện bảng
                var names = (await recommender!.ListSchoolOptionsAsync(userId, ct)).Select(o => o.Name).ToList();
                var note = await WriteCompareNoteAsync(
                    new CompareNoteRequestDto(infos.Select(i => i.Name).ToList(), ChatSchoolInfo.CompareFacts(infos, fields), names), ct);
                // Có lời AI thì chỉ hiện lời AI (đã nêu đủ số liệu); bảng code dựng chỉ làm câu thay thế khi AI lỗi
                var text = note is null ? table : ChatSchoolInfo.CompareAnswerFromNote(infos, fields, note);
                // Thẻ so sánh nói đủ bảng lẫn nhận xét (tách riêng khối "AI nhận xét"), nên giao diện bỏ đoạn chữ này, chỉ hiện thẻ
                return MergeSchoolInfo(response, asQuery, text, clearKb: true) with { Widget = ChatSchoolInfo.WidgetOfCompare(infos, fields, note, text) };
            }
            catch (InvalidOperationException)
            {
                return MergeSchoolInfo(response, asQuery, ChatSchoolInfo.Unavailable, clearKb: true);
            }
        }

        // Thay câu trả lời của kho kiến thức bằng câu từ cơ sở dữ liệu. Trả lời nhiều ý gộp một văn bản: thay đúng đoạn của kho, giữ các đoạn khác.
        // Số liệu cơ sở dữ liệu không dựa vào nguồn của kho nên bỏ nguồn trích và lời lưu ý của kho (clearKb).
        private static AdvisorChatResponse MergeSchoolInfo(AdvisorChatResponse response, ChatSchoolInfo.Query query, string text, bool clearKb)
        {
            var answer = query.KbAnswer is { Length: > 0 } kb && response.Answer.Contains(kb, StringComparison.Ordinal)
                ? response.Answer.Replace(kb, text, StringComparison.Ordinal)
                : string.IsNullOrWhiteSpace(response.Answer) ? text : $"{response.Answer}\n\n{text}";
            return response with
            {
                Answer = answer,
                Citations = clearKb ? [] : response.Citations,
                Disclaimer = clearKb ? null : response.Disclaimer,
                QuickReplies = null,
            };
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
                var steps = ChatGoalSeek.NextSteps(result);
                // Trường đang trong danh sách thì có thể giải thích vì sao xếp nhóm này; trường bị loại đã có nút riêng theo lý do loại
                if (steps.Count == 0)
                    steps = [new QuickReply("Giải thích trường này", "Giải thích trường này", new ChatAction(ChatExplanation.Action, result.Code))];
                return response with { Answer = answer, Widget = ChatGoalSeek.WidgetOf(result), QuickReplies = ChatNextSteps.WithCenter(steps) };
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

