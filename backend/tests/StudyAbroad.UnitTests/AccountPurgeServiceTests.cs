using StudyAbroad.Application.Auth;
using StudyAbroad.Application.Profile.Flow;
using StudyAbroad.Domain.Constants;

namespace StudyAbroad.UnitTests;

/// <summary>[UC-80] Job xóa hẳn tài khoản: thứ tự các bước, bỏ qua tài khoản vừa khôi phục, lỗi một người không chặn người khác.</summary>
public class AccountPurgeServiceTests
{
    private sealed class FakePurgeRepository : IAccountPurgeRepository
    {
        public List<Guid> DueIds { get; } = [];
        /// <summary>Id không còn thỏa điều kiện lúc kiểm tra lại (vừa khôi phục).</summary>
        public HashSet<Guid> Restored { get; } = [];
        /// <summary>Id làm bước xóa user ném lỗi.</summary>
        public HashSet<Guid> FailOnDelete { get; } = [];
        public List<string> Steps { get; } = [];
        public DateTime? LastDeletedBefore { get; private set; }
        public List<string>? LastRoles { get; private set; }

        public Task<List<Guid>> GetDueUserIdsAsync(DateTime deletedBefore, IReadOnlyCollection<string> roles, CancellationToken ct = default)
        {
            LastDeletedBefore = deletedBefore;
            LastRoles = roles.ToList();
            return Task.FromResult(DueIds.ToList());
        }

        public Task<bool> LockIfStillDueAsync(Guid userId, DateTime deletedBefore, IReadOnlyCollection<string> roles, CancellationToken ct = default)
        {
            Steps.Add($"lock:{userId}");
            return Task.FromResult(!Restored.Contains(userId));
        }

        public Task<int> ClearPaymentPayloadsAsync(Guid userId, CancellationToken ct = default)
        {
            Steps.Add($"payments:{userId}");
            return Task.FromResult(1);
        }

        public Task<ForumPurgeResult> PurgeForumPostsAsync(Guid userId, DateTime now, CancellationToken ct = default)
        {
            Steps.Add($"forum:{userId}");
            return Task.FromResult(new ForumPurgeResult(1, 1));
        }

        public Task DeleteUserAsync(Guid userId, CancellationToken ct = default)
        {
            if (FailOnDelete.Contains(userId)) throw new InvalidOperationException("db lỗi");
            Steps.Add($"delete:{userId}");
            return Task.CompletedTask;
        }
    }

    /// <summary>Ghi lại thời điểm xóa tệp so với các bước DB (dùng chung danh sách Steps).</summary>
    private sealed class FakeFileStorage(List<string> steps) : IProfileFileStorageService
    {
        public HashSet<Guid> FailFor { get; } = [];

        public Task DeleteAllUserFilesAsync(Guid userId, CancellationToken ct = default)
        {
            if (FailFor.Contains(userId)) throw new IOException("tệp đang bị khóa");
            steps.Add($"files:{userId}");
            return Task.CompletedTask;
        }

