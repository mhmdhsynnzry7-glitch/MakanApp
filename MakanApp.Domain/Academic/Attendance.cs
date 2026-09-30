namespace MakanApp.Domain.Academic;

public sealed class Attendance
{
    private Attendance()
    {
    }

    private Attendance(
        Guid id,
        Guid organizationId,
        Guid classId,
        Guid sessionId,
        Guid enrollmentId,
        AttendanceStatus status,
        DateTime recordedAtUtc,
        Guid recordedByMembershipId)
    {
        Id = id;
        OrganizationId = organizationId;
        ClassId = classId;
        SessionId = sessionId;
        EnrollmentId = enrollmentId;
        Status = status;
        RecordedAtUtc = recordedAtUtc;
        RecordedByMembershipId = recordedByMembershipId;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ClassId { get; private set; }
    public Guid SessionId { get; private set; }
    public Guid EnrollmentId { get; private set; }
    public AttendanceStatus Status { get; private set; }
    public DateTime RecordedAtUtc { get; private set; }
    public Guid RecordedByMembershipId { get; private set; }
    public byte[] RowVersion { get; private set; } = [];

    public static Attendance Record(
        Guid organizationId,
        Guid classId,
        Guid sessionId,
        Guid enrollmentId,
        AttendanceStatus status,
        DateTime recordedAtUtc,
        Guid recordedByMembershipId)
    {
        ValidateRecordedStatus(status);
        return new Attendance(
            Guid.NewGuid(),
            organizationId,
            classId,
            sessionId,
            enrollmentId,
            status,
            recordedAtUtc,
            recordedByMembershipId);
    }

    public AttendanceRevision Correct(
        AttendanceStatus newStatus,
        string reason,
        DateTime correctedAtUtc,
        Guid correctedByMembershipId)
    {
        ValidateRecordedStatus(newStatus);
        var normalizedReason = reason?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedReason) || normalizedReason.Length > 500)
        {
            throw new ArgumentException("دلیل اصلاح حضور الزامی و حداکثر ۵۰۰ نویسه است.", nameof(reason));
        }

        if (newStatus == Status)
        {
            throw new InvalidOperationException("وضعیت جدید با وضعیت فعلی یکسان است.");
        }

        var revision = AttendanceRevision.Create(
            OrganizationId,
            ClassId,
            SessionId,
            Id,
            EnrollmentId,
            Status,
            newStatus,
            correctedAtUtc,
            correctedByMembershipId,
            normalizedReason);
        Status = newStatus;
        return revision;
    }

    private static void ValidateRecordedStatus(AttendanceStatus status)
    {
        if (status == AttendanceStatus.NotRecorded || !Enum.IsDefined(status))
        {
            throw new ArgumentException("وضعیت حضور ثبت‌شدنی معتبر نیست.", nameof(status));
        }
    }
}
