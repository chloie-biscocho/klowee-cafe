using System.Linq.Expressions;
using Klowee.Api.Entities;
using Klowee.Api.Entities.Common;
using Klowee.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Klowee.Api.Data;

public class KloweeDbContext : DbContext
{
    private readonly ICurrentUser? _currentUser;

    /// <summary>Used by the design-time factory, where there is no HTTP request.</summary>
    public KloweeDbContext(DbContextOptions<KloweeDbContext> options)
        : this(options, currentUser: null)
    {
    }

    /// <summary>
    /// Used at runtime: the DI container supplies the request's
    /// <see cref="ICurrentUser"/> so <c>created_by</c> can be stamped centrally.
    /// </summary>
    public KloweeDbContext(DbContextOptions<KloweeDbContext> options, ICurrentUser? currentUser)
        : base(options)
    {
        _currentUser = currentUser;
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

        // PostgreSQL generates the key; other providers (the SQLite used by the
        // test suite) fall back to EF's client-side Guid generator, which the
        // convention for a Guid key already supplies.
        var isPostgres = Database.IsNpgsql();

        // Conventions shared by every BaseEntity-derived table:
        //   - a database-generated uuid primary key
        //   - a global query filter that hides soft-deleted rows
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            if (isPostgres)
            {
                modelBuilder.Entity(entityType.ClrType)
                    .Property(nameof(BaseEntity.Id))
                    .HasDefaultValueSql("gen_random_uuid()");
            }

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
        ApplyAuditFields();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditFields();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// Maintains created_at / updated_at and stamps created_by from the caller's
    /// JWT "sub" claim, centrally, for every tracked entity. Rows written outside
    /// a request (the seeder, migrations) leave created_by null.
    /// </summary>
    private void ApplyAuditFields()
    {
        var now = DateTimeOffset.UtcNow;
        var userId = _currentUser?.UserId;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.CreatedBy ??= userId;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    break;
            }
        }
    }
}
