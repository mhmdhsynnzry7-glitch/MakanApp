# مرجع Endpointها و قراردادهای API ماکان

این مرجع مکمل [راهنمای استفاده از بک‌اند](backend-api-guide.md) است. قراردادهای HTTP از Controllerهای فعلی و مدل‌های متصل به آن‌ها آمده‌اند؛ مدل‌های داخلی persistence در فهرست قراردادها قرار ندارند.

در این نسخه 121 عملیات HTTP در 20 Controller وجود دارد. مسیرهای `/health`، `/openapi/v1.json`، `/swagger` و Hub در این شمارش نیستند.

همه مسیرها کامل نوشته شده‌اند. پارامترهای داخل `{...}` از مسیر، موارد ستون Query از URL و Command از بدنه JSON دریافت می‌شوند؛ تنها Upload از form-data استفاده می‌کند. همه عملیات به‌جز درخواست و تأیید OTP نیازمند `Authorization: Bearer <accessToken>` هستند.

ستون پاسخ فقط status موفق تعریف‌شده در Controller را نشان می‌دهد. خطاهای مشترک در راهنما و فهرست کدها در پایان این مرجع آمده‌اند. DTO و Enum قابل کلیک‌اند. علامت `?` یا عبارت «یا null» نشان‌دهنده nullable بودن قرارداد است؛ قواعد الزامی‌بودن و lifecycle در سرویس نیز بررسی می‌شوند.

## فهرست بخش‌ها

