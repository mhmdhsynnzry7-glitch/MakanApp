using MakanApp.Domain.Guardian;

namespace MakanApp.Application.Guardian;

public sealed record AuthorizedChildResult(
    Guid LearnerOrganizationPersonId,
    string DisplayName,
    Guid OrganizationId,
    string OrganizationName,
    GuardianRelationStatus RelationStatus);

public sealed record GuardianRelationSummaryResult(
    Guid RelationId,
    Guid LearnerOrganizationPersonId,
    string DisplayName,
    Guid OrganizationId,
    string OrganizationName,
    GuardianRelationStatus Status,
    DateTime ValidFromUtc,
    DateTime CreatedAtUtc);

public sealed record AuthorizedChildRecord(
    Guid RelationId,
    Guid LearnerOrganizationPersonId,
    string DisplayName,
    Guid OrganizationId,
    string OrganizationName,
    GuardianRelationStatus Status,
    DateTime ValidFromUtc,
    DateTime CreatedAtUtc);
