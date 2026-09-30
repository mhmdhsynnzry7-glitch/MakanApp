using MakanApp.Api.Authentication;
using MakanApp.Application.Academic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MakanApp.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/academic/classes")]
public sealed class ClassesController(IAcademicService academicService) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<ClassResult>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ClassResult>> Create(
        CreateClassCommand command,
        CancellationToken cancellationToken)
    {
        var result = await academicService.CreateClassAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            command,
            cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyCollection<ClassResult>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<ClassResult>>> GetClasses(
        CancellationToken cancellationToken)
    {
        var result = await academicService.GetClassesAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            cancellationToken);
        return Ok(result);
    }

    [HttpPost("{classId:guid}/enrollments")]
    [ProducesResponseType<EnrollmentResult>(StatusCodes.Status201Created)]
    public async Task<ActionResult<EnrollmentResult>> EnrollLearner(
        Guid classId,
        EnrollLearnerCommand command,
        CancellationToken cancellationToken)
    {
        var result = await academicService.EnrollLearnerAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            classId,
            command,
            cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPost("{classId:guid}/enrollments/{enrollmentId:guid}/end")]
    [ProducesResponseType<EnrollmentResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<EnrollmentResult>> EndEnrollment(
        Guid classId,
        Guid enrollmentId,
        EndEnrollmentCommand command,
        CancellationToken cancellationToken)
    {
        var result = await academicService.EndEnrollmentAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            classId,
            enrollmentId,
            command,
            cancellationToken);
        return Ok(result);
    }

    [HttpPost("{classId:guid}/teachers")]
    [ProducesResponseType<TeacherAssignmentResult>(StatusCodes.Status201Created)]
    public async Task<ActionResult<TeacherAssignmentResult>> AssignTeacher(
        Guid classId,
        AssignTeacherCommand command,
        CancellationToken cancellationToken)
    {
        var result = await academicService.AssignTeacherAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            classId,
            command,
            cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPost("{classId:guid}/teachers/{teacherAssignmentId:guid}/end")]
    [ProducesResponseType<TeacherAssignmentResult>(StatusCodes.Status200OK)]
    public async Task<ActionResult<TeacherAssignmentResult>> EndTeacherAssignment(
        Guid classId,
        Guid teacherAssignmentId,
        CancellationToken cancellationToken)
    {
        var result = await academicService.EndTeacherAssignmentAsync(
            AuthenticatedSession.GetUserId(User),
            AuthenticatedSession.GetSessionId(User),
            classId,
            teacherAssignmentId,
            cancellationToken);
        return Ok(result);
    }
}
