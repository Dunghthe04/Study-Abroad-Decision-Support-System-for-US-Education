using System.Net.Mail;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StudyAbroad.Domain.Entities;
using StudyAbroad.Infrastructure.Auth;

namespace StudyAbroad.UnitTests;

/// <summary>Hàng đợi mail trên DB: mã hóa, gộp mail cùng loại, lịch thử lại, phân loại lỗi SMTP.</summary>
public class EmailOutboxTests
{
    private static readonly DateTime Now = new(2026, 11, 27, 8, 0, 0, DateTimeKind.Utc);

    private static MailPayloadProtector Protector(string? key = null) => new(
        Options.Create(new MailOutboxOptions { EncryptionKey = key ?? Convert.ToBase64String(new byte[32]) }),
        Options.Create(new JwtOptions()),
        NullLogger<MailPayloadProtector>.Instance);

    private static readonly OutgoingMail Mail = new("vietpt@gmail.com", "[USAS] Mã OTP", "<p>123456</p>");

    // ---------- Mã hóa ----------

    [Fact]
    public void Protect_RoundTrips_AndHidesContent()
    {
        var protector = Protector();
        var payload = protector.Protect(Mail);

        Assert.DoesNotContain("vietpt", payload);
        Assert.DoesNotContain("123456", payload);
        Assert.Equal(Mail, protector.Unprotect(payload));
    }

    [Fact]
    public void Protect_UsesFreshNonce_EachTime()
    {
        var protector = Protector();
        Assert.NotEqual(protector.Protect(Mail), protector.Protect(Mail));
    }

    [Fact]
    public void Unprotect_ReturnsNull_WhenOneByteChanged()
    {
        var protector = Protector();
        var data = Convert.FromBase64String(protector.Protect(Mail));
        data[20] ^= 0x01;
        Assert.Null(protector.Unprotect(Convert.ToBase64String(data)));
    }

    [Fact]
    public void Unprotect_ReturnsNull_WithOtherKey_OrGarbage()
    {
        var payload = Protector().Protect(Mail);
        var other = Protector(Convert.ToBase64String(Enumerable.Repeat((byte)7, 32).ToArray()));

        Assert.Null(other.Unprotect(payload));
        Assert.Null(other.Unprotect("không-phải-base64"));
        Assert.Null(other.Unprotect(Convert.ToBase64String(new byte[10])));
    }

    [Fact]
    public void Protector_RejectsKeyThatIsNot32Bytes()
    {
        var bad = Protector(Convert.ToBase64String(new byte[16]));
        Assert.Throws<InvalidOperationException>(() => bad.Protect(Mail));
    }

    [Fact]
    public async Task Writer_BadKey_DoesNotThrow_AndWritesNothing()
    {
        var store = new FakeStore();
        var writer = new EmailOutboxWriter(store, Protector(Convert.ToBase64String(new byte[16])), new EmailOutboxSignal(),
            NullLogger<EmailOutboxWriter>.Instance);

        await writer.EnqueueAsync(Mail);
        Assert.Equal(0, store.AddCalls);
    }

    [Fact]
    public void Protector_DerivesFromJwt_WhenNoKey()
    {
        var protector = new MailPayloadProtector(
            Options.Create(new MailOutboxOptions()),
            Options.Create(new JwtOptions { SecretKey = "usas_dev_jwt_secret_key_minimum_32_characters" }),
            NullLogger<MailPayloadProtector>.Instance);
        Assert.Equal(Mail, protector.Unprotect(protector.Protect(Mail)));
    }

    // ---------- dedupe_key ----------

    [Fact]
    public void DedupeKey_SameEmailAndKind_SameKey_IgnoringCase()
    {
        var p = Protector();
        Assert.Equal(p.DedupeKey("VietPT@Gmail.com ", "verify_email"), p.DedupeKey("vietpt@gmail.com", "verify_email"));
        Assert.NotEqual(p.DedupeKey("vietpt@gmail.com", "verify_email"), p.DedupeKey("vietpt@gmail.com", "reset_password"));
        Assert.NotEqual(p.DedupeKey("vietpt@gmail.com", "verify_email"), p.DedupeKey("khac@gmail.com", "verify_email"));
    }

