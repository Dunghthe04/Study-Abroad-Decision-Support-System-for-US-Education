using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StudyAbroad.Application.AcademicProfiles;

public class AcademicProfileService(
    IAcademicProfileRepository repository) : IAcademicProfileService
{
    private static readonly Dictionary<string, decimal> LetterGradeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "A+", 4.0m },
        { "A", 4.0m },
        { "A-", 3.7m },
        { "B+", 3.3m },
        { "B", 3.0m },
        { "B-", 2.7m },
        { "C+", 2.3m },
        { "C", 2.0m },
        { "C-", 1.7m },
        { "D+", 1.3m },
        { "D", 1.0m },
        { "D-", 0.7m },
        { "F", 0.0m }
    };

    public async Task<AcademicProfileResult> GetAcademicProfileAsync(Guid userId, CancellationToken ct = default)
    {
        var profile = await repository.GetByUserIdAsync(userId, ct);
        if (profile == null)
        {
            return AcademicProfileResult.Fail(AcademicProfileError.NotFound, "Chưa tìm thấy hồ sơ học thuật của người dùng.");
        }

        var scores = await repository.GetTranscriptScoresAsync(profile.Id, ct);
        var terms = scores
            .GroupBy(s => new { s.TermOrder, s.TermName })
            .OrderBy(g => g.Key.TermOrder)
            .Select(g => new TranscriptTermDto(
                g.Key.TermName,
                g.Key.TermOrder,
                g.Select(s => new TranscriptScoreItemDto(s.Subject, s.RawScore, s.Score, s.Credits)).ToList()
            ))
            .ToList();

        var response = MapToResponse(profile, terms);
        return AcademicProfileResult.Ok(response);
    }

    public async Task<AcademicProfileResult> SaveAcademicProfileAsync(Guid userId, SaveAcademicProfileRequest request, CancellationToken ct = default)
    {
        // 1. Validate thông tin học vấn bắt buộc & độ dài chuỗi
        if (string.IsNullOrWhiteSpace(request.TargetLevel) || !StudyLevels.IsValid(request.TargetLevel.Trim()))
        {
            return AcademicProfileResult.Fail(AcademicProfileError.Validation, "Bậc học mục tiêu không hợp lệ. Vui lòng chọn từ danh sách hỗ trợ.");
        }

        if (string.IsNullOrWhiteSpace(request.GradeScale) || !GradeScales.IsValid(request.GradeScale.Trim()))
        {
            return AcademicProfileResult.Fail(AcademicProfileError.Validation, "Thang điểm không hợp lệ (hỗ trợ Thang 10, Thang 100, Thang 4, Thang chữ).");
        }

        if (!string.IsNullOrWhiteSpace(request.EducationSystem) && !EducationSystems.IsValid(request.EducationSystem.Trim()))
        {
            return AcademicProfileResult.Fail(AcademicProfileError.Validation, "Hệ chương trình học tập không hợp lệ.");
        }

        if (request.GraduationYear.HasValue && (request.GraduationYear.Value < 1990 || request.GraduationYear.Value > 2040))
        {
            return AcademicProfileResult.Fail(AcademicProfileError.Validation, "Năm tốt nghiệp không hợp lệ (phải từ năm 1990 đến 2040).");
        }

        if (request.CurrentSchool?.Length > 256)
        {
            return AcademicProfileResult.Fail(AcademicProfileError.Validation, "Tên trường hiện tại không được vượt quá 256 ký tự.");
        }

        if (request.CurrentGrade?.Length > 64)
        {
            return AcademicProfileResult.Fail(AcademicProfileError.Validation, "Bậc/lớp hiện tại không được vượt quá 64 ký tự.");
        }

        if (request.IntendedMajor?.Length > 128)
        {
            return AcademicProfileResult.Fail(AcademicProfileError.Validation, "Ngành học mong muốn không được vượt quá 128 ký tự.");
        }

        // 2. Validate chứng chỉ (nếu có thi, cho phép null nếu chưa thi)
        if (request.Ielts.HasValue)
        {
            if (request.Ielts.Value < 0.0m || request.Ielts.Value > 9.0m || (request.Ielts.Value * 10) % 5 != 0)
                return AcademicProfileResult.Fail(AcademicProfileError.Validation, "Điểm IELTS không hợp lệ (từ 0.0 đến 9.0, bước nhảy 0.5).");
        }

        if (request.Toefl.HasValue)
        {
            if (request.Toefl.Value < 0m || request.Toefl.Value > 120m)
                return AcademicProfileResult.Fail(AcademicProfileError.Validation, "Điểm TOEFL iBT không hợp lệ (từ 0 đến 120).");
        }

        if (request.Duolingo.HasValue)
        {
            if (request.Duolingo.Value < 10m || request.Duolingo.Value > 160m || request.Duolingo.Value % 5 != 0)
                return AcademicProfileResult.Fail(AcademicProfileError.Validation, "Điểm Duolingo English Test không hợp lệ (từ 10 đến 160, bước nhảy 5).");
        }

        if (request.Sat.HasValue)
        {
            if (request.Sat.Value < 400m || request.Sat.Value > 1600m)
                return AcademicProfileResult.Fail(AcademicProfileError.Validation, "Điểm SAT không hợp lệ (từ 400 đến 1600).");
        }

        if (request.Act.HasValue)
        {
            if (request.Act.Value < 1m || request.Act.Value > 36m)
                return AcademicProfileResult.Fail(AcademicProfileError.Validation, "Điểm ACT không hợp lệ (từ 1 đến 36).");
        }

        // 3. Validate Bảng điểm: Điểm theo thang, môn trùng lặp trong từng kỳ
        var cleanScale = request.GradeScale.Trim();
        var processedScores = new List<TranscriptScore>();
        var totalScoreSum = 0.0m;
        var totalCredits = 0.0m;
        var hasScores = false;
        var hasCredits = false;

        if (request.Terms != null && request.Terms.Count > 0)
        {
            foreach (var term in request.Terms)
            {
                if (string.IsNullOrWhiteSpace(term.TermName))
                {
                    return AcademicProfileResult.Fail(AcademicProfileError.Validation, "Tên học kỳ không được để trống.");
                }

                if (term.TermOrder < 1)
                {
                    return AcademicProfileResult.Fail(AcademicProfileError.Validation, "Thứ tự học kỳ phải lớn hơn hoặc bằng 1.");
                }

                if (term.Scores == null || term.Scores.Count == 0) continue;

                // Kiểm tra môn trùng lặp trong cùng 1 học kỳ
                var subjectSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var item in term.Scores)
                {
                    if (string.IsNullOrWhiteSpace(item.Subject))
                    {
                        return AcademicProfileResult.Fail(AcademicProfileError.Validation, $"Học kỳ '{term.TermName}' có môn học bị để trống tên.");
                    }

                    var cleanSubject = item.Subject.Trim();
                    if (cleanSubject.Length > 128)
                    {
                        return AcademicProfileResult.Fail(AcademicProfileError.Validation, $"Tên môn học '{cleanSubject}' vượt quá 128 ký tự.");
                    }

                    if (!subjectSet.Add(cleanSubject))
                    {
                        return AcademicProfileResult.Fail(AcademicProfileError.Validation, $"Học kỳ '{term.TermName}' có môn '{cleanSubject}' bị nhập trùng lặp.");
                    }

                    decimal normalizedScore;
                    string? rawScore = item.RawScore?.Trim();

                    // Kiểm tra điểm theo thang điểm đã chọn
                    if (cleanScale == GradeScales.Scale10)
                    {
                        if (item.Score < 0.0m || item.Score > 10.0m)
                        {
                            return AcademicProfileResult.Fail(AcademicProfileError.Validation,
                                $"Điểm môn '{cleanSubject}' ({item.Score}) không hợp lệ với Thang 10 (phải từ 0.0 đến 10.0).");
                        }
                        normalizedScore = item.Score;
                        rawScore ??= item.Score.ToString("0.##");
                    }
                    else if (cleanScale == GradeScales.Scale100)
                    {
                        if (item.Score < 0.0m || item.Score > 100.0m)
                        {
                            return AcademicProfileResult.Fail(AcademicProfileError.Validation,
                                $"Điểm môn '{cleanSubject}' ({item.Score}) không hợp lệ với Thang 100 (phải từ 0 đến 100).");
                        }
                        normalizedScore = item.Score;
                        rawScore ??= item.Score.ToString("0.##");
                    }
                    else if (cleanScale == GradeScales.Scale4)
                    {
                        if (item.Score < 0.0m || item.Score > 4.0m)
                        {
                            return AcademicProfileResult.Fail(AcademicProfileError.Validation,
                                $"Điểm môn '{cleanSubject}' ({item.Score}) không hợp lệ với Thang 4 (phải từ 0.0 đến 4.0).");
                        }
                        normalizedScore = item.Score;
                        rawScore ??= item.Score.ToString("0.##");
                    }
                    else if (cleanScale == GradeScales.ScaleLetter)
                    {
                        var key = rawScore ?? item.Score.ToString();
                        if (!LetterGradeMap.TryGetValue(key, out var mappedValue))
                        {
                            return AcademicProfileResult.Fail(AcademicProfileError.Validation,
                                $"Điểm chữ môn '{cleanSubject}' ('{key}') không hợp lệ (chỉ hỗ trợ: A+, A, A-, B+, B, B-, C+, C, C-, D+, D, D-, F).");
                        }
                        normalizedScore = mappedValue;
                        rawScore = key.ToUpperInvariant();
                    }
                    else
                    {
                        normalizedScore = item.Score;
                    }

                    if (item.Credits.HasValue && item.Credits.Value <= 0)
                    {
                        return AcademicProfileResult.Fail(AcademicProfileError.Validation, $"Số tín chỉ môn '{cleanSubject}' phải lớn hơn 0.");
                    }

                    processedScores.Add(new TranscriptScore
                    {
                        TermName = term.TermName.Trim(),
                        TermOrder = term.TermOrder,
                        Subject = cleanSubject,
                        Score = normalizedScore,
                        RawScore = rawScore,
                        Credits = item.Credits
                    });

                    // Tính toán GPA
                    if (item.Credits.HasValue && item.Credits.Value > 0)
                    {
                        hasCredits = true;
                        totalScoreSum += normalizedScore * item.Credits.Value;
                        totalCredits += item.Credits.Value;
                    }
                    else
                    {
                        totalScoreSum += normalizedScore;
                    }
                    hasScores = true;
                }
            }
        }

        // Tính GPA trung bình tích lũy
        decimal? calculatedGpa = null;
        if (hasScores)
        {
            if (hasCredits && totalCredits > 0)
            {
                calculatedGpa = Math.Round(totalScoreSum / totalCredits, 2);
            }
            else if (processedScores.Count > 0)
            {
                calculatedGpa = Math.Round(totalScoreSum / processedScores.Count, 2);
            }
        }

        // 4. Lưu hồ sơ học thuật vào CSDL
        var profile = await repository.GetByUserIdAsync(userId, ct);
        var isNew = profile == null;

        if (isNew)
        {
            profile = new StudentProfile
            {
                UserId = userId,
                TargetLevel = request.TargetLevel.Trim(),
                CurrentSchool = request.CurrentSchool?.Trim(),
                EducationSystem = request.EducationSystem?.Trim(),
                GraduationYear = request.GraduationYear,
                CurrentGrade = request.CurrentGrade?.Trim(),
                GradeScale = cleanScale,
                OverallGpa = calculatedGpa,
                IntendedMajor = request.IntendedMajor?.Trim(),
                Ielts = request.Ielts,
                Toefl = request.Toefl,
                Duolingo = request.Duolingo,
                Sat = request.Sat,
                Act = request.Act
            };
            await repository.AddProfileAsync(profile, ct);
        }
        else
        {
            profile!.TargetLevel = request.TargetLevel.Trim();
            profile.CurrentSchool = request.CurrentSchool?.Trim();
            profile.EducationSystem = request.EducationSystem?.Trim();
            profile.GraduationYear = request.GraduationYear;
            profile.CurrentGrade = request.CurrentGrade?.Trim();
            profile.GradeScale = cleanScale;
            profile.OverallGpa = calculatedGpa;
            profile.IntendedMajor = request.IntendedMajor?.Trim();
            profile.Ielts = request.Ielts;
            profile.Toefl = request.Toefl;
            profile.Duolingo = request.Duolingo;
            profile.Sat = request.Sat;
            profile.Act = request.Act;
            await repository.UpdateProfileAsync(profile, ct);
        }

        // Gán foreign key cho các điểm và thay thế bảng điểm cũ
        foreach (var score in processedScores)
        {
            score.StudentProfileId = profile.Id;
        }

        await repository.ReplaceTranscriptScoresAsync(profile.Id, processedScores, ct);
        await repository.SaveChangesAsync(ct);

        // 5. Chuẩn bị kết quả trả về
        var termsResult = processedScores
            .GroupBy(s => new { s.TermOrder, s.TermName })
            .OrderBy(g => g.Key.TermOrder)
            .Select(g => new TranscriptTermDto(
                g.Key.TermName,
                g.Key.TermOrder,
                g.Select(s => new TranscriptScoreItemDto(s.Subject, s.RawScore, s.Score, s.Credits)).ToList()
            ))
            .ToList();

        var response = MapToResponse(profile, termsResult);
        return AcademicProfileResult.Ok(response, "Lưu hồ sơ học thuật và bảng điểm thành công.");
    }

    private static AcademicProfileResponse MapToResponse(StudentProfile p, List<TranscriptTermDto> terms)
    {
        return new AcademicProfileResponse(
            p.Id,
            p.UserId,
            p.TargetLevel,
            p.CurrentSchool,
            p.EducationSystem,
            p.GraduationYear,
            p.CurrentGrade,
            p.GradeScale,
            p.OverallGpa,
            p.IntendedMajor,
            p.Ielts,
            p.Toefl,
            p.Duolingo,
            p.Sat,
            p.Act,
            terms,
            p.CreatedAt,
            p.UpdatedAt
        );
    }
}
