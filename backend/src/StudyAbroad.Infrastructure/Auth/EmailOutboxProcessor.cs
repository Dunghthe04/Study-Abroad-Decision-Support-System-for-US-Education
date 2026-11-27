using System.Net.Mail;
using Microsoft.Extensions.Logging;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Auth;

public enum OutboxStepResult
{
    /// <summary>Không còn mail tới giờ gửi.</summary>
    Idle,
    /// <summary>Đã xử lý một mail (gửi được, hẹn lại hoặc bỏ).</summary>
    Handled,
    /// <summary>SMTP báo đang bị giới hạn: cả job nên tạm dừng.</summary>
    Throttled,
}

public enum SmtpFailureKind { Temporary, Permanent, Throttled }

/// <summary>Lịch thử lại: sau 1, 5, 15, 60 phút, rồi mỗi 2 giờ, tối đa 24 giờ kể từ lúc tạo mail.</summary>
public static class EmailRetrySchedule
{
    public static readonly TimeSpan MaxAge = TimeSpan.FromHours(24);
    private static readonly TimeSpan[] Steps =
        [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(60)];
    private static readonly TimeSpan Repeat = TimeSpan.FromHours(2);

    public static TimeSpan DelayAfter(int failedAttempts) =>
        failedAttempts >= 1 && failedAttempts <= Steps.Length ? Steps[failedAttempts - 1] : Repeat;

    /// <summary>Giờ thử lại kế tiếp, hoặc null khi phải bỏ (quá hạn mail hoặc quá 24 giờ).</summary>
    public static DateTime? Next(DateTime createdAt, DateTime? expiresAt, DateTime nowUtc, TimeSpan delay)
    {
        var next = nowUtc + delay;
        if (expiresAt is { } exp && next >= exp)
            return null;
        if (next > createdAt + MaxAge)
            return null;
        return next;
    }
}

/// <summary>Lấy một mail khỏi email_outbox, giải mã trong RAM, gửi, rồi xóa dòng hoặc hẹn thử lại.</summary>
public sealed class EmailOutboxProcessor
{
    public static readonly TimeSpan LockFor = TimeSpan.FromMinutes(2);
    public static readonly TimeSpan ThrottlePause = TimeSpan.FromMinutes(1);

    private readonly IEmailOutboxStore _store;
    private readonly MailPayloadProtector _protector;
    private readonly IMailTransport _transport;
    private readonly ILogger<EmailOutboxProcessor> _logger;
    private readonly Func<TimeSpan> _jitter;

    public EmailOutboxProcessor(IEmailOutboxStore store, MailPayloadProtector protector, IMailTransport transport,
        ILogger<EmailOutboxProcessor> logger, Func<TimeSpan>? jitter = null)
    {
        _store = store;
        _protector = protector;
        _transport = transport;
        _logger = logger;
        // Vài giây ngẫu nhiên để các mail lỗi cùng lúc không thử lại đúng cùng một giây
        _jitter = jitter ?? (() => TimeSpan.FromMilliseconds(Random.Shared.Next(0, 10_000)));
    }

