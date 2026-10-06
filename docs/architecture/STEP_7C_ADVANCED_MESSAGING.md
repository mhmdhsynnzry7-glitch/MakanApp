# STEP 7C — قابلیت‌های پیشرفته پیام‌رسان

این مرحله قابلیت‌های `Reply`، `Forward`، ویرایش و حذف منطقی پیام، واکنش، منشن، سنجاق و پیوست‌های مبتنی بر `FileAsset` را روی زیرساخت مراحل 7A و 7B اضافه می‌کند. معماری همچنان Onion Architecture داخل Modular Monolith است و دیتابیس رسمی SQL Server باقی می‌ماند.

## مدل محتوای جاری و تاریخچه

`Message` محل خواندن سریع وضعیت جاری است و `Text` فعلی، `CurrentRevisionNumber`، `EditedAtUtc`، وضعیت حذف و `rowversion` را نگه می‌دارد. برای هر پیام در زمان ارسال revision شماره 1 ساخته می‌شود و هر ویرایش پذیرفته‌شده یک `MessageRevision` تازه می‌سازد. بنابراین نمایش گفتگو نیازمند replay تاریخچه نیست، ولی متن قبلی نیز با update مخرب از بین نمی‌رود.

تاریخچه revision endpoint عمومی ندارد و در DTO معمولی serialize نمی‌شود. این جداسازی مانع افشای متن قدیمی به شرکت‌کننده عادی می‌شود. `rowversion` با `ExpectedVersion` از overwrite خاموش میان دو دستگاه جلوگیری می‌کند؛ ویرایش یا حذف stale با `MESSAGE_EDIT_CONFLICT` و HTTP 412 پاسخ می‌گیرد.

## حذف و tombstone

حذف STEP 7C فقط «حذف برای همه توسط فرستنده اصلی» است. «حذف برای من» به state محلی Client یا تصمیم سیاستی آینده واگذار شده است. حذف، ردیف `Message`، `Sequence` و شناسه‌ها را نگه می‌دارد، `Text` جاری را پاک می‌کند و `DeletedAtUtc` و `DeletedByUserId` را ثبت می‌کند. تاریخچه عادی برای پیام حذف‌شده فقط tombstone امن برمی‌گرداند و متن، نوع محتوا، منشن، واکنش و metadata پیوست را نمایش نمی‌دهد.

## Reply و Forward

`ReplyToMessageId` با FK ترکیبی به پیام همان Conversation محدود است. متن quoted به‌صورت authoritative کپی نمی‌شود؛ در صورت حذف پیام مرجع، DTO فقط placeholder حذف‌شده می‌دهد.

`ForwardMessage` یک پیام تازه با `MessageId`، `ClientMessageId` و `Sequence` تازه می‌سازد. سرویس، محتوای مجاز را از پیام منبع می‌خواند و Client نمی‌تواند payload جعلی forwarded بسازد. در نبود مجوز export صریح، Forward فقط میان Conversationهای دارای Scope و `OrganizationId` یکسان مجاز است. API فقط marker امن `IsForwarded` را نشان می‌دهد و هویت یا metadata محدوده منبع را افشا نمی‌کند.

## واکنش، منشن و سنجاق

- واکنش‌ها به مجموعه محدود `Like`، `Love`، `Laugh`، `Wow` و `Sad` محدودند. برای هر User روی هر Message فقط یک واکنش فعال وجود دارد؛ تغییر و حذف با lifecycle منطقی انجام می‌شود تا STEP 7D بتواند تغییر را همگام کند.
- منشن از متن `@` مجوز استخراج نمی‌کند. `MessageMention` فقط برای User دارای Participant فعال همان Conversation ساخته می‌شود و هیچ دسترسی تازه‌ای اعطا نمی‌کند.
- در Direct هر Participant فعال می‌تواند سنجاق را مدیریت کند. در Group/Channel کاربرساخته فقط Owner/Admin مجاز است. endpoint عمومی روی `SystemManagedAcademic` اختیار مدیریت ایجاد نمی‌کند.
- چند پیام می‌توانند هم‌زمان سنجاق باشند؛ برای هر زوج Conversation/Message فقط یک سنجاق فعال مجاز است. Unpin ردیف را حذف فیزیکی نمی‌کند.

