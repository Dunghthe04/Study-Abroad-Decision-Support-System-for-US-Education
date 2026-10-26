using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StudyAbroad.Application.Auth;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace StudyAbroad.Infrastructure.Auth
{
    /// <summary>Hàng đợi mail trong bộ nhớ, dùng chung giữa <see cref="QueuedEmailSender"/> và <see cref="EmailDispatchBackgroundService"/>.</summary>
    public sealed class EmailQueue
    {
        // Đầy hàng đợi (SMTP treo lâu) thì bỏ mail mới thay vì làm chậm request
        private readonly Channel<Func<IEmailSender, CancellationToken, Task>> _channel =
            Channel.CreateBounded<Func<IEmailSender, CancellationToken, Task>>(new BoundedChannelOptions(1000)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
            });

        public bool TryEnqueue(Func<IEmailSender, CancellationToken, Task> send) => _channel.Writer.TryWrite(send);

        public IAsyncEnumerable<Func<IEmailSender, CancellationToken, Task>> ReadAllAsync(CancellationToken ct) =>
            _channel.Reader.ReadAllAsync(ct);
    }

    /// <summary>
    /// [Bảo mật] Gửi mail ở nền: request trả về ngay, không phải chờ SMTP. Nhờ vậy thời gian phản hồi của
    /// đăng ký / quên mật khẩu như nhau dù email có tồn tại hay không (chống dò tài khoản qua thời gian).
    /// </summary>
    public sealed class QueuedEmailSender(EmailQueue queue, ILogger<QueuedEmailSender> logger) : IEmailSender
    {
        public Task SendVerificationEmailAsync(string toEmail, string fullName, string otpCode, int expiryMinutes = 5, CancellationToken ct = default) =>
            Enqueue(toEmail, (s, t) => s.SendVerificationEmailAsync(toEmail, fullName, otpCode, expiryMinutes, t));

        public Task SendPasswordResetEmailAsync(string toEmail, string fullName, string otpCode, int expiryMinutes = 5, CancellationToken ct = default) =>
            Enqueue(toEmail, (s, t) => s.SendPasswordResetEmailAsync(toEmail, fullName, otpCode, expiryMinutes, t));

        public Task SendAccountTempLockedNotificationAsync(string toEmail, string fullName, int lockoutMinutes = 15, CancellationToken ct = default) =>
            Enqueue(toEmail, (s, t) => s.SendAccountTempLockedNotificationAsync(toEmail, fullName, lockoutMinutes, t));

        public Task SendAccountAlreadyExistsEmailAsync(string toEmail, string fullName, CancellationToken ct = default) =>
            Enqueue(toEmail, (s, t) => s.SendAccountAlreadyExistsEmailAsync(toEmail, fullName, t));

        public Task SendPhoneAlreadyInUseEmailAsync(string toEmail, CancellationToken ct = default) =>
            Enqueue(toEmail, (s, t) => s.SendPhoneAlreadyInUseEmailAsync(toEmail, t));

        public Task SendPasswordChangedNotificationAsync(string toEmail, string fullName, CancellationToken ct = default) =>
            Enqueue(toEmail, (s, t) => s.SendPasswordChangedNotificationAsync(toEmail, fullName, t));

        public Task SendParentInviteEmailAsync(string toEmail, string studentName, string inviteToken, int expiryDays, CancellationToken ct = default) =>
            Enqueue(toEmail, (s, t) => s.SendParentInviteEmailAsync(toEmail, studentName, inviteToken, expiryDays, t));

        private Task Enqueue(string toEmail, Func<IEmailSender, CancellationToken, Task> send)
        {
            if (!queue.TryEnqueue(send))
                logger.LogError("Hàng đợi email đã đầy, bỏ qua email gửi tới {Email}", toEmail);
            return Task.CompletedTask;
        }
    }

    /// <summary>Lấy mail khỏi <see cref="EmailQueue"/> và gửi lần lượt qua SMTP.</summary>
    public sealed class EmailDispatchBackgroundService(
        EmailQueue queue,
        SmtpEmailSender smtp,
        ILogger<EmailDispatchBackgroundService> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await foreach (var send in queue.ReadAllAsync(stoppingToken))
                {
                    try
                    {
                        await send(smtp, stoppingToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        logger.LogError(ex, "Lỗi khi gửi email từ hàng đợi");
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
        }
    }
}
