using StudyAbroad.Application.AcademicProfiles;
using StudyAbroad.Application.Auth;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.Profile.Flow;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.ParentLinks;

/// <summary>Một con đã liên kết nhìn từ trang Tài khoản của phụ huynh: hồ sơ học thuật của con (chỉ đọc).
/// <see cref="IsManaged"/> (1.5): con chưa có tài khoản do phụ huynh tạo, frontend hiện nút "Nhập hồ sơ cho con".</summary>
public record ParentChildProfileDto(
    Guid LinkId,
    Guid StudentUserId,
    string StudentName,
    string? ProfileTransferStatus,
    AcademicProfileResponse Profile,
    bool IsManaged);

/// <summary>
/// Trang Tài khoản của phụ huynh. <see cref="HasPendingOwnProfile"/>: phụ huynh đã nhập hồ sơ trước khi liên kết và chưa chọn
/// chuyển cho con nào (chỉ xảy ra khi có từ 2 con; 1 con thì hệ thống tự chuyển lúc liên kết).
/// </summary>
public record ParentProfileOverviewDto(
    bool HasOwnProfile,
    bool HasPendingOwnProfile,
    List<ParentChildProfileDto> Children);

/// <summary>Phụ huynh chọn con nhận hồ sơ, hoặc <see cref="LinkId"/> null là không chuyển cho con nào.</summary>
public record ShareParentProfileRequest(Guid? LinkId);

/// <summary>Kết quả chuyển: copied = đã chép sang con; offered = con đã có hồ sơ, chờ con chọn; skipped = không chuyển.</summary>
public record ShareParentProfileResult(string Status, string? StudentName);

/// <summary>Đề nghị dùng hồ sơ phụ huynh hiện cho học sinh đã có hồ sơ riêng.</summary>
public record ParentProfileOfferDto(
    Guid LinkId,
    string ParentName,
    DateTime? OfferedAt,
    AcademicProfileResponse ParentProfile);

public interface IParentProfileShareService
{
    Task<ParentLinkResult<ParentProfileOverviewDto>> GetParentOverviewAsync(Guid parentUserId, CancellationToken ct = default);
    Task<ParentLinkResult<ShareParentProfileResult>> ShareAsync(Guid parentUserId, Guid? linkId, CancellationToken ct = default);
    Task<ParentLinkResult<List<ParentProfileOfferDto>>> GetStudentOffersAsync(Guid studentUserId, CancellationToken ct = default);
    Task<ParentLinkResult<ParentProfileOfferDto>> DecideOfferAsync(Guid studentUserId, Guid linkId, bool accept, CancellationToken ct = default);

    /// <summary>
    /// Gọi ngay sau khi phụ huynh có tài khoản được gắn vào liên kết: phụ huynh có đúng 1 con thì chuyển hồ sơ luôn
    /// (con chưa có hồ sơ → chép, con có rồi → để con chọn). Từ 2 con thì để phụ huynh chọn trên trang Tài khoản.
    /// <paramref name="knownLinks"/>: các link vừa lưu trong request này (đang được EF theo dõi), dùng lại để không gắn 2 bản cùng khóa.
    /// </summary>
    Task AutoShareAfterLinkAsync(Guid parentUserId, IReadOnlyList<ParentStudentLink> knownLinks, CancellationToken ct = default);
}

