using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Assessment;
using MakanApp.Domain.Assessment;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class ExamEndpointsTests
{
    [Fact]
    public async Task EligibleStudentStartsPublishedExam()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, setup.Manager.OrganizationId, setup.ClassId);

        var attempt = await StartAttemptAsync(studentClient, setup.Exam.Id);

        Assert.Equal(setup.Exam.Id, attempt.ExamId);
        Assert.Equal(setup.Exam.VersionId, attempt.ExamVersionId);
        Assert.Equal(1, attempt.AttemptNumber);
        Assert.Equal(ExamAttemptStatus.InProgress, attempt.Status);
        Assert.Single(attempt.Questions);
    }

    [Fact]
    public async Task NonEnrolledStudentCannotStartExam()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(
            studentClient,
            setup.Manager.OrganizationId,
            setup.ClassId,
            enrolled: false);

        using var response = await studentClient.PostAsJsonAsync(
            $"/api/v1/academic/exams/{setup.Exam.Id}/attempts/start",
            new StartExamCommand(Guid.NewGuid()),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(AssessmentErrorCodes.ExamAttemptNotAllowed, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task StudentFromAnotherOrganizationCannotStartExam()
    {
        using var ownerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(ownerClient);
        using var otherManagerClient = _factory.CreateClient();
        var otherManager = await CreateManagerAsync(otherManagerClient);
        var otherClass = await _factory.CreateAcademicClassAsync(otherManager.OrganizationId);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, otherManager.OrganizationId, otherClass.ClassId);

        using var response = await studentClient.PostAsJsonAsync(
            $"/api/v1/academic/exams/{setup.Exam.Id}/attempts/start",
            new StartExamCommand(Guid.NewGuid()),
            JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(AssessmentErrorCodes.ExamNotFound, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task ParentCannotStartAttemptForChild()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient);
        using var parentClient = _factory.CreateClient();
        _ = await CreateParentAsync(parentClient, setup.Manager.OrganizationId, setup.ClassId);

        using var response = await parentClient.PostAsJsonAsync(
            $"/api/v1/academic/exams/{setup.Exam.Id}/attempts/start",
            new StartExamCommand(Guid.NewGuid()),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(AssessmentErrorCodes.ExamAttemptNotAllowed, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task TeacherCannotUseStudentStartEndpoint()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient);
        using var teacherClient = _factory.CreateClient();
        _ = await CreateTeacherAsync(
            teacherClient,
            setup.Manager.OrganizationId,
            setup.ClassId,
            assigned: true);

        using var response = await teacherClient.PostAsJsonAsync(
            $"/api/v1/academic/exams/{setup.Exam.Id}/attempts/start",
            new StartExamCommand(Guid.NewGuid()),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ExamBeforeStartWindowCannotStart()
    {
        var nowUtc = DateTime.UtcNow;
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(
            managerClient,
            availableFromUtc: nowUtc.AddHours(1),
            availableUntilUtc: nowUtc.AddHours(2));
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, setup.Manager.OrganizationId, setup.ClassId);

        using var response = await studentClient.PostAsJsonAsync(
            $"/api/v1/academic/exams/{setup.Exam.Id}/attempts/start",
            new StartExamCommand(Guid.NewGuid()),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(AssessmentErrorCodes.ExamNotAvailableYet, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task ExamAfterWindowCannotStart()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient);
        await _factory.SetExamVersionWindowAsync(
            setup.Exam.VersionId,
            DateTime.UtcNow.AddHours(-2),
            DateTime.UtcNow.AddHours(-1));
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, setup.Manager.OrganizationId, setup.ClassId);

        using var response = await studentClient.PostAsJsonAsync(
            $"/api/v1/academic/exams/{setup.Exam.Id}/attempts/start",
            new StartExamCommand(Guid.NewGuid()),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(AssessmentErrorCodes.ExamWindowClosed, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task StartedAtAndAttemptNumberAreServerGenerated()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, setup.Manager.OrganizationId, setup.ClassId);
        var beforeUtc = DateTime.UtcNow;

        var attempt = await StartAttemptAsync(studentClient, setup.Exam.Id);
        var afterUtc = DateTime.UtcNow;

        Assert.InRange(attempt.StartedAtUtc, beforeUtc, afterUtc);
        Assert.InRange(attempt.ServerNowUtc, beforeUtc, afterUtc);
        Assert.Equal(1, attempt.AttemptNumber);
    }

    [Fact]
    public async Task DeadlineUsesDurationWhenEarlierThanWindowEnd()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient, durationMinutes: 30);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, setup.Manager.OrganizationId, setup.ClassId);

        var attempt = await StartAttemptAsync(studentClient, setup.Exam.Id);

        Assert.Equal(attempt.StartedAtUtc.AddMinutes(30), attempt.EffectiveDeadlineUtc);
    }

    [Fact]
    public async Task DeadlineIsCappedByExamWindow()
    {
        var untilUtc = DateTime.UtcNow.AddMinutes(5);
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(
            managerClient,
            availableUntilUtc: untilUtc,
            durationMinutes: 60);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, setup.Manager.OrganizationId, setup.ClassId);

        var attempt = await StartAttemptAsync(studentClient, setup.Exam.Id);

        Assert.Equal(setup.Exam.AvailableUntilUtc, attempt.EffectiveDeadlineUtc);
    }

    [Fact]
    public async Task MaxAttemptsIsEnforcedAfterCommittedAttempt()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient, maxAttempts: 1);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, setup.Manager.OrganizationId, setup.ClassId);
        var first = await StartAttemptAsync(studentClient, setup.Exam.Id);
        await _factory.ExpireExamAttemptAsync(first.ExamAttemptId);

        using var response = await studentClient.PostAsJsonAsync(
            $"/api/v1/academic/exams/{setup.Exam.Id}/attempts/start",
            new StartExamCommand(Guid.NewGuid()),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(AssessmentErrorCodes.ExamAttemptsExhausted, await ReadProblemCodeAsync(response));
        Assert.Equal(1, await _factory.CountExamAttemptsAsync(setup.Exam.Id));
    }

    [Fact]
    public async Task SecondAttemptGetsNextServerNumber()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient, maxAttempts: 2);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, setup.Manager.OrganizationId, setup.ClassId);
        var first = await StartAttemptAsync(studentClient, setup.Exam.Id);
        await _factory.ExpireExamAttemptAsync(first.ExamAttemptId);

        var second = await StartAttemptAsync(studentClient, setup.Exam.Id);

        Assert.Equal(2, second.AttemptNumber);
        Assert.NotEqual(first.ExamAttemptId, second.ExamAttemptId);
        Assert.Equal(2, await _factory.CountExamAttemptsAsync(setup.Exam.Id));
    }

    [Fact]
    public async Task RetryingSameOperationReturnsSameAttempt()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, setup.Manager.OrganizationId, setup.ClassId);
        var operationId = Guid.NewGuid();

        var first = await StartAttemptAsync(studentClient, setup.Exam.Id, operationId);
        var retry = await StartAttemptAsync(studentClient, setup.Exam.Id, operationId);

        AssertSameAttempt(first, retry);
        Assert.Equal(1, await _factory.CountExamAttemptsAsync(setup.Exam.Id));
    }

    [Fact]
    public async Task ConcurrentDoubleClickCreatesSingleAttempt()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient);
        using var studentClient = _factory.CreateClient();
        var student = await CreateStudentAsync(
            studentClient,
            setup.Manager.OrganizationId,
            setup.ClassId);
        using var secondDeviceClient = _factory.CreateClient();
        UseBearerToken(secondDeviceClient, student.AccessToken);
        var operationId = Guid.NewGuid();

        var results = await Task.WhenAll(
            StartAttemptAsync(studentClient, setup.Exam.Id, operationId),
            StartAttemptAsync(secondDeviceClient, setup.Exam.Id, operationId));

        AssertSameAttempt(results[0], results[1]);
        Assert.Equal(1, await _factory.CountExamAttemptsAsync(setup.Exam.Id));
    }

    [Fact]
    public async Task ConcurrentStartsWithDifferentKeysResumeSameActiveAttempt()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient, maxAttempts: 2);
        using var studentClient = _factory.CreateClient();
        var student = await CreateStudentAsync(
            studentClient,
            setup.Manager.OrganizationId,
            setup.ClassId);
        using var secondDeviceClient = _factory.CreateClient();
        UseBearerToken(secondDeviceClient, student.AccessToken);

        var results = await Task.WhenAll(
            StartAttemptAsync(studentClient, setup.Exam.Id, Guid.NewGuid()),
            StartAttemptAsync(secondDeviceClient, setup.Exam.Id, Guid.NewGuid()));

        AssertSameAttempt(results[0], results[1]);
        Assert.Equal(1, await _factory.CountExamAttemptsAsync(setup.Exam.Id));
    }

    [Fact]
    public async Task ActiveAttemptEndpointRestoresSameFrozenAttempt()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient, questionCount: 4);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, setup.Manager.OrganizationId, setup.ClassId);
        var started = await StartAttemptAsync(studentClient, setup.Exam.Id);

        var resumed = await studentClient.GetFromJsonAsync<StudentExamAttemptDto>(
            $"/api/v1/academic/exams/{setup.Exam.Id}/attempts/me/active",
            JsonOptions);

        Assert.NotNull(resumed);
        AssertSameAttempt(started, resumed);
    }

    [Fact]
    public async Task OwningStudentCanGetAttemptById()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, setup.Manager.OrganizationId, setup.ClassId);
        var started = await StartAttemptAsync(studentClient, setup.Exam.Id);

        var loaded = await studentClient.GetFromJsonAsync<StudentExamAttemptDto>(
            $"/api/v1/academic/exam-attempts/{started.ExamAttemptId}",
            JsonOptions);

        Assert.NotNull(loaded);
        AssertSameAttempt(started, loaded);
    }

    [Fact]
    public async Task AnotherStudentCannotReadAttempt()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient);
        using var ownerClient = _factory.CreateClient();
        _ = await CreateStudentAsync(ownerClient, setup.Manager.OrganizationId, setup.ClassId);
        var started = await StartAttemptAsync(ownerClient, setup.Exam.Id);
        using var attackerClient = _factory.CreateClient();
        _ = await CreateStudentAsync(attackerClient, setup.Manager.OrganizationId, setup.ClassId);

        using var response = await attackerClient.GetAsync(
            $"/api/v1/academic/exam-attempts/{started.ExamAttemptId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(AssessmentErrorCodes.ExamAttemptNotFound, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task ManagerAndTeacherCannotImpersonateStudentAttemptRead()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, setup.Manager.OrganizationId, setup.ClassId);
        var started = await StartAttemptAsync(studentClient, setup.Exam.Id);
        using var teacherClient = _factory.CreateClient();
        _ = await CreateTeacherAsync(
            teacherClient,
            setup.Manager.OrganizationId,
            setup.ClassId,
            assigned: true);

        using var managerResponse = await managerClient.GetAsync(
            $"/api/v1/academic/exam-attempts/{started.ExamAttemptId}");
        using var teacherResponse = await teacherClient.GetAsync(
            $"/api/v1/academic/exam-attempts/{started.ExamAttemptId}");

        Assert.Equal(HttpStatusCode.Forbidden, managerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, teacherResponse.StatusCode);
        Assert.Equal(
            AssessmentErrorCodes.ExamAttemptNotAllowed,
            await ReadProblemCodeAsync(managerResponse));
        Assert.Equal(
            AssessmentErrorCodes.ExamAttemptNotAllowed,
            await ReadProblemCodeAsync(teacherResponse));
    }

    [Fact]
    public async Task PublishingNextVersionDoesNotRewriteExistingAttempt()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, setup.Manager.OrganizationId, setup.ClassId);
        var started = await StartAttemptAsync(studentClient, setup.Exam.Id);
        using var nextResponse = await managerClient.PostAsJsonAsync(
            $"/api/v1/academic/exams/{setup.Exam.Id}/versions",
            new CreateNextExamVersionCommand(
                setup.Exam.ExamRowVersion,
                setup.Exam.VersionRowVersion),
            JsonOptions);
        nextResponse.EnsureSuccessStatusCode();
        var next = await ReadRequiredAsync<ExamEditorDto>(nextResponse);
        var publishedV2 = await PublishAsync(managerClient, next);

        var resumed = await StartAttemptAsync(studentClient, setup.Exam.Id);

        Assert.Equal(setup.Exam.VersionId, resumed.ExamVersionId);
        Assert.NotEqual(publishedV2.VersionId, resumed.ExamVersionId);
        AssertSameAttempt(started, resumed);
    }

    [Fact]
    public async Task RandomizedQuestionOrderIsFrozenAcrossRetry()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(
            managerClient,
            questionCount: 4,
            randomizationPolicy: ExamRandomizationPolicy.QuestionOrder);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, setup.Manager.OrganizationId, setup.ClassId);

        var first = await StartAttemptAsync(studentClient, setup.Exam.Id);
        var second = await StartAttemptAsync(studentClient, setup.Exam.Id);

        Assert.Equal(
            first.Questions.Select(question => question.QuestionVersionId),
            second.Questions.Select(question => question.QuestionVersionId));
        Assert.Equal(4, await _factory.CountExamAttemptQuestionsAsync(first.ExamAttemptId));
    }

    [Fact]
    public async Task NoneRandomizationPreservesPublishedQuestionOrder()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(
            managerClient,
            questionCount: 4,
            randomizationPolicy: ExamRandomizationPolicy.None);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, setup.Manager.OrganizationId, setup.ClassId);

        var attempt = await StartAttemptAsync(studentClient, setup.Exam.Id);

        Assert.Equal(
            setup.Exam.Questions.OrderBy(question => question.Order).Select(question => question.Id),
            attempt.Questions.OrderBy(question => question.DisplayOrder).Select(question => question.QuestionVersionId));
    }

    [Fact]
    public async Task StudentAttemptResponseContainsNoAnswerKey()
    {
        const string secretAnswerKey = "ULTRA_SECRET_EXAM_KEY_842";
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient);
        var correctOption = Assert.Single(
            Assert.Single(setup.Exam.Questions).Options,
            option => option.IsCorrect);
        var privateAnswerKey = $"{secretAnswerKey}:{correctOption.Id}";
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, setup.Manager.OrganizationId, setup.ClassId);

        using var response = await studentClient.PostAsJsonAsync(
            $"/api/v1/academic/exams/{setup.Exam.Id}/attempts/start",
            new StartExamCommand(Guid.NewGuid()),
            JsonOptions);
        var json = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.DoesNotContain("isCorrect", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("correctOption", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("answerKey", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(secretAnswerKey, json, StringComparison.Ordinal);
        Assert.DoesNotContain(privateAnswerKey, json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MyAttemptsReturnsOnlyOwningStudentHistory()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient, maxAttempts: 2);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, setup.Manager.OrganizationId, setup.ClassId);
        var first = await StartAttemptAsync(studentClient, setup.Exam.Id);
        await _factory.ExpireExamAttemptAsync(first.ExamAttemptId);
        _ = await StartAttemptAsync(studentClient, setup.Exam.Id);

        var attempts = await studentClient.GetFromJsonAsync<StudentExamAttemptSummaryDto[]>(
            $"/api/v1/academic/exams/{setup.Exam.Id}/attempts/me",
            JsonOptions);

        Assert.Equal([1, 2], attempts!.Select(attempt => attempt.AttemptNumber));
    }

    [Fact]
    public async Task RulesPreviewDoesNotConsumeAttempt()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, setup.Manager.OrganizationId, setup.ClassId);

        using var preview = await studentClient.GetAsync(
            $"/api/v1/academic/exams/{setup.Exam.Id}/student-preview");

        preview.EnsureSuccessStatusCode();
        Assert.Equal(0, await _factory.CountExamAttemptsAsync(setup.Exam.Id));
    }

    [Fact]
    public async Task ReusingOperationIdForAnotherExamIsRejected()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient);
        var secondExam = await CreatePublishedExamAsync(managerClient, setup.ClassId);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, setup.Manager.OrganizationId, setup.ClassId);
        var operationId = Guid.NewGuid();
        _ = await StartAttemptAsync(studentClient, setup.Exam.Id, operationId);

        using var response = await studentClient.PostAsJsonAsync(
            $"/api/v1/academic/exams/{secondExam.Id}/attempts/start",
            new StartExamCommand(operationId),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(
            AssessmentErrorCodes.ExamStartIdempotencyConflict,
            await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task CrossOrganizationAttemptIsRejectedByCompositeForeignKeys()
    {
        using var firstManagerClient = _factory.CreateClient();
        var first = await CreateAttemptExamAsync(firstManagerClient);
        using var studentClient = _factory.CreateClient();
        var student = await CreateStudentAsync(
            studentClient,
            first.Manager.OrganizationId,
            first.ClassId);
        using var secondManagerClient = _factory.CreateClient();
        var second = await CreateAttemptExamAsync(secondManagerClient);

        var rejected = await _factory.CrossOrganizationExamAttemptIsRejectedAsync(
            first.Manager.OrganizationId,
            second.Exam.Id,
            second.Exam.VersionId,
            first.ClassId,
            student.EnrollmentId!.Value);

        Assert.True(rejected);
    }

    [Fact]
    public async Task AttemptQuestionCannotReferenceAnotherExamVersion()
    {
        using var managerClient = _factory.CreateClient();
        var first = await CreateAttemptExamAsync(managerClient);
        var secondExam = await CreatePublishedExamAsync(managerClient, first.ClassId);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, first.Manager.OrganizationId, first.ClassId);
        var attempt = await StartAttemptAsync(studentClient, first.Exam.Id);

        var rejected = await _factory.ForeignVersionQuestionMappingIsRejectedAsync(
            attempt.ExamAttemptId,
            Assert.Single(secondExam.Questions).Id);

        Assert.True(rejected);
    }

    [Fact]
    public async Task ExpiredAttemptIsNotReturnedAsActive()
    {
        using var managerClient = _factory.CreateClient();
        var setup = await CreateAttemptExamAsync(managerClient);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, setup.Manager.OrganizationId, setup.ClassId);
        var attempt = await StartAttemptAsync(studentClient, setup.Exam.Id);
        await _factory.ExpireExamAttemptAsync(attempt.ExamAttemptId);

        using var response = await studentClient.GetAsync(
            $"/api/v1/academic/exams/{setup.Exam.Id}/attempts/me/active");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AnonymousStartIsRejected()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"/api/v1/academic/exams/{Guid.NewGuid()}/attempts/start",
            new StartExamCommand(Guid.NewGuid()),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<AttemptExamSetup> CreateAttemptExamAsync(
        HttpClient managerClient,
        DateTime? availableFromUtc = null,
        DateTime? availableUntilUtc = null,
        int durationMinutes = 45,
        int maxAttempts = 1,
        int questionCount = 1,
        ExamRandomizationPolicy randomizationPolicy = ExamRandomizationPolicy.QuestionOrder)
    {
        var manager = await CreateManagerAsync(managerClient);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);
        var exam = await CreatePublishedExamAsync(
            managerClient,
            academicClass.ClassId,
            availableFromUtc,
            availableUntilUtc,
            durationMinutes,
            maxAttempts,
            questionCount,
            randomizationPolicy);
        return new AttemptExamSetup(manager, academicClass.ClassId, exam);
    }

    private static async Task<ExamEditorDto> CreatePublishedExamAsync(
        HttpClient managerClient,
        Guid classId,
        DateTime? availableFromUtc = null,
        DateTime? availableUntilUtc = null,
        int durationMinutes = 45,
        int maxAttempts = 1,
        int questionCount = 1,
        ExamRandomizationPolicy randomizationPolicy = ExamRandomizationPolicy.QuestionOrder)
    {
        const decimal questionScore = 10m;
        using var createResponse = await managerClient.PostAsJsonAsync(
            $"/api/v1/academic/classes/{classId}/exams",
            NewDraftCommand(
                availableFromUtc: availableFromUtc,
                availableUntilUtc: availableUntilUtc,
                durationMinutes: durationMinutes,
                maxAttempts: maxAttempts,
                maxScore: questionScore * questionCount,
                randomizationPolicy: randomizationPolicy),
            JsonOptions);
        createResponse.EnsureSuccessStatusCode();
        var exam = await ReadRequiredAsync<ExamEditorDto>(createResponse);
        exam = await AddObjectiveQuestionAsync(
            managerClient,
            exam,
            order: 1,
            prompt: "گزینه صحیح کدام است؟",
            score: questionScore);
        for (var order = 2; order <= questionCount; order++)
        {
            using var addResponse = await managerClient.PostAsJsonAsync(
                $"/api/v1/academic/exams/{exam.Id}/questions",
                new AddExamQuestionCommand(
                    order,
                    ExamQuestionType.Descriptive,
                    $"سؤال تشریحی {order}",
                    questionScore,
                    null,
                    exam.VersionRowVersion),
                JsonOptions);
            addResponse.EnsureSuccessStatusCode();
            exam = await ReadRequiredAsync<ExamEditorDto>(addResponse);
        }

        return await PublishAsync(managerClient, exam);
    }

    private static async Task<StudentExamAttemptDto> StartAttemptAsync(
        HttpClient client,
        Guid examId,
        Guid? clientOperationId = null)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/v1/academic/exams/{examId}/attempts/start",
            new StartExamCommand(clientOperationId ?? Guid.NewGuid()),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<StudentExamAttemptDto>(response);
    }

    private static void AssertSameAttempt(
        StudentExamAttemptDto expected,
        StudentExamAttemptDto actual)
    {
        Assert.Equal(expected.ExamAttemptId, actual.ExamAttemptId);
        Assert.Equal(expected.ExamVersionId, actual.ExamVersionId);
        Assert.Equal(expected.AttemptNumber, actual.AttemptNumber);
        Assert.Equal(expected.StartedAtUtc, actual.StartedAtUtc);
        Assert.Equal(expected.EffectiveDeadlineUtc, actual.EffectiveDeadlineUtc);
        Assert.Equal(
            expected.Questions.Select(question => question.QuestionVersionId),
            actual.Questions.Select(question => question.QuestionVersionId));
    }

    private sealed record AttemptExamSetup(
        ManagerContext Manager,
        Guid ClassId,
        ExamEditorDto Exam);
}
