# Makan Architecture v3.1 — SQL Server Override

**Status:** Authoritative database correction for Makan Architecture v3  
**Scope:** Database, EF Core persistence, database concurrency, constraints, transactions, migrations, search assumptions, backup/restore, and DB-specific test requirements.

> این سند معماری اصلی را بازنویسی کامل نمی‌کند. تمام تصمیم‌های غیر دیتابیسی معماری v3 پابرجا هستند. هر بخش MySQL-specific در معماری اصلی، در صورت تعارض، با این فایل جایگزین می‌شود.

---

# 1. تصمیم نهایی

Database اصلی Makan:

```text
Microsoft SQL Server
```

ORM:

```text
Entity Framework Core
Microsoft.EntityFrameworkCore.SqlServer
```

نسخه Provider باید با major version واقعی EF Core پروژه یکسان باشد.

مثال:

```text
EF Core 9  -> Microsoft.EntityFrameworkCore.SqlServer 9.x
EF Core 10 -> Microsoft.EntityFrameworkCore.SqlServer 10.x
```

این فایل درباره تغییر TargetFramework تصمیم تازه‌ای نمی‌گیرد.

---

# 2. موارد MySQL که از سند قبلی حذف می‌شوند

موارد زیر دیگر مبنای پیاده‌سازی نیستند:

```text
MySQL 8.4 LTS
InnoDB
MySql.EntityFrameworkCore
Pomelo.EntityFrameworkCore.MySql
BINARY(16) for UUID
DATETIME(6) as MySQL-specific contract
generated active_key workaround
SELECT ... FOR UPDATE
MySQL SKIP LOCKED design
MySQL implicit-commit assumptions
MySQL FULLTEXT as selected search engine
MySQL binary log backup strategy
MySQL-specific SQL appendix
```

هر نمونه SQL در سند قبلی که syntax مخصوص MySQL دارد، فقط historical reference است.

---

# 3. ساختار منطقی SQL Server

برای شروع:

```text
One SQL Server Database
One primary EF Core DbContext
Module-owned entity configurations
Module-owned repositories/ports where needed
```

Database نمونه:

```text
Makan
```

SQL Server برخلاف MySQL از Schema واقعی داخل یک Database پشتیبانی می‌کند؛ بنابراین به جای prefixهای اجباری MySQL از Schema برای مرزبندی ماژول‌ها استفاده شود.

پیشنهاد:

```text
identity
org
guardian
academic
curriculum
assessment
messaging
knowledge
intelligence
copilot
commerce
notification
storage
audit
infra
platform
```

نمونه:

```text
identity.users
identity.credentials
identity.sessions

org.organizations
org.persons
org.memberships
org.invitations

academic.classes
academic.enrollments
academic.sessions

assessment.assignments
assessment.submissions
assessment.exams
assessment.attempts

messaging.conversations
messaging.participants
messaging.messages

infra.outbox
infra.inbox
infra.idempotency
```

این مرزبندی مالکیت ماژول است؛ Schema مجوز کسب‌وکاری ایجاد نمی‌کند.

---

# 4. قرارداد نوع داده

## 4.1 شناسه

پیشنهاد پایه:

```text
uniqueidentifier
```

در C#:

```csharp
Guid
```

برای جدول‌های پرتعداد، نحوه تولید Guid باید با benchmark واقعی بررسی شود.

می‌توان از Guid ترتیبی/Sequential Guid در لایه EF یا default مناسب SQL Server استفاده کرد؛ انتخاب دقیق باید یک ADR داشته باشد.

هیچ `BINARY(16)` converter مخصوص MySQL لازم نیست.

---

## 4.2 زمان

لحظه‌های رسمی:

```text
datetime2(7)
```

و در Application:

```csharp
DateTimeOffset / DateTime UTC according to the concrete model
```

قاعده:

- Event timestampها به UTC نگهداری شوند.
- TimeZoneId آموزشگاه مستقل ذخیره شود.
- برنامه تکرارشونده، زمان محلی + منطقه زمانی را حفظ کند.
- Deadline رسمی از ساعت Client محاسبه نشود.