    [Fact]
    public void DedupeKey_IsKeyed_AndHidesEmail()
    {
        var key = Protector().DedupeKey("vietpt@gmail.com", "verify_email");
        var otherKey = Protector(Convert.ToBase64String(Enumerable.Repeat((byte)9, 32).ToArray()))
            .DedupeKey("vietpt@gmail.com", "verify_email");
        // Băm thường (không khóa) thì ai cũng thử đoán được: phải khác SHA-256 thường
        var plainHash = Convert.ToHexString(SHA256.HashData("vietpt@gmail.com\nverify_email"u8.ToArray())).ToLowerInvariant();

        Assert.Equal(64, key.Length);
        Assert.NotEqual(otherKey, key);
        Assert.NotEqual(plainHash, key);
        Assert.DoesNotContain("vietpt", key);
    }

    // ---------- Che email trong log ----------

    [Theory]
    [InlineData("vietpt@gmail.com", "v***@gmail.com")]
    [InlineData("a@b.vn", "a***@b.vn")]
    [InlineData("khongcoacong", "***")]
    [InlineData("", "(trống)")]
    [InlineData(null, "(trống)")]
    public void Mask_HidesLocalPart(string? email, string expected)
    {
        Assert.Equal(expected, MailLog.Mask(email));
    }

