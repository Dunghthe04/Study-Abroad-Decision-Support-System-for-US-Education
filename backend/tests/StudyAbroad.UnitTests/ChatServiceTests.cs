using System.Text.Json;
using System.Text.Json.Nodes;
using StudyAbroad.Application.Advisor;
using StudyAbroad.Application.Profile.Academic;
using StudyAbroad.Application.Profile.Flow;
using StudyAbroad.Application.Recommendations;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

public class ChatServiceTests
{
    private static readonly IReadOnlyList<Application.Advisor.ChatMessage> Hello = [new Application.Advisor.ChatMessage("user", "chào")];

    [Fact]
    public async Task NewChat_SendsNoState_ThenSavesStateFromAdvisor()
    {
        var advisor = new FakeAdvisor("B0");
        var store = new FakeSessions();
        var userId = Guid.NewGuid();

        var res = await new ChatService(advisor, store, new FakeProfileFlow()).ChatAsync(userId, new AdvisorChatRequest(Hello));

        Assert.Null(advisor.LastRequest!.State);        // chưa có phiên thì chưa có trạng thái
        var saved = Assert.Single(store.Rows);
        Assert.Equal(userId, saved.UserId);
        Assert.Equal("B0", saved.CurrentStep);
        Assert.Contains("\"currentStep\":\"B0\"", saved.StateJson);
        Assert.Equal(saved.Id.ToString(), res.SessionId); // trình duyệt nhận mã phiên
        Assert.Null(res.State);                           // trạng thái nội bộ không lộ ra ngoài
    }

    [Fact]
    public async Task NextTurn_SendsSavedState_AndUpdatesStep()
    {
        var userId = Guid.NewGuid();
        var store = new FakeSessions();
        var session = new ChatSession { UserId = userId, StateJson = "{\"currentStep\":\"B1\"}", CurrentStep = "B1" };
        store.Rows.Add(session);
        var advisor = new FakeAdvisor("B2");

        await new ChatService(advisor, store, new FakeProfileFlow()).ChatAsync(userId, new AdvisorChatRequest(Hello, session.Id.ToString()));

        Assert.Equal("B1", advisor.LastRequest!.State!.Value.GetProperty("currentStep").GetString());
        Assert.Single(store.Rows);                       // cập nhật phiên cũ, không tạo phiên mới
        Assert.Equal("B2", session.CurrentStep);
    }

