using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using StudyAssistant.Models;

namespace StudyAssistant.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<School> Schools { get; set; }
    public DbSet<Class> Classes { get; set; }
    public DbSet<ClassTeacher> ClassTeachers { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }
    public DbSet<ChatSession> ChatSessions { get; set; }
    public DbSet<Subject> Subjects { get; set; }
    public DbSet<TeacherSubject> TeacherSubjects { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<PasswordResetCode> PasswordResetCodes { get; set; }
    public DbSet<SchoolTeacherCode> SchoolTeacherCodes { get; set; }
    public DbSet<RateLimitEntry> RateLimitEntries { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(b =>
        {
            b.Property(u => u.Role).HasConversion<string>();
            b.Property(u => u.CreatedAt).HasDefaultValueSql("now()");

            b.HasOne(u => u.School)
             .WithMany()
             .HasForeignKey(u => u.SchoolId)
             .IsRequired(false)
             .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Class>(b =>
        {
            b.HasOne(c => c.School)
             .WithMany()
             .HasForeignKey(c => c.SchoolId)
             .IsRequired()
             .OnDelete(DeleteBehavior.Restrict);

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

        builder.Entity<School>(b =>
        {
            b.Property(s => s.Name).IsRequired().HasMaxLength(200);
            b.HasIndex(s => s.Name).IsUnique();
            b.Property(s => s.CreatedAt).HasDefaultValueSql("now()");
        });

        builder.Entity<Subject>(b =>
        {
            b.Property(s => s.Name).IsRequired();

            b.HasOne(s => s.School)
             .WithMany()
             .HasForeignKey(s => s.SchoolId)
             .IsRequired()
             .OnDelete(DeleteBehavior.Restrict);
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

        builder.Entity<ChatSession>(b =>
        {
            b.HasOne(s => s.User)
             .WithMany()
             .HasForeignKey(s => s.UserId)
             .IsRequired()
             .OnDelete(DeleteBehavior.Cascade);

            b.HasOne(s => s.Subject)
             .WithMany()
             .HasForeignKey(s => s.SubjectId)
             .IsRequired(false)
             .OnDelete(DeleteBehavior.SetNull);

            b.HasIndex(s => new { s.UserId, s.LastMessageAt });
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

            b.HasOne(m => m.Session)
             .WithMany(s => s.Messages)
             .HasForeignKey(m => m.SessionId)
             .IsRequired()
             .OnDelete(DeleteBehavior.Cascade);
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

        builder.Entity<PasswordResetCode>(b =>
        {
            b.Property(c => c.UserId).IsRequired().HasMaxLength(450);
            b.Property(c => c.Code).IsRequired().HasMaxLength(6);
            b.HasIndex(c => c.UserId);
            b.HasOne(c => c.User)
             .WithMany()
             .HasForeignKey(c => c.UserId)
             .IsRequired()
             .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<SchoolTeacherCode>(b =>
        {
            b.Property(c => c.Code).IsRequired().HasMaxLength(24);
            b.HasIndex(c => c.Code).IsUnique();
            b.HasIndex(c => new { c.SchoolId, c.IsActive });

            b.HasOne(c => c.School)
             .WithMany()
             .HasForeignKey(c => c.SchoolId)
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
