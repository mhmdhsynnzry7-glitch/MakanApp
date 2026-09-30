using MakanApp.Domain.Academic;
using Xunit;

namespace MakanApp.UnitTests.Academic;

public sealed class SessionAttendanceModelTests
{
    [Fact]
    public void SessionRejectsEndBeforeStart()
    {
        var startUtc = UtcNow().AddDays(1);

        Assert.Throws<ArgumentException>(() => Session.CreateScheduled(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "جلسه اول",
            startUtc,
            startUtc.AddMinutes(-1),
            "UTC",
            null,
            UtcNow()));
    }

    [Fact]
    public void CancelledSessionPreservesRecordAndCannotBeEdited()
    {
        var session = CreateSession();
        var sessionId = session.Id;
        var startUtc = session.StartUtc;

        session.Cancel(UtcNow());

        Assert.Equal(sessionId, session.Id);
        Assert.Equal(startUtc, session.StartUtc);
        Assert.Equal(SessionStatus.Cancelled, session.Status);
        Assert.NotNull(session.CancelledAtUtc);
        Assert.Throws<InvalidOperationException>(() => session.Update(
            "عنوان جدید",
            startUtc.AddHours(1),
            startUtc.AddHours(2),
            "UTC",
            null,
            UtcNow()));
    }

    [Fact]
    public void NotRecordedIsDistinctFromAbsentAndIsNotPersistedAsAttendance()
    {
        Assert.NotEqual(AttendanceStatus.NotRecorded, AttendanceStatus.Absent);
        Assert.Throws<ArgumentException>(() => Attendance.Record(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            AttendanceStatus.NotRecorded,
            UtcNow(),
            Guid.NewGuid()));
    }

    [Fact]
    public void CompletedSessionPreservesHistoricalTiming()
    {
        var session = CreateSession();
        var sessionId = session.Id;
        var startUtc = session.StartUtc;
        var endUtc = session.EndUtc;

        session.Complete(endUtc);

        Assert.Equal(sessionId, session.Id);
        Assert.Equal(startUtc, session.StartUtc);
        Assert.Equal(endUtc, session.EndUtc);
        Assert.Equal(SessionStatus.Completed, session.Status);
        Assert.NotNull(session.CompletedAtUtc);
    }

    [Fact]
    public void AttendanceCorrectionChangesStatusAndCreatesRevision()
    {
        var attendance = CreateAttendance(AttendanceStatus.Present);
        var actorMembershipId = Guid.NewGuid();

        var revision = attendance.Correct(
            AttendanceStatus.Late,
            "ثبت اولیه اشتباه بود",
            UtcNow(),
            actorMembershipId);

        Assert.Equal(AttendanceStatus.Late, attendance.Status);
        Assert.Equal(AttendanceStatus.Present, revision.PreviousStatus);
        Assert.Equal(AttendanceStatus.Late, revision.NewStatus);
        Assert.Equal(actorMembershipId, revision.CorrectedByMembershipId);
        Assert.Equal("ثبت اولیه اشتباه بود", revision.Reason);
    }

    [Fact]
    public void AttendanceCorrectionRequiresReason()
    {
        var attendance = CreateAttendance(AttendanceStatus.Absent);

        Assert.Throws<ArgumentException>(() => attendance.Correct(
            AttendanceStatus.Excused,
            " ",
            UtcNow(),
            Guid.NewGuid()));
    }

    [Fact]
    public void UpdatingScheduleRuleDoesNotRewriteExistingSession()
    {
        var from = DateOnly.FromDateTime(UtcNow().AddDays(2));
        var oldDay = from.DayOfWeek;
        var rule = ScheduleRule.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            oldDay,
            new TimeOnly(8, 0),
            60,
            "UTC",
            from,
            from.AddDays(14),
            "برنامه قدیم",
            null,
            UtcNow());
        var range = rule.GetUtcRange(rule.GetOccurrenceDates(from).First());
        var historicalSession = Session.CreateScheduled(
            rule.OrganizationId,
            rule.ClassId,
            rule.SessionTitle,
            range.StartUtc,
            range.EndUtc,
            rule.TimeZoneId,
            null,
            UtcNow(),
            rule.Id,
            from);

        rule.UpdateForFuture(
            from.AddDays(1).DayOfWeek,
            new TimeOnly(10, 0),
            90,
            "UTC",
            from,
            from.AddDays(14),
            "برنامه جدید",
            null);

        Assert.Equal("برنامه قدیم", historicalSession.Title);
        Assert.Equal(range.StartUtc, historicalSession.StartUtc);
        Assert.Equal("برنامه جدید", rule.SessionTitle);
    }

    [Fact]
    public void ScheduleRuleRejectsInvalidDayOfWeek()
    {
        var from = DateOnly.FromDateTime(UtcNow().AddDays(2));

        Assert.Throws<ArgumentOutOfRangeException>(() => ScheduleRule.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            (DayOfWeek)99,
            new TimeOnly(8, 0),
            60,
            "UTC",
            from,
            from.AddDays(7),
            "Invalid recurrence",
            null,
            UtcNow()));
    }

    private static Session CreateSession()
    {
        var startUtc = UtcNow().AddDays(1);
        return Session.CreateScheduled(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "جلسه",
            startUtc,
            startUtc.AddHours(1),
            "UTC",
            "https://meet.google.com/example",
            UtcNow());
    }

    private static Attendance CreateAttendance(AttendanceStatus status) =>
        Attendance.Record(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            status,
            UtcNow(),
            Guid.NewGuid());

    private static DateTime UtcNow() => DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Utc);
}