    // ---------- Lịch thử lại ----------

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 5)]
    [InlineData(3, 15)]
    [InlineData(4, 60)]
    [InlineData(5, 120)]
    [InlineData(9, 120)]
    public void RetrySchedule_FollowsPlanB(int failedAttempts, int minutes)
    {
        Assert.Equal(TimeSpan.FromMinutes(minutes), EmailRetrySchedule.DelayAfter(failedAttempts));
    }

    [Fact]
    public void RetrySchedule_GivesUp_After24Hours_OrAfterExpiry()
    {
        var created = Now.AddHours(-23);
        Assert.NotNull(EmailRetrySchedule.Next(created, null, Now, TimeSpan.FromMinutes(30)));
        Assert.Null(EmailRetrySchedule.Next(created, null, Now, TimeSpan.FromHours(2)));
        Assert.Null(EmailRetrySchedule.Next(Now, Now.AddMinutes(5), Now, TimeSpan.FromMinutes(5)));
    }

    // ---------- Phân loại lỗi SMTP ----------

    [Fact]
    public void Classify_SmtpErrors()
    {
        Assert.Equal(SmtpFailureKind.Permanent,
            EmailOutboxProcessor.Classify(new SmtpFailedRecipientException(SmtpStatusCode.MailboxUnavailable, "x@y.z")));
        Assert.Equal(SmtpFailureKind.Permanent, EmailOutboxProcessor.Classify(new FormatException()));
        Assert.Equal(SmtpFailureKind.Throttled,
            EmailOutboxProcessor.Classify(new SmtpException(SmtpStatusCode.ServiceNotAvailable, "421 4.7.0 Try again later")));
        Assert.Equal(SmtpFailureKind.Throttled,
            EmailOutboxProcessor.Classify(new SmtpException(SmtpStatusCode.GeneralFailure, "550 5.4.5 Daily user sending limit exceeded")));
        Assert.Equal(SmtpFailureKind.Temporary,
            EmailOutboxProcessor.Classify(new SmtpFailedRecipientException(SmtpStatusCode.MailboxBusy, "x@y.z")));
        Assert.Equal(SmtpFailureKind.Temporary, EmailOutboxProcessor.Classify(new TimeoutException()));
        Assert.Equal(SmtpFailureKind.Temporary, EmailOutboxProcessor.Classify(new IOException()));
    }

    [Fact]
    public void Describe_KeepsOnlyTypeAndCode_NotMessage()
    {
        var text = EmailOutboxProcessor.Describe(new SmtpException(SmtpStatusCode.MailboxUnavailable, "550 vietpt@gmail.com does not exist"));
        Assert.Equal("SmtpException:550", text);
        Assert.Equal("IOException", EmailOutboxProcessor.Describe(new IOException("vietpt@gmail.com")));
    }

    // ---------- Job xử lý một mail ----------

    private sealed class Harness
    {
        public FakeStore Store { get; } = new();
        public FakeTransport Transport { get; } = new();
        public MailPayloadProtector Protector { get; } = EmailOutboxTests.Protector();
        public EmailOutboxProcessor Processor { get; }

        public Harness() => Processor = new EmailOutboxProcessor(Store, Protector, Transport,
            NullLogger<EmailOutboxProcessor>.Instance, jitter: () => TimeSpan.Zero);

        public EmailOutbox Add(DateTime? createdAt = null, DateTime? expiresAt = null, int attempts = 0, string? payload = null)
        {
            var row = new EmailOutbox
            {
                Payload = payload ?? Protector.Protect(Mail),
                Status = EmailOutboxStatuses.Pending,
                NextAttemptAt = createdAt ?? Now,
                CreatedAt = createdAt ?? Now,
                ExpiresAt = expiresAt,
                Attempts = attempts,
            };
            Store.Rows.Add(row);
            return row;
        }
    }

    [Fact]
    public async Task Process_SendsAndDeletes()
    {
        var h = new Harness();
        h.Add();

        Assert.Equal(OutboxStepResult.Handled, await h.Processor.ProcessNextAsync(Now, default));
        Assert.Equal(Mail, Assert.Single(h.Transport.Sent));
        Assert.Empty(h.Store.Rows);
        Assert.Equal(OutboxStepResult.Idle, await h.Processor.ProcessNextAsync(Now, default));
    }

    [Fact]
    public async Task Process_ExpiredOtp_IsDropped_NotSent()
    {
        var h = new Harness();
        h.Add(createdAt: Now.AddMinutes(-11), expiresAt: Now.AddMinutes(-1));

        await h.Processor.ProcessNextAsync(Now, default);
        Assert.Empty(h.Transport.Sent);
        Assert.Empty(h.Store.Rows);
    }

    [Fact]
    public async Task Process_TamperedPayload_IsDropped_NotSent()
    {
        var h = new Harness();
        var data = Convert.FromBase64String(h.Protector.Protect(Mail));
        data[^1] ^= 0xFF;
        h.Add(payload: Convert.ToBase64String(data));

        await h.Processor.ProcessNextAsync(Now, default);
        Assert.Empty(h.Transport.Sent);
        Assert.Empty(h.Store.Rows);
    }

    [Fact]
    public async Task Process_TemporaryError_ReschedulesByPlan()
    {
        var h = new Harness();
        var row = h.Add();
        h.Transport.Fail = new IOException("mất mạng");

        Assert.Equal(OutboxStepResult.Handled, await h.Processor.ProcessNextAsync(Now, default));
        Assert.Equal(EmailOutboxStatuses.Pending, row.Status);
        Assert.Equal(1, row.Attempts);
        Assert.Equal(Now.AddMinutes(1), row.NextAttemptAt);
        Assert.Equal("IOException", row.LastError);
        Assert.Null(row.LockedUntil);

        // Chưa tới giờ thì không lấy lại: mạng có lại cũng không gửi dồn
        Assert.Equal(OutboxStepResult.Idle, await h.Processor.ProcessNextAsync(Now.AddSeconds(30), default));

        await h.Processor.ProcessNextAsync(Now.AddMinutes(1), default);
        Assert.Equal(2, row.Attempts);
        Assert.Equal(Now.AddMinutes(6), row.NextAttemptAt);
    }

    [Fact]
    public async Task Process_TemporaryError_Past24Hours_IsDropped()
    {
        var h = new Harness();
        h.Add(createdAt: Now.AddHours(-23), attempts: 6);
        h.Transport.Fail = new TimeoutException();

        await h.Processor.ProcessNextAsync(Now, default);
        Assert.Empty(h.Store.Rows);
    }

    [Fact]
    public async Task Process_PermanentRecipientError_IsDropped_WithoutRetry()
    {
        var h = new Harness();
        h.Add();
        h.Transport.Fail = new SmtpFailedRecipientException(SmtpStatusCode.MailboxUnavailable, Mail.To);

        await h.Processor.ProcessNextAsync(Now, default);
        Assert.Empty(h.Store.Rows);
        Assert.Equal(1, h.Transport.Calls);
    }

    [Fact]
    public async Task Process_Throttled_PausesJob_AndKeepsAttemptCount()
    {
        var h = new Harness();
        var row = h.Add();
        h.Transport.Fail = new SmtpException(SmtpStatusCode.ServiceNotAvailable, "421 4.7.0 Try again later");

        Assert.Equal(OutboxStepResult.Throttled, await h.Processor.ProcessNextAsync(Now, default));
        Assert.Equal(0, row.Attempts);
        Assert.Equal(Now.AddMinutes(1), row.NextAttemptAt);
    }

    [Fact]
    public async Task Process_Shutdown_LeavesRowLocked_ForLaterClaim()
    {
        var h = new Harness();
        var row = h.Add();
        using var cts = new CancellationTokenSource();
        h.Transport.OnSend = () => cts.Cancel();
        h.Transport.Fail = new OperationCanceledException();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => h.Processor.ProcessNextAsync(Now, cts.Token));
        Assert.Equal(EmailOutboxStatuses.Sending, row.Status);

        // Hết hạn khóa thì api khác (hoặc lần chạy sau) lấy lại và gửi
        h.Transport.Fail = null;
        h.Transport.OnSend = null;
        await h.Processor.ProcessNextAsync(Now + EmailOutboxProcessor.LockFor + TimeSpan.FromSeconds(1), default);
        Assert.Single(h.Transport.Sent);
        Assert.Empty(h.Store.Rows);
    }

    // ---------- Ghi vào hàng đợi ----------

    [Fact]
    public async Task Writer_DbError_DoesNotThrow_AndRetriesInBackground()
    {
        var store = new FakeStore { FailNextAdds = 1 };
        var signal = new EmailOutboxSignal();
        var writer = new EmailOutboxWriter(store, Protector(), signal, NullLogger<EmailOutboxWriter>.Instance,
            retryDelays: [TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(10)]);

        await writer.EnqueueAsync(Mail);
        Assert.Empty(store.Rows);

        await signal.WaitAsync(TimeSpan.FromSeconds(5), default);
        Assert.Single(store.Rows);
    }

    [Fact]
    public async Task Writer_DbDownForAllRetries_DoesNotThrow()
    {
        var store = new FakeStore { FailNextAdds = 10 };
        var writer = new EmailOutboxWriter(store, Protector(), new EmailOutboxSignal(), NullLogger<EmailOutboxWriter>.Instance,
            retryDelays: [TimeSpan.FromMilliseconds(1)]);

        await writer.EnqueueAsync(Mail);
        await Task.Delay(100);
        Assert.Empty(store.Rows);
        Assert.Equal(2, store.AddCalls);
    }

    [Fact]
    public async Task Sender_ThreeOtps_KeepOnlyLatest_ThreeReceipts_KeepAll()
    {
        var store = new FakeStore();
        var writer = new EmailOutboxWriter(store, Protector(), new EmailOutboxSignal(), NullLogger<EmailOutboxWriter>.Instance);
        var sender = new SmtpEmailSender(Options.Create(new SmtpOptions()), writer);

        await sender.SendVerificationEmailAsync(Mail.To, "Việt", "111111");
        await sender.SendVerificationEmailAsync(Mail.To, "Việt", "222222");
        await sender.SendVerificationEmailAsync(Mail.To, "Việt", "333333");
        var otp = Assert.Single(store.Rows);
        Assert.Contains("333333", Protector().Unprotect(otp.Payload)!.HtmlBody);
        Assert.True(otp.ExpiresAt > otp.CreatedAt);

        for (var i = 1; i <= 3; i++)
            await sender.SendPaymentReceiptEmailAsync(Mail.To, "Việt", i, "Gói Cơ bản", 79000, Now, Now, Now.AddMonths(1));
        Assert.Equal(4, store.Rows.Count);
        Assert.All(store.Rows.Skip(1), r => Assert.Null(r.DedupeKey));
    }

    [Fact]
    public async Task Sender_ParentInvites_FromTwoStudents_AreBothKept()
    {
        var store = new FakeStore();
        var writer = new EmailOutboxWriter(store, Protector(), new EmailOutboxSignal(), NullLogger<EmailOutboxWriter>.Instance);
        var sender = new SmtpEmailSender(Options.Create(new SmtpOptions()), writer);

        await sender.SendParentInviteEmailAsync("me@gmail.com", "Nguyễn An", "t1", 7, true);
        await sender.SendParentInviteEmailAsync("me@gmail.com", "Nguyễn Bình", "t2", 7, true);
        await sender.SendParentInviteEmailAsync("me@gmail.com", "Nguyễn An", "t3", 7, true);

        Assert.Equal(2, store.Rows.Count);
    }

    // ---------- Giả lập ----------

    /// <summary>Giữ đúng ngữ nghĩa của EfEmailOutboxStore trên một danh sách trong RAM.</summary>
    private sealed class FakeStore : IEmailOutboxStore
    {
        public List<EmailOutbox> Rows { get; } = [];
        public int FailNextAdds { get; set; }
        public int AddCalls { get; private set; }

        public Task AddAsync(EmailOutbox row, CancellationToken ct)
        {
            AddCalls++;
            if (FailNextAdds-- > 0)
                throw new TimeoutException("db down");
            if (row.DedupeKey is not null)
                Rows.RemoveAll(x => x.DedupeKey == row.DedupeKey && x.Status == EmailOutboxStatuses.Pending);
            Rows.Add(row);
            return Task.CompletedTask;
        }

        public Task<EmailOutbox?> ClaimNextAsync(DateTime nowUtc, DateTime lockedUntil, CancellationToken ct)
        {
            var row = Rows
                .Where(x => (x.Status == EmailOutboxStatuses.Pending && x.NextAttemptAt <= nowUtc)
                            || (x.Status == EmailOutboxStatuses.Sending && x.LockedUntil < nowUtc))
                .OrderBy(x => x.CreatedAt)
                .FirstOrDefault();
            if (row is not null)
            {
                row.Status = EmailOutboxStatuses.Sending;
                row.LockedUntil = lockedUntil;
            }
            return Task.FromResult(row);
        }

        public Task DeleteAsync(Guid id, CancellationToken ct)
        {
            Rows.RemoveAll(x => x.Id == id);
            return Task.CompletedTask;
        }

        public Task RescheduleAsync(Guid id, int attempts, DateTime nextAttemptAt, string? lastError, CancellationToken ct)
        {
            var row = Rows.Single(x => x.Id == id);
            row.Status = EmailOutboxStatuses.Pending;
            row.Attempts = attempts;
            row.NextAttemptAt = nextAttemptAt;
            row.LockedUntil = null;
            row.LastError = lastError;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTransport : IMailTransport
    {
        public List<OutgoingMail> Sent { get; } = [];
        public Exception? Fail { get; set; }
        public Action? OnSend { get; set; }
        public int Calls { get; private set; }

        public Task SendAsync(OutgoingMail mail, CancellationToken ct)
        {
            Calls++;
            OnSend?.Invoke();
            if (Fail is not null)
                throw Fail;
            Sent.Add(mail);
            return Task.CompletedTask;
        }
    }
}
