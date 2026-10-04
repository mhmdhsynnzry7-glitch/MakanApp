namespace MakanApp.Application.Assessment;

public static class AssessmentErrorCodes
{
    public const string AssignmentNotFound = "ASSIGNMENT_NOT_FOUND";
    public const string AssignmentNotDraft = "ASSIGNMENT_NOT_DRAFT";
    public const string AssignmentAlreadyPublished = "ASSIGNMENT_ALREADY_PUBLISHED";
    public const string AssignmentTitleRequired = "ASSIGNMENT_TITLE_REQUIRED";
    public const string AssignmentDescriptionInvalid = "ASSIGNMENT_DESCRIPTION_INVALID";
    public const string AssignmentDueDateInvalid = "ASSIGNMENT_DUE_DATE_INVALID";
    public const string AssignmentAttemptsInvalid = "ASSIGNMENT_ATTEMPTS_INVALID";
    public const string AssignmentNotAllowed = "ASSIGNMENT_NOT_ALLOWED";
    public const string TeacherNotAssigned = "TEACHER_NOT_ASSIGNED";
    public const string ConcurrencyConflict = "CONCURRENCY_CONFLICT";
}