        public Task<DraftTranscriptInfoDto> SaveDraftTranscriptAsync(Guid userId, Stream fileStream, string fileName, string contentType, long length, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<string?> CommitDraftTranscriptAsync(Guid userId, string draftRelativePath, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteDraftFileAsync(Guid userId, string? draftRelativePath, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteTranscriptFileAsync(Guid userId, string? transcriptRelativePath, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteDraftFilesAsync(Guid userId, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private readonly FakePurgeRepository _repo = new();
    private readonly FakeFileStorage _files;
    private readonly FakeAuditLogRepository _audit = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly AccountDeletionOptions _options = new() { RetentionPeriod = TimeSpan.FromDays(30) };
    private static readonly DateTime Now = new(2026, 11, 17, 8, 0, 0, DateTimeKind.Utc);

    public AccountPurgeServiceTests() => _files = new FakeFileStorage(_repo.Steps);

    private AccountPurgeService CreateService() => new(_repo, _audit, _unitOfWork, _files, _options);

    [Fact]
    public async Task Purge_UsesRetentionCutoffAndOnlyStudentParentRoles()
    {
        await CreateService().PurgeDueAccountsAsync(Now);

        Assert.Equal(Now.AddDays(-30), _repo.LastDeletedBefore);
        Assert.NotNull(_repo.LastRoles);
        Assert.Equal(new[] { UserRoles.Parent, UserRoles.Student }, _repo.LastRoles!.OrderBy(r => r));
    }

    [Fact]
    public async Task Purge_DueUser_RunsStepsInOrderInOneTransactionAndAuditsIdOnly()
    {
        var id = Guid.NewGuid();
        _repo.DueIds.Add(id);

        var summary = await CreateService().PurgeDueAccountsAsync(Now);

        Assert.Equal(1, summary.Purged);
        Assert.Equal(0, summary.Skipped);
        Assert.Empty(summary.Failures);
        Assert.Equal(1, _unitOfWork.Calls);
        // Tệp xóa sau cùng, ngoài transaction
        Assert.Equal(new[] { $"lock:{id}", $"payments:{id}", $"forum:{id}", $"delete:{id}", $"files:{id}" }, _repo.Steps);
        Assert.Empty(summary.FileCleanupFailures);

        var entry = Assert.Single(_audit.Entries);
        Assert.Equal(AccountPurgeService.AuditAction, entry.Action);
        Assert.Null(entry.ActorUserId);
        Assert.Equal("user", entry.EntityType);
        Assert.Equal(id.ToString(), entry.EntityId);
        Assert.Null(entry.DetailJson);
    }

    [Fact]
    public async Task Purge_UserRestoredMeanwhile_SkipsWithoutTouchingData()
    {
        var id = Guid.NewGuid();
        _repo.DueIds.Add(id);
        _repo.Restored.Add(id);

        var summary = await CreateService().PurgeDueAccountsAsync(Now);

        Assert.Equal(0, summary.Purged);
        Assert.Equal(1, summary.Skipped);
        Assert.Equal(new[] { $"lock:{id}" }, _repo.Steps);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public async Task Purge_OneUserFails_OthersStillPurgedAndFailureReported()
    {
        var bad = Guid.NewGuid();
        var good = Guid.NewGuid();
        _repo.DueIds.AddRange([bad, good]);
        _repo.FailOnDelete.Add(bad);

        var summary = await CreateService().PurgeDueAccountsAsync(Now);

        Assert.Equal(1, summary.Purged);
        var failure = Assert.Single(summary.Failures);
        Assert.Equal(bad, failure.UserId);
        Assert.IsType<InvalidOperationException>(failure.Error);
        Assert.Contains($"delete:{good}", _repo.Steps);
        var entry = Assert.Single(_audit.Entries);
        Assert.Equal(good.ToString(), entry.EntityId);
    }

    [Fact]
    public async Task Purge_FailedUser_FilesNotDeleted()
    {
        var id = Guid.NewGuid();
        _repo.DueIds.Add(id);
        _repo.FailOnDelete.Add(id);

        await CreateService().PurgeDueAccountsAsync(Now);

        // DB hoàn tác nên tài khoản còn nguyên: tệp cũng phải còn
        Assert.DoesNotContain($"files:{id}", _repo.Steps);
    }

    [Fact]
    public async Task Purge_FileDeleteFails_StillCountedPurgedAndReportedSeparately()
    {
        var bad = Guid.NewGuid();
        var good = Guid.NewGuid();
        _repo.DueIds.AddRange([bad, good]);
        _files.FailFor.Add(bad);

        var summary = await CreateService().PurgeDueAccountsAsync(Now);

        Assert.Equal(2, summary.Purged);
        Assert.Empty(summary.Failures);
        var fileFailure = Assert.Single(summary.FileCleanupFailures);
        Assert.Equal(bad, fileFailure.UserId);
        Assert.IsType<IOException>(fileFailure.Error);
        Assert.Contains($"files:{good}", _repo.Steps);
        Assert.Equal(2, _audit.Entries.Count);
    }

    [Fact]
    public async Task Purge_NoDueUsers_DoesNothing()
    {
        var summary = await CreateService().PurgeDueAccountsAsync(Now);

        Assert.Equal(0, summary.Purged);
        Assert.Equal(0, _unitOfWork.Calls);
        Assert.Empty(_audit.Entries);
    }

    [Fact]
    public async Task Purge_Cancelled_Throws()
    {
        _repo.DueIds.Add(Guid.NewGuid());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateService().PurgeDueAccountsAsync(Now, cts.Token));
    }
}
