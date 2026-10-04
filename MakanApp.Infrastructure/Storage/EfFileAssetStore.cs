using MakanApp.Application.Storage;
using MakanApp.Domain.Storage;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MakanApp.Infrastructure.Storage;

public sealed class EfFileAssetStore(MakanDbContext dbContext) : IFileAssetStore
{
    public Task<FileAsset?> GetAsync(Guid fileAssetId, CancellationToken cancellationToken) =>
        dbContext.FileAssets.AsNoTracking().SingleOrDefaultAsync(
            fileAsset => fileAsset.Id == fileAssetId,
            cancellationToken);

    public Task<FileAsset?> GetForUpdateAsync(Guid fileAssetId, CancellationToken cancellationToken) =>
        dbContext.FileAssets.SingleOrDefaultAsync(
            fileAsset => fileAsset.Id == fileAssetId,
            cancellationToken);

    public void Add(FileAsset fileAsset) => dbContext.FileAssets.Add(fileAsset);

    public void SetOriginalRowVersion(FileAsset fileAsset, byte[] rowVersion) =>
        dbContext.Entry(fileAsset).Property(item => item.RowVersion).OriginalValue = rowVersion;

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new StorageException(
                StorageErrorCodes.ConcurrencyConflict,
                "فایل هم‌زمان تغییر کرده است؛ فراداده را دوباره دریافت کنید.",
                exception);
        }
    }
}
