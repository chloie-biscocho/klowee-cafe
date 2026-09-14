using Klowee.Api.Contracts.Settings;
using Klowee.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Klowee.Api.Controllers;

[ApiController]
[Route("api/settings")]
public class SettingsController : ControllerBase
{
    private readonly ISiteSettingService _settings;

    public SettingsController(ISiteSettingService settings) => _settings = settings;

    /// <summary>Every stored site setting, as key/value pairs.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<SiteSettingDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<SiteSettingDto>>> List(CancellationToken cancellationToken) =>
        Ok(await _settings.ListAsync(cancellationToken));

    /// <summary>Upserts the supplied settings and returns the full set. Unknown keys are a 400.</summary>
    [HttpPut]
    [ProducesResponseType<IReadOnlyList<SiteSettingDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<SiteSettingDto>>> Upsert(
        List<SiteSettingRequest> settings,
        CancellationToken cancellationToken) =>
        Ok(await _settings.UpsertAsync(settings, cancellationToken));
}
