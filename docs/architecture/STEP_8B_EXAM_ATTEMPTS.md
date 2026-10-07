# STEP 8B — شروع آزمون و تثبیت تلاش دانش‌آموز

این مرحله lifecycle سمت سرور برای شروع آزمون را اضافه می‌کند. ایجاد attempt فقط با
درخواست موفق `StartExam` انجام می‌شود؛ مشاهده قوانین یا preview هیچ attemptی مصرف
نمی‌کند.

## مدل دامنه

- `ExamAttempt` یک تلاش رسمی دانش‌آموز را برای یک `ExamVersion` مشخص نگه می‌دارد.
- `ExamAttemptQuestion` مجموعه و ترتیب سؤال‌هایی را که در شروع تلاش انتخاب شده‌اند
  به‌صورت صریح ذخیره می‌کند.
- lifecycle فعلی عمداً فقط `InProgress` و `Expired` است. `Finalized` در STEP 8D
  اضافه می‌شود.
- برای آزاد شدن invariant «یک تلاش فعال»، انقضا هنگام Start بعدی و داخل همان
  transaction ثبت می‌شود. در readها نیز status مؤثر پس از deadline به‌صورت
  `Expired` نمایش داده می‌شود.

## مجوز و ownership

فقط کاربر احراز هویت‌شده با session فعال، workspace سازمانی، نقش فعال `Student`،
`OrganizationPerson` فعال و `Enrollment` فعال همان کلاس می‌تواند تلاش را شروع یا
مطالعه کند. شناسه Organization، Student، Enrollment، ExamVersion، AttemptNumber،
زمان و ترتیب سؤال از client پذیرفته نمی‌شود.

Parent، Teacher و Manager از endpoint دانش‌آموز برای شروع یا impersonation استفاده
نمی‌کنند. داشبورد عملیاتی TE-08 به مرحله‌ای جدا موکول شده است؛ داده‌های attempt
برای آن حفظ می‌شوند.

## زمان و deadline

زمان مرجع از `TimeProvider` سرور دریافت می‌شود. شروع فقط در بازه زیر مجاز است:

```text
NowUtc >= AvailableFromUtc
NowUtc < AvailableUntilUtc
```

deadline مؤثر:

```text
min(AvailableUntilUtc, StartedAtUtc + DurationMinutes)
```

Accommodation هنوز مدل مصوبی ندارد و در این مرحله ساخته نشده است.

## idempotency و concurrency

`ClientOperationId` برای Start الزامی است. تکرار همان عملیات، همان attempt را
برمی‌گرداند و استفاده مجدد آن برای آزمون دیگر conflict است. علاوه بر آن، هر Start
در صورت وجود attempt فعال همان آزمون، همان attempt را resume می‌کند؛ بنابراین
تغییر device یا کلید retry تلاش دوم ایجاد نمی‌کند.

تخصیص `AttemptNumber` داخل transaction کوتاه `Serializable` انجام می‌شود. ردیف‌های
Exam، Enrollment، ExamVersion و بازه attemptها با `UPDLOCK, HOLDLOCK` خوانده
می‌شوند. filtered unique index نیز وجود بیش از یک attempt فعال برای یک
`Exam + Enrollment` را در سطح SQL Server رد می‌کند. این invariant از حداقل خواسته
`ExamVersion + Enrollment` قوی‌تر است و هنگام انتشار نسخه جدید نیز attempt فعال
قدیمی را قابل resume نگه می‌دارد.

## تثبیت نسخه و ترتیب سؤال

- `ExamVersionId` هنگام ایجاد attempt ذخیره می‌شود و بعداً از latest version دوباره
  resolve نمی‌شود.
- سیاست `None` ترتیب منتشرشده را ذخیره می‌کند.
- سیاست `QuestionOrder` یک shuffle امن سمت سرور انجام می‌دهد و نتیجه را فقط یک‌بار
  در `ExamAttemptQuestions.DisplayOrder` ثبت می‌کند.
- option randomization در STEP 8A مصوب نشده بود و اینجا نیز اضافه نشده است.
- DTO دانش‌آموز فقط متن و metadata قابل نمایش گزینه‌ها را دارد و `IsCorrect`،
  answer key یا یادداشت خصوصی را برنمی‌گرداند.

## SQL Server

Migration `AddExamAttempts` جدول‌های زیر را در schema `assessment` ایجاد می‌کند:

- `ExamAttempts`
- `ExamAttemptQuestions`

قیود مهم شامل composite FK برای Organization/Class/Enrollment/Exam/ExamVersion،
یکتایی شماره تلاش، یکتایی `ClientOperationId` در enrollment، filtered unique index
برای تلاش فعال، یکتایی سؤال و ترتیب نمایش در attempt، check deadline و `rowversion`
است.

## API

```text
POST /api/v1/academic/exams/{examId}/attempts/start
GET  /api/v1/academic/exams/{examId}/attempts/me/active
GET  /api/v1/academic/exam-attempts/{attemptId}
GET  /api/v1/academic/exams/{examId}/attempts/me
```

## خارج از محدوده

- `AnswerRevision`، ذخیره پاسخ و autosave
- `WriteLease` و سیاست نویسنده چنددستگاهی
- finalize و server receipt
- grading و result release
- accommodation
- dashboard تفصیلی TE-08
- AI، Copilot و تولید/تصحیح پاسخ

`WriteLease` و `AnswerRevision` موضوع STEP 8C هستند.
