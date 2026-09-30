namespace MakanApp.Domain.Academic;

public sealed class AttendanceRevision
{
    private AttendanceRevision()
    {
    }

    private AttendanceRevision(
        Guid id,
        Guid organizationId,
        Guid classId,
        Guid sessionId,
        Guid attendanceId,
        Guid enrollmentId,
        AttendanceStatus previousStatus,
        AttendanceStatus newStatus,
        DateTime correctedAtUtc,
        Guid correctedByMembershipId,
        string reason)
    {
        Id = id;
        OrganizationId = organizationId;
        ClassId = classId;
        SessionId = sessionId;
        AttendanceId = attendanceId;
        EnrollmentId = enrollmentId;
        PreviousStatus = previousStatus;
        NewStatus = newStatus;
        CorrectedAtUtc = correctedAtUtc;
        CorrectedByMembershipId = correctedByMembershipId;
        Reason = reason;
    }

    public Guid Id { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid ClassId { get; private set; }
    public Guid SessionId { get; private set; }
    public Guid AttendanceId { get; private set; }
    public Guid EnrollmentId { get; private set; }
    public AttendanceStatus PreviousStatus { get; private set; }
    public AttendanceStatus NewStatus { get; private set; }
    public DateTime CorrectedAtUtc { get; private set; }
    public Guid CorrectedByMembershipId { get; private set; }
    public string Reason { get; private set; } = string.Empty;

    internal static AttendanceRevision Create(
        Guid organizationId,
        Guid classId,
        Guid sessionId,
        Guid attendanceId,
        Guid enrollmentId,
        AttendanceStatus previousStatus,
        AttendanceStatus newStatus,
        DateTime correctedAtUtc,
        Guid correctedByMembershipId,
        string reason) =>
        new(
            Guid.NewGuid(),
            organizationId,
            classId,
            sessionId,
            attendanceId,
            enrollmentId,
            previousStatus,
            newStatus,
            correctedAtUtc,
            correctedByMembershipId,
            reason);
}
