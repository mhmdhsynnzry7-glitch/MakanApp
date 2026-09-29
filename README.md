# ماکان‌اپ — MakanApp Backend

این مخزن فقط Backend ماکان را نگه می‌دارد. ساختار فعلی یک Modular Monolith با Onion Architecture روی ASP.NET Core و .NET 9 است. Persistence بر Entity Framework Core 9 و Microsoft SQL Server بنا شده است. Frontend در این workspace وجود ندارد.

اسناد مرجع در `docs/project-context` و قواعد کار در `AGENTS.md` قرار دارند.

## مسیرها

~~~text
Workspace:      D:\Project\MakanApp\MakanApp-Backend
Solution:       D:\Project\MakanApp\MakanApp-Backend\MakanApp.sln
API project:    D:\Project\MakanApp\MakanApp-Backend\MakanApp.Api\MakanApp.Api.csproj
Domain project: D:\Project\MakanApp\MakanApp-Backend\MakanApp.Domain\MakanApp.Domain.csproj
~~~

## ساختار Onion

~~~text
MakanApp.sln
MakanApp.Domain/
  Common/DomainAssembly.cs
MakanApp.Application/
  Common/ApplicationAssembly.cs
MakanApp.Infrastructure/
  Persistence/MakanDbContext.cs
  DependencyInjection.cs
MakanApp.Api/
  Controllers/
  Errors/GlobalExceptionHandler.cs
  Properties/launchSettings.json
  Program.cs
  appsettings.json
  appsettings.Development.json
  MakanApp.Api.http
tests/
  MakanApp.UnitTests/
  MakanApp.IntegrationTests/
  MakanApp.ArchitectureTests/
docs/project-context/
~~~

مسئولیت لایه‌ها:

- `MakanApp.Domain`: قوانین و مدل خالص دامنه؛ بدون وابستگی به framework یا لایه‌های بیرونی.
- `MakanApp.Application`: use caseها، DTOها و portهای موردنیاز؛ وابسته فقط به Domain.
- `MakanApp.Infrastructure`: EF Core، SQL Server و adapterهای بیرونی؛ وابسته به Application و Domain.
- `MakanApp.Api`: HTTP host، Controllerها، ProblemDetails، OpenAPI و composition root؛ وابسته به Application و Infrastructure.

جهت مجاز وابستگی:

~~~text
Api -> Infrastructure -> Application -> Domain
Api --------------------> Application
Infrastructure ----------------------> Domain
~~~

وابستگی معکوس مجاز نیست. Controllerها فقط در `MakanApp.Api/Controllers` قرار می‌گیرند و EF Core فقط در Infrastructure استفاده می‌شود.

## پیش‌نیازها

- Windows و Visual Studio 2022 یا VS Code با C# Dev Kit
- .NET SDK 9
- Microsoft SQL Server
- دسترسی به NuGet برای نخستین restore

تمام پروژه‌ها `net9.0` را هدف می‌گیرند. وجود SDK جدیدتر روی سیستم، TargetFramework پروژه را تغییر نمی‌دهد.

## SQL Server و EF Core

`MakanDbContext` در `MakanApp.Infrastructure/Persistence` قرار دارد و با `Microsoft.EntityFrameworkCore.SqlServer` در DI ثبت می‌شود. کلید اتصال:

~~~text
ConnectionStrings:MakanDatabase
~~~

`appsettings.Development.json` یک نمونه credential-free برای LocalDB دارد. برای جایگزینی امن مقدار در توسعه:

~~~powershell
dotnet user-secrets set "ConnectionStrings:MakanDatabase" "Server=(localdb)\MSSQLLocalDB;Database=MakanApp;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True" --project MakanApp.Api/MakanApp.Api.csproj
~~~

در محیط‌های دیگر مقدار باید از secret store یا `ConnectionStrings__MakanDatabase` تأمین شود. migrationهای `InitialIdentity` و `AddOrganizationMemberships` در Infrastructure قرار دارند. `Database.EnsureCreated` در برنامه استفاده نمی‌شود و migration هنگام startup اجرا نمی‌شود؛ اعمال migration یک عملیات کنترل‌شده و جداگانه است.

## API Foundation

- ASP.NET Core Controllers
- OpenAPI و Swagger UI در Development
- ProblemDetails و exception handling متمرکز
- HTTPS redirection خارج از Testing
- health checks
- DI composition در `MakanApp.Api/Program.cs`

| مسیر | محیط | کاربرد |
|---|---|---|
| `GET /health` | همه محیط‌ها | سلامت application |
| `GET /openapi/v1.json` | Development | سند OpenAPI |
| `/swagger` | Development | Swagger UI |

## Identity، OTP و پروفایل

مدل‌های `User`، `Person`، `UserCredential`، `OtpChallenge` و `UserSession` در schema با نام `identity` نگهداری می‌شوند. هویت User از شماره تلفن جدا است و نقش سازمانی در ثبت‌نام یا claim نشست قرار نمی‌گیرد.

| متد | مسیر | نیاز به ورود |
|---|---|---|
| `POST` | `/api/v1/auth/otp/challenges` | خیر |
| `POST` | `/api/v1/auth/otp/verify` | خیر |
| `POST` | `/api/v1/auth/logout` | بله |
| `GET` | `/api/v1/me` | بله |
| `PATCH` | `/api/v1/me/profile` | بله |

