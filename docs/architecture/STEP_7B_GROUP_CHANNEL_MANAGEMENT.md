# STEP 7B — گروه، کانال و مدیریت عضویت

این مرحله قابلیت‌های `Group` و `Channel` را روی زیرساخت پایدار Messaging مرحله 7A اضافه می‌کند. پیاده‌سازی در همان ماژول `Messaging` از Modular Monolith و مطابق جهت وابستگی Onion Architecture انجام شده است.

## مدل گفتگو و سیاست مدیریت

- `ConversationType` شامل `Direct`، `Group` و `Channel` است.
- گفتگوی Group/Channel دارای عنوان اجباری، توضیح اختیاری، scope تغییرناپذیر و policy مدیریت است.
- `UserManaged` فقط از endpointهای عمومی این مرحله ساخته می‌شود.
- `SystemManagedAcademic` در مدل و دیتابیس پشتیبانی می‌شود، اما ساخت و همگام‌سازی خودکار آن به مرحله academic provisioning آینده موکول شده است.
- endpointهای عمومی مدیریت یا انتشار، گفتگوی `SystemManagedAcademic` را تغییر نمی‌دهند.
- ساخت UserManaged با `(CreatedByUserId, ClientOperationId)` idempotent است. retry با payload یکسان همان گفتگو را برمی‌گرداند و payload متفاوت با `CONVERSATION_CREATION_CONFLICT` رد می‌شود.

## نقش‌ها و lifecycle عضویت

هر `ConversationParticipant` یکی از نقش‌های زیر را دارد:

- `Owner`: مالک یکتای فعال گفتگو
- `Admin`: مدیر قابل‌انتصاب فقط توسط Owner
- `Member`: عضو عادی

وضعیت عضویت یکی از `Active`، `Left` یا `Removed` است. پایان عضویت رکورد تاریخی را حذف یا دوباره فعال نمی‌کند. ورود مجدد یک رکورد lifecycle تازه ایجاد می‌کند و filtered unique index تضمین می‌کند برای هر User در هر Conversation فقط یک عضویت فعال وجود داشته باشد.

سیاست مجوزها:

- Owner می‌تواند Member/Admin را حذف کند، نقش Admin/Member را تغییر دهد، مالکیت را منتقل کند و گفتگو را archive کند.
- Admin می‌تواند Member عادی را اضافه یا حذف کند، اما Owner یا Admin دیگر را تغییر نمی‌دهد.
- Owner تا پیش از انتقال پذیرفته‌شده مالکیت نمی‌تواند از گفتگو خارج شود.
- عضو `Left` یا `Removed` بلافاصله امکان details، history و send را از دست می‌دهد.

## انتقال مالکیت

`ConversationOwnershipTransfer` درخواست انتقال را به participant مبدا و مقصد همان Conversation متصل می‌کند. شروع انتقال نقش‌ها را تغییر نمی‌دهد. فقط مقصد فعال می‌تواند درخواست را قبول یا رد کند.

در پذیرش، Owner قبلی در همان transaction به Admin تبدیل می‌شود، مقصد Owner می‌شود و درخواست به `Accepted` می‌رود. retry پذیرش همان نتیجه قبلی را برمی‌گرداند. filtered unique index فقط یک Owner فعال و فقط یک transfer در انتظار را مجاز می‌کند. FKهای composite نیز سازگاری Participant، User و Conversation را در SQL Server enforce می‌کنند.

## ارسال و خواندن

- همه نقش‌های فعال در Group می‌توانند پیام متنی بفرستند.
- در Channel فقط Owner و Admin حق انتشار دارند؛ Member فقط می‌خواند.
- Conversation بایگانی‌شده پیام جدید نمی‌پذیرد، ولی history اعضای فعال باقی می‌ماند.
- عضویت سازمانی فعال و workspace همان Organization برای عملیات سازمانی دوباره سمت سرور بررسی می‌شود.
- `Message.SenderParticipantId` پیام را به lifecycle دقیق فرستنده متصل می‌کند؛ بنابراین rejoin سابقه پیام‌های lifecycle قبلی را بازنویسی نمی‌کند.

## دیتابیس

Migration `AddGroupAndChannelMessaging` موارد زیر را اضافه یا تغییر می‌دهد:

- metadata و policy مدیریت در `messaging.Conversations`
- Role، lifecycle و عامل پایان عضویت در `messaging.ConversationParticipants`
- `SenderParticipantId` در `messaging.Messages`
- جدول `messaging.ConversationOwnershipTransfers`
- filtered unique index برای creation idempotency، active membership، active owner و pending transfer
- check constraintهای type، policy، managed shape، role و lifecycle

برای ارتقای داده‌های مرحله 7A، ستون‌های الزامی تازه ابتدا nullable اضافه می‌شوند. participant پیام‌های موجود، Role اعضای Direct و policy گفتگوهای Direct backfill می‌شوند و سپس ستون‌ها `NOT NULL` می‌شوند. migration در startup یا روی Production خودکار اجرا نمی‌شود.

## API

| متد | مسیر | کاربرد |
|---|---|---|
| `POST` | `/api/v1/conversations/groups` | ساخت idempotent گروه UserManaged |
| `POST` | `/api/v1/conversations/channels` | ساخت idempotent کانال UserManaged |
| `GET` | `/api/v1/conversations/{conversationId}` | جزئیات و اعضای فعال |
| `POST` | `/api/v1/conversations/{conversationId}/members` | افزودن Member |
| `DELETE` | `/api/v1/conversations/{conversationId}/members/{userId}` | حذف عضو مطابق ماتریس مجوز |
| `POST` | `/api/v1/conversations/{conversationId}/leave` | خروج عضو غیرمالک |
| `PATCH` | `/api/v1/conversations/{conversationId}/members/{userId}/role` | تغییر Admin/Member توسط Owner |
| `POST` | `/api/v1/conversations/{conversationId}/ownership-transfers` | شروع انتقال مالکیت |
| `POST` | `/api/v1/conversations/{conversationId}/ownership-transfers/{transferId}/accept` | پذیرش توسط مقصد |
| `POST` | `/api/v1/conversations/{conversationId}/ownership-transfers/{transferId}/decline` | رد توسط مقصد |
| `POST` | `/api/v1/conversations/{conversationId}/archive` | بایگانی توسط Owner |

endpointهای list، history و send مرحله 7A اکنون Direct، Group و Channel را با policy مربوط پشتیبانی می‌کنند.

## موارد خارج از محدوده

- provisioning و sync خودکار `SystemManagedAcademic`
- دسترسی تاریخی پس از `Left` یا `Removed`
- Reply، Edit/Delete، Reaction، Mention، Search، Pin و Moderation
- attachment پیام و SignalR/realtime delivery
- read receipt، delivered/seen و push notification
- AI و Copilot
