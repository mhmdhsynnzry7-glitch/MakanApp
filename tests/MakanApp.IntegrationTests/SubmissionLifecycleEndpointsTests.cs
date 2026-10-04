using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Assessment;
using MakanApp.Application.Storage;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Storage;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class AssignmentEndpointsTests
{
    [Fact]
    public async Task ValidRecipientCreatesDraftSubmission()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateSubmissionScenarioAsync(managerClient, studentClient);

        var attempt = await CreateSubmissionDraftAsync(studentClient, scenario.Assignment.Id);

        Assert.Equal(SubmissionAttemptStatus.Draft, attempt.Status);
        Assert.Equal(1, attempt.AttemptNumber);
        Assert.Equal(scenario.Assignment.VersionId, attempt.AssignmentVersionId);
        Assert.Equal(scenario.Student.EnrollmentId, attempt.EnrollmentId);
        Assert.Null(attempt.SubmittedAtUtc);
    }

    [Fact]
    public async Task NonRecipientCannotCreateDraftSubmission()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateSubmissionScenarioAsync(
            managerClient,
            studentClient,
            studentIsRecipient: false);

        using var response = await studentClient.PostAsync(
            $"/api/v1/academic/assignments/{scenario.Assignment.Id}/attempts",
            null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(
            AssessmentErrorCodes.AssignmentRecipientNotFound,
            await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task DraftSaveDoesNotMarkAttemptSubmitted()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateSubmissionScenarioAsync(managerClient, studentClient);
        var attempt = await CreateSubmissionDraftAsync(studentClient, scenario.Assignment.Id);

        var saved = await SaveSubmissionDraftAsync(studentClient, attempt, "  saved answer  ");
        var persisted = await _factory.GetSubmissionAttemptAsync(attempt.Id);

        Assert.Equal("saved answer", saved.AnswerText);
        Assert.Equal(SubmissionAttemptStatus.Draft, saved.Status);
        Assert.Equal(SubmissionAttemptStatus.Draft, persisted.Status);
        Assert.Null(persisted.SubmittedAtUtc);
    }

    [Fact]
    public async Task RepeatedDraftCreationResumesSameAttemptAndSqlServerRejectsDuplicateDraft()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateSubmissionScenarioAsync(managerClient, studentClient);

        var first = await CreateSubmissionDraftAsync(studentClient, scenario.Assignment.Id);
        var second = await CreateSubmissionDraftAsync(studentClient, scenario.Assignment.Id);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, await _factory.CountSubmissionAttemptsAsync(scenario.Assignment.Id));
        Assert.True(await _factory.DuplicateDraftIsRejectedBySqlServerAsync(first.Id));
    }

    [Fact]
    public async Task ReadyUploadedFileCanBeAttachedToDraft()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateSubmissionScenarioAsync(managerClient, studentClient);
        var attempt = await CreateSubmissionDraftAsync(studentClient, scenario.Assignment.Id);
        var file = await UploadSubmissionFileAsync(studentClient);

        var attached = await AttachSubmissionFileAsync(studentClient, attempt, file.Id);

        Assert.Equal(FileAssetStatus.Ready, Assert.Single(attached.Attachments).FileStatus);
        Assert.Equal(SubmissionAttemptStatus.Draft, attached.Status);
        Assert.Equal(1, await _factory.CountSubmissionAttachmentsAsync(attempt.Id));
    }

    [Fact]
    public async Task DraftAttachmentCanBeRemovedWithoutDeletingFileAsset()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateSubmissionScenarioAsync(managerClient, studentClient);
        var attempt = await CreateSubmissionDraftAsync(studentClient, scenario.Assignment.Id);
        var file = await UploadSubmissionFileAsync(studentClient);
        var attached = await AttachSubmissionFileAsync(studentClient, attempt, file.Id);

        using var response = await studentClient.DeleteAsync(
            $"/api/v1/academic/submission-attempts/{attempt.Id}/attachments/{file.Id}?expectedRowVersion={Uri.EscapeDataString(attached.RowVersion)}");
        var result = await ReadRequiredAsync<SubmissionAttemptResult>(response);
        var storedFile = await _factory.GetFileAssetAsync(file.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(result.Attachments);
        Assert.Equal(FileAssetStatus.Ready, storedFile.Status);
    }

    [Fact]
    public async Task UploadAloneNeverCreatesOfficialSubmission()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateSubmissionScenarioAsync(managerClient, studentClient);

        var file = await UploadSubmissionFileAsync(studentClient);

        Assert.Equal(FileAssetStatus.Ready, file.Status);
        Assert.Equal(0, await _factory.CountSubmissionAttemptsAsync(scenario.Assignment.Id));
        Assert.Equal(0, await _factory.CountSubmittedAttemptsAsync(scenario.Assignment.Id));
    }

    [Fact]
    public async Task ValidFinalSubmitReturnsDurableServerReceipt()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateSubmissionScenarioAsync(managerClient, studentClient);
        var attempt = await CreateSubmissionDraftAsync(studentClient, scenario.Assignment.Id);
        attempt = await SaveSubmissionDraftAsync(studentClient, attempt);

        var beforeUtc = DateTime.UtcNow;
        var receipt = await SubmitAsync(studentClient, attempt);
        var afterUtc = DateTime.UtcNow;

        Assert.Equal(attempt.Id, receipt.SubmissionAttemptId);
        Assert.Equal(SubmissionAttemptStatus.Submitted, receipt.Status);
        Assert.InRange(receipt.SubmittedAtUtc, beforeUtc, afterUtc);
        Assert.Equal(1, receipt.AttemptNumber);
    }

    [Fact]
    public async Task ReceiptTimestampMatchesPersistedServerTimestamp()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateSubmissionScenarioAsync(managerClient, studentClient);
        var attempt = await SaveSubmissionDraftAsync(
            studentClient,
            await CreateSubmissionDraftAsync(studentClient, scenario.Assignment.Id));

        var receipt = await SubmitAsync(studentClient, attempt);
        var persisted = await _factory.GetSubmissionAttemptAsync(attempt.Id);

        Assert.Equal(receipt.SubmittedAtUtc, persisted.SubmittedAtUtc);
        Assert.Equal(SubmissionAttemptStatus.Submitted, persisted.Status);
    }

    [Fact]
    public async Task FinalSubmitBeforeDeadlineSucceedsWithoutLateFlag()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateSubmissionScenarioAsync(managerClient, studentClient);
        var attempt = await SaveSubmissionDraftAsync(
            studentClient,
            await CreateSubmissionDraftAsync(studentClient, scenario.Assignment.Id));

        var receipt = await SubmitAsync(studentClient, attempt);

        Assert.False(receipt.IsLate);
    }

    [Fact]
    public async Task FinalSubmitAfterDeadlineFailsWhenLateSubmissionIsDisabled()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateSubmissionScenarioAsync(managerClient, studentClient);
        var attempt = await SaveSubmissionDraftAsync(
            studentClient,
            await CreateSubmissionDraftAsync(studentClient, scenario.Assignment.Id));
        await _factory.SetAssignmentDeadlineAsync(
            scenario.Assignment.VersionId,
            DateTime.UtcNow.AddMinutes(-1),
            false);

        using var response = await studentClient.PostAsJsonAsync(
            $"/api/v1/academic/submission-attempts/{attempt.Id}/submit",
            new FinalSubmitAssignmentCommand(attempt.RowVersion),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(
            AssessmentErrorCodes.SubmissionDeadlinePassed,
            await ReadProblemCodeAsync(response));
        Assert.Equal(0, await _factory.CountSubmittedAttemptsAsync(scenario.Assignment.Id));
    }

    [Fact]
    public async Task FinalSubmitAfterDeadlineSucceedsAndIsLateWhenAllowed()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateSubmissionScenarioAsync(
            managerClient,
            studentClient,
            allowLateSubmission: true);
        var attempt = await SaveSubmissionDraftAsync(
            studentClient,
            await CreateSubmissionDraftAsync(studentClient, scenario.Assignment.Id));
        await _factory.SetAssignmentDeadlineAsync(
            scenario.Assignment.VersionId,
            DateTime.UtcNow.AddMinutes(-1),
            true);

        var receipt = await SubmitAsync(studentClient, attempt);

        Assert.True(receipt.IsLate);
    }

    [Fact]
    public async Task MaxAttemptsRejectsCreatingAnotherAttempt()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateSubmissionScenarioAsync(
            managerClient,
            studentClient,
            maxAttempts: 1);
        var first = await SaveSubmissionDraftAsync(
            studentClient,
            await CreateSubmissionDraftAsync(studentClient, scenario.Assignment.Id));
        _ = await SubmitAsync(studentClient, first);

        using var response = await studentClient.PostAsync(
            $"/api/v1/academic/assignments/{scenario.Assignment.Id}/attempts",
            null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(
            AssessmentErrorCodes.SubmissionAttemptsExhausted,
            await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task NewAttemptGetsSequentialNumberAndPreservesPreviousSubmission()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateSubmissionScenarioAsync(
            managerClient,
            studentClient,
            maxAttempts: 2);
        var first = await SaveSubmissionDraftAsync(
            studentClient,
            await CreateSubmissionDraftAsync(studentClient, scenario.Assignment.Id),
            "first answer");
        _ = await SubmitAsync(studentClient, first);

        var second = await CreateSubmissionDraftAsync(studentClient, scenario.Assignment.Id);
        var persistedFirst = await _factory.GetSubmissionAttemptAsync(first.Id);

        Assert.Equal(2, second.AttemptNumber);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal("first answer", persistedFirst.AnswerText);
        Assert.Equal(SubmissionAttemptStatus.Submitted, persistedFirst.Status);
    }

    [Fact]
    public async Task EmptyFinalSubmissionIsRejected()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateSubmissionScenarioAsync(managerClient, studentClient);
        var attempt = await CreateSubmissionDraftAsync(studentClient, scenario.Assignment.Id);

        using var response = await studentClient.PostAsJsonAsync(
            $"/api/v1/academic/submission-attempts/{attempt.Id}/submit",
            new FinalSubmitAssignmentCommand(attempt.RowVersion),
            JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(AssessmentErrorCodes.SubmissionEmpty, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task SubmittedAttachmentIsRetainedAndCannotBeDeletedFromStorage()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateSubmissionScenarioAsync(managerClient, studentClient);
        var attempt = await CreateSubmissionDraftAsync(studentClient, scenario.Assignment.Id);
        var file = await UploadSubmissionFileAsync(studentClient);
        attempt = await AttachSubmissionFileAsync(studentClient, attempt, file.Id);
        _ = await SubmitAsync(studentClient, attempt);
        var metadata = await studentClient.GetFromJsonAsync<FileAssetResult>(
            $"/api/v1/files/{file.Id}",
            JsonOptions);

        using var response = await studentClient.DeleteAsync(
            $"/api/v1/files/{file.Id}?expectedRowVersion={Uri.EscapeDataString(metadata!.RowVersion)}");
        var stored = await _factory.GetFileAssetAsync(file.Id);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(StorageErrorCodes.FileInUse, await ReadProblemCodeAsync(response));
        Assert.NotNull(stored.RetainedAtUtc);
        Assert.Equal(FileAssetStatus.Ready, stored.Status);
    }

    [Fact]
    public async Task FailedFinalSubmitDoesNotRetainAttachedFile()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateSubmissionScenarioAsync(managerClient, studentClient);
        var attempt = await CreateSubmissionDraftAsync(studentClient, scenario.Assignment.Id);
        var file = await UploadSubmissionFileAsync(studentClient);
        attempt = await AttachSubmissionFileAsync(studentClient, attempt, file.Id);
        await _factory.SetAssignmentDeadlineAsync(
            scenario.Assignment.VersionId,
            DateTime.UtcNow.AddMinutes(-1),
            false);

        using var response = await studentClient.PostAsJsonAsync(
            $"/api/v1/academic/submission-attempts/{attempt.Id}/submit",
            new FinalSubmitAssignmentCommand(attempt.RowVersion),
            JsonOptions);
        var stored = await _factory.GetFileAssetAsync(file.Id);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Null(stored.RetainedAtUtc);
        Assert.Equal(SubmissionAttemptStatus.Draft,
            (await _factory.GetSubmissionAttemptAsync(attempt.Id)).Status);
    }
}
