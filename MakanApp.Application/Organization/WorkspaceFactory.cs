using MakanApp.Domain.Organization;

namespace MakanApp.Application.Organization;

public static class WorkspaceFactory
{
    public static WorkspaceResult CreatePersonal() =>
        new(
            WorkspaceType.Personal,
            null,
            null,
            null,
            null,
            "فضای شخصی",
            "Active");

    public static WorkspaceResult CreateOrganization(OrganizationWorkspaceRecord record) =>
        new(
            WorkspaceType.Organization,
            record.OrganizationId,
            record.OrganizationName,
            record.MembershipId,
            record.Role,
            $"{record.OrganizationName} - {record.Role}",
            "Active");
}
