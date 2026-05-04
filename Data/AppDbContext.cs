using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using StudyAssistant.Models;

namespace StudyAssistant.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Class> Classes { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }

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
            // Teacher FK: one ApplicationUser owns many Class records (no inverse collection)
            b.HasOne(c => c.Teacher)
             .WithMany()
             .HasForeignKey(c => c.TeacherId)
             .IsRequired()
             .OnDelete(DeleteBehavior.Restrict);

            // Students: one Class has many ApplicationUser via ClassId (inverse: User.Class)
            b.HasMany(c => c.Students)
             .WithOne(u => u.Class)
             .HasForeignKey(u => u.ClassId)
             .IsRequired(false)
             .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<ChatMessage>(b =>
        {
            b.HasOne(m => m.User)
             .WithMany()
             .HasForeignKey(m => m.UserId)
             .IsRequired()
             .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
