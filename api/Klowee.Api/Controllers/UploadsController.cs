using Klowee.Api.Contracts.Media;
using Klowee.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Klowee.Api.Controllers;

[ApiController]
[Route("api/uploads")]
public class UploadsController : ControllerBase
{
    private readonly IUploadService _uploads;

    public UploadsController(IUploadService uploads) => _uploads = uploads;

    /// <summary>
    /// Stores an image and returns its public URL. Multipart, field name
    /// <c>file</c>; JPEG, PNG or WebP; 5 MB maximum.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<UploadResultDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<UploadResultDto>> Upload(
        IFormFile? file,
        [FromQuery] string folder,
        CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation failed",
                Detail = "Send the image as multipart form data in a field named 'file'."
            });
        }

        await using var stream = file.OpenReadStream();

        return Ok(await _uploads.UploadAsync(
            folder ?? string.Empty,
            file.ContentType,
            file.FileName,
            file.Length,
            stream,
            cancellationToken));
    }

    /// <summary>Removes a stored object. Takes the <c>path</c> an upload returned.</summary>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Delete([FromQuery] string path, CancellationToken cancellationToken)
    {
        await _uploads.DeleteAsync(path ?? string.Empty, cancellationToken);
        return NoContent();
    }
}
