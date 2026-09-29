# AGENTS.md — MakanApp Backend

This file contains the permanent development rules for Codex and other coding agents working on the MakanApp backend.

These rules apply to the entire repository unless a more specific `AGENTS.md` exists in a deeper directory.

---

## زبان، نام‌گذاری و شیوه همکاری

- درخواست صریح کاربر محدوده هر کار را تعیین می‌کند؛ پیشنهادهای اسناد پروژه به‌تنهایی مجوز پیاده‌سازی نیستند.
- پیش از تغییر، این فایل، هر `AGENTS.md` اختصاصی در مسیر هدف و اسناد مرتبط `docs/project-context` را بخوانید.
- نام پروژه، فایل کد، namespace، کلاس، متد، property و متغیر انگلیسی باشد.
- سبک موجود را حفظ کنید: `PascalCase` برای type و عضو عمومی، `camelCase` برای پارامتر و متغیر محلی و `_camelCase` برای فیلد خصوصی.
- توضیحات جدید یا ویرایش‌شده در کد، مستندات و گزارش توسعه‌دهنده فارسی باشند؛ شناسه‌ها، نام ابزارها و فرمان‌ها انگلیسی بمانند.
- برای ترجمه توضیحات قدیمی یا یکسان‌سازی ظاهری، تغییر نامرتبط ایجاد نکنید.
- تغییرات را کوچک، مرتبط و قابل بازبینی نگه دارید؛ بازآرایی، ارتقای وابستگی یا format نامرتبط انجام ندهید.
- وابستگی‌ها را با Dependency Injection و ترجیحاً constructor injection تأمین کنید؛ وابستگی قابل تزریق را مستقیم داخل کلاس نسازید.

## 1. Project Context

Project root:

```text
D:\Project\MakanApp\MakanApp-Backend
```

Solution:

```text
D:\Project\MakanApp\MakanApp-Backend\MakanApp.sln
```

Project documents:

```text
D:\Project\MakanApp\MakanApp-Backend\docs\project-context
```

Before implementing a new feature, read the relevant project documents under `docs/project-context`.

Important source-of-truth rule:

- SQL Server is the required primary database.
- Any MySQL-specific database decision in older architecture documents is obsolete.
- `docs/project-context/01_SQLSERVER_ARCHITECTURE_OVERRIDE.md` is authoritative for database-specific decisions.
- Frontend information in the documents is reference-only for UX flows, API needs, states, and expected behavior.
- Do not clone, recreate, or modify frontend code unless explicitly requested.

---

## 2. Fixed Technical Baseline

Use:

- C#
- .NET 9
- ASP.NET Core Web API
- ASP.NET Core Controllers
- Entity Framework Core 9
- Microsoft SQL Server
- `Microsoft.EntityFrameworkCore.SqlServer`
- xUnit
- OpenAPI / Swagger
- Onion Architecture
- Modular Monolith

Do not change `.NET 9` without explicit approval.

Do not introduce another primary database.

Do not use:

- MySQL
- PostgreSQL
- Pomelo
- `MySql.EntityFrameworkCore`

unless explicitly approved in a future architectural decision.

---

## 3. Solution Structure

The main projects live directly under the repository root.

Expected structure:

```text
MakanApp-Backend/
├── MakanApp.sln
├── MakanApp.Domain/
├── MakanApp.Application/
├── MakanApp.Infrastructure/
├── MakanApp.Api/
├── tests/
│   ├── MakanApp.UnitTests/
│   ├── MakanApp.IntegrationTests/
│   └── MakanApp.ArchitectureTests/
├── docs/
│   └── project-context/
├── AGENTS.md
└── README.md
```

Do not create a `src/` directory unless explicitly requested.

---

## 4. Architecture Rules

The backend uses:

```text
Onion Architecture
inside a
Modular Monolith
```

Onion Architecture defines dependency direction.

Modular Monolith defines business-module boundaries and deployment strategy.

### Dependency direction

```text
Domain
  ↑
Application
  ↑
Infrastructure

Api -> Application
Api -> Infrastructure
```

