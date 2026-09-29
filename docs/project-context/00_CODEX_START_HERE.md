# MakanApp — CODEX START HERE

> این فایل اولین مرجع Codex برای شروع کار روی پروژه ماکان است.

## 1) هدف

این بسته برای هم‌راستا کردن Codex با سه چیز ساخته شده است:

1. معماری فنی ماکان
2. جریان‌ها و صفحات UX
3. وضعیت واقعی Frontend موجود

**Frontend از قبل پیاده‌سازی شده است. Codex نباید آن را از صفر بازسازی یا بدون بررسی بازنویسی کند.**

Repository فعلی Frontend:

```text
https://github.com/RastinaxAI/MakanApp-Client.git
```

---

## 2) ترتیب اعتبار منابع

Codex باید منابع را با این اولویت بخواند:

1. `01_SQLSERVER_ARCHITECTURE_OVERRIDE.md`
   - فقط برای تصمیم‌های دیتابیس، EF Core persistence، concurrency، migration، locking، search و backup مرجع نهایی است.
   - هر اشاره به MySQL در سند معماری اصلی توسط این فایل override می‌شود.

2. `02_Makan_Architecture_v3_Original_MySQL_Reference.pdf`
   - مرجع معماری برای تصمیم‌های غیر دیتابیسی.
   - بخش‌های MySQL آن فقط سابقه هستند و نباید پیاده‌سازی شوند.

3. `03_Makan_User_Flows_and_Screens_UX_v1.0.pdf`
   - مرجع Flowها، شناسه صفحات، stateهای UX و acceptance criteria.
   - شناسه‌هایی مثل `C-03`, `MSG-02`, `ED-07`, `TE-06`, `MG-03` باید در مستندات و mapping توسعه حفظ شوند.

4. `04_Flow.png`
   - نقشه تصویری سطح بالا برای درک ارتباط Website، Login، App، Messenger، Copilot، School، Profile و Backoffice.

5. `05_Makan_Project_Status.pdf`
   - مرجع وضعیت واقعی خروجی فعلی Frontend/Sandbox/Tests.
   - این سند «معماری مطلوب» نیست؛ می‌گوید اکنون چه چیزهایی وجود دارند و چه چیزهایی هنوز mock/sandbox هستند.

6. **کد واقعی Repository**
   - برای دانستن آنچه واقعاً پیاده‌سازی شده است.
   - اگر کد با اسناد متفاوت بود، اختلاف را گزارش کن؛ خودسرانه یکی را روی دیگری overwrite نکن.

---

## 3) تصمیم قطعی دیتابیس

دیتابیس اصلی Backend ماکان:

```text
Microsoft SQL Server
```

است.

بنابراین موارد زیر در معماری اصلی **منسوخ و غیرمجاز برای پیاده‌سازی جدید** هستند:

```text
MySQL
InnoDB
MySql.EntityFrameworkCore
Pomelo.EntityFrameworkCore.MySql
BINARY(16) UUID storage
MySQL generated-column workaround for active uniqueness
SELECT ... FOR UPDATE
SKIP LOCKED as a MySQL-specific design assumption
MySQL FULLTEXT as the selected search solution
MySQL binlog/PITR runbook
```

جایگزین دقیق این موارد در `01_SQLSERVER_ARCHITECTURE_OVERRIDE.md` آمده است.

---

## 4) فناوری‌هایی که این Override تغییر نمی‌دهد

این بسته فقط تصمیم دیتابیس را اصلاح می‌کند.

Codex نباید به دلیل این فایل‌ها نسخه .NET، React، React Native، Expo، Tailwind یا ساختار Frontend را خودکار تغییر دهد.

قبل از هر تغییر نسخه:

- `global.json`
- فایل‌های `.csproj`
- `package.json`
- lockfile
- README
- AGENTS.md

را بررسی کن.

اگر TargetFramework موجود با سند معماری اختلاف دارد، اختلاف را گزارش کن و بدون تأیید مالک پروژه TargetFramework را تغییر نده.

---

## 5) مدل کلان محصول

چهار ناحیه اصلی اپ:

```text
Messenger
Copilot
School
Profile
```

Dashboard هر نقش داخل `School` قرار می‌گیرد؛ Home پنجم جداگانه ایجاد نشود مگر تصمیم محصول بعداً تغییر کند.

### نقش‌های آموزشی

```text
Student
Teacher
Guardian/Parent
School Manager
```

Platform Admin/Backoffice مرز جدا دارد و School Manager ادمین پلتفرم نیست.

### فضای شخصی

کاربر حتی بدون عضویت آموزشگاهی می‌تواند فضای شخصی داشته باشد.

Messenger شخصی و Copilot شخصی باید از عضویت آموزشگاه جدا بمانند.

---

## 6) اصول معماری غیرقابل دور زدن

