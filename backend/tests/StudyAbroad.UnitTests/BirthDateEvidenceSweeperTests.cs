using StudyAbroad.Application.BirthDateChanges;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using static StudyAbroad.UnitTests.BirthDateChangeServiceTests;

namespace StudyAbroad.UnitTests;

/// <summary>[UC-107 / UC-150] Job dọn minh chứng ngày sinh: chỉ giữ tệp còn thuộc yêu cầu Chờ duyệt.</summary>
public class BirthDateEvidenceSweeperTests
{
    private static readonly DateTime Now = new(2026, 11, 28, 2, 0, 0, DateTimeKind.Utc);
    private readonly FakeStorage _storage = new();
    private readonly FakeRepo _repo = new();

    private string AddFile(string name, DateTime uploadedAt)
    {
        _storage.Files[name] = [1];
        _storage.UploadedAt[name] = uploadedAt;
        return name;
    }

    private BirthDateEvidenceSweeper Create() => new(_storage, _repo);

    [Fact]
    public async Task Sweep_DeletesOrphans_KeepsPendingEvidence()
    {
        var pending = AddFile("pending", Now.AddDays(-3));
        var orphan = AddFile("orphan", Now.AddDays(-2));
        _repo.Items.Add(new BirthDateChangeRequest { Id = Guid.NewGuid(), Status = BirthDateChangeStatuses.Pending, EvidencePath = pending });
        // Yêu cầu đã duyệt: đường dẫn đã về null, tệp còn sót là mồ côi
        _repo.Items.Add(new BirthDateChangeRequest { Id = Guid.NewGuid(), Status = BirthDateChangeStatuses.Approved, EvidencePath = null });

        var result = await Create().SweepAsync(Now);

        Assert.Equal(new BirthDateEvidenceSweepResult(2, 1), result);
        Assert.Equal([orphan], _storage.Deleted);
        Assert.True(_storage.Files.ContainsKey(pending));
    }

    [Fact]
    public async Task Sweep_FileUploadedWithinLastHour_NotTouched()
    {
        // Yêu cầu đang gửi dở: tệp đã lên Cloudinary nhưng chưa ghi DB
        AddFile("in-flight", Now.AddMinutes(-20));

        var result = await Create().SweepAsync(Now);

        Assert.Equal(new BirthDateEvidenceSweepResult(0, 0), result);
        Assert.Empty(_storage.Deleted);
    }

    [Fact]
    public async Task Sweep_ListFails_DeletesNothing()
    {
        var storage = new ThrowingListStorage();

        await Assert.ThrowsAsync<InvalidOperationException>(() => new BirthDateEvidenceSweeper(storage, _repo).SweepAsync(Now));
        Assert.Equal(0, storage.Deletes);
    }

    private sealed class ThrowingListStorage : IBirthDateEvidenceStorage
    {
        public int Deletes { get; private set; }
        public Task<string> SaveAsync(Guid requestId, byte[] content, string extension, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<Stream?> OpenAsync(string path, CancellationToken ct = default) => throw new NotSupportedException();
        public Task DeleteAsync(string? path, CancellationToken ct = default) { Deletes++; return Task.CompletedTask; }
        public Task<IReadOnlyList<string>> ListAsync(DateTime uploadedBefore, CancellationToken ct = default) =>
            throw new InvalidOperationException("Cloudinary lỗi");
    }
}
