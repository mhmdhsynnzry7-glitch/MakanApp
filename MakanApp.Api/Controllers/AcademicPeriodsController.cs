using MakanApp.Api.Authentication;
using MakanApp.Application.Academic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/academic/periods")]
public sealed class AcademicPeriodsController(IAcademicService academicService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<AcademicPeriodResult>(StatusCodes.Status201Created)]
    public async Task<ActionResult<AcademicPeriodResult>> Create(
        CreateAcademicPeriodCommand command,
        CancellationToken cancellationToken)
    {
        var result = await academicService.CreateAcademicPeriodAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            command,
            cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }
}
