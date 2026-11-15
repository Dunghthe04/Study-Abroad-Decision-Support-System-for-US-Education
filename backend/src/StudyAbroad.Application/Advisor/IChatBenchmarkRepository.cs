using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace StudyAbroad.Application.Advisor
{
    /// <summary>Đọc 4 cột tính mốc (ChatBenchmarks) của các trường đang hoạt động ở một bậc học</summary>
    public interface IChatBenchmarkRepository
    {
        Task<IReadOnlyList<BenchmarkRow>> ListAsync(string studyLevel, CancellationToken ct = default);
    }
}
