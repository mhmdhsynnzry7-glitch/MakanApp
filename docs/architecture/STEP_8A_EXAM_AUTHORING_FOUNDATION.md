# STEP 8A — زیرساخت تعریف، نسخه‌بندی و انتشار آزمون

این مرحله زیرساخت authoring آزمون را برای جریان‌های معلم و دانش‌آموز مرتبط با
`TE-07`، `TE-08`، `ED-09` و `ED-10` فراهم می‌کند. اجرای attempt، timer، autosave،
ثبت پاسخ، finalize، grading و انتشار نتیجه عمداً در این مرحله وجود ندارد.

## مدل نسخه‌بندی

- `Exam` هویت پایدار آزمون و کلاس مالک آن را نگه می‌دارد.
- `ExamVersion` محتوای قابل انتشار، بازه زمانی، مدت، تعداد تلاش، سقف نمره و سیاست
  ترتیب سؤال‌ها را نگه می‌دارد.
- `CurrentVersionNumber` نسخه جاری editor را مشخص می‌کند.
- `LatestPublishedVersionNumber` نسخه‌ای را مشخص می‌کند که برای دانش‌آموز و والد
  قابل مشاهده است؛ بنابراین ساخت V2 پیش‌نویس، معنای تاریخی V1 را تغییر نمی‌دهد.
- نسخه منتشرشده immutable است. تغییر بعد از انتشار فقط با
  `CreateNextExamVersion` و کپی عمیق سؤال‌ها و گزینه‌ها انجام می‌شود.

## سؤال و کلید پاسخ

دو نوع سؤال در این مرحله وجود دارد:

- `ObjectiveSingleChoice`: حداقل دو گزینه متمایز و دقیقاً یک گزینه صحیح دارد.
- `Descriptive`: گزینه و answer key ندارد و ارزیابی آن در مرحله بعدی دستی خواهد بود.

امتیاز هر سؤال `decimal(9,2)` و مثبت است. هنگام انتشار، مجموع امتیاز سؤال‌ها باید
دقیقاً با `ExamVersion.MaxScore` برابر باشد. مقدار سراسری ۲۰ یا SQL default وجود
ندارد.

DTOهای editor و student جدا هستند. `ExamEditorDto` برای معلم/مدیر شامل
`IsCorrect` است، اما `StudentSafeExamPreview` چنین propertyای ندارد. endpoint
پیش‌نمایش معلم و دسترسی واقعی دانش‌آموز از یک projection مشترک استفاده می‌کنند.

## انتشار و زمان

- `AvailableUntilUtc` باید بعد از `AvailableFromUtc` باشد.
- `DurationMinutes` و `MaxAttempts` باید مثبت باشند.
- انتشار در transaction کوتاه `Serializable` انجام می‌شود و مدل، سؤال‌ها، مجموع
  امتیاز و answer key دوباره اعتبارسنجی می‌شوند.
- محتوای دانش‌آموز فقط برای نسخه منتشرشده، enrollment فعال و داخل بازه آزمون
  ارائه می‌شود.
- زمان authoritative از `TimeProvider` سمت سرور و UTC گرفته می‌شود.

سیاست result release در اسناد فعلی برای این slice به اندازه کافی تعریف نشده است؛
بنابراین metadata یا رفتار تازه‌ای برای انتشار نتیجه اختراع نشده و پایان آزمون با
انتشار نتیجه یکی فرض نشده است.

## مجوز

- Manager فقط در Organization فعال فعلی مجاز است.
- Teacher علاوه بر membership و role فعال، به `TeacherAssignment` فعال همان کلاس
  نیاز دارد.
- Student فقط آزمون منتشرشده کلاس دارای enrollment فعال را می‌بیند.
- Parent با child context معتبر فقط metadata امن آزمون منتشرشده را می‌بیند و به
  محتوای سؤال یا mutation دسترسی ندارد.
- `OrganizationId` و `MembershipId` از درخواست گرفته نمی‌شوند و از
  `AccessContext` معتبر سمت سرور استخراج می‌شوند.

## SQL Server

Migration `AddExamAuthoringFoundation` جدول‌های زیر را در schema `assessment`
ایجاد می‌کند:

- `Exams`
- `ExamVersions`
- `QuestionVersions`
- `QuestionOptions`

FKهای ترکیبی شامل `OrganizationId` از اتصال cross-tenant جلوگیری می‌کنند. ترتیب
سؤال در نسخه و ترتیب گزینه در سؤال unique است. برای هر آزمون حداکثر یک نسخه draft
با filtered unique index مجاز است. `Exam`، `ExamVersion` و `QuestionVersion` دارای
`rowversion` هستند و stale mutation با `CONCURRENCY_CONFLICT` رد می‌شود.

## API

```text
POST   /api/v1/academic/classes/{classId}/exams
GET    /api/v1/academic/exams
GET    /api/v1/academic/exams/{examId}/editor
GET    /api/v1/academic/exams/{examId}/teacher-preview
PATCH  /api/v1/academic/exams/{examId}
POST   /api/v1/academic/exams/{examId}/questions
PATCH  /api/v1/academic/exams/{examId}/questions/{questionId}
DELETE /api/v1/academic/exams/{examId}/questions/{questionId}
GET    /api/v1/academic/exams/{examId}/student-preview
POST   /api/v1/academic/exams/{examId}/publish
POST   /api/v1/academic/exams/{examId}/versions
```

## خارج از محدوده

- شروع و وضعیت attempt
- timer و محاسبه deadline هر attempt
- answer draft/revision و autosave
- write lease و offline retry
- finalize و server receipt
- grading، grade revision و result release
- rubric، AI grading، Copilot و RAG
- randomization گزینه‌ها
