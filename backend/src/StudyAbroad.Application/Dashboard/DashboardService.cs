using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using StudyAbroad.Application.AcademicProfiles;
using StudyAbroad.Application.Auth;
using StudyAbroad.Application.ParentLinks;
using StudyAbroad.Application.Profile.Flow;
using StudyAbroad.Application.Recommendations;
using StudyAbroad.Application.TargetSchools;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Dashboard;

/// <summary>
/// Cài đặt xử lý Dashboard KPI theo UC-94.
/// Chỉ đọc các số liệu sẵn có từ Recommendations, Flow State, Target Schools và University Offerings.
/// </summary>
public class DashboardService(
    RecommendationHistoryService historyService,
    IProfileFlowService flowService,
    ITargetSchoolService targetSchoolService,
    IDashboardRepository dashboardRepository,
    IParentLinkRepository linkRepository,
    IAcademicProfileRepository profileRepository,
    IUserRepository userRepository) : IDashboardService
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    public async Task<StudentDashboardDto> GetStudentDashboardAsync(Guid studentId, CancellationToken ct = default)
    {
        // 1. KPI Gợi ý mới nhất
        var recommendationKpi = await ComputeRecommendationKpiAsync(studentId, ct);

        // 2. KPI Tiến độ hoàn thiện hồ sơ & 4. Mức đầy đủ dữ liệu
        var flowState = await flowService.GetFlowStateAsync(studentId, ct);
        var committedDetails = await flowService.GetCommittedDetailsAsync(studentId, ct);
        var profile = await profileRepository.GetByUserIdAsync(studentId, ct);

        var profileProgressKpi = ComputeProfileProgressKpi(flowState);
        var completenessKpi = ComputeDataCompletenessKpi(flowState, committedDetails, profile);

        // 3. KPI Hạn nộp sắp tới
        var deadlinesKpi = await ComputeUpcomingDeadlinesKpiAsync(studentId, ct);

        return new StudentDashboardDto(
            recommendationKpi,
            profileProgressKpi,
            deadlinesKpi,
            completenessKpi
        );
    }

    public async Task<ParentChildDashboardDto?> GetParentChildDashboardAsync(Guid parentId, Guid studentId, CancellationToken ct = default)
    {
        // Kiểm tra quyền: phụ huynh phải có liên kết đang hiệu lực với con này
        var activeLinks = await linkRepository.GetActiveByParentAsync(parentId, ct);
        var link = activeLinks.FirstOrDefault(l => l.StudentUserId == studentId);
        if (link is null || link.Status != ParentLinkStatuses.Active)
            return null; // Không có quyền hoặc liên kết không hiệu lực -> 404

        // 1. Lấy thông tin con
        var childUser = await userRepository.GetUserById(studentId, ct);
        var studentName = childUser?.FullName ?? "Học sinh";
        var isManaged = childUser is not null && AgeAccess.IsOperatedByParent(childUser);

        // 2. Lấy các KPI học sinh của con
        var studentKpis = await GetStudentDashboardAsync(studentId, ct);

        // 3. Tính toán KPI tài chính theo trường mục tiêu của con
        var financialKpis = await ComputeParentFinancialKpiAsync(link, studentId, isManaged, ct);

        return new ParentChildDashboardDto(
            studentId,
            studentName,
            isManaged,
            studentKpis,
            financialKpis
        );
    }

    // ========================================================================
    // LOGIC CHI TIẾT CÁC CHỈ SỐ HỌC SINH
    // ========================================================================

    private async Task<RecommendationKpiDto> ComputeRecommendationKpiAsync(Guid studentId, CancellationToken ct)
    {
        var runs = await historyService.ListAsync(studentId, null, ct);
        var latest = runs?.FirstOrDefault();

        if (latest is not null)
        {
            var total = latest.Counts.Reach + latest.Counts.Match + latest.Counts.Safety + latest.Counts.Aid;
            var explanation = $"Đếm số trường trong từng nhóm của lần gợi ý lúc {latest.CreatedAt:HH:mm dd/MM/yyyy}.";

            return new RecommendationKpiDto(
                latest.Counts.Reach,
                latest.Counts.Match,
                latest.Counts.Safety,
                latest.Counts.Aid,
                total,
                latest.CreatedAt,
                explanation,
                HasRun: true
            );
        }

        return new RecommendationKpiDto(
            Reach: 0,
            Match: 0,
            Safety: 0,
            Aid: 0,
            Total: 0,
            RunAt: null,
            Explanation: "Chưa chạy gợi ý trường nào. Hãy thực hiện gợi ý để nhận danh sách trường phù hợp.",
            HasRun: false
        );
    }

    private static ProfileProgressKpiDto ComputeProfileProgressKpi(ProfileFlowStateDto flowState)
    {
        var completedCount = flowState.CompletedSteps?
            .Where(s => s >= 1 && s <= 5)
            .Distinct()
            .Count() ?? 0;

        var hasCommitted = flowState.HasCommittedProfile;
        var explanation = $"{completedCount}/5 bước đã xong. Hồ sơ tính là đã gửi khi bấm Lưu ở bước 6.";

        return new ProfileProgressKpiDto(
            completedCount,
            TotalSteps: 5,
            hasCommitted,
            explanation
        );
    }

    private async Task<UpcomingDeadlinesKpiDto> ComputeUpcomingDeadlinesKpiAsync(Guid studentId, CancellationToken ct)
    {
        var targetSummary = await targetSchoolService.GetTargetSchoolsAsync(studentId, ct: ct);
        var today = DateTime.UtcNow.Date;

        var upcomingList = targetSummary.Items
            .Where(t => t.Deadline.HasValue && t.Deadline.Value.Date >= today)
            .OrderBy(t => t.Deadline!.Value)
            .Take(5)
            .Select(t => new UpcomingDeadlineItemDto(
                t.Id,
                t.UniversityId,
                t.UniversityCode,
                t.UniversityName,
                t.Category,
                t.ApplicationRound,
                t.Deadline,
                (int)(t.Deadline!.Value.Date - today).TotalDays
            ))
            .ToList();

        var undecidedRoundCount = targetSummary.Items.Count(t => !t.Deadline.HasValue);
        var explanation = "Các trường mục tiêu có hạn từ hôm nay trở đi, sắp theo ngày gần nhất (tối đa 5 trường).";

        return new UpcomingDeadlinesKpiDto(
            upcomingList,
            undecidedRoundCount,
            targetSummary.TotalCount,
            explanation
        );
    }

    private static DataCompletenessKpiDto ComputeDataCompletenessKpi(
        ProfileFlowStateDto flowState,
        ProfileFlowDraftDto? committed,
        StudentProfile? profile)
    {
        var draft = committed ?? flowState.Draft;

        // 1. GPA (Bảng điểm hoặc GPA tính toán)
        var gpaFilled = (draft?.Step2?.CalculatedGpa4Scale > 0)
                     || (draft?.Step2?.ManualGpa10Scale > 0)
                     || (draft?.Step2?.AcademicYears?.Any(y => y.Subjects?.Any(s => s.Semester1Score > 0 || s.Semester2Score > 0) == true) == true)
                     || (profile?.OverallGpa > 0 || profile?.OverallGpa10 > 0);

        // 2. Chứng chỉ tiếng Anh (IELTS, TOEFL, Duolingo, PTE...)
        var englishFilled = (draft?.Step3?.Certifications?.Any(c => (string.Equals(c.Category, "english", StringComparison.OrdinalIgnoreCase) || IsEnglishType(c.Type)) && c.Overall > 0) == true)
                         || (draft?.Step3?.EnglishOverallScore > 0)
                         || (profile?.Ielts > 0 || profile?.Toefl > 0 || profile?.Duolingo > 0);

        // 3. Bài thi chuẩn hóa (SAT, ACT)
        var testFilled = (draft?.Step3?.Certifications?.Any(c => (string.Equals(c.Category, "standardized", StringComparison.OrdinalIgnoreCase) || IsStandardizedType(c.Type)) && c.Overall > 0) == true)
                      || (draft?.Step3?.OtherTests?.Any(t => (string.Equals(t.TestType, "SAT", StringComparison.OrdinalIgnoreCase) || string.Equals(t.TestType, "ACT", StringComparison.OrdinalIgnoreCase)) && t.Score > 0) == true)
                      || (profile?.Sat > 0 || profile?.Act > 0);

        // 4. Ngân sách du học (Mức ngân sách / Tier)
        var budgetFilled = !string.IsNullOrWhiteSpace(draft?.Step4?.AnnualBudgetTier)
                        || !string.IsNullOrWhiteSpace(profile?.BudgetTier)
                        || (profile?.BudgetMaxUsd > 0);

        // 5. Ngành học mục tiêu (Ngành cụ thể hoặc đã chọn Chưa rõ ngành)
        var majorFilled = !string.IsNullOrWhiteSpace(draft?.Step1?.TargetMajor)
                       || (draft?.Step1?.MajorUndecided == true)
                       || !string.IsNullOrWhiteSpace(profile?.IntendedMajor);

        var items = new List<DataCompletenessItemDto>
        {
            new("gpa", "Điểm trung bình GPA", gpaFilled),
            new("english", "Chứng chỉ tiếng Anh (IELTS/TOEFL/Duolingo)", englishFilled),
            new("standardized_test", "Bài thi chuẩn hóa (SAT/ACT)", testFilled),
            new("budget", "Mức ngân sách hàng năm", budgetFilled),
            new("major", "Ngành học mục tiêu", majorFilled),
        };

        var filledCount = items.Count(i => i.IsFilled);
        var missingLabels = items.Where(i => !i.IsFilled).Select(i => i.Label).ToList();

        var explanation = filledCount == 5
            ? "Hồ sơ đã đầy đủ 5/5 thông tin quan trọng nhất để chạy thuật toán gợi ý trường."
            : $"Hồ sơ đã có {filledCount}/5 mục thông tin cho gợi ý trường. Còn thiếu: {string.Join(", ", missingLabels)}.";

        return new DataCompletenessKpiDto(
            filledCount,
            TotalCount: 5,
            items,
            missingLabels,
            explanation
        );
    }

    private static bool IsEnglishType(string? type) =>
        type is not null && (type.Contains("ielts", StringComparison.OrdinalIgnoreCase)
                          || type.Contains("toefl", StringComparison.OrdinalIgnoreCase)
                          || type.Contains("duolingo", StringComparison.OrdinalIgnoreCase)
                          || type.Contains("pte", StringComparison.OrdinalIgnoreCase));

    private static bool IsStandardizedType(string? type) =>
        type is not null && (type.Contains("sat", StringComparison.OrdinalIgnoreCase)
                          || type.Contains("act", StringComparison.OrdinalIgnoreCase));

    // ========================================================================
    // LOGIC CHI TIẾT CÁC CHỈ SỐ PHỤ HUYNH
    // ========================================================================

    private async Task<ParentFinancialKpiDto> ComputeParentFinancialKpiAsync(
        ParentStudentLink link,
        Guid studentId,
        bool isManaged,
        CancellationToken ct)
    {
        var profile = await profileRepository.GetByUserIdAsync(studentId, ct);

        // Q1: Xác định ngân sách
        decimal? budgetPerYear = null;
        string budgetSource = "none";
        string budgetSourceLabel = "Chưa có thông tin ngân sách";

        // Con managed: không có tab UC-76 -> luôn dùng hạng ngân sách của con
        if (isManaged)
        {
            if (profile?.BudgetMaxUsd is decimal managedBudget && managedBudget > 0)
            {
                budgetPerYear = managedBudget;
                budgetSource = "student_profile_tier";
                budgetSourceLabel = $"Theo mức trần ngân sách của con: ${budgetPerYear:N0}/năm";
            }
        }
        else
        {
            // Phụ huynh con liên kết thật: ưu tiên ngân sách phụ huynh nhập ở UC-76
            var prefBudget = ReadParentPreferenceBudget(link.ParentPreferenceJson);
            if (prefBudget.HasValue && prefBudget.Value > 0)
            {
                budgetPerYear = prefBudget.Value;
                budgetSource = "parent_preference";
                budgetSourceLabel = $"Theo ngân sách phụ huynh đặt: ${budgetPerYear:N0}/năm";
            }
            else if (profile?.BudgetMaxUsd is decimal studentBudget && studentBudget > 0)
            {
                budgetPerYear = studentBudget;
                budgetSource = "student_profile_tier";
                budgetSourceLabel = $"Theo mức trần ngân sách của con: ${budgetPerYear:N0}/năm";
            }
        }

        // Lấy danh sách trường mục tiêu của con
        var targetSummary = await targetSchoolService.GetTargetSchoolsAsync(studentId, ct: ct);
        var targetSchools = targetSummary.Items;

        var universityIds = targetSchools.Select(t => t.UniversityId).Distinct().ToList();
        var level = string.IsNullOrWhiteSpace(profile?.TargetLevel) ? "undergraduate" : profile.TargetLevel;
        var costDict = await dashboardRepository.GetUniversityCostsAsync(universityIds, level, ct);

        var shortfallList = new List<SchoolShortfallItemDto>();
        decimal totalCost4YearsAll = 0;
        int withinBudgetCount = 0;
        int withCostCount = 0;
        int missingCostCount = 0;

        foreach (var school in targetSchools)
        {
            costDict.TryGetValue(school.UniversityId, out var costRow);
            var cost1Year = costRow?.TotalCostUsd;

            if (cost1Year.HasValue && cost1Year.Value > 0)
            {
                withCostCount++;
                var cost4Years = cost1Year.Value * 4;
                totalCost4YearsAll += cost4Years;

                decimal? budget4Years = budgetPerYear.HasValue ? budgetPerYear.Value * 4 : null;
                decimal? shortfall4Years = budget4Years.HasValue ? Math.Max(0, cost4Years - budget4Years.Value) : null;

                if (budgetPerYear.HasValue && cost1Year.Value <= budgetPerYear.Value)
                {
                    withinBudgetCount++;
                }

                shortfallList.Add(new SchoolShortfallItemDto(
                    school.UniversityId,
                    school.UniversityCode,
                    school.UniversityName,
                    cost1Year.Value,
                    cost4Years,
                    budget4Years,
                    shortfall4Years,
                    HasCostData: true
                ));
            }
            else
            {
                missingCostCount++;
                shortfallList.Add(new SchoolShortfallItemDto(
                    school.UniversityId,
                    school.UniversityCode,
                    school.UniversityName,
                    null,
                    null,
                    budgetPerYear.HasValue ? budgetPerYear.Value * 4 : null,
                    null,
                    HasCostData: false
                ));
            }
        }

        // Q3: Tìm trường thiếu nhiều nhất / ít nhất
        var schoolsWithShortfall = shortfallList
            .Where(s => s.HasCostData && s.Shortfall4Years.HasValue)
            .ToList();

        var mostShortfall = schoolsWithShortfall
            .OrderByDescending(s => s.Shortfall4Years)
            .FirstOrDefault();

        var leastShortfall = schoolsWithShortfall
            .OrderBy(s => s.Shortfall4Years)
            .FirstOrDefault();

        // Dòng giải thích theo kế hoạch
        var withinBudgetExplanation = budgetPerYear.HasValue
            ? $"Trường có tổng chi phí 1 năm ≤ ngân sách/năm ({budgetSourceLabel}). Trường chưa có học phí thì ghi riêng 'chưa có số liệu'."
            : "Chưa có mức ngân sách hàng năm để so sánh số trường trong ngân sách.";

        var totalCost4YearsExplanation = "Chi phí năm học hiện tại × 4, chưa tính tăng giá, chưa trừ học bổng.";
        var shortfallExplanation = "Phần chi phí 4 năm vượt ngân sách 4 năm của từng trường mục tiêu (không cộng dồn vì con chỉ theo học 1 trường).";

        return new ParentFinancialKpiDto(
            budgetPerYear,
            budgetSource,
            budgetSourceLabel,
            withinBudgetCount,
            withCostCount,
            missingCostCount,
            totalCost4YearsAll,
            shortfallList,
            mostShortfall,
            leastShortfall,
            withinBudgetExplanation,
            totalCost4YearsExplanation,
            shortfallExplanation
        );
    }

    private static decimal? ReadParentPreferenceBudget(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var node = JsonNode.Parse(json);
            if (node is JsonObject obj && obj.TryGetPropertyValue("BudgetUsd", out var val) && val is JsonValue jv)
            {
                if (jv.TryGetValue<decimal>(out var d) && d > 0)
                    return d;
            }
        }
        catch
        {
            // Bỏ qua lỗi parse
        }
        return null;
    }
}