OTP با مولد تصادفی رمزنگاری تولید و فقط به‌صورت HMAC همراه salt در SQL Server ذخیره می‌شود. کد منقضی می‌شود، یک‌بارمصرف است و محدودیت تلاش و ارسال مجدد دارد. نشست با Bearer token تصادفی کار می‌کند و فقط HMAC توکن در `identity.UserSessions` ذخیره می‌شود؛ logout همان نشست را revoke می‌کند.

`DevelopmentSmsSender` فقط در محیط‌های `Development` و `Testing` ثبت می‌شود. این adapter اتصال واقعی پیامک نیست، OTP را در response یا log منتشر نمی‌کند و نگهداری موقت in-memory آن صرفاً برای تست خودکار است. در محیط‌های دیگر adapter توسعه فعال نمی‌شود و تا زمان پیکربندی سرویس واقعی، درخواست ارسال پیامک با خطای کنترل‌شده رد می‌شود.

کلید `Identity:SecurityKey` باید خارج از Development/Testing از secret store تأمین شود و حداقل ۳۲ بایت UTF-8 داشته باشد. کلید Development/Testing هنگام شروع process به‌صورت تصادفی ساخته می‌شود؛ بنابراین نشست‌های آن محیط‌ها بعد از restart معتبر نمی‌مانند.

درخواست OTP علاوه بر محدودیت پایدار مبتنی بر شماره تلفن، policy داخلی ASP.NET Core مبتنی بر IP دارد. policy مبتنی بر IP در این مرحله in-memory و تک-instance است؛ استقرار چند-instance در آینده به rate limiter توزیع‌شده نیاز دارد.

## سازمان، عضویت، دعوت و فضای کاری

مدل‌های `Organization`، `Membership`، `RoleAssignment` و `Invitation` در schema با نام `organization` نگهداری می‌شوند. نقش سازمانی به عضویت تعلق دارد و از چهار مقدار پایدار `Student`، `Teacher`، `Parent` و `Manager` استفاده می‌کند؛ هیچ نقش سراسری روی User ذخیره نمی‌شود.

| متد | مسیر | نیاز به ورود |
|---|---|---|
| `GET` | `/api/v1/workspaces/me` | بله |
| `POST` | `/api/v1/workspaces/select` | بله |
| `GET` | `/api/v1/workspaces/current` | بله |
| `GET` | `/api/v1/invitations` | بله |
| `POST` | `/api/v1/invitations/{invitationId}/accept` | بله |
| `POST` | `/api/v1/invitations/{invitationId}/decline` | بله |

فضای شخصی مستقل از عضویت سازمانی همیشه در دسترس است. فضای سازمانی فقط از عضویت فعال، سازمان فعال و نقش فعال ساخته می‌شود. انتخاب فضا در همان `UserSession` ثبت می‌شود و در هر resolve مجدداً سمت سرور اعتبارسنجی می‌شود؛ شناسه سازمان یا نقش ارسالی Client به‌تنهایی مجوز نیست.

پذیرش دعوت در transaction با isolation سطح `Serializable` و lockهای SQL Server انجام می‌شود و تکرار پذیرش همان اثر قبلی را برمی‌گرداند. filtered unique indexها از عضویت فعال و نقش فعال تکراری جلوگیری می‌کنند و `rowversion` روی رکوردهای قابل تغییر تعارض stale write را آشکار می‌کند.

برای اعمال migration روی database مجاز و ایزوله:

~~~powershell
dotnet tool restore
dotnet tool run dotnet-ef database update --project MakanApp.Infrastructure/MakanApp.Infrastructure.csproj --startup-project MakanApp.Infrastructure/MakanApp.Infrastructure.csproj
~~~

این فرمان را روی Production یا database عادی توسعه بدون فرایند migration مصوب اجرا نکنید.

## Restore، build و test

از ریشه workspace اجرا کنید:

~~~powershell
dotnet restore MakanApp.sln
dotnet build MakanApp.sln --configuration Debug --nologo
dotnet test MakanApp.sln --configuration Debug --nologo
~~~

- Unit Tests علاوه بر قواعد Identity، انقضا و idempotency دعوت، چرخه عضویت و استقلال فضای شخصی را بررسی می‌کنند.
- Integration Tests migration واقعی و endpointهای Identity/Profile/Organization را روی SQL Server LocalDB اختصاصی `MakanApp_Organization_IntegrationTests_Step4` بررسی و آن database را در پایان حذف می‌کنند؛ EF Core InMemory استفاده نمی‌شود.
- Architecture Tests جهت وابستگی Onion و نبود EF Core/ASP.NET Core در لایه‌های داخلی را enforce می‌کنند.

## اجرا

~~~powershell
dotnet run --project MakanApp.Api/MakanApp.Api.csproj --configuration Debug --launch-profile https
~~~

~~~text
https://localhost:7063/health
https://localhost:7063/swagger
https://localhost:7063/openapi/v1.json
~~~

## Visual Studio

1. `MakanApp.sln` را باز کنید.
2. `MakanApp.Api` را Startup Project قرار دهید.
3. پیکربندی `Debug / Any CPU` و پروفایل `https` را انتخاب کنید.
4. Solution را build و اجرا کنید.
5. `/health` یا Swagger UI را بررسی کنید.

## محدودیت‌ها

ارسال پیامک واقعی و rate limiting توزیع‌شده هنوز پیاده‌سازی نشده‌اند. ایجاد دعوت توسط مدیر و مدیریت عمومی سازمان در API این مرحله ارائه نشده‌اند. `GuardianRelation`، انتخاب فرزند، مدل‌های آموزشی، Messaging، Copilot، audit، outbox و deployment نیز هنوز پیاده‌سازی نشده‌اند.