## FileAsset و رسانه

نوع‌های `Text`، `Image`، `Video`، `Voice` و `File` همگی در مدل واحد `Message` نگه‌داری می‌شوند. `Voice` یک فایل صوتی ذخیره‌شده است و تماس زنده نیست. بایت فایل در Messaging ذخیره نمی‌شود؛ `MessageAttachment` فقط به `storage.FileAssets` وصل می‌شود.

پیش از binding، وضعیت `Ready`، uploader، Scope و سازگاری MIME معتبر Storage با `MessageKind` بررسی می‌شود. فایل bound با `FileAsset.Retain` از cleanup عادی محافظت می‌شود. خواندن metadata یا دانلود توسط دریافت‌کننده علاوه بر آماده‌بودن FileAsset، Participant فعال Conversation و Membership جاری همان Scope را دوباره کنترل می‌کند؛ دانستن `FileAssetId` یا عضویت صرف در همان سازمان کافی نیست. مسیر فیزیکی Storage در DTO قرار نمی‌گیرد.

## Endpointها

| Method | Route | کاربرد |
|---|---|---|
| `POST` | `/api/v1/conversations/{conversationId}/messages` | ارسال Text/Media، Reply و Mention |
| `PATCH` | `/api/v1/conversations/{conversationId}/messages/{messageId}` | ویرایش متن با `ExpectedVersion` |
| `DELETE` | `/api/v1/conversations/{conversationId}/messages/{messageId}` | حذف برای همه با `expectedVersion` |
| `POST` | `/api/v1/conversations/{conversationId}/messages/{messageId}/forward` | Forward سرورمحور به Conversation مقصد |
| `POST` | `/api/v1/conversations/{conversationId}/messages/{messageId}/reactions` | افزودن یا تغییر واکنش خود کاربر |
| `DELETE` | `/api/v1/conversations/{conversationId}/messages/{messageId}/reactions/{reaction}` | حذف واکنش خود کاربر |
| `POST` | `/api/v1/conversations/{conversationId}/pins/{messageId}` | سنجاق پیام |
| `DELETE` | `/api/v1/conversations/{conversationId}/pins/{messageId}` | برداشتن سنجاق |
| `GET` | `/api/v1/conversations/{conversationId}/media` | فهرست مجاز Image/Video/Voice/File |

## SQL Server

Migration `AddAdvancedMessagingFeatures` جدول‌های زیر را در schema `messaging` ایجاد می‌کند:

- `MessageRevisions`
- `MessageAttachments`
- `MessageReactions`
- `MessageMentions`
- `ConversationPins`

رکوردهای پیام موجود با `CurrentRevisionNumber = 1` و revision اولیه backfill می‌شوند و سپس ستون revision اجباری می‌شود. unique indexها از revision تکراری، اتصال تکراری فایل، منشن تکراری، واکنش فعال تکراری و سنجاق فعال تکراری جلوگیری می‌کنند. FK ترکیبی Reply و Pin تضمین می‌کند پیام مرجع متعلق به همان Conversation باشد. پیام، واکنش و سنجاق mutable دارای `rowversion` هستند و mutationها در transaction کوتاه `Serializable` با `UPDLOCK/HOLDLOCK` انجام می‌شوند.

## خارج از محدوده

SignalR، Delta Sync، `ChangeEvent/ChangeCursor`، Delivered/Read cursor، صف mutation آفلاین، جست‌وجو، Block، Report، Moderation و AI/Copilot در این مرحله پیاده‌سازی نشده‌اند. lifecycleهای revision، reaction، pin و tombstone طوری حفظ شده‌اند که STEP 7D بتواند روی آن‌ها همگام‌سازی پایدار بسازد.