### MakanApp.Domain

`MakanApp.Domain` is a pure Class Library.

It may contain:

- Entities
- Value Objects
- Domain Enums
- Domain Rules
- Domain Exceptions
- Domain Services only when domain behavior genuinely requires them

It must not depend on:

- Application
- Infrastructure
- Api
- Entity Framework Core
- SQL Server
- ASP.NET Core
- HTTP
- SignalR
- SMS providers
- AI providers

Do not place persistence attributes or HTTP concerns in Domain unless an explicit architectural decision permits it.

### MakanApp.Application

`MakanApp.Application` is a Class Library.

It may depend on:

- `MakanApp.Domain`

It owns:

- Application use cases
- Application services / handlers
- Commands or requests when useful
- Queries when useful
- Application DTOs
- Use-case validation
- Ports / abstractions required by the use cases

It must not depend on:

- Infrastructure
- Api
- EF Core implementation
- SQL Server implementation
- ASP.NET Core Controllers

Do not introduce MediatR merely to implement use cases.

Plain application services/handlers are preferred.

### MakanApp.Infrastructure

`MakanApp.Infrastructure` is a Class Library.

It may depend on:

- `MakanApp.Application`
- `MakanApp.Domain`

It owns technical implementations such as:

- EF Core
- SQL Server
- `MakanDbContext`
- Entity configurations
- Migrations
- SMS implementations
- File storage implementations
- External provider adapters
- Authentication/session persistence infrastructure
- Background/integration adapters when introduced

It must not depend on:

- `MakanApp.Api`

### MakanApp.Api

`MakanApp.Api` is the executable ASP.NET Core Web API host.

It owns:

- Controllers
- HTTP routing
- Authentication pipeline
- Authorization pipeline
- ProblemDetails
- HTTP exception handling
- OpenAPI
- Health checks
- Composition Root
- `Program.cs`
- `appsettings*.json`
- `launchSettings.json`

Controllers live only in:

```text
MakanApp.Api/Controllers
```

Controllers must remain thin.

A controller should primarily:

```text
HTTP Request
-> map transport input
-> call Application use case
-> map Application result
-> HTTP Response
```

Do not put:

- EF Core queries
- SQL
- domain rules
- persistence logic
- OTP logic
- authorization business rules

inside Controllers.

---

## 5. Modular Monolith Rules

The system is a Modular Monolith, not a collection of microservices.

Expected business areas include:

- Identity
- Organization
- Guardian
- Academic
- Curriculum
- Assessment
- Messaging
- Knowledge
- Intelligence
- Copilot
- Commerce
- Notification
- Storage / Audit
- Platform Operations

Do not create all modules or placeholder files in advance.

Create a module only when a real feature requires it.

A module must not write directly into another module's internal data just because the database is shared.

Prefer explicit application/module contracts.

Do not introduce Microservices unless there is a documented concrete need.

---

## 6. Avoid Unnecessary Abstractions

Do not introduce the following merely to make the project look more architectural:

- Microservices
- Event Sourcing
- MediatR
- `IGenericRepository<T>`
- `GenericRepository<T>`
- CQRS or heavy CQRS infrastructure without a concrete requirement
- unnecessary factories
- unnecessary wrapper services
- unused interfaces
- placeholder classes

Every abstraction must solve a real problem.

EF Core already provides important Repository / Unit of Work behavior.
Do not hide EF Core behind a generic repository without a concrete reason.

---

## 7. SQL Server and EF Core Rules

Primary database:

```text
Microsoft SQL Server
```

ORM:

```text
Entity Framework Core
Microsoft.EntityFrameworkCore.SqlServer
```

Database-specific code belongs in Infrastructure.

Use SQL Server-native features where appropriate, including:

- `uniqueidentifier` / `Guid`
- `datetime2`
- `nvarchar`
- `rowversion`
- filtered unique indexes
- database constraints
- transactions
- appropriate SQL Server locking/isolation where concurrency requires it

Use UTC for persisted event timestamps.

Do not trust client/device time for authoritative deadlines or accepted timestamps.

