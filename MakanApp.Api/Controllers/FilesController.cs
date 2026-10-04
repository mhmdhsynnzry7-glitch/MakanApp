using MakanApp.Api.Authentication;
using MakanApp.Application.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/files")]
public sealed class FilesController(IStorageService storageService) : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<FileAssetResult>(StatusCodes.Status201Created)]
    public async Task<ActionResult<FileAssetResult>> Upload(
        [FromForm] UploadFileRequest request,
        CancellationToken cancellationToken)
    {
        await using var content = request.File.OpenReadStream();
        var result = await storageService.UploadFileAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            new UploadFileCommand(
                request.File.FileName,
                request.File.ContentType,
                request.File.Length,
                content),
            cancellationToken);
        return CreatedAtAction(nameof(GetMetadata), new { fileId = result.Id }, result);
    }

    [HttpGet("{fileId:guid}")]
    [ProducesResponseType<FileAssetResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<FileAssetResult>> GetMetadata(
        Guid fileId,
        CancellationToken cancellationToken) =>
        Ok(await storageService.GetFileMetadataAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            fileId,
            cancellationToken));

    [HttpGet("{fileId:guid}/content")]
    [ProducesResponseType<FileStreamResult>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Download(
        Guid fileId,
        CancellationToken cancellationToken)
    {
        var result = await storageService.DownloadFileAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            fileId,
            cancellationToken);
        Response.ContentLength = result.SizeBytes;
        return File(result.Content, result.ContentType, result.DownloadFileName);
    }

    [HttpDelete("{fileId:guid}")]
    [ProducesResponseType<FileAssetResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<FileAssetResult>> Delete(
        Guid fileId,
        [FromQuery] string expectedRowVersion,
        CancellationToken cancellationToken) =>
        Ok(await storageService.DeleteFileAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            fileId,
            expectedRowVersion,
            cancellationToken));
}

public sealed class UploadFileRequest
{
    public required IFormFile File { get; init; }
}
