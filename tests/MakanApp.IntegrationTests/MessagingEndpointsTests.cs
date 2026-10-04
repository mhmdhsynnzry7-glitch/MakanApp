using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MakanApp.Application.Identity;
using MakanApp.Application.Messaging;
using MakanApp.Application.Organization;
using MakanApp.Domain.Identity;
using MakanApp.Domain.Messaging;
using MakanApp.Domain.Organization;
using Xunit;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class MessagingEndpointsTests(MakanAppWebApplicationFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static int _phoneSequence = 81_000_000;

    [Fact]
    public async Task EligibleAdultsCreateOneCanonicalPersonalConversationWithoutPhoneDisclosure()
    {
        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();
        var first = await CreateMessagingUserAsync(firstClient);
        var second = await CreateMessagingUserAsync(secondClient);
        await factory.CreatePersonalCommunicationGrantAsync(first.UserId, second.UserId);

        using var firstResponse = await StartDirectAsync(
            firstClient,
            second.UserId,
            ConversationScope.Personal);
        var firstJson = await firstResponse.Content.ReadAsStringAsync();
        var created = JsonSerializer.Deserialize<DirectConversationResult>(firstJson, JsonOptions)!;
        using var reverseResponse = await StartDirectAsync(
            secondClient,
            first.UserId,
            ConversationScope.Personal);
        var existing = await reverseResponse.Content.ReadFromJsonAsync<DirectConversationResult>(JsonOptions);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, reverseResponse.StatusCode);
        Assert.False(created.AlreadyExisted);
        Assert.True(existing!.AlreadyExisted);
        Assert.Equal(created.ConversationId, existing.ConversationId);
        Assert.DoesNotContain("phone", firstJson, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            new[] { first.UserId, second.UserId }.OrderBy(id => id),
            (await factory.GetConversationParticipantUserIdsAsync(created.ConversationId)).OrderBy(id => id));
        Assert.Equal(
            1,
            await factory.CountDirectConversationsAsync(
                first.UserId,
                second.UserId,
                ConversationScope.Personal,
                null));
    }

    [Fact]
    public async Task PersonalConversationRequiresGrantAndAdultParticipantsUsingGenericFailure()
    {
        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();
        var first = await CreateMessagingUserAsync(firstClient);
        var second = await CreateMessagingUserAsync(secondClient);

        using var missingGrant = await StartDirectAsync(
            firstClient,
            second.UserId,
            ConversationScope.Personal);
        Assert.Equal(HttpStatusCode.NotFound, missingGrant.StatusCode);
        Assert.Equal(
            MessagingErrorCodes.DirectRecipientNotAvailable,
            await ReadProblemCodeAsync(missingGrant));

        await factory.CreatePersonalCommunicationGrantAsync(first.UserId, second.UserId);
        await factory.SetCommunicationAgeCategoryAsync(
            second.UserId,
            CommunicationAgeCategory.Minor);
        using var minor = await StartDirectAsync(
            firstClient,
            second.UserId,
            ConversationScope.Personal);

        Assert.Equal(HttpStatusCode.NotFound, minor.StatusCode);
        Assert.Equal(
            MessagingErrorCodes.DirectRecipientNotAvailable,
            await ReadProblemCodeAsync(minor));
    }

    [Fact]
    public async Task SamePairCanHaveDistinctPersonalAndOrganizationConversations()
    {
        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();
        var first = await CreateMessagingUserAsync(firstClient);
        var second = await CreateMessagingUserAsync(secondClient);
        await factory.CreatePersonalCommunicationGrantAsync(first.UserId, second.UserId);
        var organizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var manager = await factory.CreateMembershipAsync(
            first.UserId,
            organizationId,
            OrganizationRole.Manager);
        await factory.CreateMembershipAsync(second.UserId, organizationId, OrganizationRole.Teacher);

        var personal = await StartDirectAndReadAsync(
            firstClient,
            second.UserId,
            ConversationScope.Personal);
        await SelectOrganizationAsync(
            firstClient,
            manager.MembershipId,
            OrganizationRole.Manager);
        var organization = await StartDirectAndReadAsync(
            firstClient,
            second.UserId,
            ConversationScope.Organization);
        var organizationList = await firstClient.GetFromJsonAsync<ConversationSummaryResult[]>(
            "/api/v1/conversations",
            JsonOptions);
        await SelectPersonalAsync(firstClient);
        var personalList = await firstClient.GetFromJsonAsync<ConversationSummaryResult[]>(
            "/api/v1/conversations",
            JsonOptions);

        Assert.NotEqual(personal.ConversationId, organization.ConversationId);
        Assert.Null(personal.OrganizationId);
        Assert.Equal(organizationId, organization.OrganizationId);
        Assert.Equal(organization.ConversationId, Assert.Single(organizationList!).ConversationId);
        Assert.Equal(personal.ConversationId, Assert.Single(personalList!).ConversationId);
        Assert.Equal(
            1,
            await factory.CountDirectConversationsAsync(
                first.UserId,
                second.UserId,
                ConversationScope.Personal,
                null));
        Assert.Equal(
            1,
            await factory.CountDirectConversationsAsync(
                first.UserId,
                second.UserId,
                ConversationScope.Organization,
                organizationId));
    }

    [Fact]
    public async Task OrganizationConversationRejectsCrossOrganizationTarget()
    {
        using var actorClient = factory.CreateClient();
        using var targetClient = factory.CreateClient();
        var actor = await CreateMessagingUserAsync(actorClient);
        var target = await CreateMessagingUserAsync(targetClient);
        var actorOrganizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var targetOrganizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var actorMembership = await factory.CreateMembershipAsync(
            actor.UserId,
            actorOrganizationId,
            OrganizationRole.Manager);
        await factory.CreateMembershipAsync(
            target.UserId,
            targetOrganizationId,
            OrganizationRole.Teacher);
        await SelectOrganizationAsync(
            actorClient,
            actorMembership.MembershipId,
            OrganizationRole.Manager);

        using var response = await StartDirectAsync(
            actorClient,
            target.UserId,
            ConversationScope.Organization);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(
            MessagingErrorCodes.DirectRecipientNotAvailable,
            await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task TeacherAndStudentCanMessageOnlyWhileSharedClassRelationshipIsActive()
    {
        var setup = await CreateTeacherStudentSetupAsync();
        using var teacherClient = setup.TeacherClient;
        using var studentClient = setup.StudentClient;
        var conversation = await StartDirectAndReadAsync(
            teacherClient,
            setup.Student.UserId,
            ConversationScope.Organization);
        var sent = await SendMessageAndReadAsync(
            teacherClient,
            conversation.ConversationId,
            Guid.NewGuid(),
            "پیام آموزشی");
        Assert.Equal(1, sent.Sequence);

        await factory.EndTeacherAssignmentAsync(setup.TeacherAssignmentId);
        using var blocked = await SendMessageAsync(
            teacherClient,
            conversation.ConversationId,
            Guid.NewGuid(),
            "نباید ارسال شود");
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        Assert.Equal(MessagingErrorCodes.MessageNotAllowed, await ReadProblemCodeAsync(blocked));

        using var history = await teacherClient.GetAsync(
            $"/api/v1/conversations/{conversation.ConversationId}/messages");
        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
    }

    [Fact]
    public async Task MessageUsesServerTimePreservesTextAndIsIdempotent()
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
        var clientMessageId = Guid.NewGuid();
        const string exactText = "  <b>سلام</b>  ";
        var beforeUtc = DateTime.UtcNow;

        var firstReceipt = await SendMessageAndReadAsync(
            firstClient,
            conversation.ConversationId,
            clientMessageId,
            exactText);
        var afterUtc = DateTime.UtcNow;
        var retried = await SendMessageAndReadAsync(
            firstClient,
            conversation.ConversationId,
            clientMessageId,
            exactText);
        using var conflict = await SendMessageAsync(
            firstClient,
            conversation.ConversationId,
            clientMessageId,
            "محتوای متفاوت");

        Assert.Equal(firstReceipt, retried);
        Assert.InRange(firstReceipt.SentAtUtc, beforeUtc, afterUtc);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal(
            MessagingErrorCodes.MessageIdempotencyConflict,
            await ReadProblemCodeAsync(conflict));
        var rows = await factory.GetMessagesAsync(conversation.ConversationId);
        var row = Assert.Single(rows);
        Assert.Equal(exactText, row.Text);

        var page = await firstClient.GetFromJsonAsync<ConversationMessagePageResult>(
            $"/api/v1/conversations/{conversation.ConversationId}/messages",
            JsonOptions);
        Assert.Equal(exactText, Assert.Single(page!.Messages).Text);
    }

    [Fact]
    public async Task NonParticipantCannotReadOrSendAndUnauthenticatedRequestsAreRejected()
    {
        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();
        using var outsiderClient = factory.CreateClient();
        using var anonymousClient = factory.CreateClient();
        var first = await CreateMessagingUserAsync(firstClient);
        var second = await CreateMessagingUserAsync(secondClient);
        _ = await CreateMessagingUserAsync(outsiderClient);
        await factory.CreatePersonalCommunicationGrantAsync(first.UserId, second.UserId);
        var conversation = await StartDirectAndReadAsync(
            firstClient,
            second.UserId,
            ConversationScope.Personal);

        using var read = await outsiderClient.GetAsync(
            $"/api/v1/conversations/{conversation.ConversationId}/messages");
        using var send = await SendMessageAsync(
            outsiderClient,
            conversation.ConversationId,
            Guid.NewGuid(),
            "غیرمجاز");
        using var anonymous = await anonymousClient.GetAsync("/api/v1/conversations");

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(MessagingErrorCodes.ConversationNotFound, await ReadProblemCodeAsync(read));
        Assert.Equal(HttpStatusCode.NotFound, send.StatusCode);
        Assert.Equal(MessagingErrorCodes.ConversationNotFound, await ReadProblemCodeAsync(send));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task PersonalGrantRevocationBlocksNewMessagesButPreservesHistory()
    {
        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();
        var first = await CreateMessagingUserAsync(firstClient);
        var second = await CreateMessagingUserAsync(secondClient);
        var grantId = await factory.CreatePersonalCommunicationGrantAsync(
            first.UserId,
            second.UserId);
        var conversation = await StartDirectAndReadAsync(
            firstClient,
            second.UserId,
            ConversationScope.Personal);
        var clientMessageId = Guid.NewGuid();
        var committed = await SendMessageAndReadAsync(
            firstClient,
            conversation.ConversationId,
            clientMessageId,
            "قبل از لغو");

        await factory.RevokePersonalCommunicationGrantAsync(grantId);
        var retried = await SendMessageAndReadAsync(
            firstClient,
            conversation.ConversationId,
            clientMessageId,
            "قبل از لغو");
        using var blocked = await SendMessageAsync(
            firstClient,
            conversation.ConversationId,
            Guid.NewGuid(),
            "بعد از لغو");
        using var history = await firstClient.GetAsync(
            $"/api/v1/conversations/{conversation.ConversationId}/messages");

        Assert.Equal(committed, retried);
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        Assert.Equal(MessagingErrorCodes.MessageNotAllowed, await ReadProblemCodeAsync(blocked));
        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        var page = await history.Content.ReadFromJsonAsync<ConversationMessagePageResult>(JsonOptions);
        Assert.Single(page!.Messages);
    }

    [Fact]
    public async Task ConcurrentStartsCreateExactlyOneConversation()
    {
        using var ownerClient = factory.CreateClient();
        using var targetClient = factory.CreateClient();
        var owner = await CreateMessagingUserAsync(ownerClient);
        var target = await CreateMessagingUserAsync(targetClient);
        await factory.CreatePersonalCommunicationGrantAsync(owner.UserId, target.UserId);
        var clients = Enumerable.Range(0, 5)
            .Select(_ => factory.CreateClient())
            .ToArray();
        foreach (var client in clients)
        {
            UseBearerToken(client, owner.AccessToken);
        }

        try
        {
            var responses = await Task.WhenAll(clients.Select(client => StartDirectAsync(
                client,
                target.UserId,
                ConversationScope.Personal)));
            try
            {
                Assert.All(responses, response => Assert.True(response.IsSuccessStatusCode));
                var results = await Task.WhenAll(responses.Select(response =>
                    response.Content.ReadFromJsonAsync<DirectConversationResult>(JsonOptions)));
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
            await factory.CountDirectConversationsAsync(
                owner.UserId,
                target.UserId,
                ConversationScope.Personal,
                null));
    }

    [Fact]
    public async Task ConcurrentMessagesReceiveUniqueMonotonicSequences()
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
        var sends = Enumerable.Range(1, 8)
            .Select(index => SendMessageAsync(
                firstClient,
                conversation.ConversationId,
                Guid.NewGuid(),
                $"پیام {index}"))
            .ToArray();

        var responses = await Task.WhenAll(sends);
        try
        {
            Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }

        var rows = await factory.GetMessagesAsync(conversation.ConversationId);
        Assert.Equal(Enumerable.Range(1, 8).Select(value => (long)value), rows.Select(row => row.Sequence));
        Assert.Equal(8, rows.Select(row => row.Sequence).Distinct().Count());
    }

    [Fact]
    public async Task MessageHistoryUsesStableSequenceCursor()
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
        for (var index = 1; index <= 5; index++)
        {
            await SendMessageAndReadAsync(
                firstClient,
                conversation.ConversationId,
                Guid.NewGuid(),
                $"پیام {index}");
        }

        var firstPage = await firstClient.GetFromJsonAsync<ConversationMessagePageResult>(
            $"/api/v1/conversations/{conversation.ConversationId}/messages?limit=2",
            JsonOptions);
        var secondPage = await firstClient.GetFromJsonAsync<ConversationMessagePageResult>(
            $"/api/v1/conversations/{conversation.ConversationId}/messages?limit=2&beforeSequence={firstPage!.NextBeforeSequence}",
            JsonOptions);

        Assert.Equal(new long[] { 4, 5 }, firstPage.Messages.Select(message => message.Sequence));
        Assert.Equal(4, firstPage.NextBeforeSequence);
        Assert.Equal(new long[] { 2, 3 }, secondPage!.Messages.Select(message => message.Sequence));
        Assert.Equal(2, secondPage.NextBeforeSequence);
    }

    private async Task<TeacherStudentSetup> CreateTeacherStudentSetupAsync()
    {
        var teacherClient = factory.CreateClient();
        var studentClient = factory.CreateClient();
        try
        {
            var teacher = await CreateMessagingUserAsync(teacherClient);
            var student = await CreateMessagingUserAsync(studentClient);
            var organizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
            var teacherMembership = await factory.CreateMembershipAsync(
                teacher.UserId,
                organizationId,
                OrganizationRole.Teacher);
            await factory.CreateMembershipAsync(
                student.UserId,
                organizationId,
                OrganizationRole.Student);
            var studentPersonId = await factory.GetUserPersonIdAsync(student.UserId);
            var studentOrganizationPersonId = await factory.CreateOrganizationPersonAsync(
                organizationId,
                studentPersonId);
            var academicClass = await factory.CreateAcademicClassAsync(organizationId);
            await factory.CreateEnrollmentAsync(
                organizationId,
                academicClass.ClassId,
                studentOrganizationPersonId);
            var teacherAssignmentId = await factory.CreateTeacherAssignmentAsync(
                organizationId,
                academicClass.ClassId,
                teacherMembership.MembershipId);
            await SelectOrganizationAsync(
                teacherClient,
                teacherMembership.MembershipId,
                OrganizationRole.Teacher);
            return new TeacherStudentSetup(
                teacherClient,
                studentClient,
                teacher,
                student,
                teacherAssignmentId);
        }
        catch
        {
            teacherClient.Dispose();
            studentClient.Dispose();
            throw;
        }
    }

    private async Task<MessagingUser> CreateMessagingUserAsync(HttpClient client)
    {
        var number = Interlocked.Increment(ref _phoneSequence);
        var phone = $"+9891{number:D8}";
        using var challengeResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/otp/challenges",
            new RequestOtpCommand(phone),
            JsonOptions);
        challengeResponse.EnsureSuccessStatusCode();
        var challenge = await challengeResponse.Content.ReadFromJsonAsync<RequestOtpResult>(JsonOptions);
        using var verifyResponse = await client.PostAsJsonAsync(
            "/api/v1/auth/otp/verify",
            new VerifyOtpCommand(
                challenge!.ChallengeId,
                phone,
                factory.GetOtpCode(challenge.ChallengeId)),
            JsonOptions);
        verifyResponse.EnsureSuccessStatusCode();
        var verified = await verifyResponse.Content.ReadFromJsonAsync<VerifyOtpResult>(JsonOptions);
        UseBearerToken(client, verified!.AccessToken);
        var suffix = Guid.NewGuid().ToString("N")[..12];
        using var profileResponse = await client.PatchAsJsonAsync(
            "/api/v1/me/profile",
            new CompleteProfileCommand("کاربر", "پیام", $"کاربر {suffix}", $"msg.{suffix}"),
            JsonOptions);
        profileResponse.EnsureSuccessStatusCode();
        await factory.SetCommunicationAgeCategoryAsync(
            verified.User.Id,
            CommunicationAgeCategory.Adult);
        return new MessagingUser(verified.User.Id, verified.AccessToken);
    }

    private static async Task SelectOrganizationAsync(
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

    private static async Task SelectPersonalAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/v1/workspaces/select",
            new SelectWorkspaceCommand(WorkspaceType.Personal, null, null),
            JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    private static Task<HttpResponseMessage> StartDirectAsync(
        HttpClient client,
        Guid targetUserId,
        ConversationScope scope) =>
        client.PostAsJsonAsync(
            "/api/v1/conversations/direct",
            new StartDirectConversationCommand(targetUserId, scope),
            JsonOptions);

    private static async Task<DirectConversationResult> StartDirectAndReadAsync(
        HttpClient client,
        Guid targetUserId,
        ConversationScope scope)
    {
        using var response = await StartDirectAsync(client, targetUserId, scope);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DirectConversationResult>(JsonOptions))!;
    }

    private static Task<HttpResponseMessage> SendMessageAsync(
        HttpClient client,
        Guid conversationId,
        Guid clientMessageId,
        string text) =>
        client.PostAsJsonAsync(
            $"/api/v1/conversations/{conversationId}/messages",
            new SendTextMessageCommand(clientMessageId, text),
            JsonOptions);

    private static async Task<MessageReceiptResult> SendMessageAndReadAsync(
        HttpClient client,
        Guid conversationId,
        Guid clientMessageId,
        string text)
    {
        using var response = await SendMessageAsync(client, conversationId, clientMessageId, text);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<MessageReceiptResult>(JsonOptions))!;
    }

    private static void UseBearerToken(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static async Task<string?> ReadProblemCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("code").GetString();
    }

    private static string NewOrganizationName() => $"مرکز پیام {Guid.NewGuid():N}";

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed record MessagingUser(Guid UserId, string AccessToken);

    private sealed record TeacherStudentSetup(
        HttpClient TeacherClient,
        HttpClient StudentClient,
        MessagingUser Teacher,
        MessagingUser Student,
        Guid TeacherAssignmentId);
}
