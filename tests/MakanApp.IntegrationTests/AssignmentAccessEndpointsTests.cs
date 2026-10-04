using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Assessment;
using MakanApp.Application.Identity;
using MakanApp.Application.Organization;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class AssignmentEndpointsTests
{
    [Fact]
    public async Task StudentCannotSeeDraft()
    {
        using var managerClient = _factory.CreateClient();
        var manager = await CreateManagerAsync(managerClient);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(
            studentClient,
            manager.OrganizationId,
            academicClass.ClassId);
        var draft = await CreateDraftAsync(managerClient, academicClass.ClassId);

        using var detailResponse = await studentClient.GetAsync(
            $"/api/v1/academic/assignments/{draft.Id}");
        var list = await studentClient.GetFromJsonAsync<AssignmentResult[]>(
            $"/api/v1/academic/classes/{academicClass.ClassId}/assignments",
            JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, detailResponse.StatusCode);
        Assert.Empty(list!);
    }

    [Fact]
    public async Task StudentRecipientReadsPublishedAssignment()
    {
        using var managerClient = _factory.CreateClient();
        var manager = await CreateManagerAsync(managerClient);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);
        using var studentClient = _factory.CreateClient();
        var student = await CreateStudentAsync(
            studentClient,
            manager.OrganizationId,
            academicClass.ClassId);
        var draft = await CreateDraftAsync(managerClient, academicClass.ClassId);
        var published = await PublishAsync(managerClient, draft);

        var detail = await studentClient.GetFromJsonAsync<AssignmentResult>(
            $"/api/v1/academic/assignments/{draft.Id}",
            JsonOptions);
        var list = await studentClient.GetFromJsonAsync<AssignmentResult[]>(
            $"/api/v1/academic/classes/{academicClass.ClassId}/assignments",
            JsonOptions);

        Assert.Equal(AssignmentStatus.Published, detail!.Status);
        Assert.Equal(published.Assignment.VersionId, detail.VersionId);
        Assert.Contains(list!, item => item.Id == draft.Id);
        Assert.Contains(
            student.EnrollmentId,
            await _factory.GetAssignmentRecipientEnrollmentIdsAsync(detail.VersionId));
    }

    [Fact]
    public async Task EndedEnrollmentAfterPublicationKeepsHistoricalRecipientAccess()
    {
        using var managerClient = _factory.CreateClient();
        var manager = await CreateManagerAsync(managerClient);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);
        using var studentClient = _factory.CreateClient();
        var student = await CreateStudentAsync(
            studentClient,
            manager.OrganizationId,
            academicClass.ClassId);
        var draft = await CreateDraftAsync(managerClient, academicClass.ClassId);
        _ = await PublishAsync(managerClient, draft);

        await _factory.EndEnrollmentAsync(student.EnrollmentId);
        using var response = await studentClient.GetAsync(
            $"/api/v1/academic/assignments/{draft.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task StudentEnrolledAfterPublicationIsNotAuthorized()
    {
        using var managerClient = _factory.CreateClient();
        var manager = await CreateManagerAsync(managerClient);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);
        var draft = await CreateDraftAsync(managerClient, academicClass.ClassId);
        _ = await PublishAsync(managerClient, draft);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(
            studentClient,
            manager.OrganizationId,
            academicClass.ClassId);

        using var response = await studentClient.GetAsync(
            $"/api/v1/academic/assignments/{draft.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ParentReadsPublishedAssignmentForSelectedAuthorizedChild()
    {
        using var managerClient = _factory.CreateClient();
        var manager = await CreateManagerAsync(managerClient);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);
        using var parentClient = _factory.CreateClient();
        var parent = await CreateParentAsync(
            parentClient,
            manager.OrganizationId,
            academicClass.ClassId);
        var draft = await CreateDraftAsync(managerClient, academicClass.ClassId);
        _ = await PublishAsync(managerClient, draft);

        var detail = await parentClient.GetFromJsonAsync<AssignmentResult>(
            $"/api/v1/academic/assignments/{draft.Id}",
            JsonOptions);

        Assert.Equal(draft.Id, detail!.Id);
        Assert.Equal(parent.ChildOrganizationPersonId, await GetRecipientLearnerIdAsync(
            detail.VersionId));
    }

    [Fact]
    public async Task ParentCannotSeeDraftEvenForSelectedEnrolledChild()
    {
        using var managerClient = _factory.CreateClient();
        var manager = await CreateManagerAsync(managerClient);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);
        using var parentClient = _factory.CreateClient();
        _ = await CreateParentAsync(
            parentClient,
            manager.OrganizationId,
            academicClass.ClassId);
        var draft = await CreateDraftAsync(managerClient, academicClass.ClassId);

        using var detailResponse = await parentClient.GetAsync(
            $"/api/v1/academic/assignments/{draft.Id}");
        var list = await parentClient.GetFromJsonAsync<AssignmentResult[]>(
            $"/api/v1/academic/assignments?classId={academicClass.ClassId}",
            JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, detailResponse.StatusCode);
        Assert.Empty(list!);
    }

    [Fact]
    public async Task ParentCannotUseAnotherSelectedChildToReadRecipientAssignment()
    {
        using var managerClient = _factory.CreateClient();
        var manager = await CreateManagerAsync(managerClient);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);
        using var parentClient = _factory.CreateClient();
        var parent = await CreateParentAsync(
            parentClient,
            manager.OrganizationId,
            academicClass.ClassId);
        var draft = await CreateDraftAsync(managerClient, academicClass.ClassId);
        _ = await PublishAsync(managerClient, draft);
        var otherPersonId = await _factory.CreatePersonWithoutUserAsync();
        var otherChildId = await _factory.CreateOrganizationPersonAsync(
            manager.OrganizationId,
            otherPersonId);
        _ = await _factory.CreateGuardianRelationAsync(
            parent.UserId,
            manager.OrganizationId,
            otherChildId);
        await SelectChildAsync(parentClient, otherChildId);

        using var response = await parentClient.GetAsync(
            $"/api/v1/academic/assignments/{draft.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RevokedGuardianRelationRemovesParentAccess()
    {
        using var managerClient = _factory.CreateClient();
        var manager = await CreateManagerAsync(managerClient);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);
        using var parentClient = _factory.CreateClient();
        var parent = await CreateParentAsync(
            parentClient,
            manager.OrganizationId,
            academicClass.ClassId);
        var draft = await CreateDraftAsync(managerClient, academicClass.ClassId);
        _ = await PublishAsync(managerClient, draft);

        await _factory.RevokeGuardianRelationAsync(parent.GuardianRelationId);
        using var response = await parentClient.GetAsync(
            $"/api/v1/academic/assignments/{draft.Id}");

        Assert.True(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task PublishedAssignmentCannotBeEditedOrPublishedAgain()
    {
        using var client = _factory.CreateClient();
        var manager = await CreateManagerAsync(client);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);
        var draft = await CreateDraftAsync(client, academicClass.ClassId);
        var published = await PublishAsync(client, draft);

        using var updateResponse = await client.PatchAsJsonAsync(
            $"/api/v1/academic/assignments/{draft.Id}",
            NewUpdateCommand(published.Assignment),
            JsonOptions);
        using var publishResponse = await client.PostAsJsonAsync(
            $"/api/v1/academic/assignments/{draft.Id}/publish",
            NewPublishCommand(published.Assignment),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, updateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, publishResponse.StatusCode);
        Assert.Equal(
            AssessmentErrorCodes.AssignmentAlreadyPublished,
            await ReadProblemCodeAsync(updateResponse));
    }

    [Fact]
    public async Task ConcurrentDraftEditsProducePreconditionConflict()
    {
        using var firstClient = _factory.CreateClient();
        var manager = await CreateManagerAsync(firstClient);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);
        var draft = await CreateDraftAsync(firstClient, academicClass.ClassId);
        var command = NewUpdateCommand(draft);
        using var secondClient = _factory.CreateClient();
        UseBearerToken(secondClient, manager.AccessToken);
        var route = $"/api/v1/academic/assignments/{draft.Id}";

        var responses = await Task.WhenAll(
            firstClient.PatchAsJsonAsync(route, command, JsonOptions),
            secondClient.PatchAsJsonAsync(route, command, JsonOptions));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
            var conflict = Assert.Single(
                responses,
                response => response.StatusCode == HttpStatusCode.PreconditionFailed);
            Assert.Equal(
                AssessmentErrorCodes.ConcurrencyConflict,
                await ReadProblemCodeAsync(conflict));
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
    public async Task EndedTeacherAssignmentCannotReadOrMutateAssignment()
    {
        using var managerClient = _factory.CreateClient();
        var manager = await CreateManagerAsync(managerClient);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);
        var draft = await CreateDraftAsync(managerClient, academicClass.ClassId);
        using var teacherClient = _factory.CreateClient();
        var teacher = await CreateTeacherAsync(
            teacherClient,
            manager.OrganizationId,
            academicClass.ClassId,
            assigned: true);
        await _factory.EndTeacherAssignmentAsync(teacher.TeacherAssignmentId!.Value);

        using var getResponse = await teacherClient.GetAsync(
            $"/api/v1/academic/assignments/{draft.Id}");
        using var updateResponse = await teacherClient.PatchAsJsonAsync(
            $"/api/v1/academic/assignments/{draft.Id}",
            NewUpdateCommand(draft),
            JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, updateResponse.StatusCode);
    }

    [Fact]
    public async Task EndedMembershipCannotAccessAssignments()
    {
        using var client = _factory.CreateClient();
        var manager = await CreateManagerAsync(client);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);
        var draft = await CreateDraftAsync(client, academicClass.ClassId);

        await _factory.EndMembershipAsync(manager.MembershipId);
        using var response = await client.GetAsync(
            $"/api/v1/academic/assignments/{draft.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PersonalWorkspaceCannotAccessOrganizationAssignment()
    {
        using var client = _factory.CreateClient();
        var manager = await CreateManagerAsync(client);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);
        var draft = await CreateDraftAsync(client, academicClass.ClassId);
        using var selectResponse = await client.PostAsJsonAsync(
            "/api/v1/workspaces/select",
            new SelectWorkspaceCommand(WorkspaceType.Personal, null, null),
            JsonOptions);
        selectResponse.EnsureSuccessStatusCode();

        using var response = await client.GetAsync(
            $"/api/v1/academic/assignments/{draft.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(
            AssessmentErrorCodes.AssignmentNotAllowed,
            await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task AnonymousRequestIsRejected()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(
            $"/api/v1/academic/assignments/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ConcurrentPublishCreatesOneSnapshot()
    {
        using var firstClient = _factory.CreateClient();
        var manager = await CreateManagerAsync(firstClient);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);
        var learners = await _factory.CreateOrganizationPersonsAsync(manager.OrganizationId, 2);
        foreach (var learner in learners)
        {
            _ = await _factory.CreateEnrollmentAsync(
                manager.OrganizationId,
                academicClass.ClassId,
                learner);
        }

        var draft = await CreateDraftAsync(firstClient, academicClass.ClassId);
        using var secondClient = _factory.CreateClient();
        UseBearerToken(secondClient, manager.AccessToken);
        var route = $"/api/v1/academic/assignments/{draft.Id}/publish";
        var command = NewPublishCommand(draft);

        var responses = await Task.WhenAll(
            firstClient.PostAsJsonAsync(route, command, JsonOptions),
            secondClient.PostAsJsonAsync(route, command, JsonOptions));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
            Assert.Equal(2, await _factory.CountAssignmentRecipientsAsync(draft.VersionId));
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    private async Task<StudentContext> CreateStudentAsync(
        HttpClient client,
        Guid organizationId,
        Guid classId)
    {
        var user = await CreateAuthenticatedUserAsync(client);
        UseBearerToken(client, user.AccessToken);
        using var profileResponse = await client.PatchAsJsonAsync(
            "/api/v1/me/profile",
            new CompleteProfileCommand(
                "Student",
                "User",
                "Student User",
                $"student.{Guid.NewGuid().ToString("N")[..12]}"),
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
        var enrollmentId = await _factory.CreateEnrollmentAsync(
            organizationId,
            classId,
            organizationPersonId);
        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Student);
        return new StudentContext(
            user.User.Id,
            membership.MembershipId,
            organizationPersonId,
            enrollmentId);
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
        var personId = await _factory.CreatePersonWithoutUserAsync();
        var childId = await _factory.CreateOrganizationPersonAsync(organizationId, personId);
        var relationId = await _factory.CreateGuardianRelationAsync(
            user.User.Id,
            organizationId,
            childId);
        var enrollmentId = await _factory.CreateEnrollmentAsync(
            organizationId,
            classId,
            childId);
        UseBearerToken(client, user.AccessToken);
        await SelectWorkspaceAsync(client, membership.MembershipId, OrganizationRole.Parent);
        await SelectChildAsync(client, childId);
        return new ParentContext(
            user.User.Id,
            membership.MembershipId,
            childId,
            relationId,
            enrollmentId);
    }

    private static async Task SelectChildAsync(HttpClient client, Guid organizationPersonId)
    {
        using var response = await client.PostAsync(
            $"/api/v1/guardian/children/{organizationPersonId}/select",
            null);
        response.EnsureSuccessStatusCode();
    }

    private async Task<Guid> GetRecipientLearnerIdAsync(Guid assignmentVersionId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanApp.Infrastructure.Persistence.MakanDbContext>();
        return await (
            from recipient in dbContext.AssignmentRecipients
            join enrollment in dbContext.Enrollments
                on recipient.EnrollmentId equals enrollment.Id
            where recipient.AssignmentVersionId == assignmentVersionId
            select enrollment.LearnerOrganizationPersonId)
            .SingleAsync();
    }

    private sealed record StudentContext(
        Guid UserId,
        Guid MembershipId,
        Guid OrganizationPersonId,
        Guid EnrollmentId);

    private sealed record ParentContext(
        Guid UserId,
        Guid MembershipId,
        Guid ChildOrganizationPersonId,
        Guid GuardianRelationId,
        Guid EnrollmentId);
}
