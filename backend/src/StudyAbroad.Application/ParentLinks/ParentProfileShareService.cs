using System.Text.Json;
using StudyAbroad.Application.AcademicProfiles;
using StudyAbroad.Application.Audit;
using StudyAbroad.Application.Auth;
using StudyAbroad.Application.Common;
using StudyAbroad.Application.Profile.Flow;
using StudyAbroad.Application.Recommendations;
using StudyAbroad.Application.TargetSchools;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.ParentLinks;

/// <summary>Một con đã liên kết nhìn từ trang Tài khoản của phụ huynh: hồ sơ học thuật của con (chỉ đọc).
/// <see cref="IsManaged"/> (1.5): con chưa có tài khoản do phụ huynh tạo, frontend hiện nút "Nhập hồ sơ cho con".
/// <see cref="DateOfBirth"/> (1.5d): hiện cạnh tên trên thẻ ghép để phụ huynh chọn đúng con; null = chưa nhập.</summary>
public record ParentChildProfileDto(
    Guid LinkId,
    Guid StudentUserId,
    string StudentName,
    string? ProfileTransferStatus,
    AcademicProfileResponse Profile,
    bool IsManaged,
    DateOnly? DateOfBirth = null);

/// <summary>[1.5d] Con thật vừa liên kết mà phụ huynh đang có con managed: hỏi "Đây có phải con bạn đã nhập?".
/// Ngày sinh con tự khai: frontend so với ngày sinh con managed, lệch thì cảnh báo nhưng vẫn cho ghép.</summary>
public record PendingMatchDto(Guid LinkId, Guid StudentUserId, string StudentName, DateOnly? DateOfBirth = null);

/// <summary>
/// Trang Tài khoản của phụ huynh. Phụ huynh không có hồ sơ riêng (1.5d), chỉ xem hồ sơ các con.
/// <see cref="PendingMatches"/>: con thật chờ phụ huynh chọn con managed tương ứng; lựa chọn là các con trong
/// <see cref="Children"/> có IsManaged = true.
/// </summary>
public record ParentProfileOverviewDto(
    List<ParentChildProfileDto> Children,
    List<PendingMatchDto> PendingMatches);

/// <summary>Phụ huynh ghép con thật (<see cref="LinkId"/>) với con managed đã nhập, hoặc <see cref="ManagedChildId"/> null
/// là "không phải ai trong số này".</summary>
public record MatchManagedChildRequest(Guid LinkId, Guid? ManagedChildId);

/// <summary>copied = đã chuyển sang con; offered = con đã có hồ sơ, chờ con chọn; skipped = không ghép.</summary>
public record MatchManagedChildResult(string Status, string StudentName);

/// <summary>Bản hồ sơ phụ huynh nhập thay (con managed) chờ học sinh đã có hồ sơ riêng chọn.</summary>
public record ParentProfileOfferDto(
    Guid LinkId,
    string ParentName,
    DateTime? OfferedAt,
    AcademicProfileResponse ParentProfile);

public interface IParentProfileShareService
{
    Task<ParentLinkResult<ParentProfileOverviewDto>> GetParentOverviewAsync(Guid parentUserId, CancellationToken ct = default);
    Task<ParentLinkResult<MatchManagedChildResult>> MatchAsync(Guid parentUserId, MatchManagedChildRequest request, CancellationToken ct = default);
    Task<ParentLinkResult<List<ParentProfileOfferDto>>> GetStudentOffersAsync(Guid studentUserId, CancellationToken ct = default);
    Task<ParentLinkResult<ParentProfileOfferDto>> DecideOfferAsync(Guid studentUserId, Guid linkId, bool accept, CancellationToken ct = default);

    /// <summary>
    /// Gọi ngay sau khi phụ huynh có tài khoản được gắn vào liên kết với con thật. Phụ huynh không có con managed thì đánh dấu
    /// skipped (không có gì để ghép); có thì để null, phụ huynh chọn trên trang Tài khoản.
    /// <paramref name="knownLinks"/>: các link vừa lưu trong request này (đang được EF theo dõi), dùng lại để không gắn 2 bản cùng khóa.
    /// </summary>
    Task PrepareMatchAfterLinkAsync(Guid parentUserId, IReadOnlyList<ParentStudentLink> knownLinks, CancellationToken ct = default);

    /// <summary>
    /// [1.5d] Job chạy một lần lúc khởi động: phụ huynh còn hồ sơ riêng từ trước 1.5d thì chuyển sang một con managed rồi xóa
    /// hồ sơ của phụ huynh. Chạy lại nhiều lần không tạo trùng (phụ huynh đã chuyển thì không còn hồ sơ). Trả số phụ huynh đã chuyển.
    /// </summary>
    Task<int> MigrateLegacyParentProfilesAsync(CancellationToken ct = default);
}

