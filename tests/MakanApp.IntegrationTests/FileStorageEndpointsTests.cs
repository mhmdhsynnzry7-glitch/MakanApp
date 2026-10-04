using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MakanApp.Application.Identity;
using MakanApp.Application.Organization;
using MakanApp.Application.Storage;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Storage;
using Xunit;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class FileStorageEndpointsTests(MakanAppWebApplicationFactory factory)
{
    private static int _phoneSequence = 1_000_000;
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [Fact]
    public async Task AuthenticatedUserUploadsValidFile()
    {
        using var client = factory.CreateClient();
        var user = await AuthenticateAsync(client);

        using var response = await UploadAsync(client, "hello.txt", "text/plain", Bytes("hello"));
        var result = await ReadRequiredAsync<FileAssetResult>(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(user.User.Id, result.UploadedByUserId);
        Assert.Equal(FileAssetStatus.Ready, result.Status);
        Assert.Equal(5, result.SizeBytes);
        Assert.Equal(64, result.Sha256Hash!.Length);
    }

    [Fact]
    public async Task UploadPersistsMetadataAndTemporaryExpiry()
    {
        using var client = factory.CreateClient();
        await AuthenticateAsync(client);
        using var response = await UploadAsync(client, "notes.txt", "text/plain", Bytes("metadata"));
        var result = await ReadRequiredAsync<FileAssetResult>(response);

        var stored = await factory.GetFileAssetAsync(result.Id);

        Assert.Equal("notes.txt", stored.OriginalFileName);
        Assert.Equal(FileAssetStatus.Ready, stored.Status);
        Assert.True(stored.UnattachedExpiresAtUtc > result.CreatedAtUtc);
    }

    [Fact]
    public async Task UploadWritesBytesToIsolatedStorage()
    {
        using var client = factory.CreateClient();
        await AuthenticateAsync(client);
        using var response = await UploadAsync(client, "stored.txt", "text/plain", Bytes("stored"));
        var result = await ReadRequiredAsync<FileAssetResult>(response);

        Assert.True(await factory.StoredBytesExistAsync(result.Id));
        Assert.StartsWith(Path.GetTempPath(), factory.StorageRootPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DownloadStreamsOriginalBytesAndHeaders()
    {
        using var client = factory.CreateClient();
        await AuthenticateAsync(client);
        var bytes = Enumerable.Range(0, 900).Select(value => (byte)(value % 251)).ToArray();
        using var upload = await UploadAsync(client, "stream.txt", "text/plain", bytes);
        var result = await ReadRequiredAsync<FileAssetResult>(upload);

        using var download = await client.GetAsync($"/api/v1/files/{result.Id}/content");

        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("text/plain", download.Content.Headers.ContentType!.MediaType);
        Assert.Equal(bytes.Length, download.Content.Headers.ContentLength);
        Assert.Equal(bytes, await download.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task MetadataDoesNotExposeStorageKeyOrPhysicalPath()
    {
        using var client = factory.CreateClient();
        await AuthenticateAsync(client);
        using var upload = await UploadAsync(client, "public-name.txt", "text/plain", Bytes("private"));
        var uploaded = await ReadRequiredAsync<FileAssetResult>(upload);

        using var response = await client.GetAsync($"/api/v1/files/{uploaded.Id}");
        var json = await response.Content.ReadAsStringAsync();

        Assert.DoesNotContain("storageKey", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(factory.StorageRootPath, json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UnsafeOriginalNameCannotControlStoragePath()
    {
        using var client = factory.CreateClient();
        await AuthenticateAsync(client);
        using var response = await UploadAsync(
            client,
            "../../outside/<escape>.txt",
            "text/plain",
            Bytes("safe"));
        var result = await ReadRequiredAsync<FileAssetResult>(response);
        var stored = await factory.GetFileAssetAsync(result.Id);

        Assert.Equal("escape.txt", result.OriginalFileName);
        Assert.DoesNotContain("..", stored.StorageKey);
        Assert.DoesNotContain("escape", stored.StorageKey, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EmptyFileIsRejected()
    {
        using var client = factory.CreateClient();
        await AuthenticateAsync(client);

        using var response = await UploadAsync(client, "empty.txt", "text/plain", []);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(StorageErrorCodes.FileEmpty, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task OversizedFileIsRejected()
    {
        using var client = factory.CreateClient();
        await AuthenticateAsync(client);

        using var response = await UploadAsync(client, "large.txt", "text/plain", new byte[1025]);

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal(StorageErrorCodes.FileTooLarge, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task UnsupportedContentTypeIsRejected()
    {
        using var client = factory.CreateClient();
        await AuthenticateAsync(client);

        using var response = await UploadAsync(
            client,
            "program.exe",
            "application/octet-stream",
            Bytes("not allowed"));

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Equal(StorageErrorCodes.FileTypeNotAllowed, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task FailedStorageWriteLeavesRejectedMetadataAndNoBytes()
    {
        using var client = factory.CreateClient();
        var user = await AuthenticateAsync(client);
        factory.FailNextStorageSave();

        using var response = await UploadAsync(client, "failure.txt", "text/plain", Bytes("fail"));
        var stored = await factory.GetLatestFileAssetForUserAsync(user.User.Id);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(FileAssetStatus.Rejected, stored.Status);
        Assert.NotNull(stored.RejectedAtUtc);
        Assert.False(await factory.StoredBytesExistAsync(stored.Id));
    }

    [Fact]
    public async Task UnauthenticatedUploadIsRejected()
    {
        using var client = factory.CreateClient();

        using var response = await UploadAsync(client, "anonymous.txt", "text/plain", Bytes("no"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UnrelatedUserCannotReadPrivateUnattachedFile()
    {
        using var ownerClient = factory.CreateClient();
        await AuthenticateAsync(ownerClient);
        using var upload = await UploadAsync(ownerClient, "private.txt", "text/plain", Bytes("secret"));
        var file = await ReadRequiredAsync<FileAssetResult>(upload);
        using var otherClient = factory.CreateClient();
        await AuthenticateAsync(otherClient);

        using var metadata = await otherClient.GetAsync($"/api/v1/files/{file.Id}");
        using var content = await otherClient.GetAsync($"/api/v1/files/{file.Id}/content");

        Assert.Equal(HttpStatusCode.NotFound, metadata.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, content.StatusCode);
    }

    [Fact]
    public async Task DeletedFileCannotBeDownloadedAndBytesAreRemoved()
    {
        using var client = factory.CreateClient();
        await AuthenticateAsync(client);
        using var upload = await UploadAsync(client, "delete.txt", "text/plain", Bytes("delete me"));
        var file = await ReadRequiredAsync<FileAssetResult>(upload);

        using var delete = await client.DeleteAsync(
            $"/api/v1/files/{file.Id}?expectedRowVersion={Uri.EscapeDataString(file.RowVersion)}");
        var deleted = await ReadRequiredAsync<FileAssetResult>(delete);
        using var download = await client.GetAsync($"/api/v1/files/{file.Id}/content");

        Assert.Equal(FileAssetStatus.Deleted, deleted.Status);
        Assert.False(await factory.StoredBytesExistAsync(file.Id));
        Assert.Equal(HttpStatusCode.NotFound, download.StatusCode);
    }

    [Fact]
    public async Task RepeatedDeleteReturnsAlreadyDeletedConflict()
    {
        using var client = factory.CreateClient();
        await AuthenticateAsync(client);
        using var upload = await UploadAsync(client, "repeat-delete.txt", "text/plain", Bytes("delete"));
        var file = await ReadRequiredAsync<FileAssetResult>(upload);
        using var firstDelete = await client.DeleteAsync(
            $"/api/v1/files/{file.Id}?expectedRowVersion={Uri.EscapeDataString(file.RowVersion)}");
        var deleted = await ReadRequiredAsync<FileAssetResult>(firstDelete);

        using var secondDelete = await client.DeleteAsync(
            $"/api/v1/files/{file.Id}?expectedRowVersion={Uri.EscapeDataString(deleted.RowVersion)}");

        Assert.Equal(HttpStatusCode.Conflict, secondDelete.StatusCode);
        Assert.Equal(StorageErrorCodes.FileAlreadyDeleted, await ReadProblemCodeAsync(secondDelete));
    }

    [Fact]
    public async Task DatabaseRejectsDuplicateStorageKey()
    {
        using var client = factory.CreateClient();
        await AuthenticateAsync(client);
        using var upload = await UploadAsync(client, "unique.txt", "text/plain", Bytes("unique"));
        var file = await ReadRequiredAsync<FileAssetResult>(upload);

        Assert.True(await factory.DuplicateStorageKeyIsRejectedAsync(file.Id));
    }

    [Fact]
    public async Task StaleLifecycleUpdateReturnsConcurrencyConflict()
    {
        using var client = factory.CreateClient();
        await AuthenticateAsync(client);
        using var upload = await UploadAsync(client, "stale.txt", "text/plain", Bytes("stale"));
        var file = await ReadRequiredAsync<FileAssetResult>(upload);
        await factory.BumpFileAssetRowVersionAsync(file.Id);

        using var response = await client.DeleteAsync(
            $"/api/v1/files/{file.Id}?expectedRowVersion={Uri.EscapeDataString(file.RowVersion)}");

        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
        Assert.Equal(StorageErrorCodes.ConcurrencyConflict, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task OrganizationScopeComesFromCurrentContextNotFormData()
    {
        using var client = factory.CreateClient();
        var user = await AuthenticateAsync(client);
        var organizationId = await factory.CreateOrganizationAsync($"Storage {Guid.NewGuid():N}");
        var membership = await factory.CreateMembershipAsync(
            user.User.Id,
            organizationId,
            OrganizationRole.Student);
        await SelectOrganizationAsync(client, membership.MembershipId, OrganizationRole.Student);
        var forgedOrganizationId = Guid.NewGuid();

        using var response = await UploadAsync(
            client,
            "organization.txt",
            "text/plain",
            Bytes("organization"),
            forgedOrganizationId);
        var result = await ReadRequiredAsync<FileAssetResult>(response);

        Assert.Equal(organizationId, result.OrganizationId);
        Assert.NotEqual(forgedOrganizationId, result.OrganizationId);
    }

    [Fact]
    public async Task UploaderMustUseTheMatchingCurrentWorkspace()
    {
        using var client = factory.CreateClient();
        var user = await AuthenticateAsync(client);
        using var upload = await UploadAsync(client, "personal.txt", "text/plain", Bytes("personal"));
        var file = await ReadRequiredAsync<FileAssetResult>(upload);
        var organizationId = await factory.CreateOrganizationAsync($"Storage {Guid.NewGuid():N}");
        var membership = await factory.CreateMembershipAsync(
            user.User.Id,
            organizationId,
            OrganizationRole.Student);
        await SelectOrganizationAsync(client, membership.MembershipId, OrganizationRole.Student);

        using var response = await client.GetAsync($"/api/v1/files/{file.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(StorageErrorCodes.FileNotAllowed, await ReadProblemCodeAsync(response));
    }

    private async Task<VerifyOtpResult> AuthenticateAsync(HttpClient client)
    {
        var sequence = Interlocked.Increment(ref _phoneSequence);
        var phoneNumber = $"+98919{sequence:D7}";
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
                factory.GetOtpCode(challenge.ChallengeId)),
            JsonOptions);
        verifyResponse.EnsureSuccessStatusCode();
        var user = await ReadRequiredAsync<VerifyOtpResult>(verifyResponse);
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", user.AccessToken);
        return user;
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

    private static async Task<HttpResponseMessage> UploadAsync(
        HttpClient client,
        string fileName,
        string contentType,
        byte[] bytes,
        Guid? suppliedOrganizationId = null)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        form.Add(file, "file", fileName);
        if (suppliedOrganizationId.HasValue)
        {
            form.Add(new StringContent(suppliedOrganizationId.Value.ToString()), "organizationId");
        }

        return await client.PostAsync("/api/v1/files", form);
    }

    private static byte[] Bytes(string value) => Encoding.UTF8.GetBytes(value);

    private static async Task<T> ReadRequiredAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;

    private static async Task<string?> ReadProblemCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("code").GetString();
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
