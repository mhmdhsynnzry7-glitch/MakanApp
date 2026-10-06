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
    public async Task SearchReturnsOnlyAuthorizedCurrentContentAcrossConversations()
    {
        const string token = "privacy-token-7e";
        using var actorClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();
        using var thirdClient = factory.CreateClient();
        var actor = await CreateMessagingUserAsync(actorClient);
        var second = await CreateMessagingUserAsync(secondClient);
        var third = await CreateMessagingUserAsync(thirdClient);
        await factory.CreatePersonalCommunicationGrantAsync(actor.UserId, second.UserId);
        await factory.CreatePersonalCommunicationGrantAsync(actor.UserId, third.UserId);
        await factory.CreatePersonalCommunicationGrantAsync(second.UserId, third.UserId);
        var firstAccessible = await StartDirectAndReadAsync(
            actorClient,
            second.UserId,
            ConversationScope.Personal);
        var secondAccessible = await StartDirectAndReadAsync(
            actorClient,
            third.UserId,
            ConversationScope.Personal);
        var inaccessible = await StartDirectAndReadAsync(
            secondClient,
            third.UserId,
            ConversationScope.Personal);
        await SendMessageAndReadAsync(
            secondClient,
            firstAccessible.ConversationId,
            Guid.NewGuid(),
            $"A {token}");
        await SendMessageAndReadAsync(
            thirdClient,
            secondAccessible.ConversationId,
            Guid.NewGuid(),
            $"B {token}");
        await SendMessageAndReadAsync(
            secondClient,
            inaccessible.ConversationId,
            Guid.NewGuid(),
            $"Hidden {token}");

        var page = await SearchAsync(actorClient, token);
        var scoped = await SearchAsync(actorClient, token, firstAccessible.ConversationId);
        var hiddenScope = await SearchAsync(actorClient, token, inaccessible.ConversationId);

        Assert.Equal(2, page.TotalCount);
        Assert.Equal(2, page.Items.Count);
        Assert.Contains(page.Items, item => item.ConversationId == firstAccessible.ConversationId);
        Assert.Contains(page.Items, item => item.ConversationId == secondAccessible.ConversationId);
        Assert.DoesNotContain(page.Items, item => item.ConversationId == inaccessible.ConversationId);
        Assert.Equal(1, scoped.TotalCount);
        Assert.Single(scoped.Items);
        Assert.Equal(0, hiddenScope.TotalCount);
        Assert.Empty(hiddenScope.Items);
    }

    [Fact]
    public async Task SearchUsesCurrentEditExcludesDeletedRevisionAndPaginatesWithOpaqueCursor()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        var edited = await SendMessageAndReadAsync(
            setup.FirstClient,
            setup.ConversationId,
            Guid.NewGuid(),
            "old-search-7e");
        using var editResponse = await setup.FirstClient.PatchAsJsonAsync(
            $"/api/v1/conversations/{setup.ConversationId}/messages/{edited.MessageId}",
            new EditMessageCommand("current-search-7e", edited.Version),
            JsonOptions);
        editResponse.EnsureSuccessStatusCode();
        var currentVersion = await editResponse.Content.ReadFromJsonAsync<MessageMutationResult>(JsonOptions);

        Assert.Empty((await SearchAsync(setup.SecondClient, "old-search-7e")).Items);
        Assert.Single((await SearchAsync(setup.SecondClient, "current-search-7e")).Items);

        using var deleteResponse = await setup.FirstClient.DeleteAsync(
            $"/api/v1/conversations/{setup.ConversationId}/messages/{edited.MessageId}?expectedVersion={Uri.EscapeDataString(currentVersion!.Version)}");
        deleteResponse.EnsureSuccessStatusCode();
        Assert.Empty((await SearchAsync(setup.SecondClient, "current-search-7e")).Items);

        for (var index = 0; index < 3; index++)
        {
            await SendMessageAndReadAsync(
                setup.FirstClient,
                setup.ConversationId,
                Guid.NewGuid(),
                $"page-search-7e {index}");
        }

        var firstPage = await SearchAsync(setup.SecondClient, "page-search-7e", limit: 2);
        var secondPage = await SearchAsync(
            setup.SecondClient,
            "page-search-7e",
            cursor: firstPage.NextCursor,
            limit: 2);
        Assert.Equal(3, firstPage.TotalCount);
        Assert.Equal(2, firstPage.Items.Count);
        Assert.NotNull(firstPage.NextCursor);
        Assert.Single(secondPage.Items);
        Assert.Empty(firstPage.Items.Select(item => item.MessageId)
            .Intersect(secondPage.Items.Select(item => item.MessageId)));
    }

    [Fact]
    public async Task FileNameSearchRequiresCurrentMessageAndConversationAuthorization()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        using var outsiderClient = factory.CreateClient();
        _ = await CreateMessagingUserAsync(outsiderClient);
        var file = await UploadFileAsync(
            setup.FirstClient,
            "application/pdf",
            "گزارش-خصوصی-هفتم.pdf");
        var sent = await SendAdvancedMessageAsync(
            setup.FirstClient,
            setup.ConversationId,
            new SendMessageCommand
            {
                ClientMessageId = Guid.NewGuid(),
                Kind = MessageKind.File,
                AttachmentIds = [file.Id]
            });

        var authorized = await SearchAsync(setup.SecondClient, "گزارش-خصوصی-هفتم");
        var unauthorized = await SearchAsync(outsiderClient, "گزارش-خصوصی-هفتم");
        var item = Assert.Single(authorized.Items);

        Assert.Equal(sent.MessageId, item.MessageId);
        Assert.Equal(file.Id, Assert.Single(item.Attachments).FileAssetId);
        Assert.Equal(0, unauthorized.TotalCount);
        Assert.Empty(unauthorized.Items);
    }

    [Fact]
    public async Task RevokedParticipantCannotResearchOrOpenCachedSearchResult()
    {
        using var ownerClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();
        var owner = await CreateMessagingUserAsync(ownerClient);
        var member = await CreateMessagingUserAsync(memberClient);
        await factory.CreatePersonalCommunicationGrantAsync(owner.UserId, member.UserId);
        var group = await CreateManagedAndReadAsync(
            ownerClient,
            "groups",
            PersonalCommand("جستجوی قابل لغو", member.UserId));
        var sent = await SendMessageAndReadAsync(
            ownerClient,
            group.ConversationId,
            Guid.NewGuid(),
            "revoked-search-7e");
        Assert.Equal(sent.MessageId, Assert.Single(
            (await SearchAsync(memberClient, "revoked-search-7e")).Items).MessageId);

        await factory.RemoveConversationParticipantAsync(group.ConversationId, member.UserId);
        var afterRevocation = await SearchAsync(memberClient, "revoked-search-7e");
        using var openCached = await memberClient.GetAsync(
            $"/api/v1/conversations/{group.ConversationId}/messages");

        Assert.Equal(0, afterRevocation.TotalCount);
        Assert.Empty(afterRevocation.Items);
        Assert.Equal(HttpStatusCode.NotFound, openCached.StatusCode);
    }

    [Fact]
    public async Task BlockIsDirectionalIdempotentAndReauthorizesOfflineDirectSend()
    {
        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();
        var first = await CreateMessagingUserAsync(firstClient);
        var second = await CreateMessagingUserAsync(secondClient);
        await factory.CreatePersonalCommunicationGrantAsync(first.UserId, second.UserId);
        var conversation = await StartDirectAndReadAsync(
            firstClient,
            second.UserId,
            ConversationScope.Personal);
        await SendMessageAndReadAsync(
            firstClient,
            conversation.ConversationId,
            Guid.NewGuid(),
            "تاریخچه باقی می‌ماند");
        var queuedClientMessageId = Guid.NewGuid();

        using var created = await secondClient.PostAsync(
            $"/api/v1/messaging/blocks/{first.UserId}",
            null);
        using var duplicate = await secondClient.PostAsync(
            $"/api/v1/messaging/blocks/{first.UserId}",
            null);
        using var startBlocked = await StartDirectAsync(
            firstClient,
            second.UserId,
            ConversationScope.Personal);
        using var offlineReplay = await SendMessageAsync(
            firstClient,
            conversation.ConversationId,
            queuedClientMessageId,
            "پیام صف آفلاین");
        using var reverseSend = await SendMessageAsync(
            secondClient,
            conversation.ConversationId,
            Guid.NewGuid(),
            "جهت معکوس نیز متوقف است");
        using var history = await firstClient.GetAsync(
            $"/api/v1/conversations/{conversation.ConversationId}/messages");

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        Assert.Equal(1, await factory.CountActiveUserBlocksAsync(second.UserId, first.UserId));
        Assert.Equal(HttpStatusCode.NotFound, startBlocked.StatusCode);
        Assert.Equal(MessagingErrorCodes.DirectRecipientNotAvailable, await ReadProblemCodeAsync(startBlocked));
        Assert.Equal(HttpStatusCode.Forbidden, offlineReplay.StatusCode);
        Assert.Equal(MessagingErrorCodes.MessageNotAllowed, await ReadProblemCodeAsync(offlineReplay));
        Assert.Equal(HttpStatusCode.Forbidden, reverseSend.StatusCode);
        Assert.Equal(HttpStatusCode.OK, history.StatusCode);

        using var unblock = await secondClient.DeleteAsync($"/api/v1/messaging/blocks/{first.UserId}");
        using var duplicateUnblock = await secondClient.DeleteAsync($"/api/v1/messaging/blocks/{first.UserId}");
        var restored = await SendMessageAndReadAsync(
            firstClient,
            conversation.ConversationId,
            queuedClientMessageId,
            "پیام صف آفلاین");
        Assert.Equal(HttpStatusCode.NoContent, unblock.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, duplicateUnblock.StatusCode);
        Assert.Equal(0, await factory.CountActiveUserBlocksAsync(second.UserId, first.UserId));
        Assert.Equal(1, await factory.CountUserBlockHistoryAsync(second.UserId, first.UserId));
        Assert.Equal(2, restored.Sequence);

        using var selfBlock = await firstClient.PostAsync($"/api/v1/messaging/blocks/{first.UserId}", null);
        Assert.Equal(HttpStatusCode.Forbidden, selfBlock.StatusCode);
        Assert.Equal(MessagingErrorCodes.UserBlockNotAllowed, await ReadProblemCodeAsync(selfBlock));
    }

    [Fact]
    public async Task BlockDoesNotChangeEnrollmentOrSharedConversationMembership()
    {
        using var ownerClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();
        var owner = await CreateMessagingUserAsync(ownerClient);
        var member = await CreateMessagingUserAsync(memberClient);
        await factory.CreatePersonalCommunicationGrantAsync(owner.UserId, member.UserId);
        var group = await CreateManagedAndReadAsync(
            ownerClient,
            "groups",
            PersonalCommand("گروه مشترک", member.UserId));
        var organizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        await factory.CreateMembershipAsync(owner.UserId, organizationId, OrganizationRole.Manager);
        await factory.CreateMembershipAsync(member.UserId, organizationId, OrganizationRole.Student);
        var personId = await factory.GetUserPersonIdAsync(member.UserId);
        var organizationPersonId = await factory.CreateOrganizationPersonAsync(organizationId, personId);
        var academicClass = await factory.CreateAcademicClassAsync(organizationId);
        await factory.CreateEnrollmentAsync(
            organizationId,
            academicClass.ClassId,
            organizationPersonId);
        var systemConversationId = await factory.CreateSystemManagedConversationAsync(
            organizationId,
            member.UserId,
            ConversationType.Group);

        using var block = await ownerClient.PostAsync($"/api/v1/messaging/blocks/{member.UserId}", null);
        block.EnsureSuccessStatusCode();
        var groupMessage = await SendMessageAndReadAsync(
            memberClient,
            group.ConversationId,
            Guid.NewGuid(),
            "Block روی گروه اعمال نمی‌شود");

        Assert.Equal(1, await factory.CountActiveEnrollmentsAsync(organizationId, academicClass.ClassId));
        Assert.Contains(
            await factory.GetConversationParticipantsAsync(systemConversationId),
            participant => participant.UserId == member.UserId &&
                           participant.Status == ConversationParticipantStatus.Active);
        Assert.Contains(
            await factory.GetConversationParticipantsAsync(group.ConversationId),
            participant => participant.UserId == member.UserId &&
                           participant.Status == ConversationParticipantStatus.Active);
        Assert.Equal(1, groupMessage.Sequence);
    }

    [Fact]
    public async Task ConcurrentBlockAndReportRetriesRemainIdempotent()
    {
        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();
        var first = await CreateMessagingUserAsync(firstClient);
        var second = await CreateMessagingUserAsync(secondClient);
        await factory.CreatePersonalCommunicationGrantAsync(first.UserId, second.UserId);
        var conversation = await StartDirectAndReadAsync(
            firstClient,
            second.UserId,
            ConversationScope.Personal);
        var message = await SendMessageAndReadAsync(
            firstClient,
            conversation.ConversationId,
            Guid.NewGuid(),
            "گزارش هم‌زمان");
        var reportCommand = new ReportMessageCommand(
            Guid.NewGuid(),
            AbuseReportReason.Spam,
            null);

        var reportClients = Enumerable.Range(0, 4).Select(_ => factory.CreateClient()).ToArray();
        foreach (var client in reportClients)
        {
            UseBearerToken(client, second.AccessToken);
        }

        try
        {
            var responses = await Task.WhenAll(reportClients.Select(client => client.PostAsJsonAsync(
                $"/api/v1/conversations/{conversation.ConversationId}/messages/{message.MessageId}/reports",
                reportCommand,
                JsonOptions)));
            try
            {
                Assert.All(responses, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
                var receipts = await Task.WhenAll(responses.Select(response =>
                    response.Content.ReadFromJsonAsync<AbuseReportReceiptResult>(JsonOptions)));
                Assert.Single(receipts.Select(receipt => receipt!.ReportId).Distinct());
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
            foreach (var client in reportClients)
            {
                client.Dispose();
            }
        }

        var blockClients = Enumerable.Range(0, 4).Select(_ => factory.CreateClient()).ToArray();
        foreach (var client in blockClients)
        {
            UseBearerToken(client, second.AccessToken);
        }

        try
        {
            var responses = await Task.WhenAll(blockClients.Select(client =>
                client.PostAsync($"/api/v1/messaging/blocks/{first.UserId}", null)));
            try
            {
                Assert.All(responses, response => Assert.True(response.IsSuccessStatusCode));
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
            foreach (var client in blockClients)
            {
                client.Dispose();
            }
        }

        Assert.Equal(1, await factory.CountAbuseReportsAsync(second.UserId, reportCommand.ClientReportId));
        Assert.Equal(1, await factory.CountActiveUserBlocksAsync(second.UserId, first.UserId));
    }

    [Fact]
    public async Task ReportIsIdempotentMinimalEvidenceAndSurvivesMessageMutation()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        await SendMessageAndReadAsync(
            setup.FirstClient,
            setup.ConversationId,
            Guid.NewGuid(),
            "private-neighbor-7e");
        var selected = await SendMessageAndReadAsync(
            setup.FirstClient,
            setup.ConversationId,
            Guid.NewGuid(),
            "selected-evidence-7e");
        var clientReportId = Guid.NewGuid();
        var command = new ReportMessageCommand(
            clientReportId,
            AbuseReportReason.Harassment,
            "لطفاً بررسی شود");

        using var createdResponse = await setup.SecondClient.PostAsJsonAsync(
            $"/api/v1/conversations/{setup.ConversationId}/messages/{selected.MessageId}/reports",
            command,
            JsonOptions);
        var createdJson = await createdResponse.Content.ReadAsStringAsync();
        var created = System.Text.Json.JsonSerializer.Deserialize<AbuseReportReceiptResult>(
            createdJson,
            JsonOptions)!;
        using var retriedResponse = await setup.SecondClient.PostAsJsonAsync(
            $"/api/v1/conversations/{setup.ConversationId}/messages/{selected.MessageId}/reports",
            command,
            JsonOptions);
        var retried = await retriedResponse.Content.ReadFromJsonAsync<AbuseReportReceiptResult>(JsonOptions);
        using var conflict = await setup.SecondClient.PostAsJsonAsync(
            $"/api/v1/conversations/{setup.ConversationId}/messages/{selected.MessageId}/reports",
            command with { Reason = AbuseReportReason.Spam },
            JsonOptions);

        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        Assert.Equal(AbuseReportStatus.Submitted, created.Status);
        Assert.DoesNotContain("violation", createdJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("evidence", createdJson, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(created.ReportId, retried!.ReportId);
        Assert.Equal(1, await factory.CountAbuseReportsAsync(setup.Second.UserId, clientReportId));
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal(MessagingErrorCodes.ReportIdempotencyConflict, await ReadProblemCodeAsync(conflict));

        using var edit = await setup.FirstClient.PatchAsJsonAsync(
            $"/api/v1/conversations/{setup.ConversationId}/messages/{selected.MessageId}",
            new EditMessageCommand("edited-after-report-7e", selected.Version),
            JsonOptions);
        edit.EnsureSuccessStatusCode();
        var edited = await edit.Content.ReadFromJsonAsync<MessageMutationResult>(JsonOptions);
        using var delete = await setup.FirstClient.DeleteAsync(
            $"/api/v1/conversations/{setup.ConversationId}/messages/{selected.MessageId}?expectedVersion={Uri.EscapeDataString(edited!.Version)}");
        delete.EnsureSuccessStatusCode();
        var evidence = await factory.GetAbuseReportEvidenceAsync(created.ReportId);

        Assert.Equal("selected-evidence-7e", evidence.ContentSnapshot);
        Assert.DoesNotContain("private-neighbor-7e", evidence.ContentSnapshot ?? string.Empty);
        Assert.Equal(selected.MessageId, evidence.MessageId);
        Assert.Equal(8, evidence.MessageVersion.Length);
        Assert.Equal(AbuseReportStatus.Submitted, evidence.Status);
    }

    [Fact]
    public async Task ReportEvidenceIsNotAvailableToOutsiderManagerGroupOwnerOrPublicModerationRoute()
    {
        using var ownerClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();
        using var outsiderClient = factory.CreateClient();
        var owner = await CreateMessagingUserAsync(ownerClient);
        var member = await CreateMessagingUserAsync(memberClient);
        var outsider = await CreateMessagingUserAsync(outsiderClient);
        await factory.CreatePersonalCommunicationGrantAsync(owner.UserId, member.UserId);
        var group = await CreateManagedAndReadAsync(
            ownerClient,
            "groups",
            PersonalCommand("گروه گزارش", member.UserId));
        var selected = await SendMessageAndReadAsync(
            ownerClient,
            group.ConversationId,
            Guid.NewGuid(),
            "پیام قابل گزارش");
        var reportCommand = new ReportMessageCommand(
            Guid.NewGuid(),
            AbuseReportReason.Other,
            null);
        using var reportResponse = await memberClient.PostAsJsonAsync(
            $"/api/v1/conversations/{group.ConversationId}/messages/{selected.MessageId}/reports",
            reportCommand,
            JsonOptions);
        var report = await reportResponse.Content.ReadFromJsonAsync<AbuseReportReceiptResult>(JsonOptions);

        var organizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var managerMembership = await factory.CreateMembershipAsync(
            outsider.UserId,
            organizationId,
            OrganizationRole.Manager);
        await SelectOrganizationAsync(
            outsiderClient,
            managerMembership.MembershipId,
            OrganizationRole.Manager);
        using var guessedReport = await outsiderClient.PostAsJsonAsync(
            $"/api/v1/conversations/{group.ConversationId}/messages/{selected.MessageId}/reports",
            new ReportMessageCommand(Guid.NewGuid(), AbuseReportReason.Spam, null),
            JsonOptions);
        using var managerRead = await outsiderClient.GetAsync(
            $"/api/v1/messaging/reports/{report!.ReportId}");
        using var ownerRead = await ownerClient.GetAsync(
            $"/api/v1/messaging/reports/{report.ReportId}");
        using var moderationRoute = await ownerClient.GetAsync(
            $"/api/v1/moderation/reports/{report.ReportId}");
        using var reporterRead = await memberClient.GetAsync(
            $"/api/v1/messaging/reports/{report.ReportId}");

        Assert.Equal(HttpStatusCode.NotFound, guessedReport.StatusCode);
        Assert.Equal(MessagingErrorCodes.ReportMessageNotFound, await ReadProblemCodeAsync(guessedReport));
        Assert.Equal(HttpStatusCode.NotFound, managerRead.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, ownerRead.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, moderationRoute.StatusCode);
        Assert.Equal(HttpStatusCode.OK, reporterRead.StatusCode);
    }

    [Fact]
    public async Task AnonymousSearchBlockAndReportAreRejected()
    {
        using var anonymous = factory.CreateClient();

        using var search = await anonymous.GetAsync("/api/v1/messages/search?q=test");
        using var block = await anonymous.PostAsync($"/api/v1/messaging/blocks/{Guid.NewGuid()}", null);
        using var report = await anonymous.PostAsJsonAsync(
            $"/api/v1/conversations/{Guid.NewGuid()}/messages/{Guid.NewGuid()}/reports",
            new ReportMessageCommand(Guid.NewGuid(), AbuseReportReason.Other, null),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, search.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, block.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, report.StatusCode);
    }

    private static async Task<MessageSearchPageResult> SearchAsync(
        HttpClient client,
        string query,
        Guid? conversationId = null,
        string? cursor = null,
        int? limit = null)
    {
        var parameters = new List<string> { $"q={Uri.EscapeDataString(query)}" };
        if (conversationId.HasValue)
        {
            parameters.Add($"conversationId={conversationId.Value}");
        }

        if (cursor is not null)
        {
            parameters.Add($"cursor={Uri.EscapeDataString(cursor)}");
        }

        if (limit.HasValue)
        {
            parameters.Add($"limit={limit.Value}");
        }

        return (await client.GetFromJsonAsync<MessageSearchPageResult>(
            $"/api/v1/messages/search?{string.Join('&', parameters)}",
            JsonOptions))!;
    }
}
