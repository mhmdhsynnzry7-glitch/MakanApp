using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Application.Storage;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Storage;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class MessagingEndpointsTests
{
    [Fact]
    public async Task ReactionIsUniqueChangeableRemovableAndRemovedParticipantCannotReact()
    {
        using var ownerClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();
        var owner = await CreateMessagingUserAsync(ownerClient);
        var member = await CreateMessagingUserAsync(memberClient);
        await factory.CreatePersonalCommunicationGrantAsync(owner.UserId, member.UserId);
        var group = await CreateManagedAndReadAsync(
            ownerClient,
            "groups",
            PersonalCommand("reaction group", member.UserId));
        var sent = await SendAdvancedMessageAsync(
            ownerClient,
            group.ConversationId,
            new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = "react" });

        using var first = await memberClient.PostAsJsonAsync(
            $"/api/v1/conversations/{group.ConversationId}/messages/{sent.MessageId}/reactions",
            new AddReactionCommand(MessageReactionType.Like),
            JsonOptions);
        using var duplicate = await memberClient.PostAsJsonAsync(
            $"/api/v1/conversations/{group.ConversationId}/messages/{sent.MessageId}/reactions",
            new AddReactionCommand(MessageReactionType.Like),
            JsonOptions);
        using var changed = await memberClient.PostAsJsonAsync(
            $"/api/v1/conversations/{group.ConversationId}/messages/{sent.MessageId}/reactions",
            new AddReactionCommand(MessageReactionType.Love),
            JsonOptions);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        Assert.Equal(1, await factory.CountActiveMessageReactionsAsync(sent.MessageId, member.UserId));

        using var removed = await memberClient.DeleteAsync(
            $"/api/v1/conversations/{group.ConversationId}/messages/{sent.MessageId}/reactions/Love");
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.Equal(0, await factory.CountActiveMessageReactionsAsync(sent.MessageId, member.UserId));

        using var removeMember = await ownerClient.DeleteAsync(
            $"/api/v1/conversations/{group.ConversationId}/members/{member.UserId}");
        removeMember.EnsureSuccessStatusCode();
        using var blocked = await memberClient.PostAsJsonAsync(
            $"/api/v1/conversations/{group.ConversationId}/messages/{sent.MessageId}/reactions",
            new AddReactionCommand(MessageReactionType.Wow),
            JsonOptions);
        Assert.Equal(HttpStatusCode.NotFound, blocked.StatusCode);
    }

    [Fact]
    public async Task MentionRequiresCurrentVisibleParticipant()
    {
        using var ownerClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();
        using var externalClient = factory.CreateClient();
        var owner = await CreateMessagingUserAsync(ownerClient);
        var member = await CreateMessagingUserAsync(memberClient);
        var external = await CreateMessagingUserAsync(externalClient);
        await factory.CreatePersonalCommunicationGrantAsync(owner.UserId, member.UserId);
        var group = await CreateManagedAndReadAsync(
            ownerClient,
            "groups",
            PersonalCommand("mention group", member.UserId));

        var valid = await SendAdvancedMessageAsync(
            ownerClient,
            group.ConversationId,
            new SendMessageCommand
            {
                ClientMessageId = Guid.NewGuid(),
                Text = "hello member",
                MentionedUserIds = [member.UserId]
            });
        var history = await ownerClient.GetFromJsonAsync<ConversationMessagePageResult>(
            $"/api/v1/conversations/{group.ConversationId}/messages",
            JsonOptions);
        using var invalid = await ownerClient.PostAsJsonAsync(
            $"/api/v1/conversations/{group.ConversationId}/messages",
            new SendMessageCommand
            {
                ClientMessageId = Guid.NewGuid(),
                Text = "hello outsider",
                MentionedUserIds = [external.UserId]
            },
            JsonOptions);

        Assert.Equal(member.UserId, Assert.Single(
            history!.Messages.Single(message => message.MessageId == valid.MessageId).Mentions).UserId);
        Assert.Equal(HttpStatusCode.Forbidden, invalid.StatusCode);
        Assert.Equal(MessagingErrorCodes.MentionNotAllowed, await ReadProblemCodeAsync(invalid));
    }

    [Fact]
    public async Task GroupPinRequiresOwnerOrAdminAndPinnedDeletedMessageRemainsIdentifiable()
    {
        using var ownerClient = factory.CreateClient();
        using var memberClient = factory.CreateClient();
        var owner = await CreateMessagingUserAsync(ownerClient);
        var member = await CreateMessagingUserAsync(memberClient);
        await factory.CreatePersonalCommunicationGrantAsync(owner.UserId, member.UserId);
        var group = await CreateManagedAndReadAsync(
            ownerClient,
            "groups",
            PersonalCommand("pin group", member.UserId));
        var sent = await SendAdvancedMessageAsync(
            ownerClient,
            group.ConversationId,
            new SendMessageCommand { ClientMessageId = Guid.NewGuid(), Text = "pin me" });

        using var denied = await memberClient.PostAsync(
            $"/api/v1/conversations/{group.ConversationId}/pins/{sent.MessageId}",
            null);
        using var pinned = await ownerClient.PostAsync(
            $"/api/v1/conversations/{group.ConversationId}/pins/{sent.MessageId}",
            null);
        using var duplicate = await ownerClient.PostAsync(
            $"/api/v1/conversations/{group.ConversationId}/pins/{sent.MessageId}",
            null);

        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(HttpStatusCode.OK, pinned.StatusCode);
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        Assert.Equal(1, await factory.CountActiveConversationPinsAsync(group.ConversationId, sent.MessageId));

        using var deleted = await ownerClient.DeleteAsync(
            $"/api/v1/conversations/{group.ConversationId}/messages/{sent.MessageId}?expectedVersion={Uri.EscapeDataString(sent.Version)}");
        deleted.EnsureSuccessStatusCode();
        var history = await ownerClient.GetFromJsonAsync<ConversationMessagePageResult>(
            $"/api/v1/conversations/{group.ConversationId}/messages",
            JsonOptions);
        Assert.True(Assert.Single(history!.Messages).IsPinned);

        using var unpinned = await ownerClient.DeleteAsync(
            $"/api/v1/conversations/{group.ConversationId}/pins/{sent.MessageId}");
        Assert.Equal(HttpStatusCode.OK, unpinned.StatusCode);
        Assert.Equal(0, await factory.CountActiveConversationPinsAsync(group.ConversationId, sent.MessageId));
    }

    [Fact]
    public async Task ReadyImageAndVoiceAreAuthorizedRetainedMediaWithoutStoragePathDisclosure()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        var image = await UploadFileAsync(setup.FirstClient, "image/png", "photo.png");
        var imageMessage = await SendAdvancedMessageAsync(
            setup.FirstClient,
            setup.ConversationId,
            new SendMessageCommand
            {
                ClientMessageId = Guid.NewGuid(),
                Kind = MessageKind.Image,
                Text = "caption",
                AttachmentIds = [image.Id]
            });
        var voice = await UploadFileAsync(setup.FirstClient, "audio/mpeg", "voice.mp3");
        var voiceMessage = await SendAdvancedMessageAsync(
            setup.FirstClient,
            setup.ConversationId,
            new SendMessageCommand
            {
                ClientMessageId = Guid.NewGuid(),
                Kind = MessageKind.Voice,
                AttachmentIds = [voice.Id]
            });

        using var download = await setup.SecondClient.GetAsync($"/api/v1/files/{image.Id}/content");
        using var mediaResponse = await setup.SecondClient.GetAsync(
            $"/api/v1/conversations/{setup.ConversationId}/media");
        var mediaJson = await mediaResponse.Content.ReadAsStringAsync();
        var media = System.Text.Json.JsonSerializer.Deserialize<ConversationMediaPageResult>(mediaJson, JsonOptions)!;
        var voiceOnly = await setup.SecondClient.GetFromJsonAsync<ConversationMediaPageResult>(
            $"/api/v1/conversations/{setup.ConversationId}/media?kind=Voice",
            JsonOptions);

        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(HttpStatusCode.OK, mediaResponse.StatusCode);
        Assert.Contains(media.Items, item => item.MessageId == imageMessage.MessageId);
        Assert.Contains(media.Items, item => item.MessageId == voiceMessage.MessageId);
        Assert.Equal(voiceMessage.MessageId, Assert.Single(voiceOnly!.Items).MessageId);
        Assert.DoesNotContain("storageKey", mediaJson, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await factory.CountMessageAttachmentsAsync(imageMessage.MessageId));
        Assert.True(await factory.IsFileAssetRetainedAsync(image.Id));
    }

    [Theory]
    [InlineData(FileAssetStatus.Pending)]
    [InlineData(FileAssetStatus.Rejected)]
    [InlineData(FileAssetStatus.Deleted)]
    public async Task NonReadyFileCannotBeSent(FileAssetStatus status)
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        var file = await UploadFileAsync(setup.FirstClient, "image/png", $"{status}.png");
        await factory.SetFileAssetStatusAsync(file.Id, status);

        using var response = await setup.FirstClient.PostAsJsonAsync(
            $"/api/v1/conversations/{setup.ConversationId}/messages",
            new SendMessageCommand
            {
                ClientMessageId = Guid.NewGuid(),
                Kind = MessageKind.Image,
                AttachmentIds = [file.Id]
            },
            JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(MessagingErrorCodes.MessageAttachmentNotReady, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task AttachmentContentTypeMustMatchMessageKind()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        var file = await UploadFileAsync(setup.FirstClient, "application/pdf", "document.pdf");

        using var response = await setup.FirstClient.PostAsJsonAsync(
            $"/api/v1/conversations/{setup.ConversationId}/messages",
            new SendMessageCommand
            {
                ClientMessageId = Guid.NewGuid(),
                Kind = MessageKind.Image,
                AttachmentIds = [file.Id]
            },
            JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(MessagingErrorCodes.MessageKindInvalid, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task AnotherUsersFileCannotBeAttachedByKnowingItsId()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        var file = await UploadFileAsync(setup.SecondClient, "image/png", "other.png");

        using var response = await setup.FirstClient.PostAsJsonAsync(
            $"/api/v1/conversations/{setup.ConversationId}/messages",
            new SendMessageCommand
            {
                ClientMessageId = Guid.NewGuid(),
                Kind = MessageKind.Image,
                AttachmentIds = [file.Id]
            },
            JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(MessagingErrorCodes.MessageAttachmentNotAllowed, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task CrossOrganizationFileAttachAndCrossScopeForwardAreRejected()
    {
        using var setup = await CreateAdvancedConversationSetupAsync();
        var organizationA = await factory.CreateOrganizationAsync($"org-a-{Guid.NewGuid():N}");
        var organizationB = await factory.CreateOrganizationAsync($"org-b-{Guid.NewGuid():N}");
        var firstA = await factory.CreateMembershipAsync(
            setup.First.UserId,
            organizationA,
            OrganizationRole.Manager);
        await factory.CreateMembershipAsync(setup.Second.UserId, organizationA, OrganizationRole.Teacher);
        var firstB = await factory.CreateMembershipAsync(
            setup.First.UserId,
            organizationB,
            OrganizationRole.Manager);
        await factory.CreateMembershipAsync(setup.Second.UserId, organizationB, OrganizationRole.Teacher);

        await SelectOrganizationAsync(setup.FirstClient, firstB.MembershipId, OrganizationRole.Manager);
        var conversationB = await StartDirectAndReadAsync(
            setup.FirstClient,
            setup.Second.UserId,
            ConversationScope.Organization);
        var fileB = await UploadFileAsync(setup.FirstClient, "application/pdf", "org-b.pdf");
        var sourceB = await SendAdvancedMessageAsync(
            setup.FirstClient,
            conversationB.ConversationId,
            new SendMessageCommand
            {
                ClientMessageId = Guid.NewGuid(),
                Kind = MessageKind.File,
                AttachmentIds = [fileB.Id]
            });

        await SelectOrganizationAsync(setup.FirstClient, firstA.MembershipId, OrganizationRole.Manager);
        var conversationA = await StartDirectAndReadAsync(
            setup.FirstClient,
            setup.Second.UserId,
            ConversationScope.Organization);
        using var attach = await setup.FirstClient.PostAsJsonAsync(
            $"/api/v1/conversations/{conversationA.ConversationId}/messages",
            new SendMessageCommand
            {
                ClientMessageId = Guid.NewGuid(),
                Kind = MessageKind.File,
                AttachmentIds = [fileB.Id]
            },
            JsonOptions);
        using var forward = await setup.FirstClient.PostAsJsonAsync(
            $"/api/v1/conversations/{conversationB.ConversationId}/messages/{sourceB.MessageId}/forward",
            new ForwardMessageCommand(conversationA.ConversationId, Guid.NewGuid()),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, attach.StatusCode);
        Assert.Equal(MessagingErrorCodes.MessageAttachmentNotAllowed, await ReadProblemCodeAsync(attach));
        Assert.Equal(HttpStatusCode.NotFound, forward.StatusCode);
        Assert.Empty(await factory.GetMessagesAsync(conversationA.ConversationId));
    }

    private static async Task<FileAssetResult> UploadFileAsync(
        HttpClient client,
        string contentType,
        string fileName)
    {
        using var form = new MultipartFormDataContent();
        using var content = new ByteArrayContent([1, 2, 3, 4]);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(content, "File", fileName);
        using var response = await client.PostAsync("/api/v1/files", form);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<FileAssetResult>(JsonOptions))!;
    }
}
