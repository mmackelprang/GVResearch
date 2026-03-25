using Iaet.Catalog.Entities;
using Microsoft.EntityFrameworkCore;

namespace Iaet.Catalog;

public class CatalogDbContext : DbContext
{
    public DbSet<CaptureSessionEntity> Sessions => Set<CaptureSessionEntity>();
    public DbSet<CapturedRequestEntity> Requests => Set<CapturedRequestEntity>();
    public DbSet<EndpointGroupEntity> EndpointGroups => Set<EndpointGroupEntity>();

    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<CaptureSessionEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasMany(x => x.Requests).WithOne(x => x.Session).HasForeignKey(x => x.SessionId);
        });

        modelBuilder.Entity<CapturedRequestEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.SessionId);
            e.HasIndex(x => x.NormalizedSignature);
        });

        modelBuilder.Entity<EndpointGroupEntity>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.SessionId, x.NormalizedSignature }).IsUnique();
        });
    }
}
