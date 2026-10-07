using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Assessment;
using MakanApp.Domain.Assessment;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class ExamEndpointsTests
{
    [Fact]
    public async Task OwningStudentFinalizesAcceptedAnswersAndGetsDurableReceipt()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient, questionCount: 2);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, setup.Manager.OrganizationId, setup.ClassId);
        var attempt = await StartAttemptAsync(studentClient, setup.Exam.Id);
        var lease = await AcquireLeaseAsync(studentClient, attempt.ExamAttemptId);
        var objective = attempt.Questions.Single(question => question.Options.Count > 0);
        var saved = await SaveAnswerAsync(
            studentClient,
            attempt.ExamAttemptId,
            objective.AttemptQuestionId,
            new SaveExamAnswerCommand(
                Guid.NewGuid(),
                lease.WriteLeaseVersion,
                null,
                objective.Options.First().Id,
                null));
        var beforeUtc = DateTime.UtcNow;

        var receipt = await FinalizeAsync(
            studentClient,
            attempt.ExamAttemptId,
            new FinalizeExamCommand(Guid.NewGuid(), 1, lease.WriteLeaseVersion));

        var afterUtc = DateTime.UtcNow;
        Assert.Equal(ExamAttemptStatus.Finalized, receipt.Status);
        Assert.Equal(attempt.ExamAttemptId, receipt.ExamAttemptId);
        Assert.Equal(attempt.ExamVersionId, receipt.ExamVersionId);
        Assert.Equal(1, receipt.FinalizedAnswerSetVersion);
        Assert.Equal(1, receipt.AnsweredQuestionCount);
        Assert.Equal(2, receipt.TotalQuestionCount);
        Assert.InRange(receipt.FinalizedAtUtc, beforeUtc, afterUtc);
        Assert.True(receipt.FinalizedAtUtc < attempt.EffectiveDeadlineUtc);

        var state = await _factory.GetExamFinalizationStateAsync(attempt.ExamAttemptId);
        var finalAnswer = Assert.Single(state.FinalAnswers);
        Assert.Equal(ExamAttemptStatus.Finalized, state.Status);
        Assert.Equal(receipt.FinalizedAtUtc, state.FinalizedAtUtc);
        Assert.Equal(1, state.FinalizedAnswerSetVersion);
        Assert.Equal(objective.AttemptQuestionId, finalAnswer.ExamAttemptQuestionId);
        Assert.Equal(saved.AnswerRevisionId, finalAnswer.AnswerRevisionId);

        var fetched = await studentClient.GetFromJsonAsync<ExamFinalReceiptDto>(
            $"/api/v1/academic/exam-attempts/{attempt.ExamAttemptId}/receipt",
            JsonOptions);
        Assert.Equal(receipt, fetched);
    }

    [Fact]
    public async Task UnansweredAttemptCanFinalizeWithZeroAnsweredCount()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient, questionCount: 2);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, setup.Manager.OrganizationId, setup.ClassId);
        var attempt = await StartAttemptAsync(studentClient, setup.Exam.Id);
        var lease = await AcquireLeaseAsync(studentClient, attempt.ExamAttemptId);

        var receipt = await FinalizeAsync(
            studentClient,
            attempt.ExamAttemptId,
            new FinalizeExamCommand(Guid.NewGuid(), 0, lease.WriteLeaseVersion));

        Assert.Equal(0, receipt.AnsweredQuestionCount);
        Assert.Equal(2, receipt.TotalQuestionCount);
        Assert.Empty((await _factory.GetExamFinalizationStateAsync(attempt.ExamAttemptId)).FinalAnswers);
    }

    [Fact]
    public async Task UnrelatedStudentParentTeacherAndManagerCannotFinalizeStudentAttempt()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient);
        using var ownerClient = _factory.CreateClient();
        _ = await CreateStudentAsync(ownerClient, setup.Manager.OrganizationId, setup.ClassId);
        var attempt = await StartAttemptAsync(ownerClient, setup.Exam.Id);
        var lease = await AcquireLeaseAsync(ownerClient, attempt.ExamAttemptId);
        var command = new FinalizeExamCommand(Guid.NewGuid(), 0, lease.WriteLeaseVersion);
        using var otherStudentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(otherStudentClient, setup.Manager.OrganizationId, setup.ClassId);
        using var parentClient = _factory.CreateClient();
        _ = await CreateParentAsync(parentClient, setup.Manager.OrganizationId, setup.ClassId);
        using var teacherClient = _factory.CreateClient();
        _ = await CreateTeacherAsync(teacherClient, setup.Manager.OrganizationId, setup.ClassId, assigned: true);

        using var unrelated = await SendFinalizeAsync(
            otherStudentClient,
            attempt.ExamAttemptId,
            command);
        using var parent = await SendFinalizeAsync(parentClient, attempt.ExamAttemptId, command);
        using var teacher = await SendFinalizeAsync(teacherClient, attempt.ExamAttemptId, command);
        using var manager = await SendFinalizeAsync(managerClient, attempt.ExamAttemptId, command);

        Assert.Equal(HttpStatusCode.NotFound, unrelated.StatusCode);
        Assert.Equal(AssessmentErrorCodes.ExamAttemptNotFound, await ReadProblemCodeAsync(unrelated));
        foreach (var response in new[] { parent, teacher, manager })
        {
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal(AssessmentErrorCodes.ExamAttemptNotAllowed, await ReadProblemCodeAsync(response));
        }
    }

    [Fact]
    public async Task UnauthenticatedFinalizeIsRejected()
    {
        using var client = _factory.CreateClient();

        using var response = await SendFinalizeAsync(
            client,
            Guid.NewGuid(),
            new FinalizeExamCommand(Guid.NewGuid(), 0, 1));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ReceiptBeforeFinalizeIsRejected()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, setup.Manager.OrganizationId, setup.ClassId);
        var attempt = await StartAttemptAsync(studentClient, setup.Exam.Id);

        using var response = await studentClient.GetAsync(
            $"/api/v1/academic/exam-attempts/{attempt.ExamAttemptId}/receipt");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(AssessmentErrorCodes.ExamAttemptNotFinalizable, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task FinalizeLostAcknowledgementRetryAndCompatibleRetryReturnSameReceipt()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, setup.Manager.OrganizationId, setup.ClassId);
        var attempt = await StartAttemptAsync(studentClient, setup.Exam.Id);
        var lease = await AcquireLeaseAsync(studentClient, attempt.ExamAttemptId);
        var operationId = Guid.NewGuid();
        var command = new FinalizeExamCommand(operationId, 0, lease.WriteLeaseVersion);

        var first = await FinalizeAsync(studentClient, attempt.ExamAttemptId, command);
        var lostAcknowledgementRetry = await FinalizeAsync(studentClient, attempt.ExamAttemptId, command);
        var compatibleRetry = await FinalizeAsync(
            studentClient,
            attempt.ExamAttemptId,
            command with { ClientOperationId = Guid.NewGuid() });

        Assert.Equal(first, lostAcknowledgementRetry);
        Assert.Equal(first, compatibleRetry);
        var state = await _factory.GetExamFinalizationStateAsync(attempt.ExamAttemptId);
        Assert.Equal(operationId, state.FinalizeClientOperationId);
        Assert.Empty(state.FinalAnswers);
    }

    [Fact]
    public async Task SameFinalizeOperationWithDifferentPayloadConflicts()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, setup.Manager.OrganizationId, setup.ClassId);
        var attempt = await StartAttemptAsync(studentClient, setup.Exam.Id);
        var lease = await AcquireLeaseAsync(studentClient, attempt.ExamAttemptId);
        var operationId = Guid.NewGuid();
        _ = await FinalizeAsync(
            studentClient,
            attempt.ExamAttemptId,
            new FinalizeExamCommand(operationId, 0, lease.WriteLeaseVersion));

        using var response = await SendFinalizeAsync(
            studentClient,
            attempt.ExamAttemptId,
            new FinalizeExamCommand(operationId, 1, lease.WriteLeaseVersion));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(
            AssessmentErrorCodes.ExamFinalizeIdempotencyConflict,
            await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task IncompatibleRetryAgainstAlreadyFinalizedAttemptConflicts()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, setup.Manager.OrganizationId, setup.ClassId);
        var attempt = await StartAttemptAsync(studentClient, setup.Exam.Id);
        var lease = await AcquireLeaseAsync(studentClient, attempt.ExamAttemptId);
        _ = await FinalizeAsync(
            studentClient,
            attempt.ExamAttemptId,
            new FinalizeExamCommand(Guid.NewGuid(), 0, lease.WriteLeaseVersion));

        using var response = await SendFinalizeAsync(
            studentClient,
            attempt.ExamAttemptId,
            new FinalizeExamCommand(Guid.NewGuid(), 1, lease.WriteLeaseVersion));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(AssessmentErrorCodes.ExamAlreadyFinalized, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task StaleExpectedAnswerSetVersionCannotFinalize()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, setup.Manager.OrganizationId, setup.ClassId);
        var attempt = await StartAttemptAsync(studentClient, setup.Exam.Id);
        var lease = await AcquireLeaseAsync(studentClient, attempt.ExamAttemptId);
        var question = Assert.Single(attempt.Questions);
        _ = await SaveAnswerAsync(
            studentClient,
            attempt.ExamAttemptId,
            question.AttemptQuestionId,
            new SaveExamAnswerCommand(
                Guid.NewGuid(),
                lease.WriteLeaseVersion,
                null,
                question.Options.First().Id,
                null));

        using var response = await SendFinalizeAsync(
            studentClient,
            attempt.ExamAttemptId,
            new FinalizeExamCommand(Guid.NewGuid(), 0, lease.WriteLeaseVersion));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(
            AssessmentErrorCodes.ExamAnswerSetVersionConflict,
            await ReadProblemCodeAsync(response));
        var state = await _factory.GetExamFinalizationStateAsync(attempt.ExamAttemptId);
        Assert.Equal(ExamAttemptStatus.InProgress, state.Status);
        Assert.Empty(state.FinalAnswers);
    }

    [Fact]
    public async Task FinalizedAttemptRejectsSaveAcquireAndTransferWriteLease()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, setup.Manager.OrganizationId, setup.ClassId);
        var attempt = await StartAttemptAsync(studentClient, setup.Exam.Id);
        var lease = await AcquireLeaseAsync(studentClient, attempt.ExamAttemptId);
        _ = await FinalizeAsync(
            studentClient,
            attempt.ExamAttemptId,
            new FinalizeExamCommand(Guid.NewGuid(), 0, lease.WriteLeaseVersion));
        var question = Assert.Single(attempt.Questions);

        using var save = await SendSaveAsync(
            studentClient,
            attempt.ExamAttemptId,
            question.AttemptQuestionId,
            new SaveExamAnswerCommand(
                Guid.NewGuid(),
                lease.WriteLeaseVersion,
                null,
                question.Options.First().Id,
                null));
        using var acquire = await studentClient.PostAsync(
            $"/api/v1/academic/exam-attempts/{attempt.ExamAttemptId}/write-lease",
            null);
        using var transfer = await studentClient.PostAsync(
            $"/api/v1/academic/exam-attempts/{attempt.ExamAttemptId}/write-lease/transfer",
            null);

        foreach (var response in new[] { save, acquire, transfer })
        {
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal(AssessmentErrorCodes.ExamAttemptNotWritable, await ReadProblemCodeAsync(response));
        }
    }

    [Fact]
    public async Task FinalizeAfterDeadlineRejectsClientLocalTimestampAndExpiresAttempt()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, setup.Manager.OrganizationId, setup.ClassId);
        var attempt = await StartAttemptAsync(studentClient, setup.Exam.Id);
        var lease = await AcquireLeaseAsync(studentClient, attempt.ExamAttemptId);
        await _factory.ExpireExamAttemptAsync(attempt.ExamAttemptId);

        using var response = await studentClient.PostAsJsonAsync(
            $"/api/v1/academic/exam-attempts/{attempt.ExamAttemptId}/finalize",
            new
            {
                ClientOperationId = Guid.NewGuid(),
                ExpectedAnswerSetVersion = 0,
                WriteLeaseVersion = lease.WriteLeaseVersion,
                CreatedAtLocal = DateTime.UtcNow.AddDays(-1)
            },
            JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(AssessmentErrorCodes.ExamDeadlinePassed, await ReadProblemCodeAsync(response));
        var state = await _factory.GetExamFinalizationStateAsync(attempt.ExamAttemptId);
        Assert.Equal(ExamAttemptStatus.Expired, state.Status);
        Assert.Null(state.FinalizedAtUtc);
        Assert.Empty(state.FinalAnswers);
    }

    [Fact]
    public async Task FinalizeAtExactDeadlineIsRejectedByControlledServerClock()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, setup.Manager.OrganizationId, setup.ClassId);
        var attempt = await StartAttemptAsync(studentClient, setup.Exam.Id);
        var lease = await AcquireLeaseAsync(studentClient, attempt.ExamAttemptId);

        try
        {
            _factory.Clock.SetUtcNow(attempt.EffectiveDeadlineUtc);
            using var response = await SendFinalizeAsync(
                studentClient,
                attempt.ExamAttemptId,
                new FinalizeExamCommand(Guid.NewGuid(), 0, lease.WriteLeaseVersion));

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal(AssessmentErrorCodes.ExamDeadlinePassed, await ReadProblemCodeAsync(response));
        }
        finally
        {
            _factory.Clock.UseSystemTime();
        }
    }

    [Fact]
    public async Task FinalAnswerSnapshotKeepsExactCurrentRevisionsImmutable()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient, questionCount: 2);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, setup.Manager.OrganizationId, setup.ClassId);
        var attempt = await StartAttemptAsync(studentClient, setup.Exam.Id);
        var lease = await AcquireLeaseAsync(studentClient, attempt.ExamAttemptId);
        var objective = attempt.Questions.Single(question => question.Options.Count > 0);
        var descriptive = attempt.Questions.Single(question => question.Options.Count == 0);
        var firstObjective = await SaveAnswerAsync(
            studentClient,
            attempt.ExamAttemptId,
            objective.AttemptQuestionId,
            new SaveExamAnswerCommand(
                Guid.NewGuid(),
                lease.WriteLeaseVersion,
                null,
                objective.Options.First().Id,
                null));
        var currentObjective = await SaveAnswerAsync(
            studentClient,
            attempt.ExamAttemptId,
            objective.AttemptQuestionId,
            new SaveExamAnswerCommand(
                Guid.NewGuid(),
                lease.WriteLeaseVersion,
                firstObjective.RevisionNumber,
                objective.Options.Last().Id,
                null));
        var currentDescriptive = await SaveAnswerAsync(
            studentClient,
            attempt.ExamAttemptId,
            descriptive.AttemptQuestionId,
            new SaveExamAnswerCommand(
                Guid.NewGuid(),
                lease.WriteLeaseVersion,
                null,
                null,
                "پاسخ نهایی"));

        _ = await FinalizeAsync(
            studentClient,
            attempt.ExamAttemptId,
            new FinalizeExamCommand(Guid.NewGuid(), 3, lease.WriteLeaseVersion));
        using var laterSave = await SendSaveAsync(
            studentClient,
            attempt.ExamAttemptId,
            objective.AttemptQuestionId,
            new SaveExamAnswerCommand(
                Guid.NewGuid(),
                lease.WriteLeaseVersion,
                currentObjective.RevisionNumber,
                objective.Options.First().Id,
                null));

        Assert.Equal(HttpStatusCode.Conflict, laterSave.StatusCode);
        var finalAnswers = (await _factory.GetExamFinalizationStateAsync(attempt.ExamAttemptId))
            .FinalAnswers
            .ToDictionary(answer => answer.ExamAttemptQuestionId);
        Assert.Equal(2, finalAnswers.Count);
        Assert.Equal(currentObjective.AnswerRevisionId, finalAnswers[objective.AttemptQuestionId].AnswerRevisionId);
        Assert.Equal(currentDescriptive.AnswerRevisionId, finalAnswers[descriptive.AttemptQuestionId].AnswerRevisionId);
        Assert.DoesNotContain(
            finalAnswers.Values,
            answer => answer.AnswerRevisionId == firstObjective.AnswerRevisionId);
    }

    [Fact]
    public async Task ConcurrentCompatibleFinalizeRequestsProduceOneDurableFinalization()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, setup.Manager.OrganizationId, setup.ClassId);
        var attempt = await StartAttemptAsync(studentClient, setup.Exam.Id);
        var lease = await AcquireLeaseAsync(studentClient, attempt.ExamAttemptId);
        var operationId = Guid.NewGuid();
        var command = new FinalizeExamCommand(operationId, 0, lease.WriteLeaseVersion);

        var responses = await Task.WhenAll(
            SendFinalizeAsync(studentClient, attempt.ExamAttemptId, command),
            SendFinalizeAsync(studentClient, attempt.ExamAttemptId, command),
            SendFinalizeAsync(studentClient, attempt.ExamAttemptId, command));
        try
        {
            Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
            var receipts = await Task.WhenAll(
                responses.Select(response => ReadRequiredAsync<ExamFinalReceiptDto>(response)));
            Assert.All(receipts, receipt => Assert.Equal(receipts[0], receipt));
            var state = await _factory.GetExamFinalizationStateAsync(attempt.ExamAttemptId);
            Assert.Equal(operationId, state.FinalizeClientOperationId);
            Assert.Equal(receipts[0].FinalizedAtUtc, state.FinalizedAtUtc);
            Assert.Empty(state.FinalAnswers);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Fact]
    public async Task ConcurrentSaveAndFinalizeProduceOneCoherentWinner()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, setup.Manager.OrganizationId, setup.ClassId);
        var attempt = await StartAttemptAsync(studentClient, setup.Exam.Id);
        var lease = await AcquireLeaseAsync(studentClient, attempt.ExamAttemptId);
        var question = Assert.Single(attempt.Questions);

        var saveTask = SendSaveAsync(
            studentClient,
            attempt.ExamAttemptId,
            question.AttemptQuestionId,
            new SaveExamAnswerCommand(
                Guid.NewGuid(),
                lease.WriteLeaseVersion,
                null,
                question.Options.First().Id,
                null));
        var finalizeTask = SendFinalizeAsync(
            studentClient,
            attempt.ExamAttemptId,
            new FinalizeExamCommand(Guid.NewGuid(), 0, lease.WriteLeaseVersion));
        await Task.WhenAll(saveTask, finalizeTask);
        using var save = await saveTask;
        using var finalize = await finalizeTask;
        var state = await _factory.GetExamFinalizationStateAsync(attempt.ExamAttemptId);

        if (finalize.IsSuccessStatusCode)
        {
            Assert.Equal(HttpStatusCode.Conflict, save.StatusCode);
            Assert.Equal(AssessmentErrorCodes.ExamAttemptNotWritable, await ReadProblemCodeAsync(save));
            Assert.Equal(ExamAttemptStatus.Finalized, state.Status);
            Assert.Equal(0, state.AnswerSetVersion);
            Assert.Empty(state.FinalAnswers);
        }
        else
        {
            Assert.True(save.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.Conflict, finalize.StatusCode);
            Assert.Equal(
                AssessmentErrorCodes.ExamAnswerSetVersionConflict,
                await ReadProblemCodeAsync(finalize));
            Assert.Equal(ExamAttemptStatus.InProgress, state.Status);
            Assert.Equal(1, state.AnswerSetVersion);
            Assert.Empty(state.FinalAnswers);
        }
    }

    [Fact]
    public async Task ConcurrentLeaseTransferAndFinalizeProduceOneCoherentWriterState()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient);
        using var firstDevice = _factory.CreateClient();
        var student = await CreateStudentAsync(
            firstDevice,
            setup.Manager.OrganizationId,
            setup.ClassId);
        var attempt = await StartAttemptAsync(firstDevice, setup.Exam.Id);
        var firstLease = await AcquireLeaseAsync(firstDevice, attempt.ExamAttemptId);
        using var secondDevice = _factory.CreateClient();
        var secondToken = await _factory.CreateAdditionalStudentSessionAsync(
            student.UserId,
            student.MembershipId);
        UseBearerToken(secondDevice, secondToken);

        var transferTask = secondDevice.PostAsync(
            $"/api/v1/academic/exam-attempts/{attempt.ExamAttemptId}/write-lease/transfer",
            null);
        var finalizeTask = SendFinalizeAsync(
            firstDevice,
            attempt.ExamAttemptId,
            new FinalizeExamCommand(Guid.NewGuid(), 0, firstLease.WriteLeaseVersion));
        await Task.WhenAll(transferTask, finalizeTask);
        using var transfer = await transferTask;
        using var finalize = await finalizeTask;
        var state = await _factory.GetExamFinalizationStateAsync(attempt.ExamAttemptId);

        if (finalize.IsSuccessStatusCode)
        {
            Assert.Equal(HttpStatusCode.Conflict, transfer.StatusCode);
            Assert.Equal(AssessmentErrorCodes.ExamAttemptNotWritable, await ReadProblemCodeAsync(transfer));
            Assert.Equal(ExamAttemptStatus.Finalized, state.Status);
        }
        else
        {
            Assert.True(transfer.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.Conflict, finalize.StatusCode);
            var errorCode = await ReadProblemCodeAsync(finalize);
            Assert.Contains(
                errorCode,
                new[]
                {
                    AssessmentErrorCodes.ExamWriteLeaseHeldByOtherSession,
                    AssessmentErrorCodes.ExamWriteLeaseStale
                });
            Assert.Equal(ExamAttemptStatus.InProgress, state.Status);
        }
    }

    [Fact]
    public async Task RevokedWriterSessionCannotFinalize()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient);
        using var studentClient = _factory.CreateClient();
        var student = await CreateStudentAsync(
            studentClient,
            setup.Manager.OrganizationId,
            setup.ClassId);
        var attempt = await StartAttemptAsync(studentClient, setup.Exam.Id);
        var lease = await AcquireLeaseAsync(studentClient, attempt.ExamAttemptId);
        await _factory.RevokeSessionByTokenAsync(student.AccessToken);

        using var response = await SendFinalizeAsync(
            studentClient,
            attempt.ExamAttemptId,
            new FinalizeExamCommand(Guid.NewGuid(), 0, lease.WriteLeaseVersion));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var state = await _factory.GetExamFinalizationStateAsync(attempt.ExamAttemptId);
        Assert.Equal(ExamAttemptStatus.InProgress, state.Status);
        Assert.Empty(state.FinalAnswers);
    }

    [Fact]
    public async Task FinalReceiptDoesNotExposeGradingOrAnswerKeyData()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, setup.Manager.OrganizationId, setup.ClassId);
        var attempt = await StartAttemptAsync(studentClient, setup.Exam.Id);
        var lease = await AcquireLeaseAsync(studentClient, attempt.ExamAttemptId);

        using var response = await SendFinalizeAsync(
            studentClient,
            attempt.ExamAttemptId,
            new FinalizeExamCommand(Guid.NewGuid(), 0, lease.WriteLeaseVersion));
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("correctOptionId", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("isCorrect", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("answerKey", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("objectiveScore", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("totalScore", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("grade", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("feedback", payload, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<ExamFinalReceiptDto> FinalizeAsync(
        HttpClient client,
        Guid attemptId,
        FinalizeExamCommand command)
    {
        using var response = await SendFinalizeAsync(client, attemptId, command);
        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<ExamFinalReceiptDto>(response);
    }

    private static Task<HttpResponseMessage> SendFinalizeAsync(
        HttpClient client,
        Guid attemptId,
        FinalizeExamCommand command) =>
        client.PostAsJsonAsync(
            $"/api/v1/academic/exam-attempts/{attemptId}/finalize",
            command,
            JsonOptions);
}
