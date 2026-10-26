using StudyAbroad.Application.Auth;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.UnitTests;

// Fake dùng chung cho AuthServiceTests và ParentLinkServiceTests (không chạm DB thật)

internal sealed class FakeUserRepository : IUserRepository
{
    public List<User> Users { get; } = [];
    public List<UserSession> Sessions { get; } = [];
    public List<OtpToken> Otps { get; } = [];

    // Giả lập request song song đã ghi trước: lần lưu kế tiếp ném ConcurrencyConflictException như UserRepository thật
    public int UserConflictsToThrow { get; set; }
    public int OtpConflictsToThrow { get; set; }

    public Task<bool> EmailExistAsync(string email, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.Any(u => u.Email == email));

    public Task<bool> PhoneExistAsync(string phone, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.Any(u => u.Phone == phone));

    public Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        Users.Add(user);
        return Task.CompletedTask;
    }

    public Task<User?> GetUserByEmail(string email, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.FirstOrDefault(u => u.Email == email));

    public Task<User?> GetUserById(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.FirstOrDefault(u => u.Id == id));

    public Task UpdateUserAsync(User user, CancellationToken cancellationToken = default)
    {
        if (UserConflictsToThrow > 0)
        {
            UserConflictsToThrow--;
            throw new ConcurrencyConflictException("conflict");
        }
        var idx = Users.FindIndex(u => u.Id == user.Id);
        if (idx >= 0) Users[idx] = user;
        return Task.CompletedTask;
    }

    public Task CreateSessionAsync(UserSession session, CancellationToken cancellationToken = default)
    {
        Sessions.Add(session);
        return Task.CompletedTask;
    }

    public Task<UserSession?> GetSessionByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        Task.FromResult(Sessions.FirstOrDefault(s => s.TokenHash == tokenHash));

    public Task RevokeSessionAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        var s = Sessions.FirstOrDefault(x => x.TokenHash == tokenHash);
        if (s != null) s.IsRevoked = true;
        return Task.CompletedTask;
    }

    public Task RevokeAllSessionsAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        foreach (var s in Sessions.Where(x => x.UserId == userId))
        {
            s.IsRevoked = true;
        }
        return Task.CompletedTask;
    }

    public Task RevokeOtherSessionsAsync(Guid userId, string keepTokenHash, CancellationToken cancellationToken = default)
    {
        foreach (var s in Sessions.Where(x => x.UserId == userId && x.TokenHash != keepTokenHash))
        {
            s.IsRevoked = true;
        }
        return Task.CompletedTask;
    }

    public Task SaveOtpAsync(OtpToken token, CancellationToken cancellationToken = default)
    {
        var idx = Otps.FindIndex(o => o.Id == token.Id);
        if (idx >= 0) Otps[idx] = token;
        else Otps.Add(token);
        return Task.CompletedTask;
    }

    public Task<OtpToken?> GetLatestOtpAsync(Guid userId, string purpose, CancellationToken cancellationToken = default) =>
        Task.FromResult(Otps
            .Where(o => o.UserId == userId && o.Purpose == purpose && o.UsedAt == null)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefault());

    public Task InvalidateOtpsAsync(Guid userId, string purpose, CancellationToken cancellationToken = default)
    {
        foreach (var o in Otps.Where(x => x.UserId == userId && x.Purpose == purpose && x.UsedAt == null))
        {
            o.UsedAt = DateTime.UtcNow;
        }
        return Task.CompletedTask;
    }

    public Task UpdateOtpAsync(OtpToken token, CancellationToken cancellationToken = default)
    {
        if (OtpConflictsToThrow > 0)
        {
            OtpConflictsToThrow--;
            throw new ConcurrencyConflictException("conflict");
        }
        var idx = Otps.FindIndex(o => o.Id == token.Id);
        if (idx >= 0) Otps[idx] = token;
        return Task.CompletedTask;
    }

    public Task<int> CountOtpRequestsInLastHourAsync(Guid userId, string purpose, CancellationToken cancellationToken = default)
    {
        var oneHourAgo = DateTime.UtcNow.AddHours(-1);
        return Task.FromResult(Otps.Count(o => o.UserId == userId && o.Purpose == purpose && o.CreatedAt >= oneHourAgo));
    }

    public Task<int> UnlockExpiredAccountsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var expired = Users.Where(u => u.Status == UserStatuses.TempLocked && u.LockoutEnd != null && u.LockoutEnd <= now).ToList();
        foreach (var u in expired)
        {
            u.Status = u.Role == UserRoles.Center ? UserStatuses.Pending : UserStatuses.Active;
            u.FailedLoginAttempts = 0;
            u.LockoutEnd = null;
        }
        return Task.FromResult(expired.Count);
    }

    public Task<int> CleanupExpiredOtpsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var count = Otps.RemoveAll(o => o.ExpiresAt < now);
        return Task.FromResult(count);
    }

    public Task<List<User>> GetAllUsersAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Users.ToList());
}

internal sealed class FakeEmailSender : IEmailSender
{
    public List<(string To, string Purpose, string Code)> SentEmails { get; } = [];

    public Task SendVerificationEmailAsync(string toEmail, string fullName, string otpCode, int expiryMinutes = 5, CancellationToken ct = default)
    {
        SentEmails.Add((toEmail, OtpPurposes.VerifyEmail, otpCode));
        return Task.CompletedTask;
    }

    public Task SendPasswordResetEmailAsync(string toEmail, string fullName, string otpCode, int expiryMinutes = 5, CancellationToken ct = default)
    {
        SentEmails.Add((toEmail, OtpPurposes.ResetPassword, otpCode));
        return Task.CompletedTask;
    }

