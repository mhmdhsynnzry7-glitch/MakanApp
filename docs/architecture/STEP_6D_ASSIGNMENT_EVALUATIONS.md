# STEP 6D — ارزیابی نسخه‌دار و انتشار کنترل‌شده نمره

این مرحله چرخه تکلیف را بدون ترکیب‌کردن «ذخیره ارزیابی» و «انتشار نتیجه» کامل می‌کند:

```text
Submitted SubmissionAttempt
-> Draft EvaluationRevision
-> Explicit GradeRelease
-> Student / authorized Parent read model
-> Correction EvaluationRevision
-> Explicit replacement release
```

## سیاست نمره

- `MaxScore` متعلق به همان `AssignmentVersion` و از نوع `decimal(9,2)` است.
- مقدار باید مثبت، بدون SQL Default و هنگام ساخت نسخه صریح باشد.
- `Score` نیز `decimal(9,2)` است و در Domain/Application در بازه `0..MaxScore` اعتبارسنجی می‌شود.
- نسخه منتشرشده قابل ویرایش درجا نیست؛ در نتیجه سقف تاریخی attempt و ارزیابی تغییر نمی‌کند.
- Rubric ساختاریافته در این مرحله وجود ندارد.

Migration مقدار `MaxScore` را ابتدا nullable می‌سازد، رکوردهای Development/Test قبلی را با ۲۰ backfill می‌کند و سپس ستون را `NOT NULL` و دارای `CHECK (MaxScore > 0)` می‌کند. این مقدار backfill، default نسخه‌های جدید نیست.

## lifecycle ارزیابی

- `Draft`: فقط ارزیاب مجاز می‌بیند و با `rowversion` ویرایش می‌کند.
- `Released`: نتیجه با یک `GradeRelease` صریح منتشر شده و دیگر مستقیماً قابل ویرایش نیست.
- `Superseded`: revision تاریخی است که پس از انتشار correction جایگزین شده است.
- correction یک `EvaluationRevision` جدید با `SupersedesEvaluationRevisionId` و `CorrectionReason` می‌سازد.
- تا پیش از release correction، نتیجه قابل مشاهده همان revision منتشرشده قبلی است.

## مرز اطلاعات مخاطبان

- DTO ارزیاب می‌تواند `TeacherPrivateNote` را داشته باشد.
- DTO دانش‌آموز فقط `Score` و `LearnerFeedback` منتشرشده را دارد.
- DTO والد فقط `Score` و `GuardianVisibleFeedback` منتشرشده را دارد.
- یادداشت خصوصی معلم نه کپی می‌شود و نه در DTO/ProblemDetails دانش‌آموز یا والد قرار می‌گیرد.

## Authorization

- Teacher به session، workspace، membership، نقش Teacher و `TeacherAssignment` فعال همان کلاس نیاز دارد.
- Manager فقط در Organization فعلی مجاز است.
- Student باید مالک attempt از زنجیره enrollment/recipient باشد.
- Parent باید نقش فعال Parent، child context انتخاب‌شده، `GuardianRelation` فعال و ارتباط همان learner با attempt را داشته باشد.
- revoke شدن GuardianRelation دسترسی‌های بعدی والد را می‌بندد، ولی نتیجه تاریخی را حذف نمی‌کند.

## یکپارچگی و concurrency

- FK ترکیبی Organization/Attempt از اتصال cross-tenant جلوگیری می‌کند.
- revision number در هر attempt یکتا است.
- filtered unique index برای هر attempt حداکثر یک Draft و یک Released جاری نگه می‌دارد.
- `GradeRelease` برای هر `EvaluationRevision` یکتا است.
- FK self-reference تضمین می‌کند correction فقط revision همان attempt را supersede کند.
- `rowversion` و original-value check، stale edit را با `CONCURRENCY_CONFLICT` رد می‌کند.
- عملیات release/correction در transaction کوتاه `Serializable` و با `UPDLOCK, HOLDLOCK` اجرا می‌شوند.
- retry انتشار همان release ذخیره‌شده و همان `ReleasedAtUtc` را برمی‌گرداند.

## محدودیت آگاهانه

عملیات جداگانه `ReturnSubmissionForRevision` اضافه نشده است. مدل فعلی اجازه attempt بعدی را فقط طبق `MaxAttempts`، deadline و late policy موجود می‌دهد، اما سیاست مستقل reopen/return هنوز تصویب نشده است. AI grading، Notification، Exam و LearningEvidence نیز خارج از STEP 6D هستند.
