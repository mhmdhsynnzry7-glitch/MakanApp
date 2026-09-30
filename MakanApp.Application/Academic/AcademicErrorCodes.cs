namespace MakanApp.Application.Academic;

public static class AcademicErrorCodes
{
    public const string AcademicPeriodInvalid = "ACADEMIC_PERIOD_INVALID";
    public const string AcademicPeriodNotFound = "ACADEMIC_PERIOD_NOT_FOUND";
    public const string CourseInvalid = "COURSE_INVALID";
    public const string CourseNotFound = "COURSE_NOT_FOUND";
    public const string ClassInvalid = "CLASS_INVALID";
    public const string ClassNotFound = "CLASS_NOT_FOUND";
    public const string ClassNotActive = "CLASS_NOT_ACTIVE";
    public const string ClassCapacityExceeded = "CLASS_CAPACITY_EXCEEDED";
    public const string LearnerNotFound = "LEARNER_NOT_FOUND";
    public const string EnrollmentAlreadyActive = "ENROLLMENT_ALREADY_ACTIVE";
    public const string EnrollmentNotActive = "ENROLLMENT_NOT_ACTIVE";
    public const string TeacherNotAllowed = "TEACHER_NOT_ALLOWED";
    public const string TeacherAssignmentAlreadyActive = "TEACHER_ASSIGNMENT_ALREADY_ACTIVE";
    public const string TeacherAssignmentNotActive = "TEACHER_ASSIGNMENT_NOT_ACTIVE";
    public const string ManagerRoleRequired = "MANAGER_ROLE_REQUIRED";
    public const string AcademicReadNotAllowed = "ACADEMIC_READ_NOT_ALLOWED";
    public const string ScheduleRuleNotFound = "SCHEDULE_RULE_NOT_FOUND";
    public const string SessionNotFound = "SESSION_NOT_FOUND";
    public const string SessionNotActive = "SESSION_NOT_ACTIVE";
    public const string SessionCancelled = "SESSION_CANCELLED";
    public const string SessionTimeInvalid = "SESSION_TIME_INVALID";
    public const string SessionTimeConflict = "SESSION_TIME_CONFLICT";
    public const string ClassSessionConflict = "CLASS_SESSION_CONFLICT";
    public const string TeacherSessionConflict = "TEACHER_SESSION_CONFLICT";
    public const string AttendanceNotAllowed = "ATTENDANCE_NOT_ALLOWED";
    public const string AttendanceEnrollmentInvalid = "ATTENDANCE_ENROLLMENT_INVALID";
    public const string AttendanceAlreadyRecorded = "ATTENDANCE_ALREADY_RECORDED";
    public const string AttendanceAlreadyChanged = "ATTENDANCE_ALREADY_CHANGED";
    public const string AttendanceCorrectionReasonRequired = "ATTENDANCE_CORRECTION_REASON_REQUIRED";
    public const string ConcurrencyConflict = "CONCURRENCY_CONFLICT";
}
