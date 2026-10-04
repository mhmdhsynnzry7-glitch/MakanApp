using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using MakanApp.Application.Assessment;
using MakanApp.Application.Identity;
using MakanApp.Application.Organization;
using MakanApp.Application.Storage;
using MakanApp.Domain.Organization;

namespace MakanApp.IntegrationTests;

public sealed partial class AssignmentEndpointsTests
{
    private async Task<SubmissionScenario> CreateSubmissionScenarioAsync(
        HttpClient managerClient,
        HttpClient studentClient,
        int maxAttempts = 2,
        decimal maxScore = 20m,
        bool allowLateSubmission = false,
        bool studentIsRecipient = true)
    {
        var manager = await CreateManagerAsync(managerClient);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);
        StudentContext? student = null;
        if (studentIsRecipient)
        {
            student = await CreateStudentAsync(
                studentClient,
                manager.OrganizationId,
                academicClass.ClassId);
        }

        using var createResponse = await managerClient.PostAsJsonAsync(
            $"/api/v1/academic/classes/{academicClass.ClassId}/assignments",
            new CreateAssignmentDraftCommand(
                "Submission test",
                "Submission lifecycle test",
                DateTime.UtcNow.AddDays(2),
                allowLateSubmission,
                maxAttempts,
                maxScore),
            JsonOptions);
        createResponse.EnsureSuccessStatusCode();
        var draft = await ReadRequiredAsync<AssignmentResult>(createResponse);
        var published = await PublishAsync(managerClient, draft);

        if (!studentIsRecipient)
        {
            student = await CreateStudentAsync(
                studentClient,
                manager.OrganizationId,
                academicClass.ClassId);
        }

        return new SubmissionScenario(
            manager.OrganizationId,
            academicClass.ClassId,
            manager.MembershipId,
            student!,
            published.Assignment);
    }

    private static async Task<SubmissionAttemptResult> CreateSubmissionDraftAsync(
        HttpClient studentClient,
        Guid assignmentId)
    {
        using var response = await studentClient.PostAsync(
            $"/api/v1/academic/assignments/{assignmentId}/attempts",
            null);
        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<SubmissionAttemptResult>(response);
    }

    private static async Task<SubmissionAttemptResult> SaveSubmissionDraftAsync(
        HttpClient studentClient,
        SubmissionAttemptResult attempt,
        string? answer = "Student answer")
    {
        using var response = await studentClient.PatchAsJsonAsync(
            $"/api/v1/academic/submission-attempts/{attempt.Id}/draft",
            new SaveSubmissionDraftCommand(answer, attempt.RowVersion),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<SubmissionAttemptResult>(response);
    }

    private static async Task<SubmissionAttemptResult> AttachSubmissionFileAsync(
        HttpClient studentClient,
        SubmissionAttemptResult attempt,
        Guid fileAssetId)
    {
        using var response = await studentClient.PostAsJsonAsync(
            $"/api/v1/academic/submission-attempts/{attempt.Id}/attachments",
            new AttachSubmissionFileCommand(fileAssetId, attempt.RowVersion),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<SubmissionAttemptResult>(response);
    }

    private static async Task<SubmissionReceipt> SubmitAsync(
        HttpClient studentClient,
        SubmissionAttemptResult attempt)
    {
        using var response = await studentClient.PostAsJsonAsync(
            $"/api/v1/academic/submission-attempts/{attempt.Id}/submit",
            new FinalSubmitAssignmentCommand(attempt.RowVersion),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<SubmissionReceipt>(response);
    }

    private static async Task<FileAssetResult> UploadSubmissionFileAsync(
        HttpClient client,
        string fileName = "answer.txt")
    {
        using var form = new MultipartFormDataContent();
        using var content = new ByteArrayContent(Encoding.UTF8.GetBytes("submission attachment"));
        content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(content, "File", fileName);
        using var response = await client.PostAsync("/api/v1/files", form);
        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<FileAssetResult>(response);
    }

    private async Task SelectStudentWorkspaceAsync(
        HttpClient client,
        Guid membershipId) =>
        await SelectWorkspaceAsync(client, membershipId, OrganizationRole.Student);

    private async Task<ParentContext> CreateParentForStudentAsync(
        HttpClient parentClient,
        SubmissionScenario scenario)
    {
        var user = await CreateAuthenticatedUserAsync(parentClient);
        var membership = await _factory.CreateMembershipAsync(
            user.User.Id,
            scenario.OrganizationId,
            OrganizationRole.Parent);
        var relationId = await _factory.CreateGuardianRelationAsync(
            user.User.Id,
            scenario.OrganizationId,
            scenario.Student.OrganizationPersonId);
        UseBearerToken(parentClient, user.AccessToken);
        await SelectWorkspaceAsync(
            parentClient,
            membership.MembershipId,
            OrganizationRole.Parent);
        await SelectChildAsync(parentClient, scenario.Student.OrganizationPersonId);
        return new ParentContext(
            user.User.Id,
            membership.MembershipId,
            scenario.Student.OrganizationPersonId,
            relationId,
            scenario.Student.EnrollmentId);
    }

    private sealed record SubmissionScenario(
        Guid OrganizationId,
        Guid ClassId,
        Guid ManagerMembershipId,
        StudentContext Student,
        AssignmentResult Assignment);
}
