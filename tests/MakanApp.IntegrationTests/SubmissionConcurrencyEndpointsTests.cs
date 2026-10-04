using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Assessment;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class AssignmentEndpointsTests
{
    [Fact]
    public async Task StaleDraftSaveReturnsExplicitConcurrencyConflict()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateSubmissionScenarioAsync(managerClient, studentClient);
        var attempt = await CreateSubmissionDraftAsync(studentClient, scenario.Assignment.Id);
        _ = await SaveSubmissionDraftAsync(studentClient, attempt, "first device");

        using var response = await studentClient.PatchAsJsonAsync(
            $"/api/v1/academic/submission-attempts/{attempt.Id}/draft",
            new SaveSubmissionDraftCommand("stale device", attempt.RowVersion),
            JsonOptions);

        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
        Assert.Equal(
            AssessmentErrorCodes.ConcurrencyConflict,
            await ReadProblemCodeAsync(response));
        Assert.Equal("first device", (await _factory.GetSubmissionAttemptAsync(attempt.Id)).AnswerText);
    }

    [Fact]
    public async Task FinalSubmitRetryReturnsSamePersistedReceipt()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateSubmissionScenarioAsync(
            managerClient,
            studentClient,
            maxAttempts: 1);
        var attempt = await SaveSubmissionDraftAsync(
            studentClient,
            await CreateSubmissionDraftAsync(studentClient, scenario.Assignment.Id));

        var first = await SubmitAsync(studentClient, attempt);
        using var retryResponse = await studentClient.PostAsJsonAsync(
            $"/api/v1/academic/submission-attempts/{attempt.Id}/submit",
            new FinalSubmitAssignmentCommand(attempt.RowVersion),
            JsonOptions);
        var retry = await ReadRequiredAsync<SubmissionReceipt>(retryResponse);

        Assert.Equal(HttpStatusCode.OK, retryResponse.StatusCode);
        Assert.Equal(first.SubmissionAttemptId, retry.SubmissionAttemptId);
        Assert.Equal(first.AttemptNumber, retry.AttemptNumber);
        Assert.Equal(first.SubmittedAtUtc, retry.SubmittedAtUtc);
        Assert.Equal(1, await _factory.CountSubmissionAttemptsAsync(scenario.Assignment.Id));
        Assert.Equal(1, await _factory.CountSubmittedAttemptsAsync(scenario.Assignment.Id));
    }

    [Fact]
    public async Task ConcurrentFinalSubmitTransitionsOnceAndReturnsSameReceipt()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateSubmissionScenarioAsync(
            managerClient,
            studentClient,
            maxAttempts: 1);
        var attempt = await SaveSubmissionDraftAsync(
            studentClient,
            await CreateSubmissionDraftAsync(studentClient, scenario.Assignment.Id));
        var route = $"/api/v1/academic/submission-attempts/{attempt.Id}/submit";
        var command = new FinalSubmitAssignmentCommand(attempt.RowVersion);

        var responses = await Task.WhenAll(
            studentClient.PostAsJsonAsync(route, command, JsonOptions),
            studentClient.PostAsJsonAsync(route, command, JsonOptions),
            studentClient.PostAsJsonAsync(route, command, JsonOptions));
        try
        {
            Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
            var receipts = new List<SubmissionReceipt>();
            foreach (var response in responses)
            {
                receipts.Add(await ReadRequiredAsync<SubmissionReceipt>(response));
            }

            Assert.Single(receipts.Select(receipt => receipt.SubmissionAttemptId).Distinct());
            Assert.Single(receipts.Select(receipt => receipt.SubmittedAtUtc).Distinct());
            Assert.Single(receipts.Select(receipt => receipt.AttemptNumber).Distinct());
            Assert.Equal(1, await _factory.CountSubmissionAttemptsAsync(scenario.Assignment.Id));
            Assert.Equal(1, await _factory.CountSubmittedAttemptsAsync(scenario.Assignment.Id));
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
    public async Task ConcurrentDraftCreationAllocatesOnlyOneAttemptNumber()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateSubmissionScenarioAsync(managerClient, studentClient);
        var route = $"/api/v1/academic/assignments/{scenario.Assignment.Id}/attempts";

        var responses = await Task.WhenAll(
            studentClient.PostAsync(route, null),
            studentClient.PostAsync(route, null),
            studentClient.PostAsync(route, null));
        try
        {
            Assert.All(responses, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
            var attempts = new List<SubmissionAttemptResult>();
            foreach (var response in responses)
            {
                attempts.Add(await ReadRequiredAsync<SubmissionAttemptResult>(response));
            }

            Assert.Single(attempts.Select(attempt => attempt.Id).Distinct());
            Assert.Single(attempts.Select(attempt => attempt.AttemptNumber).Distinct());
            Assert.Equal(1, await _factory.CountSubmissionAttemptsAsync(scenario.Assignment.Id));
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
    public async Task ConcurrentRequestsCannotExceedMaximumAcceptedAttempts()
    {
        using var managerClient = _factory.CreateClient();
        using var studentClient = _factory.CreateClient();
        var scenario = await CreateSubmissionScenarioAsync(
            managerClient,
            studentClient,
            maxAttempts: 1);
        var attempt = await SaveSubmissionDraftAsync(
            studentClient,
            await CreateSubmissionDraftAsync(studentClient, scenario.Assignment.Id));
        _ = await SubmitAsync(studentClient, attempt);
        var route = $"/api/v1/academic/assignments/{scenario.Assignment.Id}/attempts";

        var responses = await Task.WhenAll(
            studentClient.PostAsync(route, null),
            studentClient.PostAsync(route, null),
            studentClient.PostAsync(route, null));
        try
        {
            Assert.All(responses, response => Assert.Equal(HttpStatusCode.Conflict, response.StatusCode));
            Assert.Equal(1, await _factory.CountSubmissionAttemptsAsync(scenario.Assignment.Id));
            Assert.Equal(1, await _factory.CountSubmittedAttemptsAsync(scenario.Assignment.Id));
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
