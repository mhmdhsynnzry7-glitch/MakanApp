using MakanApp.Domain.Assessment;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MakanApp.IntegrationTests;

public sealed partial class MakanAppWebApplicationFactory
{
    public async Task<bool> CrossOrganizationQuestionReferenceIsRejectedAsync(
        Guid organizationId,
        Guid foreignExamVersionId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        dbContext.QuestionVersions.Add(QuestionVersion.CreateDraft(
            organizationId,
            foreignExamVersionId,
            1,
            ExamQuestionType.Descriptive,
            "سؤال با مرجع نامعتبر",
            1m,
            null,
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

    public async Task<(string FirstPrompt, string SecondPrompt)> GetExamVersionPromptsAsync(
        Guid examId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var prompts = await (
            from version in dbContext.ExamVersions
            join question in dbContext.QuestionVersions on version.Id equals question.ExamVersionId
            where version.ExamId == examId
            orderby version.VersionNumber
            select question.Prompt)
            .ToArrayAsync();
        return (prompts[0], prompts[1]);
    }

    public async Task<int> CountExamVersionsAsync(Guid examId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        return await dbContext.ExamVersions.CountAsync(version => version.ExamId == examId);
    }

    public async Task<(int Questions, int Options)> GetExamContentCountsAsync(Guid versionId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var questions = await dbContext.QuestionVersions.CountAsync(
            question => question.ExamVersionId == versionId);
        var options = await (
            from option in dbContext.QuestionOptions
            join question in dbContext.QuestionVersions on option.QuestionVersionId equals question.Id
            where question.ExamVersionId == versionId
            select option.Id)
            .CountAsync();
        return (questions, options);
    }

    public async Task<(string Version, string Question)> GetExamRowVersionsAsync(
        Guid versionId,
        Guid questionId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var version = await dbContext.ExamVersions
            .AsNoTracking()
            .Where(item => item.Id == versionId)
            .Select(item => item.RowVersion)
            .SingleAsync();
        var question = await dbContext.QuestionVersions
            .AsNoTracking()
            .Where(item => item.Id == questionId)
            .Select(item => item.RowVersion)
            .SingleAsync();
        return (Convert.ToBase64String(version), Convert.ToBase64String(question));
    }
}