    public async Task<OutboxStepResult> ProcessNextAsync(DateTime nowUtc, CancellationToken ct)
    {
        var row = await _store.ClaimNextAsync(nowUtc, nowUtc + LockFor, ct);
        if (row is null)
            return OutboxStepResult.Idle;

        if (row.ExpiresAt is { } exp && exp <= nowUtc)
        {
            await _store.DeleteAsync(row.Id, CancellationToken.None);
            _logger.LogWarning("Mail {Id} quá hạn khi chưa gửi được, bỏ (sau {Attempts} lần lỗi)", row.Id, row.Attempts);
            return OutboxStepResult.Handled;
        }

        var mail = _protector.Unprotect(row.Payload);
        if (mail is null)
        {
            // Bị sửa trong DB hoặc mã bằng khóa khác: không gửi nội dung không kiểm được
            await _store.DeleteAsync(row.Id, CancellationToken.None);
            _logger.LogError("Mail {Id} không giải mã được (bản mã bị sửa hoặc sai khóa), bỏ", row.Id);
            return OutboxStepResult.Handled;
        }

        var masked = MailLog.Mask(mail.To);
        try
        {
            await _transport.SendAsync(mail, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // api đang tắt: dòng vẫn là sending, hết hạn khóa thì được lấy lại
            throw;
        }
        catch (Exception ex)
        {
            return await HandleFailureAsync(row, ex, masked, nowUtc);
        }

        // Đã giao cho SMTP: không để việc tắt api chặn bước xóa, tránh gửi lại lần nữa
        await _store.DeleteAsync(row.Id, CancellationToken.None);
        return OutboxStepResult.Handled;
    }

    private async Task<OutboxStepResult> HandleFailureAsync(EmailOutbox row, Exception ex, string masked, DateTime nowUtc)
    {
        var error = Describe(ex);
        var kind = Classify(ex);

        if (kind == SmtpFailureKind.Permanent)
        {
            await _store.DeleteAsync(row.Id, CancellationToken.None);
            _logger.LogWarning("Mail {Id} tới {Email} bị từ chối vĩnh viễn ({Error}), bỏ không thử lại", row.Id, masked, error);
            return OutboxStepResult.Handled;
        }

        // Bị giới hạn là lỗi của cả tài khoản SMTP, không tính vào số lần thử của mail này
        var attempts = kind == SmtpFailureKind.Throttled ? row.Attempts : row.Attempts + 1;
        var delay = kind == SmtpFailureKind.Throttled ? ThrottlePause : EmailRetrySchedule.DelayAfter(attempts);
        var next = EmailRetrySchedule.Next(row.CreatedAt, row.ExpiresAt, nowUtc, delay + _jitter());
        if (next is null)
        {
            await _store.DeleteAsync(row.Id, CancellationToken.None);
            _logger.LogError("Mail {Id} tới {Email} hết lịch thử lại sau {Attempts} lần lỗi ({Error}), bỏ", row.Id, masked, attempts, error);
            return OutboxStepResult.Handled;
        }

        await _store.RescheduleAsync(row.Id, attempts, next.Value, error, CancellationToken.None);
        _logger.LogWarning("Gửi mail {Id} tới {Email} lỗi lần {Attempts} ({Error}), thử lại lúc {Next:HH:mm:ss} UTC",
            row.Id, masked, attempts, error, next.Value);
        return kind == SmtpFailureKind.Throttled ? OutboxStepResult.Throttled : OutboxStepResult.Handled;
    }

    public static SmtpFailureKind Classify(Exception ex)
    {
        // Người nhận không tồn tại / bị từ chối hẳn (5xx): thử lại cũng vô ích
        if (ex is SmtpFailedRecipientException recipient && (int)recipient.StatusCode >= 500)
            return SmtpFailureKind.Permanent;
        // Địa chỉ sai định dạng
        if (ex is FormatException)
            return SmtpFailureKind.Permanent;
        // Gmail giới hạn tốc độ / hạn mức: 421, mã mở rộng 4.7.x, 5.4.5 (hết hạn mức ngày)
        if (ex is SmtpException smtp &&
            (smtp.StatusCode == SmtpStatusCode.ServiceNotAvailable
             || smtp.Message.Contains(" 4.7.", StringComparison.Ordinal)
             || smtp.Message.Contains(" 5.4.5", StringComparison.Ordinal)))
            return SmtpFailureKind.Throttled;
        // Mất mạng, timeout, Gmail bận, sai cấu hình SMTP...
        return SmtpFailureKind.Temporary;
    }

    /// <summary>Chỉ tên loại lỗi và mã SMTP: thông điệp lỗi SMTP có thể lẫn địa chỉ email.</summary>
    public static string Describe(Exception ex)
    {
        var text = ex is SmtpException smtp ? $"{ex.GetType().Name}:{(int)smtp.StatusCode}" : ex.GetType().Name;
        return text.Length <= 128 ? text : text[..128];
    }
}
