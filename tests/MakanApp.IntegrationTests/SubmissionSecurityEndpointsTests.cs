using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Assessment;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Storage;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class AssignmentEndpointsTests
{
    [Theory]
    [InlineData(FileAssetStatus.Pending)]
    [InlineData(FileAssetStatus.Rejected)]
    [InlineData(FileAssetStatus.Deleted)]
    public async Task NonReadyAttachedFileCannotBeFinalized(FileAssetStatus status)
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateSubmissionScenarioAsync(managerClient, studentClient);
        var attempt = await CreateSubmissionDraftAsync(studentClient, scenario.Assignment.Id);
        var file = await UploadSubmissionFileAsync(studentClient);
        attempt = await AttachSubmissionFileAsync(studentClient, attempt, file.Id);
        await _factory.SetFileAssetStatusAsync(file.Id, status);

        using var response = await studentClient.PostAsJsonAsync(
            $"/api/v1/academic/submission-attempts/{attempt.Id}/submit",
            new FinalSubmitAssignmentCommand(attempt.RowVersion),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(
            AssessmentErrorCodes.SubmissionFileNotReady,
            await ReadProblemCodeAsync(response));
        Assert.Equal(SubmissionAttemptStatus.Draft,
            (await _factory.GetSubmissionAttemptAsync(attempt.Id)).Status);
    }

    [Fact]
    public async Task AnotherUsersFileAssetCannotBeAttached()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateSubmissionScenarioAsync(managerClient, studentClient);
        var attempt = await CreateSubmissionDraftAsync(studentClient, scenario.Assignment.Id);
        using var otherClient = _factory.CreateClient();
        var otherUser = await CreateAuthenticatedUserAsync(otherClient);
        UseBearerToken(otherClient, otherUser.AccessToken);
        var foreignFile = await UploadSubmissionFileAsync(otherClient, "foreign.txt");

        using var response = await studentClient.PostAsJsonAsync(
            $"/api/v1/academic/submission-attempts/{attempt.Id}/attachments",
            new AttachSubmissionFileCommand(foreignFile.Id, attempt.RowVersion),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(
            AssessmentErrorCodes.SubmissionFileNotAllowed,
            await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task CrossOrganizationFileAssetCannotBeAttachedBySameUser()
    {
        using var firstManagerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var first = await CreateSubmissionScenarioAsync(firstManagerClient, studentClient);
        var firstOrganizationFile = await UploadSubmissionFileAsync(studentClient, "org-a.txt");
        using var secondManagerClient = _factory.CreateClient();
        var secondManager = await CreateManagerAsync(secondManagerClient);
        var secondClass = await _factory.CreateAcademicClassAsync(secondManager.OrganizationId);
        var personId = await _factory.GetUserPersonIdAsync(first.Student.UserId);
        var secondOrganizationPerson = await _factory.CreateOrganizationPersonAsync(
            secondManager.OrganizationId,
            personId);
        var secondMembership = await _factory.CreateMembershipAsync(
            first.Student.UserId,
            secondManager.OrganizationId,
            OrganizationRole.Student);
        _ = await _factory.CreateEnrollmentAsync(
            secondManager.OrganizationId,
            secondClass.ClassId,
            secondOrganizationPerson);
        var secondDraft = await CreateDraftAsync(secondManagerClient, secondClass.ClassId);
        var secondAssignment = await PublishAsync(secondManagerClient, secondDraft);
        await SelectStudentWorkspaceAsync(studentClient, secondMembership.MembershipId);
        var attempt = await CreateSubmissionDraftAsync(
            studentClient,
            secondAssignment.Assignment.Id);

        using var response = await studentClient.PostAsJsonAsync(
            $"/api/v1/academic/submission-attempts/{attempt.Id}/attachments",
            new AttachSubmissionFileCommand(firstOrganizationFile.Id, attempt.RowVersion),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(
            AssessmentErrorCodes.SubmissionFileNotAllowed,
            await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task SubmittedAttachmentCannotBeRemoved()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateSubmissionScenarioAsync(managerClient, studentClient);
        var attempt = await CreateSubmissionDraftAsync(studentClient, scenario.Assignment.Id);
        var file = await UploadSubmissionFileAsync(studentClient);
        attempt = await AttachSubmissionFileAsync(studentClient, attempt, file.Id);
        var receipt = await SubmitAsync(studentClient, attempt);

        using var response = await studentClient.DeleteAsync(
            $"/api/v1/academic/submission-attempts/{attempt.Id}/attachments/{file.Id}?expectedRowVersion={Uri.EscapeDataString(receipt.RowVersion)}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(
            AssessmentErrorCodes.SubmissionNotDraft,
            await ReadProblemCodeAsync(response));
        Assert.Equal(1, await _factory.CountSubmissionAttachmentsAsync(attempt.Id));
    }

    [Fact]
    public async Task StudentCannotModifySubmittedAnswer()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateSubmissionScenarioAsync(managerClient, studentClient);
        var attempt = await SaveSubmissionDraftAsync(
            studentClient,
            await CreateSubmissionDraftAsync(studentClient, scenario.Assignment.Id),
            "original");
        var receipt = await SubmitAsync(studentClient, attempt);

        using var response = await studentClient.PatchAsJsonAsync(
            $"/api/v1/academic/submission-attempts/{attempt.Id}/draft",
            new SaveSubmissionDraftCommand("replacement", receipt.RowVersion),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("original", (await _factory.GetSubmissionAttemptAsync(attempt.Id)).AnswerText);
    }

    [Fact]
    public async Task StudentCannotAttachNewFileAfterSubmission()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateSubmissionScenarioAsync(managerClient, studentClient);
        var attempt = await SaveSubmissionDraftAsync(
            studentClient,
            await CreateSubmissionDraftAsync(studentClient, scenario.Assignment.Id));
        var receipt = await SubmitAsync(studentClient, attempt);
        var file = await UploadSubmissionFileAsync(studentClient, "after-submit.txt");

        using var response = await studentClient.PostAsJsonAsync(
            $"/api/v1/academic/submission-attempts/{attempt.Id}/attachments",
            new AttachSubmissionFileCommand(file.Id, receipt.RowVersion),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(
            AssessmentErrorCodes.SubmissionNotDraft,
            await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task TeacherCannotModifyStudentSubmission()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateSubmissionScenarioAsync(managerClient, studentClient);
        var attempt = await CreateSubmissionDraftAsync(studentClient, scenario.Assignment.Id);
        using var teacherClient = _factory.CreateClient();
        _ = await CreateTeacherAsync(
            teacherClient,
            scenario.OrganizationId,
            scenario.ClassId,
            assigned: true);

        using var response = await teacherClient.PatchAsJsonAsync(
            $"/api/v1/academic/submission-attempts/{attempt.Id}/draft",
            new SaveSubmissionDraftCommand("teacher edit", attempt.RowVersion),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(
            AssessmentErrorCodes.SubmissionNotAllowed,
            await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task AssignedTeacherCanReadFinalizedSubmissionAndReviewQueue()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateSubmissionScenarioAsync(managerClient, studentClient);
        var attempt = await SaveSubmissionDraftAsync(
            studentClient,
            await CreateSubmissionDraftAsync(studentClient, scenario.Assignment.Id));
        _ = await SubmitAsync(studentClient, attempt);
        using var teacherClient = _factory.CreateClient();
        _ = await CreateTeacherAsync(
            teacherClient,
            scenario.OrganizationId,
            scenario.ClassId,
            assigned: true);

        var detail = await teacherClient.GetFromJsonAsync<SubmissionAttemptResult>(
            $"/api/v1/academic/submission-attempts/{attempt.Id}",
            JsonOptions);
        var list = await teacherClient.GetFromJsonAsync<SubmissionAttemptResult[]>(
            $"/api/v1/academic/assignments/{scenario.Assignment.Id}/submitted-attempts",
            JsonOptions);

        Assert.True(detail!.ContentVisible);
        Assert.Equal("Student answer", detail.AnswerText);
        Assert.Contains(list!, item => item.Id == attempt.Id);
    }

    [Fact]
    public async Task UnassignedTeacherCannotReadFinalizedSubmission()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateSubmissionScenarioAsync(managerClient, studentClient);
        var attempt = await SaveSubmissionDraftAsync(
            studentClient,
            await CreateSubmissionDraftAsync(studentClient, scenario.Assignment.Id));
        _ = await SubmitAsync(studentClient, attempt);
        using var teacherClient = _factory.CreateClient();
        _ = await CreateTeacherAsync(
            teacherClient,
            scenario.OrganizationId,
            scenario.ClassId,
            assigned: false);

        using var detail = await teacherClient.GetAsync(
            $"/api/v1/academic/submission-attempts/{attempt.Id}");
        using var list = await teacherClient.GetAsync(
            $"/api/v1/academic/assignments/{scenario.Assignment.Id}/submitted-attempts");

        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, list.StatusCode);
    }

    [Fact]
    public async Task ManagerCannotReadAnotherOrganizationsSubmission()
    {
        using var firstManagerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateSubmissionScenarioAsync(firstManagerClient, studentClient);
        var attempt = await SaveSubmissionDraftAsync(
            studentClient,
            await CreateSubmissionDraftAsync(studentClient, scenario.Assignment.Id));
        _ = await SubmitAsync(studentClient, attempt);
        using var otherManagerClient = _factory.CreateClient();
        _ = await CreateManagerAsync(otherManagerClient);

        using var response = await otherManagerClient.GetAsync(
            $"/api/v1/academic/submission-attempts/{attempt.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ParentCannotSubmitOnBehalfOfChild()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateSubmissionScenarioAsync(managerClient, studentClient);
        var attempt = await SaveSubmissionDraftAsync(
            studentClient,
            await CreateSubmissionDraftAsync(studentClient, scenario.Assignment.Id));
        using var parentClient = _factory.CreateClient();
        await CreateParentForStudentAsync(parentClient, scenario);

        using var response = await parentClient.PostAsJsonAsync(
            $"/api/v1/academic/submission-attempts/{attempt.Id}/submit",
            new FinalSubmitAssignmentCommand(attempt.RowVersion),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(0, await _factory.CountSubmittedAttemptsAsync(scenario.Assignment.Id));
    }

    [Fact]
    public async Task ParentCanReadReceiptStatusButNotPrivateSubmittedContent()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateSubmissionScenarioAsync(managerClient, studentClient);
        var attempt = await SaveSubmissionDraftAsync(
            studentClient,
            await CreateSubmissionDraftAsync(studentClient, scenario.Assignment.Id),
            "private answer");
        _ = await SubmitAsync(studentClient, attempt);
        using var parentClient = _factory.CreateClient();
        await CreateParentForStudentAsync(parentClient, scenario);

        var result = await parentClient.GetFromJsonAsync<SubmissionAttemptResult>(
            $"/api/v1/academic/submission-attempts/{attempt.Id}",
            JsonOptions);

        Assert.NotNull(result);
        Assert.Equal(SubmissionAttemptStatus.Submitted, result.Status);
        Assert.False(result.ContentVisible);
        Assert.Null(result.AnswerText);
        Assert.Empty(result.Attachments);
    }

    [Fact]
    public async Task ParentCannotReadStudentDraftContent()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateSubmissionScenarioAsync(managerClient, studentClient);
        var attempt = await SaveSubmissionDraftAsync(
            studentClient,
            await CreateSubmissionDraftAsync(studentClient, scenario.Assignment.Id),
            "private draft");
        using var parentClient = _factory.CreateClient();
        await CreateParentForStudentAsync(parentClient, scenario);

        using var response = await parentClient.GetAsync(
            $"/api/v1/academic/submission-attempts/{attempt.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AnotherStudentCannotReadSubmissionByGuessingAttemptId()
    {
        using var managerClient = _factory.CreateClient();
        using var ownerClient = _factory.CreateClient();
        var scenario = await CreateSubmissionScenarioAsync(managerClient, ownerClient);
        var attempt = await SaveSubmissionDraftAsync(
            ownerClient,
            await CreateSubmissionDraftAsync(ownerClient, scenario.Assignment.Id));
        using var otherStudentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(
            otherStudentClient,
            scenario.OrganizationId,
            scenario.ClassId);

        using var response = await otherStudentClient.GetAsync(
            $"/api/v1/academic/submission-attempts/{attempt.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UnauthenticatedSubmissionRequestIsRejected()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsync(
            $"/api/v1/academic/assignments/{Guid.NewGuid()}/attempts",
            null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AuthoritativeClientTamperingFieldsAreIgnored()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateSubmissionScenarioAsync(managerClient, studentClient);
        var attempt = await CreateSubmissionDraftAsync(studentClient, scenario.Assignment.Id);

        using var response = await studentClient.PatchAsJsonAsync(
            $"/api/v1/academic/submission-attempts/{attempt.Id}/draft",
            new
            {
                AnswerText = "trusted content only",
                ExpectedRowVersion = attempt.RowVersion,
                OrganizationId = Guid.NewGuid(),
                AssignmentId = Guid.NewGuid(),
                AssignmentVersionId = Guid.NewGuid(),
                AssignmentRecipientId = Guid.NewGuid(),
                EnrollmentId = Guid.NewGuid(),
                AttemptNumber = 99,
                Status = SubmissionAttemptStatus.Submitted,
                SubmittedAtUtc = DateTime.UtcNow.AddYears(-1),
                IsLate = true,
                Role = OrganizationRole.Manager
            },
            JsonOptions);
        var persisted = await _factory.GetSubmissionAttemptAsync(attempt.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(scenario.OrganizationId, persisted.OrganizationId);
        Assert.Equal(scenario.Assignment.Id, persisted.AssignmentId);
        Assert.Equal(scenario.Assignment.VersionId, persisted.AssignmentVersionId);
        Assert.Equal(scenario.Student.EnrollmentId, persisted.EnrollmentId);
        Assert.Equal(1, persisted.AttemptNumber);
        Assert.Equal(SubmissionAttemptStatus.Draft, persisted.Status);
        Assert.Null(persisted.SubmittedAtUtc);
        Assert.False(persisted.IsLate);
    }
}
