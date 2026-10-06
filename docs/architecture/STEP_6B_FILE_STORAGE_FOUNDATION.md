# زیرساخت مشترک فایل و بارگذاری — STEP 6B

این سند تصمیم‌های اجرایی زیرساخت مشترک فایل را ثبت می‌کند. این قابلیت بخشی از ماژول مشترک `Storage` در Modular Monolith است. اتصال اولیه آن به Submission در STEP 6C و اتصال `FileAsset` به Message و مجوز دانلود مبتنی بر Conversation در STEP 7C انجام شده است؛ Storage همچنان به جزئیات داخلی Messaging وابسته نیست.

## مرز مسئولیت‌ها

- SQL Server فقط metadata و چرخه عمر `storage.FileAssets` را نگه می‌دارد؛ محتوای باینری داخل database ذخیره نمی‌شود.
- قرارداد provider-neutral با نام `IFileStorage` در Application قرار دارد و فقط با `Stream` کار می‌کند.
- `LocalFileStorage` adapter محیط‌های Development و Testing است. محیط Production باید provider واقعی خود را پیکربندی کند و تا آن زمان `UnavailableFileStorage` خطای کنترل‌شده برمی‌گرداند.
- `IFormFile` فقط در `FilesController` استفاده می‌شود و وارد Application یا Domain نمی‌شود.

## چرخه عمر و سازگاری

چرخه عمر فعلی `Pending -> Ready | Rejected -> Deleted` است. بارگذاری در سه گام انجام می‌شود:

1. metadata با وضعیت `Pending` در SQL Server ثبت می‌شود.
2. stream فایل با کلید server-generated در storage نوشته و SHA-256 محاسبه می‌شود.
3. metadata با اندازه واقعی، hash و زمان تکمیل به `Ready` تغییر می‌کند.

تراکنش SQL نمی‌تواند نوشتن فایل و database را اتمیک کند. اگر نوشتن storage شکست بخورد، رکورد `Rejected` می‌شود و پاک‌سازی bytes به‌صورت best effort انجام می‌شود. اگر process یا finalize پس از نوشتن bytes شکست بخورد، رکورد `Pending` همراه `StorageKey` و زمان‌ها باقی می‌ماند تا reconciliation بعدی orphan را پیدا کند. حذف عادی metadata را `Deleted` می‌کند و سپس پاک‌سازی bytes را best effort انجام می‌دهد؛ بنابراین شکست پاک‌سازی تاریخچه را از بین نمی‌برد.

## مالکیت و دسترسی

- فایل unattached فقط برای همان uploader و در همان context جاری قابل دسترسی است.
- scope سازمانی فقط از `AccessContext` معتبر گرفته می‌شود؛ `OrganizationId` ارسالی client مجوز یا scope ایجاد نمی‌کند.
- دانستن `FileAssetId` به‌تنهایی مجوز دانلود نیست و مسیر فیزیکی یا `StorageKey` در API برگردانده نمی‌شود.
- مجوزهای آینده برای attachment باید resource-based باشند؛ مثلاً دسترسی Assignment یا Submission نباید به سیاست کلی «همه اعضای سازمان» تبدیل شود.

## نگه‌داری و پاک‌سازی آینده

indexهای وضعیت/زمان ایجاد، `UnattachedExpiresAtUtc` و `StorageKey` امکان شناسایی این موارد را فراهم می‌کنند:

- `Pending` قدیمی که هرگز finalize نشده است؛
- `Ready` unattached که عمر موقت آن تمام شده است؛
- metadata با وضعیت `Deleted` یا `Rejected` که پاک‌سازی bytes آن نیاز به retry دارد.

در این مرحله background job ساخته نشده است. اضافه‌کردن cleanup worker باید در یک مرحله مستقل، idempotent و با مشاهده‌پذیری مناسب انجام شود.

## محدودیت‌های امنیتی فعلی

لیست MIME مجاز و سقف حجم configurable هستند و نام فایل client فقط به‌عنوان metadata پاک‌سازی‌شده نگه‌داری می‌شود. `Content-Type` ادعای client است و اثبات امنیت محتوا نیست. در این مرحله antivirus، تشخیص واقعی نوع فایل، cloud storage، CDN، transformation و deduplication بین tenantها وجود ندارد و هیچ فایل به‌عنوان scan‌شده یا clean علامت‌گذاری نمی‌شود.
