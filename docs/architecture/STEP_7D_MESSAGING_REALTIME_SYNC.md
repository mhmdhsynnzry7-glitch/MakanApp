# STEP 7D — همگام‌سازی پایدار و realtime پیام‌رسانی

این مرحله Delta Sync، اعلان SignalR، وضعیت Delivered/Read و بازیابی چنددستگاهی را به ماژول `Messaging` اضافه می‌کند. SQL Server تنها مرجع حقیقت است؛ SignalR فقط باعث می‌شود کلاینت زودتر از وجود تغییر آگاه شود.

## دو جریان ترتیب مستقل

- `MessageSequence` فقط ترتیب پیام‌های Conversation و paging تاریخچه را مشخص می‌کند.
- `ChangeSequence` ترتیب همه تغییرات قابل همگام‌سازی را مشخص می‌کند؛ ساخت پیام هر دو sequence را جلو می‌برد، اما Edit/Delete/Reaction/Pin/Participant فقط `ChangeSequence` را تغییر می‌دهند.
- sequence بعدی روی ردیف `Conversation` نگه‌داری و زیر lock تراکنشی SQL Server تخصیص داده می‌شود؛ از `MAX + 1` استفاده نمی‌شود.
- `MessagingChangeCursor` یک cursor opaque و versioned است که Conversation و ChangeSequence را در قرارداد HTTP پنهان می‌کند. cursor یک Conversation برای Conversation دیگر پذیرفته نمی‌شود.

## atomicity و Outbox

mutation تجاری، `MessagingChangeEvent` و `MessagingRealtimeOutboxMessage` در همان transaction ثبت می‌شوند. پس از commit، `MessagingOutboxWakeSignal` dispatcher را بیدار می‌کند. `MessagingRealtimeOutboxProcessor` ردیف‌های آماده را با `UPDLOCK`، `READPAST` و lease کوتاه claim می‌کند و تحویل ناموفق را با backoff محدود دوباره امتحان می‌کند.

ارسال SignalR قبل از commit انجام نمی‌شود. شکست notifier داده commit‌شده را rollback نمی‌کند و کلاینت می‌تواند همان تغییر را از Delta دریافت کند. تحویل Outbox از نوع at-least-once است؛ notification باید در کلاینت به‌عنوان invalidation idempotent در نظر گرفته شود.

## ChangeEvent و tombstone

جدول `messaging.ChangeEvents` فقط metadata لازم را نگه می‌دارد: شناسه Conversation، `ChangeSequence`، نوع تغییر، شناسه/نسخه resource، زمان UTC، actor اختیاری، audience اختیاری و نسخه payload. متن کامل پیام خصوصی در ChangeEvent یا Outbox کپی نمی‌شود.

حذف پیام یک `MessageDeleted` پایدار با `ResourceId = MessageId` می‌سازد تا دستگاه دیگر حذف را استنتاج نکند. در STEP 7D هیچ job پاک‌سازی برای ChangeEvent یا tombstone تعریف نشده است؛ رکوردها فعلاً حذف نمی‌شوند تا policy رسمی retention تصویب شود. این وضعیت تعهد به نگه‌داری بی‌نهایت نیست و پیش از افزودن cleanup باید حداکثر دوره آفلاین دستگاه‌ها و قرارداد resync کامل تعیین شود.

## Delta و authorization

`GET /api/v1/conversations/{conversationId}/changes?afterCursor=...&limit=...` تغییرات جدیدتر را همراه `nextCursor` و `hasMore` برمی‌گرداند. هر درخواست session، workspace، scope و participant فعال را دوباره بررسی می‌کند؛ داشتن cursor قدیمی مجوز نیست. participant حذف‌شده نمی‌تواند delta آینده را بخواند.

Changeهای عمومی Conversation برای همه participantهای مجاز قابل دریافت‌اند. `ReadCursorAdvanced` و `DeliveryCursorAdvanced` با `AudienceUserId` فقط برای sessionهای همان User قابل مشاهده‌اند تا وضعیت چند دستگاه او همگرا شود و analytics جزئی سایر participantها افشا نشود.

