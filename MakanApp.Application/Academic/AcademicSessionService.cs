using MakanApp.Application.Organization;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Organization;

namespace MakanApp.Application.Academic;

public sealed partial class AcademicSessionService : IAcademicSessionService
{
    private readonly IAccessContextResolver _accessContextResolver;
    private readonly IAcademicSessionStore _store;
    private readonly TimeProvider _timeProvider;

    public AcademicSessionService(
        IAccessContextResolver accessContextResolver,
        IAcademicSessionStore store,
        TimeProvider timeProvider)
    {
        _accessContextResolver = accessContextResolver;
        _store = store;
        _timeProvider = timeProvider;
    }

    public async Task<SessionResult> CreateSessionAsync(
        Guid userId,
        Guid userSessionId,
        Guid classId,
        CreateSessionCommand command,
        CancellationToken cancellationToken)
    {
        var context = await GetOrganizationContextAsync(userId, userSessionId, cancellationToken);
        await using var transaction = await _store.BeginSerializableTransactionAsync(cancellationToken);
        var academicClass = await GetActiveClassForUpdateAsync(
            context.OrganizationId!.Value,
            classId,
            cancellationToken);
        await EnsureCanManageClassAsync(context, classId, cancellationToken);
        var session = CreateSessionEntity(
            context.OrganizationId.Value,
            classId,
            command.Title,
            command.StartUtc,
            command.EndUtc,
            command.TimeZoneId,
            command.MeetingUrl);
        await EnsureNoTimeConflictAsync(session, null, cancellationToken);

        _store.Add(session);
        await _store.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResult(session, academicClass.Title);
    }

    public async Task<SessionResult> UpdateSessionAsync(
        Guid userId,
        Guid userSessionId,
        Guid sessionId,
        UpdateSessionCommand command,
        CancellationToken cancellationToken)
    {
        var context = await GetOrganizationContextAsync(userId, userSessionId, cancellationToken);
        await using var transaction = await _store.BeginSerializableTransactionAsync(cancellationToken);
        var record = await GetSessionForUpdateAsync(
            context.OrganizationId!.Value,
            sessionId,
            cancellationToken);
        await EnsureCanManageClassAsync(context, record.Session.ClassId, cancellationToken);
        _store.SetOriginalRowVersion(record.Session, DecodeRowVersion(command.ExpectedRowVersion));
        try
        {
            record.Session.Update(
                command.Title,
                command.StartUtc,
                command.EndUtc,
                command.TimeZoneId,
                command.MeetingUrl,
                UtcNow());
        }
        catch (Exception exception) when (IsSessionValidationException(exception))
        {
            throw new AcademicException(AcademicErrorCodes.SessionTimeInvalid, exception.Message);
        }

        await EnsureNoTimeConflictAsync(record.Session, sessionId, cancellationToken);
        await _store.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResult(record.Session, record.ClassTitle);
    }

    public Task<SessionResult> CancelSessionAsync(
        Guid userId,
        Guid userSessionId,
        Guid sessionId,
        SessionVersionCommand command,
        CancellationToken cancellationToken) =>
        ChangeSessionStatusAsync(
            userId,
            userSessionId,
            sessionId,
            command.ExpectedRowVersion,
            complete: false,
            cancellationToken);

    public Task<SessionResult> CompleteSessionAsync(
        Guid userId,
        Guid userSessionId,
        Guid sessionId,
        SessionVersionCommand command,
        CancellationToken cancellationToken) =>
        ChangeSessionStatusAsync(
            userId,
            userSessionId,
            sessionId,
            command.ExpectedRowVersion,
            complete: true,
            cancellationToken);

    public async Task<SessionResult> GetSessionAsync(
        Guid userId,
        Guid userSessionId,
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var context = await GetOrganizationContextAsync(userId, userSessionId, cancellationToken);
        var record = await _store.GetSessionAsync(
            context.OrganizationId!.Value,
            sessionId,
            cancellationToken);
        if (record is null ||
            !await CanReadClassAsync(context, record.Session.ClassId, cancellationToken))
        {
            throw SessionNotFound();
        }

        return ToResult(record.Session, record.ClassTitle);
    }

