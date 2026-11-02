using StudyAbroad.Domain.Entities;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace StudyAbroad.Application.Profile.Flow;

/// <summary>
/// [USAS-640] Repository phục vụ luồng hồ sơ đa bước và metadata động.
/// </summary>
public interface IProfileFlowRepository
{
    Task<string?> GetAppSettingValueAsync(string key, CancellationToken ct = default);
    Task<StudentProfile?> GetProfileByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task UpdateProfileAsync(StudentProfile profile, CancellationToken ct = default);
    Task<StudentProfile> EnsureProfileExistsAsync(Guid userId, CancellationToken ct = default);
    /// <summary>Ngành trong bảng majors có trường đào tạo (để chọn trên form)</summary>
    Task<IReadOnlyList<MajorOption>> GetMajorOptionsAsync(CancellationToken ct = default);
    /// <summary>51 bang trong bảng us_states (để chọn trên form, chia theo vùng)</summary>
    Task<IReadOnlyList<UsStateOption>> GetUsStateOptionsAsync(CancellationToken ct = default);
    /// <summary>
    /// Lưu chính thức hồ sơ: cập nhật profile, thay toàn bộ điểm bảng điểm, hoạt động và chứng chỉ — trong 1 transaction.
    /// </summary>
    Task CommitProfileFlowAsync(
        StudentProfile profile,
        IEnumerable<TranscriptScore> scores,
        IEnumerable<ProfileActivity> activities,
        IEnumerable<ProfileCertification> certifications,
        CancellationToken ct = default);
    Task<IReadOnlyList<TranscriptScore>> GetTranscriptScoresAsync(Guid profileId, CancellationToken ct = default);
    Task<IReadOnlyList<ProfileActivity>> GetProfileActivitiesAsync(Guid profileId, CancellationToken ct = default);
    Task<IReadOnlyList<ProfileCertification>> GetProfileCertificationsAsync(Guid profileId, CancellationToken ct = default);
}

/// <summary>Một ngành để chọn: Name (tiếng Anh) là giá trị lưu vào hồ sơ, NameVi để hiện</summary>
public record MajorOption(string Name, string? NameVi, IReadOnlyList<string> Aliases, bool IsPopular);

/// <summary>Một bang để chọn: Code là giá trị lưu vào hồ sơ, Region là vùng (northeast, midwest, south, west)</summary>
public record UsStateOption(string Code, string Name, string Region, bool IsPopular);
