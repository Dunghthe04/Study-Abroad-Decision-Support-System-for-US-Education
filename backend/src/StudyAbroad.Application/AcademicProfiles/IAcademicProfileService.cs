using System;
using System.Threading;
using System.Threading.Tasks;

namespace StudyAbroad.Application.AcademicProfiles;

public enum AcademicProfileError
{
    None,
    NotFound,
    Validation,
    Forbidden
}

public record AcademicProfileResult(
    bool Succeeded,
    AcademicProfileError Error,
    string? Message,
    AcademicProfileResponse? Data
)
{
    public static AcademicProfileResult Ok(AcademicProfileResponse data, string? message = null)
        => new(true, AcademicProfileError.None, message, data);

    public static AcademicProfileResult Fail(AcademicProfileError error, string message)
        => new(false, error, message, null);
}

public interface IAcademicProfileService
{
    Task<AcademicProfileResult> GetAcademicProfileAsync(Guid userId, CancellationToken ct = default);
    Task<AcademicProfileResult> SaveAcademicProfileAsync(Guid userId, SaveAcademicProfileRequest request, CancellationToken ct = default);
}