---

## 4.3 متن

متن فارسی/چندزبانه:

```text
nvarchar(...)
nvarchar(max)
```

Collation باید در زمان ایجاد Database صریح انتخاب و در محیط Test/Production یکسان نگه داشته شود.

Normalization جستجوی فارسی، متن نمایشی اصلی کاربر را overwrite نکند.

---

## 4.4 JSON

برای Payloadهای محدود و versioned:

```text
nvarchar(max)
```

با validation مناسب، و در صورت نیاز Constraint مبتنی بر `ISJSON`.

JSON جای روابط اصلی Domain را نمی‌گیرد.

---

## 4.5 پول

از نوع SQL Server `money` به‌عنوان قرارداد عمومی Domain استفاده نشود.

پیشنهاد:

```text
decimal(p,s)
CurrencyCode
Unit
```

تبدیل ریال/تومان فقط در لایه presentation/commerce policy انجام شود.

---

# 5. Optimistic Concurrency

SQL Server قابلیت native `rowversion` دارد.

برای Entityهایی که تغییر همزمان حساس دارند:

```text
rowversion
```

در EF Core:

```csharp
builder.Property(x => x.RowVersion)
    .IsRowVersion();
```

نمونه Entity:

```csharp
public byte[] RowVersion { get; private set; } = [];
```

`rowversion`:

- timestamp زمانی نیست.
- مقدار کسب‌وکاری نیست.
- با هر update ردیف عوض می‌شود.
- برای detect کردن overwrite همزمان استفاده می‌شود.

برای API، representation مناسب مثل ETag/Base64 می‌تواند روی آن ساخته شود.

مثال‌های حساس:

```text
Grade/Evaluation
Organization settings
Membership
Invitation
Class configuration
Exam definition before lock
Assignment draft/version
```

در conflict، API باید conflict قابل‌فهم برگرداند و تغییر دوم را بی‌صدا روی اول ننویسد.

---

# 6. یکتایی وضعیت فعال

در MySQL سند قبلی از Generated Column برای Active Key استفاده کرده بود.

در SQL Server از **Filtered Unique Index** استفاده شود.

نمونه:

```sql
CREATE UNIQUE INDEX UX_enrollments_active
ON academic.enrollments
(
    organization_id,
    class_id,
    learner_person_id
)
WHERE status = N'active';
```

نمونه‌های مشابه:

```text
Active membership
Active enrollment
Current role binding
Current guardian relation when model requires uniqueness
```

قبل از افزودن Index باید دقیقاً تعریف شود چه statusهایی «active» حساب می‌شوند.

---

# 7. Tenant / Organization Isolation

SQL Server شدن دیتابیس، مدل مجوز را تغییر نمی‌دهد.

هر رکورد سازمانی حساس باید `OrganizationId` معتبر داشته باشد.

فقط داشتن این ستون کافی نیست.

الزام‌ها:

1. Composite relationship constraints در داده
2. Resource authorization در Application
3. Queryها با trusted AccessContext
4. Cache key شامل scope لازم
5. File/Search/Worker/AI scope جدا
6. تست حداقل دو Organization

مثال:

```text
Class A از Organization A
نباید Course/Person/Enrollment متعلق به Organization B را reference کند.
```

برای SQL Server، Row-Level Security می‌تواند بعداً به‌عنوان defense-in-depth بررسی شود، ولی جای Authorization اپلیکیشن را نمی‌گیرد و قبل از استفاده نیازمند Spike، تست connection pooling و runbook است.

---

# 8. Transaction — آخرین ظرفیت کلاس

قانون «آخرین صندلی» باید در Database transaction حل شود؛ check کردن capacity در Client کافی نیست.

ترتیب پیشنهادی:

```text
Begin transaction
-> resolve current authorized organization
-> acquire/update-lock on quota row if seat quota is consumed
-> acquire/update-lock on class capacity row
-> recheck active enrollment
-> recheck reservation if applicable
-> recheck class capacity
-> insert enrollment
-> update counters if used
-> append audit/outbox
-> commit
```

