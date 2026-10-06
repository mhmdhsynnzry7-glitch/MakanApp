using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MakanApp.Application.Assessment;
using MakanApp.Application.Identity;
using MakanApp.Application.Organization;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Organization;

namespace MakanApp.IntegrationTests;

public sealed partial class ExamEndpointsTests
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static int _phoneSequence = 7_000_000;
    private readonly MakanAppWebApplicationFactory _factory;

    public ExamEndpointsTests(MakanAppWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<ManagerContext> CreateManagerAsync(HttpClient client)
    {
        var user = await CreateAuthenticatedUserAsync(client);
        var organizationId = await _factory.CreateOrganizationAsync($"Exam Test {Guid.NewGuid():N}");
        var membership = await _factory.CreateMembershipAsync(
            user.User.Id,
            organizationId,
            OrganizationRole.Manager);
        UseBearerToken(client, user.AccessToken);
        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Manager);
        return new ManagerContext(
            user.User.Id,
            user.AccessToken,
            organizationId,
            membership.MembershipId);
    }

    private async Task<TeacherContext> CreateTeacherAsync(
        HttpClient client,
        Guid organizationId,
        Guid classId,
        bool assigned)
    {
        var user = await CreateAuthenticatedUserAsync(client);
        var membership = await _factory.CreateMembershipAsync(
            user.User.Id,
            organizationId,
            OrganizationRole.Teacher);
        if (assigned)
        {
            _ = await _factory.CreateTeacherAssignmentAsync(
                organizationId,
                classId,
                membership.MembershipId);
        }

        UseBearerToken(client, user.AccessToken);
        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Teacher);
        return new TeacherContext(user.User.Id, user.AccessToken, membership.MembershipId);
    }

    private async Task<StudentContext> CreateStudentAsync(
        HttpClient client,
        Guid organizationId,
        Guid classId,
        bool enrolled = true)
    {
        var user = await CreateAuthenticatedUserAsync(client);
        UseBearerToken(client, user.AccessToken);
        using var profileResponse = await client.PatchAsJsonAsync(
            "/api/v1/me/profile",
            new CompleteProfileCommand(
                "Student",
                "User",
                "Student User",
                $"exam.student.{Guid.NewGuid().ToString("N")[..12]}"),
            JsonOptions);
        profileResponse.EnsureSuccessStatusCode();
        var personId = await _factory.GetUserPersonIdAsync(user.User.Id);
        var organizationPersonId = await _factory.CreateOrganizationPersonAsync(
            organizationId,
            personId);
        var membership = await _factory.CreateMembershipAsync(
            user.User.Id,
            organizationId,
            OrganizationRole.Student);
        if (enrolled)
        {
            _ = await _factory.CreateEnrollmentAsync(
                organizationId,
                classId,
                organizationPersonId);
        }

        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Student);
        return new StudentContext(user.User.Id, user.AccessToken, organizationPersonId);
    }

    private async Task<ParentContext> CreateParentAsync(
        HttpClient client,
        Guid organizationId,
        Guid classId)
    {
        var user = await CreateAuthenticatedUserAsync(client);
        var membership = await _factory.CreateMembershipAsync(
            user.User.Id,
            organizationId,
            OrganizationRole.Parent);
        var childPersonId = await _factory.CreatePersonWithoutUserAsync();
        var childId = await _factory.CreateOrganizationPersonAsync(organizationId, childPersonId);
        _ = await _factory.CreateGuardianRelationAsync(user.User.Id, organizationId, childId);
        _ = await _factory.CreateEnrollmentAsync(organizationId, classId, childId);
        UseBearerToken(client, user.AccessToken);
        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Parent);
        using var selectResponse = await client.PostAsync(
            $"/api/v1/guardian/children/{childId}/select",
            null);
        selectResponse.EnsureSuccessStatusCode();
        return new ParentContext(user.User.Id, membership.MembershipId, childId);
    }

    private async Task<VerifyOtpResult> CreateAuthenticatedUserAsync(HttpClient client)
    {
        var sequence = Interlocked.Increment(ref _phoneSequence);
        var phoneNumber = $"+98912{sequence:D7}";
        using var challengeResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/otp/challenges",
            new RequestOtpCommand(phoneNumber),
            JsonOptions);
        challengeResponse.EnsureSuccessStatusCode();
        var challenge = await ReadRequiredAsync<RequestOtpResult>(challengeResponse);
        using var verifyResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/otp/verify",
            new VerifyOtpCommand(
                challenge.ChallengeId,
                phoneNumber,
                _factory.GetOtpCode(challenge.ChallengeId)),
            JsonOptions);
        verifyResponse.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<VerifyOtpResult>(verifyResponse);
    }

    private static async Task SelectWorkspaceAsync(
        HttpClient client,
        Guid membershipId,
        OrganizationRole role)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/v1/workspaces/select",
            new SelectWorkspaceCommand(WorkspaceType.Organization, membershipId, role),
            JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<ExamEditorDto> CreateDraftAsync(
        HttpClient client,
        Guid classId,
        decimal maxScore = 20m)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/v1/academic/classes/{classId}/exams",
            NewDraftCommand(maxScore: maxScore),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<ExamEditorDto>(response);
    }

    private static async Task<ExamEditorDto> AddObjectiveQuestionAsync(
        HttpClient client,
        ExamEditorDto exam,
        int order = 1,
        string prompt = "2 + 2?",
        decimal score = 20m)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/v1/academic/exams/{exam.Id}/questions",
            new AddExamQuestionCommand(
                order,
                ExamQuestionType.ObjectiveSingleChoice,
                prompt,
                score,
                [
                    new ExamQuestionOptionCommand(1, "3", false),
                    new ExamQuestionOptionCommand(2, "4", true)
                ],
                exam.VersionRowVersion),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<ExamEditorDto>(response);
    }

    private static async Task<ExamEditorDto> AddDescriptiveQuestionAsync(
        HttpClient client,
        ExamEditorDto exam,
        int order = 1,
        decimal score = 20m)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/v1/academic/exams/{exam.Id}/questions",
            new AddExamQuestionCommand(
                order,
                ExamQuestionType.Descriptive,
                "پاسخ را توضیح دهید.",
                score,
                null,
                exam.VersionRowVersion),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<ExamEditorDto>(response);
    }

    private static async Task<ExamEditorDto> PublishAsync(HttpClient client, ExamEditorDto exam)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/v1/academic/exams/{exam.Id}/publish",
            new PublishExamCommand(exam.ExamRowVersion, exam.VersionRowVersion),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<ExamEditorDto>(response);
    }

    private static CreateExamDraftCommand NewDraftCommand(
        string title = "آزمون فصل اول",
        DateTime? availableFromUtc = null,
        DateTime? availableUntilUtc = null,
        int durationMinutes = 45,
        int maxAttempts = 1,
        decimal maxScore = 20m) =>
        new(
            title,
            "قوانین آزمون فصل اول",
            availableFromUtc ?? DateTime.UtcNow.AddMinutes(-5),
            availableUntilUtc ?? DateTime.UtcNow.AddHours(2),
            durationMinutes,
            maxAttempts,
            maxScore,
            ExamRandomizationPolicy.QuestionOrder);

    private static UpdateExamDraftCommand NewUpdateCommand(
        ExamEditorDto exam,
        string? title = null) =>
        new(
            title ?? exam.Title,
            exam.Description,
            exam.AvailableFromUtc,
            exam.AvailableUntilUtc,
            exam.DurationMinutes,
            exam.MaxAttempts,
            exam.MaxScore,
            exam.RandomizationPolicy,
            exam.VersionRowVersion);

    private static async Task<T> ReadRequiredAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;

    private static async Task<string?> ReadProblemCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("code").GetString();
    }

    private static void UseBearerToken(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed record ManagerContext(
        Guid UserId,
        string AccessToken,
        Guid OrganizationId,
        Guid MembershipId);

    private sealed record TeacherContext(Guid UserId, string AccessToken, Guid MembershipId);
    private sealed record StudentContext(Guid UserId, string AccessToken, Guid OrganizationPersonId);
    private sealed record ParentContext(Guid UserId, Guid MembershipId, Guid ChildId);
}
