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
    public const string AssignmentMaxScoreInvalid = "ASSIGNMENT_MAX_SCORE_INVALID";
    public const string AssignmentNotAllowed = "ASSIGNMENT_NOT_ALLOWED";
    public const string TeacherNotAssigned = "TEACHER_NOT_ASSIGNED";
    public const string ConcurrencyConflict = "CONCURRENCY_CONFLICT";
    public const string SubmissionNotFound = "SUBMISSION_NOT_FOUND";
    public const string SubmissionNotAllowed = "SUBMISSION_NOT_ALLOWED";
    public const string SubmissionNotDraft = "SUBMISSION_NOT_DRAFT";
    public const string SubmissionEmpty = "SUBMISSION_EMPTY";
    public const string SubmissionDeadlinePassed = "SUBMISSION_DEADLINE_PASSED";
    public const string SubmissionAttemptsExhausted = "SUBMISSION_ATTEMPTS_EXHAUSTED";
    public const string SubmissionFileNotReady = "SUBMISSION_FILE_NOT_READY";
    public const string SubmissionFileNotAllowed = "SUBMISSION_FILE_NOT_ALLOWED";
    public const string SubmissionVersionMismatch = "SUBMISSION_VERSION_MISMATCH";
    public const string AssignmentRecipientNotFound = "ASSIGNMENT_RECIPIENT_NOT_FOUND";
    public const string AssignmentNotSubmittable = "ASSIGNMENT_NOT_SUBMITTABLE";
    public const string SubmissionAnswerInvalid = "SUBMISSION_ANSWER_INVALID";
    public const string EvaluationNotFound = "EVALUATION_NOT_FOUND";
    public const string EvaluationNotAllowed = "EVALUATION_NOT_ALLOWED";
    public const string EvaluationSubmissionNotFinal = "EVALUATION_SUBMISSION_NOT_FINAL";
    public const string EvaluationScoreInvalid = "EVALUATION_SCORE_INVALID";
    public const string EvaluationNotReadyForRelease = "EVALUATION_NOT_READY_FOR_RELEASE";
    public const string EvaluationAlreadyReleased = "EVALUATION_ALREADY_RELEASED";
    public const string EvaluationCorrectionReasonRequired = "EVALUATION_CORRECTION_REASON_REQUIRED";
    public const string GradeNotReleased = "GRADE_NOT_RELEASED";
    public const string GradeReleaseConflict = "GRADE_RELEASE_CONFLICT";
}