Do not use `Database.EnsureCreated()` for the application database.

Do not automatically execute migrations during application startup unless explicitly approved later.

Use EF Core migrations.

Production schema changes must be versioned and reproducible.

Do not manually mutate the production schema.

---

## 8. Database Integrity

Application checks alone are not enough for important invariants.

Where appropriate, enforce integrity using:

- Primary Keys
- Foreign Keys
- Unique Indexes
- Filtered Unique Indexes
- Check Constraints
- `rowversion`
- transaction boundaries

For concurrency-sensitive operations, do not use:

```text
check -> later write
```

without considering race conditions.

Important examples include:

- class capacity
- duplicate active membership
- duplicate active enrollment
- invitation acceptance
- grade updates
- payment confirmation
- message idempotency

---

## 9. Authentication and Authorization Principles

Authentication and authorization are different concerns.

Role alone is not sufficient authorization.

Authorization may depend on:

- authenticated actor
- active session
- valid organization membership
- role assignment
- organization
- resource
- enrollment
- teacher assignment
- guardian relationship
- current permission/policy state

Client-provided values such as:

- `OrganizationId`
- `MembershipId`
- `Role`
- `ChildId`
- `ClassId`
- `StudentId`

are selectors, not proof of authorization.

The server must resolve and validate the authoritative context.

Do not create a global `User.Role` for organization roles.

Roles come from valid membership/assignment relationships.

---

## 10. Security Rules

Never hard-code or commit:

- passwords
- OTP values
- API keys
- payment secrets
- AI provider secrets
- production connection strings
- private tokens

Use configuration / User Secrets / environment-specific secret stores. In shared documentation, record only the setting name and a non-secret sample value.

Treat `.vs/`, `*.csproj.user`, `bin/`, and `obj/` as personal/generated artifacts, not shareable source. Do not delete existing artifacts without approval.

Do not log secrets.

Do not log plaintext OTPs or session credentials.

Do not expose internal exception stack traces to production clients.

Use stable ProblemDetails/error codes.

Do not leak the existence of inaccessible resources when the authorization policy requires concealment.

---

## 11. Async and Cancellation

Use `async` / `await` for relevant I/O.

Pass `CancellationToken` through relevant:

- Controllers
- Application use cases
- EF Core calls
- external service calls
- file operations
- background operations

Do not add asynchronous APIs when the operation is entirely synchronous just for style.

---

## 12. API Rules

Base versioned API path:

```text
/api/v1
```

Use ASP.NET Core Controllers.

Do not expose EF entities directly.

Use explicit request/response contracts.

Important API behavior should define:

- request
- response
- validation
- authentication
- authorization
- success status
- error status
- idempotency where relevant
- concurrency behavior where relevant

Use ProblemDetails for errors.

Keep machine-readable error codes stable.

---

## 13. Idempotency and Transactions

For operations that may be safely retried, explicitly consider idempotency.

Examples:

- OTP verification
- invitation acceptance
- enrollment
- assignment submission
- exam finalization
- message sending
- payment callback
- organization provisioning

Do not create duplicate business effects because the client retried after losing a response.

Keep database transactions short.

Do not execute long network operations inside a database transaction, such as:

- SMS sending
- AI calls
- HTTP provider calls
- file scanning
- large document processing

Use Outbox or another appropriate post-commit mechanism when the feature requires durable side effects.

---

## 14. Migrations

Create migrations only after the relevant model is ready.

Use meaningful migration names.

Inspect generated migrations.

Do not apply migrations to production unless explicitly requested.

Integration tests must not modify a normal developer database.

Use a clearly isolated test database.

---

## 15. Testing Rules

Every meaningful feature should have relevant tests.

Use:

### Unit Tests

For pure Domain/Application rules.

### Integration Tests

For behavior involving:

- ASP.NET Core host
- Controllers/API
- SQL Server
- EF Core
- migrations
- database constraints
- authentication/session
- authorization
- concurrency
- idempotency

Do not use EF Core InMemory as proof of SQL Server behavior.

Use a real SQL Server-compatible test database such as an isolated LocalDB/database when appropriate.