    [Fact]
    public async Task OtherUsersSession_IsIgnored_AndClientStateIsNotTrusted()
    {
        var store = new FakeSessions();
        var other = new ChatSession { UserId = Guid.NewGuid(), StateJson = "{\"currentStep\":\"B4\"}" };
        store.Rows.Add(other);
        var advisor = new FakeAdvisor("B0");
        var fake = JsonDocument.Parse("{\"currentStep\":\"B6\"}").RootElement;

        var res = await new ChatService(advisor, store, new FakeProfileFlow())
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello, other.Id.ToString(), State: fake));

        Assert.Null(advisor.LastRequest!.State);         // không đọc phiên người khác, không tin state từ trình duyệt
        Assert.Equal(2, store.Rows.Count);
        Assert.NotEqual(other.Id.ToString(), res.SessionId);
        Assert.Equal("{\"currentStep\":\"B4\"}", other.StateJson);
    }

    [Fact]
    public async Task AiUsage_FromAdvisor_IsNotSentToTheBrowser()
    {
        // Số token là việc nội bộ của admin: advisor gửi cho backend, backend không chuyển tiếp cho học sinh
        var advisor = new FakeAdvisor("B0")
        {
            AiUsage = [new AiUsageDto("ollama", "usas-slm-v4", "slm_intents", "3.0", 1700, 180, 0, true, null, 21000)],
            // Nhãn model gán chứa chữ của học sinh, chỉ lưu cho admin
            Slm = new AdvisorSlmDto([new AdvisorSlmIntentDto("collect_profile", "em GPA 3.5")], ["collect_profile"], null, 0.9, false),
        };

        var res = await new ChatService(advisor, new FakeSessions(), new FakeProfileFlow()).ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        Assert.Null(res.AiUsage);
        Assert.Null(res.Slm);
    }

    // ---- lưu tin nhắn cho admin xem (chat_messages)

    [Fact]
    public async Task EachTurn_IsSavedAsTheStudentsMessageAndTheAssistantsReply()
    {
        var store = new FakeSessions();
        var saved = new FakeMessages();
        var userId = Guid.NewGuid();

        var res = await new ChatService(new FakeAdvisor("B0"), store, new FakeProfileFlow(), messages: saved)
            .ChatAsync(userId, new AdvisorChatRequest(Hello));

        var session = Assert.Single(store.Rows);
        Assert.Equal(["user", "assistant"], saved.Rows.Select(r => r.Role));
        Assert.All(saved.Rows, r => Assert.Equal(session.Id, r.ChatSessionId));
        Assert.Equal(session.Id.ToString(), res.SessionId);
        Assert.Equal("chào", saved.Rows[0].Content);
        Assert.Equal("ok", saved.Rows[1].Content);
        Assert.True(saved.Rows[0].CreatedAt <= saved.Rows[1].CreatedAt);   // tin hỏi đứng trước tin trả lời
    }

    [Fact]
    public async Task OutcomeAndMeta_AreOnlyOnTheAssistantsReply()
    {
        var saved = new FakeMessages();

        await new ChatService(new FakeAdvisor("B0"), new FakeSessions(), new FakeProfileFlow(), messages: saved)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        var (student, reply) = (saved.Rows[0], saved.Rows[1]);
        Assert.Null(student.Outcome);
        Assert.Null(student.MetaJson);
        Assert.Null(student.ReviewStatus);
        Assert.Equal("answered", reply.Outcome);
        Assert.Null(reply.ReviewStatus);                                  // trả lời được thì admin không phải xem
        Assert.Equal(1, JsonDocument.Parse(reply.MetaJson!).RootElement.GetProperty("v").GetInt32());
    }

    [Fact]
    public async Task UnderstoodNothing_GoesToTheAdminQueue_WithTheLabelTheModelGave()
    {
        var saved = new FakeMessages();
        var advisor = new FakeAdvisor("B0")
        {
            Slm = new AdvisorSlmDto([new AdvisorSlmIntentDto("out_of_scope", "asdfgh")], ["out_of_scope"], "ambiguous", 0.2, false),
        };

        await new ChatService(advisor, new FakeSessions(), new FakeProfileFlow(), messages: saved)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest([new Application.Advisor.ChatMessage("user", "asdfgh")]));

        var (student, reply) = (saved.Rows[0], saved.Rows[1]);
        Assert.Equal("no_answer", reply.Outcome);
        Assert.Equal("open", reply.ReviewStatus);
        // Nhãn model gán thuộc về CÂU HỎI nên nằm ở tin của học sinh, không phải tin trả lời
        var label = JsonDocument.Parse(student.MetaJson!).RootElement.GetProperty("slm").GetProperty("modelIntents")[0];
        Assert.Equal("asdfgh", label.GetProperty("span").GetString());
        Assert.False(JsonDocument.Parse(reply.MetaJson!).RootElement.TryGetProperty("slm", out _));
    }

    [Fact]
    public async Task TheReplyPointsExactlyAtTheQuestionItAnswers()
    {
        var saved = new FakeMessages();

        await new ChatService(new FakeAdvisor("B0"), new FakeSessions(), new FakeProfileFlow(), messages: saved)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        var (student, reply) = (saved.Rows[0], saved.Rows[1]);
        Assert.Equal(student.Id, reply.ReplyToMessageId);
        Assert.Null(student.ReplyToMessageId);
    }

    [Fact]
    public async Task ReplyMeta_SplitsLocalAndCloudTokensOfTheTurn()
    {
        var saved = new FakeMessages();
        var advisor = new FakeAdvisor("B0")
        {
            AiUsage =
            [
                new AiUsageDto("ollama", "usas-slm-v4", "slm_intents", "3.0", 1700, 180, 0, true, null, 21000),
                new AiUsageDto("claude", "claude-haiku-5-5", "skill_tra_loi", "1.0", 2400, 500, 0, true, null, 3000),
            ],
        };

        await new ChatService(advisor, new FakeSessions(), new FakeProfileFlow(), messages: saved)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        var meta = JsonDocument.Parse(saved.Rows[1].MetaJson!).RootElement;
        Assert.Equal(1700, meta.GetProperty("local").GetProperty("inputTokens").GetInt32());
        Assert.Equal(2400, meta.GetProperty("cloud").GetProperty("inputTokens").GetInt32());
    }

    [Fact]
    public async Task ReplyWithoutAQuestion_PointsAtNothing()
    {
        var saved = new FakeMessages();

        await new ChatService(new FakeAdvisor("B0"), new FakeSessions(), new FakeProfileFlow(), messages: saved)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest([]));

        Assert.Null(saved.Rows.Single().ReplyToMessageId);
    }

    [Fact]
    public async Task AiStepThatAlwaysFailed_IsSavedAsFailedAndQueued()
    {
        var saved = new FakeMessages();
        var advisor = new FakeAdvisor("B0")
        {
            AiUsage = [new AiUsageDto("claude", "m", "skill_tra_loi", "1.0", 0, 0, 0, false, "timeout", 30000)],
        };

        await new ChatService(advisor, new FakeSessions(), new FakeProfileFlow(), messages: saved)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        Assert.Equal(("failed", "open"), (saved.Rows[1].Outcome, saved.Rows[1].ReviewStatus));
    }

    [Fact]
    public async Task FailureToSaveMessages_NeverBreaksTheChat()
    {
        var store = new FakeSessions();

        var res = await new ChatService(new FakeAdvisor("B0"), store, new FakeProfileFlow(), messages: new FakeMessages { Fail = true })
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        Assert.Equal("ok", res.Answer);                  // học sinh vẫn nhận câu trả lời
        Assert.Equal(store.Rows.Single().Id.ToString(), res.SessionId);   // phiên vẫn lưu
    }

    [Fact]
    public async Task WithoutARepository_NothingIsSaved_AndTheChatStillWorks()
    {
        var res = await new ChatService(new FakeAdvisor("B0"), new FakeSessions(), new FakeProfileFlow())
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        Assert.Equal("ok", res.Answer);
    }

    [Fact]
    public async Task NextTurnOfAnExistingSession_IsSavedUnderThatSession()
    {
        var userId = Guid.NewGuid();
        var store = new FakeSessions();
        var session = new ChatSession { UserId = userId, StateJson = "{\"currentStep\":\"B1\"}", CurrentStep = "B1" };
        store.Rows.Add(session);
        var saved = new FakeMessages();

        await new ChatService(new FakeAdvisor("B2"), store, new FakeProfileFlow(), messages: saved)
            .ChatAsync(userId, new AdvisorChatRequest(Hello, session.Id.ToString()));

        Assert.All(saved.Rows, r => Assert.Equal(session.Id, r.ChatSessionId));
        Assert.Single(store.Rows);
    }

    [Fact]
    public async Task StreamingTurn_IsSavedToo()
    {
        var saved = new FakeMessages();

        await new ChatService(new FakeAdvisor("B0"), new FakeSessions(), new FakeProfileFlow(), messages: saved)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello), onEvent: _ => Task.CompletedTask);

        Assert.Equal(2, saved.Rows.Count);
    }

    // ---- nhật ký các lần gọi AI (ai_calls)

    private static readonly IReadOnlyList<AiUsageDto> TwoCalls =
    [
        new AiUsageDto("ollama", "usas-slm-v4", "slm_intents", "3.0", 1700, 180, 0, true, null, 21000),
        new AiUsageDto("claude", "claude-haiku-5-5", "skill_tra_loi", "1.0", 2400, 500, 0, true, null, 3000),
    ];

    [Fact]
    public async Task EveryAiCallOfTheTurn_IsLoggedOnceAndLinkedToTheReplyAndTheUser()
    {
        var saved = new FakeMessages();
        var calls = new FakeAiCalls();
        var userId = Guid.NewGuid();

        await new ChatService(new FakeAdvisor("B0") { AiUsage = TwoCalls }, new FakeSessions(), new FakeProfileFlow(), messages: saved, aiCalls: calls)
            .ChatAsync(userId, new AdvisorChatRequest(Hello));

        Assert.Equal(["ollama", "claude"], calls.Rows.Select(c => c.Kind));
        Assert.All(calls.Rows, c => Assert.Equal(userId, c.UserId));
        Assert.All(calls.Rows, c => Assert.Equal(saved.Rows[1].Id, c.ChatMessageId));   // nối với tin TRẢ LỜI, không phải tin hỏi
        Assert.Equal((1700, 180), (calls.Rows[0].PromptTokens, calls.Rows[0].CompletionTokens));
    }

    [Fact]
    public async Task TokensAreStillLogged_WhenStoringMessagesIsSwitchedOff()
    {
        // Tắt lưu nội dung chat vì riêng tư thì vẫn theo dõi được token (chỉ là số đo), chỉ không có liên kết tới tin nhắn
        var calls = new FakeAiCalls();

        await new ChatService(new FakeAdvisor("B0") { AiUsage = TwoCalls }, new FakeSessions(), new FakeProfileFlow(), aiCalls: calls)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        Assert.Equal(2, calls.Rows.Count);
        Assert.All(calls.Rows, c => Assert.Null(c.ChatMessageId));
    }

    [Fact]
    public async Task TokensAreStillLogged_WhenSavingTheMessagesFailed()
    {
        var calls = new FakeAiCalls();

        await new ChatService(new FakeAdvisor("B0") { AiUsage = TwoCalls }, new FakeSessions(), new FakeProfileFlow(),
                messages: new FakeMessages { Fail = true }, aiCalls: calls)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        Assert.Equal(2, calls.Rows.Count);
        Assert.All(calls.Rows, c => Assert.Null(c.ChatMessageId));   // tin không lưu được nên không có gì để nối, không được nối vào mã không tồn tại
    }

    [Fact]
    public async Task FailureToLogAiCalls_NeverBreaksTheChat()
    {
        var res = await new ChatService(new FakeAdvisor("B0") { AiUsage = TwoCalls }, new FakeSessions(), new FakeProfileFlow(),
                aiCalls: new FakeAiCalls { Fail = true })
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        Assert.Equal("ok", res.Answer);
    }

    [Fact]
    public async Task NoMeasurements_MeansNothingIsWritten()
    {
        var calls = new FakeAiCalls();

        await new ChatService(new FakeAdvisor("B0"), new FakeSessions(), new FakeProfileFlow(), aiCalls: calls)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        Assert.Equal(0, calls.Writes);   // advisor cũ không gửi aiUsage: không ghi gì, không lỗi
    }

    [Fact]
    public async Task FailedAiCall_IsLoggedWithItsError()
    {
        var calls = new FakeAiCalls();
        var usage = new[] { new AiUsageDto("claude", "m", "skill_tra_loi", "1.0", 0, 0, 0, false, "quá tải", 30000) };

        await new ChatService(new FakeAdvisor("B0") { AiUsage = usage }, new FakeSessions(), new FakeProfileFlow(), aiCalls: calls)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        var call = Assert.Single(calls.Rows);
        Assert.Equal((false, "quá tải"), (call.Success, call.Error));
    }

    // ---- một lượt gọi advisor nhiều lần: gom đủ số đo của cả giỏ (goal-seek gọi advisor chat rồi gọi thêm advisor viết lời)

    private static FakeAdvisor GoalSeekAdvisor(AiUsageDto? noteCall = null) => new("B4")
    {
        Action = "goal_seek", ActionData = BuAction,
        AiUsage = [new AiUsageDto("ollama", "usas-slm-v4", "slm_intents", "3.0", 1700, 180, 0, true, null, 21000)],
        GoalNote = new GoalNoteResponseDto("1.0", "claude", "OK", "Để lên nhóm Vừa sức ở Boston University, bạn cần nâng SAT.",
            [noteCall ?? new AiUsageDto("claude", "claude-haiku-5-5", "skill_goal_seek", "1.0", 2400, 500, 0, true, null, 3000)]),
    };

    private static ChatService WithBasket(FakeAdvisor advisor, AiUsageCollector basket, FakeMessages saved, FakeAiCalls calls) =>
        new(new UsageCollectingAdvisorClient(advisor, basket), new FakeSessions(), new FakeProfileFlow(), null,
            new FakeRunner { Match = Found(), GoalResult = GoalOf() }, messages: saved, aiCalls: calls, usageCollector: basket);

    [Fact]
    public async Task ATurnThatCallsTheAdvisorTwice_LogsBothCalls_NotOnlyTheFirst()
    {
        var (basket, saved, calls) = (new AiUsageCollector(), new FakeMessages(), new FakeAiCalls());

        await WithBasket(GoalSeekAdvisor(), basket, saved, calls).ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        Assert.Equal(["slm_intents", "skill_goal_seek"], calls.Rows.Select(c => c.Purpose));   // đủ hai lần, không đếm trùng
        Assert.All(calls.Rows, c => Assert.Equal(saved.Rows[1].Id, c.ChatMessageId));
    }

    [Fact]
    public async Task MetaJson_CountsEveryCallOfTheTurn_SplitIntoLocalAndCloud()
    {
        var (basket, saved, calls) = (new AiUsageCollector(), new FakeMessages(), new FakeAiCalls());

        await WithBasket(GoalSeekAdvisor(), basket, saved, calls).ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        var meta = JsonDocument.Parse(saved.Rows[1].MetaJson!).RootElement;
        Assert.Equal(1, meta.GetProperty("local").GetProperty("calls").GetInt32());
        Assert.Equal(1700, meta.GetProperty("local").GetProperty("inputTokens").GetInt32());
        Assert.Equal(1, meta.GetProperty("cloud").GetProperty("calls").GetInt32());
        Assert.Equal(2400, meta.GetProperty("cloud").GetProperty("inputTokens").GetInt32());   // lần gọi thêm không bị bỏ sót nữa
    }

    [Fact]
    public async Task AnExtraStepThatAlwaysFailed_MakesTheTurnFailed()
    {
        var failedNote = new AiUsageDto("claude", "claude-haiku-5-5", "skill_goal_seek", "1.0", 0, 0, 0, false, "quá tải", 30000);
        var (basket, saved, calls) = (new AiUsageCollector(), new FakeMessages(), new FakeAiCalls());

        await WithBasket(GoalSeekAdvisor(failedNote), basket, saved, calls).ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        // Lần gọi chat đầu ổn nhưng bước viết lời hỏng hẳn: trước đây lỗi này không ai thấy
        Assert.Equal(("failed", "open"), (saved.Rows[1].Outcome, saved.Rows[1].ReviewStatus));
        Assert.Contains("skill_goal_seek", calls.Rows.Where(c => !c.Success).Select(c => c.Purpose));
    }

    [Fact]
    public async Task TheBasketIsEmptiedAtTheStartOfEachTurn_SoOldUsageNeverLeaksIn()
    {
        var basket = new AiUsageCollector();
        basket.Add([new AiUsageDto("claude", "m", "stale_from_a_previous_turn", "1", 999, 999, 0, true, null, 1)]);
        var (saved, calls) = (new FakeMessages(), new FakeAiCalls());

        await WithBasket(GoalSeekAdvisor(), basket, saved, calls).ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        Assert.DoesNotContain(calls.Rows, c => c.Purpose == "stale_from_a_previous_turn");
        Assert.Equal(2, calls.Rows.Count);
    }

    [Fact]
    public async Task AnEmptyBasket_FallsBackToTheUsageOfTheChatCall()
    {
        // Advisor chưa được bọc (hoặc test cũ): giỏ rỗng nên dùng số đo đi kèm phản hồi chat, không mất dữ liệu
        var calls = new FakeAiCalls();

        await new ChatService(new FakeAdvisor("B0") { AiUsage = TwoCalls }, new FakeSessions(), new FakeProfileFlow(),
                aiCalls: calls, usageCollector: new AiUsageCollector())
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        Assert.Equal(2, calls.Rows.Count);
    }

    // ---- advisor sập hẳn: câu hỏi không được mất

    [Fact]
    public async Task AdvisorDown_SavesTheQuestionAndAFailedSystemRow_AndStillThrowsForTheController()
    {
        var store = new FakeSessions();
        var saved = new FakeMessages();
        var advisor = new FakeAdvisor("B0") { ChatError = new HttpRequestException("máy chủ nội bộ 10.0.0.5 không phản hồi") };

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            new ChatService(advisor, store, new FakeProfileFlow(), messages: saved)
                .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello)));   // học sinh vẫn nhận 503 như cũ

        Assert.Equal(["user", "system"], saved.Rows.Select(r => r.Role));
        var (question, failure) = (saved.Rows[0], saved.Rows[1]);
        Assert.Equal("chào", question.Content);
        Assert.Equal(("failed", "open"), (failure.Outcome, failure.ReviewStatus));
        Assert.Equal(question.Id, failure.ReplyToMessageId);
        Assert.Equal(store.Rows.Single().Id, question.ChatSessionId);          // phiên mới được lưu để tin nhắn có chỗ bám
        Assert.Equal(question.ChatSessionId, failure.ChatSessionId);
        var meta = JsonDocument.Parse(failure.MetaJson!).RootElement;
        Assert.Equal("HttpRequestException", meta.GetProperty("error").GetString());
        Assert.Equal(["advisor"], meta.GetProperty("failedSteps").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task AdvisorDown_NeverStoresTheErrorMessage_ItMayHoldInternalAddresses()
    {
        var saved = new FakeMessages();
        var advisor = new FakeAdvisor("B0") { ChatError = new HttpRequestException("máy chủ nội bộ 10.0.0.5 không phản hồi") };

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            new ChatService(advisor, new FakeSessions(), new FakeProfileFlow(), messages: saved)
                .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello)));

        Assert.DoesNotContain(saved.Rows, r => (r.Content + r.MetaJson).Contains("10.0.0.5"));
    }

    [Fact]
    public async Task AdvisorDown_OnAnExistingSession_KeepsThatSession()
    {
        var userId = Guid.NewGuid();
        var store = new FakeSessions();
        var session = new ChatSession { UserId = userId, StateJson = "{\"currentStep\":\"B1\"}", CurrentStep = "B1" };
        store.Rows.Add(session);
        var saved = new FakeMessages();
        var advisor = new FakeAdvisor("B2") { ChatError = new HttpRequestException() };

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            new ChatService(advisor, store, new FakeProfileFlow(), messages: saved)
                .ChatAsync(userId, new AdvisorChatRequest(Hello, session.Id.ToString())));

        Assert.Single(store.Rows);                                              // không tạo thêm phiên
        Assert.All(saved.Rows, r => Assert.Equal(session.Id, r.ChatSessionId));
        Assert.Equal("{\"currentStep\":\"B1\"}", session.StateJson);            // trạng thái cũ không bị đổi
    }

    [Fact]
    public async Task AdvisorDown_WhileStreaming_IsSavedToo()
    {
        var saved = new FakeMessages();
        var advisor = new FakeAdvisor("B0") { StreamError = new HttpRequestException() };

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            new ChatService(advisor, new FakeSessions(), new FakeProfileFlow(), messages: saved)
                .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello), onEvent: _ => Task.CompletedTask));

        Assert.Equal(["user", "system"], saved.Rows.Select(r => r.Role));
    }

    [Fact]
    public async Task WhenTheStudentCancels_NothingIsRecorded()
    {
        // Học sinh đóng tab giữa chừng không phải lỗi hệ thống, không đưa vào hàng đợi của admin
        var saved = new FakeMessages();
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var advisor = new FakeAdvisor("B0") { ChatError = new TaskCanceledException() };

        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            new ChatService(advisor, new FakeSessions(), new FakeProfileFlow(), messages: saved)
                .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello), cancelled.Token));

        Assert.Empty(saved.Rows);
    }

    [Fact]
    public async Task FailureToRecordTheFailure_DoesNotHideTheOriginalError()
    {
        var advisor = new FakeAdvisor("B0") { ChatError = new HttpRequestException("lỗi gốc") };

        var thrown = await Assert.ThrowsAsync<HttpRequestException>(() =>
            new ChatService(advisor, new FakeSessions(), new FakeProfileFlow(), messages: new FakeMessages { Fail = true })
                .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello)));

        Assert.Equal("lỗi gốc", thrown.Message);   // bộ điều khiển vẫn thấy đúng lỗi advisor, không phải lỗi ghi database
    }

    [Fact]
    public async Task AdvisorDown_WithoutARepository_ThrowsAsBefore()
    {
        var advisor = new FakeAdvisor("B0") { ChatError = new HttpRequestException() };

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            new ChatService(advisor, new FakeSessions(), new FakeProfileFlow()).ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello)));
    }

    [Fact]
    public async Task EmptyStudentMessage_SavesOnlyTheReply()
    {
        var saved = new FakeMessages();

        await new ChatService(new FakeAdvisor("B0"), new FakeSessions(), new FakeProfileFlow(), messages: saved)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest([]));

        Assert.Equal(["assistant"], saved.Rows.Select(r => r.Role));
    }

    [Fact]
    public async Task SavedMessageDoesNotLeakInternalSignalsToTheBrowser()
    {
        var advisor = new FakeAdvisor("B0") { Slm = new AdvisorSlmDto([new AdvisorSlmIntentDto("knowledge", "I-20 là gì")]) };

        var res = await new ChatService(advisor, new FakeSessions(), new FakeProfileFlow(), messages: new FakeMessages())
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        Assert.Null(res.Slm);
        Assert.Null(res.AiUsage);
    }

    [Fact]
    public async Task QuickRepliesAndWidget_FromAdvisor_ReachTheBrowser()
    {
        var replies = new List<QuickReply> { new("Điền bằng form", "Điền bằng form") };
        var widget = new Widget("major");
        var advisor = new FakeAdvisor("B1", replies, widget);

        var res = await new ChatService(advisor, new FakeSessions(), new FakeProfileFlow()).ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        Assert.Equal(replies, res.QuickReplies);
        Assert.Equal(widget, res.Widget);
        Assert.Null(res.State); // trạng thái nội bộ vẫn không lộ ra ngoài
    }

    // C4: hồ sơ gửi sang advisor lấy từ nháp, chat đổi gì thì ghi vào nháp
    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private const string BudgetChanged = """{ "budget": { "value": "tier_20k_35k", "status": "confirmed" } }""";

    [Fact]
    public async Task SendsProfileFromDraft_NotFromBrowser()
    {
        var flow = new FakeProfileFlow { Draft = new ProfileFlowDraftDto(Step4: new FlowFinancialDraftDto("tier_35k_50k")) };
        var advisor = new FakeAdvisor("B2");
        var fromBrowser = Json("""{ "budget": { "value": "tier_above_70k", "status": "confirmed" } }""");

        await new ChatService(advisor, new FakeSessions(), flow)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello, Profile: fromBrowser));

        var budget = advisor.LastRequest!.Profile!.Value.GetProperty("budget");
        Assert.Equal("tier_35k_50k", budget.GetProperty("value").GetString());
        Assert.Equal("confirmed", budget.GetProperty("status").GetString());
    }

    // Học bạ theo năm gửi thành mục riêng, không nằm trong hồ sơ chat (chat chỉ đọc, chỉ form nhập được)
    [Fact]
    public async Task SendsTranscriptBesideProfile_NotInsideIt()
    {
        var flow = new FakeProfileFlow
        {
            Draft = new ProfileFlowDraftDto(Step2: new FlowTranscriptDraftDto(
                [new FlowAcademicYearDraftDto("Lớp 10", [new FlowSubjectDraftDto("Toán", 8m, 9m)])])),
        };
        var advisor = new FakeAdvisor("B2");

        await new ChatService(advisor, new FakeSessions(), flow).ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        var year = advisor.LastRequest!.Transcript!.Value.GetProperty("years")[0];
        Assert.Equal("Lớp 10", year.GetProperty("grade").GetString());
        Assert.False(advisor.LastRequest.Profile!.Value.TryGetProperty("transcript", out _));
    }

    // Mốc GPA / tiếng Anh tính từ trường cùng bậc học mục tiêu, gửi riêng cạnh hồ sơ
    [Fact]
    public async Task SendsBenchmarksOfTargetLevel()
    {
        var flow = new FakeProfileFlow { Draft = new ProfileFlowDraftDto(Step1: new FlowGoalDraftDto(TargetDegreeLevel: "master")) };
        var marks = new FakeBenchmarks([new BenchmarkRow(3.6m, 6.5m, null, null)]);
        var advisor = new FakeAdvisor("B2");

        await new ChatService(advisor, new FakeSessions(), flow, benchmarks: marks).ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        Assert.Equal("master", marks.Level);
        var sent = advisor.LastRequest!.Benchmarks!.Value;
        Assert.Equal(3.6m, sent.GetProperty("gpa4").GetDecimal());
        Assert.Equal(6.5m, sent.GetProperty("english").GetProperty("ielts").GetDecimal());
    }

    // Hồ sơ chưa chọn bậc học thì lấy đại học; chưa có dữ liệu trường thì không gửi mốc
    [Fact]
    public async Task NoTargetLevel_UsesUndergraduate_NoDataSendsNull()
    {
        var marks = new FakeBenchmarks([]);
        var advisor = new FakeAdvisor("B2");

        await new ChatService(advisor, new FakeSessions(), new FakeProfileFlow(), benchmarks: marks)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        Assert.Equal("undergraduate", marks.Level);
        Assert.Null(advisor.LastRequest!.Benchmarks);
    }

    // Bảng quy đổi gửi advisor lấy từ DB (provider), bảng trình duyệt gửi bị bỏ
    [Fact]
    public async Task SendsGradeScaleFromServer_IgnoresBrowserTable()
    {
        var advisor = new FakeAdvisor("B2");
        var fromBrowser = new List<GradeScaleRuleDto> { new(0m, 10m, 4m, "A") };

        await new ChatService(advisor, new FakeSessions(), new FakeProfileFlow(), gradeScale: new FakeGradeScale())
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello, GradeScale: fromBrowser));

        Assert.Equal(AcademicAnalysisService.GetDefaultWesScaleConfig().Rules, advisor.LastRequest!.GradeScale);
        var json = JsonSerializer.SerializeToElement(advisor.LastRequest, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var first = json.GetProperty("gradeScale")[0];
        Assert.Equal(9.0m, first.GetProperty("minScore").GetDecimal());
        Assert.Equal(4.0m, first.GetProperty("gpa4").GetDecimal());
    }

    [Fact]
    public async Task NoGradeScaleProvider_SendsNoTable()
    {
        var advisor = new FakeAdvisor("B2");

        await new ChatService(advisor, new FakeSessions(), new FakeProfileFlow())
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello, GradeScale: [new(0m, 10m, 4m, "A")]));

        Assert.Null(advisor.LastRequest!.GradeScale);
    }

    private sealed class FakeGradeScale : IGradeScaleProvider
    {
        public Task<GradeScaleConfigDto> GetAsync(CancellationToken ct = default) =>
            Task.FromResult(AcademicAnalysisService.GetDefaultWesScaleConfig());
        public void Invalidate() { }
    }

    // Nhập GPA tổng: không gửi học bạ
    [Fact]
    public async Task OverallGpa_SendsNoTranscript()
    {
        var flow = new FakeProfileFlow { Draft = new ProfileFlowDraftDto(Step2: new FlowTranscriptDraftDto(ManualGpa10Scale: 8m, GpaMode: "overall")) };
        var advisor = new FakeAdvisor("B2");

        await new ChatService(advisor, new FakeSessions(), flow).ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        Assert.Null(advisor.LastRequest!.Transcript);
    }

    [Fact]
    public async Task KeepsNoneStatusFromPreviousTurn_WhenDraftHasNoValue()
    {
        var userId = Guid.NewGuid();
        var store = new FakeSessions();
        var session = new ChatSession
        {
            UserId = userId,
            StateJson = """{"currentStep":"B2","profile":{"sat":{"value":null,"status":"none"}}}""",
        };
        store.Rows.Add(session);
        var advisor = new FakeAdvisor("B2");

        await new ChatService(advisor, store, new FakeProfileFlow())
            .ChatAsync(userId, new AdvisorChatRequest(Hello, session.Id.ToString()));

        // nháp không lưu được "chưa thi SAT", lấy lại từ lượt trước để chat không hỏi lại
        Assert.Equal("none", advisor.LastRequest!.Profile!.Value.GetProperty("sat").GetProperty("status").GetString());
    }

    [Fact]
    public async Task ChangedKeys_ArePatchedIntoDraft_AndProfileStaysInternal()
    {
        var flow = new FakeProfileFlow();
        var advisor = new FakeAdvisor("B2") { Profile = Json(BudgetChanged), Changed = ["budget"] };

        var res = await new ChatService(advisor, new FakeSessions(), flow).ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        Assert.Equal(["budget"], flow.PatchedKeys);
        Assert.Equal("tier_20k_35k", flow.PatchedProfile!["budget"]!["value"]!.GetValue<string>());
        Assert.Equal(["budget"], res.ProfileChanged);
        Assert.Null(res.Profile);
    }

    [Fact]
    public async Task NothingChanged_DoesNotTouchDraft()
    {
        var flow = new FakeProfileFlow();
        var advisor = new FakeAdvisor("B2") { Profile = Json(BudgetChanged), Changed = [] };

        var res = await new ChatService(advisor, new FakeSessions(), flow).ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        Assert.Null(flow.PatchedKeys);
        Assert.Empty(res.ProfileChanged!);
    }

    [Fact]
    public async Task DraftOverLimit_ChatStillAnswers_AndReportsNoChange()
    {
        var store = new FakeSessions();
        var flow = new FakeProfileFlow { PatchError = new ArgumentException("Dữ liệu nháp quá lớn") };
        var advisor = new FakeAdvisor("B2") { Profile = Json(BudgetChanged), Changed = ["budget"] };

        var res = await new ChatService(advisor, store, flow).ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        Assert.Equal("ok", res.Answer);
        Assert.Empty(res.ProfileChanged!);
        Assert.Single(store.Rows); // phiên chat vẫn được lưu
    }

    // Mục 7: trả trang form của câu chat đang hỏi
    [Theory]
    [InlineData("B1.3", 1)]
    [InlineData("D2", 2)]
    [InlineData("E2", 3)]
    [InlineData("F1", 4)]
    [InlineData("D5", 5)]
    [InlineData("X1", 6)]
    [InlineData("N4", null)]
    [InlineData(null, null)]
    public async Task Response_HasFormStepOfAskedQuestion(string? questionId, int? expected)
    {
        var advisor = new FakeAdvisor("B2") { QuestionId = questionId };

        var res = await new ChatService(advisor, new FakeSessions(), new FakeProfileFlow())
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        Assert.Equal(expected, res.FormStep);
    }

    // C5: học sinh xác nhận ở X1 (chat sang B3) thì lưu hồ sơ chính thức
    private static ChatSession AtReview(Guid userId, string step = "B2") =>
        new() { UserId = userId, StateJson = $"{{\"currentStep\":\"{step}\",\"questionId\":\"X1\"}}", CurrentStep = step };

    [Fact]
    public async Task ConfirmAtReview_CommitsProfile()
    {
        var userId = Guid.NewGuid();
        var store = new FakeSessions();
        var session = AtReview(userId);
        store.Rows.Add(session);
        var flow = new FakeProfileFlow();

        var res = await new ChatService(new FakeAdvisor("B3"), store, flow)
            .ChatAsync(userId, new AdvisorChatRequest(Hello, session.Id.ToString()));

        Assert.Equal(1, flow.Commits);
        Assert.Equal("ok", res.Answer);
        Assert.Equal("B3", session.CurrentStep);
        Assert.True(res.ProfileSaved); // trang Gợi ý trường biết mà hiện nút cập nhật danh sách
    }

    // Đánh giá chạy ngay trong lượt xác nhận và chuyển chat sang B4: vẫn phải lưu hồ sơ chính thức
    [Fact]
    public async Task ConfirmAtReview_AssessedToB4_CommitsProfile()
    {
        var userId = Guid.NewGuid();
        var store = new FakeSessions();
        var session = AtReview(userId);
        store.Rows.Add(session);
        var flow = new FakeProfileFlow();

        await new ChatService(new FakeAdvisor("B4"), store, flow)
            .ChatAsync(userId, new AdvisorChatRequest(Hello, session.Id.ToString()));

        Assert.Equal(1, flow.Commits);
        Assert.Equal("B4", session.CurrentStep);
    }

    // Chỉ lưu khi rời bảng rà soát X1, sang B4 từ câu khác thì không lưu
    [Fact]
    public async Task ToB4WithoutReview_DoesNotCommit()
    {
        var userId = Guid.NewGuid();
        var store = new FakeSessions();
        var session = new ChatSession
        {
            UserId = userId, StateJson = "{\"currentStep\":\"B0\",\"questionId\":\"B0.2\"}", CurrentStep = "B0",
        };
        store.Rows.Add(session);
        var flow = new FakeProfileFlow();

        var res = await new ChatService(new FakeAdvisor("B4"), store, flow)
            .ChatAsync(userId, new AdvisorChatRequest(Hello, session.Id.ToString()));

        Assert.Equal(0, flow.Commits);
        Assert.Null(res.ProfileSaved);
    }

    [Fact]
    public async Task CommitFails_KeepsReviewStateAndDraft_AndExplainsInVietnamese()
    {
        var userId = Guid.NewGuid();
        var store = new FakeSessions();
        var session = AtReview(userId);
        var oldState = session.StateJson;
        store.Rows.Add(session);
        var flow = new FakeProfileFlow { CommitError = new InvalidOperationException("Vui lòng nhập GPA hợp lệ.") };
        var advisor = new FakeAdvisor("B3", [new QuickReply("Đánh giá hồ sơ", "Đánh giá hồ sơ")])
        {
            Profile = Json(BudgetChanged),
            Changed = ["budget"],
        };

        var res = await new ChatService(advisor, store, flow)
            .ChatAsync(userId, new AdvisorChatRequest(Hello, session.Id.ToString()));

        Assert.Contains("Hồ sơ chưa lưu được", res.Answer);
        Assert.Contains("Vui lòng nhập GPA hợp lệ.", res.Answer);
        Assert.Null(res.QuickReplies);           // không mời sang bước sau khi chưa lưu được
        Assert.Equal(oldState, session.StateJson); // vẫn ở bảng rà soát, xác nhận lại được
        Assert.Equal("B2", session.CurrentStep);
        Assert.Equal(["budget"], flow.PatchedKeys); // nháp vẫn giữ những gì vừa nói
        Assert.Equal(["budget"], res.ProfileChanged);
        Assert.Null(res.ProfileSaved); // chưa lưu được thì không báo trang cập nhật
    }

    [Fact]
    public async Task AlreadyPastReview_DoesNotCommitAgain()
    {
        var userId = Guid.NewGuid();
        var store = new FakeSessions();
        var session = AtReview(userId, "B3");
        store.Rows.Add(session);
        var flow = new FakeProfileFlow();

        await new ChatService(new FakeAdvisor("B3"), store, flow)
            .ChatAsync(userId, new AdvisorChatRequest(Hello, session.Id.ToString()));

        Assert.Equal(0, flow.Commits);
    }

    // Đánh giá chạy ngay khi xác nhận X1: lưu câu đánh giá vào analysis_results, không trả danh sách skill cho trình duyệt
    [Fact]
    public async Task ConfirmAtReview_SavesAssessment()
    {
        var userId = Guid.NewGuid();
        var store = new FakeSessions();
        var session = AtReview(userId);
        store.Rows.Add(session);
        var analyses = new FakeAnalyses();
        var advisor = new FakeAdvisor("B4") { Skills = ["skill_ho_so", "skill_danh_gia"] };

        var res = await new ChatService(advisor, store, new FakeProfileFlow(), analyses)
            .ChatAsync(userId, new AdvisorChatRequest(Hello, session.Id.ToString()));

        var saved = Assert.Single(analyses.Saved);
        Assert.Equal((userId, "strengths_weaknesses", "{\"answer\":\"ok\"}", "chat:skill_danh_gia"), saved);
        Assert.Null(res.Skills);
    }

    [Fact]
    public async Task Roadmap_SavedAsStrategy()
    {
        var analyses = new FakeAnalyses();
        var advisor = new FakeAdvisor("B4") { Skills = ["skill_lo_trinh"] };

        await new ChatService(advisor, new FakeSessions(), new FakeProfileFlow(), analyses)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        Assert.Equal("strategy", Assert.Single(analyses.Saved).Kind);
    }

    // Hồ sơ chưa lưu được thì câu đánh giá bị thay bằng câu báo lỗi: không lưu đánh giá
    [Fact]
    public async Task CommitFails_DoesNotSaveAssessment()
    {
        var userId = Guid.NewGuid();
        var store = new FakeSessions();
        var session = AtReview(userId);
        store.Rows.Add(session);
        var analyses = new FakeAnalyses();
        var flow = new FakeProfileFlow { CommitError = new InvalidOperationException("Thiếu GPA.") };

        await new ChatService(new FakeAdvisor("B4") { Skills = ["skill_danh_gia"] }, store, flow, analyses)
            .ChatAsync(userId, new AdvisorChatRequest(Hello, session.Id.ToString()));

        Assert.Empty(analyses.Saved);
    }

    [Fact]
    public async Task OtherSkills_SaveNothing()
    {
        var analyses = new FakeAnalyses();
        var advisor = new FakeAdvisor("B2") { Skills = ["skill_ho_so", "skill_tra_loi"] };

        await new ChatService(advisor, new FakeSessions(), new FakeProfileFlow(), analyses)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        Assert.Empty(analyses.Saved);
    }

    // Gợi ý trường trong chat: advisor trả action "recommend" (lộ trình đại học thẳng) thì chạy gợi ý có sẵn
    private const string OneSchool = """{ "scored": [ { "code": "ASU", "name": "Arizona State University" } ], "match": [ { "code": "ASU" } ] }""";

    [Fact]
    public async Task RecommendAction_RunsTheRecommendation_AndAddsTheSummaryToTheAnswer()
    {
        var userId = Guid.NewGuid();
        var runner = new FakeRunner { Result = new Recommendation { ItemsJson = OneSchool } };
        var store = new FakeSessions();

        var res = await new ChatService(new FakeAdvisor("B4") { Action = "recommend" }, store, new FakeProfileFlow(), null, runner)
            .ChatAsync(userId, new AdvisorChatRequest(Hello));

        Assert.Equal(userId, runner.CalledFor);
        Assert.StartsWith("ok\n\nMình đã tìm được các trường phù hợp", res.Answer); // câu lộ trình giữ nguyên ở đầu
        Assert.Contains("- Vừa sức: Arizona State University", res.Answer);
        Assert.Equal(new Widget(ChatRecommendation.WidgetType), res.Widget); // nút xem đầy đủ
        Assert.Null(res.Action); // action là việc nội bộ, không trả cho trình duyệt
        Assert.Contains("\"hasResults\":true", Assert.Single(store.Rows).StateJson); // lượt sau skill_explain chạy được
    }

    [Fact]
    public async Task NoAction_DoesNotRunTheRecommendation()
    {
        var runner = new FakeRunner();

        var res = await new ChatService(new FakeAdvisor("B4"), new FakeSessions(), new FakeProfileFlow(), null, runner)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        Assert.Null(runner.CalledFor);
        Assert.Equal("ok", res.Answer);
    }

    [Fact]
    public async Task RecommendationSummary_IsNotSavedAsPartOfTheRoadmapAnalysis()
    {
        var analyses = new FakeAnalyses();
        var runner = new FakeRunner { Result = new Recommendation { ItemsJson = OneSchool } };
        var advisor = new FakeAdvisor("B4") { Skills = ["skill_lo_trinh"], Action = "recommend" };

        await new ChatService(advisor, new FakeSessions(), new FakeProfileFlow(), analyses, runner)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        Assert.DoesNotContain("Arizona", Assert.Single(analyses.Saved).Json);
    }

    [Theory]
    [InlineData("Hồ sơ chưa chọn ngành, cần chọn ngành trước khi gợi ý trường.", "Mình chưa tạo được danh sách trường: Hồ sơ chưa chọn ngành")]
    [InlineData("Gemini trả code lạ", ChatRecommendation.Invalid)]
    public async Task RecommendationFails_KeepsTheRoadmap_AndExplains(string error, string expected)
    {
        var runner = new FakeRunner { Error = new InvalidOperationException(error) };
        var store = new FakeSessions();

        var res = await new ChatService(new FakeAdvisor("B4") { Action = "recommend" }, store, new FakeProfileFlow(), null, runner)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        Assert.StartsWith("ok\n\n", res.Answer);
        Assert.Contains(expected, res.Answer);
        Assert.Null(res.Widget);
        Assert.DoesNotContain("hasResults", Assert.Single(store.Rows).StateJson); // chưa có kết quả thì không bật
    }

    [Fact]
    public async Task RecommendationServiceDown_SaysBusy()
    {
        var runner = new FakeRunner { Error = new HttpRequestException("advisor tắt") };

        var res = await new ChatService(new FakeAdvisor("B4") { Action = "recommend" }, new FakeSessions(), new FakeProfileFlow(), null, runner)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        Assert.Equal($"ok\n\n{ChatRecommendation.Busy}", res.Answer);
    }

    // Gemini treo: advisor thử lại nhiều lần, HttpClient của .NET hết giờ (TaskCanceledException). Không được làm lỗi 500 cả lượt chat
    [Fact]
    public async Task RecommendationTimesOut_SaysBusy_AndKeepsTheRoadmap()
    {
        var runner = new FakeRunner { Error = new TaskCanceledException("HttpClient.Timeout") };

        var res = await new ChatService(new FakeAdvisor("B4") { Action = "recommend" }, new FakeSessions(), new FakeProfileFlow(), null, runner)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        Assert.Equal($"ok\n\n{ChatRecommendation.Busy}", res.Answer);
    }

    // Trả lời theo từng ý (stream): các tin của advisor đi thẳng ra ngoài, còn lại chạy y như lượt chat thường
    [Fact]
    public async Task OnEvent_ForwardsTheAdvisorEvents_AndStillSavesTheSession()
    {
        var advisor = new FakeAdvisor("B2") { Events = [new("progress", Answers: 2), new("partial", Text: "đáp 1")] };
        var store = new FakeSessions();
        var seen = new List<AdvisorStreamEvent>();

        var res = await new ChatService(advisor, store, new FakeProfileFlow()).ChatAsync(
            Guid.NewGuid(), new AdvisorChatRequest(Hello), default, e => { seen.Add(e); return Task.CompletedTask; });

        Assert.Equal(advisor.Events, seen);
        Assert.Equal("B2", Assert.Single(store.Rows).CurrentStep);
        Assert.Null(res.State);                      // trạng thái nội bộ vẫn không lộ ra ngoài
    }

    [Fact]
    public async Task WithoutOnEvent_TheAdvisorIsNotAskedToStream()
    {
        var advisor = new FakeAdvisor("B2") { Events = [new("partial", Text: "không được gửi")] };

        await new ChatService(advisor, new FakeSessions(), new FakeProfileFlow()).ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        Assert.Equal(0, advisor.StreamCalls);
    }

    // Câu cuối cùng vẫn do bước sau advisor quyết định (gợi ý trường thay câu trả lời), nên dòng final mới là câu để hiện
    [Fact]
    public async Task OnEvent_TheFinalAnswerStillGoesThroughTheExplainStep()
    {
        var userId = Guid.NewGuid();
        var runner = new FakeRunner { Explanation = Explanation() };
        var advisor = new FakeAdvisor("B4") { Action = "explain", ActionData = IowaCode, Events = [new("partial", Text: "câu tạm")] };

        var res = await new ChatService(advisor, new FakeSessions(), new FakeProfileFlow(), null, runner)
            .ChatAsync(userId, new AdvisorChatRequest(Hello), default, _ => Task.CompletedTask);

        Assert.StartsWith("University of Iowa:", res.Answer);
    }

    // Đường /chat/stream trả từng dòng JSON: các tin, rồi final là đúng JSON của đường /chat
    [Fact]
    public async Task StreamEndpoint_WritesOneJsonLinePerEvent_ThenTheFinalResponse()
    {
        var advisor = new FakeAdvisor("B2") { Events = [new("progress", Answers: 1), new("partial", Text: "đáp 1")] };
        var controller = StreamController(advisor, out var body);

        var result = await controller.ChatStream(new AdvisorChatRequest(Hello), Json(), CancellationToken.None);

        Assert.IsType<Microsoft.AspNetCore.Mvc.EmptyResult>(result);
        Assert.Equal("application/x-ndjson", controller.Response.ContentType);
        Assert.Equal("no", controller.Response.Headers["X-Accel-Buffering"]);
        var lines = Lines(body);
        Assert.Equal(3, lines.Count);
        Assert.Equal(("progress", 1), (lines[0].GetProperty("type").GetString(), lines[0].GetProperty("answers").GetInt32()));
        Assert.Equal("đáp 1", lines[1].GetProperty("text").GetString());
        var final = lines[2];
        Assert.Equal("final", final.GetProperty("type").GetString());
        Assert.Equal("ok", final.GetProperty("response").GetProperty("answer").GetString());
        Assert.False(final.GetProperty("response").TryGetProperty("action", out var action) && action.ValueKind != JsonValueKind.Null);
    }

    [Fact]
    public async Task StreamEndpoint_WhenTheAdvisorFails_EndsWithAnErrorLine()
    {
        var advisor = new FakeAdvisor("B2") { Events = [new("progress", Answers: 1)], StreamError = new HttpRequestException("advisor tắt") };
        var controller = StreamController(advisor, out var body);

        await controller.ChatStream(new AdvisorChatRequest(Hello), Json(), CancellationToken.None);

        var lines = Lines(body);
        Assert.Equal(["progress", "error"], lines.Select(l => l.GetProperty("type").GetString()));
    }

    [Fact]
    public async Task StreamEndpoint_RejectsAnEmptyMessageListBeforeStreaming()
    {
        var controller = StreamController(new FakeAdvisor("B2"), out var body);

        var result = await controller.ChatStream(new AdvisorChatRequest([]), Json(), CancellationToken.None);

        Assert.IsNotType<Microsoft.AspNetCore.Mvc.EmptyResult>(result);
        Assert.Equal(0, body.Length);
    }

    private static Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.Mvc.JsonOptions> Json() =>
        Microsoft.Extensions.Options.Options.Create(new Microsoft.AspNetCore.Mvc.JsonOptions());

    private static StudyAbroad.Api.Controllers.AdvisorController StreamController(FakeAdvisor advisor, out MemoryStream body)
    {
        body = new MemoryStream();
        var http = new Microsoft.AspNetCore.Http.DefaultHttpContext
        {
            User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
                [new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())], "test")),
        };
        http.Response.Body = body;
        var service = new ChatService(advisor, new FakeSessions(), new FakeProfileFlow());
        return new StudyAbroad.Api.Controllers.AdvisorController(service, Microsoft.Extensions.Logging.Abstractions.NullLogger<StudyAbroad.Api.Controllers.AdvisorController>.Instance)
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = http },
        };
    }

    private static List<JsonElement> Lines(MemoryStream body) =>
        System.Text.Encoding.UTF8.GetString(body.ToArray())
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => JsonDocument.Parse(l).RootElement.Clone())
            .ToList();

    // Học sinh đóng trang giữa chừng (request bị hủy) thì không nuốt lỗi: để ASP.NET dừng lượt chat như bình thường
    [Fact]
    public async Task RequestCancelledByTheUser_IsNotSwallowed()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var runner = new FakeRunner { Error = new TaskCanceledException() };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new ChatService(new FakeAdvisor("B4") { Action = "recommend" }, new FakeSessions(), new FakeProfileFlow(), null, runner)
                .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello), cts.Token));
    }

    // Hỏi giải thích một trường: advisor trả action explain kèm mã trường, .NET gọi phần tư vấn từng trường có sẵn
    private static readonly JsonElement IowaCode = JsonDocument.Parse("""{"code":"iowa"}""").RootElement;

    private static SchoolExplanation Explanation(string status = "OK") =>
        new("iowa", "University of Iowa", status, "Iowa là trường khó vào với hồ sơ hiện tại.",
            ["Ngoại khóa nổi bật."], ["Nâng SAT lên khoảng 1450."]);

    [Fact]
    public async Task ExplainAction_ReplacesTheTempAnswerWithTheSchoolAdvice()
    {
        var userId = Guid.NewGuid();
        var runner = new FakeRunner { Explanation = Explanation() };

        var res = await new ChatService(new FakeAdvisor("B4") { Action = "explain", ActionData = IowaCode }, new FakeSessions(), new FakeProfileFlow(), null, runner)
            .ChatAsync(userId, new AdvisorChatRequest(Hello));

        Assert.Equal(("iowa", userId), (runner.ExplainedCode, runner.ExplainedFor!.Value));
        Assert.Equal(
            "University of Iowa: Iowa là trường khó vào với hồ sơ hiện tại.\n\n" +
            "Điểm mạnh của hồ sơ với trường này:\n- Ngoại khóa nổi bật.\n\n" +
            "Bạn có thể cải thiện:\n- Nâng SAT lên khoảng 1450.",
            res.Answer);
        Assert.Null(res.Action);     // việc nội bộ, không trả cho trình duyệt
        Assert.Null(res.ActionData);
    }

    // Thẻ giải thích do code dựng gửi trước, rồi mới tới lời tư vấn (AI); widget cũng nằm trong câu trả lời cuối
    [Fact]
    public async Task ExplainAction_SendsTheCardBeforeTheAdviceIsWritten()
    {
        var order = new List<string>();
        var runner = new FakeRunner { Explanation = Explanation(), OnExplain = () => order.Add("advice") };
        AdvisorStreamEvent? card = null;

        var res = await new ChatService(new FakeAdvisor("B4") { Action = "explain", ActionData = IowaCode }, new FakeSessions(), new FakeProfileFlow(), null, runner)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello), default,
                e => { if (e.Type == "card") { card = e; order.Add("card"); } return Task.CompletedTask; });

        Assert.Equal(["card", "advice"], order);
        Assert.Equal("explain", card!.Widget!.Type);
        Assert.Equal("explain", res.Widget!.Type);
        Assert.Equal("University of Iowa", res.Widget.Data!.Value.GetProperty("card").GetProperty("name").GetString());
        Assert.Equal("R", res.Widget.Data.Value.GetProperty("card").GetProperty("g2").GetString());
    }

    // Giải thích đi theo cái học sinh đang xem trên trang: có giá trị giả định thì theo giả định và thẻ ghi rõ gốc
    private static async Task<(AdvisorChatResponse Res, FakeRunner Runner)> ExplainWith(
        SimulationOverrides? simulation, string? basis = null)
    {
        var runner = new FakeRunner { Explanation = Explanation() };
        var res = await new ChatService(new FakeAdvisor("B4") { Action = "explain", ActionData = IowaCode }, new FakeSessions(), new FakeProfileFlow(), null, runner)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello, Simulation: simulation, Action: basis is null ? null : new ChatAction("explain", "iowa", null, basis)));
        return (res, runner);
    }

    private static readonly SimulationOverrides Gpa36 = new(3.6m, null, null, null, null, null, null);

    [Fact]
    public async Task Explain_WhileTheStudentIsSimulating_FollowsTheSimulationAndLabelsIt()
    {
        var (res, runner) = await ExplainWith(Gpa36);

        Assert.Equal(Gpa36, runner.ExplainOverrides);
        Assert.Equal("GPA 3.6", runner.ExplainBasisLabel);
        var basis = res.Widget!.Data!.Value.GetProperty("basis");
        Assert.Equal(("simulated", "GPA 3.6"), (basis.GetProperty("kind").GetString(), basis.GetProperty("label").GetString()));
    }

    [Fact]
    public async Task Explain_WithoutASimulation_IsTheRealProfileAndSaysSo()
    {
        var (res, runner) = await ExplainWith(null);

        Assert.Null(runner.ExplainOverrides);
        Assert.Null(runner.ExplainBasisLabel);
        Assert.Equal("real", res.Widget!.Data!.Value.GetProperty("basis").GetProperty("kind").GetString());
    }

    [Fact]
    public async Task Explain_TheRealProfileButton_IgnoresTheSimulationOnThePage()
    {
        var (res, runner) = await ExplainWith(Gpa36, basis: "real");

        Assert.Null(runner.ExplainOverrides);
        Assert.Equal("real", res.Widget!.Data!.Value.GetProperty("basis").GetProperty("kind").GetString());
    }

    [Fact]
    public async Task Explain_AnEmptySimulationIsNotASimulation()
    {
        var (_, runner) = await ExplainWith(new SimulationOverrides(null, null, null, null, null, null, null));

        Assert.Null(runner.ExplainOverrides);
    }

    [Fact]
    public async Task ExplainAction_WithoutACode_SaysItFailed()
    {
        var runner = new FakeRunner { Explanation = Explanation() };

        var res = await new ChatService(new FakeAdvisor("B4") { Action = "explain" }, new FakeSessions(), new FakeProfileFlow(), null, runner)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        Assert.Equal(ChatExplanation.Failed, res.Answer);
        Assert.Null(runner.ExplainedCode);
    }

    [Fact]
    public async Task ExplainAction_WhenTheAdviceIsNotOk_SaysItFailed()
    {
        var runner = new FakeRunner { Explanation = Explanation("FAILED") };

        var res = await new ChatService(new FakeAdvisor("B4") { Action = "explain", ActionData = IowaCode }, new FakeSessions(), new FakeProfileFlow(), null, runner)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        Assert.Equal(ChatExplanation.Failed, res.Answer);
    }

    [Theory]
    [InlineData("Chưa có trường iowa trong lần gợi ý gần nhất.", "Mình chưa giải thích được trường này: Chưa có trường iowa")]
    [InlineData("Dữ liệu lưu bị hỏng", ChatRecommendation.Invalid)]
    public async Task ExplainAction_WhenTheServiceRejects_ExplainsWhy(string error, string expected)
    {
        var runner = new FakeRunner { ExplainError = new InvalidOperationException(error) };

        var res = await new ChatService(new FakeAdvisor("B4") { Action = "explain", ActionData = IowaCode }, new FakeSessions(), new FakeProfileFlow(), null, runner)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        Assert.Contains(expected, res.Answer);
    }

    // Hỏi "vì sao" về trường không nằm trong danh sách (bị bộ lọc cứng loại): nói đúng lý do bằng thẻ goal-seeking, không báo lỗi
    [Fact]
    public async Task ExplainAction_ForASchoolOutsideTheList_AnswersWithTheReasonItIsExcluded()
    {
        var excluded = GoalOf() with { Position = new GoalPosition("excluded", "M", "STATE", false), FilterReason = "STATE" };
        var runner = new FakeRunner
        {
            ExplainError = new InvalidOperationException("Chưa có trường 188429 trong lần gợi ý gần nhất."),
            GoalResult = excluded,
        };

        var res = await new ChatService(new FakeAdvisor("B4") { Action = "explain", ActionData = IowaCode }, new FakeSessions(), new FakeProfileFlow(), null, runner)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        Assert.Equal("goal_seek", res.Widget!.Type);
        Assert.DoesNotContain("chưa giải thích được", res.Answer);
        Assert.Equal(["Sửa hồ sơ", ChatNextSteps.CenterLabel], res.QuickReplies!.Select(q => q.Label));
    }

    [Fact]
    public async Task ExplainAction_WhenTheServiceIsBusyOrTimesOut_SaysBusy()
    {
        foreach (Exception error in new Exception[] { new HttpRequestException("advisor tắt"), new TaskCanceledException("HttpClient.Timeout") })
        {
            var runner = new FakeRunner { ExplainError = error };

            var res = await new ChatService(new FakeAdvisor("B4") { Action = "explain", ActionData = IowaCode }, new FakeSessions(), new FakeProfileFlow(), null, runner)
                .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

            Assert.Equal(ChatExplanation.Busy, res.Answer);
        }
    }

    // What-if: advisor trả action simulate kèm giá trị đã kiểm tra, .NET chạy mô phỏng có sẵn rồi nhờ advisor viết lời
    private static readonly JsonElement Sat1500 = JsonDocument.Parse("""{"changes":{"sat":1500},"unsupported":[]}""").RootElement;

    private static SimulationResult Simulation() => new(
        new SimulationProfile(3.5m, 1290, null, 6.5m, 40_000m, "preferred", 3),
        new GroupSummary(2, 1, 1, 0, 0, 0), new GroupSummary(2, 0, 2, 0, 0, 0),
        [new SchoolChange("bu", "Boston University", "R", "M")],
        "{}",
        """{"scored":[{"code":"bu","name":"Boston University"},{"code":"ne","name":"Northeastern University"}],"reach":[],"match":[{"code":"bu","why":["SAT_ABOVE"]},{"code":"ne","why":[]}],"safety":[],"aid":[],"other":[]}""");

    private static ScenarioNoteResponseDto Written(string status = "OK", string answer = "Với SAT 1500, Boston University lên Vừa sức.") =>
        new("2.0", "gemini", status, answer);

    private static async Task<AdvisorChatResponse> SimulateChat(FakeAdvisor advisor, FakeRunner runner) =>
        await new ChatService(advisor, new FakeSessions(), new FakeProfileFlow(), null, runner)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

    [Fact]
    public async Task SimulateAction_RunsTheSimulationAndAnswersWithTheWordsOfTheAdvisor()
    {
        var advisor = new FakeAdvisor("B4") { Action = "simulate", ActionData = Sat1500, Note = Written() };
        var runner = new FakeRunner { Simulation = Simulation() };

        var res = await SimulateChat(advisor, runner);

        Assert.Equal(1500m, runner.SimulatedWith!.Sat);
        Assert.Equal("SAT 1500 (hiện tại 1290)", advisor.LastNote!.Label);
        Assert.Equal("Với SAT 1500, Boston University lên Vừa sức.", res.Answer);
        Assert.Equal("simulate", res.Widget!.Type);
        Assert.Equal(1500, res.Widget.Data!.Value.GetProperty("sat").GetInt32());
        Assert.Null(res.Action);     // việc nội bộ, không trả cho trình duyệt
        Assert.Null(res.ActionData);
    }

    // ---- what-if về ưu tiên (chi phí thấp, học bổng nhiều, danh tiếng): code tính, chỉ đổi thứ tự trong nhóm

    private static SimulationResult OrderedSimulation(params string[] matchOrder) => new(
        new SimulationProfile(3.5m, 1290, null, 6.5m, 40_000m, "preferred", 3),
        new GroupSummary(2, 0, 2, 0, 0, 0), new GroupSummary(2, 0, 2, 0, 0, 0), [],
        "{}",
        "{\"scored\":[{\"code\":\"bu\",\"name\":\"Boston University\",\"cost\":52000,\"aidPolicy\":\"none\",\"h\":0,\"q\":0.9},"
        + "{\"code\":\"ne\",\"name\":\"Northeastern University\",\"cost\":31000,\"aidAvg\":25000,\"aidPolicy\":\"need_and_merit\",\"h\":1,\"q\":0.7}],"
        + "\"reach\":[],\"match\":[" + string.Join(",", matchOrder.Select(c => "{\"code\":\"" + c + "\",\"why\":[]}")) + "],\"safety\":[],\"aid\":[],\"other\":[]}");

    private static readonly JsonElement AidFirst =
        JsonDocument.Parse("""{"changes":{},"unsupported":[],"priorities":{"aid":4}}""").RootElement;

    [Fact]
    public async Task SimulateAction_WithOnlyPriorities_ReordersInsideTheGroupAndSaysSo()
    {
        var advisor = new FakeAdvisor("B4") { Action = "simulate", ActionData = AidFirst };
        var runner = new FakeRunner
        {
            Latest = new Recommendation { CriteriaJson = """{"pref":[3,2,1]}""" },
            // ưu tiên học bổng 4 (giữ chi phí 3, danh tiếng 1): Northeastern (học bổng cao) lên đầu
            SimulationFor = pref => pref!.SequenceEqual([3, 4, 1]) ? OrderedSimulation("ne", "bu") : OrderedSimulation("bu", "ne"),
        };

        var res = await SimulateChat(advisor, runner);

        Assert.Equal([3, 4, 1], runner.SimulatedPrefs[0]);          // học bổng đổi, hai mức kia giữ như lần gợi ý đã lưu
        Assert.Equal([3, 2, 1], runner.SimulatedPrefs[1]);          // danh sách so sánh: cùng hồ sơ, ưu tiên cũ
        Assert.Contains("học bổng nhiều 4/4 (trước là 2/4)", res.Answer);
        Assert.Contains("không đổi nhóm của trường nào", res.Answer);
        Assert.Contains("Northeastern University (Vừa sức): từ vị trí 2 lên vị trí 1, người được học bổng nhận trung bình 25.000 USD mỗi năm (khoảng 81% chi phí)", res.Answer);
        Assert.Contains("Boston University (Vừa sức): từ vị trí 1 xuống vị trí 2, trường không có học bổng cho du học sinh", res.Answer);
        Assert.DoesNotContain("/100", res.Answer);   // nói bằng USD thật, không nói điểm
        Assert.Null(advisor.LastNote);                              // không có giá trị hồ sơ nên không nhờ AI viết lời
        Assert.Equal([3, 4, 1], res.Widget!.Data!.Value.GetProperty("pref").EnumerateArray().Select(e => e.GetInt32()));
    }

    // ---- nút bước tiếp theo sau what-if: hiểu vì sao đổi, rồi xem cần gì để vào trường khó

    private static SimulationResult WithReach() => new(
        new SimulationProfile(3.5m, 1290, null, 6.5m, 40_000m, "preferred", 3),
        new GroupSummary(3, 1, 2, 0, 0, 0), new GroupSummary(3, 1, 2, 0, 0, 0),
        [new SchoolChange("bu", "Boston University", "R", "M"), new SchoolChange("gone", "Gone University", "M", "OUT")],
        "{}",
        """{"scored":[{"code":"bu","name":"Boston University"},{"code":"ne","name":"Northeastern University"},{"code":"mit","name":"MIT"}],"reach":[{"code":"bu","why":[]},{"code":"mit","why":[]}],"match":[{"code":"ne","why":[]}],"safety":[],"aid":[],"other":[]}""");

    [Fact]
    public async Task SimulateAction_OffersTheNextSteps_AsButtonsThatRunByCode()
    {
        var advisor = new FakeAdvisor("B4") { Action = "simulate", ActionData = Sat1500, Note = Written() };
        var runner = new FakeRunner { Simulation = WithReach() };

        var res = await SimulateChat(advisor, runner);

        Assert.Equal(3, res.QuickReplies!.Count);
        Assert.Equal(ChatNextSteps.CenterLabel, res.QuickReplies[2].Label);      // nút gặp tư vấn viên luôn ở cuối
        var why = res.QuickReplies[0];
        Assert.Equal("Vì sao các trường đổi nhóm?", why.Label);      // không nêu tên trường; trường bị loại (Gone) không có trong danh sách chọn
        Assert.Null(why.Action!.SchoolCode);
        Assert.Equal(["bu"], why.Action.SchoolCodes);
        var need = res.QuickReplies[1];
        Assert.Equal("Cần gì để vào trường em muốn?", need.Label);               // không chọn sẵn trường: học sinh chưa nói quan tâm trường nào
        Assert.Equal(new ChatAction("goal_seek"), need.Action);
    }

    [Fact]
    public async Task SimulateAction_WithOnlyPriorities_OffersToExplainTheSchoolThatMovedMost()
    {
        var advisor = new FakeAdvisor("B4") { Action = "simulate", ActionData = AidFirst };
        var runner = new FakeRunner
        {
            SimulationFor = pref => pref!.SequenceEqual([2, 4, 2]) ? OrderedSimulation("ne", "bu") : OrderedSimulation("bu", "ne"),
        };

        var res = await SimulateChat(advisor, runner);

        Assert.Equal(2, res.QuickReplies!.Count);
        var why = res.QuickReplies[0];
        Assert.Equal("Vì sao các trường đổi vị trí?", why.Label);
        Assert.Null(why.Action!.SchoolCode);
        Assert.Contains("ne", why.Action.SchoolCodes!);
    }

    [Fact]
    public async Task SimulateAction_WhenNothingChanged_OffersNoButtons()
    {
        var advisor = new FakeAdvisor("B4") { Action = "simulate", ActionData = Sat1500, Note = Written() };
        var runner = new FakeRunner { Simulation = OrderedSimulation("bu", "ne") };   // không đổi nhóm, không có trường Khó vào

        var res = await SimulateChat(advisor, runner);

        Assert.Null(res.QuickReplies);
    }

    [Fact]
    public async Task SimulateAction_WhenTheOrderDoesNotChange_SaysSoAndInventsNoMoves()
    {
        var advisor = new FakeAdvisor("B4") { Action = "simulate", ActionData = AidFirst };
        var runner = new FakeRunner { Simulation = OrderedSimulation("bu", "ne") };   // không có Latest: mức hiện tại 2-2-2

        var res = await SimulateChat(advisor, runner);

        Assert.Contains("gần như không đổi", res.Answer);
        Assert.DoesNotContain("từ vị trí", res.Answer);
    }

    [Fact]
    public async Task SimulateAction_WithPrioritiesAndProfileValues_AnswersBoth()
    {
        var data = JsonDocument.Parse("""{"changes":{"sat":1500},"unsupported":[],"priorities":{"cost":4}}""").RootElement;
        var advisor = new FakeAdvisor("B4") { Action = "simulate", ActionData = data, Note = Written() };
        var runner = new FakeRunner { Simulation = OrderedSimulation("bu", "ne") };

        var res = await SimulateChat(advisor, runner);

        Assert.Equal(1500m, runner.SimulatedWith!.Sat);
        Assert.StartsWith("Với SAT 1500, Boston University lên Vừa sức.", res.Answer);
        Assert.Contains("chi phí thấp 4/4", res.Answer);
        var widget = res.Widget!.Data!.Value;
        Assert.Equal(1500, widget.GetProperty("sat").GetInt32());
        Assert.Equal(3, widget.GetProperty("pref").GetArrayLength());
    }

    [Theory]
    [InlineData("""{"changes":{},"priorities":{"aid":5}}""")]       // mức ngoài 0..4
    [InlineData("""{"changes":{},"priorities":{"aid":"cao"}}""")]
    [InlineData("""{"changes":{},"priorities":{}}""")]
    public void Parse_IgnoresPrioritiesThatAreNotALevelFromZeroToFour(string json)
    {
        Assert.Null(ChatSimulation.Parse(JsonDocument.Parse(json).RootElement));
    }

    [Fact]
    public void BasePref_ReadsTheSavedOneAndFallsBackToBalanced()
    {
        Assert.Equal([0, 4, 2], ChatSimulation.BasePref("""{"pref":[0,4,2]}"""));
        Assert.Equal([2, 2, 2], ChatSimulation.BasePref(null));
        Assert.Equal([2, 2, 2], ChatSimulation.BasePref("""{"pref":[9,0,0]}"""));
        Assert.Equal([2, 2, 2], ChatSimulation.BasePref("không phải json"));
        Assert.Equal([3, 2, 4], ChatSimulation.MergePref([2, 2, 4], new ChatPriorities(3, null, null)));
    }

    [Theory]
    [InlineData("FAILED")]   // Gemini lỗi hoặc lời không qua kiểm tra số liệu
    [InlineData("OK")]       // trả rỗng cũng coi như lỗi
    public async Task SimulateAction_WhenTheWordsAreMissing_UsesTheSentenceBuiltByCode(string status)
    {
        var advisor = new FakeAdvisor("B4") { Action = "simulate", ActionData = Sat1500, Note = Written(status, "") };

        var res = await SimulateChat(advisor, new FakeRunner { Simulation = Simulation() });

        Assert.StartsWith("Với SAT 1500 (hiện tại 1290) thì mình gợi ý như sau:", res.Answer);
        Assert.Contains("- Vừa sức: Boston University, Northeastern University", res.Answer);
        Assert.Equal("simulate", res.Widget!.Type);   // panel vẫn hiện kết quả
    }

    [Fact]
    public async Task SimulateAction_WhenTheAdvisorIsDown_UsesTheSentenceBuiltByCode()
    {
        foreach (Exception error in new Exception[] { new HttpRequestException("advisor tắt"), new TaskCanceledException("HttpClient.Timeout") })
        {
            var advisor = new FakeAdvisor("B4") { Action = "simulate", ActionData = Sat1500, NoteError = error };

            var res = await SimulateChat(advisor, new FakeRunner { Simulation = Simulation() });

            Assert.StartsWith("Với SAT 1500 (hiện tại 1290) thì mình gợi ý như sau:", res.Answer);
        }
    }

    [Fact]
    public async Task SimulateAction_WithoutUsableValues_SaysItFailed_AndDoesNotSimulate()
    {
        var runner = new FakeRunner { Simulation = Simulation() };

        var res = await SimulateChat(new FakeAdvisor("B4") { Action = "simulate" }, runner);

        Assert.Equal(ChatSimulation.Failed, res.Answer);
        Assert.Null(runner.SimulatedWith);
        Assert.Null(res.Widget);
    }

    [Theory]
    [InlineData(typeof(ArgumentException), "SAT giả định phải từ 400 đến 1600.", "Mình chưa thử được kịch bản này: SAT giả định phải từ 400 đến 1600.")]
    [InlineData(typeof(InvalidOperationException), "Chưa có hồ sơ.", "Mình chưa thử được kịch bản này: Chưa có hồ sơ.")]
    [InlineData(typeof(InvalidOperationException), "Hồ sơ chưa có bậc học muốn theo.", "Mình chưa thử được kịch bản này: Hồ sơ chưa có bậc học")]
    [InlineData(typeof(InvalidOperationException), "Dữ liệu lưu bị hỏng", ChatSimulation.Failed)]
    public async Task SimulateAction_WhenTheSimulationIsRejected_ExplainsWhy(Type type, string message, string expected)
    {
        var runner = new FakeRunner { SimulateError = (Exception)Activator.CreateInstance(type, message)! };

        var res = await SimulateChat(new FakeAdvisor("B4") { Action = "simulate", ActionData = Sat1500 }, runner);

        Assert.Contains(expected, res.Answer);
        Assert.Null(res.Widget);
    }

    // "Cần gì để vào trường X" trong chat: advisor trả action goal_seek kèm tên trường chữ thô, .NET khớp tên rồi hỏi lại hoặc chạy goal-seeking
    private static readonly JsonElement BuAction = JsonDocument.Parse("""{"school":"BU","target":"match"}""").RootElement;

    private static NameMatch Found(string code = "188429", string name = "Boston University") =>
        new(NameMatchKind.Found, [new SchoolRef(code, name)]);

    private static SchoolGoalResult GoalOf() => new(
        "188429", "Boston University", "R", "M", "reachable", "M", [],
        new GoalPlanDto([new("sat", 1290, 1430)], 1.4, "hard"), null, [new GoalPlanDto([new("sat", 1290, 1430)], 1.4, "hard")], [],
        new GoalBudgetDto("ok", 38_000m, 50_000m, 34_600m, null, null), null, []);

    private static async Task<AdvisorChatResponse> GoalChat(JsonElement? data, FakeRunner runner) =>
        await new ChatService(new FakeAdvisor("B4") { Action = "goal_seek", ActionData = data }, new FakeSessions(), new FakeProfileFlow(), null, runner)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

    [Fact]
    public async Task GoalSeekAction_WhenTheSchoolIsClear_RunsGoalSeekingAndAnswersWithTheSummary()
    {
        var runner = new FakeRunner { Match = Found(), GoalResult = GoalOf() };

        var res = await GoalChat(BuAction, runner);

        Assert.Equal("BU", runner.SearchedFor);
        Assert.Equal(("188429", "match"), runner.GoalCalledWith);
        Assert.StartsWith("Mình hiểu bạn hỏi về Boston University, nhóm muốn đạt: Vừa sức.", res.Answer);
        Assert.Equal("goal_seek", res.Widget!.Type);
        // trường đang trong danh sách: giải thích trường này, rồi gặp tư vấn viên
        Assert.Equal(new ChatAction("explain", "188429"), res.QuickReplies![0].Action);
        Assert.Equal(ChatNextSteps.CenterLabel, res.QuickReplies[1].Label);
        Assert.Null(res.Action);     // việc nội bộ, không trả cho trình duyệt
    }

    // ---- hành động có cấu trúc từ nút trên thẻ trường: không qua advisor, không khớp tên

    private static async Task<(AdvisorChatResponse Res, FakeAdvisor Advisor)> ActionChat(ChatAction? action, FakeRunner runner)
    {
        var advisor = new FakeAdvisor("B4") { Action = "explain", ActionData = IowaCode };   // nếu advisor bị gọi nhầm sẽ trả kết quả khác hẳn
        var res = await new ChatService(advisor, new FakeSessions(), new FakeProfileFlow(), null, runner)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello, Action: action));
        return (res, advisor);
    }

    [Fact]
    public async Task ChatAction_GoalSeek_RunsOnTheCodeWithoutTheAdvisorOrNameMatching()
    {
        var runner = new FakeRunner { GoalResult = GoalOf() };

        var (res, advisor) = await ActionChat(new ChatAction("goal_seek", "188429", "safety"), runner);

        Assert.Null(advisor.LastRequest);                 // không gọi SLM
        Assert.Null(runner.SearchedFor);                  // không khớp tên
        Assert.Equal(("188429", "safety"), runner.GoalCalledWith);
        Assert.StartsWith("Mình hiểu bạn hỏi về Boston University", res.Answer);
        Assert.Equal("goal_seek", res.Widget!.Type);
        Assert.Null(res.Action);
    }

    // ---- hỏi thông tin một trường: cơ sở dữ liệu trước, không có mới giữ câu của kho kiến thức

    private static SchoolInfoDto InfoOf(string name = "Harvard University") => new(
        "harvard", name, "Cambridge", "MA", "private", "https://harvard.example.edu",
        new SchoolCostInfo(59_000m, 21_000m, 4_000m, 2_000m, 86_000m), new SchoolEnglishInfo(7m, null, null),
        new SchoolTestInfo("required", 1480, 1580, 33, 36), new SchoolAdmissionInfo(0.04m, 0.03m),
        new SchoolAidInfo("need_only", 70_000m, 600), new SchoolOutcomeInfo(0.98m, 0.97m), [], ["Economics"], [], null, null);

    private static JsonElement KnowledgeAction(string question, string? school = null, bool kbFound = false, string kb = "ok") =>
        JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["question"] = question, ["school"] = school, ["kb_answer"] = kb, ["kb_found"] = kbFound,
        })).RootElement;

    private static async Task<AdvisorChatResponse> KnowledgeChat(JsonElement data, FakeRunner runner) =>
        // Câu trả lời advisor gửi lên chính là kb_answer trong dữ liệu (kho kiến thức, hoặc câu tạm khi model đã bỏ qua kho), như thật
        await new ChatService(
                new FakeAdvisor("B4") { Action = "school_info", ActionData = data, Answer = data.TryGetProperty("kb_answer", out var kb) ? kb.GetString()! : "ok" },
                new FakeSessions(), new FakeProfileFlow(), null, runner)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

    [Fact]
    public async Task SchoolInfo_TheSlmGaveTheSchool_AnswersFromTheDatabaseInsteadOfTheKnowledgeBase()
    {
        var runner = new FakeRunner { Match = Found("harvard", "Harvard University"), Info = InfoOf() };

        var res = await KnowledgeChat(KnowledgeAction("học phí Harvard bao nhiêu", school: "Harvard"), runner);

        Assert.Equal("harvard", runner.InfoCode);
        Assert.Contains("học phí 59.000 USD", res.Answer);
        Assert.NotEqual("ok", res.Answer);
        Assert.Equal("school_info", res.Widget!.Type);   // thẻ số liệu hiện trên câu trả lời
        Assert.Equal(["cost"], res.Widget.Data!.Value.GetProperty("fields").EnumerateArray().Select(e => e.GetString()));
        Assert.Empty(res.Citations);
        Assert.Null(res.Action);
    }

    [Fact]
    public async Task SchoolInfo_TheCardCoversTheText_ExceptWhenTheTextAlsoSaysSomethingTheCardDoesNot()
    {
        var runner = new FakeRunner { Match = Found("harvard", "Harvard University"), Info = InfoOf() };

        var plain = await KnowledgeChat(KnowledgeAction("học phí Harvard bao nhiêu", school: "Harvard"), runner);
        var withUnstored = await KnowledgeChat(KnowledgeAction("học phí và ký túc xá của Harvard", school: "Harvard"), runner);

        Assert.Equal(plain.Answer, plain.Widget!.Data!.Value.GetProperty("coveredText").GetString());   // thẻ nói đủ: giao diện bỏ chữ
        Assert.False(withUnstored.Widget!.Data!.Value.TryGetProperty("coveredText", out _));            // có lời "chưa lưu": giữ chữ
        Assert.Contains("hệ thống chưa lưu", withUnstored.Answer);
    }

    // ---- model đã đọc câu và trả trường lẫn mục: tin model, từ khóa chỉ là dự phòng

    private static JsonElement ModelAction(string question, string? school, string[] fields, bool skipped = true) =>
        JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["question"] = question, ["school"] = school, ["fields"] = fields,
            ["kb_answer"] = "Mình đang tra số liệu của trường này nhé.", ["kb_found"] = false, ["kb_skipped"] = skipped,
        })).RootElement;

    [Fact]
    public async Task SchoolInfo_TheFieldsTheModelReturnedWinOverTheKeywordsInTheSentence()
    {
        var runner = new FakeRunner { Match = Found("harvard", "Harvard University"), Info = InfoOf() with { Gpa = new SchoolGpaInfo(3.9m, 0.7m, 0.9m) } };

        // Câu không có từ khóa nào (chỉ "harvard"), nhưng model đã trả muc = gpa
        var res = await KnowledgeChat(ModelAction("harvard", "harvard", ["gpa"]), runner);

        Assert.Contains("GPA trung bình của sinh viên trúng tuyển khoảng 3,9", res.Answer);
        Assert.DoesNotContain("Mình đang tra số liệu", res.Answer);   // câu tạm của advisor đã bị thay
        Assert.Equal(["gpa"], res.Widget!.Data!.Value.GetProperty("fields").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task SchoolInfo_ModelFieldsDecideEvenWhenTheSentenceMentionsAnotherTopic()
    {
        var runner = new FakeRunner { Match = Found("harvard", "Harvard University"), Info = InfoOf() };

        var res = await KnowledgeChat(ModelAction("học phí Harvard", "Harvard", ["ranking"]), runner);

        Assert.Equal(["ranking"], res.Widget!.Data!.Value.GetProperty("fields").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task SchoolInfo_ModelSkippedTheKnowledgeBase_ASchoolNotInTheSystemIsSaidClearly()
    {
        var runner = new FakeRunner { Match = new NameMatch(NameMatchKind.NotFound, []), Info = InfoOf() };

        var res = await KnowledgeChat(ModelAction("học phí Hogwarts", "Hogwarts", ["cost"]), runner);

        Assert.Contains("chưa thấy trường “Hogwarts”", res.Answer);
        Assert.DoesNotContain("Mình đang tra số liệu", res.Answer);
    }

    [Fact]
    public async Task SchoolInfo_ModelSkippedTheKnowledgeBase_ADatabaseFailureSaysItInsteadOfLeavingTheTempSentence()
    {
        var runner = new FakeRunner { Match = Found("harvard", "Harvard University") };   // Info = null: bộ giả ném "Chưa có trường"

        var res = await KnowledgeChat(ModelAction("học phí Harvard", "Harvard", ["cost"]), runner);

        Assert.Equal(ChatSchoolInfo.Unavailable, res.Answer);
    }

    [Fact]
    public async Task SchoolInfo_ModelSaysKhacBesideAKnownField_AnswersTheKnownAndAdmitsTheRest()
    {
        var runner = new FakeRunner { Match = Found("harvard", "Harvard University"), Info = InfoOf() };

        var res = await KnowledgeChat(ModelAction("học phí và ký túc xá Harvard", "Harvard", ["cost", "other"]), runner);

        Assert.Contains("học phí 59.000 USD", res.Answer);
        Assert.Contains("hệ thống chưa lưu", res.Answer);
    }

    [Fact]
    public async Task SchoolInfo_ModelAsksAboutHousing_AnswersTheKnownFieldAndAdmitsTheSystemHasNoHousingData()
    {
        var runner = new FakeRunner { Match = Found("harvard", "Harvard University"), Info = InfoOf() };

        var res = await KnowledgeChat(ModelAction("học phí và ký túc xá Harvard", "Harvard", ["cost", "housing"]), runner);

        Assert.Contains("học phí 59.000 USD", res.Answer);
        Assert.Contains("hệ thống chưa lưu", res.Answer);
    }

    // ---- tìm trường theo điều kiện: model trả bo_loc, advisor chuẩn hóa, backend lọc và trả lời bằng số liệu thật (không Gemini)

    private static JsonElement SearchAction(object filters, string sort = "quality", string[]? unresolved = null) =>
        JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["question"] = "tìm trường", ["filters"] = filters, ["sort"] = sort, ["unresolved"] = unresolved ?? [],
            ["kb_answer"] = "Mình đang tra số liệu của trường này nhé.",
        })).RootElement;

    private static async Task<(AdvisorChatResponse Res, FakeRunner Runner)> SearchChat(JsonElement data, FakeRunner runner)
    {
        var res = await new ChatService(
                new FakeAdvisor("B4") { Action = "school_search", ActionData = data, Answer = "Mình đang tra số liệu của trường này nhé." },
                new FakeSessions(), new FakeProfileFlow(), null, runner)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));
        return (res, runner);
    }

    private static SchoolSearchItem SearchItem(string name, string state = "CA", decimal? cost = 40_000m, decimal? accept = 0.3m, int? rank = 20) =>
        new(name.ToLowerInvariant(), name, "Los Angeles", state, "public", cost, accept, "need_and_merit", 20_000m, rank is null ? null : 0.8, rank);

    [Fact]
    public async Task SchoolSearch_PassesTheNormalizedFiltersAndSortToTheDatabaseSearch()
    {
        var runner = new FakeRunner { SearchResult = new SchoolSearchResult(1, [SearchItem("UCLA")], []) };
        var data = SearchAction(new { states = new[] { "CA", "TX" }, aid = "any", maxCostUsd = 30000, maxIelts = 6.5, ivy = false }, sort: "lowestCost");

        await SearchChat(data, runner);

        var (filters, sort) = runner.SearchedWith!.Value;
        Assert.Equal(["CA", "TX"], filters.States);
        Assert.Equal(("any", 30000m, 6.5m), (filters.Aid, filters.MaxCostUsd, filters.MaxIelts));
        Assert.Equal(SchoolSort.LowestCost, sort);
    }

    [Fact]
    public async Task SchoolSearch_SaysHowManyAndListsTheFirstFiveInOrder()
    {
        var items = Enumerable.Range(1, 8).Select(i => SearchItem($"School {i}", rank: i)).ToList();
        var runner = new FakeRunner { SearchResult = new SchoolSearchResult(8, items, []) };

        var (res, _) = await SearchChat(SearchAction(new { states = new[] { "CA" }, aid = "any" }), runner);

        Assert.Contains("Có 8 trường ở CA, có học bổng cho du học sinh.", res.Answer);
        Assert.Contains("Xếp theo xếp hạng (chất lượng) cao đến thấp.", res.Answer);
        Assert.Contains("1. School 1 (Los Angeles, CA, công lập): chi phí 40.000 USD · tỷ lệ nhận 30% · hạng #1", res.Answer);
        Assert.Contains("5. School 5", res.Answer);
        Assert.DoesNotContain("6. School 6", res.Answer);
        Assert.Contains("Và 3 trường khác.", res.Answer);
        Assert.DoesNotContain("Mình đang tra số liệu", res.Answer);
    }

    [Fact]
    public async Task SchoolSearch_GivesACardWithEveryResultAndTheNextStepButtons()
    {
        var items = Enumerable.Range(1, 8).Select(i => SearchItem($"School {i}", rank: i)).ToList();
        var runner = new FakeRunner { SearchResult = new SchoolSearchResult(8, items, []) };

        var (res, _) = await SearchChat(SearchAction(new { states = new[] { "CA" } }), runner);

        var data = res.Widget!.Data!.Value;
        Assert.Equal("school_search", res.Widget.Type);
        Assert.Equal(8, data.GetProperty("total").GetInt32());
        Assert.Equal(5, data.GetProperty("visible").GetInt32());
        Assert.Equal(8, data.GetProperty("items").GetArrayLength());                 // toàn bộ để "Xem thêm" mở ngay, không gửi tin
        Assert.Equal(res.Answer, data.GetProperty("coveredText").GetString());
        var compare = res.QuickReplies![0];
        Assert.Equal("So sánh hai trường đầu", compare.Label);
        Assert.Equal(new[] { "school 1", "school 2" }, compare.Action!.SchoolCodes);   // hai trường đầu theo thứ tự xếp
        Assert.Equal("Gợi ý trường theo hồ sơ của em", res.QuickReplies[1].Label);
    }

    [Fact]
    public async Task SchoolSearch_OneResult_HasNoCompareButton()
    {
        var runner = new FakeRunner { SearchResult = new SchoolSearchResult(1, [SearchItem("Only")], []) };

        var (res, _) = await SearchChat(SearchAction(new { states = new[] { "CA" } }), runner);

        Assert.Equal(["Gợi ý trường theo hồ sơ của em"], res.QuickReplies!.Select(b => b.Label));
    }

    [Fact]
    public async Task SchoolSearch_NoResult_HasNoCardAndNoButtons()
    {
        var (res, _) = await SearchChat(SearchAction(new { states = new[] { "CA" } }), new FakeRunner());

        Assert.Null(res.Widget);
        Assert.Null(res.QuickReplies);
    }

    // Nút "So sánh hai trường đầu": chạy thẳng theo mã trường, không qua advisor, ra bảng so sánh
    [Fact]
    public async Task ChatAction_CompareByCodes_RunsTheComparisonWithoutTheAdvisor()
    {
        var runner = CompareRunner();
        runner.MatchByName!["bu"] = Found("bu", "Boston University");
        runner.MatchByName["nyu"] = Found("nyu", "New York University");

        var (res, advisor) = await ActionChat(new ChatAction("school_compare", SchoolCodes: ["bu", "nyu"], Question: "So sánh"), runner);

        Assert.Null(advisor.LastRequest);
        Assert.StartsWith("So sánh Boston University và New York University:", res.Answer);
        Assert.Equal("school_compare", res.Widget!.Type);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    public async Task ChatAction_CompareNeedsTwoToFourCodes(int count)
    {
        var codes = Enumerable.Range(0, count).Select(i => $"c{i}").ToArray();

        var (_, advisor) = await ActionChat(new ChatAction("school_compare", SchoolCodes: codes), new FakeRunner { Explanation = Explanation() });

        Assert.NotNull(advisor.LastRequest);              // hành động sai: chat chạy như thường qua advisor
    }

    [Fact]
    public async Task SchoolSearch_NoResult_SaysWhichConditionToDropToGetSome()
    {
        var runner = new FakeRunner
        {
            SearchResult = new SchoolSearchResult(0, [], [new RelaxedFilter("loai_truong", 12), new RelaxedFilter("hoc_bong", 3)]),
        };

        var (res, _) = await SearchChat(SearchAction(new { states = new[] { "TX" }, control = "public", aid = "any" }), runner);

        Assert.Contains("Không có trường nào thỏa tất cả điều kiện", res.Answer);
        Assert.Contains("Nếu bỏ điều kiện loại trường thì có 12 trường.", res.Answer);
        Assert.Contains("Nếu bỏ điều kiện học bổng thì có 3 trường.", res.Answer);
    }

    [Fact]
    public async Task SchoolSearch_AConditionThatCouldNotBeReadStopsTheSearchAndAsksAgain()
    {
        var runner = new FakeRunner { SearchResult = new SchoolSearchResult(5, [SearchItem("UCLA")], []) };

        var (res, r) = await SearchChat(SearchAction(new { aid = "any" }, unresolved: ["bang “Califonia”"]), runner);

        Assert.Contains("chưa hiểu chắc điều kiện bang “Califonia”", res.Answer);
        Assert.Null(r.SearchedWith);                 // không tìm với phần điều kiện còn lại: danh sách sẽ sai ý
    }

    [Fact]
    public async Task SchoolSearch_NoFilterAtAll_AsksWhatToFilterBy()
    {
        var (res, r) = await SearchChat(SearchAction(new { }), new FakeRunner());

        Assert.Equal(ChatSchoolInfo.SearchNoFilter, res.Answer);
        Assert.Null(r.SearchedWith);
    }

    // ---- so sánh từ hai trường: model trả schools, advisor chuyển sang, backend lấy số liệu từng trường và dựng bảng

    private static JsonElement CompareAction(string[] schools, string[] fields) =>
        JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["question"] = "so sánh", ["schools"] = schools, ["fields"] = fields, ["kb_answer"] = "Mình đang tra số liệu của trường này nhé.",
        })).RootElement;

    private static async Task<AdvisorChatResponse> CompareChat(JsonElement data, FakeRunner runner) =>
        (await CompareChatWith(data, runner)).Res;

    private static async Task<(AdvisorChatResponse Res, FakeAdvisor Advisor)> CompareChatWith(
        JsonElement data, FakeRunner runner, CompareNoteResponseDto? note = null, Exception? noteError = null)
    {
        var advisor = new FakeAdvisor("B4")
        {
            Action = "school_compare", ActionData = data, Answer = "Mình đang tra số liệu của trường này nhé.",
            CompareNote = note ?? new("1.0", "gemini", "FAILED", ""), CompareNoteError = noteError,
        };
        var res = await new ChatService(advisor, new FakeSessions(), new FakeProfileFlow(), null, runner)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));
        return (res, advisor);
    }

    // ---- nhận xét của Gemini cho phần so sánh: bảng số liệu do code luôn có, nhận xét đứng sau, có nhãn "AI nhận xét"

    [Fact]
    public async Task SchoolCompare_GeminiAddsAShortNoteAfterTheTable_LabelledAsAi()
    {
        var note = new CompareNoteResponseDto("1.0", "gemini", "OK", "New York University tốn ít tiền hơn mỗi năm.");

        var (res, advisor) = await CompareChatWith(CompareAction(["BU", "NYU"], ["cost"]), CompareRunner(), note);

        // Chỉ lời của AI (đã nêu đủ số liệu), không kèm bảng code dựng để khỏi nói hai lần; chi phí thì thêm lời nhắc kiểm tra lại
        Assert.StartsWith("New York University tốn ít tiền hơn mỗi năm.", res.Answer);
        Assert.Contains("kiểm tra lại", res.Answer);
        Assert.DoesNotContain("So sánh Boston University và New York University:", res.Answer);
        Assert.DoesNotContain("AI nhận xét", res.Answer);
        // Gemini nhận đúng các dòng số liệu (kèm câu chênh lệch chi phí, để không phải tự trừ) và tên các trường khác để chặn nhắc nhầm
        var sent = advisor.LastCompareNote!;
        // Thẻ so sánh đi kèm: có bảng và nhận xét tách riêng; giao diện bỏ đoạn chữ vì thẻ đã nói đủ
        var card = res.Widget!;
        Assert.Equal("school_compare", card.Type);
        Assert.Equal("New York University tốn ít tiền hơn mỗi năm.", card.Data!.Value.GetProperty("aiNote").GetString());
        Assert.Equal(res.Answer, card.Data.Value.GetProperty("coveredText").GetString());
        Assert.Equal(["Boston University", "New York University"], sent.Schools);
        Assert.Contains(sent.Facts, f => f.StartsWith("Chi phí mỗi năm: Boston University 86.000 USD"));
        Assert.Contains(sent.Facts, f => f.Contains("rẻ hơn Boston University khoảng 11.000 USD"));
        Assert.NotEmpty(sent.Others);
    }

    [Fact]
    public async Task SchoolCompare_WhenGeminiFailsOrIsRejected_OnlyTheTableShows()
    {
        var failed = await CompareChat(CompareAction(["BU", "NYU"], ["cost"]), CompareRunner());                // FAILED mặc định
        var down = (await CompareChatWith(CompareAction(["BU", "NYU"], ["cost"]), CompareRunner(), noteError: new HttpRequestException("advisor tắt"))).Res;

        Assert.DoesNotContain("AI nhận xét", failed.Answer);
        Assert.DoesNotContain("AI nhận xét", down.Answer);
        Assert.StartsWith("So sánh Boston University và New York University:", down.Answer);
    }

    [Fact]
    public async Task SchoolCompare_AnEmptyNote_IsTreatedAsNone()
    {
        var empty = new CompareNoteResponseDto("1.0", "gemini", "OK", "   ");

        var (res, _) = await CompareChatWith(CompareAction(["BU", "NYU"], ["cost"]), CompareRunner(), empty);

        Assert.DoesNotContain("AI nhận xét", res.Answer);
    }

    private static FakeRunner CompareRunner() => new()
    {
        MatchByName = new()
        {
            ["BU"] = Found("bu", "Boston University"),
            ["NYU"] = Found("nyu", "New York University"),
            ["Boston"] = new NameMatch(NameMatchKind.Ambiguous, [new SchoolRef("bu", "Boston University"), new SchoolRef("bc", "Boston College")]),
            ["Hogwarts"] = new NameMatch(NameMatchKind.NotFound, []),
        },
        InfoByCode = new()
        {
            ["bu"] = InfoOf("Boston University"),
            ["nyu"] = InfoOf("New York University") with { Code = "nyu", Cost = new SchoolCostInfo(50_000m, 20_000m, 3_000m, 2_000m, 75_000m) },
        },
    };

    [Fact]
    public async Task SchoolCompare_TwoResolvedSchools_GiveATableOfTheirNumbersAndTheCostGap()
    {
        var res = await CompareChat(CompareAction(["BU", "NYU"], ["cost"]), CompareRunner());

        Assert.StartsWith("So sánh Boston University và New York University:", res.Answer);
        Assert.Contains("Boston University 86.000 USD · New York University 75.000 USD", res.Answer);
        Assert.Contains("New York University rẻ hơn Boston University khoảng 11.000 USD mỗi năm.", res.Answer);
        Assert.DoesNotContain("Mình đang tra số liệu", res.Answer);
        Assert.Empty(res.Citations);
    }

    [Fact]
    public async Task SchoolCompare_WithoutFieldsFromTheModel_UsesTheDefaultFields()
    {
        var res = await CompareChat(CompareAction(["BU", "NYU"], []), CompareRunner());

        Assert.Contains("- Tiếng Anh tối thiểu:", res.Answer);
        Assert.Contains("- Xếp hạng:", res.Answer);
    }

    [Fact]
    public async Task SchoolCompare_ASchoolNotInTheSystem_IsNamedInsteadOfGuessed()
    {
        var res = await CompareChat(CompareAction(["BU", "Hogwarts"], ["cost"]), CompareRunner());

        Assert.Contains("“Hogwarts”", res.Answer);
        Assert.DoesNotContain("So sánh", res.Answer);
        Assert.Null(res.QuickReplies);            // không có ứng viên để chọn
    }

    // Tên mơ hồ: hỏi lại đúng trường đó bằng nút; mỗi nút chạy lại so sánh theo mã, giữ trường đã rõ và các mục
    [Fact]
    public async Task SchoolCompare_AnAmbiguousName_AsksWhichOneAndKeepsTheOthers()
    {
        var res = await CompareChat(CompareAction(["Boston", "NYU"], ["cost"]), CompareRunner());

        Assert.Contains("“Boston”", res.Answer);
        Assert.DoesNotContain("So sánh Boston", res.Answer);
        Assert.Equal(["Boston University", "Boston College"], res.QuickReplies!.Select(b => b.Label));
        var action = res.QuickReplies![1].Action!;                    // chọn Boston College
        Assert.Equal("school_compare", action.Type);
        Assert.Equal(["bc", "nyu"], action.SchoolCodes);               // Boston thành mã đã chọn, NYU (đã rõ) thành mã
        Assert.Equal(["cost"], action.Fields);
    }

    [Fact]
    public async Task SchoolCompare_TwoAmbiguousNames_AreAskedOneAtATime()
    {
        var runner = CompareRunner();
        runner.MatchByName!["Washington"] = new NameMatch(NameMatchKind.Ambiguous, [new SchoolRef("gw", "George Washington University"), new SchoolRef("wl", "Washington and Lee University")]);

        var res = await CompareChat(CompareAction(["Boston", "Washington"], ["cost"]), runner);

        Assert.Contains("“Boston”", res.Answer);                                   // hỏi tên đầu trước
        Assert.Equal(["bu", "Washington"], res.QuickReplies![0].Action!.SchoolCodes);   // tên còn lại giữ chữ để hỏi lượt sau
    }

    // Bấm nút chọn: so sánh chạy thẳng theo mã (và tên đã rõ), không qua advisor
    [Fact]
    public async Task ChatAction_CompareAfterChoosingTheAmbiguousSchool_RunsTheComparison()
    {
        var runner = CompareRunner();
        runner.MatchByName!["bu"] = Found("bu", "Boston University");
        runner.MatchByName["NYU"] = Found("nyu", "New York University");

        var (res, advisor) = await ActionChat(
            new ChatAction("school_compare", SchoolCodes: ["bu", "NYU"], Question: "so sánh", Fields: ["cost"]), runner);

        Assert.Null(advisor.LastRequest);
        Assert.StartsWith("So sánh Boston University và New York University:", res.Answer);
    }

    [Fact]
    public async Task SchoolCompare_TheSameSchoolTwice_AsksForTwoDifferentSchools()
    {
        var res = await CompareChat(CompareAction(["BU", "BU"], ["cost"]), CompareRunner());

        Assert.Equal(ChatSchoolInfo.CompareNeedsTwo, res.Answer);
    }

    [Fact]
    public async Task SchoolCompare_ADatabaseFailure_SaysSoInsteadOfLeavingTheTempSentence()
    {
        var runner = CompareRunner();
        runner.InfoByCode!.Remove("nyu");   // thiếu trường này: bộ giả ném "Chưa có trường"

        var res = await CompareChat(CompareAction(["BU", "NYU"], ["cost"]), runner);

        Assert.Equal(ChatSchoolInfo.Unavailable, res.Answer);
    }

    [Fact]
    public async Task SchoolCompare_MoreThanFourSchools_ComparesTheFirstFour()
    {
        var runner = CompareRunner();
        foreach (var n in new[] { "A", "B", "C", "D", "E" })
        {
            runner.MatchByName![n] = Found(n.ToLowerInvariant(), $"School {n}");
            runner.InfoByCode![n.ToLowerInvariant()] = InfoOf($"School {n}") with { Code = n.ToLowerInvariant() };
        }

        var res = await CompareChat(CompareAction(["A", "B", "C", "D", "E"], ["cost"]), runner);

        Assert.Contains("School D", res.Answer);
        Assert.DoesNotContain("School E", res.Answer);
    }

    [Fact]
    public async Task SchoolInfo_TheSlmGaveNoSchool_FindsItInTheSentence()
    {
        var runner = new FakeRunner { Mentioned = [new SchoolRef("harvard", "Harvard University")], Info = InfoOf() };

        var res = await KnowledgeChat(KnowledgeAction("Harvard cần IELTS bao nhiêu"), runner);

        Assert.Equal("harvard", runner.InfoCode);
        Assert.Contains("Tiếng Anh tối thiểu: IELTS 7.", res.Answer);
    }

    // Biết đúng trường nhưng không nhận ra mục nào: kho có nguồn thật thì giữ câu của kho, kho không có thì tóm tắt kèm nút chọn mục
    [Fact]
    public async Task SchoolInfo_ASchoolWithoutATopic_GivesTheSummaryWhenTheKnowledgeBaseHasNothing()
    {
        var runner = new FakeRunner { Match = Found("harvard", "Harvard University"), Info = InfoOf() };

        var res = await KnowledgeChat(KnowledgeAction("Harvard thế nào", school: "Harvard"), runner);

        Assert.StartsWith("Mình chưa rõ bạn muốn biết mục nào của Harvard University.", res.Answer);
        Assert.Contains("Tóm tắt về Harvard University", res.Answer);
        Assert.Equal(6, res.QuickReplies!.Count);                                 // nút chọn mục
        Assert.Equal("school_info", res.Widget!.Type);
        Assert.Equal(res.Answer, res.Widget.Data!.Value.GetProperty("coveredText").GetString());
    }

    [Fact]
    public async Task SchoolInfo_ASchoolWithoutATopic_KeepsTheKnowledgeBaseAnswerWhenItHasASource()
    {
        var runner = new FakeRunner { Match = Found("harvard", "Harvard University"), Info = InfoOf() };

        var res = await KnowledgeChat(KnowledgeAction("Harvard là trường như thế nào", school: "Harvard", kbFound: true, kb: "Mô tả của kho"), runner);

        Assert.Equal("Mô tả của kho", res.Answer);
        Assert.Null(runner.InfoCode);
    }

    [Fact]
    public async Task SchoolInfo_AGeneralQuestionWithNoSchool_KeepsTheKnowledgeBaseAnswerEvenWhenItHasNoSource()
    {
        var runner = new FakeRunner { Info = InfoOf() };

        var res = await KnowledgeChat(KnowledgeAction("I-20 là gì"), runner);

        Assert.Equal("ok", res.Answer);
        Assert.Null(runner.InfoCode);
    }

    [Fact]
    public async Task SchoolInfo_NoSchoolAnywhere_KeepsTheKnowledgeBaseAnswer()
    {
        var runner = new FakeRunner { Info = InfoOf() };

        var res = await KnowledgeChat(KnowledgeAction("học phí đại học Mỹ bao nhiêu"), runner);

        Assert.Equal("ok", res.Answer);
        Assert.Null(runner.InfoCode);
    }

    [Fact]
    public async Task SchoolInfo_AnAmbiguousName_AsksWhichSchoolAndKeepsTheQuestionOnEachButton()
    {
        var runner = new FakeRunner
        {
            Match = new NameMatch(NameMatchKind.Ambiguous, [new SchoolRef("bu", "Boston University"), new SchoolRef("bc", "Boston College")]),
            Info = InfoOf(),
        };

        var res = await KnowledgeChat(KnowledgeAction("học phí Boston bao nhiêu", school: "Boston"), runner);

        Assert.Null(runner.InfoCode);
        Assert.Contains("Boston", res.Answer);
        Assert.Equal(["Boston University", "Boston College"], res.QuickReplies!.Select(b => b.Label));
        Assert.Equal(new ChatAction("school_info", "bu", Question: "học phí Boston bao nhiêu"), res.QuickReplies![0].Action);
    }

    [Fact]
    public async Task SchoolInfo_OnlyAnUnstoredTopic_KeepsTheKnowledgeBaseAnswerWhenItHasASource()
    {
        var runner = new FakeRunner { Match = Found("harvard", "Harvard University"), Info = InfoOf() };

        var kept = await KnowledgeChat(KnowledgeAction("ký túc xá của Harvard thế nào", school: "Harvard", kbFound: true, kb: "Câu của kho"), runner);
        var pointed = await KnowledgeChat(KnowledgeAction("ký túc xá của Harvard thế nào", school: "Harvard", kbFound: false), runner);

        Assert.Equal("Câu của kho", kept.Answer);                              // kho có nguồn thật: giữ
        Assert.Contains("chưa có thông tin này của Harvard University", pointed.Answer);   // kho không có: nói chưa có và chỉ website
        Assert.Contains("https://harvard.example.edu", pointed.Answer);
    }

    [Fact]
    public async Task SchoolInfo_OtherIntentsInTheSameMessageStayIntact()
    {
        var runner = new FakeRunner { Match = Found("harvard", "Harvard University"), Info = InfoOf() };
        var advisor = new FakeAdvisor("B4")
        {
            Action = "school_info", Answer = "Mở đầu.\n\nCâu của kho\n\nKết thúc.",
            ActionData = KnowledgeAction("học phí Harvard bao nhiêu", school: "Harvard", kb: "Câu của kho"),
        };
        var res = await new ChatService(advisor, new FakeSessions(), new FakeProfileFlow(), null, runner)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

        Assert.StartsWith("Mở đầu.", res.Answer);
        Assert.EndsWith("Kết thúc.", res.Answer);
        Assert.Contains("học phí 59.000 USD", res.Answer);
        Assert.DoesNotContain("Câu của kho", res.Answer);
    }

    [Fact]
    public async Task SchoolInfo_ADatabaseFailure_KeepsTheKnowledgeBaseAnswer()
    {
        var runner = new FakeRunner { Match = Found("harvard", "Harvard University") };   // Info = null: bộ giả ném "Chưa có trường"

        var res = await KnowledgeChat(KnowledgeAction("học phí Harvard bao nhiêu", school: "Harvard"), runner);

        Assert.Equal("ok", res.Answer);
    }

    // SLM thật (usas-slm-v2) xếp "MIT cần IELTS bao nhiêu" vào goal-seeking và "UCLA xếp hạng bao nhiêu" vào giải thích hồ sơ: chuyển sang số liệu
    private static async Task<(AdvisorChatResponse Res, FakeRunner Runner)> TypedChat(
        string typed, string action, JsonElement data, FakeRunner runner)
    {
        var res = await new ChatService(new FakeAdvisor("B4") { Action = action, ActionData = data, Answer = "câu tạm của advisor" },
                new FakeSessions(), new FakeProfileFlow(), null, runner)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest([new StudyAbroad.Application.Advisor.ChatMessage("user", typed)]));
        return (res, runner);
    }

    [Fact]
    public async Task SchoolInfo_AGoalSeekingTheSlmGaveToAnEnglishRequirementQuestion_AnswersTheNumbersInstead()
    {
        var runner = new FakeRunner { Info = InfoOf(), GoalResult = GoalOf(), Match = Found("mit", "MIT") };
        var data = JsonDocument.Parse("""{"school":"MIT","target":null}""").RootElement;

        var (res, r) = await TypedChat("MIT cần IELTS bao nhiêu", "goal_seek", data, runner);

        Assert.Null(r.GoalCalledWith);                      // không chạy goal-seeking
        Assert.Contains("Tiếng Anh tối thiểu: IELTS 7", res.Answer);
        Assert.DoesNotContain("câu tạm của advisor", res.Answer);
    }

    // Lỗi thật khi thử: "GPA đại học harvard là bao nhiêu" SLM xếp kiến thức không kèm trường, kho không có (knowledge_miss); GPA phải là mục số liệu
    [Fact]
    public async Task SchoolInfo_AGpaQuestionWithNoSchoolFromTheSlm_FindsTheSchoolInTheSentenceAndAnswers()
    {
        var runner = new FakeRunner
        {
            Mentioned = [new SchoolRef("harvard", "Harvard University")],
            Info = InfoOf() with { Gpa = new SchoolGpaInfo(3.9m, 0.54m, 0.9m) },
        };

        var res = await KnowledgeChat(KnowledgeAction("GPA đại học harvard là bao nhiêu"), runner);

        Assert.Contains("GPA trung bình của sinh viên trúng tuyển khoảng 3,9 (thang 4)", res.Answer);
    }

    [Theory]
    [InlineData("GPA em 3.5 thì vào Harvard được không", "goal_seek")]
    [InlineData("em IELTS 7.0, Harvard cần IELTS bao nhiêu", "goal_seek")]
    public async Task SchoolInfo_AStudentsOwnNumbersMeanItIsNotAFactQuestion(string typed, string action)
    {
        var runner = new FakeRunner { Info = InfoOf(), GoalResult = GoalOf(), Match = Found("harvard", "Harvard University") };

        await TypedChat(typed, action, JsonDocument.Parse("""{"school":"Harvard"}""").RootElement, runner);

        Assert.Null(runner.InfoCode);
    }

    [Fact]
    public async Task SchoolInfo_AnExplainTheSlmGaveToARankingQuestion_AnswersTheNumbersInstead()
    {
        var runner = new FakeRunner { Info = InfoOf(), Explanation = Explanation() };

        var (res, r) = await TypedChat("UCLA xếp hạng bao nhiêu", "explain", IowaCode, runner);

        Assert.Null(r.ExplainedCode);                       // không chạy giải thích nhóm
        Assert.Equal("iowa", r.InfoCode);
    }

    [Theory]
    [InlineData("MIT cần gì để vào", "goal_seek")]
    [InlineData("làm sao để vào Harvard, cần IELTS bao nhiêu", "goal_seek")]
    [InlineData("vì sao Harvard khó vào", "explain")]            // "khó vào" là nhóm, không phải hỏi tỷ lệ nhận
    [InlineData("Harvard xếp nhóm nào với hồ sơ của em", "explain")]
    public async Task SchoolInfo_QuestionsAboutGroupsOrHowToGetIn_KeepTheirOwnFlow(string typed, string action)
    {
        var runner = new FakeRunner { Info = InfoOf(), GoalResult = GoalOf(), Explanation = Explanation(), Match = Found("188429", "Boston University") };
        var data = action == "goal_seek" ? JsonDocument.Parse("""{"school":"MIT"}""").RootElement : IowaCode;

        await TypedChat(typed, action, data, runner);

        Assert.Null(runner.InfoCode);
    }

    [Fact]
    public async Task SchoolInfo_AButtonActionIsNeverRerouted()
    {
        var runner = new FakeRunner { Info = InfoOf(), GoalResult = GoalOf() };

        var (res, _) = await ActionChat(new ChatAction("goal_seek", "188429"), runner);

        Assert.Equal("goal_seek", res.Widget!.Type);
        Assert.Null(runner.InfoCode);
    }

    // Nút chọn trường khi tên mơ hồ: chạy thẳng theo mã và câu hỏi gốc, không qua advisor
    [Fact]
    public async Task ChatAction_SchoolInfo_AnswersTheOriginalQuestionForTheChosenSchool()
    {
        var runner = new FakeRunner { Info = InfoOf("Boston University") };

        var (res, advisor) = await ActionChat(new ChatAction("school_info", "bu", Question: "học phí Boston bao nhiêu"), runner);

        Assert.Null(advisor.LastRequest);
        Assert.Equal("bu", runner.InfoCode);
        Assert.Contains("học phí 59.000 USD", res.Answer);
    }

    [Fact]
    public async Task ChatAction_SchoolInfo_WithoutAQuestion_GivesTheSummaryAndFieldButtons()
    {
        var runner = new FakeRunner { Info = InfoOf("Boston University") };

        var (res, _) = await ActionChat(new ChatAction("school_info", "bu"), runner);

        Assert.StartsWith("Tóm tắt về Boston University", res.Answer);
        Assert.Equal(6, res.QuickReplies!.Count);
    }

    // Nút "Cần gì để vào trường em muốn?" không kèm trường: mở ô chọn trường, không gọi advisor và không chạy goal-seeking
    [Fact]
    public async Task ChatAction_GoalSeekWithoutASchool_AsksWhichSchoolWithThePicker()
    {
        var runner = new FakeRunner { GoalResult = GoalOf() };

        var (res, advisor) = await ActionChat(new ChatAction("goal_seek"), runner);

        Assert.Null(advisor.LastRequest);
        Assert.Null(runner.GoalCalledWith);
        Assert.Equal(ChatGoalSeek.AskSchool, res.Answer);
        Assert.Equal("school", res.Widget!.Type);
    }

    [Fact]
    public async Task ChatAction_ExplainWithoutASchool_OpensTheExplainPickerWithTheSuggestedSchools_WithoutTheAdvisor()
    {
        var runner = new FakeRunner();

        var (res, advisor) = await ActionChat(new ChatAction("explain", Suggested: ["bu", "ne"]), runner);

        Assert.Null(advisor.LastRequest);   // không qua SLM: nếu qua, advisor sẽ hỏi lại bằng nút tên trường thay vì ô chọn
        Assert.Equal(ChatGoalSeek.AskExplainSchool, res.Answer);
        Assert.Equal("school", res.Widget!.Type);
        var data = res.Widget.Data!.Value;
        Assert.Equal("explain", data.GetProperty("kind").GetString());
        Assert.Equal(["bu", "ne"], data.GetProperty("suggested").EnumerateArray().Select(c => c.GetString()));
    }

    // Thẻ kết quả do code dựng được gửi TRƯỚC khi Gemini được gọi (Gemini chậm), rồi lời mới tới sau trong dòng final
    [Fact]
    public async Task GoalSeek_SendsTheCardBeforeAskingGemini()
    {
        var order = new List<string>();
        var advisor = new FakeAdvisor("B4")
        {
            Action = "goal_seek", ActionData = BuAction,
            GoalNote = new GoalNoteResponseDto("1.0", "gemini", "OK", "Để lên nhóm Vừa sức ở Boston University, bạn cần nâng SAT."),
            OnGoalNote = () => order.Add("gemini"),
        };
        var runner = new FakeRunner { Match = Found(), GoalResult = GoalOf() };
        AdvisorStreamEvent? card = null;

        var res = await new ChatService(advisor, new FakeSessions(), new FakeProfileFlow(), null, runner).ChatAsync(
            Guid.NewGuid(), new AdvisorChatRequest(Hello), default,
            e => { if (e.Type == "card") { card = e; order.Add("card"); } return Task.CompletedTask; });

        Assert.Equal(["card", "gemini"], order);
        Assert.Equal("goal_seek", card!.Widget!.Type);
        Assert.StartsWith("Để lên nhóm Vừa sức", res.Answer);
    }

    [Fact]
    public async Task ChatAction_GoalSeek_WithoutATarget_LetsTheSystemPickOneGroupUp()
    {
        var runner = new FakeRunner { GoalResult = GoalOf() };

        await ActionChat(new ChatAction("goal_seek", "188429"), runner);

        Assert.Equal(("188429", null), runner.GoalCalledWith);
    }

    [Fact]
    public async Task ChatAction_Explain_RunsTheSchoolAdviceOnTheCode()
    {
        var runner = new FakeRunner { Explanation = Explanation() };

        var (res, advisor) = await ActionChat(new ChatAction("explain", "iowa"), runner);

        Assert.Null(advisor.LastRequest);
        Assert.Equal("iowa", runner.ExplainedCode);
        Assert.StartsWith("University of Iowa: ", res.Answer);
    }

    // Hành động sai (loại lạ, goal_seek có nhóm đích mà thiếu mã, nhóm đích không hợp lệ, mã quá dài): bỏ qua, chat chạy như thường qua advisor.
    // goal_seek và explain không có mã (goal_seek cũng không có nhóm đích) là hợp lệ: mở ô chọn trường (test riêng)
    [Theory]
    [InlineData("unknown", "188429", null)]
    [InlineData("goal_seek", "", "match")]
    [InlineData("goal_seek", "188429", "reach")]
    [InlineData("goal_seek", "188429", "xyz")]
    [InlineData("explain", "0123456789012345678901234567890123", null)]
    public async Task ChatAction_Invalid_FallsBackToTheNormalAdvisorTurn(string type, string? code, string? target)
    {
        var runner = new FakeRunner { Explanation = Explanation() };

        var (_, advisor) = await ActionChat(new ChatAction(type, code, target), runner);

        Assert.NotNull(advisor.LastRequest);   // advisor vẫn được gọi
        Assert.Null(runner.GoalCalledWith);
    }

    [Fact]
    public async Task ChatAction_WithoutARunner_IsIgnored()
    {
        var advisor = new FakeAdvisor("B4");

        await new ChatService(advisor, new FakeSessions(), new FakeProfileFlow())
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello, Action: new ChatAction("goal_seek", "188429")));

        Assert.NotNull(advisor.LastRequest);
    }

    private static async Task<AdvisorChatResponse> GoalChatWith(FakeAdvisor advisor, FakeRunner runner) =>
        await new ChatService(advisor, new FakeSessions(), new FakeProfileFlow(), null, runner)
            .ChatAsync(Guid.NewGuid(), new AdvisorChatRequest(Hello));

    [Fact]
    public async Task GoalSeekAction_AnswersWithTheWordsOfTheAdvisor_WhenTheyPassTheCheck()
    {
        var advisor = new FakeAdvisor("B4")
        {
            Action = "goal_seek", ActionData = BuAction,
            GoalNote = new GoalNoteResponseDto("1.0", "gemini", "OK", "Để lên nhóm Vừa sức ở Boston University, bạn cần nâng SAT."),
        };
        var runner = new FakeRunner { Match = Found(), GoalResult = GoalOf() };

        var res = await GoalChatWith(advisor, runner);

        Assert.Equal("Để lên nhóm Vừa sức ở Boston University, bạn cần nâng SAT.", res.Answer);
        Assert.Equal("goal_seek", res.Widget!.Type);   // khung kết quả trên trang vẫn mở
        // Dữ liệu gửi Gemini: trường đã hiểu, nhóm đích, dữ kiện đã tính, và tên các trường khác (không gồm trường đang hỏi)
        var note = advisor.LastGoalNote!;
        Assert.Equal(("Boston University", "Vừa sức", "reachable"), (note.School, note.Target, note.Outcome));
        Assert.StartsWith("Tài chính:", note.Facts[0]);
        Assert.Equal(["Baylor University", "Harvard University"], note.Others);
    }

    [Theory]
    [InlineData("FAILED")]   // Gemini lỗi hoặc lời không qua kiểm tra
    [InlineData("OK")]       // trả rỗng cũng coi như lỗi
    public async Task GoalSeekAction_WhenTheWordsAreMissing_UsesTheSentenceBuiltByCode(string status)
    {
        var advisor = new FakeAdvisor("B4") { Action = "goal_seek", ActionData = BuAction, GoalNote = new GoalNoteResponseDto("1.0", "gemini", status, "") };

        var res = await GoalChatWith(advisor, new FakeRunner { Match = Found(), GoalResult = GoalOf() });

        Assert.StartsWith("Mình hiểu bạn hỏi về Boston University, nhóm muốn đạt: Vừa sức.", res.Answer);
        Assert.Equal("goal_seek", res.Widget!.Type);
    }

    [Fact]
    public async Task GoalSeekAction_WhenTheAdvisorIsDown_UsesTheSentenceBuiltByCode()
    {
        foreach (Exception error in new Exception[] { new HttpRequestException("advisor tắt"), new TaskCanceledException("HttpClient.Timeout") })
        {
            var advisor = new FakeAdvisor("B4") { Action = "goal_seek", ActionData = BuAction, GoalNoteError = error };

            var res = await GoalChatWith(advisor, new FakeRunner { Match = Found(), GoalResult = GoalOf() });

            Assert.StartsWith("Mình hiểu bạn hỏi về Boston University", res.Answer);
        }
    }

    [Fact]
    public async Task GoalSeekAction_WhenTheNameCouldBeSeveralSchools_AsksWhichOneWithButtons_AndDoesNotRunGoalSeeking()
    {
        var match = new NameMatch(NameMatchKind.Ambiguous, [new SchoolRef("1", "Boston University"), new SchoolRef("2", "Baylor University")]);
        var runner = new FakeRunner { Match = match, GoalResult = GoalOf() };

        var res = await GoalChat(BuAction, runner);

        Assert.Contains("ý bạn là trường nào", res.Answer);
        Assert.Equal(
            [new QuickReply("Boston University", "Cần gì để vào Boston University ở nhóm Vừa sức?"), new QuickReply("Baylor University", "Cần gì để vào Baylor University ở nhóm Vừa sức?")],
            res.QuickReplies);
        Assert.Null(runner.GoalCalledWith);
        // Ngoài các nút còn có ô chọn trường có gợi ý, giữ nhóm đích để câu hỏi gửi lại đúng ý
        Assert.Equal("school", res.Widget!.Type);
        Assert.Equal("match", res.Widget.Data!.Value.GetProperty("target").GetString());
    }

    [Fact]
    public async Task GoalSeekAction_WhenTheSchoolIsNotInTheList_SaysSo()
    {
        var runner = new FakeRunner { Match = new NameMatch(NameMatchKind.NotFound, []) };

        var res = await GoalChat(JsonDocument.Parse("""{"school":"Yale"}""").RootElement, runner);

        Assert.Contains("chưa thấy trường “Yale”", res.Answer);
        Assert.Null(runner.GoalCalledWith);
        Assert.Equal("school", res.Widget!.Type);          // không còn ngõ cụt: học sinh tìm trong ô chọn
        Assert.Equal(JsonValueKind.Null, res.Widget.Data!.Value.GetProperty("target").ValueKind);
    }

    [Fact]
    public async Task GoalSeekAction_WithoutASchool_SaysItFailed_AndDoesNotSearch()
    {
        var runner = new FakeRunner { Match = Found() };

        var res = await GoalChat(null, runner);

        Assert.Equal(ChatGoalSeek.Failed, res.Answer);
        Assert.Null(runner.SearchedFor);
    }

    [Theory]
    [InlineData(typeof(ArgumentException), "Nhóm đích phải là reach, match hoặc safety.", "Mình chưa tính được: Nhóm đích phải là reach, match hoặc safety.")]
    [InlineData(typeof(InvalidOperationException), "Hồ sơ chưa có bậc học muốn theo.", "Mình chưa tính được: Hồ sơ chưa có bậc học muốn theo.")]
    [InlineData(typeof(InvalidOperationException), "Dữ liệu lưu bị hỏng", ChatGoalSeek.Failed)]
    public async Task GoalSeekAction_WhenGoalSeekingIsRejected_ExplainsWhy(Type type, string message, string expected)
    {
        var runner = new FakeRunner { Match = Found(), GoalError = (Exception)Activator.CreateInstance(type, message)! };

        var res = await GoalChat(BuAction, runner);

        Assert.Equal(expected, res.Answer);
        Assert.Null(res.Widget);
    }

    // Có kết quả rồi thì mỗi lượt gửi kèm danh sách trường mới nhất cho advisor. Lấy từ DB, không dựa vào trạng thái chat
    [Fact]
    public async Task WithResults_SendsTheLatestSchoolListToTheAdvisor()
    {
        var userId = Guid.NewGuid();
        var store = new FakeSessions();
        var session = new ChatSession { UserId = userId, StateJson = "{\"currentStep\":\"B4\",\"hasResults\":true}", CurrentStep = "B4" };
        store.Rows.Add(session);
        var runner = new FakeRunner { Latest = new Recommendation { ItemsJson = OneSchool } };
        var advisor = new FakeAdvisor("B4");

        await new ChatService(advisor, store, new FakeProfileFlow(), null, runner)
            .ChatAsync(userId, new AdvisorChatRequest(Hello, session.Id.ToString()));

        Assert.Equal(new ChatSchool("ASU", "Arizona State University", null), Assert.Single(advisor.LastRequest!.Schools!));
    }

    [Fact]
    public async Task ARecommendationMadeOnThePage_IsSentEvenThoughTheChatNeverRanOne()
    {
        var userId = Guid.NewGuid();
        var store = new FakeSessions();
        var session = new ChatSession { UserId = userId, StateJson = "{\"currentStep\":\"B4\"}", CurrentStep = "B4" };
        store.Rows.Add(session);
        var runner = new FakeRunner { Latest = new Recommendation { ItemsJson = OneSchool } };
        var advisor = new FakeAdvisor("B4");

        await new ChatService(advisor, store, new FakeProfileFlow(), null, runner)
            .ChatAsync(userId, new AdvisorChatRequest(Hello, session.Id.ToString()));

        Assert.Equal(new ChatSchool("ASU", "Arizona State University", null), Assert.Single(advisor.LastRequest!.Schools!));
    }

    [Fact]
    public async Task WithoutAnyRecommendation_SendsNoSchoolList()
    {
        var userId = Guid.NewGuid();
        var store = new FakeSessions();
        var session = new ChatSession { UserId = userId, StateJson = "{\"currentStep\":\"B4\"}", CurrentStep = "B4" };
        store.Rows.Add(session);
        var runner = new FakeRunner();
        var advisor = new FakeAdvisor("B4");

        await new ChatService(advisor, store, new FakeProfileFlow(), null, runner)
            .ChatAsync(userId, new AdvisorChatRequest(Hello, session.Id.ToString()));

        Assert.Null(advisor.LastRequest!.Schools);
        Assert.True(runner.LatestRead);
    }

    private sealed class FakeRunner : IRecommendationRunner
    {
        public Recommendation? Result { get; init; }
        public Exception? Error { get; init; }
        public Guid? CalledFor { get; private set; }
        public Recommendation? Latest { get; init; }
        public bool LatestRead { get; private set; }
        public SchoolExplanation? Explanation { get; init; }
        public Exception? ExplainError { get; init; }
        public string? ExplainedCode { get; private set; }
        public Guid? ExplainedFor { get; private set; }

        public Task<Recommendation?> GetLatestAsync(Guid userId, CancellationToken ct)
        {
            LatestRead = true;
            return Task.FromResult(Latest);
        }

        public ExplainSchoolDto ExplainCard { get; init; } =
            new("University of Iowa", "IA", "public", "R", "MAIN", ["GPA_MID", "BUDGET_OK"], 0.17, 1200, 1400, 40.0, 30.0, 30_000, null);
        public Action? OnExplain { get; init; }   // chạy ngay khi lời tư vấn được viết, để kiểm thứ tự

        public SimulationOverrides? ExplainOverrides { get; private set; }
        public string? ExplainBasisLabel { get; private set; }

        public Task<ExplainSchoolDto> ExplainCardAsync(Guid userId, string code, CancellationToken ct, SimulationOverrides? overrides = null) => Task.FromResult(ExplainCard);

        public Task<SchoolExplanation> ExplainAsync(Guid userId, string code, CancellationToken ct, SimulationOverrides? overrides = null, string? basis = null)
        {
            ExplainOverrides = overrides;
            ExplainBasisLabel = basis;
            OnExplain?.Invoke();
            ExplainedFor = userId;
            ExplainedCode = code;
            if (ExplainError is not null)
                throw ExplainError;
            return Task.FromResult(Explanation!);
        }

        public IReadOnlyList<SchoolOption> Options { get; init; } =
            [new SchoolOption("188429", "Boston University", "MA"), new SchoolOption("1", "Baylor University", "TX"), new SchoolOption("2", "Harvard University", "MA")];

        public Task<IReadOnlyList<SchoolOption>> ListSchoolOptionsAsync(Guid userId, CancellationToken ct) => Task.FromResult(Options);

        public NameMatch? Match { get; init; }
        public SchoolGoalResult? GoalResult { get; init; }
        public Exception? GoalError { get; init; }
        public string? SearchedFor { get; private set; }
        public (string Code, string? Target)? GoalCalledWith { get; private set; }

        public Task<NameMatch> FindSchoolsAsync(Guid userId, string text, CancellationToken ct)
        {
            SearchedFor = text;
            if (MatchByName is not null && MatchByName.TryGetValue(text, out var byName))
                return Task.FromResult(byName);
            return Task.FromResult(Match!);
        }

        public Task<SchoolGoalResult> GoalSeekAsync(Guid userId, string code, string? target, CancellationToken ct)
        {
            GoalCalledWith = (code, target);
            if (GoalError is not null)
                throw GoalError;
            if (GoalResult is null)
                throw new InvalidOperationException($"Chưa có trường {code} trong dữ liệu.");
            return Task.FromResult(GoalResult);
        }

        public SchoolInfoDto? Info { get; init; }
        public Dictionary<string, SchoolInfoDto>? InfoByCode { get; init; }   // so sánh: mỗi trường một thông tin
        public Dictionary<string, NameMatch>? MatchByName { get; init; }      // so sánh: mỗi tên một kết quả khớp
        public string? InfoCode { get; private set; }
        public IReadOnlyList<SchoolRef> Mentioned { get; init; } = [];
        public SchoolSearchResult? SearchResult { get; init; }
        public (SchoolSearchFilters Filters, SchoolSort Sort)? SearchedWith { get; private set; }

        public Task<SchoolSearchResult> SearchSchoolsAsync(Guid userId, SchoolSearchFilters filters, SchoolSort sort, CancellationToken ct)
        {
            SearchedWith = (filters, sort);
            return Task.FromResult(SearchResult ?? new SchoolSearchResult(0, [], []));
        }

        public Task<IReadOnlyList<SchoolRef>> FindMentionedSchoolsAsync(Guid userId, string text, CancellationToken ct) => Task.FromResult(Mentioned);

        public Task<SchoolInfoDto> SchoolInfoAsync(Guid userId, string code, CancellationToken ct)
        {
            InfoCode = code;
            if (InfoByCode is not null && InfoByCode.TryGetValue(code, out var byCode))
                return Task.FromResult(byCode);
            return Task.FromResult(Info ?? throw new InvalidOperationException($"Chưa có trường {code} trong dữ liệu."));
        }

        public SimulationResult? Simulation { get; init; }
        public Exception? SimulateError { get; init; }
        public SimulationOverrides? SimulatedWith { get; private set; }
        public List<IReadOnlyList<int>?> SimulatedPrefs { get; } = [];
        public Func<IReadOnlyList<int>?, SimulationResult>? SimulationFor { get; init; }   // kết quả theo ưu tiên, để kiểm thứ tự đổi

        public Task<SimulationResult> SimulateAsync(Guid userId, SimulationOverrides overrides, IReadOnlyList<int>? pref, CancellationToken ct)
        {
            SimulatedWith = overrides;
            SimulatedPrefs.Add(pref);
            if (SimulateError is not null)
                throw SimulateError;
            return Task.FromResult(SimulationFor?.Invoke(pref) ?? Simulation!);
        }

        public Task<Recommendation> CreateAsync(Guid userId, IReadOnlyList<int>? pref, CancellationToken ct)
        {
            CalledFor = userId;
            if (Error is not null)
                throw Error;
            return Task.FromResult(Result!);
        }
    }

    private sealed class FakeAnalyses : IChatAnalysisRepository
    {
        public List<(Guid UserId, string Kind, string Json, string Model)> Saved { get; } = [];

        public Task SaveAsync(Guid userId, string kind, string resultJson, string modelVersion, CancellationToken ct = default)
        {
            Saved.Add((userId, kind, resultJson, modelVersion));
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAdvisor(string nextStep, IReadOnlyList<QuickReply>? replies = null, Widget? widget = null) : IAdvisorClient
    {
        public AdvisorChatRequest? LastRequest { get; private set; }
        public JsonElement? Profile { get; init; }
        public IReadOnlyList<string>? Changed { get; init; }
        public string? QuestionId { get; init; }
        public IReadOnlyList<string>? Skills { get; init; }
        public string? Action { get; init; }
        public JsonElement? ActionData { get; init; }
        public string Answer { get; init; } = "ok";
        public IReadOnlyList<AiUsageDto>? AiUsage { get; init; }
        public AdvisorSlmDto? Slm { get; init; }

        public Exception? ChatError { get; init; }   // advisor sập hẳn: ChatAsync ném lỗi này

        public Task<AdvisorChatResponse> ChatAsync(AdvisorChatRequest request, CancellationToken ct = default)
        {
            LastRequest = request;
            if (ChatError is not null)
                throw ChatError;
            var question = QuestionId is null ? "" : $",\"questionId\":\"{QuestionId}\"";
            var state = JsonDocument.Parse($"{{\"currentStep\":\"{nextStep}\"{question}}}").RootElement;
            return Task.FromResult(new AdvisorChatResponse(
                Answer, [], null, state, QuickReplies: replies, Widget: widget, Profile: Profile, ProfileChanged: Changed, Skills: Skills, Action: Action, ActionData: ActionData,
                AiUsage: AiUsage, Slm: Slm));
        }

        // Trả lời theo từng ý: gọi onEvent cho từng tin trong Events rồi trả kết quả như ChatAsync
        public IReadOnlyList<AdvisorStreamEvent> Events { get; init; } = [];
        public int StreamCalls { get; private set; }
        public Exception? StreamError { get; init; }

        public async Task<AdvisorChatResponse> ChatStreamAsync(
            AdvisorChatRequest request, Func<AdvisorStreamEvent, Task> onEvent, CancellationToken ct = default)
        {
            StreamCalls++;
            foreach (var e in Events)
                await onEvent(e);
            if (StreamError is not null)
                throw StreamError;
            return await ChatAsync(request, ct);
        }

        public Task<ExplainResponseDto> ExplainAsync(ExplainRequestDto request, CancellationToken ct = default) => throw new NotSupportedException();

        // Mặc định Gemini lỗi: goal-seeking dùng câu ghép bằng code. Test cần lời của Gemini thì đặt GoalNote
        public GoalNoteResponseDto GoalNote { get; init; } = new("1.0", "gemini", "FAILED", "");
        public Exception? GoalNoteError { get; init; }
        public GoalNoteRequestDto? LastGoalNote { get; private set; }
        public Action? OnGoalNote { get; init; }   // chạy ngay khi Gemini được gọi, để kiểm thứ tự

        public Task<GoalNoteResponseDto> WriteGoalNoteAsync(GoalNoteRequestDto request, CancellationToken ct = default)
        {
            OnGoalNote?.Invoke();
            LastGoalNote = request;
            if (GoalNoteError is not null)
                throw GoalNoteError;
            return Task.FromResult(GoalNote);
        }

        // Mặc định Gemini lỗi: so sánh chỉ hiện bảng. Test cần nhận xét của Gemini thì đặt CompareNote
        public CompareNoteResponseDto CompareNote { get; init; } = new("1.0", "gemini", "FAILED", "");
        public Exception? CompareNoteError { get; init; }
        public CompareNoteRequestDto? LastCompareNote { get; private set; }

        public Task<CompareNoteResponseDto> WriteCompareNoteAsync(CompareNoteRequestDto request, CancellationToken ct = default)
        {
            LastCompareNote = request;
            if (CompareNoteError is not null)
                throw CompareNoteError;
            return Task.FromResult(CompareNote);
        }

        public ScenarioNoteResponseDto? Note { get; init; }
        public Exception? NoteError { get; init; }
        public ScenarioNoteRequestDto? LastNote { get; private set; }

        public Task<ScenarioNoteResponseDto> WriteScenarioNoteAsync(ScenarioNoteRequestDto request, CancellationToken ct = default)
        {
            LastNote = request;
            if (NoteError is not null)
                throw NoteError;
            return Task.FromResult(Note!);
        }
    }

    private sealed class FakeAiCalls : IAiCallRepository
    {
        public List<AiCall> Rows { get; } = [];
        public int Writes { get; private set; }
        public bool Fail { get; init; }

        public Task AddRangeAsync(IReadOnlyList<AiCall> calls, CancellationToken ct = default)
        {
            Writes++;
            if (Fail)
                throw new InvalidOperationException("Database không ghi được");
            Rows.AddRange(calls);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeMessages : IChatMessageRepository
    {
        public List<StudyAbroad.Domain.Entities.ChatMessage> Rows { get; } = [];
        public bool Fail { get; init; }

        public Task AddRangeAsync(IReadOnlyList<StudyAbroad.Domain.Entities.ChatMessage> messages, CancellationToken ct = default)
        {
            if (Fail)
                throw new InvalidOperationException("Database không ghi được");
            Rows.AddRange(messages);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSessions : IChatSessionRepository
    {
        public List<ChatSession> Rows { get; } = [];

        public Task<ChatSession?> FindAsync(Guid id, Guid userId, CancellationToken ct = default) =>
            Task.FromResult(Rows.FirstOrDefault(s => s.Id == id && s.UserId == userId));

        public Task AddAsync(ChatSession session, CancellationToken ct = default)
        {
            Rows.Add(session);
            return Task.CompletedTask;
        }

        public Task SaveAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeBenchmarks(IReadOnlyList<BenchmarkRow> rows) : IChatBenchmarkRepository
    {
        public string? Level { get; private set; }

        public Task<IReadOnlyList<BenchmarkRow>> ListAsync(string studyLevel, CancellationToken ct = default)
        {
            Level = studyLevel;
            return Task.FromResult(rows);
        }
    }

    private sealed class FakeProfileFlow : IProfileFlowService
    {
        public ProfileFlowDraftDto Draft { get; init; } = new();
        public Exception? PatchError { get; init; }
        public Exception? CommitError { get; init; }
        public int Commits { get; private set; }
        public JsonObject? PatchedProfile { get; private set; }
        public IReadOnlyCollection<string>? PatchedKeys { get; private set; }

        public Task<ProfileFlowStateDto> GetFlowStateAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(new ProfileFlowStateDto(1, [], Draft, null, null));

        public Task<ProfileFlowStateDto> PatchDraftAsync(
            Guid userId, JsonObject chatProfile, IReadOnlyCollection<string> changedKeys, CancellationToken ct = default)
        {
            if (PatchError is not null) throw PatchError;
            PatchedProfile = chatProfile;
            PatchedKeys = changedKeys;
            return Task.FromResult(new ProfileFlowStateDto(1, [], Draft, null, null));
        }

        public Task<ProfileFlowStateDto> SaveDraftAsync(Guid userId, SaveProfileDraftRequest request, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<UploadDraftTranscriptResultDto> UploadDraftTranscriptAsync(Guid userId, Stream fileStream, string fileName, string contentType, long length, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<CommitProfileFlowResponse> CommitFlowAsync(Guid userId, CommitProfileFlowRequest? request, CancellationToken ct = default)
        {
            if (CommitError is not null) throw CommitError;
            Commits++;
            return Task.FromResult<CommitProfileFlowResponse>(null!);
        }
        public Task ClearDraftAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