/// <summary>
/// Quy tắc hồ sơ phụ huynh – con (Việt chốt 17/11/2026): phụ huynh chưa liên kết tự nhập hồ sơ như học sinh; liên kết rồi thì
/// xem hồ sơ của con (chỉ đọc). Hồ sơ phụ huynh nhập trước được chuyển cho một con: con chưa có hồ sơ thì chép sang,
/// con có rồi thì con chọn dùng bản phụ huynh hay giữ bản của mình, không bao giờ tự ghi đè.
/// </summary>
public class ParentProfileShareService(
    IParentLinkRepository links,
    IUserRepository users,
    IProfileFlowRepository profiles,
    IAcademicProfileService academicProfiles) : IParentProfileShareService
{
    private const string NotFoundMessage = "Không tìm thấy liên kết.";
    private const string NoProfileMessage = "Bạn chưa có hồ sơ học thuật để chuyển cho con.";
    private const string AlreadyHandledMessage = "Hồ sơ của bạn đã được chuyển trước đó.";
    private const string OfferGoneMessage = "Đề nghị này không còn hiệu lực. Vui lòng tải lại trang.";
    private const string ConflictMessage = "Liên kết vừa được cập nhật bởi một thao tác khác. Vui lòng tải lại trang.";

    public async Task<ParentLinkResult<ParentProfileOverviewDto>> GetParentOverviewAsync(Guid parentUserId, CancellationToken ct = default)
    {
        var activeLinks = await GetActiveParentLinksAsync(parentUserId, ct);
        var hasOwnProfile = await HasCommittedProfileAsync(parentUserId, ct);

        var children = new List<ParentChildProfileDto>();
        foreach (var link in activeLinks)
        {
            var student = await users.GetUserById(link.StudentUserId, ct);
            if (student == null)
                continue;
            var profile = await academicProfiles.GetAcademicProfileAsync(student.Id, ct);
            if (profile.Data == null)
                continue;
            children.Add(new ParentChildProfileDto(link.Id, student.Id, student.FullName, link.ProfileTransferStatus, profile.Data,
                AgeAccess.IsOperatedByParent(student)));
        }

        var pending = hasOwnProfile && children.Count > 0 && !IsHandled(activeLinks);
        return ParentLinkResult<ParentProfileOverviewDto>.Ok(new ParentProfileOverviewDto(hasOwnProfile, pending, children));
    }

    public async Task<ParentLinkResult<ShareParentProfileResult>> ShareAsync(Guid parentUserId, Guid? linkId, CancellationToken ct = default)
    {
        var activeLinks = await GetActiveParentLinksAsync(parentUserId, ct);
        if (IsHandled(activeLinks))
            return ParentLinkResult<ShareParentProfileResult>.Fail(ParentLinkError.Conflict, AlreadyHandledMessage);
        if (!await HasCommittedProfileAsync(parentUserId, ct))
            return ParentLinkResult<ShareParentProfileResult>.Fail(ParentLinkError.Validation, NoProfileMessage);

        if (linkId is null)
        {
            // Không chuyển cho con nào: đánh dấu mọi liên kết để thẻ chọn không hiện lại
            var now = DateTime.UtcNow;
            foreach (var link in activeLinks)
            {
                link.ProfileTransferStatus = ProfileTransferStatuses.Skipped;
                link.ProfileTransferAt = now;
                if (!await TryUpdateAsync(link, ct))
                    return ParentLinkResult<ShareParentProfileResult>.Fail(ParentLinkError.Conflict, ConflictMessage);
            }
            return ParentLinkResult<ShareParentProfileResult>.Ok(
                new ShareParentProfileResult(ProfileTransferStatuses.Skipped, null), "Đã ghi nhận không chuyển hồ sơ.");
        }

        var target = activeLinks.FirstOrDefault(l => l.Id == linkId);
        if (target == null)
            return ParentLinkResult<ShareParentProfileResult>.Fail(ParentLinkError.NotFound, NotFoundMessage);

        var status = await ShareToLinkAsync(parentUserId, target, ct);
        if (status == null)
            return ParentLinkResult<ShareParentProfileResult>.Fail(ParentLinkError.Conflict, ConflictMessage);

        var student = await users.GetUserById(target.StudentUserId, ct);
        var message = status == ProfileTransferStatuses.Copied
            ? "Đã chuyển hồ sơ sang tài khoản của con."
            : "Con đã có hồ sơ riêng. Hệ thống đã gửi đề nghị để con chọn dùng bản của bạn hay giữ bản của con.";
        return ParentLinkResult<ShareParentProfileResult>.Ok(new ShareParentProfileResult(status, student?.FullName), message);
    }

    public async Task<ParentLinkResult<List<ParentProfileOfferDto>>> GetStudentOffersAsync(Guid studentUserId, CancellationToken ct = default)
    {
        var offers = new List<ParentProfileOfferDto>();
        foreach (var link in await links.GetByStudentAsync(studentUserId, ct))
        {
            var offer = await ToOfferAsync(link, ct);
            if (offer != null)
                offers.Add(offer);
        }
        return ParentLinkResult<List<ParentProfileOfferDto>>.Ok(offers);
    }

    public async Task<ParentLinkResult<ParentProfileOfferDto>> DecideOfferAsync(Guid studentUserId, Guid linkId, bool accept, CancellationToken ct = default)
    {
        var link = await links.GetByIdAsync(linkId, ct);
        if (link == null || link.StudentUserId != studentUserId)
            return ParentLinkResult<ParentProfileOfferDto>.Fail(ParentLinkError.NotFound, NotFoundMessage);

        var offer = await ToOfferAsync(link, ct);
        if (offer == null)
            return ParentLinkResult<ParentProfileOfferDto>.Fail(ParentLinkError.Conflict, OfferGoneMessage);

        if (accept)
            await CopyProfileAsync(link.ParentUserId!.Value, studentUserId, ct);

        link.ProfileTransferStatus = accept ? ProfileTransferStatuses.Copied : ProfileTransferStatuses.Declined;
        link.ProfileTransferAt = DateTime.UtcNow;
        if (!await TryUpdateAsync(link, ct))
            return ParentLinkResult<ParentProfileOfferDto>.Fail(ParentLinkError.Conflict, ConflictMessage);

        return ParentLinkResult<ParentProfileOfferDto>.Ok(offer,
            accept ? "Đã dùng hồ sơ của phụ huynh cho tài khoản của bạn." : "Đã giữ hồ sơ của bạn.");
    }

    public async Task AutoShareAfterLinkAsync(Guid parentUserId, IReadOnlyList<ParentStudentLink> knownLinks, CancellationToken ct = default)
    {
        var activeLinks = (await GetActiveParentLinksAsync(parentUserId, ct))
            .Select(l => knownLinks.FirstOrDefault(k => k.Id == l.Id) ?? l)
            .ToList();
        if (activeLinks.Count != 1 || IsHandled(activeLinks))
            return;
        if (!await HasCommittedProfileAsync(parentUserId, ct))
            return;

        await ShareToLinkAsync(parentUserId, activeLinks[0], ct);
    }

    // ───────────────────────────── Nội bộ ─────────────────────────────

    /// <summary>Con chưa có hồ sơ → chép; có rồi → để con chọn. Trả trạng thái mới, null khi link vừa bị sửa cùng lúc.</summary>
    private async Task<string?> ShareToLinkAsync(Guid parentUserId, ParentStudentLink link, CancellationToken ct)
    {
        var childHasProfile = await HasCommittedProfileAsync(link.StudentUserId, ct);
        if (!childHasProfile)
            await CopyProfileAsync(parentUserId, link.StudentUserId, ct);

        link.ProfileTransferStatus = childHasProfile ? ProfileTransferStatuses.Offered : ProfileTransferStatuses.Copied;
        link.ProfileTransferAt = DateTime.UtcNow;
        return await TryUpdateAsync(link, ct) ? link.ProfileTransferStatus : null;
    }

    /// <summary>
    /// Chép hồ sơ phụ huynh sang con trong một transaction (dùng lại lệnh lưu của luồng 6 bước): các trường học thuật, tài chính,
    /// nguyện vọng, bảng điểm, chứng chỉ, hoạt động. Không chép nháp, tệp học bạ, kết quả phân tích hay gợi ý trường.
    /// </summary>
    private async Task CopyProfileAsync(Guid parentUserId, Guid studentUserId, CancellationToken ct)
    {
        var source = await profiles.GetProfileByUserIdAsync(parentUserId, ct)
            ?? throw new InvalidOperationException("Phụ huynh chưa có hồ sơ.");
        var scores = await profiles.GetTranscriptScoresAsync(source.Id, ct);
        var activities = await profiles.GetProfileActivitiesAsync(source.Id, ct);
        var certifications = await profiles.GetProfileCertificationsAsync(source.Id, ct);

        var target = await profiles.EnsureProfileExistsAsync(studentUserId, ct);
        CopyFields(source, target);

        await profiles.CommitProfileFlowAsync(
            target,
            scores.Select(s => new TranscriptScore
            {
                TermName = s.TermName,
                TermOrder = s.TermOrder,
                GradeName = s.GradeName,
                Semester = s.Semester,
                Subject = s.Subject,
                Score = s.Score,
                RawScore = s.RawScore,
                Credits = s.Credits,
            }).ToList(),
            activities.Select(a => new ProfileActivity
            {
                Kind = a.Kind,
                Title = a.Title,
                Organization = a.Organization,
                Role = a.Role,
                Description = a.Description,
                StartDate = a.StartDate,
                EndDate = a.EndDate,
                AwardYear = a.AwardYear,
                ImpactLevel = a.ImpactLevel,
                DurationMonths = a.DurationMonths,
                IsOngoing = a.IsOngoing,
            }).ToList(),
            certifications.Select(c => new ProfileCertification
            {
                Category = c.Category,
                TestType = c.TestType,
                CustomName = c.CustomName,
                Subject = c.Subject,
                OverallScore = c.OverallScore,
                ScaleText = c.ScaleText,
                SectionScoresJson = c.SectionScoresJson,
                TestDate = c.TestDate,
                IsTarget = c.IsTarget,
                SortOrder = c.SortOrder,
            }).ToList(),
            ct);
    }

    internal static void CopyFields(StudentProfile source, StudentProfile target)
    {
        target.TargetLevel = source.TargetLevel;
        target.CurrentSchool = source.CurrentSchool;
        target.EducationSystem = source.EducationSystem;
        target.CurrentEducationLevel = source.CurrentEducationLevel;
        target.GraduationYear = source.GraduationYear;
        target.CurrentGrade = source.CurrentGrade;
        target.GradeScale = source.GradeScale;
        target.OverallGpa = source.OverallGpa;
        target.GpaMode = source.GpaMode;
        target.OverallGpa10 = source.OverallGpa10;
        target.IntendedMajor = source.IntendedMajor;
        target.TargetIntake = source.TargetIntake;
        target.EnglishTestPlanned = source.EnglishTestPlanned;
        target.StandardizedTestPlanned = source.StandardizedTestPlanned;
        target.ExtracurricularTier = source.ExtracurricularTier;
        target.BudgetTier = source.BudgetTier;
        target.BudgetMinUsd = source.BudgetMinUsd;
        target.BudgetMaxUsd = source.BudgetMaxUsd;
        target.MaxExpectedTuitionUsd = source.MaxExpectedTuitionUsd;
        target.FundingSource = source.FundingSource;
        target.NeedsScholarship = source.NeedsScholarship;
        target.ScholarshipNeed = source.ScholarshipNeed;
        target.FinancialNotes = source.FinancialNotes;
        target.PreferredStates = [.. source.PreferredStates];
        target.TargetSchoolType = source.TargetSchoolType;
        // Bản nháp cũ của con không còn khớp với hồ sơ mới; tệp học bạ là của phụ huynh nên không chép
        target.DraftFlowStateJson = null;
        target.TranscriptFileUrl = null;
        target.FlowSubmittedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Liên kết đang chờ con chọn, phụ huynh còn tài khoản và còn hồ sơ; không còn thì null.</summary>
    private async Task<ParentProfileOfferDto?> ToOfferAsync(ParentStudentLink link, CancellationToken ct)
    {
        if (link.Status != ParentLinkStatuses.Active
            || link.ProfileTransferStatus != ProfileTransferStatuses.Offered
            || link.ParentUserId is not { } parentUserId)
            return null;

        var parent = await users.GetUserById(parentUserId, ct);
        if (parent == null || parent.DeletedAt != null || !await HasCommittedProfileAsync(parentUserId, ct))
            return null;

        var profile = await academicProfiles.GetAcademicProfileAsync(parentUserId, ct);
        return profile.Data == null
            ? null
            : new ParentProfileOfferDto(link.Id, parent.FullName, link.ProfileTransferAt, profile.Data);
    }

    private async Task<List<ParentStudentLink>> GetActiveParentLinksAsync(Guid parentUserId, CancellationToken ct) =>
        await links.GetActiveByParentAsync(parentUserId, ct);

    /// <summary>Phụ huynh đã chọn (chuyển cho một con hoặc không chuyển) thì không hỏi lại.</summary>
    private static bool IsHandled(IEnumerable<ParentStudentLink> activeLinks) =>
        activeLinks.Any(l => l.ProfileTransferStatus != null);

    private async Task<bool> HasCommittedProfileAsync(Guid userId, CancellationToken ct) =>
        await profiles.GetProfileByUserIdAsync(userId, ct) is { } profile && ProfileFlowService.HasCommittedProfile(profile);

    private async Task<bool> TryUpdateAsync(ParentStudentLink link, CancellationToken ct)
    {
        try
        {
            await links.UpdateAsync(link, ct);
            return true;
        }
        catch (ConcurrencyConflictException)
        {
            return false;
        }
    }
}
