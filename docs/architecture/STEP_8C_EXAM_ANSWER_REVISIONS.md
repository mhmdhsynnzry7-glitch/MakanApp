# STEP 8C — ذخیره پاسخ آزمون، autosave و کنترل نوشتن چنددستگاهی

این مرحله وضعیت دوم پاسخ آزمون را پیاده‌سازی می‌کند: هر درخواست موفق `SaveExamAnswer` یک
`AnswerRevision` پذیرفته‌شده و ماندگار در سرور می‌سازد. این پذیرش به معنی نهایی‌شدن آزمون،
تصحیح پاسخ یا انتشار نمره نیست.

## مدل دامنه

- `AnswerRevision` به یک `ExamAttempt` و دقیقاً یکی از `ExamAttemptQuestion`های تثبیت‌شده آن
  تعلق دارد. پاسخ به latest question خارج از attempt مجاز نیست.
- revisionهای قبلی حذف یا overwrite نمی‌شوند. `RevisionNumber` برای هر سؤال attempt صعودی است.
- `ExamAttemptQuestion.CurrentAnswerRevisionId` دسترسی مستقیم به آخرین پاسخ پذیرفته‌شده را
  فراهم می‌کند؛ بنابراین نمایش پاسخ فعلی نیازمند replay تاریخچه نیست.
- پاسخ objective فقط `SelectedOptionId` و پاسخ descriptive فقط `TextAnswer` دارد. گزینه با
  composite FK به همان `QuestionVersion` محدود می‌شود.
- متن descriptive عیناً نگه‌داری می‌شود، رشته خالی برای پاک‌کردن پاسخ معتبر است و حداکثر طول
  آن ۲۰٬۰۰۰ نویسه است.

## Write lease

هر `ExamAttempt` حداکثر یک نویسنده فعال دارد:

```text
WriterSessionId
WriteLeaseVersion
WriteLeaseAcquiredAtUtc
```

مالک lease یک `UserSession` معتبر است، نه صرفاً `UserId`. دریافت مجدد توسط همان session
idempotent است. session دیگر نمی‌تواند عادی بنویسد یا lease را بی‌صدا بگیرد؛ کاربر باید endpoint
انتقال صریح را فراخوانی کند. انتقال، `WriteLeaseVersion` را افزایش می‌دهد و درخواست‌های device قبلی
با نسخه قدیمی به `EXAM_WRITE_LEASE_STALE` می‌رسند.

lease timeout یا heartbeat مستقل ساخته نشده است. اعتبار آن با session، وضعیت attempt، انتقال صریح
و deadline کنترل می‌شود. session لغوشده پیش از رسیدن به use case در authentication رد می‌شود.

## Autosave، optimistic concurrency و idempotency

autosave همان تکرار `SaveExamAnswer` است و مدل جداگانه‌ای در سرور ندارد. هر تغییر معنایی موفق:

1. lease و deadline را با زمان سرور بررسی می‌کند.
2. `ExpectedRevisionNumber` را با revision فعلی سؤال مقایسه می‌کند.
3. یک `AnswerRevision` جدید می‌سازد.
4. current pointer سؤال را جابه‌جا می‌کند.
5. `ExamAttempt.AnswerSetVersion` را در همان transaction افزایش می‌دهد.

`ClientOperationId` همراه با SHA-256 payload و نسخه‌های مورد انتظار ذخیره می‌شود. retry همان
عملیات و همان payload دقیقاً receipt قبلی را برمی‌گرداند و نسخه جدیدی نمی‌سازد. استفاده از همان
شناسه با payload متفاوت conflict است.

ذخیره‌ها در transaction کوتاه `Serializable` انجام می‌شوند. ردیف attempt با
`UPDLOCK, HOLDLOCK` گرفته می‌شود؛ در نتیجه افزایش `AnswerSetVersion` اتمیک و صعودی است و
ذخیره هم‌زمان سؤال‌های مختلف فقط در محدوده همان attempt هماهنگ می‌شود، نه با lock سراسری.
`rowversion` روی attempt و current-answer state نیز دفاع تکمیلی optimistic concurrency است.

## Deadline و پاسخ آفلاین

مرجع پذیرش فقط `TimeProvider` سرور و `EffectiveDeadlineUtc` است:

```text
NowUtc < EffectiveDeadlineUtc
```

زمان محلی client در قرارداد پذیرفته نمی‌شود. پاسخی که قبل از deadline روی device ساخته ولی پس از
deadline به سرور رسیده است، revision رسمی ایجاد نمی‌کند.

## SQL Server

Migration `AddExamAnswerRevisions` موارد زیر را اضافه می‌کند:

- جدول `assessment.AnswerRevisions` با FKهای composite برای Organization، AttemptQuestion،
  QuestionVersion و Option.
- unique index روی revision هر AttemptQuestion.
- unique index روی `ClientOperationId` در محدوده attempt.
- check constraintهای نوع/شکل پاسخ، طول متن، hash، revision، lease version و answer-set version.
- `CurrentAnswerRevisionId` و `rowversion` در `assessment.ExamAttemptQuestions`.
- `WriterSessionId`، `WriteLeaseVersion`، `WriteLeaseAcquiredAtUtc` و `AnswerSetVersion` در
  `assessment.ExamAttempts`.
- FKها با `Restrict` برای جلوگیری از حذف cascade تاریخچه پاسخ.

ستون‌های نسخه attempt ابتدا nullable ایجاد، داده‌های موجود با صفر backfill و سپس `NOT NULL`
می‌شوند؛ default دائمی SQL برای داده جدید وجود ندارد.

## API

```text
POST /api/v1/academic/exam-attempts/{attemptId}/write-lease
POST /api/v1/academic/exam-attempts/{attemptId}/write-lease/transfer
PUT  /api/v1/academic/exam-attempts/{attemptId}/answers/{attemptQuestionId}
GET  /api/v1/academic/exam-attempts/{attemptId}/answers
```

فقط Student مالک با session، membership، OrganizationPerson و Enrollment فعال دسترسی دارد.
DTOهای دانش‌آموز شناسه session دستگاه دیگر، `IsCorrect`، answer key، correctness یا نمره را
برنمی‌گردانند.

## خارج از محدوده

- finalize و submit رسمی آزمون (STEP 8D)
- auto-finalize
- grading و GradeRelease
- AI، Copilot و تولید یا تصحیح پاسخ

`AnswerSetVersion` و current pointerهای ذخیره‌شده، ورودی لازم برای finalize دقیق در STEP 8D را
فراهم می‌کنند، اما هیچ رفتار finalize در این مرحله وجود ندارد.