### Architecture Tests

Enforce Onion dependency rules.

At minimum preserve these invariants:

- Domain does not depend on Application
- Domain does not depend on Infrastructure
- Domain does not depend on Api
- Application does not depend on Infrastructure
- Application does not depend on Api
- Infrastructure does not depend on Api
- Domain/Application do not accidentally gain EF Core implementation dependencies

Do not create meaningless tests only to increase test count.

---

## 16. Mandatory Build / Test / Fix Workflow

At the end of every implementation step, always run validation.

Minimum sequence:

```text
Implement
-> restore
-> build
-> fix build errors
-> test
-> fix failing tests
-> final build
-> final test
-> review git diff
-> local commit
-> final learning report
```

Run:

```powershell
dotnet restore MakanApp.sln
dotnet build MakanApp.sln --configuration Debug --nologo
dotnet test MakanApp.sln --configuration Debug --nologo
```

If build fails:

- diagnose the actual cause
- fix errors introduced by the current step
- run build again
- repeat until successful

If tests fail:

- diagnose the actual cause
- fix failures introduced by the current step
- run tests again

Do not:

- hide errors
- suppress meaningful warnings simply to make CI green
- disable tests to claim success
- claim success when commands were not actually run
- treat the absence of a test project or discovery of zero tests as a successful test run

Report the actual number of executed, passed, failed, and skipped tests when available. Separate environment, SDK, network, and restore failures from code failures. Do not change .NET versions or disable security checks to work around an environment problem.

If a blocker cannot be fixed safely, stop and report the blocker.

---

## 17. Git Workflow

Before each implementation step:

```powershell
git status
```

Understand pre-existing user changes.

Never overwrite unrelated user work.

Before commit:

```powershell
git status
git diff --stat
git diff
```

Check that:

- only expected files changed
- no secrets were added
- no production credentials were added
- no frontend files were touched
- no `bin/` or `obj/` artifacts are staged
- no unrelated files were deleted

Stage only files related to the current step.

Create a local commit only when:

```text
Build = Success
Tests = Success
```

Use a clear Conventional Commit-style message when appropriate.

Do not push unless the user explicitly asks.

Do not publish or deploy unless explicitly requested.

---

## 18. Destructive Operations

Do not perform destructive operations without explicit approval.

Examples include:

- dropping databases
- deleting user data
- deleting non-template project files
- resetting Git history
- `git reset --hard`
- `git clean`
- force push
- deleting migrations with existing production use
- destructive production migrations

When uncertain, preserve the file/data and report the issue.

---

## 19. Existing User Changes

Always preserve existing user changes.

Do not assume an uncommitted file was generated by Codex.

Check Git state before making significant edits.

Do not revert, overwrite, reformat, or replace unrelated modifications.

If `.git` is unavailable, report that history comparison is not possible and use file-content or hash comparison where useful. Do not initialize a new repository unless explicitly requested.

---

## 20. Frontend Boundary

Frontend code is outside the backend implementation boundary. In backend tasks it is read-only reference material, even when UX information is available in project documents. A separately authorized frontend task must use an explicitly provided frontend workspace and must not be inferred from a backend request.

Do not:

- clone the frontend repository
- recreate the frontend
- edit frontend files
- refactor frontend components
- change React/React Native/Expo/Tailwind

Frontend documentation may be used only to understand:

- UX flows
- screen identifiers
- API needs
- loading/error/empty states
- expected behavior

Backend business/security decisions remain server-side.

---

## 21. Project Documentation

Keep README and technical documentation accurate when architecture or developer workflow changes.

Do not rewrite unrelated documentation.

When a step introduces an important architecture decision, consider documenting it with an ADR only when the decision is significant enough to justify one.

Do not generate large quantities of documentation that simply duplicate the code.

---

## 22. Coding Style

Code/file/class/member names must be English.

Developer-facing reports to the project owner should be Persian.

Prefer:

- clear names
- small focused methods
- explicit responsibilities
- constructor injection
- immutable request/result models where appropriate
- straightforward code over clever code

