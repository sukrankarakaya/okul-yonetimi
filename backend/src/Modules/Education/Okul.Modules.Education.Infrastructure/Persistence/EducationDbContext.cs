using Microsoft.EntityFrameworkCore;
using Okul.Core.Persistence;
using Okul.Core.Tenancy;
using Okul.Modules.Education.Domain;

namespace Okul.Modules.Education.Infrastructure.Persistence;

public sealed class EducationDbContext : TenantDbContext
{
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<LessonCompletion> LessonCompletions => Set<LessonCompletion>();

    public EducationDbContext(DbContextOptions<EducationDbContext> options, ITenantContext tenantContext)
        : base(options, tenantContext) { }

    protected override void ConfigureModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("education");

        modelBuilder.Entity<Course>(b =>
        {
            b.ToTable("courses");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.TenantId).IsRequired();
            b.Property(x => x.Title).IsRequired().HasMaxLength(255);
            b.Property(x => x.Description).IsRequired();
            b.Property(x => x.Level).IsRequired();
            b.Property(x => x.DurationMinutes).IsRequired();
            b.Property(x => x.InstructorId).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.Level });
        });

        modelBuilder.Entity<Enrollment>(b =>
        {
            b.ToTable("enrollments");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.TenantId).IsRequired();
            b.Property(x => x.StudentId).IsRequired();
            b.Property(x => x.CourseId).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.StudentId, x.CourseId }).IsUnique();
        });

        modelBuilder.Entity<Lesson>(b =>
        {
            b.ToTable("lessons");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.CourseId).IsRequired();
            b.Property(x => x.Title).IsRequired().HasMaxLength(255);
            b.Property(x => x.VideoUrl).IsRequired();
            b.Property(x => x.DurationMinutes).IsRequired();
            b.Property(x => x.Order).IsRequired();
            b.HasIndex(x => x.CourseId);
        });

        modelBuilder.Entity<LessonCompletion>(b =>
        {
            b.ToTable("lesson_completions");
            b.HasKey(x => x.Id);
            b.Property(x => x.Id).ValueGeneratedNever();
            b.Property(x => x.EnrollmentId).IsRequired();
            b.Property(x => x.LessonId).IsRequired();
            b.Property(x => x.CompletedAt).IsRequired();
            b.HasIndex(x => new { x.EnrollmentId, x.LessonId }).IsUnique();
        });
    }
}
