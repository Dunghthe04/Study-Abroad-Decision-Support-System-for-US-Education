using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Application.Recommendations;

/// <summary>Kịch bản what-if học sinh đã lưu (bảng what_if_simulations, UC-86).</summary>
public interface IScenarioRepository
{
    /// <summary>Số kịch bản của một học sinh, để chặn khi vượt giới hạn.</summary>
    Task<int> CountByUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>Mọi kịch bản của một học sinh, mới nhất trước.</summary>
    Task<List<WhatIfSimulation>> ListByUserAsync(Guid userId, CancellationToken ct = default);

    Task<WhatIfSimulation?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task AddAsync(WhatIfSimulation row, CancellationToken ct = default);

    Task UpdateAsync(WhatIfSimulation row, CancellationToken ct = default);

    Task DeleteAsync(WhatIfSimulation row, CancellationToken ct = default);
}
