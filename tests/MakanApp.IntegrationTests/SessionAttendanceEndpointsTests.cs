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
public sealed class SessionAttendanceEndpointsTests(MakanAppWebApplicationFactory factory)
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private static int _phoneSequence = 2000;

    [Fact]
    public async Task ManagerCreatesSessionOnlyForCurrentOrganizationClass()
    {
        using var client = factory.CreateClient();
        var manager = await CreateManagerAsync(client);
        var ownClass = await factory.CreateAcademicClassAsync(manager.OrganizationId);
        var otherOrganizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var otherClass = await factory.CreateAcademicClassAsync(otherOrganizationId);
        var startUtc = FutureUtc(2);

        using var validResponse = await CreateSessionAsync(
            client,
            ownClass.ClassId,
            startUtc,
            startUtc.AddHours(1));
        var created = await ReadRequiredAsync<SessionResult>(validResponse);
        using var foreignResponse = await CreateSessionAsync(
            client,
            otherClass.ClassId,
            startUtc.AddHours(2),
            startUtc.AddHours(3));

        Assert.Equal(HttpStatusCode.Created, validResponse.StatusCode);
        Assert.Equal(manager.OrganizationId, created.OrganizationId);
        Assert.Equal(ownClass.ClassId, created.ClassId);
        Assert.Equal(SessionStatus.Scheduled, created.Status);
        Assert.Equal(HttpStatusCode.NotFound, foreignResponse.StatusCode);
        Assert.Equal(AcademicErrorCodes.ClassNotFound, await ReadProblemCodeAsync(foreignResponse));
    }

    [Fact]
    public async Task AssignedTeacherCanCreateAndUpdateSessionButUnassignedTeacherCannot()
    {
        using var managerClient = factory.CreateClient();
        var manager = await CreateManagerAsync(managerClient);
        var assignedClass = await factory.CreateAcademicClassAsync(manager.OrganizationId);
        var unrelatedClass = await factory.CreateAcademicClassAsync(manager.OrganizationId);
        using var teacherClient = factory.CreateClient();
        var teacherUser = await CreateAuthenticatedUserAsync(teacherClient);
        var teacher = await factory.CreateMembershipAsync(
            teacherUser.User.Id,
            manager.OrganizationId,
            OrganizationRole.Teacher);
        await factory.CreateTeacherAssignmentAsync(
            manager.OrganizationId,
            assignedClass.ClassId,
            teacher.MembershipId);
        await SelectWorkspaceAsync(
            teacherClient,
            teacherUser.AccessToken,
            teacher.MembershipId,
            OrganizationRole.Teacher);
        var startUtc = FutureUtc(2);

        using var createResponse = await CreateSessionAsync(
            teacherClient,
            assignedClass.ClassId,
            startUtc,
            startUtc.AddHours(1));
        var created = await ReadRequiredAsync<SessionResult>(createResponse);
        using var updateResponse = await teacherClient.PatchAsJsonAsync(
            $"/api/v1/academic/sessions/{created.Id}",
            new UpdateSessionCommand(
                "Updated",
                startUtc.AddMinutes(15),
                startUtc.AddHours(1).AddMinutes(15),
                "UTC",
                null,
                created.RowVersion),
            JsonOptions);
        using var forbiddenResponse = await CreateSessionAsync(
            teacherClient,
            unrelatedClass.ClassId,
            startUtc.AddHours(2),
            startUtc.AddHours(3));

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);
        Assert.Equal(
            AcademicErrorCodes.TeacherNotAllowed,
            await ReadProblemCodeAsync(forbiddenResponse));
    }

    [Fact]
    public async Task StudentScheduleContainsOnlyActivelyEnrolledClasses()
    {
        var organizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var enrolledClass = await factory.CreateAcademicClassAsync(organizationId);
        var unrelatedClass = await factory.CreateAcademicClassAsync(organizationId);
        var startUtc = FutureUtc(2);
        var visibleSessionId = await factory.CreateAcademicSessionAsync(
            organizationId,
            enrolledClass.ClassId,
            startUtc,
            startUtc.AddHours(1));
        var hiddenSessionId = await factory.CreateAcademicSessionAsync(
            organizationId,
            unrelatedClass.ClassId,
            startUtc.AddHours(2),
            startUtc.AddHours(3));
        using var client = factory.CreateClient();
        var studentUser = await CreateAuthenticatedUserAsync(client);
        await CompleteProfileAsync(client, studentUser);
        var personId = await factory.GetUserPersonIdAsync(studentUser.User.Id);
        var organizationPersonId = await factory.CreateOrganizationPersonAsync(
            organizationId,
            personId);
        var membership = await factory.CreateMembershipAsync(
            studentUser.User.Id,
            organizationId,
            OrganizationRole.Student);
        await factory.CreateEnrollmentAsync(
            organizationId,
            enrolledClass.ClassId,
            organizationPersonId);
        await SelectWorkspaceAsync(
            client,
            studentUser.AccessToken,
            membership.MembershipId,
            OrganizationRole.Student);

        var schedule = await GetScheduleAsync(client, startUtc.AddHours(-1), startUtc.AddHours(4));

        var visible = Assert.Single(schedule);
        Assert.Equal(visibleSessionId, visible.Id);
        Assert.DoesNotContain(schedule, session => session.Id == hiddenSessionId);
    }

    [Fact]
    public async Task ParentSeesOnlySelectedAuthorizedChildScheduleAndRevocationStopsAccess()
    {
        var organizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var childClass = await factory.CreateAcademicClassAsync(organizationId);
        var otherClass = await factory.CreateAcademicClassAsync(organizationId);
        var childId = Assert.Single(await factory.CreateOrganizationPersonsAsync(organizationId, 1));
        var otherChildId = Assert.Single(await factory.CreateOrganizationPersonsAsync(organizationId, 1));
        await factory.CreateEnrollmentAsync(organizationId, childClass.ClassId, childId);
        await factory.CreateEnrollmentAsync(organizationId, otherClass.ClassId, otherChildId);
        var startUtc = FutureUtc(2);
        var childSessionId = await factory.CreateAcademicSessionAsync(
            organizationId,
            childClass.ClassId,
            startUtc,
            startUtc.AddHours(1));
        var otherSessionId = await factory.CreateAcademicSessionAsync(
            organizationId,
            otherClass.ClassId,
            startUtc.AddHours(2),
            startUtc.AddHours(3));
        using var client = factory.CreateClient();
        var parentUser = await CreateAuthenticatedUserAsync(client);
        var parent = await factory.CreateMembershipAsync(
            parentUser.User.Id,
            organizationId,
            OrganizationRole.Parent);
        var relationId = await factory.CreateGuardianRelationAsync(
            parentUser.User.Id,
            organizationId,
            childId);
        await SelectWorkspaceAsync(
            client,
            parentUser.AccessToken,
            parent.MembershipId,
            OrganizationRole.Parent);
        await SelectChildAsync(client, childId);

        var schedule = await GetScheduleAsync(client, startUtc.AddHours(-1), startUtc.AddHours(4));
        using var tamperResponse = await client.PostAsync(
            $"/api/v1/guardian/children/{otherChildId}/select",
            null);
        await factory.RevokeGuardianRelationAsync(relationId);
        using var revokedResponse = await client.GetAsync(ScheduleUrl(
            startUtc.AddHours(-1),
            startUtc.AddHours(4)));

        var visible = Assert.Single(schedule);
        Assert.Equal(childSessionId, visible.Id);
        Assert.DoesNotContain(schedule, session => session.Id == otherSessionId);
        Assert.Equal(HttpStatusCode.NotFound, tamperResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, revokedResponse.StatusCode);
    }

    [Fact]
    public async Task ClassAndTeacherTimeConflictsAreRejectedWithoutTenantLeakage()
    {
        using var firstManagerClient = factory.CreateClient();
        using var secondManagerClient = factory.CreateClient();
        var firstManager = await CreateManagerAsync(firstManagerClient);
        var secondManager = await CreateManagerAsync(secondManagerClient);
        var firstClass = await factory.CreateAcademicClassAsync(firstManager.OrganizationId);
        var secondClass = await factory.CreateAcademicClassAsync(secondManager.OrganizationId);
        using var teacherClient = factory.CreateClient();
        var teacherUser = await CreateAuthenticatedUserAsync(teacherClient);
        var firstTeacher = await factory.CreateMembershipAsync(
            teacherUser.User.Id,
            firstManager.OrganizationId,
            OrganizationRole.Teacher);
        var secondTeacher = await factory.CreateMembershipAsync(
            teacherUser.User.Id,
            secondManager.OrganizationId,
            OrganizationRole.Teacher);
        await factory.CreateTeacherAssignmentAsync(
            firstManager.OrganizationId,
            firstClass.ClassId,
            firstTeacher.MembershipId);
        await factory.CreateTeacherAssignmentAsync(
            secondManager.OrganizationId,
            secondClass.ClassId,
            secondTeacher.MembershipId);
        var startUtc = FutureUtc(3);
        using var firstResponse = await CreateSessionAsync(
            firstManagerClient,
            firstClass.ClassId,
            startUtc,
            startUtc.AddHours(1));
        firstResponse.EnsureSuccessStatusCode();

        using var classConflict = await CreateSessionAsync(
            firstManagerClient,
            firstClass.ClassId,
            startUtc.AddMinutes(10),
            startUtc.AddMinutes(40));
        using var teacherConflict = await CreateSessionAsync(
            secondManagerClient,
            secondClass.ClassId,
            startUtc.AddMinutes(15),
            startUtc.AddMinutes(45));
        var teacherProblem = await teacherConflict.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Conflict, classConflict.StatusCode);
        Assert.Equal(AcademicErrorCodes.ClassSessionConflict, await ReadProblemCodeAsync(classConflict));
        Assert.Equal(HttpStatusCode.Conflict, teacherConflict.StatusCode);
        Assert.Contains(AcademicErrorCodes.TeacherSessionConflict, teacherProblem);
        Assert.DoesNotContain(firstManager.OrganizationName, teacherProblem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CancellingSessionPreservesRowAndCreatesNoAutomaticAbsence()
    {
        using var client = factory.CreateClient();
        var manager = await CreateManagerAsync(client);
        var academicClass = await factory.CreateAcademicClassAsync(manager.OrganizationId);
        var startUtc = FutureUtc(1);
        using var createResponse = await CreateSessionAsync(
            client,
            academicClass.ClassId,
            startUtc,
            startUtc.AddHours(1));
        var session = await ReadRequiredAsync<SessionResult>(createResponse);

        using var cancelResponse = await client.PostAsJsonAsync(
            $"/api/v1/academic/sessions/{session.Id}/cancel",
            new SessionVersionCommand(session.RowVersion),
            JsonOptions);
        var cancelled = await ReadRequiredAsync<SessionResult>(cancelResponse);

        Assert.Equal(SessionStatus.Cancelled, cancelled.Status);
        Assert.Equal(SessionStatus.Cancelled, await factory.GetSessionStatusAsync(session.Id));
        Assert.Equal(1, await factory.CountSessionsAsync(manager.OrganizationId, academicClass.ClassId));
        Assert.Equal(0, await factory.CountAttendanceAsync(manager.OrganizationId, session.Id));
    }

    [Fact]
    public async Task AssignedTeacherRecordsAttendanceButCannotTouchUnrelatedClass()
    {
        using var managerClient = factory.CreateClient();
        var manager = await CreateManagerAsync(managerClient);
        var assignedClass = await factory.CreateAcademicClassAsync(manager.OrganizationId);
        var unrelatedClass = await factory.CreateAcademicClassAsync(manager.OrganizationId);
        var assignedLearner = Assert.Single(
            await factory.CreateOrganizationPersonsAsync(manager.OrganizationId, 1));
        var unrelatedLearner = Assert.Single(
            await factory.CreateOrganizationPersonsAsync(manager.OrganizationId, 1));
        var assignedEnrollmentId = await factory.CreateEnrollmentAsync(
            manager.OrganizationId,
            assignedClass.ClassId,
            assignedLearner);
        var unrelatedEnrollmentId = await factory.CreateEnrollmentAsync(
            manager.OrganizationId,
            unrelatedClass.ClassId,
            unrelatedLearner);
        var assignedStart = DateTime.UtcNow;
        var assignedSessionId = await factory.CreateAcademicSessionAsync(
            manager.OrganizationId,
            assignedClass.ClassId,
            assignedStart,
            assignedStart.AddHours(1));
        var unrelatedStart = DateTime.UtcNow;
        var unrelatedSessionId = await factory.CreateAcademicSessionAsync(
            manager.OrganizationId,
            unrelatedClass.ClassId,
            unrelatedStart,
            unrelatedStart.AddHours(1));
        using var teacherClient = factory.CreateClient();
        var teacherUser = await CreateAuthenticatedUserAsync(teacherClient);
        var teacher = await factory.CreateMembershipAsync(
            teacherUser.User.Id,
            manager.OrganizationId,
            OrganizationRole.Teacher);
        await factory.CreateTeacherAssignmentAsync(
            manager.OrganizationId,
            assignedClass.ClassId,
            teacher.MembershipId);
        await SelectWorkspaceAsync(
            teacherClient,
            teacherUser.AccessToken,
            teacher.MembershipId,
            OrganizationRole.Teacher);

        using var allowedResponse = await RecordAttendanceAsync(
            teacherClient,
            assignedSessionId,
            assignedEnrollmentId,
            AttendanceStatus.Present);
        var recorded = await ReadRequiredAsync<AttendanceEntryResult[]>(allowedResponse);
        using var forbiddenResponse = await RecordAttendanceAsync(
            teacherClient,
            unrelatedSessionId,
            unrelatedEnrollmentId,
            AttendanceStatus.Absent);

        Assert.Equal(HttpStatusCode.OK, allowedResponse.StatusCode);
        Assert.Equal(AttendanceStatus.Present, Assert.Single(recorded).Status);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);
        Assert.Equal(
            AcademicErrorCodes.AttendanceNotAllowed,
            await ReadProblemCodeAsync(forbiddenResponse));
    }

    [Fact]
    public async Task AttendanceRejectsOtherClassAndOtherOrganizationEnrollment()
    {
        using var client = factory.CreateClient();
        var manager = await CreateManagerAsync(client);
        var targetClass = await factory.CreateAcademicClassAsync(manager.OrganizationId);
        var otherClass = await factory.CreateAcademicClassAsync(manager.OrganizationId);
        var otherClassLearner = Assert.Single(
            await factory.CreateOrganizationPersonsAsync(manager.OrganizationId, 1));
        var otherClassEnrollmentId = await factory.CreateEnrollmentAsync(
            manager.OrganizationId,
            otherClass.ClassId,
            otherClassLearner);
        var otherOrganizationId = await factory.CreateOrganizationAsync(NewOrganizationName());
        var foreignClass = await factory.CreateAcademicClassAsync(otherOrganizationId);
        var foreignLearner = Assert.Single(
            await factory.CreateOrganizationPersonsAsync(otherOrganizationId, 1));
        var foreignEnrollmentId = await factory.CreateEnrollmentAsync(
            otherOrganizationId,
            foreignClass.ClassId,
            foreignLearner);
        var startUtc = DateTime.UtcNow;
        var sessionId = await factory.CreateAcademicSessionAsync(
            manager.OrganizationId,
            targetClass.ClassId,
            startUtc,
            startUtc.AddHours(1));

        using var otherClassResponse = await RecordAttendanceAsync(
            client,
            sessionId,
            otherClassEnrollmentId,
            AttendanceStatus.Present);
        using var foreignResponse = await RecordAttendanceAsync(
            client,
            sessionId,
            foreignEnrollmentId,
            AttendanceStatus.Present);

        Assert.Equal(HttpStatusCode.BadRequest, otherClassResponse.StatusCode);
        Assert.Equal(
            AcademicErrorCodes.AttendanceEnrollmentInvalid,
            await ReadProblemCodeAsync(otherClassResponse));
        Assert.Equal(HttpStatusCode.BadRequest, foreignResponse.StatusCode);
        Assert.True(await factory.InvalidAttendanceReferenceIsRejectedAsync(
            manager.OrganizationId,
            targetClass.ClassId,
            sessionId,
            otherClassEnrollmentId,
            manager.MembershipId));
        Assert.True(await factory.InvalidAttendanceReferenceIsRejectedAsync(
            manager.OrganizationId,
            targetClass.ClassId,
            sessionId,
            foreignEnrollmentId,
            manager.MembershipId));
    }

    [Fact]
    public async Task DuplicateAttendanceIsRejectedByApplicationAndSqlServer()
    {
        using var client = factory.CreateClient();
        var manager = await CreateManagerAsync(client);
        var academicClass = await factory.CreateAcademicClassAsync(manager.OrganizationId);
        var learnerId = Assert.Single(
            await factory.CreateOrganizationPersonsAsync(manager.OrganizationId, 1));
        var enrollmentId = await factory.CreateEnrollmentAsync(
            manager.OrganizationId,
            academicClass.ClassId,
            learnerId);
        var startUtc = DateTime.UtcNow;
        var sessionId = await factory.CreateAcademicSessionAsync(
            manager.OrganizationId,
            academicClass.ClassId,
            startUtc,
            startUtc.AddHours(1));

        using var first = await RecordAttendanceAsync(
            client,
            sessionId,
            enrollmentId,
            AttendanceStatus.Present);
        using var duplicate = await RecordAttendanceAsync(
            client,
            sessionId,
            enrollmentId,
            AttendanceStatus.Absent);
        var secondSessionId = await factory.CreateAcademicSessionAsync(
            manager.OrganizationId,
            academicClass.ClassId,
            startUtc.AddHours(-2),
            startUtc.AddHours(-1));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(
            AcademicErrorCodes.AttendanceAlreadyRecorded,
            await ReadProblemCodeAsync(duplicate));
        Assert.True(await factory.DuplicateAttendanceIsRejectedAsync(
            manager.OrganizationId,
            academicClass.ClassId,
            secondSessionId,
            enrollmentId,
            manager.MembershipId));
    }

    [Fact]
    public async Task AttendanceRosterUsesNotRecordedAndOpeningMeetingDoesNotWriteAttendance()
    {
        using var client = factory.CreateClient();
        var manager = await CreateManagerAsync(client);
        var academicClass = await factory.CreateAcademicClassAsync(manager.OrganizationId);
        var learnerId = Assert.Single(
            await factory.CreateOrganizationPersonsAsync(manager.OrganizationId, 1));
        var enrollmentId = await factory.CreateEnrollmentAsync(
            manager.OrganizationId,
            academicClass.ClassId,
            learnerId);
        var startUtc = DateTime.UtcNow;
        var sessionId = await factory.CreateAcademicSessionAsync(
            manager.OrganizationId,
            academicClass.ClassId,
            startUtc,
            startUtc.AddHours(1),
            meetingUrl: "https://meet.google.com/example");

        using var sessionResponse = await client.GetAsync($"/api/v1/academic/sessions/{sessionId}");
        var session = await ReadRequiredAsync<SessionResult>(sessionResponse);
        using var rosterResponse = await client.GetAsync(
            $"/api/v1/academic/sessions/{sessionId}/attendance");
        var roster = await ReadRequiredAsync<SessionAttendanceResult>(rosterResponse);

        Assert.Equal("https://meet.google.com/example", session.MeetingUrl);
        var entry = Assert.Single(roster.Entries);
        Assert.Equal(enrollmentId, entry.EnrollmentId);
        Assert.Equal(AttendanceStatus.NotRecorded, entry.Status);
        Assert.Null(entry.AttendanceId);
        Assert.Equal(0, await factory.CountAttendanceAsync(manager.OrganizationId, sessionId));
    }

    [Fact]
    public async Task AttendanceCorrectionRequiresReasonAndPreservesRevisionHistory()
    {
        var scenario = await CreateRecordedAttendanceScenarioAsync();
        using var missingReason = await scenario.Client.PatchAsJsonAsync(
            $"/api/v1/academic/sessions/{scenario.SessionId}/attendance/{scenario.Attendance.AttendanceId!.Value}",
            new CorrectAttendanceCommand(
                AttendanceStatus.Late,
                " ",
                scenario.Attendance.RowVersion!),
            JsonOptions);
        using var correctedResponse = await scenario.Client.PatchAsJsonAsync(
            $"/api/v1/academic/sessions/{scenario.SessionId}/attendance/{scenario.Attendance.AttendanceId!.Value}",
            new CorrectAttendanceCommand(
                AttendanceStatus.Late,
                "ورود با تأخیر ثبت شده بود",
                scenario.Attendance.RowVersion!),
            JsonOptions);
        var corrected = await ReadRequiredAsync<AttendanceEntryResult>(correctedResponse);
        using var rosterResponse = await scenario.Client.GetAsync(
            $"/api/v1/academic/sessions/{scenario.SessionId}/attendance");
        var roster = await ReadRequiredAsync<SessionAttendanceResult>(rosterResponse);

        Assert.Equal(HttpStatusCode.BadRequest, missingReason.StatusCode);
        Assert.Equal(
            AcademicErrorCodes.AttendanceCorrectionReasonRequired,
            await ReadProblemCodeAsync(missingReason));
        Assert.Equal(AttendanceStatus.Late, corrected.Status);
        var revision = Assert.Single(roster.Revisions);
        Assert.Equal(AttendanceStatus.Present, revision.PreviousStatus);
        Assert.Equal(AttendanceStatus.Late, revision.NewStatus);
        Assert.Equal(1, await factory.CountAttendanceRevisionsAsync(
            scenario.Attendance.AttendanceId!.Value));
        scenario.Dispose();
    }

    [Fact]
    public async Task ConcurrentAttendanceCorrectionsProduceOneExplicitConflict()
    {
        var scenario = await CreateRecordedAttendanceScenarioAsync();
        var attendanceId = scenario.Attendance.AttendanceId!.Value;
        var firstTask = scenario.Client.PatchAsJsonAsync(
            $"/api/v1/academic/sessions/{scenario.SessionId}/attendance/{attendanceId}",
            new CorrectAttendanceCommand(
                AttendanceStatus.Absent,
                "Correction from first editor",
                scenario.Attendance.RowVersion!),
            JsonOptions);
        var secondTask = scenario.Client.PatchAsJsonAsync(
            $"/api/v1/academic/sessions/{scenario.SessionId}/attendance/{attendanceId}",
            new CorrectAttendanceCommand(
                AttendanceStatus.Late,
                "Correction from second editor",
                scenario.Attendance.RowVersion!),
            JsonOptions);

        var responses = await Task.WhenAll(firstTask, secondTask);
        try
        {
            Assert.Single(
                responses,
                response => response.StatusCode == HttpStatusCode.OK);
            var conflict = Assert.Single(
                responses,
                response => response.StatusCode == HttpStatusCode.PreconditionFailed);
            Assert.Equal(
                OrganizationErrorCodes.ConcurrencyConflict,
                await ReadProblemCodeAsync(conflict));
            Assert.Equal(1, await factory.CountAttendanceRevisionsAsync(attendanceId));
        }
        finally
        {
            DisposeResponses(responses);
            scenario.Dispose();
        }
    }

    [Fact]
    public async Task ConcurrentSessionUpdatesProduceOneExplicitConflict()
    {
        using var client = factory.CreateClient();
        var manager = await CreateManagerAsync(client);
        var academicClass = await factory.CreateAcademicClassAsync(manager.OrganizationId);
        var startUtc = FutureUtc(4);
        using var createResponse = await CreateSessionAsync(
            client,
            academicClass.ClassId,
            startUtc,
            startUtc.AddHours(1));
        var session = await ReadRequiredAsync<SessionResult>(createResponse);
        var firstTask = client.PatchAsJsonAsync(
            $"/api/v1/academic/sessions/{session.Id}",
            new UpdateSessionCommand(
                "First editor",
                startUtc.AddMinutes(5),
                startUtc.AddHours(1).AddMinutes(5),
                "UTC",
                null,
                session.RowVersion),
            JsonOptions);
        var secondTask = client.PatchAsJsonAsync(
            $"/api/v1/academic/sessions/{session.Id}",
            new UpdateSessionCommand(
                "Second editor",
                startUtc.AddMinutes(10),
                startUtc.AddHours(1).AddMinutes(10),
                "UTC",
                null,
                session.RowVersion),
            JsonOptions);

        var responses = await Task.WhenAll(firstTask, secondTask);
        try
        {
            Assert.Single(
                responses,
                response => response.StatusCode == HttpStatusCode.OK);
            var conflict = Assert.Single(
                responses,
                response => response.StatusCode == HttpStatusCode.PreconditionFailed);
            Assert.Equal(
                OrganizationErrorCodes.ConcurrencyConflict,
                await ReadProblemCodeAsync(conflict));
        }
        finally
        {
            DisposeResponses(responses);
        }
    }

    [Fact]
    public async Task RevokedTeacherAssignmentStopsSessionManagement()
    {
        using var managerClient = factory.CreateClient();
        var manager = await CreateManagerAsync(managerClient);
        var academicClass = await factory.CreateAcademicClassAsync(manager.OrganizationId);
        using var teacherClient = factory.CreateClient();
        var teacherUser = await CreateAuthenticatedUserAsync(teacherClient);
        var teacher = await factory.CreateMembershipAsync(
            teacherUser.User.Id,
            manager.OrganizationId,
            OrganizationRole.Teacher);
        var assignmentId = await factory.CreateTeacherAssignmentAsync(
            manager.OrganizationId,
            academicClass.ClassId,
            teacher.MembershipId);
        await SelectWorkspaceAsync(
            teacherClient,
            teacherUser.AccessToken,
            teacher.MembershipId,
            OrganizationRole.Teacher);
        var startUtc = FutureUtc(5);
        using var createResponse = await CreateSessionAsync(
            managerClient,
            academicClass.ClassId,
            startUtc,
            startUtc.AddHours(1));
        var session = await ReadRequiredAsync<SessionResult>(createResponse);
        using var endResponse = await managerClient.PostAsync(
            $"/api/v1/academic/classes/{academicClass.ClassId}/teachers/{assignmentId}/end",
            null);
        endResponse.EnsureSuccessStatusCode();

        using var updateResponse = await teacherClient.PatchAsJsonAsync(
            $"/api/v1/academic/sessions/{session.Id}",
            new UpdateSessionCommand(
                "Denied",
                startUtc.AddMinutes(5),
                startUtc.AddHours(1).AddMinutes(5),
                "UTC",
                null,
                session.RowVersion),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, updateResponse.StatusCode);
        Assert.Equal(
            AcademicErrorCodes.TeacherNotAllowed,
            await ReadProblemCodeAsync(updateResponse));
    }

    [Fact]
    public async Task EndedEnrollmentRemovesSessionFromStudentSchedule()
    {
        using var managerClient = factory.CreateClient();
        var manager = await CreateManagerAsync(managerClient);
        var academicClass = await factory.CreateAcademicClassAsync(manager.OrganizationId);
        using var studentClient = factory.CreateClient();
        var studentUser = await CreateAuthenticatedUserAsync(studentClient);
        await CompleteProfileAsync(studentClient, studentUser);
        var personId = await factory.GetUserPersonIdAsync(studentUser.User.Id);
        var learnerId = await factory.CreateOrganizationPersonAsync(
            manager.OrganizationId,
            personId);
        var student = await factory.CreateMembershipAsync(
            studentUser.User.Id,
            manager.OrganizationId,
            OrganizationRole.Student);
        var enrollmentId = await factory.CreateEnrollmentAsync(
            manager.OrganizationId,
            academicClass.ClassId,
            learnerId);
        var startUtc = FutureUtc(2);
        var sessionId = await factory.CreateAcademicSessionAsync(
            manager.OrganizationId,
            academicClass.ClassId,
            startUtc,
            startUtc.AddHours(1));
        await SelectWorkspaceAsync(
            studentClient,
            studentUser.AccessToken,
            student.MembershipId,
            OrganizationRole.Student);
        Assert.Contains(
            await GetScheduleAsync(studentClient, startUtc.AddHours(-1), startUtc.AddHours(2)),
            item => item.Id == sessionId);

        using var endResponse = await managerClient.PostAsJsonAsync(
            $"/api/v1/academic/classes/{academicClass.ClassId}/enrollments/{enrollmentId}/end",
            new EndEnrollmentCommand(EnrollmentStatus.Withdrawn),
            JsonOptions);
        endResponse.EnsureSuccessStatusCode();

        Assert.DoesNotContain(
            await GetScheduleAsync(studentClient, startUtc.AddHours(-1), startUtc.AddHours(2)),
            item => item.Id == sessionId);
    }

    [Fact]
    public async Task RequestOrganizationAndMembershipIdsCannotOverrideAccessContext()
    {
        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();
        var firstManager = await CreateManagerAsync(firstClient);
        var secondManager = await CreateManagerAsync(secondClient);
        var academicClass = await factory.CreateAcademicClassAsync(
            firstManager.OrganizationId);
        var startUtc = FutureUtc(3);

        using var response = await firstClient.PostAsJsonAsync(
            $"/api/v1/academic/classes/{academicClass.ClassId}/sessions",
            new
            {
                OrganizationId = secondManager.OrganizationId,
                MembershipId = secondManager.MembershipId,
                Title = "Context-owned session",
                StartUtc = startUtc,
                EndUtc = startUtc.AddHours(1),
                TimeZoneId = "UTC",
                MeetingUrl = (string?)null
            },
            JsonOptions);
        var session = await ReadRequiredAsync<SessionResult>(response);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(firstManager.OrganizationId, session.OrganizationId);
        Assert.NotEqual(secondManager.OrganizationId, session.OrganizationId);
    }

    [Fact]
    public async Task WrongOrganizationWorkspaceCannotReadSessionByTamperedId()
    {
        using var firstClient = factory.CreateClient();
        using var secondClient = factory.CreateClient();
        var firstManager = await CreateManagerAsync(firstClient);
        _ = await CreateManagerAsync(secondClient);
        var academicClass = await factory.CreateAcademicClassAsync(
            firstManager.OrganizationId);
        var startUtc = FutureUtc(2);
        var sessionId = await factory.CreateAcademicSessionAsync(
            firstManager.OrganizationId,
            academicClass.ClassId,
            startUtc,
            startUtc.AddHours(1));

        using var response = await secondClient.GetAsync(
            $"/api/v1/academic/sessions/{sessionId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(
            AcademicErrorCodes.SessionNotFound,
            await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task UpdatingScheduleRulePreservesOldFutureOccurrencesAsCancelledHistory()
    {
        using var client = factory.CreateClient();
        var manager = await CreateManagerAsync(client);
        var academicClass = await factory.CreateAcademicClassAsync(manager.OrganizationId);
        var effectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));
        var effectiveUntil = effectiveFrom.AddDays(14);
        var localDay = effectiveFrom.DayOfWeek;
        using var createResponse = await client.PostAsJsonAsync(
            $"/api/v1/academic/classes/{academicClass.ClassId}/schedule-rules",
            new CreateScheduleRuleCommand(
                localDay,
                new TimeOnly(10, 0),
                60,
                "UTC",
                effectiveFrom,
                effectiveUntil,
                "Recurring session",
                null),
            JsonOptions);
        var rule = await ReadRequiredAsync<ScheduleRuleResult>(createResponse);
        var fromUtc = effectiveFrom.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toUtc = effectiveUntil.AddDays(1).ToDateTime(
            TimeOnly.MinValue,
            DateTimeKind.Utc);
        var initialOccurrences = (await GetScheduleAsync(client, fromUtc, toUtc))
            .Where(item => item.ScheduleRuleId == rule.Id)
            .ToArray();

        using var updateResponse = await client.PatchAsJsonAsync(
            $"/api/v1/academic/schedule-rules/{rule.Id}",
            new UpdateScheduleRuleCommand(
                localDay,
                new TimeOnly(11, 0),
                60,
                "UTC",
                effectiveFrom,
                effectiveUntil,
                effectiveFrom,
                "Updated recurring session",
                null,
                rule.RowVersion),
            JsonOptions);
        var updatedRule = await ReadRequiredAsync<ScheduleRuleResult>(updateResponse);
        var updatedSchedule = await GetScheduleAsync(client, fromUtc, toUtc);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        Assert.True(rule.GeneratedSessionCount >= 2);
        Assert.Equal(rule.GeneratedSessionCount, initialOccurrences.Length);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.Equal(rule.GeneratedSessionCount, updatedRule.GeneratedSessionCount);
        Assert.All(initialOccurrences, oldOccurrence => Assert.Contains(
            updatedSchedule,
            current => current.Id == oldOccurrence.Id &&
                       current.Status == SessionStatus.Cancelled));
        Assert.Equal(
            updatedRule.GeneratedSessionCount,
            updatedSchedule.Count(item =>
                item.ScheduleRuleId == rule.Id &&
                item.Status == SessionStatus.Scheduled));
    }

    private async Task<RecordedAttendanceScenario> CreateRecordedAttendanceScenarioAsync()
    {
        var client = factory.CreateClient();
        var manager = await CreateManagerAsync(client);
        var academicClass = await factory.CreateAcademicClassAsync(manager.OrganizationId);
        var learnerId = Assert.Single(
            await factory.CreateOrganizationPersonsAsync(manager.OrganizationId, 1));
        var enrollmentId = await factory.CreateEnrollmentAsync(
            manager.OrganizationId,
            academicClass.ClassId,
            learnerId);
        var startUtc = DateTime.UtcNow;
        var sessionId = await factory.CreateAcademicSessionAsync(
            manager.OrganizationId,
            academicClass.ClassId,
            startUtc,
            startUtc.AddHours(1));
        using var response = await RecordAttendanceAsync(
            client,
            sessionId,
            enrollmentId,
            AttendanceStatus.Present);
        var attendance = Assert.Single(
            await ReadRequiredAsync<AttendanceEntryResult[]>(response));
        return new RecordedAttendanceScenario(client, sessionId, attendance);
    }

    private async Task<ManagerContext> CreateManagerAsync(HttpClient client)
    {
        var user = await CreateAuthenticatedUserAsync(client);
        var organizationName = NewOrganizationName();
        var organizationId = await factory.CreateOrganizationAsync(organizationName);
        var membership = await factory.CreateMembershipAsync(
            user.User.Id,
            organizationId,
            OrganizationRole.Manager);
        await SelectWorkspaceAsync(
            client,
            user.AccessToken,
            membership.MembershipId,
            OrganizationRole.Manager);
        return new ManagerContext(
            user.User.Id,
            user.AccessToken,
            organizationId,
            organizationName,
            membership.MembershipId);
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
                factory.GetOtpCode(challenge.ChallengeId)),
            JsonOptions);
        verifyResponse.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<VerifyOtpResult>(verifyResponse);
    }

    private static async Task CompleteProfileAsync(HttpClient client, VerifyOtpResult user)
    {
        UseBearerToken(client, user.AccessToken);
        var username = "student." + Guid.NewGuid().ToString("N")[..12];
        using var response = await client.PatchAsJsonAsync(
            "/api/v1/me/profile",
            new CompleteProfileCommand("Student", "User", "Student User", username),
            JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    private static async Task SelectWorkspaceAsync(
        HttpClient client,
        string accessToken,
        Guid membershipId,
        OrganizationRole role)
    {
        UseBearerToken(client, accessToken);
        using var response = await client.PostAsJsonAsync(
            "/api/v1/workspaces/select",
            new SelectWorkspaceCommand(WorkspaceType.Organization, membershipId, role),
            JsonOptions);
        response.EnsureSuccessStatusCode();
    }

    private static async Task SelectChildAsync(HttpClient client, Guid learnerId)
    {
        using var response = await client.PostAsync(
            $"/api/v1/guardian/children/{learnerId}/select",
            null);
        response.EnsureSuccessStatusCode();
    }

    private static Task<HttpResponseMessage> CreateSessionAsync(
        HttpClient client,
        Guid classId,
        DateTime startUtc,
        DateTime endUtc) =>
        client.PostAsJsonAsync(
            $"/api/v1/academic/classes/{classId}/sessions",
            new CreateSessionCommand("Session", startUtc, endUtc, "UTC", null),
            JsonOptions);

    private static Task<HttpResponseMessage> RecordAttendanceAsync(
        HttpClient client,
        Guid sessionId,
        Guid enrollmentId,
        AttendanceStatus status) =>
        client.PutAsJsonAsync(
            $"/api/v1/academic/sessions/{sessionId}/attendance",
            new RecordAttendanceCommand(
                [new RecordAttendanceEntryCommand(enrollmentId, status)]),
            JsonOptions);

    private static async Task<SessionResult[]> GetScheduleAsync(
        HttpClient client,
        DateTime fromUtc,
        DateTime toUtc) =>
        (await client.GetFromJsonAsync<SessionResult[]>(
            ScheduleUrl(fromUtc, toUtc),
            JsonOptions))!;

    private static string ScheduleUrl(DateTime fromUtc, DateTime toUtc) =>
        $"/api/v1/academic/schedule?fromUtc={Uri.EscapeDataString(fromUtc.ToString("O"))}" +
        $"&toUtc={Uri.EscapeDataString(toUtc.ToString("O"))}";

    private static async Task<T> ReadRequiredAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(JsonOptions))!;

    private static async Task<string?> ReadProblemCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("code").GetString();
    }

    private static void UseBearerToken(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

    private static void DisposeResponses(IEnumerable<HttpResponseMessage> responses)
    {
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    private static DateTime FutureUtc(int days) => DateTime.UtcNow.AddDays(days);

    private static string NewOrganizationName() =>
        $"Session Test {Guid.NewGuid():N}";

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
        string OrganizationName,
        Guid MembershipId);

    private sealed record RecordedAttendanceScenario(
        HttpClient Client,
        Guid SessionId,
        AttendanceEntryResult Attendance) : IDisposable
    {
        public void Dispose() => Client.Dispose();
    }
}
