using Microsoft.EntityFrameworkCore;
using StudyAbroad.Domain.Common;
using StudyAbroad.Domain.Entities;

namespace StudyAbroad.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<StudyCenter> StudyCenters => Set<StudyCenter>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserSession> UserSessions => Set<UserSession>();

    public DbSet<OtpToken> OtpTokens => Set<OtpToken>();
    public DbSet<Consent> Consents => Set<Consent>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<StudentProfile> StudentProfiles => Set<StudentProfile>();
    public DbSet<TranscriptScore> TranscriptScores => Set<TranscriptScore>();
    public DbSet<ProfileActivity> ProfileActivities => Set<ProfileActivity>();
    public DbSet<University> Universities => Set<University>();
    public DbSet<UniversityOffering> UniversityOfferings => Set<UniversityOffering>();
    public DbSet<Scholarship> Scholarships => Set<Scholarship>();
    public DbSet<TargetSchool> TargetSchools => Set<TargetSchool>();
    public DbSet<Recommendation> Recommendations => Set<Recommendation>();
    public DbSet<AnalysisResult> AnalysisResults => Set<AnalysisResult>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<KnowledgeDocument> KnowledgeDocuments => Set<KnowledgeDocument>();
    public DbSet<AiCall> AiCalls => Set<AiCall>();
    public DbSet<EvalCase> EvalCases => Set<EvalCase>();
    public DbSet<EvalRun> EvalRuns => Set<EvalRun>();
    public DbSet<RoadmapStep> RoadmapSteps => Set<RoadmapStep>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<ForumPost> ForumPosts => Set<ForumPost>();
    public DbSet<ForumReport> ForumReports => Set<ForumReport>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Business tables live in the "app" schema; the advisor service owns the "advisor" schema (pgvector).
        modelBuilder.HasDefaultSchema("app");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = DateTime.UtcNow;
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}
