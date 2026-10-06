using MakanApp.Domain.Messaging;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MakanApp.IntegrationTests;

public sealed partial class MakanAppWebApplicationFactory
{
    public async Task<int> CountActiveUserBlocksAsync(Guid blockerUserId, Guid blockedUserId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.UserBlocks.CountAsync(block =>
            block.BlockerUserId == blockerUserId &&
            block.BlockedUserId == blockedUserId &&
            block.Status == UserBlockStatus.Active &&
            block.EndedAtUtc == null);
    }

    public async Task<int> CountUserBlockHistoryAsync(Guid blockerUserId, Guid blockedUserId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.UserBlocks.CountAsync(block =>
            block.BlockerUserId == blockerUserId && block.BlockedUserId == blockedUserId);
    }

    public async Task RemoveConversationParticipantAsync(Guid conversationId, Guid userId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var participant = await dbContext.ConversationParticipants.SingleAsync(item =>
            item.ConversationId == conversationId &&
            item.UserId == userId &&
            item.Status == ConversationParticipantStatus.Active &&
            item.EndedAtUtc == null);
        participant.Remove(userId, DateTime.UtcNow);
        await dbContext.SaveChangesAsync();
    }

    public async Task<AbuseReportEvidenceDatabaseRecord> GetAbuseReportEvidenceAsync(Guid reportId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.AbuseReports
            .AsNoTracking()
            .Where(report => report.Id == reportId)
            .Select(report => new AbuseReportEvidenceDatabaseRecord(
                report.Id,
                report.MessageId,
                report.ReportedUserId,
                report.ReportedMessageKind,
                report.ReportedContentSnapshot,
                report.ReportedMessageVersion,
                report.Description,
                report.Status))
            .SingleAsync();
    }

    public async Task<int> CountAbuseReportsAsync(Guid reporterUserId, Guid clientReportId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.AbuseReports.CountAsync(report =>
            report.ReporterUserId == reporterUserId &&
            report.ClientReportId == clientReportId);
    }
}

public sealed record AbuseReportEvidenceDatabaseRecord(
    Guid Id,
    Guid MessageId,
    Guid ReportedUserId,
    MessageKind MessageKind,
    string? ContentSnapshot,
    byte[] MessageVersion,
    string? Description,
    AbuseReportStatus Status);
