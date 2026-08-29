namespace ShopKeeper.Api.Controllers;

using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShopKeeper.Application.Uploads.Commands;

[Authorize]
[ApiController]
[Route("api/uploads")]
public class UploadsController(ISender mediator) : ControllerBase
{
    private const long MaxFileSizeBytes = 5 * 1024 * 1024;

    [HttpPost("product-image")]
    [RequestSizeLimit(MaxFileSizeBytes)]
    public async Task<ActionResult<object>> UploadProductImage(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { title = "No file was provided." });
        }

        if (file.Length > MaxFileSizeBytes)
        {
            return BadRequest(new { title = "Images must be 5MB or smaller." });
        }

        await using var stream = file.OpenReadStream();
        var url = await mediator.Send(new UploadProductImageCommand(stream, file.ContentType), ct);
        return Ok(new { url });
    }

    [HttpPost("profile-photo")]
    [RequestSizeLimit(MaxFileSizeBytes)]
    public async Task<ActionResult<object>> UploadProfilePhoto(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { title = "No file was provided." });
        }

        if (file.Length > MaxFileSizeBytes)
        {
            return BadRequest(new { title = "Images must be 5MB or smaller." });
        }

        await using var stream = file.OpenReadStream();
        var url = await mediator.Send(new UploadProfilePhotoCommand(stream, file.ContentType), ct);
        return Ok(new { url });
    }

    [HttpPost("business-logo")]
    [RequestSizeLimit(MaxFileSizeBytes)]
    public async Task<ActionResult<object>> UploadBusinessLogo(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { title = "No file was provided." });
        }

        if (file.Length > MaxFileSizeBytes)
        {
            return BadRequest(new { title = "Images must be 5MB or smaller." });
        }

        await using var stream = file.OpenReadStream();
        var url = await mediator.Send(new UploadBusinessLogoCommand(stream, file.ContentType), ct);
        return Ok(new { url });
    }
}
