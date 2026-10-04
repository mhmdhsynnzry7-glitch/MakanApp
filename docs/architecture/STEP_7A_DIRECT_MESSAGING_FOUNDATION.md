# STEP 7A — زیرساخت گفت‌وگوی مستقیم

این سند تصمیم‌های اجرایی مرحله 7A را ثبت می‌کند. پیاده‌سازی در ماژول `Messaging` از Modular Monolith و مطابق Onion Architecture قرار دارد.

## محدوده

این مرحله فقط گفت‌وگوی مستقیم دو کاربر، پیام متنی پایدار، فهرست گفت‌وگوها، تاریخچه مبتنی بر Sequence و ارسال retry-safe را فراهم می‌کند. Group، Channel، Attachment، Reply، Edit/Delete، Reaction، SignalR، جست‌وجو، Block/Report، Moderation و AI خارج از این مرحله هستند.

## هویت و Scope

- Participant همیشه `identity.User` احراز هویت‌شده است؛ `OrganizationPerson` جایگزین حساب پیام‌رسانی نیست.
- `ConversationScope.Personal` هیچ `OrganizationId` ندارد.
- `ConversationScope.Organization`، `OrganizationId` معتبر و تغییرناپذیر خود را نگه می‌دارد.
- سازمان از `AccessContext` معتبر نشست تعیین می‌شود و Client نمی‌تواند با ارسال `OrganizationId` مجوز ایجاد کند.
- یک زوج کاربر می‌تواند یک گفت‌وگوی Personal و برای هر سازمان مجاز یک گفت‌وگوی Organization جدا داشته باشد.

## سیاست صلاحیت ارتباط

شناختن `UserId` مجوز تماس نیست. سیاست مرحله 7A به این صورت است:

### Personal

- هر دو حساب باید پروفایل کامل و `CommunicationAgeCategory.Adult` داشته باشند.
- باید یک `PersonalCommunicationGrant` فعال برای زوج canonical وجود داشته باشد.
- endpoint عمومی برای ساخت Grant در این مرحله وجود ندارد؛ Grant یک رکورد سیاستی server-managed است.
- لغو Grant ارسال جدید را متوقف می‌کند، اما Participant همچنان تاریخچه شخصی خودش را می‌خواند.

### Organization

- هر دو User باید Membership فعال در همان سازمان داشته باشند.
- نقش Actor از `AccessContext` فعال و دوباره اعتبارسنجی می‌شود.
- Manager می‌تواند با Manager، Teacher و Parent مرتبط شود؛ تماس با Student به Enrollment فعال Student نیاز دارد.
- Teacher می‌تواند با Manager و Teacher مرتبط شود؛ تماس Teacher/Student به Class مشترک فعال و تماس Teacher/Parent به GuardianRelation و Class مشترک فعال نیاز دارد.
- Student فقط با Manager سازمان یا Teacher کلاس فعال خودش ارتباط می‌گیرد.
- Parent فقط با Manager یا Teacher مرتبط با فرزند دارای رابطه فعال ارتباط می‌گیرد.
- Student/Student، Parent/Parent و Parent/Student در این مرحله مجاز نیستند.
- از بین رفتن رابطه آموزشی ارسال را متوقف می‌کند. تا وقتی Membership سازمانی User فعال است تاریخچه خودش قابل خواندن است؛ پایان Membership خواندن و ارسال را مسدود می‌کند.

خطای شروع تماس نامجاز عمداً با کد عمومی `DIRECT_RECIPIENT_NOT_AVAILABLE` برگردانده می‌شود تا وجود یا وضعیت حساب محافظت‌شده افشا نشود. شماره تلفن در قرارداد Messaging وجود ندارد.

## یکتایی گفت‌وگوی مستقیم

`DirectUserPair` دو `Guid` را با نمایش `N` و مقایسه ordinal مرتب می‌کند و در `Conversation` به‌صورت `DirectUserLowId` و `DirectUserHighId` ذخیره می‌شود. filtered unique index روی `(Scope, OrganizationId, DirectUserLowId, DirectUserHighId)` برای `Type = Direct` از تکرار جلوگیری می‌کند. ساخت داخل transaction با isolation سطح `Serializable` و lockهای `UPDLOCK, HOLDLOCK` انجام می‌شود؛ Conversation و دو Participant در یک transaction ثبت می‌شوند.

## ترتیب و idempotency پیام

- `Conversation.NextMessageSequence` شمارنده server-authoritative است.
- هنگام ارسال، Conversation در transaction `Serializable` با `UPDLOCK, HOLDLOCK` قفل و Sequence بعدی از همان رکورد تخصیص داده می‌شود.
- زمان `SentAtUtc` از `TimeProvider` سمت سرور می‌آید.
- unique index روی `(ConversationId, Sequence)` از Sequence تکراری جلوگیری می‌کند.
- unique index روی `(ConversationId, SenderUserId, ClientMessageId)` retry را idempotent می‌کند.
- retry با همان شناسه و همان متن، همان Message receipt را برمی‌گرداند؛ استفاده مجدد از شناسه با متن متفاوت با `MESSAGE_IDEMPOTENCY_CONFLICT` رد می‌شود.
- `Sent` فقط یعنی Message در SQL Server commit شده است و به معنی Delivered یا Seen نیست.

## دیتابیس

Migration `AddDirectMessagingFoundation` schema با نام `messaging` و جدول‌های زیر را ایجاد می‌کند:

- `messaging.Conversations`
- `messaging.ConversationParticipants`
- `messaging.Messages`
- `messaging.PersonalCommunicationGrants`

FKهای تاریخچه‌دار از delete cascade استفاده نمی‌کنند. فرستنده Message علاوه بر FK به User، با FK ترکیبی `(ConversationId, SenderUserId)` باید Participant همان Conversation باشد. `Conversation`، `ConversationParticipant` و `PersonalCommunicationGrant` برای تغییرات lifecycle یا شمارنده دارای `rowversion` هستند. متن Message حداکثر `nvarchar(4000)` است و مقدار قابل استفاده از تنظیم `Messaging:MaximumTextLength` می‌آید.

Migration تولید شده است، اما توسط برنامه هنگام startup یا روی Production اجرا نمی‌شود.

## API

| متد | مسیر | کاربرد |
|---|---|---|
| `POST` | `/api/v1/conversations/direct` | ساخت یا resolve گفت‌وگوی مستقیم canonical |
| `GET` | `/api/v1/conversations` | فهرست گفت‌وگوهای مجاز User |
| `GET` | `/api/v1/conversations/{conversationId}/messages` | تاریخچه با `beforeSequence` و `limit` |
| `POST` | `/api/v1/conversations/{conversationId}/messages` | ثبت پایدار پیام متن با `ClientMessageId` |

همه endpointها به نشست معتبر نیاز دارند. Controller فقط ورودی HTTP را به `IMessagingService` می‌دهد و هیچ query مربوط به EF Core ندارد.

## نکات حریم خصوصی و محدودیت فعلی

- قراردادهای امن مخاطب فقط `UserId`، `Username` و `DisplayName` را برمی‌گردانند.
- متن پیام در logging اضافه‌شده این مرحله ثبت نمی‌شود.
- Message به `FileAsset` متصل نشده است.
- دیتابیس منبع حقیقت است و realtime delivery هنوز وجود ندارد.
- Block/Report و policy مدیریت آن‌ها باید در مرحله بعدی مرتبط، پیش از گسترش ارتباطات عمومی طراحی شوند.
