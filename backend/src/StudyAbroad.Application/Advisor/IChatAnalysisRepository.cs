using System;
using System.Threading;
using System.Threading.Tasks;

namespace StudyAbroad.Application.Advisor
{
    /// <summary>Lưu câu đánh giá / lộ trình chat vừa trả vào bảng analysis_results của hồ sơ học sinh</summary>
    public interface IChatAnalysisRepository
    {
        /// <summary>kind: strengths_weaknesses | strategy. Học sinh chưa có hồ sơ thì bỏ qua</summary>
        Task SaveAsync(Guid userId, string kind, string resultJson, string modelVersion, CancellationToken ct = default);
    }
}
