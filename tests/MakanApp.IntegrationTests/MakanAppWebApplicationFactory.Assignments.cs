using MakanApp.Domain.Academic;
using MakanApp.Domain.Assessment;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MakanApp.IntegrationTests;

public sealed partial class MakanAppWebApplicationFactory
{
    public async Task EndEnrollmentAsync(Guid enrollmentId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var enrollment = await dbContext.Enrollments.SingleAsync(item => item.Id == enrollmentId);
        enrollment.Withdraw(DateTime.UtcNow);
        await dbContext.SaveChangesAsync();
    }

    public async Task EndTeacherAssignmentAsync(Guid teacherAssignmentId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var assignment = await dbContext.TeacherAssignments.SingleAsync(
            item => item.Id == teacherAssignmentId);
        assignment.End(DateTime.UtcNow);
        await dbContext.SaveChangesAsync();
    }

    public async Task<int> CountAssignmentRecipientsAsync(Guid assignmentVersionId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.AssignmentRecipients.CountAsync(
            recipient => recipient.AssignmentVersionId == assignmentVersionId);
    }

    public async Task<Guid[]> GetAssignmentRecipientEnrollmentIdsAsync(Guid assignmentVersionId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.AssignmentRecipients
            .Where(recipient => recipient.AssignmentVersionId == assignmentVersionId)
            .Select(recipient => recipient.EnrollmentId)
            .ToArrayAsync();
    }

    public async Task<bool> DuplicateAssignmentRecipientIsRejectedAsync(Guid assignmentVersionId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var existing = await dbContext.AssignmentRecipients
            .AsNoTracking()
            .FirstAsync(recipient => recipient.AssignmentVersionId == assignmentVersionId);
        dbContext.AssignmentRecipients.Add(AssignmentRecipient.Create(
            existing.OrganizationId,
            existing.ClassId,
            existing.AssignmentId,
            existing.AssignmentVersionId,
            existing.EnrollmentId,
            DateTime.UtcNow));
        try
        {
            await dbContext.SaveChangesAsync();
            return false;
        }
        catch (DbUpdateException)
        {
            return true;
        }
    }

    public async Task<bool> CrossOrganizationEnrollmentRecipientIsRejectedAsync(
        Guid assignmentVersionId,
        Guid foreignEnrollmentId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var version = await dbContext.AssignmentVersions
            .AsNoTracking()
            .SingleAsync(item => item.Id == assignmentVersionId);
        dbContext.AssignmentRecipients.Add(AssignmentRecipient.Create(
            version.OrganizationId,
            version.ClassId,
            version.AssignmentId,
            version.Id,
            foreignEnrollmentId,
            DateTime.UtcNow));
        try
        {
            await dbContext.SaveChangesAsync();
            return false;
        }
        catch (DbUpdateException)
        {
            return true;
        }
    }
}
