using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StudyAbroad.Application.Auth;

namespace StudyAbroad.Infrastructure.Auth;

/// <summary>Gửi một mail thật. Lỗi thì ném ra để job quyết định thử lại hay bỏ.</summary>
public interface IMailTransport
{
    Task SendAsync(OutgoingMail mail, CancellationToken ct);
}

public sealed class SmtpMailTransport(IOptions<SmtpOptions> options, ILogger<SmtpMailTransport> logger) : IMailTransport
{
    private static readonly TimeSpan SendTimeout = TimeSpan.FromSeconds(10);
    private readonly SmtpOptions _options = options.Value;

    public async Task SendAsync(OutgoingMail mail, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.User) || string.IsNullOrWhiteSpace(_options.Password))
        {
            // Máy dev chưa cấu hình SMTP: coi như đã gửi. Không ghi tiêu đề vì tiêu đề cho biết người đó vừa làm gì
            logger.LogWarning("SMTP chưa cấu hình (SMTP_USER/SMTP_PASSWORD), bỏ qua mail tới {Email}", MailLog.Mask(mail.To));
            return;
        }

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl,
            Credentials = new NetworkCredential(_options.User, _options.Password),
        };
        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromEmail, _options.FromName),
            Subject = mail.Subject,
            Body = mail.HtmlBody,
            IsBodyHtml = true,
        };
        message.To.Add(mail.To);

        // SmtpClient.Timeout không áp dụng cho SendMailAsync, nên tự cắt sau 10 giây
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(SendTimeout);
        try
        {
            await client.SendMailAsync(message, timeout.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException("Gửi SMTP quá 10 giây");
        }
        logger.LogInformation("Đã gửi mail tới {Email} qua SMTP {Host}:{Port}", MailLog.Mask(mail.To), _options.Host, _options.Port);
    }
}