## Sent، Delivered و Read

- `Sent`: Message در SQL Server commit شده است.
- `Delivered`: کلاینت با `POST /api/v1/conversations/{conversationId}/delivered` دریافت تا `UpToMessageSequence` را صریحاً ACK کرده است. موفقیت SignalR یا Push به‌تنهایی Delivered نیست.
- `Read`: کلاینت با `POST /api/v1/conversations/{conversationId}/read` خواندن تا sequence مشخص را ثبت کرده است.

`LastDeliveredMessageSequence` و `LastReadMessageSequence` روی `ConversationParticipant` ذخیره می‌شوند. هر دو monotonic هستند، Read در صورت نیاز Delivered را نیز جلو می‌برد و retry عقب‌تر no-op است. مدل فعلی participant-level است: بیشترین cursor همه دستگاه‌های User را نشان می‌دهد و وضعیت مستقل هر device را نگه نمی‌دارد.

`UnreadCount` ذخیره و دستی increment نمی‌شود؛ از پیام‌های حذف‌نشده دیگران با sequence بزرگ‌تر از read cursor محاسبه می‌شود. پیام خود User و tombstone حذف‌شده در شمارش قرار نمی‌گیرند.

## SignalR و reconnect

Hub احراز هویت‌شده در `/hubs/messaging` فقط `SubscribeConversation` و `UnsubscribeConversation` دارد و business mutationها از HTTP انجام می‌شوند. Hub قبل از subscription از `IMessagingService` برای کنترل دسترسی جاری استفاده می‌کند و EF Core را مستقیم صدا نمی‌زند.

هر connection به group سمت سرور session خودش افزوده می‌شود. dispatcher پیش از ارسال، participantها و sessionهای فعال را دوباره از SQL Server resolve می‌کند و اعلان را فقط به session groupهای مجاز می‌فرستد؛ conversation group قدیمی مدرک authorization نیست. session revoked در resolve حذف می‌شود و عملیات حساس Hub نیز access جاری را دوباره بررسی می‌کند.

جریان reconnect مورد انتظار:

```text
Reconnect + authenticate current session
-> subscribe to authorized conversation
-> GET Delta from last durable cursor
-> apply missed changes
-> continue lightweight realtime invalidations
```

## دیتابیس

Migration `AddMessagingRealtimeAndSync` این تغییرها را اعمال می‌کند:

- `messaging.Conversations.NextChangeSequence` با backfill مقدار ۱ و constraint مثبت؛ default دائمی SQL ندارد.
- cursorهای Delivered/Read، زمان به‌روزرسانی و `rowversion` موجود روی `messaging.ConversationParticipants` با constraintهای عدم منفی بودن و `Read <= Delivered`.
- جدول `messaging.ChangeEvents` با FKهای restrict، unique index روی `(ConversationId, ChangeSequence)` و index خواندن audience.
- جدول `messaging.RealtimeOutbox` با FK restrict و unique index روی `ChangeEventId`، index فیلترشده pending، lease/retry fields و `rowversion`.

Migration در startup یا روی Production خودکار اجرا نمی‌شود.

## محدودیت‌های آگاهانه

- SignalR فعلی برای یک instance از API است. deployment چند instance قبل از rollout به backplane/scale-out مصوب و آزمون‌شده نیاز دارد؛ Redis در این مرحله اضافه نشده است.
- cleanup/archival برای ChangeEvent، tombstone و Outbox dispatch‌شده تا تصویب retention policy وجود ندارد.
- Delivered به تفکیک participant است، نه device/session؛ جزئیات telemetry هر دستگاه نگه‌داری نمی‌شود.
- Push provider، Search، Block، Report، Moderation و AI/Copilot خارج از STEP 7D هستند.
- durable offline mutation queue مسئولیت client آینده است؛ backend فقط retry امن `ClientMessageId` و re-authorization زمان اجرای mutation را فراهم می‌کند.
