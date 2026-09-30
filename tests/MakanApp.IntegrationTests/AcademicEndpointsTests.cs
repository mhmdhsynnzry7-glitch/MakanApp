using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using MakanApp.Application.Academic;
using MakanApp.Application.Identity;
using MakanApp.Application.Organization;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Organization;
using Xunit;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed class AcademicEndpointsTests(MakanAppWebApplicationFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static int _phoneSequence = 800;

    [Fact]
    public async Task ManagerCreatesAcademicPeriodCourseAndClass()
    {
        using var client = factory.CreateClient();
        var manager = await CreateManagerAsync(client);

        using var periodResponse = await client.PostAsJsonAsync(
            "/api/v1/academic/periods",
            new CreateAcademicPeriodCommand(
                "سال تحصیلی ۱۴۰۵",
                new DateOnly(2026, 9, 1),
                new DateOnly(2027, 6, 30)),
            JsonOptions);
        var period = await ReadRequiredAsync<AcademicPeriodResult>(periodResponse);

        using var courseResponse = await client.PostAsJsonAsync(
            "/api/v1/academic/courses",
            new CreateCourseCommand("ریاضی"),
            JsonOptions);
        var course = await ReadRequiredAsync<CourseResult>(courseResponse);

        using var classResponse = await client.PostAsJsonAsync(
            "/api/v1/academic/classes",
            new CreateClassCommand(period.Id, course.Id, "ریاضی هفتم الف", 25, true),
            JsonOptions);
        var academicClass = await ReadRequiredAsync<ClassResult>(classResponse);

        Assert.Equal(HttpStatusCode.Created, periodResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, courseResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, classResponse.StatusCode);
        Assert.Equal(manager.OrganizationId, period.OrganizationId);
        Assert.Equal(manager.OrganizationId, course.OrganizationId);
        Assert.Equal(manager.OrganizationId, academicClass.OrganizationId);
        Assert.Equal(ClassStatus.Active, academicClass.Status);
    }

    [Fact]
    public async Task ClassRejectsCourseAndPeriodFromAnotherOrganization()
    {
        using var client = factory.CreateClient();
        var manager = await CreateManagerAsync(client);
        var ownPeriodId = await factory.CreateAcademicPeriodAsync(manager.OrganizationId);
        var ownCourseId = await factory.CreateCourseAsync(manager.OrganizationId);
        var otherOrganizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var otherPeriodId = await factory.CreateAcademicPeriodAsync(otherOrganizationId);
        var otherCourseId = await factory.CreateCourseAsync(otherOrganizationId);

        using var courseResponse = await client.PostAsJsonAsync(
            "/api/v1/academic/classes",
            new CreateClassCommand(ownPeriodId, otherCourseId, "Invalid", 10, true),
            JsonOptions);
        using var periodResponse = await client.PostAsJsonAsync(
            "/api/v1/academic/classes",
            new CreateClassCommand(otherPeriodId, ownCourseId, "Invalid", 10, true),
            JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, courseResponse.StatusCode);
        Assert.Equal(AcademicErrorCodes.CourseNotFound, await ReadProblemCodeAsync(courseResponse));
        Assert.Equal(HttpStatusCode.NotFound, periodResponse.StatusCode);
        Assert.Equal(
            AcademicErrorCodes.AcademicPeriodNotFound,
            await ReadProblemCodeAsync(periodResponse));
        Assert.True(await factory.CrossOrganizationClassReferencesAreRejectedAsync(
            manager.OrganizationId,
            ownPeriodId,
            otherCourseId));
        Assert.True(await factory.CrossOrganizationClassReferencesAreRejectedAsync(
            manager.OrganizationId,
            otherPeriodId,
            ownCourseId));
    }

    [Fact]
    public async Task EnrollmentLifecyclePreservesHistoryAndAllowsLaterClass()
    {
        using var client = factory.CreateClient();
        var manager = await CreateManagerAsync(client);
        var learnerId = Assert.Single(
            await factory.CreateOrganizationPersonsAsync(manager.OrganizationId, 1));
        var firstClass = await factory.CreateAcademicClassAsync(manager.OrganizationId);
        var secondClass = await factory.CreateAcademicClassAsync(manager.OrganizationId);

        using var firstResponse = await EnrollAsync(client, firstClass.ClassId, learnerId);
        var firstEnrollment = await ReadRequiredAsync<EnrollmentResult>(firstResponse);
        using var endResponse = await client.PostAsJsonAsync(
            $"/api/v1/academic/classes/{firstClass.ClassId}/enrollments/{firstEnrollment.Id}/end",
            new EndEnrollmentCommand(EnrollmentStatus.Withdrawn),
            JsonOptions);
        var ended = await ReadRequiredAsync<EnrollmentResult>(endResponse);
        using var secondResponse = await EnrollAsync(client, secondClass.ClassId, learnerId);
        var secondEnrollment = await ReadRequiredAsync<EnrollmentResult>(secondResponse);

        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        Assert.Equal(EnrollmentStatus.Withdrawn, ended.Status);
        Assert.NotNull(ended.EndedAtUtc);
        Assert.Equal(EnrollmentStatus.Withdrawn, await factory.GetEnrollmentStatusAsync(firstEnrollment.Id));
        Assert.Equal(HttpStatusCode.Created, secondResponse.StatusCode);
        Assert.NotEqual(firstEnrollment.Id, secondEnrollment.Id);
        Assert.Equal(1, await factory.CountEnrollmentsAsync(
            manager.OrganizationId,
            firstClass.ClassId,
            learnerId));
    }

    [Fact]
    public async Task EnrollmentRejectsCrossOrganizationDraftAndDuplicateRequests()
    {
        using var client = factory.CreateClient();
        var manager = await CreateManagerAsync(client);
        var activeClass = await factory.CreateAcademicClassAsync(manager.OrganizationId);
        var draftClass = await factory.CreateAcademicClassAsync(manager.OrganizationId, active: false);
        var ownLearnerId = Assert.Single(
            await factory.CreateOrganizationPersonsAsync(manager.OrganizationId, 1));
        var otherOrganizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var otherLearnerId = Assert.Single(
            await factory.CreateOrganizationPersonsAsync(otherOrganizationId, 1));

        using var crossOrganization = await EnrollAsync(
            client,
            activeClass.ClassId,
            otherLearnerId);
        using var draft = await EnrollAsync(client, draftClass.ClassId, ownLearnerId);
        using var first = await EnrollAsync(client, activeClass.ClassId, ownLearnerId);
        using var duplicate = await EnrollAsync(client, activeClass.ClassId, ownLearnerId);

        Assert.Equal(HttpStatusCode.NotFound, crossOrganization.StatusCode);
        Assert.Equal(AcademicErrorCodes.LearnerNotFound, await ReadProblemCodeAsync(crossOrganization));
        Assert.Equal(HttpStatusCode.Conflict, draft.StatusCode);
        Assert.Equal(AcademicErrorCodes.ClassNotActive, await ReadProblemCodeAsync(draft));
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(
            AcademicErrorCodes.EnrollmentAlreadyActive,
            await ReadProblemCodeAsync(duplicate));
        Assert.Equal(1, await factory.CountActiveEnrollmentsAsync(
            manager.OrganizationId,
            activeClass.ClassId));
        Assert.True(await factory.CrossOrganizationEnrollmentIsRejectedAsync(
            manager.OrganizationId,
            activeClass.ClassId,
            otherLearnerId));
    }

    [Fact]
    public async Task TeacherAssignmentRequiresSameOrganizationActiveTeacherRoleAndPreservesHistory()
    {
        using var client = factory.CreateClient();
        var manager = await CreateManagerAsync(client);
        var academicClass = await factory.CreateAcademicClassAsync(manager.OrganizationId);
        var teacherUser = await CreateAuthenticatedUserAsync(client);
        var teacher = await factory.CreateMembershipAsync(
            teacherUser.User.Id,
            manager.OrganizationId,
            OrganizationRole.Teacher);
        var nonTeacherUser = await CreateAuthenticatedUserAsync(client);
        var nonTeacher = await factory.CreateMembershipAsync(
            nonTeacherUser.User.Id,
            manager.OrganizationId,
            OrganizationRole.Student);
        var otherOrganizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var otherTeacher = await factory.CreateMembershipAsync(
            teacherUser.User.Id,
            otherOrganizationId,
            OrganizationRole.Teacher);

        UseBearerToken(client, manager.AccessToken);
        using var validResponse = await AssignTeacherAsync(
            client,
            academicClass.ClassId,
            teacher.MembershipId);
        var assignment = await ReadRequiredAsync<TeacherAssignmentResult>(validResponse);
        using var duplicateResponse = await AssignTeacherAsync(
            client,
            academicClass.ClassId,
            teacher.MembershipId);
        using var nonTeacherResponse = await AssignTeacherAsync(
            client,
            academicClass.ClassId,
            nonTeacher.MembershipId);
        using var crossOrganizationResponse = await AssignTeacherAsync(
            client,
            academicClass.ClassId,
            otherTeacher.MembershipId);
        using var endResponse = await client.PostAsync(
            $"/api/v1/academic/classes/{academicClass.ClassId}/teachers/{assignment.Id}/end",
            null);
        var ended = await ReadRequiredAsync<TeacherAssignmentResult>(endResponse);

        Assert.Equal(HttpStatusCode.Created, validResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
        Assert.Equal(
            AcademicErrorCodes.TeacherAssignmentAlreadyActive,
            await ReadProblemCodeAsync(duplicateResponse));
        Assert.Equal(HttpStatusCode.Forbidden, nonTeacherResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, crossOrganizationResponse.StatusCode);
        Assert.Equal(TeacherAssignmentStatus.Ended, ended.Status);
        Assert.NotNull(ended.EndedAtUtc);
        Assert.Equal(
            TeacherAssignmentStatus.Ended,
            await factory.GetTeacherAssignmentStatusAsync(assignment.Id));
        Assert.True(await factory.CrossOrganizationTeacherAssignmentIsRejectedAsync(
            manager.OrganizationId,
            academicClass.ClassId,
            otherTeacher.MembershipId));
    }

    [Fact]
    public async Task SqlServerFilteredIndexesRejectDuplicateActiveRows()
    {
        var organizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var academicClass = await factory.CreateAcademicClassAsync(organizationId);
        var learnerId = Assert.Single(await factory.CreateOrganizationPersonsAsync(organizationId, 1));
        using var client = factory.CreateClient();
        var teacherUser = await CreateAuthenticatedUserAsync(client);
        var teacher = await factory.CreateMembershipAsync(
            teacherUser.User.Id,
            organizationId,
            OrganizationRole.Teacher);

        Assert.True(await factory.DuplicateActiveEnrollmentIsRejectedAsync(
            organizationId,
            academicClass.ClassId,
            learnerId));
        Assert.True(await factory.DuplicateActiveTeacherAssignmentIsRejectedAsync(
            organizationId,
            academicClass.ClassId,
            teacher.MembershipId));
    }

    [Fact]
    public async Task ConcurrentEnrollmentProtectsLastSeat()
    {
        using var client = factory.CreateClient();
        var manager = await CreateManagerAsync(client);
        var academicClass = await factory.CreateAcademicClassAsync(
            manager.OrganizationId,
            capacity: 1);
        var learnerIds = await factory.CreateOrganizationPersonsAsync(manager.OrganizationId, 2);

        var responses = await Task.WhenAll(learnerIds.Select(
            learnerId => EnrollAsync(client, academicClass.ClassId, learnerId)));
        try
        {
            Assert.Single(
                responses,
                response => response.StatusCode == HttpStatusCode.Created);
            var conflict = Assert.Single(
                responses,
                response => response.StatusCode == HttpStatusCode.Conflict);
            Assert.Equal(
                AcademicErrorCodes.ClassCapacityExceeded,
                await ReadProblemCodeAsync(conflict));
            Assert.Equal(1, await factory.CountActiveEnrollmentsAsync(
                manager.OrganizationId,
                academicClass.ClassId));
        }
        finally
        {
            DisposeResponses(responses);
        }
    }

    [Fact]
    public async Task HigherContentionEnrollmentNeverExceedsCapacity()
    {
        using var client = factory.CreateClient();
        var manager = await CreateManagerAsync(client);
        var academicClass = await factory.CreateAcademicClassAsync(
            manager.OrganizationId,
            capacity: 100);
        var learnerIds = await factory.CreateOrganizationPersonsAsync(manager.OrganizationId, 105);

        var responses = await Task.WhenAll(learnerIds.Select(
            learnerId => EnrollAsync(client, academicClass.ClassId, learnerId)));
        try
        {
            Assert.Equal(100, responses.Count(response => response.StatusCode == HttpStatusCode.Created));
            Assert.Equal(5, responses.Count(response => response.StatusCode == HttpStatusCode.Conflict));
            Assert.All(
                responses.Where(response => response.StatusCode != HttpStatusCode.Created),
                response => Assert.Equal(HttpStatusCode.Conflict, response.StatusCode));
            Assert.Equal(100, await factory.CountActiveEnrollmentsAsync(
                manager.OrganizationId,
                academicClass.ClassId));
        }
        finally
        {
            DisposeResponses(responses);
        }
    }

    [Fact]
    public async Task ManagerReadsOnlyCurrentOrganizationClasses()
    {
        using var client = factory.CreateClient();
        var manager = await CreateManagerAsync(client);
        var ownClass = await factory.CreateAcademicClassAsync(manager.OrganizationId);
        var otherOrganizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var otherClass = await factory.CreateAcademicClassAsync(otherOrganizationId);

        var classes = await client.GetFromJsonAsync<ClassResult[]>(
            "/api/v1/academic/classes",
            JsonOptions);

        Assert.Contains(classes!, item => item.Id == ownClass.ClassId);
        Assert.DoesNotContain(classes!, item => item.Id == otherClass.ClassId);
        Assert.All(classes!, item => Assert.Equal(manager.OrganizationId, item.OrganizationId));
    }

    [Fact]
    public async Task TeacherReadsOnlyActivelyAssignedClasses()
    {
        using var managerClient = factory.CreateClient();
        var manager = await CreateManagerAsync(managerClient);
        var assignedClass = await factory.CreateAcademicClassAsync(manager.OrganizationId);
        var endedClass = await factory.CreateAcademicClassAsync(manager.OrganizationId);
        var unrelatedClass = await factory.CreateAcademicClassAsync(manager.OrganizationId);
        using var teacherClient = factory.CreateClient();
        var teacherUser = await CreateAuthenticatedUserAsync(teacherClient);
        var teacher = await factory.CreateMembershipAsync(
            teacherUser.User.Id,
            manager.OrganizationId,
            OrganizationRole.Teacher);

        using var assignedResponse = await AssignTeacherAsync(
            managerClient,
            assignedClass.ClassId,
            teacher.MembershipId);
        var assigned = await ReadRequiredAsync<TeacherAssignmentResult>(assignedResponse);
        using var endedResponse = await AssignTeacherAsync(
            managerClient,
            endedClass.ClassId,
            teacher.MembershipId);
        var ended = await ReadRequiredAsync<TeacherAssignmentResult>(endedResponse);
        using var endResponse = await managerClient.PostAsync(
            $"/api/v1/academic/classes/{endedClass.ClassId}/teachers/{ended.Id}/end",
            null);
        endResponse.EnsureSuccessStatusCode();

        UseBearerToken(teacherClient, teacherUser.AccessToken);
        using var selection = await SelectWorkspaceAsync(
            teacherClient,
            teacher.MembershipId,
            OrganizationRole.Teacher);
        selection.EnsureSuccessStatusCode();
        var classes = await teacherClient.GetFromJsonAsync<ClassResult[]>(
            "/api/v1/academic/classes",
            JsonOptions);

        var visible = Assert.Single(classes!);
        Assert.Equal(assignedClass.ClassId, visible.Id);
        Assert.Equal(teacher.MembershipId, assigned.TeacherMembershipId);
        Assert.DoesNotContain(classes!, item => item.Id == unrelatedClass.ClassId);
    }

    [Fact]
    public async Task StudentReadsOnlyActivelyEnrolledClasses()
    {
        var organizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var enrolledClass = await factory.CreateAcademicClassAsync(organizationId);
        var unrelatedClass = await factory.CreateAcademicClassAsync(organizationId);
        using var studentClient = factory.CreateClient();
        var studentUser = await CreateAuthenticatedUserAsync(studentClient);
        UseBearerToken(studentClient, studentUser.AccessToken);
        using var profileResponse = await studentClient.PatchAsJsonAsync(
            "/api/v1/me/profile",
            new CompleteProfileCommand(
                "Student",
                "User",
                "Student User",
                $"student.{Guid.NewGuid().ToString("N")[..12]}"),
            JsonOptions);
        profileResponse.EnsureSuccessStatusCode();
        var personId = await factory.GetUserPersonIdAsync(studentUser.User.Id);
        var organizationPersonId = await factory.CreateOrganizationPersonAsync(
            organizationId,
            personId);
        var student = await factory.CreateMembershipAsync(
            studentUser.User.Id,
            organizationId,
            OrganizationRole.Student);
        await factory.CreateEnrollmentAsync(
            organizationId,
            enrolledClass.ClassId,
            organizationPersonId);

        using var selection = await SelectWorkspaceAsync(
            studentClient,
            student.MembershipId,
            OrganizationRole.Student);
        selection.EnsureSuccessStatusCode();
        var classes = await studentClient.GetFromJsonAsync<ClassResult[]>(
            "/api/v1/academic/classes",
            JsonOptions);

        var visible = Assert.Single(classes!);
        Assert.Equal(enrolledClass.ClassId, visible.Id);
        Assert.DoesNotContain(classes!, item => item.Id == unrelatedClass.ClassId);
    }

    [Fact]
    public async Task ParentClassReadIsDeferredAndDenied()
    {
        using var client = factory.CreateClient();
        var user = await CreateAuthenticatedUserAsync(client);
        var organizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var parent = await factory.CreateMembershipAsync(
            user.User.Id,
            organizationId,
            OrganizationRole.Parent);
        UseBearerToken(client, user.AccessToken);
        using var selection = await SelectWorkspaceAsync(
            client,
            parent.MembershipId,
            OrganizationRole.Parent);
        selection.EnsureSuccessStatusCode();

        using var response = await client.GetAsync("/api/v1/academic/classes");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(AcademicErrorCodes.AcademicReadNotAllowed, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task ManagerCannotMutateClassFromAnotherOrganization()
    {
        using var client = factory.CreateClient();
        var manager = await CreateManagerAsync(client);
        var learnerId = Assert.Single(
            await factory.CreateOrganizationPersonsAsync(manager.OrganizationId, 1));
        var otherOrganizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var otherClass = await factory.CreateAcademicClassAsync(otherOrganizationId);

        using var response = await EnrollAsync(client, otherClass.ClassId, learnerId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(AcademicErrorCodes.ClassNotFound, await ReadProblemCodeAsync(response));
        Assert.Equal(0, await factory.CountActiveEnrollmentsAsync(
            otherOrganizationId,
            otherClass.ClassId));
    }

    [Fact]
    public async Task RequestOrganizationIdCannotOverrideSelectedWorkspace()
    {
        using var client = factory.CreateClient();
        var manager = await CreateManagerAsync(client);
        var otherOrganizationId = await factory.CreateOrganizationAsync(NewOrganizationName());

        using var response = await client.PostAsJsonAsync(
            "/api/v1/academic/courses",
            new
            {
                OrganizationId = otherOrganizationId,
                Title = "علوم"
            },
            JsonOptions);
        var course = await ReadRequiredAsync<CourseResult>(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(manager.OrganizationId, course.OrganizationId);
        Assert.NotEqual(otherOrganizationId, course.OrganizationId);
    }

    [Fact]
    public async Task InactiveManagerMembershipCannotWriteAcademicData()
    {
        using var client = factory.CreateClient();
        var manager = await CreateManagerAsync(client);
        await factory.EndMembershipAsync(manager.MembershipId);

        using var response = await client.PostAsJsonAsync(
            "/api/v1/academic/courses",
            new CreateCourseCommand("علوم"),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task InactiveTeacherMembershipCannotReceiveAssignment()
    {
        using var client = factory.CreateClient();
        var manager = await CreateManagerAsync(client);
        var academicClass = await factory.CreateAcademicClassAsync(manager.OrganizationId);
        var teacherUser = await CreateAuthenticatedUserAsync(client);
        var teacher = await factory.CreateMembershipAsync(
            teacherUser.User.Id,
            manager.OrganizationId,
            OrganizationRole.Teacher);
        await factory.EndMembershipAsync(teacher.MembershipId);
        UseBearerToken(client, manager.AccessToken);

        using var response = await AssignTeacherAsync(
            client,
            academicClass.ClassId,
            teacher.MembershipId);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(AcademicErrorCodes.TeacherNotAllowed, await ReadProblemCodeAsync(response));
    }

    private async Task<ManagerContext> CreateManagerAsync(HttpClient client)
    {
        var user = await CreateAuthenticatedUserAsync(client);
        var organizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var membership = await factory.CreateMembershipAsync(
            user.User.Id,
            organizationId,
            OrganizationRole.Manager);
        UseBearerToken(client, user.AccessToken);
        using var selection = await SelectWorkspaceAsync(
            client,
            membership.MembershipId,
            OrganizationRole.Manager);
        selection.EnsureSuccessStatusCode();
        return new ManagerContext(
            user.User.Id,
            user.AccessToken,
            organizationId,
            membership.MembershipId);
    }

    private async Task<VerifyOtpResult> CreateAuthenticatedUserAsync(HttpClient client)
    {
        var sequence = Interlocked.Increment(ref _phoneSequence);
        var phoneNumber = $"+98915{sequence:D7}";
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
        return await ReadRequiredAsync<VerifyOtpResult>(verifyResponse);
    }

    private static Task<HttpResponseMessage> EnrollAsync(
        HttpClient client,
        Guid classId,
        Guid learnerOrganizationPersonId) =>
        client.PostAsJsonAsync(
            $"/api/v1/academic/classes/{classId}/enrollments",
            new EnrollLearnerCommand(learnerOrganizationPersonId),
            JsonOptions);

    private static Task<HttpResponseMessage> AssignTeacherAsync(
        HttpClient client,
        Guid classId,
        Guid teacherMembershipId) =>
        client.PostAsJsonAsync(
            $"/api/v1/academic/classes/{classId}/teachers",
            new AssignTeacherCommand(teacherMembershipId),
            JsonOptions);

    private static Task<HttpResponseMessage> SelectWorkspaceAsync(
        HttpClient client,
        Guid membershipId,
        OrganizationRole role) =>
        client.PostAsJsonAsync(
            "/api/v1/workspaces/select",
            new SelectWorkspaceCommand(WorkspaceType.Organization, membershipId, role),
            JsonOptions);

    private static async Task<T> ReadRequiredAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;

    private static async Task<string?> ReadProblemCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("code").GetString();
    }

    private static void UseBearerToken(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    private static void DisposeResponses(IEnumerable<HttpResponseMessage> responses)
    {
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    private static string NewOrganizationName() => $"آموزشگاه {Guid.NewGuid():N}";

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
}
