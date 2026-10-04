using System.Net.Http.Json;
using MakanApp.Application.Assessment;

namespace MakanApp.IntegrationTests;

public sealed partial class AssignmentEndpointsTests
{
    private async Task<EvaluationScenario> CreateEvaluationScenarioAsync(
        HttpClient managerClient,
        HttpClient studentClient,
        decimal maxScore = 20m,
        bool submit = true)
    {
        var scenario = await CreateSubmissionScenarioAsync(
            managerClient,
            studentClient,
            maxScore: maxScore);
        var attempt = await SaveSubmissionDraftAsync(
            studentClient,
            await CreateSubmissionDraftAsync(studentClient, scenario.Assignment.Id),
            "Final student answer");
        SubmissionReceipt? receipt = null;
        if (submit)
        {
            receipt = await SubmitAsync(studentClient, attempt);
        }

        return new EvaluationScenario(scenario, attempt, receipt);
    }

    private static async Task<EvaluatorEvaluationResult> SaveEvaluationAsync(
        HttpClient evaluatorClient,
        Guid attemptId,
        decimal score = 17.25m,
        string learnerFeedback = "Learner feedback",
        string guardianFeedback = "Guardian feedback",
        string privateNote = "PRIVATE-DISTINCTIVE-NOTE",
        string? expectedRowVersion = null)
    {
        using var response = await evaluatorClient.PutAsJsonAsync(
            $"/api/v1/academic/submission-attempts/{attemptId}/evaluation",
            new SaveEvaluationDraftCommand(
                score,
                learnerFeedback,
                guardianFeedback,
                privateNote,
                expectedRowVersion),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<EvaluatorEvaluationResult>(response);
    }

    private static async Task<GradeReleaseResult> ReleaseEvaluationAsync(
        HttpClient evaluatorClient,
        Guid attemptId,
        string expectedRowVersion)
    {
        using var response = await evaluatorClient.PostAsJsonAsync(
            $"/api/v1/academic/submission-attempts/{attemptId}/evaluation/release",
            new ReleaseEvaluationCommand(expectedRowVersion),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<GradeReleaseResult>(response);
    }

    private static async Task<EvaluatorEvaluationResult> CreateCorrectionAsync(
        HttpClient evaluatorClient,
        Guid attemptId,
        string expectedReleasedRowVersion,
        decimal score = 18m,
        string reason = "Approved calculation correction")
    {
        using var response = await evaluatorClient.PostAsJsonAsync(
            $"/api/v1/academic/submission-attempts/{attemptId}/evaluation/corrections",
            new CorrectReleasedEvaluationCommand(
                score,
                "Corrected learner feedback",
                "Corrected guardian feedback",
                "CORRECTION-PRIVATE-NOTE",
                reason,
                expectedReleasedRowVersion),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<EvaluatorEvaluationResult>(response);
    }

    private sealed record EvaluationScenario(
        SubmissionScenario Submission,
        SubmissionAttemptResult Attempt,
        SubmissionReceipt? Receipt);
}
