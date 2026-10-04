using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Assessment;
using MakanApp.Domain.Assessment;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class AssignmentEndpointsTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(-0.01)]
    public async Task NegativeEvaluationScoreIsRejected(double invalidScore)
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateEvaluationScenarioAsync(managerClient, studentClient);

        using var response = await managerClient.PutAsJsonAsync(
            $"/api/v1/academic/submission-attempts/{scenario.Attempt.Id}/evaluation",
            new SaveEvaluationDraftCommand((decimal)invalidScore, null, null, null, null),
            JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(AssessmentErrorCodes.EvaluationScoreInvalid, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task ScoreAboveAssignmentVersionMaximumIsRejected()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateEvaluationScenarioAsync(managerClient, studentClient, maxScore: 10m);

        using var response = await managerClient.PutAsJsonAsync(
            $"/api/v1/academic/submission-attempts/{scenario.Attempt.Id}/evaluation",
            new SaveEvaluationDraftCommand(11m, null, null, null, null),
            JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(AssessmentErrorCodes.EvaluationScoreInvalid, await ReadProblemCodeAsync(response));
    }

    [Theory]
    [InlineData(10, 10)]
    [InlineData(17.25, 20)]
    [InlineData(18, 20)]
    public async Task ValidExactAndDecimalScoresAreAccepted(double score, double maxScore)
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateEvaluationScenarioAsync(
            managerClient,
            studentClient,
            maxScore: (decimal)maxScore);

        var evaluation = await SaveEvaluationAsync(
            managerClient,
            scenario.Attempt.Id,
            score: (decimal)score);

        Assert.Equal((decimal)score, evaluation.Score);
        Assert.Equal((decimal)maxScore, evaluation.MaxScore);
        Assert.Equal((decimal)maxScore,
            await _factory.GetAssignmentMaximumScoreAsync(scenario.Submission.Assignment.VersionId));
    }

    [Fact]
    public async Task SavedDraftRemainsInvisibleToStudentAndParent()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateEvaluationScenarioAsync(managerClient, studentClient);
        const string privateNote = "PRIVATE-BEFORE-RELEASE-98127";
        _ = await SaveEvaluationAsync(
            managerClient,
            scenario.Attempt.Id,
            privateNote: privateNote);
        var evaluatorView = await managerClient.GetFromJsonAsync<EvaluatorEvaluationResult>(
            $"/api/v1/academic/submission-attempts/{scenario.Attempt.Id}/evaluation",
            JsonOptions);
        using var parentClient = _factory.CreateClient();
        _ = await CreateParentForStudentAsync(parentClient, scenario.Submission);

        using var studentResponse = await studentClient.GetAsync(
            $"/api/v1/academic/submission-attempts/{scenario.Attempt.Id}/result");
        using var parentResponse = await parentClient.GetAsync(
            $"/api/v1/academic/submission-attempts/{scenario.Attempt.Id}/guardian-result");
        var studentPayload = await studentResponse.Content.ReadAsStringAsync();
        var parentPayload = await parentResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, studentResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, parentResponse.StatusCode);
        Assert.Equal(privateNote, evaluatorView!.TeacherPrivateNote);
        Assert.DoesNotContain(privateNote, studentPayload, StringComparison.Ordinal);
        Assert.DoesNotContain(privateNote, parentPayload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReleaseExposesOnlyAudienceApprovedFieldsAndKeepsPrivateNotePrivate()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateEvaluationScenarioAsync(managerClient, studentClient);
        const string learnerFeedback = "LEARNER-ONLY-FEEDBACK";
        const string guardianFeedback = "GUARDIAN-ONLY-FEEDBACK";
        const string privateNote = "PRIVATE-DISTINCTIVE-771923";
        var draft = await SaveEvaluationAsync(
            managerClient,
            scenario.Attempt.Id,
            17.25m,
            learnerFeedback,
            guardianFeedback,
            privateNote);
        _ = await ReleaseEvaluationAsync(managerClient, scenario.Attempt.Id, draft.RowVersion);
        using var parentClient = _factory.CreateClient();
        _ = await CreateParentForStudentAsync(parentClient, scenario.Submission);

        using var studentResponse = await studentClient.GetAsync(
            $"/api/v1/academic/submission-attempts/{scenario.Attempt.Id}/result");
        using var parentResponse = await parentClient.GetAsync(
            $"/api/v1/academic/submission-attempts/{scenario.Attempt.Id}/guardian-result");
        var studentPayload = await studentResponse.Content.ReadAsStringAsync();
        var parentPayload = await parentResponse.Content.ReadAsStringAsync();
        var studentResult = await studentResponse.Content.ReadFromJsonAsync<StudentReleasedResult>(JsonOptions);
        var parentResult = await parentResponse.Content.ReadFromJsonAsync<ParentReleasedResult>(JsonOptions);
        var stored = Assert.Single(await _factory.GetEvaluationRevisionsAsync(scenario.Attempt.Id));

        Assert.Equal(HttpStatusCode.OK, studentResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, parentResponse.StatusCode);
        Assert.Equal(17.25m, studentResult!.Score);
        Assert.Equal(learnerFeedback, studentResult.LearnerFeedback);
        Assert.Equal(guardianFeedback, parentResult!.GuardianVisibleFeedback);
        Assert.DoesNotContain(privateNote, studentPayload, StringComparison.Ordinal);
        Assert.DoesNotContain(privateNote, parentPayload, StringComparison.Ordinal);
        Assert.DoesNotContain("guardianVisibleFeedback", studentPayload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("learnerFeedback", parentPayload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("teacherPrivateNote", studentPayload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("teacherPrivateNote", parentPayload, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(privateNote, stored.TeacherPrivateNote);
    }

    [Fact]
    public async Task ReleaseRetryReturnsSameEffectiveReleaseWithoutDuplicate()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateEvaluationScenarioAsync(managerClient, studentClient);
        var draft = await SaveEvaluationAsync(managerClient, scenario.Attempt.Id);

        var first = await ReleaseEvaluationAsync(managerClient, scenario.Attempt.Id, draft.RowVersion);
        var retry = await ReleaseEvaluationAsync(managerClient, scenario.Attempt.Id, draft.RowVersion);

        Assert.Equal(first.GradeReleaseId, retry.GradeReleaseId);
        Assert.Equal(first.EvaluationRevisionId, retry.EvaluationRevisionId);
        Assert.Equal(first.ReleasedAtUtc, retry.ReleasedAtUtc);
        Assert.Equal(1, await _factory.CountGradeReleasesAsync(scenario.Attempt.Id));
    }

    [Fact]
    public async Task ReleasedEvaluationCannotBeEditedDirectly()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateEvaluationScenarioAsync(managerClient, studentClient);
        var draft = await SaveEvaluationAsync(managerClient, scenario.Attempt.Id);
        var released = await ReleaseEvaluationAsync(managerClient, scenario.Attempt.Id, draft.RowVersion);

        using var response = await managerClient.PutAsJsonAsync(
            $"/api/v1/academic/submission-attempts/{scenario.Attempt.Id}/evaluation",
            new SaveEvaluationDraftCommand(
                19m,
                "rewrite",
                "rewrite",
                "rewrite",
                released.EvaluationRowVersion),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(AssessmentErrorCodes.EvaluationAlreadyReleased, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task CorrectionRequiresReason()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateEvaluationScenarioAsync(managerClient, studentClient);
        var draft = await SaveEvaluationAsync(managerClient, scenario.Attempt.Id, score: 15m);
        var released = await ReleaseEvaluationAsync(managerClient, scenario.Attempt.Id, draft.RowVersion);

        using var response = await managerClient.PostAsJsonAsync(
            $"/api/v1/academic/submission-attempts/{scenario.Attempt.Id}/evaluation/corrections",
            new CorrectReleasedEvaluationCommand(
                17m,
                null,
                null,
                null,
                " ",
                released.EvaluationRowVersion),
            JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            AssessmentErrorCodes.EvaluationCorrectionReasonRequired,
            await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task CorrectionPreservesHistoryAndBecomesVisibleOnlyAfterSecondRelease()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateEvaluationScenarioAsync(managerClient, studentClient);
        var revisionOne = await SaveEvaluationAsync(managerClient, scenario.Attempt.Id, score: 15m);
        var firstRelease = await ReleaseEvaluationAsync(
            managerClient,
            scenario.Attempt.Id,
            revisionOne.RowVersion);
        var correction = await CreateCorrectionAsync(
            managerClient,
            scenario.Attempt.Id,
            firstRelease.EvaluationRowVersion,
            score: 17m);

        var beforeRelease = await studentClient.GetFromJsonAsync<StudentReleasedResult>(
            $"/api/v1/academic/submission-attempts/{scenario.Attempt.Id}/result",
            JsonOptions);
        var secondRelease = await ReleaseEvaluationAsync(
            managerClient,
            scenario.Attempt.Id,
            correction.RowVersion);
        var afterRelease = await studentClient.GetFromJsonAsync<StudentReleasedResult>(
            $"/api/v1/academic/submission-attempts/{scenario.Attempt.Id}/result",
            JsonOptions);
        var history = await _factory.GetEvaluationRevisionsAsync(scenario.Attempt.Id);

        Assert.Equal(15m, beforeRelease!.Score);
        Assert.Equal(1, beforeRelease.EvaluationRevisionNumber);
        Assert.Equal(17m, afterRelease!.Score);
        Assert.Equal(2, afterRelease.EvaluationRevisionNumber);
        Assert.Equal(2, secondRelease.RevisionNumber);
        Assert.Equal(2, history.Length);
        Assert.Equal(EvaluationRevisionStatus.Superseded, history[0].Status);
        Assert.Equal(EvaluationRevisionStatus.Released, history[1].Status);
        Assert.Equal(history[0].Id, history[1].SupersedesEvaluationRevisionId);
        Assert.Equal(2, await _factory.CountGradeReleasesAsync(scenario.Attempt.Id));
    }

    [Fact]
    public async Task UnrelatedParentCannotReadReleasedResult()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateEvaluationScenarioAsync(managerClient, studentClient);
        var draft = await SaveEvaluationAsync(managerClient, scenario.Attempt.Id);
        _ = await ReleaseEvaluationAsync(managerClient, scenario.Attempt.Id, draft.RowVersion);
        using var otherParentClient = _factory.CreateClient();
        _ = await CreateParentAsync(
            otherParentClient,
            scenario.Submission.OrganizationId,
            scenario.Submission.ClassId);

        using var response = await otherParentClient.GetAsync(
            $"/api/v1/academic/submission-attempts/{scenario.Attempt.Id}/guardian-result");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(AssessmentErrorCodes.EvaluationNotFound, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task RevokedGuardianRelationBlocksFutureResultAccess()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateEvaluationScenarioAsync(managerClient, studentClient);
        var draft = await SaveEvaluationAsync(managerClient, scenario.Attempt.Id);
        _ = await ReleaseEvaluationAsync(managerClient, scenario.Attempt.Id, draft.RowVersion);
        using var parentClient = _factory.CreateClient();
        var parent = await CreateParentForStudentAsync(parentClient, scenario.Submission);
        using var before = await parentClient.GetAsync(
            $"/api/v1/academic/submission-attempts/{scenario.Attempt.Id}/guardian-result");

        await _factory.RevokeGuardianRelationAsync(parent.GuardianRelationId);
        using var after = await parentClient.GetAsync(
            $"/api/v1/academic/submission-attempts/{scenario.Attempt.Id}/guardian-result");

        Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, after.StatusCode);
    }

    [Fact]
    public async Task PublishedAssignmentMaximumScoreCannotBeSilentlyRewritten()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateEvaluationScenarioAsync(managerClient, studentClient, maxScore: 20m);
        var assignment = scenario.Submission.Assignment;

        using var response = await managerClient.PatchAsJsonAsync(
            $"/api/v1/academic/assignments/{assignment.Id}",
            new UpdateAssignmentDraftCommand(
                assignment.Title,
                assignment.Description,
                assignment.DueAtUtc,
                assignment.AllowLateSubmission,
                assignment.MaxAttempts,
                10m,
                assignment.AssignmentRowVersion,
                assignment.VersionRowVersion),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(20m, await _factory.GetAssignmentMaximumScoreAsync(assignment.VersionId));
    }
}