در SQL Server الگوی locking باید با `UPDLOCK` و در صورت نیاز `HOLDLOCK`/سطح isolation مناسب پیاده و با load test اثبات شود.

مثال مفهومی:

```sql
SELECT id, capacity, active_count
FROM academic.classes WITH (UPDLOCK, HOLDLOCK)
WHERE organization_id = @organizationId
  AND id = @classId;
```

قواعد:

- ترتیب گرفتن Lock برای منابع متعدد deterministic باشد.
- transaction کوتاه بماند.
- هیچ SMS، HTTP، file scan یا LLM call داخل transaction انجام نشود.
- Deadlock/timeout به معنی retry کور هر Command نیست.
- عملیات user-facing باید idempotency contract داشته باشد.

---

# 9. Idempotency

برای عملیات‌هایی مانند:

```text
AcceptInvitation
EnrollLearner
SubmitAssignment
FinalizeExamAttempt
SendMessage
VerifyPayment callback
ProvisionOrganization
```

از Operation/Idempotency key استفاده شود.

جدول نمونه:

```text
infra.idempotency
```

حداقل:

```text
operation_id
actor_id
scope
command_type
request_hash
status
response_reference
created_at_utc
expires_at_utc
```

تکرار همان Operation با همان payload همان نتیجه معتبر را برگرداند.

همان OperationId با payload متفاوت conflict است.

---

# 10. Outbox / Inbox

الگوی Transactional Outbox حفظ می‌شود.

در همان transaction کسب‌وکاری:

```text
Domain data
Audit metadata
Outbox message
COMMIT
```

ثبت می‌شوند.

Dispatcher سپس Outbox را claim می‌کند.

در SQL Server می‌توان الگوی کنترل‌شده‌ای با update locks / `READPAST` برای پردازش رقابتی Workerها استفاده کرد، اما query دقیق باید با integration/load test تایید شود.

اصل مهم:

```text
At-least-once delivery
+
Idempotent consumer effect
```

نه ادعای Exactly Once روی شبکه.

Outbox payload نباید کپی کامل پیام خصوصی یا داده حساس غیرلازم باشد.

---

# 11. EF Core Persistence

Provider:

```text
Microsoft.EntityFrameworkCore.SqlServer
```

Configuration نمونه:

```csharp
services.AddDbContext<MakanDbContext>(options =>
{
    options.UseSqlServer(connectionString);
});
```

نسخه دقیق Package باید با EF Core پروژه match باشد.

Connection String واقعی:

- در source code hard-code نشود.
- در Git commit نشود.
- Development از User Secrets یا secret mechanism مناسب استفاده کند.
- Production از secret store محیط استقرار استفاده کند.

Runtime DB principal باید least privilege باشد.

Migration principal جدا از runtime principal باشد.

Runtime account به‌صورت پیش‌فرض `db_owner` نباشد.

---

# 12. Migration

Migrationهای جدید SQL Server با EF Core SQL Server provider تولید شوند.

اصل:

```text
Expand
-> deploy compatible code
-> backfill
-> verify
-> switch reads/writes
-> contract later
```

Migration مخرب در اولین rollout انجام نشود.

Pipeline migration:

```text
separate credential
controlled environment
recorded migration version
backup/restore plan for risky changes
```

تغییر دستی schema در Production ممنوع باشد مگر runbook اضطراری مصوب.

---

# 13. Search

MySQL FULLTEXT دیگر تصمیم معماری نیست.

برای شروع:

1. ابتدا نیازهای واقعی Search و dataset فارسی تعریف شود.
2. normalization ی/ک، نیم‌فاصله، اعداد فارسی/انگلیسی و عبارت‌های ترکیبی تست شوند.
3. SQL Server indexes / Full-Text Search می‌تواند Candidate باشد.
4. اگر معیار کیفیت/بار را پاس نکرد، search engine اختصاصی بعداً اضافه شود.

در همه حالت‌ها:

```text
Search result != authorization
```