    public async Task<IReadOnlyCollection<SessionResult>> GetScheduleAsync(
        Guid userId,
        Guid userSessionId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken)
    {
        ValidateScheduleRange(fromUtc, toUtc);
        var context = await GetOrganizationContextAsync(userId, userSessionId, cancellationToken);
        var organizationId = context.OrganizationId!.Value;
        IReadOnlyCollection<SessionWithClassRecord> records = context.ActiveRole switch
        {
            OrganizationRole.Manager => await _store.GetScheduleForManagerAsync(
                organizationId,
                fromUtc,
                toUtc,
                cancellationToken),
            OrganizationRole.Teacher => await _store.GetScheduleForTeacherAsync(
                organizationId,
                context.MembershipId!.Value,
                fromUtc,
                toUtc,
                cancellationToken),
            OrganizationRole.Student => await _store.GetScheduleForStudentAsync(
                organizationId,
                userId,
                fromUtc,
                toUtc,
                cancellationToken),
            OrganizationRole.Parent when context.SubjectOrganizationPersonId.HasValue =>
                await _store.GetScheduleForLearnerAsync(
                    organizationId,
                    context.SubjectOrganizationPersonId.Value,
                    fromUtc,
                    toUtc,
                    cancellationToken),
            _ => throw SessionNotFound()
        };

        return records.Select(record => ToResult(record.Session, record.ClassTitle)).ToArray();
    }

    private async Task<SessionResult> ChangeSessionStatusAsync(
        Guid userId,
        Guid userSessionId,
        Guid sessionId,
        string expectedRowVersion,
        bool complete,
        CancellationToken cancellationToken)
    {
        var context = await GetOrganizationContextAsync(userId, userSessionId, cancellationToken);
        await using var transaction = await _store.BeginSerializableTransactionAsync(cancellationToken);
        var record = await GetSessionForUpdateAsync(
            context.OrganizationId!.Value,
            sessionId,
            cancellationToken);
        await EnsureCanManageClassAsync(context, record.Session.ClassId, cancellationToken);
        _store.SetOriginalRowVersion(record.Session, DecodeRowVersion(expectedRowVersion));
        try
        {
            if (complete)
            {
                if (record.Session.StartUtc > UtcNow())
                {
                    throw new InvalidOperationException("جلسه آینده قابل تکمیل نیست.");
                }

                record.Session.Complete(UtcNow());
            }
            else
            {
                record.Session.Cancel(UtcNow());
            }
        }
        catch (InvalidOperationException exception)
        {
            throw new AcademicException(AcademicErrorCodes.SessionNotActive, exception.Message);
        }

        await _store.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResult(record.Session, record.ClassTitle);
    }

    private async Task EnsureNoTimeConflictAsync(
        Session session,
        Guid? excludedSessionId,
        CancellationToken cancellationToken)
    {
        if (await _store.HasClassTimeConflictAsync(
                session.OrganizationId,
                session.ClassId,
                session.StartUtc,
                session.EndUtc,
                excludedSessionId,
                cancellationToken))
        {
            throw new AcademicException(
                AcademicErrorCodes.ClassSessionConflict,
                "کلاس در این بازه زمانی جلسه دیگری دارد.");
        }

        if (await _store.HasTeacherTimeConflictAsync(
                session.OrganizationId,
                session.ClassId,
                session.StartUtc,
                session.EndUtc,
                excludedSessionId,
                cancellationToken))
        {
            throw new AcademicException(
                AcademicErrorCodes.TeacherSessionConflict,
                "معلم یک جلسه متعارض دیگر دارد.");
        }
    }

    private async Task<AccessContext> GetOrganizationContextAsync(
        Guid userId,
        Guid userSessionId,
        CancellationToken cancellationToken)
    {
        var context = await _accessContextResolver.ResolveAsync(
            userId,
            userSessionId,
            cancellationToken);
        if (context.WorkspaceType != WorkspaceType.Organization ||
            !context.OrganizationId.HasValue ||
            !context.MembershipId.HasValue ||
            !context.ActiveRole.HasValue)
        {
            throw SessionNotFound();
        }

        return context;
    }

    private async Task EnsureCanManageClassAsync(
        AccessContext context,
        Guid classId,
        CancellationToken cancellationToken)
    {
        if (context.ActiveRole == OrganizationRole.Manager)
        {
            return;
        }

        if (context.ActiveRole == OrganizationRole.Teacher &&
            await _store.HasActiveTeacherAssignmentAsync(
                context.OrganizationId!.Value,
                classId,
                context.MembershipId!.Value,
                cancellationToken))
        {
            return;
        }

        throw new AcademicException(
            AcademicErrorCodes.TeacherNotAllowed,
            "مدیریت جلسه برای کلاس انتخاب‌شده مجاز نیست.");
    }

    private async Task EnsureCanManageAttendanceAsync(
        AccessContext context,
        Guid classId,
        CancellationToken cancellationToken)
    {
        if (context.ActiveRole == OrganizationRole.Manager)
        {
            return;
        }

        if (context.ActiveRole == OrganizationRole.Teacher &&
            await _store.HasActiveTeacherAssignmentAsync(
                context.OrganizationId!.Value,
                classId,
                context.MembershipId!.Value,
                cancellationToken))
        {
            return;
        }

        throw new AcademicException(
            AcademicErrorCodes.AttendanceNotAllowed,
            "ثبت یا مشاهده حضور و غیاب این کلاس مجاز نیست.");
    }