/// <summary>
/// [1.5d] Phụ huynh không có hồ sơ riêng: hồ sơ nhập trước khi con có tài khoản nằm ở con managed. Khi con thật liên kết,
/// phụ huynh chọn con managed nào là con này. Con thật chưa có hồ sơ (hoặc con managed chưa nhập gì) thì chuyển luôn;
/// cả hai đều có thì con chọn dùng bản phụ huynh hay giữ bản của mình, không bao giờ tự ghi đè.
/// Chốt xong (chuyển, đồng ý hay từ chối) thì chép trường mục tiêu, kịch bản và xóa mềm con managed.
/// </summary>
public class ParentProfileShareService(
    IParentLinkRepository links,
    IUserRepository users,
    IProfileFlowRepository profiles,
    IAcademicProfileService academicProfiles,
    ManagedChildService managedChildren,
    ITargetSchoolService targetSchools,
    IScenarioRepository scenarios,
    IAuditLogRepository auditLogs) : IParentProfileShareService
{
    private const string NotFoundMessage = "Không tìm thấy liên kết.";
    private const string ManagedNotFoundMessage = "Không tìm thấy hồ sơ con bạn đã nhập.";
    private const string AlreadyHandledMessage = "Bạn đã chọn cho con này rồi.";
    private const string ManagedBusyMessage = "Hồ sơ này đang chờ một con khác chọn.";
    private const string OfferGoneMessage = "Đề nghị này không còn hiệu lực. Vui lòng tải lại trang.";
    private const string ConflictMessage = "Liên kết vừa được cập nhật bởi một thao tác khác. Vui lòng tải lại trang.";
    /// <summary>Tên con managed job tạo để giữ hồ sơ cũ của phụ huynh; phụ huynh đổi tên được ở Con của tôi.</summary>
    public const string LegacyHolderName = "Con của bạn";

    public async Task<ParentLinkResult<ParentProfileOverviewDto>> GetParentOverviewAsync(Guid parentUserId, CancellationToken ct = default)
    {
        var children = new List<ParentChildProfileDto>();
        var pending = new List<PendingMatchDto>();
        foreach (var link in await links.GetActiveByParentAsync(parentUserId, ct))
        {
            var student = await users.GetUserById(link.StudentUserId, ct);
            if (student == null)
                continue;
            var profile = await academicProfiles.GetAcademicProfileAsync(student.Id, ct);
            if (profile.Data == null)
                continue;
            var isManaged = AgeAccess.IsOperatedByParent(student);
            children.Add(new ParentChildProfileDto(link.Id, student.Id, student.FullName, link.ProfileTransferStatus, profile.Data, isManaged, student.DateOfBirth));
            if (!isManaged && link.ProfileTransferStatus == null)
                pending.Add(new PendingMatchDto(link.Id, student.Id, student.FullName, student.DateOfBirth));
        }

        // Không còn con managed nào để chọn thì không hỏi
        if (!children.Any(c => c.IsManaged))
            pending.Clear();
        return ParentLinkResult<ParentProfileOverviewDto>.Ok(new ParentProfileOverviewDto(children, pending));
    }

    public async Task<ParentLinkResult<MatchManagedChildResult>> MatchAsync(Guid parentUserId, MatchManagedChildRequest request, CancellationToken ct = default)
    {
        var activeLinks = await links.GetActiveByParentAsync(parentUserId, ct);
        var link = activeLinks.FirstOrDefault(l => l.Id == request.LinkId);
        var student = link == null ? null : await users.GetUserById(link.StudentUserId, ct);
        if (link == null || student == null || AgeAccess.IsOperatedByParent(student))
            return ParentLinkResult<MatchManagedChildResult>.Fail(ParentLinkError.NotFound, NotFoundMessage);
        if (link.ProfileTransferStatus != null)
            return ParentLinkResult<MatchManagedChildResult>.Fail(ParentLinkError.Conflict, AlreadyHandledMessage);

        var now = DateTime.UtcNow;
        if (request.ManagedChildId is not { } managedId)
        {
            link.ProfileTransferStatus = ProfileTransferStatuses.Skipped;
            link.ProfileTransferAt = now;
            return await TryUpdateAsync(link, ct)
                ? ParentLinkResult<MatchManagedChildResult>.Ok(new MatchManagedChildResult(ProfileTransferStatuses.Skipped, student.FullName),
                    "Đã ghi nhận. Hồ sơ bạn đã nhập vẫn giữ nguyên.")
                : ParentLinkResult<MatchManagedChildResult>.Fail(ParentLinkError.Conflict, ConflictMessage);
        }

        if (await managedChildren.FindManagedChildAsync(parentUserId, managedId, ct) is null)
            return ParentLinkResult<MatchManagedChildResult>.Fail(ParentLinkError.NotFound, ManagedNotFoundMessage);
        // Một bản nhập thay chỉ chờ một con chọn: con kia chốt xong là bản này bị xóa
        if (activeLinks.Any(l => l.ProfileTransferStatus == ProfileTransferStatuses.Offered && l.SourceManagedUserId == managedId))
            return ParentLinkResult<MatchManagedChildResult>.Fail(ParentLinkError.Conflict, ManagedBusyMessage);

        var bothHaveProfile = await HasCommittedProfileAsync(managedId, ct) && await HasCommittedProfileAsync(student.Id, ct);
        link.ProfileTransferStatus = bothHaveProfile ? ProfileTransferStatuses.Offered : ProfileTransferStatuses.Copied;
        link.SourceManagedUserId = bothHaveProfile ? managedId : null;
        link.ProfileTransferAt = now;
        // Ghi liên kết trước: bấm đúp thì lần sau trượt xmin, không chép hai lần
        if (!await TryUpdateAsync(link, ct))
            return ParentLinkResult<MatchManagedChildResult>.Fail(ParentLinkError.Conflict, ConflictMessage);

        if (bothHaveProfile)
            return ParentLinkResult<MatchManagedChildResult>.Ok(new MatchManagedChildResult(ProfileTransferStatuses.Offered, student.FullName),
                $"{student.FullName} đã có hồ sơ riêng. Hệ thống đã gửi đề nghị để con chọn dùng bản của bạn hay giữ bản của con.");

        await ResolveAsync(parentUserId, managedId, student.Id, copyProfile: true, ct);
        return ParentLinkResult<MatchManagedChildResult>.Ok(new MatchManagedChildResult(ProfileTransferStatuses.Copied, student.FullName),
            $"Đã chuyển hồ sơ sang tài khoản của {student.FullName}.");
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

        var parentUserId = link.ParentUserId!.Value;
        var managedId = link.SourceManagedUserId!.Value;
        link.ProfileTransferStatus = accept ? ProfileTransferStatuses.Copied : ProfileTransferStatuses.Declined;
        link.SourceManagedUserId = null;
        link.ProfileTransferAt = DateTime.UtcNow;
        if (!await TryUpdateAsync(link, ct))
            return ParentLinkResult<ParentProfileOfferDto>.Fail(ParentLinkError.Conflict, ConflictMessage);

        // Từ chối vẫn nhận trường mục tiêu, kịch bản và vẫn xóa bản nhập thay (Việt chốt 19/11)
        await ResolveAsync(parentUserId, managedId, studentUserId, copyProfile: accept, ct);
        return ParentLinkResult<ParentProfileOfferDto>.Ok(offer,
            accept ? "Đã dùng hồ sơ của phụ huynh cho tài khoản của bạn." : "Đã giữ hồ sơ của bạn.");
    }

    public async Task PrepareMatchAfterLinkAsync(Guid parentUserId, IReadOnlyList<ParentStudentLink> knownLinks, CancellationToken ct = default)
    {
        if (await HasManagedChildAsync(parentUserId, ct))
            return;   // để null: trang Tài khoản hỏi phụ huynh

        var now = DateTime.UtcNow;
        foreach (var link in knownLinks.Where(l => l.ProfileTransferStatus == null))
        {
            link.ProfileTransferStatus = ProfileTransferStatuses.Skipped;
            link.ProfileTransferAt = now;
            await TryUpdateAsync(link, ct);
        }
    }

    public async Task<int> MigrateLegacyParentProfilesAsync(CancellationToken ct = default)
    {
        var migrated = 0;
        foreach (var parentUserId in await profiles.GetParentUserIdsWithProfileAsync(ct))
        {
            // Phụ huynh đang chờ xóa (UC-80) thì để nguyên: khôi phục tài khoản vẫn còn hồ sơ, xóa hẳn thì mất theo
            var parent = await users.GetUserById(parentUserId, ct);
            if (parent == null || parent.DeletedAt != null)
                continue;
            var profile = await profiles.GetProfileByUserIdAsync(parentUserId, ct);
            if (profile == null)
                continue;

            Guid? holderId = null;
            if (ProfileFlowService.HasCommittedProfile(profile))
            {
                var parentLinks = await links.GetActiveByParentAsync(parentUserId, ct);
                var waiting = parentLinks
                    .Where(l => l.ProfileTransferStatus == ProfileTransferStatuses.Offered && l.SourceManagedUserId == null)
                    .ToList();
                // Đã chuyển cho một con (copied) và không còn đề nghị nào chờ thì hồ sơ đã có ở con, chỉ cần xóa
                if (waiting.Count > 0 || !parentLinks.Any(l => l.ProfileTransferStatus == ProfileTransferStatuses.Copied))
                {
                    holderId = await GetOrCreateHolderAsync(parentUserId, ct);
                    if (holderId == null)
                        continue;   // đủ 5 con managed: giữ hồ sơ, lần khởi động sau thử lại
                    await CopyProfileAsync(parentUserId, holderId.Value, ct);
                    await CopyTargetSchoolsAsync(parentUserId, holderId.Value, ct);

                    // Một con managed chỉ làm nguồn cho một đề nghị; đề nghị khác (hiếm) về null để phụ huynh ghép lại
                    for (var i = 0; i < waiting.Count; i++)
                    {
                        waiting[i].SourceManagedUserId = i == 0 ? holderId : null;
                        if (i > 0)
                            waiting[i].ProfileTransferStatus = null;
                        await TryUpdateAsync(waiting[i], ct);
                    }
                }
            }

            await profiles.DeleteProfileAsync(parentUserId, ct);
            await auditLogs.AddAsync(new AuditLog
            {
                ActorUserId = null,
                Action = "parent.profile.migrate",
                EntityType = "user",
                EntityId = parentUserId.ToString(),
                DetailJson = JsonSerializer.Serialize(new { managedChildId = holderId }),
            }, ct);
            migrated++;
        }
        return migrated;
    }

    /// <summary>
    /// Chép hồ sơ <paramref name="fromUserId"/> sang <paramref name="toUserId"/> trong một transaction (dùng lại lệnh lưu của luồng 6 bước):
    /// các trường học thuật, tài chính, nguyện vọng, bảng điểm, chứng chỉ, hoạt động. Không chép nháp, tệp học bạ, kết quả phân tích
    /// hay gợi ý trường. Job chuyển hồ sơ cũ của phụ huynh (1.5d) cũng gọi hàm này.
    /// </summary>
    public async Task CopyProfileAsync(Guid fromUserId, Guid toUserId, CancellationToken ct)
    {
        var source = await profiles.GetProfileByUserIdAsync(fromUserId, ct)
            ?? throw new InvalidOperationException("Không có hồ sơ để chép.");
        var scores = await profiles.GetTranscriptScoresAsync(source.Id, ct);
        var activities = await profiles.GetProfileActivitiesAsync(source.Id, ct);
        var certifications = await profiles.GetProfileCertificationsAsync(source.Id, ct);

        var target = await profiles.EnsureProfileExistsAsync(toUserId, ct);
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
        // Bản nháp cũ của con không còn khớp với hồ sơ mới; tệp học bạ thuộc bản nguồn nên không chép
        target.DraftFlowStateJson = null;
        target.TranscriptFileUrl = null;
        target.FlowSubmittedAt = DateTimeOffset.UtcNow;
    }

    // ───────────────────────────── Nội bộ ─────────────────────────────

    /// <summary>
    /// Chốt chuyển từ con managed sang con thật: chép hồ sơ (khi <paramref name="copyProfile"/> và con managed có hồ sơ),
    /// chép trường mục tiêu, kịch bản, rồi mới xóa mềm con managed. Lỗi giữa chừng thì con managed vẫn còn, không mất dữ liệu.
    /// Không chép lịch sử gợi ý (UC-87) và lịch sử chat.
    /// </summary>
    private async Task ResolveAsync(Guid parentUserId, Guid managedId, Guid studentUserId, bool copyProfile, CancellationToken ct)
    {
        var profileCopied = copyProfile && await HasCommittedProfileAsync(managedId, ct);
        if (profileCopied)
            await CopyProfileAsync(managedId, studentUserId, ct);

        var schools = await CopyTargetSchoolsAsync(managedId, studentUserId, ct);
        var copiedScenarios = await CopyScenariosAsync(managedId, studentUserId, ct);

        // Không ghi tên con vào log, chỉ id
        await auditLogs.AddAsync(new AuditLog
        {
            ActorUserId = parentUserId,
            Action = "parent.managed_child.transfer",
            EntityType = "user",
            EntityId = studentUserId.ToString(),
            DetailJson = JsonSerializer.Serialize(new
            {
                managedChildId = managedId,
                studentUserId,
                profileCopied,
                targetSchools = schools,
                scenarios = copiedScenarios,
            }),
        }, ct);

        await managedChildren.DeleteAsync(parentUserId, managedId, ct);
    }

    /// <summary>Thêm từng trường qua service trường mục tiêu (UC-30) để giữ đúng kiểm tra trùng, hạn nộp.
    /// Trường con đã có thì service trả lỗi trùng, bỏ qua. Dòng "phụ huynh gợi ý" chưa phải trường của con managed nên không chép.</summary>
    private async Task<int> CopyTargetSchoolsAsync(Guid managedId, Guid studentUserId, CancellationToken ct)
    {
        var copied = 0;
        foreach (var school in (await targetSchools.GetTargetSchoolsAsync(managedId, ct: ct)).Items)
        {
            if (TargetSchoolStatuses.IsParentSuggestion(school.Status))
                continue;
            var result = await targetSchools.AddTargetSchoolAsync(studentUserId,
                new AddTargetSchoolRequest(school.UniversityId, null, school.Category, school.ApplicationRound, school.Deadline, school.Notes), ct);
            if (result.Success)
                copied++;
        }
        return copied;
    }

    /// <summary>Chép kịch bản what-if (UC-86), không vượt giới hạn mỗi học sinh. Thêm bản cũ trước để thứ tự mới nhất giữ nguyên.</summary>
    private async Task<int> CopyScenariosAsync(Guid managedId, Guid studentUserId, CancellationToken ct)
    {
        var room = ScenarioService.MaxPerUser - await scenarios.CountByUserAsync(studentUserId, ct);
        if (room <= 0)
            return 0;

        var rows = (await scenarios.ListByUserAsync(managedId, ct)).Take(room).Reverse().ToList();
        foreach (var row in rows)
        {
            await scenarios.AddAsync(new WhatIfSimulation
            {
                UserId = studentUserId,
                Title = row.Title,
                SimulatedProfileJson = row.SimulatedProfileJson,
                SimulatedRecommendationsJson = row.SimulatedRecommendationsJson,
                Notes = row.Notes,
            }, ct);
        }
        return rows.Count;
    }

    /// <summary>Liên kết đang chờ con chọn, con managed nguồn vẫn là con managed của phụ huynh này và có hồ sơ; không thì null.</summary>
    private async Task<ParentProfileOfferDto?> ToOfferAsync(ParentStudentLink link, CancellationToken ct)
    {
        if (link.Status != ParentLinkStatuses.Active
            || link.ProfileTransferStatus != ProfileTransferStatuses.Offered
            || link.ParentUserId is not { } parentUserId
            || link.SourceManagedUserId is not { } managedId)
            return null;

        var parent = await users.GetUserById(parentUserId, ct);
        if (parent == null || parent.DeletedAt != null)
            return null;
        if (await managedChildren.FindManagedChildAsync(parentUserId, managedId, ct) is null || !await HasCommittedProfileAsync(managedId, ct))
            return null;

        var profile = await academicProfiles.GetAcademicProfileAsync(managedId, ct);
        return profile.Data == null
            ? null
            : new ParentProfileOfferDto(link.Id, parent.FullName, link.ProfileTransferAt, profile.Data);
    }

    /// <summary>Con managed giữ hồ sơ cũ của phụ huynh. Lần chạy trước tạo xong mà chưa chép (lỗi giữa chừng) thì dùng lại,
    /// không tạo thêm; con managed tên khác hoặc đã có hồ sơ thì không đụng. null = không tạo được (đủ 5 con).</summary>
    private async Task<Guid?> GetOrCreateHolderAsync(Guid parentUserId, CancellationToken ct)
    {
        foreach (var link in await links.GetActiveByParentAsync(parentUserId, ct))
        {
            if (await users.GetUserById(link.StudentUserId, ct) is { } user && AgeAccess.IsOperatedByParent(user)
                && user.FullName == LegacyHolderName && !await HasCommittedProfileAsync(user.Id, ct))
                return user.Id;
        }
        var created = await managedChildren.AddWithoutDateOfBirthAsync(parentUserId, LegacyHolderName, ct);
        return created.Child?.StudentUserId;
    }

    private async Task<bool> HasManagedChildAsync(Guid parentUserId, CancellationToken ct)
    {
        foreach (var link in await links.GetActiveByParentAsync(parentUserId, ct))
        {
            if (await users.GetUserById(link.StudentUserId, ct) is { } user && AgeAccess.IsOperatedByParent(user))
                return true;
        }
        return false;
    }

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
