using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Assessment;
using MakanApp.Domain.Assessment;
using Xunit;

namespace MakanApp.IntegrationTests;

[Collection(SqlServerCollection.Name)]
public sealed partial class ExamEndpointsTests
{
    [Fact]
    public async Task AssignedTeacherCreatesExamDraft()
    {
        using var managerClient = _factory.CreateClient();
        var manager = await CreateManagerAsync(managerClient);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);
        using var teacherClient = _factory.CreateClient();
        _ = await CreateTeacherAsync(
            teacherClient,
            manager.OrganizationId,
            academicClass.ClassId,
            assigned: true);

        var exam = await CreateDraftAsync(teacherClient, academicClass.ClassId);

        Assert.Equal(ExamStatus.Draft, exam.Status);
        Assert.Equal(ExamVersionStatus.Draft, exam.VersionStatus);
        Assert.Equal(1, exam.VersionNumber);
    }

    [Fact]
    public async Task UnassignedTeacherCannotCreateExam()
    {
        using var managerClient = _factory.CreateClient();
        var manager = await CreateManagerAsync(managerClient);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);
        using var teacherClient = _factory.CreateClient();
        _ = await CreateTeacherAsync(
            teacherClient,
            manager.OrganizationId,
            academicClass.ClassId,
            assigned: false);

        using var response = await teacherClient.PostAsJsonAsync(
            $"/api/v1/academic/classes/{academicClass.ClassId}/exams",
            NewDraftCommand(),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(AssessmentErrorCodes.TeacherNotAssigned, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task TeacherCannotCreateExamForAnotherOrganizationClass()
    {
        using var firstManagerClient = _factory.CreateClient();
        var firstManager = await CreateManagerAsync(firstManagerClient);
        var firstClass = await _factory.CreateAcademicClassAsync(firstManager.OrganizationId);
        using var secondManagerClient = _factory.CreateClient();
        var secondManager = await CreateManagerAsync(secondManagerClient);
        var secondClass = await _factory.CreateAcademicClassAsync(secondManager.OrganizationId);
        using var teacherClient = _factory.CreateClient();
        _ = await CreateTeacherAsync(
            teacherClient,
            firstManager.OrganizationId,
            firstClass.ClassId,
            assigned: true);

        using var response = await teacherClient.PostAsJsonAsync(
            $"/api/v1/academic/classes/{secondClass.ClassId}/exams",
            NewDraftCommand(),
            JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ExamVersionPersistsAndLoadsInEditor()
    {
        using var client = _factory.CreateClient();
        var manager = await CreateManagerAsync(client);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);
        var draft = await CreateDraftAsync(client, academicClass.ClassId);

        var editor = await client.GetFromJsonAsync<ExamEditorDto>(
            $"/api/v1/academic/exams/{draft.Id}/editor",
            JsonOptions);

        Assert.NotNull(editor);
        Assert.Equal(draft.VersionId, editor.VersionId);
        Assert.Equal(20m, editor.MaxScore);
        Assert.Equal(ExamRandomizationPolicy.QuestionOrder, editor.RandomizationPolicy);
    }

    [Fact]
    public async Task ObjectiveQuestionAndOptionsPersist()
    {
        using var client = _factory.CreateClient();
        var manager = await CreateManagerAsync(client);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);
        var draft = await CreateDraftAsync(client, academicClass.ClassId);

        var updated = await AddObjectiveQuestionAsync(client, draft);
        var counts = await _factory.GetExamContentCountsAsync(updated.VersionId);

        var question = Assert.Single(updated.Questions);
        Assert.Equal(ExamQuestionType.ObjectiveSingleChoice, question.Type);
        Assert.Equal(2, question.Options.Count);
        Assert.Single(question.Options, option => option.IsCorrect);
        Assert.Equal((1, 2), counts);
    }

    [Fact]
    public async Task DescriptiveQuestionPersistsWithoutAnswerKey()
    {
        using var client = _factory.CreateClient();
        var manager = await CreateManagerAsync(client);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);
        var draft = await CreateDraftAsync(client, academicClass.ClassId);

        var updated = await AddDescriptiveQuestionAsync(client, draft);

        var question = Assert.Single(updated.Questions);
        Assert.Equal(ExamQuestionType.Descriptive, question.Type);
        Assert.Empty(question.Options);
    }

    [Fact]
    public async Task DraftQuestionCanBeRemoved()
    {
        using var client = _factory.CreateClient();
        var manager = await CreateManagerAsync(client);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);
        var exam = await AddDescriptiveQuestionAsync(
            client,
            await CreateDraftAsync(client, academicClass.ClassId));
        var question = Assert.Single(exam.Questions);
        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/v1/academic/exams/{exam.Id}/questions/{question.Id}")
        {
            Content = JsonContent.Create(
                new DeleteExamQuestionCommand(
                    exam.VersionRowVersion,
                    question.RowVersion),
                options: JsonOptions)
        };

        using var response = await client.SendAsync(request);
        var editor = await client.GetFromJsonAsync<ExamEditorDto>(
            $"/api/v1/academic/exams/{exam.Id}/editor",
            JsonOptions);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(editor!.Questions);
    }

    [Fact]
    public async Task DuplicateQuestionOrderIsRejectedBySqlServer()
    {
        using var client = _factory.CreateClient();
        var manager = await CreateManagerAsync(client);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);
        var draft = await CreateDraftAsync(client, academicClass.ClassId, 40m);
        var first = await AddObjectiveQuestionAsync(client, draft, score: 20m);

        using var response = await client.PostAsJsonAsync(
            $"/api/v1/academic/exams/{draft.Id}/questions",
            new AddExamQuestionCommand(
                1,
                ExamQuestionType.Descriptive,
                "سؤال دوم با ترتیب تکراری",
                20m,
                null,
                first.VersionRowVersion),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(
            AssessmentErrorCodes.ExamQuestionOrderInvalid,
            await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task InvalidMaxScoreIsRejected()
    {
        using var client = _factory.CreateClient();
        var manager = await CreateManagerAsync(client);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);

        using var response = await client.PostAsJsonAsync(
            $"/api/v1/academic/classes/{academicClass.ClassId}/exams",
            NewDraftCommand(maxScore: 0m),
            JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(AssessmentErrorCodes.ExamMaxScoreInvalid, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task InvalidWindowIsRejected()
    {
        using var client = _factory.CreateClient();
        var manager = await CreateManagerAsync(client);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);
        var now = DateTime.UtcNow;

        using var response = await client.PostAsJsonAsync(
            $"/api/v1/academic/classes/{academicClass.ClassId}/exams",
            NewDraftCommand(
                availableFromUtc: now.AddHours(2),
                availableUntilUtc: now.AddHours(1)),
            JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(AssessmentErrorCodes.ExamWindowInvalid, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task ScoreTotalMismatchPreventsPublication()
    {
        using var client = _factory.CreateClient();
        var manager = await CreateManagerAsync(client);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);
        var draft = await CreateDraftAsync(client, academicClass.ClassId, 20m);
        var withQuestion = await AddDescriptiveQuestionAsync(client, draft, score: 10m);

        using var response = await client.PostAsJsonAsync(
            $"/api/v1/academic/exams/{draft.Id}/publish",
            new PublishExamCommand(withQuestion.ExamRowVersion, withQuestion.VersionRowVersion),
            JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(AssessmentErrorCodes.ExamScoreTotalInvalid, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task ExamWithoutQuestionCannotBePublished()
    {
        using var client = _factory.CreateClient();
        var manager = await CreateManagerAsync(client);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);
        var draft = await CreateDraftAsync(client, academicClass.ClassId);

        using var response = await client.PostAsJsonAsync(
            $"/api/v1/academic/exams/{draft.Id}/publish",
            new PublishExamCommand(draft.ExamRowVersion, draft.VersionRowVersion),
            JsonOptions);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(AssessmentErrorCodes.ExamQuestionRequired, await ReadProblemCodeAsync(response));
    }

    [Fact]
    public async Task ValidExamPublishesExplicitly()
    {
        using var client = _factory.CreateClient();
        var manager = await CreateManagerAsync(client);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);
        var draft = await CreateDraftAsync(client, academicClass.ClassId);
        var withQuestion = await AddObjectiveQuestionAsync(client, draft);

        var published = await PublishAsync(client, withQuestion);

        Assert.Equal(ExamStatus.Published, published.Status);
        Assert.Equal(ExamVersionStatus.Published, published.VersionStatus);
        Assert.NotNull(published.PublishedAtUtc);
    }

    [Fact]
    public async Task EnrolledStudentSeesPublishedExamMetadata()
    {
        using var managerClient = _factory.CreateClient();
        var manager = await CreateManagerAsync(managerClient);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);
        var exam = await AddObjectiveQuestionAsync(
            managerClient,
            await CreateDraftAsync(managerClient, academicClass.ClassId));
        _ = await PublishAsync(managerClient, exam);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(
            studentClient,
            manager.OrganizationId,
            academicClass.ClassId);

        var exams = await studentClient.GetFromJsonAsync<StudentExamSummary[]>(
            "/api/v1/academic/exams",
            JsonOptions);

        var summary = Assert.Single(exams!);
        Assert.Equal(exam.Id, summary.Id);
        Assert.Equal(ExamVersionStatus.Published, summary.PublicationState);
    }

    [Fact]
    public async Task StudentCannotSeeDraftExam()
    {
        using var managerClient = _factory.CreateClient();
        var manager = await CreateManagerAsync(managerClient);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);
        var draft = await CreateDraftAsync(managerClient, academicClass.ClassId);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(
            studentClient,
            manager.OrganizationId,
            academicClass.ClassId);

        var list = await studentClient.GetFromJsonAsync<StudentExamSummary[]>(
            "/api/v1/academic/exams",
            JsonOptions);
        using var previewResponse = await studentClient.GetAsync(
            $"/api/v1/academic/exams/{draft.Id}/student-preview");

        Assert.Empty(list!);
        Assert.Equal(HttpStatusCode.NotFound, previewResponse.StatusCode);
    }
}
