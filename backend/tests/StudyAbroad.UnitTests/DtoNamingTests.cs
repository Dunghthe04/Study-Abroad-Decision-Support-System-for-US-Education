using StudyAbroad.Application.Recommendations;

namespace StudyAbroad.UnitTests;

public class DtoNamingTests
{
    [Fact]
    public void DtoNames_AreUniqueAcrossNamespaces()
    {
        // Swagger đặt tên schema theo tên class: hai DTO trùng tên ở hai namespace làm swagger.json lỗi 500
        var duplicates = typeof(RecommendationResultDto).Assembly.GetTypes()
            .Where(t => t.IsPublic && t.Name.EndsWith("Dto", StringComparison.Ordinal))
            .GroupBy(t => t.Name)
            .Where(g => g.Count() > 1)
            .Select(g => string.Join(", ", g.Select(t => t.FullName)))
            .ToList();

        Assert.Empty(duplicates);
    }
}
