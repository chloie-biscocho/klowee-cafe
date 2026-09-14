using Klowee.Api.Common;
using Klowee.Api.Contracts.Packages;
using Klowee.Api.Data;
using Klowee.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Klowee.Api.Services;

public class PackageService : IPackageService
{
    private readonly KloweeDbContext _db;

    public PackageService(KloweeDbContext db) => _db = db;

    // Ordering happens on the entity query, before the projection: EF cannot
    // translate an OrderBy applied to a DTO that already carries a collection.
    public async Task<IReadOnlyList<PackageDto>> ListAsync(
        bool includeInactive,
        CancellationToken cancellationToken) =>
        await Project(_db.Packages
                .AsNoTracking()
                .Where(p => includeInactive || p.IsActive)
                .OrderBy(p => p.SortOrder)
                .ThenBy(p => p.Name))
            .ToListAsync(cancellationToken);

    public async Task<PackageDto> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await Project(_db.Packages.AsNoTracking().Where(p => p.Id == id))
            .FirstOrDefaultAsync(cancellationToken)
        ?? throw NotFoundException.For("Package", id);

    public async Task<PackageDto> CreateAsync(PackageRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        await EnsureNameIsFreeAsync(name, excludingId: null, cancellationToken);

        var package = new Package
        {
            Name = name,
            Price = request.Price,
            Description = request.Description?.Trim() ?? string.Empty,
            GuestCountNote = request.GuestCountNote?.Trim() ?? string.Empty,
            IsActive = request.IsActive,
            SortOrder = request.SortOrder
        };

        foreach (var inclusion in request.Inclusions)
        {
            package.Inclusions.Add(new PackageInclusion
            {
                Text = inclusion.Text.Trim(),
                SortOrder = inclusion.SortOrder
            });
        }

        _db.Packages.Add(package);
        await _db.SaveChangesAsync(cancellationToken);

        return await GetAsync(package.Id, cancellationToken);
    }

    /// <summary>
    /// Replaces the package and its whole inclusion list in one transaction —
    /// the same shape as menu version items and event photos. Inclusions have no
    /// identity worth preserving: they are lines of text the owners retype.
    /// </summary>
    public async Task<PackageDto> UpdateAsync(
        Guid id,
        PackageRequest request,
        CancellationToken cancellationToken)
    {
        var package = await LoadAsync(id, cancellationToken);

        var name = request.Name.Trim();
        await EnsureNameIsFreeAsync(name, excludingId: id, cancellationToken);

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        package.Name = name;
        package.Price = request.Price;
        package.Description = request.Description?.Trim() ?? string.Empty;
        package.GuestCountNote = request.GuestCountNote?.Trim() ?? string.Empty;
        package.IsActive = request.IsActive;
        package.SortOrder = request.SortOrder;

        var existing = await _db.PackageInclusions
            .Where(i => i.PackageId == id)
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        foreach (var row in existing)
        {
            row.DeletedAt = now;
        }

        foreach (var inclusion in request.Inclusions)
        {
            _db.PackageInclusions.Add(new PackageInclusion
            {
                PackageId = id,
                Text = inclusion.Text.Trim(),
                SortOrder = inclusion.SortOrder
            });
        }

        // One save is enough here: unlike menu version items, inclusions carry no
        // partial unique index, so an insert cannot collide with a tombstone.
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    /// <summary>Idempotent: activating an active package is a 200, unchanged.</summary>
    public async Task<PackageDto> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        var package = await LoadAsync(id, cancellationToken);

        package.IsActive = isActive;
        await _db.SaveChangesAsync(cancellationToken);

        return await GetAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var package = await LoadAsync(id, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        package.DeletedAt = now;

        // Cascade the soft delete, so no inclusion is left pointing at a package
        // that no longer exists.
        var inclusions = await _db.PackageInclusions
            .Where(i => i.PackageId == id)
            .ToListAsync(cancellationToken);

        foreach (var inclusion in inclusions)
        {
            inclusion.DeletedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static IQueryable<PackageDto> Project(IQueryable<Package> packages) =>
        packages.Select(p => new PackageDto(
            p.Id,
            p.Name,
            p.Price,
            p.Description,
            p.GuestCountNote,
            p.IsActive,
            p.SortOrder,
            p.Inclusions
                .OrderBy(i => i.SortOrder)
                .Select(i => new PackageInclusionDto(i.Id, i.Text, i.SortOrder))
                .ToList()));

    private async Task<Package> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        await _db.Packages.FirstOrDefaultAsync(p => p.Id == id, cancellationToken)
        ?? throw NotFoundException.For("Package", id);

    private async Task EnsureNameIsFreeAsync(string name, Guid? excludingId, CancellationToken cancellationToken)
    {
        var taken = await _db.Packages.AnyAsync(
            p => p.Name.ToLower() == name.ToLower() && (excludingId == null || p.Id != excludingId),
            cancellationToken);

        if (taken)
        {
            throw new ConflictException($"A package named '{name}' already exists.");
        }
    }
}
