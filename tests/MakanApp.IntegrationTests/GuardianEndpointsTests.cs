using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MakanApp.Application.Guardian;
using MakanApp.Application.Identity;
using MakanApp.Application.Organization;
using MakanApp.Domain.Guardian;
using MakanApp.Domain.Organization;
using Xunit;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class GuardianEndpointsTests(MakanAppWebApplicationFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static int _phoneSequence = 500;

    [Fact]
    public async Task ParentWithOneActiveRelationSeesOneChild()
    {
        using var client = factory.CreateClient();
        var scenario = await CreateParentScenarioAsync(client);

        var children = await GetChildrenAsync(client);

        var child = Assert.Single(children);
        Assert.Equal(scenario.Children[0].OrganizationPersonId, child.LearnerOrganizationPersonId);
        Assert.Equal(scenario.OrganizationId, child.OrganizationId);
        Assert.Equal(GuardianRelationStatus.Active, child.RelationStatus);
    }

    [Fact]
    public async Task ParentWithTwoActiveRelationsSeesBothChildren()
    {
        using var client = factory.CreateClient();
        var scenario = await CreateParentScenarioAsync(client, childCount: 2);

        var children = await GetChildrenAsync(client);

        Assert.Equal(2, children.Length);
        Assert.All(
            scenario.Children,
            expected => Assert.Contains(
                children,
                child => child.LearnerOrganizationPersonId == expected.OrganizationPersonId));
    }

    [Fact]
    public async Task ChildrenAreScopedToCurrentOrganization()
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var first = await CreateParentOrganizationAsync(user.User.Id);
        var second = await CreateParentOrganizationAsync(user.User.Id);
        var firstChild = await CreateChildAsync(user.User.Id, first.OrganizationId);
        var secondChild = await CreateChildAsync(user.User.Id, second.OrganizationId);
        UseBearerToken(client, user.AccessToken);

        await SelectWorkspaceSuccessfullyAsync(client, first.MembershipId, OrganizationRole.Parent);
        var firstChildren = await GetChildrenAsync(client);
        await SelectWorkspaceSuccessfullyAsync(client, second.MembershipId, OrganizationRole.Parent);
        var secondChildren = await GetChildrenAsync(client);

        Assert.Equal(firstChild.OrganizationPersonId, Assert.Single(firstChildren).LearnerOrganizationPersonId);
        Assert.Equal(secondChild.OrganizationPersonId, Assert.Single(secondChildren).LearnerOrganizationPersonId);
    }

    [Fact]
    public async Task SameLearnerCanBeAuthorizedForMultipleGuardians()
    {
        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();
        var firstUser = await CreateAuthenticatedUserAsync(firstClient);
        var secondUser = await CreateAuthenticatedUserAsync(secondClient);
        var organizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var firstMembership = await factory.CreateMembershipAsync(
            firstUser.User.Id,
            organizationId,
            OrganizationRole.Parent);
        var secondMembership = await factory.CreateMembershipAsync(
            secondUser.User.Id,
            organizationId,
            OrganizationRole.Parent);
        var personId = await factory.CreatePersonWithoutUserAsync();
        var organizationPersonId = await factory.CreateOrganizationPersonAsync(organizationId, personId);
        await factory.CreateGuardianRelationAsync(firstUser.User.Id, organizationId, organizationPersonId);
        await factory.CreateGuardianRelationAsync(secondUser.User.Id, organizationId, organizationPersonId);
        UseBearerToken(firstClient, firstUser.AccessToken);
        UseBearerToken(secondClient, secondUser.AccessToken);
        await SelectWorkspaceSuccessfullyAsync(
            firstClient,
            firstMembership.MembershipId,
            OrganizationRole.Parent);
        await SelectWorkspaceSuccessfullyAsync(
            secondClient,
            secondMembership.MembershipId,
            OrganizationRole.Parent);

        var firstChildren = await GetChildrenAsync(firstClient);
        var secondChildren = await GetChildrenAsync(secondClient);

        Assert.Equal(organizationPersonId, Assert.Single(firstChildren).LearnerOrganizationPersonId);
        Assert.Equal(organizationPersonId, Assert.Single(secondChildren).LearnerOrganizationPersonId);
    }

    [Fact]
    public async Task ParentCannotSelectArbitraryLearner()
    {
        using var client = factory.CreateClient();
        await CreateParentScenarioAsync(client);

        using var response = await client.PostAsync(
            $"/api/v1/guardian/children/{Guid.NewGuid()}/select",
            null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(GuardianErrorCodes.ChildContextNotFound, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task ParentRoleWithoutRelationCannotAccessLearner()
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var parent = await CreateParentOrganizationAsync(user.User.Id);
        var personId = await factory.CreatePersonWithoutUserAsync();
        var organizationPersonId = await factory.CreateOrganizationPersonAsync(
            parent.OrganizationId,
            personId);
        UseBearerToken(client, user.AccessToken);
        await SelectWorkspaceSuccessfullyAsync(client, parent.MembershipId, OrganizationRole.Parent);

        using var response = await client.PostAsync(
            $"/api/v1/guardian/children/{organizationPersonId}/select",
            null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(GuardianErrorCodes.ChildContextNotFound, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task GuardianEndpointsRequireActiveParentWorkspace()
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var organizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var membership = await factory.CreateMembershipAsync(
            user.User.Id,
            organizationId,
            OrganizationRole.Teacher);
        UseBearerToken(client, user.AccessToken);
        await SelectWorkspaceSuccessfullyAsync(client, membership.MembershipId, OrganizationRole.Teacher);

        using var response = await client.GetAsync("/api/v1/guardian/children");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(GuardianErrorCodes.ParentRoleRequired, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task TeacherWorkspaceCannotLeakChildrenFromParentWorkspace()
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var teacherOrganizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var teacherMembership = await factory.CreateMembershipAsync(
            user.User.Id,
            teacherOrganizationId,
            OrganizationRole.Teacher);
        var parent = await CreateParentOrganizationAsync(user.User.Id);
        var child = await CreateChildAsync(user.User.Id, parent.OrganizationId);
        UseBearerToken(client, user.AccessToken);
        await SelectWorkspaceSuccessfullyAsync(
            client,
            teacherMembership.MembershipId,
            OrganizationRole.Teacher);

        using var teacherResponse = await client.GetAsync("/api/v1/guardian/children");
        await SelectWorkspaceSuccessfullyAsync(client, parent.MembershipId, OrganizationRole.Parent);
        var parentChildren = await GetChildrenAsync(client);

        Assert.Equal(HttpStatusCode.Forbidden, teacherResponse.StatusCode);
        Assert.Equal(GuardianErrorCodes.ParentRoleRequired, await ReadProblemCodeAsync(teacherResponse));
        Assert.Equal(child.OrganizationPersonId, Assert.Single(parentChildren).LearnerOrganizationPersonId);
    }

    [Fact]
    public async Task RevokedRelationDisappearsFromAuthorizedChildren()
    {
        using var client = factory.CreateClient();
        var scenario = await CreateParentScenarioAsync(client);
        await factory.RevokeGuardianRelationAsync(scenario.Children[0].RelationId);

        var children = await GetChildrenAsync(client);

        Assert.Empty(children);
    }

    [Fact]
    public async Task RevokedSelectedChildNoLongerAuthorizesCurrentContext()
    {
        using var client = factory.CreateClient();
        var scenario = await CreateParentScenarioAsync(client);
        await SelectChildSuccessfullyAsync(client, scenario.Children[0].OrganizationPersonId);
        await factory.RevokeGuardianRelationAsync(scenario.Children[0].RelationId);

        using var response = await client.GetAsync("/api/v1/workspaces/current");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(GuardianErrorCodes.ChildContextNotAllowed, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task RevokedRelationCannotBeSelectedAgain()
    {
        using var client = factory.CreateClient();
        var scenario = await CreateParentScenarioAsync(client);
        await factory.RevokeGuardianRelationAsync(scenario.Children[0].RelationId);

        using var response = await client.PostAsync(
            $"/api/v1/guardian/children/{scenario.Children[0].OrganizationPersonId}/select",
            null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(GuardianErrorCodes.ChildContextNotFound, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task RevokingOneRelationLeavesAnotherChildAvailable()
    {
        using var client = factory.CreateClient();
        var scenario = await CreateParentScenarioAsync(client, childCount: 2);
        await factory.RevokeGuardianRelationAsync(scenario.Children[0].RelationId);

        var children = await GetChildrenAsync(client);
        var selected = await SelectChildSuccessfullyAsync(
            client,
            scenario.Children[1].OrganizationPersonId);

        Assert.Equal(scenario.Children[1].OrganizationPersonId, Assert.Single(children).LearnerOrganizationPersonId);
        Assert.Equal(scenario.Children[1].OrganizationPersonId, selected.SubjectOrganizationPersonId);
    }

    [Fact]
    public async Task RelationSummaryReturnsCurrentAuthorizedRelation()
    {
        using var client = factory.CreateClient();
        var scenario = await CreateParentScenarioAsync(client);

        var summary = await client.GetFromJsonAsync<GuardianRelationSummaryResult>(
            $"/api/v1/guardian/relations/{scenario.Children[0].OrganizationPersonId}",
            JsonOptions);

        Assert.NotNull(summary);
        Assert.Equal(scenario.Children[0].RelationId, summary.RelationId);
        Assert.Equal(GuardianRelationStatus.Active, summary.Status);
        Assert.Equal(scenario.OrganizationId, summary.OrganizationId);
    }

    [Fact]
    public async Task PendingRelationDoesNotAuthorizeChild()
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var parent = await CreateParentOrganizationAsync(user.User.Id);
        var personId = await factory.CreatePersonWithoutUserAsync();
        var organizationPersonId = await factory.CreateOrganizationPersonAsync(
            parent.OrganizationId,
            personId);
        await factory.CreateGuardianRelationAsync(
            user.User.Id,
            parent.OrganizationId,
            organizationPersonId,
            GuardianRelationStatus.Pending);
        UseBearerToken(client, user.AccessToken);
        await SelectWorkspaceSuccessfullyAsync(client, parent.MembershipId, OrganizationRole.Parent);

        var children = await GetChildrenAsync(client);

        Assert.Empty(children);
    }

    [Fact]
    public async Task InactiveOrganizationPersonDoesNotAuthorizeChild()
    {
        using var client = factory.CreateClient();
        var scenario = await CreateParentScenarioAsync(client);
        await factory.EndOrganizationPersonAsync(scenario.Children[0].OrganizationPersonId);

        var children = await GetChildrenAsync(client);

        Assert.Empty(children);
    }

    [Fact]
    public async Task SelectingDifferentWorkspaceClearsSelectedChild()
    {
        using var client = factory.CreateClient();
        var scenario = await CreateParentScenarioAsync(client);
        await SelectChildSuccessfullyAsync(client, scenario.Children[0].OrganizationPersonId);
        var second = await CreateParentOrganizationAsync(scenario.UserId);

        var context = await SelectWorkspaceSuccessfullyAsync(
            client,
            second.MembershipId,
            OrganizationRole.Parent);
        var current = await client.GetFromJsonAsync<AccessContext>(
            "/api/v1/workspaces/current",
            JsonOptions);

        Assert.Null(context.SubjectOrganizationPersonId);
        Assert.NotNull(current);
        Assert.Null(current.SubjectOrganizationPersonId);
        Assert.Equal(second.OrganizationId, current.OrganizationId);
    }

    [Fact]
    public async Task DatabaseRejectsDuplicateActiveRelation()
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var parent = await CreateParentOrganizationAsync(user.User.Id);
        var personId = await factory.CreatePersonWithoutUserAsync();
        var organizationPersonId = await factory.CreateOrganizationPersonAsync(
            parent.OrganizationId,
            personId);

        var rejected = await factory.DuplicateActiveGuardianRelationIsRejectedAsync(
            user.User.Id,
            parent.OrganizationId,
            organizationPersonId);

        Assert.True(rejected);
    }

    [Fact]
    public async Task CompositeForeignKeyRejectsCrossOrganizationLearner()
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var firstOrganizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var secondOrganizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var personId = await factory.CreatePersonWithoutUserAsync();
        var secondOrganizationPersonId = await factory.CreateOrganizationPersonAsync(
            secondOrganizationId,
            personId);

        var rejected = await factory.CrossOrganizationGuardianRelationIsRejectedAsync(
            user.User.Id,
            firstOrganizationId,
            secondOrganizationPersonId);

        Assert.True(rejected);
    }

    [Fact]
    public async Task ConcurrentCreationProducesOneActiveRelation()
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var parent = await CreateParentOrganizationAsync(user.User.Id);
        var personId = await factory.CreatePersonWithoutUserAsync();
        var organizationPersonId = await factory.CreateOrganizationPersonAsync(
            parent.OrganizationId,
            personId);

        var result = await factory.CreateGuardianRelationsConcurrentlyAsync(
            user.User.Id,
            parent.OrganizationId,
            organizationPersonId);

        Assert.Equal(1, result.SuccessfulWrites);
        Assert.Equal(1, result.ActiveRelations);
    }

    [Fact]
    public async Task RowVersionRejectsStaleRelationMutation()
    {
        using var client = factory.CreateClient();
        var scenario = await CreateParentScenarioAsync(client);

        var detected = await factory.GuardianRelationRowVersionDetectsStaleWriteAsync(
            scenario.Children[0].RelationId);

        Assert.True(detected);
    }

    [Fact]
    public async Task UnauthenticatedUserCannotListChildren()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/v1/guardian/children");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RequestSelectorsCannotOverrideSessionWorkspace()
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var first = await CreateParentOrganizationAsync(user.User.Id);
        var second = await CreateParentOrganizationAsync(user.User.Id);
        var secondChild = await CreateChildAsync(user.User.Id, second.OrganizationId);
        UseBearerToken(client, user.AccessToken);
        await SelectWorkspaceSuccessfullyAsync(client, first.MembershipId, OrganizationRole.Parent);

        using var response = await client.PostAsJsonAsync(
            $"/api/v1/guardian/children/{secondChild.OrganizationPersonId}/select" +
            $"?organizationId={second.OrganizationId}&membershipId={second.MembershipId}&role=Parent",
            new
            {
                OrganizationId = second.OrganizationId,
                MembershipId = second.MembershipId,
                Role = OrganizationRole.Parent
            },
            JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(GuardianErrorCodes.ChildContextNotFound, await ReadProblemCodeAsync(response));
    }

    private async Task<GuardianScenario> CreateParentScenarioAsync(
        HttpClient client,
        int childCount = 1)
    {
        var user = await CreateAuthenticatedUserAsync(client);
        var parent = await CreateParentOrganizationAsync(user.User.Id);
        var children = new List<ChildFixture>();
        for (var index = 0; index < childCount; index++)
        {
            children.Add(await CreateChildAsync(user.User.Id, parent.OrganizationId));
        }

        UseBearerToken(client, user.AccessToken);
        await SelectWorkspaceSuccessfullyAsync(client, parent.MembershipId, OrganizationRole.Parent);
        return new GuardianScenario(
            user.User.Id,
            parent.OrganizationId,
            parent.MembershipId,
            children);
    }

    private async Task<(Guid OrganizationId, Guid MembershipId)> CreateParentOrganizationAsync(
        Guid userId)
    {
        var organizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var membership = await factory.CreateMembershipAsync(
            userId,
            organizationId,
            OrganizationRole.Parent);
        return (organizationId, membership.MembershipId);
    }

    private async Task<ChildFixture> CreateChildAsync(Guid userId, Guid organizationId)
    {
        var personId = await factory.CreatePersonWithoutUserAsync();
        var organizationPersonId = await factory.CreateOrganizationPersonAsync(
            organizationId,
            personId);
        var relationId = await factory.CreateGuardianRelationAsync(
            userId,
            organizationId,
            organizationPersonId);
        return new ChildFixture(organizationPersonId, relationId);
    }

    private async Task<VerifyOtpResult> CreateAuthenticatedUserAsync(HttpClient client)
    {
        var sequence = Interlocked.Increment(ref _phoneSequence);
        var phoneNumber = $"+98914{sequence:D7}";
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

    private static async Task<AuthorizedChildResult[]> GetChildrenAsync(HttpClient client) =>
        (await client.GetFromJsonAsync<AuthorizedChildResult[]>(
            "/api/v1/guardian/children",
            JsonOptions))!;

    private static async Task<AccessContext> SelectWorkspaceSuccessfullyAsync(
        HttpClient client,
        Guid membershipId,
        OrganizationRole role)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/v1/workspaces/select",
            new SelectWorkspaceCommand(WorkspaceType.Organization, membershipId, role),
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AccessContext>(JsonOptions))!;
    }

    private static async Task<AccessContext> SelectChildSuccessfullyAsync(
        HttpClient client,
        Guid organizationPersonId)
    {
        using var response = await client.PostAsync(
            $"/api/v1/guardian/children/{organizationPersonId}/select",
            null);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AccessContext>(JsonOptions))!;
    }

    private static void UseBearerToken(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static async Task<string?> ReadProblemCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("code").GetString();
    }

    private static string NewOrganizationName() => $"Guardian Test {Guid.NewGuid():N}";

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed record ChildFixture(Guid OrganizationPersonId, Guid RelationId);

    private sealed record GuardianScenario(
        Guid UserId,
        Guid OrganizationId,
        Guid MembershipId,
        IReadOnlyList<ChildFixture> Children);
}
