using MakanApp.Domain.Academic;

namespace MakanApp.Application.Academic;

public sealed partial class AcademicSessionService
{
    public async Task<ScheduleRuleResult> CreateScheduleRuleAsync(
        Guid userId,
        Guid userSessionId,
        Guid classId,
        CreateScheduleRuleCommand command,
        CancellationToken cancellationToken)
    {
        var context = await GetOrganizationContextAsync(userId, userSessionId, cancellationToken);
        await using var transaction = await _store.BeginSerializableTransactionAsync(cancellationToken);
        _ = await GetActiveClassForUpdateAsync(
            context.OrganizationId!.Value,
            classId,
            cancellationToken);
        await EnsureCanManageClassAsync(context, classId, cancellationToken);
        var rule = CreateScheduleRuleEntity(context.OrganizationId.Value, classId, command);
        var sessions = await CreateRuleSessionsAsync(rule, rule.EffectiveFrom, cancellationToken);

        _store.Add(rule);
        foreach (var session in sessions)
        {
            _store.Add(session);
        }

        await _store.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResult(rule, sessions.Count);
    }

    public async Task<ScheduleRuleResult> UpdateScheduleRuleAsync(
        Guid userId,
        Guid userSessionId,
        Guid scheduleRuleId,
        UpdateScheduleRuleCommand command,
        CancellationToken cancellationToken)
    {
        var context = await GetOrganizationContextAsync(userId, userSessionId, cancellationToken);
        await using var transaction = await _store.BeginSerializableTransactionAsync(cancellationToken);
        var rule = await _store.GetScheduleRuleForUpdateAsync(
            context.OrganizationId!.Value,
            scheduleRuleId,
            cancellationToken);
        if (rule is null)
        {
            throw new AcademicException(
                AcademicErrorCodes.ScheduleRuleNotFound,
                "برنامه تکرارشونده پیدا نشد.");
        }

        await EnsureCanManageClassAsync(context, rule.ClassId, cancellationToken);
        ValidateApplyFromDate(command.ApplyFromDate, command.TimeZoneId);
        _store.SetOriginalRowVersion(rule, DecodeRowVersion(command.ExpectedRowVersion));
        var oldFutureSessions = await _store.GetFutureRuleSessionsAsync(
            context.OrganizationId.Value,
            rule.Id,
            command.ApplyFromDate,
            UtcNow(),
            cancellationToken);
        foreach (var session in oldFutureSessions)
        {
            session.Cancel(UtcNow());
        }

        try
        {
            rule.UpdateForFuture(
                command.LocalDayOfWeek,
                command.LocalStartTime,
                command.DurationMinutes,
                command.TimeZoneId,
                command.EffectiveFrom,
                command.EffectiveUntil,
                command.SessionTitle,
                command.MeetingUrl);
        }
        catch (Exception exception) when (IsSessionValidationException(exception))
        {
            throw new AcademicException(AcademicErrorCodes.SessionTimeInvalid, exception.Message);
        }

        await _store.SaveChangesAsync(cancellationToken);
        var newSessions = await CreateRuleSessionsAsync(rule, command.ApplyFromDate, cancellationToken);
        foreach (var session in newSessions)
        {
            _store.Add(session);
        }

        await _store.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResult(rule, newSessions.Count);
    }

    private async Task<List<Session>> CreateRuleSessionsAsync(
        ScheduleRule rule,
        DateOnly fromDate,
        CancellationToken cancellationToken)
    {
        var sessions = new List<Session>();
        foreach (var occurrenceDate in rule.GetOccurrenceDates(fromDate))
        {
            (DateTime StartUtc, DateTime EndUtc) range;
            try
            {
                range = rule.GetUtcRange(occurrenceDate);
            }
            catch (InvalidOperationException exception)
            {
                throw new AcademicException(AcademicErrorCodes.SessionTimeInvalid, exception.Message);
            }

            if (range.StartUtc <= UtcNow())
            {
                continue;
            }

            var session = Session.CreateScheduled(
                rule.OrganizationId,
                rule.ClassId,
                rule.SessionTitle,
                range.StartUtc,
                range.EndUtc,
                rule.TimeZoneId,
                rule.MeetingUrl,
                UtcNow(),
                rule.Id,
                occurrenceDate);
            await EnsureNoTimeConflictAsync(session, null, cancellationToken);
            sessions.Add(session);
        }

        return sessions;
    }

    private ScheduleRule CreateScheduleRuleEntity(
        Guid organizationId,
        Guid classId,
        CreateScheduleRuleCommand command)
    {
        ValidateApplyFromDate(command.EffectiveFrom, command.TimeZoneId);
        try
        {
            return ScheduleRule.Create(
                organizationId,
                classId,
                command.LocalDayOfWeek,
                command.LocalStartTime,
                command.DurationMinutes,
                command.TimeZoneId,
                command.EffectiveFrom,
                command.EffectiveUntil,
                command.SessionTitle,
                command.MeetingUrl,
                UtcNow());
        }
        catch (Exception exception) when (IsSessionValidationException(exception))
        {
            throw new AcademicException(AcademicErrorCodes.SessionTimeInvalid, exception.Message);
        }
    }

    private void ValidateApplyFromDate(DateOnly applyFromDate, string timeZoneId)
    {
        try
        {
            var localToday = DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTimeFromUtc(
                    UtcNow(),
                    TimeZoneInfo.FindSystemTimeZoneById(timeZoneId)));
            if (applyFromDate < localToday)
            {
                throw new AcademicException(
                    AcademicErrorCodes.SessionTimeInvalid,
                    "تغییر برنامه فقط از امروز یا آینده مجاز است.");
            }
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new AcademicException(AcademicErrorCodes.SessionTimeInvalid, exception.Message);
        }
    }

    private static ScheduleRuleResult ToResult(ScheduleRule rule, int generatedSessionCount) =>
        new(
            rule.Id,
            rule.OrganizationId,
            rule.ClassId,
            rule.LocalDayOfWeek,
            rule.LocalStartTime,
            rule.DurationMinutes,
            rule.TimeZoneId,
            rule.EffectiveFrom,
            rule.EffectiveUntil,
            rule.SessionTitle,
            rule.MeetingUrl,
            rule.Status,
            generatedSessionCount,
            Convert.ToBase64String(rule.RowVersion));
}
