using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using StudyAssistant.Models;

namespace StudyAssistant.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Class> Classes { get; set; }
    public DbSet<ClassTeacher> ClassTeachers { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }
    public DbSet<Subject> Subjects { get; set; }
    public DbSet<TeacherSubject> TeacherSubjects { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<RateLimitEntry> RateLimitEntries { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(b =>
        {
            b.Property(u => u.Role).HasConversion<string>();
            b.Property(u => u.CreatedAt).HasDefaultValueSql("now()");
        });

        builder.Entity<Class>(b =>
        {
            b.HasOne(c => c.HomeroomTeacher)
             .WithMany()
             .HasForeignKey(c => c.HomeroomTeacherId)
             .IsRequired(false)
             .OnDelete(DeleteBehavior.SetNull);

            b.HasMany(c => c.Students)
             .WithOne(u => u.Class)
             .HasForeignKey(u => u.ClassId)
             .IsRequired(false)
             .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<ClassTeacher>(b =>
        {
            b.HasKey(ct => new { ct.ClassId, ct.TeacherId, ct.SubjectId });

            b.HasOne(ct => ct.Class)
             .WithMany(c => c.ClassTeachers)
             .HasForeignKey(ct => ct.ClassId)
             .IsRequired()
             .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(ct => ct.Teacher)
             .WithMany()
             .HasForeignKey(ct => ct.TeacherId)
             .IsRequired()
             .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(ct => ct.Subject)
             .WithMany()
             .HasForeignKey(ct => ct.SubjectId)
             .IsRequired()
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Subject>(b =>
        {
            b.Property(s => s.Name).IsRequired();
            b.Property(s => s.School).IsRequired();
        });

        builder.Entity<TeacherSubject>(b =>
        {
            b.HasKey(ts => new { ts.TeacherId, ts.SubjectId });

            b.HasOne(ts => ts.Teacher)
             .WithMany(u => u.TeacherSubjects)
             .HasForeignKey(ts => ts.TeacherId)
             .IsRequired()
             .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(ts => ts.Subject)
             .WithMany(s => s.TeacherSubjects)
             .HasForeignKey(ts => ts.SubjectId)
             .IsRequired()
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ChatMessage>(b =>
        {
            b.HasOne(m => m.User)
             .WithMany()
             .HasForeignKey(m => m.UserId)
             .IsRequired()
             .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(m => m.Subject)
             .WithMany()
             .HasForeignKey(m => m.SubjectId)
             .IsRequired(false)
             .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<RefreshToken>(b =>
        {
            b.Property(r => r.Token).IsRequired().HasMaxLength(128);
            b.HasIndex(r => r.Token).IsUnique();
            b.HasOne(r => r.User)
             .WithMany()
             .HasForeignKey(r => r.UserId)
             .IsRequired()
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<RateLimitEntry>(b =>
        {
            b.HasKey(e => new { e.Key, e.Type });
            b.Property(e => e.Key).HasMaxLength(256);
            b.Property(e => e.Type).HasMaxLength(32);
        });
    }
}
