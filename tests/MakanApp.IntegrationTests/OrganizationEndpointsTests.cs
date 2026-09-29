using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MakanApp.Application.Identity;
using MakanApp.Application.Organization;
using MakanApp.Domain.Organization;
using Xunit;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class OrganizationEndpointsTests(MakanAppWebApplicationFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static int _phoneSequence = 100;

    [Fact]
    public async Task UserWithoutMembershipGetsOnlyPersonalWorkspace()
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        UseBearerToken(client, user.AccessToken);

        var workspaces = await client.GetFromJsonAsync<WorkspaceResult[]>(
            "/api/v1/workspaces/me",
            JsonOptions);

        var workspace = Assert.Single(workspaces!);
        Assert.Equal(WorkspaceType.Personal, workspace.WorkspaceType);
    }

    [Fact]
    public async Task ActiveMembershipAddsOrganizationWorkspace()
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var organizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        await factory.CreateMembershipAsync(user.User.Id, organizationId, OrganizationRole.Teacher);
        UseBearerToken(client, user.AccessToken);

        var workspaces = await GetWorkspacesAsync(client);

        Assert.Equal(2, workspaces.Length);
        Assert.Contains(workspaces, workspace =>
            workspace.OrganizationId == organizationId &&
            workspace.Role == OrganizationRole.Teacher);
    }

    [Fact]
    public async Task UserWithTwoOrganizationsSeesBoth()
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var firstOrganizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var secondOrganizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        await factory.CreateMembershipAsync(user.User.Id, firstOrganizationId, OrganizationRole.Teacher);
        await factory.CreateMembershipAsync(user.User.Id, secondOrganizationId, OrganizationRole.Teacher);
        UseBearerToken(client, user.AccessToken);

        var workspaces = await GetWorkspacesAsync(client);

        Assert.Equal(3, workspaces.Length);
        Assert.Contains(workspaces, workspace => workspace.OrganizationId == firstOrganizationId);
        Assert.Contains(workspaces, workspace => workspace.OrganizationId == secondOrganizationId);
    }

    [Fact]
    public async Task SameUserCanHaveDifferentRolesInDifferentOrganizations()
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var teacherOrganizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var parentOrganizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        await factory.CreateMembershipAsync(
            user.User.Id,
            teacherOrganizationId,
            OrganizationRole.Teacher);
        await factory.CreateMembershipAsync(
            user.User.Id,
            parentOrganizationId,
            OrganizationRole.Parent);
        UseBearerToken(client, user.AccessToken);

        var workspaces = await GetWorkspacesAsync(client);

        Assert.Contains(workspaces, workspace =>
            workspace.OrganizationId == teacherOrganizationId &&
            workspace.Role == OrganizationRole.Teacher);
        Assert.Contains(workspaces, workspace =>
            workspace.OrganizationId == parentOrganizationId &&
            workspace.Role == OrganizationRole.Parent);
    }

    [Fact]
    public async Task OrganizationWithoutMembershipCannotBeSelected()
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        UseBearerToken(client, user.AccessToken);

        using var response = await SelectWorkspaceAsync(
            client,
            Guid.NewGuid(),
            OrganizationRole.Teacher);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(OrganizationErrorCodes.WorkspaceNotFound, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task InactiveRoleCannotBeSelected()
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var organizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var membership = await factory.CreateMembershipAsync(
            user.User.Id,
            organizationId,
            OrganizationRole.Teacher);
        await factory.EndRoleAssignmentAsync(membership.RoleAssignmentId);
        UseBearerToken(client, user.AccessToken);

        using var response = await SelectWorkspaceAsync(
            client,
            membership.MembershipId,
            OrganizationRole.Teacher);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(OrganizationErrorCodes.RoleNotActive, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task ValidInvitationCreatesIntendedMembershipAndRole()
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var organizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var invitationId = await factory.CreateInvitationAsync(
            user.User.Id,
            organizationId,
            OrganizationRole.Parent);
        UseBearerToken(client, user.AccessToken);

        using var response = await client.PostAsync(
            $"/api/v1/invitations/{invitationId}/accept",
            null);
        var accepted = await response.Content.ReadFromJsonAsync<AcceptInvitationResult>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(accepted);
        Assert.Equal(OrganizationRole.Parent, accepted.Role);
        Assert.False(accepted.AlreadyAccepted);
        Assert.Equal(1, await factory.CountMembershipsAsync(user.User.Id, organizationId));
        Assert.Equal(
            1,
            await factory.CountRoleAssignmentsAsync(
                accepted.MembershipId,
                OrganizationRole.Parent));
    }

    [Fact]
    public async Task RepeatedAcceptanceDoesNotDuplicateMembership()
    {
        var result = await AcceptInvitationTwiceAsync(OrganizationRole.Student);

        Assert.Equal(1, await factory.CountMembershipsAsync(result.UserId, result.OrganizationId));
        Assert.True(result.Second.AlreadyAccepted);
        Assert.Equal(result.First.MembershipId, result.Second.MembershipId);
    }

    [Fact]
    public async Task RepeatedAcceptanceDoesNotDuplicateRoleAssignment()
    {
        var result = await AcceptInvitationTwiceAsync(OrganizationRole.Manager);

        Assert.Equal(
            1,
            await factory.CountRoleAssignmentsAsync(
                result.First.MembershipId,
                OrganizationRole.Manager));
        Assert.Equal(result.First.RoleAssignmentId, result.Second.RoleAssignmentId);
    }

    [Fact]
    public async Task ExpiredInvitationIsRejected()
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var organizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var invitationId = await factory.CreateInvitationAsync(
            user.User.Id,
            organizationId,
            OrganizationRole.Teacher,
            expired: true);
        UseBearerToken(client, user.AccessToken);

        using var response = await client.PostAsync(
            $"/api/v1/invitations/{invitationId}/accept",
            null);

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
        Assert.Equal(OrganizationErrorCodes.InvitationExpired, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task RevokedInvitationIsRejected()
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var organizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var invitationId = await factory.CreateInvitationAsync(
            user.User.Id,
            organizationId,
            OrganizationRole.Teacher,
            revoked: true);
        UseBearerToken(client, user.AccessToken);

        using var response = await client.PostAsync(
            $"/api/v1/invitations/{invitationId}/accept",
            null);

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
        Assert.Equal(OrganizationErrorCodes.InvitationRevoked, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task EndingFirstMembershipLeavesSecondMembershipActive()
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var firstOrganizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var secondOrganizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var first = await factory.CreateMembershipAsync(
            user.User.Id,
            firstOrganizationId,
            OrganizationRole.Teacher);
        await factory.CreateMembershipAsync(
            user.User.Id,
            secondOrganizationId,
            OrganizationRole.Parent);
        await factory.EndMembershipAsync(first.MembershipId);
        UseBearerToken(client, user.AccessToken);

        var workspaces = await GetWorkspacesAsync(client);

        Assert.DoesNotContain(workspaces, workspace => workspace.OrganizationId == firstOrganizationId);
        Assert.Contains(workspaces, workspace => workspace.OrganizationId == secondOrganizationId);
    }

    [Fact]
    public async Task RevokedMembershipDisappearsFromWorkspaces()
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var organizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var membership = await factory.CreateMembershipAsync(
            user.User.Id,
            organizationId,
            OrganizationRole.Student);
        await factory.EndMembershipAsync(membership.MembershipId);
        UseBearerToken(client, user.AccessToken);

        var workspaces = await GetWorkspacesAsync(client);

        Assert.Single(workspaces);
        Assert.Equal(WorkspaceType.Personal, workspaces[0].WorkspaceType);
    }

    [Fact]
    public async Task PreviouslySelectedRevokedWorkspaceNoLongerAuthorizesRequest()
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var organizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var membership = await factory.CreateMembershipAsync(
            user.User.Id,
            organizationId,
            OrganizationRole.Teacher);
        UseBearerToken(client, user.AccessToken);
        using var selection = await SelectWorkspaceAsync(
            client,
            membership.MembershipId,
            OrganizationRole.Teacher);
        Assert.Equal(HttpStatusCode.OK, selection.StatusCode);
        await factory.EndMembershipAsync(membership.MembershipId);

        using var response = await client.GetAsync("/api/v1/workspaces/current");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(OrganizationErrorCodes.WorkspaceNotAllowed, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task DatabasePreventsDuplicateActiveMembership()
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var organizationId = await factory.CreateOrganizationAsync(NewOrganizationName());

        var enforced = await factory.ActiveMembershipUniquenessIsEnforcedAsync(
            user.User.Id,
            organizationId);

        Assert.True(enforced);
    }

    [Fact]
    public async Task ConcurrentInvitationAcceptanceProducesOneBusinessEffect()
    {
        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(firstClient);
        var organizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var invitationId = await factory.CreateInvitationAsync(
            user.User.Id,
            organizationId,
            OrganizationRole.Teacher);
        UseBearerToken(firstClient, user.AccessToken);
        UseBearerToken(secondClient, user.AccessToken);

        var firstTask = firstClient.PostAsync($"/api/v1/invitations/{invitationId}/accept", null);
        var secondTask = secondClient.PostAsync($"/api/v1/invitations/{invitationId}/accept", null);
        var responses = await Task.WhenAll(firstTask, secondTask);
        foreach (var response in responses)
        {
            using (response)
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }
        }

        Assert.Equal(1, await factory.CountMembershipsAsync(user.User.Id, organizationId));
        var workspaces = await GetWorkspacesAsync(firstClient);
        var organizationWorkspace = Assert.Single(
            workspaces,
            workspace => workspace.OrganizationId == organizationId);
        Assert.Equal(
            1,
            await factory.CountRoleAssignmentsAsync(
                organizationWorkspace.MembershipId!.Value,
                OrganizationRole.Teacher));
    }

    [Fact]
    public async Task UnauthenticatedUserCannotListPrivateWorkspaces()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/v1/workspaces/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(IdentityErrorCodes.AuthRequired, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task UserCannotSelectAnotherUsersMembership()
    {
        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();
        var firstUser = await CreateAuthenticatedUserAsync(firstClient);
        var secondUser = await CreateAuthenticatedUserAsync(secondClient);
        var organizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var membership = await factory.CreateMembershipAsync(
            secondUser.User.Id,
            organizationId,
            OrganizationRole.Teacher);
        UseBearerToken(firstClient, firstUser.AccessToken);

        using var response = await SelectWorkspaceAsync(
            firstClient,
            membership.MembershipId,
            OrganizationRole.Teacher);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(OrganizationErrorCodes.WorkspaceNotFound, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task UserCannotAcceptInvitationForAnotherIdentity()
    {
        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();
        var firstUser = await CreateAuthenticatedUserAsync(firstClient);
        var secondUser = await CreateAuthenticatedUserAsync(secondClient);
        var organizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var invitationId = await factory.CreateInvitationAsync(
            secondUser.User.Id,
            organizationId,
            OrganizationRole.Parent);
        UseBearerToken(firstClient, firstUser.AccessToken);

        using var response = await firstClient.PostAsync(
            $"/api/v1/invitations/{invitationId}/accept",
            null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(OrganizationErrorCodes.InvitationNotFound, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task OrganizationIdManipulationDoesNotChangeAuthorizedWorkspace()
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var allowedOrganizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var unrelatedOrganizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var membership = await factory.CreateMembershipAsync(
            user.User.Id,
            allowedOrganizationId,
            OrganizationRole.Teacher);
        UseBearerToken(client, user.AccessToken);

        using var response = await client.PostAsJsonAsync(
            "/api/v1/workspaces/select",
            new
            {
                workspaceType = "Organization",
                membershipId = membership.MembershipId,
                role = "Teacher",
                organizationId = unrelatedOrganizationId
            });
        var context = await response.Content.ReadFromJsonAsync<AccessContext>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(context);
        Assert.Equal(allowedOrganizationId, context.OrganizationId);
        Assert.NotEqual(unrelatedOrganizationId, context.OrganizationId);
    }

    [Fact]
    public async Task RoleManipulationDoesNotGrantAuthority()
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var organizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var membership = await factory.CreateMembershipAsync(
            user.User.Id,
            organizationId,
            OrganizationRole.Parent);
        UseBearerToken(client, user.AccessToken);

        using var response = await SelectWorkspaceAsync(
            client,
            membership.MembershipId,
            OrganizationRole.Manager);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(OrganizationErrorCodes.RoleNotActive, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task UserSeesOnlyInvitationsIntendedForTheirIdentity()
    {
        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();
        var firstUser = await CreateAuthenticatedUserAsync(firstClient);
        var secondUser = await CreateAuthenticatedUserAsync(secondClient);
        var firstOrganizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var secondOrganizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var firstInvitationId = await factory.CreateInvitationAsync(
            firstUser.User.Id,
            firstOrganizationId,
            OrganizationRole.Teacher);
        await factory.CreateInvitationAsync(
            secondUser.User.Id,
            secondOrganizationId,
            OrganizationRole.Parent);
        UseBearerToken(firstClient, firstUser.AccessToken);

        var invitations = await firstClient.GetFromJsonAsync<InvitationResult[]>(
            "/api/v1/invitations",
            JsonOptions);

        var invitation = Assert.Single(invitations!);
        Assert.Equal(firstInvitationId, invitation.Id);
        Assert.Equal(firstOrganizationId, invitation.OrganizationId);
        Assert.Equal(OrganizationRole.Teacher, invitation.Role);
    }
    [Fact]
    public async Task InvitationCanBeDeclinedWithoutCreatingMembership()
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var organizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var invitationId = await factory.CreateInvitationAsync(
            user.User.Id,
            organizationId,
            OrganizationRole.Student);
        UseBearerToken(client, user.AccessToken);

        using var response = await client.PostAsync(
            $"/api/v1/invitations/{invitationId}/decline",
            null);
        var result = await response.Content.ReadFromJsonAsync<DeclineInvitationResult>(JsonOptions);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(result);
        Assert.Equal(InvitationStatus.Declined, result.Status);
        Assert.Equal(0, await factory.CountMembershipsAsync(user.User.Id, organizationId));
    }

    private async Task<(
        Guid UserId,
        Guid OrganizationId,
        AcceptInvitationResult First,
        AcceptInvitationResult Second)> AcceptInvitationTwiceAsync(OrganizationRole role)
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var organizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var invitationId = await factory.CreateInvitationAsync(user.User.Id, organizationId, role);
        UseBearerToken(client, user.AccessToken);

        using var firstResponse = await client.PostAsync(
            $"/api/v1/invitations/{invitationId}/accept",
            null);
        firstResponse.EnsureSuccessStatusCode();
        var first = (await firstResponse.Content.ReadFromJsonAsync<AcceptInvitationResult>(JsonOptions))!;
        using var secondResponse = await client.PostAsync(
            $"/api/v1/invitations/{invitationId}/accept",
            null);
        secondResponse.EnsureSuccessStatusCode();
        var second = (await secondResponse.Content.ReadFromJsonAsync<AcceptInvitationResult>(JsonOptions))!;

        return (user.User.Id, organizationId, first, second);
    }

    private async Task<VerifyOtpResult> CreateAuthenticatedUserAsync(HttpClient client)
    {
        var sequence = Interlocked.Increment(ref _phoneSequence);
        var phoneNumber = $"+98913{sequence:D7}";
        using var challengeResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/otp/challenges",
            new RequestOtpCommand(phoneNumber));
        challengeResponse.EnsureSuccessStatusCode();
        var challenge = (await challengeResponse.Content.ReadFromJsonAsync<RequestOtpResult>())!;
        using var verifyResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/otp/verify",
            new VerifyOtpCommand(
                challenge.ChallengeId,
                phoneNumber,
                factory.GetOtpCode(challenge.ChallengeId)));
        verifyResponse.EnsureSuccessStatusCode();
        return (await verifyResponse.Content.ReadFromJsonAsync<VerifyOtpResult>())!;
    }

    private static async Task<WorkspaceResult[]> GetWorkspacesAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<WorkspaceResult[]>(
            "/api/v1/workspaces/me",
            JsonOptions))!;

    private static Task<HttpResponseMessage> SelectWorkspaceAsync(
        HttpClient client,
        Guid membershipId,
        OrganizationRole role) =>
        client.PostAsJsonAsync(
            "/api/v1/workspaces/select",
            new SelectWorkspaceCommand(WorkspaceType.Organization, membershipId, role),
            JsonOptions);

    private static void UseBearerToken(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static async Task<string?> ReadProblemCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("code").GetString();
    }

    private static string NewOrganizationName() => $"آموزشگاه {Guid.NewGuid():N}";

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
