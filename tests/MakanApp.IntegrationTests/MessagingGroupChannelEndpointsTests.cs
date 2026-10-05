using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task GroupCreationIsIdempotentAndReturnsOwnerAndInitialMember()
    {
        using var ownerClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();
        var owner = await CreateMessagingUserAsync(ownerClient);
        var member = await CreateMessagingUserAsync(memberClient);
        await factory.CreatePersonalCommunicationGrantAsync(owner.UserId, member.UserId);
        var operationId = Guid.NewGuid();
        var command = new CreateManagedConversationCommand(
            operationId,
            ConversationScope.Personal,
            "گروه مطالعه",
            "مرور درس",
            new[] { member.UserId });

        using var createdResponse = await ownerClient.PostAsJsonAsync(
            "/api/v1/conversations/groups",
            command,
            JsonOptions);
        using var retriedResponse = await ownerClient.PostAsJsonAsync(
            "/api/v1/conversations/groups",
            command,
            JsonOptions);
        var created = await createdResponse.Content.ReadFromJsonAsync<ManagedConversationResult>(JsonOptions);
        var retried = await retriedResponse.Content.ReadFromJsonAsync<ManagedConversationResult>(JsonOptions);
        using var conflict = await ownerClient.PostAsJsonAsync(
            "/api/v1/conversations/groups",
            command with { Title = "عنوان متفاوت" },
            JsonOptions);

        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, retriedResponse.StatusCode);
        Assert.False(created!.AlreadyExisted);
        Assert.True(retried!.AlreadyExisted);
        Assert.Equal(created.ConversationId, retried.ConversationId);
        Assert.Equal(
            ConversationParticipantRole.Owner,
            created.Participants.Single(item => item.User.UserId == owner.UserId).Role);
        Assert.Equal(
            ConversationParticipantRole.Member,
            created.Participants.Single(item => item.User.UserId == member.UserId).Role);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal(
            MessagingErrorCodes.ConversationCreationConflict,
            await ReadProblemCodeAsync(conflict));

        var summaries = await ownerClient.GetFromJsonAsync<ConversationSummaryResult[]>(
            "/api/v1/conversations",
            JsonOptions);
        var summary = Assert.Single(summaries!);
        Assert.Equal("گروه مطالعه", summary.Title);
        Assert.Null(summary.OtherParticipant);
    }

    [Fact]
    public async Task ChannelMemberCannotPublishButAdminCanAndCreationRetryIsStable()
    {
        using var ownerClient = factory.CreateClient();
        using var adminClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();
        var owner = await CreateMessagingUserAsync(ownerClient);
        var admin = await CreateMessagingUserAsync(adminClient);
        var member = await CreateMessagingUserAsync(memberClient);
        await factory.CreatePersonalCommunicationGrantAsync(owner.UserId, admin.UserId);
        await factory.CreatePersonalCommunicationGrantAsync(owner.UserId, member.UserId);
        var command = new CreateManagedConversationCommand(
            Guid.NewGuid(),
            ConversationScope.Personal,
            "کانال اطلاع‌رسانی",
            null,
            new[] { admin.UserId, member.UserId });
        var channel = await CreateManagedAndReadAsync(ownerClient, "channels", command);
        using var retry = await ownerClient.PostAsJsonAsync(
            "/api/v1/conversations/channels",
            command,
            JsonOptions);
        var retried = await retry.Content.ReadFromJsonAsync<ManagedConversationResult>(JsonOptions);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        Assert.Equal(channel.ConversationId, retried!.ConversationId);

        using var promote = await ownerClient.PatchAsJsonAsync(
            $"/api/v1/conversations/{channel.ConversationId}/members/{admin.UserId}/role",
            new ChangeConversationMemberRoleCommand(ConversationParticipantRole.Admin),
            JsonOptions);
        promote.EnsureSuccessStatusCode();
        using var memberSend = await SendMessageAsync(
            memberClient,
            channel.ConversationId,
            Guid.NewGuid(),
            "عضو نباید منتشر کند");
        using var adminSend = await SendMessageAsync(
            adminClient,
            channel.ConversationId,
            Guid.NewGuid(),
            "پیام مدیر کانال");

        Assert.Equal(HttpStatusCode.Forbidden, memberSend.StatusCode);
        Assert.Equal(
            MessagingErrorCodes.MessagePublishNotAllowed,
            await ReadProblemCodeAsync(memberSend));
        Assert.Equal(HttpStatusCode.OK, adminSend.StatusCode);
    }

    [Fact]
    public async Task ConcurrentGroupCreationReturnsOneStableConversation()
    {
        using var ownerClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();
        var owner = await CreateMessagingUserAsync(ownerClient);
        var member = await CreateMessagingUserAsync(memberClient);
        await factory.CreatePersonalCommunicationGrantAsync(owner.UserId, member.UserId);
        var operationId = Guid.NewGuid();
        var command = new CreateManagedConversationCommand(
            operationId,
            ConversationScope.Personal,
            "ساخت هم‌زمان گروه",
            null,
            new[] { member.UserId });
        var clients = Enumerable.Range(0, 5).Select(_ => factory.CreateClient()).ToArray();
        foreach (var client in clients)
        {
            UseBearerToken(client, owner.AccessToken);
        }

        try
        {
            var responses = await Task.WhenAll(clients.Select(client => client.PostAsJsonAsync(
                "/api/v1/conversations/groups",
                command,
                JsonOptions)));
            try
            {
                Assert.All(responses, response => Assert.True(response.IsSuccessStatusCode));
                var results = await Task.WhenAll(responses.Select(response =>
                    response.Content.ReadFromJsonAsync<ManagedConversationResult>(JsonOptions)));
                Assert.Single(results.Select(result => result!.ConversationId).Distinct());
            }
            finally
            {
                foreach (var response in responses)
                {
                    response.Dispose();
                }
            }
        }
        finally
        {
            foreach (var client in clients)
            {
                client.Dispose();
            }
        }

        Assert.Equal(
            1,
            await factory.CountManagedConversationsAsync(owner.UserId, operationId));
    }

    [Fact]
    public async Task ActiveGroupMemberCanPublish()
    {
        using var ownerClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();
        var owner = await CreateMessagingUserAsync(ownerClient);
        var member = await CreateMessagingUserAsync(memberClient);
        await factory.CreatePersonalCommunicationGrantAsync(owner.UserId, member.UserId);
        var group = await CreateManagedAndReadAsync(
            ownerClient,
            "groups",
            PersonalCommand("گروه گفتگو", member.UserId));

        using var response = await SendMessageAsync(
            memberClient,
            group.ConversationId,
            Guid.NewGuid(),
            "پیام عضو گروه");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(await factory.GetMessagesAsync(group.ConversationId));
    }

    [Fact]
    public async Task AdminCanManageMembersOnlyAndReentryCreatesNewLifecycleRow()
    {
        using var ownerClient = factory.CreateClient();
        using var adminClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();
        using var newMemberClient = factory.CreateClient();
        var owner = await CreateMessagingUserAsync(ownerClient);
        var admin = await CreateMessagingUserAsync(adminClient);
        var member = await CreateMessagingUserAsync(memberClient);
        var newMember = await CreateMessagingUserAsync(newMemberClient);
        await factory.CreatePersonalCommunicationGrantAsync(owner.UserId, admin.UserId);
        await factory.CreatePersonalCommunicationGrantAsync(owner.UserId, member.UserId);
        await factory.CreatePersonalCommunicationGrantAsync(admin.UserId, newMember.UserId);
        var group = await CreateManagedAndReadAsync(
            ownerClient,
            "groups",
            new CreateManagedConversationCommand(
                Guid.NewGuid(),
                ConversationScope.Personal,
                "گروه مدیریت اعضا",
                null,
                new[] { admin.UserId, member.UserId }));
        using var promote = await ownerClient.PatchAsJsonAsync(
            $"/api/v1/conversations/{group.ConversationId}/members/{admin.UserId}/role",
            new ChangeConversationMemberRoleCommand(ConversationParticipantRole.Admin),
            JsonOptions);
        promote.EnsureSuccessStatusCode();

        using var add = await adminClient.PostAsJsonAsync(
            $"/api/v1/conversations/{group.ConversationId}/members",
            new AddConversationMemberCommand(newMember.UserId),
            JsonOptions);
        using var removeOwner = await adminClient.DeleteAsync(
            $"/api/v1/conversations/{group.ConversationId}/members/{owner.UserId}");
        using var changeRole = await adminClient.PatchAsJsonAsync(
            $"/api/v1/conversations/{group.ConversationId}/members/{member.UserId}/role",
            new ChangeConversationMemberRoleCommand(ConversationParticipantRole.Admin),
            JsonOptions);
        using var removeMember = await adminClient.DeleteAsync(
            $"/api/v1/conversations/{group.ConversationId}/members/{member.UserId}");

        Assert.Equal(HttpStatusCode.OK, add.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, removeOwner.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, changeRole.StatusCode);
        Assert.Equal(HttpStatusCode.OK, removeMember.StatusCode);
        using var deniedDetails = await memberClient.GetAsync(
            $"/api/v1/conversations/{group.ConversationId}");
        Assert.Equal(HttpStatusCode.NotFound, deniedDetails.StatusCode);

        using var readd = await ownerClient.PostAsJsonAsync(
            $"/api/v1/conversations/{group.ConversationId}/members",
            new AddConversationMemberCommand(member.UserId),
            JsonOptions);
        readd.EnsureSuccessStatusCode();
        var rows = (await factory.GetConversationParticipantsAsync(group.ConversationId))
            .Where(item => item.UserId == member.UserId)
            .ToArray();
        Assert.Equal(2, rows.Length);
        Assert.Single(rows, item => item.Status == ConversationParticipantStatus.Active);
        Assert.Single(rows, item => item.Status == ConversationParticipantStatus.Removed);

        using var leave = await memberClient.PostAsync(
            $"/api/v1/conversations/{group.ConversationId}/leave",
            null);
        leave.EnsureSuccessStatusCode();
        using var deniedSend = await SendMessageAsync(
            memberClient,
            group.ConversationId,
            Guid.NewGuid(),
            "پس از خروج");
        Assert.Equal(HttpStatusCode.NotFound, deniedSend.StatusCode);
        var endedRows = (await factory.GetConversationParticipantsAsync(group.ConversationId))
            .Where(item => item.UserId == member.UserId)
            .ToArray();
        Assert.DoesNotContain(endedRows, item => item.Status == ConversationParticipantStatus.Active);
        Assert.Contains(endedRows, item => item.Status == ConversationParticipantStatus.Left);
    }

    [Fact]
    public async Task OwnerCannotLeaveAndAcceptedTransferIsIdempotentWithExactlyOneOwner()
    {
        using var ownerClient = factory.CreateClient();
        using var targetClient = factory.CreateClient();
        var owner = await CreateMessagingUserAsync(ownerClient);
        var target = await CreateMessagingUserAsync(targetClient);
        await factory.CreatePersonalCommunicationGrantAsync(owner.UserId, target.UserId);
        var group = await CreateManagedAndReadAsync(
            ownerClient,
            "groups",
            PersonalCommand("انتقال مالکیت", target.UserId));

        using var ownerLeave = await ownerClient.PostAsync(
            $"/api/v1/conversations/{group.ConversationId}/leave",
            null);
        Assert.Equal(HttpStatusCode.Conflict, ownerLeave.StatusCode);
        Assert.Equal(MessagingErrorCodes.LastOwnerRequired, await ReadProblemCodeAsync(ownerLeave));

        using var start = await ownerClient.PostAsJsonAsync(
            $"/api/v1/conversations/{group.ConversationId}/ownership-transfers",
            new StartOwnershipTransferCommand(target.UserId),
            JsonOptions);
        start.EnsureSuccessStatusCode();
        var transfer = await start.Content.ReadFromJsonAsync<OwnershipTransferResult>(JsonOptions);
        var acceptTasks = Enumerable.Range(0, 2).Select(_ => targetClient.PostAsync(
            $"/api/v1/conversations/{group.ConversationId}/ownership-transfers/{transfer!.TransferId}/accept",
            null));
        var acceptResponses = await Task.WhenAll(acceptTasks);
        try
        {
            Assert.All(acceptResponses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        }
        finally
        {
            foreach (var response in acceptResponses)
            {
                response.Dispose();
            }
        }

        var rows = await factory.GetConversationParticipantsAsync(group.ConversationId);
        Assert.Single(rows, item =>
            item.Status == ConversationParticipantStatus.Active &&
            item.Role == ConversationParticipantRole.Owner &&
            item.UserId == target.UserId);
        Assert.Contains(rows, item =>
            item.UserId == owner.UserId && item.Role == ConversationParticipantRole.Admin);

        using var retry = await targetClient.PostAsync(
            $"/api/v1/conversations/{group.ConversationId}/ownership-transfers/{transfer!.TransferId}/accept",
            null);
        var retryResult = await retry.Content.ReadFromJsonAsync<OwnershipTransferResult>(JsonOptions);
        Assert.True(retryResult!.AlreadyCompleted);
    }

    [Fact]
    public async Task DeclinedOwnershipTransferKeepsExistingOwner()
    {
        using var ownerClient = factory.CreateClient();
        using var targetClient = factory.CreateClient();
        var owner = await CreateMessagingUserAsync(ownerClient);
        var target = await CreateMessagingUserAsync(targetClient);
        await factory.CreatePersonalCommunicationGrantAsync(owner.UserId, target.UserId);
        var group = await CreateManagedAndReadAsync(
            ownerClient,
            "groups",
            PersonalCommand("رد انتقال", target.UserId));
        using var start = await ownerClient.PostAsJsonAsync(
            $"/api/v1/conversations/{group.ConversationId}/ownership-transfers",
            new StartOwnershipTransferCommand(target.UserId),
            JsonOptions);
        var transfer = await start.Content.ReadFromJsonAsync<OwnershipTransferResult>(JsonOptions);

        using var decline = await targetClient.PostAsync(
            $"/api/v1/conversations/{group.ConversationId}/ownership-transfers/{transfer!.TransferId}/decline",
            null);

        Assert.Equal(HttpStatusCode.OK, decline.StatusCode);
        var rows = await factory.GetConversationParticipantsAsync(group.ConversationId);
        Assert.Single(rows, item =>
            item.Role == ConversationParticipantRole.Owner && item.UserId == owner.UserId);
    }

    [Fact]
    public async Task ConcurrentMemberAddsProduceExactlyOneActiveMembership()
    {
        using var ownerClient = factory.CreateClient();
        using var targetClient = factory.CreateClient();
        var owner = await CreateMessagingUserAsync(ownerClient);
        var target = await CreateMessagingUserAsync(targetClient);
        await factory.CreatePersonalCommunicationGrantAsync(owner.UserId, target.UserId);
        var group = await CreateManagedAndReadAsync(
            ownerClient,
            "groups",
            new CreateManagedConversationCommand(
                Guid.NewGuid(),
                ConversationScope.Personal,
                "افزودن هم‌زمان",
                null,
                Array.Empty<Guid>()));
        var clients = Enumerable.Range(0, 5).Select(_ => factory.CreateClient()).ToArray();
        foreach (var client in clients)
        {
            UseBearerToken(client, owner.AccessToken);
        }

        try
        {
            var responses = await Task.WhenAll(clients.Select(client => client.PostAsJsonAsync(
                $"/api/v1/conversations/{group.ConversationId}/members",
                new AddConversationMemberCommand(target.UserId),
                JsonOptions)));
            try
            {
                Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
                Assert.All(
                    responses.Where(response => response.StatusCode != HttpStatusCode.OK),
                    response => Assert.Equal(HttpStatusCode.Conflict, response.StatusCode));
            }
            finally
            {
                foreach (var response in responses)
                {
                    response.Dispose();
                }
            }
        }
        finally
        {
            foreach (var client in clients)
            {
                client.Dispose();
            }
        }

        var rows = await factory.GetConversationParticipantsAsync(group.ConversationId);
        Assert.Single(rows, item =>
            item.UserId == target.UserId && item.Status == ConversationParticipantStatus.Active);
    }

    [Fact]
    public async Task ArchiveRejectsNewMessagesButPreservesCommittedHistory()
    {
        using var ownerClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();
        var owner = await CreateMessagingUserAsync(ownerClient);
        var member = await CreateMessagingUserAsync(memberClient);
        await factory.CreatePersonalCommunicationGrantAsync(owner.UserId, member.UserId);
        var group = await CreateManagedAndReadAsync(
            ownerClient,
            "groups",
            PersonalCommand("بایگانی", member.UserId));
        await SendMessageAndReadAsync(
            memberClient,
            group.ConversationId,
            Guid.NewGuid(),
            "پیش از بایگانی");
        using var archive = await ownerClient.PostAsync(
            $"/api/v1/conversations/{group.ConversationId}/archive",
            null);
        archive.EnsureSuccessStatusCode();

        using var blocked = await SendMessageAsync(
            ownerClient,
            group.ConversationId,
            Guid.NewGuid(),
            "پس از بایگانی");
        using var history = await memberClient.GetAsync(
            $"/api/v1/conversations/{group.ConversationId}/messages");

        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Equal(MessagingErrorCodes.ConversationArchived, await ReadProblemCodeAsync(blocked));
        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        Assert.Single(await factory.GetMessagesAsync(group.ConversationId));
    }

    [Fact]
    public async Task OrganizationCreationUsesAuthoritativeWorkspaceAndRejectsIneligibleActorsAndTargets()
    {
        using var managerClient = factory.CreateClient();
        using var teacherClient = factory.CreateClient();
        using var studentClient = factory.CreateClient();
        using var outsiderClient = factory.CreateClient();
        var manager = await CreateMessagingUserAsync(managerClient);
        var teacher = await CreateMessagingUserAsync(teacherClient);
        var student = await CreateMessagingUserAsync(studentClient);
        var outsider = await CreateMessagingUserAsync(outsiderClient);
        var organizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var otherOrganizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var managerMembership = await factory.CreateMembershipAsync(
            manager.UserId,
            organizationId,
            OrganizationRole.Manager);
        await factory.CreateMembershipAsync(teacher.UserId, organizationId, OrganizationRole.Teacher);
        var studentMembership = await factory.CreateMembershipAsync(
            student.UserId,
            organizationId,
            OrganizationRole.Student);
        await factory.CreateMembershipAsync(outsider.UserId, otherOrganizationId, OrganizationRole.Teacher);
        await SelectOrganizationAsync(
            managerClient,
            managerMembership.MembershipId,
            OrganizationRole.Manager);

        var created = await CreateManagedAndReadAsync(
            managerClient,
            "groups",
            new CreateManagedConversationCommand(
                Guid.NewGuid(),
                ConversationScope.Organization,
                "گروه سازمانی",
                null,
                new[] { teacher.UserId }));
        Assert.Equal(organizationId, created.OrganizationId);

        using var crossOrganization = await managerClient.PostAsJsonAsync(
            "/api/v1/conversations/groups",
            new CreateManagedConversationCommand(
                Guid.NewGuid(),
                ConversationScope.Organization,
                "عضو نامعتبر",
                null,
                new[] { outsider.UserId }),
            JsonOptions);
        Assert.Equal(HttpStatusCode.NotFound, crossOrganization.StatusCode);

        await SelectOrganizationAsync(
            studentClient,
            studentMembership.MembershipId,
            OrganizationRole.Student);
        using var studentCreate = await studentClient.PostAsJsonAsync(
            "/api/v1/conversations/groups",
            new CreateManagedConversationCommand(
                Guid.NewGuid(),
                ConversationScope.Organization,
                "غیرمجاز",
                null,
                Array.Empty<Guid>()),
            JsonOptions);
        Assert.Equal(HttpStatusCode.Forbidden, studentCreate.StatusCode);
    }

    [Fact]
    public async Task SystemManagedConversationRejectsPublicMutationAndUnrelatedDetailsAreConcealed()
    {
        using var managerClient = factory.CreateClient();
        using var outsiderClient = factory.CreateClient();
        using var anonymousClient = factory.CreateClient();
        var manager = await CreateMessagingUserAsync(managerClient);
        var outsider = await CreateMessagingUserAsync(outsiderClient);
        var organizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var membership = await factory.CreateMembershipAsync(
            manager.UserId,
            organizationId,
            OrganizationRole.Manager);
        await SelectOrganizationAsync(
            managerClient,
            membership.MembershipId,
            OrganizationRole.Manager);
        var conversationId = await factory.CreateSystemManagedConversationAsync(
            organizationId,
            manager.UserId,
            ConversationType.Group);

        using var mutate = await managerClient.PostAsJsonAsync(
            $"/api/v1/conversations/{conversationId}/members",
            new AddConversationMemberCommand(outsider.UserId),
            JsonOptions);
        using var publish = await SendMessageAsync(
            managerClient,
            conversationId,
            Guid.NewGuid(),
            "انتشار غیرمجاز");
        using var unrelated = await outsiderClient.GetAsync(
            $"/api/v1/conversations/{conversationId}");
        using var anonymous = await anonymousClient.GetAsync(
            $"/api/v1/conversations/{conversationId}");

        Assert.Equal(HttpStatusCode.Forbidden, mutate.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, publish.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unrelated.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    private static CreateManagedConversationCommand PersonalCommand(
        string title,
        params Guid[] participantUserIds) =>
        new(
            Guid.NewGuid(),
            ConversationScope.Personal,
            title,
            null,
            participantUserIds);

    private static async Task<ManagedConversationResult> CreateManagedAndReadAsync(
        HttpClient client,
        string resource,
        CreateManagedConversationCommand command)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/v1/conversations/{resource}",
            command,
            JsonOptions);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ManagedConversationResult>(JsonOptions))!;
    }
}
