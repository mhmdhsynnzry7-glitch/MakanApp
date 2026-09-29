namespace MakanApp.Application.Guardian;

public static class GuardianErrorCodes
{
    public const string GuardianRelationNotFound = "GUARDIAN_RELATION_NOT_FOUND";
    public const string ParentRoleRequired = "PARENT_ROLE_REQUIRED";
    public const string ChildContextNotAllowed = "CHILD_CONTEXT_NOT_ALLOWED";
    public const string ChildContextNotFound = "CHILD_CONTEXT_NOT_FOUND";
    public const string ConcurrencyConflict = "CONCURRENCY_CONFLICT";
}
