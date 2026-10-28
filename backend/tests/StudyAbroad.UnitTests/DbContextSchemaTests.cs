using System.Reflection;
using Microsoft.EntityFrameworkCore;
using StudyAbroad.Domain.Entities;
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

    [Fact]
    public void FullSchemaEntities_ShouldMapTablesAndForeignKeys()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=usas_schema_check")
            .UseSnakeCaseNamingConvention()
            .Options;
        using var db = new AppDbContext(options);

        Assert.Equal("state_living_costs", TableName<StateLivingCost>(db));
        Assert.Equal("recommendation_items", TableName<RecommendationItem>(db));
        Assert.Equal("expert_rules", TableName<ExpertRule>(db));
        Assert.Equal("what_if_simulations", TableName<WhatIfSimulation>(db));
        Assert.Equal("lead_messages", TableName<LeadMessage>(db));
        Assert.Equal("center_reviews", TableName<CenterReview>(db));
        Assert.Equal("subscription_plans", TableName<SubscriptionPlan>(db));
        Assert.Equal("user_subscriptions", TableName<UserSubscription>(db));
        Assert.Equal("user_daily_usages", TableName<UserDailyUsage>(db));
        Assert.Equal("payment_transactions", TableName<PaymentTransaction>(db));
        Assert.Equal("policy_alerts", TableName<PolicyAlert>(db));
        Assert.Equal("data_import_batches", TableName<DataImportBatch>(db));
        Assert.Equal("field_evidence", TableName<FieldEvidence>(db));

        AssertForeignKey<RecommendationItem>(db, nameof(RecommendationItem.RecommendationId), "recommendations", DeleteBehavior.Cascade);
        AssertForeignKey<RecommendationItem>(db, nameof(RecommendationItem.UniversityId), "universities", DeleteBehavior.Cascade);
        AssertForeignKey<WhatIfSimulation>(db, nameof(WhatIfSimulation.UserId), "users", DeleteBehavior.Cascade);
        AssertForeignKey<WhatIfSimulation>(db, nameof(WhatIfSimulation.StudentProfileId), "student_profiles", DeleteBehavior.Cascade);
        AssertForeignKey<LeadMessage>(db, nameof(LeadMessage.LeadId), "leads", DeleteBehavior.Cascade);
        AssertForeignKey<LeadMessage>(db, nameof(LeadMessage.SenderUserId), "users", DeleteBehavior.Cascade);
        AssertForeignKey<LeadMessage>(db, nameof(LeadMessage.RemovedByUserId), "users", DeleteBehavior.SetNull);
        AssertForeignKey<CenterReview>(db, nameof(CenterReview.StudyCenterId), "study_centers", DeleteBehavior.Cascade);
        AssertForeignKey<CenterReview>(db, nameof(CenterReview.UserId), "users", DeleteBehavior.Cascade);
        AssertForeignKey<CenterReview>(db, nameof(CenterReview.LeadId), "leads", DeleteBehavior.SetNull);
        AssertForeignKey<UserSubscription>(db, nameof(UserSubscription.UserId), "users", DeleteBehavior.Cascade);
        AssertForeignKey<UserSubscription>(db, nameof(UserSubscription.SubscriptionPlanId), "subscription_plans", DeleteBehavior.Cascade);
        AssertForeignKey<UserDailyUsage>(db, nameof(UserDailyUsage.UserId), "users", DeleteBehavior.Cascade);
        AssertForeignKey<PaymentTransaction>(db, nameof(PaymentTransaction.UserId), "users", DeleteBehavior.Cascade);
        AssertForeignKey<PaymentTransaction>(db, nameof(PaymentTransaction.UserSubscriptionId), "user_subscriptions", DeleteBehavior.SetNull);
        AssertForeignKey<FieldEvidence>(db, nameof(FieldEvidence.UniversityId), "universities", DeleteBehavior.Cascade);

        Assert.Equal("jsonb", ColumnType<ExpertRule>(db, nameof(ExpertRule.ConditionJson)));
        Assert.Equal("jsonb", ColumnType<WhatIfSimulation>(db, nameof(WhatIfSimulation.SimulatedProfileJson)));
        Assert.Equal("jsonb", ColumnType<WhatIfSimulation>(db, nameof(WhatIfSimulation.SimulatedRecommendationsJson)));
        Assert.True(HasUniqueIndex<ExpertRule>(db, nameof(ExpertRule.RuleCode)));
        Assert.True(HasUniqueIndex<SubscriptionPlan>(db, nameof(SubscriptionPlan.Code)));
        Assert.True(HasUniqueIndex<DataImportBatch>(db, nameof(DataImportBatch.BatchCode)));
        Assert.True(HasUniqueIndex<UserDailyUsage>(db, nameof(UserDailyUsage.UserId), nameof(UserDailyUsage.UsageDate)));
        Assert.Equal("state_code", db.Model.FindEntityType(typeof(StateLivingCost))!.FindPrimaryKey()!.Properties.Single().GetColumnName());
    }

    private static string TableName<T>(AppDbContext db) where T : class
        => db.Model.FindEntityType(typeof(T))!.GetTableName()!;

    private static string? ColumnType<T>(AppDbContext db, string propertyName) where T : class
        => db.Model.FindEntityType(typeof(T))!.FindProperty(propertyName)!.GetColumnType();

    private static bool HasUniqueIndex<T>(AppDbContext db, params string[] propertyNames) where T : class
        => db.Model.FindEntityType(typeof(T))!.GetIndexes()
            .Any(index => index.IsUnique && index.Properties.Select(p => p.Name).SequenceEqual(propertyNames));

    private static void AssertForeignKey<T>(AppDbContext db, string propertyName, string principalTable, DeleteBehavior deleteBehavior)
        where T : class
    {
        var foreignKey = db.Model.FindEntityType(typeof(T))!.GetForeignKeys()
            .SingleOrDefault(fk => fk.Properties.Count == 1 && fk.Properties[0].Name == propertyName);
        Assert.True(foreignKey is not null, $"{typeof(T).Name}.{propertyName} chưa có khóa ngoại.");
        Assert.Equal(principalTable, foreignKey!.PrincipalEntityType.GetTableName());
        Assert.Equal(deleteBehavior, foreignKey.DeleteBehavior);
    }
}

