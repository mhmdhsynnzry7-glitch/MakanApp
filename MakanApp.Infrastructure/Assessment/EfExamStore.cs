using System.Data;
using MakanApp.Application.Assessment;
using MakanApp.Domain.Academic;
using MakanApp.Domain.Assessment;
using MakanApp.Domain.Organization;
using MakanApp.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using AcademicClass = MakanApp.Domain.Academic.Class;

namespace MakanApp.Infrastructure.Assessment;

public sealed class EfExamStore(
    MakanDbContext dbContext,
    ILogger<EfExamStore> logger) : IExamStore
{
    public async Task<IAssessmentTransaction> BeginSerializableTransactionAsync(
        CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        return new EfExamTransaction(transaction);
    }

    public Task<AcademicClass?> GetClassForUpdateAsync(
        Guid organizationId,
        Guid classId,
        CancellationToken cancellationToken) =>
        dbContext.Classes
            .FromSqlInterpolated(
                $"SELECT * FROM [academic].[Classes] WITH (UPDLOCK, HOLDLOCK) WHERE [OrganizationId] = {organizationId} AND [Id] = {classId}")
            .SingleOrDefaultAsync(cancellationToken);

    public Task<bool> HasActiveTeacherAssignmentAsync(
        Guid organizationId,
        Guid classId,
        Guid membershipId,
        CancellationToken cancellationToken) =>
        dbContext.TeacherAssignments.AnyAsync(
            assignment => assignment.OrganizationId == organizationId &&
                          assignment.ClassId == classId &&
                          assignment.TeacherMembershipId == membershipId &&
                          assignment.Status == TeacherAssignmentStatus.Active &&
                          assignment.EndedAtUtc == null,
            cancellationToken);

    public Task<ExamAggregateRecord?> GetCurrentAsync(
        Guid organizationId,
        Guid examId,
        CancellationToken cancellationToken) =>
        GetAggregateAsync(organizationId, examId, published: false, forUpdate: false, cancellationToken);

    public Task<ExamAggregateRecord?> GetCurrentForUpdateAsync(
        Guid organizationId,
        Guid examId,
        CancellationToken cancellationToken) =>
        GetAggregateAsync(organizationId, examId, published: false, forUpdate: true, cancellationToken);

    public Task<ExamAggregateRecord?> GetLatestPublishedAsync(
        Guid organizationId,
        Guid examId,
        CancellationToken cancellationToken) =>
        GetAggregateAsync(organizationId, examId, published: true, forUpdate: false, cancellationToken);

    public Task<bool> IsStudentActivelyEnrolledAsync(
        Guid organizationId,
        Guid classId,
        Guid userId,
        CancellationToken cancellationToken) =>
        (
            from enrollment in dbContext.Enrollments
            join organizationPerson in dbContext.OrganizationPersons
                on new { enrollment.OrganizationId, Id = enrollment.LearnerOrganizationPersonId }
                equals new { organizationPerson.OrganizationId, organizationPerson.Id }
            join user in dbContext.Users on organizationPerson.PersonId equals user.PersonId
            where enrollment.OrganizationId == organizationId &&
                  enrollment.ClassId == classId &&
                  enrollment.Status == EnrollmentStatus.Active &&
                  enrollment.EndedAtUtc == null &&
                  organizationPerson.Status == OrganizationPersonStatus.Active &&
                  organizationPerson.EndedAtUtc == null &&
                  user.Id == userId
            select enrollment.Id)
            .AnyAsync(cancellationToken);

    public async Task<IReadOnlyCollection<ExamSummaryRecord>> GetForManagerAsync(
        Guid organizationId,
        CancellationToken cancellationToken) =>
        await CreateCurrentSummaryQuery(organizationId)
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<ExamSummaryRecord>> GetForTeacherAsync(
        Guid organizationId,
        Guid membershipId,
        CancellationToken cancellationToken) =>
        await (
            from exam in dbContext.Exams
            join version in dbContext.ExamVersions
                on new
                {
                    exam.OrganizationId,
                    ExamId = exam.Id,
                    VersionNumber = exam.CurrentVersionNumber
                }
                equals new
                {
                    version.OrganizationId,
                    version.ExamId,
                    version.VersionNumber
                }
            join academicClass in dbContext.Classes
                on new { exam.OrganizationId, Id = exam.ClassId }
                equals new { academicClass.OrganizationId, academicClass.Id }
            join assignment in dbContext.TeacherAssignments
                on new { exam.OrganizationId, exam.ClassId }
                equals new { assignment.OrganizationId, assignment.ClassId }
            where exam.OrganizationId == organizationId &&
                  assignment.TeacherMembershipId == membershipId &&
                  assignment.Status == TeacherAssignmentStatus.Active &&
                  assignment.EndedAtUtc == null
            orderby version.AvailableFromUtc, exam.Id
            select new ExamSummaryRecord(exam, version, academicClass.Title))
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<ExamSummaryRecord>> GetForStudentAsync(
        Guid organizationId,
        Guid userId,
        CancellationToken cancellationToken) =>
        await (
            from exam in dbContext.Exams
            join version in dbContext.ExamVersions
                on new
                {
                    exam.OrganizationId,
                    ExamId = exam.Id,
                    VersionNumber = exam.LatestPublishedVersionNumber
                }
                equals new
                {
                    version.OrganizationId,
                    version.ExamId,
                    VersionNumber = (int?)version.VersionNumber
                }
            join academicClass in dbContext.Classes
                on new { exam.OrganizationId, Id = exam.ClassId }
                equals new { academicClass.OrganizationId, academicClass.Id }
            join enrollment in dbContext.Enrollments
                on new { exam.OrganizationId, exam.ClassId }
                equals new { enrollment.OrganizationId, enrollment.ClassId }
            join organizationPerson in dbContext.OrganizationPersons
                on new
                {
                    enrollment.OrganizationId,
                    Id = enrollment.LearnerOrganizationPersonId
                }
                equals new { organizationPerson.OrganizationId, organizationPerson.Id }
            join user in dbContext.Users on organizationPerson.PersonId equals user.PersonId
            where exam.OrganizationId == organizationId &&
                  exam.Status == ExamStatus.Published &&
                  version.Status == ExamVersionStatus.Published &&
                  enrollment.Status == EnrollmentStatus.Active &&
                  enrollment.EndedAtUtc == null &&
                  organizationPerson.Status == OrganizationPersonStatus.Active &&
                  organizationPerson.EndedAtUtc == null &&
                  user.Id == userId
            orderby version.AvailableFromUtc, exam.Id
            select new ExamSummaryRecord(exam, version, academicClass.Title))
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<ExamSummaryRecord>> GetForLearnerAsync(
        Guid organizationId,
        Guid learnerOrganizationPersonId,
        CancellationToken cancellationToken) =>
        await (
            from exam in dbContext.Exams
            join version in dbContext.ExamVersions
                on new
                {
                    exam.OrganizationId,
                    ExamId = exam.Id,
                    VersionNumber = exam.LatestPublishedVersionNumber
                }
                equals new
                {
                    version.OrganizationId,
                    version.ExamId,
                    VersionNumber = (int?)version.VersionNumber
                }
            join academicClass in dbContext.Classes
                on new { exam.OrganizationId, Id = exam.ClassId }
                equals new { academicClass.OrganizationId, academicClass.Id }
            join enrollment in dbContext.Enrollments
                on new { exam.OrganizationId, exam.ClassId }
                equals new { enrollment.OrganizationId, enrollment.ClassId }
            where exam.OrganizationId == organizationId &&
                  exam.Status == ExamStatus.Published &&
                  version.Status == ExamVersionStatus.Published &&
                  enrollment.LearnerOrganizationPersonId == learnerOrganizationPersonId &&
                  enrollment.Status == EnrollmentStatus.Active &&
                  enrollment.EndedAtUtc == null
            orderby version.AvailableFromUtc, exam.Id
            select new ExamSummaryRecord(exam, version, academicClass.Title))
            .AsNoTracking()
            .ToArrayAsync(cancellationToken);

    public void SetOriginalRowVersion(Exam exam, byte[] rowVersion) =>
        dbContext.Entry(exam).Property(item => item.RowVersion).OriginalValue = rowVersion;

    public void SetOriginalRowVersion(ExamVersion version, byte[] rowVersion) =>
        dbContext.Entry(version).Property(item => item.RowVersion).OriginalValue = rowVersion;

    public void SetOriginalRowVersion(QuestionVersion question, byte[] rowVersion) =>
        dbContext.Entry(question).Property(item => item.RowVersion).OriginalValue = rowVersion;

    public void Add(Exam exam) => dbContext.Exams.Add(exam);
    public void Add(ExamVersion version) => dbContext.ExamVersions.Add(version);
    public void Add(QuestionVersion question) => dbContext.QuestionVersions.Add(question);
    public void AddRange(IEnumerable<QuestionVersion> questions) => dbContext.QuestionVersions.AddRange(questions);
    public void ReplaceOptions(
        QuestionVersion question,
        IEnumerable<QuestionOption> previousOptions)
    {
        dbContext.QuestionOptions.RemoveRange(previousOptions);
        dbContext.QuestionOptions.AddRange(question.Options);
    }

    public void Remove(QuestionVersion question) => dbContext.QuestionVersions.Remove(question);

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            logger.LogWarning(
                "Exam optimistic concurrency conflict for entries: {EntryTypes}",
                string.Join(",", exception.Entries.Select(entry => entry.Metadata.ClrType.Name)));
            throw new AssessmentException(
                AssessmentErrorCodes.ConcurrencyConflict,
                "اطلاعات آزمون هم‌زمان تغییر کرده است؛ داده را دوباره دریافت کنید.");
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException { Number: 2601 or 2627 } sqlException &&
                  sqlException.Message.Contains(
                      "UX_QuestionVersions_Organization_ExamVersion_Order",
                      StringComparison.Ordinal))
        {
            throw new AssessmentException(
                AssessmentErrorCodes.ExamQuestionOrderInvalid,
                "ترتیب سؤال در نسخه آزمون باید یکتا باشد.");
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException { Number: 2601 or 2627 } sqlException &&
                  sqlException.Message.Contains(
                      "UX_QuestionOptions_Organization_Question_Order",
                      StringComparison.Ordinal))
        {
            throw new AssessmentException(
                AssessmentErrorCodes.ExamAnswerKeyInvalid,
                "ترتیب گزینه‌های سؤال باید یکتا باشد.");
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is SqlException { Number: 2601 or 2627 } sqlException &&
                  (sqlException.Message.Contains(
                       "UX_ExamVersions_Organization_Exam_VersionNumber",
                       StringComparison.Ordinal) ||
                   sqlException.Message.Contains(
                       "UX_ExamVersions_Organization_Exam_ActiveDraft",
                       StringComparison.Ordinal)))
        {
            throw new AssessmentException(
                AssessmentErrorCodes.ConcurrencyConflict,
                "نسخه دیگری برای آزمون هم‌زمان ایجاد شده است.");
        }
    }

    private async Task<ExamAggregateRecord?> GetAggregateAsync(
        Guid organizationId,
        Guid examId,
        bool published,
        bool forUpdate,
        CancellationToken cancellationToken)
    {
        Exam? exam;
        if (forUpdate)
        {
            exam = await dbContext.Exams
                .FromSqlInterpolated(
                    $"SELECT * FROM [assessment].[Exams] WITH (UPDLOCK, HOLDLOCK) WHERE [OrganizationId] = {organizationId} AND [Id] = {examId}")
                .SingleOrDefaultAsync(cancellationToken);
        }
        else
        {
            exam = await dbContext.Exams
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.OrganizationId == organizationId && item.Id == examId,
                    cancellationToken);
        }

        var versionNumber = published ? exam?.LatestPublishedVersionNumber : exam?.CurrentVersionNumber;
        if (exam is null || !versionNumber.HasValue)
        {
            return null;
        }

        ExamVersion? version;
        if (forUpdate)
        {
            version = await dbContext.ExamVersions
                .FromSqlInterpolated(
                    $"SELECT * FROM [assessment].[ExamVersions] WITH (UPDLOCK, HOLDLOCK) WHERE [OrganizationId] = {organizationId} AND [ExamId] = {examId} AND [VersionNumber] = {versionNumber.Value}")
                .SingleOrDefaultAsync(cancellationToken);
        }
        else
        {
            version = await dbContext.ExamVersions
                .AsNoTracking()
                .SingleOrDefaultAsync(
                    item => item.OrganizationId == organizationId &&
                            item.ExamId == examId &&
                            item.VersionNumber == versionNumber.Value,
                    cancellationToken);
        }

        if (version is null)
        {
            return null;
        }

        var questionQuery = dbContext.QuestionVersions
            .Include(question => question.Options)
            .Where(question => question.OrganizationId == organizationId &&
                               question.ExamVersionId == version.Id)
            .OrderBy(question => question.Order);
        var questions = forUpdate
            ? await questionQuery.ToArrayAsync(cancellationToken)
            : await questionQuery.AsNoTracking().ToArrayAsync(cancellationToken);
        var classData = await dbContext.Classes
            .AsNoTracking()
            .Where(academicClass => academicClass.OrganizationId == organizationId &&
                                    academicClass.Id == exam.ClassId)
            .Select(academicClass => new
            {
                academicClass.Title,
                IsActive = academicClass.Status == ClassStatus.Active && academicClass.EndedAtUtc == null
            })
            .SingleAsync(cancellationToken);
        return new ExamAggregateRecord(exam, version, classData.Title, classData.IsActive, questions);
    }

    private IQueryable<ExamSummaryRecord> CreateCurrentSummaryQuery(Guid organizationId) =>
        from exam in dbContext.Exams
        join version in dbContext.ExamVersions
            on new
            {
                exam.OrganizationId,
                ExamId = exam.Id,
                VersionNumber = exam.CurrentVersionNumber
            }
            equals new
            {
                version.OrganizationId,
                version.ExamId,
                version.VersionNumber
            }
        join academicClass in dbContext.Classes
            on new { exam.OrganizationId, Id = exam.ClassId }
            equals new { academicClass.OrganizationId, academicClass.Id }
        where exam.OrganizationId == organizationId
        orderby version.AvailableFromUtc, exam.Id
        select new ExamSummaryRecord(exam, version, academicClass.Title);

    private sealed class EfExamTransaction(IDbContextTransaction transaction) : IAssessmentTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken) =>
            transaction.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
