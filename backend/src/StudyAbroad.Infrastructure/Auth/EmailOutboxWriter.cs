using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Auth;

/// <summary>
/// Tín hiệu "có mail mới" từ request sang job gửi, để job gửi ngay thay vì đợi lượt quét.
/// Chỉ là một cờ, không mang dữ liệu mail.
/// </summary>
public sealed class EmailOutboxSignal
{
    private readonly Channel<bool> _channel = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

    public void Notify() => _channel.Writer.TryWrite(true);

    /// <summary>Chờ tới khi có tín hiệu hoặc hết <paramref name="timeout"/>.</summary>
    public async Task WaitAsync(TimeSpan timeout, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        try
        {
            await _channel.Reader.ReadAsync(cts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Hết giờ chờ: quét DB như bình thường
        }
    }
}

/// <summary>
/// Mã hóa mail rồi ghi vào email_outbox. Không bao giờ ném lỗi ra request: ở đăng ký, quên mật khẩu,
/// chỉ nhánh "email có tài khoản" mới gửi mail, nên báo lỗi là lộ email đó có tồn tại.
/// </summary>
public sealed class EmailOutboxWriter
{
    private static readonly TimeSpan[] DefaultRetryDelays = [TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(7)];

    private readonly IEmailOutboxStore _store;
    private readonly MailPayloadProtector _protector;
    private readonly EmailOutboxSignal _signal;
    private readonly ILogger<EmailOutboxWriter> _logger;
    private readonly TimeSpan[] _retryDelays;

    public EmailOutboxWriter(IEmailOutboxStore store, MailPayloadProtector protector, EmailOutboxSignal signal,
        ILogger<EmailOutboxWriter> logger, TimeSpan[]? retryDelays = null)
    {
        _store = store;
        _protector = protector;
        _signal = signal;
        _logger = logger;
        _retryDelays = retryDelays ?? DefaultRetryDelays;
    }

    /// <param name="dedupeKind">Loại mail mà mail mới làm mail cũ hết giá trị (OTP, lời mời); null với mail phải gửi đủ.</param>
    /// <param name="lifetime">Hạn của mail (OTP); quá hạn chưa gửi được thì bỏ.</param>
    public async Task EnqueueAsync(OutgoingMail mail, string? dedupeKind = null, TimeSpan? lifetime = null)
    {
        var now = DateTime.UtcNow;
        var masked = MailLog.Mask(mail.To);
        EmailOutbox row;
        try
        {
            row = new EmailOutbox
            {
                Payload = _protector.Protect(mail),
                DedupeKey = dedupeKind is null ? null : _protector.DedupeKey(mail.To, dedupeKind),
                Status = EmailOutboxStatuses.Pending,
                NextAttemptAt = now,
                ExpiresAt = lifetime is null ? null : now + lifetime.Value,
                CreatedAt = now,
            };
        }
        catch (Exception ex)
        {
            // Sai cấu hình khóa: không ghi mail dạng rõ, chỉ báo cho người vận hành
            _logger.LogError("Không mã hóa được mail tới {Email} ({Error}), bỏ mail. Kiểm tra MAIL_ENCRYPTION_KEY", masked, ex.GetType().Name);
            return;
        }

        // Không dùng token của request: người dùng đóng tab sau khi OTP đã lưu thì mail vẫn phải đi
        if (await TryAddAsync(row, masked, attempt: 1))
        {
            _signal.Notify();
            return;
        }

        // DB chập: giữ bản đã mã hóa trong RAM, ghi lại ở nền để request trả về ngay
        _ = RetryInBackgroundAsync(row, masked);
    }

    private async Task RetryInBackgroundAsync(EmailOutbox row, string masked)
    {
        for (var i = 0; i < _retryDelays.Length; i++)
        {
            await Task.Delay(_retryDelays[i]);
            if (await TryAddAsync(row, masked, attempt: i + 2))
            {
                _signal.Notify();
                return;
            }
        }
        _logger.LogError("Không ghi được mail tới {Email} vào hàng đợi sau {Attempts} lần, bỏ mail", masked, _retryDelays.Length + 1);
    }

    private async Task<bool> TryAddAsync(EmailOutbox row, string masked, int attempt)
    {
        try
        {
            await _store.AddAsync(row, CancellationToken.None);
            return true;
        }
        catch (Exception ex)
        {
            // Chỉ ghi loại lỗi: thông điệp lỗi DB có thể chứa giá trị tham số
            _logger.LogWarning("Ghi mail tới {Email} vào hàng đợi lỗi lần {Attempt}: {Error}", masked, attempt, ex.GetType().Name);
            return false;
        }
    }
}
