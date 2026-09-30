using MakanApp.Domain.Academic;

namespace MakanApp.Application.Academic;

public sealed partial class AcademicSessionService
{
    public async Task<IReadOnlyCollection<AttendanceEntryResult>> RecordAttendanceAsync(
        Guid userId,
        Guid userSessionId,
        Guid sessionId,
        RecordAttendanceCommand command,
        CancellationToken cancellationToken)
    {
        var context = await GetOrganizationContextAsync(userId, userSessionId, cancellationToken);
        if (command.Entries.Count == 0 ||
            command.Entries.Select(entry => entry.EnrollmentId).Distinct().Count() != command.Entries.Count)
        {
            throw new AcademicException(
                AcademicErrorCodes.AttendanceEnrollmentInvalid,
                "فهرست ثبت حضور معتبر نیست.");
        }

        await using var transaction = await _store.BeginSerializableTransactionAsync(cancellationToken);
        var record = await GetSessionForUpdateAsync(
            context.OrganizationId!.Value,
            sessionId,
            cancellationToken);
        await EnsureCanManageAttendanceAsync(context, record.Session.ClassId, cancellationToken);
        EnsureSessionAllowsAttendance(record.Session);
        var recordedAttendance = new List<(Attendance Attendance, Guid LearnerId)>(
            command.Entries.Count);
        foreach (var entry in command.Entries)
        {
            var enrollment = await _store.GetEnrollmentForAttendanceAsync(
                context.OrganizationId.Value,
                record.Session.ClassId,
                entry.EnrollmentId,
                cancellationToken);
            if (enrollment is null || !EnrollmentCoversSession(enrollment, record.Session.StartUtc))
            {
                throw new AcademicException(
                    AcademicErrorCodes.AttendanceEnrollmentInvalid,
                    "ثبت‌نام معتبر برای این جلسه پیدا نشد.");
            }

            if (await _store.GetAttendanceAsync(
                    context.OrganizationId.Value,
                    sessionId,
                    enrollment.Id,
                    cancellationToken) is not null)
            {
                throw new AcademicException(
                    AcademicErrorCodes.AttendanceAlreadyRecorded,
                    "حضور این ثبت‌نام قبلاً ثبت شده است.");
            }

            Attendance attendance;
            try
            {
                attendance = Attendance.Record(
                    context.OrganizationId.Value,
                    record.Session.ClassId,
                    sessionId,
                    enrollment.Id,
                    entry.Status,
                    UtcNow(),
                    context.MembershipId!.Value);
            }
            catch (ArgumentException exception)
            {
                throw new AcademicException(
                    AcademicErrorCodes.AttendanceEnrollmentInvalid,
                    exception.Message);
            }

            _store.Add(attendance);
            recordedAttendance.Add((attendance, enrollment.LearnerOrganizationPersonId));
        }

        await _store.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return recordedAttendance
            .Select(item => ToResult(item.Attendance, item.LearnerId))
            .ToArray();
    }

