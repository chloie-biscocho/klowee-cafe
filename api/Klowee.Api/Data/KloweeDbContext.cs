using System.Linq.Expressions;
using Klowee.Api.Entities;
using Klowee.Api.Entities.Common;
using Microsoft.EntityFrameworkCore;

namespace Klowee.Api.Data;

public class KloweeDbContext : DbContext
{
    public KloweeDbContext(DbContextOptions<KloweeDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<MenuCategory> MenuCategories => Set<MenuCategory>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<MenuVersion> MenuVersions => Set<MenuVersion>();
    public DbSet<MenuVersionItem> MenuVersionItems => Set<MenuVersionItem>();
    public DbSet<AddOn> AddOns => Set<AddOn>();
    public DbSet<Package> Packages => Set<Package>();
    public DbSet<PackageInclusion> PackageInclusions => Set<PackageInclusion>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<EventPhoto> EventPhotos => Set<EventPhoto>();
    public DbSet<Announcement> Announcements => Set<Announcement>();
    public DbSet<SiteSetting> SiteSettings => Set<SiteSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(KloweeDbContext).Assembly);

        // Conventions shared by every BaseEntity-derived table:
        //   - a database-generated uuid primary key
        //   - a global query filter that hides soft-deleted rows
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            modelBuilder.Entity(entityType.ClrType)
                .Property(nameof(BaseEntity.Id))
                .HasDefaultValueSql("gen_random_uuid()");

            // Build: entity => entity.DeletedAt == null
            var parameter = Expression.Parameter(entityType.ClrType, "entity");
            var deletedAt = Expression.Property(parameter, nameof(BaseEntity.DeletedAt));
            var filter = Expression.Lambda(
                Expression.Equal(deletedAt, Expression.Constant(null, typeof(DateTimeOffset?))),
                parameter);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
        }
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyAuditTimestamps();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditTimestamps();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>Maintains created_at / updated_at centrally for every tracked entity.</summary>
    private void ApplyAuditTimestamps()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.UpdatedAt = now;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    break;
            }
        }
    }
}
