using MakanApp.Application.Organization;
using MakanApp.Domain.Organization;
using MakanApp.Domain.Storage;

namespace MakanApp.Application.Storage;

public static class FileAccessPolicy
{
    public static bool IsUploader(FileAsset fileAsset, AccessContext context) =>
        fileAsset.UploadedByUserId == context.UserId;

    public static bool IsCurrentScope(FileAsset fileAsset, AccessContext context) =>
        fileAsset.OrganizationId.HasValue
            ? context.WorkspaceType == WorkspaceType.Organization &&
              context.OrganizationId == fileAsset.OrganizationId
            : context.WorkspaceType == WorkspaceType.Personal &&
              context.OrganizationId is null;
}