Avoid:

- giant services
- giant controllers
- god entities
- static service locators
- hidden global mutable state
- copy/pasted authorization logic
- magic strings spread through the codebase

Follow the repository's existing formatting and naming conventions.

---

## 23. Scope Discipline

Implement only the requested step.

Do not implement future modules "while already here".

Prefer complete vertical slices over broad unfinished scaffolding.

Do not create placeholder classes/directories merely to make the architecture look complete.

---

## 24. End-of-Step Developer Learning Report

At the end of every implementation step, after validation and the local commit,
provide a clear educational report in Persian.

The report is mandatory.

The project owner is a backend developer and wants to understand and learn from
the actual implementation.

Use the following structure.

### 24.1 خلاصه مرحله

Explain:
- چه مسئله‌ای حل شد؟
- قبل از این مرحله چه چیزی وجود نداشت؟
- بعد از این مرحله چه چیزی داریم؟

### 24.2 معماری

Explain:
- تغییرات در کدام لایه‌های Onion انجام شدند؟
- چرا هر بخش در Domain / Application / Infrastructure / Api قرار گرفت؟
- Dependency flow این قابلیت چیست؟

### 24.3 فایل‌های مهم ایجادشده

For every important new file provide:
- relative path
- responsibility
- why it exists
- important concept/code a backend developer should understand

### 24.4 فایل‌های مهم تغییرکرده

Explain:
- what changed
- why
- effect on the system

### 24.5 جریان اجرای قابلیت

Explain the actual runtime flow using real class names created in the step.

Example:

```text
HTTP Request
-> Controller
-> Application Use Case
-> Domain Rule
-> Application Port
-> Infrastructure Implementation
-> SQL Server
-> Application Result
-> HTTP Response
```

### 24.6 دیتابیس

If the step changed persistence, explain:
- tables
- important columns
- primary keys
- foreign keys
- unique indexes
- filtered indexes
- constraints
- rowversion/concurrency
- migration
- transaction boundaries
- why important constraints exist

### 24.7 Endpointها

For each important endpoint explain:
- HTTP Method
- Route
- Request
- Response
- Validation
- Authentication
- Authorization
- important error cases

### 24.8 مفاهیم فنی استفاده‌شده

Teach only concepts actually used in the step.

### 24.9 تصمیم‌های طراحی

Explain:
- alternatives considered
- selected approach
- why it was chosen
- important trade-offs

### 24.10 تست‌ها

Explain:
- tests added
- what each important test proves
- why it is Unit / Integration / Architecture test

### 24.11 Validation

Report actual results:
- restore
- build
- tests
- warnings
- migration validation if relevant

Success must only be reported when the command actually succeeded.

### 24.12 Git

Report:
- commit hash
- commit message
- summary of files included in the commit
- confirm whether Push was performed

Default expectation: no Push.

### 24.13 محدودیت‌های فعلی

Clearly explain what is intentionally not implemented yet.

### 24.14 نکات آموزشی

Provide 3–7 concrete learning points from the actual code written in this step.

### 24.15 مسیر پیشنهادی مطالعه کد

Give an ordered reading path through the important files.

Prefer a flow such as:
1. Controller
2. Application use case
3. Domain model
4. Application abstraction/port
5. Infrastructure implementation
6. EF configuration
7. Migration
8. Tests

Adjust this list to the code that actually exists.

### 24.16 مرحله بعد

Explain:
- exact recommended next step
- why it logically comes next
- what must not be mixed into it yet

---

## 25. Final Rule

Do not optimize for producing the largest amount of code.

Optimize for:

```text
Correctness
Security
Clarity
Testability
Maintainability
Small reviewable steps
Understanding of the system
```

When requirements are ambiguous in a way that affects security, data model,
authorization, or core behavior, stop and ask before guessing.
## End-of-Step Developer Learning Report

At the end of every implementation step, after the requested implementation,
successful build/tests, and local commit, provide a developer-oriented report
in Persian.

The project owner is a backend developer and wants to understand and learn
from the actual implementation.

The final report must include:

