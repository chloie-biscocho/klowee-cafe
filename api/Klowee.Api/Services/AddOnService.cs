using Klowee.Api.Common;
using Klowee.Api.Contracts.Menu;
using Klowee.Api.Data;
using Klowee.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace Klowee.Api.Services;

public class AddOnService : IAddOnService
{
    private readonly KloweeDbContext _db;

    public AddOnService(KloweeDbContext db) => _db = db;

    public async Task<IReadOnlyList<AddOnDto>> ListAsync(CancellationToken cancellationToken) =>
        await _db.AddOns
            .AsNoTracking()
            .OrderBy(a => a.Name)
            .Select(a => new AddOnDto(a.Id, a.Name, a.Price, a.IsActive))
            .ToListAsync(cancellationToken);

    public async Task<AddOnDto> CreateAsync(AddOnRequest request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        await EnsureNameIsFreeAsync(name, excludingId: null, cancellationToken);

        var addOn = new AddOn { Name = name, Price = request.Price, IsActive = request.IsActive };

        _db.AddOns.Add(addOn);
        await _db.SaveChangesAsync(cancellationToken);

        return new AddOnDto(addOn.Id, addOn.Name, addOn.Price, addOn.IsActive);
    }

    public async Task<AddOnDto> UpdateAsync(Guid id, AddOnRequest request, CancellationToken cancellationToken)
    {
        var addOn = await _db.AddOns.FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw NotFoundException.For("Add-on", id);

        var name = request.Name.Trim();
        await EnsureNameIsFreeAsync(name, excludingId: id, cancellationToken);

        addOn.Name = name;
        addOn.Price = request.Price;
        addOn.IsActive = request.IsActive;
        await _db.SaveChangesAsync(cancellationToken);

        return new AddOnDto(addOn.Id, addOn.Name, addOn.Price, addOn.IsActive);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var addOn = await _db.AddOns.FirstOrDefaultAsync(a => a.Id == id, cancellationToken)
            ?? throw NotFoundException.For("Add-on", id);

        addOn.DeletedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Mirrors the partial unique index on add_ons.name.</summary>
    private async Task EnsureNameIsFreeAsync(string name, Guid? excludingId, CancellationToken cancellationToken)
    {
        var taken = await _db.AddOns.AnyAsync(
            a => a.Name.ToLower() == name.ToLower() && (excludingId == null || a.Id != excludingId),
            cancellationToken);

        if (taken)
        {
            throw new ConflictException($"An add-on named '{name}' already exists.");
        }
    }
}
