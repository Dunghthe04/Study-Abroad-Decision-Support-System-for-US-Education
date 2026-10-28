using System.Reflection;
using Microsoft.EntityFrameworkCore;
using StudyAbroad.Infrastructure.Persistence;
using Xunit;

namespace StudyAbroad.UnitTests;

public class DbContextSchemaTests
{
    [Fact]
    public void AppDbContext_ShouldHaveAll42EntitiesRegisteredAsDbSets()
    {
        var dbSetProperties = typeof(AppDbContext)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
            .ToList();

        var entityNames = dbSetProperties
            .Select(p => p.PropertyType.GetGenericArguments()[0].Name)
            .ToList();

        // 42 distinct business entity types
        Assert.True(entityNames.Count >= 42, $"Expected at least 42 DbSet properties, but found {entityNames.Count}");

        var expectedEntities = new[]
        {
            // Phân hệ 1: Tài khoản & Gia đình (5)
            "User", "UserSession", "OtpToken", "Consent", "ParentStudentLink",
            // Phân hệ 2: Hồ sơ & Học bạ (4)
            "StudentProfile", "TranscriptScore", "ProfileActivity", "RoadmapStep",
            // Phân hệ 3: Trường, Tuyển sinh & Chi phí (5)
            "University", "UniversityOffering", "Scholarship", "TargetSchool", "StateLivingCost",
            // Phân hệ 4: Mô hình DSS & Mô phỏng (6)
            "AppSetting", "AnalysisResult", "Recommendation", "RecommendationItem", "ExpertRule", "WhatIfSimulation",
            // Phân hệ 5: Trung tâm tư vấn (4)
            "StudyCenter", "Lead", "LeadMessage", "CenterReview",
            // Phân hệ 6: Trợ lý AI RAG (7)
            "Skill", "KnowledgeDocument", "ChatSession", "ChatMessage", "AiCall", "EvalCase", "EvalRun",
            // Phân hệ 7: Gói dịch vụ & Thanh toán (4)
            "SubscriptionPlan", "UserSubscription", "UserDailyUsage", "PaymentTransaction",
            // Phân hệ 8: Diễn đàn, Thông báo & Kiểm toán (5)
            "ForumPost", "ForumReport", "PolicyAlert", "Notification", "AuditLog",
            // Phân hệ 9: ETL & Xác minh dữ liệu (2)
            "DataImportBatch", "FieldEvidence"
        };

        foreach (var entity in expectedEntities)
        {
            Assert.Contains(entity, entityNames);
        }
    }
}

