using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MakanApp.Application.Assessment;
using MakanApp.Application.Identity;
using MakanApp.Application.Organization;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed partial class AssignmentEndpointsTests
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static int _phoneSequence = 2_000_000;
    private readonly MakanAppWebApplicationFactory _factory;

    public AssignmentEndpointsTests(MakanAppWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ManagerCreatesReadsAndListsDraft()
    {
        using var client = _factory.CreateClient();
        var manager = await CreateManagerAsync(client);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);

        var draft = await CreateDraftAsync(client, academicClass.ClassId);
        var detail = await client.GetFromJsonAsync<AssignmentResult>(
            $"/api/v1/academic/assignments/{draft.Id}",
            JsonOptions);
        var list = await client.GetFromJsonAsync<AssignmentResult[]>(
            $"/api/v1/academic/classes/{academicClass.ClassId}/assignments",
            JsonOptions);
        var alternateList = await client.GetFromJsonAsync<AssignmentResult[]>(
            $"/api/v1/academic/assignments?classId={academicClass.ClassId}",
            JsonOptions);

        Assert.Equal(AssignmentStatus.Draft, draft.Status);
        Assert.Equal(1, draft.VersionNumber);
        Assert.Equal(draft.Id, detail!.Id);
        Assert.Contains(list!, item => item.Id == draft.Id);
        Assert.Contains(alternateList!, item => item.Id == draft.Id);
        Assert.Equal(manager.OrganizationId, draft.OrganizationId);
    }

    [Fact]
    public async Task ManagerUpdatesDraftAndAdvancesBothRowVersions()
    {
        using var client = _factory.CreateClient();
        var manager = await CreateManagerAsync(client);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);
        var draft = await CreateDraftAsync(client, academicClass.ClassId);

        using var response = await client.PatchAsJsonAsync(
            $"/api/v1/academic/assignments/{draft.Id}",
            new UpdateAssignmentDraftCommand(
                "تمرین ویرایش‌شده",
                "توضیح ویرایش‌شده",
                DateTime.UtcNow.AddDays(5),
                true,
                3,
                25m,
                draft.AssignmentRowVersion,
                draft.VersionRowVersion),
            JsonOptions);
        var updated = await ReadRequiredAsync<AssignmentResult>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("تمرین ویرایش‌شده", updated.Title);
        Assert.True(updated.AllowLateSubmission);
        Assert.Equal(3, updated.MaxAttempts);
        Assert.Equal(25m, updated.MaxScore);
        Assert.NotEqual(draft.AssignmentRowVersion, updated.AssignmentRowVersion);
        Assert.NotEqual(draft.VersionRowVersion, updated.VersionRowVersion);
    }

    [Fact]
    public async Task DraftRejectsInvalidTitle()
    {
        using var client = _factory.CreateClient();
        var manager = await CreateManagerAsync(client);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);

        using var response = await client.PostAsJsonAsync(
            $"/api/v1/academic/classes/{academicClass.ClassId}/assignments",
            NewDraftCommand(title: " "),
            JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            AssessmentErrorCodes.AssignmentTitleRequired,
            await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task DraftRejectsPastDueDate()
    {
        using var client = _factory.CreateClient();
        var manager = await CreateManagerAsync(client);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);

        using var response = await client.PostAsJsonAsync(
            $"/api/v1/academic/classes/{academicClass.ClassId}/assignments",
            NewDraftCommand(dueAtUtc: DateTime.UtcNow.AddMinutes(-1)),
            JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            AssessmentErrorCodes.AssignmentDueDateInvalid,
            await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task DraftRejectsNonPositiveAttempts()
    {
        using var client = _factory.CreateClient();
        var manager = await CreateManagerAsync(client);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);

        using var response = await client.PostAsJsonAsync(
            $"/api/v1/academic/classes/{academicClass.ClassId}/assignments",
            NewDraftCommand(maxAttempts: 0),
            JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            AssessmentErrorCodes.AssignmentAttemptsInvalid,
            await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task DraftRejectsNonPositiveMaxScore()
    {
        using var client = _factory.CreateClient();
        var manager = await CreateManagerAsync(client);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);

        using var response = await client.PostAsJsonAsync(
            $"/api/v1/academic/classes/{academicClass.ClassId}/assignments",
            NewDraftCommand(maxScore: 0m),
            JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            AssessmentErrorCodes.AssignmentMaxScoreInvalid,
            await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task AssignedTeacherCreatesDraft()
    {
        using var managerClient = _factory.CreateClient();
        var manager = await CreateManagerAsync(managerClient);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);
        using var teacherClient = _factory.CreateClient();
        var teacher = await CreateTeacherAsync(
            teacherClient,
            manager.OrganizationId,
            academicClass.ClassId,
            assigned: true);

        var draft = await CreateDraftAsync(teacherClient, academicClass.ClassId);

        Assert.Equal(teacher.MembershipId, await GetCreatorMembershipIdAsync(draft.Id));
        Assert.Equal(AssignmentStatus.Draft, draft.Status);
    }

    [Fact]
    public async Task UnassignedTeacherCannotCreateOrListAssignments()
    {
        using var managerClient = _factory.CreateClient();
        var manager = await CreateManagerAsync(managerClient);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);
        using var teacherClient = _factory.CreateClient();
        _ = await CreateTeacherAsync(
            teacherClient,
            manager.OrganizationId,
            academicClass.ClassId,
            assigned: false);

        using var createResponse = await teacherClient.PostAsJsonAsync(
            $"/api/v1/academic/classes/{academicClass.ClassId}/assignments",
            NewDraftCommand(),
            JsonOptions);
        using var listResponse = await teacherClient.GetAsync(
            $"/api/v1/academic/classes/{academicClass.ClassId}/assignments");

        Assert.Equal(HttpStatusCode.Forbidden, createResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, listResponse.StatusCode);
    }

    [Fact]
    public async Task TeacherInOrganizationACannotCreateAssignmentForOrganizationBClass()
    {
        using var firstManagerClient = _factory.CreateClient();
        var firstManager = await CreateManagerAsync(firstManagerClient);
        var firstClass = await _factory.CreateAcademicClassAsync(firstManager.OrganizationId);
        using var secondManagerClient = _factory.CreateClient();
        var secondManager = await CreateManagerAsync(secondManagerClient);
        var secondClass = await _factory.CreateAcademicClassAsync(secondManager.OrganizationId);
        using var teacherClient = _factory.CreateClient();
        _ = await CreateTeacherAsync(
            teacherClient,
            firstManager.OrganizationId,
            firstClass.ClassId,
            assigned: true);

        using var response = await teacherClient.PostAsJsonAsync(
            $"/api/v1/academic/classes/{secondClass.ClassId}/assignments",
            NewDraftCommand(),
            JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ClientAuthorityAndRecipientSelectorsAreIgnored()
    {
        using var client = _factory.CreateClient();
        var manager = await CreateManagerAsync(client);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);
        var otherOrganizationId = await _factory.CreateOrganizationAsync(NewOrganizationName());
        var otherClass = await _factory.CreateAcademicClassAsync(otherOrganizationId);
        var otherLearner = Assert.Single(
            await _factory.CreateOrganizationPersonsAsync(otherOrganizationId, 1));
        var foreignEnrollmentId = await _factory.CreateEnrollmentAsync(
            otherOrganizationId,
            otherClass.ClassId,
            otherLearner);

        using var response = await client.PostAsJsonAsync(
            $"/api/v1/academic/classes/{academicClass.ClassId}/assignments",
            new
            {
                Title = "Trusted context",
                Description = "Server-derived authority",
                DueAtUtc = DateTime.UtcNow.AddDays(3),
                AllowLateSubmission = false,
                MaxAttempts = 1,
                MaxScore = 20m,
                OrganizationId = otherOrganizationId,
                MembershipId = Guid.NewGuid(),
                Role = OrganizationRole.Teacher,
                EnrollmentId = foreignEnrollmentId,
                Recipients = new[] { foreignEnrollmentId }
            },
            JsonOptions);
        var draft = await ReadRequiredAsync<AssignmentResult>(response);
        var published = await PublishAsync(client, draft);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(manager.OrganizationId, draft.OrganizationId);
        Assert.Equal(0, published.RecipientCount);
    }

    [Fact]
    public async Task OtherOrganizationCannotReadUpdateOrPublishAssignment()
    {
        using var ownerClient = _factory.CreateClient();
        var owner = await CreateManagerAsync(ownerClient);
        var academicClass = await _factory.CreateAcademicClassAsync(owner.OrganizationId);
        var draft = await CreateDraftAsync(ownerClient, academicClass.ClassId);
        using var attackerClient = _factory.CreateClient();
        _ = await CreateManagerAsync(attackerClient);

        using var getResponse = await attackerClient.GetAsync(
            $"/api/v1/academic/assignments/{draft.Id}");
        using var updateResponse = await attackerClient.PatchAsJsonAsync(
            $"/api/v1/academic/assignments/{draft.Id}",
            NewUpdateCommand(draft),
            JsonOptions);
        using var publishResponse = await attackerClient.PostAsJsonAsync(
            $"/api/v1/academic/assignments/{draft.Id}/publish",
            NewPublishCommand(draft),
            JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, updateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, publishResponse.StatusCode);
    }

    [Fact]
    public async Task ManagerCannotCreateDraftForAnotherOrganizationClass()
    {
        using var managerClient = _factory.CreateClient();
        _ = await CreateManagerAsync(managerClient);
        var otherOrganizationId = await _factory.CreateOrganizationAsync(NewOrganizationName());
        var otherClass = await _factory.CreateAcademicClassAsync(otherOrganizationId);

        using var response = await managerClient.PostAsJsonAsync(
            $"/api/v1/academic/classes/{otherClass.ClassId}/assignments",
            NewDraftCommand(),
            JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(
            AssessmentErrorCodes.AssignmentNotFound,
            await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task PublishSnapshotsOnlyActiveEnrollmentsAndDatabaseRejectsDuplicateRecipient()
    {
        using var client = _factory.CreateClient();
        var manager = await CreateManagerAsync(client);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);
        var learners = await _factory.CreateOrganizationPersonsAsync(manager.OrganizationId, 3);
        var firstEnrollment = await _factory.CreateEnrollmentAsync(
            manager.OrganizationId,
            academicClass.ClassId,
            learners[0]);
        var secondEnrollment = await _factory.CreateEnrollmentAsync(
            manager.OrganizationId,
            academicClass.ClassId,
            learners[1]);
        var endedEnrollment = await _factory.CreateEnrollmentAsync(
            manager.OrganizationId,
            academicClass.ClassId,
            learners[2]);
        await _factory.EndEnrollmentAsync(endedEnrollment);
        var draft = await CreateDraftAsync(client, academicClass.ClassId);

        var published = await PublishAsync(client, draft);
        var recipients = await _factory.GetAssignmentRecipientEnrollmentIdsAsync(
            published.Assignment.VersionId);

        Assert.Equal(2, published.RecipientCount);
        Assert.Contains(firstEnrollment, recipients);
        Assert.Contains(secondEnrollment, recipients);
        Assert.DoesNotContain(endedEnrollment, recipients);
        Assert.True(await _factory.DuplicateAssignmentRecipientIsRejectedAsync(
            published.Assignment.VersionId));
    }

    [Fact]
    public async Task EnrollmentAddedAfterPublicationIsNotAddedToSnapshot()
    {
        using var client = _factory.CreateClient();
        var manager = await CreateManagerAsync(client);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);
        var draft = await CreateDraftAsync(client, academicClass.ClassId);
        var published = await PublishAsync(client, draft);
        var learner = Assert.Single(
            await _factory.CreateOrganizationPersonsAsync(manager.OrganizationId, 1));

        _ = await _factory.CreateEnrollmentAsync(
            manager.OrganizationId,
            academicClass.ClassId,
            learner);

        Assert.Equal(0, published.RecipientCount);
        Assert.Equal(
            0,
            await _factory.CountAssignmentRecipientsAsync(published.Assignment.VersionId));
    }

    [Fact]
    public async Task SqlServerRejectsCrossOrganizationEnrollmentAsRecipient()
    {
        using var client = _factory.CreateClient();
        var manager = await CreateManagerAsync(client);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);
        var draft = await CreateDraftAsync(client, academicClass.ClassId);
        var published = await PublishAsync(client, draft);
        var otherOrganizationId = await _factory.CreateOrganizationAsync(NewOrganizationName());
        var otherClass = await _factory.CreateAcademicClassAsync(otherOrganizationId);
        var otherLearner = Assert.Single(
            await _factory.CreateOrganizationPersonsAsync(otherOrganizationId, 1));
        var foreignEnrollmentId = await _factory.CreateEnrollmentAsync(
            otherOrganizationId,
            otherClass.ClassId,
            otherLearner);

        Assert.True(await _factory.CrossOrganizationEnrollmentRecipientIsRejectedAsync(
            published.Assignment.VersionId,
            foreignEnrollmentId));
    }

    private async Task<Guid> GetCreatorMembershipIdAsync(Guid assignmentId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanApp.Infrastructure.Persistence.MakanDbContext>();
        return await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(
            dbContext.Assignments
                .Where(assignment => assignment.Id == assignmentId)
                .Select(assignment => assignment.CreatedByMembershipId));
    }

    private async Task<ManagerContext> CreateManagerAsync(HttpClient client)
    {
        var user = await CreateAuthenticatedUserAsync(client);
        var organizationId = await _factory.CreateOrganizationAsync(NewOrganizationName());
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
        Guid? teacherAssignmentId = null;
        if (assigned)
        {
            teacherAssignmentId = await _factory.CreateTeacherAssignmentAsync(
                organizationId,
                classId,
                membership.MembershipId);
        }

        UseBearerToken(client, user.AccessToken);
        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Teacher);
        return new TeacherContext(
            user.User.Id,
            user.AccessToken,
            membership.MembershipId,
            teacherAssignmentId);
    }

    private async Task<VerifyOtpResult> CreateAuthenticatedUserAsync(HttpClient client)
    {
        var sequence = Interlocked.Increment(ref _phoneSequence);
        var phoneNumber = $"+98913{sequence:D7}";
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

    private static async Task<AssignmentResult> CreateDraftAsync(
        HttpClient client,
        Guid classId)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/v1/academic/classes/{classId}/assignments",
            NewDraftCommand(),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<AssignmentResult>(response);
    }

    private static async Task<PublishAssignmentResult> PublishAsync(
        HttpClient client,
        AssignmentResult assignment)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/v1/academic/assignments/{assignment.Id}/publish",
            NewPublishCommand(assignment),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<PublishAssignmentResult>(response);
    }

    private static CreateAssignmentDraftCommand NewDraftCommand(
        string title = "تمرین فصل اول",
        DateTime? dueAtUtc = null,
        int maxAttempts = 2,
        decimal maxScore = 20m) =>
        new(
            title,
            "صورت تمرین فصل اول",
            dueAtUtc ?? DateTime.UtcNow.AddDays(3),
            false,
            maxAttempts,
            maxScore);

    private static UpdateAssignmentDraftCommand NewUpdateCommand(AssignmentResult assignment) =>
        new(
            assignment.Title,
            assignment.Description,
            assignment.DueAtUtc,
            assignment.AllowLateSubmission,
            assignment.MaxAttempts,
            assignment.MaxScore,
            assignment.AssignmentRowVersion,
            assignment.VersionRowVersion);

    private static PublishAssignmentCommand NewPublishCommand(AssignmentResult assignment) =>
        new(assignment.AssignmentRowVersion, assignment.VersionRowVersion);

    private static async Task<T> ReadRequiredAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;

    private static async Task<string?> ReadProblemCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("code").GetString();
    }

    private static void UseBearerToken(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static string NewOrganizationName() => $"Assignment Test {Guid.NewGuid():N}";

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

    private sealed record TeacherContext(
        Guid UserId,
        string AccessToken,
        Guid MembershipId,
        Guid? TeacherAssignmentId);
}