قبل از count/highlight/result و قبل از ارسال source به LLM، مجوز جاری دوباره کنترل شود.

---

# 14. Backup / Restore

برای Production، تصمیم پایه:

```text
SQL Server Full Recovery Model
```

تا امکان transaction log backup و point-in-time restore وجود داشته باشد.

Runbook باید شامل این‌ها باشد:

```text
Full backup
Differential backup where useful
Transaction log backups
Backup retention
Encryption/key recovery where applicable
Restore drill on isolated environment
Application-level verification after restore
Object storage consistency check
```

هدف RPO/RTO فقط با restore drill واقعی اثبات می‌شود.

وجود فایل Backup به‌تنهایی اثبات Recovery نیست.

Dev می‌تواند سیاست سبک‌تری داشته باشد، اما Production policy نباید از روی Dev copy شود.

---

# 15. Data Deletion

Delete منبع فقط `DELETE` یک ردیف نیست.

وابستگی‌ها باید بررسی شوند:

```text
Database rows
File/object
Extracted text
Search index
Vector index
Cache
AI derivative
Notification/job references
Audit/official retention exception
Backups according to policy
```

Official educational history، personal message deletion و account closure یک policy واحد ندارند.

---

# 16. SQL Server Sample DDL

> این DDL نمونه معماری است، نه Migration نهایی Production.

```sql
CREATE SCHEMA org;
GO

CREATE SCHEMA academic;
GO

CREATE TABLE org.organizations
(
    id UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT PK_organizations PRIMARY KEY
        CONSTRAINT DF_organizations_id DEFAULT NEWSEQUENTIALID(),

    name NVARCHAR(200) NOT NULL,

    created_at_utc DATETIME2(7) NOT NULL
        CONSTRAINT DF_organizations_created
        DEFAULT SYSUTCDATETIME(),

    row_version ROWVERSION NOT NULL
);
GO

CREATE TABLE org.persons
(
    id UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT PK_persons PRIMARY KEY
        CONSTRAINT DF_persons_id DEFAULT NEWSEQUENTIALID(),

    organization_id UNIQUEIDENTIFIER NOT NULL,
    display_name NVARCHAR(200) NOT NULL,

    created_at_utc DATETIME2(7) NOT NULL
        CONSTRAINT DF_persons_created
        DEFAULT SYSUTCDATETIME(),

    row_version ROWVERSION NOT NULL,

    CONSTRAINT FK_persons_organization
        FOREIGN KEY (organization_id)
        REFERENCES org.organizations(id),

    CONSTRAINT UQ_persons_org_id
        UNIQUE (organization_id, id)
);
GO

CREATE TABLE academic.classes
(
    id UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT PK_classes PRIMARY KEY
        CONSTRAINT DF_classes_id DEFAULT NEWSEQUENTIALID(),

    organization_id UNIQUEIDENTIFIER NOT NULL,
    capacity INT NOT NULL,

    created_at_utc DATETIME2(7) NOT NULL
        CONSTRAINT DF_classes_created
        DEFAULT SYSUTCDATETIME(),

    row_version ROWVERSION NOT NULL,

    CONSTRAINT CK_classes_capacity
        CHECK (capacity > 0),

    CONSTRAINT FK_classes_organization
        FOREIGN KEY (organization_id)
        REFERENCES org.organizations(id),

    CONSTRAINT UQ_classes_org_id
        UNIQUE (organization_id, id)
);
GO

CREATE TABLE academic.enrollments
(
    id UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT PK_enrollments PRIMARY KEY
        CONSTRAINT DF_enrollments_id DEFAULT NEWSEQUENTIALID(),

    organization_id UNIQUEIDENTIFIER NOT NULL,
    class_id UNIQUEIDENTIFIER NOT NULL,
    learner_person_id UNIQUEIDENTIFIER NOT NULL,

    status NVARCHAR(16) NOT NULL,

    accepted_at_utc DATETIME2(7) NOT NULL,

    row_version ROWVERSION NOT NULL,

    CONSTRAINT CK_enrollments_status
        CHECK (status IN (N'active', N'completed', N'withdrawn')),

    CONSTRAINT FK_enrollments_class
        FOREIGN KEY (organization_id, class_id)
        REFERENCES academic.classes(organization_id, id),

    CONSTRAINT FK_enrollments_person
        FOREIGN KEY (organization_id, learner_person_id)
        REFERENCES org.persons(organization_id, id)
);
GO

CREATE UNIQUE INDEX UX_enrollments_active
ON academic.enrollments
(
    organization_id,
    class_id,
    learner_person_id
)
WHERE status = N'active';
GO
```

