using StudyAbroad.Domain.Common;

namespace StudyAbroad.Domain.Entities;

/// <summary>Kịch bản mô phỏng Nếu - Thì What-If trên sandbox (UC-82, UC-85, UC-86 - Q3, Q7).</summary>
public class WhatIfSimulation : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid? StudentProfileId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? SimulatedProfileJson { get; set; }
    public string? SimulatedRecommendationsJson { get; set; }
    public string? Notes { get; set; }
}
