# STEP 6C — چرخه ارسال تکلیف

این مرحله چرخه‌ی سمت سرور زیر را به ماژول `Assessment` اضافه می‌کند:

```text
Published Assignment
-> AssignmentRecipient
-> Draft SubmissionAttempt
-> Answer / Ready FileAsset
-> Final Submit
-> Submitted SubmissionAttempt
-> Server Receipt
```

## سیاست تلاش و پیش‌نویس

- برای هر `AssignmentRecipient + AssignmentVersion` حداکثر یک پیش‌نویس فعال وجود دارد.
- بازکردن صفحه و ایجاد پیش‌نویس، یک تلاش نهایی‌شده مصرف نمی‌کند.
- `AttemptNumber` هنگام ایجاد پیش‌نویس و داخل تراکنش `Serializable` توسط سرور تعیین می‌شود.
- شماره‌ی تلاش بعدی برابر تعداد تلاش‌های `Submitted` قبلی به‌علاوه یک است.
- `MaxAttempts` فقط تعداد تلاش‌های پذیرفته‌شده را محدود می‌کند؛ یک Draft رهاشده به‌تنهایی سهمیه را مصرف نمی‌کند.
- filtered unique index و unique attempt-number index از ایجاد Draft یا شماره‌ی تکراری در SQL Server جلوگیری می‌کنند.

## مرز Draft و Submitted

- ذخیره متن پاسخ یا آماده‌شدن `FileAsset` به معنی ارسال رسمی نیست.
- ارسال رسمی فقط با endpoint نهایی‌سازی و ثبت موفق تراکنش انجام می‌شود.
- پاسخ باید متن غیرخالی یا حداقل یک فایل `Ready` داشته باشد.
- زمان پذیرش و تشخیص دیرکرد فقط از ساعت سرور گرفته می‌شود.
- بعد از `Submitted`، متن و فهرست پیوست‌ها تغییرناپذیر هستند.
- retry همان Final Submit، رسید ذخیره‌شده با همان `AttemptNumber` و `SubmittedAtUtc` را برمی‌گرداند.

## اتصال به Storage

- بایت فایل در Assessment ذخیره نمی‌شود؛ `SubmissionAttachment` فقط به `FileAsset` موجود اشاره می‌کند.
- فقط فایل `Ready` متعلق به همان کاربر و همان Organization قابل اتصال و نهایی‌سازی است.
- حذف پیوست Draft، خود فایل را حذف نمی‌کند؛ lifecycle فایل همچنان متعلق به Storage است.
- هنگام پذیرش نهایی، `RetainedAtUtc` روی `FileAsset` ثبت می‌شود تا حذف عادی کاربر سابقه رسمی را خراب نکند.
- Storage از مفهوم اختصاصی Submission آگاه نیست و فقط قرارداد عمومی retention را اجرا می‌کند.

## Authorization

- Student فقط از طریق زنجیره‌ی معتبر `OrganizationPerson -> Enrollment -> AssignmentRecipient` می‌تواند Draft خود را ایجاد یا تغییر دهد.
- Teacher فقط تلاش‌های `Submitted` کلاس‌هایی را می‌خواند که `TeacherAssignment` فعال دارد و اجازه mutation ندارد.
- Manager فقط تلاش‌های `Submitted` Organization جاری را می‌خواند.
- Parent اجازه mutation ندارد. با selected-child معتبر فقط وضعیت/رسید `Submitted` را می‌بیند؛ متن پاسخ و پیوست‌های خصوصی در پاسخ Parent حذف می‌شوند.
- شناسه‌های دریافتی از client فقط selector هستند و Organization، Enrollment، Version، AttemptNumber، زمان و Status از داده معتبر سرور resolve می‌شوند.

## Concurrency و تراکنش

- ایجاد Draft و Final Submit در تراکنش کوتاه `Serializable` انجام می‌شوند.
- روی scopeهای recipient، attempt و file از قفل‌های `UPDLOCK, HOLDLOCK` استفاده می‌شود.
- `rowversion` از overwrite خاموش بین چند دستگاه جلوگیری می‌کند.
- درخواست‌های هم‌زمان Final Submit روی یک Attempt به یک transition و یک رسید پایدار ختم می‌شوند.
- هیچ تماس شبکه، SMS، Notification یا AI داخل تراکنش وجود ندارد.
