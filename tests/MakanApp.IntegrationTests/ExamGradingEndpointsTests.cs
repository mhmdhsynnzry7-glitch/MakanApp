using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Assessment;
using MakanApp.Application.Organization;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Organization;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class ExamEndpointsTests
{
    private const string SecretAnswerKey = "SUPER_SECRET_EXAM_KEY_8821";
    private const string PrivateEvaluatorNote = "PRIVATE_EVALUATOR_NOTE_8E";

    [Fact]
    public async Task AssignedTeacherGradesFrozenObjectiveAnswersDeterministically()
    {
        using var managerClient = _factory.CreateClient();
        var manager = await CreateManagerAsync(managerClient);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);
        var exam = await CreateObjectiveGradingExamAsync(managerClient, academicClass.ClassId);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, manager.OrganizationId, academicClass.ClassId);
        var attempt = await StartAttemptAsync(studentClient, exam.Id);
        var lease = await AcquireLeaseAsync(studentClient, attempt.ExamAttemptId);
        var correct = attempt.Questions.Single(question => question.Prompt == "objective-correct");
        var incorrect = attempt.Questions.Single(question => question.Prompt == "objective-incorrect");
        _ = await SaveAnswerAsync(
            studentClient,
            attempt.ExamAttemptId,
            correct.AttemptQuestionId,
            new SaveExamAnswerCommand(
                Guid.NewGuid(),
                lease.WriteLeaseVersion,
                null,
                correct.Options.Single(option => option.Text == SecretAnswerKey).Id,
                null));
        _ = await SaveAnswerAsync(
            studentClient,
            attempt.ExamAttemptId,
            incorrect.AttemptQuestionId,
            new SaveExamAnswerCommand(
                Guid.NewGuid(),
                lease.WriteLeaseVersion,
                null,
                incorrect.Options.Single(option => option.Text == "wrong-two").Id,
                null));
        _ = await FinalizeAsync(
            studentClient,
            attempt.ExamAttemptId,
            new FinalizeExamCommand(Guid.NewGuid(), 2, lease.WriteLeaseVersion));
        using var teacherClient = _factory.CreateClient();
        _ = await CreateTeacherAsync(
            teacherClient,
            manager.OrganizationId,
            academicClass.ClassId,
            assigned: true);

        var queue = await teacherClient.GetFromJsonAsync<ExamGradingQueueItemDto[]>(
            $"/api/v1/academic/exams/{exam.Id}/grading",
            JsonOptions);
        var grade = await InitializeGradeAsync(teacherClient, attempt.ExamAttemptId);

        var queueItem = Assert.Single(queue!);
        Assert.Equal(attempt.ExamAttemptId, queueItem.ExamAttemptId);
        Assert.Equal(ExamGradingQueueStatus.AwaitingGrading, queueItem.Status);
        Assert.Equal(6m, grade.MaximumScore);
        Assert.Equal(2m, grade.TotalScore);
        Assert.Equal(2m, grade.Questions.Single(question =>
            question.ExamAttemptQuestionId == correct.AttemptQuestionId).AwardedScore);
        Assert.Equal(0m, grade.Questions.Single(question =>
            question.ExamAttemptQuestionId == incorrect.AttemptQuestionId).AwardedScore);
        var unanswered = grade.Questions.Single(question => question.Prompt == "objective-unanswered");
        Assert.Equal(0m, unanswered.AwardedScore);
        Assert.False(unanswered.IsAnswered);
        Assert.All(grade.Questions, question =>
        {
            Assert.Equal(ExamQuestionGradingMode.Objective, question.GradingMode);
            Assert.True(question.IsReviewed);
        });
    }

    [Fact]
    public async Task GradingAuthorizationRequiresFinalizedAttemptAndAssignedEvaluator()
    {
        using var managerClient = _factory.CreateClient();
        var manager = await CreateManagerAsync(managerClient);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);
        var exam = await CreateSingleDescriptiveExamAsync(managerClient, academicClass.ClassId, 20m);
        using var studentClient = _factory.CreateClient();
        var student = await CreateStudentAsync(
            studentClient,
            manager.OrganizationId,
            academicClass.ClassId);
        var attempt = await StartAttemptAsync(studentClient, exam.Id);
        var lease = await AcquireLeaseAsync(studentClient, attempt.ExamAttemptId);
        using var teacherClient = _factory.CreateClient();
        _ = await CreateTeacherAsync(
            teacherClient,
            manager.OrganizationId,
            academicClass.ClassId,
            assigned: true);

        using var inProgress = await teacherClient.PostAsync(
            $"/api/v1/academic/exam-attempts/{attempt.ExamAttemptId}/grading",
            null);
        Assert.Equal(HttpStatusCode.Conflict, inProgress.StatusCode);
        Assert.Equal(
            AssessmentErrorCodes.ExamGradeAttemptNotFinalized,
            await ReadProblemCodeAsync(inProgress));

        _ = await FinalizeAsync(
            studentClient,
            attempt.ExamAttemptId,
            new FinalizeExamCommand(Guid.NewGuid(), 0, lease.WriteLeaseVersion));
        var managerGrade = await InitializeGradeAsync(managerClient, attempt.ExamAttemptId);
        using var unassignedClient = _factory.CreateClient();
        _ = await CreateTeacherAsync(
            unassignedClient,
            manager.OrganizationId,
            academicClass.ClassId,
            assigned: false);
        using var unassigned = await unassignedClient.PostAsync(
            $"/api/v1/academic/exam-attempts/{attempt.ExamAttemptId}/grading",
            null);
        Assert.Equal(HttpStatusCode.Forbidden, unassigned.StatusCode);
        Assert.Equal(AssessmentErrorCodes.TeacherNotAssigned, await ReadProblemCodeAsync(unassigned));

        using var otherManagerClient = _factory.CreateClient();
        var otherManager = await CreateManagerAsync(otherManagerClient);
        var otherClass = await _factory.CreateAcademicClassAsync(otherManager.OrganizationId);
        using var otherTeacherClient = _factory.CreateClient();
        _ = await CreateTeacherAsync(
            otherTeacherClient,
            otherManager.OrganizationId,
            otherClass.ClassId,
            assigned: true);
        using var crossOrganization = await otherTeacherClient.PostAsync(
            $"/api/v1/academic/exam-attempts/{attempt.ExamAttemptId}/grading",
            null);
        Assert.Equal(HttpStatusCode.NotFound, crossOrganization.StatusCode);
        Assert.Equal(AssessmentErrorCodes.ExamGradeNotFound, await ReadProblemCodeAsync(crossOrganization));

        using var studentGrade = await studentClient.PostAsync(
            $"/api/v1/academic/exam-attempts/{attempt.ExamAttemptId}/grading",
            null);
        Assert.Equal(HttpStatusCode.Forbidden, studentGrade.StatusCode);
        Assert.Equal(AssessmentErrorCodes.ExamGradeNotAllowed, await ReadProblemCodeAsync(studentGrade));
        using var studentRelease = await studentClient.PostAsJsonAsync(
            $"/api/v1/academic/exam-attempts/{attempt.ExamAttemptId}/grade/release",
            new ReleaseExamGradeCommand(Guid.NewGuid(), managerGrade.RowVersion),
            JsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, studentRelease.StatusCode);

        using var parentClient = _factory.CreateClient();
        _ = await CreateGradingParentAsync(
            parentClient,
            manager.OrganizationId,
            student.OrganizationPersonId);
        using var parentGrade = await parentClient.PostAsync(
            $"/api/v1/academic/exam-attempts/{attempt.ExamAttemptId}/grading",
            null);
        using var parentRelease = await parentClient.PostAsJsonAsync(
            $"/api/v1/academic/exam-attempts/{attempt.ExamAttemptId}/grade/release",
            new ReleaseExamGradeCommand(Guid.NewGuid(), Convert.ToBase64String(new byte[8])),
            JsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, parentGrade.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, parentRelease.StatusCode);

        using var anonymousClient = _factory.CreateClient();
        using var anonymous = await anonymousClient.GetAsync(
            $"/api/v1/academic/exam-attempts/{attempt.ExamAttemptId}/grading");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task MixedExamRequiresManualReviewAndReleaseBeforeSafeResultVisibility()
    {
        using var managerClient = _factory.CreateClient();
        var manager = await CreateManagerAsync(managerClient);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);
        var exam = await CreateMixedGradingExamAsync(managerClient, academicClass.ClassId);
        using var studentClient = _factory.CreateClient();
        var student = await CreateStudentAsync(
            studentClient,
            manager.OrganizationId,
            academicClass.ClassId);
        var attempt = await StartAttemptAsync(studentClient, exam.Id);
        var lease = await AcquireLeaseAsync(studentClient, attempt.ExamAttemptId);
        var first = attempt.Questions.Single(question => question.Prompt == "mixed-objective-correct");
        var second = attempt.Questions.Single(question => question.Prompt == "mixed-objective-wrong");
        var manual = attempt.Questions.Single(question => question.Prompt == "mixed-descriptive");
        _ = await SaveAnswerAsync(
            studentClient,
            attempt.ExamAttemptId,
            first.AttemptQuestionId,
            new SaveExamAnswerCommand(
                Guid.NewGuid(),
                lease.WriteLeaseVersion,
                null,
                first.Options.Single(option => option.Text == SecretAnswerKey).Id,
                null));
        _ = await SaveAnswerAsync(
            studentClient,
            attempt.ExamAttemptId,
            second.AttemptQuestionId,
            new SaveExamAnswerCommand(
                Guid.NewGuid(),
                lease.WriteLeaseVersion,
                null,
                second.Options.Single(option => option.Text == "mixed-wrong").Id,
                null));
        _ = await SaveAnswerAsync(
            studentClient,
            attempt.ExamAttemptId,
            manual.AttemptQuestionId,
            new SaveExamAnswerCommand(
                Guid.NewGuid(),
                lease.WriteLeaseVersion,
                null,
                null,
                "پاسخ تشریحی دانش‌آموز"));
        _ = await FinalizeAsync(
            studentClient,
            attempt.ExamAttemptId,
            new FinalizeExamCommand(Guid.NewGuid(), 3, lease.WriteLeaseVersion));
        using var teacherClient = _factory.CreateClient();
        _ = await CreateTeacherAsync(
            teacherClient,
            manager.OrganizationId,
            academicClass.ClassId,
            assigned: true);
        var grade = await InitializeGradeAsync(teacherClient, attempt.ExamAttemptId);
        Assert.Equal(2m, grade.TotalScore);
        Assert.False(grade.Questions.Single(question =>
            question.ExamAttemptQuestionId == manual.AttemptQuestionId).IsReviewed);

        using var authorizedParentClient = _factory.CreateClient();
        var authorizedParent = await CreateGradingParentAsync(
            authorizedParentClient,
            manager.OrganizationId,
            student.OrganizationPersonId);
        using var unrelatedParentClient = _factory.CreateClient();
        _ = await CreateParentAsync(
            unrelatedParentClient,
            manager.OrganizationId,
            academicClass.ClassId);

        using var studentBefore = await studentClient.GetAsync(
            $"/api/v1/academic/exam-attempts/{attempt.ExamAttemptId}/result");
        using var parentBefore = await authorizedParentClient.GetAsync(
            $"/api/v1/academic/exam-attempts/{attempt.ExamAttemptId}/guardian-result");
        var studentBeforeBody = await studentBefore.Content.ReadAsStringAsync();
        var parentBeforeBody = await parentBefore.Content.ReadAsStringAsync();
        studentBefore.EnsureSuccessStatusCode();
        parentBefore.EnsureSuccessStatusCode();
        var studentBeforeResult = await ReadRequiredAsync<StudentExamResultDto>(studentBefore);
        var parentBeforeResult = await ReadRequiredAsync<GuardianExamResultDto>(parentBefore);
        Assert.Equal(ExamResultReleaseStatus.AwaitingRelease, studentBeforeResult.ReleaseStatus);
        Assert.Null(studentBeforeResult.Score);
        Assert.Null(parentBeforeResult.Score);
        AssertSafeResult(studentBeforeBody);
        AssertSafeResult(parentBeforeBody);

        using var unrelated = await unrelatedParentClient.GetAsync(
            $"/api/v1/academic/exam-attempts/{attempt.ExamAttemptId}/guardian-result");
        Assert.Equal(HttpStatusCode.NotFound, unrelated.StatusCode);

        using var incompleteRelease = await teacherClient.PostAsJsonAsync(
            $"/api/v1/academic/exam-attempts/{attempt.ExamAttemptId}/grade/release",
            new ReleaseExamGradeCommand(Guid.NewGuid(), grade.RowVersion),
            JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, incompleteRelease.StatusCode);
        Assert.Equal(AssessmentErrorCodes.ExamGradeIncomplete, await ReadProblemCodeAsync(incompleteRelease));

        foreach (var invalidScore in new[] { -0.01m, 5.01m })
        {
            using var invalid = await teacherClient.PatchAsJsonAsync(
                $"/api/v1/academic/exam-attempts/{attempt.ExamAttemptId}/grading/questions/{manual.AttemptQuestionId}",
                new GradeExamQuestionCommand(invalidScore, null, null, grade.RowVersion),
                JsonOptions);
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            Assert.Equal(AssessmentErrorCodes.ExamGradeScoreInvalid, await ReadProblemCodeAsync(invalid));
        }

        grade = await GradeQuestionAsync(
            teacherClient,
            attempt.ExamAttemptId,
            manual.AttemptQuestionId,
            new GradeExamQuestionCommand(
                4m,
                "بازخورد سؤال تشریحی",
                PrivateEvaluatorNote,
                grade.RowVersion));
        Assert.Equal(6m, grade.TotalScore);
        Assert.True(grade.TotalScore <= grade.MaximumScore);
        grade = await CompleteGradeAsync(
            teacherClient,
            attempt.ExamAttemptId,
            new CompleteExamGradeCommand(
                "بازخورد دانش‌آموز",
                "بازخورد والد",
                PrivateEvaluatorNote,
                grade.RowVersion));
        Assert.Equal(ExamGradeRevisionStatus.ReadyForRelease, grade.Status);
        var release = await ReleaseGradeAsync(
            teacherClient,
            attempt.ExamAttemptId,
            new ReleaseExamGradeCommand(Guid.NewGuid(), grade.RowVersion));
        Assert.Equal(6m, release.TotalScore);
        Assert.Equal(10m, release.MaximumScore);

        using var studentAfter = await studentClient.GetAsync(
            $"/api/v1/academic/exam-attempts/{attempt.ExamAttemptId}/result");
        using var parentAfter = await authorizedParentClient.GetAsync(
            $"/api/v1/academic/exam-attempts/{attempt.ExamAttemptId}/guardian-result");
        var studentAfterBody = await studentAfter.Content.ReadAsStringAsync();
        var parentAfterBody = await parentAfter.Content.ReadAsStringAsync();
        studentAfter.EnsureSuccessStatusCode();
        parentAfter.EnsureSuccessStatusCode();
        var studentAfterResult = await ReadRequiredAsync<StudentExamResultDto>(studentAfter);
        var parentAfterResult = await ReadRequiredAsync<GuardianExamResultDto>(parentAfter);
        Assert.Equal(6m, studentAfterResult.Score);
        Assert.Equal(10m, studentAfterResult.MaximumScore);
        Assert.Equal("بازخورد دانش‌آموز", studentAfterResult.LearnerFeedback);
        Assert.Equal(6m, parentAfterResult.Score);
        Assert.Equal("بازخورد والد", parentAfterResult.GuardianVisibleFeedback);
        AssertSafeResult(studentAfterBody);
        AssertSafeResult(parentAfterBody);

        var persistence = await _factory.GetExamGradePersistenceStateAsync(attempt.ExamAttemptId);
        Assert.Single(persistence.Releases);
        Assert.Equal(6m, Assert.Single(persistence.Revisions).TotalScore);
        Assert.Equal(
            4m,
            persistence.QuestionGrades.Single(question =>
                question.ExamAttemptQuestionId == manual.AttemptQuestionId).AwardedScore);

        await _factory.RevokeGuardianRelationAsync(authorizedParent.GuardianRelationId);
        using var revoked = await authorizedParentClient.GetAsync(
            $"/api/v1/academic/exam-attempts/{attempt.ExamAttemptId}/guardian-result");
        Assert.Equal(HttpStatusCode.Forbidden, revoked.StatusCode);
    }

    [Fact]
    public async Task StaleEvaluatorEditConflictsAndConcurrentReleaseIsIdempotent()
    {
        var scenario = await CreateFinalizedDescriptiveScenarioAsync(10m);
        using var studentClient = scenario.StudentClient;
        using var firstTeacherClient = _factory.CreateClient();
        _ = await CreateTeacherAsync(
            firstTeacherClient,
            scenario.Manager.OrganizationId,
            scenario.ClassId,
            assigned: true);
        using var secondTeacherClient = _factory.CreateClient();
        _ = await CreateTeacherAsync(
            secondTeacherClient,
            scenario.Manager.OrganizationId,
            scenario.ClassId,
            assigned: true);
        var firstView = await InitializeGradeAsync(firstTeacherClient, scenario.Attempt.ExamAttemptId);
        var secondView = await secondTeacherClient.GetFromJsonAsync<ExamGradeDto>(
            $"/api/v1/academic/exam-attempts/{scenario.Attempt.ExamAttemptId}/grading",
            JsonOptions);
        Assert.Equal(firstView.RowVersion, secondView!.RowVersion);

        var saved = await GradeQuestionAsync(
            firstTeacherClient,
            scenario.Attempt.ExamAttemptId,
            scenario.Question.AttemptQuestionId,
            new GradeExamQuestionCommand(7m, null, null, firstView.RowVersion));
        using var stale = await secondTeacherClient.PatchAsJsonAsync(
            $"/api/v1/academic/exam-attempts/{scenario.Attempt.ExamAttemptId}/grading/questions/{scenario.Question.AttemptQuestionId}",
            new GradeExamQuestionCommand(8m, null, null, secondView.RowVersion),
            JsonOptions);
        Assert.Equal(HttpStatusCode.PreconditionFailed, stale.StatusCode);
        Assert.Equal(AssessmentErrorCodes.ConcurrencyConflict, await ReadProblemCodeAsync(stale));

        var ready = await CompleteGradeAsync(
            firstTeacherClient,
            scenario.Attempt.ExamAttemptId,
            new CompleteExamGradeCommand(null, null, null, saved.RowVersion));
        var operationId = Guid.NewGuid();
        var command = new ReleaseExamGradeCommand(operationId, ready.RowVersion);
        var releaseResponses = await Task.WhenAll(
            firstTeacherClient.PostAsJsonAsync(
                $"/api/v1/academic/exam-attempts/{scenario.Attempt.ExamAttemptId}/grade/release",
                command,
                JsonOptions),
            secondTeacherClient.PostAsJsonAsync(
                $"/api/v1/academic/exam-attempts/{scenario.Attempt.ExamAttemptId}/grade/release",
                command,
                JsonOptions));
        try
        {
            Assert.All(releaseResponses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
            var firstRelease = await ReadRequiredAsync<ExamGradeReleaseDto>(releaseResponses[0]);
            var concurrentRetry = await ReadRequiredAsync<ExamGradeReleaseDto>(releaseResponses[1]);
            Assert.Equal(firstRelease, concurrentRetry);

            var lostAcknowledgementRetry = await ReleaseGradeAsync(
                firstTeacherClient,
                scenario.Attempt.ExamAttemptId,
                command);
            Assert.Equal(firstRelease, lostAcknowledgementRetry);
            var state = await _factory.GetExamGradePersistenceStateAsync(scenario.Attempt.ExamAttemptId);
            Assert.Single(state.Releases);
            Assert.Equal(7m, Assert.Single(state.Revisions).TotalScore);
        }
        finally
        {
            foreach (var response in releaseResponses)
            {
                response.Dispose();
            }
        }

        using var incompatibleRetry = await firstTeacherClient.PostAsJsonAsync(
            $"/api/v1/academic/exam-attempts/{scenario.Attempt.ExamAttemptId}/grade/release",
            command with { ExpectedGradeRowVersion = Convert.ToBase64String(new byte[8]) },
            JsonOptions);
        Assert.Equal(HttpStatusCode.Conflict, incompatibleRetry.StatusCode);
        Assert.Equal(
            AssessmentErrorCodes.ExamGradeReleaseIdempotencyConflict,
            await ReadProblemCodeAsync(incompatibleRetry));
    }

    [Fact]
    public async Task CorrectionPreservesReleasedRevisionUntilReplacementIsReleased()
    {
        var scenario = await CreateFinalizedDescriptiveScenarioAsync(20m);
        using var studentClient = scenario.StudentClient;
        using var teacherClient = _factory.CreateClient();
        _ = await CreateTeacherAsync(
            teacherClient,
            scenario.Manager.OrganizationId,
            scenario.ClassId,
            assigned: true);
        var grade = await InitializeGradeAsync(teacherClient, scenario.Attempt.ExamAttemptId);
        grade = await GradeQuestionAsync(
            teacherClient,
            scenario.Attempt.ExamAttemptId,
            scenario.Question.AttemptQuestionId,
            new GradeExamQuestionCommand(16m, null, null, grade.RowVersion));
        grade = await CompleteGradeAsync(
            teacherClient,
            scenario.Attempt.ExamAttemptId,
            new CompleteExamGradeCommand("نسخه اول", null, PrivateEvaluatorNote, grade.RowVersion));
        var firstRelease = await ReleaseGradeAsync(
            teacherClient,
            scenario.Attempt.ExamAttemptId,
            new ReleaseExamGradeCommand(Guid.NewGuid(), grade.RowVersion));
        var firstVisible = await scenario.StudentClient.GetFromJsonAsync<StudentExamResultDto>(
            $"/api/v1/academic/exam-attempts/{scenario.Attempt.ExamAttemptId}/result",
            JsonOptions);
        Assert.Equal(16m, firstVisible!.Score);

        using var correctionResponse = await teacherClient.PostAsJsonAsync(
            $"/api/v1/academic/exam-attempts/{scenario.Attempt.ExamAttemptId}/grade/corrections",
            new CorrectReleasedExamGradeCommand(
                "اصلاح مصوب نمره تشریحی",
                firstRelease.GradeRowVersion),
            JsonOptions);
        Assert.Equal(HttpStatusCode.Created, correctionResponse.StatusCode);
        var correction = await ReadRequiredAsync<ExamGradeDto>(correctionResponse);
        Assert.Equal(2, correction.RevisionNumber);
        Assert.Equal(ExamGradeRevisionStatus.Draft, correction.Status);
        Assert.Equal("اصلاح مصوب نمره تشریحی", correction.CorrectionReason);
        Assert.Equal(grade.ExamGradeRevisionId, correction.SupersedesExamGradeRevisionId);

        var beforeReplacementRelease = await scenario.StudentClient.GetFromJsonAsync<StudentExamResultDto>(
            $"/api/v1/academic/exam-attempts/{scenario.Attempt.ExamAttemptId}/result",
            JsonOptions);
        Assert.Equal(16m, beforeReplacementRelease!.Score);
        Assert.Equal(1, beforeReplacementRelease.GradeRevisionNumber);

        correction = await GradeQuestionAsync(
            teacherClient,
            scenario.Attempt.ExamAttemptId,
            scenario.Question.AttemptQuestionId,
            new GradeExamQuestionCommand(18m, null, null, correction.RowVersion));
        correction = await CompleteGradeAsync(
            teacherClient,
            scenario.Attempt.ExamAttemptId,
            new CompleteExamGradeCommand("نسخه اصلاح‌شده", null, null, correction.RowVersion));
        var stillFirst = await scenario.StudentClient.GetFromJsonAsync<StudentExamResultDto>(
            $"/api/v1/academic/exam-attempts/{scenario.Attempt.ExamAttemptId}/result",
            JsonOptions);
        Assert.Equal(16m, stillFirst!.Score);

        _ = await ReleaseGradeAsync(
            teacherClient,
            scenario.Attempt.ExamAttemptId,
            new ReleaseExamGradeCommand(Guid.NewGuid(), correction.RowVersion));
        var replacement = await scenario.StudentClient.GetFromJsonAsync<StudentExamResultDto>(
            $"/api/v1/academic/exam-attempts/{scenario.Attempt.ExamAttemptId}/result",
            JsonOptions);
        Assert.Equal(18m, replacement!.Score);
        Assert.Equal(2, replacement.GradeRevisionNumber);

        var state = await _factory.GetExamGradePersistenceStateAsync(scenario.Attempt.ExamAttemptId);
        Assert.Equal(2, state.Revisions.Count);
        Assert.Equal(2, state.Releases.Count);
        var history = state.Revisions.OrderBy(item => item.RevisionNumber).ToArray();
        Assert.Equal(ExamGradeRevisionStatus.Superseded, history[0].Status);
        Assert.Equal(16m, history[0].TotalScore);
        Assert.Equal(ExamGradeRevisionStatus.Released, history[1].Status);
        Assert.Equal(18m, history[1].TotalScore);
    }

    private async Task<FinalizedDescriptiveScenario> CreateFinalizedDescriptiveScenarioAsync(
        decimal maximumScore)
    {
        var managerClient = _factory.CreateClient();
        var manager = await CreateManagerAsync(managerClient);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);
        var exam = await CreateSingleDescriptiveExamAsync(
            managerClient,
            academicClass.ClassId,
            maximumScore);
        var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, manager.OrganizationId, academicClass.ClassId);
        var attempt = await StartAttemptAsync(studentClient, exam.Id);
        var question = Assert.Single(attempt.Questions);
        var lease = await AcquireLeaseAsync(studentClient, attempt.ExamAttemptId);
        _ = await SaveAnswerAsync(
            studentClient,
            attempt.ExamAttemptId,
            question.AttemptQuestionId,
            new SaveExamAnswerCommand(
                Guid.NewGuid(),
                lease.WriteLeaseVersion,
                null,
                null,
                "پاسخ تشریحی"));
        _ = await FinalizeAsync(
            studentClient,
            attempt.ExamAttemptId,
            new FinalizeExamCommand(Guid.NewGuid(), 1, lease.WriteLeaseVersion));
        managerClient.Dispose();
        return new FinalizedDescriptiveScenario(
            manager,
            academicClass.ClassId,
            attempt,
            question,
            studentClient);
    }

    private static async Task<ExamEditorDto> CreateObjectiveGradingExamAsync(
        HttpClient managerClient,
        Guid classId)
    {
        var exam = await CreateDraftAsync(managerClient, classId, 6m);
        exam = await AddGradingObjectiveQuestionAsync(
            managerClient,
            exam,
            1,
            "objective-correct",
            2m,
            SecretAnswerKey,
            "wrong-one");
        exam = await AddGradingObjectiveQuestionAsync(
            managerClient,
            exam,
            2,
            "objective-incorrect",
            2m,
            "correct-two",
            "wrong-two");
        exam = await AddGradingObjectiveQuestionAsync(
            managerClient,
            exam,
            3,
            "objective-unanswered",
            2m,
            "correct-three",
            "wrong-three");
        return await PublishAsync(managerClient, exam);
    }

    private static async Task<ExamEditorDto> CreateMixedGradingExamAsync(
        HttpClient managerClient,
        Guid classId)
    {
        var exam = await CreateDraftAsync(managerClient, classId, 10m);
        exam = await AddGradingObjectiveQuestionAsync(
            managerClient,
            exam,
            1,
            "mixed-objective-correct",
            2m,
            SecretAnswerKey,
            "mixed-wrong-one");
        exam = await AddGradingObjectiveQuestionAsync(
            managerClient,
            exam,
            2,
            "mixed-objective-wrong",
            3m,
            "mixed-correct-two",
            "mixed-wrong");
        using var response = await managerClient.PostAsJsonAsync(
            $"/api/v1/academic/exams/{exam.Id}/questions",
            new AddExamQuestionCommand(
                3,
                ExamQuestionType.Descriptive,
                "mixed-descriptive",
                5m,
                null,
                exam.VersionRowVersion),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        exam = await ReadRequiredAsync<ExamEditorDto>(response);
        return await PublishAsync(managerClient, exam);
    }

    private static async Task<ExamEditorDto> CreateSingleDescriptiveExamAsync(
        HttpClient managerClient,
        Guid classId,
        decimal maximumScore)
    {
        var exam = await CreateDraftAsync(managerClient, classId, maximumScore);
        using var response = await managerClient.PostAsJsonAsync(
            $"/api/v1/academic/exams/{exam.Id}/questions",
            new AddExamQuestionCommand(
                1,
                ExamQuestionType.Descriptive,
                "single-descriptive",
                maximumScore,
                null,
                exam.VersionRowVersion),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        exam = await ReadRequiredAsync<ExamEditorDto>(response);
        return await PublishAsync(managerClient, exam);
    }

    private static async Task<ExamEditorDto> AddGradingObjectiveQuestionAsync(
        HttpClient client,
        ExamEditorDto exam,
        int order,
        string prompt,
        decimal score,
        string correctText,
        string wrongText)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/v1/academic/exams/{exam.Id}/questions",
            new AddExamQuestionCommand(
                order,
                ExamQuestionType.ObjectiveSingleChoice,
                prompt,
                score,
                [
                    new ExamQuestionOptionCommand(1, wrongText, false),
                    new ExamQuestionOptionCommand(2, correctText, true)
                ],
                exam.VersionRowVersion),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<ExamEditorDto>(response);
    }

    private async Task<GradingParentContext> CreateGradingParentAsync(
        HttpClient client,
        Guid organizationId,
        Guid studentOrganizationPersonId)
    {
        var user = await CreateAuthenticatedUserAsync(client);
        var membership = await _factory.CreateMembershipAsync(
            user.User.Id,
            organizationId,
            OrganizationRole.Parent);
        var relationId = await _factory.CreateGuardianRelationAsync(
            user.User.Id,
            organizationId,
            studentOrganizationPersonId);
        UseBearerToken(client, user.AccessToken);
        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Parent);
        using var selectResponse = await client.PostAsync(
            $"/api/v1/guardian/children/{studentOrganizationPersonId}/select",
            null);
        selectResponse.EnsureSuccessStatusCode();
        return new GradingParentContext(relationId);
    }

    private static async Task<ExamGradeDto> InitializeGradeAsync(HttpClient client, Guid attemptId)
    {
        using var response = await client.PostAsync(
            $"/api/v1/academic/exam-attempts/{attemptId}/grading",
            null);
        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<ExamGradeDto>(response);
    }

    private static async Task<ExamGradeDto> GradeQuestionAsync(
        HttpClient client,
        Guid attemptId,
        Guid attemptQuestionId,
        GradeExamQuestionCommand command)
    {
        using var response = await client.PatchAsJsonAsync(
            $"/api/v1/academic/exam-attempts/{attemptId}/grading/questions/{attemptQuestionId}",
            command,
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<ExamGradeDto>(response);
    }

    private static async Task<ExamGradeDto> CompleteGradeAsync(
        HttpClient client,
        Guid attemptId,
        CompleteExamGradeCommand command)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/v1/academic/exam-attempts/{attemptId}/grading/complete",
            command,
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<ExamGradeDto>(response);
    }

    private static async Task<ExamGradeReleaseDto> ReleaseGradeAsync(
        HttpClient client,
        Guid attemptId,
        ReleaseExamGradeCommand command)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/v1/academic/exam-attempts/{attemptId}/grade/release",
            command,
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<ExamGradeReleaseDto>(response);
    }

    private static void AssertSafeResult(string json)
    {
        Assert.DoesNotContain(SecretAnswerKey, json, StringComparison.Ordinal);
        Assert.DoesNotContain(PrivateEvaluatorNote, json, StringComparison.Ordinal);
        Assert.DoesNotContain("correctOption", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("answerKey", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("evaluatorPrivateNote", json, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record GradingParentContext(Guid GuardianRelationId);

    private sealed record FinalizedDescriptiveScenario(
        ManagerContext Manager,
        Guid ClassId,
        StudentExamAttemptDto Attempt,
        StudentExamAttemptQuestionDto Question,
        HttpClient StudentClient);
}
