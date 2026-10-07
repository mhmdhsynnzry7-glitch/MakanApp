using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Assessment;
using MakanApp.Domain.Assessment;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class ExamEndpointsTests
{
    [Fact]
    public async Task OwningStudentAcquiresWriteLeaseIdempotently()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, setup.Manager.OrganizationId, setup.ClassId);
        var attempt = await StartAttemptAsync(studentClient, setup.Exam.Id);

        var first = await AcquireLeaseAsync(studentClient, attempt.ExamAttemptId);
        var retry = await AcquireLeaseAsync(studentClient, attempt.ExamAttemptId);

        Assert.True(first.IsCurrentSessionWriter);
        Assert.Equal(1, first.WriteLeaseVersion);
        Assert.Equal(first.WriteLeaseVersion, retry.WriteLeaseVersion);
        Assert.False(first.CanRequestTransfer);
    }

    [Fact]
    public async Task UnrelatedStudentCannotAcquireWriteLease()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient);
        using var ownerClient = _factory.CreateClient();
        _ = await CreateStudentAsync(ownerClient, setup.Manager.OrganizationId, setup.ClassId);
        var attempt = await StartAttemptAsync(ownerClient, setup.Exam.Id);
        using var otherClient = _factory.CreateClient();
        _ = await CreateStudentAsync(otherClient, setup.Manager.OrganizationId, setup.ClassId);

        using var response = await otherClient.PostAsync(
            $"/api/v1/academic/exam-attempts/{attempt.ExamAttemptId}/write-lease",
            null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(AssessmentErrorCodes.ExamAttemptNotFound, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task ParentAndTeacherCannotAcquireStudentWriteLease()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, setup.Manager.OrganizationId, setup.ClassId);
        var attempt = await StartAttemptAsync(studentClient, setup.Exam.Id);
        using var parentClient = _factory.CreateClient();
        _ = await CreateParentAsync(parentClient, setup.Manager.OrganizationId, setup.ClassId);
        using var teacherClient = _factory.CreateClient();
        _ = await CreateTeacherAsync(
            teacherClient,
            setup.Manager.OrganizationId,
            setup.ClassId,
            assigned: true);

        using var parentResponse = await parentClient.PostAsync(
            $"/api/v1/academic/exam-attempts/{attempt.ExamAttemptId}/write-lease",
            null);
        using var teacherResponse = await teacherClient.PostAsync(
            $"/api/v1/academic/exam-attempts/{attempt.ExamAttemptId}/write-lease",
            null);

        Assert.Equal(HttpStatusCode.Forbidden, parentResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, teacherResponse.StatusCode);
        Assert.Equal(AssessmentErrorCodes.ExamAttemptNotAllowed, await ReadProblemCodeAsync(parentResponse));
        Assert.Equal(AssessmentErrorCodes.ExamAttemptNotAllowed, await ReadProblemCodeAsync(teacherResponse));
    }

    [Fact]
    public async Task ActiveWriterSavesObjectiveAnswerAndPersistsRevision()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, setup.Manager.OrganizationId, setup.ClassId);
        var attempt = await StartAttemptAsync(studentClient, setup.Exam.Id);
        var lease = await AcquireLeaseAsync(studentClient, attempt.ExamAttemptId);
        var question = Assert.Single(attempt.Questions);

        var receipt = await SaveAnswerAsync(
            studentClient,
            attempt.ExamAttemptId,
            question.AttemptQuestionId,
            new SaveExamAnswerCommand(
                Guid.NewGuid(),
                lease.WriteLeaseVersion,
                null,
                question.Options.First().Id,
                null));

        Assert.Equal(1, receipt.RevisionNumber);
        Assert.Equal(1, receipt.AnswerSetVersion);
        Assert.Equal("Saved", receipt.Status);
        Assert.Equal(1, await _factory.CountAnswerRevisionsAsync(attempt.ExamAttemptId));
    }

    [Fact]
    public async Task ObjectiveOptionFromAnotherQuestionIsRejected()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient);
        var anotherExam = await CreatePublishedExamAsync(managerClient, setup.ClassId);
        var foreignOptionId = Assert.Single(anotherExam.Questions).Options.First().Id;
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, setup.Manager.OrganizationId, setup.ClassId);
        var attempt = await StartAttemptAsync(studentClient, setup.Exam.Id);
        var lease = await AcquireLeaseAsync(studentClient, attempt.ExamAttemptId);
        var question = Assert.Single(attempt.Questions);

        using var response = await SendSaveAsync(
            studentClient,
            attempt.ExamAttemptId,
            question.AttemptQuestionId,
            new SaveExamAnswerCommand(Guid.NewGuid(), lease.WriteLeaseVersion, null, foreignOptionId, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(AssessmentErrorCodes.ExamAnswerOptionInvalid, await ReadProblemCodeAsync(response));
        Assert.Equal(0, await _factory.CountAnswerRevisionsAsync(attempt.ExamAttemptId));
    }

    [Fact]
    public async Task ActiveWriterSavesDescriptiveAnswerWithoutChangingText()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient, questionCount: 2);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, setup.Manager.OrganizationId, setup.ClassId);
        var attempt = await StartAttemptAsync(studentClient, setup.Exam.Id);
        var lease = await AcquireLeaseAsync(studentClient, attempt.ExamAttemptId);
        var question = Assert.Single(
            attempt.Questions,
            item => item.QuestionType == ExamQuestionType.Descriptive);
        const string answer = "  پاسخ دقیق کاربر\nبا خط دوم  ";

        _ = await SaveAnswerAsync(
            studentClient,
            attempt.ExamAttemptId,
            question.AttemptQuestionId,
            new SaveExamAnswerCommand(Guid.NewGuid(), lease.WriteLeaseVersion, null, null, answer));
        var current = await studentClient.GetFromJsonAsync<StudentExamAnswersDto>(
            $"/api/v1/academic/exam-attempts/{attempt.ExamAttemptId}/answers",
            JsonOptions);

        Assert.Equal(answer, Assert.Single(current!.Answers).TextAnswer);
    }

    [Fact]
    public async Task SecondSaveCreatesRevisionHistoryAndMovesCurrentPointer()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, setup.Manager.OrganizationId, setup.ClassId);
        var attempt = await StartAttemptAsync(studentClient, setup.Exam.Id);
        var lease = await AcquireLeaseAsync(studentClient, attempt.ExamAttemptId);
        var question = Assert.Single(attempt.Questions);
        var options = question.Options.ToArray();
        var first = await SaveAnswerAsync(
            studentClient,
            attempt.ExamAttemptId,
            question.AttemptQuestionId,
            new SaveExamAnswerCommand(Guid.NewGuid(), lease.WriteLeaseVersion, null, options[0].Id, null));
        var second = await SaveAnswerAsync(
            studentClient,
            attempt.ExamAttemptId,
            question.AttemptQuestionId,
            new SaveExamAnswerCommand(Guid.NewGuid(), lease.WriteLeaseVersion, 1, options[1].Id, null));

        var state = await _factory.GetAnswerPersistenceStateAsync(
            attempt.ExamAttemptId,
            question.AttemptQuestionId);

        Assert.Equal([1, 2], state.RevisionNumbers);
        Assert.Equal(second.AnswerRevisionId, state.CurrentRevisionId);
        Assert.NotEqual(first.AnswerRevisionId, second.AnswerRevisionId);
        Assert.Equal(2, state.AnswerSetVersion);
    }

    [Fact]
    public async Task IdempotentRetryReturnsSameReceiptAndChangedPayloadConflicts()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, setup.Manager.OrganizationId, setup.ClassId);
        var attempt = await StartAttemptAsync(studentClient, setup.Exam.Id);
        var lease = await AcquireLeaseAsync(studentClient, attempt.ExamAttemptId);
        var question = Assert.Single(attempt.Questions);
        var operationId = Guid.NewGuid();
        var command = new SaveExamAnswerCommand(
            operationId,
            lease.WriteLeaseVersion,
            null,
            question.Options.First().Id,
            null);

        var first = await SaveAnswerAsync(studentClient, attempt.ExamAttemptId, question.AttemptQuestionId, command);
        var retry = await SaveAnswerAsync(studentClient, attempt.ExamAttemptId, question.AttemptQuestionId, command);
        using var conflict = await SendSaveAsync(
            studentClient,
            attempt.ExamAttemptId,
            question.AttemptQuestionId,
            command with { SelectedOptionId = question.Options.Last().Id });

        Assert.Equal(first, retry);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal(AssessmentErrorCodes.ExamAnswerIdempotencyConflict, await ReadProblemCodeAsync(conflict));
        var state = await _factory.GetAnswerPersistenceStateAsync(
            attempt.ExamAttemptId,
            question.AttemptQuestionId);
        Assert.Equal(1, state.AnswerSetVersion);
        Assert.Equal([1], state.RevisionNumbers);
    }

    [Fact]
    public async Task AnswerSetVersionTracksMultipleQuestionsAndRetryDoesNotIncrement()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient, questionCount: 2);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, setup.Manager.OrganizationId, setup.ClassId);
        var attempt = await StartAttemptAsync(studentClient, setup.Exam.Id);
        var lease = await AcquireLeaseAsync(studentClient, attempt.ExamAttemptId);
        var objective = Assert.Single(attempt.Questions, item => item.QuestionType == ExamQuestionType.ObjectiveSingleChoice);
        var descriptive = Assert.Single(attempt.Questions, item => item.QuestionType == ExamQuestionType.Descriptive);
        var firstCommand = new SaveExamAnswerCommand(Guid.NewGuid(), 1, null, objective.Options.First().Id, null);
        var first = await SaveAnswerAsync(studentClient, attempt.ExamAttemptId, objective.AttemptQuestionId, firstCommand);
        var second = await SaveAnswerAsync(
            studentClient,
            attempt.ExamAttemptId,
            descriptive.AttemptQuestionId,
            new SaveExamAnswerCommand(Guid.NewGuid(), 1, null, null, "draft"));
        var editCommand = firstCommand with
        {
            ClientOperationId = Guid.NewGuid(),
            ExpectedRevisionNumber = 1,
            SelectedOptionId = objective.Options.Last().Id
        };
        var third = await SaveAnswerAsync(studentClient, attempt.ExamAttemptId, objective.AttemptQuestionId, editCommand);
        var retry = await SaveAnswerAsync(studentClient, attempt.ExamAttemptId, objective.AttemptQuestionId, editCommand);

        Assert.Equal(1, first.AnswerSetVersion);
        Assert.Equal(2, second.AnswerSetVersion);
        Assert.Equal(3, third.AnswerSetVersion);
        Assert.Equal(third, retry);
    }

    [Fact]
    public async Task ConcurrentDifferentQuestionSavesKeepMonotonicAnswerSetVersion()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient, questionCount: 2);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, setup.Manager.OrganizationId, setup.ClassId);
        var attempt = await StartAttemptAsync(studentClient, setup.Exam.Id);
        _ = await AcquireLeaseAsync(studentClient, attempt.ExamAttemptId);
        var objective = Assert.Single(attempt.Questions, item => item.QuestionType == ExamQuestionType.ObjectiveSingleChoice);
        var descriptive = Assert.Single(attempt.Questions, item => item.QuestionType == ExamQuestionType.Descriptive);

        var receipts = await Task.WhenAll(
            SaveAnswerAsync(
                studentClient,
                attempt.ExamAttemptId,
                objective.AttemptQuestionId,
                new SaveExamAnswerCommand(Guid.NewGuid(), 1, null, objective.Options.First().Id, null)),
            SaveAnswerAsync(
                studentClient,
                attempt.ExamAttemptId,
                descriptive.AttemptQuestionId,
                new SaveExamAnswerCommand(Guid.NewGuid(), 1, null, null, "concurrent")));

        Assert.Equal([1L, 2L], receipts.Select(receipt => receipt.AnswerSetVersion).Order().ToArray());
        Assert.Equal(2, await _factory.CountAnswerRevisionsAsync(attempt.ExamAttemptId));
    }

    [Fact]
    public async Task ConcurrentStaleExpectedRevisionProducesOneExplicitConflict()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, setup.Manager.OrganizationId, setup.ClassId);
        var attempt = await StartAttemptAsync(studentClient, setup.Exam.Id);
        _ = await AcquireLeaseAsync(studentClient, attempt.ExamAttemptId);
        var question = Assert.Single(attempt.Questions);
        _ = await SaveAnswerAsync(
            studentClient,
            attempt.ExamAttemptId,
            question.AttemptQuestionId,
            new SaveExamAnswerCommand(Guid.NewGuid(), 1, null, question.Options.First().Id, null));

        var responses = await Task.WhenAll(
            SendSaveAsync(
                studentClient,
                attempt.ExamAttemptId,
                question.AttemptQuestionId,
                new SaveExamAnswerCommand(Guid.NewGuid(), 1, 1, question.Options.First().Id, null)),
            SendSaveAsync(
                studentClient,
                attempt.ExamAttemptId,
                question.AttemptQuestionId,
                new SaveExamAnswerCommand(Guid.NewGuid(), 1, 1, question.Options.Last().Id, null)));
        using var firstResponse = responses[0];
        using var secondResponse = responses[1];
        var success = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        var conflict = Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);

        Assert.NotNull(success);
        Assert.Equal(AssessmentErrorCodes.ExamAnswerVersionConflict, await ReadProblemCodeAsync(conflict));
        Assert.Equal(2, await _factory.CountAnswerRevisionsAsync(attempt.ExamAttemptId));
    }

    [Fact]
    public async Task TwoDeviceTransferPreventsLostUpdate()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient);
        using var firstDevice = _factory.CreateClient();
        var student = await CreateStudentAsync(firstDevice, setup.Manager.OrganizationId, setup.ClassId);
        var attempt = await StartAttemptAsync(firstDevice, setup.Exam.Id);
        var firstLease = await AcquireLeaseAsync(firstDevice, attempt.ExamAttemptId);
        using var secondDevice = _factory.CreateClient();
        var secondToken = await _factory.CreateAdditionalStudentSessionAsync(
            student.UserId,
            student.MembershipId);
        UseBearerToken(secondDevice, secondToken);
        var question = Assert.Single(attempt.Questions);
        var firstOption = question.Options.First().Id;
        var secondOption = question.Options.Last().Id;

        using var blocked = await SendSaveAsync(
            secondDevice,
            attempt.ExamAttemptId,
            question.AttemptQuestionId,
            new SaveExamAnswerCommand(Guid.NewGuid(), firstLease.WriteLeaseVersion, null, secondOption, null));
        var secondLease = await TransferLeaseAsync(secondDevice, attempt.ExamAttemptId);
        using var stale = await SendSaveAsync(
            firstDevice,
            attempt.ExamAttemptId,
            question.AttemptQuestionId,
            new SaveExamAnswerCommand(Guid.NewGuid(), firstLease.WriteLeaseVersion, null, firstOption, null));
        var accepted = await SaveAnswerAsync(
            secondDevice,
            attempt.ExamAttemptId,
            question.AttemptQuestionId,
            new SaveExamAnswerCommand(Guid.NewGuid(), secondLease.WriteLeaseVersion, null, secondOption, null));

        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Equal(
            AssessmentErrorCodes.ExamWriteLeaseHeldByOtherSession,
            await ReadProblemCodeAsync(blocked));
        Assert.Equal(2, secondLease.WriteLeaseVersion);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(AssessmentErrorCodes.ExamWriteLeaseStale, await ReadProblemCodeAsync(stale));
        Assert.Equal(1, accepted.RevisionNumber);
        Assert.Equal(1, await _factory.CountAnswerRevisionsAsync(attempt.ExamAttemptId));
    }

    [Fact]
    public async Task RevokedWriterSessionCannotSaveAndValidSessionCanTransfer()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient);
        using var firstDevice = _factory.CreateClient();
        var student = await CreateStudentAsync(firstDevice, setup.Manager.OrganizationId, setup.ClassId);
        var attempt = await StartAttemptAsync(firstDevice, setup.Exam.Id);
        var lease = await AcquireLeaseAsync(firstDevice, attempt.ExamAttemptId);
        await _factory.RevokeSessionByTokenAsync(student.AccessToken);
        var question = Assert.Single(attempt.Questions);

        using var revokedResponse = await SendSaveAsync(
            firstDevice,
            attempt.ExamAttemptId,
            question.AttemptQuestionId,
            new SaveExamAnswerCommand(Guid.NewGuid(), lease.WriteLeaseVersion, null, question.Options.First().Id, null));
        using var secondDevice = _factory.CreateClient();
        var secondToken = await _factory.CreateAdditionalStudentSessionAsync(student.UserId, student.MembershipId);
        UseBearerToken(secondDevice, secondToken);
        var transferred = await TransferLeaseAsync(secondDevice, attempt.ExamAttemptId);

        Assert.Equal(HttpStatusCode.Unauthorized, revokedResponse.StatusCode);
        Assert.True(transferred.IsCurrentSessionWriter);
        Assert.Equal(2, transferred.WriteLeaseVersion);
    }

    [Fact]
    public async Task ServerDeadlineRejectsOfflineAnswerDespiteClientTimestamp()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, setup.Manager.OrganizationId, setup.ClassId);
        var attempt = await StartAttemptAsync(studentClient, setup.Exam.Id);
        var lease = await AcquireLeaseAsync(studentClient, attempt.ExamAttemptId);
        await _factory.ExpireExamAttemptAsync(attempt.ExamAttemptId);
        var question = Assert.Single(attempt.Questions);

        using var response = await studentClient.PutAsJsonAsync(
            $"/api/v1/academic/exam-attempts/{attempt.ExamAttemptId}/answers/{question.AttemptQuestionId}",
            new
            {
                ClientOperationId = Guid.NewGuid(),
                WriteLeaseVersion = lease.WriteLeaseVersion,
                ExpectedRevisionNumber = (int?)null,
                SelectedOptionId = question.Options.First().Id,
                TextAnswer = (string?)null,
                CreatedAtLocal = attempt.EffectiveDeadlineUtc.AddMinutes(-1)
            },
            JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(AssessmentErrorCodes.ExamDeadlinePassed, await ReadProblemCodeAsync(response));
        Assert.Equal(0, await _factory.CountAnswerRevisionsAsync(attempt.ExamAttemptId));
    }

    [Fact]
    public async Task NonWritableAttemptCannotAcceptNewAnswer()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, setup.Manager.OrganizationId, setup.ClassId);
        var attempt = await StartAttemptAsync(studentClient, setup.Exam.Id);
        var lease = await AcquireLeaseAsync(studentClient, attempt.ExamAttemptId);
        await _factory.MarkExamAttemptExpiredAsync(attempt.ExamAttemptId);
        var question = Assert.Single(attempt.Questions);

        using var response = await SendSaveAsync(
            studentClient,
            attempt.ExamAttemptId,
            question.AttemptQuestionId,
            new SaveExamAnswerCommand(Guid.NewGuid(), lease.WriteLeaseVersion, null, question.Options.First().Id, null));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(AssessmentErrorCodes.ExamAttemptNotWritable, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task QuestionNotFrozenIntoAttemptCannotBeAnswered()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient);
        var secondExam = await CreatePublishedExamAsync(managerClient, setup.ClassId);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, setup.Manager.OrganizationId, setup.ClassId);
        var firstAttempt = await StartAttemptAsync(studentClient, setup.Exam.Id);
        var secondAttempt = await StartAttemptAsync(studentClient, secondExam.Id);
        var lease = await AcquireLeaseAsync(studentClient, firstAttempt.ExamAttemptId);
        var foreignQuestion = Assert.Single(secondAttempt.Questions);

        using var response = await SendSaveAsync(
            studentClient,
            firstAttempt.ExamAttemptId,
            foreignQuestion.AttemptQuestionId,
            new SaveExamAnswerCommand(Guid.NewGuid(), lease.WriteLeaseVersion, null, foreignQuestion.Options.First().Id, null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(AssessmentErrorCodes.ExamAnswerNotAllowed, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task ReloadReturnsOnlyCurrentAcceptedAnswersAndSafeLeaseState()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, setup.Manager.OrganizationId, setup.ClassId);
        var attempt = await StartAttemptAsync(studentClient, setup.Exam.Id);
        var lease = await AcquireLeaseAsync(studentClient, attempt.ExamAttemptId);
        var question = Assert.Single(attempt.Questions);
        var receipt = await SaveAnswerAsync(
            studentClient,
            attempt.ExamAttemptId,
            question.AttemptQuestionId,
            new SaveExamAnswerCommand(Guid.NewGuid(), 1, null, question.Options.First().Id, null));

        var answers = await studentClient.GetFromJsonAsync<StudentExamAnswersDto>(
            $"/api/v1/academic/exam-attempts/{attempt.ExamAttemptId}/answers",
            JsonOptions);

        var answer = Assert.Single(answers!.Answers);
        Assert.Equal(receipt.AnswerRevisionId, answer.AnswerRevisionId);
        Assert.Equal(receipt.RevisionNumber, answer.RevisionNumber);
        Assert.Equal(1, answers.AnswerSetVersion);
        Assert.True(answers.WriteLease.IsCurrentSessionWriter);
        Assert.Equal(lease.WriteLeaseVersion, answers.WriteLease.WriteLeaseVersion);
    }

    [Fact]
    public async Task StudentExamAndAnswerResponsesContainNoAnswerKeyCanary()
    {
        const string secretAnswerKey = "SUPER_SECRET_EXAM_KEY_991";
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient);
        var correctOption = Assert.Single(
            Assert.Single(setup.Exam.Questions).Options,
            option => option.IsCorrect);
        var privateAnswerKey = $"{secretAnswerKey}:{correctOption.Id}";
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, setup.Manager.OrganizationId, setup.ClassId);

        using var startResponse = await studentClient.PostAsJsonAsync(
            $"/api/v1/academic/exams/{setup.Exam.Id}/attempts/start",
            new StartExamCommand(Guid.NewGuid()),
            JsonOptions);
        var attempt = await ReadRequiredAsync<StudentExamAttemptDto>(startResponse);
        _ = await AcquireLeaseAsync(studentClient, attempt.ExamAttemptId);
        var question = Assert.Single(attempt.Questions);
        using var saveResponse = await SendSaveAsync(
            studentClient,
            attempt.ExamAttemptId,
            question.AttemptQuestionId,
            new SaveExamAnswerCommand(Guid.NewGuid(), 1, null, question.Options.First().Id, null));
        using var answersResponse = await studentClient.GetAsync(
            $"/api/v1/academic/exam-attempts/{attempt.ExamAttemptId}/answers");
        var payload = string.Join(
            '\n',
            await startResponse.Content.ReadAsStringAsync(),
            await saveResponse.Content.ReadAsStringAsync(),
            await answersResponse.Content.ReadAsStringAsync());

        Assert.DoesNotContain(secretAnswerKey, payload, StringComparison.Ordinal);
        Assert.DoesNotContain(privateAnswerKey, payload, StringComparison.Ordinal);
        Assert.DoesNotContain("isCorrect", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("correctOption", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("answerKey", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnauthenticatedAnswerWriteIsRejected()
    {
        using var client = _factory.CreateClient();

        using var response = await SendSaveAsync(
            client,
            Guid.NewGuid(),
            Guid.NewGuid(),
            new SaveExamAnswerCommand(Guid.NewGuid(), 1, null, Guid.NewGuid(), null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<ExamWriteLeaseDto> AcquireLeaseAsync(
        HttpClient client,
        Guid attemptId)
    {
        using var response = await client.PostAsync(
            $"/api/v1/academic/exam-attempts/{attemptId}/write-lease",
            null);
        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<ExamWriteLeaseDto>(response);
    }

    private static async Task<ExamWriteLeaseDto> TransferLeaseAsync(
        HttpClient client,
        Guid attemptId)
    {
        using var response = await client.PostAsync(
            $"/api/v1/academic/exam-attempts/{attemptId}/write-lease/transfer",
            null);
        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<ExamWriteLeaseDto>(response);
    }

    private static async Task<ExamAnswerReceiptDto> SaveAnswerAsync(
        HttpClient client,
        Guid attemptId,
        Guid attemptQuestionId,
        SaveExamAnswerCommand command)
    {
        using var response = await SendSaveAsync(client, attemptId, attemptQuestionId, command);
        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<ExamAnswerReceiptDto>(response);
    }

    private static Task<HttpResponseMessage> SendSaveAsync(
        HttpClient client,
        Guid attemptId,
        Guid attemptQuestionId,
        SaveExamAnswerCommand command) =>
        client.PutAsJsonAsync(
            $"/api/v1/academic/exam-attempts/{attemptId}/answers/{attemptQuestionId}",
            command,
            JsonOptions);
}