### Modular Monolith

Backend در شروع Modular Monolith است، نه مجموعه Microserviceهای متعدد.

Hostهای اجرایی می‌توانند جدا باشند:

```text
Makan.Api
Makan.Worker
Makan.AiWorker
```

اما قواعد دامنه و قراردادها ماژولار باقی می‌مانند.

### ماژول‌های اصلی Backend

```text
Identity
Organization
Guardian
Academic
Curriculum
Assessment
Messaging
Knowledge
Intelligence
Copilot
Commerce
Notification
Storage/Audit
Platform Operations
```

### مجوز

Role به‌تنهایی مجوز نیست.

هر عملیات حساس باید حداقل این موارد را بررسی کند:

```text
Authenticated Actor
Current valid membership / scope
Organization
Resource ownership / relationship
Permission
Current policy/version when applicable
```

`OrganizationId`, `ChildId`, `Role`, `ClassId` یا هر شناسه‌ای که Client می‌فرستد به‌تنهایی مجوز محسوب نمی‌شود.

### داده رسمی

Frontend cache، SignalR، Redis، Push و LLM مرجع حقیقت داده تراکنشی نیستند.

SQL Server مرجع داده رسمی تراکنشی خواهد بود.

---

## 7) Frontend فعلی را چگونه برخورد کن

Frontend موجود را **اول inspect کن**.

قبل از هر بازنویسی:

```text
- repo structure
- README / AGENTS.md
- package.json / pnpm workspace
- apps/web
- apps/mobile
- packages/core
- apps/sandbox
- tests
- OpenAPI contract
```

را بررسی کن.

اگر صفحه یا flow در Frontend از قبل ساخته شده است:

- ابتدا آن را به شناسه UX مربوط map کن.
- Componentهای موجود و Design System را reuse کن.
- صفحه مشابه جدید نساز مگر دلیل فنی ثبت‌شده داشته باشد.

Sandbox API را Production Backend فرض نکن.

---

## 8) اولین کار Codex روی Repository

در اولین پاس **هیچ قابلیت بزرگ جدیدی پیاده‌سازی نکن**.

ابتدا:

1. دستورهای Repository را بخوان.
2. ساختار واقعی را گزارش کن.
3. build موجود را اجرا کن.
4. testهای موجود را اجرا کن.
5. OpenAPI فعلی را بخوان.
6. routeهای Web/Mobile را با شناسه‌های UX map کن.
7. mock/static/sandbox و production-ready code را از هم تفکیک کن.
8. اختلاف‌های کد با اسناد را گزارش کن.
9. هیچ فایل موجود را حذف نکن.
10. هیچ framework/package major version را خودکار ارتقا نده.

خروجی مورد انتظار:

```text
Current repository map
Build result
Test result
Implemented UX screen IDs
Partially implemented UX screen IDs
Missing UX screen IDs
Current sandbox/OpenAPI capabilities
Architecture mismatches
Backend integration gaps
Blocking decisions
Recommended first backend vertical slice
```

---

## 9) شروع Backend بعد از Baseline

پس از تأیید baseline، Backend باید از Foundation شروع شود:

```text
SQL Server
EF Core SQL Server Provider
Identity
Person/User
Organization/Membership
Trusted AccessContext
Session/Auth
Audit
Idempotency
Outbox
```

بعد از آن یک Vertical Slice واقعی ساخته شود، نه اینکه همه Entityها یک‌جا تولید شوند.

اولین slice پیشنهادی:

```text
Request OTP
Verify OTP
Create/resolve User
Complete Profile
Resolve Workspaces
Accept Invitation
Select valid Workspace
Authenticated bootstrap
```

و سپس:

```text
Organization -> Class -> Enrollment -> Teacher Assignment
```

---

## 10) ممنوعیت‌ها برای Codex

بدون تأیید مالک پروژه:

- Solution/Repo را از صفر بازسازی نکن.
- Frontend موجود را جایگزین نکن.
- Database را MySQL/PostgreSQL نکن.
- TargetFramework را تغییر نده.
- Microservices اضافه نکن.
- CQRS/MediatR/Generic Repository را صرفاً برای نمایش معماری اضافه نکن.
- Secret/ConnectionString واقعی commit نکن.
- Migration مخرب اجرا نکن.
- Production data حذف نکن.
- Commit/Push/Publish/Deploy نکن.
- sandbox/mock را اتصال Production گزارش نکن.

---

## 11) تعریف Done

یک Feature فقط وقتی Done است که متناسب با آن Feature این موارد بررسی شده باشند:

```text
UI state
API contract
positive authorization
negative authorization
persistence
validation
concurrency/idempotency where relevant
error handling
audit where sensitive
tests
build
```

موفقیت build/test فقط در صورت اجرای واقعی فرمان گزارش شود.
