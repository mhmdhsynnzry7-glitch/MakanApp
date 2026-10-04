using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Assessment;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class AssignmentEndpointsTests
{
    [Fact]
    public async Task StaleEvaluationDraftSaveReturnsExplicitConcurrencyConflict()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateEvaluationScenarioAsync(managerClient, studentClient);
        var original = await SaveEvaluationAsync(managerClient, scenario.Attempt.Id, score: 15m);
        _ = await SaveEvaluationAsync(
            managerClient,
            scenario.Attempt.Id,
            score: 17m,
            expectedRowVersion: original.RowVersion);

        using var staleResponse = await managerClient.PutAsJsonAsync(
            $"/api/v1/academic/submission-attempts/{scenario.Attempt.Id}/evaluation",
            new SaveEvaluationDraftCommand(
                14m,
                "stale",
                "stale",
                "stale",
                original.RowVersion),
            JsonOptions);
        var stored = Assert.Single(await _factory.GetEvaluationRevisionsAsync(scenario.Attempt.Id));

        Assert.Equal(HttpStatusCode.PreconditionFailed, staleResponse.StatusCode);
        Assert.Equal(AssessmentErrorCodes.ConcurrencyConflict, await ReadProblemCodeAsync(staleResponse));
        Assert.Equal(17m, stored.Score);
    }

    [Fact]
    public async Task SqlServerRowVersionRejectsTwoIndependentDbContextWrites()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateEvaluationScenarioAsync(managerClient, studentClient);
        var draft = await SaveEvaluationAsync(managerClient, scenario.Attempt.Id);

        Assert.True(await _factory.EvaluationRowVersionDetectsStaleWriteAsync(draft.Id));
    }

    [Fact]
    public async Task ConcurrentReleaseRequestsProduceOneEffectiveRelease()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateEvaluationScenarioAsync(managerClient, studentClient);
        var draft = await SaveEvaluationAsync(managerClient, scenario.Attempt.Id);
        var route = $"/api/v1/academic/submission-attempts/{scenario.Attempt.Id}/evaluation/release";
        var command = new ReleaseEvaluationCommand(draft.RowVersion);

        var responses = await Task.WhenAll(
            managerClient.PostAsJsonAsync(route, command, JsonOptions),
            managerClient.PostAsJsonAsync(route, command, JsonOptions));
        try
        {
            Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
            var results = new List<GradeReleaseResult>();
            foreach (var response in responses)
            {
                results.Add(await ReadRequiredAsync<GradeReleaseResult>(response));
            }

            Assert.Single(results.Select(result => result.GradeReleaseId).Distinct());
            Assert.Single(results.Select(result => result.EvaluationRevisionId).Distinct());
            Assert.Equal(1, await _factory.CountGradeReleasesAsync(scenario.Attempt.Id));
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
    public async Task ConcurrentInitialDraftRequestsCreateOnlyOneEvaluationRevision()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateEvaluationScenarioAsync(managerClient, studentClient);
        var route = $"/api/v1/academic/submission-attempts/{scenario.Attempt.Id}/evaluation";
        var command = new SaveEvaluationDraftCommand(17m, null, null, null, null);

        var responses = await Task.WhenAll(
            managerClient.PutAsJsonAsync(route, command, JsonOptions),
            managerClient.PutAsJsonAsync(route, command, JsonOptions));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.PreconditionFailed);
            Assert.Single(await _factory.GetEvaluationRevisionsAsync(scenario.Attempt.Id));
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
    public async Task ConcurrentCorrectionRequestsCreateOnlyOneDraftRevision()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateEvaluationScenarioAsync(managerClient, studentClient);
        var firstDraft = await SaveEvaluationAsync(managerClient, scenario.Attempt.Id, score: 15m);
        var release = await ReleaseEvaluationAsync(
            managerClient,
            scenario.Attempt.Id,
            firstDraft.RowVersion);
        var route = $"/api/v1/academic/submission-attempts/{scenario.Attempt.Id}/evaluation/corrections";
        var command = new CorrectReleasedEvaluationCommand(
            17m,
            null,
            null,
            null,
            "Concurrent correction",
            release.EvaluationRowVersion);

        var responses = await Task.WhenAll(
            managerClient.PostAsJsonAsync(route, command, JsonOptions),
            managerClient.PostAsJsonAsync(route, command, JsonOptions));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
            Assert.Equal(2, (await _factory.GetEvaluationRevisionsAsync(scenario.Attempt.Id)).Length);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }
}
