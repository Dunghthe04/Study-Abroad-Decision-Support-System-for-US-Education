using Microsoft.Extensions.Caching.Memory;
using StudyAbroad.Application.Profile.Flow;
using StudyAbroad.Domain.Constants;
using StudyAbroad.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace StudyAbroad.UnitTests;

/// <summary>
/// [USAS-640] Unit tests kiểm tra ProfileMetadataService:
/// - Đọc và deserialize thành công siêu dữ liệu động từ app_settings
/// - Sử dụng In-Memory Cache để tránh query lặp lại
/// - Xử lý ngoại lệ khi thiếu cấu hình
/// </summary>
public class ProfileMetadataServiceTests
{
    private sealed class FakeProfileFlowRepository : IProfileFlowRepository
    {
        public string? MockSettingValue { get; set; }
        public int CallCount { get; private set; }

        public Task<string?> GetAppSettingValueAsync(string key, CancellationToken ct = default)
        {
            CallCount++;
            return Task.FromResult(MockSettingValue);
        }

        public Task<StudentProfile?> GetProfileByUserIdAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult<StudentProfile?>(null);

        public Task UpdateProfileAsync(StudentProfile profile, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<StudentProfile> EnsureProfileExistsAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(new StudentProfile { UserId = userId });
    }

    private const string ValidMetadataJson = """
    {
      "studyLevels": [
        { "id": "undergraduate", "name": "Đại học", "subName": "Bachelor", "duration": "4 năm", "description": "Cử nhân", "defaultGradeScale": "10", "defaultEducationSystem": "standard" }
      ],
      "budgetTiers": [
        { "id": "tier_1", "label": "Dưới 20.000 USD", "minUsd": 0, "maxUsd": 20000, "approxUsd": 15000, "description": "Học bổng" }
      ],
      "scholarshipNeeds": [
        { "id": "mandatory", "label": "Bắt buộc", "subLabel": "Cần học bổng", "needsScholarship": true }
      ],
      "fundingSources": [
        { "id": "family_support", "label": "Gia đình" }
      ],
      "educationSystems": [
        { "id": "standard", "label": "Hệ chuẩn" }
      ],
      "gradeScales": [
        { "id": "10", "name": "Thang 10", "desc": "0-10", "min": 0, "max": 10, "step": 0.1 }
      ],
      "gradeFormTemplates": {
        "high_school": {
          "levelType": "high_school",
          "description": "THPT",
          "defaultGradeScale": "10",
          "terms": [ { "termOrder": 1, "termName": "Lớp 10 Kỳ 1" } ],
          "defaultSubjects": [ { "subject": "Toán", "credits": 2 } ]
        }
      },
      "certifications": [
        { "id": "ielts", "name": "IELTS", "category": "english", "min": 0, "max": 9.0, "step": 0.5, "placeholder": "7.0" }
      ],
      "activityCategories": [
        { "id": "extracurricular", "label": "Ngoại khóa" }
      ],
      "impactLevels": [
        { "level": 1, "label": "Cấp Trường", "desc": "Trường" }
      ],
      "usStates": [
        { "code": "CA", "name": "California" }
      ],
      "usMajors": [
        { "id": "cs", "name": "Khoa học máy tính", "englishName": "Computer Science" }
      ],
      "institutionTypes": [
        { "id": "any", "label": "Không lọc" }
      ]
    }
    """;

    [Fact]
    public async Task GetMetadataAsync_ReturnsDeserializedMetadata_WhenSettingExists()
    {
        var repo = new FakeProfileFlowRepository { MockSettingValue = ValidMetadataJson };
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new ProfileMetadataService(repo, cache);

        var result = await service.GetMetadataAsync();

        Assert.NotNull(result);
        Assert.Single(result.StudyLevels);
        Assert.Equal("undergraduate", result.StudyLevels[0].Id);
        Assert.Single(result.BudgetTiers);
        Assert.Equal("tier_1", result.BudgetTiers[0].Id);
        Assert.Single(result.GradeScales);
        Assert.Equal("10", result.GradeScales[0].Id);
        Assert.Single(result.UsStates);
        Assert.Equal("CA", result.UsStates[0].Code);
        Assert.Equal(1, repo.CallCount);
    }

    [Fact]
    public async Task GetMetadataAsync_UsesMemoryCache_OnSubsequentCalls()
    {
        var repo = new FakeProfileFlowRepository { MockSettingValue = ValidMetadataJson };
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new ProfileMetadataService(repo, cache);

        var result1 = await service.GetMetadataAsync();
        var result2 = await service.GetMetadataAsync();

        Assert.NotNull(result1);
        Assert.NotNull(result2);
        Assert.Equal(1, repo.CallCount); // Only 1 DB query made because second was cached
    }

    [Fact]
    public async Task GetMetadataAsync_ThrowsInvalidOperationException_WhenSettingMissing()
    {
        var repo = new FakeProfileFlowRepository { MockSettingValue = null };
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new ProfileMetadataService(repo, cache);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetMetadataAsync());
    }
}