1. خلاصه مرحله

   - چه مسئله‌ای حل شد؟
   - قبل از این مرحله چه چیزی نداشتیم؟
   - بعد از این مرحله چه چیزی داریم؟

2. معماری

   - تغییرات در کدام لایه‌های Onion انجام شدند؟
   - چرا هر بخش در Domain / Application / Infrastructure / Api قرار گرفت؟
   - Dependency flow قابلیت را توضیح بده.

3. فایل‌های مهم ایجادشده
   برای هر فایل مهم:

   - مسیر
   - مسئولیت
   - دلیل ایجاد
   - نکته مهمی که یک برنامه‌نویس باید از آن یاد بگیرد

4. فایل‌های مهم تغییرکرده

   - چه چیزی تغییر کرد؟
   - چرا؟
   - چه اثری روی سیستم دارد؟

5. جریان اجرای قابلیت
   با نام واقعی کلاس‌های ایجادشده توضیح بده:

   HTTP Request
   -> Controller
   -> Application Use Case
   -> Domain Rules
   -> Infrastructure
   -> SQL Server
   -> Result
   -> HTTP Response

6. دیتابیس
   اگر دیتابیس تغییر کرده:

   - Tableها
   - Columnهای مهم
   - PK / FK
   - Indexها
   - Constraints
   - rowversion / concurrency
   - Migration
   - دلیل تصمیم‌ها

7. Endpointها
   برای هر Endpoint:

   - Method
   - Route
   - Request
   - Response
   - Validation
   - Authentication / Authorization
   - خطاهای مهم

8. مفاهیم فنی استفاده‌شده
   فقط مفاهیمی را توضیح بده که واقعاً در همان مرحله استفاده شده‌اند.

9. تصمیم‌های طراحی

   - چه گزینه‌هایی وجود داشت؟
   - چه روشی انتخاب شد؟
   - چرا؟
   - Trade-off چیست؟

10. تست‌ها

    - چه تست‌هایی اضافه شدند؟
    - هر تست چه چیزی را اثبات می‌کند؟
    - Unit / Integration / Architecture بودن آن را توضیح بده.

11. نتیجه Validation
    نتیجه واقعی:

    - restore
    - build
    - tests
    - warnings
    - migrations

12. Git

    - Commit hash
    - Commit message
    - خلاصه فایل‌های commit
    - آیا Push انجام شده یا خیر

13. محدودیت‌های فعلی
    مواردی که عمداً هنوز پیاده‌سازی نشده‌اند.

14. نکات آموزشی
    3 تا 7 نکته مشخص که یک Backend Developer باید از این مرحله یاد بگیرد.

15. مسیر پیشنهادی مطالعه کد
    فایل‌های مهم را به ترتیب مطالعه معرفی کن، مثلاً:
    Controller
    -> Application
    -> Domain
    -> Infrastructure
    -> EF Configuration
    -> Migration
    -> Tests

16. مرحله بعد
    مرحله بعدی و دلیل آن را کوتاه توضیح بده.

Rules:

- Report only code that actually exists.
- Do not invent tests, files, commands, or behavior.
- Do not claim success unless build/tests actually succeeded.
- Keep the explanation practical and educational.

## Git Completion Rule

At the end of every implementation step:

1. Run restore.
2. Run build.
3. Fix all build errors caused by the step.
4. Run tests.
5. Fix all failing tests caused by the step.
6. Run a final build and final test suite.
7. Review:
   - git status
   - git diff --stat
   - git diff
8. If build and tests both succeed:
   - stage only relevant files
   - create a local commit
   - push the commit to the current tracked remote branch
9. If build or tests fail:
   - do NOT commit
   - do NOT push
   - report the blocker
10. Never force-push.
11. Never rewrite remote history.
12. Never push unrelated or pre-existing user changes.
13. If the current branch has no configured upstream, stop and ask before creating one.
14. If authentication or remote permissions prevent push, report the exact blocker.
15. After a successful push, include:
   - commit hash
   - commit message
   - branch name
   - remote name
   - push result
   in the final Persian developer learning report.