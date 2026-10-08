using Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<ScanJobEntity> ScanJobs => Set<ScanJobEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        var job = modelBuilder.Entity<ScanJobEntity>();
        job.HasKey(j => j.Id);
        job.Property(j => j.Host).IsRequired();
        job.Property(j => j.PortsJson).IsRequired();
        job.Property(j => j.Status).HasConversion<string>().HasMaxLength(16);
        job.Property(j => j.Type).HasConversion<string>().HasMaxLength(8);
        job.HasIndex(j => j.Status);
    }
}