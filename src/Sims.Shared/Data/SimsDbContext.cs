using Microsoft.EntityFrameworkCore;
using Sims.Shared.Entities;

namespace Sims.Shared.Data;

public class SimsDbContext(DbContextOptions<SimsDbContext> options) : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Scan> Scans => Set<Scan>();
    public DbSet<Component> Components => Set<Component>();
    public DbSet<Finding> Findings => Set<Finding>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("scan");

        modelBuilder.Entity<Project>(entity =>
        {
            entity.Property(p => p.Name).HasMaxLength(200).IsRequired();
            entity.HasIndex(p => p.TeamId);
            entity.HasMany(p => p.Scans).WithOne(s => s.Project).HasForeignKey(s => s.ProjectId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Scan>(entity =>
        {
            entity.Property(s => s.Version).HasMaxLength(50).IsRequired();
            entity.Property(s => s.ErrorMessage).HasMaxLength(500);
            entity.HasIndex(s => s.ProjectId);
            entity.HasMany(s => s.Components).WithOne(c => c.Scan).HasForeignKey(c => c.ScanId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(s => s.Findings).WithOne(f => f.Scan).HasForeignKey(f => f.ScanId).OnDelete(DeleteBehavior.Cascade);
            entity.Property(s => s.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(s => s.Verdict).HasConversion<string>().HasMaxLength(32);
        });

        modelBuilder.Entity<Component>(entity =>
        {
            entity.Property(c => c.Name).HasMaxLength(200).IsRequired();
            entity.Property(c => c.Version).HasMaxLength(100);
            entity.Property(c => c.Purl).HasMaxLength(500);
            entity.HasMany(c => c.Findings).WithOne(f => f.Component).HasForeignKey(f => f.ComponentId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Finding>(entity =>
        {
            entity.Property(f => f.VulnId).HasMaxLength(100).IsRequired();
            entity.Property(f => f.Cve).HasMaxLength(50);
            entity.Property(f => f.Summary).HasMaxLength(500);
            entity.HasIndex(f => f.ScanId);
            entity.Property(f => f.Severity).HasConversion<string>().HasMaxLength(32);
        });

        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.Property(t => t.Title).HasMaxLength(200).IsRequired();
            entity.Property(t => t.Description).HasMaxLength(4000);
            entity.Property(t => t.Cve).HasMaxLength(50);
            entity.Property(t => t.System).HasMaxLength(200);
            entity.Property(t => t.ReporterName).HasMaxLength(200).IsRequired();
            entity.Property(t => t.AssigneeName).HasMaxLength(200);
            entity.Property(t => t.AssignedRole).HasMaxLength(32);
            entity.Property(t => t.Severity).HasConversion<string>().HasMaxLength(32);
            entity.Property(t => t.Status).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(t => new { t.TeamId, t.Status });
            entity.HasIndex(t => new { t.ProjectId, t.Cve, t.System });
            entity.HasOne<Project>().WithMany().HasForeignKey(t => t.ProjectId).OnDelete(DeleteBehavior.NoAction);
            entity.HasOne<Finding>().WithMany().HasForeignKey(t => t.FindingId).OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<AuditEntry>(entity =>
        {
            entity.Property(a => a.Action).HasMaxLength(64).IsRequired();
            entity.Property(a => a.UserName).HasMaxLength(200).IsRequired();
            entity.Property(a => a.Details).HasMaxLength(2000);
            entity.HasIndex(a => a.Timestamp);
        });
    }
}