- [ورود و نشست](#api-auth)
- [پروفایل](#api-profile)
- [فضای کاری](#api-workspaces)
- [دعوت](#api-invitations)
- [سرپرست](#api-guardian)
- [دوره آموزشی](#api-academicperiods)
- [درس](#api-courses)
- [کلاس و ثبت‌نام](#api-classes)
- [برنامه آموزشی](#api-schedule)
- [جلسه و حضور و غیاب](#api-sessions)
- [تکلیف](#api-assignments)
- [پاسخ تکلیف](#api-submissions)
- [ارزیابی تکلیف](#api-evaluations)
- [آزمون و پاسخ](#api-exams)
- [تصحیح آزمون](#api-examgradings)
- [فایل](#api-files)
- [گفت‌وگو و پیام](#api-conversations)
- [جست‌وجوی پیام](#api-messages)
- [مسدودسازی و رسید گزارش](#api-messagingsafety)
- [گزارش پیام](#api-messagereports)
- [قراردادهای ورودی و خروجی](#contracts)
- [مقادیر Enum](#enums)
- [کدهای خطا](#error-codes)

<a id="api-auth"></a>
## ورود و نشست

درخواست و تأیید OTP عمومی‌اند؛ خروج به Bearer نیاز دارد. خروج نشست جاری را باطل می‌کند.

منبع: [AuthController](../MakanApp.Api/Controllers/AuthController.cs).

| Method | Route | Query | بدنه | پاسخ موفق |
|---|---|---|---|---|
| `POST` | `/api/v1/auth/otp/challenges` | — | [RequestOtpCommand](#type-requestotpcommand) | `202` [RequestOtpResult](#type-requestotpresult) |
| `POST` | `/api/v1/auth/otp/verify` | — | [VerifyOtpCommand](#type-verifyotpcommand) | `200` [VerifyOtpResult](#type-verifyotpresult) |
| `POST` | `/api/v1/auth/logout` | — | — | `204` بدون بدنه |

<a id="api-profile"></a>
## پروفایل

نشست معتبر؛ فقط پروفایل خود کاربر.

منبع: [ProfileController](../MakanApp.Api/Controllers/ProfileController.cs).

| Method | Route | Query | بدنه | پاسخ موفق |
|---|---|---|---|---|
| `GET` | `/api/v1/me` | — | — | `200` [CurrentUserResult](#type-currentuserresult) |
| `PATCH` | `/api/v1/me/profile` | — | [CompleteProfileCommand](#type-completeprofilecommand) | `200` [CurrentUserResult](#type-currentuserresult) |

<a id="api-workspaces"></a>
## فضای کاری

نشست معتبر؛ انتخاب سازمان و نقش فقط از عضویت‌های مجاز خود کاربر.

منبع: [WorkspacesController](../MakanApp.Api/Controllers/WorkspacesController.cs).

| Method | Route | Query | بدنه | پاسخ موفق |
|---|---|---|---|---|
| `GET` | `/api/v1/workspaces/me` | — | — | `200` آرایه [WorkspaceResult](#type-workspaceresult) |
| `POST` | `/api/v1/workspaces/select` | — | [SelectWorkspaceCommand](#type-selectworkspacecommand) | `200` [AccessContext](#type-accesscontext) |
| `GET` | `/api/v1/workspaces/current` | — | — | `200` [AccessContext](#type-accesscontext) |

<a id="api-invitations"></a>
## دعوت

نشست معتبر؛ دعوت‌های متعلق به حساب جاری. پذیرش، workspace را خودکار انتخاب نمی‌کند.

منبع: [InvitationsController](../MakanApp.Api/Controllers/InvitationsController.cs).

| Method | Route | Query | بدنه | پاسخ موفق |
|---|---|---|---|---|
| `GET` | `/api/v1/invitations` | — | — | `200` آرایه [InvitationResult](#type-invitationresult) |
| `POST` | `/api/v1/invitations/{invitationId}/accept` | — | — | `200` [AcceptInvitationResult](#type-acceptinvitationresult) |
| `POST` | `/api/v1/invitations/{invitationId}/decline` | — | — | `200` [DeclineInvitationResult](#type-declineinvitationresult) |

<a id="api-guardian"></a>
## سرپرست

نشست معتبر با workspace سازمانی، نقش Parent و رابطه فعال همان سازمان.

منبع: [GuardianController](../MakanApp.Api/Controllers/GuardianController.cs).

| Method | Route | Query | بدنه | پاسخ موفق |
|---|---|---|---|---|
| `GET` | `/api/v1/guardian/children` | — | — | `200` آرایه [AuthorizedChildResult](#type-authorizedchildresult) |
| `POST` | `/api/v1/guardian/children/{organizationPersonId}/select` | — | — | `200` [AccessContext](#type-accesscontext) |
| `GET` | `/api/v1/guardian/relations/{organizationPersonId}` | — | — | `200` [GuardianRelationSummaryResult](#type-guardianrelationsummaryresult) |

<a id="api-academicperiods"></a>
## دوره آموزشی

نشست معتبر با نقش Manager سازمان جاری.

منبع: [AcademicPeriodsController](../MakanApp.Api/Controllers/AcademicPeriodsController.cs).

| Method | Route | Query | بدنه | پاسخ موفق |
|---|---|---|---|---|
| `POST` | `/api/v1/academic/periods` | — | [CreateAcademicPeriodCommand](#type-createacademicperiodcommand) | `201` [AcademicPeriodResult](#type-academicperiodresult) |

<a id="api-courses"></a>
## درس

نشست معتبر با نقش Manager سازمان جاری.

منبع: [CoursesController](../MakanApp.Api/Controllers/CoursesController.cs).

| Method | Route | Query | بدنه | پاسخ موفق |
|---|---|---|---|---|
| `POST` | `/api/v1/academic/courses` | — | [CreateCourseCommand](#type-createcoursecommand) | `201` [CourseResult](#type-courseresult) |

<a id="api-classes"></a>
## کلاس و ثبت‌نام

نوشتن فقط Manager؛ خواندن برای Manager، Teacher منتسب یا Student ثبت‌نام‌شده. Parent در فهرست کلاس‌ها پشتیبانی نمی‌شود.

منبع: [ClassesController](../MakanApp.Api/Controllers/ClassesController.cs).

| Method | Route | Query | بدنه | پاسخ موفق |
|---|---|---|---|---|
| `POST` | `/api/v1/academic/classes` | — | [CreateClassCommand](#type-createclasscommand) | `201` [ClassResult](#type-classresult) |
| `GET` | `/api/v1/academic/classes` | — | — | `200` آرایه [ClassResult](#type-classresult) |
| `POST` | `/api/v1/academic/classes/{classId}/enrollments` | — | [EnrollLearnerCommand](#type-enrolllearnercommand) | `201` [EnrollmentResult](#type-enrollmentresult) |
| `POST` | `/api/v1/academic/classes/{classId}/enrollments/{enrollmentId}/end` | — | [EndEnrollmentCommand](#type-endenrollmentcommand) | `200` [EnrollmentResult](#type-enrollmentresult) |
| `POST` | `/api/v1/academic/classes/{classId}/teachers` | — | [AssignTeacherCommand](#type-assignteachercommand) | `201` [TeacherAssignmentResult](#type-teacherassignmentresult) |
| `POST` | `/api/v1/academic/classes/{classId}/teachers/{teacherAssignmentId}/end` | — | — | `200` [TeacherAssignmentResult](#type-teacherassignmentresult) |

<a id="api-schedule"></a>
## برنامه آموزشی

نوشتن برای Manager یا Teacher منتسب؛ خواندن برنامه متناسب با نقش و رابطه کلاس، و برای Parent با context فرزند معتبر.

منبع: [ScheduleController](../MakanApp.Api/Controllers/ScheduleController.cs).

| Method | Route | Query | بدنه | پاسخ موفق |
|---|---|---|---|---|
| `GET` | `/api/v1/academic/schedule` | `fromUtc`: `DateTime`، `toUtc`: `DateTime` | — | `200` آرایه [SessionResult](#type-sessionresult) |
| `POST` | `/api/v1/academic/classes/{classId}/schedule-rules` | — | [CreateScheduleRuleCommand](#type-createschedulerulecommand) | `201` [ScheduleRuleResult](#type-scheduleruleresult) |
| `PATCH` | `/api/v1/academic/schedule-rules/{scheduleRuleId}` | — | [UpdateScheduleRuleCommand](#type-updateschedulerulecommand) | `200` [ScheduleRuleResult](#type-scheduleruleresult) |

<a id="api-sessions"></a>
## جلسه و حضور و غیاب

مدیریت جلسه و دفتر حضور و غیاب برای Manager یا Teacher منتسب؛ جزئیات جلسه برای نقش دارای دسترسی به کلاس.

منبع: [SessionsController](../MakanApp.Api/Controllers/SessionsController.cs).

| Method | Route | Query | بدنه | پاسخ موفق |
|---|---|---|---|---|
| `POST` | `/api/v1/academic/classes/{classId}/sessions` | — | [CreateSessionCommand](#type-createsessioncommand) | `201` [SessionResult](#type-sessionresult) |
| `GET` | `/api/v1/academic/sessions/{sessionId}` | — | — | `200` [SessionResult](#type-sessionresult) |
| `PATCH` | `/api/v1/academic/sessions/{sessionId}` | — | [UpdateSessionCommand](#type-updatesessioncommand) | `200` [SessionResult](#type-sessionresult) |
| `POST` | `/api/v1/academic/sessions/{sessionId}/cancel` | — | [SessionVersionCommand](#type-sessionversioncommand) | `200` [SessionResult](#type-sessionresult) |
| `POST` | `/api/v1/academic/sessions/{sessionId}/complete` | — | [SessionVersionCommand](#type-sessionversioncommand) | `200` [SessionResult](#type-sessionresult) |
| `GET` | `/api/v1/academic/sessions/{sessionId}/attendance` | — | — | `200` [SessionAttendanceResult](#type-sessionattendanceresult) |
| `PUT` | `/api/v1/academic/sessions/{sessionId}/attendance` | — | [RecordAttendanceCommand](#type-recordattendancecommand) | `200` آرایه [AttendanceEntryResult](#type-attendanceentryresult) |
| `PATCH` | `/api/v1/academic/sessions/{sessionId}/attendance/{attendanceId}` | — | [CorrectAttendanceCommand](#type-correctattendancecommand) | `200` [AttendanceEntryResult](#type-attendanceentryresult) |

<a id="api-assignments"></a>
## تکلیف

ساخت، ویرایش و انتشار برای Manager یا Teacher منتسب. خواندن بر اساس نقش، وضعیت انتشار و رابطه کلاس/فرزند محدود است.

منبع: [AssignmentsController](../MakanApp.Api/Controllers/AssignmentsController.cs).

| Method | Route | Query | بدنه | پاسخ موفق |
|---|---|---|---|---|
| `POST` | `/api/v1/academic/classes/{classId}/assignments` | — | [CreateAssignmentDraftCommand](#type-createassignmentdraftcommand) | `201` [AssignmentResult](#type-assignmentresult) |
| `GET` | `/api/v1/academic/classes/{classId}/assignments` | — | — | `200` آرایه [AssignmentResult](#type-assignmentresult) |
| `GET` | `/api/v1/academic/assignments` | `classId`: `Guid` | — | `200` آرایه [AssignmentResult](#type-assignmentresult) |
| `GET` | `/api/v1/academic/assignments/{assignmentId}` | — | — | `200` [AssignmentResult](#type-assignmentresult) |
| `PATCH` | `/api/v1/academic/assignments/{assignmentId}` | — | [UpdateAssignmentDraftCommand](#type-updateassignmentdraftcommand) | `200` [AssignmentResult](#type-assignmentresult) |
| `POST` | `/api/v1/academic/assignments/{assignmentId}/publish` | — | [PublishAssignmentCommand](#type-publishassignmentcommand) | `200` [PublishAssignmentResult](#type-publishassignmentresult) |

<a id="api-submissions"></a>
## پاسخ تکلیف

عملیات پاسخ برای Student مالک و واجد شرایط؛ فهرست submitted-attempts برای ارزیاب مجاز. contentVisible قرارداد نمایش محتوای تلاش را مشخص می‌کند.

منبع: [SubmissionsController](../MakanApp.Api/Controllers/SubmissionsController.cs).

| Method | Route | Query | بدنه | پاسخ موفق |
|---|---|---|---|---|
| `POST` | `/api/v1/academic/assignments/{assignmentId}/attempts` | — | — | `201` [SubmissionAttemptResult](#type-submissionattemptresult) |
| `GET` | `/api/v1/academic/assignments/{assignmentId}/attempts/me` | — | — | `200` آرایه [SubmissionAttemptResult](#type-submissionattemptresult) |
| `GET` | `/api/v1/academic/submission-attempts/{attemptId}` | — | — | `200` [SubmissionAttemptResult](#type-submissionattemptresult) |
| `PATCH` | `/api/v1/academic/submission-attempts/{attemptId}/draft` | — | [SaveSubmissionDraftCommand](#type-savesubmissiondraftcommand) | `200` [SubmissionAttemptResult](#type-submissionattemptresult) |
| `POST` | `/api/v1/academic/submission-attempts/{attemptId}/attachments` | — | [AttachSubmissionFileCommand](#type-attachsubmissionfilecommand) | `200` [SubmissionAttemptResult](#type-submissionattemptresult) |
| `DELETE` | `/api/v1/academic/submission-attempts/{attemptId}/attachments/{fileAssetId}` | `expectedRowVersion`: `string` | — | `200` [SubmissionAttemptResult](#type-submissionattemptresult) |
| `POST` | `/api/v1/academic/submission-attempts/{attemptId}/submit` | — | [FinalSubmitAssignmentCommand](#type-finalsubmitassignmentcommand) | `200` [SubmissionReceipt](#type-submissionreceipt) |
| `GET` | `/api/v1/academic/assignments/{assignmentId}/submitted-attempts` | — | — | `200` آرایه [SubmissionAttemptResult](#type-submissionattemptresult) |

<a id="api-evaluations"></a>
## ارزیابی تکلیف

صف و mutationهای ارزیابی برای Manager یا Teacher منتسب؛ result برای Student مالک و guardian-result برای Parent با context فرزند معتبر.

منبع: [EvaluationsController](../MakanApp.Api/Controllers/EvaluationsController.cs).

| Method | Route | Query | بدنه | پاسخ موفق |
|---|---|---|---|---|
| `GET` | `/api/v1/academic/evaluations/queue` | `assignmentId`: `Guid?`، `classId`: `Guid?`، `reviewStatus`: [EvaluationReviewStatus](#type-evaluationreviewstatus) یا null، `isLate`: `bool?` | — | `200` آرایه [EvaluationQueueItemResult](#type-evaluationqueueitemresult) |
| `GET` | `/api/v1/academic/submission-attempts/{attemptId}/evaluation/submission` | — | — | `200` [SubmissionForEvaluationResult](#type-submissionforevaluationresult) |
| `GET` | `/api/v1/academic/submission-attempts/{attemptId}/evaluation` | — | — | `200` [EvaluatorEvaluationResult](#type-evaluatorevaluationresult) |
| `PUT` | `/api/v1/academic/submission-attempts/{attemptId}/evaluation` | — | [SaveEvaluationDraftCommand](#type-saveevaluationdraftcommand) | `200` [EvaluatorEvaluationResult](#type-evaluatorevaluationresult) |
| `POST` | `/api/v1/academic/submission-attempts/{attemptId}/evaluation/release` | — | [ReleaseEvaluationCommand](#type-releaseevaluationcommand) | `200` [GradeReleaseResult](#type-gradereleaseresult) |
| `POST` | `/api/v1/academic/submission-attempts/{attemptId}/evaluation/corrections` | — | [CorrectReleasedEvaluationCommand](#type-correctreleasedevaluationcommand) | `201` [EvaluatorEvaluationResult](#type-evaluatorevaluationresult) |
| `GET` | `/api/v1/academic/submission-attempts/{attemptId}/result` | — | — | `200` [StudentReleasedResult](#type-studentreleasedresult) |
| `GET` | `/api/v1/academic/submission-attempts/{attemptId}/guardian-result` | — | — | `200` [ParentReleasedResult](#type-parentreleasedresult) |

<a id="api-exams"></a>
## آزمون و پاسخ

طراحی و انتشار برای Manager یا Teacher منتسب؛ شروع، پاسخ و رسید برای Student مالک تلاش. preview و فهرست قواعد جداگانه دارند که در راهنما آمده‌اند.

منبع: [ExamsController](../MakanApp.Api/Controllers/ExamsController.cs).

| Method | Route | Query | بدنه | پاسخ موفق |
|---|---|---|---|---|
| `POST` | `/api/v1/academic/classes/{classId}/exams` | — | [CreateExamDraftCommand](#type-createexamdraftcommand) | `201` [ExamEditorDto](#type-exameditordto) |
| `GET` | `/api/v1/academic/exams` | — | — | `200` آرایه [StudentExamSummary](#type-studentexamsummary) |
| `GET` | `/api/v1/academic/exams/{examId}/editor` | — | — | `200` [ExamEditorDto](#type-exameditordto) |
| `GET` | `/api/v1/academic/exams/{examId}/teacher-preview` | — | — | `200` [ExamTeacherPreview](#type-examteacherpreview) |
| `PATCH` | `/api/v1/academic/exams/{examId}` | — | [UpdateExamDraftCommand](#type-updateexamdraftcommand) | `200` [ExamEditorDto](#type-exameditordto) |
| `POST` | `/api/v1/academic/exams/{examId}/questions` | — | [AddExamQuestionCommand](#type-addexamquestioncommand) | `201` [ExamEditorDto](#type-exameditordto) |
| `PATCH` | `/api/v1/academic/exams/{examId}/questions/{questionId}` | — | [UpdateExamQuestionCommand](#type-updateexamquestioncommand) | `200` [ExamEditorDto](#type-exameditordto) |
| `DELETE` | `/api/v1/academic/exams/{examId}/questions/{questionId}` | — | [DeleteExamQuestionCommand](#type-deleteexamquestioncommand) | `204` بدون بدنه |
| `GET` | `/api/v1/academic/exams/{examId}/student-preview` | — | — | `200` [StudentSafeExamPreview](#type-studentsafeexampreview) |
| `POST` | `/api/v1/academic/exams/{examId}/publish` | — | [PublishExamCommand](#type-publishexamcommand) | `200` [ExamEditorDto](#type-exameditordto) |
| `POST` | `/api/v1/academic/exams/{examId}/versions` | — | [CreateNextExamVersionCommand](#type-createnextexamversioncommand) | `201` [ExamEditorDto](#type-exameditordto) |
| `POST` | `/api/v1/academic/exams/{examId}/attempts/start` | — | [StartExamCommand](#type-startexamcommand) | `201` [StudentExamAttemptDto](#type-studentexamattemptdto) |
| `GET` | `/api/v1/academic/exams/{examId}/attempts/me/active` | — | — | `200` [StudentExamAttemptDto](#type-studentexamattemptdto) |
| `GET` | `/api/v1/academic/exam-attempts/{attemptId}` | — | — | `200` [StudentExamAttemptDto](#type-studentexamattemptdto) |
| `GET` | `/api/v1/academic/exams/{examId}/attempts/me` | — | — | `200` آرایه [StudentExamAttemptSummaryDto](#type-studentexamattemptsummarydto) |
| `POST` | `/api/v1/academic/exam-attempts/{attemptId}/write-lease` | — | — | `200` [ExamWriteLeaseDto](#type-examwriteleasedto) |
| `POST` | `/api/v1/academic/exam-attempts/{attemptId}/write-lease/transfer` | — | — | `200` [ExamWriteLeaseDto](#type-examwriteleasedto) |
| `PUT` | `/api/v1/academic/exam-attempts/{attemptId}/answers/{attemptQuestionId}` | — | [SaveExamAnswerCommand](#type-saveexamanswercommand) | `200` [ExamAnswerReceiptDto](#type-examanswerreceiptdto) |
| `GET` | `/api/v1/academic/exam-attempts/{attemptId}/answers` | — | — | `200` [StudentExamAnswersDto](#type-studentexamanswersdto) |
| `POST` | `/api/v1/academic/exam-attempts/{attemptId}/finalize` | — | [FinalizeExamCommand](#type-finalizeexamcommand) | `200` [ExamFinalReceiptDto](#type-examfinalreceiptdto) |
| `GET` | `/api/v1/academic/exam-attempts/{attemptId}/receipt` | — | — | `200` [ExamFinalReceiptDto](#type-examfinalreceiptdto) |

<a id="api-examgradings"></a>
## تصحیح آزمون

صف، تصحیح و انتشار برای Manager یا Teacher منتسب؛ نتیجه برای Student مالک یا Parent با context فرزند. فقط تلاش Finalized قابل تصحیح است.

منبع: [ExamGradingsController](../MakanApp.Api/Controllers/ExamGradingsController.cs).

| Method | Route | Query | بدنه | پاسخ موفق |
|---|---|---|---|---|
| `GET` | `/api/v1/academic/exams/{examId}/grading` | `classId`: `Guid?`، `status`: [ExamGradingQueueStatus](#type-examgradingqueuestatus) یا null | — | `200` آرایه [ExamGradingQueueItemDto](#type-examgradingqueueitemdto) |
| `GET` | `/api/v1/academic/exam-attempts/{attemptId}/grading` | — | — | `200` [ExamGradeDto](#type-examgradedto) |
| `POST` | `/api/v1/academic/exam-attempts/{attemptId}/grading` | — | — | `200` [ExamGradeDto](#type-examgradedto) |
| `PATCH` | `/api/v1/academic/exam-attempts/{attemptId}/grading/questions/{attemptQuestionId}` | — | [GradeExamQuestionCommand](#type-gradeexamquestioncommand) | `200` [ExamGradeDto](#type-examgradedto) |
| `POST` | `/api/v1/academic/exam-attempts/{attemptId}/grading/complete` | — | [CompleteExamGradeCommand](#type-completeexamgradecommand) | `200` [ExamGradeDto](#type-examgradedto) |
| `POST` | `/api/v1/academic/exam-attempts/{attemptId}/grade/release` | — | [ReleaseExamGradeCommand](#type-releaseexamgradecommand) | `200` [ExamGradeReleaseDto](#type-examgradereleasedto) |
| `POST` | `/api/v1/academic/exam-attempts/{attemptId}/grade/corrections` | — | [CorrectReleasedExamGradeCommand](#type-correctreleasedexamgradecommand) | `201` [ExamGradeDto](#type-examgradedto) |
| `GET` | `/api/v1/academic/exam-attempts/{attemptId}/result` | — | — | `200` [StudentExamResultDto](#type-studentexamresultdto) |
| `GET` | `/api/v1/academic/exam-attempts/{attemptId}/guardian-result` | — | — | `200` [GuardianExamResultDto](#type-guardianexamresultdto) |

<a id="api-files"></a>
## فایل

نشست معتبر؛ مالکیت، scope و مجوز منبعی که فایل به آن متصل است بررسی می‌شود. دانلود stream و آپلود multipart است.

منبع: [FilesController](../MakanApp.Api/Controllers/FilesController.cs).

| Method | Route | Query | بدنه | پاسخ موفق |
|---|---|---|---|---|
| `POST` | `/api/v1/files` | — | form-data: [UploadFileRequest](#type-uploadfilerequest) | `201` [FileAssetResult](#type-fileassetresult) |
| `GET` | `/api/v1/files/{fileId}` | — | — | `200` [FileAssetResult](#type-fileassetresult) |
| `GET` | `/api/v1/files/{fileId}/content` | — | — | `200` باینری فایل |
| `DELETE` | `/api/v1/files/{fileId}` | `expectedRowVersion`: `string` | — | `200` [FileAssetResult](#type-fileassetresult) |

<a id="api-conversations"></a>
## گفت‌وگو و پیام

نشست معتبر؛ عضویت گفت‌وگو و policy ارتباط/مدیریت جاری بررسی می‌شود. Owner/Admin/Member نقش‌های خود گفت‌وگو هستند.

منبع: [ConversationsController](../MakanApp.Api/Controllers/ConversationsController.cs).

| Method | Route | Query | بدنه | پاسخ موفق |
|---|---|---|---|---|
| `POST` | `/api/v1/conversations/groups` | — | [CreateManagedConversationCommand](#type-createmanagedconversationcommand) | `200` [ManagedConversationResult](#type-managedconversationresult)؛ `201` [ManagedConversationResult](#type-managedconversationresult) |
| `POST` | `/api/v1/conversations/channels` | — | [CreateManagedConversationCommand](#type-createmanagedconversationcommand) | `200` [ManagedConversationResult](#type-managedconversationresult)؛ `201` [ManagedConversationResult](#type-managedconversationresult) |
| `POST` | `/api/v1/conversations/direct` | — | [StartDirectConversationCommand](#type-startdirectconversationcommand) | `200` [DirectConversationResult](#type-directconversationresult)؛ `201` [DirectConversationResult](#type-directconversationresult) |
| `GET` | `/api/v1/conversations` | — | — | `200` آرایه [ConversationSummaryResult](#type-conversationsummaryresult) |
| `GET` | `/api/v1/conversations/{conversationId}/messages` | `beforeSequence`: `long?`، `limit`: `int?` | — | `200` [ConversationMessagePageResult](#type-conversationmessagepageresult) |
| `POST` | `/api/v1/conversations/{conversationId}/messages` | — | [SendMessageCommand](#type-sendmessagecommand) | `200` [MessageReceiptResult](#type-messagereceiptresult) |
| `PATCH` | `/api/v1/conversations/{conversationId}/messages/{messageId}` | — | [EditMessageCommand](#type-editmessagecommand) | `200` [MessageMutationResult](#type-messagemutationresult) |
| `DELETE` | `/api/v1/conversations/{conversationId}/messages/{messageId}` | `expectedVersion`: `string` | — | `200` [MessageMutationResult](#type-messagemutationresult) |
| `POST` | `/api/v1/conversations/{conversationId}/messages/{messageId}/forward` | — | [ForwardMessageCommand](#type-forwardmessagecommand) | `200` [MessageReceiptResult](#type-messagereceiptresult) |
| `POST` | `/api/v1/conversations/{conversationId}/messages/{messageId}/reactions` | — | [AddReactionCommand](#type-addreactioncommand) | `200` [MessageReactionResult](#type-messagereactionresult) |
| `DELETE` | `/api/v1/conversations/{conversationId}/messages/{messageId}/reactions/{reaction}` | — | — | `204` بدون بدنه |
| `POST` | `/api/v1/conversations/{conversationId}/pins/{messageId}` | — | — | `200` [ConversationPinResult](#type-conversationpinresult) |
| `DELETE` | `/api/v1/conversations/{conversationId}/pins/{messageId}` | — | — | `200` [ConversationPinResult](#type-conversationpinresult) |
| `GET` | `/api/v1/conversations/{conversationId}/media` | `kind`: [MessageKind](#type-messagekind) یا null، `beforeSequence`: `long?`، `limit`: `int?` | — | `200` [ConversationMediaPageResult](#type-conversationmediapageresult) |
| `GET` | `/api/v1/conversations/{conversationId}/changes` | `afterCursor`: `string?`، `limit`: `int?` | — | `200` [ConversationChangePageResult](#type-conversationchangepageresult) |
| `POST` | `/api/v1/conversations/{conversationId}/read` | — | [AdvanceConversationCursorCommand](#type-advanceconversationcursorcommand) | `200` [ConversationCursorStateResult](#type-conversationcursorstateresult) |
| `POST` | `/api/v1/conversations/{conversationId}/delivered` | — | [AdvanceConversationCursorCommand](#type-advanceconversationcursorcommand) | `200` [ConversationCursorStateResult](#type-conversationcursorstateresult) |
| `GET` | `/api/v1/conversations/{conversationId}` | — | — | `200` [ManagedConversationResult](#type-managedconversationresult) |
| `POST` | `/api/v1/conversations/{conversationId}/members` | — | [AddConversationMemberCommand](#type-addconversationmembercommand) | `200` [ManagedConversationResult](#type-managedconversationresult) |
| `DELETE` | `/api/v1/conversations/{conversationId}/members/{targetUserId}` | — | — | `200` [ManagedConversationResult](#type-managedconversationresult) |
| `POST` | `/api/v1/conversations/{conversationId}/leave` | — | — | `200` [ManagedConversationResult](#type-managedconversationresult) |
| `PATCH` | `/api/v1/conversations/{conversationId}/members/{targetUserId}/role` | — | [ChangeConversationMemberRoleCommand](#type-changeconversationmemberrolecommand) | `200` [ManagedConversationResult](#type-managedconversationresult) |
| `POST` | `/api/v1/conversations/{conversationId}/ownership-transfers` | — | [StartOwnershipTransferCommand](#type-startownershiptransfercommand) | `201` [OwnershipTransferResult](#type-ownershiptransferresult) |
| `POST` | `/api/v1/conversations/{conversationId}/ownership-transfers/{transferId}/accept` | — | — | `200` [OwnershipTransferResult](#type-ownershiptransferresult) |
| `POST` | `/api/v1/conversations/{conversationId}/ownership-transfers/{transferId}/decline` | — | — | `200` [OwnershipTransferResult](#type-ownershiptransferresult) |
| `POST` | `/api/v1/conversations/{conversationId}/archive` | — | — | `200` [ManagedConversationResult](#type-managedconversationresult) |

<a id="api-messages"></a>
## جست‌وجوی پیام

نشست معتبر؛ جست‌وجو فقط در پیام‌ها و scopeهای قابل دسترسی کاربر.

منبع: [MessagesController](../MakanApp.Api/Controllers/MessagesController.cs).

| Method | Route | Query | بدنه | پاسخ موفق |
|---|---|---|---|---|
| `GET` | `/api/v1/messages/search` | `q`: `string?`، `conversationId`: `Guid?`، `kind`: [MessageKind](#type-messagekind) یا null، `fromUtc`: `DateTime?`، `toUtc`: `DateTime?`، `cursor`: `string?`، `limit`: `int?` | — | `200` [MessageSearchPageResult](#type-messagesearchpageresult) |

<a id="api-messagingsafety"></a>
## مسدودسازی و رسید گزارش

نشست معتبر؛ فهرست Block و رسید Report متعلق به خود کاربر.

منبع: [MessagingSafetyController](../MakanApp.Api/Controllers/MessagingSafetyController.cs).

| Method | Route | Query | بدنه | پاسخ موفق |
|---|---|---|---|---|
| `POST` | `/api/v1/messaging/blocks/{userId}` | — | — | `200` [UserBlockResult](#type-userblockresult)؛ `201` [UserBlockResult](#type-userblockresult) |
| `DELETE` | `/api/v1/messaging/blocks/{userId}` | — | — | `204` بدون بدنه |
| `GET` | `/api/v1/messaging/blocks` | — | — | `200` آرایه [BlockedUserResult](#type-blockeduserresult) |
| `GET` | `/api/v1/messaging/reports/{reportId}` | — | — | `200` [AbuseReportReceiptResult](#type-abusereportreceiptresult) |

<a id="api-messagereports"></a>
## گزارش پیام

نشست معتبر و دسترسی به پیام قابل گزارش؛ clientReportId شناسه retry است.

منبع: [MessageReportsController](../MakanApp.Api/Controllers/MessageReportsController.cs).

| Method | Route | Query | بدنه | پاسخ موفق |
|---|---|---|---|---|
| `POST` | `/api/v1/conversations/{conversationId}/messages/{messageId}/reports` | — | [ReportMessageCommand](#type-reportmessagecommand) | `201` [AbuseReportReceiptResult](#type-abusereportreceiptresult) |

<a id="contracts"></a>
## قراردادهای ورودی و خروجی

نام ستون فیلد، نام JSON است؛ نام type برای تطبیق با کد C# حفظ شده است. `Guid` رشته UUID، `DateTime` رشته زمان UTC، `DateOnly` تاریخ، `TimeOnly` ساعت، `decimal` عدد JSON و `IReadOnlyCollection` آرایه JSON است. `IFormFile` فقط بخش فایل multipart است. `SendMessageCommand.kind` در صورت حذف، پیش‌فرض `Text` دارد.

<a id="type-abusereportreceiptresult"></a>
### AbuseReportReceiptResult

منبع: [MessagingSafetyContracts.cs](../MakanApp.Application/Messaging/MessagingSafetyContracts.cs).

| فیلد | نوع |
|---|---|
| `reportId` | `Guid` |
| `conversationId` | `Guid` |
| `messageId` | `Guid` |
| `reason` | [AbuseReportReason](#type-abusereportreason) |
| `status` | [AbuseReportStatus](#type-abusereportstatus) |
| `createdAtUtc` | `DateTime` |

<a id="type-academicperiodresult"></a>
### AcademicPeriodResult

منبع: [AcademicContracts.cs](../MakanApp.Application/Academic/AcademicContracts.cs).

| فیلد | نوع |
|---|---|
| `id` | `Guid` |
| `organizationId` | `Guid` |
| `title` | `string` |
| `startDate` | `DateOnly` |
| `endDate` | `DateOnly` |
| `status` | [AcademicPeriodStatus](#type-academicperiodstatus) |

<a id="type-acceptinvitationresult"></a>
### AcceptInvitationResult

منبع: [OrganizationContracts.cs](../MakanApp.Application/Organization/OrganizationContracts.cs).

| فیلد | نوع |
|---|---|
| `invitationId` | `Guid` |
| `organizationId` | `Guid` |
| `organizationName` | `string` |
| `membershipId` | `Guid` |
| `roleAssignmentId` | `Guid` |
| `role` | [OrganizationRole](#type-organizationrole) |
| `alreadyAccepted` | `bool` |

<a id="type-accesscontext"></a>
### AccessContext

منبع: [OrganizationContracts.cs](../MakanApp.Application/Organization/OrganizationContracts.cs).

| فیلد | نوع |
|---|---|
| `actorId` | `Guid` |
| `userId` | `Guid` |
| `sessionId` | `Guid` |
| `workspaceType` | [WorkspaceType](#type-workspacetype) |
| `organizationId` | `Guid?` |
| `membershipId` | `Guid?` |
| `activeRole` | [OrganizationRole](#type-organizationrole) یا null |
| `subjectOrganizationPersonId` | `Guid?` |

<a id="type-addconversationmembercommand"></a>
### AddConversationMemberCommand

منبع: [ConversationManagementContracts.cs](../MakanApp.Application/Messaging/ConversationManagementContracts.cs).

| فیلد | نوع |
|---|---|
| `userId` | `Guid` |

<a id="type-addexamquestioncommand"></a>
### AddExamQuestionCommand

منبع: [ExamContracts.cs](../MakanApp.Application/Assessment/ExamContracts.cs).

| فیلد | نوع |
|---|---|
| `order` | `int` |
| `type` | [ExamQuestionType](#type-examquestiontype) |
| `prompt` | `string` |
| `score` | `decimal` |
| `options` | آرایه [ExamQuestionOptionCommand](#type-examquestionoptioncommand) یا null |
| `expectedVersionRowVersion` | `string` |

<a id="type-addreactioncommand"></a>
### AddReactionCommand

منبع: [MessagingContracts.cs](../MakanApp.Application/Messaging/MessagingContracts.cs).

| فیلد | نوع |
|---|---|
| `reaction` | [MessageReactionType](#type-messagereactiontype) |

<a id="type-advanceconversationcursorcommand"></a>
### AdvanceConversationCursorCommand

منبع: [MessagingContracts.cs](../MakanApp.Application/Messaging/MessagingContracts.cs).

| فیلد | نوع |
|---|---|
| `upToMessageSequence` | `long` |

<a id="type-assignteachercommand"></a>
### AssignTeacherCommand

منبع: [AcademicContracts.cs](../MakanApp.Application/Academic/AcademicContracts.cs).

| فیلد | نوع |
|---|---|
| `teacherMembershipId` | `Guid` |

<a id="type-assignmentresult"></a>
### AssignmentResult

منبع: [AssignmentContracts.cs](../MakanApp.Application/Assessment/AssignmentContracts.cs).

| فیلد | نوع |
|---|---|
| `id` | `Guid` |
| `organizationId` | `Guid` |
| `classId` | `Guid` |
| `classTitle` | `string` |
| `status` | [AssignmentStatus](#type-assignmentstatus) |
| `createdAtUtc` | `DateTime` |
| `updatedAtUtc` | `DateTime` |
| `publishedAtUtc` | `DateTime?` |
| `versionId` | `Guid` |
| `versionNumber` | `int` |
| `title` | `string` |
| `description` | `string` |
| `dueAtUtc` | `DateTime` |
| `allowLateSubmission` | `bool` |
| `maxAttempts` | `int` |
| `maxScore` | `decimal` |
| `assignmentRowVersion` | `string` |
| `versionRowVersion` | `string` |

<a id="type-attachsubmissionfilecommand"></a>
### AttachSubmissionFileCommand

منبع: [SubmissionContracts.cs](../MakanApp.Application/Assessment/SubmissionContracts.cs).

| فیلد | نوع |
|---|---|
| `fileAssetId` | `Guid` |
| `expectedRowVersion` | `string` |

<a id="type-attendanceentryresult"></a>
### AttendanceEntryResult

منبع: [AcademicSessionContracts.cs](../MakanApp.Application/Academic/AcademicSessionContracts.cs).

| فیلد | نوع |
|---|---|
| `attendanceId` | `Guid?` |
| `enrollmentId` | `Guid` |
| `learnerOrganizationPersonId` | `Guid` |
| `status` | [AttendanceStatus](#type-attendancestatus) |
| `recordedAtUtc` | `DateTime?` |
| `recordedByMembershipId` | `Guid?` |
| `rowVersion` | `string?` |

<a id="type-attendancerevisionresult"></a>
### AttendanceRevisionResult

منبع: [AcademicSessionContracts.cs](../MakanApp.Application/Academic/AcademicSessionContracts.cs).

| فیلد | نوع |
|---|---|
| `id` | `Guid` |
| `previousStatus` | [AttendanceStatus](#type-attendancestatus) |
| `newStatus` | [AttendanceStatus](#type-attendancestatus) |
| `correctedAtUtc` | `DateTime` |
| `correctedByMembershipId` | `Guid` |
| `reason` | `string` |

<a id="type-authorizedchildresult"></a>
### AuthorizedChildResult

منبع: [GuardianContracts.cs](../MakanApp.Application/Guardian/GuardianContracts.cs).

| فیلد | نوع |
|---|---|
| `learnerOrganizationPersonId` | `Guid` |
| `displayName` | `string` |
| `organizationId` | `Guid` |
| `organizationName` | `string` |
| `relationStatus` | [GuardianRelationStatus](#type-guardianrelationstatus) |

<a id="type-blockeduserresult"></a>
### BlockedUserResult

منبع: [MessagingSafetyContracts.cs](../MakanApp.Application/Messaging/MessagingSafetyContracts.cs).

| فیلد | نوع |
|---|---|
| `blockId` | `Guid` |
| `user` | [SafeMessagingIdentityResult](#type-safemessagingidentityresult) |
| `createdAtUtc` | `DateTime` |
| `version` | `string` |

<a id="type-changeconversationmemberrolecommand"></a>
### ChangeConversationMemberRoleCommand

منبع: [ConversationManagementContracts.cs](../MakanApp.Application/Messaging/ConversationManagementContracts.cs).

| فیلد | نوع |
|---|---|
| `role` | [ConversationParticipantRole](#type-conversationparticipantrole) |

<a id="type-classresult"></a>
### ClassResult

منبع: [AcademicContracts.cs](../MakanApp.Application/Academic/AcademicContracts.cs).

| فیلد | نوع |
|---|---|
| `id` | `Guid` |
| `organizationId` | `Guid` |
| `academicPeriodId` | `Guid` |
| `academicPeriodTitle` | `string` |
| `courseId` | `Guid` |
| `courseTitle` | `string` |
| `title` | `string` |
| `capacity` | `int` |
| `status` | [ClassStatus](#type-classstatus) |

<a id="type-completeexamgradecommand"></a>
### CompleteExamGradeCommand

منبع: [ExamGradingContracts.cs](../MakanApp.Application/Assessment/ExamGradingContracts.cs).

| فیلد | نوع |
|---|---|
| `learnerFeedback` | `string?` |
| `guardianVisibleFeedback` | `string?` |
| `evaluatorPrivateNote` | `string?` |
| `expectedGradeRowVersion` | `string` |

<a id="type-completeprofilecommand"></a>
### CompleteProfileCommand

منبع: [IdentityContracts.cs](../MakanApp.Application/Identity/IdentityContracts.cs).

| فیلد | نوع |
|---|---|
| `firstName` | `string` |
| `lastName` | `string` |
| `displayName` | `string?` |
| `username` | `string` |

<a id="type-conversationchangepageresult"></a>
### ConversationChangePageResult

منبع: [MessagingContracts.cs](../MakanApp.Application/Messaging/MessagingContracts.cs).

| فیلد | نوع |
|---|---|
| `changes` | آرایه [ConversationChangeResult](#type-conversationchangeresult) |
| `nextCursor` | `string` |
| `hasMore` | `bool` |

<a id="type-conversationchangeresult"></a>
### ConversationChangeResult

منبع: [MessagingContracts.cs](../MakanApp.Application/Messaging/MessagingContracts.cs).

| فیلد | نوع |
|---|---|
| `changeId` | `Guid` |
| `type` | [MessagingChangeType](#type-messagingchangetype) |
| `resourceId` | `Guid` |
| `resourceVersion` | `string?` |
| `cursor` | `string` |
| `occurredAtUtc` | `DateTime` |
| `actorUserId` | `Guid?` |
| `payloadVersion` | `int` |

<a id="type-conversationcursorstateresult"></a>
### ConversationCursorStateResult

منبع: [MessagingContracts.cs](../MakanApp.Application/Messaging/MessagingContracts.cs).

| فیلد | نوع |
|---|---|
| `conversationId` | `Guid` |
| `lastDeliveredMessageSequence` | `long` |
| `lastReadMessageSequence` | `long` |
| `unreadCount` | `int` |
| `updatedAtUtc` | `DateTime?` |
| `version` | `string` |

<a id="type-conversationmediaitemresult"></a>
### ConversationMediaItemResult

منبع: [MessagingContracts.cs](../MakanApp.Application/Messaging/MessagingContracts.cs).

| فیلد | نوع |
|---|---|
| `messageId` | `Guid` |
| `sequence` | `long` |
| `senderUserId` | `Guid` |
| `sentAtUtc` | `DateTime` |
| `attachment` | [MessageAttachmentResult](#type-messageattachmentresult) |

<a id="type-conversationmediapageresult"></a>
### ConversationMediaPageResult

منبع: [MessagingContracts.cs](../MakanApp.Application/Messaging/MessagingContracts.cs).

| فیلد | نوع |
|---|---|
| `items` | آرایه [ConversationMediaItemResult](#type-conversationmediaitemresult) |
| `nextBeforeSequence` | `long?` |

<a id="type-conversationmessagepageresult"></a>
### ConversationMessagePageResult

منبع: [MessagingContracts.cs](../MakanApp.Application/Messaging/MessagingContracts.cs).

| فیلد | نوع |
|---|---|
| `messages` | آرایه [ConversationMessageResult](#type-conversationmessageresult) |
| `nextBeforeSequence` | `long?` |

<a id="type-conversationmessageresult"></a>
### ConversationMessageResult

منبع: [MessagingContracts.cs](../MakanApp.Application/Messaging/MessagingContracts.cs).

| فیلد | نوع |
|---|---|
| `messageId` | `Guid` |
| `conversationId` | `Guid` |
| `senderUserId` | `Guid` |
| `sender` | [SafeMessagingIdentityResult](#type-safemessagingidentityresult) |
| `clientMessageId` | `Guid` |
| `sequence` | `long` |
| `kind` | [MessageKind](#type-messagekind) یا null |
| `text` | `string?` |
| `sentAtUtc` | `DateTime` |
| `editedAtUtc` | `DateTime?` |
| `deletedAtUtc` | `DateTime?` |
| `isEdited` | `bool` |
| `isDeleted` | `bool` |
| `reply` | [MessageReplySummaryResult](#type-messagereplysummaryresult) یا null |
| `isForwarded` | `bool` |
| `reactions` | آرایه [MessageReactionSummaryResult](#type-messagereactionsummaryresult) |
| `mentions` | آرایه [SafeMessagingIdentityResult](#type-safemessagingidentityresult) |
| `attachments` | آرایه [MessageAttachmentResult](#type-messageattachmentresult) |
| `isPinned` | `bool` |
| `version` | `string` |

<a id="type-conversationparticipantresult"></a>
### ConversationParticipantResult

منبع: [ConversationManagementContracts.cs](../MakanApp.Application/Messaging/ConversationManagementContracts.cs).

| فیلد | نوع |
|---|---|
| `participantId` | `Guid` |
| `user` | [SafeMessagingIdentityResult](#type-safemessagingidentityresult) |
| `role` | [ConversationParticipantRole](#type-conversationparticipantrole) |
| `status` | [ConversationParticipantStatus](#type-conversationparticipantstatus) |
| `joinedAtUtc` | `DateTime` |
| `endedAtUtc` | `DateTime?` |

<a id="type-conversationpinresult"></a>
### ConversationPinResult

منبع: [MessagingContracts.cs](../MakanApp.Application/Messaging/MessagingContracts.cs).

| فیلد | نوع |
|---|---|
| `conversationId` | `Guid` |
| `messageId` | `Guid` |
| `pinnedByUserId` | `Guid` |
| `pinnedAtUtc` | `DateTime` |
| `isPinned` | `bool` |

<a id="type-conversationsummaryresult"></a>
### ConversationSummaryResult

منبع: [MessagingContracts.cs](../MakanApp.Application/Messaging/MessagingContracts.cs).

| فیلد | نوع |
|---|---|
| `conversationId` | `Guid` |
| `type` | [ConversationType](#type-conversationtype) |
| `scope` | [ConversationScope](#type-conversationscope) |
| `organizationId` | `Guid?` |
| `title` | `string?` |
| `otherParticipant` | [SafeMessagingIdentityResult](#type-safemessagingidentityresult) یا null |
| `lastMessagePreview` | `string?` |
| `lastMessageAtUtc` | `DateTime?` |
| `lastMessageSequence` | `long?` |
| `lastDeliveredMessageSequence` | `long` |
| `lastReadMessageSequence` | `long` |
| `unreadCount` | `int` |

<a id="type-correctattendancecommand"></a>
### CorrectAttendanceCommand

منبع: [AcademicSessionContracts.cs](../MakanApp.Application/Academic/AcademicSessionContracts.cs).

| فیلد | نوع |
|---|---|
| `status` | [AttendanceStatus](#type-attendancestatus) |
| `correctionReason` | `string` |
| `expectedRowVersion` | `string` |

<a id="type-correctreleasedevaluationcommand"></a>
### CorrectReleasedEvaluationCommand

منبع: [EvaluationContracts.cs](../MakanApp.Application/Assessment/EvaluationContracts.cs).

| فیلد | نوع |
|---|---|
| `score` | `decimal` |
| `learnerFeedback` | `string?` |
| `guardianVisibleFeedback` | `string?` |
| `teacherPrivateNote` | `string?` |
| `correctionReason` | `string` |
| `expectedReleasedEvaluationRowVersion` | `string` |

<a id="type-correctreleasedexamgradecommand"></a>
### CorrectReleasedExamGradeCommand

منبع: [ExamGradingContracts.cs](../MakanApp.Application/Assessment/ExamGradingContracts.cs).

| فیلد | نوع |
|---|---|
| `correctionReason` | `string` |
| `expectedReleasedGradeRowVersion` | `string` |

<a id="type-courseresult"></a>
### CourseResult

منبع: [AcademicContracts.cs](../MakanApp.Application/Academic/AcademicContracts.cs).

| فیلد | نوع |
|---|---|
| `id` | `Guid` |
| `organizationId` | `Guid` |
| `title` | `string` |
| `status` | [CourseStatus](#type-coursestatus) |

<a id="type-createacademicperiodcommand"></a>
### CreateAcademicPeriodCommand

منبع: [AcademicContracts.cs](../MakanApp.Application/Academic/AcademicContracts.cs).

| فیلد | نوع |
|---|---|
| `title` | `string` |
| `startDate` | `DateOnly` |
| `endDate` | `DateOnly` |

<a id="type-createassignmentdraftcommand"></a>
### CreateAssignmentDraftCommand

منبع: [AssignmentContracts.cs](../MakanApp.Application/Assessment/AssignmentContracts.cs).

| فیلد | نوع |
|---|---|
| `title` | `string` |
| `description` | `string` |
| `dueAtUtc` | `DateTime` |
| `allowLateSubmission` | `bool` |
| `maxAttempts` | `int` |
| `maxScore` | `decimal` |

<a id="type-createclasscommand"></a>
### CreateClassCommand

منبع: [AcademicContracts.cs](../MakanApp.Application/Academic/AcademicContracts.cs).

| فیلد | نوع |
|---|---|
| `academicPeriodId` | `Guid` |
| `courseId` | `Guid` |
| `title` | `string` |
| `capacity` | `int` |
| `activateImmediately` | `bool` |

<a id="type-createcoursecommand"></a>
### CreateCourseCommand

منبع: [AcademicContracts.cs](../MakanApp.Application/Academic/AcademicContracts.cs).

| فیلد | نوع |
|---|---|
| `title` | `string` |

<a id="type-createexamdraftcommand"></a>
### CreateExamDraftCommand

منبع: [ExamContracts.cs](../MakanApp.Application/Assessment/ExamContracts.cs).

| فیلد | نوع |
|---|---|
| `title` | `string` |
| `description` | `string?` |
| `availableFromUtc` | `DateTime` |
| `availableUntilUtc` | `DateTime` |
| `durationMinutes` | `int` |
| `maxAttempts` | `int` |
| `maxScore` | `decimal` |
| `randomizationPolicy` | [ExamRandomizationPolicy](#type-examrandomizationpolicy) |

<a id="type-createmanagedconversationcommand"></a>
### CreateManagedConversationCommand

منبع: [ConversationManagementContracts.cs](../MakanApp.Application/Messaging/ConversationManagementContracts.cs).

| فیلد | نوع |
|---|---|
| `clientOperationId` | `Guid` |
| `scope` | [ConversationScope](#type-conversationscope) |
| `title` | `string` |
| `description` | `string?` |
| `initialParticipantUserIds` | `IReadOnlyCollection<Guid>?` |

<a id="type-createnextexamversioncommand"></a>
### CreateNextExamVersionCommand

منبع: [ExamContracts.cs](../MakanApp.Application/Assessment/ExamContracts.cs).

| فیلد | نوع |
|---|---|
| `expectedExamRowVersion` | `string` |
| `expectedPublishedVersionRowVersion` | `string` |

<a id="type-createschedulerulecommand"></a>
### CreateScheduleRuleCommand

منبع: [AcademicSessionContracts.cs](../MakanApp.Application/Academic/AcademicSessionContracts.cs).

| فیلد | نوع |
|---|---|
| `localDayOfWeek` | [DayOfWeek](#type-dayofweek) |
| `localStartTime` | `TimeOnly` |
| `durationMinutes` | `int` |
| `timeZoneId` | `string` |
| `effectiveFrom` | `DateOnly` |
| `effectiveUntil` | `DateOnly` |
| `sessionTitle` | `string` |
| `meetingUrl` | `string?` |

<a id="type-createsessioncommand"></a>
### CreateSessionCommand

منبع: [AcademicSessionContracts.cs](../MakanApp.Application/Academic/AcademicSessionContracts.cs).

| فیلد | نوع |
|---|---|
| `title` | `string` |
| `startUtc` | `DateTime` |
| `endUtc` | `DateTime` |
| `timeZoneId` | `string` |
| `meetingUrl` | `string?` |

<a id="type-currentuserresult"></a>
### CurrentUserResult

منبع: [IdentityContracts.cs](../MakanApp.Application/Identity/IdentityContracts.cs).

| فیلد | نوع |
|---|---|
| `id` | `Guid` |
| `phoneNumber` | `string` |
| `isProfileComplete` | `bool` |
| `firstName` | `string?` |
| `lastName` | `string?` |
| `displayName` | `string?` |
| `username` | `string?` |

<a id="type-declineinvitationresult"></a>
### DeclineInvitationResult

منبع: [OrganizationContracts.cs](../MakanApp.Application/Organization/OrganizationContracts.cs).

| فیلد | نوع |
|---|---|
| `invitationId` | `Guid` |
| `status` | [InvitationStatus](#type-invitationstatus) |

<a id="type-deleteexamquestioncommand"></a>
### DeleteExamQuestionCommand

منبع: [ExamContracts.cs](../MakanApp.Application/Assessment/ExamContracts.cs).

| فیلد | نوع |
|---|---|
| `expectedVersionRowVersion` | `string` |
| `expectedQuestionRowVersion` | `string` |

<a id="type-directconversationresult"></a>
### DirectConversationResult

منبع: [MessagingContracts.cs](../MakanApp.Application/Messaging/MessagingContracts.cs).

| فیلد | نوع |
|---|---|
| `conversationId` | `Guid` |
| `type` | [ConversationType](#type-conversationtype) |
| `scope` | [ConversationScope](#type-conversationscope) |
| `organizationId` | `Guid?` |
| `otherParticipant` | [SafeMessagingIdentityResult](#type-safemessagingidentityresult) |
| `createdAtUtc` | `DateTime` |
| `alreadyExisted` | `bool` |

<a id="type-editmessagecommand"></a>
### EditMessageCommand

منبع: [MessagingContracts.cs](../MakanApp.Application/Messaging/MessagingContracts.cs).

| فیلد | نوع |
|---|---|
| `text` | `string?` |
| `expectedVersion` | `string` |

<a id="type-endenrollmentcommand"></a>
### EndEnrollmentCommand

منبع: [AcademicContracts.cs](../MakanApp.Application/Academic/AcademicContracts.cs).

| فیلد | نوع |
|---|---|
| `finalStatus` | [EnrollmentStatus](#type-enrollmentstatus) |

<a id="type-enrolllearnercommand"></a>
### EnrollLearnerCommand

منبع: [AcademicContracts.cs](../MakanApp.Application/Academic/AcademicContracts.cs).

| فیلد | نوع |
|---|---|
| `learnerOrganizationPersonId` | `Guid` |

<a id="type-enrollmentresult"></a>
### EnrollmentResult

منبع: [AcademicContracts.cs](../MakanApp.Application/Academic/AcademicContracts.cs).

| فیلد | نوع |
|---|---|
| `id` | `Guid` |
| `organizationId` | `Guid` |
| `classId` | `Guid` |
| `learnerOrganizationPersonId` | `Guid` |
| `status` | [EnrollmentStatus](#type-enrollmentstatus) |
| `enrolledAtUtc` | `DateTime` |
| `endedAtUtc` | `DateTime?` |

<a id="type-evaluationqueueitemresult"></a>
### EvaluationQueueItemResult

منبع: [EvaluationContracts.cs](../MakanApp.Application/Assessment/EvaluationContracts.cs).

| فیلد | نوع |
|---|---|
| `submissionAttemptId` | `Guid` |
| `assignmentId` | `Guid` |
| `assignmentVersionId` | `Guid` |
| `classId` | `Guid` |
| `assignmentTitle` | `string` |
| `studentDisplayName` | `string` |
| `attemptNumber` | `int` |
| `submittedAtUtc` | `DateTime` |
| `isLate` | `bool` |
| `maxScore` | `decimal` |
| `reviewStatus` | [EvaluationReviewStatus](#type-evaluationreviewstatus) |
| `evaluationRevisionNumber` | `int?` |
| `score` | `decimal?` |

<a id="type-evaluatorevaluationresult"></a>
### EvaluatorEvaluationResult

منبع: [EvaluationContracts.cs](../MakanApp.Application/Assessment/EvaluationContracts.cs).

| فیلد | نوع |
|---|---|
| `id` | `Guid` |
| `submissionAttemptId` | `Guid` |
| `revisionNumber` | `int` |
| `score` | `decimal` |
| `maxScore` | `decimal` |
| `learnerFeedback` | `string?` |
| `guardianVisibleFeedback` | `string?` |
| `teacherPrivateNote` | `string?` |
| `status` | [EvaluationRevisionStatus](#type-evaluationrevisionstatus) |
| `createdByMembershipId` | `Guid` |
| `createdAtUtc` | `DateTime` |
| `updatedAtUtc` | `DateTime` |
| `supersedesEvaluationRevisionId` | `Guid?` |
| `correctionReason` | `string?` |
| `releasedAtUtc` | `DateTime?` |
| `rowVersion` | `string` |

<a id="type-examanswerreceiptdto"></a>
### ExamAnswerReceiptDto

منبع: [ExamAnswerContracts.cs](../MakanApp.Application/Assessment/ExamAnswerContracts.cs).

| فیلد | نوع |
|---|---|
| `examAttemptId` | `Guid` |
| `examAttemptQuestionId` | `Guid` |
| `answerRevisionId` | `Guid` |
| `revisionNumber` | `int` |
| `acceptedAtUtc` | `DateTime` |
| `writeLeaseVersion` | `long` |
| `answerSetVersion` | `long` |
| `status` | `string` |

<a id="type-exameditordto"></a>
### ExamEditorDto

منبع: [ExamContracts.cs](../MakanApp.Application/Assessment/ExamContracts.cs).

| فیلد | نوع |
|---|---|
| `id` | `Guid` |
| `organizationId` | `Guid` |
| `classId` | `Guid` |
| `classTitle` | `string` |
| `status` | [ExamStatus](#type-examstatus) |
| `createdAtUtc` | `DateTime` |
| `examRowVersion` | `string` |
| `versionId` | `Guid` |
| `versionNumber` | `int` |
| `versionStatus` | [ExamVersionStatus](#type-examversionstatus) |
| `title` | `string` |
| `description` | `string?` |
| `availableFromUtc` | `DateTime` |
| `availableUntilUtc` | `DateTime` |
| `durationMinutes` | `int` |
| `maxAttempts` | `int` |
| `maxScore` | `decimal` |
| `randomizationPolicy` | [ExamRandomizationPolicy](#type-examrandomizationpolicy) |
| `publishedAtUtc` | `DateTime?` |
| `versionRowVersion` | `string` |
| `questions` | آرایه [ExamEditorQuestionDto](#type-exameditorquestiondto) |

<a id="type-exameditoroptiondto"></a>
### ExamEditorOptionDto

منبع: [ExamContracts.cs](../MakanApp.Application/Assessment/ExamContracts.cs).

| فیلد | نوع |
|---|---|
| `id` | `Guid` |
| `order` | `int` |
| `text` | `string` |
| `isCorrect` | `bool` |

<a id="type-exameditorquestiondto"></a>
### ExamEditorQuestionDto

منبع: [ExamContracts.cs](../MakanApp.Application/Assessment/ExamContracts.cs).

| فیلد | نوع |
|---|---|
| `id` | `Guid` |
| `order` | `int` |
| `type` | [ExamQuestionType](#type-examquestiontype) |
| `prompt` | `string` |
| `score` | `decimal` |
| `options` | آرایه [ExamEditorOptionDto](#type-exameditoroptiondto) |
| `rowVersion` | `string` |

<a id="type-examfinalreceiptdto"></a>
### ExamFinalReceiptDto

منبع: [ExamFinalizationContracts.cs](../MakanApp.Application/Assessment/ExamFinalizationContracts.cs).

| فیلد | نوع |
|---|---|
| `examAttemptId` | `Guid` |
| `examId` | `Guid` |
| `examVersionId` | `Guid` |
| `attemptNumber` | `int` |
| `startedAtUtc` | `DateTime` |
| `finalizedAtUtc` | `DateTime` |
| `finalizedAnswerSetVersion` | `long` |
| `answeredQuestionCount` | `int` |
| `totalQuestionCount` | `int` |
| `status` | [ExamAttemptStatus](#type-examattemptstatus) |

<a id="type-examgradedto"></a>
### ExamGradeDto

منبع: [ExamGradingContracts.cs](../MakanApp.Application/Assessment/ExamGradingContracts.cs).

| فیلد | نوع |
|---|---|
| `examGradeRevisionId` | `Guid` |
| `examAttemptId` | `Guid` |
| `examId` | `Guid` |
| `examVersionId` | `Guid` |
| `classId` | `Guid` |
| `examTitle` | `string` |
| `studentDisplayName` | `string` |
| `attemptNumber` | `int` |
| `finalizedAtUtc` | `DateTime` |
| `revisionNumber` | `int` |
| `totalScore` | `decimal` |
| `maximumScore` | `decimal` |
| `status` | [ExamGradeRevisionStatus](#type-examgraderevisionstatus) |
| `learnerFeedback` | `string?` |
| `guardianVisibleFeedback` | `string?` |
| `evaluatorPrivateNote` | `string?` |
| `createdByMembershipId` | `Guid` |
| `createdAtUtc` | `DateTime` |
| `updatedAtUtc` | `DateTime` |
| `supersedesExamGradeRevisionId` | `Guid?` |
| `correctionReason` | `string?` |
| `releasedAtUtc` | `DateTime?` |
| `rowVersion` | `string` |
| `questions` | آرایه [ExamQuestionGradeDto](#type-examquestiongradedto) |

<a id="type-examgradereleasedto"></a>
### ExamGradeReleaseDto

منبع: [ExamGradingContracts.cs](../MakanApp.Application/Assessment/ExamGradingContracts.cs).

| فیلد | نوع |
|---|---|
| `examGradeReleaseId` | `Guid` |
| `examAttemptId` | `Guid` |
| `examGradeRevisionId` | `Guid` |
| `gradeRevisionNumber` | `int` |
| `totalScore` | `decimal` |
| `maximumScore` | `decimal` |
| `releasedAtUtc` | `DateTime` |
| `releasedByMembershipId` | `Guid` |
| `gradeRowVersion` | `string` |

<a id="type-examgradingqueueitemdto"></a>
### ExamGradingQueueItemDto

منبع: [ExamGradingContracts.cs](../MakanApp.Application/Assessment/ExamGradingContracts.cs).

| فیلد | نوع |
|---|---|
| `examAttemptId` | `Guid` |
| `examId` | `Guid` |
| `examVersionId` | `Guid` |
| `classId` | `Guid` |
| `examTitle` | `string` |
| `studentDisplayName` | `string` |
| `attemptNumber` | `int` |
| `finalizedAtUtc` | `DateTime` |
| `maximumScore` | `decimal` |
| `status` | [ExamGradingQueueStatus](#type-examgradingqueuestatus) |
| `gradeRevisionNumber` | `int?` |
| `requiresManualReview` | `bool` |

<a id="type-examquestiongradedto"></a>
### ExamQuestionGradeDto

منبع: [ExamGradingContracts.cs](../MakanApp.Application/Assessment/ExamGradingContracts.cs).

| فیلد | نوع |
|---|---|
| `examAttemptQuestionId` | `Guid` |
| `questionVersionId` | `Guid` |
| `displayOrder` | `int` |
| `questionType` | [ExamQuestionType](#type-examquestiontype) |
| `prompt` | `string` |
| `isAnswered` | `bool` |
| `selectedOptionText` | `string?` |
| `textAnswer` | `string?` |
| `awardedScore` | `decimal` |
| `maximumScore` | `decimal` |
| `gradingMode` | [ExamQuestionGradingMode](#type-examquestiongradingmode) |
| `isReviewed` | `bool` |
| `learnerFeedback` | `string?` |
| `evaluatorPrivateNote` | `string?` |
| `reviewedByMembershipId` | `Guid?` |
| `reviewedAtUtc` | `DateTime?` |

<a id="type-examquestionoptioncommand"></a>
### ExamQuestionOptionCommand

منبع: [ExamContracts.cs](../MakanApp.Application/Assessment/ExamContracts.cs).

| فیلد | نوع |
|---|---|
| `order` | `int` |
| `text` | `string` |
| `isCorrect` | `bool` |

<a id="type-examteacherpreview"></a>
### ExamTeacherPreview

منبع: [ExamContracts.cs](../MakanApp.Application/Assessment/ExamContracts.cs).

| فیلد | نوع |
|---|---|
| `exam` | [StudentExamSummary](#type-studentexamsummary) |
| `questions` | آرایه [ExamEditorQuestionDto](#type-exameditorquestiondto) |

<a id="type-examwriteleasedto"></a>
### ExamWriteLeaseDto

منبع: [ExamAnswerContracts.cs](../MakanApp.Application/Assessment/ExamAnswerContracts.cs).

| فیلد | نوع |
|---|---|
| `examAttemptId` | `Guid` |
| `isCurrentSessionWriter` | `bool` |
| `writeLeaseVersion` | `long` |
| `canRequestTransfer` | `bool` |
| `acquiredAtUtc` | `DateTime?` |
| `answerSetVersion` | `long` |
| `serverNowUtc` | `DateTime` |

<a id="type-fileassetresult"></a>
### FileAssetResult

منبع: [StorageContracts.cs](../MakanApp.Application/Storage/StorageContracts.cs).

| فیلد | نوع |
|---|---|
| `id` | `Guid` |
| `organizationId` | `Guid?` |
| `uploadedByUserId` | `Guid` |
| `originalFileName` | `string` |
| `contentType` | `string` |
| `sizeBytes` | `long` |
| `sha256Hash` | `string?` |
| `status` | [FileAssetStatus](#type-fileassetstatus) |
| `createdAtUtc` | `DateTime` |
| `completedAtUtc` | `DateTime?` |
| `deletedAtUtc` | `DateTime?` |
| `retainedAtUtc` | `DateTime?` |
| `unattachedExpiresAtUtc` | `DateTime` |
| `rowVersion` | `string` |

<a id="type-finalsubmitassignmentcommand"></a>
### FinalSubmitAssignmentCommand

منبع: [SubmissionContracts.cs](../MakanApp.Application/Assessment/SubmissionContracts.cs).

| فیلد | نوع |
|---|---|
| `expectedRowVersion` | `string` |

<a id="type-finalizeexamcommand"></a>
### FinalizeExamCommand

منبع: [ExamFinalizationContracts.cs](../MakanApp.Application/Assessment/ExamFinalizationContracts.cs).

| فیلد | نوع |
|---|---|
| `clientOperationId` | `Guid` |
| `expectedAnswerSetVersion` | `long` |
| `writeLeaseVersion` | `long` |

<a id="type-forwardmessagecommand"></a>
### ForwardMessageCommand

منبع: [MessagingContracts.cs](../MakanApp.Application/Messaging/MessagingContracts.cs).

| فیلد | نوع |
|---|---|
| `destinationConversationId` | `Guid` |
| `clientMessageId` | `Guid` |

<a id="type-gradeexamquestioncommand"></a>
### GradeExamQuestionCommand

منبع: [ExamGradingContracts.cs](../MakanApp.Application/Assessment/ExamGradingContracts.cs).

| فیلد | نوع |
|---|---|
| `awardedScore` | `decimal` |
| `learnerFeedback` | `string?` |
| `evaluatorPrivateNote` | `string?` |
| `expectedGradeRowVersion` | `string` |

<a id="type-gradereleaseresult"></a>
### GradeReleaseResult

منبع: [EvaluationContracts.cs](../MakanApp.Application/Assessment/EvaluationContracts.cs).

| فیلد | نوع |
|---|---|
| `gradeReleaseId` | `Guid` |
| `submissionAttemptId` | `Guid` |
| `evaluationRevisionId` | `Guid` |
| `revisionNumber` | `int` |
| `score` | `decimal` |
| `releasedAtUtc` | `DateTime` |
| `releasedByMembershipId` | `Guid` |
| `evaluationRowVersion` | `string` |

<a id="type-guardianexamresultdto"></a>
### GuardianExamResultDto

منبع: [ExamGradingContracts.cs](../MakanApp.Application/Assessment/ExamGradingContracts.cs).

| فیلد | نوع |
|---|---|
| `examAttemptId` | `Guid` |
| `examId` | `Guid` |
| `examVersionId` | `Guid` |
| `attemptNumber` | `int` |
| `finalizedAtUtc` | `DateTime` |
| `releaseStatus` | [ExamResultReleaseStatus](#type-examresultreleasestatus) |
| `gradeRevisionNumber` | `int?` |
| `releasedAtUtc` | `DateTime?` |
| `score` | `decimal?` |
| `maximumScore` | `decimal?` |
| `guardianVisibleFeedback` | `string?` |

<a id="type-guardianrelationsummaryresult"></a>
### GuardianRelationSummaryResult

منبع: [GuardianContracts.cs](../MakanApp.Application/Guardian/GuardianContracts.cs).

| فیلد | نوع |
|---|---|
| `relationId` | `Guid` |
| `learnerOrganizationPersonId` | `Guid` |
| `displayName` | `string` |
| `organizationId` | `Guid` |
| `organizationName` | `string` |
| `status` | [GuardianRelationStatus](#type-guardianrelationstatus) |
| `validFromUtc` | `DateTime` |
| `createdAtUtc` | `DateTime` |

<a id="type-invitationresult"></a>
### InvitationResult

منبع: [OrganizationContracts.cs](../MakanApp.Application/Organization/OrganizationContracts.cs).

| فیلد | نوع |
|---|---|
| `id` | `Guid` |
| `organizationId` | `Guid` |
| `organizationName` | `string` |
| `invitedByUserId` | `Guid` |
| `role` | [OrganizationRole](#type-organizationrole) |
| `status` | [InvitationStatus](#type-invitationstatus) |
| `expiresAtUtc` | `DateTime` |

<a id="type-managedconversationresult"></a>
### ManagedConversationResult

منبع: [ConversationManagementContracts.cs](../MakanApp.Application/Messaging/ConversationManagementContracts.cs).

| فیلد | نوع |
|---|---|
| `conversationId` | `Guid` |
| `type` | [ConversationType](#type-conversationtype) |
| `scope` | [ConversationScope](#type-conversationscope) |
| `organizationId` | `Guid?` |
| `title` | `string` |
| `description` | `string?` |
| `managementPolicy` | [ConversationManagementPolicy](#type-conversationmanagementpolicy) |
| `status` | [ConversationStatus](#type-conversationstatus) |
| `createdAtUtc` | `DateTime` |
| `archivedAtUtc` | `DateTime?` |
| `participants` | آرایه [ConversationParticipantResult](#type-conversationparticipantresult) |
| `alreadyExisted` | `bool` |

<a id="type-messageattachmentresult"></a>
### MessageAttachmentResult

منبع: [MessagingContracts.cs](../MakanApp.Application/Messaging/MessagingContracts.cs).

| فیلد | نوع |
|---|---|
| `fileAssetId` | `Guid` |
| `kind` | [MessageKind](#type-messagekind) |
| `fileName` | `string` |
| `contentType` | `string` |
| `sizeBytes` | `long` |
| `createdAtUtc` | `DateTime` |

<a id="type-messagemutationresult"></a>
### MessageMutationResult

منبع: [MessagingContracts.cs](../MakanApp.Application/Messaging/MessagingContracts.cs).

| فیلد | نوع |
|---|---|
| `messageId` | `Guid` |
| `sequence` | `long` |
| `editedAtUtc` | `DateTime?` |
| `deletedAtUtc` | `DateTime?` |
| `isDeleted` | `bool` |
| `version` | `string` |

<a id="type-messagereactionresult"></a>
### MessageReactionResult

منبع: [MessagingContracts.cs](../MakanApp.Application/Messaging/MessagingContracts.cs).

| فیلد | نوع |
|---|---|
| `messageId` | `Guid` |
| `reaction` | [MessageReactionType](#type-messagereactiontype) |
| `createdAtUtc` | `DateTime` |

<a id="type-messagereactionsummaryresult"></a>
### MessageReactionSummaryResult

منبع: [MessagingContracts.cs](../MakanApp.Application/Messaging/MessagingContracts.cs).

| فیلد | نوع |
|---|---|
| `reaction` | [MessageReactionType](#type-messagereactiontype) |
| `count` | `int` |
| `reactedByCurrentUser` | `bool` |

<a id="type-messagereceiptresult"></a>
### MessageReceiptResult

منبع: [MessagingContracts.cs](../MakanApp.Application/Messaging/MessagingContracts.cs).

| فیلد | نوع |
|---|---|
| `messageId` | `Guid` |
| `conversationId` | `Guid` |
| `clientMessageId` | `Guid` |
| `sequence` | `long` |
| `kind` | [MessageKind](#type-messagekind) |
| `sentAtUtc` | `DateTime` |
| `status` | [MessageReceiptStatus](#type-messagereceiptstatus) |
| `version` | `string` |

<a id="type-messagereplysummaryresult"></a>
### MessageReplySummaryResult

منبع: [MessagingContracts.cs](../MakanApp.Application/Messaging/MessagingContracts.cs).

| فیلد | نوع |
|---|---|
| `messageId` | `Guid` |
| `senderUserId` | `Guid` |
| `kind` | [MessageKind](#type-messagekind) یا null |
| `text` | `string?` |
| `isDeleted` | `bool` |

<a id="type-messagesearchitemresult"></a>
### MessageSearchItemResult

منبع: [MessagingSafetyContracts.cs](../MakanApp.Application/Messaging/MessagingSafetyContracts.cs).

| فیلد | نوع |
|---|---|
| `messageId` | `Guid` |
| `conversationId` | `Guid` |
| `conversationDisplayName` | `string?` |
| `sender` | [SafeMessagingIdentityResult](#type-safemessagingidentityresult) |
| `snippet` | `string?` |
| `kind` | [MessageKind](#type-messagekind) |
| `sentAtUtc` | `DateTime` |
| `sequence` | `long` |
| `attachments` | آرایه [MessageAttachmentResult](#type-messageattachmentresult) |

<a id="type-messagesearchpageresult"></a>
### MessageSearchPageResult

منبع: [MessagingSafetyContracts.cs](../MakanApp.Application/Messaging/MessagingSafetyContracts.cs).

| فیلد | نوع |
|---|---|
| `items` | آرایه [MessageSearchItemResult](#type-messagesearchitemresult) |
| `totalCount` | `int` |
| `nextCursor` | `string?` |

<a id="type-ownershiptransferresult"></a>
### OwnershipTransferResult

منبع: [ConversationManagementContracts.cs](../MakanApp.Application/Messaging/ConversationManagementContracts.cs).

| فیلد | نوع |
|---|---|
| `transferId` | `Guid` |
| `conversationId` | `Guid` |
| `fromUserId` | `Guid` |
| `toUserId` | `Guid` |
| `status` | [OwnershipTransferStatus](#type-ownershiptransferstatus) |
| `createdAtUtc` | `DateTime` |
| `acceptedAtUtc` | `DateTime?` |
| `declinedAtUtc` | `DateTime?` |
| `alreadyCompleted` | `bool` |

<a id="type-parentreleasedresult"></a>
### ParentReleasedResult

منبع: [EvaluationContracts.cs](../MakanApp.Application/Assessment/EvaluationContracts.cs).

| فیلد | نوع |
|---|---|
| `submissionAttemptId` | `Guid` |
| `assignmentId` | `Guid` |
| `assignmentVersionId` | `Guid` |
| `evaluationRevisionNumber` | `int` |
| `score` | `decimal` |
| `maxScore` | `decimal` |
| `guardianVisibleFeedback` | `string?` |
| `releasedAtUtc` | `DateTime` |

<a id="type-publishassignmentcommand"></a>
### PublishAssignmentCommand

منبع: [AssignmentContracts.cs](../MakanApp.Application/Assessment/AssignmentContracts.cs).

| فیلد | نوع |
|---|---|
| `expectedAssignmentRowVersion` | `string` |
| `expectedVersionRowVersion` | `string` |

<a id="type-publishassignmentresult"></a>
### PublishAssignmentResult

منبع: [AssignmentContracts.cs](../MakanApp.Application/Assessment/AssignmentContracts.cs).

| فیلد | نوع |
|---|---|
| `assignment` | [AssignmentResult](#type-assignmentresult) |
| `recipientCount` | `int` |

<a id="type-publishexamcommand"></a>
### PublishExamCommand

منبع: [ExamContracts.cs](../MakanApp.Application/Assessment/ExamContracts.cs).

| فیلد | نوع |
|---|---|
| `expectedExamRowVersion` | `string` |
| `expectedVersionRowVersion` | `string` |

<a id="type-recordattendancecommand"></a>
### RecordAttendanceCommand

منبع: [AcademicSessionContracts.cs](../MakanApp.Application/Academic/AcademicSessionContracts.cs).

| فیلد | نوع |
|---|---|
| `entries` | آرایه [RecordAttendanceEntryCommand](#type-recordattendanceentrycommand) |

<a id="type-recordattendanceentrycommand"></a>
### RecordAttendanceEntryCommand

منبع: [AcademicSessionContracts.cs](../MakanApp.Application/Academic/AcademicSessionContracts.cs).

| فیلد | نوع |
|---|---|
| `enrollmentId` | `Guid` |
| `status` | [AttendanceStatus](#type-attendancestatus) |

<a id="type-releaseevaluationcommand"></a>
### ReleaseEvaluationCommand

منبع: [EvaluationContracts.cs](../MakanApp.Application/Assessment/EvaluationContracts.cs).

| فیلد | نوع |
|---|---|
| `expectedRowVersion` | `string` |

<a id="type-releaseexamgradecommand"></a>
### ReleaseExamGradeCommand

منبع: [ExamGradingContracts.cs](../MakanApp.Application/Assessment/ExamGradingContracts.cs).

| فیلد | نوع |
|---|---|
| `clientOperationId` | `Guid` |
| `expectedGradeRowVersion` | `string` |

<a id="type-reportmessagecommand"></a>
### ReportMessageCommand

منبع: [MessagingSafetyContracts.cs](../MakanApp.Application/Messaging/MessagingSafetyContracts.cs).

| فیلد | نوع |
|---|---|
| `clientReportId` | `Guid` |
| `reason` | [AbuseReportReason](#type-abusereportreason) |
| `description` | `string?` |

<a id="type-requestotpcommand"></a>
### RequestOtpCommand

منبع: [IdentityContracts.cs](../MakanApp.Application/Identity/IdentityContracts.cs).

| فیلد | نوع |
|---|---|
| `phoneNumber` | `string` |

<a id="type-requestotpresult"></a>
### RequestOtpResult

منبع: [IdentityContracts.cs](../MakanApp.Application/Identity/IdentityContracts.cs).

| فیلد | نوع |
|---|---|
| `challengeId` | `Guid` |
| `expiresAtUtc` | `DateTime` |
| `resendAvailableAtUtc` | `DateTime` |

<a id="type-safemessagingidentityresult"></a>
### SafeMessagingIdentityResult

منبع: [MessagingContracts.cs](../MakanApp.Application/Messaging/MessagingContracts.cs).

| فیلد | نوع |
|---|---|
| `userId` | `Guid` |
| `username` | `string` |
| `displayName` | `string` |

<a id="type-saveevaluationdraftcommand"></a>
### SaveEvaluationDraftCommand

منبع: [EvaluationContracts.cs](../MakanApp.Application/Assessment/EvaluationContracts.cs).

| فیلد | نوع |
|---|---|
| `score` | `decimal` |
| `learnerFeedback` | `string?` |
| `guardianVisibleFeedback` | `string?` |
| `teacherPrivateNote` | `string?` |
| `expectedRowVersion` | `string?` |

<a id="type-saveexamanswercommand"></a>
### SaveExamAnswerCommand

منبع: [ExamAnswerContracts.cs](../MakanApp.Application/Assessment/ExamAnswerContracts.cs).

| فیلد | نوع |
|---|---|
| `clientOperationId` | `Guid` |
| `writeLeaseVersion` | `long` |
| `expectedRevisionNumber` | `int?` |
| `selectedOptionId` | `Guid?` |
| `textAnswer` | `string?` |

<a id="type-savesubmissiondraftcommand"></a>
### SaveSubmissionDraftCommand

منبع: [SubmissionContracts.cs](../MakanApp.Application/Assessment/SubmissionContracts.cs).

| فیلد | نوع |
|---|---|
| `answerText` | `string?` |
| `expectedRowVersion` | `string` |

<a id="type-scheduleruleresult"></a>
### ScheduleRuleResult

منبع: [AcademicSessionContracts.cs](../MakanApp.Application/Academic/AcademicSessionContracts.cs).

| فیلد | نوع |
|---|---|
| `id` | `Guid` |
| `organizationId` | `Guid` |
| `classId` | `Guid` |
| `localDayOfWeek` | [DayOfWeek](#type-dayofweek) |
| `localStartTime` | `TimeOnly` |
| `durationMinutes` | `int` |
| `timeZoneId` | `string` |
| `effectiveFrom` | `DateOnly` |
| `effectiveUntil` | `DateOnly` |
| `sessionTitle` | `string` |
| `meetingUrl` | `string?` |
| `status` | [ScheduleRuleStatus](#type-schedulerulestatus) |
| `generatedSessionCount` | `int` |
| `rowVersion` | `string` |

<a id="type-selectworkspacecommand"></a>
### SelectWorkspaceCommand

منبع: [OrganizationContracts.cs](../MakanApp.Application/Organization/OrganizationContracts.cs).

| فیلد | نوع |
|---|---|
| `workspaceType` | [WorkspaceType](#type-workspacetype) |
| `membershipId` | `Guid?` |
| `role` | [OrganizationRole](#type-organizationrole) یا null |

<a id="type-sendmessagecommand"></a>
### SendMessageCommand

منبع: [MessagingContracts.cs](../MakanApp.Application/Messaging/MessagingContracts.cs).

| فیلد | نوع |
|---|---|
| `clientMessageId` | `Guid` |
| `kind` | [MessageKind](#type-messagekind) |
| `text` | `string?` |
| `attachmentIds` | `IReadOnlyCollection<Guid>?` |
| `replyToMessageId` | `Guid?` |
| `mentionedUserIds` | `IReadOnlyCollection<Guid>?` |

<a id="type-sessionattendanceresult"></a>
### SessionAttendanceResult

منبع: [AcademicSessionContracts.cs](../MakanApp.Application/Academic/AcademicSessionContracts.cs).

| فیلد | نوع |
|---|---|
| `sessionId` | `Guid` |
| `sessionStatus` | [SessionStatus](#type-sessionstatus) |
| `entries` | آرایه [AttendanceEntryResult](#type-attendanceentryresult) |
| `revisions` | آرایه [AttendanceRevisionResult](#type-attendancerevisionresult) |

<a id="type-sessionresult"></a>
### SessionResult

منبع: [AcademicSessionContracts.cs](../MakanApp.Application/Academic/AcademicSessionContracts.cs).

| فیلد | نوع |
|---|---|
| `id` | `Guid` |
| `organizationId` | `Guid` |
| `classId` | `Guid` |
| `classTitle` | `string` |
| `scheduleRuleId` | `Guid?` |
| `title` | `string` |
| `startUtc` | `DateTime` |
| `endUtc` | `DateTime` |
| `timeZoneId` | `string` |
| `meetingUrl` | `string?` |
| `status` | [SessionStatus](#type-sessionstatus) |
| `cancelledAtUtc` | `DateTime?` |
| `completedAtUtc` | `DateTime?` |
| `rowVersion` | `string` |

<a id="type-sessionversioncommand"></a>
### SessionVersionCommand

منبع: [AcademicSessionContracts.cs](../MakanApp.Application/Academic/AcademicSessionContracts.cs).

| فیلد | نوع |
|---|---|
| `expectedRowVersion` | `string` |

<a id="type-startdirectconversationcommand"></a>
### StartDirectConversationCommand

منبع: [MessagingContracts.cs](../MakanApp.Application/Messaging/MessagingContracts.cs).

| فیلد | نوع |
|---|---|
| `targetUserId` | `Guid` |
| `scope` | [ConversationScope](#type-conversationscope) |

<a id="type-startexamcommand"></a>
### StartExamCommand

منبع: [ExamAttemptContracts.cs](../MakanApp.Application/Assessment/ExamAttemptContracts.cs).

| فیلد | نوع |
|---|---|
| `clientOperationId` | `Guid` |

<a id="type-startownershiptransfercommand"></a>
### StartOwnershipTransferCommand

منبع: [ConversationManagementContracts.cs](../MakanApp.Application/Messaging/ConversationManagementContracts.cs).

| فیلد | نوع |
|---|---|
| `targetUserId` | `Guid` |

<a id="type-studentcurrentexamanswerdto"></a>
### StudentCurrentExamAnswerDto

منبع: [ExamAnswerContracts.cs](../MakanApp.Application/Assessment/ExamAnswerContracts.cs).

| فیلد | نوع |
|---|---|
| `examAttemptQuestionId` | `Guid` |
| `questionVersionId` | `Guid` |
| `answerRevisionId` | `Guid` |
| `revisionNumber` | `int` |
| `answerType` | [ExamQuestionType](#type-examquestiontype) |
| `selectedOptionId` | `Guid?` |
| `textAnswer` | `string?` |
| `acceptedAtUtc` | `DateTime` |

<a id="type-studentexamanswersdto"></a>
### StudentExamAnswersDto

منبع: [ExamAnswerContracts.cs](../MakanApp.Application/Assessment/ExamAnswerContracts.cs).

| فیلد | نوع |
|---|---|
| `examAttemptId` | `Guid` |
| `answerSetVersion` | `long` |
| `writeLease` | [ExamWriteLeaseDto](#type-examwriteleasedto) |
| `answers` | آرایه [StudentCurrentExamAnswerDto](#type-studentcurrentexamanswerdto) |

<a id="type-studentexamattemptdto"></a>
### StudentExamAttemptDto

منبع: [ExamAttemptContracts.cs](../MakanApp.Application/Assessment/ExamAttemptContracts.cs).

| فیلد | نوع |
|---|---|
| `examAttemptId` | `Guid` |
| `examId` | `Guid` |
| `examVersionId` | `Guid` |
| `attemptNumber` | `int` |
| `startedAtUtc` | `DateTime` |
| `effectiveDeadlineUtc` | `DateTime` |
| `serverNowUtc` | `DateTime` |
| `status` | [ExamAttemptStatus](#type-examattemptstatus) |
| `questions` | آرایه [StudentExamAttemptQuestionDto](#type-studentexamattemptquestiondto) |

<a id="type-studentexamattemptoptiondto"></a>
### StudentExamAttemptOptionDto

منبع: [ExamAttemptContracts.cs](../MakanApp.Application/Assessment/ExamAttemptContracts.cs).

| فیلد | نوع |
|---|---|
| `id` | `Guid` |
| `order` | `int` |
| `text` | `string` |

<a id="type-studentexamattemptquestiondto"></a>
### StudentExamAttemptQuestionDto

منبع: [ExamAttemptContracts.cs](../MakanApp.Application/Assessment/ExamAttemptContracts.cs).

| فیلد | نوع |
|---|---|
| `attemptQuestionId` | `Guid` |
| `questionVersionId` | `Guid` |
| `displayOrder` | `int` |
| `questionType` | [ExamQuestionType](#type-examquestiontype) |
| `prompt` | `string` |
| `score` | `decimal` |
| `options` | آرایه [StudentExamAttemptOptionDto](#type-studentexamattemptoptiondto) |

<a id="type-studentexamattemptsummarydto"></a>
### StudentExamAttemptSummaryDto

منبع: [ExamAttemptContracts.cs](../MakanApp.Application/Assessment/ExamAttemptContracts.cs).

| فیلد | نوع |
|---|---|
| `examAttemptId` | `Guid` |
| `examVersionId` | `Guid` |
| `attemptNumber` | `int` |
| `startedAtUtc` | `DateTime` |
| `effectiveDeadlineUtc` | `DateTime` |
| `serverNowUtc` | `DateTime` |
| `status` | [ExamAttemptStatus](#type-examattemptstatus) |

<a id="type-studentexamresultdto"></a>
### StudentExamResultDto

منبع: [ExamGradingContracts.cs](../MakanApp.Application/Assessment/ExamGradingContracts.cs).

| فیلد | نوع |
|---|---|
| `examAttemptId` | `Guid` |
| `examId` | `Guid` |
| `examVersionId` | `Guid` |
| `attemptNumber` | `int` |
| `finalizedAtUtc` | `DateTime` |
| `releaseStatus` | [ExamResultReleaseStatus](#type-examresultreleasestatus) |
| `gradeRevisionNumber` | `int?` |
| `releasedAtUtc` | `DateTime?` |
| `score` | `decimal?` |
| `maximumScore` | `decimal?` |
| `learnerFeedback` | `string?` |

<a id="type-studentexamrules"></a>
### StudentExamRules

منبع: [ExamContracts.cs](../MakanApp.Application/Assessment/ExamContracts.cs).

| فیلد | نوع |
|---|---|
| `availableFromUtc` | `DateTime` |
| `availableUntilUtc` | `DateTime` |
| `durationMinutes` | `int` |
| `maxAttempts` | `int` |
| `maxScore` | `decimal` |
| `randomizationPolicy` | [ExamRandomizationPolicy](#type-examrandomizationpolicy) |

<a id="type-studentexamsummary"></a>
### StudentExamSummary

منبع: [ExamContracts.cs](../MakanApp.Application/Assessment/ExamContracts.cs).

| فیلد | نوع |
|---|---|
| `id` | `Guid` |
| `versionId` | `Guid` |
| `classId` | `Guid` |
| `classTitle` | `string` |
| `versionNumber` | `int` |
| `title` | `string` |
| `description` | `string?` |
| `publicationState` | [ExamVersionStatus](#type-examversionstatus) |
| `rules` | [StudentExamRules](#type-studentexamrules) |

<a id="type-studentreleasedresult"></a>
### StudentReleasedResult

منبع: [EvaluationContracts.cs](../MakanApp.Application/Assessment/EvaluationContracts.cs).

| فیلد | نوع |
|---|---|
| `submissionAttemptId` | `Guid` |
| `assignmentId` | `Guid` |
| `assignmentVersionId` | `Guid` |
| `evaluationRevisionNumber` | `int` |
| `score` | `decimal` |
| `maxScore` | `decimal` |
| `learnerFeedback` | `string?` |
| `releasedAtUtc` | `DateTime` |

<a id="type-studentsafeexamoption"></a>
### StudentSafeExamOption

منبع: [ExamContracts.cs](../MakanApp.Application/Assessment/ExamContracts.cs).

| فیلد | نوع |
|---|---|
| `id` | `Guid` |
| `order` | `int` |
| `text` | `string` |

<a id="type-studentsafeexampreview"></a>
### StudentSafeExamPreview

منبع: [ExamContracts.cs](../MakanApp.Application/Assessment/ExamContracts.cs).

| فیلد | نوع |
|---|---|
| `exam` | [StudentExamSummary](#type-studentexamsummary) |
| `questions` | آرایه [StudentSafeExamQuestion](#type-studentsafeexamquestion) |

<a id="type-studentsafeexamquestion"></a>
### StudentSafeExamQuestion

منبع: [ExamContracts.cs](../MakanApp.Application/Assessment/ExamContracts.cs).

| فیلد | نوع |
|---|---|
| `id` | `Guid` |
| `order` | `int` |
| `type` | [ExamQuestionType](#type-examquestiontype) |
| `prompt` | `string` |
| `score` | `decimal` |
| `options` | آرایه [StudentSafeExamOption](#type-studentsafeexamoption) |

<a id="type-submissionattachmentresult"></a>
### SubmissionAttachmentResult

منبع: [SubmissionContracts.cs](../MakanApp.Application/Assessment/SubmissionContracts.cs).

| فیلد | نوع |
|---|---|
| `fileAssetId` | `Guid` |
| `originalFileName` | `string` |
| `contentType` | `string` |
| `sizeBytes` | `long` |
| `fileStatus` | [FileAssetStatus](#type-fileassetstatus) |
| `attachedAtUtc` | `DateTime` |

<a id="type-submissionattemptresult"></a>
### SubmissionAttemptResult

منبع: [SubmissionContracts.cs](../MakanApp.Application/Assessment/SubmissionContracts.cs).

| فیلد | نوع |
|---|---|
| `id` | `Guid` |
| `organizationId` | `Guid` |
| `assignmentId` | `Guid` |
| `assignmentVersionId` | `Guid` |
| `assignmentRecipientId` | `Guid` |
| `enrollmentId` | `Guid` |
| `attemptNumber` | `int` |
| `status` | [SubmissionAttemptStatus](#type-submissionattemptstatus) |
| `contentVisible` | `bool` |
| `answerText` | `string?` |
| `attachments` | آرایه [SubmissionAttachmentResult](#type-submissionattachmentresult) |
| `createdAtUtc` | `DateTime` |
| `lastSavedAtUtc` | `DateTime?` |
| `submittedAtUtc` | `DateTime?` |
| `isLate` | `bool` |
| `rowVersion` | `string` |

<a id="type-submissionforevaluationresult"></a>
### SubmissionForEvaluationResult

منبع: [EvaluationContracts.cs](../MakanApp.Application/Assessment/EvaluationContracts.cs).

| فیلد | نوع |
|---|---|
| `submissionAttemptId` | `Guid` |
| `assignmentId` | `Guid` |
| `assignmentVersionId` | `Guid` |
| `classId` | `Guid` |
| `assignmentTitle` | `string` |
| `studentDisplayName` | `string` |
| `attemptNumber` | `int` |
| `submittedAtUtc` | `DateTime` |
| `isLate` | `bool` |
| `maxScore` | `decimal` |
| `answerText` | `string?` |
| `attachments` | آرایه [SubmissionAttachmentResult](#type-submissionattachmentresult) |

<a id="type-submissionreceipt"></a>
### SubmissionReceipt

منبع: [SubmissionContracts.cs](../MakanApp.Application/Assessment/SubmissionContracts.cs).

| فیلد | نوع |
|---|---|
| `submissionAttemptId` | `Guid` |
| `assignmentId` | `Guid` |
| `assignmentVersionId` | `Guid` |
| `attemptNumber` | `int` |
| `submittedAtUtc` | `DateTime` |
| `isLate` | `bool` |
| `status` | [SubmissionAttemptStatus](#type-submissionattemptstatus) |
| `rowVersion` | `string` |

<a id="type-teacherassignmentresult"></a>
### TeacherAssignmentResult

منبع: [AcademicContracts.cs](../MakanApp.Application/Academic/AcademicContracts.cs).

| فیلد | نوع |
|---|---|
| `id` | `Guid` |
| `organizationId` | `Guid` |
| `classId` | `Guid` |
| `teacherMembershipId` | `Guid` |
| `status` | [TeacherAssignmentStatus](#type-teacherassignmentstatus) |
| `assignedAtUtc` | `DateTime` |
| `endedAtUtc` | `DateTime?` |

<a id="type-updateassignmentdraftcommand"></a>
### UpdateAssignmentDraftCommand

منبع: [AssignmentContracts.cs](../MakanApp.Application/Assessment/AssignmentContracts.cs).

| فیلد | نوع |
|---|---|
| `title` | `string` |
| `description` | `string` |
| `dueAtUtc` | `DateTime` |
| `allowLateSubmission` | `bool` |
| `maxAttempts` | `int` |
| `maxScore` | `decimal` |
| `expectedAssignmentRowVersion` | `string` |
| `expectedVersionRowVersion` | `string` |

<a id="type-updateexamdraftcommand"></a>
### UpdateExamDraftCommand

منبع: [ExamContracts.cs](../MakanApp.Application/Assessment/ExamContracts.cs).

| فیلد | نوع |
|---|---|
| `title` | `string` |
| `description` | `string?` |
| `availableFromUtc` | `DateTime` |
| `availableUntilUtc` | `DateTime` |
| `durationMinutes` | `int` |
| `maxAttempts` | `int` |
| `maxScore` | `decimal` |
| `randomizationPolicy` | [ExamRandomizationPolicy](#type-examrandomizationpolicy) |
| `expectedVersionRowVersion` | `string` |

<a id="type-updateexamquestioncommand"></a>
### UpdateExamQuestionCommand

منبع: [ExamContracts.cs](../MakanApp.Application/Assessment/ExamContracts.cs).

| فیلد | نوع |
|---|---|
| `order` | `int` |
| `type` | [ExamQuestionType](#type-examquestiontype) |
| `prompt` | `string` |
| `score` | `decimal` |
| `options` | آرایه [ExamQuestionOptionCommand](#type-examquestionoptioncommand) یا null |
| `expectedVersionRowVersion` | `string` |
| `expectedQuestionRowVersion` | `string` |

<a id="type-updateschedulerulecommand"></a>
### UpdateScheduleRuleCommand

منبع: [AcademicSessionContracts.cs](../MakanApp.Application/Academic/AcademicSessionContracts.cs).

| فیلد | نوع |
|---|---|
| `localDayOfWeek` | [DayOfWeek](#type-dayofweek) |
| `localStartTime` | `TimeOnly` |
| `durationMinutes` | `int` |
| `timeZoneId` | `string` |
| `effectiveFrom` | `DateOnly` |
| `effectiveUntil` | `DateOnly` |
| `applyFromDate` | `DateOnly` |
| `sessionTitle` | `string` |
| `meetingUrl` | `string?` |
| `expectedRowVersion` | `string` |

<a id="type-updatesessioncommand"></a>
### UpdateSessionCommand

منبع: [AcademicSessionContracts.cs](../MakanApp.Application/Academic/AcademicSessionContracts.cs).

| فیلد | نوع |
|---|---|
| `title` | `string` |
| `startUtc` | `DateTime` |
| `endUtc` | `DateTime` |
| `timeZoneId` | `string` |
| `meetingUrl` | `string?` |
| `expectedRowVersion` | `string` |

<a id="type-uploadfilerequest"></a>
### UploadFileRequest

منبع: [FilesController.cs](../MakanApp.Api/Controllers/FilesController.cs).

| فیلد | نوع |
|---|---|
| `File` | `IFormFile` |

<a id="type-userblockresult"></a>
### UserBlockResult

منبع: [MessagingSafetyContracts.cs](../MakanApp.Application/Messaging/MessagingSafetyContracts.cs).

| فیلد | نوع |
|---|---|
| `blockId` | `Guid` |
| `blockedUser` | [SafeMessagingIdentityResult](#type-safemessagingidentityresult) |
| `status` | [UserBlockStatus](#type-userblockstatus) |
| `createdAtUtc` | `DateTime` |
| `alreadyExisted` | `bool` |
| `version` | `string` |

<a id="type-verifyotpcommand"></a>
### VerifyOtpCommand

منبع: [IdentityContracts.cs](../MakanApp.Application/Identity/IdentityContracts.cs).

| فیلد | نوع |
|---|---|
| `challengeId` | `Guid` |
| `phoneNumber` | `string` |
| `code` | `string` |

<a id="type-verifyotpresult"></a>
### VerifyOtpResult

منبع: [IdentityContracts.cs](../MakanApp.Application/Identity/IdentityContracts.cs).

| فیلد | نوع |
|---|---|
| `accessToken` | `string` |
| `tokenType` | `string` |
| `expiresAtUtc` | `DateTime` |
| `user` | [CurrentUserResult](#type-currentuserresult) |

<a id="type-workspaceresult"></a>
### WorkspaceResult

منبع: [OrganizationContracts.cs](../MakanApp.Application/Organization/OrganizationContracts.cs).

| فیلد | نوع |
|---|---|
| `workspaceType` | [WorkspaceType](#type-workspacetype) |
| `organizationId` | `Guid?` |
| `organizationName` | `string?` |
| `membershipId` | `Guid?` |
| `role` | [OrganizationRole](#type-organizationrole) یا null |
| `displayName` | `string` |
| `status` | `string` |

<a id="enums"></a>
## مقادیر Enum

رشته‌های زیر عین مقادیر قرارداد هستند. وجود یک وضعیت در Enum تضمین وجود Endpoint برای ایجاد آن وضعیت نیست.

<a id="type-abusereportreason"></a>
### AbuseReportReason

`Harassment`، `Spam`، `Threat`، `InappropriateContent`، `Impersonation`، `Other`

منبع: [AbuseReportReason.cs](../MakanApp.Domain/Messaging/AbuseReportReason.cs).

<a id="type-abusereportstatus"></a>
### AbuseReportStatus

`Submitted`، `UnderReview`، `Resolved`، `Dismissed`

منبع: [AbuseReportStatus.cs](../MakanApp.Domain/Messaging/AbuseReportStatus.cs).

<a id="type-academicperiodstatus"></a>
### AcademicPeriodStatus

`Active`، `Closed`

منبع: [AcademicPeriodStatus.cs](../MakanApp.Domain/Academic/AcademicPeriodStatus.cs).

<a id="type-assignmentstatus"></a>
### AssignmentStatus

`Draft`، `Published`، `Closed`، `Archived`

منبع: [AssignmentStatus.cs](../MakanApp.Domain/Assessment/AssignmentStatus.cs).

<a id="type-attendancestatus"></a>
### AttendanceStatus

`NotRecorded`، `Present`، `Absent`، `Late`، `Excused`

منبع: [AttendanceStatus.cs](../MakanApp.Domain/Academic/AttendanceStatus.cs).

<a id="type-classstatus"></a>
### ClassStatus

`Draft`، `Active`، `Completed`، `Archived`

منبع: [ClassStatus.cs](../MakanApp.Domain/Academic/ClassStatus.cs).

<a id="type-conversationmanagementpolicy"></a>
### ConversationManagementPolicy

`None`، `UserManaged`، `SystemManagedAcademic`

منبع: [ConversationManagementPolicy.cs](../MakanApp.Domain/Messaging/ConversationManagementPolicy.cs).

<a id="type-conversationparticipantrole"></a>
### ConversationParticipantRole

`Owner`، `Admin`، `Member`

منبع: [ConversationParticipantRole.cs](../MakanApp.Domain/Messaging/ConversationParticipantRole.cs).

<a id="type-conversationparticipantstatus"></a>
### ConversationParticipantStatus

`Active`، `Removed`، `Left`

منبع: [ConversationParticipantStatus.cs](../MakanApp.Domain/Messaging/ConversationParticipantStatus.cs).

<a id="type-conversationscope"></a>
### ConversationScope

`Personal`، `Organization`

منبع: [ConversationScope.cs](../MakanApp.Domain/Messaging/ConversationScope.cs).

<a id="type-conversationstatus"></a>
### ConversationStatus

`Active`، `Archived`

منبع: [ConversationStatus.cs](../MakanApp.Domain/Messaging/ConversationStatus.cs).

<a id="type-conversationtype"></a>
### ConversationType

`Direct`، `Group`، `Channel`

منبع: [ConversationType.cs](../MakanApp.Domain/Messaging/ConversationType.cs).

<a id="type-coursestatus"></a>
### CourseStatus

`Active`، `Archived`

منبع: [CourseStatus.cs](../MakanApp.Domain/Academic/CourseStatus.cs).

<a id="type-dayofweek"></a>
### DayOfWeek

`Sunday`، `Monday`، `Tuesday`، `Wednesday`، `Thursday`، `Friday`، `Saturday`

<a id="type-enrollmentstatus"></a>
### EnrollmentStatus

`Active`، `Completed`، `Withdrawn`

منبع: [EnrollmentStatus.cs](../MakanApp.Domain/Academic/EnrollmentStatus.cs).

<a id="type-evaluationreviewstatus"></a>
### EvaluationReviewStatus

`AwaitingEvaluation`، `Draft`، `Released`، `CorrectionDraft`

منبع: [EvaluationContracts.cs](../MakanApp.Application/Assessment/EvaluationContracts.cs).

<a id="type-evaluationrevisionstatus"></a>
### EvaluationRevisionStatus

`Draft`، `Released`، `Superseded`

منبع: [EvaluationRevisionStatus.cs](../MakanApp.Domain/Assessment/EvaluationRevisionStatus.cs).

<a id="type-examattemptstatus"></a>
### ExamAttemptStatus

`InProgress`، `Expired`، `Finalized`

منبع: [ExamAttemptStatus.cs](../MakanApp.Domain/Assessment/ExamAttemptStatus.cs).

<a id="type-examgraderevisionstatus"></a>
### ExamGradeRevisionStatus

`Draft`، `ReadyForRelease`، `Released`، `Superseded`

منبع: [ExamGradeRevisionStatus.cs](../MakanApp.Domain/Assessment/ExamGradeRevisionStatus.cs).

<a id="type-examgradingqueuestatus"></a>
### ExamGradingQueueStatus

`AwaitingGrading`، `Draft`، `ReadyForRelease`، `Released`، `CorrectionDraft`

منبع: [ExamGradingContracts.cs](../MakanApp.Application/Assessment/ExamGradingContracts.cs).

<a id="type-examquestiongradingmode"></a>
### ExamQuestionGradingMode

`Objective`، `Manual`

منبع: [ExamQuestionGradingMode.cs](../MakanApp.Domain/Assessment/ExamQuestionGradingMode.cs).

<a id="type-examquestiontype"></a>
### ExamQuestionType

`ObjectiveSingleChoice`، `Descriptive`

منبع: [ExamQuestionType.cs](../MakanApp.Domain/Assessment/ExamQuestionType.cs).

<a id="type-examrandomizationpolicy"></a>
### ExamRandomizationPolicy

`None`، `QuestionOrder`

منبع: [ExamRandomizationPolicy.cs](../MakanApp.Domain/Assessment/ExamRandomizationPolicy.cs).

<a id="type-examresultreleasestatus"></a>
### ExamResultReleaseStatus

`AwaitingGrading`، `AwaitingRelease`، `Released`

منبع: [ExamGradingContracts.cs](../MakanApp.Application/Assessment/ExamGradingContracts.cs).

<a id="type-examstatus"></a>
### ExamStatus

`Draft`، `Published`

منبع: [ExamStatus.cs](../MakanApp.Domain/Assessment/ExamStatus.cs).

<a id="type-examversionstatus"></a>
### ExamVersionStatus

`Draft`، `Published`

منبع: [ExamVersionStatus.cs](../MakanApp.Domain/Assessment/ExamVersionStatus.cs).

<a id="type-fileassetstatus"></a>
### FileAssetStatus

`Pending`، `Ready`، `Rejected`، `Deleted`

منبع: [FileAssetStatus.cs](../MakanApp.Domain/Storage/FileAssetStatus.cs).

<a id="type-guardianrelationstatus"></a>
### GuardianRelationStatus

`Pending`، `Active`، `Ended`، `Revoked`

منبع: [GuardianRelationStatus.cs](../MakanApp.Domain/Guardian/GuardianRelationStatus.cs).

<a id="type-invitationstatus"></a>
### InvitationStatus

`Pending`، `Accepted`، `Declined`، `Expired`، `Revoked`

منبع: [InvitationStatus.cs](../MakanApp.Domain/Organization/InvitationStatus.cs).

<a id="type-messagekind"></a>
### MessageKind

`Text`، `Image`، `Video`، `Voice`، `File`

منبع: [MessageKind.cs](../MakanApp.Domain/Messaging/MessageKind.cs).

<a id="type-messagereactiontype"></a>
### MessageReactionType

`Like`، `Love`، `Laugh`، `Wow`، `Sad`

منبع: [MessageReactionType.cs](../MakanApp.Domain/Messaging/MessageReactionType.cs).

<a id="type-messagereceiptstatus"></a>
### MessageReceiptStatus

`Sent`

منبع: [MessagingContracts.cs](../MakanApp.Application/Messaging/MessagingContracts.cs).

<a id="type-messagingchangetype"></a>
### MessagingChangeType

`MessageCreated`، `MessageEdited`، `MessageDeleted`، `ReactionChanged`، `PinChanged`، `ParticipantChanged`، `ConversationChanged`، `ReadCursorAdvanced`، `DeliveryCursorAdvanced`

منبع: [MessagingChangeType.cs](../MakanApp.Domain/Messaging/MessagingChangeType.cs).

<a id="type-organizationrole"></a>
### OrganizationRole

`Student`، `Teacher`، `Parent`، `Manager`

منبع: [OrganizationRole.cs](../MakanApp.Domain/Organization/OrganizationRole.cs).

<a id="type-ownershiptransferstatus"></a>
### OwnershipTransferStatus

`Pending`، `Accepted`، `Declined`، `Cancelled`، `Expired`

منبع: [OwnershipTransferStatus.cs](../MakanApp.Domain/Messaging/OwnershipTransferStatus.cs).

<a id="type-schedulerulestatus"></a>
### ScheduleRuleStatus

`Active`، `Ended`

منبع: [ScheduleRuleStatus.cs](../MakanApp.Domain/Academic/ScheduleRuleStatus.cs).

<a id="type-sessionstatus"></a>
### SessionStatus

`Scheduled`، `Cancelled`، `Completed`

منبع: [SessionStatus.cs](../MakanApp.Domain/Academic/SessionStatus.cs).

<a id="type-submissionattemptstatus"></a>
### SubmissionAttemptStatus

`Draft`، `Submitted`

منبع: [SubmissionAttemptStatus.cs](../MakanApp.Domain/Assessment/SubmissionAttemptStatus.cs).

<a id="type-teacherassignmentstatus"></a>
### TeacherAssignmentStatus

`Active`، `Ended`

منبع: [TeacherAssignmentStatus.cs](../MakanApp.Domain/Academic/TeacherAssignmentStatus.cs).

<a id="type-userblockstatus"></a>
### UserBlockStatus

`Active`، `Ended`

منبع: [UserBlockStatus.cs](../MakanApp.Domain/Messaging/UserBlockStatus.cs).

<a id="type-workspacetype"></a>
### WorkspaceType

`Personal`، `Organization`

منبع: [WorkspaceType.cs](../MakanApp.Domain/Organization/WorkspaceType.cs).

<a id="error-codes"></a>
## کدهای خطا

جدول زیر نگاشت کدهای تعریف‌شده در Application به HTTP را از GlobalExceptionHandler نشان می‌دهد. کدی که case اختصاصی ندارد از مسیر پیش‌فرض `400` می‌گذرد. فهرست، نگاشت قرارداد را نشان می‌دهد و به معنی رخ‌دادن همه کدها در همه مسیرها نیست. خطای ناشناخته `500 UNEXPECTED_ERROR` و خطای نشست `401 AUTH_REQUIRED` است. خطاهای خود framework ممکن است code کاربردی نداشته باشند.

منبع نگاشت: [GlobalExceptionHandler](../MakanApp.Api/Errors/GlobalExceptionHandler.cs).

| code | HTTP | تعریف |
|---|---|---|
| `ACADEMIC_PERIOD_INVALID` | `400` | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `ACADEMIC_PERIOD_NOT_FOUND` | `404` | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `ACADEMIC_READ_NOT_ALLOWED` | `403` | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `ASSIGNMENT_ALREADY_PUBLISHED` | `409` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `ASSIGNMENT_ATTEMPTS_INVALID` | `400` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `ASSIGNMENT_DESCRIPTION_INVALID` | `400` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `ASSIGNMENT_DUE_DATE_INVALID` | `400` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `ASSIGNMENT_MAX_SCORE_INVALID` | `400` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `ASSIGNMENT_NOT_ALLOWED` | `403` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `ASSIGNMENT_NOT_DRAFT` | `409` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `ASSIGNMENT_NOT_FOUND` | `404` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `ASSIGNMENT_NOT_SUBMITTABLE` | `409` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `ASSIGNMENT_RECIPIENT_NOT_FOUND` | `404` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `ASSIGNMENT_TITLE_REQUIRED` | `400` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `ATTENDANCE_ALREADY_CHANGED` | `409` | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `ATTENDANCE_ALREADY_RECORDED` | `409` | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `ATTENDANCE_CORRECTION_REASON_REQUIRED` | `400` | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `ATTENDANCE_ENROLLMENT_INVALID` | `400` | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `ATTENDANCE_NOT_ALLOWED` | `403` | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `AUTH_REQUIRED` | `401` | [IdentityErrorCodes](../MakanApp.Application/Identity/IdentityErrorCodes.cs) |
| `CHANGE_CURSOR_INVALID` | `400` | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `CHILD_CONTEXT_NOT_ALLOWED` | `403` | [GuardianErrorCodes](../MakanApp.Application/Guardian/GuardianErrorCodes.cs) |
| `CHILD_CONTEXT_NOT_FOUND` | `404` | [GuardianErrorCodes](../MakanApp.Application/Guardian/GuardianErrorCodes.cs) |
| `CLASS_CAPACITY_EXCEEDED` | `409` | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `CLASS_INVALID` | `400` | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `CLASS_NOT_ACTIVE` | `409` | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `CLASS_NOT_FOUND` | `404` | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `CLASS_SESSION_CONFLICT` | `409` | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `CONCURRENCY_CONFLICT` | `412` | [StorageErrorCodes](../MakanApp.Application/Storage/StorageErrorCodes.cs) |
| `CONVERSATION_ARCHIVED` | `409` | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `CONVERSATION_CREATION_CONFLICT` | `409` | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `CONVERSATION_MANAGEMENT_NOT_ALLOWED` | `403` | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `CONVERSATION_NOT_ALLOWED` | `403` | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `CONVERSATION_NOT_FOUND` | `404` | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `CONVERSATION_ROLE_INVALID` | `400` | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `COURSE_INVALID` | `400` | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `COURSE_NOT_FOUND` | `404` | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `DELIVERY_CURSOR_INVALID` | `400` | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `DIRECT_CONVERSATION_CONFLICT` | `409` | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `DIRECT_RECIPIENT_NOT_AVAILABLE` | `404` | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `ENROLLMENT_ALREADY_ACTIVE` | `409` | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `ENROLLMENT_NOT_ACTIVE` | `409` | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `EVALUATION_ALREADY_RELEASED` | `409` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EVALUATION_CORRECTION_REASON_REQUIRED` | `400` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EVALUATION_NOT_ALLOWED` | `403` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EVALUATION_NOT_FOUND` | `404` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EVALUATION_NOT_READY_FOR_RELEASE` | `409` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EVALUATION_SCORE_INVALID` | `400` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EVALUATION_SUBMISSION_NOT_FINAL` | `409` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_ALREADY_FINALIZED` | `409` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_ALREADY_PUBLISHED` | `409` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_ANSWER_IDEMPOTENCY_CONFLICT` | `409` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_ANSWER_KEY_INVALID` | `400` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_ANSWER_NOT_ALLOWED` | `403` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_ANSWER_OPTION_INVALID` | `400` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_ANSWER_SET_VERSION_CONFLICT` | `409` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_ANSWER_TYPE_INVALID` | `400` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_ANSWER_VERSION_CONFLICT` | `409` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_ATTEMPTS_EXHAUSTED` | `409` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_ATTEMPTS_INVALID` | `400` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_ATTEMPT_NOT_ALLOWED` | `403` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_ATTEMPT_NOT_FINALIZABLE` | `409` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_ATTEMPT_NOT_FOUND` | `404` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_ATTEMPT_NOT_WRITABLE` | `409` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_DEADLINE_PASSED` | `409` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_DURATION_INVALID` | `400` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_FINALIZE_CONFLICT` | `409` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_FINALIZE_IDEMPOTENCY_CONFLICT` | `409` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_GRADE_ALREADY_RELEASED` | `409` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_GRADE_ATTEMPT_NOT_FINALIZED` | `409` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_GRADE_CORRECTION_REASON_REQUIRED` | `400` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_GRADE_INCOMPLETE` | `409` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_GRADE_NOT_ALLOWED` | `403` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_GRADE_NOT_FOUND` | `404` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_GRADE_NOT_RELEASED` | `409` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_GRADE_QUESTION_NOT_REVIEWED` | `409` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_GRADE_RELEASE_CONFLICT` | `409` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_GRADE_RELEASE_IDEMPOTENCY_CONFLICT` | `409` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_GRADE_RELEASE_NOT_ALLOWED` | `403` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_GRADE_SCORE_INVALID` | `400` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_MAX_SCORE_INVALID` | `400` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_NOT_ALLOWED` | `403` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_NOT_AVAILABLE_YET` | `409` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_NOT_DRAFT` | `409` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_NOT_FOUND` | `404` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_NOT_PUBLISHED` | `409` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_QUESTION_ORDER_INVALID` | `409` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_QUESTION_REQUIRED` | `400` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_QUESTION_SCORE_INVALID` | `400` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_QUESTION_SET_INVALID` | `409` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_RANDOMIZATION_INVALID` | `400` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_SCORE_TOTAL_INVALID` | `400` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_START_IDEMPOTENCY_CONFLICT` | `409` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_STUDENT_PREVIEW_NOT_ALLOWED` | `403` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_TITLE_REQUIRED` | `400` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_VERSION_LOCKED` | `409` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_WINDOW_CLOSED` | `409` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_WINDOW_INVALID` | `400` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_WRITE_LEASE_HELD_BY_OTHER_SESSION` | `409` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_WRITE_LEASE_REQUIRED` | `409` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `EXAM_WRITE_LEASE_STALE` | `409` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `FILE_ALREADY_DELETED` | `409` | [StorageErrorCodes](../MakanApp.Application/Storage/StorageErrorCodes.cs) |
| `FILE_EMPTY` | `400` | [StorageErrorCodes](../MakanApp.Application/Storage/StorageErrorCodes.cs) |
| `FILE_IN_USE` | `409` | [StorageErrorCodes](../MakanApp.Application/Storage/StorageErrorCodes.cs) |
| `FILE_NOT_ALLOWED` | `403` | [StorageErrorCodes](../MakanApp.Application/Storage/StorageErrorCodes.cs) |
| `FILE_NOT_FOUND` | `404` | [StorageErrorCodes](../MakanApp.Application/Storage/StorageErrorCodes.cs) |
| `FILE_NOT_READY` | `409` | [StorageErrorCodes](../MakanApp.Application/Storage/StorageErrorCodes.cs) |
| `FILE_STORAGE_FAILED` | `503` | [StorageErrorCodes](../MakanApp.Application/Storage/StorageErrorCodes.cs) |
| `FILE_TOO_LARGE` | `413` | [StorageErrorCodes](../MakanApp.Application/Storage/StorageErrorCodes.cs) |
| `FILE_TYPE_NOT_ALLOWED` | `415` | [StorageErrorCodes](../MakanApp.Application/Storage/StorageErrorCodes.cs) |
| `FILE_UPLOAD_FAILED` | `500` | [StorageErrorCodes](../MakanApp.Application/Storage/StorageErrorCodes.cs) |
| `GRADE_NOT_RELEASED` | `404` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `GRADE_RELEASE_CONFLICT` | `409` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `GUARDIAN_RELATION_NOT_FOUND` | `404` | [GuardianErrorCodes](../MakanApp.Application/Guardian/GuardianErrorCodes.cs) |
| `INVITATION_ALREADY_ACCEPTED` | `409` | [OrganizationErrorCodes](../MakanApp.Application/Organization/OrganizationErrorCodes.cs) |
| `INVITATION_EXPIRED` | `410` | [OrganizationErrorCodes](../MakanApp.Application/Organization/OrganizationErrorCodes.cs) |
| `INVITATION_NOT_FOUND` | `404` | [OrganizationErrorCodes](../MakanApp.Application/Organization/OrganizationErrorCodes.cs) |
| `INVITATION_REVOKED` | `410` | [OrganizationErrorCodes](../MakanApp.Application/Organization/OrganizationErrorCodes.cs) |
| `LAST_OWNER_REQUIRED` | `409` | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `LEARNER_NOT_FOUND` | `404` | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `MANAGED_CONVERSATION_INVALID` | `400` | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `MANAGER_ROLE_REQUIRED` | `403` | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `MEMBERSHIP_NOT_ACTIVE` | `403` | [OrganizationErrorCodes](../MakanApp.Application/Organization/OrganizationErrorCodes.cs) |
| `MENTION_NOT_ALLOWED` | `403` | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `MESSAGE_ATTACHMENT_NOT_ALLOWED` | `403` | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `MESSAGE_ATTACHMENT_NOT_READY` | `409` | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `MESSAGE_ATTACHMENT_REQUIRED` | `400` | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `MESSAGE_CLIENT_ID_REQUIRED` | `400` | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `MESSAGE_DELETED` | `409` | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `MESSAGE_EDIT_CONFLICT` | `412` | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `MESSAGE_EMPTY` | `400` | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `MESSAGE_FORWARD_NOT_ALLOWED` | `403` | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `MESSAGE_FORWARD_SCOPE_NOT_ALLOWED` | `403` | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `MESSAGE_IDEMPOTENCY_CONFLICT` | `409` | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `MESSAGE_KIND_INVALID` | `400` | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `MESSAGE_NOT_ALLOWED` | `403` | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `MESSAGE_NOT_EDITABLE` | `409` | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `MESSAGE_NOT_FOUND` | `404` | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `MESSAGE_PAGE_INVALID` | `400` | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `MESSAGE_PUBLISH_NOT_ALLOWED` | `403` | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `MESSAGE_REPLY_NOT_ALLOWED` | `403` | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `MESSAGE_TOO_LONG` | `400` | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `ORGANIZATION_NOT_FOUND` | `404` | [OrganizationErrorCodes](../MakanApp.Application/Organization/OrganizationErrorCodes.cs) |
| `ORGANIZATION_SCOPE_MISMATCH` | `403` | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `OTP_ALREADY_USED` | `409` | [IdentityErrorCodes](../MakanApp.Application/Identity/IdentityErrorCodes.cs) |
| `OTP_EXPIRED` | `410` | [IdentityErrorCodes](../MakanApp.Application/Identity/IdentityErrorCodes.cs) |
| `OTP_INVALID` | `400` | [IdentityErrorCodes](../MakanApp.Application/Identity/IdentityErrorCodes.cs) |
| `OTP_RATE_LIMITED` | `429` | [IdentityErrorCodes](../MakanApp.Application/Identity/IdentityErrorCodes.cs) |
| `OTP_TOO_MANY_ATTEMPTS` | `429` | [IdentityErrorCodes](../MakanApp.Application/Identity/IdentityErrorCodes.cs) |
| `OWNERSHIP_TRANSFER_CONFLICT` | `409` | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `OWNERSHIP_TRANSFER_NOT_FOUND` | `404` | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `PARENT_ROLE_REQUIRED` | `403` | [GuardianErrorCodes](../MakanApp.Application/Guardian/GuardianErrorCodes.cs) |
| `PARTICIPANT_ALREADY_ACTIVE` | `409` | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `PARTICIPANT_NOT_ACTIVE` | `403` | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `PARTICIPANT_NOT_FOUND` | `404` | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `PHONE_INVALID` | `400` | [IdentityErrorCodes](../MakanApp.Application/Identity/IdentityErrorCodes.cs) |
| `PIN_NOT_ALLOWED` | `403` | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `PROFILE_VALIDATION_FAILED` | `400` | [IdentityErrorCodes](../MakanApp.Application/Identity/IdentityErrorCodes.cs) |
| `REACTION_NOT_ALLOWED` | `403` | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `READ_CURSOR_INVALID` | `400` | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `REALTIME_SUBSCRIPTION_NOT_ALLOWED` | `403` | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `REPORT_IDEMPOTENCY_CONFLICT` | `409` | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `REPORT_MESSAGE_NOT_FOUND` | `404` | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `REPORT_NOT_ALLOWED` | `403` | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `ROLE_NOT_ACTIVE` | `403` | [OrganizationErrorCodes](../MakanApp.Application/Organization/OrganizationErrorCodes.cs) |
| `SCHEDULE_RULE_NOT_FOUND` | `404` | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `SEARCH_CURSOR_INVALID` | `400` | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `SEARCH_NOT_ALLOWED` | `403` | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `SEARCH_QUERY_INVALID` | `400` | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `SEARCH_QUERY_REQUIRED` | `400` | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `SESSION_CANCELLED` | `409` | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `SESSION_NOT_ACTIVE` | `409` | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `SESSION_NOT_FOUND` | `404` | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `SESSION_TIME_CONFLICT` | `409` | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `SESSION_TIME_INVALID` | `400` | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `SMS_PROVIDER_UNAVAILABLE` | `503` | [IdentityErrorCodes](../MakanApp.Application/Identity/IdentityErrorCodes.cs) |
| `SUBMISSION_ANSWER_INVALID` | `400` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `SUBMISSION_ATTEMPTS_EXHAUSTED` | `409` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `SUBMISSION_DEADLINE_PASSED` | `409` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `SUBMISSION_EMPTY` | `400` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `SUBMISSION_FILE_NOT_ALLOWED` | `403` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `SUBMISSION_FILE_NOT_READY` | `409` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `SUBMISSION_NOT_ALLOWED` | `403` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `SUBMISSION_NOT_DRAFT` | `409` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `SUBMISSION_NOT_FOUND` | `404` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `SUBMISSION_VERSION_MISMATCH` | `409` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `TEACHER_ASSIGNMENT_ALREADY_ACTIVE` | `409` | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `TEACHER_ASSIGNMENT_NOT_ACTIVE` | `409` | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `TEACHER_NOT_ALLOWED` | `403` | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `TEACHER_NOT_ASSIGNED` | `403` | [AssessmentErrorCodes](../MakanApp.Application/Assessment/AssessmentErrorCodes.cs) |
| `TEACHER_SESSION_CONFLICT` | `409` | [AcademicErrorCodes](../MakanApp.Application/Academic/AcademicErrorCodes.cs) |
| `USERNAME_ALREADY_EXISTS` | `409` | [IdentityErrorCodes](../MakanApp.Application/Identity/IdentityErrorCodes.cs) |
| `USER_BLOCK_NOT_ALLOWED` | `403` | [MessagingErrorCodes](../MakanApp.Application/Messaging/MessagingErrorCodes.cs) |
| `WORKSPACE_NOT_ALLOWED` | `403` | [OrganizationErrorCodes](../MakanApp.Application/Organization/OrganizationErrorCodes.cs) |
| `WORKSPACE_NOT_FOUND` | `404` | [OrganizationErrorCodes](../MakanApp.Application/Organization/OrganizationErrorCodes.cs) |

برای جریان استفاده، تنظیمات و نمونه درخواست‌ها به [راهنما](backend-api-guide.md) برگردید.
