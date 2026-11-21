using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
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
            // Phân hệ 3: Trường, Tuyển sinh & Chi phí (4)
            "University", "UniversityOffering", "Scholarship", "TargetSchool",
            // Phân hệ 4: Mô hình DSS & Mô phỏng (6)
            "AppSetting", "AnalysisResult", "Recommendation", "RecommendationItem", "ExpertRule", "WhatIfSimulation",
            // Phân hệ 5: Trung tâm tư vấn (4)
            "StudyCenter", "Lead", "LeadMessage", "CenterReview",
            // Phân hệ 6: Trợ lý AI RAG (7)
            "Skill", "KnowledgeDocument", "ChatSession", "ChatMessage", "AiCall", "EvalCase", "EvalRun",
            // Phân hệ 7: Gói dịch vụ & Thanh toán (4)
            "SubscriptionPlan", "UserSubscription", "UserDailyUsage", "PaymentTransaction", "FamilyGroup", "FamilyMember",
            // Phân hệ 8: Diễn đàn, Thông báo & Kiểm toán (5)
            "ForumPost", "ForumReport", "PolicyAlert", "Notification", "AuditLog",
            // Phân hệ 9: Xác minh dữ liệu (1)
            "FieldEvidence"
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
        AssertForeignKey<UserSubscription>(db, nameof(UserSubscription.SubscriptionPlanId), "subscription_plans", DeleteBehavior.Restrict);   // gói đã có kỳ thì không xóa được
        AssertForeignKey<UserDailyUsage>(db, nameof(UserDailyUsage.UserId), "users", DeleteBehavior.Cascade);
        AssertForeignKey<PaymentTransaction>(db, nameof(PaymentTransaction.UserId), "users", DeleteBehavior.SetNull);   // [UC-80] giữ giao dịch ẩn danh khi xóa hẳn user
        AssertForeignKey<PaymentTransaction>(db, nameof(PaymentTransaction.UserSubscriptionId), "user_subscriptions", DeleteBehavior.SetNull);
        AssertForeignKey<FieldEvidence>(db, nameof(FieldEvidence.UniversityId), "universities", DeleteBehavior.Cascade);
        // Nhật ký AI nối với tin trả lời của trợ lý; xóa tin thì giữ dòng nhật ký (token đã tiêu thật), chỉ bỏ liên kết
        AssertForeignKey<AiCall>(db, nameof(AiCall.ChatMessageId), "chat_messages", DeleteBehavior.SetNull);
        // Tin trợ lý trỏ tới tin hỏi nó trả lời; xóa tin hỏi thì giữ tin trả lời
        AssertForeignKey<ChatMessage>(db, nameof(ChatMessage.ReplyToMessageId), "chat_messages", DeleteBehavior.SetNull);

        // Màn token của admin: tổng thể (theo thời gian), theo người (người rồi thời gian), theo lượt chat (tin trả lời)
        Assert.True(HasIndex<AiCall>(db, nameof(AiCall.CreatedAt)));
        Assert.True(HasIndex<AiCall>(db, nameof(AiCall.UserId), nameof(AiCall.CreatedAt)));
        Assert.True(HasIndex<AiCall>(db, nameof(AiCall.ChatMessageId)));

        Assert.Equal("jsonb", ColumnType<ChatMessage>(db, nameof(ChatMessage.MetaJson)));
        AssertBillingSchema(db);
        Assert.Equal("jsonb", ColumnType<ExpertRule>(db, nameof(ExpertRule.ConditionJson)));
        Assert.Equal("jsonb", ColumnType<WhatIfSimulation>(db, nameof(WhatIfSimulation.SimulatedProfileJson)));
        Assert.Equal("jsonb", ColumnType<WhatIfSimulation>(db, nameof(WhatIfSimulation.SimulatedRecommendationsJson)));
        Assert.True(HasUniqueIndex<ExpertRule>(db, nameof(ExpertRule.RuleCode)));
        Assert.True(HasUniqueIndex<SubscriptionPlan>(db, nameof(SubscriptionPlan.Code)));
        Assert.True(HasUniqueIndex<UserDailyUsage>(db, nameof(UserDailyUsage.UserId), nameof(UserDailyUsage.UsageDate)));

        var studyCenterEntity = db.Model.FindEntityType(typeof(StudyCenter))!;
        Assert.Equal(32, studyCenterEntity.FindProperty(nameof(StudyCenter.TaxCode))!.GetMaxLength());
        Assert.Equal(150, studyCenterEntity.FindProperty(nameof(StudyCenter.LegalRepresentative))!.GetMaxLength());
        Assert.Equal(256, studyCenterEntity.FindProperty(nameof(StudyCenter.LicenseIssuedBy))!.GetMaxLength());
        Assert.Equal(32, studyCenterEntity.FindProperty(nameof(StudyCenter.LegalComplianceMode))!.GetMaxLength());
        Assert.Equal("licensed", studyCenterEntity.FindProperty(nameof(StudyCenter.LegalComplianceMode))!.GetDefaultValue());
        Assert.Equal(128, studyCenterEntity.FindProperty(nameof(StudyCenter.TrainingLicenseNumber))!.GetMaxLength());
        Assert.Equal("text[]", ColumnType<StudyCenter>(db, nameof(StudyCenter.LegalDocumentUrls)));
        Assert.Equal(1000, studyCenterEntity.FindProperty(nameof(StudyCenter.VerificationNote))!.GetMaxLength());
    }

    // Gói dịch vụ v2 (migration AddSubscriptionBillingV2): bảng gia đình, cột mới, index duy nhất có điều kiện, ràng buộc kiểm tra
    private static void AssertBillingSchema(AppDbContext db)
    {
        Assert.Equal("family_groups", TableName<FamilyGroup>(db));
        Assert.Equal("family_members", TableName<FamilyMember>(db));
        AssertForeignKey<UserSubscription>(db, nameof(UserSubscription.FamilyGroupId), "family_groups", DeleteBehavior.SetNull);
        AssertForeignKey<FamilyGroup>(db, nameof(FamilyGroup.OwnerUserId), "users", DeleteBehavior.Cascade);
        AssertForeignKey<FamilyMember>(db, nameof(FamilyMember.FamilyGroupId), "family_groups", DeleteBehavior.Cascade);
        AssertForeignKey<FamilyMember>(db, nameof(FamilyMember.UserId), "users", DeleteBehavior.SetNull);
        AssertForeignKey<PaymentTransaction>(db, nameof(PaymentTransaction.SubscriptionPlanId), "subscription_plans", DeleteBehavior.Restrict);
        AssertForeignKey<PaymentTransaction>(db, nameof(PaymentTransaction.HandledByUserId), "users", DeleteBehavior.SetNull);
        AssertForeignKey<AiCall>(db, nameof(AiCall.UserSubscriptionId), "user_subscriptions", DeleteBehavior.SetNull);
        Assert.True(HasIndex<AiCall>(db, nameof(AiCall.UserSubscriptionId), nameof(AiCall.CreatedAt)));   // cộng token của một kỳ

        // Cột: kỳ có ends_at (null khi xếp hàng), bản chụp gói; đã bỏ auto_renew và các hạn mức cũ của gói
        var cycle = db.Model.FindEntityType(typeof(UserSubscription))!;
        Assert.Equal("ends_at", cycle.FindProperty(nameof(UserSubscription.EndsAt))!.GetColumnName());
        Assert.True(cycle.FindProperty(nameof(UserSubscription.StartsAt))!.IsNullable);
        Assert.Equal("warned_80_at", cycle.FindProperty(nameof(UserSubscription.Warned80At))!.GetColumnName());
        Assert.Equal("bigint", ColumnType<UserSubscription>(db, nameof(UserSubscription.TokenQuota)) ?? cycle.FindProperty(nameof(UserSubscription.TokenQuota))!.GetRelationalTypeMapping().StoreType);
        Assert.Null(cycle.FindProperty("AutoRenew"));
        var plan = db.Model.FindEntityType(typeof(SubscriptionPlan))!;
        Assert.Null(plan.FindProperty("DailyAiChatQuota"));
        Assert.Null(plan.FindProperty("MaxLeadsQuota"));
        Assert.Null(plan.FindProperty("EstimatedQuestions"));
        Assert.True(plan.FindProperty(nameof(SubscriptionPlan.DurationDays))!.IsNullable);
        Assert.True(plan.FindProperty(nameof(SubscriptionPlan.QuotaPeriodDays))!.IsNullable);
        Assert.True(cycle.FindProperty(nameof(UserSubscription.QuotaPeriodDays))!.IsNullable);
        Assert.True(db.Model.FindEntityType(typeof(PaymentTransaction))!.FindProperty(nameof(PaymentTransaction.PlanQuotaPeriodDays))!.IsNullable);
        Assert.Equal(16, plan.FindProperty(nameof(SubscriptionPlan.Kind))!.GetMaxLength());
        Assert.True(HasUniqueIndex<PaymentTransaction>(db, nameof(PaymentTransaction.OrderCode)));
        Assert.True(db.Model.FindEntityType(typeof(FamilyMember))!.FindProperty(nameof(FamilyMember.Version))!.IsConcurrencyToken);

        // Kỳ cộng dồn nên không có index duy nhất theo (người, loại); một nhóm active mỗi chủ, một thành viên active mỗi người, một lời mời chờ mỗi email,
        // một đơn chờ mỗi người, một gói free
        Assert.DoesNotContain(cycle.GetIndexes(), i => i.IsUnique);
        Assert.Equal("status = 'active'", PartialUniqueFilter<FamilyGroup>(db, "ux_family_groups_one_active_per_owner", "OwnerUserId"));
        Assert.Equal("status = 'active'", PartialUniqueFilter<FamilyMember>(db, "ux_family_members_one_active_per_user", "UserId"));
        Assert.Equal("status = 'pending'", PartialUniqueFilter<FamilyMember>(db, "ux_family_members_one_pending_per_email", "FamilyGroupId", "InvitedEmail"));
        Assert.Equal("token_hash IS NOT NULL", db.Model.FindEntityType(typeof(FamilyMember))!.GetIndexes()
            .Single(i => i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual([nameof(FamilyMember.TokenHash)])).GetFilter());
        Assert.Equal("status = 'pending'", PartialUniqueFilter<PaymentTransaction>(db, "ux_payment_transactions_one_pending_per_user", "UserId"));
        Assert.Equal("kind = 'free'", PartialUniqueFilter<SubscriptionPlan>(db, "ux_subscription_plans_single_free", "Kind"));

        var designModel = db.GetService<IDesignTimeModel>().Model;   // ràng buộc kiểm tra chỉ có trong model lúc thiết kế
        Assert.Equal(
            ["ck_subscription_plans_duration", "ck_subscription_plans_kind", "ck_subscription_plans_quota_period"],
            designModel.FindEntityType(typeof(SubscriptionPlan))!.GetCheckConstraints().Select(c => c.Name).Order());
        Assert.Contains("'mismatch'", designModel.FindEntityType(typeof(PaymentTransaction))!.GetCheckConstraints()
            .Single(c => c.Name == "ck_payment_transactions_status").Sql);
    }

    private static string? PartialUniqueFilter<T>(AppDbContext db, string name, params string[] propertyNames) where T : class
    {
        var index = db.Model.FindEntityType(typeof(T))!.GetIndexes().Single(i => i.GetDatabaseName() == name);
        Assert.True(index.IsUnique, $"{name} phải là index duy nhất");
        Assert.Equal(propertyNames, index.Properties.Select(p => p.Name));
        return index.GetFilter();
    }

    private static string TableName<T>(AppDbContext db) where T : class
        => db.Model.FindEntityType(typeof(T))!.GetTableName()!;

    private static string? ColumnType<T>(AppDbContext db, string propertyName) where T : class
        => db.Model.FindEntityType(typeof(T))!.FindProperty(propertyName)!.GetColumnType();

    private static bool HasIndex<T>(AppDbContext db, params string[] propertyNames) where T : class
        => db.Model.FindEntityType(typeof(T))!.GetIndexes()
            .Any(index => index.Properties.Select(p => p.Name).SequenceEqual(propertyNames));

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