    public async Task<AttendanceEntryResult> CorrectAttendanceAsync(
        Guid userId,
        Guid userSessionId,
        Guid sessionId,
        Guid attendanceId,
        CorrectAttendanceCommand command,
        CancellationToken cancellationToken)
    {
        var context = await GetOrganizationContextAsync(userId, userSessionId, cancellationToken);
        await using var transaction = await _store.BeginSerializableTransactionAsync(cancellationToken);
        var sessionRecord = await GetSessionForUpdateAsync(
            context.OrganizationId!.Value,
            sessionId,
            cancellationToken);
        await EnsureCanManageAttendanceAsync(
            context,
            sessionRecord.Session.ClassId,
            cancellationToken);
        if (sessionRecord.Session.Status == SessionStatus.Cancelled)
        {
            throw new AcademicException(
                AcademicErrorCodes.SessionCancelled,
                "حضور جلسه لغوشده قابل اصلاح نیست.");
        }

        var attendance = await _store.GetAttendanceForUpdateAsync(
            context.OrganizationId.Value,
            sessionId,
            attendanceId,
            cancellationToken);
        if (attendance is null)
        {
            throw new AcademicException(
                AcademicErrorCodes.AttendanceEnrollmentInvalid,
                "رکورد حضور پیدا نشد.");
        }

        _store.SetOriginalRowVersion(attendance, DecodeRowVersion(command.ExpectedRowVersion));
        AttendanceRevision revision;
        try
        {
            revision = attendance.Correct(
                command.Status,
                command.CorrectionReason,
                UtcNow(),
                context.MembershipId!.Value);
        }
        catch (ArgumentException exception)
        {
            throw new AcademicException(
                AcademicErrorCodes.AttendanceCorrectionReasonRequired,
                exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            throw new AcademicException(
                AcademicErrorCodes.AttendanceAlreadyChanged,
                exception.Message);
        }

        _store.Add(revision);
        await _store.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var enrollment = await _store.GetEnrollmentForAttendanceAsync(
            context.OrganizationId.Value,
            attendance.ClassId,
            attendance.EnrollmentId,
            cancellationToken);
        return ToResult(attendance, enrollment!.LearnerOrganizationPersonId);
    }

    public async Task<SessionAttendanceResult> GetSessionAttendanceAsync(
        Guid userId,
        Guid userSessionId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var context = await GetOrganizationContextAsync(userId, userSessionId, cancellationToken);
        var sessionRecord = await _store.GetSessionAsync(
            context.OrganizationId!.Value,
            sessionId,
            cancellationToken);
        if (sessionRecord is null)
        {
            throw SessionNotFound();
        }

        await EnsureCanManageAttendanceAsync(context, sessionRecord.Session.ClassId, cancellationToken);
        var roster = await _store.GetAttendanceRosterAsync(
            context.OrganizationId.Value,
            sessionRecord.Session.ClassId,
            sessionId,
            sessionRecord.Session.StartUtc,
            cancellationToken);
        var revisions = await _store.GetAttendanceRevisionsAsync(
            context.OrganizationId.Value,
            sessionId,
            cancellationToken);
        return new SessionAttendanceResult(
            sessionId,
            sessionRecord.Session.Status,
            roster.Select(ToResult).ToArray(),
            revisions.Select(ToResult).ToArray());
    }

    private void EnsureSessionAllowsAttendance(Session session)
    {
        if (session.Status == SessionStatus.Cancelled)
        {
            throw new AcademicException(
                AcademicErrorCodes.SessionCancelled,
                "برای جلسه لغوشده حضور ثبت نمی‌شود.");
        }

        if (session.StartUtc > UtcNow())
        {
            throw new AcademicException(
                AcademicErrorCodes.SessionNotActive,
                "پیش از شروع جلسه نمی‌توان حضور ثبت کرد.");
        }
    }

    private static bool EnrollmentCoversSession(Enrollment enrollment, DateTime sessionStartUtc) =>
        enrollment.EnrolledAtUtc <= sessionStartUtc &&
        (!enrollment.EndedAtUtc.HasValue || enrollment.EndedAtUtc.Value >= sessionStartUtc);

    private static AttendanceEntryResult ToResult(
        Attendance attendance,
        Guid learnerOrganizationPersonId) =>
        new(
            attendance.Id,
            attendance.EnrollmentId,
            learnerOrganizationPersonId,
            attendance.Status,
            attendance.RecordedAtUtc,
            attendance.RecordedByMembershipId,
            Convert.ToBase64String(attendance.RowVersion));

    private static AttendanceEntryResult ToResult(AttendanceRosterRecord record) =>
        record.Attendance is null
            ? new AttendanceEntryResult(
                null,
                record.Enrollment.Id,
                record.Enrollment.LearnerOrganizationPersonId,
                AttendanceStatus.NotRecorded,
                null,
                null,
                null)
            : ToResult(record.Attendance, record.Enrollment.LearnerOrganizationPersonId);

    private static AttendanceRevisionResult ToResult(AttendanceRevision revision) =>
        new(
            revision.Id,
            revision.PreviousStatus,
            revision.NewStatus,
            revision.CorrectedAtUtc,
            revision.CorrectedByMembershipId,
            revision.Reason);
}
