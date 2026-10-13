namespace StudyAbroad.Application.Recommendations
{
    //Score = null: không có điểm, SAW bỏ tiêu chí ngoại khóa. Fresh = false: đang dùng điểm lưu lần trước
    public record ExtracurricularOutcome(
        decimal? Score,
        IReadOnlyList<ScoredActivity> Activities,
        bool AiUsed,
        bool Fresh,
        string? Warning);

    /// <summary>
    /// Bước đầu của nút "Lọc trường": tính lại điểm ngoại khóa qua advisor.
    /// Advisor lỗi thì không chặn việc lọc trường: dùng điểm cũ, chưa có điểm cũ thì bỏ tiêu chí này.
    /// </summary>
    public class ExtracurricularScoring(IExtracurricularAi ai)
    {
        public const string StaleWarning = "Chưa cập nhật được điểm ngoại khóa, tạm dùng điểm lần trước.";
        public const string MissingWarning = "Chưa tính được điểm ngoại khóa, chưa xét tiêu chí ngoại khóa.";
        public const string KeywordWarning = "AI chưa đọc được mô tả hoạt động, điểm ngoại khóa tạm tính theo vai trò học sinh khai.";
        public const string NoActivitiesWarning = "Hồ sơ chưa có hoạt động ngoại khóa hoặc kinh nghiệm nào, điểm ngoại khóa tính là 0.";

        public async Task<ExtracurricularOutcome> ScoreAsync(
            IReadOnlyList<ExtracurricularActivityInput> activities,
            decimal? storedScore,
            int timeoutSeconds,
            CancellationToken ct = default)
        {
            //Không có hoạt động nào: điểm 0, không cần gọi advisor
            if (activities.Count == 0)
                return new ExtracurricularOutcome(0m, [], AiUsed: false, Fresh: true, NoActivitiesWarning);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
            try
            {
                var response = await ai.ScoreAsync(new ExtracurricularScoreRequest(activities), timeout.Token);
                return new ExtracurricularOutcome(
                    response.Score, response.Activities, response.AiUsed, Fresh: true,
                    Warning: response.AiUsed ? null : KeywordWarning);
            }
            catch (Exception) when (!ct.IsCancellationRequested)   // người dùng tự hủy request thì không nuốt lỗi
            {
                return storedScore is { } old
                    ? new ExtracurricularOutcome(old, [], AiUsed: false, Fresh: false, StaleWarning)
                    : new ExtracurricularOutcome(null, [], AiUsed: false, Fresh: false, MissingWarning);
            }
        }
    }
}