    public Task SendAccountTempLockedNotificationAsync(string toEmail, string fullName, int lockoutMinutes = 15, CancellationToken ct = default)
    {
        SentEmails.Add((toEmail, "temp_locked_notice", $"lockout:{lockoutMinutes}m"));
        return Task.CompletedTask;
    }

    public Task SendAccountAlreadyExistsEmailAsync(string toEmail, string fullName, CancellationToken ct = default)
    {
        SentEmails.Add((toEmail, "account_exists_notice", ""));
        return Task.CompletedTask;
    }

    public Task SendPhoneAlreadyInUseEmailAsync(string toEmail, CancellationToken ct = default)
    {
        SentEmails.Add((toEmail, "phone_in_use_notice", ""));
        return Task.CompletedTask;
    }

    public Task SendPasswordChangedNotificationAsync(string toEmail, string fullName, CancellationToken ct = default)
    {
        SentEmails.Add((toEmail, "password_changed_notice", ""));
        return Task.CompletedTask;
    }

    public Task SendParentInviteEmailAsync(string toEmail, string studentName, string inviteToken, int expiryDays, CancellationToken ct = default)
    {
        SentEmails.Add((toEmail, "parent_invite", inviteToken));
        return Task.CompletedTask;
    }

    public Task SendParentLinkChangedEmailAsync(string toEmail, string studentName, string parentLabel, bool linked, CancellationToken ct = default)
    {
        SentEmails.Add((toEmail, linked ? "parent_linked" : "parent_unlinked", parentLabel));
        return Task.CompletedTask;
    }
}

/// <summary>Giả lập ParentLinkRepository: trả bản sao như AsNoTracking, lưu thay bản cũ; có thể ép ném conflict (xmin).</summary>
internal sealed class FakeParentLinkRepository : IParentLinkRepository
{
    public List<ParentStudentLink> Links { get; } = [];
    public List<Consent> Consents { get; } = [];

    // Giả lập request song song đã ghi trước: lần lưu kế tiếp ném ConcurrencyConflictException
    public int ConflictsToThrow { get; set; }

    public Task AddAsync(ParentStudentLink link, CancellationToken ct = default)
    {
        Links.Add(Clone(link));
        return Task.CompletedTask;
    }

    public Task UpdateAsync(ParentStudentLink link, CancellationToken ct = default)
    {
        ThrowIfConflict();
        Replace(link);
        return Task.CompletedTask;
    }

    public Task<ParentStudentLink?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        Task.FromResult(Links.Where(l => l.Id == id).Select(Clone).FirstOrDefault());

    public Task<ParentStudentLink?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default) =>
        Task.FromResult(Links.Where(l => l.TokenHash == tokenHash).Select(Clone).FirstOrDefault());

    public Task<List<ParentStudentLink>> GetByStudentAsync(Guid studentUserId, CancellationToken ct = default) =>
        Task.FromResult(Links.Where(l => l.StudentUserId == studentUserId).OrderByDescending(l => l.CreatedAt).Select(Clone).ToList());

    public Task<List<ParentStudentLink>> GetActiveByParentAsync(Guid parentUserId, CancellationToken ct = default) =>
        Task.FromResult(Links.Where(l => l.ParentUserId == parentUserId && l.Status == ParentLinkStatuses.Active).Select(Clone).ToList());

    public Task<Consent?> GetConsentAsync(Guid consentId, CancellationToken ct = default) =>
        Task.FromResult(Consents.Where(c => c.Id == consentId).Select(c => new Consent
        {
            Id = c.Id, CreatedAt = c.CreatedAt, UserId = c.UserId, Purpose = c.Purpose, Granted = c.Granted,
            PolicyVersion = c.PolicyVersion, GrantedByUserId = c.GrantedByUserId, GrantedIp = c.GrantedIp,
        }).FirstOrDefault());

    public Task SaveAcceptAsync(ParentStudentLink link, Consent consent, CancellationToken ct = default)
    {
        ThrowIfConflict();
        Consents.Add(consent);
        Replace(link);
        return Task.CompletedTask;
    }

    public Task SaveRevokeAsync(ParentStudentLink link, Consent? consent, CancellationToken ct = default)
    {
        ThrowIfConflict();
        Replace(link);
        if (consent != null)
        {
            var idx = Consents.FindIndex(c => c.Id == consent.Id);
            if (idx >= 0) Consents[idx] = consent;
        }
        return Task.CompletedTask;
    }

    private void ThrowIfConflict()
    {
        if (ConflictsToThrow <= 0) return;
        ConflictsToThrow--;
        throw new ConcurrencyConflictException("conflict");
    }

    private void Replace(ParentStudentLink link)
    {
        var idx = Links.FindIndex(l => l.Id == link.Id);
        if (idx >= 0) Links[idx] = Clone(link);
    }

    private static ParentStudentLink Clone(ParentStudentLink l) => new()
    {
        Id = l.Id, CreatedAt = l.CreatedAt, UpdatedAt = l.UpdatedAt, StudentUserId = l.StudentUserId, ParentEmail = l.ParentEmail,
        ParentUserId = l.ParentUserId, Status = l.Status, TokenHash = l.TokenHash, TokenExpiresAt = l.TokenExpiresAt,
        InviteSentCount = l.InviteSentCount, LastInviteSentAt = l.LastInviteSentAt, AcceptedAt = l.AcceptedAt,
        ConsentId = l.ConsentId, RevokedAt = l.RevokedAt, RevokedByUserId = l.RevokedByUserId, Version = l.Version,
    };
}
