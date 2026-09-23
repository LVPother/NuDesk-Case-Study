using Microsoft.EntityFrameworkCore;

namespace WeeklySalesCoach.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Rep> Reps => Set<Rep>();
    public DbSet<Week> Weeks => Set<Week>();
    public DbSet<WeeklyMetrics> WeeklyMetrics => Set<WeeklyMetrics>();
    public DbSet<Deal> Deals => Set<Deal>();
    public DbSet<CoachingReport> CoachingReports => Set<CoachingReport>();
    public DbSet<FeedbackEmail> FeedbackEmails => Set<FeedbackEmail>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<AppUser>().HasIndex(u => u.Username).IsUnique();
        b.Entity<AppUser>().Property(u => u.Role).HasConversion<string>();
        b.Entity<Rep>().Property(r => r.FeedbackStyle).HasConversion<string>();
        b.Entity<WeeklyMetrics>().HasIndex(m => new { m.RepId, m.WeekId }).IsUnique();
        b.Entity<CoachingReport>().HasIndex(r => new { r.RepId, r.WeekId }).IsUnique();
        b.Entity<CoachingReport>().Property(r => r.Status).HasConversion<string>();
        b.Entity<CoachingReport>().Property(r => r.Source).HasConversion<string>();
        b.Entity<FeedbackEmail>().HasIndex(e => e.ReportId); // one email per approval: a revised report gets a second one
        b.Entity<FeedbackEmail>().Property(e => e.Status).HasConversion<string>();
    }
}
