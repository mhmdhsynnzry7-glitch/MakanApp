using MakanApp.Domain.Assessment;
using MakanApp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MakanApp.IntegrationTests;

public sealed partial class MakanAppWebApplicationFactory
{
    public async Task<ExamFinalizationPersistenceState> GetExamFinalizationStateAsync(
        Guid attemptId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MakanDbContext>();
        var attempt = await dbContext.ExamAttempts
            .AsNoTracking()
            .SingleAsync(item => item.Id == attemptId);
        var finalAnswers = await dbContext.ExamFinalAnswers
            .AsNoTracking()
            .Where(answer => answer.ExamAttemptId == attemptId)
            .OrderBy(answer => answer.ExamAttemptQuestionId)
            .Select(answer => new FinalAnswerPersistenceState(
                answer.ExamAttemptQuestionId,
                answer.QuestionVersionId,
                answer.AnswerRevisionId))
            .ToArrayAsync();
        return new ExamFinalizationPersistenceState(
            attempt.Status,
            attempt.AnswerSetVersion,
            attempt.FinalizedAtUtc,
            attempt.FinalizedAnswerSetVersion,
            attempt.FinalizeClientOperationId,
            attempt.FinalizedBySessionId,
            finalAnswers);
    }

    public sealed record ExamFinalizationPersistenceState(
        ExamAttemptStatus Status,
        long AnswerSetVersion,
        DateTime? FinalizedAtUtc,
        long? FinalizedAnswerSetVersion,
        Guid? FinalizeClientOperationId,
        Guid? FinalizedBySessionId,
        IReadOnlyCollection<FinalAnswerPersistenceState> FinalAnswers);

    public sealed record FinalAnswerPersistenceState(
        Guid ExamAttemptQuestionId,
        Guid QuestionVersionId,
        Guid AnswerRevisionId);
}