نکته:

`capacity > 0` فقط مثبت بودن ظرفیت را تضمین می‌کند؛ جلوگیری از فروش/ثبت‌نام بیش از ظرفیت همچنان transaction و locking صحیح می‌خواهد.

---

# 17. Test Matrix مخصوص SQL Server

Integration test باید روی SQL Server واقعی اجرا شود، نه فقط EF InMemory.

حداقل:

### Persistence

```text
Guid roundtrip
Unicode/Persian text
datetime UTC behavior
decimal precision
JSON validation where used
EF migrations
CancellationToken
```

### Concurrency

```text
rowversion conflict
two graders edit same grade
two admins edit same membership
stale If-Match / ETag
```

### Tenant

```text
Organization A cannot reference Organization B resource
cross-org composite FK fails
query authorization denies cross-org read
```

### Capacity

```text
100 seats + many simultaneous enrollment attempts
never create > 100 active enrollments
duplicate request does not consume second seat
```

### Idempotency

```text
duplicate invitation acceptance
duplicate message send
duplicate assignment submit
duplicate payment callback
duplicate provisioning execution
```

### Outbox

```text
worker crash after claim
worker crash after external side effect
retry does not duplicate local business effect
```

### Recovery

```text
restore database to isolated environment
login/account links survive
membership/class/submission/grade relations survive
audit/outbox state is coherent
files referenced by DB can be resolved
```

---

# 18. بخش‌های معماری v3 که این فایل Override می‌کند

به‌صورت مفهومی هر اشاره MySQL-specific در این بخش‌ها جایگزین می‌شود:

```text
Technology replacement table
Database provider selection
Persistence.MySql naming
MySQL tenant section
Data architecture / storage culture
MySQL data types
Manual Version workaround chosen because of MySQL
Generated-column uniqueness workaround
FOR UPDATE / SKIP LOCKED assumptions
MySQL migration caveats
MySQL FULLTEXT choice
MySQL backup/binlog/PITR
MySQL provider spike/test gate
MySQL appendix DDL
MySQL references that only justified those choices
```

ساختار Domain، API، Messaging، AI، React/React Native، UX IDs و business rules با این فایل تغییر نمی‌کنند.

---

# 19. تصمیمی که Codex باید اجرا کند

وقتی Codex معماری اصلی را می‌خواند:

```text
If architecture says MySQL:
    use this SQL Server override instead.
```

Codex حق ندارد برای «هم‌راستا شدن با PDF» MySQL package نصب کند.

قبل از نوشتن Backend باید:

```text
1. inspect actual TargetFramework
2. select matching EF Core SqlServer provider major version
3. confirm local SQL Server development strategy
4. create migrations for SQL Server only
5. run integration tests against SQL Server
```

---

# 20. Official technical references

مرجع‌های اصلی برای تصمیم اصلاحی:

- Microsoft EF Core SQL Server Provider  
  https://learn.microsoft.com/ef/core/providers/sql-server/

- EF Core optimistic concurrency / SQL Server rowversion  
  https://learn.microsoft.com/ef/core/saving/concurrency

- SQL Server filtered indexes  
  https://learn.microsoft.com/sql/relational-databases/indexes/create-filtered-indexes

- SQL Server recovery models  
  https://learn.microsoft.com/sql/relational-databases/backup-restore/recovery-models-sql-server

- SQL Server point-in-time/full recovery restore guidance  
  https://learn.microsoft.com/sql/relational-databases/backup-restore/complete-database-restores-full-recovery-model
