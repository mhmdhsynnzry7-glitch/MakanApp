using MakanApp.Api.Authentication;
using MakanApp.Application.Academic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/academic/courses")]
public sealed class CoursesController(IAcademicService academicService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<CourseResult>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CourseResult>> Create(
        CreateCourseCommand command,
        CancellationToken cancellationToken)
    {
        var result = await academicService.CreateCourseAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            command,
            cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }
}
