# راهنمای استفاده از بک‌اند و API ماکان

این راهنما برای توسعه‌دهنده‌ای است که می‌خواهد بک‌اند ماکان را اجرا کند، از APIها در Postman یا یک کلاینت استفاده کند و جریان‌های آموزشی و پیام‌رسانی را بشناسد. مبنای سند، کد موجود در تاریخ ۲۰۲۶/۱۰/۰۷ و قابلیت‌های پیاده‌سازی‌شده تا تصحیح و انتشار نمره آزمون است.

برای شروع، بخش راه‌اندازی و ورود را بخوانید؛ سپس فضای کاری مناسب را انتخاب کنید و جریان موردنیاز را دنبال کنید. [مرجع کامل Endpointها و قراردادها](backend-api-reference.md) همراه این راهنماست و مسیر، پارامتر، بدنه، نوع پاسخ و فیلدهای تمام APIهای Controllerها را نشان می‌دهد.

## فهرست مطالب

- [قابلیت‌ها و معماری](#overview)
- [راه‌اندازی محلی](#setup)
- [تنظیمات](#configuration)
- [قرارداد مشترک HTTP](#http)
- [ورود و پروفایل](#identity)
- [فضای کاری و سرپرست](#workspace)
- [آموزش و حضور و غیاب](#academic)
- [تکلیف و ارزیابی](#assignments)
- [آزمون و انتشار نمره](#exams)
- [فایل‌ها](#storage)
- [پیام‌رسانی و همگام‌سازی](#messaging)
- [خطاها و تلاش مجدد](#errors)
- [تست و عیب‌یابی](#testing)
- [محدودیت‌ها و مسیر مطالعه](#limits)

<a id="overview"></a>
## قابلیت‌ها و معماری

| بخش | قابلیت موجود |
|---|---|
| Identity | درخواست و تأیید OTP، نشست Bearer، خروج، مشاهده و تکمیل پروفایل |
| Organization | فهرست و انتخاب فضای کاری، مشاهده و پذیرش یا رد دعوت |
| Guardian | فهرست فرزندان مجاز، انتخاب فرزند و مشاهده رابطه |
| Academic | دوره، درس، کلاس، ثبت‌نام، انتساب معلم، برنامه تکرارشونده، جلسه و حضور و غیاب |
| Assignment | پیش‌نویس و انتشار تکلیف، پاسخ متنی و فایل، تحویل نهایی، ارزیابی و انتشار یا اصلاح نمره |
| Exam | نسخه‌بندی آزمون، سؤال تستی و تشریحی، شروع تلاش، ذخیره پاسخ، انتقال حق نوشتن، نهایی‌سازی، تصحیح و انتشار نمره |
| Storage | بارگذاری، metadata، دانلود مجاز و حذف فایل |
| Messaging | گفت‌وگوی مستقیم، گروه، کانال، مدیریت اعضا، پیام و رسانه، جست‌وجو، Block، Report، Delta و SignalR |

پروژه یک Modular Monolith با Onion Architecture است. همه قابلیت‌ها در یک میزبان API اجرا می‌شوند و SQL Server مرجع داده‌های تراکنشی است.

| پروژه | مسئولیت |
|---|---|
| `MakanApp.Domain` | مدل و قانون دامنه، بدون EF Core یا HTTP |
| `MakanApp.Application` | سرویس کاربردی، قرارداد ورودی و خروجی، اعتبارسنجی و مجوز منبع |
| `MakanApp.Infrastructure` | پیاده‌سازی portها، EF Core 9، SQL Server، فایل و adapterهای فنی |
| `MakanApp.Api` | Controller، احراز هویت، ProblemDetails، SignalR و ترکیب DI |
| `tests` | تست‌های Unit، Integration و Architecture |

```text
HTTP -> Controller -> Application Service -> Domain rules / Application port
                                             -> Infrastructure -> SQL Server
```

وابستگی کامپایل از بیرون به داخل است: `Api -> Application`، `Api -> Infrastructure`، `Infrastructure -> Application/Domain` و `Application -> Domain`. مسیر اجرای درخواست با جهت وابستگی یکسان نیست؛ Application از interface استفاده می‌کند و DI پیاده‌سازی Infrastructure را به آن می‌دهد.

<a id="setup"></a>
## راه‌اندازی محلی

### پیش‌نیاز و بررسی اولیه

- .NET SDK سازگار با هدف `net9.0`؛ baseline پروژه .NET 9 است.
- SQL Server محلی یا یک پایگاه توسعه مجاز؛ تست‌های Integration مشخصاً به Windows و SQL Server LocalDB نیاز دارند.
- دسترسی به NuGet برای restore اولیه.
- برای کار با migration، ابزار محلی `dotnet-ef` نسخه `9.0.5` در `.config/dotnet-tools.json` تعریف شده است.

از ریشه مخزن اجرا کنید:

```powershell
dotnet --list-sdks
dotnet restore MakanApp.sln
dotnet tool restore
dotnet build MakanApp.sln --configuration Debug --nologo
```

### انتخاب دیتابیس توسعه

کلید اتصال `ConnectionStrings:MakanDatabase` است. نمونه زیر فقط از Windows Authentication و یک نام مشخص برای دیتابیس توسعه استفاده می‌کند:

```powershell
$env:ConnectionStrings__MakanDatabase = 'Server=.;Database=MakanApp_Development;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True'
```

این متغیر فقط برای PowerShell فعلی و فرایندهای فرزند آن تنظیم می‌شود. برای ذخیره تنظیم runtime در توسعه می‌توان از User Secrets استفاده کرد:

```powershell
dotnet user-secrets set 'ConnectionStrings:MakanDatabase' $env:ConnectionStrings__MakanDatabase --project MakanApp.Api/MakanApp.Api.csproj
```

`MakanDbContextFactory` هنگام اجرای ابزار EF، متغیر محیطی بالا را می‌خواند و User Secrets پروژه API را نمی‌خواند. در نبود متغیر، به `Server=.;Database=MakanApp` برمی‌گردد. برای اینکه migration روی مقصد دیگری اعمال نشود، متغیر اتصال را در همان پنجره‌ای که فرمان EF اجرا می‌شود صریح تنظیم کنید.

### اعمال migration و اجرا

پس از انتخاب دیتابیس توسعه مجاز، migrationها را با فرمان زیر اعمال کنید. این فرمان schema مقصد را تغییر می‌دهد؛ مقصد آن باید همان پایگاه توسعه انتخاب‌شده باشد.

```powershell
dotnet tool run dotnet-ef database update --project MakanApp.Infrastructure/MakanApp.Infrastructure.csproj --startup-project MakanApp.Infrastructure/MakanApp.Infrastructure.csproj
dotnet dev-certs https --trust
dotnet run --project MakanApp.Api/MakanApp.Api.csproj --configuration Debug --launch-profile https
```

برنامه migration را خودکار در startup اجرا نمی‌کند. آخرین migration موجود هنگام نگارش، `20261007075650_AddExamGradingAndRelease` است. اعمال migration به معنی ایجاد سازمان، دعوت، دانش‌آموز یا حساب مدیر نیست؛ seed عمومی و Endpoint راه‌اندازی سازمان در پروژه وجود ندارد.

| نشانی محلی | کاربرد |
|---|---|
| `https://localhost:7063/health` | زنده‌بودن میزبان؛ بررسی اتصال SQL Server در آن ثبت نشده است |
| `https://localhost:7063/swagger` | Swagger UI در محیط Development |
| `https://localhost:7063/openapi/v1.json` | سند OpenAPI در محیط Development |
| `https://localhost:7063/api/v1` | پیشوند APIهای کاربردی |
| `https://localhost:7063/hubs/messaging` | SignalR پیام‌رسانی |

پروفایل HTTPS، HTTP را هم روی `http://localhost:5077` باز می‌کند. خارج از محیط `Testing`، HTTPS redirection فعال است؛ برای نمونه‌های این سند از HTTPS استفاده کنید.

<a id="configuration"></a>
## تنظیمات

مقادیر زیر از `appsettings.json` و `DependencyInjection.cs` آمده‌اند. برای متغیر محیطی، `:` را با `__` جایگزین کنید؛ مثلاً `Storage__MaxFileSizeBytes`.

| کلید | پیش‌فرض یا رفتار |
|---|---|
| `ConnectionStrings:MakanDatabase` | در تنظیم پایه خالی است؛ Development نمونه اتصال محلی دارد |
| `Identity:SecurityKey` | در Development/Testing اگر تنظیم نشود، در هر شروع تصادفی است؛ در محیط دیگر الزامی و حداقل ۳۲ بایت UTF-8 |
| `Identity:Otp:LifetimeSeconds` | `300`، اعتبار پنج‌دقیقه‌ای OTP |
| `Identity:Otp:ResendDelaySeconds` | `30`، فاصله ارسال مجدد؛ در Testing صفر |
| `Identity:Otp:RateLimitWindowSeconds` | `3600` |
| `Identity:Otp:MaxRequestsPerWindow` | `5` درخواست برای شماره در پنجره تعیین‌شده در تنظیم پایه |
| `Identity:Otp:MaxFailedAttempts` | `5` تلاش ناموفق |
| `Identity:Session:LifetimeSeconds` | `2592000`، برابر ۳۰ روز |
| `Storage:RootPath` | در Development برابر `%LOCALAPPDATA%\MakanApp\Storage` |
| `Storage:MaxFileSizeBytes` | `10485760`، برابر ۱۰ MiB |
| `Storage:UnattachedLifetimeHours` | `24` برای فایل هنوز متصل‌نشده |
| `Storage:AllowedContentTypes` | `application/pdf`، `image/jpeg`، `image/png`، `text/plain` |
| `Messaging:MaximumTextLength` | `4000`؛ بیشتر از سقف مدل قابل تنظیم نیست |
| `Messaging:DefaultHistoryLimit` / `MaximumHistoryLimit` | `50` / `100` |
| `Messaging:MaximumAttachmentsPerMessage` | `10` |
| `Messaging:MaximumMentionsPerMessage` | `50` |
| `Messaging:DefaultChangeLimit` / `MaximumChangeLimit` | `100` / `200` |
| `Messaging:MaximumSearchQueryLength` | `200` |
| `Messaging:DefaultSearchLimit` / `MaximumSearchLimit` | `20` / `50` |

مقادیر انتهای جدول که در JSON نوشته نشده‌اند، fallback کد DI هستند. محدودیت IP درخواست OTP هم در `Program.cs` برابر ۱۰ درخواست در دقیقه است؛ این محدودیت درون‌حافظه‌ای و برای یک instance است.

کلید امنیت را در مخزن یا سند ذخیره نکنید. اگر در توسعه کلید ثابتی از Secret Store تنظیم نشده باشد، توکن نشست قبلی بعد از restart دیگر معتبر نیست. خارج از Development/Testing، adapterهای فعلی پیامک و فایل به‌ترتیب `UnavailableSmsSender` و `UnavailableFileStorage` هستند؛ صرف تنظیم کلید و connection string آن‌ها را به سرویس واقعی تبدیل نمی‌کند.

<a id="http"></a>
## قرارداد مشترک HTTP

به‌جز درخواست و تأیید OTP، تمام Endpointهای Controllerها نیازمند نشست معتبر هستند:

```http
Authorization: Bearer <accessToken>
Content-Type: application/json
Accept: application/json
```

توکن `accessToken` یک مقدار تصادفی opaque است و JWT نیست. از decode کردن آن برای استخراج نقش استفاده نکنید؛ context معتبر را از `/workspaces/current` بگیرید. Endpoint مستقل refresh token وجود ندارد؛ ورود مجدد از OTP انجام می‌شود.

- نام فیلدهای JSON به شکل `camelCase` است؛ Enumها را به شکل رشته‌های تعریف‌شده مثل `Student` یا `Text` ارسال کنید.
- شناسه‌ها `Guid` هستند. مقدارهای `<...>` در نمونه‌های JSON جایگزین‌شونده‌اند و باید از پاسخ واقعی پر شوند.
- زمان‌های لحظه‌ای را به UTC و با پسوند `Z`، تاریخ‌ها را `yyyy-MM-dd` و ساعت محلی برنامه را `HH:mm:ss` ارسال کنید.
- پاسخ عادی envelope عمومی مثل `data` ندارد؛ همان شیء یا آرایه قرارداد برمی‌گردد. دانلود فایل پاسخ باینری است.
- روش `PATCH` در این پروژه JSON Patch نیست؛ بدنه کامل Command مربوط را ارسال کنید.
- نسخه‌هایی مانند `rowVersion` رشته Base64 هستند. مقدار را همان‌طور که دریافت کرده‌اید در فیلد `expected...` بعدی بفرستید. این APIها قرارداد عمومی `If-Match` ندارند.
- برای نسخه یا cursor داخل Query از URL encoding استفاده کنید؛ مخصوصاً `+` و `/` در Base64 نباید خراب شوند.
- مجوز به انتخاب workspace، عضویت، نقش و رابطه با منبع وابسته است. ارسال `OrganizationId` یا `ClassId` مجوز ایجاد نمی‌کند.

### استفاده در PowerShell و Postman

برای درخواست‌های JSON در PowerShell، این helper محلی را می‌توانید در همان نشست تعریف کنید. ارسال بایت UTF-8 متن فارسی را حفظ می‌کند:

```powershell
$apiBase = 'https://localhost:7063/api/v1'

function Invoke-MakanApi {
    param(
        [string]$Method,
        [string]$Path,
        [object]$Body,
        [string]$Token
    )

    $requestOptions = @{
        Method = $Method
        Uri = "$apiBase$Path"
        Headers = @{ Accept = 'application/json' }
    }
    if ($Token) {
        $requestOptions.Headers.Authorization = "Bearer $Token"
    }
    if ($null -ne $Body) {
        $jsonBody = $Body | ConvertTo-Json -Depth 12 -Compress
        $requestOptions.ContentType = 'application/json; charset=utf-8'
        $requestOptions.Body = [System.Text.Encoding]::UTF8.GetBytes($jsonBody)
    }
    Invoke-RestMethod @requestOptions
}
```

در Postman، سند `/openapi/v1.json` را Import کنید و Authorization مجموعه را روی `Bearer Token` با مقدار توکن جاری بگذارید. در تنظیم فعلی OpenAPI، security scheme اختصاصی Bearer اضافه نشده است؛ وجود Swagger UI را به معنی آماده‌بودن دکمه Authorize برای نشست‌ها در نظر نگیرید. توکن‌های واقعی را در collection یا environment اشتراکی صادر نکنید.

<a id="identity"></a>
## ورود و پروفایل

### دریافت OTP و نشست

```powershell
$phoneNumber = Read-Host 'شماره موبایل حساب آزمایشی'
$challenge = Invoke-MakanApi -Method POST -Path '/auth/otp/challenges' -Body @{
    phoneNumber = $phoneNumber
}
```

پاسخ `202` شامل `challengeId`، `expiresAtUtc` و `resendAvailableAtUtc` است؛ کد در پاسخ نیست.

**محدودیت ورود دستی در توسعه:** `DevelopmentSmsSender` پیامک نمی‌فرستد و کد را فقط در `DevelopmentOtpStore` همان فرایند نگه می‌دارد. API عمومی برای دریافت کد و OTP ثابت وجود ندارد. برای آزمایش دستی، برنامه را با debugger محلی اجرا کنید و در `DevelopmentSmsSender.SendOtpAsync` مقدار پارامتر `code` همان challenge را ببینید؛ آن را در log، فایل یا Git ثبت نکنید. تست خودکار از `MakanAppWebApplicationFactory.GetOtpCode` استفاده می‌کند. در محیط دارای adapter واقعی، کد از پیامک دریافت می‌شود.

```powershell
$otpCode = Read-Host 'کد دریافت‌شده برای همین challenge'
$login = Invoke-MakanApi -Method POST -Path '/auth/otp/verify' -Body @{
    challengeId = $challenge.challengeId
    phoneNumber = $phoneNumber
    code = $otpCode
}
$accessToken = $login.accessToken
$otpCode = $null
$me = Invoke-MakanApi -Method GET -Path '/me' -Token $accessToken
```

پاسخ تأیید `200` شامل `accessToken`، `tokenType`، `expiresAtUtc` و `user` است. OTP مصرف‌شده قابل استفاده مجدد نیست و تکرار تأیید می‌تواند `OTP_ALREADY_USED` بدهد. اگر پاسخ ورود در شبکه گم شد و توکن را ندارید، مسیر ورود جدید را با رعایت محدودیت ارسال طی کنید.

### تکمیل پروفایل و خروج

```powershell
$me = Invoke-MakanApi -Method PATCH -Path '/me/profile' -Token $accessToken -Body @{
    firstName = 'کاربر'
    lastName = 'آزمایشی'
    displayName = 'کاربر آزمایشی'
    username = 'demo.user'
}
```

نام و نام خانوادگی اجباری و هرکدام حداکثر ۱۰۰ کاراکترند. `displayName` اختیاری و حداکثر ۲۰۰ کاراکتر است. `username` باید ۳ تا ۳۲ کاراکتر و شامل حروف لاتین، رقم، نقطه یا زیرخط باشد؛ یکتایی آن نسبت به بزرگی و کوچکی حروف حساس نیست. مقدار نمونه را برای هر حساب تغییر دهید.

`POST /auth/logout` بدون بدنه، نشست جاری را revoke می‌کند و `204` می‌دهد. پس از موفقیت، توکن محلی را پاک کنید.

<a id="workspace"></a>
## فضای کاری و سرپرست

### شناسه‌ها را با هم اشتباه نگیرید

| شناسه | مفهوم و محل مصرف |
|---|---|
| `UserId` | حساب ورود و طرف گفت‌وگو |
| `PersonId` | شخص در مدل Identity؛ برابر UserId فرض نشود |
| `OrganizationPersonId` | رکورد شخص در سازمان؛ ثبت‌نام و انتخاب فرزند از این شناسه استفاده می‌کنند |
| `MembershipId` | عضویت کاربر در سازمان؛ انتخاب workspace و انتساب معلم |
| `EnrollmentId` | ثبت‌نام شخص در یک کلاس؛ حضور و غیاب |
| `SessionId` | در Auth نشست ورود، اما در `/academic/sessions` شناسه جلسه آموزشی است |
| `ExamAttemptId` | تلاش دانش‌آموز در آزمون |
| `AttemptQuestionId` | سؤال منجمدشده در یک تلاش؛ برای ذخیره پاسخ، با شناسه سؤال editor جایگزین نشود |

### انتخاب فضای شخصی یا سازمانی

```powershell
$workspaces = Invoke-MakanApi -Method GET -Path '/workspaces/me' -Token $accessToken
$personalContext = Invoke-MakanApi -Method POST -Path '/workspaces/select' -Token $accessToken -Body @{
    workspaceType = 'Personal'
    membershipId = $null
    role = $null
}
```

برای فضای سازمانی، `membershipId` و `role` را از همان عضو فهرست `/workspaces/me` بگیرید:

```json
{
  "workspaceType": "Organization",
  "membershipId": "<membershipId از فهرست فضاها>",
  "role": "Teacher"
}
```

نتیجه `AccessContext` شامل `actorId`، `userId`، `sessionId`، `workspaceType`، `organizationId`، `membershipId`، `activeRole` و `subjectOrganizationPersonId` است. انتخاب در نشست ذخیره می‌شود؛ تغییر آن در یک tab روی درخواست‌های tab دیگری که همان توکن را دارد هم اثر دارد. بعد از تغییر فضا، داده‌های context قبلی را دوباره از API مربوط دریافت کنید.

کاربر بدون عضویت، فضای شخصی دارد. ساخت حساب و تکمیل پروفایل به‌خودی‌خود نقش `Manager`، عضویت سازمانی، رابطه سرپرست یا مجوز تماس شخصی ایجاد نمی‌کند.

### دعوت و فرزند

1. با `GET /invitations` دعوت‌های حساب جاری را بگیرید.
2. `POST /invitations/{invitationId}/accept` یا `/decline` را بدون بدنه بفرستید.
3. پس از پذیرش، فهرست workspaceها را تازه کنید و عضویت/نقش موردنظر را انتخاب کنید. پذیرش تکراری همان دعوت می‌تواند با `alreadyAccepted=true` نتیجه قبلی را بدهد.
4. برای والد، ابتدا فضای سازمانی با نقش دقیق `Parent` را انتخاب کنید؛ مقدار `Guardian` نام نقش API نیست.
5. `GET /guardian/children` را بخوانید و `learnerOrganizationPersonId` مجاز را به `POST /guardian/children/{organizationPersonId}/select` بدهید.
6. `GET /workspaces/current` اکنون context فرزند را نشان می‌دهد. رابطه فعال در درخواست‌های بعدی دوباره بررسی می‌شود.

ایجاد سازمان، عضویت اولیه، دعوت، شخص سازمانی و رابطه سرپرست API عمومی ندارند. برای اجرای سناریو سازمانی به داده از قبل آماده‌شده در محیط مجاز نیاز دارید. fixtureهای Integration الگوی داده آزمایشی‌اند و ابزار seed دیتابیس عادی نیستند.

<a id="academic"></a>
## آموزش و حضور و غیاب

### مجوزهای اصلی

| عملیات | بافت مجاز |
|---|---|
| ساخت دوره، درس، کلاس، ثبت‌نام و انتساب معلم | `Manager` سازمان انتخاب‌شده |
| فهرست کلاس‌ها | Manager سازمان، Teacher منتسب، Student ثبت‌نام‌شده؛ Parent در این Endpoint پشتیبانی نمی‌شود |
| مدیریت جلسه و برنامه، ثبت و اصلاح حضور و غیاب | Manager یا Teacher دارای انتساب فعال به کلاس |
| برنامه و جزئیات جلسه | نقش مجاز مرتبط با کلاس؛ Parent با فرزند انتخاب‌شده و رابطه معتبر نیز پشتیبانی می‌شود |
| مشاهده دفتر حضور و غیاب کلاس | Manager یا Teacher منتسب |
| مدیریت تکلیف و آزمون، ارزیابی و انتشار | Manager یا Teacher منتسب به کلاس |
| تحویل تکلیف و پاسخ آزمون | Student واجد شرایط و مالک تلاش |
| نتیجه والد | Parent و context فرزند مجاز؛ DTO مخصوص والد |

همه نقش‌های جدول سازمانی‌اند و فقط در همان سازمان و روابط معتبر معنی دارند.

### ساخت کلاس

با توکن مدیری که workspace سازمانی را انتخاب کرده است:

```powershell
$period = Invoke-MakanApi -Method POST -Path '/academic/periods' -Token $accessToken -Body @{
    title = 'دوره آزمایشی'
    startDate = '2026-10-01'
    endDate = '2027-02-01'
}
$course = Invoke-MakanApi -Method POST -Path '/academic/courses' -Token $accessToken -Body @{
    title = 'ریاضی'
}
$class = Invoke-MakanApi -Method POST -Path '/academic/classes' -Token $accessToken -Body @{
    academicPeriodId = $period.id
    courseId = $course.id
    title = 'ریاضی مقدماتی'
    capacity = 20
    activateImmediately = $true
}
```

تاریخ‌های نمونه را متناسب با دوره خود انتخاب کنید. شناسه دوره و درس باید از همین سازمان باشند. ظرفیت مثبت است؛ رقابت برای آخرین ظرفیت در transaction SQL Server کنترل می‌شود. برای مسیر معمول استفاده، `activateImmediately=true` بفرستید؛ Endpoint عمومی مستقلی برای فعال‌کردن کلاس Draft وجود ندارد.

| عملیات | بدنه |
|---|---|
| `POST /academic/classes/{classId}/enrollments` | `{"learnerOrganizationPersonId":"<شناسه شخص سازمانی>"}` |
| `POST /academic/classes/{classId}/teachers` | `{"teacherMembershipId":"<عضویت فعال معلم>"}` |
| `POST /academic/classes/{classId}/enrollments/{enrollmentId}/end` | `{"finalStatus":"Completed"}` یا `Withdrawn` |
| `POST /academic/classes/{classId}/teachers/{teacherAssignmentId}/end` | بدون بدنه |

ثبت‌نام یا انتساب فعال تکراری خطا می‌دهد؛ این دو عملیات قرارداد عمومی `clientOperationId` ندارند. پایان ثبت‌نام/انتساب، تاریخچه را حذف نمی‌کند.

### جلسه و برنامه تکرارشونده

برای `POST /academic/classes/{classId}/sessions`:

```json
{
  "title": "جلسه اول",
  "startUtc": "2026-11-01T06:30:00Z",
  "endUtc": "2026-11-01T08:00:00Z",
  "timeZoneId": "Asia/Tehran",
  "meetingUrl": null
}
```

زمان جلسه جدید باید آینده باشد؛ هنگام اجرای مثال، تاریخ را متناسب با ساعت سرور تغییر دهید. پایان بعد از شروع است. تداخل زمانی کلاس و معلم بررسی می‌شود. پاسخ `SessionResult` دارای `rowVersion` است؛ برای ویرایش، علاوه بر فیلدهای بالا `expectedRowVersion` لازم است. لغو یا تکمیل فقط بدنه `{"expectedRowVersion":"<rowVersion>"}` می‌خواهند.

برای `POST /academic/classes/{classId}/schedule-rules`:

```json
{
  "localDayOfWeek": "Sunday",
  "localStartTime": "10:00:00",
  "durationMinutes": 90,
  "timeZoneId": "Asia/Tehran",
  "effectiveFrom": "2026-11-01",
  "effectiveUntil": "2026-11-30",
  "sessionTitle": "جلسه هفتگی",
  "meetingUrl": null
}
```

قانون برنامه، روز و ساعت محلی را همراه timezone نگه می‌دارد و جلسه‌های دارای زمان UTC تولید می‌کند. برای ویرایش قانون، `applyFromDate` و `expectedRowVersion` نیز لازم‌اند. برنامه بازه را با `GET /academic/schedule?fromUtc=...&toUtc=...` بخوانید؛ هر دو زمان UTC، پایان بزرگ‌تر از شروع و بازه حداکثر یک سال است.

### ثبت و اصلاح حضور و غیاب

ابتدا `GET /academic/sessions/{sessionId}/attendance` را بگیرید. برای `PUT` همان مسیر:

```json
{
  "entries": [
    { "enrollmentId": "<enrollmentId از roster>", "status": "Present" }
  ]
}
```

مقادیر ثبت‌کردنی `Present`، `Absent`، `Late` و `Excused` هستند. `NotRecorded` وضعیت نمایش ثبت‌نشدن است. ثبت پیش از شروع جلسه و برای جلسه لغوشده مجاز نیست. برای اصلاح رکورد موجود از `PATCH /academic/sessions/{sessionId}/attendance/{attendanceId}` با `status`، `correctionReason` و `expectedRowVersion` استفاده کنید؛ اصلاح تاریخچه جدا دارد.

<a id="assignments"></a>
## تکلیف و ارزیابی

```text
ساخت Draft -> انتشار و تعیین گیرندگان -> Draft پاسخ دانش‌آموز
-> ذخیره متن / اتصال فایل -> Submit -> Draft ارزیابی -> Release -> نتیجه
```

### ساخت و انتشار

با توکن Manager یا Teacher منتسب:

```powershell
$assignment = Invoke-MakanApi -Method POST -Path "/academic/classes/$($class.id)/assignments" -Token $accessToken -Body @{
    title = 'تمرین اول'
    description = 'پاسخ تمرین را ارسال کنید.'
    dueAtUtc = [DateTime]::UtcNow.AddDays(7).ToString('o')
    allowLateSubmission = $false
    maxAttempts = 2
    maxScore = 20
}
$published = Invoke-MakanApi -Method POST -Path "/academic/assignments/$($assignment.id)/publish" -Token $accessToken -Body @{
    expectedAssignmentRowVersion = $assignment.assignmentRowVersion
    expectedVersionRowVersion = $assignment.versionRowVersion
}
```

پاسخ انتشار شامل `assignment` و `recipientCount` است. `MaxScore` متعلق به نسخه تکلیف است؛ نمره ارزیابی از این سقف تبعیت می‌کند. پیش‌نویس از مسیر `PATCH /academic/assignments/{assignmentId}` قابل ویرایش است و به هر دو نسخه نیاز دارد. انتشار گیرندگان را از ثبت‌نام‌های واجد شرایط همان زمان تعیین می‌کند؛ صرف عضویت Student برای پاسخ کافی نیست.

فهرست را با `GET /academic/classes/{classId}/assignments` یا `GET /academic/assignments?classId=...` بخوانید. نقش و رابطه با کلاس بر داده قابل مشاهده اثر دارند.

### پاسخ و تحویل دانش‌آموز

`$studentToken` در مثال باید توکن نشست Student با workspace سازمانی انتخاب‌شده باشد:

```powershell
$attempt = Invoke-MakanApi -Method POST -Path "/academic/assignments/$($assignment.id)/attempts" -Token $studentToken
$attempt = Invoke-MakanApi -Method PATCH -Path "/academic/submission-attempts/$($attempt.id)/draft" -Token $studentToken -Body @{
    answerText = 'پاسخ دانش‌آموز'
    expectedRowVersion = $attempt.rowVersion
}
$receipt = Invoke-MakanApi -Method POST -Path "/academic/submission-attempts/$($attempt.id)/submit" -Token $studentToken -Body @{
    expectedRowVersion = $attempt.rowVersion
}
```

برای پیوست، ابتدا فایل را با همان حساب و scope مناسب آپلود کنید؛ سپس به `/academic/submission-attempts/{attemptId}/attachments` بدنه `fileAssetId` و `expectedRowVersion` بدهید. پس از هر ذخیره یا اتصال/حذف فایل، از `rowVersion` پاسخ جدید استفاده کنید. حذف پیوست، `expectedRowVersion` را در Query می‌گیرد.

ایجاد Draft، تلاش باز موجود را resume می‌کند. ذخیره Draft تحویل رسمی نیست؛ رسید `SubmissionReceipt` موفقیت Submit را با `submittedAtUtc`، `isLate` و وضعیت `Submitted` نشان می‌دهد. سرور مهلت، سیاست تأخیر، تعداد تلاش و آمادگی فایل را کنترل می‌کند. فیلد `contentVisible` را در پاسخ رعایت کنید؛ وجود metadata تلاش به معنی مجازبودن نمایش محتوای آن نیست.

### ارزیابی و انتشار نمره

1. ارزیاب مجاز از `/academic/evaluations/queue`، با فیلترهای اختیاری `assignmentId`، `classId`، `reviewStatus` و `isLate`، صف را می‌خواند.
2. محتوای پاسخ نهایی از `/academic/submission-attempts/{attemptId}/evaluation/submission` خوانده می‌شود.
3. `PUT /academic/submission-attempts/{attemptId}/evaluation` یک Draft می‌سازد یا ویرایش می‌کند:

```json
{
  "score": 18,
  "learnerFeedback": "پاسخ خوب است؛ محاسبه آخر را بررسی کن.",
  "guardianVisibleFeedback": "پیشرفت مناسبی داشته است.",
  "teacherPrivateNote": "یادداشت داخلی ارزیاب",
  "expectedRowVersion": null
}
```

در ایجاد اولیه، `expectedRowVersion=null` است؛ در ویرایش باید نسخه فعلی را بفرستید. نمره در بازه صفر تا `maxScore` نسخه تکلیف است. برای انتشار، به `/evaluation/release` مقدار `expectedRowVersion` پاسخ ارزیابی را بدهید.

دانش‌آموز `/academic/submission-attempts/{attemptId}/result` و والد `/guardian-result` را می‌خوانند. قبل از انتشار، نتیجه تکلیف می‌تواند `404 GRADE_NOT_RELEASED` بدهد. `teacherPrivateNote` به هیچ‌کدام داده نمی‌شود. اصلاح نتیجه منتشرشده از `/evaluation/corrections` با `correctionReason` و `expectedReleasedEvaluationRowVersion` یک Draft تازه می‌سازد؛ انتشار آن نیز جداگانه است.

<a id="exams"></a>
## آزمون و انتشار نمره

### طراحی و نسخه‌بندی آزمون

با Manager یا Teacher مجاز، `POST /academic/classes/{classId}/exams`:

```json
{
  "title": "آزمون ریاضی",
  "description": "آزمون کوتاه فصل اول",
  "availableFromUtc": "2026-11-01T06:30:00Z",
  "availableUntilUtc": "2026-11-01T08:30:00Z",
  "durationMinutes": 30,
  "maxAttempts": 1,
  "maxScore": 20,
  "randomizationPolicy": "QuestionOrder"
}
```

بازه نمونه را برای زمان اجرای خود تنظیم کنید. سیاست ترتیب سؤال `None` یا `QuestionOrder` است. پاسخ `ExamEditorDto` شامل `examRowVersion`، `versionRowVersion` و سؤال‌هاست. نمونه افزودن یک سؤال ۲۰ نمره‌ای به `POST /academic/exams/{examId}/questions`:

```json
{
  "order": 1,
  "type": "ObjectiveSingleChoice",
  "prompt": "حاصل ۲ + ۲ کدام است؟",
  "score": 20,
  "options": [
    { "order": 1, "text": "۴", "isCorrect": true },
    { "order": 2, "text": "۵", "isCorrect": false }
  ],
  "expectedVersionRowVersion": "<versionRowVersion فعلی>"
}
```

سؤال تستی حداقل دو گزینه و دقیقاً یک گزینه درست می‌خواهد؛ ترتیب و متن گزینه‌ها نباید تکراری باشد. سؤال `Descriptive` گزینه ندارد. هنگام انتشار، آزمون باید حداقل یک سؤال داشته باشد و مجموع نمره سؤال‌ها دقیقاً برابر `maxScore` شود.

به `POST /academic/exams/{examId}/publish` دو فیلد `expectedExamRowVersion` و `expectedVersionRowVersion` را از آخرین پاسخ بدهید. نسخه منتشرشده درجا ویرایش نمی‌شود؛ `/versions` با `expectedExamRowVersion` و `expectedPublishedVersionRowVersion` نسخه بعدی می‌سازد. حذف سؤال، برخلاف بسیاری از DELETEها، بدنه JSON با نسخه سؤال و نسخه آزمون دارد.

`GET /academic/exams` فهرست را بر اساس نقش/کلاس یا فرزند مجاز می‌دهد. `/editor` و `/teacher-preview` مخصوص ارزیاب مجازند و اطلاعات پاسخ صحیح دارند. `/student-preview` برای Teacher/Manager مجاز، نمایش امن بدون پاسخ صحیح می‌سازد؛ Student فقط با ثبت‌نام فعال و در بازه فعال آزمون می‌تواند آن را بگیرد. Parent اجازه دیدن محتوای سؤال از این مسیر ندارد.

### شروع، ذخیره پاسخ و نهایی‌سازی

```text
Student workspace -> Start -> دریافت سؤال‌های منجمد و deadline سرور
-> دریافت write lease -> Save Answer -> دریافت رسید هر ذخیره
-> توقف ذخیره‌های در حال ارسال -> Finalize -> دریافت رسید نهایی
```

1. به `POST /academic/exams/{examId}/attempts/start` بدنه `{"clientOperationId":"<Guid جدید>"}` بفرستید. برای retry همان عملیات، همین Guid را نگه دارید.
2. پاسخ دارای `examAttemptId`، `examVersionId`، `effectiveDeadlineUtc`، `serverNowUtc` و سؤال‌های دارای `attemptQuestionId` است. deadline از محدودترِ پایان بازه آزمون و مدت تلاش محاسبه می‌شود. ترتیب سؤال‌ها برای همان تلاش ثابت می‌ماند.
3. `POST /academic/exam-attempts/{attemptId}/write-lease` بدون بدنه بفرستید. `writeLeaseVersion` برگشتی را نگه دارید. حق نوشتن به نشست تعلق دارد؛ انتقال صریح از `/write-lease/transfer` نسخه را عوض می‌کند و نویسنده قبلی دیگر معتبر نیست.
4. برای هر سؤال، به `PUT /academic/exam-attempts/{attemptId}/answers/{attemptQuestionId}` بدنه زیر را بدهید:

```json
{
  "clientOperationId": "<Guid مستقل این ذخیره>",
  "writeLeaseVersion": 1,
  "expectedRevisionNumber": null,
  "selectedOptionId": "<optionId همان سؤال در تلاش>",
  "textAnswer": null
}
```

`writeLeaseVersion=1` فقط نمونه است؛ مقدار واقعی پاسخ lease را استفاده کنید. در اولین پاسخ `expectedRevisionNumber=null` و در ویرایش، `revisionNumber` آخرین پاسخ پذیرفته‌شده لازم است. برای سؤال تشریحی `selectedOptionId=null` و `textAnswer` متن پاسخ است. رسید ذخیره، `revisionNumber`، `acceptedAtUtc` و `answerSetVersion` جدید را می‌دهد.

برای ادامه پس از reconnect یا تعارض، `GET /academic/exam-attempts/{attemptId}/answers` را بگیرید. این پاسخ مجموعه پاسخ پذیرفته‌شده، نسخه مجموعه و وضعیت write lease را نشان می‌دهد. ذخیره محلی کلاینت یا موفقیت HTTP ارسال‌نشده، اثبات پذیرش پاسخ در سرور نیست.

5. قبل از Finalize، تمام ذخیره‌های در حال ارسال را تمام کنید و نسخه نهایی مجموعه را بگیرید؛ سپس `POST /academic/exam-attempts/{attemptId}/finalize`:

```json
{
  "clientOperationId": "<Guid مستقل نهایی‌سازی>",
  "expectedAnswerSetVersion": 1,
  "writeLeaseVersion": 1
}
```

هر دو نسخه نمونه‌اند و باید از پاسخ‌های جاری گرفته شوند. رسید شامل `finalizedAtUtc`، `finalizedAnswerSetVersion`، `answeredQuestionCount`، `totalQuestionCount` و `status=Finalized` است. `GET /academic/exam-attempts/{attemptId}/receipt` آن را دوباره می‌دهد. رسید، نمره یا پاسخ صحیح ندارد.

پس از deadline، ذخیره یا Finalize جدید رد می‌شود و تلاش می‌تواند `Expired` شود. **auto-finalize یا scheduler پایان آزمون وجود ندارد**؛ وجود پاسخ ذخیره‌شده، تلاش Expired را به پاسخ تحویل‌شده تبدیل نمی‌کند. retry دقیق Finalize موفق با همان شناسه و payload، رسید قبلی را برمی‌گرداند؛ payload متفاوت با همان شناسه conflict است.

### تصحیح و انتشار نمره آزمون

فقط تلاش `Finalized` وارد تصحیح می‌شود؛ منبع تصحیح `ExamFinalAnswers` و revisionهای منجمدشده است.

1. `GET /academic/exams/{examId}/grading` صف تصحیح را با فیلتر اختیاری `classId` و `status` می‌دهد.
2. `POST /academic/exam-attempts/{attemptId}/grading` بدون بدنه، Draft تصحیح را ایجاد یا بازیابی می‌کند. `GET` همان مسیر فقط Draft موجود را می‌خواند.
3. سؤال تستی بر مبنای کلید نسخه منجمد تصحیح می‌شود. سؤال تشریحی به بررسی دستی نیاز دارد. مسیر `PATCH /academic/exam-attempts/{attemptId}/grading/questions/{attemptQuestionId}` این بدنه را می‌گیرد:

```json
{
  "awardedScore": 15,
  "learnerFeedback": "استدلال را کامل‌تر بنویس.",
  "evaluatorPrivateNote": "یادداشت خصوصی مصحح",
  "expectedGradeRowVersion": "<rowVersion فعلی تصحیح>"
}
```

نمره هر سؤال باید در بازه مجاز همان سؤال باشد. `expectedGradeRowVersion` نسخه کل Draft نمره است؛ پس از تغییر هر سؤال آن را از پاسخ تازه کنید.

4. به `/grading/complete` فیلدهای `learnerFeedback`، `guardianVisibleFeedback`، `evaluatorPrivateNote` و `expectedGradeRowVersion` بدهید؛ بررسی‌های لازم باید کامل شده باشند. وضعیت به `ReadyForRelease` می‌رسد.
5. به `/grade/release` بدنه `clientOperationId` و `expectedGradeRowVersion` بدهید. این مرحله انتشار رسمی است.
6. دانش‌آموز `/result` و والد `/guardian-result` را زیر `/academic/exam-attempts/{attemptId}` می‌خوانند. پیش از انتشار، برای تلاش نهایی‌شده پاسخ `200` با `releaseStatus=AwaitingGrading` یا `AwaitingRelease` و نمره `null` ممکن است؛ این با قرارداد نتیجه تکلیف متفاوت است.
7. `/grade/corrections` با `correctionReason` و `expectedReleasedGradeRowVersion` Draft اصلاحی می‌سازد؛ آن را دوباره کامل و منتشر کنید. تا انتشار اصلاحیه، نتیجه منتشرشده قبلی معتبر می‌ماند.

DTO دانش‌آموز فقط بازخورد دانش‌آموز و DTO والد فقط بازخورد والد را دارد؛ یادداشت خصوصی مصحح و کلید پاسخ در نتیجه عمومی قرار نمی‌گیرند.

<a id="storage"></a>
## فایل‌ها

بارگذاری فایل از `POST /files` با `multipart/form-data` انجام می‌شود؛ نام فیلد `File` است. در Postman بخش Body را روی form-data بگذارید، نوع `File` را برای آن فیلد انتخاب کنید و Content-Type دارای boundary را به خود ابزار بسپارید.

```powershell
$uploadPath = Read-Host 'مسیر فایل PDF آزمایشی'
curl.exe --request POST "$apiBase/files" --header "Authorization: Bearer $accessToken" --form "File=@$uploadPath;type=application/pdf"
```

پاسخ `201` شامل `id`، `status`، `contentType`، `sizeBytes`، `sha256Hash`، `unattachedExpiresAtUtc` و `rowVersion` است. `id` را برای اتصال به تکلیف یا پیام نگه دارید؛ فقط فایل آماده و مجاز قابل اتصال است.

| عملیات | مسیر و نکته |
|---|---|
| metadata | `GET /files/{fileId}` |
| دانلود | `GET /files/{fileId}/content` با Bearer؛ پاسخ stream است |
| حذف | `DELETE /files/{fileId}?expectedRowVersion=...` با نسخه URL-encoded |

فایل لینک عمومی ندارد. مالکیت، scope و binding به پیام/پاسخ در مجوز دانلود اثر دارند. حذف فایل retained یا در حال استفاده می‌تواند `FILE_IN_USE` بدهد. مهلت فایل unattached به معنی وجود پاک‌سازی پس‌زمینه زمان‌بندی‌شده نیست.

در تنظیم پیش‌فرض فقط PDF، JPEG، PNG و متن مجازند. مدل Messaging نوع `Video` و `Voice` را می‌شناسد، اما برای آپلود آن‌ها باید MIME مجاز مناسب در تنظیم محیط وجود داشته باشد؛ تنظیم پایه این نوع‌ها را نمی‌پذیرد. اعتبارسنجی MIME و اندازه، جای اسکن بدافزار نیست؛ اسکن و object storage تولیدی در این نسخه ارائه نشده‌اند.

<a id="messaging"></a>
## پیام‌رسانی و همگام‌سازی

### آغاز گفت‌وگو و مجوز ارتباط

`POST /conversations/direct`:

```json
{
  "targetUserId": "<UserId مخاطب مجاز>",
  "scope": "Organization"
}
```

Scope یکی از `Personal` یا `Organization` است. در Scope سازمانی، سازمان از workspace جاری به دست می‌آید. ایجاد گفت‌وگوی تازه `201` و بازیابی زوج موجود `200` با `alreadyExisted=true` دارد.

در فضای شخصی، شروع تماس به دو حساب با وضعیت سنی `Adult` و `PersonalCommunicationGrant` فعال نیاز دارد؛ ثبت‌نام ساده این شرایط را تأمین نمی‌کند. برای وضعیت سنی و ایجاد grant، API عمومی وجود ندارد.

در فضای سازمانی، عضویت‌ها و نقش‌ها فعال‌اند و policy فعلی این روابط را کنترل می‌کند:

| نقش آغازگر | مخاطب‌های مجاز با شرایط لازم |
|---|---|
| Manager | Manager، Teacher، Parent و Student با ثبت‌نام فعال |
| Teacher | Manager و Teacher؛ Student کلاس مرتبط؛ Parent با رابطه آموزشی معتبر |
| Student | با ثبت‌نام فعال: Manager و Teacher مرتبط |
| Parent | Manager و Teacher مرتبط با فرزند |

شناسه مخاطب ناشناخته یا غیرمجاز می‌تواند خطای عمومی `DIRECT_RECIPIENT_NOT_AVAILABLE` بدهد. API عمومی جست‌وجوی کاربران با شماره تلفن وجود ندارد.

### گروه، کانال و پیام

برای ایجاد گروه یا کانال از `/conversations/groups` یا `/conversations/channels` استفاده کنید:

```json
{
  "clientOperationId": "<Guid ایجاد گفت‌وگو>",
  "scope": "Organization",
  "title": "گروه آموزشی",
  "description": "هماهنگی کلاس",
  "initialParticipantUserIds": []
}
```

ایجاد گروه/کانال سازمانی به workspace معتبر با نقش Manager یا Teacher نیاز دارد؛ ایجاد شخصی به workspace شخصی و وضعیت سنی Adult. افزودن عضو نیز policy ارتباط با مخاطب را طی می‌کند. نقش‌های داخل گفت‌وگو `Owner`، `Admin` و `Member` از نقش‌های سازمانی جدا هستند. در Group اعضای فعال مجاز به ارسال‌اند؛ در Channel انتشار به Owner/Admin محدود است. مالک باید پیش از خروج، مالکیت را با جریان درخواست انتقال و پذیرش مقصد واگذار کند. archive با حذف تاریخچه یکی نیست.

برای ارسال `POST /conversations/{conversationId}/messages`:

```json
{
  "clientMessageId": "<Guid این پیام منطقی>",
  "kind": "Text",
  "text": "سلام، زمان جلسه تأیید شد.",
  "attachmentIds": [],
  "replyToMessageId": null,
  "mentionedUserIds": []
}
```

رسید `200` دارای `messageId`، `sequence`، `sentAtUtc`، `status=Sent` و `version` است. `Sent` یعنی commit موفق در SQL Server؛ دریافت یا خواندن توسط مخاطب را تضمین نمی‌کند. همان `clientMessageId` را برای retry همان پیام نگه دارید؛ تغییر payload با شناسه قبلی `MESSAGE_IDEMPOTENCY_CONFLICT` می‌دهد.

برای پیام رسانه‌ای `kind` مناسب و `attachmentIds` فایل‌های آماده لازم است. Reply با شناسه پیام همان گفت‌وگو، Mention با UserId عضو مجاز و Forward با `destinationConversationId` و یک `clientMessageId` جدید انجام می‌شود؛ دسترسی به هر دو طرف و محدودیت Scope کنترل می‌شود.

ویرایش پیام `text` و `expectedVersion` می‌گیرد؛ حذف پیام `expectedVersion` در Query می‌خواهد. حذف یک tombstone ایجاد می‌کند؛ در نمایش `isDeleted` و محتوای پنهان‌شده را رعایت کنید. واکنش‌های موجود `Like`، `Love`، `Laugh`، `Wow` و `Sad` هستند. مسیرهای Pin، اعضا، انتقال مالکیت و رسانه در مرجع Endpointها آمده‌اند.

### تاریخچه، Delta و رسید خواندن

| نیاز | درخواست یا پاسخ |
|---|---|
| فهرست گفت‌وگوها | `GET /conversations`؛ دارای `unreadCount` و cursorهای Read/Delivered |
| تاریخچه | `GET /conversations/{id}/messages?limit=50` |
| صفحه قدیمی‌تر | مقدار `nextBeforeSequence` صفحه قبلی را در `beforeSequence` بفرستید |
| تغییرات | `GET /conversations/{id}/changes?afterCursor=...&limit=100` |
| صفحه بعد تغییرات | `nextCursor` را حفظ کنید و تا `hasMore=false` ادامه دهید |
| اعلام دریافت | `POST /conversations/{id}/delivered` با `{"upToMessageSequence":12}` |
| اعلام خواندن | `POST /conversations/{id}/read` با همان شکل بدنه |

عدد `12` نمونه است؛ فقط آخرین Sequence واقعاً دریافت‌شده یا خوانده‌شده را ACK کنید. cursor خواندن/دریافت عقب نمی‌رود. `MessageSequence` ترتیب پیام‌ها و cursor تغییرات ترتیب mutationها را بیان می‌کنند؛ این دو قابل جایگزینی نیستند. cursorها را opaque نگه دارید و به گفت‌وگو/جست‌وجوی دیگری منتقل نکنید.

### SignalR

Hub مسیر `/hubs/messaging` دارد. متدهای قابل فراخوانی `SubscribeConversation(conversationId)` و `UnsubscribeConversation(conversationId)` هستند. event سمت کلاینت `ConversationChanged` نام دارد و اطلاعات سبک مثل `conversationId`، `type`، `resourceId`، `resourceVersion`، `cursor`، `occurredAtUtc` و `payloadVersion` را حمل می‌کند؛ متن کامل پیام نیست.

پس از اتصال یا reconnect، دسترسی و subscriptionها را برقرار کنید و تغییرات را از آخرین cursor پردازش‌شده با REST بگیرید. صرف دریافت یک اعلان یا cursor جدید، مجوز ردکردن تغییرات بین cursor قبلی و آن اعلان نیست. بعد از پردازش موفق صفحه Delta، cursor ذخیره‌شده را جلو ببرید. تحویل اعلان می‌تواند تکراری باشد؛ `changeId` را برای تشخیص تغییر تکراری نگه دارید.

**محدودیت فعلی احراز هویت Hub:** `SessionAuthenticationHandler` فقط هدر `Authorization: Bearer ...` را می‌خواند و پارامتر Query با نام `access_token` را نمی‌خواند. بنابراین نباید فرض کرد اتصال پیش‌فرض WebSocket/SSE کلاینت مرورگر با توکن Query کار می‌کند. transport یا کلاینتی لازم است که هدر موردنیاز را بفرستد؛ مسیر احراز هویت مرورگر باید پیش از اتکا به آن آزمایش شود. در زمان قطع realtime، REST و Delta همچنان مسیر بازیابی داده‌اند. در `Program.cs` نیز CORS ثبت نشده؛ اتصال cross-origin مرورگر آماده نیست و به تنظیم مجاز جداگانه نیاز دارد.

### جست‌وجو، مسدودسازی و گزارش

`GET /messages/search?q=...` از فیلترهای اختیاری `conversationId`، `kind`، `fromUtc`، `toUtc`، `cursor` و `limit` پشتیبانی می‌کند. پاسخ شامل `items`، `totalCount` و `nextCursor` است. Query و cursor را URL-encode کنید؛ نتایج بر اساس دسترسی جاری محدود می‌شوند.

- `POST /messaging/blocks/{userId}` حساب را مسدود می‌کند؛ `DELETE` همان مسیر رفع مسدودسازی است؛ `GET /messaging/blocks` فهرست خود کاربر را می‌دهد.
- `POST /conversations/{conversationId}/messages/{messageId}/reports` بدنه `clientReportId`، `reason` و `description` می‌گیرد.
- دلیل گزارش یکی از `Harassment`، `Spam`، `Threat`، `InappropriateContent`، `Impersonation` یا `Other` است.
- `GET /messaging/reports/{reportId}` رسید گزارش متعلق به خود کاربر را می‌دهد. این API پنل رسیدگی مدیر یا تضمین اقدام خودکار روی گزارش نیست.

<a id="errors"></a>
## خطاها و تلاش مجدد

خطای کاربردی به شکل ProblemDetails برمی‌گردد؛ نمونه:

```json
{
  "type": "urn:makan:problem:auth-required",
  "title": "Authentication required",
  "status": 401,
  "detail": "برای ادامه باید وارد شوید.",
  "code": "AUTH_REQUIRED",
  "traceId": "<شناسه پیگیری درخواست>"
}
```

کلاینت تصمیم ماشینی را از status و `code` بگیرد و متن `detail` را برای توضیح به کاربر استفاده کند. خطاهای model binding، JSON نامعتبر یا validation خود ASP.NET Core ممکن است `errors` داشته باشند و `code` کاربردی نداشته باشند؛ مسیر fallback لازم است.

| status | نمونه و اقدام |
|---|---|
| `400` | ورودی نامعتبر، نوع پاسخ یا cursor اشتباه؛ درخواست را اصلاح کنید |
| `401` | `AUTH_REQUIRED`؛ توکن منقضی، revoke یا با restart نامعتبر شده است |
| `403` | نقش، workspace، رابطه آموزشی یا مجوز منبع کافی نیست |
| `404` | منبع پیدا نشد یا برای جلوگیری از افشای وجود آن پنهان شده؛ شناسه و context را بررسی کنید |
| `409` | تعارض lifecycle، ظرفیت، مهلت، idempotency یا نسخه پاسخ آزمون؛ `code` را تفکیک کنید |
| `410` | OTP یا دعوت منقضی/غیرقابل استفاده شده است |
| `412` | `CONCURRENCY_CONFLICT` یا `MESSAGE_EDIT_CONFLICT`؛ داده و نسخه جاری را دوباره بگیرید |
| `413` | حجم فایل بیش از سقف |
| `415` | MIME فایل مجاز نیست |
| `429` | محدودیت OTP یا تلاش‌ها؛ ارسال خودکار سریع انجام ندهید |
| `503` | provider پیامک یا storage آماده نیست |
| `500` | خطای غیرمنتظره؛ `traceId` را برای بررسی سرور نگه دارید |

### قراردادهای retry

| عملیات | شناسه یا قاعده |
|---|---|
| پذیرش دعوت | همان دعوت؛ نتیجه قبلی با `alreadyAccepted` قابل بازیابی است |
| شروع Direct | زوج کاربران و Scope؛ گفت‌وگوی قبلی resolve می‌شود |
| ساخت Group/Channel | `clientOperationId` |
| ارسال یا Forward پیام | `clientMessageId` در محدوده فرستنده و گفت‌وگو |
| گزارش پیام | `clientReportId` |
| شروع آزمون | `clientOperationId` |
| ذخیره پاسخ آزمون | `clientOperationId` هر ذخیره، همراه lease و revision موردانتظار |
| Finalize آزمون | `clientOperationId` و payload یکسان |
| انتشار نمره آزمون | `clientOperationId` و payload یکسان |
| ویرایش‌های مبتنی بر rowversion | پس از conflict ابتدا داده را بخوانید و تغییر را بازبینی کنید |

هدر عمومی `Idempotency-Key` پیاده‌سازی نشده است. برای عملیات جدید Guid جدید و برای retry همان عملیات همان Guid و payload را نگه دارید. همه POSTها retry-safe نیستند؛ ساخت دوره، درس، کلاس یا تکلیف را با فرض idempotency تکرار نکنید. کنترل نسخه نیز با idempotency یک مفهوم نیست.

<a id="testing"></a>
## تست و عیب‌یابی

### اجرای validation

```powershell
dotnet restore MakanApp.sln
dotnet build MakanApp.sln --configuration Debug --nologo
dotnet test MakanApp.sln --configuration Debug --nologo
```

برای تمرکز بر یک مجموعه:

```powershell
dotnet test tests/MakanApp.UnitTests/MakanApp.UnitTests.csproj --configuration Debug --nologo
dotnet test tests/MakanApp.ArchitectureTests/MakanApp.ArchitectureTests.csproj --configuration Debug --nologo
dotnet test tests/MakanApp.IntegrationTests/MakanApp.IntegrationTests.csproj --configuration Debug --nologo
```

Integration از SQL Server LocalDB و پایگاه ثابت `MakanApp_MessagingSafety_IntegrationTests_Step7E` استفاده می‌کند؛ fixture آن را در شروع حذف و با migration بازسازی می‌کند و در پایان حذف می‌کند. این نام فقط برای تست است؛ داده شخصی/توسعه را در آن نگذارید و دو اجرای هم‌زمان این suite را روی یک LocalDB شروع نکنید. test fixture اتصال runtime را برای تست override می‌کند. فایل‌ها هم در پوشه موقت مختص تست نوشته می‌شوند.

Unit قوانین مدل و سرویس‌های قابل تست را بررسی می‌کند. Integration رفتار HTTP، SQL Server، migration، مجوز، هم‌زمانی، idempotency و SignalR را پوشش می‌دهد. Architecture جهت وابستگی و عدم ورود EF Core/ASP.NET Core به لایه‌های داخلی را کنترل می‌کند.

### عیب‌یابی سریع

| نشانه | بررسی |
|---|---|
| برنامه با خطای connection string شروع نمی‌شود | تنظیم `ConnectionStrings:MakanDatabase` در محیط واقعی اجرا |
| `/health` سالم ولی API داده خطا می‌دهد | health فعلی probe دیتابیس نیست؛ اتصال و migration را بررسی کنید |
| `Invalid object name` | مقصد migration با مقصد runtime یکسان باشد و migrationهای جاری اعمال شده باشند |
| Swagger در دسترس نیست | فقط در Development ثبت می‌شود؛ پروفایل و آدرس را بررسی کنید |
| OTP نمی‌رسد | adapter توسعه پیامک واقعی ندارد؛ debugger محلی یا تست خودکار را مطابق بخش ورود به کار ببرید |
| پس از restart پاسخ `401` می‌گیرید | کلید موقت توسعه عوض شده؛ دوباره وارد شوید یا کلید توسعه را امن و پایدار تنظیم کنید |
| فضای سازمانی در فهرست نیست | عضویت، سازمان و نقش باید فعال باشند؛ ایجاد حساب جای provisioning را نمی‌گیرد |
| والد کلاس‌ها را نمی‌بیند | `/academic/classes` برای Parent ارائه نشده؛ برنامه/آزمون/نتیجه از مسیرهای مجاز با context فرزند مصرف شود |
| نمره بعد از Save دیده نمی‌شود | Draft و Release جدا هستند |
| پاسخ آزمون ذخیره شده ولی رسید نهایی نیست | Finalize مستقل است و باید پیش از deadline موفق شود |
| ویدئو یا صدا `415` می‌دهد | `Storage:AllowedContentTypes` پیش‌فرض این MIMEها را ندارد |
| SignalR مرورگر `401` می‌دهد | محدودیت هدر در مقابل `access_token` Query و transport مورد استفاده |
| مرورگر خطای CORS دارد | CORS در میزبان فعلی ثبت نشده است؛ موفقیت Postman اثبات اتصال cross-origin نیست |
| Integration شکست می‌خورد | دسترس‌پذیری LocalDB، اجرای موازی suite و مجوز ایجاد دیتابیس تست |

<a id="limits"></a>
## محدودیت‌ها و مسیر مطالعه

### مرز قابلیت موجود

در این نسخه، provisioning عمومی سازمان/عضویت/دعوت/اشخاص سازمانی/رابطه سرپرست، provider واقعی پیامک و فایل در محیط تولید، refresh token، CORS، پاک‌سازی زمان‌بندی‌شده فایل‌های unattached، auto-finalize آزمون، پنل moderation، Push provider، backplane چند-instance SignalR، Copilot، AI، Commerce و گزارش‌گیری عمومی API قابل استفاده ندارند. وجود مدل یا Enum برای یک وضعیت، به معنی وجود Endpoint انتقال به آن وضعیت نیست.

تست Integration داده لازم سناریوها را خودش می‌سازد. برای آزمودن دستی کل جریان سازمانی روی دیتابیس تازه، آماده‌سازی داده اولیه یک کار جدا و نیازمند روش مصوب است؛ این راهنما seed یا SQL تولیدی ارائه نمی‌کند.

### دیتابیس و منابع کد

Persistence با `MakanDbContext` و schemaهای `identity`، `organization`، `guardian`، `academic`، `assessment`، `storage` و `messaging` سازمان‌دهی شده است. روابط هم‌سازمانی با FKهای ترکیبی، یکتایی وضعیت فعال با filtered unique index و تغییرات حساس با `rowversion` و transaction محافظت می‌شوند. Outbox پیام‌رسانی اعلان را پس از commit کسب‌وکاری ارسال می‌کند.

برای جزئیات مدل، [راهنمای ساختار دیتابیس](database-current-structure.md) را کنار migrationهای جدید بخوانید؛ آن سند لزوماً پوشش migrationهای پس از تاریخ نگارشش را ندارد. مرجع schema جاری، configurationها و migrationهای موجود است.

مسیر مطالعه یک جریان واقعی آزمون:

1. [ExamsController](../MakanApp.Api/Controllers/ExamsController.cs) و [ExamGradingsController](../MakanApp.Api/Controllers/ExamGradingsController.cs) برای قرارداد HTTP.
2. [ExamService](../MakanApp.Application/Assessment/ExamService.cs)، [ExamAttemptService](../MakanApp.Application/Assessment/ExamAttemptService.cs)، [ExamAnswerService](../MakanApp.Application/Assessment/ExamAnswerService.cs)، [ExamFinalizationService](../MakanApp.Application/Assessment/ExamFinalizationService.cs) و [ExamGradingService](../MakanApp.Application/Assessment/ExamGradingService.cs) برای use case.
3. [مدل‌های Assessment](../MakanApp.Domain/Assessment) برای lifecycle و قوانین دامنه.
4. [ExamGradingAbstractions](../MakanApp.Application/Assessment/ExamGradingAbstractions.cs) برای مرز persistence.
5. [پیاده‌سازی Assessment](../MakanApp.Infrastructure/Assessment) و [EF configurationها](../MakanApp.Infrastructure/Persistence/Configurations/Assessment).
6. [Migrationها](../MakanApp.Infrastructure/Persistence/Migrations) برای ساختار SQL Server.
7. [ExamGradingEndpointsTests](../tests/MakanApp.IntegrationTests/ExamGradingEndpointsTests.cs) و سایر تست‌های آزمون برای نمونه درخواست و سناریوهای مجاز/غیرمجاز.

برای قرارداد دقیق هر عملیات به [مرجع API](backend-api-reference.md) و برای قواعد کلی مخزن به [AGENTS.md](../AGENTS.md) مراجعه کنید.
