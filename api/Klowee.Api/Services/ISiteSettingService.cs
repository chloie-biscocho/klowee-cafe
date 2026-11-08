using Klowee.Api.Contracts.Settings;

namespace Klowee.Api.Services;

public interface ISiteSettingService
{
    Task<IReadOnlyList<SiteSettingDto>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Stamps per free drink (loyalty_stamps_per_reward); 10 when unset.</summary>
    Task<int> GetLoyaltyStampsPerRewardAsync(CancellationToken cancellationToken);

    /// <summary>Upserts the supplied keys. Unknown keys are rejected as a 400.</summary>
    Task<IReadOnlyList<SiteSettingDto>> UpsertAsync(
        IReadOnlyList<SiteSettingRequest> settings,
        CancellationToken cancellationToken);
}
