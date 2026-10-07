# STEP 8D — نهایی‌سازی آزمون و رسید پایدار

این مرحله مرز صریح میان «پاسخ ذخیره‌شده» و «آزمون نهایی‌شده» را ایجاد می‌کند. تا پیش از موفقیت
`FinalizeExam`، وجود `AnswerRevision` به معنی تحویل رسمی آزمون نیست. پس از نهایی‌سازی، وضعیت رسمی
پاسخ‌ها از snapshot پایدار خوانده می‌شود و current pointerهای قابل‌تغییر مبنای تصحیح آینده نیستند.

## جریان نهایی‌سازی

```text
POST /api/v1/academic/exam-attempts/{attemptId}/finalize
-> ExamsController
-> ExamFinalizationService
-> Student access + ownership + active session
-> Serializable transaction
-> ExamAttempt row lock
-> deadline + write lease + ExpectedAnswerSetVersion
-> ExamFinalAnswer snapshot
-> ExamAttempt.Finalize
-> SQL Server commit
-> ExamFinalReceiptDto
```

درخواست فقط `ClientOperationId`، `ExpectedAnswerSetVersion` و `WriteLeaseVersion` را می‌پذیرد.
زمان نهایی‌سازی، وضعیت، Student، Organization و revisionهای رسمی همگی از وضعیت معتبر سرور تعیین
می‌شوند.

## مدل دامنه و snapshot رسمی

- `ExamAttemptStatus.Finalized` وضعیت نهایی و غیرقابل‌نوشتن attempt است.
- `FinalizedAtUtc` با `TimeProvider` سرور تعیین می‌شود.
- `FinalizedAnswerSetVersion` دقیقاً برابر `AnswerSetVersion` پذیرفته‌شده است.
- `ExamFinalAnswer` برای هر سؤال پاسخ‌داده‌شده، شناسه دقیق `AnswerRevision` جاری در لحظه commit را
  ثبت می‌کند.
- سؤال بدون revision در snapshot ردیف ندارد. revision توصیفی خالی برای audit منجمد می‌شود، ولی در
  `AnsweredQuestionCount` شمرده نمی‌شود.
- تاریخچه `AnswerRevision` تغییر یا حذف نمی‌شود و پس از Finalize هیچ Save، Acquire lease یا Transfer
  lease پذیرفته نیست.

این مدل باعث می‌شود STEP 8E بتواند دقیقاً همان revisionهای رسمی را تصحیح کند، حتی اگر در آینده
current pointerها به‌اشتباه تغییر کنند.

## هم‌زمانی و تراکنش SQL Server

نهایی‌سازی در transaction کوتاه `Serializable` انجام می‌شود. `EfExamFinalizationStore` ابتدا ردیف
`ExamAttempt` را با `UPDLOCK, HOLDLOCK` می‌گیرد. مسیر `SaveExamAnswer` نیز همین ردیف و همین ترتیب lock
را دارد. بنابراین رقابت Save و Finalize فقط یکی از این دو نتیجه را دارد:

1. Save زودتر commit می‌شود؛ `AnswerSetVersion` افزایش می‌یابد و Finalize با
   `EXAM_ANSWER_SET_VERSION_CONFLICT` رد می‌شود.
2. Finalize زودتر commit می‌شود؛ Save بعدی با `EXAM_ATTEMPT_NOT_WRITABLE` رد می‌شود.

Finalizeهای هم‌زمان نیز روی همان attempt سریال می‌شوند و فقط یک اثر پایدار و یک snapshot ایجاد
می‌کنند. `rowversion` دفاع تکمیلی optimistic concurrency است.

## deadline

مرز زمانی با رفتار SaveAnswer یکسان است:

```text
NowUtc < EffectiveDeadlineUtc
```

در لحظه دقیق deadline درخواست پذیرفته نمی‌شود. ساعت یا timestamp محلی client هیچ اثر authoritative
ندارد. Attemptی که بدون Finalize معتبر از deadline عبور کند `Expired` می‌شود؛ ذخیره‌شدن پاسخ پیش از
deadline به‌تنهایی به معنی تحویل رسمی نیست. auto-finalize در این مرحله وجود ندارد.

## idempotency و retry

`ClientOperationId` و hash معنایی درخواست روی `ExamAttempt` ذخیره می‌شوند:

- تکرار همان operation و همان payload، همان receipt پایدار را برمی‌گرداند.
- استفاده از همان operation با payload متفاوت،
  `EXAM_FINALIZE_IDEMPOTENCY_CONFLICT` است.
- retry سازگار با operation جدید ولی همان `FinalizedAnswerSetVersion` و `WriteLeaseVersion`، receipt
  قبلی را برمی‌گرداند.
- retry ناسازگار روی attempt نهایی‌شده، `EXAM_ALREADY_FINALIZED` است.

این رفتار lost acknowledgement را بدون ایجاد Finalize، timestamp یا snapshot دوم پوشش می‌دهد.

## SQL Server

Migration `AddExamFinalization` موارد زیر را اضافه می‌کند:

- ستون‌های nullable مربوط به نهایی‌سازی در `assessment.ExamAttempts`:
  `FinalizedAtUtc`، `FinalizedAnswerSetVersion`، `FinalizeClientOperationId`،
  `FinalizeRequestHash` و `FinalizedBySessionId`.
- جدول `assessment.ExamFinalAnswers` با کلید اصلی مرکب
  `(OrganizationId, ExamAttemptId, ExamAttemptQuestionId)`.
- FK مرکب به `ExamAttempts`، `ExamAttemptQuestions` و `AnswerRevisions` برای تضمین Organization،
  Attempt، QuestionVersion و revision یکسان.
- FK از `FinalizedBySessionId` به `identity.UserSessions`.
- `Restrict` روی تمام FKهای تاریخچه نهایی برای جلوگیری از cascade delete ناامن.
- check constraintهای سازگاری status/metadata، برابری نسخه نهایی با نسخه مجموعه پاسخ‌ها، زمان پیش از
  deadline و طول ثابت request hash.

ستون‌های جدید nullable هستند تا attemptهای قدیمی InProgress/Expired بدون backfill جعلی معتبر بمانند.

## API و حریم خصوصی

```text
POST /api/v1/academic/exam-attempts/{attemptId}/finalize
GET  /api/v1/academic/exam-attempts/{attemptId}/receipt
```

فقط Student مالک attempt با membership، OrganizationPerson، Enrollment و session فعال مجاز است.
Parent، Teacher، Manager و Student دیگر امکان نهایی‌سازی ندارند. receipt شامل شناسه attempt/exam/version،
شماره attempt، زمان شروع و نهایی‌سازی، نسخه نهایی، تعداد پاسخ‌داده‌شده، تعداد کل و status است. score،
correctness، answer key، grade و feedback در آن وجود ندارد.

## خارج از محدوده

- تصحیح objective یا descriptive
- GradeRelease و اصلاح نتیجه
- auto-finalize یا background scheduler
- Notification consumer
- AI، Copilot و تشخیص تقلب

تصحیح باید در STEP 8E تنها از `ExamFinalAnswers` و revisionهای منجمدشده استفاده کند.