    private async Task<bool> CanReadClassAsync(
        AccessContext context,
        Guid classId,
        CancellationToken cancellationToken) =>
        context.ActiveRole switch
        {
            OrganizationRole.Manager => true,
            OrganizationRole.Teacher => await _store.HasActiveTeacherAssignmentAsync(
                context.OrganizationId!.Value,
                classId,
                context.MembershipId!.Value,
                cancellationToken),
            OrganizationRole.Student => await _store.CanStudentAccessClassAsync(
                context.OrganizationId!.Value,
                classId,
                context.UserId,
                cancellationToken),
            OrganizationRole.Parent when context.SubjectOrganizationPersonId.HasValue =>
                await _store.CanLearnerAccessClassAsync(
                    context.OrganizationId!.Value,
                    classId,
                    context.SubjectOrganizationPersonId.Value,
                    cancellationToken),
            _ => false
        };

    private async Task<Class> GetActiveClassForUpdateAsync(
        Guid organizationId,
        Guid classId,
        CancellationToken cancellationToken)
    {
        var academicClass = await _store.GetClassForUpdateAsync(
            organizationId,
            classId,
            cancellationToken);
        if (academicClass is null)
        {
            throw new AcademicException(AcademicErrorCodes.ClassNotFound, "کلاس پیدا نشد.");
        }

        if (!academicClass.IsActive)
        {
            throw new AcademicException(
                AcademicErrorCodes.ClassNotActive,
                "کلاس برای برنامه‌ریزی جلسه فعال نیست.");
        }

        return academicClass;
    }

    private async Task<SessionWithClassRecord> GetSessionForUpdateAsync(
        Guid organizationId,
        Guid sessionId,
        CancellationToken cancellationToken) =>
        await _store.GetSessionForUpdateAsync(organizationId, sessionId, cancellationToken) ??
        throw SessionNotFound();

    private Session CreateSessionEntity(
        Guid organizationId,
        Guid classId,
        string title,
        DateTime startUtc,
        DateTime endUtc,
        string timeZoneId,
        string? meetingUrl)
    {
        if (startUtc <= UtcNow())
        {
            throw new AcademicException(
                AcademicErrorCodes.SessionTimeInvalid,
                "جلسه جدید باید در آینده شروع شود.");
        }

        try
        {
            return Session.CreateScheduled(
                organizationId,
                classId,
                title,
                startUtc,
                endUtc,
                timeZoneId,
                meetingUrl,
                UtcNow());
        }
        catch (Exception exception) when (IsSessionValidationException(exception))
        {
            throw new AcademicException(AcademicErrorCodes.SessionTimeInvalid, exception.Message);
        }
    }

    private static void ValidateScheduleRange(DateTime fromUtc, DateTime toUtc)
    {
        if (fromUtc.Kind != DateTimeKind.Utc ||
            toUtc.Kind != DateTimeKind.Utc ||
            toUtc <= fromUtc ||
            toUtc > fromUtc.AddYears(1))
        {
            throw new AcademicException(
                AcademicErrorCodes.SessionTimeInvalid,
                "بازه دریافت برنامه معتبر نیست.");
        }
    }

    private static byte[] DecodeRowVersion(string value)
    {
        try
        {
            var rowVersion = Convert.FromBase64String(value);
            if (rowVersion.Length != 8)
            {
                throw new FormatException();
            }

            return rowVersion;
        }
        catch (FormatException)
        {
            throw new AcademicException(
                AcademicErrorCodes.ConcurrencyConflict,
                "نسخه رکورد معتبر نیست؛ داده را دوباره دریافت کنید.");
        }
    }

    private static bool IsSessionValidationException(Exception exception) =>
        exception is ArgumentException or InvalidOperationException or
            TimeZoneNotFoundException or InvalidTimeZoneException;

    private DateTime UtcNow() => _timeProvider.GetUtcNow().UtcDateTime;

    private static AcademicException SessionNotFound() =>
        new(AcademicErrorCodes.SessionNotFound, "جلسه پیدا نشد.");

    private static SessionResult ToResult(Session session, string classTitle) =>
        new(
            session.Id,
            session.OrganizationId,
            session.ClassId,
            classTitle,
            session.ScheduleRuleId,
            session.Title,
            session.StartUtc,
            session.EndUtc,
            session.TimeZoneId,
            session.MeetingUrl,
            session.Status,
            session.CancelledAtUtc,
            session.CompletedAtUtc,
            Convert.ToBase64String(session.RowVersion));
}
