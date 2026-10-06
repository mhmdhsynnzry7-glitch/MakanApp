using System.Net;
using System.Net.Http.Json;
using MakanApp.Application.Assessment;
using MakanApp.Domain.Assessment;
using Xunit;

namespace MakanApp.IntegrationTests;

public sealed partial class ExamEndpointsTests
{
    [Fact]
    public async Task UnrelatedStudentCannotSeePublishedExam()
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
            academicClass.ClassId,
            enrolled: false);

        var list = await studentClient.GetFromJsonAsync<StudentExamSummary[]>(
            "/api/v1/academic/exams",
            JsonOptions);
        using var previewResponse = await studentClient.GetAsync(
            $"/api/v1/academic/exams/{exam.Id}/student-preview");

        Assert.Empty(list!);
        Assert.Equal(HttpStatusCode.NotFound, previewResponse.StatusCode);
    }

    [Fact]
    public async Task StudentListContainsNoQuestionOrAnswerKey()
    {
        using var managerClient = _factory.CreateClient();
        var manager = await CreateManagerAsync(managerClient);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);
        var exam = await AddObjectiveQuestionAsync(
            managerClient,
            await CreateDraftAsync(managerClient, academicClass.ClassId));
        _ = await PublishAsync(managerClient, exam);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, manager.OrganizationId, academicClass.ClassId);

        using var response = await studentClient.GetAsync("/api/v1/academic/exams");
        var json = await response.Content.ReadAsStringAsync();

        response.EnsureSuccessStatusCode();
        Assert.DoesNotContain("questions", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("isCorrect", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StudentPreviewNeverExposesCorrectOptionMarker()
    {
        using var managerClient = _factory.CreateClient();
        var manager = await CreateManagerAsync(managerClient);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);
        var exam = await AddObjectiveQuestionAsync(
            managerClient,
            await CreateDraftAsync(managerClient, academicClass.ClassId));
        _ = await PublishAsync(managerClient, exam);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, manager.OrganizationId, academicClass.ClassId);

        using var response = await studentClient.GetAsync(
            $"/api/v1/academic/exams/{exam.Id}/student-preview");
        var json = await response.Content.ReadAsStringAsync();
        var preview = await response.Content.ReadFromJsonAsync<StudentSafeExamPreview>(JsonOptions);

        response.EnsureSuccessStatusCode();
        Assert.Equal(2, Assert.Single(preview!.Questions).Options.Count);
        Assert.DoesNotContain("isCorrect", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ParentSeesOnlyMetadataAndCannotPreviewOrMutateExam()
    {
        using var managerClient = _factory.CreateClient();
        var manager = await CreateManagerAsync(managerClient);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);
        var exam = await AddObjectiveQuestionAsync(
            managerClient,
            await CreateDraftAsync(managerClient, academicClass.ClassId));
        var published = await PublishAsync(managerClient, exam);
        using var parentClient = _factory.CreateClient();
        _ = await CreateParentAsync(parentClient, manager.OrganizationId, academicClass.ClassId);

        var list = await parentClient.GetFromJsonAsync<StudentExamSummary[]>(
            "/api/v1/academic/exams",
            JsonOptions);
        using var previewResponse = await parentClient.GetAsync(
            $"/api/v1/academic/exams/{exam.Id}/student-preview");
        using var updateResponse = await parentClient.PatchAsJsonAsync(
            $"/api/v1/academic/exams/{exam.Id}",
            NewUpdateCommand(published),
            JsonOptions);

        Assert.Single(list!);
        Assert.Equal(HttpStatusCode.Forbidden, previewResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, updateResponse.StatusCode);
    }

    [Fact]
    public async Task PublishedVersionCannotBeEditedOrReceiveQuestions()
    {
        using var client = _factory.CreateClient();
        var manager = await CreateManagerAsync(client);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);
        var exam = await AddObjectiveQuestionAsync(
            client,
            await CreateDraftAsync(client, academicClass.ClassId));
        var published = await PublishAsync(client, exam);

        using var updateResponse = await client.PatchAsJsonAsync(
            $"/api/v1/academic/exams/{exam.Id}",
            NewUpdateCommand(published, "ویرایش غیرمجاز"),
            JsonOptions);
        using var addResponse = await client.PostAsJsonAsync(
            $"/api/v1/academic/exams/{exam.Id}/questions",
            new AddExamQuestionCommand(
                2,
                ExamQuestionType.Descriptive,
                "سؤال غیرمجاز",
                1m,
                null,
                published.VersionRowVersion),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Conflict, updateResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, addResponse.StatusCode);
        Assert.Equal(AssessmentErrorCodes.ExamVersionLocked, await ReadProblemCodeAsync(updateResponse));
    }

    [Fact]
    public async Task NextDraftVersionPreservesPublishedHistory()
    {
        using var client = _factory.CreateClient();
        var manager = await CreateManagerAsync(client);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);
        var exam = await AddObjectiveQuestionAsync(
            client,
            await CreateDraftAsync(client, academicClass.ClassId),
            prompt: "2 + 2?");
        var published = await PublishAsync(client, exam);
        using var nextResponse = await client.PostAsJsonAsync(
            $"/api/v1/academic/exams/{exam.Id}/versions",
            new CreateNextExamVersionCommand(
                published.ExamRowVersion,
                published.VersionRowVersion),
            JsonOptions);
        var next = await ReadRequiredAsync<ExamEditorDto>(nextResponse);
        var copiedQuestion = Assert.Single(next.Questions);
        Assert.Equal(8, Convert.FromBase64String(next.VersionRowVersion).Length);
        Assert.Equal(8, Convert.FromBase64String(copiedQuestion.RowVersion).Length);
        var persistedRowVersions = await _factory.GetExamRowVersionsAsync(
            next.VersionId,
            copiedQuestion.Id);
        Assert.Equal(persistedRowVersions.Version, next.VersionRowVersion);
        Assert.Equal(persistedRowVersions.Question, copiedQuestion.RowVersion);

        using var updateResponse = await client.PatchAsJsonAsync(
            $"/api/v1/academic/exams/{exam.Id}/questions/{copiedQuestion.Id}",
            new UpdateExamQuestionCommand(
                1,
                ExamQuestionType.ObjectiveSingleChoice,
                "3 + 3?",
                20m,
                [
                    new ExamQuestionOptionCommand(1, "5", false),
                    new ExamQuestionOptionCommand(2, "6", true)
                ],
                next.VersionRowVersion,
                copiedQuestion.RowVersion),
            JsonOptions);
        updateResponse.EnsureSuccessStatusCode();
        var prompts = await _factory.GetExamVersionPromptsAsync(exam.Id);

        Assert.Equal(HttpStatusCode.Created, nextResponse.StatusCode);
        Assert.Equal(2, next.VersionNumber);
        Assert.Equal(2, await _factory.CountExamVersionsAsync(exam.Id));
        Assert.Equal("2 + 2?", prompts.FirstPrompt);
        Assert.Equal("3 + 3?", prompts.SecondPrompt);
    }

    [Fact]
    public async Task StaleDraftEditReturnsConcurrencyConflict()
    {
        using var client = _factory.CreateClient();
        var manager = await CreateManagerAsync(client);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);
        var draft = await CreateDraftAsync(client, academicClass.ClassId);
        var command = NewUpdateCommand(draft, "ویرایش اول");
        using var firstResponse = await client.PatchAsJsonAsync(
            $"/api/v1/academic/exams/{draft.Id}",
            command,
            JsonOptions);
        firstResponse.EnsureSuccessStatusCode();

        using var staleResponse = await client.PatchAsJsonAsync(
            $"/api/v1/academic/exams/{draft.Id}",
            command,
            JsonOptions);

        Assert.Equal(HttpStatusCode.PreconditionFailed, staleResponse.StatusCode);
        Assert.Equal(AssessmentErrorCodes.ConcurrencyConflict, await ReadProblemCodeAsync(staleResponse));
    }

    [Fact]
    public async Task StalePublishReturnsConcurrencyConflict()
    {
        using var client = _factory.CreateClient();
        var manager = await CreateManagerAsync(client);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);
        var exam = await AddObjectiveQuestionAsync(
            client,
            await CreateDraftAsync(client, academicClass.ClassId));
        var stalePublish = new PublishExamCommand(exam.ExamRowVersion, exam.VersionRowVersion);
        using var updateResponse = await client.PatchAsJsonAsync(
            $"/api/v1/academic/exams/{exam.Id}",
            NewUpdateCommand(exam, "عنوان تازه"),
            JsonOptions);
        updateResponse.EnsureSuccessStatusCode();

        using var publishResponse = await client.PostAsJsonAsync(
            $"/api/v1/academic/exams/{exam.Id}/publish",
            stalePublish,
            JsonOptions);

        Assert.Equal(HttpStatusCode.PreconditionFailed, publishResponse.StatusCode);
        Assert.Equal(AssessmentErrorCodes.ConcurrencyConflict, await ReadProblemCodeAsync(publishResponse));
    }

    [Fact]
    public async Task KnownSecretAnswerKeyNeverAppearsInStudentResponse()
    {
        const string secretAnswerKey = "ULTRA_SECRET_ANSWER_719";
        using var managerClient = _factory.CreateClient();
        var manager = await CreateManagerAsync(managerClient);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);
        var exam = await AddObjectiveQuestionAsync(
            managerClient,
            await CreateDraftAsync(managerClient, academicClass.ClassId));
        var published = await PublishAsync(managerClient, exam);
        var correctOption = Assert.Single(
            Assert.Single(published.Questions).Options,
            option => option.IsCorrect);
        var confidentialKey = $"{secretAnswerKey}:{correctOption.Id}";
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, manager.OrganizationId, academicClass.ClassId);

        using var listResponse = await studentClient.GetAsync("/api/v1/academic/exams");
        using var previewResponse = await studentClient.GetAsync(
            $"/api/v1/academic/exams/{exam.Id}/student-preview");
        var combinedStudentJson =
            await listResponse.Content.ReadAsStringAsync() +
            await previewResponse.Content.ReadAsStringAsync();

        Assert.DoesNotContain(secretAnswerKey, combinedStudentJson, StringComparison.Ordinal);
        Assert.DoesNotContain(confidentialKey, combinedStudentJson, StringComparison.Ordinal);
        Assert.DoesNotContain("isCorrect", combinedStudentJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnonymousWriteIsRejected()
    {
        using var client = _factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            $"/api/v1/academic/classes/{Guid.NewGuid()}/exams",
            NewDraftCommand(),
            JsonOptions);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CrossOrganizationQuestionReferenceIsRejectedByCompositeForeignKey()
    {
        using var firstClient = _factory.CreateClient();
        var firstManager = await CreateManagerAsync(firstClient);
        using var secondClient = _factory.CreateClient();
        var secondManager = await CreateManagerAsync(secondClient);
        var secondClass = await _factory.CreateAcademicClassAsync(secondManager.OrganizationId);
        var foreignExam = await CreateDraftAsync(secondClient, secondClass.ClassId);

        var rejected = await _factory.CrossOrganizationQuestionReferenceIsRejectedAsync(
            firstManager.OrganizationId,
            foreignExam.VersionId);

        Assert.True(rejected);
    }

    [Fact]
    public async Task TeacherAndStudentUseIdenticalStudentSafePreviewProjection()
    {
        using var managerClient = _factory.CreateClient();
        var manager = await CreateManagerAsync(managerClient);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);
        var exam = await AddObjectiveQuestionAsync(
            managerClient,
            await CreateDraftAsync(managerClient, academicClass.ClassId));
        _ = await PublishAsync(managerClient, exam);
        using var teacherClient = _factory.CreateClient();
        _ = await CreateTeacherAsync(
            teacherClient,
            manager.OrganizationId,
            academicClass.ClassId,
            assigned: true);
        using var studentClient = _factory.CreateClient();
        _ = await CreateStudentAsync(studentClient, manager.OrganizationId, academicClass.ClassId);

        var teacherJson = await teacherClient.GetStringAsync(
            $"/api/v1/academic/exams/{exam.Id}/student-preview");
        var studentJson = await studentClient.GetStringAsync(
            $"/api/v1/academic/exams/{exam.Id}/student-preview");

        Assert.Equal(teacherJson, studentJson);
        Assert.DoesNotContain("isCorrect", teacherJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AssignedTeacherPreviewIncludesAnswerKey()
    {
        using var managerClient = _factory.CreateClient();
        var manager = await CreateManagerAsync(managerClient);
        var academicClass = await _factory.CreateAcademicClassAsync(manager.OrganizationId);
        var exam = await AddObjectiveQuestionAsync(
            managerClient,
            await CreateDraftAsync(managerClient, academicClass.ClassId));
        using var teacherClient = _factory.CreateClient();
        _ = await CreateTeacherAsync(
            teacherClient,
            manager.OrganizationId,
            academicClass.ClassId,
            assigned: true);

        var preview = await teacherClient.GetFromJsonAsync<ExamTeacherPreview>(
            $"/api/v1/academic/exams/{exam.Id}/teacher-preview",
            JsonOptions);

        Assert.Single(Assert.Single(preview!.Questions).Options, option => option.IsCorrect);
    }

    [Fact]
    public async Task ManagerCannotReadOrMutateAnotherOrganizationExam()
    {
        using var ownerClient = _factory.CreateClient();
        var owner = await CreateManagerAsync(ownerClient);
        var academicClass = await _factory.CreateAcademicClassAsync(owner.OrganizationId);
        var exam = await CreateDraftAsync(ownerClient, academicClass.ClassId);
        using var attackerClient = _factory.CreateClient();
        _ = await CreateManagerAsync(attackerClient);

        using var readResponse = await attackerClient.GetAsync(
            $"/api/v1/academic/exams/{exam.Id}/editor");
        using var updateResponse = await attackerClient.PatchAsJsonAsync(
            $"/api/v1/academic/exams/{exam.Id}",
            NewUpdateCommand(exam),
            JsonOptions);

        Assert.Equal(HttpStatusCode.NotFound, readResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, updateResponse.StatusCode);
    }
}
