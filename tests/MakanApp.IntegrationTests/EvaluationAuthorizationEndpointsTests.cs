using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Assessment;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class AssignmentEndpointsTests
{
    [Fact]
    public async Task AssignedTeacherCanOpenSubmittedAttemptAndSeeItInQueue()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateEvaluationScenarioAsync(managerClient, studentClient);
        using var teacherClient = _factory.CreateClient();
        _ = await CreateTeacherAsync(
            teacherClient,
            scenario.Submission.OrganizationId,
            scenario.Submission.ClassId,
            assigned: true);

        var submission = await teacherClient.GetFromJsonAsync<SubmissionForEvaluationResult>(
            $"/api/v1/academic/submission-attempts/{scenario.Attempt.Id}/evaluation/submission",
            JsonOptions);
        var queue = await teacherClient.GetFromJsonAsync<EvaluationQueueItemResult[]>(
            $"/api/v1/academic/evaluations/queue?assignmentId={scenario.Submission.Assignment.Id}",
            JsonOptions);

        Assert.NotNull(submission);
        Assert.Equal("Final student answer", submission.AnswerText);
        Assert.Contains(queue!, item => item.SubmissionAttemptId == scenario.Attempt.Id);
    }

    [Fact]
    public async Task UnassignedTeacherCannotEvaluateSubmittedAttempt()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateEvaluationScenarioAsync(managerClient, studentClient);
        using var teacherClient = _factory.CreateClient();
        _ = await CreateTeacherAsync(
            teacherClient,
            scenario.Submission.OrganizationId,
            scenario.Submission.ClassId,
            assigned: false);

        using var response = await teacherClient.PutAsJsonAsync(
            $"/api/v1/academic/submission-attempts/{scenario.Attempt.Id}/evaluation",
            new SaveEvaluationDraftCommand(18m, null, null, null, null),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(AssessmentErrorCodes.TeacherNotAssigned, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task TeacherAssignedToAnotherClassCannotEvaluateAttempt()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateEvaluationScenarioAsync(managerClient, studentClient);
        var otherClass = await _factory.CreateAcademicClassAsync(scenario.Submission.OrganizationId);
        using var teacherClient = _factory.CreateClient();
        _ = await CreateTeacherAsync(
            teacherClient,
            scenario.Submission.OrganizationId,
            otherClass.ClassId,
            assigned: true);

        using var response = await teacherClient.GetAsync(
            $"/api/v1/academic/submission-attempts/{scenario.Attempt.Id}/evaluation/submission");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(AssessmentErrorCodes.TeacherNotAssigned, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task TeacherCannotEvaluateAnotherOrganizationsSubmission()
    {
        using var firstManagerClient = _factory.CreateClient();
        var firstManager = await CreateManagerAsync(firstManagerClient);
        var firstClass = await _factory.CreateAcademicClassAsync(firstManager.OrganizationId);
        using var teacherClient = _factory.CreateClient();
        _ = await CreateTeacherAsync(
            teacherClient,
            firstManager.OrganizationId,
            firstClass.ClassId,
            assigned: true);
        using var secondManagerClient = _factory.CreateClient();
        using var secondStudentClient = _factory.CreateClient();
        var secondScenario = await CreateEvaluationScenarioAsync(
            secondManagerClient,
            secondStudentClient);

        using var response = await teacherClient.GetAsync(
            $"/api/v1/academic/submission-attempts/{secondScenario.Attempt.Id}/evaluation/submission");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(AssessmentErrorCodes.EvaluationNotFound, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task DraftSubmissionAttemptCannotBeEvaluated()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateEvaluationScenarioAsync(
            managerClient,
            studentClient,
            submit: false);

        using var response = await managerClient.PutAsJsonAsync(
            $"/api/v1/academic/submission-attempts/{scenario.Attempt.Id}/evaluation",
            new SaveEvaluationDraftCommand(18m, null, null, null, null),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(
            AssessmentErrorCodes.EvaluationSubmissionNotFinal,
            await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task EvaluationQueueNeverContainsStudentDrafts()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateEvaluationScenarioAsync(
            managerClient,
            studentClient,
            submit: false);

        var queue = await managerClient.GetFromJsonAsync<EvaluationQueueItemResult[]>(
            $"/api/v1/academic/evaluations/queue?assignmentId={scenario.Submission.Assignment.Id}",
            JsonOptions);

        Assert.Empty(queue!);
    }

    [Fact]
    public async Task StudentCannotCreateOrUpdateEvaluation()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateEvaluationScenarioAsync(managerClient, studentClient);

        using var response = await studentClient.PutAsJsonAsync(
            $"/api/v1/academic/submission-attempts/{scenario.Attempt.Id}/evaluation",
            new SaveEvaluationDraftCommand(20m, "tampered", null, null, null),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(AssessmentErrorCodes.EvaluationNotAllowed, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task ParentCannotCreateOrUpdateEvaluation()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateEvaluationScenarioAsync(managerClient, studentClient);
        using var parentClient = _factory.CreateClient();
        _ = await CreateParentForStudentAsync(parentClient, scenario.Submission);

        using var response = await parentClient.PutAsJsonAsync(
            $"/api/v1/academic/submission-attempts/{scenario.Attempt.Id}/evaluation",
            new SaveEvaluationDraftCommand(20m, "tampered", null, null, null),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(AssessmentErrorCodes.EvaluationNotAllowed, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task ManagerCannotEvaluateAnotherOrganizationsSubmission()
    {
        using var ownerManagerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateEvaluationScenarioAsync(ownerManagerClient, studentClient);
        using var attackerManagerClient = _factory.CreateClient();
        _ = await CreateManagerAsync(attackerManagerClient);

        using var response = await attackerManagerClient.PutAsJsonAsync(
            $"/api/v1/academic/submission-attempts/{scenario.Attempt.Id}/evaluation",
            new SaveEvaluationDraftCommand(18m, null, null, null, null),
            JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(AssessmentErrorCodes.EvaluationNotFound, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task TeacherQueueContainsOnlyAssignedClasses()
    {
        using var managerClient = _factory.CreateClient();
        using var firstStudentClient = _factory.CreateClient();
        var assignedScenario = await CreateEvaluationScenarioAsync(managerClient, firstStudentClient);
        var otherClass = await _factory.CreateAcademicClassAsync(assignedScenario.Submission.OrganizationId);
        using var teacherClient = _factory.CreateClient();
        _ = await CreateTeacherAsync(
            teacherClient,
            assignedScenario.Submission.OrganizationId,
            assignedScenario.Submission.ClassId,
            assigned: true);

        var queue = await teacherClient.GetFromJsonAsync<EvaluationQueueItemResult[]>(
            "/api/v1/academic/evaluations/queue",
            JsonOptions);

        Assert.Contains(queue!, item => item.SubmissionAttemptId == assignedScenario.Attempt.Id);
        Assert.DoesNotContain(queue!, item => item.ClassId == otherClass.ClassId);
    }
}
