using Klowee.Api.Contracts.Auth;
using Klowee.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Klowee.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    /// <summary>Exchanges owner credentials for an access token.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _auth.LoginAsync(request, cancellationToken));

    /// <summary>Returns the owner behind the presented token.</summary>
    [HttpGet("me")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserDto>> Me(CancellationToken cancellationToken) =>
        Ok(await _auth.GetCurrentUserAsync(cancellationToken));
}
